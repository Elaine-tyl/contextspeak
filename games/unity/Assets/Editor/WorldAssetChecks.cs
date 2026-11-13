using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    public static class WorldAssetChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("World assets: " + message);
        }
        public static void Verify()
        {
            var catalog = WorldAssetBuilder.Read();
            Check(catalog.Select(entry => entry.sourceHash).ToHashSet().SetEquals(MeshyLibrary.Read().Select(entry => entry.sha256)) && catalog.Select(entry => entry.sourceHash).Distinct().Count() == catalog.Length,
                "All distinct downloaded FBXs must have an explicit use.");
            Check(catalog.Count(entry => entry.usage == "character") == MeshyCharacters.Names.Length, "All inspected characters need real cast assignments.");
            foreach (var entry in catalog.Where(entry => entry.usage == "world"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + entry.id + ".prefab");
                Check(prefab != null, "Missing live prefab: " + entry.id);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh).ToArray();
                Check(meshes.Length > 0 && meshes.Sum(mesh => mesh.triangles.Length / 3) <= entry.triangleBudget && entry.triangleBudget <= 180000, "Geometry budget: " + entry.id);
                var renderers = prefab.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var measured = entry.targetHeight > 0 ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                Check(Mathf.Abs(measured - (entry.targetHeight > 0 ? entry.targetHeight : entry.extent)) < 0.15f,
                    "World scale differs from its explicit plot size: " + entry.id + " " + bounds);
                foreach (var material in renderers.SelectMany(renderer => renderer.sharedMaterials))
                {
                    Check(material != null && material.shader != null && !material.shader.name.Contains("Error"), "Broken material: " + entry.id);
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property);
                        if (texture != null) Check(texture.width <= 512 && texture.height <= 512, "Texture budget: " + entry.id);
                    }
                }
            }
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var scenery = UnityEngine.Object.FindFirstObjectByType<WorldScenery>();
            Check(scenery != null && scenery.Entries.Length == catalog.Length, "Runtime catalog missing.");
            var anchors = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            foreach (var entry in scenery.Entries.Where(entry => entry.usage == "world"))
            {
                var anchor = anchors.SingleOrDefault(item => item.Id == entry.id);
                Check(anchor != null && anchor.Arrival != null, "Scenery is not inspectable: " + entry.id);
                var path = new NavMeshPath();
                Check(NavMesh.CalculatePath(new Vector3(7.5f, 0.1f, -0.5f), anchor.Arrival.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete, "Scenery approach is unreachable: " + entry.id);
            }
            NpcCastChecks.VerifyScene();
            Debug.Log("All " + catalog.Length + " FBX usages verified; " + catalog.Count(entry => entry.usage == "world") + " bounded scenery prefabs have reachable inspection points");
        }
    }
}
