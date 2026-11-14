using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Luxury Villa — static validation of the built scene (no Play mode):
    /// art present, rooms, doors (collider, hinge, swing clear of walls), stairs and NavMesh reachability of
    /// every room on both floors, seats/fixtures/spots, lights, pool + ladder + water body, car, portal,
    /// family profiles + schedules, the 8 speaking topics. Report: Logs/LuxuryVillaValidation.md.
    /// ContextSpeak > Checks > Luxury Villa Play Mode Tests — runs VillaPlayTest in Play mode.
    [InitializeOnLoad]
    public static class LuxuryVillaChecks
    {
        private const string Flag = "ContextSpeak.VillaPlayTest";
        static LuxuryVillaChecks() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("ContextSpeak/Checks/Luxury Villa")]
        public static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var lines = Run();
            LuxuryVillaBuilder.WriteReport("LuxuryVillaValidation.md", "Luxury villa validation", lines);
            var failed = lines.Count(l => l.StartsWith("FAIL"));
            Debug.Log("Luxury villa checks: " + (lines.Count - failed) + " passed, " + failed + " failed — Logs/LuxuryVillaValidation.md\n- " + string.Join("\n- ", lines));
        }

        public static List<string> Run()
        {
            var lines = new List<string>();
            void Check(string name, bool ok, string detail) => lines.Add((ok ? "PASS " : "FAIL ") + name + " — " + detail);

            foreach (var p in new[] { "Architecture", "Interior", "Doors", "Exterior" })
                Check("Model " + p, AssetDatabase.LoadAssetAtPath<GameObject>(LuxuryVillaBuilder.Art + "LuxuryVilla_" + p + ".glb") != null, LuxuryVillaBuilder.Art + "LuxuryVilla_" + p + ".glb");
            var data = LuxuryVillaBuilder.Load();
            Check("Manifest", data != null, LuxuryVillaBuilder.Manifest);
            if (!File.Exists(LuxuryVillaBuilder.Scene)) { Check("Scene", false, "build it with ContextSpeak > Villa > Build Luxury Villa"); return lines; }
            EditorSceneManager.OpenScene(LuxuryVillaBuilder.Scene);

            var rooms = Object.FindObjectsByType<VillaRoom>(FindObjectsSortMode.None);
            Check("Rooms on 3 floors", rooms.Length >= 26 && Enumerable.Range(0, 3).All(f => rooms.Any(r => r.Floor == f)) && rooms.Count(r => r.Kind == "terrace") >= 2,
                rooms.Length + " rooms (" + string.Join(" / ", Enumerable.Range(0, 3).Select(f => rooms.Count(r => r.Floor == f) + " on floor " + (f + 1))) + ", " + rooms.Count(r => r.Kind == "terrace") + " roof terraces): " + string.Join(", ", rooms.Select(r => r.name.Replace("Room · ", ""))));

            // Doors.
            var doors = Object.FindObjectsByType<VillaDoor>(FindObjectsSortMode.None);
            var wallColliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c => c.name.Contains("Wall") || c.name.Contains("Column")).ToArray();
            var badDoors = new List<string>();
            Physics.SyncTransforms();
            foreach (var d in doors.Where(d => d.Mode == VillaDoor.Motion.Swing))
            {
                var box = d.GetComponent<BoxCollider>();
                if (box == null) { badDoors.Add(d.Id + " (no collider)"); continue; }
                var rot = d.transform.rotation * Quaternion.Euler(0, d.Angle, 0);
                var centre = d.transform.position + rot * Vector3.Scale(box.center, d.transform.lossyScale);
                var half = Vector3.Scale(box.size, d.transform.lossyScale) / 2 - Vector3.one * 0.06f; half.y = Mathf.Max(0.1f, half.y - 0.3f);
                if (Physics.OverlapBox(centre, half, rot).Any(c => wallColliders.Contains(c))) badDoors.Add(d.Id + " (open leaf hits a wall)");
            }
            Check("Doors", doors.Length >= 15 && badDoors.Count == 0, doors.Length + " doors (" + doors.Count(d => d.Lockable) + " lockable, garage " + doors.Any(d => d.Mode == VillaDoor.Motion.RollUp) + ", gate " + doors.Any(d => d.Mode == VillaDoor.Motion.Slide) + ")" + (badDoors.Count > 0 ? "; problems: " + string.Join(", ", badDoors) : ""));
            Check("Doors excluded from the NavMesh bake", doors.All(d => d.GetComponent<Unity.AI.Navigation.NavMeshModifier>() is var m && m != null && m.ignoreFromBuild), "residents path through door openings and open doors as they arrive");

            // NavMesh.
            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            Check("NavMesh baked", surface != null && surface.navMeshData != null, surface != null ? "agent '" + NavMesh.GetSettingsNameFromID(surface.agentTypeID) + "'" : "no NavMeshSurface");
            if (surface != null && surface.navMeshData != null && data != null)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
                var start = LuxuryVillaBuilder.U(data.entrance);
                var okStart = NavMesh.SamplePosition(start, out var from, 2, filter);
                var unreachable = new List<string>();
                foreach (var r in rooms)
                {
                    if (r.Kind == "garage") continue;
                    var c = r.Box.center; c.y = r.Box.min.y;
                    var path = new NavMeshPath();
                    if (!okStart || !NavMesh.SamplePosition(c, out var hit, 2.5f, filter) || !NavMesh.CalculatePath(from.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) unreachable.Add(r.name.Replace("Room · ", ""));
                }
                Check("Every room reachable from the front door", unreachable.Count == 0, unreachable.Count == 0 ? rooms.Length - 1 + " rooms" : "unreachable: " + string.Join(", ", unreachable));
                var up = new NavMeshPath();
                var bottom = LuxuryVillaBuilder.U(data.stairs.bottom); var top = LuxuryVillaBuilder.U(data.stairs.top);
                var stairsOk = NavMesh.SamplePosition(bottom, out var b, 1, filter) && NavMesh.SamplePosition(top, out var t, 1, filter) && NavMesh.CalculatePath(b.position, t.position, filter, up) && up.status == NavMeshPathStatus.PathComplete;
                Check("Stairs on the NavMesh", stairsOk, stairsOk ? "bottom → landing path " + up.corners.Length + " corners, rises " + (top.y - bottom.y).ToString("0.0") + " m" : "no path from the bottom step to the upper landing");
                var top2 = data.stairs.top2 != null ? LuxuryVillaBuilder.U(data.stairs.top2) : top;
                var up2 = new NavMeshPath();
                var stairs2 = NavMesh.SamplePosition(bottom, out var b0, 1, filter) && NavMesh.SamplePosition(top2, out var t2, 1, filter) && NavMesh.CalculatePath(b0.position, t2.position, filter, up2) && up2.status == NavMeshPathStatus.PathComplete;
                Check("Stairs to the third floor", stairs2, stairs2 ? "ground → third-floor landing, rises " + (top2.y - bottom.y).ToString("0.0") + " m" : "no path to the third floor");
                var pool = LuxuryVillaBuilder.U(data.pool.min);
                Check("Pool basin not walkable for residents", !NavMesh.SamplePosition(new Vector3((LuxuryVillaBuilder.U(data.pool.min).x + LuxuryVillaBuilder.U(data.pool.max).x) / 2, pool.y + 0.2f, (LuxuryVillaBuilder.U(data.pool.min).z + LuxuryVillaBuilder.U(data.pool.max).z) / 2), out _, 0.5f, filter), "agents walk around the pool");
            }

            // Lift.
            var lift = Object.FindFirstObjectByType<VillaElevator>();
            Check("Lift", lift != null && lift.Cab != null && lift.Floors.Length == 3 && lift.LandingL.Length == 3 && lift.LandingL.All(t => t != null) && lift.LandingR.All(t => t != null) && lift.CabDoorL != null && lift.CabDoorR != null
                    && lift.Cab.GetComponent<Rigidbody>() != null && lift.Cab.GetComponentInChildren<MeshCollider>() != null,
                lift == null ? "missing" : "floors " + string.Join(", ", lift.Floors.Select(f => f.ToString("0.0"))) + ", cab collider + kinematic body, " + Object.FindObjectsByType<VillaElevatorCall>(FindObjectsSortMode.None).Length + " call buttons, " + lift.Indicators.Length + " indicators");
            if (lift != null && lift.Cab != null)
            {
                var c = lift.Cab.TransformPoint(lift.Interior.center);
                Check("Lift cab fits a person", lift.Interior.size.x > 1.2f && lift.Interior.size.z > 1.2f && lift.Interior.size.y > 2.0f, "interior " + lift.Interior.size.ToString("0.00"));
                Check("Lift cab at a floor", lift.Floors.Any(f => Mathf.Abs(f - lift.Cab.position.y) < 0.01f), "cab floor at y " + lift.Cab.position.y.ToString("0.00"));
            }

            // Interactions.
            var seats = Object.FindObjectsByType<SeatController>(FindObjectsSortMode.None);
            var fixtures = Object.FindObjectsByType<VillaFixture>(FindObjectsSortMode.None);
            var kinds = fixtures.Select(f => f.Kind).Distinct().ToList();
            var needed = new[] { VillaFixture.Use.Exercise, VillaFixture.Use.Umbrella, VillaFixture.Use.Sleep, VillaFixture.Use.Computer, VillaFixture.Use.Wardrobe, VillaFixture.Use.Fridge, VillaFixture.Use.Cook, VillaFixture.Use.TV, VillaFixture.Use.Plants, VillaFixture.Use.Pet, VillaFixture.Use.Laundry };
            Check("Seats", seats.Length >= 20 && seats.All(s => s.GetComponent<VillaSeatLink>() != null), seats.Length + " seats linked to resident spots");
            Check("Home interactions", needed.All(kinds.Contains), "present: " + string.Join(", ", kinds) + (needed.Any(n => !kinds.Contains(n)) ? " · missing: " + string.Join(", ", needed.Where(n => !kinds.Contains(n))) : ""));
            var tv = fixtures.Where(f => f.Kind == VillaFixture.Use.TV).ToArray();
            Check("TVs have screens", tv.Length > 0 && tv.All(f => f.Screen != null), tv.Length + " TVs");
            var home = Object.FindObjectsByType<LivingPlace>(FindObjectsSortMode.None).FirstOrDefault(p => p.Id == "villa");
            Check("Resident place 'villa'", home != null && home.Count(SpotKind.Bed) >= 6 && home.Count(SpotKind.Sofa) >= 4 && home.Count(SpotKind.DiningSeat) >= 6 && home.Count(SpotKind.Stove) >= 1,
                home != null ? home.Count(SpotKind.Bed) + " beds, " + home.Count(SpotKind.Sofa) + " sofa seats, " + home.Count(SpotKind.DiningSeat) + " dining seats, " + home.Count(SpotKind.Desk) + " desks" : "missing");
            foreach (var id in new[] { "villa_garden", "villa_city" })
                Check("Place " + id, Object.FindObjectsByType<LivingPlace>(FindObjectsSortMode.None).Any(p => p.Id == id), id);

            // Lights.
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Point).ToArray();
            Check("Lights + day/night control", lights.Length >= 30 && Object.FindFirstObjectByType<VillaLighting>() != null && rooms.Count(r => r.Lights.Length > 0) >= 15, lights.Length + " point lights, " + rooms.Count(r => r.Lights.Length > 0) + " rooms with lights");
            Check("Reflection probes", Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length >= 3, "ground / upper / garden");

            // Pool / swimming.
            var bodies = Object.FindObjectsByType<WaterBody>(FindObjectsSortMode.None);
            Check("Pool water body", bodies.Length > 0, bodies.Length + " WaterBody (swimming starts automatically in deep water)");
            Check("Pool ladder exit", Object.FindObjectsByType<WaterExitPoint>(FindObjectsSortMode.None).Any(e => e.Kind == WaterExitPoint.ExitKind.Ladder), "deep-end ladder");

            // Car, portal, family, pet.
            var car = Object.FindObjectsByType<DrivableVehicle>(FindObjectsSortMode.None);
            Check("Cars (SUV + 3 sports cars)", car.Length >= 4 && car.Count(c => c.Label.Contains("sports")) >= 3,
                car.Length + " drivable: " + string.Join(", ", car.Select(c => c.Label)));
            foreach (var c in car)
            {
                var upright = Vector3.Dot(c.transform.up, Vector3.up) > 0.98f;
                var rs = c.GetComponentsInChildren<Renderer>(); var cb = rs[0].bounds; foreach (var r in rs) cb.Encapsulate(r.bounds);
                Check("Car upright: " + c.Label, upright && cb.size.y < 2.2f && Mathf.Max(cb.size.x, cb.size.z) > 3.5f, "height " + cb.size.y.ToString("0.00") + " m, length " + Mathf.Max(cb.size.x, cb.size.z).ToString("0.00") + " m");
            }
            Check("Exit to Metro City", Object.FindObjectsByType<PortalDoor>(FindObjectsSortMode.None).Any(p => p.TargetScene == "MetroCity"), "portal at the street");
            var family = Object.FindObjectsByType<LivingNPC>(FindObjectsSortMode.None);
            Check("Family (LivingNPC)", family.Length == 4 && family.All(f => f.Profile != null && f.Profile.Home == "villa" && NPCDailySchedule.FromJson(f.Profile.ScheduleJson).Blocks.Count > 0),
                string.Join(", ", family.Select(f => f.Profile != null ? f.Profile.DisplayName + " (" + NPCDailySchedule.FromJson(f.Profile.ScheduleJson).Blocks.Count + " blocks)" : f.name)));
            Check("Pet", Object.FindFirstObjectByType<VillaPet>() != null, "Mochi the cat");
            var carriers = Object.FindObjectsByType<UmbrellaCarrier>(FindObjectsSortMode.None);
            Check("Umbrellas", carriers.Length >= 5 && carriers.All(c => c.Prefab != null) && Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Pastel parasol") >= 4,
                carriers.Length + " umbrella carriers (player + residents), " + Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Pastel parasol") + " parasols");
            var birds = Object.FindObjectsByType<ContextSpeak.Life.WildBird>(FindObjectsSortMode.None).Length;
            var butterflies = Object.FindObjectsByType<ContextSpeak.Life.WildButterfly>(FindObjectsSortMode.None).Length;
            var koi = Object.FindObjectsByType<ContextSpeak.Life.KoiSchool>(FindObjectsSortMode.None).Sum(k => k.Fish.Length);
            var garden = GameObject.Find("Garden life");
            var flowerCount = garden != null ? garden.transform.Cast<Transform>().Count(t => t.GetComponent<ContextSpeak.Life.WildBird>() == null && t.GetComponent<ContextSpeak.Life.WildButterfly>() == null && t.GetComponent<ContextSpeak.Life.KoiSchool>() == null && !t.name.Contains("water")) : 0;
            Check("Flowers, beds & wildlife", flowerCount >= 60 && birds >= 8 && butterflies >= 8 && koi >= 6,
                flowerCount + " flowers in beds, " + birds + " birds, " + butterflies + " butterflies, " + koi + " koi");
            Check("Player", Object.FindFirstObjectByType<CafePlayer>() != null, "spawns at the front door");

            // Speaking topics.
            var flow = Object.FindFirstObjectByType<VillaFlow>();
            var topics = VillaFlow.Topics.Select(t => (t.id, s: VillaScenarios.Create(t.id))).ToList();
            Check("VillaFlow", flow != null, "location flow present");
            Check("8 IELTS home topics", topics.Count == 8 && topics.All(t => t.s != null && t.s.Steps.Count >= 3 && t.s.Steps.All(st => !string.IsNullOrEmpty(st.model) && !string.IsNullOrEmpty(st.meaning))),
                string.Join(", ", topics.Select(t => t.id + " (" + (t.s != null ? t.s.Steps.Count : 0) + ")")));
            var models = topics.Where(t => t.s != null).SelectMany(t => t.s.Steps).Count(st => System.Text.RegularExpressions.Regex.IsMatch(st.model.ToLowerInvariant(), st.meaning));
            var total = topics.Where(t => t.s != null).Sum(t => t.s.Steps.Count);
            Check("Model answers satisfy their own meaning patterns", models == total, models + "/" + total + " (the repeat step can always be passed by saying the model)");

            // Texture resolution of the art.
            var textures = Directory.Exists("ArtSource/LuxuryVilla/Textures") ? Directory.GetFiles("ArtSource/LuxuryVilla/Textures", "*.png").Length : 0;
            Check("Source textures", textures >= 45, textures + " PNG maps in ArtSource/LuxuryVilla/Textures (BaseColor/Roughness/Normal)");
            return lines;
        }

        [MenuItem("ContextSpeak/Checks/Luxury Villa Play Mode Tests")]
        public static void PlayTests()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(LuxuryVillaBuilder.Scene)) { Debug.LogError("Build the villa first: ContextSpeak > Villa > Build Luxury Villa."); return; }
            EditorSceneManager.OpenScene(LuxuryVillaBuilder.Scene);
            EditorPrefs.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(Flag, false)) return;
            EditorPrefs.SetBool(Flag, false);
            new GameObject("Villa play test").AddComponent<VillaPlayTest>();
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        private static void Watch()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Watch; return; }
            if (!VillaPlayTest.Finished) return;
            EditorApplication.update -= Watch;
            Debug.Log("Villa Play-mode tests finished: " + VillaPlayTest.Summary + " — report: Logs/LuxuryVillaPlayModeReport.md");
            EditorApplication.ExitPlaymode();
        }
    }
}
