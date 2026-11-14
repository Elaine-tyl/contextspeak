using System;
using System.Linq;
using ContextSpeak.Slice;

namespace ContextSpeak.Learning
{
    /// Learning policy owns evidence, never orders, quest rewards, wallet or scene transitions.
    public sealed class IELTSPracticeManager
    {
        public LearnerMemory Memory { get; }
        public PracticeScenario Scenario { get; private set; }
        public GameMode Mode { get; private set; }
        public LearningStage Stage { get; private set; }
        public AssessmentRecord Last { get; private set; }
        private string target = "";
        private readonly int[] intervals;
        public string VisibleCorrection => Stage == LearningStage.Feedback || Stage == LearningStage.Repetition ? target : "";
        public string Reference => target;

        public IELTSPracticeManager(LearnerMemory memory, int[] reviewDays = null)
        {
            Memory = LearningMemoryService.Sanitize(memory);
            intervals = reviewDays;
        }

        public void Begin(PracticeScenario scenario, GameMode mode, DateTime utc)
        {
            Scenario = scenario;
            Mode = mode;
            Stage = LearningStage.IndependentAnswer;
            Last = null;
            target = "";
            var pending = Memory.Pending.FirstOrDefault(session => session.Scenario == scenario.Id);
            if (mode == GameMode.GuidedLearning && pending != null)
            {
                Stage = pending.Stage;
                Scenario = pending.Content ?? scenario;
                target = pending.Correction;
                Scenario.Prompt = pending.Prompt;
            }
            var item = LearningMemoryService.Item(Memory, scenario);
            if (SpacedReviewScheduler.Due(item, utc) && item.State != KnowledgeState.NEEDS_REVIEW) item.State = KnowledgeState.REVIEW_DUE;
        }

        public void ShowRepetition()
        {
            if (Stage == LearningStage.Feedback) { Stage = LearningStage.Repetition; SavePending(); }
        }

        private void SavePending()
        {
            Memory.Pending.RemoveAll(session => session.Scenario == Scenario.Id);
            Memory.Pending.Add(new PendingLearningSession { Scenario = Scenario.Id, Prompt = Scenario.Prompt, Correction = target, Stage = Stage, Content = Scenario });
        }

        public PracticeOutcome Submit(UtteranceEvidence evidence, DateTime utc)
        {
            if (Scenario == null || Stage == LearningStage.Complete || Stage == LearningStage.Feedback)
                return new PracticeOutcome { Status = "Use the practice action before recording again." };
            Last = SpeakingAssessmentService.Assess(evidence, (IeltsBand)Scenario.Band);
            if (!Last.Assessed) return new PracticeOutcome { Status = Last.Feedback, Assessment = Last };
            var independent = Stage == LearningStage.IndependentAnswer || Stage == LearningStage.Recall;
            var success = independent ? IndependentRecallManager.Passed(evidence, Last) : RepeatVerificationManager.Passed(evidence, Last);
            LearningMemoryService.Observe(Memory, Scenario, Stage, Last, success, independent, utc, intervals);
            if (Mode == GameMode.FreeLife)
            {
                Stage = LearningStage.Complete;
                Memory.Pending.RemoveAll(session => session.Scenario == Scenario.Id);
                return new PracticeOutcome { Advance = true, Status = "Conversation continues. " + (Last.Correction == "" ? "" : "Optional: " + Last.Correction), Assessment = Last };
            }
            if (success && Stage == LearningStage.Repetition)
            {
                Stage = LearningStage.Recall;
                SavePending();
                return new PracticeOutcome { Status = "Now answer again in your own words. The correction is hidden; this checks independent recall.", Assessment = Last };
            }
            if (success)
            {
                Stage = LearningStage.Complete;
                Memory.Pending.RemoveAll(session => session.Scenario == Scenario.Id);
                return new PracticeOutcome { Advance = true, Status = "Independent answer recorded. A different NPC will revisit this after a delay.", Assessment = Last };
            }
            if (Stage != LearningStage.Repetition)
            {
                target = Last.TaskMet && Last.Correction != "" ? Last.Correction : Scenario.Model;
                Stage = LearningStage.Feedback;
            }
            SavePending();
            return new PracticeOutcome { Status = CorrectiveFeedbackManager.Focus(Last), Assessment = Last };
        }
    }
}
