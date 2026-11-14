using System;
using System.Collections.Generic;

namespace ContextSpeak.Learning
{
    public enum GameMode { FreeLife, GuidedLearning, MockExam }
    public enum LearningStage { IndependentAnswer, Feedback, Repetition, Recall, Complete }
    public enum KnowledgeState { NEW, LEARNING, REVIEW_DUE, PRACTISING, MASTERED, NEEDS_REVIEW }
    public enum FollowUpKind { Clarification, Example, Comparison, Opinion, TopicChange, EndConversation }

    [Serializable]
    public sealed class PracticeScenario
    {
        public string Id = "", Npc = "", Topic = "", ItemId = "", Prompt = "", Goal = "", Model = "";
        public string ObjectWord = "", StressTip = "", RecallPrompt = "";
        public string[] Vocabulary = Array.Empty<string>();
        public int Band = 5;
    }

    /// Acoustic values may only be populated by an audio assessor, never from STT confidence or text.
    [Serializable]
    public sealed class UtteranceEvidence
    {
        public string Text = "", Correction = "", Explanation = "", ErrorCategory = "", AcousticProvider = "";
        public bool Spoken, ProviderAvailable, GoalAssessed, Relevant, GoalMet;
        public double? AcousticScore;
        public string ImprovedExpression = "", ScoreEvidence = "";
        public bool LanguageScoresAssessed;
        public int CoherenceScore, LexicalScore, GrammarScore;
    }

    [Serializable]
    public sealed class AssessmentRecord
    {
        public string Text = "", Correction = "", Feedback = "", GrammarError = "";
        public string Coherence = "", LexicalResource = "", Grammar = "", PronunciationNote = "";
        public double? Pronunciation, FluencyScore, CoherenceScore, LexicalScore, GrammarScore;
        public string ImprovedExpression = "";
        public bool TaskMet, Assessed;
    }

    [Serializable]
    public sealed class LearningEvidence
    {
        public string Id = "", ItemId = "", Scenario = "", Text = "", Correction = "", Error = "", Feedback = "";
        public string AcousticObservation = "", ImprovedExpression = "";
        public bool LanguageScoresAssessed;
        public int CoherenceScore, LexicalScore, GrammarScore;
        public LearningStage Stage;
        public long Utc;
        public bool Success, Independent;
    }

    [Serializable]
    public sealed class LearningItem
    {
        public string Id = "", Expression = "", LastScenario = "";
        public KnowledgeState State;
        public int IndependentSuccesses, RepeatSuccesses, Failures, IntervalIndex;
        public long DueUtc, FirstSuccessUtc, LastSuccessUtc;
        public string LastSuccessfulScenario = "";
        public bool DelayedTransfer;
        public List<string> Contexts = new List<string>();
    }

    [Serializable]
    public sealed class ExamRecord
    {
        public long Utc;
        public List<LearningEvidence> Answers = new List<LearningEvidence>();
        public string Feedback = "";
    }

    [Serializable]
    public sealed class PendingLearningSession
    {
        public string Scenario = "", Prompt = "", Correction = "";
        public LearningStage Stage;
        public PracticeScenario Content;
    }

    [Serializable]
    public sealed class LearnerMemory
    {
        public int Version = 1;
        public GameMode Mode = GameMode.GuidedLearning;
        public List<LearningItem> Items = new List<LearningItem>();
        public List<LearningEvidence> History = new List<LearningEvidence>();
        public List<ExamRecord> Exams = new List<ExamRecord>();
        public List<PendingLearningSession> Pending = new List<PendingLearningSession>();
        public string LastLocation = "", JournalDate = "";
    }

    public sealed class PracticeOutcome
    {
        public bool Advance;
        public string Status = "";
        public AssessmentRecord Assessment;
    }
}
