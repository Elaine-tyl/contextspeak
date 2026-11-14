using System;
using System.Linq;
using ContextSpeak.Learning;
namespace ContextSpeak.Proactive
{
    public static class NPCMemoryService
    {
        public static ProactiveSave Sanitize(ProactiveSave save)
        {
            save ??= new ProactiveSave();
            save.Settings ??= new ProactiveSettings();
            save.Learner = LearningMemoryService.Sanitize(save.Learner);
            save.Memories ??= new System.Collections.Generic.List<SocialMemory>();
            save.Cooldowns ??= new System.Collections.Generic.List<CooldownRecord>();
            save.Completed ??= new System.Collections.Generic.List<string>();
            if (!Enum.IsDefined(typeof(InteractionFrequency), save.Settings.Frequency)) save.Settings.Frequency = InteractionFrequency.High;
            if (!Enum.IsDefined(typeof(GameMode), save.Settings.Mode)) save.Settings.Mode = GameMode.GuidedLearning;
            if (save.Run != null && (!ProactiveEventContent.All().Any(e => e.Id == save.Run.Id) || save.Run.Step < 0 || save.Run.Step > 6)) save.Run = null;
            return save;
        }
        public static SocialMemory Of(ProactiveSave save, string npc)
        {
            var found = save.Memories.FirstOrDefault(m => m.NpcId == npc);
            if (found != null) return found;
            var memory = new SocialMemory { NpcId = npc };
            save.Memories.Add(memory);
            return memory;
        }
        public static void Remember(ProactiveSave save, string npc, string fact)
        {
            var memory = Of(save, npc);
            memory.Summaries ??= new System.Collections.Generic.List<string>();
            memory.Summaries.Add(fact.Length > 180 ? fact.Substring(0, 180) : fact);
            while (memory.Summaries.Count > 6) memory.Summaries.RemoveAt(0);
        }
    }
    public static class NPCRelationshipManager
    {
        public static void Helped(ProactiveSave save, string npc, string fact)
        {
            var memory = NPCMemoryService.Of(save, npc);
            memory.Relationship = Math.Min(100, memory.Relationship + 4);
            NPCMemoryService.Remember(save, npc, fact);
        }
    }
}
