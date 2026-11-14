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
        [Serializable] private sealed class Spec { public string model, motion, file, take; public float height; }
        [Serializable] private sealed class Catalog { public Spec[] items; }

        public static void BuildAndVerify()
        {
            Build();
            SuppliedMotionChecks.Verify();
        }

        private static Spec[] Read() => JsonUtility.FromJson<Catalog>("{\"items\":" + File.ReadAllText(Source + "manifest.json") + "}").items;
        public static string[] Names => Read().Select(entry => entry.model).Distinct().ToArray();
        public static string[] Roles(string model) => Read().Where(entry => entry.model == model).Select(entry => entry.motion)
            .Distinct().ToArray();
        public static bool Has(string name) => Names.Contains(name);

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var entries = Read();
            foreach (var entry in entries.GroupBy(item => item.file).Select(group => group.First()))
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
                var extras = new System.Collections.Generic.List<AnimationClip>();
                foreach (var state in animation.Cast<AnimationState>().ToArray()) animation.RemoveClip(state.name);
                var material = Material(spec.model);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                foreach (var entry in group)
                {
                    var original = AssetDatabase.LoadAllAssetsAtPath(Source + entry.file).OfType<AnimationClip>()
                        .FirstOrDefault(clip => !clip.name.StartsWith("__preview")
                            && (string.IsNullOrEmpty(entry.take) || clip.name.Split('|').Contains(entry.take)));
                    if (original == null) throw new Exception("Supplied FBX clip missing: " + entry.file);
                    var clip = UnityEngine.Object.Instantiate(original);
                    clip.name = entry.model + "-" + entry.motion;
                    clip.legacy = true;
                    clip.wrapMode = entry.motion == "Walk" || entry.motion == "Run" ? WrapMode.Loop : WrapMode.Once;
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
                    if (entry.motion == "Walk") motion.Walk = clip;
                    else if (entry.motion == "Run") motion.Run = clip;
                    else if (entry.motion == "Vault") motion.Vault = clip;
                    else extras.Add(clip);
                }
                motion.Extras = extras.ToArray();
                // Idle = a real standing pose, not the first (mid-stride) frame of the walk: take the
                // neutral start of a gesture clip, the end of a sit-to-stand, or else the walk frame
                // with both feet planted.
                AnimationClip reference = null;
                var standTime = 0f;
                foreach (var (take, atEnd) in new[] { ("Agree_Gesture", false), ("Big_Wave_Hello", false), ("Sit_to_Stand_Transition_F", true), ("Alert", false) })
                {
                    reference = extras.FirstOrDefault(clip => clip.name == spec.model + "-" + take);
                    if (reference != null) { standTime = atEnd ? reference.length : 0; break; }
                }
                if (reference == null)
                {
                    reference = motion.Walk != null ? motion.Walk : motion.Vault;
                    if (motion.Walk != null) standTime = PlantedPose(motion.Walk, model);
                }
                var idle = new AnimationClip { name = spec.model + "-Idle", legacy = true, wrapMode = WrapMode.Loop };
                foreach (var binding in AnimationUtility.GetCurveBindings(reference))
                {
                    if (binding.type == typeof(Transform) && string.IsNullOrEmpty(binding.path)) continue;
                    var curve = AnimationUtility.GetEditorCurve(reference, binding);
                    var value = curve.Evaluate(standTime);
                    if (binding.path.EndsWith("Hips") && (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                        value = curve.Evaluate(0);   // keep the body centred over the actor
                    AnimationUtility.SetEditorCurve(idle, binding, AnimationCurve.Constant(0, 1, value));
                }
                idle = SaveClip(idle);
                motion.Idle = idle;
                // The supplied leap contains a run-up and two hops. Extract its longest airborne
                // section; keep all original takes available separately for the animation gallery.
                var leap = motion.Find("Run_and_Leap");
                if (leap != null) BuildJump(motion, leap, model, idle);
                var crouch = motion.Find("air_squat") ?? motion.Find("Run_and_Jump");
                if (crouch != null) BuildCrouch(motion, crouch, model, idle);
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
                var turned = CharacterFacing.Align(root.transform, model.transform);
                if (turned != 0) Debug.Log("Motion " + spec.model + ": turned " + turned + "° to face forward.");
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

        private static void BuildCrouch(AuthoredMotion motion, AnimationClip source, GameObject model, AnimationClip idle)
        {
            var hips = CharacterFacing.Bone(model.transform, "Hips");
            var head = CharacterFacing.Bone(model.transform, "Head");
            var left = CharacterFacing.Bone(model.transform, "LeftFoot");
            var right = CharacterFacing.Bone(model.transform, "RightFoot");
            idle.SampleAnimation(model, 0);
            var floor = Mathf.Min(left.position.y, right.position.y);
            var tolerance = (hips.position.y - floor) * .15f;
            var lowest = hips.position.y; var at = 0f; var lift = 0f;
            // Use a grounded squat/landing pose, never an airborne tuck or a scaled standing body.
            for (var i = 0; i <= 120; i++)
            {
                var time = source.length * i / 120;
                source.SampleAnimation(model, time);
                var feet = Mathf.Min(left.position.y, right.position.y);
                var higherFoot = Mathf.Max(left.position.y, right.position.y);
                var torso = head.position - hips.position;
                if (higherFoot > floor + tolerance || hips.position.y - higherFoot < tolerance
                    || torso.normalized.y < .65f || hips.position.y >= lowest) continue;
                lowest = hips.position.y; at = time; lift = feet - floor;
            }
            var pose = new AnimationClip { name = motion.ModelName + "-Crouch", legacy = true, wrapMode = WrapMode.Loop };
            var correction = hips.parent.InverseTransformVector(Vector3.up * lift).y;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(at);
                if (binding.path.EndsWith("Hips") && binding.propertyName == "m_LocalPosition.y") value -= correction;
                AnimationUtility.SetEditorCurve(pose, binding, AnimationCurve.Constant(0, 1, value));
            }
            motion.Crouch = SaveClip(pose);
        }

        private static void BuildJump(AuthoredMotion motion, AnimationClip leap, GameObject model, AnimationClip idle)
        {
            var left = CharacterFacing.Bone(model.transform, "LeftFoot");
            var right = CharacterFacing.Bone(model.transform, "RightFoot");
            var hips = CharacterFacing.Bone(model.transform, "Hips");
            if (left == null || right == null || hips == null) throw new Exception("Leap rig is missing feet/hips.");
            idle.SampleAnimation(model, 0);
            var hipHeight = hips.localPosition.y;
            var clearance = Mathf.Abs(hips.position.y - Mathf.Min(left.position.y, right.position.y)) * .1f;
            var steps = Mathf.CeilToInt(leap.length * 60);
            var heights = new float[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                leap.SampleAnimation(model, leap.length * i / steps);
                heights[i] = Mathf.Min(left.position.y, right.position.y);
            }
            var floor = heights.Min();
            var start = 0; var end = 0; var run = -1;
            for (var i = 0; i <= steps + 1; i++)
            {
                if (i <= steps && heights[i] > floor + clearance) { if (run < 0) run = i; }
                else if (run >= 0)
                {
                    if (i - run > end - start) { start = run; end = i; }
                    run = -1;
                }
            }
            if (end <= start) throw new Exception("No airborne section found in " + leap.name);
            start = Mathf.Max(0, start - 2); end = Mathf.Min(steps, end + 2);
            var from = leap.length * start / steps;
            var duration = leap.length * (end - start) / steps;
            var jump = new AnimationClip { name = motion.ModelName + "-Jump", legacy = true, wrapMode = WrapMode.ClampForever };
            foreach (var binding in AnimationUtility.GetCurveBindings(leap))
            {
                var source = AnimationUtility.GetEditorCurve(leap, binding);
                var keys = new Keyframe[end - start + 1];
                for (var i = 0; i < keys.Length; i++)
                {
                    var t = duration * i / (keys.Length - 1);
                    var value = binding.path.EndsWith("Hips") && binding.propertyName == "m_LocalPosition.y"
                        ? hipHeight : source.Evaluate(from + t);
                    keys[i] = new Keyframe(t, value);
                }
                AnimationUtility.SetEditorCurve(jump, binding, new AnimationCurve(keys));
            }
            jump.EnsureQuaternionContinuity();
            motion.Jump = SaveClip(jump);
            var apex = Array.IndexOf(heights, heights.Skip(start).Take(end - start + 1).Max(), start);
            motion.JumpApex = (float)(apex - start) / (end - start);
        }

        /// Prefer double support over a passing pose with one foot raised.
        private static float PlantedPose(AnimationClip walk, GameObject model)
        {
            var left = CharacterFacing.Bone(model.transform, "LeftFoot");
            var right = CharacterFacing.Bone(model.transform, "RightFoot");
            if (left == null || right == null) return 0;
            var best = 0f; var closest = float.MaxValue;
            for (var i = 0; i < 32; i++)
            {
                var t = walk.length * i / 32f;
                walk.SampleAnimation(model, t);
                var gap = left.position - right.position;
                var score = Mathf.Abs(gap.y) * 100 + Vector3.ProjectOnPlane(gap, Vector3.up).magnitude;
                if (score < closest) { closest = score; best = t; }
            }
            return best;
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
