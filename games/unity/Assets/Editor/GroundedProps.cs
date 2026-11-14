using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    /// Authoring-only repair: move whole objects, never individual limbs or furniture legs.
    public static class GroundedProps
    {
        private static readonly string[] Standing = { "tree", "pine", "oak", "bush", "plant", "fern", "grass", "flower", "rock", "shrub", "hedge", "table", "chair", "sofa", "bench", "stool", "piano", "cabinet", "fridge", "lamp" };
        private static readonly string[] Suspended = { "hanging", "pendant", "ceiling", "wall", "roof", "window", "glass", "sign", "board", "rail", "beam", "hood", "ivy", "sky", "cloud", "bird", "curtain", "runner" };
        private static readonly string[] Surfaces = { "ground", "floor", "terrain", "road", "path", "deck", "terrace", "platform", "stairs", "step" };
        private static readonly string[] RaisedSupports = { "table", "counter", "shelf", "cabinet", "bench", "planter", "pot" };
        private static readonly string[] Watercraft = { "boat", "yacht", "ship" };

        public static int Repair(Scene scene, List<string> changes = null)
        {
            Physics.SyncTransforms();
            var candidates = new HashSet<GameObject>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene || !VisibleMesh(renderer)) continue;
                var candidate = Candidate(renderer);
                if (candidate != null) candidates.Add(candidate);
            }
            var moved = 0;
            foreach (var item in candidates.OrderBy(item => item.transform.position.y))
            {
                var name = item.name.ToLowerInvariant();
                if (Suspended.Any(name.Contains) || Surfaces.Any(name.Contains) || name.Contains("canopy") || name.Contains("foliage")) continue;
                if (HasSuspendedParent(item.transform)) continue;
                var renderers = item.GetComponentsInChildren<Renderer>().Where(VisibleMesh).ToArray();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var character = IsCharacterRoot(item.transform) || item.GetComponentInChildren<Animator>() != null
                    || item.GetComponentInChildren<SkinnedMeshRenderer>() != null;
                // Unclassified imported assets can be cups, boats or set dressing. Leave them authored.
                if (!character && !Standing.Any(name.Contains)) continue;
                var imported = PrefabUtility.IsPartOfPrefabInstance(item) || item.GetComponentsInChildren<MeshFilter>()
                    .Any(mesh => mesh.sharedMesh != null && AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/", StringComparison.Ordinal));
                var assembly = renderers.Length > 1 && item.GetComponent<Renderer>() == null;
                // Procedural tabletops, seats and cushions are separate primitive pieces, not floor-standing models.
                if (!character && !imported && !assembly && item.GetComponent<LODGroup>() == null) continue;
                if (character) bounds = CharacterPoseChecks.Measure(item);
                if (bounds.size.x > 25 || bounds.size.z > 25 || bounds.size.y > 40) continue;
                var origin = new Vector3(bounds.center.x, bounds.max.y + 100, bounds.center.z);
                var hits = Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 250, ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => !hit.collider.transform.IsChildOf(item.transform) && hit.normal.y > .65f
                        && hit.point.y <= bounds.min.y + .5f && IsSupport(hit.collider, name))
                    .OrderByDescending(hit => hit.point.y).ToArray();
                if (hits.Length == 0) continue;
                var delta = hits[0].point.y - bounds.min.y;
                if (Mathf.Abs(delta) <= .025f) continue;
                Undo.RecordObject(item.transform, "Ground world object");
                item.transform.position += Vector3.up * delta;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                changes?.Add(scene.name + "/" + item.name + ": " + delta.ToString("F3") + " m");
                moved++;
                Physics.SyncTransforms();
            }
            return moved;
        }

        private static bool VisibleMesh(Renderer renderer) => renderer.enabled && renderer.gameObject.activeInHierarchy
            && !(renderer is ParticleSystemRenderer) && !(renderer is LineRenderer) && renderer.GetComponent<TextMesh>() == null;

        private static bool IsSupport(Collider collider, string itemName)
        {
            var name = collider.name.ToLowerInvariant();
            var plant = new[] { "plant", "flower", "grass", "shrub", "fern", "bush" }.Any(itemName.Contains);
            return collider is TerrainCollider || Surfaces.Any(name.Contains) || plant && RaisedSupports.Any(name.Contains);
        }

        private static bool HasSuspendedParent(Transform item)
        {
            for (var parent = item; parent != null; parent = parent.parent)
            {
                var name = parent.name.ToLowerInvariant();
                if (Suspended.Any(name.Contains) || Watercraft.Any(name.Contains)) return true;
            }
            return false;
        }

        private static GameObject Candidate(Renderer renderer)
        {
            GameObject named = null;
            for (var parent = renderer.transform; parent != null; parent = parent.parent)
            {
                if (IsCharacterRoot(parent))
                    return parent.gameObject;
                if (Standing.Any(parent.name.ToLowerInvariant().Contains)) named = parent.gameObject;
            }
            // Imported models may have no descriptive child names. The prefab or animator owns their mesh.
            var animator = renderer.GetComponentInParent<Animator>();
            if (animator != null) return animator.gameObject;
            var lod = renderer.GetComponentInParent<LODGroup>();
            if (lod != null) return lod.gameObject;
            return PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? named;
        }

        private static bool IsCharacterRoot(Transform item) => item.GetComponent<CafePlayer>() != null
            || item.GetComponent<NpcActor>() != null || item.GetComponent<LearningNpc>() != null
            || item.GetComponent<ContextSpeak.Life.NPCController>() != null || item.GetComponent<SliceWalker>() != null
            || item.GetComponent<CharacterController>() != null || item.GetComponent<NavMeshAgent>() != null;

        [UnityEditor.MenuItem("ContextSpeak/Environment/Repair saved café grounding")]
        public static void RepairSavedWorlds()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var backup = Path.GetFullPath("../../.local/grounding/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var changes = new List<string>();
            var scenes = WorldSceneCatalog.All.Where(entry => entry.Name == "ForestSlice" || entry.Name == "ForestCafe" || entry.Name == "LifeTown").ToArray();
            try
            {
                // Back up every scene before the first edit; do not run any scene generators.
                foreach (var entry in scenes)
                {
                    var path = Path.Combine(backup, entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(entry.Path, path);
                }
                foreach (var entry in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(entry.Path, OpenSceneMode.Single);
                    if (Repair(scene, changes) == 0) continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                File.WriteAllLines(Path.Combine(backup, "changes.txt"), changes);
                File.WriteAllText("../../.local/grounding/latest.txt", backup);
                Debug.Log("Grounded " + changes.Count + " world objects. Scene backups: " + backup);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
