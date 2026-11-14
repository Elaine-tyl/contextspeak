using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Flowers, flower beds and small animals for any ContextSpeak Life scene.
    ///  • Flowers: the project's realistic flower prefabs (EnvironmentSetup vegetation, with wind shaders).
    ///  • Animals: tools/blender/villa_fauna.py → Assets/ContextSpeak/Art/Fauna/*.glb (light meshes with moving
    ///    wings / tail / legs) driven by WildBird, WildButterfly, WildCrab, KoiSchool.
    /// ContextSpeak > World > Add Flowers & Wildlife To Open Scene works in any scene (Undo supported); the
    /// villa builder calls the same helpers with its planned beds and pond.
    public static class WildlifeTools
    {
        public const string Fauna = "Assets/ContextSpeak/Art/Fauna/";
        public static readonly string[] FlowerSpecies = { "BlossomA", "BlossomB", "BlossomC", "BlossomD", "BlossomE", "TropicalFlowers", "PeachRose", "Jasmine", "Irises", "Allium", "CrimsonBlossom", "Chrysanthemum", "RockFlowerA", "RockFlowerB", "WildDaisies" };
        private static Dictionary<string, GameObject> flowers;

        public static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Fauna + name + ".glb");

        public static List<GameObject> FlowerPrefabs()
        {
            if (flowers == null)
            {
                try { flowers = EnvironmentSetup.EnsureVegetationPrefabs(); }
                catch (System.Exception e) { Debug.LogWarning("Flower prefabs unavailable: " + e.Message); flowers = new Dictionary<string, GameObject>(); }
            }
            return FlowerSpecies.Where(n => flowers.ContainsKey(n) && flowers[n] != null).Select(n => flowers[n]).ToList();
        }

        private static GameObject Place(GameObject prefab, Vector3 p, float yaw, float scale, Transform parent, bool undo)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale *= scale;
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Add flowers & wildlife");
            return go;
        }

        /// Fills a world-space rectangle (x/z, at height y) with flowers in drifts of one species (as gardeners
        /// plant them), ~density plants per m². Returns the bloom positions (for butterflies).
        public static List<Vector3> PlantBed(Rect rect, float y, float density, Transform parent, System.Random r, bool undo = false)
        {
            var result = new List<Vector3>();
            var prefabs = FlowerPrefabs();
            if (prefabs.Count == 0) return result;
            var count = Mathf.Max(3, Mathf.RoundToInt(rect.width * rect.height * density));
            var drift = Mathf.Max(1, count / Mathf.Max(1, Mathf.RoundToInt(rect.width * rect.height / 1.2f)));
            GameObject species = null;
            for (var i = 0; i < count; i++)
            {
                if (i % Mathf.Max(3, drift * 3) == 0) species = prefabs[r.Next(prefabs.Count)];
                var p = new Vector3(rect.xMin + (float)r.NextDouble() * rect.width, y, rect.yMin + (float)r.NextDouble() * rect.height);
                var go = Place(species, p, r.Next(360), 0.85f + (float)r.NextDouble() * 0.3f, parent, undo);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var b = AirportKit.Measure(go);
                result.Add(new Vector3(p.x, b.size.y > 0.05f ? b.max.y : p.y + 0.4f, p.z));
            }
            return result;
        }

        public static int Birds(string[] models, int count, Bounds area, Vector3[] perches, bool soar, float size, float speed, Transform parent, System.Random r, bool undo = false)
        {
            var made = 0;
            for (var i = 0; i < count; i++)
            {
                var m = Model(models[i % models.Length]);
                if (m == null) continue;
                var p = new Vector3(Mathf.Lerp(area.min.x, area.max.x, (float)r.NextDouble()), area.center.y + (soar ? 10 : 0), Mathf.Lerp(area.min.z, area.max.z, (float)r.NextDouble()));
                var go = Place(m, p, r.Next(360), 1, parent, undo);
                go.name = models[i % models.Length].Replace("Bird_", "") + " (wild)";
                var b = go.AddComponent<WildBird>();
                b.Area = area; b.Perches = perches ?? new Vector3[0]; b.Soar = soar; b.Size = size; b.FlySpeed = speed; b.FleeDistance = soar ? 6 : 3.5f;
                made++;
            }
            return made;
        }

        public static int Butterflies(int count, Vector3[] blooms, Bounds area, Transform parent, System.Random r, bool undo = false)
        {
            var kinds = new[] { "Butterfly_Monarch", "Butterfly_Blue", "Butterfly_White", "Butterfly_Yellow" };
            var made = 0;
            for (var i = 0; i < count; i++)
            {
                var m = Model(kinds[i % kinds.Length]);
                if (m == null) continue;
                var p = blooms.Length > 0 ? blooms[r.Next(blooms.Length)] + Vector3.up * 0.5f : area.center + Vector3.up;
                var go = Place(m, p, r.Next(360), 1, parent, undo);
                go.name = kinds[i % kinds.Length].Replace("_", " ");
                var b = go.AddComponent<WildButterfly>(); b.Flowers = blooms; b.Area = area;
                made++;
            }
            return made;
        }

        public static int Crabs(int count, Bounds area, IList<Vector3> spots, Transform parent, System.Random r, bool undo = false)
        {
            var m = Model("Crab"); if (m == null || spots.Count == 0) return 0;
            for (var i = 0; i < count; i++)
            {
                var go = Place(m, spots[r.Next(spots.Count)], r.Next(360), 0.9f + (float)r.NextDouble() * 0.5f, parent, undo);
                go.name = "Crab (wild)";
                go.AddComponent<WildCrab>().Area = area;
            }
            return count;
        }

        public static int Fish(int count, Bounds water, Transform parent, System.Random r, bool undo = false)
        {
            var school = new GameObject("Koi school").AddComponent<KoiSchool>();
            school.transform.SetParent(parent, false);
            if (undo) Undo.RegisterCreatedObjectUndo(school.gameObject, "Add fish");
            school.Water = water;
            var fish = new List<Transform>();
            for (var i = 0; i < count; i++)
            {
                var m = Model("Koi_" + (i % 4)); if (m == null) continue;
                var p = new Vector3(Mathf.Lerp(water.min.x + 0.4f, water.max.x - 0.4f, (float)r.NextDouble()), water.center.y, Mathf.Lerp(water.min.z + 0.4f, water.max.z - 0.4f, (float)r.NextDouble()));
                var go = Place(m, p, r.Next(360), 0.8f + (float)r.NextDouble() * 0.6f, school.transform, false);
                go.name = "Koi " + (i + 1);
                fish.Add(go.transform);
            }
            school.Fish = fish.ToArray();
            return fish.Count;
        }

        // ------------------------------------------------------------------ any scene

        [MenuItem("ContextSpeak/World/Add Flowers & Wildlife To Open Scene")]
        public static void AddToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var player = Object.FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            var centre = player != null ? player.transform.position : (SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero);
            var old = GameObject.Find("Flowers & wildlife (generated)");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject("Flowers & wildlife (generated)");
            Undo.RegisterCreatedObjectUndo(root, "Add flowers & wildlife");
            var r = new System.Random(scene.name.GetHashCode());
            Physics.SyncTransforms();
            var report = new List<string>();

            // Sample the ground around the player: grassy spots for flowers/birds, sand near sea level for crabs.
            var grass = new List<Vector3>(); var shore = new List<Vector3>();
            var beach = scene.name.ToLowerInvariant().Contains("beach") || Object.FindFirstObjectByType<OceanWaves>() != null;
            var sea = OceanWaves.SeaLevel;
            for (var i = 0; i < 1600; i++)
            {
                var p = centre + new Vector3((float)(r.NextDouble() - 0.5) * 160, 40, (float)(r.NextDouble() - 0.5) * 160);
                if (!Physics.Raycast(p, Vector3.down, out var hit, 120, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.85f) continue;
                var label = (hit.collider.name + " " + (hit.collider.GetComponent<Renderer>() != null && hit.collider.GetComponent<Renderer>().sharedMaterial != null ? hit.collider.GetComponent<Renderer>().sharedMaterial.name : "")).ToLowerInvariant();
                if (hit.collider is TerrainCollider) label += " terrain";
                if (beach && hit.point.y > sea + 0.05f && hit.point.y < sea + 1.4f) shore.Add(hit.point);
                else if (label.Contains("grass") || label.Contains("lawn") || label.Contains("meadow") || label.Contains("garden") || label.Contains("park") || label.Contains("terrain")) grass.Add(hit.point);
            }
            var blooms = new List<Vector3>();
            foreach (var g in grass.OrderBy(_ => r.Next()).Take(14))
            {
                var size = 1.2f + (float)r.NextDouble() * 1.6f;
                blooms.AddRange(PlantBed(new Rect(g.x - size / 2, g.z - size / 2, size, size), g.y, 3.5f, root.transform, r, true));
            }
            report.Add(blooms.Count + " flowers in " + Mathf.Min(14, grass.Count) + " drifts on grass");
            var area = new Bounds(centre, new Vector3(120, 0, 120));
            report.Add(Butterflies(Mathf.Min(14, 4 + blooms.Count / 6), blooms.ToArray(), area, root.transform, r, true) + " butterflies");
            if (grass.Count > 0)
            {
                var g = new Bounds(grass[0], Vector3.zero); foreach (var p in grass) g.Encapsulate(p);
                report.Add(Birds(new[] { "Bird_Sparrow", "Bird_Bluebird" }, 8, g, new Vector3[0], false, 0.6f, 5, root.transform, r, true) + " songbirds");
            }
            if (beach && shore.Count > 0)
            {
                var s = new Bounds(shore[0], Vector3.zero); foreach (var p in shore) s.Encapsulate(p);
                report.Add(Crabs(18, s, shore, root.transform, r, true) + " crabs on the sand");
                report.Add(Birds(new[] { "Bird_Sandpiper" }, 6, s, new Vector3[0], false, 0.8f, 6, root.transform, r, true) + " sandpipers at the waterline");
                report.Add(Birds(new[] { "Bird_Gull" }, 8, s, new Vector3[0], true, 1.6f, 7, root.transform, r, true) + " gulls over the shore");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Flowers & wildlife added to " + scene.name + ": " + string.Join(", ", report) + (FlowerPrefabs().Count == 0 ? " (no flower prefabs — run ContextSpeak > Environment > Setup Realistic World once)" : "") + ". Save the scene to keep them.");
        }
    }
}
