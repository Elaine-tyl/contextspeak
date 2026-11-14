using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Learning
{
    /// Transcript practice points are optional provider evidence, never guessed IELTS bands.
    public static class VoiceCoach
    {
        public static string[] Hints(PracticeScenario scenario, LearnerMemory memory, LearningStage stage, bool exam = false)
        {
            if (scenario == null || exam || stage == LearningStage.Recall) return Array.Empty<string>();
            var context = (scenario.Id + " " + scenario.Topic + " " + scenario.Prompt).ToLowerInvariant();
            var extra = context.Contains("airport") ? new[] { "itinerary", "accommodation", "duration", "sightseeing" }
                : context.Contains("hospital") ? new[] { "symptoms", "since yesterday", "appointment", "discomfort" }
                : context.Contains("home") ? new[] { "breakfast", "delicious", "helpful", "thank you" }
                : context.Contains("school") ? new[] { "assignment", "deadline", "explain", "example" }
                : context.Contains("cafe") || context.Contains("drink") ? new[] { "recommendation", "oat milk", "medium", "without sugar" }
                : new[] { "prefer", "because", "perhaps", "for example" };
            var weak = memory?.Items.Where(item => item.State == KnowledgeState.NEEDS_REVIEW || item.State == KnowledgeState.REVIEW_DUE)
                .Select(item => item.Expression).ToArray() ?? Array.Empty<string>();
            return (scenario.Vocabulary ?? Array.Empty<string>()).Concat(extra)
                .Where(word => !string.IsNullOrWhiteSpace(word) && word.Length <= 40 && word != scenario.Model)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(word => weak.Contains(word))
                .Take(scenario.Band >= 8 ? 3 : scenario.Band >= 5 ? 4 : 6).ToArray();
        }
        public static void ApplyScores(UtteranceEvidence evidence, AssessmentRecord result)
        {
            if (!result.Assessed || !evidence.LanguageScoresAssessed || string.IsNullOrWhiteSpace(evidence.ScoreEvidence)) return;
            string Normalize(string text) => Regex.Replace((text ?? "").ToLowerInvariant().Replace('’', '\''), @"\s+", " ").Trim();
            if (!Normalize(evidence.Text).Contains(Normalize(evidence.ScoreEvidence))) return;
            var scores = new[] { evidence.CoherenceScore, evidence.LexicalScore, evidence.GrammarScore };
            if (scores.Any(score => score < 0 || score > 100)) return;
            result.CoherenceScore = evidence.CoherenceScore;
            result.LexicalScore = evidence.LexicalScore;
            result.GrammarScore = evidence.GrammarScore;
        }
        public static string Marks(AssessmentRecord result)
        {
            string Mark(double? score) => score.HasValue ? score.Value.ToString("0") + "/100" : "Not assessed";
            if (result == null || !result.Assessed) return "Practice marks unavailable · no assessed spoken answer.";
            return "Last reply · transcript practice estimates · not IELTS bands\nCoherence " + Mark(result.CoherenceScore)
                + " · Vocabulary " + Mark(result.LexicalScore) + " · Grammar " + Mark(result.GrammarScore)
                + "\nFluency: Not assessed · Pronunciation: " + Mark(result.Pronunciation)
                + " · Task: " + (result.TaskMet ? "met" : "try again");
        }
        public static bool CanCapture(bool npcAudio, bool replayAudio, bool busy) => !npcAudio && !replayAudio && !busy;
        // A signal-quality gate, not speech recognition or acoustic pronunciation assessment.
        public static bool HasSpeech(float[] samples, int rate)
        {
            if (samples == null || rate <= 0 || samples.Length < rate / 4) return false;
            double energy = 0; var changes = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample)) return false;
                energy += sample * sample;
                if (i > 0 && Math.Abs(sample - samples[i - 1]) > .0005) changes++;
            }
            return Math.Sqrt(energy / samples.Length) > .004 && changes > samples.Length / 100;
        }
    }
}
