using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Native FBX import avoids legacy centimetre bind-matrix differences in glTF runtimes.
    public static class MotionAssets
    {
        private const string Source = "Assets/_Game/Art/Motion/";
        public const string Folder = "Assets/Generated/Motion/";
        [Serializable] private sealed class Spec { public string model, motion, file; public float height; }
        [Serializable] private sealed class Catalog { public Spec[] items; }

        public static void BuildAndVerify()
        {
            Build();
            SuppliedMotionChecks.Verify();
        }

        private static Spec[] Read() => JsonUtility.FromJson<Catalog>("{\"items\":" + File.ReadAllText(Source + "manifest.json") + "}").items;
        public static string[] Names => Read().Select(entry => entry.model).Distinct().ToArray();
        public static string[] Roles(string model) => Read().Where(entry => entry.model == model).Select(entry => entry.motion)
            .Concat(model == "Academy" ? new[] { "Walk", "Run" } : Array.Empty<string>()).Distinct().ToArray();
        public static bool Has(string name) => Names.Contains(name);

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var entries = Read();
            foreach (var entry in entries)
            {
                var path = Source + entry.file;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            foreach (var group in entries.GroupBy(entry => entry.model))
            {
                var spec = group.First();
                var root = new GameObject(spec.model);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source + spec.file));
                model.transform.SetParent(root.transform, false);
                foreach (var animator in model.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(animator);
                var animation = model.GetComponent<Animation>() ?? model.AddComponent<Animation>();
                animation.playAutomatically = false;
                var motion = root.AddComponent<AuthoredMotion>();
                motion.Player = animation;
                motion.ModelName = spec.model;
                var material = Material(spec.model);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                foreach (var entry in group)
                {
                    var original = AssetDatabase.LoadAllAssetsAtPath(Source + entry.file).OfType<AnimationClip>()
                        .FirstOrDefault(clip => !clip.name.StartsWith("__preview"));
                    if (original == null) throw new Exception("Supplied FBX clip missing: " + entry.file);
                    var clip = UnityEngine.Object.Instantiate(original);
                    clip.name = entry.model + "-" + entry.motion;
                    clip.legacy = true;
                    clip.wrapMode = entry.motion == "Vault" ? WrapMode.Once : WrapMode.Loop;
                    // Remove only scene/root movement. Bone curves retain authored joint motion.
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.type != typeof(Transform)) continue;
                        if (string.IsNullOrEmpty(binding.path)) { AnimationUtility.SetEditorCurve(clip, binding, null); continue; }
                        if (model.transform.Find(binding.path) == null)
                            throw new Exception("Unresolved supplied bone: " + entry.file + "/" + binding.path);
                        var hips = binding.path.EndsWith("Hips");
                        if (hips && (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                        {
                            var curve = AnimationUtility.GetEditorCurve(clip, binding);
                            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, clip.length, curve.Evaluate(0)));
                        }
                    }
                    clip = SaveClip(clip);
                    typeof(AuthoredMotion).GetField(entry.motion).SetValue(motion, clip);
                }
                if (spec.model == "Academy")
                {
                    motion.Walk = Retarget(model, entries.First(entry => entry.model == "Daydream" && entry.motion == "Walk"));
                    motion.Run = Retarget(model, entries.First(entry => entry.model == "Daydream" && entry.motion == "Run"));
                }
                var reference = motion.Walk != null ? motion.Walk : motion.Vault;
                var idle = new AnimationClip { name = spec.model + "-Idle", legacy = true, wrapMode = WrapMode.Loop };
                foreach (var binding in AnimationUtility.GetCurveBindings(reference))
                {
                    var curve = AnimationUtility.GetEditorCurve(reference, binding);
                    AnimationUtility.SetEditorCurve(idle, binding, AnimationCurve.Constant(0, 1, curve.Evaluate(0)));
                }
                idle = SaveClip(idle);
                motion.Idle = idle;
                idle.SampleAnimation(model, 0);
                // Normalize the posed visual child, keeping profile height/build on its outer root.
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bounds = new Bounds(); var first = true;
                foreach (var skin in skins)
                {
                    skin.updateWhenOffscreen = true;
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                if (first || bounds.size.y < 0.001f) throw new Exception("Invalid native motion skin: " + spec.model);
                var scale = spec.height / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                if (motion.Walk == null)
                {
                    var joints = model.GetComponentsInChildren<Transform>();
                    var fallback = root.AddComponent<CozyAvatar>();
                    fallback.Head = joints.FirstOrDefault(bone => bone.name.EndsWith(":Head"));
                    fallback.LeftArm = joints.FirstOrDefault(bone => bone.name.EndsWith(":LeftArm"));
                    fallback.RightArm = joints.FirstOrDefault(bone => bone.name.EndsWith(":RightArm"));
                    fallback.LeftLeg = joints.FirstOrDefault(bone => bone.name.EndsWith(":LeftUpLeg"));
                    fallback.RightLeg = joints.FirstOrDefault(bone => bone.name.EndsWith(":RightUpLeg"));
                    fallback.enabled = false;
                    motion.Fallback = fallback;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Folder + spec.model + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip Retarget(GameObject target, Spec source)
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source + source.file));
            try
            {
                var original = AssetDatabase.LoadAllAssetsAtPath(Source + source.file).OfType<AnimationClip>()
                    .First(clip => !clip.name.StartsWith("__preview"));
                var from = actor.GetComponentsInChildren<Transform>().Where(bone => bone.name.StartsWith("mixamorig:"))
                    .ToDictionary(bone => bone.name);
                var to = target.GetComponentsInChildren<Transform>().Where(bone => from.ContainsKey(bone.name)).ToArray();
                // FBX transforms can hold frame zero (a running/vault pose), not the skin bind pose.
                var sourceBind = BindPose(actor);
                var targetBind = BindPose(target);
                var rests = to.ToDictionary(bone => bone.name, bone => (targetRotation: targetBind[bone].rotation,
                    sourceRotation: sourceBind[from[bone.name]].rotation, position: (Vector3)targetBind[bone].GetColumn(3),
                    sourceY: sourceBind[from[bone.name]].m13));
                var clip = new AnimationClip { name = "Academy-" + source.motion, legacy = true, frameRate = 30, wrapMode = WrapMode.Loop };
                var curves = to.ToDictionary(bone => bone.name, _ => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
                var hipsY = new AnimationCurve();
                var steps = Mathf.CeilToInt(original.length * 30);
                for (var frame = 0; frame <= steps; frame++)
                {
                    var time = Mathf.Min(frame / 30f, original.length);
                    original.SampleAnimation(actor, time);
                    foreach (var bone in to)
                    {
                        var rest = rests[bone.name];
                        var rotation = rest.targetRotation * Quaternion.Inverse(rest.sourceRotation) * from[bone.name].localRotation;
                        var values = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
                        for (var axis = 0; axis < 4; axis++) curves[bone.name][axis].AddKey(time, values[axis]);
                        if (bone.name == "mixamorig:Hips")
                        {
                            var ratio = Mathf.Abs(rest.sourceY) > 0.001f ? rest.position.y / rest.sourceY : 1;
                            hipsY.AddKey(time, rest.position.y + (from[bone.name].localPosition.y - rest.sourceY) * ratio);
                        }
                    }
                }
                foreach (var bone in to)
                {
                    var path = AnimationUtility.CalculateTransformPath(bone, target.transform);
                    for (var axis = 0; axis < 4; axis++) clip.SetCurve(path, typeof(Transform), "localRotation." + "xyzw"[axis], curves[bone.name][axis]);
                    var rest = rests[bone.name].position;
                    clip.SetCurve(path, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, original.length, rest.x));
                    clip.SetCurve(path, typeof(Transform), "localPosition.y", bone.name == "mixamorig:Hips" ? hipsY : AnimationCurve.Constant(0, original.length, rest.y));
                    clip.SetCurve(path, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0, original.length, rest.z));
                }
                clip.EnsureQuaternionContinuity();
                return SaveClip(clip);
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        private static System.Collections.Generic.Dictionary<Transform, Matrix4x4> BindPose(GameObject model)
        {
            var world = new System.Collections.Generic.Dictionary<Transform, Matrix4x4>();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                for (var index = 0; index < skin.bones.Length; index++)
                    world[skin.bones[index]] = skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[index].inverse;
            return world.ToDictionary(pair => pair.Key, pair =>
                (world.TryGetValue(pair.Key.parent, out var parent) ? parent.inverse : pair.Key.parent.worldToLocalMatrix) * pair.Value);
        }

        private static AnimationClip SaveClip(AnimationClip clip)
        {
            var path = Folder + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Material Material(string model)
        {
            var path = Folder + model + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Textures/" + model + "/BaseColor.png"));
            var normalPath = Source + "Textures/" + model + "/Normal.png";
            if (File.Exists(normalPath))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                if (importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
                result.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                result.EnableKeyword("_NORMALMAP");
            }
            var surfacePath = Source + "Textures/" + model + "/MetallicSmoothness.png";
            if (File.Exists(surfacePath))
            {
                result.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(surfacePath));
                result.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            result.SetColor("_BaseColor", Color.white);
            result.SetFloat("_Smoothness", 0.25f);
            result.SetFloat("_Cull", 0);
            return result;
        }
    }
}
