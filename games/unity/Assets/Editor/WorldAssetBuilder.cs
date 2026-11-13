using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    public static class WorldAssetBuilder
    {
        public const string Source = "Assets/_Game/Art/WorldAssets/";
        public const string Folder = "Assets/Generated/WorldAssets/";
        [Serializable] private sealed class Catalog { public SceneryEntry[] items; }
        public static SceneryEntry[] Read() => JsonUtility.FromJson<Catalog>("{\"items\":" + File.ReadAllText(Source + "catalog.json") + "}").items;

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            foreach (var entry in Read().Where(entry => entry.usage == "world"))
            {
                var path = Source + entry.id + ".glb";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) throw new Exception("Scenery export missing: " + entry.id);
                var root = new GameObject(entry.Label);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + entry.id + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void Addressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("Streamed scenery") ?? settings.CreateGroup("Streamed scenery", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            foreach (var entry in Read().Where(entry => entry.usage == "world"))
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(Folder + entry.id + ".prefab"), group).address = entry.id;
            EditorUtility.SetDirty(settings);
        }

        public static void Place()
        {
            var catalog = Read();
            var root = new GameObject("Downloaded scenery trails");
            var streamer = root.AddComponent<WorldScenery>();
            streamer.CatalogJson = AssetDatabase.LoadAssetAtPath<TextAsset>(Source + "catalog.json");
            var placed = new List<(Vector3 point, float radius)>();
            var residents = UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None);
            var entrances = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None)
                .Where(item => item.Arrival != null).Select(item => item.Arrival.position).ToArray();
            var zones = new Dictionary<string, Vector3>
            {
                ["Architecture"] = new Vector3(-60, 0, -55), ["Home"] = new Vector3(-60, 0, -4),
                ["Wardrobe"] = new Vector3(-16, 0, -60), ["Furniture"] = new Vector3(20, 0, -48),
                ["Vehicles"] = new Vector3(57, 0, -58), ["Waterside"] = new Vector3(58, 0, 50),
                ["Aquarium"] = new Vector3(38, 0, 62), ["Animals"] = new Vector3(23, 0, 70),
                ["Garden"] = new Vector3(-20, 0, 72), ["Market"] = new Vector3(62, 0, -2),
                ["Crafts"] = new Vector3(-60, 0, 77), ["Roads"] = new Vector3(32, 0, -58)
            };
            // Evaluate empty ground against existing geometry, keeping NPC approaches and roads clear.
            Physics.SyncTransforms();
            foreach (var entry in catalog.Where(entry => entry.usage == "world").OrderByDescending(entry => entry.extent))
            {
                var radius = entry.extent / 2 + 0.35f;
                var candidates = new List<Vector3>();
                for (var x = -65f; x <= 65; x += 3)
                    for (var z = -65f; z <= 77; z += 3)
                    {
                        var point = new Vector3(x, 0.05f, z);
                        if (Mathf.Abs(x) < 29 && z > -21 && z < 41) continue;
                        if (Mathf.Abs(Mathf.Abs(x) - 48) < 1.8f || Mathf.Abs(z + 16) < 1.8f || Mathf.Abs(z - 33) < 1.8f) continue;
                        if (Mathf.Abs(x) + radius > 69 || z + radius > 81 || z - radius < -69) continue;
                        if (residents.Any(npc => Vector3.Distance(point, npc.Arrival) < radius + 3)) continue;
                        if (entrances.Any(entrance => Vector3.Distance(point, entrance) < radius + 1.2f)) continue;
                        if (placed.Any(other => Vector3.Distance(point, other.point) < radius + other.radius + 1.4f)) continue;
                        if (Physics.CheckBox(point + Vector3.up, new Vector3(radius, 0.7f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (!NavMesh.SamplePosition(point, out var ground, 0.25f, NavMesh.AllAreas)) continue;
                        var arrival = point + Vector3.back * (radius + 1.2f);
                        if (!NavMesh.SamplePosition(arrival, out _, 0.3f, NavMesh.AllAreas)) continue;
                        candidates.Add(point);
                    }
                if (candidates.Count == 0) throw new Exception("No safe scenery plot remains: " + entry.id);
                var selected = candidates.OrderBy(point => Vector3.SqrMagnitude(point - zones[entry.zone])).First();
                entry.position = new[] { selected.x, selected.y, selected.z };
                placed.Add((selected, radius));
                var anchor = new GameObject(entry.Label);
                anchor.transform.SetParent(root.transform, false);
                anchor.transform.position = selected;
                var interactable = anchor.AddComponent<Interactable>();
                interactable.Id = entry.id;
                interactable.Prompt = "Inspect " + entry.Label;
                interactable.Range = radius + 1.8f;
                var arrivalPoint = new GameObject("Safe approach").transform;
                arrivalPoint.SetParent(anchor.transform, false);
                arrivalPoint.localPosition = Vector3.back * (radius + 1.2f);
                interactable.Arrival = arrivalPoint;
                // Flowers/grass have no collision. Larger objects have a small central solid proxy.
                if (entry.extent > 1.5f && entry.zone != "Roads")
                {
                    var collider = anchor.AddComponent<BoxCollider>();
                    collider.center = Vector3.up * 0.55f;
                    collider.size = new Vector3(entry.extent * 0.65f, 1.1f, entry.extent * 0.65f);
                }
            }
            // Preserve all metadata when updating only the editor-computed placements.
            var path = Source + "catalog.json";
            var json = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path));
            foreach (var entry in catalog.Where(entry => entry.usage == "world"))
                json.First(token => token["id"].ToString() == entry.id)["position"] = new Newtonsoft.Json.Linq.JArray(entry.position);
            File.WriteAllText(path, json.ToString() + "\n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// Recheck approaches after all scenery proxies participate in the final navigation bake.
        public static void ResolveApproaches()
        {
            var start = new Vector3(7.5f, .1f, -.5f);
            foreach (var item in UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None)
                .Where(item => item.Id.StartsWith("World") && item.Arrival != null))
            {
                var original = item.Arrival.position;
                var found = false;
                for (var ring = 0; ring <= 5 && !found; ring++)
                    for (var direction = 0; direction < (ring == 0 ? 1 : 12); direction++)
                    {
                        var angle = direction * Mathf.PI / 6;
                        var point = original + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * ring * .5f;
                        if (!NavMesh.SamplePosition(point, out var hit, .3f, NavMesh.AllAreas)) continue;
                        if (Physics.CheckCapsule(hit.position + Vector3.up * .4f, hit.position + Vector3.up * 1.4f, .3f)) continue;
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        item.Arrival.position = hit.position;
                        // A repaired approach may sit just beyond the original crowded exhibit radius.
                        item.Range = Mathf.Max(item.Range, Vector3.Distance(hit.position, item.transform.position) + .2f);
                        found = true;
                        break;
                    }
                if (!found) throw new Exception("No connected approach after scenery bake: " + item.Id);
            }
        }
    }
}
