using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.LifeBuild;
using static ContextSpeak.Editor.AirportKit;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Build Tropical Beach Resort → Assets/ContextSpeak/Scenes/TropicalBeach.unity.
    /// Palm Bay: a procedural island terrain (sand / wet sand / grass / rock / path layers with normal maps),
    /// a Gerstner ocean, wind-animated palms and plants, the café and juice bar, Coral Kitchen restaurant,
    /// the beach club, watersports centre, dive hut, marina and pier, the Palm Bay Resort hotel (lobby,
    /// garden and ocean-view rooms, villas, pool), souvenir stalls, the sunset headland and its trail,
    /// rideable craft, buoy courses, guided rides, reef fish, litter and the station back to Metro City.
    ///
    /// Island layout: centre C, shoreline radius R. Site(θ, s): θ = degrees round the coast (0 = main beach,
    /// negative = west), s = metres from the shoreline (negative inland, positive out to sea).
    public static class BeachBuilder
    {
        public const string ScenePath = Scenes + "/TropicalBeach.unity";
        public const string Art = "Assets/Art/beach/";
        private const string Folder = Data + "/Beach";
        private static readonly Vector3 C = new Vector3(0, 0, 260);
        private const float R = 260;
        private static readonly Vector3 TerrainOrigin = new Vector3(-420, -25, -160);
        private const float TerrainSize = 840, TerrainHeight = 50;

        private static readonly Color Sand = Hex("E9D9B4"), Thatch = Hex("A8834F"), Timber = Hex("8A6240"), DarkTimber = Hex("5B3E28"),
            Plaster = Hex("F3EDE2"), Coral = Hex("E8735A"), Lagoon = Hex("2BB3B1"), Palm = Hex("3E7D3A"), Cream = Hex("FFF6E5"), Ink = Hex("2A2522");

        private static Terrain terrain;
        private static Mesh pyramid;
        private static readonly List<(Vector2 centre, float radius)> keepClear = new List<(Vector2, float)>();
        private static readonly Dictionary<string, Material> windMaterials = new Dictionary<string, Material>();

        // ------------------------------------------------------------------ geometry helpers
        private struct Frame
        {
            public Vector3 O;
            public float Yaw;
            public Vector3 P(float x, float y, float z) => O + Quaternion.Euler(0, Yaw, 0) * new Vector3(x, y, z);
            public float Y(float local) => Yaw + local;
        }

        /// Point on (or off) the coast: θ degrees round the island, s metres seaward of the shoreline.
        private static Vector3 Coast(float degrees, float s)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return C + new Vector3(Mathf.Sin(radians), 0, -Mathf.Cos(radians)) * (R + s);
        }

        private static float Ground(Vector3 point) => terrain.SampleHeight(new Vector3(point.x, 0, point.z)) + TerrainOrigin.y;
        private static Vector3 OnGround(Vector3 point, float lift = 0.02f) { point.y = Ground(point) + lift; return point; }
        private static Vector3 OnGround(float degrees, float s, float lift = 0.02f) => OnGround(Coast(degrees, s), lift);

        /// A building plot facing the sea: floor level = highest ground under the footprint + lift; foundation below.
        private static Frame Site(float degrees, float s, float width, float depth, float lift = 0.25f, bool foundation = true, Color? base_ = null)
        {
            var frame = new Frame { O = Coast(degrees, s), Yaw = -degrees };
            var high = float.MinValue;
            var low = float.MaxValue;
            for (var x = -0.5f; x <= 0.5f; x += 0.25f)
                for (var z = -0.5f; z <= 0.5f; z += 0.25f)
                {
                    var g = Ground(frame.P(x * width, 0, z * depth));
                    high = Mathf.Max(high, g);
                    low = Mathf.Min(low, g);
                }
            frame.O.y = high + lift;
            if (foundation)
            {
                var drop = frame.O.y - low + 0.8f;
                Paint(Box("Foundation", frame.P(0, -drop / 2, 0), new Vector3(width, drop, depth), base_ ?? Hex("CDBFA6"), true, null, frame.Yaw), base_ ?? Hex("CDBFA6"), 0.2f);
            }
            keepClear.Add((new Vector2(frame.O.x, frame.O.z), Mathf.Max(width, depth) * 0.6f + 3));
            return frame;
        }

        private static float H(float x, float z)
        {
            var point = new Vector2(x, z);
            var d = Vector2.Distance(point, new Vector2(C.x, C.z));
            var s = d - R;
            float h;
            if (s < -60)
            {
                var inland = -s - 60;
                var hills = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(90, 200, inland));
                h = 3 + hills * (6 + 10 * Mathf.PerlinNoise(x * 0.012f + 3, z * 0.012f + 7)) + Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 0.25f * (0.3f + hills);
            }
            else if (s < 0) h = Mathf.Lerp(0.15f, 3, Mathf.SmoothStep(0, 1, -s / 60f)) + Mathf.PerlinNoise(x * 0.08f, z * 0.08f) * 0.12f;
            else if (s < 40) h = Mathf.Lerp(0.15f, -3, s / 40f);
            else if (s < 140) h = Mathf.Lerp(-3, -10, (s - 40) / 100f);
            else h = Mathf.Lerp(-10, -20, Mathf.Clamp01((s - 140) / 200f));
            if (s > 0) h += (Mathf.PerlinNoise(x * 0.04f + 11, z * 0.04f) - 0.5f) * 0.8f;
            // The sunset headland (rocky promontory, west).
            var headland = new Vector2(Coast(-42, -4).x, Coast(-42, -4).z);
            h += 9.5f * Mathf.Exp(-Vector2.SqrMagnitude(point - headland) / (2 * 22f * 22f));
            // The reef: a shallow shelf off the west rocks for snorkelling, dropping to the dive site.
            var reef = new Vector2(Coast(-28, 38).x, Coast(-28, 38).z);
            var reefLift = 0.8f * Mathf.Exp(-Vector2.SqrMagnitude(point - reef) / (2 * 16f * 16f));
            h += reefLift * (0.75f + 0.5f * Mathf.PerlinNoise(x * 0.3f, z * 0.3f));
            return h;
        }

        // ------------------------------------------------------------------ menu
        [UnityEditor.MenuItem("ContextSpeak/Build Tropical Beach Resort")]
        public static void BuildMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Missing.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("ContextSpeak", "Checking dependencies…", 0.05f);
                CafeBuild.ConfigurePipeline();
                foreach (var file in new[] { "CoconutPalm1", "CoconutPalm2", "CoconutPalm3", "CoconutPalm4", "BananaPlant", "HibiscusBush", "BirdOfParadise", "TropicalFern", "BeachGrass", "SeaAlmondTree",
                             "BeachShell", "Driftwood", "CoastRock", "SunLounger", "BeachUmbrella", "Coral", "FishBlue", "FishClown", "FishYellow", "Kayak", "JetSki", "Speedboat", "Sailboat", "Yacht", "Surfboard", "Paddleboard" })
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(Art + file + ".glb") == null) Missing.Add(Art + file + ".glb (not imported — placeholders will be used)");
                if (Shader.Find("ContextSpeak/Ocean") == null) Missing.Add("Shader ContextSpeak/Ocean did not compile — ocean uses a flat Lit material");
                if (Shader.Find("ContextSpeak/Foliage Wind") == null) Missing.Add("Shader ContextSpeak/Foliage Wind did not compile — vegetation will not sway");
                Directory.CreateDirectory(Folder);
                Directory.CreateDirectory(Scenes);
                AssetDatabase.Refresh();
                Build();
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            var report = "Tropical beach built: " + ScenePath + " (registered in Build Settings). Rebuild Metro City to add the Coastal Line entrance at Central Station.";
            if (Missing.Count > 0) Debug.LogWarning(report + "\nMissing assets / placeholders:\n- " + string.Join("\n- ", Missing.Distinct()));
            else Debug.Log(report);
            var errors = BeachChecks.Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak beach checks passed.");
            else Debug.LogError("ContextSpeak beach checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static void Build()
        {
            keepClear.Clear();
            windMaterials.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Palm Bay");
            Daylight("TropicalBeach", "DCEFF5");
            var flow = new GameObject("Beach systems").AddComponent<BeachFlow>();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Sculpting the island…", 0.12f);
            BuildTerrain();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Filling the ocean…", 0.25f);
            BuildOcean();
            pyramid = Pyramid();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Building the resort…", 0.35f);
            Hotel(flow);
            Cafe(flow);
            Restaurant(flow);
            BeachClub();
            Watersports(flow);
            DiveHut();
            Marina(flow);
            Promenade();
            Viewpoint(flow);
            Station(flow);
            EditorUtility.DisplayProgressBar("ContextSpeak", "Reef and sea life…", 0.55f);
            Reef();
            EditorUtility.DisplayProgressBar("ContextSpeak", "Planting palms…", 0.65f);
            Vegetation();
            Beachfront(flow);
            People();
            Boundaries();
            flow.WatersportsZone = ZoneAround(Coast(22, -22), 26);
            flow.MapBounds = new Rect(-300, -160, 600, 680);
            flow.MapLabels = new List<string>
            {
                MapLabel("Coconut Café", -8, -42), MapLabel("Coral Kitchen", 12, -48), MapLabel("Beach club", -18, -30), MapLabel("Watersports", 22, -22),
                MapLabel("Dive hut", -25, -20), MapLabel("Marina & pier", 32, 10), MapLabel("Palm Bay Resort", 0, -120), MapLabel("Sunset Point", -42, -4),
                MapLabel("Reef", -28, 38), MapLabel("Lifeguard", 3, -14), MapLabel("Station", -12, -128), MapLabel("Souvenirs", 0, -66),
            };

            EditorUtility.DisplayProgressBar("ContextSpeak", "Baking navigation…", 0.8f);
            var (player, camera) = Player(flow.Spawn.position, flow.Spawn.eulerAngles.y);
            camera.farClipPlane = 2500;
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture = true;      // ocean colour by water depth, shore foam
            data.requiresColorTexture = true;      // ocean refraction
            var voxel = WorldBuild.VoxelSize;
            WorldBuild.VoxelSize = 0.2f;           // a large outdoor area: coarser voxels keep the bake quick
            try { Finish(player, camera, flow, "Palm Bay", "TropicalBeach"); }
            finally { WorldBuild.VoxelSize = voxel; }
            var systems = flow.gameObject;
            var swim = player.gameObject.AddComponent<Swimming>();
            swim.Player = player;
            flow.Swim = swim;
            flow.Observer = systems.AddComponent<MarineObserver>();
            systems.AddComponent<UnderwaterView>();
            systems.AddComponent<FoliageWind>();
            var prints = systems.AddComponent<Footprints>();
            prints.Player = player.transform;
            prints.Print = Surface(new Color(0.55f, 0.45f, 0.33f, 0.45f), 0.1f);

            EditorUtility.DisplayProgressBar("ContextSpeak", "Sky and weather…", 0.9f);
            var sky = SkyInstaller.Install();
            if (sky != null)
            {
                sky.NorthOffset = 165;             // the sun sets over the sea, seen from Sunset Point
                sky.SetTime(10.5f);
                EditorUtility.SetDirty(sky);
            }
            LifeBuild.Save(scene, ScenePath);
        }

        private static string MapLabel(string name, float degrees, float s) { var p = Coast(degrees, s); return name + "|" + p.x.ToString("0") + "|" + p.z.ToString("0"); }

        private static Bounds ZoneAround(Vector3 centre, float size) => new Bounds(OnGround(centre, 1), new Vector3(size, 8, size));

        // ------------------------------------------------------------------ terrain
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
            data.terrainLayers = new[]
            {
                Layer("Sand", Hex("E6D3A8"), 0.35f, 3, 101),
                Layer("WetSand", Hex("B79F78"), 0.2f, 3, 202),
                Layer("Grass", Hex("6F9140"), 0.5f, 4, 303),
                Layer("Rock", Hex("8D857B"), 0.6f, 6, 404),
                Layer("Path", Hex("CDBB98"), 0.25f, 2.5f, 505),
            };
            const int alpha = 512;
            data.alphamapResolution = alpha;
            var weights = new float[alpha, alpha, 5];
            var trails = Trails();
            for (var j = 0; j < alpha; j++)
                for (var i = 0; i < alpha; i++)
                {
                    var x = TerrainOrigin.x + (i + 0.5f) / alpha * TerrainSize;
                    var z = TerrainOrigin.z + (j + 0.5f) / alpha * TerrainSize;
                    var s = Vector2.Distance(new Vector2(x, z), new Vector2(C.x, C.z)) - R;
                    var steep = data.GetSteepness((i + 0.5f) / alpha, (j + 0.5f) / alpha);
                    var height = H(x, z);
                    var wet = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2, 9, Mathf.Abs(s + 2)));
                    if (s > 6) wet = Mathf.Max(wet, 0.6f);
                    var grass = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-55, -72, s)) * (0.75f + 0.5f * Mathf.PerlinNoise(x * 0.07f, z * 0.07f));
                    var rock = Mathf.Max(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 34, steep)), height > 5 && s > -30 ? 0.8f : 0, s > 20 && height > -3.2f ? 0.7f : 0);
                    var path = 0f;
                    foreach (var trail in trails) path = Mathf.Max(path, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.2f, 2.1f, DistanceToPolyline(new Vector2(x, z), trail))));
                    var sand = Mathf.Clamp01(1 - wet - grass - rock);
                    var w = new[] { sand, wet * (1 - rock), grass * (1 - rock), rock, path };
                    for (var k = 0; k < 4; k++) w[k] *= 1 - path;
                    var total = w.Sum();
                    for (var k = 0; k < 5; k++) weights[j, i, k] = total > 0 ? w[k] / total : (k == 0 ? 1 : 0);
                }
            data.SetAlphamaps(0, 0, weights);
            Replace(data, Folder + "/PalmBayTerrain.asset");
            var item = Terrain.CreateTerrainGameObject(data);
            item.name = "Palm Bay terrain";
            item.transform.position = TerrainOrigin;
            item.isStatic = true;
            terrain = item.GetComponent<Terrain>();
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                Replace(material, Folder + "/PalmBayTerrain.mat");
                terrain.materialTemplate = material;
            }
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4;
            terrain.basemapDistance = 260;
            terrain.treeDistance = 400;
            terrain.Flush();
        }

        private static List<Vector2[]> Trails()
        {
            Vector2 P(float degrees, float s) { var p = Coast(degrees, s); return new Vector2(p.x, p.z); }
            return new List<Vector2[]>
            {
                new[] { P(0, -102), P(0, -80), P(-1, -62), P(0, -45) },                         // hotel → beach
                new[] { P(-12, -128), P(-10, -105), P(-4, -80), P(0, -80) },                     // station → resort
                new[] { P(-10, -62), P(10, -62), P(18, -60), P(28, -40), P(31, -8) },            // promenade → marina
                new[] { P(-10, -62), P(-22, -56), P(-32, -40), P(-38, -24), P(-42, -10) },       // sunset trail
                new[] { P(-8, -62), P(-8, -50) }, new[] { P(12, -62), P(12, -55) },              // café, restaurant
                new[] { P(5, -112), P(16, -112), P(26, -110) },                                   // to the villas
            };
        }

        private static float DistanceToPolyline(Vector2 point, Vector2[] line)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < line.Length; i++)
            {
                var a = line[i];
                var ab = line[i + 1] - a;
                var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
            }
            return best;
        }

        /// A terrain layer with a generated albedo (tinted fractal noise) and a matching normal map.
        private static TerrainLayer Layer(string name, Color tint, float contrast, float tile, int seed)
        {
            var path = Folder + "/Textures/" + name;
            Directory.CreateDirectory(Folder + "/Textures");
            const int size = 256;
            var albedo = new Texture2D(size, size, TextureFormat.RGB24, true);
            var normal = new Texture2D(size, size, TextureFormat.RGB24, true);
            var field = new float[size, size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    float v = 0, amplitude = 0.5f, frequency = 4;
                    for (var octave = 0; octave < 5; octave++)
                    {
                        // Tileable: sample noise on a torus.
                        var u = x / (float)size * Mathf.PI * 2;
                        var w = y / (float)size * Mathf.PI * 2;
                        v += amplitude * Mathf.PerlinNoise(seed + Mathf.Cos(u) * frequency / 6.283f * 4 + Mathf.Sin(w) * 0.37f * frequency, seed * 0.5f + Mathf.Sin(u) * frequency / 6.283f * 4 + Mathf.Cos(w) * frequency / 6.283f * 4);
                        amplitude *= 0.5f;
                        frequency *= 2;
                    }
                    field[x, y] = v;
                    var shade = 1 + (v - 0.5f) * contrast * 2;
                    albedo.SetPixel(x, y, new Color(tint.r * shade, tint.g * shade, tint.b * shade));
                }
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = (field[(x + 1) % size, y] - field[(x + size - 1) % size, y]) * contrast * 6;
                    var dy = (field[x, (y + 1) % size] - field[x, (y + size - 1) % size]) * contrast * 6;
                    var n = new Vector3(-dx, -dy, 1).normalized;
                    normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
                }
            File.WriteAllBytes(path + ".png", albedo.EncodeToPNG());
            File.WriteAllBytes(path + "_N.png", normal.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(albedo);
            UnityEngine.Object.DestroyImmediate(normal);
            AssetDatabase.ImportAsset(path + ".png");
            AssetDatabase.ImportAsset(path + "_N.png");
            if (AssetImporter.GetAtPath(path + "_N.png") is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            var layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png"),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_N.png"),
                tileSize = new Vector2(tile, tile),
                normalScale = 1,
                smoothness = name == "WetSand" ? 0.55f : name == "Rock" ? 0.25f : 0.08f,
            };
            Replace(layer, path + ".terrainlayer");
            return layer;
        }

        // ------------------------------------------------------------------ ocean
        private static void BuildOcean()
        {
            // One grid, 2 m spacing over the 400 m round the bay, growing geometrically out to ~3 km (no seams).
            var coordinates = new List<float>();
            for (var i = -100; i <= 100; i++) coordinates.Add(i * 2f);
            var step = 2f;
            var edge = 200f;
            for (var i = 0; i < 27; i++) { step *= 1.237f; edge += step; coordinates.Add(edge); coordinates.Insert(0, -edge); }
            var n = coordinates.Count;
            var vertices = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (var j = 0; j < n; j++)
                for (var i = 0; i < n; i++)
                {
                    vertices[j * n + i] = new Vector3(coordinates[i], 0, coordinates[j]);
                    uv[j * n + i] = new Vector2(coordinates[i], coordinates[j]) / 50f;
                }
            var triangles = new List<int>((n - 1) * (n - 1) * 6);
            for (var j = 0; j + 1 < n; j++)
                for (var i = 0; i + 1 < n; i++)
                {
                    var a = j * n + i;
                    triangles.AddRange(new[] { a, a + n, a + 1, a + 1, a + n, a + n + 1 });
                }
            var mesh = new Mesh { name = "Palm Bay ocean", indexFormat = n * n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(edge * 2, 8, edge * 2));
            Replace(mesh, Folder + "/Ocean.asset");
            var ocean = new GameObject("Ocean");
            ocean.transform.position = new Vector3(0, OceanWaves.SeaLevel, -40);
            ocean.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ocean.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var shader = Shader.Find("ContextSpeak/Ocean");
            Material material;
            if (shader != null) material = new Material(shader);
            else { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", Lagoon); material.SetFloat("_Smoothness", 0.92f); }
            Replace(material, Folder + "/Ocean.mat");
            renderer.sharedMaterial = material;
            var waves = ocean.AddComponent<OceanWaves>();
            waves.IslandCentre = C;
            waves.ShoreRadius = R;
            // Swell rolling in from the open sea toward the beach (+Z).
            waves.Waves = new List<GerstnerWave>
            {
                new GerstnerWave(15, 48, 0.12f), new GerstnerWave(-5, 31, 0.10f), new GerstnerWave(30, 19, 0.09f),
                new GerstnerWave(-25, 11, 0.07f), new GerstnerWave(50, 7, 0.05f), new GerstnerWave(10, 4.2f, 0.04f),
            };
        }

        // ------------------------------------------------------------------ building helpers
        private static Mesh Pyramid()
        {
            var path = Folder + "/Pyramid.asset";
            var mesh = new Mesh { name = "Roof pyramid" };
            var apex = new Vector3(0, 1, 0);
            var corners = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                vertices.AddRange(new[] { a, apex, b });
                triangles.AddRange(new[] { vertices.Count - 3, vertices.Count - 2, vertices.Count - 1 });
            }
            vertices.AddRange(new[] { corners[0], corners[1], corners[2], corners[3] });
            var c = vertices.Count - 4;
            triangles.AddRange(new[] { c, c + 1, c + 2, c, c + 2, c + 3 });   // underside (ceiling)
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Replace(mesh, path);
            return mesh;
        }

        /// A thatched (or tiled) hip roof: a pyramid with a timber fascia.
        private static void Roof(Frame f, Vector3 centre, Vector2 size, float height, Color colour)
        {
            var roof = new GameObject("Roof");
            roof.transform.SetParent(Group("Roof", Vector3.zero), false);
            roof.transform.position = f.P(centre.x, centre.y, centre.z);
            roof.transform.rotation = Quaternion.Euler(0, f.Yaw, 0);
            roof.transform.localScale = new Vector3(size.x, height, size.y);
            roof.AddComponent<MeshFilter>().sharedMesh = pyramid;
            roof.AddComponent<MeshRenderer>().sharedMaterial = Surface(colour, 0.08f);
            roof.isStatic = true;
            Box("Fascia", f.P(centre.x, centre.y - 0.1f, centre.z - size.y / 2), new Vector3(size.x, 0.25f, 0.12f), DarkTimber, false, null, f.Yaw);
            Box("Fascia", f.P(centre.x, centre.y - 0.1f, centre.z + size.y / 2), new Vector3(size.x, 0.25f, 0.12f), DarkTimber, false, null, f.Yaw);
        }

        private static void Post(Frame f, float x, float z, float height, Color colour) =>
            Shape("Post", PrimitiveType.Cylinder, f.P(x, height / 2, z), new Vector3(0.22f, height / 2, 0.22f), colour, true, null, f.Yaw);

        private static void Deck(Frame f, Vector3 centre, Vector2 size, Color colour) =>
            Paint(Box("Deck", f.P(centre.x, centre.y - 0.06f, centre.z), new Vector3(size.x, 0.12f, size.y), colour, true, null, f.Yaw), colour, 0.3f);

        private static void LocalWall(Frame f, string name, Vector2 a, Vector2 b, float height, Color colour, params Vector2[] gaps) =>
            Wall(name, f.P(a.x, 0, a.y), f.P(b.x, 0, b.y), height, colour, gaps);

        private static void Counter(Frame f, Vector3 centre, float length, Color body, Color top)
        {
            Paint(Box("Counter", f.P(centre.x, centre.y + 0.5f, centre.z), new Vector3(length, 1.0f, 0.7f), body, true, null, f.Yaw), body, 0.35f);
            Paint(Box("Counter top", f.P(centre.x, centre.y + 1.03f, centre.z), new Vector3(length + 0.1f, 0.06f, 0.8f), top, false, null, f.Yaw), top, 0.7f);
        }

        private static void Stool(Frame f, float x, float z) => Shape("Bar stool", PrimitiveType.Cylinder, f.P(x, 0.38f, z), new Vector3(0.38f, 0.38f, 0.38f), Timber, true, null, f.Yaw);

        // ------------------------------------------------------------------ the hotel
        private static void Hotel(BeachFlow flow)
        {
            var f = Site(0, -120, 26, 18, 0.35f);
            var wall = Plaster;
            // Lobby: 26 × 18, glass front toward the sea with sliding doors.
            Paint(Box("Lobby floor", f.P(0, 0.02f, 9), new Vector3(26, 0.05f, 18), Hex("E2D2B6"), true, null, f.Yaw), Hex("E2D2B6"), 0.75f);
            LocalWall(f, "Lobby wall", new Vector2(-13, 18), new Vector2(13, 18), 6, wall);
            LocalWall(f, "Lobby wall", new Vector2(-13, 0), new Vector2(-13, 18), 6, wall, new Vector2(12, 2.4f));
            LocalWall(f, "Lobby wall", new Vector2(13, 0), new Vector2(13, 18), 6, wall, new Vector2(12, 2.4f));
            Curtain("Lobby glass", f.P(-13, 0, 0), f.P(13, 0, 0), 6, new Vector2(13, 3.2f));
            SlidingDoor("Hotel entrance", f.P(0, 0, 0), f.Y(0), 3.2f).Range = 3.4f;
            Paint(Box("Lobby ceiling", f.P(0, 6.1f, 9), new Vector3(26.4f, 0.2f, 18.4f), Cream, true, null, f.Yaw), Cream, 0.3f);
            Roof(f, new Vector3(0, 6.2f, 9), new Vector2(30, 22), 5, Hex("9C4A2F"));
            Board("PALM BAY RESORT", f.P(0, 7.0f, -0.4f), f.Y(0), new Vector2(10, 1.1f), Hex("1F5E5B"), Hex("F6E7C1"));
            Paint(Box("Canopy", f.P(0, 3.4f, -3), new Vector3(8, 0.2f, 6), Timber, true, null, f.Yaw), Timber, 0.4f);
            Post(f, -3.8f, -5.8f, 3.4f, DarkTimber); Post(f, 3.8f, -5.8f, 3.4f, DarkTimber);
            Counter(f, new Vector3(0, 0, 13), 7, DarkTimber, Hex("EDE3D2"));
            Board("RECEPTION", f.P(0, 3.4f, 17.85f), f.Y(0), new Vector2(4, 0.6f), DarkTimber, Cream);
            Npc("reception", "Ms Ana Lim", "Hotel receptionist", "Secretary", f.P(0, 0.02f, 14.2f), f.Y(180));
            for (var x = -9f; x <= 9; x += 18) { Box("Sofa", f.P(x, 0.4f, 6), new Vector3(3, 0.8f, 1), Hex("2F6E68"), true, null, f.Yaw); Plant(f.P(x, 0.02f, 16.5f), 1.8f); }
            for (var x = -8f; x <= 8; x += 8) Lamp(f.P(x, 5.7f, 9), 14, 1.4f, "FFE7C6");
            flow.HotelLobby = new Bounds(f.P(0, 2, 9), new Vector3(24, 4, 16));
            Board("ROOMS 101–102 →", f.P(-12.8f, 2.6f, 12), f.Y(90), new Vector2(3, 0.5f), Hex("1F5E5B"), Cream);
            Board("ROOMS 103–104 →", f.P(12.8f, 2.6f, 12), f.Y(-90), new Vector2(3, 0.5f), Hex("1F5E5B"), Cream);

            // Room wings: two garden rooms west, two ocean-view rooms east; doors open from the side gardens.
            var doors = new List<DoorController>();
            var rooms = new[] { ("101", -22f, 6f), ("102", -22f, 14f), ("103", 22f, 6f), ("104", 22f, 14f) };
            foreach (var (number, x, z) in rooms)
            {
                var room = new Frame { O = f.P(x, 0, z), Yaw = f.Y(x < 0 ? 90 : -90) };      // local −Z faces the lobby side door
                doors.Add(Room(room, number, false));
            }
            // Villas on the slope east of the resort, facing the sea.
            foreach (var (number, degrees) in new[] { ("V1", 16f), ("V2", 21f), ("V3", 26f) })
            {
                var villa = Site(degrees, -106, 9, 9, 0.5f);
                doors.Add(Room(villa, number, true));
            }
            flow.RoomDoors = doors.ToArray();

            // Pool terrace in front of the lobby.
            var pool = Site(0, -88, 30, 16, 0.2f, true, Hex("E8DFCF"));
            Paint(Box("Pool terrace", pool.P(0, 0, 0), new Vector3(30, 0.1f, 16), Hex("EFE6D6"), true, null, pool.Yaw), Hex("EFE6D6"), 0.4f);
            Paint(Box("Pool water", pool.P(0, 0.07f, 0), new Vector3(18, 0.05f, 7), new Color(0.2f, 0.75f, 0.85f, 0.75f), false, null, pool.Yaw), new Color(0.2f, 0.75f, 0.85f, 0.75f), 0.95f);
            Paint(Box("Pool edge", pool.P(0, 0.1f, -3.7f), new Vector3(18.6f, 0.08f, 0.4f), Hex("FFFFFF"), true, null, pool.Yaw), Hex("FFFFFF"), 0.5f);
            Paint(Box("Pool edge", pool.P(0, 0.1f, 3.7f), new Vector3(18.6f, 0.08f, 0.4f), Hex("FFFFFF"), true, null, pool.Yaw), Hex("FFFFFF"), 0.5f);
            Invisible("Pool barrier", pool.P(0, 0.6f, 0), new Vector3(18, 1, 7));
            for (var x = -11f; x <= 11; x += 3.6f)
                Model(Art + "SunLounger.glb", pool.P(x, 0.06f, -6), pool.Y(180), 0.75f, "Sun lounger");
            Lamp(pool.P(-12, 2.5f, 5), 8, 0.8f, "FFD8A0"); Lamp(pool.P(12, 2.5f, 5), 8, 0.8f, "FFD8A0");
        }

        /// A guest room or villa: hinged door (locked until the key card is tapped), bed, wardrobe, phone, tray.
        private static DoorController Room(Frame f, string number, bool villa)
        {
            var w = villa ? 9f : 7f;
            var d = villa ? 9f : 7.5f;
            var middle = f.P(0, 0, d / 2);
            keepClear.Add((new Vector2(middle.x, middle.z), 7));
            Paint(Box("Room floor " + number, f.P(0, 0.02f, d / 2), new Vector3(w, 0.05f, d), Hex("C9A57E"), true, null, f.Yaw), Hex("C9A57E"), 0.6f);
            if (!villa) Paint(Box("Room plinth " + number, f.P(0, -0.4f, d / 2), new Vector3(w, 0.8f, d), Hex("CDBFA6"), true, null, f.Yaw), Hex("CDBFA6"), 0.2f);
            LocalWall(f, "Room wall", new Vector2(-w / 2, 0), new Vector2(w / 2, 0), 3.2f, Plaster, new Vector2(w / 2, 1.1f));
            LocalWall(f, "Room wall", new Vector2(-w / 2, d), new Vector2(w / 2, d), 3.2f, Plaster);
            LocalWall(f, "Room wall", new Vector2(-w / 2, 0), new Vector2(-w / 2, d), 3.2f, Plaster);
            LocalWall(f, "Room wall", new Vector2(w / 2, 0), new Vector2(w / 2, d), 3.2f, Plaster);
            Window(f.P(-w / 4, 0, d), f.Y(0), 2.2f, 1.4f, 1.0f);
            Paint(Box("Room ceiling", f.P(0, 3.25f, d / 2), new Vector3(w + 0.2f, 0.12f, d + 0.2f), Cream, true, null, f.Yaw), Cream, 0.3f);
            Roof(f, new Vector3(0, 3.3f, d / 2), new Vector2(w + 1.6f, d + 1.6f), villa ? 2.8f : 2.2f, villa ? Thatch : Hex("9C4A2F"));
            var door = HingedDoor("Room " + number, f.P(0, 0.02f, 0), f.Y(0), 1.05f, DarkTimber);
            door.Locked = true;
            Board((villa ? "VILLA " : "ROOM ") + number, f.P(1.1f, 2.4f, -0.12f), f.Y(0), new Vector2(1.1f, 0.35f), DarkTimber, Cream);
            Spot("room-door:" + number, "Tap your key card · " + number, f.P(0, 1.2f, -1.0f), 1.6f);
            // Bed, wardrobe, phone, tray table.
            Paint(Box("Bed", f.P(w / 2 - 1.3f, 0.3f, d - 1.4f), new Vector3(2.0f, 0.5f, 2.2f), Hex("F8F6F2"), true, null, f.Yaw), Hex("F8F6F2"), 0.2f);
            Box("Headboard", f.P(w / 2 - 1.3f, 0.8f, d - 0.25f), new Vector3(2.1f, 1.2f, 0.1f), Timber, true, null, f.Yaw);
            Spot("sleep", "Sleep until morning", f.P(w / 2 - 1.3f, 0.9f, d - 2.6f), 1.8f);
            Paint(Box("Wardrobe", f.P(-w / 2 + 0.5f, 1.05f, d - 1.2f), new Vector3(0.6f, 2.1f, 1.6f), Timber, true, null, f.Yaw), Timber, 0.45f);
            Spot("wardrobe", "Open the wardrobe", f.P(-w / 2 + 1.3f, 1.2f, d - 1.2f), 1.6f);
            Table(f.P(-w / 2 + 1.2f, 0.02f, 1.8f), new Vector2(0.8f, 0.8f), Timber);
            Box("Phone", f.P(-w / 2 + 1.2f, 0.8f, 1.6f), new Vector3(0.2f, 0.08f, 0.15f), Ink, false, null, f.Yaw);
            Spot("roomservice", "Call room service", f.P(-w / 2 + 1.9f, 1.0f, 1.8f), 1.5f);
            Group("Room service tray · " + number, f.P(-w / 2 + 1.0f, 0.78f, 2.0f), f.Yaw);
            Lamp(f.P(0, 2.9f, d / 2), 7, 0.9f, "FFE2B8");
            if (villa) Model(Art + "SunLounger.glb", f.P(w / 2 - 1.5f, 0.02f, -2.2f), f.Y(180), 0.75f, "Villa lounger");
            return door;
        }

        // ------------------------------------------------------------------ café and juice bar
        private static void Cafe(BeachFlow flow)
        {
            var f = Site(-8, -42, 12, 8, 0.35f);
            Deck(f, new Vector3(0, 0, 0), new Vector2(12, 8), Timber);
            Deck(f, new Vector3(0, 0, -7), new Vector2(14, 6), Hex("B98E62"));
            foreach (var (x, z) in new[] { (-5.6f, -3.6f), (5.6f, -3.6f), (-5.6f, 3.6f), (5.6f, 3.6f) }) Post(f, x, z, 3.2f, DarkTimber);
            Roof(f, new Vector3(0, 3.2f, 0), new Vector2(14, 10), 3.2f, Thatch);
            LocalWall(f, "Café back wall", new Vector2(-6, 4), new Vector2(6, 4), 3.2f, Hex("D8C3A0"));
            Counter(f, new Vector3(-1, 0, 1.4f), 7, Hex("2F7F79"), Hex("F1E6CF"));
            Paint(Box("Pick-up counter", f.P(4.6f, 0.5f, 1.4f), new Vector3(2.4f, 1.0f, 0.7f), Coral, true, null, f.Yaw), Coral, 0.4f);
            flow.CafePickup = Group("Café pick-up", f.P(4.0f, 1.08f, 1.3f), f.Yaw);
            Board("PICK UP", f.P(4.6f, 2.5f, 1.4f), f.Y(0), new Vector2(1.6f, 0.4f), Coral, Color.white);
            Board("COCONUT CAFÉ  ·  JUICE BAR", f.P(0, 3.0f, -5.2f), f.Y(0), new Vector2(7, 0.8f), Hex("2F7F79"), Cream);
            Board("COFFEE · SMOOTHIES · FRESH JUICE\nCOCONUT WATER · SNACKS", f.P(-1, 2.3f, 3.85f), f.Y(0), new Vector2(5, 1.0f), Ink, Hex("FFD58A"));
            Npc("barista", "Kai", "Barista", "SkateboardBoy", f.P(-1, 0.02f, 2.4f), f.Y(180));
            for (var x = -3f; x <= 1; x += 1.3f) Stool(f, x, 0.4f);
            for (var i = 0; i < 3; i++)
            {
                var t = f.P(-4.5f + i * 4.5f, 0.02f, -7);
                Table(t, new Vector2(0.9f, 0.9f), Hex("F2E3C6"));
                Chair(f.P(-4.5f + i * 4.5f, 0.02f, -7.9f), f.Y(0), Hex("2F7F79"), "beach-cafe");
                Chair(f.P(-4.5f + i * 4.5f, 0.02f, -6.1f), f.Y(180), Hex("2F7F79"), "beach-cafe");
            }
            Lamp(f.P(0, 2.9f, 0), 9, 1.0f, "FFE2B0");
            flow.CafeZone = new Bounds(f.P(0, 1, -2), new Vector3(16, 6, 16));
        }

        // ------------------------------------------------------------------ Coral Kitchen
        private static void Restaurant(BeachFlow flow)
        {
            var f = Site(12, -48, 18, 20, 0.5f);
            Paint(Box("Restaurant floor", f.P(0, 0.02f, 4.5f), new Vector3(18, 0.05f, 11), Hex("D7C4A3"), true, null, f.Yaw), Hex("D7C4A3"), 0.6f);
            Deck(f, new Vector3(0, 0, -5), new Vector2(18, 9), Timber);
            LocalWall(f, "Restaurant wall", new Vector2(-9, 10), new Vector2(9, 10), 4, Plaster);
            LocalWall(f, "Restaurant wall", new Vector2(-9, -1), new Vector2(-9, 10), 4, Plaster);
            LocalWall(f, "Restaurant wall", new Vector2(9, -1), new Vector2(9, 10), 4, Plaster);
            LocalWall(f, "Kitchen wall", new Vector2(-9, 7), new Vector2(9, 7), 4, Plaster, new Vector2(4, 1.4f), new Vector2(13, 3.4f));
            Paint(Box("Restaurant ceiling", f.P(0, 4.1f, 4.5f), new Vector3(18.4f, 0.15f, 11.4f), Cream, true, null, f.Yaw), Cream, 0.3f);
            Roof(f, new Vector3(0, 4.15f, 4.5f), new Vector2(20, 13), 3.4f, Thatch);
            foreach (var x in new[] { -8.8f, -3, 3, 8.8f }) Post(f, x, -1, 4, DarkTimber);
            foreach (var x in new[] { -8.8f, 0, 8.8f }) Post(f, x, -9.3f, 1.0f, DarkTimber);
            Box("Deck rail", f.P(0, 1.0f, -9.4f), new Vector3(18, 0.08f, 0.08f), DarkTimber, true, null, f.Yaw);
            Board("CORAL KITCHEN", f.P(0, 3.6f, -1.15f), f.Y(0), new Vector2(6, 0.8f), Coral, Cream);
            // Host stand and the cashier's counter by the entrance; the open kitchen pass at the back.
            Box("Host stand", f.P(-7, 0.55f, -1.6f), new Vector3(0.8f, 1.1f, 0.5f), DarkTimber, true, null, f.Yaw);
            Npc("host", "Maya", "Restaurant host", "BlueLace", f.P(-7, 0.02f, -0.9f), f.Y(180));
            Counter(f, new Vector3(6.5f, 0, 5.8f), 3, DarkTimber, Hex("EFE6D6"));
            Npc("cashier", "Tom", "Cashier", "Plaid", f.P(6.5f, 0.02f, 6.6f), f.Y(180));
            Counter(f, new Vector3(-2, 0, 6.6f), 6, Hex("8F9AA0"), Hex("D9DEE1"));
            flow.Kitchen = Group("Kitchen pass", f.P(-2, 0.02f, 5.6f), f.Yaw);
            Npc("chef", "Chef Rui", "Chef", "Executive", f.P(-2, 0.02f, 8.5f), f.Y(180));
            Npc("waiter", "Leo", "Waiter", "SoccerCaptain", f.P(3, 0.02f, 5.4f), f.Y(180));
            Paint(Box("Grill", f.P(-6, 0.5f, 8.6f), new Vector3(2, 1, 0.8f), Hex("3A3A3A"), true, null, f.Yaw), Hex("3A3A3A"), 0.5f, 0.6f);
            Board("MENU\nGrilled snapper · Prawn curry · Poke bowl\nCoconut rice · Mango sticky rice", f.P(5, 2.6f, 9.85f), f.Y(0), new Vector2(4, 1.6f), Ink, Cream);
            // Inside tables and deck tables.
            foreach (var (x, z) in new[] { (-5.5f, 2f), (0f, 2f), (5.5f, 2f), (-5.5f, 4.6f), (0f, 4.6f) }) DiningTable(f, x, z, "beach-in");
            foreach (var (x, z) in new[] { (-6f, -4f), (0f, -4f), (6f, -4f), (-3f, -7.4f), (3f, -7.4f) }) DiningTable(f, x, z, "beach-out");
            for (var x = -6f; x <= 6; x += 6) Lamp(f.P(x, 3.7f, 4), 9, 1.1f, "FFE0B0");
            Lamp(f.P(0, 3, -5), 12, 0.8f, "FFD9A0");
            flow.RestaurantZone = new Bounds(f.P(0, 1, 0), new Vector3(20, 6, 22));
        }

        private static void DiningTable(Frame f, float x, float z, string group)
        {
            Table(f.P(x, 0.02f, z), new Vector2(1.0f, 0.9f), Hex("F4EAD8"));
            Chair(f.P(x, 0.02f, z - 0.75f), f.Y(0), group == "beach-in" ? Coral : Hex("2F7F79"), group);
            Chair(f.P(x, 0.02f, z + 0.75f), f.Y(180), group == "beach-in" ? Coral : Hex("2F7F79"), group);
        }

        // ------------------------------------------------------------------ beach club
        private static void BeachClub()
        {
            var f = Site(-18, -30, 10, 7, 0.3f);
            Deck(f, Vector3.zero, new Vector2(10, 7), Hex("C8A27A"));
            foreach (var (x, z) in new[] { (-4.6f, -3.1f), (4.6f, -3.1f), (-4.6f, 3.1f), (4.6f, 3.1f) }) Post(f, x, z, 3.0f, DarkTimber);
            Roof(f, new Vector3(0, 3.0f, 0), new Vector2(12, 9), 2.8f, Thatch);
            Counter(f, new Vector3(0, 0, 0.8f), 6, Hex("1F5E5B"), Hex("F1E6CF"));
            for (var x = -2.4f; x <= 2.4f; x += 1.2f) Stool(f, x, -0.2f);
            Board("DRIFTWOOD BEACH CLUB", f.P(0, 2.7f, -3.6f), f.Y(0), new Vector2(6, 0.6f), Hex("1F5E5B"), Cream);
            Npc("bartender", "Nico", "Bartender", "UrbanTrench", f.P(0, 0.02f, 1.8f), f.Y(180));
            Lamp(f.P(0, 2.7f, 0), 8, 1.0f, "FFC98A");
            for (var i = 0; i < 4; i++)
            {
                var bed = f.P(-6 + i * 4, 0, -8);
                bed = OnGround(bed, 0.02f);
                Paint(Box("Day bed", bed + Vector3.up * 0.25f, new Vector3(2.2f, 0.4f, 2.2f), Hex("F5F1EA"), true, null, f.Yaw), Hex("F5F1EA"), 0.2f);
                Model(Art + "BeachUmbrella.glb", bed + Quaternion.Euler(0, f.Yaw, 0) * new Vector3(1.4f, 0, 0), f.Yaw, 2.6f, "Beach umbrella");
            }
        }

        // ------------------------------------------------------------------ watersports centre
        private static void Watersports(BeachFlow flow)
        {
            var f = Site(22, -22, 10, 7, 0.4f);
            Deck(f, Vector3.zero, new Vector2(10, 7), Hex("D9C29E"));
            LocalWall(f, "Shack wall", new Vector2(-5, 3.5f), new Vector2(5, 3.5f), 3, Hex("2C8FB8"));
            LocalWall(f, "Shack wall", new Vector2(-5, -1), new Vector2(-5, 3.5f), 3, Hex("2C8FB8"));
            LocalWall(f, "Shack wall", new Vector2(5, -1), new Vector2(5, 3.5f), 3, Hex("2C8FB8"));
            foreach (var x in new[] { -4.8f, 4.8f }) Post(f, x, -3.3f, 3, DarkTimber);
            Roof(f, new Vector3(0, 3.0f, 0.5f), new Vector2(12, 9), 2.2f, Hex("E8E2D4"));
            Counter(f, new Vector3(0, 0, 0.6f), 5, Hex("F2C14E"), Hex("FFFFFF"));
            Board("PALM BAY WATERSPORTS", f.P(0, 2.7f, -1.1f), f.Y(0), new Vector2(6, 0.6f), Hex("F2C14E"), Ink);
            var prices = string.Join("\n", BeachActivities.All.Take(6).Select(a => a.Name + "  " + (a.Cents == 0 ? "free" : CurrencyManager.Format(a.Cents))));
            Board(prices, f.P(-3.6f, 1.8f, 3.35f), f.Y(0), new Vector2(2.6f, 1.8f), Hex("FFFFFF"), Ink);
            var more = string.Join("\n", BeachActivities.All.Skip(6).Select(a => a.Name + "  " + CurrencyManager.Format(a.Cents)));
            Board(more, f.P(3.6f, 1.8f, 3.35f), f.Y(0), new Vector2(2.6f, 1.8f), Hex("FFFFFF"), Ink);
            Npc("watersports", "Jess", "Watersports instructor", "SoccerCaptain", f.P(0, 0.02f, 1.6f), f.Y(180));
            Lamp(f.P(0, 2.7f, 1), 8, 0.9f, "FFF0D8");
            // Board rack and the rental fleet at the water's edge.
            for (var i = 0; i < 4; i++) Model(Art + "Surfboard.glb", f.P(-6 + i * 0.6f, 0, -1.5f), f.Y(90), 2.2f, "Surfboard on rack");
            var crafts = new List<WaterCraft>();
            WaterCraft Craft(string file, string id, float degrees, float s, float height, float speed)
            {
                var dock = Group("Dock · " + id, Coast(degrees, s) + Vector3.up * OceanWaves.SeaLevel, -degrees);
                var model = Model(Art + file, dock.position, dock.eulerAngles.y + 180, height, id);
                var craft = model.gameObject.AddComponent<WaterCraft>();
                craft.ActivityId = id;
                craft.Dock = dock;
                craft.MaxSpeed = speed;
                craft.BoundaryCentre = Coast(15, 80);
                craft.BoundaryRadius = 150;
                craft.Prompt = "Ride";
                craft.Range = 3.2f;
                crafts.Add(craft);
                return craft;
            }
            Craft("JetSki.glb", "jetski", 24, 6, 1.0f, 16);
            Craft("Kayak.glb", "kayak", 19, 4, 0.5f, 3.2f);
            Craft("Paddleboard.glb", "paddleboard", 17, 4, 0.25f, 2.6f);
            flow.Crafts = crafts.ToArray();
            flow.JetSkiCourse = Course("Jet ski course", "jetski", new[] { (26f, 60f), (36f, 95f), (24f, 130f), (12f, 95f) }, Hex("FF6A00"));
            flow.KayakCourse = Course("Kayak loop", "kayak", new[] { (10f, 22f), (2f, 34f), (-6f, 22f) }, Hex("F2C14E"));
            Npc("surf", "Mako", "Surf instructor", "SkateboardBoy", OnGround(8, -8), 180 - 8);
            flow.SurfStart = Group("Surf start", OnGround(8, -6), -8);
            Model(Art + "Surfboard.glb", OnGround(9.5f, -8), 70, 2.2f, "Lesson surfboard");
        }

        private static BuoyCourse Course(string name, string id, (float degrees, float s)[] points, Color colour)
        {
            var root = Group(name, Vector3.zero);
            var course = root.gameObject.AddComponent<BuoyCourse>();
            course.ActivityId = id;
            course.Gates = points.Select((p, i) =>
            {
                var buoy = Shape("Course buoy " + (i + 1), PrimitiveType.Sphere, Coast(p.degrees, p.s) + Vector3.up * 0.2f, Vector3.one * 1.1f, colour, false, root);
                buoy.isStatic = false;
                buoy.AddComponent<Floater>().Draft = 0.35f;
                return buoy.transform;
            }).ToArray();
            return course;
        }

        // ------------------------------------------------------------------ dive hut
        private static void DiveHut()
        {
            var f = Site(-25, -20, 7, 6, 0.4f);
            Deck(f, Vector3.zero, new Vector2(7, 6), Hex("C8A27A"));
            foreach (var (x, z) in new[] { (-3.2f, -2.7f), (3.2f, -2.7f), (-3.2f, 2.7f), (3.2f, 2.7f) }) Post(f, x, z, 2.8f, DarkTimber);
            Roof(f, new Vector3(0, 2.8f, 0), new Vector2(8.5f, 7.5f), 2.4f, Thatch);
            Counter(f, new Vector3(0, 0, 0.8f), 3.6f, Hex("0F3D8C"), Hex("F1F1F1"));
            Board("REEF DIVERS  ·  SNORKEL & SCUBA", f.P(0, 2.5f, -3.0f), f.Y(0), new Vector2(5, 0.5f), Hex("0F3D8C"), Cream);
            for (var i = 0; i < 3; i++) Shape("Dive tank", PrimitiveType.Cylinder, f.P(2.4f, 0.4f, 2.2f - i * 0.4f), new Vector3(0.18f, 0.35f, 0.18f), Hex("F2C14E"), false, null, f.Yaw);
            Npc("dive", "Rina", "Dive instructor", "VirtualGuide", f.P(0, 0.02f, 1.7f), f.Y(180));
            Lamp(f.P(0, 2.5f, 0), 7, 0.8f, "FFF0D8");
        }

        // ------------------------------------------------------------------ marina and pier
        private static void Marina(BeachFlow flow)
        {
            const float degrees = 32;
            var deck = OceanWaves.SeaLevel + 1.3f;
            var start = Coast(degrees, -8);
            var end = Coast(degrees, 72);
            var along = (end - start).normalized;
            var length = Vector3.Distance(start, end);
            var yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
            var middle = (start + end) / 2;
            Paint(Box("Pier deck", new Vector3(middle.x, deck - 0.1f, middle.z), new Vector3(4, 0.2f, length), Timber, true, null, yaw), Timber, 0.3f);
            Paint(Box("Pier head", new Vector3(end.x, deck - 0.1f, end.z) + along * 4, new Vector3(14, 0.2f, 8), Timber, true, null, yaw), Timber, 0.3f);
            // A ramp from the sand up to the deck.
            var foot = OnGround(Coast(degrees, -16), 0);
            var top = new Vector3(start.x, deck, start.z);
            var ramp = top - foot;
            var rampLength = ramp.magnitude;
            var rampItem = Paint(Box("Pier ramp", (foot + top) / 2, new Vector3(4, 0.2f, rampLength), Timber, true, null, yaw), Timber, 0.3f);
            rampItem.transform.rotation = Quaternion.LookRotation(ramp.normalized, Vector3.up);
            for (var t = 0f; t <= length + 4; t += 4)
                foreach (var side in new[] { -1.9f, 1.9f })
                {
                    var pile = start + along * t + Vector3.Cross(Vector3.up, along) * side;
                    var bed = Ground(pile);
                    Shape("Pile", PrimitiveType.Cylinder, new Vector3(pile.x, (bed + deck) / 2, pile.z), new Vector3(0.3f, (deck - bed) / 2 + 0.2f, 0.3f), DarkTimber, false);
                    if (t > 2) Box("Pier rail", new Vector3(pile.x, deck + 0.5f, pile.z), new Vector3(0.08f, 1.0f, 0.08f), DarkTimber, true);
                }
            var right = Vector3.Cross(Vector3.up, along);
            Invisible("Pier rail west", middle + right * -2.05f + Vector3.up * (deck + 0.6f), new Vector3(0.1f, 1.2f, length));
            Invisible("Pier rail east", middle + right * 2.05f + Vector3.up * (deck + 0.6f), new Vector3(0.1f, 1.2f, length));
            Board("PALM BAY MARINA  ·  BOAT TOURS · FISHING", new Vector3(start.x, deck + 2.6f, start.z) - along * 1.5f, yaw, new Vector2(5.5f, 0.6f), Hex("0F3D8C"), Cream);
            Shape("Arch post", PrimitiveType.Cylinder, new Vector3(start.x, deck + 1.2f, start.z) - along * 1.5f - right * 2.8f, new Vector3(0.2f, 1.6f, 0.2f), DarkTimber, true);
            Shape("Arch post", PrimitiveType.Cylinder, new Vector3(start.x, deck + 1.2f, start.z) - along * 1.5f + right * 2.8f, new Vector3(0.2f, 1.6f, 0.2f), DarkTimber, true);
            Npc("captain", "Captain Joe", "Boat captain", "OfficerJames", new Vector3(start.x, deck, start.z) + along * 6 + right * 1.2f, yaw + 180);
            Board("FISHING SPOT", new Vector3(end.x, deck + 1.8f, end.z) + along * 7.5f, yaw + 180, new Vector2(2.2f, 0.4f), Hex("F2C14E"), Ink);
            for (var t = 10f; t < length; t += 20) StreetLamp(new Vector3(start.x, deck, start.z) + along * t + right * 1.8f, yaw - 90);

            // Moored boats and the guided rides (each boat sails its loop with the player aboard).
            var rides = new List<GuidedRide>();
            GuidedRide Boat(string file, string id, float t, float side, float height, float speed, params (float d, float s)[] route)
            {
                var position = start + along * t + right * side;
                var model = Model(Art + file, new Vector3(position.x, OceanWaves.SeaLevel - 0.2f, position.z), yaw, height, id);
                model.gameObject.AddComponent<Floater>().Draft = 0.25f;
                var ride = model.gameObject.AddComponent<GuidedRide>();
                ride.ActivityId = id;
                ride.Speed = speed;
                ride.Route = route.Select(p => Coast(p.d, p.s)).Prepend(model.position).Append(model.position).ToArray();
                rides.Add(ride);
                return ride;
            }
            Boat("Speedboat.glb", "boattour", 20, 5.5f, 1.6f, 9, (40, 120), (20, 220), (-20, 230), (-45, 120), (-30, 60), (15, 60));
            var banana = Boat("Speedboat.glb", "banana", 40, 5.5f, 1.6f, 10, (30, 110), (10, 150), (-10, 110), (10, 80));
            var tow = Shape("Banana boat", PrimitiveType.Capsule, banana.transform.position - along * 8, new Vector3(0.9f, 2.6f, 0.9f), Yellow, false, banana.transform);
            tow.transform.rotation = Quaternion.LookRotation(Vector3.up, along);
            var parasail = Boat("Speedboat.glb", "parasail", 55, -5.5f, 1.6f, 12, (40, 140), (15, 200), (-15, 170), (0, 110));
            var chute = Shape("Parasail canopy", PrimitiveType.Sphere, parasail.transform.position + Vector3.up * 26 - along * 30, new Vector3(9, 2.2f, 6), Coral, false, parasail.transform);
            chute.SetActive(true);
            parasail.Height = 0;
            Boat("Yacht.glb", "yacht", 64, -7.5f, 5.5f, 6, (50, 160), (30, 260), (-10, 280), (-40, 200), (-35, 110), (10, 120));
            var sail = Model(Art + "Sailboat.glb", new Vector3(Coast(38, 40).x, OceanWaves.SeaLevel, Coast(38, 40).z), yaw + 30, 8, "Moored sailboat");
            sail.gameObject.AddComponent<Floater>();
            flow.Rides = rides.ToArray();
        }

        // ------------------------------------------------------------------ promenade, souvenirs, lifeguard
        private static void Promenade()
        {
            for (var degrees = -10f; degrees <= 18; degrees += 4)
                StreetLamp(OnGround(degrees, -64, 0), degrees);
            var colours = new[] { Coral, Hex("2C8FB8"), Hex("F2C14E") };
            for (var i = 0; i < 3; i++)
            {
                var f = Site(-2 + i * 3.2f, -68, 3.6f, 2.6f, 0.15f, false);
                Box("Stall counter", f.P(0, 0.5f, 0), new Vector3(3, 1.0f, 0.8f), Timber, true, null, f.Yaw);
                foreach (var x in new[] { -1.5f, 1.5f }) Post(f, x, 0.9f, 2.5f, DarkTimber);
                Paint(Box("Stall awning", f.P(0, 2.55f, 0.2f), new Vector3(3.6f, 0.1f, 2.4f), colours[i], false, null, f.Yaw), colours[i], 0.2f);
                for (var k = 0; k < 5; k++)
                    Shape("Souvenir", k % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, f.P(-1.2f + k * 0.6f, 1.1f, 0), Vector3.one * 0.2f, Color.HSVToRGB((i * 0.3f + k * 0.13f) % 1, 0.6f, 0.9f), false, null, f.Yaw);
                if (i == 1)
                {
                    Board("SOUVENIRS · HATS · SHELLS · POSTCARDS", f.P(0, 2.9f, -1.0f), f.Y(0), new Vector2(3.6f, 0.4f), Ink, Hex("FFD58A"));
                    Npc("souvenir", "Mrs Wong", "Souvenir seller", "QuietMorning", f.P(0, 0.02f, 0.9f), f.Y(180));
                }
            }
            // Lifeguard tower and the swimming flags.
            var tower = Site(3, -14, 3, 3, 0.1f, false);
            foreach (var (x, z) in new[] { (-1.2f, -1.2f), (1.2f, -1.2f), (-1.2f, 1.2f), (1.2f, 1.2f) }) Post(tower, x, z, 2.4f, Hex("F4F4F2"));
            Paint(Box("Lifeguard platform", tower.P(0, 2.4f, 0), new Vector3(3, 0.15f, 3), Hex("E94B3C"), true, null, tower.Yaw), Hex("E94B3C"), 0.3f);
            Paint(Box("Lifeguard hut", tower.P(0, 3.4f, 0.3f), new Vector3(2.4f, 1.8f, 2.2f), Hex("E94B3C"), true, null, tower.Yaw), Hex("E94B3C"), 0.3f);
            Board("LIFEGUARD", tower.P(0, 3.6f, -0.85f), tower.Y(0), new Vector2(2, 0.45f), Hex("F2C14E"), Hex("E94B3C"));
            Npc("lifeguard", "Ben", "Lifeguard", "SoccerCaptain", tower.P(0, 0.02f, -2.4f), tower.Y(180));
            foreach (var degrees in new[] { -5f, 8f })
            {
                var foot = OnGround(degrees, -3, 0);
                Shape("Flag pole", PrimitiveType.Cylinder, foot + Vector3.up * 1.6f, new Vector3(0.06f, 1.6f, 0.06f), Hex("F4F4F2"), false);
                Box("Flag (red)", foot + new Vector3(0.35f, 2.95f, 0), new Vector3(0.7f, 0.25f, 0.02f), Hex("D63A2F"), false, null, -degrees);
                Box("Flag (yellow)", foot + new Vector3(0.35f, 2.7f, 0), new Vector3(0.7f, 0.25f, 0.02f), Hex("F2C14E"), false, null, -degrees);
                Spot("swimflag", "What do the flags mean?", foot + Vector3.up * 1.2f, 2.0f);
            }
        }

        // ------------------------------------------------------------------ sunset point
        private static void Viewpoint(BeachFlow flow)
        {
            var top = OnGround(-42, -4, 0);
            var f = new Frame { O = top, Yaw = 42 };
            Paint(Box("Lookout platform", f.P(0, 0.05f, 0), new Vector3(7, 0.2f, 5), Timber, true, null, f.Yaw), Timber, 0.3f);
            Box("Lookout rail", f.P(0, 0.6f, -2.5f), new Vector3(7, 0.08f, 0.08f), DarkTimber, true, null, f.Yaw);
            for (var x = -3.4f; x <= 3.4f; x += 1.7f) Box("Rail post", f.P(x, 0.4f, -2.5f), new Vector3(0.08f, 0.8f, 0.08f), DarkTimber, true, null, f.Yaw);
            Box("Bench", f.P(0, 0.45f, 1.2f), new Vector3(2.4f, 0.1f, 0.5f), Timber, true, null, f.Yaw);
            Board("SUNSET POINT", f.P(-3.2f, 1.6f, 2.2f), f.Y(180), new Vector2(2.4f, 0.45f), Hex("E8735A"), Cream);
            Spot("sunset", "Sit and watch the sunset", f.P(0, 1.0f, 0.8f), 2.4f);
            flow.ViewpointZone = new Bounds(top + Vector3.up, new Vector3(18, 8, 18));
            Npc("tourist", "Sofia", "Tourist (IELTS conversation)", "Contemplation", f.P(2.4f, 0.12f, 0.2f), f.Y(180));
            foreach (var (d, s, h) in new[] { (-46f, 6f, 3.5f), (-38f, 4f, 2.6f), (-44f, 14f, 4.2f), (-35f, 10f, 2f), (-50f, 2f, 3f) })
                Model(Art + "CoastRock.glb", OnGround(d, s, -0.4f), d * 7, h, "Coastal rock");
            keepClear.Add((new Vector2(top.x, top.z), 9));
        }

        // ------------------------------------------------------------------ station
        private static void Station(BeachFlow flow)
        {
            var f = Site(-12, -128, 16, 6, 0.4f);
            Paint(Box("Platform", f.P(0, 0.4f, 0), new Vector3(16, 0.8f, 6), Hex("C9C5BC"), true, null, f.Yaw), Hex("C9C5BC"), 0.3f);
            Paint(Box("Track bed", f.P(0, 0.1f, 5.5f), new Vector3(30, 0.2f, 4), Hex("6E6A64"), true, null, f.Yaw), Hex("6E6A64"), 0.2f);
            foreach (var x in new[] { -6f, 0, 6f }) Post(f, x, 1.8f, 3.6f, Hex("F4F4F2"));
            Paint(Box("Shelter roof", f.P(0, 4.5f, 0.6f), new Vector3(14, 0.2f, 4), Hex("1F5E5B"), true, null, f.Yaw), Hex("1F5E5B"), 0.4f);
            Board("PALM BAY  ·  COASTAL LINE", f.P(0, 3.6f, -1.3f), f.Y(0), new Vector2(6, 0.6f), Hex("1F5E5B"), Cream);
            Model("Assets/Art/city/CityTram.glb", f.P(0, 0.2f, 6), f.Y(90), 3.4f, "Coastal train");
            flow.Spawn = Group("Spawn · from Metro City", f.P(2, 0.82f, -1.5f), f.Y(180));
            var portal = new GameObject("Portal → MetroCity").AddComponent<PortalDoor>();
            portal.transform.position = f.P(-3, 1.8f, 1.6f);
            portal.TargetScene = "MetroCity";
            portal.Exit = true;
            portal.Prompt = "Take the Coastal Line back to Metro City";
            portal.Range = 2.4f;
            Board("TRAINS TO METRO CITY", f.P(-3, 2.6f, 2.5f), f.Y(0), new Vector2(3.2f, 0.45f), Ink, Hex("FFD58A"));
            for (var i = -1; i <= 1; i += 2) StreetLamp(f.P(i * 7, 0.8f, -2.6f), f.Y(180));
            // Steps down from the platform.
            for (var k = 0; k < 4; k++) Box("Platform step", f.P(0, 0.1f + k * 0.2f, -3.0f - (3 - k) * 0.35f), new Vector3(4, 0.2f, 0.35f), Hex("C9C5BC"), true, null, f.Yaw);
        }

        // ------------------------------------------------------------------ reef and sea life
        private static void Reef()
        {
            foreach (var (d, s, h) in new[] { (-30f, 30f, 1.6f), (-26f, 36f, 1.2f), (-29f, 44f, 1.9f), (-24f, 42f, 1.4f), (-32f, 38f, 1.1f), (-27f, 50f, 1.5f), (-22f, 34f, 1.0f), (-33f, 48f, 1.7f), (-20f, 60f, 2.2f), (-30f, 64f, 2.0f) })
            {
                var bed = OnGround(d, s, -0.1f);
                Model(Art + "Coral.glb", bed, d * 31 % 360, h, "Coral");
            }
            foreach (var (d, s) in new[] { (-34f, 26f), (-21f, 30f), (-28f, 56f), (-18f, 52f) }) Model(Art + "CoastRock.glb", OnGround(d, s, -0.3f), d * 13, 2.4f, "Reef rock");
            void School(string species, string fact, string file, float d, float s, int count, float radius, float depth)
            {
                var centre = Coast(d, s);
                var bed = Ground(centre);
                centre.y = Mathf.Max(bed + 0.8f, depth);
                var school = Group("Fish school · " + species, centre).gameObject.AddComponent<FishSchool>();
                school.Species = species;
                school.Fact = fact;
                school.Radius = radius;
                school.Depth = depth;
                var fish = new List<Transform>();
                for (var i = 0; i < count; i++)
                {
                    var item = Model(Art + file, centre + new Vector3(i * 0.3f, 0, 0), 0, 0.22f, species);
                    item.SetParent(school.transform, true);
                    fish.Add(item);
                }
                school.Fish = fish.ToArray();
            }
            School("Blue tang", "Blue tangs graze on algae and keep the reef clean.", "FishBlue.glb", -27, 38, 7, 2.5f, -1.6f);
            School("Clownfish", "Clownfish live among sea anemones, protected from their sting.", "FishClown.glb", -30, 44, 4, 1.2f, -1.4f);
            School("Yellow butterflyfish", "Butterflyfish usually swim in pairs.", "FishYellow.glb", -24, 42, 6, 2.0f, -1.8f);
            School("Sergeant major", "Sergeant majors are named after the stripes on their sides.", "FishYellow.glb", -22, 60, 9, 3.2f, -4f);
            School("Parrotfish", "Parrotfish crunch coral and turn it into sand.", "FishBlue.glb", -30, 64, 3, 2.6f, -5f);
        }

        // ------------------------------------------------------------------ vegetation
        private static Material WindMaterial(Material original, float flex)
        {
            var key = original.name + "-" + flex;
            if (windMaterials.TryGetValue(key, out var existing)) return existing;
            var shader = Shader.Find("ContextSpeak/Foliage Wind");
            if (shader == null) return original;
            var colour = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("baseColorFactor") ? original.GetColor("baseColorFactor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Palm;
            var material = new Material(shader) { name = "Wind " + original.name, enableInstancing = true };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Flex", flex);
            Directory.CreateDirectory(Folder + "/Materials");
            Replace(material, Folder + "/Materials/Wind-" + string.Concat(original.name.Where(char.IsLetterOrDigit)) + "-" + Mathf.RoundToInt(flex * 100) + ".mat");
            windMaterials[key] = material;
            return material;
        }

        private static Transform Vegetate(string file, Vector3 position, float yaw, float height, bool collide = false)
        {
            var item = Model(Art + file, position, yaw, height, file.Replace(".glb", ""));
            var renderers = item.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var name = materials[i].name;
                    if (name.StartsWith("Leaf")) materials[i] = WindMaterial(materials[i], 1);
                    else if (name.StartsWith("Flower")) materials[i] = WindMaterial(materials[i], 0.8f);
                    else if (name.StartsWith("Trunk")) materials[i] = WindMaterial(materials[i], 0.15f);
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = height > 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            var lod = item.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(height > 5 ? 0.012f : 0.03f, renderers) });
            if (collide)
            {
                var trunk = item.gameObject.AddComponent<CapsuleCollider>();
                trunk.radius = 0.25f;
                trunk.height = height;
                trunk.center = new Vector3(0, height / 2, 0);
            }
            return item;
        }

        private static bool Clear(Vector3 point) => keepClear.All(zone => Vector2.Distance(zone.centre, new Vector2(point.x, point.z)) > zone.radius);

        private static void Vegetation()
        {
            var random = new System.Random(20261113);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var palms = new[] { "CoconutPalm1.glb", "CoconutPalm2.glb", "CoconutPalm3.glb", "CoconutPalm4.glb" };
            var placed = 0;
            for (var attempt = 0; attempt < 900 && placed < 140; attempt++)
            {
                var degrees = Range(-58, 50);
                var s = attempt % 3 == 0 ? Range(-150, -70) : Range(-72, -40);
                var point = OnGround(degrees, s, -0.05f);
                if (!Clear(point) || point.y < 0.6f) continue;
                Vegetate(palms[random.Next(palms.Length)], point, Range(0, 360), Range(7, 12), true);
                placed++;
            }
            foreach (var (file, count, height, inner, outer) in new[] { ("BananaPlant.glb", 40, 2.6f, -160f, -66f), ("HibiscusBush.glb", 60, 1.4f, -140f, -55f), ("BirdOfParadise.glb", 40, 1.2f, -140f, -55f), ("TropicalFern.glb", 70, 0.9f, -170f, -60f), ("SeaAlmondTree.glb", 12, 9f, -170f, -80f) })
                for (int attempt = 0, done = 0; attempt < count * 6 && done < count; attempt++)
                {
                    var point = OnGround(Range(-60, 50), Range(inner, outer), -0.02f);
                    if (!Clear(point)) continue;
                    Vegetate(file, point, Range(0, 360), height * Range(0.8f, 1.25f), height > 5);
                    done++;
                }
            for (var i = 0; i < 150; i++)
            {
                var point = OnGround(Range(-55, 45), Range(-60, -42), -0.02f);
                if (Clear(point)) Vegetate("BeachGrass.glb", point, Range(0, 360), Range(0.4f, 0.8f));
            }
            // Shells, driftwood and rocks on the sand.
            for (var i = 0; i < 40; i++) { var p = OnGround(Range(-30, 30), Range(-12, -2), 0); if (Clear(p)) Model(Art + "BeachShell.glb", p, Range(0, 360), Range(0.06f, 0.12f), "Shell"); }
            for (var i = 0; i < 10; i++) { var p = OnGround(Range(-36, 36), Range(-20, -6), 0); if (Clear(p)) Model(Art + "Driftwood.glb", p, Range(0, 360), Range(0.25f, 0.45f), "Driftwood"); }
            for (var i = 0; i < 14; i++) { var p = OnGround(Range(36, 50), Range(-12, 10), -0.3f); Model(Art + "CoastRock.glb", p, Range(0, 360), Range(1, 3.5f), "Coastal rock"); }
        }

        // ------------------------------------------------------------------ beachfront dressing, litter, lost bag
        private static void Beachfront(BeachFlow flow)
        {
            for (var degrees = -12f; degrees <= 6; degrees += 4.5f)
                for (var row = 0; row < 2; row++)
                {
                    var s = -26 + row * 7;
                    var umbrella = OnGround(degrees, s, 0);
                    if (!Clear(umbrella)) continue;
                    Model(Art + "BeachUmbrella.glb", umbrella, -degrees, 2.6f, "Beach umbrella");
                    Model(Art + "SunLounger.glb", OnGround(degrees - 0.6f, s + 0.6f, 0), 180 - degrees, 0.75f, "Sun lounger");
                    Model(Art + "SunLounger.glb", OnGround(degrees + 0.6f, s + 0.6f, 0), 180 - degrees, 0.75f, "Sun lounger");
                }
            var buoy = Shape("Swim buoy", PrimitiveType.Sphere, Coast(2, 45) + Vector3.up * 0.2f, Vector3.one * 1.3f, Yellow, false);
            buoy.isStatic = false;
            buoy.AddComponent<Floater>().Draft = 0.4f;
            flow.SwimBuoy = buoy.transform;
            var random = new System.Random(7);
            for (var i = 0; i < 10; i++)
            {
                var p = OnGround(-20 + (float)random.NextDouble() * 40, -10 - (float)random.NextDouble() * 30, 0.06f);
                var litter = Box(i % 2 == 0 ? "Litter · bottle" : "Litter · can", p, i % 2 == 0 ? new Vector3(0.08f, 0.08f, 0.26f) : new Vector3(0.07f, 0.12f, 0.07f), i % 2 == 0 ? new Color(0.6f, 0.85f, 0.75f) : Hex("C0392B"), false, null, i * 37);
                litter.isStatic = false;
                var piece = litter.AddComponent<LitterPiece>();
                piece.Range = 1.6f;
            }
            var bag = Group("Lost bag", OnGround(-34, -70, 0.15f));
            Box("Blue beach bag", bag.position, new Vector3(0.5f, 0.3f, 0.35f), Hex("2C5FB8"), false, bag).isStatic = false;
            Box("Striped towel", bag.position + new Vector3(0.1f, 0.17f, 0), new Vector3(0.5f, 0.05f, 0.3f), Hex("F2C14E"), false, bag).isStatic = false;
            var spot = Spot("lostbag", "Pick up the blue bag", bag.position + Vector3.up * 0.5f, 1.8f);
            spot.transform.SetParent(bag, true);
            flow.LostBag = bag.gameObject;
        }

        // ------------------------------------------------------------------ people
        private static void People()
        {
            Npc("tourist-lost", "Hannah", "Tourist", "OpenArms", OnGround(-16, -40), 160);
            Npc("tourist-directions", "Mr Okafor", "Tourist", "ProfessorReed", OnGround(4, -66), 180);
            Npc("volunteer", "Lina", "Beach cleanup volunteer", "Schoolgirl", OnGround(14, -20), 190);
            Npc("resident-1", "Aunty Mei", "Local resident", "Rose", OnGround(-6, -64), 90, OnGround(-6, -64), OnGround(14, -64), OnGround(26, -44));
            Npc("resident-2", "Pak Hasan", "Local resident", "Executive", OnGround(10, -64), -90, OnGround(10, -64), OnGround(-12, -62), OnGround(-30, -40));
            Npc("beachgoer-1", "Chloe", "Holidaymaker", "BlueLace", OnGround(-4, -18), 180, OnGround(-4, -18), OnGround(4, -6), OnGround(-8, -8));
            Npc("beachgoer-2", "Raj", "Holidaymaker", "Plaid", OnGround(10, -30), 180, OnGround(10, -30), OnGround(-14, -24));
        }

        private static void Limit(string name, Vector3 centre, Vector3 size, float yaw)
        {
            var wall = new GameObject(name);
            wall.transform.SetPositionAndRotation(centre, Quaternion.Euler(0, yaw, 0));
            wall.AddComponent<BoxCollider>().size = size;
            wall.isStatic = true;
        }

        private static void Boundaries()
        {
            // Invisible limits: far out at sea (the craft have their own boundary) and the inland hills.
            for (var i = 0; i < 36; i++)
            {
                var a = i * 10f;
                Limit("Sea limit " + i, Coast(a, 155) + Vector3.up * 2, new Vector3(2 * (R + 155) * Mathf.Sin(5 * Mathf.Deg2Rad) + 2, 40, 1), -a);
                var inland = Coast(a, -205);
                if (inland.x > TerrainOrigin.x + 5 && inland.x < TerrainOrigin.x + TerrainSize - 5 && inland.z > TerrainOrigin.z + 5 && inland.z < TerrainOrigin.z + TerrainSize - 5)
                {
                    Limit("Inland limit " + i, OnGround(inland, 2), new Vector3(2 * (R - 205) * Mathf.Sin(5 * Mathf.Deg2Rad) + 2, 6, 1), -a);
                }
            }
        }
    }
}
