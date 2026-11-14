using System;
using System.Linq;
using System.Text;

namespace ContextSpeak.Learning
{
    public static class LearningDashboardController
    {
        public static string Build(LearnerMemory memory, DateTime utc)
        {
            var text = new StringBuilder("LEARNING PROGRESS · formative evidence, not official IELTS scores\n");
            text.AppendLine("Due reviews: " + memory.Items.Count(item => SpacedReviewScheduler.Due(item, utc)));
            var mastered = memory.Items.Where(item => item.State == KnowledgeState.MASTERED).ToArray();
            text.AppendLine("Mastered expressions: " + mastered.Length);
            foreach (var item in mastered) text.AppendLine("• " + item.Expression + " · " + item.Contexts.Count + " contexts");
            if (memory.History.Count == 0) text.AppendLine("No speaking evidence yet. Try a conversation at the café.");
            else
            {
                var independent = memory.History.Where(entry => entry.Independent).ToArray();
                text.AppendLine("Independent successful answers: " + independent.Count(entry => entry.Success) + " / " + independent.Length);
                text.AppendLine("Repetition attempts: " + memory.History.Count(entry => entry.Stage == LearningStage.Repetition) + " (kept separate from mastery)");
                text.AppendLine("Recurring errors / practice needs:");
                foreach (var error in memory.History.Where(entry => entry.Error != "").GroupBy(entry => entry.Error).OrderByDescending(group => group.Count()).Take(5))
                    text.AppendLine("• " + error.Key + " · observed " + error.Count() + " times");
                foreach (var day in independent.GroupBy(entry => new DateTime(entry.Utc, DateTimeKind.Utc).ToString("yyyy-MM-dd")).TakeLast(7))
                    text.AppendLine(day.Key + " · " + day.Count(entry => entry.Success) + "/" + day.Count() + " independent successes");
                text.AppendLine("Recent speaking practice:");
                foreach (var entry in memory.History.TakeLast(5).Reverse())
                    text.AppendLine("• " + entry.Scenario + " · " + entry.Stage + " · " + (entry.Success ? "successful" : "needs practice"));
            }
            text.AppendLine("Pronunciation requires acoustic assessment. Transcript timing is not measured fluency.");
            if (memory.Exams.Count > 0) text.AppendLine("\nLatest mock exam\n" + memory.Exams.Last().Feedback);
            return text.ToString();
        }
    }
}
