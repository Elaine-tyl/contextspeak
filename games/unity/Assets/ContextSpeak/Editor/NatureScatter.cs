using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Scatters the vegetation prefabs over the green areas of a scene (lawns with the Grass ground
    /// material, terrain where the grass layer dominates): mixed grass species in noise-driven
    /// fresh/meadow/dry patches, clustered wildflowers, a few rocks and — in large parks — trees.
    /// Placement raycasts onto the real surface, keeps clear of walls, furniture and roofs, and
    /// shortens grass next to paths (trampled edges). Birds, butterflies and insects are added too.
    public static class NatureScatter
    {
        public const int MaxGrass = 2600, MaxFlowers = 520;

        private struct Patch
        {
            public Collider Ground;
            public Terrain Terrain;
            public int GrassLayer;
            public Bounds Bounds;
            public float Area;
        }

        public static int Scatter(Transform parent, Dictionary<string, GameObject> plants, Dictionary<string, Material> ground, string scene)
        {
            if (plants == null || plants.Count == 0) return 0;
            Physics.SyncTransforms();
            var patches = FindPatches(ground);
            if (patches.Count == 0) return 0;
            var root = new GameObject("Nature (generated)").transform;
            root.SetParent(parent, false);
            var controller = root.gameObject.AddComponent<VegetationController>();
            var items = new List<VegetationController.Item>();
            var random = new System.Random(scene.GetHashCode());
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var tropical = scene == "TropicalBeach";
            var paths = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => ground != null && r.sharedMaterial != null && (r.sharedMaterial == Get(ground, "Gravel") || r.sharedMaterial == Get(ground, "Concrete") || r.sharedMaterial == Get(ground, "Asphalt")))
                .Select(r => r.bounds).ToArray();
            var totalArea = patches.Sum(p => p.Area);

            GameObject Plant(string name) => plants.TryGetValue(name, out var prefab) ? prefab : null;
            var grassFresh = new[] { "GrassFresh", "GrassShort" }.Select(Plant).Where(p => p != null).ToArray();
            var grassMeadow = new[] { "GrassMeadow", "MeadowGrass" }.Select(Plant).Where(p => p != null).ToArray();
            var grassDry = new[] { "GrassDry" }.Select(Plant).Where(p => p != null).ToArray();
            var wild = (tropical ? new[] { "TropicalFlowers", "CrimsonBlossom", "Jasmine", "WildButtercups" }
                : new[] { "WildDaisies", "WildButtercups", "WildViolets", "WildDaisies", "RockFlowerA", "RockFlowerB", "RockFlowerC", "RockFlowerD", "BlossomA", "BlossomB", "BlossomC", "BlossomD", "BlossomE", "Chrysanthemum", "Irises", "PeachRose" })
                .Select(Plant).Where(p => p != null).ToArray();
            var rocks = new[] { "TundraRock", "MountainRocks" }.Select(Plant).Where(p => p != null).ToArray();
            var trees = tropical ? new GameObject[0] : new[] { "OakTree", "OakTree", "PineTree", "SpruceTree", "BroadleafTree" }.Select(Plant).Where(p => p != null).ToArray();

            bool TrySpot(Patch patch, Vector2 xz, float clearance, bool needSky, out Vector3 point, out Vector3 normal)
            {
                point = normal = Vector3.zero;
                var top = new Vector3(xz.x, patch.Bounds.max.y + 3, xz.y);
                if (patch.Terrain != null)
                {
                    if (!GrassAt(patch.Terrain, patch.GrassLayer, top)) return false;
                    point = new Vector3(xz.x, patch.Terrain.SampleHeight(top) + patch.Terrain.transform.position.y, xz.y);
                    normal = patch.Terrain.terrainData.GetInterpolatedNormal((xz.x - patch.Terrain.transform.position.x) / patch.Terrain.terrainData.size.x, (xz.y - patch.Terrain.transform.position.z) / patch.Terrain.terrainData.size.z);
                    if (normal.y < 0.8f || point.y < 0.4f) return false;
                }
                else
                {
                    if (!Physics.Raycast(top, Vector3.down, out var hit, patch.Bounds.size.y + 6, ~0, QueryTriggerInteraction.Ignore) || hit.collider != patch.Ground) return false;
                    point = hit.point; normal = hit.normal;
                    if (normal.y < 0.8f) return false;
                }
                foreach (var blocker in Physics.OverlapSphere(point + Vector3.up * (clearance + 0.05f), clearance, ~0, QueryTriggerInteraction.Ignore))
                    if (blocker != patch.Ground && !(blocker is TerrainCollider)) return false;
                if (needSky && Physics.Raycast(point + Vector3.up * 0.5f, Vector3.up, 14, ~0, QueryTriggerInteraction.Ignore)) return false;
                return true;
            }

            float PathDistance(Vector3 point) => paths.Length == 0 ? 99 : paths.Min(b => Vector3.Distance(b.ClosestPoint(point), point));

            void Place(GameObject prefab, Vector3 point, Vector3 normal, float scale, VegetationController.Layer layer, float heightScale = 1)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                item.transform.position = point - Vector3.up * 0.02f;
                item.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, 0.6f)) * Quaternion.Euler(0, R(0, 360), 0);
                item.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(scale, scale * heightScale, scale));   // prefab carries the real-world size
                items.Add(new VegetationController.Item { Plant = item, Type = layer, Keep = R(0, 1) });
            }

            var grassCount = 0; var flowerCount = 0; var other = 0;
            var clusters = new List<Vector3>();
            foreach (var patch in patches)
            {
                var share = patch.Area / totalArea;
                var grassBudget = Mathf.Min(Mathf.RoundToInt(MaxGrass * share), Mathf.RoundToInt(patch.Area * 1.6f));
                var tries = grassBudget * (patch.Terrain != null ? 10 : 3);
                for (var attempt = 0; attempt < tries && grassBudget > 0; attempt++)
                {
                    var xz = new Vector2(R(patch.Bounds.min.x, patch.Bounds.max.x), R(patch.Bounds.min.z, patch.Bounds.max.z));
                    if (!TrySpot(patch, xz, 0.18f, false, out var point, out var normal)) continue;
                    // Fresh / meadow / dry patches from low-frequency noise; meadow grass in the less-used middle.
                    var n = Mathf.PerlinNoise(xz.x * 0.045f + 3.1f, xz.y * 0.045f + 7.7f);
                    var dry = Mathf.PerlinNoise(xz.x * 0.03f + 11, xz.y * 0.03f + 5) > 0.68f;
                    var pool = dry && grassDry.Length > 0 ? grassDry : n > 0.55f && grassMeadow.Length > 0 ? grassMeadow : grassFresh.Length > 0 ? grassFresh : grassMeadow;
                    if (pool.Length == 0) break;
                    var near = PathDistance(point);
                    if (near < 0.4f) continue;
                    var trampled = near < 1.6f ? Mathf.Lerp(0.35f, 1, (near - 0.4f) / 1.2f) : 1;      // shorter, sparser at path edges
                    if (trampled < 1 && R(0, 1) > trampled) continue;
                    Place(pool[random.Next(pool.Length)], point, normal, R(0.8f, 1.3f), VegetationController.Layer.Grass, trampled);
                    grassCount++;
                    if (--grassBudget <= 0) break;
                }
                // Wildflowers in natural clusters.
                var flowerBudget = Mathf.Min(Mathf.RoundToInt(MaxFlowers * share), Mathf.RoundToInt(patch.Area * 0.25f));
                for (var c = 0; c < flowerBudget / 8 + 1 && wild.Length > 0 && flowerBudget > 0; c++)
                {
                    var centre = new Vector2(R(patch.Bounds.min.x, patch.Bounds.max.x), R(patch.Bounds.min.z, patch.Bounds.max.z));
                    var species = wild[random.Next(wild.Length)];
                    var placed = 0;
                    for (var k = 0; k < (patch.Terrain != null ? 30 : 14) && placed < 10; k++)
                    {
                        var xz = centre + new Vector2(R(-1.6f, 1.6f), R(-1.6f, 1.6f)) * R(0.3f, 1);
                        if (!TrySpot(patch, xz, 0.15f, false, out var point, out var normal) || PathDistance(point) < 0.8f) continue;
                        Place(R(0, 1) < 0.15f ? wild[random.Next(wild.Length)] : species, point, normal, R(0.8f, 1.2f), VegetationController.Layer.Flower);
                        placed++; flowerCount++;
                        if (placed == 1) clusters.Add(point);
                    }
                    flowerBudget -= placed;
                }
                // A few rocks.
                for (var k = 0; k < Mathf.Min(12, patch.Area / 250) && rocks.Length > 0; k++)
                {
                    var xz = new Vector2(R(patch.Bounds.min.x, patch.Bounds.max.x), R(patch.Bounds.min.z, patch.Bounds.max.z));
                    if (!TrySpot(patch, xz, 0.6f, false, out var point, out var normal) || PathDistance(point) < 1.5f) continue;
                    Place(rocks[random.Next(rocks.Length)], point - Vector3.up * 0.15f, normal, R(0.25f, 0.6f), VegetationController.Layer.Bush);
                    other++;
                }
                // Trees in large parks only, with room for the crown and open sky above.
                if (trees.Length > 0 && patch.Area > 300 && patch.Terrain == null)
                    for (var k = 0; k < Mathf.Min(10, patch.Area / 420); k++)
                    {
                        var xz = new Vector2(R(patch.Bounds.min.x + 3, patch.Bounds.max.x - 3), R(patch.Bounds.min.z + 3, patch.Bounds.max.z - 3));
                        if (!TrySpot(patch, xz, 2.5f, true, out var point, out var normal) || PathDistance(point) < 3) continue;
                        Place(trees[random.Next(trees.Length)], point, Vector3.up, R(0.75f, 1.15f), VegetationController.Layer.Tree);
                        other++;
                    }
            }
            controller.Items = items.ToArray();

            // Living world: birds over the biggest green area, butterflies and insects at flowers.
            var biggest = patches.OrderByDescending(p => p.Area).First();
            var flock = new GameObject("Birds").AddComponent<BirdFlock>();
            flock.transform.SetParent(root, false);
            flock.transform.position = new Vector3(biggest.Bounds.center.x, biggest.Bounds.max.y, biggest.Bounds.center.z);
            flock.Radius = Mathf.Clamp(Mathf.Sqrt(biggest.Area) * 0.6f, 12, 40);
            var insectMaterialPath = EnvironmentSetup.Root + "/Insects.mat";
            var insectMaterial = AssetDatabase.LoadAssetAtPath<Material>(insectMaterialPath);
            if (insectMaterial == null)
            {
                insectMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                insectMaterial.SetColor("_BaseColor", new Color(0.1f, 0.09f, 0.07f, 1));
                AssetDatabase.CreateAsset(insectMaterial, insectMaterialPath);
            }
            foreach (var spot in clusters.OrderBy(_ => random.Next()).Take(4))
            {
                var swarm = new GameObject("Butterflies").AddComponent<ButterflySwarm>();
                swarm.transform.SetParent(root, false);
                swarm.transform.position = spot;
            }
            foreach (var spot in clusters.OrderBy(_ => random.Next()).Take(3)) InsectCloud.Create(root, spot + Vector3.up * 0.9f, insectMaterial);
            return grassCount + flowerCount + other;
        }

        private static Material Get(Dictionary<string, Material> ground, string name) => ground != null && ground.TryGetValue(name, out var m) ? m : null;

        private static bool GrassAt(Terrain terrain, int layer, Vector3 world)
        {
            if (layer < 0) return true;
            var data = terrain.terrainData;
            var local = world - terrain.transform.position;
            var x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            var z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            return data.GetAlphamaps(x, z, 1, 1)[0, 0, layer] > 0.5f;
        }

        private static List<Patch> FindPatches(Dictionary<string, Material> ground)
        {
            var patches = new List<Patch>();
            var grass = Get(ground, "Grass");
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (grass == null || renderer.sharedMaterial != grass) continue;
                var collider = renderer.GetComponent<Collider>();
                if (collider == null) continue;
                var area = renderer.bounds.size.x * renderer.bounds.size.z;
                if (area < 4) continue;
                patches.Add(new Patch { Ground = collider, Bounds = renderer.bounds, Area = area, GrassLayer = -1 });
            }
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                var layers = terrain.terrainData.terrainLayers;
                var index = System.Array.FindIndex(layers, l => l != null && l.name.ToLowerInvariant().Contains("grass"));
                if (index < 0) continue;
                // Area actually covered by grass (sampled).
                var data = terrain.terrainData;
                var maps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
                var covered = 0;
                for (var z = 0; z < data.alphamapHeight; z += 4)
                    for (var x = 0; x < data.alphamapWidth; x += 4)
                        if (maps[z, x, index] > 0.5f) covered++;
                var area = covered * 16f / (data.alphamapWidth * data.alphamapHeight) * data.size.x * data.size.z;
                if (area < 10) continue;
                var bounds = new Bounds(terrain.transform.position + data.size / 2, data.size);
                patches.Add(new Patch { Ground = terrain.GetComponent<TerrainCollider>(), Terrain = terrain, GrassLayer = index, Bounds = bounds, Area = Mathf.Min(area, 6000) });
            }
            return patches;
        }
    }
}
