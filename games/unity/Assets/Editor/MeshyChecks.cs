using System;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ContextSpeak.Editor
{
    /// Checks imported geometry and saved prefabs, not preview images or file names alone.
    public static class MeshyChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Meshy characters: " + message);
        }

        public static void VerifyColdImport()
        {
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            foreach (var source in System.IO.Directory.GetFiles(MeshyCharacters.Source, "*.glb"))
                AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceUpdate);
            // Startup can import sources before the generated URP configuration exists.
            MeshyCharacters.ImportAndVerify();
        }

        private static bool SupportsUrp(Material material)
        {
            var shader = ShaderUtil.GetShaderData(material.shader);
            return Enumerable.Range(0, shader.SerializedSubshaderCount).Any(index =>
                shader.GetSerializedSubshader(index).FindTagValue(new ShaderTagId("RenderPipeline")).name == "UniversalPipeline");
        }

        public static void Verify()
        {
            foreach (var spec in MeshyCharacters.Specs)
            {
                var name = spec.id;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/Meshy/" + name + ".prefab");
                Check(prefab != null, name + " has a Unity prefab.");
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    Check(renderers.Length > 0, name + " contains actual renderable geometry.");
                    // Skinned renderer bounds include animation padding; measure actual posed vertices.
                    var points = instance.GetComponentsInChildren<MeshFilter>()
                        .SelectMany(filter => filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToList();
                    foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var baked = new Mesh();
                        skin.BakeMesh(baked, MotionAssets.Has(name));
                        points.AddRange(baked.vertices.Select(skin.transform.TransformPoint));
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                    var bounds = new Bounds(points[0], Vector3.zero);
                    foreach (var point in points.Skip(1)) bounds.Encapsulate(point);
                    Check(Mathf.Abs(bounds.size.y - spec.height) < 0.04f, name + " keeps its inspected standing/seated metre scale (actual " + bounds + ").");
                    Check(Mathf.Abs(bounds.min.y) < 0.03f, name + " stands on the floor.");
                    if (name == "TrenchElder" || name == "OldCardigan")
                        Check(bounds.size.x < spec.height * 0.7f, name + " uses its supplied relaxed walking pose instead of a T-pose.");
                    Check(instance.GetComponentsInChildren<Collider>().Length == 0, name + " visual does not add navigation blockers.");
                    Check(renderers.All(renderer => renderer.sharedMaterials.All(material => material != null
                        && material.shader != null && material.mainTexture != null && SupportsUrp(material))), name + " keeps its textured materials.");
                    var meshes = instance.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh)
                        .Concat(instance.GetComponentsInChildren<SkinnedMeshRenderer>().Select(skin => skin.sharedMesh));
                    var triangles = meshes.Sum(mesh => (long)mesh.triangles.Length / 3);
                    Check(triangles > 1000 && triangles <= 60000, name + " stays inside its geometry budget.");
                    if (MotionAssets.Has(name))
                    {
                        var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                        var motion = instance.GetComponent<AuthoredMotion>();
                        Check(skin != null && skin.bones.Length >= 20 && motion != null,
                            name + " retains its supplied skeleton and native clip player.");
                        Check(motion.Idle != null && (name == "Academy" ? motion.Vault != null : motion.Walk != null),
                            name + " has a neutral idle and its supplied action.");
                        Check(motion.Player != null && !motion.Player.playAutomatically,
                            "Supplied actions never auto-start as idle animations.");
                        if (name == "Daydream") Check(motion.Run != null, "Daydream uses both distinct supplied locomotion clips.");
                    }
                    else if (spec.rigSource == "generated-basic")
                    {
                        var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                        var avatar = instance.GetComponent<CozyAvatar>();
                        Check(skin != null && skin.bones.Length >= 7 && avatar?.LeftLeg != null && avatar.RightLeg != null,
                            name + " has a generated basic locomotion rig, separate from the protagonist meshes.");
                        var before = new Mesh();
                        var after = new Mesh();
                        try
                        {
                            skin.BakeMesh(before);
                            avatar.LeftLeg.localRotation *= Quaternion.Euler(20, 0, 0);
                            skin.BakeMesh(after);
                            Check(before.vertices.Zip(after.vertices, Vector3.Distance).Any(distance => distance > 0.03f),
                                name + " leg weights actually deform the mesh.");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
                        Check(!AssetDatabase.LoadAllAssetsAtPath(MeshyCharacters.Source + name + ".glb").OfType<AnimationClip>().Any(),
                            "Generated rigs must not claim supplied motion clips.");
                    }
                    else if (spec.rigSource == "supplied")
                    {
                        var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                        var clips = AssetDatabase.LoadAllAssetsAtPath(MeshyCharacters.Source + name + ".glb").OfType<AnimationClip>();
                        Check(skin != null && skin.bones.Length >= 20 && clips.Any(), name + " retains the supplied older resident rig and walking clip.");
                        Check(instance.GetComponent<CozyAvatar>()?.LeftLeg != null, name + " can use ordinary locomotion without auto-playing its source clip.");
                    }
                    else
                    {
                        Check(instance.GetComponentInChildren<SkinnedMeshRenderer>() == null, name + " is honestly marked as a static source mesh.");
                        Check(instance.GetComponentInChildren<CozyAvatar>() == null, name + " has no invented bone animation.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            Check(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Generated/Meshy/CharacterPreview.unity") != null,
                "the supplied characters have an Editor preview scene.");
            Debug.Log("ContextSpeak Meshy character checks passed");
        }
    }
}
