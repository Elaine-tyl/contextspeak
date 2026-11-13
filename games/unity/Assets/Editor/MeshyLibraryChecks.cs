using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Verify actual native imports, not just files copied into Assets.
    public static class MeshyLibraryChecks
    {
        [Serializable] private class Catalog { public Entry[] models; }
        [Serializable] private class Entry { public string name; public string fbx; public Maps textures; }
        [Serializable] private class Maps { public string baseColor; public string normal; public string surface; public string emission; }

        public static void Verify()
        {
            const string root = "Assets/_Game/Art/MeshyLibrary/";
            if (!File.Exists(root + "manifest.json")) throw new Exception("Meshy library manifest is missing.");
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(root + "manifest.json"));
            if (catalog.models.Length == 0) throw new Exception("No Meshy FBX models found.");
            var animated = 0;
            foreach (var item in catalog.models)
            {
                var path = root + item.fbx;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new Exception("Missing imported FBX geometry: " + item.name);
                var folder = Path.GetDirectoryName(path).Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Model.prefab");
                if (prefab == null) throw new Exception("Missing library prefab: " + item.name);
                var materials = model.GetComponentsInChildren<Renderer>(true).Concat(prefab.GetComponentsInChildren<Renderer>(true))
                    .SelectMany(r => r.sharedMaterials);
                foreach (var material in materials)
                {
                    if (material == null || material.shader.name != "Universal Render Pipeline/Lit")
                        throw new Exception("Unmapped native FBX material: " + item.name);
                    if (item.name.EndsWith("_animated_mixamo_Walking") && material.GetTexture("_BaseMap") == null)
                        throw new Exception("Missing embedded character texture: " + item.name);
                    if (!string.IsNullOrEmpty(item.textures.baseColor) && material.GetTexture("_BaseMap") == null)
                        throw new Exception("Missing base texture: " + item.name);
                    if (!string.IsNullOrEmpty(item.textures.normal) && material.GetTexture("_BumpMap") == null)
                        throw new Exception("Missing normal texture: " + item.name);
                    if (!string.IsNullOrEmpty(item.textures.surface))
                    {
                        if (material.GetTexture("_MetallicGlossMap") == null)
                            throw new Exception("Missing surface texture: " + item.name);
                        if (!Mathf.Approximately(material.GetFloat("_Smoothness"), 1f))
                            throw new Exception("Packed roughness was scaled incorrectly: " + item.name);
                    }
                }
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).ToArray();
                if (item.name.Contains("animated_mixamo"))
                {
                    if (clips.Length == 0 || model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                        throw new Exception("Supplied rig/animation was lost: " + item.name);
                    animated++;
                }
            }
            Debug.Log($"Meshy library verified: {catalog.models.Length} native FBXs, prefabs and URP materials; {animated} supplied animated models.");
        }
    }
}
