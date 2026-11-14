using System;
using ContextSpeak.Slice;

namespace ContextSpeak.Learning
{
    public static class SpeakingAssessmentService
    {
        public static AssessmentRecord Assess(UtteranceEvidence evidence, IeltsBand band)
        {
            var assessed = evidence.Spoken && evidence.ProviderAvailable && evidence.GoalAssessed;
            var result = new AssessmentRecord { Text = evidence.Text, Assessed = assessed,
                TaskMet = assessed && evidence.Relevant && evidence.GoalMet,
                PronunciationNote = "Pronunciation not assessed — no acoustic evidence. STT is not a pronunciation score." };
            if (!assessed) { result.Feedback = "MOCK / unassessed — reconnect speech and AI. No learning credit recorded."; return result; }
            var local = SpeakingCoach.Review(evidence.Text, band, true, evidence.Spoken, includePragmatics: false);
            result.ImprovedExpression = evidence.ImprovedExpression ?? "";
            VoiceCoach.ApplyScores(evidence, result);
            result.Correction = string.IsNullOrWhiteSpace(evidence.Correction) ? local.Corrected : evidence.Correction.Trim();
            result.GrammarError = result.Correction == "" ? "" : string.IsNullOrWhiteSpace(evidence.ErrorCategory) ? "grammar-or-word-choice" : evidence.ErrorCategory;
            result.Coherence = result.TaskMet ? "Your answer addresses the current question. Develop a clear reason or example." : "Clarify your meaning and answer the current question.";
            result.LexicalResource = "Transcript feedback only: aim for precise, context-appropriate expressions; rare words are not required.";
            result.Grammar = result.Correction == "" ? "No correction identified by this limited check; this is not a full grammar band assessment." : evidence.Explanation != "" ? evidence.Explanation : local.Explanation;
            if (evidence.AcousticScore.HasValue && !string.IsNullOrWhiteSpace(evidence.AcousticProvider)
                && !double.IsNaN(evidence.AcousticScore.Value) && evidence.AcousticScore >= 0 && evidence.AcousticScore <= 100)
            {
                result.Pronunciation = evidence.AcousticScore;
                result.PronunciationNote = "Acoustic practice score " + evidence.AcousticScore.Value.ToString("0") + "/100 · " + evidence.AcousticProvider + ". Understandable accents are accepted; not an IELTS band.";
            }
            result.Feedback = VoiceCoach.Marks(result) + "\n" + string.Join("\n", new[] {
                "Fluency & coherence: " + result.Coherence + " Timing and fluency are not measured from a transcript.",
                "Lexical resource: " + result.LexicalResource,
                "Grammatical range & accuracy: " + result.Grammar,
                result.PronunciationNote,
                "Gameplay communication goal: " + (result.TaskMet ? "met" : "not yet met") + ". Not an official IELTS result.",
            });
            return result;
        }
    }
}
