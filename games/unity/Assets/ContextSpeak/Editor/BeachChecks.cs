using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Beach: wave maths, quest order, activity closures, hotel totals, the surf
    /// balance model, the 18 conversations (EditMode, no save touched) and the TropicalBeach scene wiring.
    /// Play-mode behaviour (swimming, riding, rides, cooking timers) still needs a manual run.
    public static class BeachChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Beach")]
        public static void Menu()
        {
            var errors = Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak beach checks passed.");
            else Debug.LogError("ContextSpeak beach checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static string[] Run()
        {
            var errors = new List<string>();
            void Check(bool value, string message) { if (!value) errors.Add(message); }
            void Guard(string name, Action body)
            {
                try { body(); } catch (Exception error) { errors.Add(name + " threw " + error.GetType().Name + ": " + error.Message); }
            }
            var persist = SaveManager.Persist;
            SaveManager.Persist = false;
            try
            {
                Guard("Waves", () => Waves(Check));
                Guard("Quest", () => Quest(Check));
                Guard("Activities", () => Activities(Check));
                Guard("Hotel", () => Hotel(Check));
                Guard("Surf", () => Surf(Check));
                Guard("Dialogue", () => Dialogue(Check));
            }
            finally { SaveManager.UseInMemory(null); SaveManager.Persist = persist; }
            Guard("Beach scene", () => Scene(Check));
            return errors.ToArray();
        }

        private static void Waves(Action<bool, string> check)
        {
            var item = new GameObject("Wave check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var waves = item.AddComponent<OceanWaves>();
                waves.IslandCentre = new Vector3(0, 0, 260);
                waves.ShoreRadius = 260;
                var worst = 0f;
                foreach (var x in new[] { -120f, -40, 0, 35, 90 })
                    foreach (var z in new[] { -200f, -90, -30, 5 })
                    {
                        var grid = new Vector2(x, z);
                        var offset = waves.Displacement(grid, 12.3f);
                        var surface = waves.Height(new Vector3(x + offset.x, 0, z + offset.z), 12.3f);
                        worst = Mathf.Max(worst, Mathf.Abs(surface - offset.y));
                    }
                check(worst < 0.08f, "Water height lookup matches the Gerstner surface (worst error " + worst.ToString("0.000") + " m).");
                check(waves.ShoreFactor(new Vector2(0, 260)) < 0.2f && waves.ShoreFactor(new Vector2(0, -300)) > 0.95f, "Waves calm down near the island and are full size in open sea.");
                check(item.AddComponent<Swimming>().SurfaceFeet < OceanWaves.SeaLevel, "Swimmers float at the flat sea level (independent of wave height).");
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }

        private static void Quest(Action<bool, string> check)
        {
            SaveManager.UseInMemory(new LifeProgress());
            check(!BeachQuest.Advance(BeachStage.FoundCafe), "Quest refuses to skip ahead (café before arriving).");
            check(BeachQuest.Advance(BeachStage.AtBeach), "Arriving at the beach starts BEACH_DAY_001.");
            check(!BeachQuest.Advance(BeachStage.CompletedActivity), "Cannot complete an activity before renting one.");
            for (var stage = BeachStage.FoundCafe; stage <= BeachStage.ReturnedToHotel; stage++)
                check(BeachQuest.Advance(stage), "Objective " + (int)stage + " advances after its prerequisites.");
            check(QuestManager.Get(BeachQuest.Id, BeachQuest.Title).done, "Completing all 12 objectives completes BEACH_DAY_001.");
            check(BeachQuest.Objectives.Length == 13, "12 objectives as specified.");
            check(BeachQuest.Optional.Length == 6, "Six optional quests.");
        }

        private static void Activities(Action<bool, string> check)
        {
            check(BeachActivities.All.Length == 12 && BeachActivities.All.Select(a => a.Id).Distinct().Count() == 12, "Twelve distinct water activities.");
            check(BeachActivities.All.All(a => a.Safety != "" && a.Equipment != "" && a.Controls != "" && a.Objective != ""), "Every activity has equipment, controls, an objective and safety text.");
            var jetski = BeachActivities.Find("jetski");
            check(BeachActivities.Closed(jetski, 3, 0, 1) == "", "Jet skis open in calm, dry daylight.");
            check(BeachActivities.Closed(jetski, 14, 0, 1) != "", "Jet skis close in strong wind.");
            check(BeachActivities.Closed(jetski, 3, 0.8f, 1) != "", "Jet skis close in heavy rain.");
            check(BeachActivities.Closed(BeachActivities.Find("parasail"), 3, 0, 0.05f) != "", "Parasailing closes after dark.");
            check(BeachActivities.Closed(BeachActivities.Find("swim"), 3, 0, 1) == "", "Swimming is open on a normal day.");
        }

        private static void Hotel(Action<bool, string> check)
        {
            check(BeachHotel.Total("Ocean View Room", 2, 1500) == 37500, "Two ocean-view nights plus room service total $375.00.");
            check(BeachHotel.Total("Beach Villa", 1, 0) == 32000, "One villa night is $320.00.");
            check(BeachHotel.Types.Sum(type => type.Rooms.Length) == 7, "Seven bookable rooms (101–104, V1–V3).");
        }

        private static void Surf(Action<bool, string> check)
        {
            var balanced = new SurfBalance(42);
            for (var t = 0f; t < 20; t += 0.02f) balanced.Tick(0.02f, balanced.Lean * 3, 0.6f);
            check(!balanced.Fallen && balanced.Riding >= 15, "A surfer who counters the lean stays up for 15 s.");
            var passive = new SurfBalance(42);
            for (var t = 0f; t < 60 && !passive.Fallen; t += 0.02f) passive.Tick(0.02f, 0, 0.6f);
            check(passive.Fallen, "A surfer who does nothing eventually falls.");
        }

        private static void Dialogue(Action<bool, string> check)
        {
            var tokens = new Dictionary<string, string> { { "activity", "Kayaking" }, { "safety", "Wear your life jacket." }, { "room", "103" }, { "roomType", "Ocean View Room" }, { "nights", "2" }, { "equipment", "Kayak" }, { "price", "$15.00" }, { "objective", "Paddle the loop." } };
            foreach (var name in BeachScenarios.Names)
            {
                var scenario = BeachScenarios.Create(name, tokens);
                check(scenario != null && scenario.Steps != null && scenario.Steps.Count > 0, "Scenario '" + name + "' builds.");
                if (scenario == null) continue;
                var text = string.Join(" ", scenario.Steps.SelectMany(step => new[] { step.simple, step.standard, step.advanced, step.expert, step.model }));
                check(!text.Contains("{"), "Scenario '" + name + "' has no unreplaced {tokens}.");
                UnityEngine.Object.DestroyImmediate(scenario);
            }
            check(BeachScenarios.Names.Length == 18, "18 beach conversations.");
        }

        private static void Scene(Action<bool, string> check)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BeachBuilder.ScenePath) == null) { check(false, "TropicalBeach.unity not built yet (ContextSpeak > Build Tropical Beach Resort)."); return; }
            var previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(BeachBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
                var flow = All<BeachFlow>().FirstOrDefault();
                check(flow != null, "Beach systems object has a BeachFlow.");
                if (flow != null)
                {
                    var missing = new List<string>();
                    if (flow.Spawn == null) missing.Add("Spawn");
                    if (flow.CafePickup == null) missing.Add("CafePickup");
                    if (flow.SwimBuoy == null) missing.Add("SwimBuoy");
                    if (flow.SurfStart == null) missing.Add("SurfStart");
                    if (flow.Swim == null) missing.Add("Swim");
                    if (flow.Observer == null) missing.Add("Observer");
                    if (flow.JetSkiCourse == null) missing.Add("JetSkiCourse");
                    if (flow.KayakCourse == null) missing.Add("KayakCourse");
                    if (flow.LostBag == null) missing.Add("LostBag");
                    if (flow.Kitchen == null) missing.Add("Kitchen");
                    if (flow.Crafts.Length != 3) missing.Add("Crafts (" + flow.Crafts.Length + "/3)");
                    if (flow.Rides.Length != 4) missing.Add("Rides (" + flow.Rides.Length + "/4)");
                    if (flow.RoomDoors.Length != 7) missing.Add("RoomDoors (" + flow.RoomDoors.Length + "/7)");
                    if (flow.CafeZone.size == Vector3.zero || flow.RestaurantZone.size == Vector3.zero || flow.WatersportsZone.size == Vector3.zero || flow.ViewpointZone.size == Vector3.zero || flow.HotelLobby.size == Vector3.zero) missing.Add("zones");
                    check(missing.Count == 0, "Beach flow wired (missing: " + string.Join(", ", missing) + ").");
                }
                var keys = new HashSet<string>(All<NPCController>().Select(npc => npc.Key));
                var needed = new[] { "barista", "bartender", "souvenir", "host", "waiter", "cashier", "chef", "watersports", "surf", "dive", "captain", "lifeguard", "reception", "tourist", "tourist-lost", "tourist-directions", "volunteer" };
                var absent = needed.Where(key => !keys.Contains(key)).ToArray();
                check(absent.Length == 0, "Every role has an NPC (missing: " + string.Join(", ", absent) + ").");
                check(All<SeatController>().Count(seat => seat.Group == "beach-in") >= 6 && All<SeatController>().Count(seat => seat.Group == "beach-out") >= 6, "Coral Kitchen has indoor and deck seating.");
                check(All<OceanWaves>().Length == 1 && All<Terrain>().Length == 1, "One ocean and one terrain.");
                check(All<FishSchool>().Length >= 3, "At least three species of reef fish (" + All<FishSchool>().Length + ").");
                check(All<LitterPiece>().Length == 10, "Ten pieces of litter for the cleanup quest.");
                check(All<PortalDoor>().Any(portal => portal.TargetScene == "MetroCity"), "A way back to Metro City.");
                check(All<SkySystem>().Length == 1, "Sky & Weather installed.");
                var spots = new HashSet<string>(All<AirportSpot>().Select(spot => spot.Action));
                var actions = new[] { "sleep", "wardrobe", "roomservice", "sunset", "lostbag", "swimflag" };
                check(actions.All(spots.Contains), "Hotel, sunset, flag and lost-bag interactions exist (missing: " + string.Join(", ", actions.Where(a => !spots.Contains(a))) + ").");
                check(BeachHotel.Types.SelectMany(type => type.Rooms).All(room => spots.Contains("room-door:" + room)), "Every room has a key-card reader.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous != "" && EditorSceneManager.GetActiveScene().path != previous) { }
            }
        }
    }
}
