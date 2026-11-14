using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Learning
{
    public sealed class FollowUp
    {
        public FollowUpKind Kind;
        public string Question;
        public string Goal = "", Model = "", ItemId = "";
    }

    public static class AdaptiveDialogueManager
    {
        public static bool Clarification(string text) => Regex.IsMatch((text ?? "").Trim(),
            @"^(?:(?:sorry[, ]+)?(?:could you repeat (?:that|the question)|(?:please )?say that again|what do you mean|(?:please )?explain that|i (?:didn't|did not) catch that))[?.! ]*$", RegexOptions.IgnoreCase);

        public static FollowUp Next(PracticeScenario scene, UtteranceEvidence answer, int turn, int band)
        {
            if (Clarification(answer.Text)) return new FollowUp { Kind = FollowUpKind.Clarification, Question = "Of course. " + scene.Prompt };
            var topicRequest = Regex.Match(answer.Text ?? "", @"(?:talk about|discuss) (travel|music|books|food|work|study|family|sports|films)\b", RegexOptions.IgnoreCase);
            if (topicRequest.Success)
            {
                var topic = topicRequest.Groups[1].Value.ToLowerInvariant();
                return new FollowUp { Kind = FollowUpKind.TopicChange, Question = "Sure. What interests you about " + topic + "?",
                    Goal = "Explain a personal interest in " + topic + ". Accept any relevant opinion.",
                    Model = "I find " + topic + " interesting because it gives me something new to learn and discuss with friends.", ItemId = "topic-" + topic };
            }
            // Topic anchors are authored and must actually occur in this answer; never assume preferences.
            var anchors = (scene.Vocabulary ?? Array.Empty<string>()).Concat(new[] { "tea", "coffee", "family", "homework", "cooking", "friends", "transport", "technology" });
            var focus = anchors.FirstOrDefault(word => word.Length > 1 && Regex.IsMatch(answer.Text ?? "", @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase)) ?? scene.Topic;
            if (turn >= 3) return new FollowUp { Kind = FollowUpKind.EndConversation, Question = "Thanks for sharing. Let's try this idea with someone in another place." };
            if (band <= 4) return new FollowUp { Kind = FollowUpKind.Example, Question = "Can you give me one example about " + focus + "?" };
            if (turn == 1) return new FollowUp { Kind = FollowUpKind.Example, Question = "You mentioned " + focus + ". Can you give me a specific example?" };
            return new FollowUp { Kind = FollowUpKind.Comparison, Question = "Thinking about " + focus + ", would everyone agree with you? Compare another point of view." };
        }

        public static int RecommendedBand(LearnerMemory memory, int selected)
        {
            var recent = memory.History.Where(entry => entry.Independent).TakeLast(6).ToArray();
            if (recent.Length < 3) return Math.Max(3, Math.Min(9, selected));
            var successes = recent.Count(entry => entry.Success);
            var change = successes == recent.Length && recent.Select(entry => entry.Scenario).Distinct().Count() >= 2 ? 1 : successes * 2 < recent.Length ? -1 : 0;
            return Math.Max(3, Math.Min(9, selected + change));
        }
    }
}
