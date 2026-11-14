using System;
using System.Linq;

namespace ContextSpeak.Learning
{
    public static class SpacedReviewScheduler
    {
        public static readonly int[] DefaultDays = { 1, 3, 7, 14 };
        public static bool CountsToday(LearningItem item, DateTime utc) =>
            item.LastSuccessUtc == 0 || utc.Date > new DateTime(item.LastSuccessUtc, DateTimeKind.Utc).Date;

        public static void Context(LearningItem item, string scenario, DateTime utc)
        {
            if (!CountsToday(item, utc)) return;
            if (item.LastSuccessfulScenario != "" && item.LastSuccessfulScenario != scenario && item.LastSuccessUtc > 0)
                item.DelayedTransfer = true;
            if (!item.Contexts.Contains(scenario)) item.Contexts.Add(scenario);
            item.LastSuccessfulScenario = scenario;
        }

        public static bool Due(LearningItem item, DateTime utc) => item.DueUtc > 0 && item.DueUtc <= utc.Ticks;

        public static void Record(LearningItem item, bool success, bool independent, DateTime utc, int[] days = null)
        {
            if (!independent) { if (success) item.RepeatSuccesses++; return; }
            var intervals = days != null && days.Length > 0 && days.All(day => day > 0) ? days : DefaultDays;
            if (!success)
            {
                item.Failures++;
                item.IntervalIndex = 0;
                item.State = KnowledgeState.NEEDS_REVIEW;
                item.DueUtc = utc.AddDays(intervals[0]).Ticks;
                return;
            }
            // A repeated answer in one session/day is practice, not fresh retention evidence.
            if (!CountsToday(item, utc)) return;
            item.IndependentSuccesses++;
            if (item.FirstSuccessUtc == 0) item.FirstSuccessUtc = utc.Ticks;
            item.LastSuccessUtc = utc.Ticks;
            item.State = item.IndependentSuccesses >= 3 && item.Contexts.Distinct().Count() >= 2
                && item.DelayedTransfer && utc - new DateTime(item.FirstSuccessUtc, DateTimeKind.Utc) >= TimeSpan.FromDays(3)
                ? KnowledgeState.MASTERED : KnowledgeState.LEARNING;
            item.DueUtc = utc.AddDays(intervals[Math.Min(item.IntervalIndex, intervals.Length - 1)]).Ticks;
            item.IntervalIndex = Math.Min(item.IntervalIndex + 1, intervals.Length - 1);
        }
    }
}
