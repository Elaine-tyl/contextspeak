using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Water > …
    ///  • Setup Character Water Interaction — adds the interaction system to the open scene without
    ///    touching existing materials or player prefabs: water profiles, WaterBody volumes for every
    ///    water surface (and the ocean), the ripple simulation + effects + debug panel, the player
    ///    component, ripple emitters on fish/floating objects, swimming where the water is deep.
    ///  • Build Water Interaction Test Scene — pool (shallow + deep end), forest pond, river, ocean strip.
    ///  • Character Interaction Debugger — live values and tuning in Play mode.
    public static class WaterInteractionSetup
    {
        public const string Folder = "Assets/ContextSpeak/Data/Water";
        public const string TestScene = "Assets/ContextSpeak/Scenes/WaterInteractionTestScene.unity";
        private const string Art = "Assets/Art/water/";

        public static WaterInteractionProfile Profile(WaterKind kind)
        {
            Directory.CreateDirectory(Folder + "/Profiles");
            var path = Folder + "/Profiles/" + kind + " Interaction.asset";
            var profile = AssetDatabase.LoadAssetAtPath<WaterInteractionProfile>(path);
            if (profile != null) return profile;                      // never overwrite tuned profiles
            profile = WaterInteractionProfile.Create(kind);
            profile.name = kind + " Interaction";
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        [UnityEditor.MenuItem("ContextSpeak/Water/Setup Character Water Interaction")]
        public static void SetupMenu()
        {
            var report = Setup();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Character water interaction set up in " + EditorSceneManager.GetActiveScene().name + ":\n- " + string.Join("\n- ", report));
        }

        public static List<string> Setup()
        {
            var report = new List<string>();
            AssetDatabase.Refresh();
            // 1. Pipeline: the water shaders need depth + opaque textures.
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) report.Add("WARNING: the active render pipeline is not URP — the ripple shaders target URP only.");
            else
            {
                if (!pipeline.supportsCameraDepthTexture) { pipeline.supportsCameraDepthTexture = true; EditorUtility.SetDirty(pipeline); report.Add("Enabled the depth texture on " + pipeline.name); }
                if (!pipeline.supportsCameraOpaqueTexture) { pipeline.supportsCameraOpaqueTexture = true; EditorUtility.SetDirty(pipeline); report.Add("Enabled the opaque texture on " + pipeline.name); }
            }
            if (Shader.Find("Hidden/ContextSpeak/Water Ripple Sim") == null) report.Add("MISSING: shader Hidden/ContextSpeak/Water Ripple Sim (Runtime/Water/Shaders) — not compiled yet?");
            foreach (var kind in new[] { WaterKind.Pool, WaterKind.Pond, WaterKind.River, WaterKind.Stream, WaterKind.Lake, WaterKind.Ocean }) Profile(kind);
            report.Add("Profiles: " + Folder + "/Profiles (Pool, Pond, River, Stream, Lake, Ocean — existing ones kept)");

            // 2. Water bodies.
            var bodies = 0;
            var waterShader = Shader.Find("ContextSpeak/Water Surface");
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var settings = renderer.GetComponent<WaterSettings>();
                var usesShader = renderer.sharedMaterial != null && renderer.sharedMaterial.shader == waterShader;
                var named = renderer.name.ToLowerInvariant();
                if (settings == null && !usesShader && !(named.Contains("water") && !named.Contains("falling") && renderer.bounds.size.y < 0.3f)) continue;
                if (settings != null && settings.Type == WaterSettings.WaterType.Waterfall) continue;
                if (named.Contains("falling") || named.Contains("waterfall") || named.Contains("spray")) continue;
                if (renderer.GetComponent<WaterBody>() != null) continue;
                var kind = settings == null ? Guess(named) : settings.Type switch
                {
                    WaterSettings.WaterType.Pool => WaterKind.Pool, WaterSettings.WaterType.River => WaterKind.River, WaterSettings.WaterType.Stream => WaterKind.Stream,
                    WaterSettings.WaterType.Lake => WaterKind.Lake, _ => WaterKind.Pond,
                };
                var b = renderer.bounds;
                var bed = WaterBody.Floor(new Vector3(b.center.x, b.max.y + 0.05f, b.center.z), 30, b.max.y - 1.5f);
                var body = renderer.gameObject.AddComponent<WaterBody>();
                body.Kind = kind;
                body.Profile = Profile(kind);
                body.Surface = renderer;
                body.Volume = new Bounds(new Vector3(b.center.x, (b.max.y + bed) / 2, b.center.z), new Vector3(b.size.x, Mathf.Max(0.1f, b.max.y - bed), b.size.z));
                body.Flow = settings != null ? settings.Flow : Vector2.zero;
                bodies++;
                if (b.max.y - bed >= 1.2f && Object.FindObjectsByType<PondSwimming>(FindObjectsSortMode.None).All(s => !s.Water.Intersects(body.Volume)))
                {
                    var swim = new GameObject("Swimming · " + renderer.name).AddComponent<PondSwimming>();
                    swim.transform.SetParent(renderer.transform.parent, true);
                    swim.Water = body.Volume;
                    report.Add("Swimming enabled in " + renderer.name + " (" + (b.max.y - bed).ToString("0.0") + " m deep)");
                }
            }
            var ocean = Object.FindFirstObjectByType<OceanWaves>();
            if (ocean != null && ocean.GetComponent<WaterBody>() == null)
            {
                var body = ocean.gameObject.AddComponent<WaterBody>();
                body.Kind = WaterKind.Ocean;
                body.Profile = Profile(WaterKind.Ocean);
                body.Volume = new Bounds(new Vector3(ocean.IslandCentre.x, OceanWaves.SeaLevel - 15, ocean.IslandCentre.z), new Vector3(4000, 34, 4000));
                bodies++;
                report.Add("Ocean registered (local ripples layered over the Gerstner waves)");
            }
            report.Add(bodies + " water bodies registered (" + WaterBodyCount() + " in scene)");

            // 3. Systems.
            var systems = GameObject.Find("Water interaction") ?? new GameObject("Water interaction");
            if (systems.GetComponent<WaterRippleSimulation>() == null) systems.AddComponent<WaterRippleSimulation>();
            if (systems.GetComponent<WaterSplashController>() == null) systems.AddComponent<WaterSplashController>();
            if (systems.GetComponent<WaterInteractionDebugPanel>() == null) systems.AddComponent<WaterInteractionDebugPanel>();

            // 4. Characters (any playable character: male and female share CafePlayer).
            var players = Object.FindObjectsByType<CafePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var player in players)
                if (player.GetComponent<CharacterWaterInteraction>() == null) player.gameObject.AddComponent<CharacterWaterInteraction>();
            if (players.Length == 0) report.Add("MISSING: no CafePlayer in this scene — open a playable scene and run again.");
            else
            {
                systems.GetComponent<WaterRippleSimulation>().Follow = players[0].transform;
                var feet = new WaterContactDetector(players[0].Model != null ? players[0].Model : players[0].transform, 1.7f);
                report.Add("Player: " + players.Length + " character(s); " + (feet.HasFeet ? "foot bones found (" + feet.LeftFoot.name + ", " + feet.RightFoot.name + ")" : "no foot bones — the stride-event fallback is used"));
            }

            // 5. Other ripple sources.
            var emitters = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var isFish = t.GetComponent("PondFish") != null || t.GetComponent<FishSchool>() != null;
                var floats = t.GetComponent<Floater>() != null;
                if (!isFish && !floats) continue;
                if (t.GetComponent<WaterRippleEmitter>() != null) continue;
                var emitter = t.gameObject.AddComponent<WaterRippleEmitter>();
                emitter.Radius = isFish ? 0.12f : 0.6f;
                emitter.Strength = isFish ? 0.4f : 1.2f;
                emitter.Reach = isFish ? 0.25f : 0.6f;
                emitter.SplashOnEntry = !isFish;
                emitters++;
            }
            report.Add(emitters + " ripple emitters added (fish, floating objects)");
            report.Add("Sound: synthesised splash/slosh/stroke/underwater sounds are used — assign recorded clips on CharacterWaterInteraction to replace them");
            report.Add("Animation events: optional — call OnLeftFootWater / OnRightFootWater / OnSwimStroke / OnDiveEntry / OnWaterExit; otherwise foot bones or stride events drive the effects");
            report.Add("Debug panel in Play mode: F4");
            return report;
        }

        private static int WaterBodyCount() => Object.FindObjectsByType<WaterBody>(FindObjectsSortMode.None).Length;

        private static WaterKind Guess(string name) =>
            name.Contains("pool") ? WaterKind.Pool : name.Contains("river") ? WaterKind.River : name.Contains("stream") ? WaterKind.Stream : name.Contains("lake") ? WaterKind.Lake : WaterKind.Pond;

        // ------------------------------------------------------------------ test scene
        [UnityEditor.MenuItem("ContextSpeak/Water/Build Water Interaction Test Scene")]
        public static void BuildTestScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Missing.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Water interaction test");
            Daylight("WaterTest", "E6EEF2");
            var ground = Box("Test ground", new Vector3(0, -0.25f, 0), new Vector3(120, 0.5f, 120), Hex("7C8F5A"), true);
            ground.name = "Test ground lawn";
            var notes = new List<string>();

            // Pool (Blender model, or boxes when it has not been exported yet).
            var poolCentre = new Vector3(-14, 0, -8);
            HoleIn(ground, poolCentre, new Vector2(15, 10));
            var poolModel = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Pool.glb");
            if (poolModel != null)
            {
                var pool = (GameObject)PrefabUtility.InstantiatePrefab(poolModel);
                pool.transform.position = poolCentre;
                Solid(pool);
                Retile(pool);
            }
            else { BoxPool(poolCentre); notes.Add("Pool.glb not found — box pool used (run tools/blender/water_test.py)"); }
            Water("Pool water", poolCentre + Vector3.up * -0.12f, new Vector2(10, 5), WaterSettings.WaterType.Pool, Art + "PoolWater.glb", 0.6f);

            // Forest pond.
            var pondCentre = new Vector3(14, 0, -8);
            HoleIn(ground, pondCentre, new Vector2(16, 16));
            var basin = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "PondBasin.glb");
            if (basin != null) { var b = (GameObject)PrefabUtility.InstantiatePrefab(basin); b.transform.position = pondCentre; Solid(b); }
            else { Box("Pond bed", pondCentre + Vector3.down * 1.7f, new Vector3(12, 0.2f, 12), Hex("3A3226"), true); notes.Add("PondBasin.glb not found — flat pond bed used"); }
            Water("Pond water", pondCentre + Vector3.down * 0.15f, new Vector2(13, 13), WaterSettings.WaterType.Pond, Art + "PondWater.glb", 0.4f);

            // River with a current.
            var riverCentre = new Vector3(0, 0, 14);
            HoleIn(ground, riverCentre, new Vector2(30, 9));
            var bed = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "RiverBed.glb");
            if (bed != null) { var r = (GameObject)PrefabUtility.InstantiatePrefab(bed); r.transform.position = riverCentre; Solid(r); }
            else { Box("River bed", riverCentre + Vector3.down * 1f, new Vector3(30, 0.2f, 6), Hex("6E6252"), true); notes.Add("RiverBed.glb not found — flat river bed used"); }
            var river = Water("River water", riverCentre + Vector3.down * 0.12f, new Vector2(30, 6), WaterSettings.WaterType.River, Art + "RiverWater.glb", 0.3f);
            river.GetComponent<WaterSettings>().Flow = new Vector2(0.9f, 0);
            river.GetComponent<WaterSettings>().Apply();

            // Ocean strip: a sand ramp into the existing ocean shader and Gerstner waves.
            var oceanMaterial = new Material(Shader.Find("ContextSpeak/Ocean") ?? Shader.Find("Universal Render Pipeline/Lit"));
            Replace(oceanMaterial, Folder + "/TestOcean.mat");
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(ocean.GetComponent<Collider>());
            ocean.name = "Ocean surface";
            ocean.transform.position = new Vector3(0, OceanWaves.SeaLevel, -95);
            ocean.transform.localScale = new Vector3(12, 1, 4);
            ocean.GetComponent<Renderer>().sharedMaterial = oceanMaterial;
            var waves = ocean.AddComponent<OceanWaves>();
            waves.IslandCentre = new Vector3(0, 0, 40);
            waves.ShoreRadius = 100;
            var sand = Box("Beach sand ramp", new Vector3(0, -1.46f, -72), new Vector3(120, 0.4f, 24), Hex("D9C9A0"), true);
            sand.transform.rotation = Quaternion.Euler(-6, 0, 0);

            Label("POOL", poolCentre + new Vector3(0, 1.4f, -4.5f), 0, 0.5f, Color.white);
            Label("FOREST POND", pondCentre + new Vector3(0, 1.4f, -9), 0, 0.5f, Color.white);
            Label("RIVER", riverCentre + new Vector3(-12, 1.4f, -5), 0, 0.5f, Color.white);
            Label("OCEAN", new Vector3(0, 1.4f, -58), 0, 0.5f, Color.white);

            // Floating object and a small boat.
            var ball = Shape("Floating ball", PrimitiveType.Sphere, poolCentre + new Vector3(2, -0.05f, 1), Vector3.one * 0.35f, Hex("E55B3C"), false);
            ball.isStatic = false;
            ball.AddComponent<WaterRippleEmitter>().Radius = 0.25f;
            var boat = Box("Small boat", pondCentre + new Vector3(-2, -0.15f, 2), new Vector3(1.1f, 0.35f, 2.6f), Hex("8A5E3B"), false);
            boat.isStatic = false;
            var drift = boat.AddComponent<TestDrift>();
            drift.Centre = pondCentre + new Vector3(0, -0.12f, 0);
            drift.Radius = 3.5f;
            boat.AddComponent<WaterRippleEmitter>().Radius = 0.6f;

            var (player, camera) = Player(new Vector3(-14, 0.05f, -16), 0);
            camera.farClipPlane = 500;
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
            WorldBuild.Configure(player, new Transform[0], "WaterTest");
            new GameObject("Systems").AddComponent<LifeUI>().LocationName = "Water interaction test";
            var sky = SkyInstaller.Install();
            if (sky != null) { sky.SetTime(11f); EditorUtility.SetDirty(sky); }
            notes.AddRange(Setup());
            LifeBuild.Save(scene, TestScene);
            Debug.Log("Water interaction test scene built: " + TestScene + "\n- " + string.Join("\n- ", notes) +
                      "\nPlay: walk into the pool's shallow end, run, jump in at the deep end, swim (WASD, C dive, Space up), climb out; try the pond, the river current and the ocean. F4 = debugger; P = pause time in the Sky panel (F2) to test morning/noon/sunset/night/rain/wind.");
        }

        internal static void HoleIn(GameObject ground, Vector3 centre, Vector2 size)
        {
            // Split the flat test ground so the basins are not covered: four boxes around the hole.
            var colour = Hex("7C8F5A");
            void Piece(float x0, float x1, float z0, float z1) { if (x1 - x0 > 0.01f && z1 - z0 > 0.01f) Box("Test ground lawn", new Vector3((x0 + x1) / 2, -0.25f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.5f, z1 - z0), colour, true); }
            var hx0 = centre.x - size.x / 2; var hx1 = centre.x + size.x / 2; var hz0 = centre.z - size.y / 2; var hz1 = centre.z + size.y / 2;
            foreach (var piece in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Test ground lawn" && t.gameObject.activeSelf).ToArray())
            {
                var r = piece.GetComponent<Renderer>().bounds;
                if (r.max.x <= hx0 || r.min.x >= hx1 || r.max.z <= hz0 || r.min.z >= hz1) continue;
                piece.gameObject.SetActive(false);
                Piece(r.min.x, hx0, r.min.z, r.max.z);
                Piece(hx1, r.max.x, r.min.z, r.max.z);
                Piece(Mathf.Max(r.min.x, hx0), Mathf.Min(r.max.x, hx1), r.min.z, hz0);
                Piece(Mathf.Max(r.min.x, hx0), Mathf.Min(r.max.x, hx1), hz1, r.max.z);
            }
            Box("Under-basin ground", new Vector3(centre.x, -2.6f, centre.z), new Vector3(size.x, 0.4f, size.y), Hex("4A4236"), true);
        }

        internal static void Solid(GameObject item)
        {
            foreach (var mesh in item.GetComponentsInChildren<MeshFilter>())
                if (mesh.GetComponent<Collider>() == null) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            item.isStatic = true;
        }

        /// The pool interior gets a real-size mosaic (2.5 cm glass tiles) — UVs from Blender are in metres.
        internal static void Retile(GameObject pool)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/PoolMosaic.mat");
            if (material == null)
            {
                var size = 256;
                var colour = new Texture2D(size, size, TextureFormat.RGBA32, true);
                var random = new System.Random(3);
                const int tiles = 10;                 // texture = 10 × 10 tiles = 0.25 m
                var cell = size / tiles;
                var tone = new float[tiles * tiles];
                for (var i = 0; i < tone.Length; i++) tone[i] = (float)random.NextDouble();
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var gx = x % cell; var gy = y % cell;
                        var grout = gx < 2 || gy < 2;
                        var t = tone[(y / cell) * tiles + x / cell];
                        var c = grout ? new Color(0.86f, 0.9f, 0.9f) : Color.Lerp(new Color(0.42f, 0.72f, 0.82f), new Color(0.6f, 0.84f, 0.9f), t);
                        colour.SetPixel(x, y, c);
                    }
                colour.Apply();
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(Folder + "/PoolMosaic.png", colour.EncodeToPNG());
                Object.DestroyImmediate(colour);
                AssetDatabase.ImportAsset(Folder + "/PoolMosaic.png");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/PoolMosaic.png"));
                material.SetTextureScale("_BaseMap", Vector2.one * 4);     // 1 m of UV = 4 textures = 40 tiles
                material.SetFloat("_Smoothness", 0.8f);
                AssetDatabase.CreateAsset(material, Folder + "/PoolMosaic.mat");
            }
            foreach (var renderer in pool.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) if (materials[i] != null && materials[i].name.StartsWith("PoolTile")) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        internal static void BoxPool(Vector3 c)
        {
            var tile = Hex("6FB7CC");
            Box("Pool floor shallow", c + new Vector3(-2.75f, -1.05f, 0), new Vector3(4.5f, 0.1f, 5), tile, true);
            var slope = Box("Pool floor slope", c + new Vector3(0.25f, -1.5f, 0), new Vector3(2.7f, 0.1f, 5), tile, true);
            slope.transform.rotation = Quaternion.Euler(0, 0, -21.8f);
            Box("Pool floor deep", c + new Vector3(3.25f, -2.05f, 0), new Vector3(3.5f, 0.1f, 5), tile, true);
            foreach (var (pos, size) in new[] { (new Vector3(0, -1, -2.55f), new Vector3(10.2f, 2.1f, 0.1f)), (new Vector3(0, -1, 2.55f), new Vector3(10.2f, 2.1f, 0.1f)), (new Vector3(-5.05f, -1, 0), new Vector3(0.1f, 2.1f, 5)), (new Vector3(5.05f, -1, 0), new Vector3(0.1f, 2.1f, 5)) })
                Box("Pool wall", c + pos, size, tile, true);
            for (var k = 0; k < 3; k++) Box("Pool step", c + new Vector3(-4.8f + k * 0.4f, -1 + (3 - k) * 0.25f / 2, -1.5f), new Vector3(0.4f, (3 - k) * 0.25f, 2), tile, true);
        }

        internal static GameObject Water(string name, Vector3 centre, Vector2 size, WaterSettings.WaterType type, string gridPath, float displace)
        {
            GameObject water;
            var grid = AssetDatabase.LoadAssetAtPath<GameObject>(gridPath);
            if (grid != null)
            {
                water = (GameObject)PrefabUtility.InstantiatePrefab(grid);
                water.transform.position = centre;
                var renderer = water.GetComponentInChildren<MeshRenderer>();
                if (renderer.gameObject != water) { renderer.transform.SetParent(null, true); Object.DestroyImmediate(water); water = renderer.gameObject; }
            }
            else
            {
                water = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(water.GetComponent<Collider>());
                water.transform.position = centre;
                water.transform.localScale = new Vector3(size.x / 10, 1, size.y / 10);
                displace = 0;
            }
            water.name = name;
            var material = new Material(Shader.Find("ContextSpeak/Water Surface") ?? Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_RippleDisplace", displace);
            Replace(material, Folder + "/" + name + ".mat");
            water.GetComponent<Renderer>().sharedMaterial = material;
            water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var settings = water.AddComponent<WaterSettings>();
            settings.Type = type;
            settings.Surfaces = new Renderer[] { water.GetComponent<Renderer>() };
            settings.Apply();
            return water;
        }

        // ------------------------------------------------------------------ debugger window
        [UnityEditor.MenuItem("ContextSpeak/Water/Character Interaction Debugger")]
        public static void OpenDebugger() => EditorWindow.GetWindow<WaterInteractionDebugger>("Water interaction");
    }

    public sealed class WaterInteractionDebugger : EditorWindow
    {
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            if (!Application.isPlaying) { EditorGUILayout.HelpBox("Enter Play mode in a scene set up with ContextSpeak > Water > Setup Character Water Interaction. Values here drive the real simulation.", MessageType.Info); return; }
            var sim = WaterRippleSimulation.Instance;
            var character = Object.FindFirstObjectByType<CharacterWaterInteraction>();
            var effects = WaterSplashController.Instance;
            var body = character != null ? character.Body : sim != null ? sim.Body : null;
            var profile = body != null ? body.ActiveProfile : null;
            EditorGUILayout.LabelField("Current water body", body != null ? body.name + " (" + body.Kind + ")" : "none");
            EditorGUILayout.LabelField("Player immersion state", character != null ? character.State.ToString() : "—");
            EditorGUILayout.LabelField("Water surface height", character != null && character.Detector?.Body != null ? character.Detector.Surface.ToString("0.00") + " m (depth " + character.Detector.Depth.ToString("0.00") + " m)" : "—");
            EditorGUILayout.LabelField("Player horizontal speed", character != null ? character.HorizontalSpeed.ToString("0.00") + " m/s" : "—");
            EditorGUILayout.LabelField("Entry vertical velocity", character != null ? character.EntrySpeed.ToString("0.00") + " m/s" : "—");
            EditorGUILayout.LabelField("Active ripple sources", sim != null ? sim.ActiveSources.ToString() : "—");
            EditorGUILayout.LabelField("Ripple simulation (CPU submit)", sim != null ? sim.LastStepMilliseconds.ToString("0.000") + " ms · " + sim.CurrentResolution + "²" : "off");
            EditorGUILayout.Space();
            if (sim != null)
            {
                EditorGUI.BeginChangeCheck();
                var over = EditorGUILayout.Toggle("Override profile", sim.Override);
                var strength = EditorGUILayout.Slider("Ripple strength", sim.NormalStrength, 0, 3);
                var speed = EditorGUILayout.Slider("Propagation speed (m/s)", sim.WaveSpeed, 0.3f, 3);
                var damping = EditorGUILayout.Slider("Damping per step", sim.Damping, 0.95f, 0.999f);
                var foam = EditorGUILayout.Slider("Foam amount", sim.FoamAmount, 0, 3);
                var displace = EditorGUILayout.Slider("Vertex displacement", sim.Displacement, 0, 1);
                if (EditorGUI.EndChangeCheck()) { sim.Override = over != sim.Override ? over : true; sim.NormalStrength = strength; sim.WaveSpeed = speed; sim.Damping = damping; sim.FoamAmount = foam; sim.Displacement = displace; }
                var resolution = EditorGUILayout.IntPopup("Disturbance resolution", sim.CurrentResolution, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
                if (resolution != sim.CurrentResolution) sim.Allocate(resolution);
            }
            if (character != null) character.RippleSize = EditorGUILayout.Slider("Ripple size", character.RippleSize, 0.3f, 3);
            if (effects != null) effects.ParticleScale = EditorGUILayout.Slider("Particle count", effects.ParticleScale, 0.2f, 2.5f);
            if (profile != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Profile: " + profile.name, EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                profile.SplashScale = EditorGUILayout.Slider("Splash intensity", profile.SplashScale, 0, 3);
                profile.WakeLength = EditorGUILayout.Slider("Wake length", profile.WakeLength, 0, 3);
                profile.BubbleIntensity = EditorGUILayout.Slider("Underwater bubbles", profile.BubbleIntensity, 0, 3);
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(profile);
            }
        }
    }
}
