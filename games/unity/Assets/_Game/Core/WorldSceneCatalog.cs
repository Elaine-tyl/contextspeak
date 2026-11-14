using System;
using System.Linq;

namespace ContextSpeak.Cafe
{
    public sealed class WorldSceneEntry
    {
        public readonly string Route, Name, Path;
        public readonly bool Streamed;
        public WorldSceneEntry(string route, string name, string folder, bool streamed = false)
        {
            Route = route; Name = name; Streamed = streamed;
            Path = folder + "/" + name + ".unity";
        }
    }

    // Build selection and URL loading share one explicit list of playable saved scenes.
    public static class WorldSceneCatalog
    {
        private const string Generated = "Assets/Generated", Locations = "Assets/ContextSpeak/Scenes";
        public static readonly WorldSceneEntry[] All = {
            new WorldSceneEntry("slice", "ForestSlice", Generated, true),
            new WorldSceneEntry("character", "CharacterCreation", Generated, true),
            new WorldSceneEntry("city", "MetroCity", Generated),
            new WorldSceneEntry("activities", "LivingWorldDistrict", Generated),
            new WorldSceneEntry("arcade", "Arcade", Generated),
            new WorldSceneEntry("airport", "Airport", Locations),
            new WorldSceneEntry("cabin", "AircraftCabin", Locations),
            new WorldSceneEntry("arrival", "ArrivalAirport", Locations),
            new WorldSceneEntry("hospital", "Hospital", Locations),
            new WorldSceneEntry("restaurant", "Restaurant", Locations),
            new WorldSceneEntry("beach", "TropicalBeach", Locations),
            new WorldSceneEntry("zoo", "WildlifeZoo", Locations),
            new WorldSceneEntry("villa", "LuxuryVilla", Locations)
        };
        /// The player's home (the villa) — where a new game starts and where links without a world open.
        public const string Home = "LuxuryVilla";
        public static WorldSceneEntry Find(string name) => Array.Find(All, scene => scene.Name == name);
        public static string[] PlayerPaths(string bootstrap) => new[] { bootstrap }
            .Concat(All.Where(scene => !scene.Streamed).Select(scene => scene.Path)).Distinct().ToArray();
        public static string Resolve(string query, bool browser)
        {
            foreach (var parameter in (query ?? "").TrimStart('?').Split('&'))
            {
                var parts = parameter.Split(new[] { '=' }, 2);
                if (parts.Length != 2 || parts[0] != "world") continue;
                // Retired duplicates keep working as links: the old café opens the Forest Café world, the old Life town opens home.
                var wanted = parts[1] == "cafe" ? "slice" : parts[1] == "life" ? "villa" : parts[1];
                var route = Array.Find(All, scene => scene.Route == wanted);
                if (route != null) return route.Name;
            }
            return Home;
        }
    }
}
