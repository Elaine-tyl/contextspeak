using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;

static class Program
{
    static int checks;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
    static void Main()
    {
        Check(WorldSceneCatalog.Resolve("?world=arrival", true) == "ArrivalAirport", "Arrival airport must have a browser entry.");
        Check(WorldSceneCatalog.Resolve("?world=city", true) == "MetroCity", "City must have a browser entry.");
        Check(WorldSceneCatalog.Resolve("?world=beach&test=1", true) == "TropicalBeach", "Query order/options must preserve the requested district.");
        Check(WorldSceneCatalog.Resolve("?world=slice", true) == "ForestSlice", "Existing café bookmarks must work.");
        Check(WorldSceneCatalog.Resolve("?world=../../private", true) == "ForestCafe", "Unknown routes cannot request arbitrary scene paths.");
        Check(WorldSceneCatalog.Resolve("", false) == "ForestSlice", "Desktop keeps its default café.");
        Check(WorldSceneCatalog.All.Select(scene => scene.Name).Distinct().Count() == WorldSceneCatalog.All.Length, "Scene names must be unique.");
        var players = WorldSceneCatalog.PlayerPaths("Assets/Generated/Bootstrap.unity");
        foreach (var name in new[] { "MetroCity", "Airport", "AircraftCabin", "ArrivalAirport", "Hospital", "Restaurant", "TropicalBeach", "WildlifeZoo" })
            Check(players.Any(path => Path.GetFileNameWithoutExtension(path) == name), name + " must ship even when Editor Build Settings are empty/disabled.");
        Check(!players.Any(path => path.Contains("EnvironmentBenchmark")), "Benchmark scene is not a player location.");
        Check(!players.Contains("Assets/Generated/ForestSlice.unity"), "Streamed café cannot be duplicated as a direct player scene.");
        Check(players[0].EndsWith("Bootstrap.unity"), "Bootstrap must remain the first player scene.");
        Console.WriteLine("World export regression checks passed: " + checks);
    }
}
