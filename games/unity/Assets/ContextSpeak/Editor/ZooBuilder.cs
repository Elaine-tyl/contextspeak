using System;
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
using static ContextSpeak.Editor.AirportKit;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Build Wildlife Zoo → Assets/ContextSpeak/Scenes/WildlifeZoo.unity.
    /// Phase 1 of the Metro City Wildlife Zoo: station, entrance plaza, ticket office and gate, information
    /// pavilion, the main gravel paths, the African Savannah (sculpted terrain with a waterhole, a rocky
    /// kopje, dry grass, shade trees, a fenced perimeter and three viewing decks) with rigged, living
    /// elephants (and a calf), antelopes and a rhino, placeholder giraffes and zebras, a small children's
    /// farm, the Savannah View Café, guide / keepers / visitors, and the ZOO_VISIT_001 quest.
    /// Zones are separate root objects ("Zone · …") so later phases can move them into additive scenes.
    /// Imported models come from Assets/Art/zoo (made by tools/blender/zoo_animals.py); anything missing
    /// becomes a clearly named placeholder and is listed in the build report.
    public static class ZooBuilder
    {
        public const string ScenePath = Scenes + "/WildlifeZoo.unity";
        public const string Art = "Assets/Art/zoo/";
        private const string Folder = Data + "/Zoo";
        private const float G = 1f;                                   // walking level (the terrain dips below it for water)
        private static readonly Vector3 TerrainOrigin = new Vector3(-190, -9, -180);
        private const float TerrainSize = 380, TerrainHeight = 30;
        private static readonly Vector2 SavannahCentre = new Vector2(0, 45), SavannahRadii = new Vector2(72, 50);
        private static readonly Vector2 Waterhole = new Vector2(-24, 30), Kopje = new Vector2(34, 66);
        private static readonly Vector2 FarmCentre = new Vector2(-54, -72), FarmRadii = new Vector2(13, 9);
        private const float WaterholeRadius = 11, WaterLevel = G - 0.45f;

        private static readonly Color Timber = Hex("8A6240"), DarkTimber = Hex("5B3E28"), Thatch = Hex("A8834F"), Cream = Hex("FFF6E5"), Ink = Hex("2A2522"),
            Leaf = Hex("3E6B3A"), SignGreen = Hex("2F5E3F"), Stone = Hex("B8B2A6");

        private static Terrain terrain;
        private static Dictionary<string, GameObject> plants;
        private static readonly List<Vector2[]> trails = new List<Vector2[]>
        {
            new[] { new Vector2(0, -142), new Vector2(0, -58) },
            new[] { new Vector2(0, -58), new Vector2(-40, -46), new Vector2(-78, -20), new Vector2(-88, 22) },
            new[] { new Vector2(0, -58), new Vector2(0, -15) },
            new[] { new Vector2(0, -58), new Vector2(40, -46), new Vector2(78, -20), new Vector2(88, 26), new Vector2(94, 62) },
            new[] { new Vector2(-40, -46), new Vector2(-50, -58) },
            new[] { new Vector2(40, -46), new Vector2(50, -54) },
        };
        private static readonly List<(Vector2 centre, float radius)> keepClear = new List<(Vector2, float)>();

        // ------------------------------------------------------------------ ground maths
        private static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));

        private static float DistanceToPolyline(Vector2 p, Vector2[] line)
        {
            var best = float.MaxValue;
            for (var i = 0; i < line.Length - 1; i++)
            {
                var a = line[i]; var b = line[i + 1];
                var t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(0.0001f, (b - a).sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + t * (b - a)));
            }
            return best;
        }

        private static float PathDistance(Vector2 p) => trails.Min(line => DistanceToPolyline(p, line));

        private static float H(float x, float z)
        {
            var p = new Vector2(x, z);
            var h = G;
            var edge = Mathf.Max(Mathf.Abs(x) - 128, z - 122, -150 - z);
            if (edge > 0) h += Smooth(0, 45, edge) * (2.5f + 6 * Mathf.PerlinNoise(x * 0.018f + 3, z * 0.018f + 7));
            var e = AnimalHabitat.Ellipse(SavannahCentre, SavannahRadii, p);
            if (e < 1) h += (Mathf.PerlinNoise(x * 0.035f + 5, z * 0.035f + 9) - 0.5f) * 0.9f * Smooth(1, 0.7f, e);
            h += 2.6f * Mathf.Exp(-(p - Kopje).sqrMagnitude / (2 * 6.5f * 6.5f));
            h -= 1.7f * Smooth(WaterholeRadius, 3.5f, Vector2.Distance(p, Waterhole));
            return h;
        }

        private static float Ground(float x, float z) => terrain != null ? terrain.SampleHeight(new Vector3(x, 0, z)) + TerrainOrigin.y : H(x, z);
        private static Vector3 At(float x, float z, float lift = 0.02f) => new Vector3(x, Ground(x, z) + lift, z);
        private static Vector3 At(Vector2 point, float lift = 0.02f) => At(point.x, point.y, lift);

        // ------------------------------------------------------------------ menu
        [UnityEditor.MenuItem("ContextSpeak/Build Wildlife Zoo")]
        public static void BuildMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Missing.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("ContextSpeak", "Inspecting the project…", 0.05f);
                CafeBuild.ConfigurePipeline();
                foreach (var file in new[] { "Elephant", "Giraffe", "Zebra", "Deer", "Rhino", "Sheep", "Pony", "Pig", "Chicken", "ZooFence" })
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(Art + file + ".glb") == null) Missing.Add(Art + file + ".glb (not imported — a placeholder is used; run tools/blender/zoo_animals.py in Blender)");
                Missing.Add("Giraffe and zebra: simplified models built in Blender (tools/blender/zoo_animals.py, build_procedural) — replace with Meshy downloads for full realism");
                Missing.Add("Antelope: the stag model (Dawn Stag) stands in for an antelope. Needed: a Meshy antelope / gazelle");
                Missing.Add("Acacia trees: oaks are used as savannah shade trees. Needed: a Meshy acacia (flat-topped) tree");
                Missing.Add("Animal sounds: no recordings in the project — animals are silent. Assign clips to AnimalAudioController.Calls (elephant rumble/trumpet, zebra, antelope, sheep, pig, chicken)");
                Directory.CreateDirectory(Folder);
                Directory.CreateDirectory(Scenes);
                AssetDatabase.Refresh();
                Build();
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            var report = "Wildlife zoo (phase 1) built: " + ScenePath + " (registered in Build Settings). Rebuild Metro City to add the Zoo Line entrance at Central Station.";
            Debug.LogWarning(report + "\nMissing assets / placeholders:\n- " + string.Join("\n- ", Missing.Distinct()));
            var errors = ZooChecks.Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak zoo checks passed.");
            else Debug.LogError("ContextSpeak zoo checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static void Build()
        {
            keepClear.Clear();
            terrain = null;
            plants = EnvironmentSetup.EnsureVegetationPrefabs();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Metro City Wildlife Zoo");
            Daylight("WildlifeZoo", "E4EEF2");
            var flow = new GameObject("Zoo systems").AddComponent<ZooFlow>();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Sculpting the savannah…", 0.15f);
            BuildTerrain();
            BuildWater();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Entrance and visitor areas…", 0.3f);
            Station(flow);
            Entrance(flow);
            Hub();
            Cafe();
            TramStop();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Habitats…", 0.45f);
            flow.Savannah = SavannahHabitat(flow);
            flow.Farm = FarmHabitat();
            Boundaries();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Animals…", 0.6f);
            SavannahAnimals(flow.Savannah);
            FarmAnimals(flow.Farm);
            Visitors();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Planting…", 0.7f);
            Planting(flow.Savannah);
            flow.MapBounds = new Rect(-135, -150, 270, 275);
            flow.MapLabels = new List<string>
            {
                "Station|0|-140", "Tickets & gate|0|-88", "Information|-16|-72", "Children's farm|-54|-72", "Café|56|-58", "Savannah deck|0|-10",
                "Elephant waterhole|-80|24", "Giraffes|80|28", "AFRICAN SAVANNAH|0|50", "Tram (soon)|94|66", "Rainforest (soon)|-95|95", "Penguins (soon)|95|100",
            };

            EditorUtility.DisplayProgressBar("ContextSpeak", "Baking navigation…", 0.82f);
            var (player, camera) = Player(flow.Spawn.position, flow.Spawn.eulerAngles.y);
            camera.farClipPlane = 700;
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture = true;      // water depth colour and shore foam
            data.requiresColorTexture = true;      // water refraction
            var voxel = WorldBuild.VoxelSize;
            WorldBuild.VoxelSize = 0.2f;
            try { Finish(player, camera, flow, "Metro City Wildlife Zoo", "WildlifeZoo"); }
            finally { WorldBuild.VoxelSize = voxel; }

            EditorUtility.DisplayProgressBar("ContextSpeak", "Sky, weather and vegetation…", 0.9f);
            var sky = SkyInstaller.Install();
            if (sky != null) { sky.SetTime(9.5f); EditorUtility.SetDirty(sky); }
            LifeBuild.Save(scene, ScenePath);
            EnvironmentSetup.ConfigureCurrentScene(true);      // PBR ground layers, water shader, wind, grass and flowers on the lawns, lighting
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ terrain and water
        private static TerrainLayer Layer(string name, Color tint, float tile)
        {
            // A plain tinted layer; ContextSpeak > Environment replaces it with the PBR ground set of the same kind.
            Directory.CreateDirectory(Folder + "/Textures");
            var texture = new Texture2D(8, 8, TextureFormat.RGB24, true);
            texture.SetPixels(Enumerable.Repeat(tint, 64).ToArray());
            texture.Apply();
            var path = Folder + "/Textures/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var layer = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path), tileSize = Vector2.one * tile, smoothness = 0.1f };
            Replace(layer, Folder + "/Textures/" + name + ".terrainlayer");
            return layer;
        }

        private static void BuildTerrain()
        {
            const int resolution = 513;
            var data = new TerrainData { heightmapResolution = resolution };
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            var heights = new float[resolution, resolution];
            for (var j = 0; j < resolution; j++)
                for (var i = 0; i < resolution; i++)
                {
                    var x = TerrainOrigin.x + i / (float)(resolution - 1) * TerrainSize;
                    var z = TerrainOrigin.z + j / (float)(resolution - 1) * TerrainSize;
                    heights[j, i] = Mathf.Clamp01((H(x, z) - TerrainOrigin.y) / TerrainHeight);
                }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { Layer("ZooGrass", Hex("6F8F3F"), 2.5f), Layer("SavannahSoil", Hex("A58C62"), 2.5f), Layer("ZooRock", Hex("8D857B"), 3f), Layer("ZooPath", Hex("BBAE94"), 1.6f) };
            const int alpha = 512;
            data.alphamapResolution = alpha;
            var weights = new float[alpha, alpha, 4];
            for (var j = 0; j < alpha; j++)
                for (var i = 0; i < alpha; i++)
                {
                    var x = TerrainOrigin.x + (i + 0.5f) / alpha * TerrainSize;
                    var z = TerrainOrigin.z + (j + 0.5f) / alpha * TerrainSize;
                    var p = new Vector2(x, z);
                    var e = AnimalHabitat.Ellipse(SavannahCentre, SavannahRadii, p);
                    var water = Vector2.Distance(p, Waterhole);
                    var steep = data.GetSteepness((i + 0.5f) / alpha, (j + 0.5f) / alpha);
                    var rock = Mathf.Max(Smooth(24, 36, steep), Mathf.Exp(-(p - Kopje).sqrMagnitude / (2 * 5f * 5f)) > 0.35f ? 0.9f : 0);
                    float soil = 0;
                    if (e < 1.03f)
                    {
                        // Dry earth with greener ground near the water and in scattered patches; bare mud at the water's edge.
                        var green = Mathf.Max(Smooth(26, 13, water), Smooth(0.55f, 0.75f, Mathf.PerlinNoise(x * 0.05f + 2, z * 0.05f + 8)) * 0.8f);
                        soil = Mathf.Clamp01(1 - green);
                        if (water < WaterholeRadius + 1.5f) soil = 1;
                    }
                    if (AnimalHabitat.Ellipse(FarmCentre, FarmRadii, p) < 1) soil = Mathf.Max(soil, 0.55f);
                    var path = e < 1.03f ? 0 : 1 - Smooth(1.5f, 2.4f, PathDistance(p));
                    var w = new[] { Mathf.Clamp01(1 - soil - rock), soil * (1 - rock), rock, path };
                    for (var k = 0; k < 3; k++) w[k] *= 1 - path;
                    var total = w.Sum();
                    for (var k = 0; k < 4; k++) weights[j, i, k] = total > 0 ? w[k] / total : (k == 0 ? 1 : 0);
                }
            data.SetAlphamaps(0, 0, weights);
            Replace(data, Folder + "/WildlifeZooTerrain.asset");
            var item = Terrain.CreateTerrainGameObject(data);
            item.name = "Zoo terrain";
            item.transform.position = TerrainOrigin;
            item.isStatic = true;
            terrain = item.GetComponent<Terrain>();
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                Replace(material, Folder + "/WildlifeZooTerrain.mat");
                terrain.materialTemplate = material;
            }
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4;
            terrain.basemapDistance = 220;
            terrain.Flush();
        }

        private static void BuildWater()
        {
            var water = Shape("Waterhole water", PrimitiveType.Cylinder, new Vector3(Waterhole.x, WaterLevel - 0.01f, Waterhole.y), new Vector3(WaterholeRadius * 2 + 1, 0.01f, WaterholeRadius * 2 + 1), Hex("4E6E63"), false);
            water.isStatic = true;
        }

        // ------------------------------------------------------------------ small construction helpers
        private static GameObject Timbered(string name, Vector3 centre, Vector3 size, float yaw = 0, bool collide = true, Color? colour = null) =>
            Paint(Box(name, centre, size, colour ?? Timber, collide, null, yaw), colour ?? Timber, 0.3f);

        private static void Post(Vector3 foot, float height, float thick = 0.22f, Color? colour = null) =>
            Timbered("Post", foot + Vector3.up * height / 2, new Vector3(thick, height, thick), 0, true, colour ?? DarkTimber);

        private static void Shelter(string name, Vector3 floor, Vector2 size, float height, float yaw = 0, Color? roof = null)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                Post(floor + rotation * new Vector3(corner.x * (size.x / 2 - 0.2f), 0, corner.z * (size.y / 2 - 0.2f)), height);
            Timbered(name + " roof", floor + Vector3.up * (height + 0.15f), new Vector3(size.x + 0.8f, 0.3f, size.y + 0.8f), yaw, false, roof ?? Thatch);
            keepClear.Add((new Vector2(floor.x, floor.z), Mathf.Max(size.x, size.y) * 0.6f + 2));
        }

        private static void Blocker(Vector3 a, Vector3 b, float height = 3)
        {
            var wall = new GameObject("Boundary").AddComponent<BoxCollider>();
            var mid = (a + b) / 2;
            wall.transform.position = new Vector3(mid.x, Mathf.Min(a.y, b.y) + height / 2 - 0.3f, mid.z);
            wall.transform.rotation = Quaternion.LookRotation(new Vector3(b.x - a.x, 0, b.z - a.z).normalized, Vector3.up);
            wall.size = new Vector3(0.3f, height, Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z)) + 0.3f);
            wall.gameObject.isStatic = true;
        }

        /// A post-and-rail fence along a closed ellipse (the habitat perimeter), following the ground.
        private static void RingFence(string name, Vector2 centre, Vector2 radii, float spacing, float height, int rails, Transform parent)
        {
            var perimeter = Mathf.PI * (3 * (radii.x + radii.y) - Mathf.Sqrt((3 * radii.x + radii.y) * (radii.x + 3 * radii.y)));
            var count = Mathf.Max(8, Mathf.RoundToInt(perimeter / spacing));
            Vector3 Point(int i) { var a = i / (float)count * Mathf.PI * 2; return At(centre.x + Mathf.Cos(a) * radii.x, centre.y + Mathf.Sin(a) * radii.y, 0); }
            for (var i = 0; i < count; i++)
            {
                var a = Point(i); var b = Point(i + 1);
                var post = Paint(Box("Fence post", a + Vector3.up * (height / 2 + 0.05f), new Vector3(0.16f, height + 0.1f, 0.16f), DarkTimber, false, parent), DarkTimber, 0.3f);
                post.isStatic = true;
                var length = Vector3.Distance(a, b);
                var yaw = -Mathf.Atan2(b.z - a.z, b.x - a.x) * Mathf.Rad2Deg;
                for (var r = 0; r < rails; r++)
                {
                    var y = height * (r + 1) / rails - 0.08f;
                    var rail = Paint(Box("Fence rail", (a + b) / 2 + Vector3.up * y, new Vector3(length, 0.09f, 0.07f), Timber, false, parent, yaw), Timber, 0.3f);
                    rail.transform.rotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0, -90, 0);
                    rail.isStatic = true;
                }
                Blocker(a, b);
            }
        }

        private static GameObject PlacePlant(string name, Vector3 position, float scale = 1, float yaw = 0, Transform parent = null)
        {
            if (plants == null || !plants.TryGetValue(name, out var prefab)) return null;
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) item.transform.SetParent(parent, true);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            item.transform.localScale = prefab.transform.localScale * scale;
            return item;
        }

        private static void InfoBoard(string speciesId, AnimalHabitat habitat, Vector3 foot, float yaw)
        {
            var species = AnimalCatalog.Find(speciesId);
            Post(foot, 1.1f, 0.1f);
            Board(species.Name.ToUpperInvariant() + "\n" + species.Status, foot + Vector3.up * 1.35f, yaw, new Vector2(1.5f, 0.6f), SignGreen, Cream);
            var spot = new GameObject("Info · " + species.Name).AddComponent<AnimalInteraction>();
            spot.transform.position = foot + Vector3.up * 1.2f;
            spot.SpeciesId = speciesId;
            spot.Habitat = habitat;
            spot.Prompt = "View Animal Information — " + species.Name;
            spot.Range = 2.6f;
        }

        // ------------------------------------------------------------------ station, entrance, hub
        private static void Station(ZooFlow flow)
        {
            var zone = Group("Zone · Station");
            Paint(Box("Station platform pavement", new Vector3(0, G + 0.2f, -146), new Vector3(22, 0.4f, 7), Stone, true, zone), Stone, 0.3f);
            Shelter("Station shelter", new Vector3(0, G + 0.4f, -146.5f), new Vector2(16, 4), 3.2f, 0, SignGreen);
            Board("WILDLIFE ZOO  ·  ZOO LINE", new Vector3(0, G + 3f, -144.4f), 180, new Vector2(6, 0.6f), SignGreen, Cream);
            Model("Assets/Art/city/CityTram.glb", new Vector3(0, G + 0.1f, -152.5f), 90, 3.4f, "Zoo Line train");
            flow.Spawn = Group("Spawn · from Metro City", new Vector3(2.5f, G + 0.45f, -144), 0);
            var portal = new GameObject("Portal → MetroCity").AddComponent<PortalDoor>();
            portal.transform.position = new Vector3(-5, G + 1.4f, -147);
            portal.TargetScene = "MetroCity";
            portal.Exit = true;
            portal.Prompt = "Take the Zoo Line back to Metro City";
            portal.Range = 2.6f;
            Board("TRAINS TO METRO CITY", new Vector3(-5, G + 2.4f, -148.2f), 180, new Vector2(3.4f, 0.45f), Ink, Hex("FFD58A"));
            for (var k = 0; k < 2; k++) Box("Platform step", new Vector3(0, G + 0.1f + k * 0.15f, -142.1f + k * 0.35f - 0.35f), new Vector3(6, 0.2f + k * 0.1f, 0.4f), Stone, true, zone);
            StreetLamp(new Vector3(-8, G + 0.4f, -143.2f), 0);
            StreetLamp(new Vector3(8, G + 0.4f, -143.2f), 0);
            keepClear.Add((new Vector2(0, -146), 16));
        }

        private static void Entrance(ZooFlow flow)
        {
            var zone = Group("Zone · Entrance");
            Paint(Box("Entrance plaza brick paving", new Vector3(0, G + 0.03f, -102), new Vector3(46, 0.1f, 32), Hex("A9765C"), true, zone), Hex("A9765C"), 0.2f);
            keepClear.Add((new Vector2(0, -102), 30));
            // Gate arch.
            foreach (var side in new[] { -1, 1 }) Post(new Vector3(side * 3.6f, G, -84), 5.2f, 0.55f);
            Timbered("Gate beam", new Vector3(0, G + 5.1f, -84), new Vector3(9.4f, 0.6f, 0.6f), 0, false, DarkTimber);
            Board("METRO CITY WILDLIFE ZOO", new Vector3(0, G + 6f, -84.2f), 0, new Vector2(8.6f, 0.9f), SignGreen, Cream);
            Board("Please keep to the paths  ·  Do not feed the animals", new Vector3(0, G + 4.3f, -84.35f), 0, new Vector2(7, 0.4f), Cream, Ink);
            // Fence either side of the gate (the imported zoo fence), then a hedge out to the boundary.
            var fence = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "ZooFence.glb");
            foreach (var side in new[] { -1, 1 })
            {
                for (var k = 0; k < 9; k++)
                {
                    var x = side * (4.8f + k * 1.78f);
                    if (fence != null)
                    {
                        var panel = (GameObject)PrefabUtility.InstantiatePrefab(fence, zone);
                        panel.transform.position = new Vector3(x, G, -84);
                        panel.isStatic = true;
                    }
                    else Timbered("Fence panel (placeholder)", new Vector3(x, G + 0.8f, -84), new Vector3(1.78f, 1.6f, 0.1f));
                }
                Blocker(new Vector3(side * 3.9f, G, -84), new Vector3(side * 20.5f, G, -84));
                Paint(Box("Boundary hedge", new Vector3(side * 75, G + 1.1f, -84), new Vector3(110, 2.2f, 1.4f), Leaf, true, zone), Leaf, 0.15f);
            }
            // Barrier across the opening until the ticket is scanned.
            var barrier = new GameObject("Gate barrier");
            barrier.transform.SetParent(zone, false);
            barrier.transform.position = new Vector3(0, G, -84);
            var block = barrier.AddComponent<BoxCollider>();
            block.center = new Vector3(0, 1.5f, 0);
            block.size = new Vector3(7.2f, 3, 0.4f);
            var boom = Box("Barrier arm", new Vector3(0, 0.95f, 0), new Vector3(6.4f, 0.1f, 0.1f), Hex("D9483B"), false, barrier.transform);
            boom.isStatic = false;
            flow.Barrier = barrier;
            Box("Ticket scanner", new Vector3(2.9f, G + 0.55f, -85.2f), new Vector3(0.35f, 1.1f, 0.35f), Hex("3A3F45"), true, zone);
            var gate = Spot("gate", "Scan your ticket", new Vector3(2.9f, G + 1.2f, -85.4f), 2.6f);
            gate.transform.SetParent(zone, true);

            // Ticket office.
            var booth = new Vector3(-11, G + 0.05f, -93);
            Timbered("Ticket office", booth + new Vector3(0, 1.5f, 1.2f), new Vector3(5, 3, 2.6f));
            Timbered("Ticket office roof", booth + new Vector3(0, 3.2f, 0.6f), new Vector3(6, 0.3f, 4.6f), 0, false, Thatch);
            Timbered("Ticket counter", booth + new Vector3(0, 0.55f, -0.9f), new Vector3(4.2f, 1.1f, 0.6f), 0, true, DarkTimber);
            Board("TICKETS", booth + new Vector3(0, 2.75f, -1.75f), 0, new Vector2(2.6f, 0.5f), SignGreen, Cream);
            Board("Standard $24  ·  Student $18  ·  Family $60", booth + new Vector3(0, 2.25f, -0.2f), 0, new Vector2(4, 0.35f), Cream, Ink);
            Npc("ticket", "Ms Okafor", "Ticket officer", "Secretary", booth + new Vector3(0, 0, -0.3f), 180);
            keepClear.Add((new Vector2(booth.x, booth.z), 6));

            // Information pavilion just inside the gate.
            var info = new Vector3(-16, G + 0.02f, -72);
            Paint(Box("Information pavilion stone floor", info + Vector3.up * 0.04f, new Vector3(8, 0.08f, 6), Stone, true, zone), Stone, 0.3f);
            Shelter("Information pavilion", info, new Vector2(8, 6), 3);
            Board("ZOO MAP\nYou are at the entrance.  Press M anywhere.", info + new Vector3(-1.8f, 1.7f, 2.2f), 0, new Vector2(3, 1.3f), Cream, Ink);
            Spot("map", "Look at the zoo map", info + new Vector3(-1.8f, 1.2f, 1.6f), 2.4f);
            Board("ANIMAL QUIZ\nFive questions — test yourself!", info + new Vector3(2, 1.7f, 2.2f), 0, new Vector2(2.8f, 1.1f), SignGreen, Cream);
            Spot("quiz", "Take the animal quiz", info + new Vector3(2, 1.2f, 1.6f), 2.4f);
            Bench(info + new Vector3(0, 0.08f, -1.6f), 0, 3, "info", SignGreen);
            Npc("guide", "Amara", "Zoo guide", "VirtualGuide", new Vector3(4.5f, G + 0.02f, -77), 180);
            foreach (var side in new[] { -1, 1 }) StreetLamp(new Vector3(side * 6.5f, G + 0.02f, -80), 0);
            foreach (var x in new[] { -17f, 17f })
                Model("Assets/Art/beach/CoconutPalm" + (x < 0 ? 1 : 3) + ".glb", new Vector3(x, G, -95), x * 7, 8.5f, "Entrance palm");
        }

        private static void Hub()
        {
            var hub = new Vector3(0, G + 0.02f, -58);
            Post(hub + new Vector3(2.6f, 0, 0), 3.2f, 0.16f);
            Board("←  Elephants  ·  Children's farm", hub + new Vector3(2.6f, 2.9f, -0.1f), 0, new Vector2(3.4f, 0.4f), SignGreen, Cream);
            Board("↑  Savannah viewing deck", hub + new Vector3(2.6f, 2.4f, -0.1f), 0, new Vector2(3.4f, 0.4f), SignGreen, Cream);
            Board("Giraffes  ·  Café  ·  Tram  →", hub + new Vector3(2.6f, 1.9f, -0.1f), 0, new Vector2(3.4f, 0.4f), SignGreen, Cream);
            Box("Drinking fountain", hub + new Vector3(-3, 0.5f, 1), new Vector3(0.45f, 1, 0.45f), Stone, true);
            Spot("fountain", "Drink some water", hub + new Vector3(-3, 1.1f, 1), 2);
            Bench(hub + new Vector3(-3.2f, 0, -3), 90, 3, "hub", SignGreen);
            Bench(hub + new Vector3(3.6f, 0, 4), -90, 3, "hub", SignGreen);
            Npc("visitor-lost", "Mr Tanaka", "Visitor", "QuietMorning", hub + new Vector3(-1.6f, 0, 3.2f), 200);
            keepClear.Add((new Vector2(0, -58), 6));
            // Areas planned for later phases, signposted honestly.
            foreach (var (text, x, z, yaw) in new[] { ("TROPICAL RAINFOREST  ·  BIG CATS\nOpening in phase 2", -92f, 30f, 180f), ("PENGUIN COVE  ·  AVIARY  ·  REPTILE HOUSE\nOpening in phase 2–3", 96f, 70f, 180f) })
            {
                var foot = At(x, z);
                Post(foot + new Vector3(-1.6f, 0, 0), 2.2f, 0.14f);
                Post(foot + new Vector3(1.6f, 0, 0), 2.2f, 0.14f);
                Board(text, foot + Vector3.up * 1.8f, yaw, new Vector2(3.6f, 0.9f), Hex("C9772E"), Cream);
                Spot("comingsoon", "Read the notice", foot + Vector3.up * 1.2f, 2.6f);
            }
        }

        private static void Cafe()
        {
            var zone = Group("Zone · Café");
            var c = new Vector3(56, G + 0.02f, -58);
            Paint(Box("Café terrace brick paving", c + new Vector3(0, 0.03f, -1), new Vector3(16, 0.08f, 14), Hex("A9765C"), true, zone), Hex("A9765C"), 0.2f);
            Timbered("Café kiosk", c + new Vector3(0, 1.5f, 4.5f), new Vector3(7, 3, 3));
            Timbered("Café roof", c + new Vector3(0, 3.25f, 3.6f), new Vector3(9, 0.3f, 6), 0, false, Thatch);
            Timbered("Café counter", c + new Vector3(0, 0.55f, 2.4f), new Vector3(5.5f, 1.1f, 0.7f), 0, true, DarkTimber);
            Board("SAVANNAH VIEW CAFÉ", c + new Vector3(0, 2.8f, 0.55f), 0, new Vector2(5, 0.55f), SignGreen, Cream);
            Board("Coffee  ·  Fresh juice  ·  Snacks", c + new Vector3(0, 2.25f, 2.9f), 0, new Vector2(4, 0.35f), Cream, Ink);
            Npc("barista", "Leo", "Barista", "SkateboardBoy", c + new Vector3(0, 0, 3.1f), 180);
            for (var i = 0; i < 3; i++)
            {
                var table = c + new Vector3(-5 + i * 5, 0.07f, -3.5f);
                Table(table, new Vector2(0.9f, 0.9f), Cream);
                Chair(table + new Vector3(0, 0, -0.85f), 0, SignGreen, "zoo-cafe");
                Chair(table + new Vector3(0, 0, 0.85f), 180, SignGreen, "zoo-cafe");
            }
            StreetLamp(c + new Vector3(-7.5f, 0, 1), 90);
            keepClear.Add((new Vector2(c.x, c.z), 12));
        }

        private static void TramStop()
        {
            var foot = At(98, 58);
            Paint(Box("Tram stop pavement", foot + Vector3.up * 0.06f, new Vector3(8, 0.12f, 4), Stone, true), Stone, 0.3f);
            Shelter("Tram stop", foot, new Vector2(7, 3), 2.8f, 0, SignGreen);
            Board("SAFARI TRAM\nOpening in phase 3", foot + new Vector3(0, 2.2f, 1.3f), 0, new Vector2(3.4f, 0.8f), Hex("C9772E"), Cream);
            Npc("tram", "Mr Banda", "Tram operator", "OfficerJames", foot + new Vector3(-2, 0.1f, -0.6f), 200);
        }

        // ------------------------------------------------------------------ habitats
        private static Transform Place(Transform parent, string name, Vector2 point)
        {
            var place = new GameObject(name).transform;
            place.SetParent(parent, false);
            place.position = At(point, 0);
            return place;
        }

        private static void Deck(string name, Vector3 centre, float yaw, Vector2 size)
        {
            // yaw 0: the railing is on the +Z side (towards the animals).
            var rotation = Quaternion.Euler(0, yaw, 0);
            Paint(Box(name + " timber deck", centre + Vector3.up * 0.07f, new Vector3(size.x, 0.14f, size.y), Timber, true, null, yaw), Timber, 0.3f);
            for (var x = -size.x / 2; x <= size.x / 2 + 0.01f; x += size.x / 6) Post(centre + rotation * new Vector3(x, 0.1f, size.y / 2 - 0.1f), 1.1f, 0.1f);
            Timbered("Deck rail", centre + rotation * new Vector3(0, 1.15f, size.y / 2 - 0.1f), new Vector3(size.x, 0.08f, 0.12f), yaw, false, DarkTimber);
            Timbered("Deck rail", centre + rotation * new Vector3(0, 0.65f, size.y / 2 - 0.1f), new Vector3(size.x, 0.06f, 0.08f), yaw, false, DarkTimber);
            Bench(centre + rotation * new Vector3(-size.x / 4, 0.14f, -size.y / 2 + 0.7f), yaw, 3, name, SignGreen);
            keepClear.Add((new Vector2(centre.x, centre.z), Mathf.Max(size.x, size.y) * 0.6f + 2));
        }

        private static AnimalHabitat SavannahHabitat(ZooFlow flow)
        {
            var zone = Group("Zone · African Savannah");
            var habitat = new GameObject("African Savannah habitat").AddComponent<AnimalHabitat>();
            habitat.transform.SetParent(zone, false);
            habitat.transform.position = At(SavannahCentre, 0);
            habitat.Title = "African Savannah";
            habitat.Radii = SavannahRadii;
            habitat.Margin = 5;
            habitat.Water = new[] { Place(habitat.transform, "Waterhole", Waterhole) };
            habitat.Food = new[] { new Vector2(-40, 52), new Vector2(-6, 18), new Vector2(18, 40), new Vector2(40, 30), new Vector2(-12, 72), new Vector2(46, 52) }.Select((p, i) => Place(habitat.transform, "Grazing " + i, p)).ToArray();
            var trees = new[] { new Vector2(-46, 40), new Vector2(-34, 66), new Vector2(8, 78), new Vector2(44, 40), new Vector2(52, 58), new Vector2(26, 22), new Vector2(-4, 46) };
            habitat.Rest = new[] { trees[0], trees[2], trees[6] }.Select((p, i) => Place(habitat.transform, "Shade " + i, p + new Vector2(3, -3))).ToArray();
            habitat.Browse = new[] { trees[3], trees[4], trees[5] }.Select((p, i) => Place(habitat.transform, "Browse tree " + i, p)).ToArray();
            var avoid = new List<Vector4> { new Vector4(Waterhole.x, Waterhole.y, 8, 0), new Vector4(Kopje.x, Kopje.y, 7.5f, 0) };
            foreach (var tree in trees)
            {
                PlacePlant("OakTree", At(tree, -0.05f), 0.85f, tree.x * 13, zone);
                avoid.Add(new Vector4(tree.x, tree.y, 1.4f, 0));
            }
            habitat.Avoid = avoid.ToArray();
            foreach (var (name, offset, scale) in new[] { ("MountainRocks", new Vector2(0, 0), 2.2f), ("TundraRock", new Vector2(4, -3), 1.6f), ("TundraRock", new Vector2(-4, 2), 1.3f), ("MountainRocks", new Vector2(2, 4), 1.4f) })
                PlacePlant(name, At(Kopje + offset, -0.3f), scale, offset.x * 40, zone);
            for (var i = 0; i < 26; i++)
            {
                var a = i / 26f * Mathf.PI * 2;
                if (a > 3.6f && a < 5.8f) continue;       // leave the south-east shore open for drinking
                PlacePlant("Reeds", At(Waterhole + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (WaterholeRadius - 2.2f + (i % 3) * 0.5f), -0.05f), 1 + (i % 4) * 0.15f, i * 47, zone);
            }
            Timbered("Feeding log", At(-6, 18, 0.25f), new Vector3(3.2f, 0.5f, 0.6f), 30, false, DarkTimber);
            RingFence("Savannah fence", SavannahCentre, SavannahRadii + Vector2.one, 3f, 1.25f, 2, zone);

            // Viewing decks: south (antelope, zebra, rhino), west (elephants), east (giraffes).
            var south = At(0, -10.5f);
            Deck("Savannah", south, 0, new Vector2(14, 6));
            InfoBoard("antelope", habitat, south + new Vector3(-5, 0.14f, 1.6f), 0);
            InfoBoard("zebra", habitat, south + new Vector3(0, 0.14f, 1.6f), 0);
            InfoBoard("rhino", habitat, south + new Vector3(5, 0.14f, 1.6f), 0);
            Npc("volunteer", "Sofia", "Conservation volunteer", "Rose", south + new Vector3(4, 0.14f, -1.5f), 200);
            var west = At(-78.5f, 24);
            Deck("Elephant", west, 90, new Vector2(14, 6));
            InfoBoard("elephant", habitat, west + new Vector3(1.6f, 0.14f, 4), 90);
            Board("ELEPHANT WATERHOLE\nKeeper talks daily", west + new Vector3(-2.4f, 2.4f, 0), 90, new Vector2(3.2f, 0.8f), SignGreen, Cream);
            Post(west + new Vector3(-2.5f, 0, 0), 2.2f, 0.14f);
            Npc("keeper-elephant", "Daniel", "Elephant keeper", "SoccerCaptain", west + new Vector3(0.8f, 0.14f, -3.5f), 110);
            flow.ElephantZone = new Bounds(west + Vector3.up * 2, new Vector3(16, 8, 26));
            var east = At(78.5f, 28);
            Deck("Giraffe", east, -90, new Vector2(14, 6));
            InfoBoard("giraffe", habitat, east + new Vector3(-1.6f, 0.14f, 4), -90);
            Npc("keeper-giraffe", "Priya", "Giraffe keeper", "OpenArms", east + new Vector3(-0.8f, 0.14f, -3.5f), -110);
            flow.GiraffeZone = new Bounds(east + Vector3.up * 2, new Vector3(16, 8, 26));
            return habitat;
        }

        private static AnimalHabitat FarmHabitat()
        {
            var zone = Group("Zone · Children's Farm");
            var habitat = new GameObject("Children's farm paddock").AddComponent<AnimalHabitat>();
            habitat.transform.SetParent(zone, false);
            habitat.transform.position = At(FarmCentre, 0);
            habitat.Title = "Children's Farm";
            habitat.Radii = FarmRadii;
            habitat.Margin = 1.2f;
            var trough = FarmCentre + new Vector2(6, 3);
            Timbered("Drinking trough", At(trough, 0.25f), new Vector3(1.6f, 0.5f, 0.6f), 20, false, DarkTimber);
            var hay = FarmCentre + new Vector2(-6, 2);
            Paint(Box("Hay rack", At(hay, 0.4f), new Vector3(1.4f, 0.8f, 0.8f), Hex("D8B863"), false), Hex("D8B863"), 0.1f);
            var shed = FarmCentre + new Vector2(-2, 5.5f);
            Shelter("Farm shelter", At(shed, 0), new Vector2(5, 3), 2.2f);
            habitat.Water = new[] { Place(habitat.transform, "Trough", trough) };
            habitat.Food = new[] { Place(habitat.transform, "Hay rack", hay) };
            habitat.Rest = new[] { Place(habitat.transform, "Shelter", shed) };
            habitat.Avoid = new[] { new Vector4(trough.x, trough.y, 0.9f, 0), new Vector4(hay.x, hay.y, 0.9f, 0) };
            RingFence("Farm fence", FarmCentre, FarmRadii + Vector2.one * 0.6f, 2.2f, 1.1f, 3, zone);
            var front = At(FarmCentre.x + 2, FarmCentre.y + FarmRadii.y + 2.4f);
            Board("CHILDREN'S FARM  (preview)", front + new Vector3(0, 2.3f, 0), 180, new Vector2(4.2f, 0.5f), SignGreen, Cream);
            Post(front + new Vector3(-2.2f, 0, 0), 2.6f, 0.14f);
            Post(front + new Vector3(2.2f, 0, 0), 2.6f, 0.14f);
            var x = -4.5f;
            foreach (var id in new[] { "sheep", "pony", "pig", "chicken" }) { InfoBoard(id, habitat, front + new Vector3(x, 0, -0.9f), 180); x += 3; }
            Npc("keeper-farm", "Hannah", "Farm keeper", "Plaid", front + new Vector3(6.5f, 0, -0.6f), 0);
            keepClear.Add((FarmCentre, 16));
            return habitat;
        }

        private static void Boundaries()
        {
            // The visitor area is closed by the terrain's hills; invisible walls stop anyone climbing out.
            var c = new[] { new Vector3(-130, G, -152), new Vector3(130, G, -152), new Vector3(130, G, 124), new Vector3(-130, G, 124) };
            for (var i = 0; i < 4; i++) Blocker(c[i], c[(i + 1) % 4], 8);
        }

        // ------------------------------------------------------------------ animals
        private static GameObject Limb(Transform parent, string name, Vector3 local, Vector3 size, Color colour, Vector3 meshOffset)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = local;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.name = name + " mesh";
            mesh.transform.SetParent(pivot, false);
            mesh.transform.localPosition = meshOffset;
            mesh.transform.localScale = size;
            mesh.GetComponent<Renderer>().sharedMaterial = Surface(colour, 0.2f);
            return pivot.gameObject;
        }

        /// A clearly temporary animal built from boxes, with the same bone names as the real rigs so it
        /// still walks, grazes and looks around until the proper model is downloaded.
        private static GameObject PlaceholderAnimal(string label, float leg, float length, float thick, float neck, float neckAngle, float headLength, Color colour, Color? stripes)
        {
            var root = new GameObject("PLACEHOLDER · " + label + " model");
            var hips = new GameObject("Hips").transform;
            hips.SetParent(root.transform, false);
            hips.localPosition = new Vector3(0, leg + thick / 2, -length / 2);
            var spine = Limb(hips, "Spine", Vector3.zero, new Vector3(thick * 0.8f, thick, length), colour, new Vector3(0, 0, length / 2)).transform;
            var chest = new GameObject("Chest").transform;
            chest.SetParent(spine, false);
            chest.localPosition = new Vector3(0, 0, length);
            var neckPivot = Limb(chest, "Neck", new Vector3(0, thick * 0.3f, 0), new Vector3(thick * 0.35f, neck, thick * 0.4f), colour, new Vector3(0, neck / 2, 0)).transform;
            neckPivot.localRotation = Quaternion.Euler(neckAngle, 0, 0);
            var head = Limb(neckPivot, "Head", new Vector3(0, neck, 0), new Vector3(thick * 0.32f, thick * 0.34f, headLength), colour, new Vector3(0, 0, headLength * 0.35f)).transform;
            head.localRotation = Quaternion.Euler(-neckAngle + 15, 0, 0);
            Limb(hips, "Tail1", new Vector3(0, thick * 0.3f, 0), new Vector3(0.06f, leg * 0.55f, 0.06f), stripes ?? colour * 0.7f, new Vector3(0, -leg * 0.27f, -0.05f));
            foreach (var (key, parent, z) in new[] { ("FL", chest, -thick * 0.25f), ("FR", chest, -thick * 0.25f), ("BL", hips, thick * 0.25f), ("BR", hips, thick * 0.25f) })
            {
                var side = key[1] == 'L' ? -1 : 1;
                var legThick = Mathf.Max(0.09f, thick * 0.16f);
                var upper = Limb(parent, "Leg_" + key + "_1", new Vector3(side * thick * 0.28f, -thick * 0.35f, z), new Vector3(legThick, leg * 0.55f, legThick), colour, new Vector3(0, -leg * 0.27f, 0)).transform;
                var lower = Limb(upper, "Leg_" + key + "_2", new Vector3(0, -leg * 0.52f, 0), new Vector3(legThick * 0.8f, leg * 0.5f, legThick * 0.8f), colour, new Vector3(0, -leg * 0.25f, 0)).transform;
                var foot = new GameObject("Leg_" + key + "_3").transform;
                foot.SetParent(lower, false);
                foot.localPosition = new Vector3(0, -leg * 0.48f, 0);
            }
            if (stripes.HasValue)
                for (var i = 0; i < 7; i++)
                    Limb(spine, "Stripe", Vector3.zero, new Vector3(thick * 0.82f, thick * 1.02f, length * 0.06f), stripes.Value, new Vector3(0, 0, length * (0.08f + i * 0.14f)));
            return root;
        }

        private static AnimalController Animal(string speciesId, AnimalHabitat habitat, Vector2 point, float yaw, float scale = 1, AnimalController follow = null, Vector2? home = null, float range = 25)
        {
            var species = AnimalCatalog.Find(speciesId);
            var root = new GameObject("Animal · " + species.Name);
            root.transform.SetParent(habitat.transform.parent, false);
            root.transform.position = At(point, 0);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + species.Model + ".glb");
            GameObject model;
            var placeholder = prefab == null;
            if (!placeholder) model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else if (speciesId == "giraffe") model = PlaceholderAnimal("Giraffe", 1.9f, 1.9f, 0.95f, 2.1f, 22, 0.6f, Hex("C99A55"), null);
            else if (speciesId == "zebra") model = PlaceholderAnimal("Zebra", 0.8f, 1.45f, 0.6f, 0.65f, 45, 0.5f, Hex("E4E2DC"), Hex("2B2B2B"));
            else model = PlaceholderAnimal(species.Name, species.Height * 0.55f, species.Height * 1.1f, species.Height * 0.4f, species.Height * 0.3f, 50, species.Height * 0.3f, Hex("FF00FF"), null);
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = model.transform.localScale * scale;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;      // bones are animated in code
            root.AddComponent<AnimalMovement>();
            var animal = root.AddComponent<AnimalController>();
            animal.SpeciesId = speciesId;
            animal.Habitat = habitat;
            animal.Follow = follow;
            animal.SizeScale = scale;
            animal.PlaceholderModel = placeholder;
            animal.HomeOffset = home.HasValue ? new Vector3(home.Value.x - habitat.transform.position.x, 0, home.Value.y - habitat.transform.position.z) : Vector3.zero;
            animal.HomeRange = range;
            root.AddComponent<AnimalAnimator>();
            root.AddComponent<AnimalLODController>();
            root.AddComponent<AudioSource>().playOnAwake = false;
            root.AddComponent<AnimalAudioController>();
            return animal;
        }

        private static void SavannahAnimals(AnimalHabitat habitat)
        {
            var west = new Vector2(-34, 44);
            var mother = Animal("elephant", habitat, new Vector2(-38, 44), 120, 1, null, west, 30);
            Animal("elephant", habitat, new Vector2(-46, 52), 80, 0.92f, null, west, 30);
            Animal("elephant", habitat, new Vector2(-35, 49), 140, 0.5f, mother, west, 20);       // calf
            var east = new Vector2(40, 42);
            Animal("giraffe", habitat, new Vector2(42, 46), 200, 1, null, east, 26);
            Animal("giraffe", habitat, new Vector2(50, 50), 250, 0.93f, null, east, 26);
            var plain = new Vector2(8, 30);
            for (var i = 0; i < 3; i++) Animal("zebra", habitat, new Vector2(4 + i * 4, 28 + (i % 2) * 5), 30 + i * 70, 1 - i * 0.04f, null, plain, 28);
            var north = new Vector2(-6, 68);
            for (var i = 0; i < 4; i++) Animal("antelope", habitat, new Vector2(-12 + i * 4.5f, 66 + (i % 2) * 4), i * 85, i == 0 ? 1 : 0.86f, null, north, 30);
            Animal("rhino", habitat, new Vector2(22, 58), 260, 1, null, new Vector2(20, 56), 18);
        }

        private static void FarmAnimals(AnimalHabitat habitat)
        {
            for (var i = 0; i < 3; i++) Animal("sheep", habitat, FarmCentre + new Vector2(-5 + i * 2.2f, -2 + i), i * 100, 1 - i * 0.08f, null, null, 9);
            Animal("pony", habitat, FarmCentre + new Vector2(5, -3), 300, 0.85f, null, null, 9);
            Animal("pig", habitat, FarmCentre + new Vector2(2, 3), 40, 1, null, null, 8);
            for (var i = 0; i < 3; i++) Animal("chicken", habitat, FarmCentre + new Vector2(-1 + i * 1.5f, -5 + i * 0.7f), i * 130, 1, null, null, 8);
        }

        private static void Visitors()
        {
            var south = At(-3, -12.5f); var west = At(-80, 20); var east = At(80, 24); var cafe = At(50, -62); var hub = At(1, -54); var farm = At(-50, -59);
            Npc("visitor-1", "Grace", "Visitor", "BlueLace", south, 0, south, hub, cafe, east);
            Npc("visitor-2", "Oliver", "Visitor", "Executive", west, 90, west, hub, south);
            Npc("visitor-3", "Mei", "Visitor", "Schoolgirl", farm, 0, farm, hub, west);
            Npc("visitor-4", "Samir", "Visitor", "Contemplation", east, -90, east, cafe, south);
            Npc("visitor-5", "Elena", "Visitor", "UrbanTrench", cafe, 0, cafe, east, hub, farm);
        }

        // ------------------------------------------------------------------ planting
        private static void Planting(AnimalHabitat savannah)
        {
            var zone = Group("Zone · Planting");
            var random = new System.Random(2027);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            // Dry savannah grass in clumps (denser away from the trampled waterhole).
            var planted = 0;
            for (var attempt = 0; attempt < 4000 && planted < 650; attempt++)
            {
                var p = SavannahCentre + new Vector2(R(-1, 1) * SavannahRadii.x, R(-1, 1) * SavannahRadii.y);
                if (AnimalHabitat.Ellipse(SavannahCentre, SavannahRadii - Vector2.one * 2, p) > 1) continue;
                if (Vector2.Distance(p, Waterhole) < WaterholeRadius + 1 || Vector2.Distance(p, Kopje) < 5) continue;
                if (Mathf.PerlinNoise(p.x * 0.06f + 4, p.y * 0.06f + 1) < 0.38f) continue;
                var name = Mathf.PerlinNoise(p.x * 0.05f + 2, p.y * 0.05f + 8) > 0.62f || Vector2.Distance(p, Waterhole) < 24 ? "GrassMeadow" : "GrassDry";
                if (PlacePlant(name, At(p, -0.03f), R(1f, 1.9f), R(0, 360), zone) != null) planted++;
            }
            // Trees around the visitor areas and along the boundary (never on paths, plazas or decks).
            var trees = 0;
            for (var attempt = 0; attempt < 3000 && trees < 70; attempt++)
            {
                var p = new Vector2(R(-126, 126), R(-150, 120));
                if (AnimalHabitat.Ellipse(SavannahCentre, SavannahRadii + Vector2.one * 5, p) < 1) continue;
                if (AnimalHabitat.Ellipse(FarmCentre, FarmRadii + Vector2.one * 3, p) < 1) continue;
                if (PathDistance(p) < 6 || keepClear.Any(area => Vector2.Distance(area.centre, p) < area.radius + 3)) continue;
                if (Mathf.Abs(p.y + 84) < 4) continue;
                var name = random.NextDouble() < 0.7 ? "OakTree" : "PineTree";
                if (PlacePlant(name, At(p, -0.05f), R(0.7f, 1.1f), R(0, 360), zone) == null) break;
                keepClear.Add((p, 5));
                trees++;
            }
            foreach (var x in new[] { -30f, -15, 15, 30 })
                Model("Assets/Art/beach/HibiscusBush.glb", At(x, -68), x * 9, 1.6f, "Flowering bush");
        }
    }
}
