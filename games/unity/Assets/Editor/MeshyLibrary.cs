using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Native, local-only FBX library. Importing a model never places it in gameplay.
    public static class MeshyLibrary
    {
        public const string Root = "Assets/_Game/Art/MeshyLibrary/";
        [Serializable] public class Catalog { public Entry[] models; }
        [Serializable] public class Entry { public string name; public string sha256; public string fbx; public Maps textures; }
        [Serializable] public class Maps { public string baseColor; public string normal; public string surface; public string emission; }
        [Serializable] private class Result { public string name; public long triangles; public int bones; public int clips; public string error; }
        [Serializable] private class Report { public Result[] models; }

        public static Entry[] Read()
        {
            return File.Exists(Root + "manifest.json")
                ? JsonUtility.FromJson<Catalog>(File.ReadAllText(Root + "manifest.json")).models
                : Array.Empty<Entry>();
        }

        public static string PrefabPath(Entry item) => Path.GetDirectoryName(Root + item.fbx).Replace('\\', '/') + "/Model.prefab";

        [MenuItem("ContextSpeak/Meshy/Build and verify FBX library")]
        public static void ImportAndVerify()
        {
            CafeBuild.ConfigurePipeline();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var items = Read();
            if (items.Length == 0) throw new Exception("Run python3 tools/meshy/library.py first.");
            var results = new Result[items.Length];
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var result = new Result { name = item.name };
                results[index] = result;
                try
                {
                    Import(item, result);
                    Debug.Log($"Meshy FBX {index + 1}/{items.Length}: {item.name}; {result.triangles:N0} triangles, {result.clips} clips.");
                }
                catch (Exception error)
                {
                    result.error = error.Message;
                    Debug.LogError("Meshy FBX failed: " + item.name + ": " + error.Message);
                }
                AssetDatabase.SaveAssets();
                if (index % 8 == 7) EditorUtility.UnloadUnusedAssetsImmediate();
            }
            File.WriteAllText(Root + "import-report.json", JsonUtility.ToJson(new Report { models = results }, true));
            AssetDatabase.Refresh();
            MeshyLibraryChecks.Verify();
        }

        private static void Import(Entry item, Result result)
        {
            var path = Root + item.fbx;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new Exception("Unity could not load the native FBX.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var embedded = string.IsNullOrEmpty(item.textures.baseColor);
            if (embedded)
            {
                // Restore authored slots before extracting embedded FBX images. Old
                // white remaps otherwise hide those images on repeat imports.
                foreach (var key in importer.GetExternalObjectMap().Keys.Where(key => key.type == typeof(Material)).ToArray())
                    importer.RemoveRemap(key);
                importer.SaveAndReimport();
                var folder = Path.GetDirectoryName(path).Replace('\\', '/') + "/EmbeddedTextures";
                Directory.CreateDirectory(folder);
                importer.ExtractTextures(folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                importer.SaveAndReimport();
                model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            var sourceMaterials = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials).Where(value => value != null)
                .GroupBy(value => value.name).Select(group => group.First()).OrderBy(value => value.name).ToArray();
            var shared = embedded ? null : CreateMaterial(item);
            var changed = false;
            for (var index = 0; index < sourceMaterials.Length; index++)
            {
                var source = sourceMaterials[index];
                var material = embedded ? CreateMaterial(item, source, index) : shared;
                if (source == material) continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = new GameObject(item.name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(root.transform, false);
                // Keep supplied clips available without unexpectedly playing a walk/vault in the Editor.
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    animator.enabled = false;
                    animator.applyRootMotion = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(item));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var mesh in assets.OfType<Mesh>())
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    result.triangles += (long)mesh.GetIndexCount(submesh) / 3;
            result.clips = assets.OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__"));
            result.bones = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(s => s.bones.Length);
        }

        private static Material CreateMaterial(Entry item, Material original = null, int slot = -1)
        {
            var path = Path.GetDirectoryName(Root + item.fbx).Replace('\\', '/') + (slot < 0 ? "/Surface.mat" : "/Embedded-" + slot + ".mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", Texture(item.textures.baseColor) ?? EmbeddedTexture(item, original));
            material.SetColor("_BaseColor", original != null && original.HasProperty("_Color") ? original.color : Color.white);
            material.SetFloat("_Smoothness", string.IsNullOrEmpty(item.textures.surface) ? 0.5f : 1f);
            AssignMap(material, "_BumpMap", "_NORMALMAP", item.textures.normal);
            AssignMap(material, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP", item.textures.surface);
            AssignMap(material, "_EmissionMap", "_EMISSION", item.textures.emission);
            material.SetColor("_EmissionColor", string.IsNullOrEmpty(item.textures.emission) ? Color.black : Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture EmbeddedTexture(Entry item, Material original)
        {
            if (original?.mainTexture != null) return original.mainTexture;
            // Some generic FBX importers extract the image but leave its material
            // slot unlinked. A single embedded atlas is unambiguous; never guess
            // among several different maps.
            var folder = Path.GetDirectoryName(Root + item.fbx).Replace('\\', '/') + "/EmbeddedTextures";
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            var images = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            return images.Length == 1 ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(images[0])) : null;
        }

        private static Texture2D Texture(string path) => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Root + path);

        private static void AssignMap(Material material, string property, string keyword, string path)
        {
            var texture = Texture(path);
            material.SetTexture(property, texture);
            if (texture != null) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }

    /// Target only the local library; existing game imports stay unchanged.
    public class MeshyLibraryImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(MeshyLibrary.Root, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(MeshyLibrary.Root, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (name.EndsWith("_normal")) importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = !(name.EndsWith("_normal") || name.EndsWith("_roughness") ||
                name.EndsWith("_metallic") || name == "metallicsmoothness");
        }
    }
}
