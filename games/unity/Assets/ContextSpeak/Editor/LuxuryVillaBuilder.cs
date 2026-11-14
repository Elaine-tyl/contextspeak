using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Villa > Build Luxury Villa
    /// Builds Assets/ContextSpeak/Scenes/LuxuryVilla.unity from the Blender villa (tools/blender/luxury_villa.py →
    /// Assets/ContextSpeak/Art/Villa/*.glb + LuxuryVilla_manifest.json):
    ///  • the four model parts (architecture, interior, doors, exterior) with mesh colliders,
    ///  • every door as a VillaDoor (hinge pivot from Blender; swing direction checked against the walls),
    ///    garage roll-up door and sliding gate,
    ///  • rooms (VillaRoom) with their lights, VillaLighting tied to the sky clock, reflection + light probes,
    ///  • seats / beds / kitchen / TV / wardrobe / laundry / plants as SeatController / VillaFixture and as
    ///    ActivitySpots for the residents,
    ///  • the pool (water surface, WaterBody via the water setup, ladder exit) for the swimming system,
    ///  • the family car in the garage (DrivableVehicle), a street and a portal to Metro City,
    ///  • four family members (LivingNPC profiles, home = "villa", work/school = "villa_city"), a pet,
    ///  • a NavMesh for a resident-sized agent (fits 0.9 m doors; includes the staircase), VillaFlow with the
    ///    eight home speaking topics, and saves the scene into the build settings.
    public static class LuxuryVillaBuilder
    {
        public const string Scene = "Assets/ContextSpeak/Scenes/LuxuryVilla.unity";
        public const string Art = "Assets/ContextSpeak/Art/Villa/";
        public const string Manifest = Art + "LuxuryVilla_manifest.json";
        public const string AgentName = "Villa Resident";
        public const string CarModel = "Assets/_Game/Art/MeshyLibrary/Fidelity_car_20250824_texture_5bb93c86ab/Meshy_AI_Fidelity_car_20250824_texture.fbx";
        public const string CatModel = "Assets/_Game/Art/MeshyLibrary/Cat_Reference_20260717_texture_64bff816a9/Model.prefab";
        public const string RainUmbrella = Art + "RainUmbrella.glb";
        public const string Parasol = Art + "PastelParasol.glb";
        public const string CatFbx = "Assets/_Game/Art/MeshyLibrary/Cat_Reference_20260717_texture_64bff816a9/Meshy_AI_Cat_Reference_20260717_texture.fbx";

        // ------------------------------------------------------------------ manifest (JsonUtility mirror)
        [System.Serializable] public class Room { public string name; public int floor; public float[] min, max; public string kind; }
        [System.Serializable] public class Door { public string name; public float[] hinge; public float width; public string along; public int opens; public bool lockable; public string room; }
        [System.Serializable] public class Anchor { public string kind; public float[] pos; public float yaw; public string room; public float seat; public float head; }
        [System.Serializable] public class LightDef { public string kind; public float[] pos; public string room; public float[] colour; public float intensity; public float range; }
        [System.Serializable] public class Stairs { public float[] bottom, top, top2; public int risers; public float rise, run; }
        [System.Serializable] public class Elevator { public float[] floors, inside, cab_min, cab_max, hall; public string[] names; public float door_slide; public float[] call, indicator; }
        [System.Serializable] public class Pond { public float[] min, max; public float water_level; }
        [System.Serializable] public class WildlifeAreas { public float[] lawn, front, perches; }
        [System.Serializable] public class Cars { public string[] names; public float[] spots; }
        [System.Serializable] public class Umbrellas { public float[] parasols, stand; }
        [System.Serializable] public class Garage { public float[] car; public float car_yaw; }
        [System.Serializable] public class Gate { public float[] pivot, slide; }
        [System.Serializable] public class Street { public float[] gate; public float road_y; public float[] city_exit; }
        [System.Serializable] public class Pool { public float[] min, max, ladder_bottom, ladder_top, steps; public float water_level, shallow, deep; }
        [System.Serializable] public class Data
        {
            public Room[] rooms; public Door[] doors; public Anchor[] anchors; public LightDef[] lights;
            public Stairs stairs; public float[] flowerbeds; public Pond koipond; public WildlifeAreas wildlife; public Cars cars; public Elevator elevator; public Umbrellas umbrellas; public Garage garage; public Gate gate; public Street street; public Pool pool; public float[] entrance, pet;
        }

        public static Data Load()
        {
            var json = File.Exists(Manifest) ? File.ReadAllText(Manifest) : null;
            return json != null ? JsonUtility.FromJson<Data>(json) : null;
        }

        /// Blender (x right, y into the plot, z up) → Unity. glTFast mirrors X when converting to Unity's
        /// left-handed space; `Mirror` is verified against the imported door positions at build time.
        public static bool Mirror = true;
        public static Vector3 U(float[] b) => U(b[0], b[1], b.Length > 2 ? b[2] : 0);
        public static Vector3 U(float x, float y, float z) => Mirror ? new Vector3(-x, z, -y) : new Vector3(x, z, -y);
        /// Blender yaw (0 = facing +Y, CCW) → Unity forward.
        public static Vector3 F(float yaw) { var r = yaw * Mathf.Deg2Rad; return U(-Mathf.Sin(r), Mathf.Cos(r), 0).normalized; }

        [MenuItem("ContextSpeak/Villa/Build Luxury Villa")]
        public static void BuildMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = Build();
            WriteReport("LuxuryVillaBuild.md", "Luxury villa build", report);
            Debug.Log("Luxury villa built:\n- " + string.Join("\n- ", report));
        }

        public static void WriteReport(string file, string title, List<string> lines)
        {
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, file), "# " + title + "\n\n" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n- " + string.Join("\n- ", lines) + "\n");
        }

        public static List<string> Build()
        {
            var report = new List<string>();
            AssetDatabase.Refresh();
            var data = Load();
            var parts = new[] { "Architecture", "Interior", "Doors", "Exterior" }.ToDictionary(p => p, p => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "LuxuryVilla_" + p + ".glb"));
            if (data == null || parts.Values.Any(p => p == null))
            {
                report.Add("ERROR: missing " + (data == null ? Manifest : string.Join(", ", parts.Where(p => p.Value == null).Select(p => Art + "LuxuryVilla_" + p.Key + ".glb"))) + " — deploy the villa art first.");
                return report;
            }
            var surroundingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "LuxuryVilla_Surroundings.glb");
            if (surroundingsPrefab != null) parts["Surroundings"] = surroundingsPrefab;
            Missing.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Luxury villa");
            Daylight("LuxuryVilla", "E9ECEF");

            // 1. Model parts.
            var villa = new GameObject("Luxury Villa").transform;
            var roots = new Dictionary<string, GameObject>();
            foreach (var p in parts)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p.Value);
                go.name = "Villa · " + p.Key;
                go.transform.SetParent(villa, false);
                roots[p.Key] = go;
            }
            // glTF space check: the entrance door's hinge must land where the manifest says.
            var entrance = Find(roots["Doors"].transform, "Door_Entrance");
            var hingeDef = data.doors.First(d => d.name == "Door_Entrance").hinge;
            if (entrance != null)
            {
                Mirror = true; var a = (entrance.position - U(hingeDef)).magnitude;
                Mirror = false; var b = (entrance.position - U(hingeDef)).magnitude;
                Mirror = a <= b;
                report.Add("Coordinate check: entrance hinge off by " + Mathf.Min(a, b).ToString("0.000") + " m (" + (Mirror ? "X mirrored" : "X kept") + ")");
            }
            else report.Add("WARNING: Door_Entrance not found in the doors model — coordinate mirror assumed");

            // 2. Colliders + static flags. Seats and beds get colliders (the player can't walk through a
            // sofa) but are left out of the NavMesh so residents can reach the seat itself.
            var walkThroughForAgents = new[] { "Sofa", "Armchair", "Chair", "Stool", "Lounger", "Bed_", "Rug" };
            var colliders = 0;
            var visualOnly = new[] { "Trim", "Tree_", "StreetTree_", "FarTree_", "Plant_", "Shrub_", "Planter_Shrubs", "Planter_Roof_Leaves", "Curtains", "LightFixtures" };
            var plants = new List<Transform>();
            foreach (var key in roots.Keys.Where(k => k != "Doors").ToArray())
                foreach (var mf in roots[key].GetComponentsInChildren<MeshFilter>(true))
                {
                    var n = mf.transform.name;
                    if (n.StartsWith("PoolWater")) { Object.DestroyImmediate(mf.gameObject); continue; }
                    if (visualOnly.Any(v => n.StartsWith(v)))
                    {
                        mf.gameObject.isStatic = !(n.Contains("Tree_") || n.StartsWith("Plant_") || n.StartsWith("Shrub_") || n.Contains("Leaves") || n.StartsWith("Planter_Shrubs"));
                        var vm = mf.gameObject.AddComponent<NavMeshModifier>(); vm.ignoreFromBuild = true;
                        if (!mf.gameObject.isStatic) plants.Add(mf.transform);
                        if (n.Contains("Tree_"))                                                 // trunk collider only
                        { var cap = mf.gameObject.AddComponent<CapsuleCollider>(); cap.center = Vector3.up * 1.5f; cap.height = 3f; cap.radius = 0.22f; }
                        continue;
                    }
                    mf.gameObject.isStatic = true;
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colliders++;
                    var owner = mf.transform.parent != null && mf.transform.parent != roots[key].transform ? mf.transform.parent.name + "|" + n : n;
                    if (walkThroughForAgents.Any(w => n.Contains(w) || owner.Contains(w)) || n.StartsWith("Elevator") || n.StartsWith("ClosedUmbrella"))
                    { var m = mf.gameObject.AddComponent<NavMeshModifier>(); m.ignoreFromBuild = true; }
                }
            report.Add("Colliders: " + colliders + " mesh colliders (architecture, interior, exterior)");

            // 3. Doors.
            Physics.SyncTransforms();
            var walls = roots["Architecture"].GetComponentsInChildren<Collider>().Where(c => c.name.Contains("Wall") || c.name.Contains("Column")).ToArray();
            var doors = new List<VillaDoor>();
            foreach (var def in data.doors)
            {
                var t = Find(roots["Doors"].transform, def.name);
                if (t == null) { report.Add("MISSING door object " + def.name); continue; }
                var door = Leaf(t, def.name.Replace("Door_", ""), def.lockable);
                door.Prompt = Pretty(def.name.Replace("Door_", "")) + " door";
                door.Angle = (Mirror ? -1 : 1) * def.opens * 90;
                // Swing check: if the open leaf would sit inside a wall, swing the other way.
                if (Hits(t, door.Angle, walls) && !Hits(t, -door.Angle, walls)) { door.Angle = -door.Angle; report.Add("Door " + def.name + ": swing reversed (would hit a wall)"); }
                else if (Hits(t, door.Angle, walls)) report.Add("WARNING: door " + def.name + " touches a wall when open in both directions");
                doors.Add(door);
            }
            var garage = Find(roots["Doors"].transform, "GarageDoor");
            if (garage != null) { var g = Leaf(garage, "Garage", false); g.Mode = VillaDoor.Motion.RollUp; g.Rise = 2.45f; g.Prompt = "garage door"; g.Speed = 0.5f; g.Range = 3.5f; doors.Add(g); }
            var gate = Find(roots["Doors"].transform, "FrontGate");
            if (gate != null) { var g = Leaf(gate, "Gate", false); g.Mode = VillaDoor.Motion.Slide; g.SlideOffset = Vector3.Scale(U(data.gate.slide), Vector3.one); g.Prompt = "front gate"; g.Speed = 0.35f; g.Range = 3.5f; doors.Add(g); }
            report.Add("Doors: " + doors.Count + " (" + doors.Count(d => d.Lockable) + " lockable; E open/close, L lock)");

            // 3b. Lift.
            var lift = Lift(roots["Doors"].transform, data, report);

            // 4. Street outside the gate + exit to the city.
            if (!roots.ContainsKey("Surroundings"))                                                  // older art without the modelled street
            {
                Box("Street", U(0, -17, 0) + Vector3.down * 0.15f, new Vector3(60, 0.3f, 10), Hex("3B3F44"), true, villa);
                Box("Pavement", U(0, -11.15f, 0) + Vector3.down * 0.05f, new Vector3(60, 0.1f, 1.7f), Hex("B9B4AA"), true, villa);
            }
            var exitGo = new GameObject("To Metro City");
            exitGo.transform.position = U(data.street.city_exit) + Vector3.up * 0.1f;
            var exit = exitGo.AddComponent<PortalDoor>();
            exit.TargetScene = "MetroCity"; exit.Exit = true; exit.Prompt = "Go into the city"; exit.Range = 3;
            Label("METRO CITY →", U(data.street.city_exit) + Vector3.up * 2.2f, 0, 0.35f, Color.white);

            // 5. Rooms and lights.
            var lightsRoot = new GameObject("Villa lights").transform;
            var rooms = new Dictionary<string, VillaRoom>();
            var roomRoot = new GameObject("Villa rooms").transform;
            foreach (var r in data.rooms)
            {
                var room = new GameObject("Room · " + r.name).AddComponent<VillaRoom>();
                room.transform.SetParent(roomRoot, false);
                room.Kind = r.kind; room.Floor = r.floor;
                var a = U(r.min); var b = U(r.max);
                var box = new Bounds((a + b) / 2, new Vector3(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z)));
                room.Box = box;
                rooms[r.name] = room;
            }
            var garden = new List<Light>(); var pool = new List<Light>(); var lamps = new List<Light>();
            var roomLights = new Dictionary<string, List<Light>>();
            foreach (var l in data.lights)
            {
                var light = new GameObject("Light · " + l.kind + " · " + l.room).AddComponent<Light>();
                light.transform.SetParent(lightsRoot, false);
                light.transform.position = U(l.pos);
                light.type = LightType.Point;
                light.color = new Color(l.colour[0], l.colour[1], l.colour[2]);
                light.range = l.range;
                light.intensity = l.intensity * (l.kind == "pendant" ? 2.4f : l.kind == "pool" ? 3f : 1.6f);
                light.shadows = l.kind == "pendant" || l.kind == "ceiling" && (l.room == "LivingRoom" || l.room == "Kitchen") ? LightShadows.Soft : LightShadows.None;
                light.enabled = false;
                if (l.kind == "street" || l.kind == "wall") light.shadows = LightShadows.None;
                if (l.kind == "garden" || l.kind == "street" || l.kind == "wall") garden.Add(light);
                else if (l.kind == "pool") pool.Add(light);
                else if (l.kind == "lamp") lamps.Add(light);
                else { if (!roomLights.TryGetValue(l.room, out var list)) roomLights[l.room] = list = new List<Light>(); list.Add(light); }
            }
            var panels = roots["Interior"].GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("LightFixtures")).ToArray();
            foreach (var kv in rooms)
            {
                var list = roomLights.TryGetValue(kv.Key, out var l) ? l : new List<Light>();
                kv.Value.Lights = list.ToArray();
                kv.Value.BaseIntensity = list.Select(x => x.intensity).ToArray();
            }
            // The light panels are one mesh: let the living room drive it (they read as "the house lights").
            if (rooms.TryGetValue("LivingRoom", out var living)) living.Panels = panels;
            var lighting = new GameObject("Villa lighting").AddComponent<VillaLighting>();
            lighting.Garden = garden.ToArray(); lighting.Pool = pool.ToArray(); lighting.Lamps = lamps.ToArray();
            report.Add("Lights: " + data.lights.Length + " (" + roomLights.Sum(x => x.Value.Count) + " room, " + lamps.Count + " lamps, " + garden.Count + " garden, " + pool.Count + " pool) — follow the sky clock");
            Probes(villa, rooms.Values, report);

            // 6. Places for the residents.
            var home = PlaceAt("villa", PlaceKind.Home, U(data.entrance), true, true, new Bounds(U(0, 8, 3.4f), new Vector3(22.5f, 7.2f, 16.5f)), "home", "住宅", "家", "house");
            home.Entrance = Point("Entrance", home.transform, U(data.entrance), F(0));
            home.Inside = Point("Inside", home.transform, U(-1.25f, 2.5f, 0), F(0));
            var gardenPlace = PlaceAt("villa_garden", PlaceKind.Park, U(0, 17.2f, 0), false, true, new Bounds(U(0, 25, 1.5f), new Vector3(34, 3, 17)), "garden", "公园", "pool");
            gardenPlace.Entrance = Point("Entrance", gardenPlace.transform, U(8.0f, 17.6f, 0), F(0));
            gardenPlace.Waypoints = new[] { U(-12, 20, 0), U(12, 20, 0), U(-10, 30, 0), U(10, 30, 0), U(0, 29.5f, 0), U(-14, 25, 0) }
                .Select(p => Point("Waypoint", gardenPlace.transform, p, F(0))).ToArray();
            var city = PlaceAt("villa_city", PlaceKind.Street, U(data.street.city_exit), true, false, new Bounds(Vector3.zero, new Vector3(6, 3, 6)), "city", "城市", "work", "office");
            city.Entrance = Point("Entrance", city.transform, U(data.street.city_exit), F(180));

            // 7. Anchors → seats, fixtures and resident spots.
            var used = new List<(Vector3 p, string kind)>();
            int seats = 0, fixtures = 0, spots = 0;
            foreach (var a in data.anchors)
            {
                var p = U(a.pos); var f = F(a.yaw);
                var placeFor = a.room == "Garden" ? gardenPlace : home;
                bool Near(string k, float d) => used.Any(u => u.kind == k && (u.p - p).sqrMagnitude < d * d);
                switch (a.kind)
                {
                    case "Sofa": case "Seat": case "DiningSeat": case "Desk": case "Lounger":
                    {
                        var seat = Point("Seat · " + a.room, placeFor.transform, p, f).gameObject.AddComponent<SeatController>();
                        seat.SeatHeight = a.seat > 0 ? a.seat : 0.45f; seat.Group = a.room;
                        var kind = a.kind == "Sofa" ? SpotKind.Sofa : a.kind == "DiningSeat" ? SpotKind.DiningSeat : a.kind == "Desk" ? SpotKind.Desk : a.kind == "Lounger" ? SpotKind.Bench : SpotKind.Seat;
                        var spot = seat.gameObject.AddComponent<ActivitySpot>(); spot.Kind = kind; spot.Group = a.room; spot.SeatHeight = seat.SeatHeight;
                        seat.gameObject.AddComponent<VillaSeatLink>();
                        seats++; spots++;
                        break;
                    }
                    case "Bed":
                    {
                        var spot = Point("Bed · " + a.room, placeFor.transform, p, f).gameObject.AddComponent<ActivitySpot>();
                        spot.Kind = SpotKind.Bed; spot.SeatHeight = a.seat; spots++;
                        if (!Near("Bed", 1.4f)) { Fixture(VillaFixture.Use.Sleep, "bedroom", p, f, a.room, placeFor.transform); fixtures++; used.Add((p, "Bed")); }
                        break;
                    }
                    case "Stove": Fixture(VillaFixture.Use.Cook, "cooking", p, f, a.room, placeFor.transform); Spot(placeFor.transform, SpotKind.Stove, p, f); fixtures++; spots++; break;
                    case "Sink": Fixture(VillaFixture.Use.Sink, "cooking", p, f, a.room, placeFor.transform); Spot(placeFor.transform, SpotKind.Sink, p, f); fixtures++; spots++; break;
                    case "Fridge": Fixture(VillaFixture.Use.Fridge, "cooking", p, f, a.room, placeFor.transform); fixtures++; break;
                    case "Counter": Fixture(VillaFixture.Use.Snack, "cooking", p, f, a.room, placeFor.transform); Spot(placeFor.transform, SpotKind.Counter, p, f); fixtures++; spots++; break;
                    case "Computer": Fixture(VillaFixture.Use.Computer, "study", p - f * 0.75f, f, a.room, placeFor.transform); fixtures++; break;
                    case "Wardrobe": Fixture(VillaFixture.Use.Wardrobe, "bedroom", p, f, a.room, placeFor.transform); fixtures++; break;
                    case "Washbasin": Fixture(VillaFixture.Use.Washbasin, "routine", p, f, a.room, placeFor.transform); Spot(placeFor.transform, SpotKind.Sink, p, f); fixtures++; spots++; break;
                    case "Laundry": Fixture(VillaFixture.Use.Laundry, "routine", p, f, a.room, placeFor.transform); fixtures++; break;
                    case "Plants": Fixture(VillaFixture.Use.Plants, "garden", p, f, a.room, placeFor.transform); Spot(placeFor.transform, SpotKind.Viewpoint, p, f); fixtures++; spots++; break;
                    case "TV":
                    {
                        var fx = Fixture(VillaFixture.Use.TV, "leisure", p, f, a.room, placeFor.transform);
                        fx.Range = 3.5f;
                        var screen = roots["Interior"].GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("TV") && (r.bounds.center - p).sqrMagnitude < 25).OrderBy(r => (r.bounds.center - p).sqrMagnitude).FirstOrDefault();
                        if (screen != null)
                        {
                            fx.Screen = screen;
                            var glow = new GameObject("TV glow").AddComponent<Light>();
                            glow.transform.SetParent(fx.transform, false);
                            glow.transform.position = screen.bounds.center + f * 0.6f; glow.type = LightType.Point; glow.range = 4; glow.intensity = 0.8f; glow.enabled = false;
                            fx.ScreenLight = glow;
                        }
                        Spot(placeFor.transform, SpotKind.TV, p, f); fixtures++; spots++;
                        break;
                    }
                    case "Family": Spot(placeFor.transform, SpotKind.Viewpoint, p, f); spots++; break;
                    case "Exercise": Fixture(VillaFixture.Use.Exercise, "routine", p, f, a.room, placeFor.transform); fixtures++; break;
                    case "Umbrella":
                    {
                        var fx = Fixture(VillaFixture.Use.Umbrella, "", p, f, a.room, placeFor.transform);
                        fx.StandUmbrella = roots["Interior"].GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.StartsWith("ClosedUmbrella"));
                        fixtures++;
                        break;
                    }
                }
            }
            home.Collect(); gardenPlace.Collect();
            report.Add("Interactions: " + seats + " seats, " + fixtures + " fixtures (sleep, computer, wardrobe, fridge, cook, sink, wash, TV, laundry, plants), " + spots + " resident spots");

            // 8. Pool: water surface, swimming, ladder.
            var pmin = U(data.pool.min); var pmax = U(data.pool.max);
            var centre = new Vector3((pmin.x + pmax.x) / 2, data.pool.water_level, (pmin.z + pmax.z) / 2);
            var size = new Vector2(Mathf.Abs(pmax.x - pmin.x), Mathf.Abs(pmax.z - pmin.z));
            var water = WaterInteractionSetup.Water("Villa pool water", centre, size, WaterSettings.WaterType.Pool, "Assets/Art/water/PoolWater.glb", 0.6f);
            var wr = water.GetComponent<Renderer>();
            if (wr != null && wr.bounds.size.x > 0.1f)
            {
                var s = water.transform.localScale;
                water.transform.localScale = new Vector3(s.x * size.x / wr.bounds.size.x, s.y, s.z * size.y / wr.bounds.size.z);
                water.transform.position += centre - new Vector3(wr.bounds.center.x, centre.y, wr.bounds.center.z);
            }
            water.transform.SetParent(villa, true);
            var top = U(data.pool.ladder_top); var bottom = U(data.pool.ladder_bottom);
            var inward = new Vector3(bottom.x - top.x, 0, bottom.z - top.z).normalized;
            var edge = new Vector3((top.x + bottom.x) / 2, 0, (top.z + bottom.z) / 2);
            SwimmingSetup.Ladder(edge, Quaternion.LookRotation(inward).eulerAngles.y, data.pool.deep - 0.1f, -data.pool.water_level).name = "Villa pool ladder (deep end)";
            var poolNotes = WaterInteractionSetup.Setup();
            report.Add("Pool: " + size.x.ToString("0.0") + " × " + size.y.ToString("0.0") + " m, " + data.pool.shallow + "–" + data.pool.deep + " m deep, water at " + data.pool.water_level + " m, ladder at the deep end; water setup: " + poolNotes.Count(n => n.Contains("Water bod")) + " body note(s)");

            // 9. Cars: family SUV + 3 sports cars (tools/blender/villa_cars.py → Art/Villa/Cars/*.glb, nose = +Z,
            // origin at the wheels' contact plane). Older art: the original Meshy FBX, with its import rotation kept.
            var cars = new List<DrivableVehicle>();
            var carNames = data.cars != null && data.cars.names != null ? data.cars.names : new[] { "FamilySUV" };
            var parking = data.cars != null && data.cars.spots != null ? data.cars.spots : new[] { data.garage.car[0], data.garage.car[1], 180f };
            for (var i = 0; i < carNames.Length && i * 3 + 2 < parking.Length; i++)
            {
                var name = carNames[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Cars/" + name + ".glb");
                var legacy = prefab == null && name == "FamilySUV";
                if (legacy) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarModel);
                if (prefab == null) { Missing.Add(Art + "Cars/" + name + ".glb"); continue; }
                var sports = name.StartsWith("SportsCar");
                var root = new GameObject(sports ? name.Replace("SportsCar_", "") + " sports car" : "Family car");
                root.transform.position = U(parking[i * 3], parking[i * 3 + 1], 0);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.transform.SetParent(root.transform, false);
                if (legacy)
                {
                    // Keep the FBX's own axis conversion (Z-up file): add the quarter turn on top of it.
                    var lb = AirportKit.Measure(model);
                    if (lb.size.x > lb.size.z) model.transform.localRotation = Quaternion.Euler(0, 90, 0) * model.transform.localRotation;
                }
                var bounds = AirportKit.Measure(model);
                var length = Mathf.Max(bounds.size.x, bounds.size.z);
                if (length > 0.01f) model.transform.localScale *= (sports ? 4.55f : 4.6f) / length;
                bounds = AirportKit.Measure(model);
                model.transform.position += new Vector3(root.transform.position.x - bounds.center.x, root.transform.position.y - bounds.min.y, root.transform.position.z - bounds.center.z);
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
                root.transform.rotation = Quaternion.LookRotation(F(parking[i * 3 + 2]));
                var car = root.AddComponent<DrivableVehicle>();
                car.Label = sports ? name.Replace("SportsCar_", "").ToLowerInvariant() + " sports car" : "family car";
                car.RightHandDrive = true;
                if (sports) { car.MaxSpeed = 30; car.Acceleration = 8.5f; car.BrakeDeceleration = 13; car.MaxSteer = 32; car.SteerSpeed = 170; }
                else car.MaxSpeed = 16;
                var obstacle = root.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.carving = true;
                var b2 = AirportKit.Measure(root);
                obstacle.center = root.transform.InverseTransformPoint(b2.center);
                obstacle.size = new Vector3(Mathf.Min(b2.size.x, b2.size.z) + 0.1f, b2.size.y, Mathf.Max(b2.size.x, b2.size.z) + 0.1f);
                cars.Add(car);
            }
            report.Add("Cars: " + cars.Count + " drivable — " + string.Join(", ", cars.Select(c => c.Label + " @ " + c.transform.position.ToString("0.0"))) + " (E in / out; sports cars: 30 m/s top speed, 8.5 m/s² acceleration)");

            // 10. Player, flow, family, pet.
            var (player, camera) = Player(U(data.entrance) + F(180) * 2.5f + Vector3.up * 0.05f, Quaternion.LookRotation(F(0)).eulerAngles.y);
            camera.farClipPlane = 300;
            var systems = new GameObject("Systems");
            var flow = systems.AddComponent<VillaFlow>();
            var family = Family(report);
            var npcs = new List<(GameObject go, NPCProfile profile)>();
            foreach (var (profile, start) in family)
            {
                var npc = Npc(profile.Id, profile.DisplayName, profile.Occupation, profile.Model, U(start), 0);
                npcs.Add((npc.gameObject, profile));
            }

            // NavMesh for a resident-sized agent (the default Humanoid radius 0.5 m cannot pass a 0.9 m door).
            var agentType = EnsureAgentType(report);
            foreach (var d in doors) { var m = d.gameObject.AddComponent<NavMeshModifier>(); m.ignoreFromBuild = true; }
            if (lift != null) foreach (var t in new[] { lift.Cab }.Concat(lift.LandingL).Concat(lift.LandingR)) if (t != null && t.GetComponent<NavMeshModifier>() == null) { var m = t.gameObject.AddComponent<NavMeshModifier>(); m.ignoreFromBuild = true; }
            var surface = new GameObject("Villa NavMesh").AddComponent<NavMeshSurface>();
            surface.agentTypeID = agentType;
            surface.collectObjects = CollectObjects.All;
            Finish(player, camera, flow, "Luxury villa", "LuxuryVilla");
            foreach (var (go, profile) in npcs)
            {
                go.GetComponent<NPCController>().enabled = false;
                var agent = go.GetComponent<NavMeshAgent>();
                if (agent != null) { agent.agentTypeID = agentType; agent.radius = 0.25f; agent.enabled = true; }
                go.AddComponent<NPCNavigationController>();
                go.AddComponent<NPCAnimationController>();
                go.AddComponent<LivingNPC>().Profile = profile;
            }
            var pet = Pet(U(data.pet), agentType, gardenPlace, home, report);
            PlaceUmbrellas(data, player, npcs.Select(n => n.go), villa, report);
            var world = new GameObject("Living world");
            world.AddComponent<NPCLifeSimulationManager>();
            world.AddComponent<NPCEventDirector>();
            world.AddComponent<NPCDebugInspector>();
            var director = systems.AddComponent<VillaDirector>();
            director.Cars = cars.ToArray();
            var sky = SkyInstaller.Install();
            if (sky != null) { sky.SetTime(10.5f); EditorUtility.SetDirty(sky); }
            // Realistic lighting stack shared with the other locations: HDRI sky, SSAO renderer, ACES post,
            // day/night, weather, wind, Performance/High/Ultra presets (F3 environment panel).
            try { EnvironmentSetup.ConfigureCurrentScene(false); report.Add("Environment: realistic lighting stack applied (HDRI sky, SSAO renderer, ACES post, day/night, weather, wind, quality presets; F3 panel)"); }
            catch (System.Exception e) { report.Add("WARNING: environment setup failed: " + e.Message); }
            VillaPost(report);
            Glass(roots, report);
            Vegetation(roots, plants, report);
            Garden(data, villa, report);
            LightSwitches(data, rooms, report);
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
            SwimmingSetup.Setup(false);

            // NavMesh coverage: entrance → every room (both floors, via the stairs).
            NavMesh.SamplePosition(home.Door, out var from, 2, new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas });
            var unreachable = new List<string>();
            foreach (var r in rooms)
            {
                if (r.Key == "Garage") continue;
                var c = r.Value.Box.center; c.y = r.Value.Box.min.y;
                var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(c, out var hit, 2.5f, filter) || !NavMesh.CalculatePath(from.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) unreachable.Add(r.Key);
            }
            report.Add("NavMesh (agent '" + AgentName + "', r 0.25): " + (unreachable.Count == 0 ? "every room reachable from the front door, upstairs via the staircase" : "NOT reachable: " + string.Join(", ", unreachable)));

            LifeBuild.Save(scene, Scene);
            report.Add("Scene: " + Scene + " (in build settings); family: " + string.Join(", ", family.Select(f => f.profile.DisplayName)) + "; pet: " + (pet != null ? pet.name : "none") + "; starts at 17:30");
            if (Missing.Count > 0) report.AddRange(Missing.Select(m => "MISSING: " + m));
            return report;
        }

        /// The lift: cab (+ its door leaves parented to it), landing doors per floor, call buttons, indicators.
        private static VillaElevator Lift(Transform doorsRoot, Data data, List<string> report)
        {
            var e = data.elevator;
            var cab = Find(doorsRoot, "Elevator_Cab");
            if (e == null || cab == null) { report.Add("MISSING: lift (Elevator_Cab / manifest)"); return null; }
            var go = new GameObject("Villa lift");
            var lift = go.AddComponent<VillaElevator>();
            lift.Cab = cab;
            foreach (var t in cab.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            var mf = cab.GetComponentInChildren<MeshFilter>();
            if (mf != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
            var rb = cab.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            Transform DoorLeaf(string name, Transform parent)
            {
                var t = Find(doorsRoot, name);
                if (t == null) { report.Add("MISSING lift part " + name); return null; }
                foreach (var x in t.GetComponentsInChildren<Transform>()) x.gameObject.isStatic = false;
                if (parent != null) t.SetParent(parent, true);
                var r = t.GetComponentsInChildren<Renderer>();
                if (r.Length > 0)
                {
                    var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds);
                    var box = t.gameObject.AddComponent<BoxCollider>();
                    box.center = t.InverseTransformPoint(b.center);
                    var sz = t.InverseTransformVector(b.size); box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                }
                return t;
            }
            lift.CabDoorL = DoorLeaf("Elevator_CabDoorL", cab);
            lift.CabDoorR = DoorLeaf("Elevator_CabDoorR", cab);
            var n = e.floors.Length;
            lift.LandingL = Enumerable.Range(0, n).Select(i => DoorLeaf("Elevator_Door_F" + i + "_L", null)).ToArray();
            lift.LandingR = Enumerable.Range(0, n).Select(i => DoorLeaf("Elevator_Door_F" + i + "_R", null)).ToArray();
            lift.Floors = e.floors.ToArray();
            lift.Names = e.names;
            lift.DoorSlide = e.door_slide;
            lift.OpenDirection = U(0, -1, 0);                      // left leaves slide towards −Y (Blender)
            // Interior / doorway boxes in cab-local space.
            var a = cab.InverseTransformPoint(U(e.cab_min[0], e.cab_min[1], 0)); var b2 = cab.InverseTransformPoint(U(e.cab_max[0], e.cab_max[1], 2.4f));
            var interior = new Bounds((a + b2) / 2, new Vector3(Mathf.Abs(a.x - b2.x), Mathf.Abs(a.y - b2.y), Mathf.Abs(a.z - b2.z)));
            interior.Expand(new Vector3(-0.1f, 0, -0.1f));
            lift.Interior = interior;
            var d0 = cab.InverseTransformPoint(U(4.6f, 3.0f, 0)); var d1 = cab.InverseTransformPoint(U(5.4f, 4.0f, 2.1f));
            lift.Doorway = new Bounds((d0 + d1) / 2, new Vector3(Mathf.Abs(d0.x - d1.x), Mathf.Abs(d0.y - d1.y), Mathf.Abs(d0.z - d1.z)));
            var hall = U(e.hall[0], e.hall[1], 0); var door = U(5.1f, e.hall[1], 0);
            var facing = Quaternion.LookRotation((door - hall).normalized).eulerAngles.y;
            var indicators = new List<TextMesh>();
            for (var i = 0; i < n; i++)
            {
                var ip = U(e.indicator[i * 3], e.indicator[i * 3 + 1], e.indicator[i * 3 + 2]) + (hall - door).normalized * 0.04f;
                indicators.Add(Label((i + 1).ToString(), ip, facing, 0.12f, new Color(1f, 0.62f, 0.25f)));
                var cp = U(e.call[i * 3], e.call[i * 3 + 1], e.call[i * 3 + 2]);
                var call = new GameObject("Lift call · floor " + (i + 1)).AddComponent<VillaElevatorCall>();
                call.transform.SetParent(go.transform, false);
                call.transform.position = cp;
                call.Floor = i; call.Lift = lift; call.Prompt = "Call the lift"; call.Range = 1.8f;
            }
            lift.Indicators = indicators.ToArray();
            report.Add("Lift: " + n + " floors (" + string.Join(" / ", e.floors.Select(f => f.ToString("0.0") + " m")) + "), cab " + interior.size.x.ToString("0.0") + " × " + interior.size.z.ToString("0.0") + " m, sliding cab + landing doors, call buttons and floor indicators; keys 1–3 inside");
            return lift;
        }

        private static void PlaceUmbrellas(Data data, CafePlayer player, IEnumerable<GameObject> residents, Transform villa, List<string> report)
        {
            var rain = AssetDatabase.LoadAssetAtPath<GameObject>(RainUmbrella);
            var parasol = AssetDatabase.LoadAssetAtPath<GameObject>(Parasol);
            if (rain == null) Missing.Add(RainUmbrella + " — no umbrellas to carry");
            else
            {
                player.gameObject.AddComponent<UmbrellaCarrier>().Prefab = rain;
                foreach (var r in residents) { var c = r.AddComponent<UmbrellaCarrier>(); c.Prefab = rain; c.Auto = true; }
            }
            var placed = 0;
            if (parasol == null) Missing.Add(Parasol + " — no parasols");
            else if (data.umbrellas != null && data.umbrellas.parasols != null)
                for (var i = 0; i + 2 < data.umbrellas.parasols.Length; i += 3)
                {
                    var p = U(data.umbrellas.parasols[i], data.umbrellas.parasols[i + 1], data.umbrellas.parasols[i + 2]);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(parasol);
                    go.name = "Pastel parasol";
                    go.transform.SetParent(villa, true);
                    go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, i * 37 % 360, 0));
                    var pole = go.AddComponent<CapsuleCollider>(); pole.center = Vector3.up * 1.1f; pole.height = 2.2f; pole.radius = 0.12f;
                    var m = go.AddComponent<NavMeshModifier>(); m.ignoreFromBuild = false;
                    placed++;
                }
            report.Add("Umbrellas: " + placed + " pastel parasols (pool deck, roof terraces); rain umbrella in the stand by the front door (E take / return), residents open one outdoors in the rain");
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) ?? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(name));

        private static string Pretty(string s) => System.Text.RegularExpressions.Regex.Replace(s, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();

        private static VillaDoor Leaf(Transform t, string id, bool lockable)
        {
            foreach (var x in t.GetComponentsInChildren<Transform>()) x.gameObject.isStatic = false;
            var renderers = t.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = t.InverseTransformPoint(b.center);
            var s = t.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            var door = t.gameObject.AddComponent<VillaDoor>();
            door.Id = id; door.Lockable = lockable; door.Range = 2.0f;
            return door;
        }

        /// Villa-specific grade on top of the shared environment volume: gentle bloom for the warm interior
        /// lights, neutral-warm white balance, mild contrast. No blur / no DOF in gameplay; a separate
        /// cinematic profile (DOF + stronger vignette) is saved for screenshots and cut-scenes.
        private static void VillaPost(List<string> report)
        {
            var folder = Art + "Lighting/";
            Directory.CreateDirectory(folder);
            var play = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var bloom = play.Add<UnityEngine.Rendering.Universal.Bloom>(); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.22f); bloom.scatter.Override(0.6f);
            play.Add<UnityEngine.Rendering.Universal.WhiteBalance>().temperature.Override(3f);
            var adj = play.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(); adj.contrast.Override(8f); adj.saturation.Override(2f); adj.postExposure.Override(0.1f);
            Replace(play, folder + "VillaGameplayPost.asset");
            var volume = new GameObject("Villa post (gameplay)").AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true; volume.priority = 45; volume.sharedProfile = play;
            var cine = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var dof = cine.Add<UnityEngine.Rendering.Universal.DepthOfField>(); dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh); dof.focusDistance.Override(6f); dof.aperture.Override(4f);
            var vig = cine.Add<UnityEngine.Rendering.Universal.Vignette>(); vig.intensity.Override(0.22f);
            Replace(cine, folder + "VillaCinematicPost.asset");
            var cineVolume = new GameObject("Villa post (cinematic — enable for screenshots)").AddComponent<UnityEngine.Rendering.Volume>();
            cineVolume.isGlobal = true; cineVolume.priority = 50; cineVolume.sharedProfile = cine; cineVolume.weight = 0; cineVolume.enabled = false;
            report.Add("Post: gameplay profile " + folder + "VillaGameplayPost.asset (bloom 0.22, WB +3, contrast +8); cinematic profile VillaCinematicPost.asset (off by default)");
        }

        /// Architectural glass for URP: transparent Lit with specular kept (Fresnel reflections stay visible),
        /// very smooth, faintly green-blue tint; the gold glass of the stair tower keeps a warm tint.
        private static void Glass(Dictionary<string, GameObject> roots, List<string> report)
        {
            var folder = Art + "Materials/";
            Directory.CreateDirectory(folder);
            Material Make(string name, Color tint)
            {
                var path = folder + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_AlphaClip", 0);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_BlendModePreserveSpecular", 1); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 2);
                m.SetColor("_BaseColor", tint); m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", 0.96f);
                m.SetFloat("_SpecularHighlights", 1); m.SetFloat("_EnvironmentReflections", 1);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
                EditorUtility.SetDirty(m);
                return m;
            }
            var clear = Make("Villa_ArchitecturalGlass", new Color(0.78f, 0.86f, 0.86f, 0.14f));
            var gold = Make("Villa_GoldGlass", new Color(0.92f, 0.76f, 0.48f, 0.3f));
            var count = 0;
            foreach (var root in roots.Values)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials; var changed = false;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (mats[i].name.StartsWith("Villa_GoldGlass")) { mats[i] = gold; changed = true; }
                        else if (mats[i].name.StartsWith("Villa_Glass")) { mats[i] = clear; changed = true; }
                    }
                    if (changed) { r.sharedMaterials = mats; count++; }
                }
            AssetDatabase.SaveAssets();
            report.Add("Glass: " + count + " renderers use " + folder + "Villa_ArchitecturalGlass.mat (URP Lit transparent, preserve-specular Fresnel, smoothness 0.96) / Villa_GoldGlass.mat");
        }

        /// Real vegetation where the project has it (the realistic-world prefabs with wind shaders): broadleaf
        /// trees replace the Blender trees, flowers fill the beds. Blender plants (tropical paddle leaves,
        /// shrubs) stay and sway with VillaWind. Nothing is hidden behind fog.
        private static void Vegetation(Dictionary<string, GameObject> roots, List<Transform> plants, List<string> report)
        {
            Dictionary<string, GameObject> prefabs = null;
            try { prefabs = EnvironmentSetup.EnsureVegetationPrefabs(); } catch (System.Exception e) { report.Add("NOTE: vegetation prefabs unavailable (" + e.Message + ")"); }
            var replaced = 0; var flowers = 0;
            GameObject Pick(params string[] names) { foreach (var n in names) if (prefabs != null && prefabs.TryGetValue(n, out var g) && g != null) return g; return null; }
            var treePrefab = Pick("BroadleafTree", "OakTree");
            var rng = new System.Random(11);
            foreach (var t in plants.ToArray())
            {
                if (t == null || !(t.name.StartsWith("Tree_") || t.name.StartsWith("StreetTree_"))) continue;
                if (treePrefab == null) break;
                var b = t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>().bounds : new Bounds(t.position, Vector3.one * 6);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
                go.name = "Real " + t.name;
                go.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0, rng.Next(360), 0));
                var mb = AirportKit.Measure(go);
                if (mb.size.y > 0.1f) go.transform.localScale *= Mathf.Clamp(b.size.y / mb.size.y, 0.2f, 3f);
                go.transform.SetParent(t.parent, true);
                var cap = go.AddComponent<CapsuleCollider>(); cap.center = Vector3.up * 1.5f / go.transform.localScale.y; cap.height = 3f / go.transform.localScale.y; cap.radius = 0.22f / go.transform.localScale.x;
                t.gameObject.SetActive(false);
                plants.Remove(t);
                replaced++;
            }
            var flowerNames = new[] { "TropicalFlowers", "BlossomA", "BlossomC", "PeachRose", "Jasmine", "Irises", "Allium" };
            foreach (var t in plants.Where(p => p != null && (p.name.StartsWith("Shrub_") || p.name.StartsWith("Planter_Shrubs"))).ToArray())
            {
                for (var k = 0; k < 4; k++)
                {
                    var f = Pick(flowerNames[(k + flowers) % flowerNames.Length]);
                    if (f == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(f);
                    go.transform.position = t.position + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 1.4f;
                    go.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
                    go.transform.SetParent(t.parent, true);
                    flowers++;
                }
            }
            var wind = new GameObject("Villa wind (Blender plants)").AddComponent<VillaWind>();
            wind.Plants = plants.Where(p => p != null && p.gameObject.activeSelf).ToArray();
            report.Add("Vegetation: " + replaced + " trees replaced with the project's realistic tree prefab" + (treePrefab != null ? " (" + treePrefab.name + ")" : " (none found — Blender trees kept)") + ", "
                       + flowers + " flower prefabs in the beds, " + wind.Plants.Length + " Blender plants (tropical leaves, shrubs) swaying with VillaWind");
        }

        /// Flower beds planted with the realistic flower prefabs, a koi pond with fish, butterflies over the
        /// beds and songbirds on the lawns (WildlifeTools).
        private static void Garden(Data data, Transform villa, List<string> report)
        {
            var root = new GameObject("Garden life").transform;
            root.SetParent(villa, false);
            var r = new System.Random(2026);
            var blooms = new List<Vector3>();
            var beds = 0;
            if (data.flowerbeds != null)
                for (var i = 0; i + 4 < data.flowerbeds.Length; i += 5)
                {
                    var a = U(data.flowerbeds[i], data.flowerbeds[i + 1], data.flowerbeds[i + 4]); var b = U(data.flowerbeds[i + 2], data.flowerbeds[i + 3], data.flowerbeds[i + 4]);
                    var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x) + 0.1f, Mathf.Min(a.z, b.z) + 0.1f, Mathf.Max(a.x, b.x) - 0.1f, Mathf.Max(a.z, b.z) - 0.1f);
                    blooms.AddRange(WildlifeTools.PlantBed(rect, a.y, 5f, root, r));
                    beds++;
                }
            var notes = new List<string> { blooms.Count + " flowers in " + beds + " beds" };
            if (data.koipond != null && data.koipond.min != null)
            {
                var a = U(data.koipond.min); var b = U(data.koipond.max);
                var pondMin = Vector3.Min(a, b); var pondMax = Vector3.Max(a, b);
                var water = WaterInteractionSetup.Water("Koi pond water", new Vector3((pondMin.x + pondMax.x) / 2, data.koipond.water_level, (pondMin.z + pondMax.z) / 2),
                    new Vector2(pondMax.x - pondMin.x, pondMax.z - pondMin.z), WaterSettings.WaterType.Pond, "", 0);
                water.transform.SetParent(root, true);
                var volume = new Bounds((pondMin + pondMax) / 2, pondMax - pondMin);
                notes.Add(WildlifeTools.Fish(9, volume, root, r) + " koi in the pond");
            }
            Bounds Area(float[] f, float y) { var a = U(f[0], f[1], y); var b = U(f[2], f[3], y); var bb = new Bounds(a, Vector3.zero); bb.Encapsulate(b); return bb; }
            var perches = new List<Vector3>();
            if (data.wildlife != null && data.wildlife.perches != null)
                for (var i = 0; i + 2 < data.wildlife.perches.Length; i += 3) perches.Add(U(data.wildlife.perches[i], data.wildlife.perches[i + 1], data.wildlife.perches[i + 2]));
            if (data.wildlife != null && data.wildlife.lawn != null)
            {
                var lawn = Area(data.wildlife.lawn, 0);
                notes.Add(WildlifeTools.Birds(new[] { "Bird_Sparrow", "Bird_Bluebird", "Bird_Sparrow" }, 7, lawn, perches.ToArray(), false, 0.6f, 5, root, r) + " songbirds in the garden");
                notes.Add(WildlifeTools.Butterflies(10, blooms.ToArray(), lawn, root, r) + " butterflies");
            }
            if (data.wildlife != null && data.wildlife.front != null)
                notes.Add(WildlifeTools.Birds(new[] { "Bird_Sparrow" }, 3, Area(data.wildlife.front, 0), perches.ToArray(), false, 0.6f, 5, root, r) + " sparrows on the front lawn");
            report.Add("Garden life: " + string.Join(", ", notes) + (WildlifeTools.Model("Crab") == null ? " (MISSING Art/Fauna models)" : ""));
        }

        /// A light switch beside the first door of every room: E cycles auto → on → off (saved).
        private static void LightSwitches(Data data, Dictionary<string, VillaRoom> rooms, List<string> report)
        {
            var count = 0; var done = new HashSet<string>();
            foreach (var d in data.doors)
            {
                if (string.IsNullOrEmpty(d.room) || done.Contains(d.room) || !rooms.TryGetValue(d.room, out var room)) continue;
                var s = d.along == "+x" || d.along == "+y" ? 1 : -1;
                var bx = d.hinge[0]; var by = d.hinge[1];
                float[] p = d.along.EndsWith("x") ? new[] { Mathf.Max(bx, bx + d.width * s) + 0.16f, by - 0.1f, d.hinge[2] + 1.09f } : new[] { bx - 0.1f, Mathf.Max(by, by + d.width * s) + 0.16f, d.hinge[2] + 1.09f };
                var sw = new GameObject("Light switch · " + d.room).AddComponent<VillaLightSwitch>();
                sw.transform.position = U(p);
                sw.Room = room; sw.Prompt = "lights"; sw.Range = 1.6f;
                done.Add(d.room); count++;
            }
            report.Add("Light switches: " + count + " rooms (E: auto → on → off, saved)");
        }

        private static bool Hits(Transform leaf, float angle, Collider[] walls)
        {
            var box = leaf.GetComponent<BoxCollider>();
            if (box == null) return false;
            var rotation = leaf.rotation * Quaternion.Euler(0, angle, 0);
            var local = Vector3.Scale(box.center, leaf.lossyScale);
            var half = Vector3.Scale(box.size, leaf.lossyScale) / 2 - Vector3.one * 0.06f;
            half.y = Mathf.Max(0.1f, half.y - 0.3f);
            // The leaf runs away from the hinge along its longest horizontal axis: drop the 0.3 m next to the
            // hinge (it always touches the jamb/wall it is hung on) and test the swinging part only.
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z)) { var t = Mathf.Min(0.3f, half.x * 0.6f); half.x -= t / 2; local.x += Mathf.Sign(local.x) * t / 2; }
            else { var t = Mathf.Min(0.3f, half.z * 0.6f); half.z -= t / 2; local.z += Mathf.Sign(local.z) * t / 2; }
            var centre = leaf.position + rotation * local;
            foreach (var hit in Physics.OverlapBox(centre, half, rotation))
                if (walls.Contains(hit)) return true;
            return false;
        }

        private static LivingPlace PlaceAt(string id, PlaceKind kind, Vector3 position, bool indoor, bool visible, Bounds worldArea, params string[] aliases)
        {
            var go = new GameObject("Place · " + id);
            go.transform.position = position;
            var place = go.AddComponent<LivingPlace>();
            place.Id = id; place.Kind = kind; place.Indoor = indoor; place.InteriorVisible = visible; place.Aliases = aliases;
            place.Area = new Bounds(worldArea.center - position, worldArea.size);
            return place;
        }

        private static Transform Point(string name, Transform parent, Vector3 world, Vector3 forward)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.position = world;
            t.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0 ? forward : Vector3.forward);
            return t;
        }

        private static ActivitySpot Spot(Transform parent, SpotKind kind, Vector3 world, Vector3 forward)
        {
            var spot = Point(kind.ToString(), parent, world, forward).gameObject.AddComponent<ActivitySpot>();
            spot.Kind = kind;
            return spot;
        }

        private static VillaFixture Fixture(VillaFixture.Use use, string topic, Vector3 world, Vector3 forward, string room, Transform parent)
        {
            var fx = Point(use + " · " + room, parent, world, forward).gameObject.AddComponent<VillaFixture>();
            fx.Kind = use; fx.Topic = topic; fx.Prompt = use.ToString(); fx.Range = 1.9f;
            return fx;
        }

        private static void Probes(Transform villa, IEnumerable<VillaRoom> rooms, List<string> report)
        {
            var probeRoot = new GameObject("Villa probes").transform;
            foreach (var (name, centre, size) in new[] { ("Ground floor", new Vector3(0, 1.5f, -8), new Vector3(23, 3.4f, 17)), ("Upper floor", new Vector3(0, 4.9f, -8), new Vector3(23, 3.2f, 17)), ("Garden", new Vector3(0, 2, -25), new Vector3(34, 6, 18)) })
            {
                var probe = new GameObject("Reflection probe · " + name).AddComponent<ReflectionProbe>();
                probe.transform.SetParent(probeRoot, false);
                probe.transform.position = centre;
                probe.size = size; probe.boxProjection = name != "Garden";
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;      // re-rendered by VillaLighting as the hour changes
                probe.resolution = 128;
            }
            var group = new GameObject("Light probes").AddComponent<LightProbeGroup>();
            group.transform.SetParent(probeRoot, false);
            var positions = new List<Vector3>();
            foreach (var r in rooms)
                for (var i = 0; i < 2; i++)
                    for (var j = 0; j < 2; j++)
                        positions.Add(new Vector3(Mathf.Lerp(r.Box.min.x + 0.4f, r.Box.max.x - 0.4f, i), r.Box.min.y + 1.2f, Mathf.Lerp(r.Box.min.z + 0.4f, r.Box.max.z - 0.4f, j)));
            group.probePositions = positions.ToArray();
            report.Add("Probes: 3 reflection probes (box-projected indoors), " + positions.Count + " light probes (bake lighting to use them)");
        }

        /// The family: profiles (kept if already edited) + where they start (Blender coordinates).
        private static List<(NPCProfile profile, float[] start)> Family(List<string> report)
        {
            Directory.CreateDirectory(LivingNPCBuilder.ProfileFolder);
            string J(params (float s, float e, string a, string l)[] blocks) =>
                "[\n" + string.Join(",\n", blocks.Select(b => "  { \"startHour\": " + b.s.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"endHour\": " + b.e.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"activity\": \"" + b.a + "\", \"location\": \"" + b.l + "\" }")) + "\n]";
            var people = new[]
            {
                (id: "villa_dad", name: "Michael Lee", job: "Architect", model: "Elliot", age: AgeGroup.Adult, work: "villa_city", band: 8, accent: EnglishAccent.Australian, start: new[] { 0f, 11f, 0f },
                    sched: J((0, 6.5f, "Sleep", "villa"), (6.5f, 7.5f, "Breakfast", "villa"), (7.5f, 8.5f, "Commute", "villa_city"), (8.5f, 17, "Work", "villa_city"), (17, 18, "Relax", "villa"),
                             (18, 19.5f, "Dinner", "villa"), (19.5f, 22.5f, "Relax", "villa"), (22.5f, 24, "Sleep", "villa")),
                    greet: new[] { "Hey, you're home!", "Evening!" }, talk: new[] { "Long day at the office.", "Shall we eat in the garden tonight?" }),
                (id: "villa_mum", name: "Grace Lee", job: "Translator (works from home)", model: "CafeCharm", age: AgeGroup.Adult, work: "villa", band: 9, accent: EnglishAccent.British, start: new[] { 7.5f, 12.9f, 0f },
                    sched: J((0, 6.5f, "Sleep", "villa"), (6.5f, 8, "Breakfast", "villa"), (8, 12, "Work", "villa"), (12, 13, "Lunch", "villa"), (13, 15, "Work", "villa"),
                             (15, 16.5f, "Wander", "villa_garden"), (16.5f, 18, "Relax", "villa"), (18, 19.5f, "Dinner", "villa"), (19.5f, 23, "Relax", "villa"), (23, 24, "Sleep", "villa")),
                    greet: new[] { "Hi, sweetheart!", "Welcome home!" }, talk: new[] { "I've nearly finished this translation.", "Dinner's at six." }),
                (id: "villa_daughter", name: "Chloe Lee", job: "High-school student", model: "Academy", age: AgeGroup.Teen, work: "villa_city", band: 7, accent: EnglishAccent.Australian, start: new[] { -7.5f, 12.5f, 3.4f },
                    sched: J((0, 7, "Sleep", "villa"), (7, 7.75f, "Breakfast", "villa"), (7.75f, 8.25f, "Commute", "villa_city"), (8.25f, 15.5f, "Work", "villa_city"), (15.5f, 17, "Wander", "villa_garden"),
                             (17, 18, "Relax", "villa"), (18, 19.5f, "Dinner", "villa"), (19.5f, 22.5f, "Relax", "villa"), (22.5f, 24, "Sleep", "villa")),
                    greet: new[] { "Hey!", "Oh, hi!" }, talk: new[] { "Can we go swimming later?", "I've got so much homework." }),
                (id: "villa_grandpa", name: "Henry Lee", job: "Retired teacher", model: "OldCardigan", age: AgeGroup.Senior, work: "", band: 8, accent: EnglishAccent.British, start: new[] { -10f, 20f, 0f },
                    sched: J((0, 6, "Sleep", "villa"), (6, 7.5f, "Breakfast", "villa"), (7.5f, 11, "Wander", "villa_garden"), (11, 12.5f, "Relax", "villa"), (12.5f, 13.5f, "Lunch", "villa"),
                             (13.5f, 16, "Rest", "villa"), (16, 18, "Wander", "villa_garden"), (18, 19.5f, "Dinner", "villa"), (19.5f, 21.5f, "Relax", "villa"), (21.5f, 24, "Sleep", "villa")),
                    greet: new[] { "Ah, there you are!", "Good to see you." }, talk: new[] { "The roses are coming along nicely.", "In my day we didn't have a pool!" }),
            };
            var result = new List<(NPCProfile, float[])>();
            foreach (var p in people)
            {
                var path = LivingNPCBuilder.ProfileFolder + "/" + p.id + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<NPCProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<NPCProfile>();
                    profile.Id = p.id; profile.DisplayName = p.name; profile.Occupation = p.job; profile.Model = p.model; profile.Age = p.age;
                    profile.Home = "villa"; profile.Work = p.work; profile.RelationshipToPlayer = "family";
                    profile.Family = people.Where(o => o.id != p.id).Select(o => o.id).ToArray();
                    profile.Personality = new NPCPersonality { Sociability = 0.7f, Shyness = 0.2f, Patience = 0.8f, Busyness = 0.4f, Distraction = 0.3f };
                    profile.ScheduleJson = p.sched; profile.EnglishBand = p.band; profile.Voice = new NPCVoice { Accent = p.accent };
                    profile.Greetings = p.greet; profile.SmallTalk = p.talk; profile.PreferredActivities = new[] { "Relax", "Wander" };
                    AssetDatabase.CreateAsset(profile, path);
                }
                result.Add((profile, p.start));
            }
            AssetDatabase.SaveAssets();
            report.Add("Family profiles: " + LivingNPCBuilder.ProfileFolder + "/villa_*.asset (4; existing edits kept)");
            return result;
        }

        private static GameObject Pet(Vector3 at, int agentType, LivingPlace garden, LivingPlace home, List<string> report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatModel) ?? AssetDatabase.LoadAssetAtPath<GameObject>(CatFbx);
            if (prefab == null) { Missing.Add(CatModel + " — no pet"); return null; }
            var root = new GameObject("Pet · Mochi (cat)");
            root.transform.position = at;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(root.transform, false);
            var b = AirportKit.Measure(model);
            var len = Mathf.Max(b.size.x, b.size.z);
            if (len > 0.01f) model.transform.localScale *= 0.5f / len;
            b = AirportKit.Measure(model);
            model.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agentType; agent.radius = 0.18f; agent.height = 0.35f; agent.speed = 1.1f;
            var pet = root.AddComponent<VillaPet>();
            pet.Favourites = garden.Waypoints.Concat(home.Spots.Where(s => s.Kind == SpotKind.Sofa || s.Kind == SpotKind.Stove).Select(s => s.transform)).ToArray();
            var fx = new GameObject("Pet the cat").AddComponent<VillaFixture>();
            fx.transform.SetParent(root.transform, false);
            fx.transform.localPosition = Vector3.up * 0.2f;
            fx.Kind = VillaFixture.Use.Pet; fx.Topic = "pets"; fx.Prompt = "Mochi"; fx.Range = 1.6f;
            report.Add("Pet: Mochi the cat (Meshy Cat_Reference, static mesh — no walk cycle; moves with a small trot bob)");
            return root;
        }

        /// Adds (or finds) a NavMesh agent type with radius 0.25 m / climb 0.3 m in Project Settings → Navigation.
        public static int EnsureAgentType(List<string> report)
        {
            for (var i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == AgentName) return s.agentTypeID;
            }
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").FirstOrDefault();
            if (asset == null) { report.Add("WARNING: NavMeshAreas.asset not found — using the default agent"); return 0; }
            var so = new SerializedObject(asset);
            var settings = so.FindProperty("m_Settings");
            var names = so.FindProperty("m_SettingNames");
            var index = settings.arraySize;
            settings.InsertArrayElementAtIndex(index - 1);
            names.InsertArrayElementAtIndex(names.arraySize - 1);
            var entry = settings.GetArrayElementAtIndex(index);
            var id = 0;
            var taken = Enumerable.Range(0, NavMesh.GetSettingsCount()).Select(i => NavMesh.GetSettingsByIndex(i).agentTypeID).ToList();
            var rng = new System.Random(7177);
            do id = rng.Next(1, int.MaxValue); while (taken.Contains(id));
            entry.FindPropertyRelative("agentTypeID").intValue = id;
            entry.FindPropertyRelative("agentRadius").floatValue = 0.25f;
            entry.FindPropertyRelative("agentHeight").floatValue = 1.7f;
            entry.FindPropertyRelative("agentSlope").floatValue = 45;
            entry.FindPropertyRelative("agentClimb").floatValue = 0.3f;
            names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = AgentName;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            report.Add("Added NavMesh agent type '" + AgentName + "' (r 0.25, climb 0.3) to Project Settings → Navigation");
            return id;
        }

        [MenuItem("ContextSpeak/Villa/Add Villa Entrance To Open Scene")]
        public static void AddEntrance()
        {
            var view = SceneView.lastActiveSceneView;
            var go = new GameObject("Portal · Luxury Villa");
            go.transform.position = view != null ? view.pivot : Vector3.zero;
            var portal = go.AddComponent<PortalDoor>();
            portal.TargetScene = "LuxuryVilla"; portal.Entrance = "villa"; portal.Prompt = "Go home (Luxury Villa)"; portal.Range = 3;
            Undo.RegisterCreatedObjectUndo(go, "Add villa entrance");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("Villa entrance added at " + go.transform.position + " — move it to the villa's door in this scene and save.");
        }
    }
}
