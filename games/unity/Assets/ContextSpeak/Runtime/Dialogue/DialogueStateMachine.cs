using System;
using System.Linq;
using System.Text.RegularExpressions;
using ContextSpeak.Slice;

namespace ContextSpeak.Life
{
    public enum DialoguePhase { Idle, AwaitingAnswer, AwaitingRepeat, StepComplete, Complete }

    /// NPC asks → learner answers → meaning, grammar and vocabulary feedback → a natural corrected
    /// sentence → learner repeats it → next line. In Learning Mode the repeat cannot be skipped.
    /// Pure C# so it can be verified without a scene.
    public sealed class DialogueStateMachine
    {
        public ScenarioDefinition Scenario { get; private set; }
        public IeltsBand Band { get; private set; }
        public bool LearningMode { get; private set; }
        public int Index { get; private set; }
        public DialoguePhase Phase { get; private set; } = DialoguePhase.Idle;
        public int Attempts { get; private set; }
        public SpeakingResult Review { get; private set; }
        public string Target { get; private set; } = "";     // sentence to repeat
        public string Hint { get; private set; } = "";
        public string Feedback { get; private set; } = "";
        public event Action<ScenarioStep> StepCompleted;

        public ScenarioStep Current => Scenario != null && Index < Scenario.Steps.Count ? Scenario.Steps[Index] : null;

        public void Start(ScenarioDefinition scenario, IeltsBand band, bool learningMode)
        {
            Scenario = scenario;
            Band = band;
            LearningMode = learningMode;
            Index = 0;
            Begin();
        }

        public string NpcLine
        {
            get
            {
                var step = Current;
                if (step == null) return "";
                var line = Band <= IeltsBand.Band4 ? step.simple : Band <= IeltsBand.Band6 ? step.standard : Band >= IeltsBand.Band9 && !string.IsNullOrWhiteSpace(step.expert) ? step.expert : step.advanced;
                if (string.IsNullOrWhiteSpace(line)) line = !string.IsNullOrWhiteSpace(step.standard) ? step.standard : step.simple;
                return line;
            }
        }

        public string[] Vocabulary => Current == null || string.IsNullOrWhiteSpace(Current.vocabulary)
            ? new string[0] : Current.vocabulary.Split('|').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

        private void Begin()
        {
            Attempts = 0;
            Review = null;
            Target = Hint = Feedback = "";
            Phase = Current == null ? DialoguePhase.Complete : DialoguePhase.AwaitingAnswer;
        }

        public static bool Conveys(ScenarioStep step, string text)
        {
            if (step == null) return false;
            var normalized = OrderIntentParser.Normalize(text ?? "");
            if (normalized.Trim().Length < 2) return false;
            if (string.IsNullOrWhiteSpace(step.meaning)) return normalized.Split(' ').Length >= 2;
            return Regex.IsMatch(normalized, @"\b(?:" + step.meaning + ")", RegexOptions.IgnoreCase);
        }

        /// The learner's answer to the current NPC line.
        public DialoguePhase Answer(string text, bool spoken)
        {
            if (Phase != DialoguePhase.AwaitingAnswer || Current == null) return Phase;
            if (!spoken)
            {
                Review = SpeakingCoach.Review(text, Band, Conveys(Current, text), false);
                Feedback = "Typed rehearsal · no speaking credit.\n" + Summary(Review);
                Hint = "Say your answer aloud to continue. Typing does not advance this conversation.";
                return Phase;
            }
            Attempts++;
            var met = Conveys(Current, text);
            Review = SpeakingCoach.Review(text, Band, met, spoken);
            Feedback = Summary(Review);
            if (!met)
            {
                if (Attempts >= 3 && LearningMode)
                {
                    // Show a natural answer and ask the learner to say it: the gate still applies.
                    Target = Current.model;
                    Hint = "Here is one natural way to answer. Say it aloud to continue.";
                    Phase = DialoguePhase.AwaitingRepeat;
                }
                else
                {
                    Hint = (Current.goal != "" ? "Try again: " + Current.goal : "Answer the question in your own words.")
                        + (Attempts >= 2 && Current.model != "" ? "  " + SpeakingCoach.Hint(Current.model, Attempts) : "");
                }
                return Phase;
            }
            if (LearningMode && !string.IsNullOrEmpty(Review.Corrected))
            {
                Target = Review.Corrected;
                Hint = "Good meaning. Now say the corrected sentence.";
                Phase = DialoguePhase.AwaitingRepeat;
                return Phase;
            }
            Finish();
            return Phase;
        }

        /// The learner repeating the corrected (or model) sentence. Alternative wordings with the same
        /// key words pass; a different or wrong sentence does not advance the dialogue.
        public bool Repeat(string text, bool spoken = true)
        {
            if (Phase != DialoguePhase.AwaitingRepeat) return false;
            if (!spoken) { Hint = "Typed rehearsal only. Say the corrected sentence aloud to continue."; return false; }
            var passed = SpeakingCoach.PracticePassed(text, Target, Band);
            if (!passed) { Hint = "Not quite. Listen again and repeat: “" + Target + "”"; return false; }
            Finish();
            return true;
        }

        /// Practice mode only: move on without repeating.
        public bool Skip()
        {
            if (LearningMode || Phase == DialoguePhase.Complete) return false;
            Finish();
            return true;
        }

        private void Finish()
        {
            var step = Current;
            Phase = DialoguePhase.StepComplete;
            StepCompleted?.Invoke(step);
            Index++;
            Begin();
        }

        private static string Summary(SpeakingResult review)
        {
            if (review == null) return "";
            var notes = review.Criteria.Where(note => note.Level != CriterionLevel.NotAssessed)
                .Select(note => BandProfile.CriterionName(note.Criterion) + ": " + note.Note);
            var task = review.TaskMet ? "Task completion: done." : "Task completion: not yet — answer the question asked.";
            return string.Join("\n", notes.Concat(new[] { task }));
        }
    }
}
