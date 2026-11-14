using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.LivingWorld
{
    [Serializable]
    public sealed class LivingWorldState
    {
        public int Version = 1, Day = 1, Minute = 15 * 60, Band = 5;
        public bool Raining;
        public StoryRun Run = new StoryRun();
        public List<StoryRun> Runs = new List<StoryRun>();
        public List<string> Completed = new List<string>(), Flags = new List<string>(), Receipts = new List<string>();
        public List<NpcMemory> Memories = new List<NpcMemory>();
        public string Itinerary = "", DeferredEvent = "", VoiceLearningJson = "";
        public int DeferredUntil;

        public static LivingWorldState Normalize(LivingWorldState value)
        {
            if (value == null || value.Version != 1) return new LivingWorldState();
            value.Run = value.Run ?? new StoryRun();
            value.Runs = (value.Runs ?? new List<StoryRun>()).Where(run => run != null && !string.IsNullOrEmpty(run.Event))
                .GroupBy(run => run.Event).Select(group => group.First()).Take(100).ToList();
            if (!string.IsNullOrEmpty(value.Run.Event) && !value.Runs.Any(run => run.Event == value.Run.Event)) value.Runs.Add(value.Run);
            value.Run = value.Runs.FirstOrDefault(run => run.Event == value.Run.Event) ?? value.Run;
            foreach (var run in value.Runs) { run.Repair = run.Repair ?? ""; run.RepairChoice = run.RepairChoice ?? ""; }
            value.Completed = Clean(value.Completed); value.Flags = Clean(value.Flags); value.Receipts = Clean(value.Receipts);
            value.Memories = (value.Memories ?? new List<NpcMemory>()).Where(item => item != null && !string.IsNullOrEmpty(item.Id))
                .GroupBy(item => item.Id).Select(group => group.First()).Take(40).ToList();
            foreach (var memory in value.Memories)
            {
                memory.Relationship = Math.Max(0, Math.Min(100, memory.Relationship));
                memory.Facts = Clean(memory.Facts); memory.Promises = Clean(memory.Promises);
            }
            value.Band = Math.Max(3, Math.Min(9, value.Band));
            value.Day = Math.Max(1, Math.Min(36500, value.Day)); value.Minute = Math.Max(0, Math.Min(1439, value.Minute));
            value.Run.Repair = value.Run.Repair ?? "";
            return value;
        }
        private static List<string> Clean(List<string> values) => (values ?? new List<string>())
            .Where(value => !string.IsNullOrEmpty(value) && value.Length <= 240).Distinct().Take(200).ToList();
    }

    [Serializable]
    public sealed class StoryRun
    {
        public string Event = "", Repair = "", RepairChoice = "", VoiceChoice = "";
        public int Step, Revision;
    }
    [Serializable]
    public sealed class NpcMemory
    {
        public string Id, Name;
        public int Relationship, LastDay;
        public List<string> Facts = new List<string>(), Promises = new List<string>();
    }
    public static class NPCMemorySystem
    {
        public static NpcMemory Find(LivingWorldState state, string id) => state.Memories.FirstOrDefault(memory => memory.Id == id);
        public static void Remember(LivingWorldState state, string id, string name, string fact, string promise, bool met)
        {
            var memory = Find(state, id);
            if (memory == null) { memory = new NpcMemory { Id = id, Name = name }; state.Memories.Add(memory); }
            if (!string.IsNullOrEmpty(fact) && !memory.Facts.Contains(fact)) memory.Facts.Add(fact);
            if (!string.IsNullOrEmpty(promise) && !memory.Promises.Contains(promise)) memory.Promises.Add(promise);
            memory.LastDay = state.Day;
            memory.Relationship = Math.Min(100, memory.Relationship + (met ? 5 : 1));
            if (memory.Facts.Count > 12) memory.Facts.RemoveAt(0);
        }
    }

    public sealed class StoryContext
    {
        public string Location;
        public int Day, Minute, Band;
        public bool Raining;
        public int AbsoluteMinute => Math.Max(1, Day) * 1440 + Minute;
    }
    public enum StoryStatus { Rejected, Stale, Unavailable, Rehearsal, Repeat, Advanced, Completed }
    public enum StoryAction { None, MeetMarket, DeclineMarket, JoinTrip, DeclineTrip, MorningFlight, Refund, ActivityComplete }
    public sealed class StoryAssessment
    {
        public bool Assessed, Relevant, GoalMet;
        public string Correction = "", Explanation = "";
    }
    public sealed class StoryResult
    {
        public StoryStatus Status;
        public string Feedback;
        public StoryResult(StoryStatus status, string feedback) { Status = status; Feedback = feedback; }
    }
}
