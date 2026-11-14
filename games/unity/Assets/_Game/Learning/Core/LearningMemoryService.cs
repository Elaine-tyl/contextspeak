using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Learning
{
    public static class LearningMemoryService
    {
        public static LearnerMemory Sanitize(LearnerMemory memory)
        {
            memory = memory ?? new LearnerMemory();
            if (!Enum.IsDefined(typeof(GameMode), memory.Mode)) memory.Mode = GameMode.GuidedLearning;
            memory.Pending = (memory.Pending ?? new List<PendingLearningSession>()).Where(session => session != null).ToList();
            memory.Items = (memory.Items ?? new List<LearningItem>()).Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id)).GroupBy(item => item.Id).Select(group => group.First()).ToList();
            foreach (var item in memory.Items)
            {
                item.Contexts = item.Contexts ?? new List<string>();
                if (item.DueUtc < 0 || item.DueUtc > DateTime.MaxValue.Ticks) item.DueUtc = 0;
                if (item.LastSuccessUtc < 0 || item.LastSuccessUtc > DateTime.MaxValue.Ticks) item.LastSuccessUtc = 0;
                if (!Enum.IsDefined(typeof(KnowledgeState), item.State)) item.State = KnowledgeState.NEW;
                item.IntervalIndex = Math.Max(0, Math.Min(3, item.IntervalIndex));
            }
            memory.History = (memory.History ?? new List<LearningEvidence>()).Where(entry => entry != null).TakeLast(500).ToList();
            memory.Exams = (memory.Exams ?? new List<ExamRecord>()).Where(entry => entry != null).TakeLast(20).ToList();
            return memory;
        }

        public static LearningItem Item(LearnerMemory memory, PracticeScenario scenario)
        {
            var item = memory.Items.FirstOrDefault(entry => entry.Id == scenario.ItemId);
            if (item != null) return item;
            item = new LearningItem { Id = scenario.ItemId, Expression = scenario.Model, State = KnowledgeState.NEW };
            memory.Items.Add(item);
            return item;
        }

        public static LearningItem ObserveObject(LearnerMemory memory, string objectName, string word)
        {
            var existing = memory.Items.FirstOrDefault(item => item.Id == "vocab:" + word);
            if (existing != null) return existing;
            var item = new LearningItem { Id = "vocab:" + word, Expression = word, LastScenario = objectName, State = KnowledgeState.NEW };
            memory.Items.Add(item);
            return item;
        }

        public static void Observe(LearnerMemory memory, PracticeScenario scenario, LearningStage stage,
            AssessmentRecord assessment, bool success, bool independent, DateTime utc, int[] intervals = null)
        {
            if (!assessment.Assessed) return;
            var item = Item(memory, scenario);
            if (independent && success) SpacedReviewScheduler.Context(item, scenario.Id, utc);
            item.LastScenario = scenario.Id;
            if (stage == LearningStage.Repetition) item.State = KnowledgeState.PRACTISING;
            SpacedReviewScheduler.Record(item, success, independent, utc, intervals);
            foreach (var word in scenario.Vocabulary ?? Array.Empty<string>())
            {
                if (!Regex.IsMatch(assessment.Text ?? "", @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase)) continue;
                var vocabulary = ObserveObject(memory, scenario.Id, word);
                if (independent && success) SpacedReviewScheduler.Context(vocabulary, scenario.Id, utc);
                vocabulary.LastScenario = scenario.Id;
                SpacedReviewScheduler.Record(vocabulary, success, independent, utc, intervals);
            }
            memory.LastLocation = scenario.Id;
            memory.History.Add(new LearningEvidence {
                Id = Guid.NewGuid().ToString("N"), ItemId = item.Id, Scenario = scenario.Id, Stage = stage, Utc = utc.Ticks,
                Text = assessment.Text, Correction = assessment.Correction, Error = assessment.GrammarError,
                Feedback = assessment.Feedback, AcousticObservation = assessment.PronunciationNote,
                ImprovedExpression = assessment.ImprovedExpression,
                LanguageScoresAssessed = assessment.GrammarScore.HasValue,
                CoherenceScore = (int)(assessment.CoherenceScore ?? 0), LexicalScore = (int)(assessment.LexicalScore ?? 0), GrammarScore = (int)(assessment.GrammarScore ?? 0),
                Success = success, Independent = independent,
            });
            if (memory.History.Count > 500) memory.History.RemoveAt(0);
        }
    }
}
