using System;
using System.Linq;
namespace ContextSpeak.Proactive
{
    public sealed class EventCooldownManager
    {
        private readonly ProactiveSave save;
        public EventCooldownManager(ProactiveSave save) { this.save = save; }
        public bool Ready(string npc, string eventId, DateTime utc, bool continuing = false)
        {
            return save.Settings.Frequency != InteractionFrequency.Off && (continuing || save.Run == null)
                && new[] { "global", "npc:" + npc, "event:" + eventId }.All(key => !save.Cooldowns.Any(c => c.Key == key && c.Until > utc.Ticks));
        }
        public void Reserve(string npc, string eventId, DateTime utc, float density = 1, float npcFrequency = 1)
        {
            var frequency = save.Settings.Frequency;
            Set("global", utc.AddSeconds((frequency == InteractionFrequency.High ? 25 : frequency == InteractionFrequency.Normal ? 60 : 150) / Factor(density)));
            Set("npc:" + npc, utc.AddSeconds(90 / Factor(npcFrequency)));
            Set("event:" + eventId, utc.AddSeconds(180));
            save.Cooldowns.RemoveAll(c => c.Until < utc.Ticks);
        }
        private static float Factor(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Math.Max(.25f, Math.Min(2, value));
        private void Set(string key, DateTime until)
        {
            save.Cooldowns.RemoveAll(c => c.Key == key);
            save.Cooldowns.Add(new CooldownRecord { Key = key, Until = until.Ticks });
        }
    }
}
