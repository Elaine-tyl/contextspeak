using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Living NPCs — Edit-mode checks of the data-driven parts.
    /// ContextSpeak > Checks > Living NPC Play Mode Tests — runs LivingNPCPlayTest in the neighbourhood.
    [InitializeOnLoad]
    public static class LivingNPCChecks
    {
        private const string Flag = "ContextSpeak.LivingNPCPlayTest";
        public const string UserScheduleJson = "[{\"startHour\":0,\"endHour\":7,\"activity\":\"Sleep\",\"location\":\"住宅\"},{\"startHour\":7,\"endHour\":8,\"activity\":\"Breakfast\",\"location\":\"住宅\"},{\"startHour\":8,\"endHour\":9,\"activity\":\"Commute\",\"location\":\"城市 / Tram\"},{\"startHour\":9,\"endHour\":12,\"activity\":\"ServeCustomers\",\"location\":\"Forest Café\"},{\"startHour\":12,\"endHour\":13,\"activity\":\"Break\",\"location\":\"员工休息区\"},{\"startHour\":13,\"endHour\":17,\"activity\":\"Work\",\"location\":\"Forest Café\"},{\"startHour\":17,\"endHour\":18,\"activity\":\"LeaveWork\",\"location\":\"城市\"},{\"startHour\":18,\"endHour\":20,\"activity\":\"Socialize\",\"location\":\"商场 / 餐厅\"},{\"startHour\":20,\"endHour\":23,\"activity\":\"Relax\",\"location\":\"住宅\"},{\"startHour\":23,\"endHour\":24,\"activity\":\"Sleep\",\"location\":\"住宅\"}]";

        static LivingNPCChecks() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("ContextSpeak/Checks/Living NPCs")]
        public static void RunMenu()
        {
            var lines = Run();
            var failed = lines.Count(l => l.StartsWith("FAIL"));
            var text = "Living NPC checks: " + (lines.Count - failed) + "/" + lines.Count + " passed\n" + string.Join("\n", lines);
            if (failed > 0) Debug.LogError(text); else Debug.Log(text);
        }

        public static List<string> Run()
        {
            var lines = new List<string>();
            void Check(string name, bool ok, string detail) => lines.Add((ok ? "PASS " : "FAIL ") + name + " — " + detail);
            var temp = new List<Object>();
            try
            {
                // Schedule: the designer JSON exactly as supplied.
                var s = NPCDailySchedule.FromJson(UserScheduleJson);
                Check("Schedule parses the supplied JSON", s.Blocks.Count == 10, s.Blocks.Count + " blocks");
                Check("Schedule lookup", s.At(10).activity == "ServeCustomers" && s.At(12.5f).activity == "Break" && s.At(23.5f).activity == "Sleep" && s.At(3).activity == "Sleep" && s.At(8.2f).activity == "Commute",
                    "10:00 " + s.At(10).activity + ", 12:30 " + s.At(12.5f).activity + ", 23:30 " + s.At(23.5f).activity + ", 03:00 " + s.At(3).activity);
                Check("Next activity", s.Next(10).activity == "Break" && s.Next(23.5f).activity == "Breakfast", "after 10:00 → " + s.Next(10).activity + ", after 23:30 → " + s.Next(23.5f).activity);
                var late = NPCDailySchedule.FromJson(UserScheduleJson, 0.1f);
                Check("Personal punctuality offset", late.At(9.05f).activity == "Commute" && s.At(9.05f).activity == "ServeCustomers", "6 min late: at 09:03 still commuting");

                // Location strings (English / Chinese / alternatives) resolve to places.
                LivingPlace Make(string id, PlaceKind kind, params string[] aliases)
                {
                    var go = new GameObject("check " + id) { hideFlags = HideFlags.HideAndDontSave };
                    temp.Add(go);
                    var place = go.AddComponent<LivingPlace>(); place.Id = id; place.Kind = kind; place.Aliases = aliases;
                    LivingPlace.All.Add(place);
                    return place;
                }
                var home = Make("riverside", PlaceKind.Home); var cafe = Make("forest_cafe", PlaceKind.Cafe, "Forest Café"); var staff = Make("staff_room", PlaceKind.BreakRoom, "员工休息区");
                var stop = Make("tram_west", PlaceKind.TramStop); var street = Make("street", PlaceKind.Street, "城市");
                Check("Location: 住宅 → own home", LivingPlace.Find("住宅", "riverside", "forest_cafe") == home, "");
                Check("Location: Forest Café", LivingPlace.Find("Forest Café") == cafe, "");
                Check("Location: 员工休息区 → staff room", LivingPlace.Find("员工休息区") == staff, "");
                Check("Location: 城市 / Tram → street first", LivingPlace.Find("城市 / Tram") == street, "");
                Check("Location: 商场 / 餐厅 with neither present → none", LivingPlace.Find("商场 / 餐厅") == null, "falls back to café in the program");

                // Reservations: one person per seat.
                var seatGo = new GameObject("check seat") { hideFlags = HideFlags.HideAndDontSave }; temp.Add(seatGo);
                var seat = seatGo.AddComponent<ActivitySpot>();
                var a = new GameObject("check npc a") { hideFlags = HideFlags.HideAndDontSave }; temp.Add(a);
                var b = new GameObject("check npc b") { hideFlags = HideFlags.HideAndDontSave }; temp.Add(b);
                a.AddComponent<UnityEngine.AI.NavMeshAgent>(); b.AddComponent<UnityEngine.AI.NavMeshAgent>();
                var npcA = a.AddComponent<LivingNPC>(); var npcB = b.AddComponent<LivingNPC>();
                var firstTaken = seat.Reserve(npcA); var secondRefused = !seat.Reserve(npcB);
                seat.Release(npcA);
                Check("Seat reservation is exclusive", firstTaken && secondRefused && seat.Reserve(npcB), "second person waits until the first leaves");

                // Service queue order.
                var tillGo = new GameObject("check till") { hideFlags = HideFlags.HideAndDontSave }; temp.Add(tillGo);
                var till = tillGo.AddComponent<ServicePoint>();
                till.Join(npcA); till.Join(npcB);
                var first = till.Queue[0];
                till.Take(first);
                Check("Queue order", first.Customer == npcA && till.Position(npcB) == 0, "first come, first served; the next moves up");

                // Utility AI: hysteresis and commitment; urgent rain overrides.
                var brain = new NPCUtilityAI { Commitment = 5 };
                var needs = new NPCNeeds { Hunger = 0.62f };
                var block = new ScheduleBlock { activity = "Wander", location = "street" };
                var switches = 0; var last = "";
                for (var i = 0; i < 300; i++)
                {
                    needs.Hunger = 0.6f + 0.04f * Mathf.Sin(i * 0.7f);
                    var g = brain.Evaluate(block, needs, new NPCPersonality(), false, false, true, 0.1f);
                    if (g.Activity != last) { switches++; last = g.Activity; }
                }
                Check("No goal flip-flopping", switches <= 2, switches + " goal changes in 30 s of hunger jitter around the threshold");
                var rain = brain.Evaluate(block, new NPCNeeds(), new NPCPersonality(), false, true, true, 0.1f);
                Check("Rain → shelter (urgent)", rain.Activity == "Shelter", rain.ToString());

                // IELTS guided mode: a corrected/model sentence must be repeated before the next line.
                var scenario = NeighborhoodScenarios.Create("Neighbourhood Café Order"); temp.Add(scenario);
                var machine = new DialogueStateMachine();
                machine.Start(scenario, ContextSpeak.Slice.IeltsBand.Band6, true);
                for (var i = 0; i < 3; i++) machine.Answer("hello there", true);
                var gated = machine.Phase == DialoguePhase.AwaitingRepeat && machine.Index == 0;
                var skipped = machine.Skip();
                var wrong = machine.Repeat("I like trains");
                var right = machine.Repeat(machine.Target);
                Check("Guided mode enforces correction + repetition", gated && !skipped && !wrong && right && machine.Index == 1,
                    "after 3 off-task answers: " + (gated ? "repeat required" : "NOT gated") + "; skip " + (skipped ? "allowed (bad)" : "refused") + "; wrong repeat " + (wrong ? "accepted (bad)" : "refused") + "; model repeat " + (right ? "advances" : "did not advance"));
                var meaning = new DialogueStateMachine();
                meaning.Start(NeighborhoodScenarios.Create("Neighbourhood Café Order"), ContextSpeak.Slice.IeltsBand.Band6, false);
                meaning.Answer("Could I get a cappuccino please", true);
                Check("Dialogue follows meaning", meaning.Index == 1 || meaning.Phase == DialoguePhase.AwaitingRepeat, "an order in different words is accepted (phase " + meaning.Phase + ")");

                // Memory persistence round trip.
                var path = Path.Combine(Application.temporaryCachePath, "living_npcs_check.json");
                var original = NPCRelationshipMemory.FilePath;
                NPCRelationshipMemory.Use(new LivingWorldSave(), path);
                NPCRelationshipMemory.Remember("emma", "preferredDrink", "flat white");
                NPCRelationshipMemory.Relate("ted", 10, "helped with directions");
                NPCRelationshipMemory.Store(new NPCSavedState { Npc = "emma", Scene = "Check", X = 1, Y = 2, Z = 3, Hunger = 0.4f });
                NPCRelationshipMemory.Save();
                NPCRelationshipMemory.Load();
                var restored = NPCRelationshipMemory.Of("emma").Get("preferredDrink") == "flat white" && NPCRelationshipMemory.Of("ted").Relationship == 10 && NPCRelationshipMemory.State("emma", "Check")?.Z == 3;
                Check("Memory and NPC state persist (save → load)", restored, path);
                NPCRelationshipMemory.Use(null, original);

                // Assets.
                var profiles = Directory.Exists(LivingNPCBuilder.ProfileFolder) ? Directory.GetFiles(LivingNPCBuilder.ProfileFolder, "*.asset").Length : 0;
                Check("Profiles exist", profiles >= 10, profiles + " profiles (run ContextSpeak > NPC > Build Living NPC System)");
                var sets = Resources.LoadAll<NPCAnimationSet>("LivingNPC");
                Check("Animation sets", sets.Length > 0 && sets.All(x => x.Sit != null), sets.Length + " models · " + string.Join(", ", sets.Select(x => x.ModelName + (x.Sit != null ? "" : " (no sit)"))));
                Check("Door hook (tools/npc_patch.py)", File.ReadAllText("Assets/ContextSpeak/Runtime/Interaction/DoorController.cs").Contains("LivingNPC.All"), "doors open for living NPCs");
            }
            finally
            {
                LivingPlace.All.RemoveAll(p => p == null || p.name.StartsWith("check "));
                foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
            }
            return lines;
        }

        [MenuItem("ContextSpeak/Checks/Living NPC Play Mode Tests")]
        public static void PlayTests()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(LivingNPCBuilder.Scene)) { Debug.LogError("Build the neighbourhood first: ContextSpeak > NPC > Build Living NPC System."); return; }
            EditorSceneManager.OpenScene(LivingNPCBuilder.Scene);
            EditorPrefs.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(Flag, false)) return;
            EditorPrefs.SetBool(Flag, false);
            new GameObject("Living NPC play test").AddComponent<LivingNPCPlayTest>();
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        private static void Watch()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Watch; return; }
            if (!LivingNPCPlayTest.Finished) return;
            EditorApplication.update -= Watch;
            Debug.Log("Living NPC Play-mode tests finished: " + LivingNPCPlayTest.Summary + " — report: Logs/LivingNPCPlayModeReport.md");
            EditorApplication.ExitPlaymode();
        }
    }
}
