using System;
using System.Linq;
namespace ContextSpeak.Proactive
{
    [Serializable] public sealed class LifeEventDefinition
    {
        public string Id = "", Title = "", Prompt = "", Goal = "", Model = "", Prop = "";
        public string[] Locations = Array.Empty<string>();
        public InteractionKind Kind = InteractionKind.Story;
        public int Priority = 10;
    }
    public static class SceneContexts
    {
        public static float Density(string location) => location == "hospital" ? .45f : location == "airport" ? .6f
            : location == "restaurant" ? .7f : location == "beach" || location == "zoo" ? .8f : 1;
        public static string Location(string name)
        {
            var value = (name ?? "").Split(':')[0].Replace(" ", "").ToLowerInvariant();
            if (value.Contains("airport") || value.Contains("aircraft")) return "airport";
            if (value.Contains("school")) return "school";
            if (value.Contains("supermarket")) return "supermarket";
            if (value.Contains("mall") || value.Contains("shopping")) return "mall";
            if (value.Contains("hospital")) return "hospital";
            if (value.Contains("restaurant")) return "restaurant";
            if (value.Contains("beach")) return "beach";
            if (value.Contains("zoo")) return "zoo";
            if (value.Contains("home")) return "home";
            if (value.Contains("city") || value.Contains("town") || value.Contains("neighborhood")) return "city";
            if (value.Contains("cafe") || value == "forestslice") return "cafe";
            return "";
        }
    }
    public sealed class ContextualEventSelector
    {
        private readonly LifeEventDefinition[] definitions;
        public ContextualEventSelector(LifeEventDefinition[] definitions = null) { this.definitions = definitions ?? ProactiveEventContent.All(); }
        public LifeEventDefinition Select(EventContext context, NPCSocialProfile npc, ProactiveSave save, bool contact)
        {
            if (contact) return definitions.First(e => e.Id == "collision");
            if (context.Hour < npc.StartHour || context.Hour >= npc.EndHour || npc.Sociability <= 0) return null;
            return definitions.Where(e => e.Locations.Contains(context.Location) && e.Id != "collision"
                && (npc.AvailableEvents.Length == 0 || npc.AvailableEvents.Contains(e.Id)))
                .OrderByDescending(e => e.Priority).FirstOrDefault();
        }
        public static string Ambient(EventContext context)
        {
            if (context.Familiar) return "Hey, good to see you again!";
            if (context.Raining) return "It's really coming down. Stay dry!";
            return context.Hour < 12 ? "Morning! Hope your day's going well." : "Hey there! Enjoy your day.";
        }
    }
}
