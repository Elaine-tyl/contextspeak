using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Water > Setup Swimming System — inspects the player, imports the supplied swim
    /// FBX clips (non-destructively: originals and character prefabs are not modified), creates the
    /// swimming profiles, wires the open scene, and reports what is missing. Also builds the
    /// SwimmingTestScene.
    public static class SwimmingSetup
    {
        public const string SwimSource = "Assets/_Game/Art/Motion/Swim/";
        public const string ClipFolder = "Assets/Generated/Motion/Swim/";
        public const string ClipSetFolder = "Assets/ContextSpeak/Resources/Swimming/";
        public const string ProfileFolder = "Assets/ContextSpeak/Data/Water/Swimming";
        public const string TestScene = "Assets/ContextSpeak/Scenes/SwimmingTestScene.unity";
        public const string PrefabFolder = "Assets/ContextSpeak/Prefabs/Water/";

        // Measured in Blender from "swimming_to_edge" (30 fps, 151 frames): frames 9–43 are one full
        // stroke cycle with the body horizontal; from frame 41 the swimmer reaches for the edge and
        // hangs on it (hands at the coping by ~frame 75, settled by ~100).
        private const float LoopStart = 8 / 30f, LoopEnd = 42 / 30f, ReachStart = 40 / 30f, ReachEnd = 100 / 30f;

        [MenuItem("ContextSpeak/Water/Setup Swimming System")]
        public static void SetupMenu()
        {
            var report = Setup(true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Swimming system set up (" + EditorSceneManager.GetActiveScene().name + "):\n- " + string.Join("\n- ", report));
        }

        public static List<string> Setup(bool importClips)
        {
            var report = new List<string>();
            // 1–2. Player controller inspection and incompatible physics.
            var players = Object.FindObjectsByType<CafePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (players.Length == 0) report.Add("NOTE: no CafePlayer in the open scene — components are also added automatically at runtime in any scene with water.");
            foreach (var player in players)
            {
                var controller = player.GetComponent<CharacterController>();
                report.Add("Player '" + player.name + "': " + (controller != null ? "CharacterController (h " + controller.height.ToString("0.00") + ", r " + controller.radius.ToString("0.00") + ")" : "NO CharacterController") +
                           ", model " + (player.Model != null ? player.Model.name : "none") +
                           ", rigs: " + string.Join(", ", player.GetComponentsInChildren<AuthoredMotion>(true).Select(m => m.ModelName)));
                var rigidbody = player.GetComponent<Rigidbody>();
                if (rigidbody != null && !rigidbody.isKinematic) report.Add("WARNING: '" + player.name + "' has a non-kinematic Rigidbody next to its CharacterController — two movement systems would fight. Not removed automatically; make it kinematic or remove it.");
                foreach (var other in player.GetComponents<MonoBehaviour>())
                    if (other is PondSwimming || other is ContextSpeak.Life.Swimming) report.Add("Note: legacy swimming component " + other.GetType().Name + " on the player hands over to SwimmingController.");
            }
            // 3. Profiles.
            foreach (var kind in new[] { WaterKind.Pool, WaterKind.Pond, WaterKind.Lake, WaterKind.River, WaterKind.Stream, WaterKind.Ocean }) Profile(kind);
            report.Add("Swimming profiles: " + ProfileFolder + " (Pool, Pond, Lake, River, Stream, Ocean — existing ones kept)");
            // Clips.
            if (importClips) report.AddRange(ImportClips());
            // 4. Water interaction triggers / bodies.
            if (Object.FindObjectsByType<WaterBody>(FindObjectsSortMode.None).Length == 0 && players.Length > 0)
                report.AddRange(WaterInteractionSetup.Setup().Select(line => "water: " + line));
            foreach (var body in Object.FindObjectsByType<WaterBody>(FindObjectsSortMode.None))
                if (body.Swimming == null) { body.Swimming = Profile(body.Kind); EditorUtility.SetDirty(body); }
            // 5–8. Player components (detector markers come from the capsule + rig bones at runtime).
            foreach (var player in players)
            {
                if (player.GetComponent<CharacterWaterInteraction>() == null) player.gameObject.AddComponent<CharacterWaterInteraction>();
                if (player.GetComponent<SwimmingController>() == null) player.gameObject.AddComponent<SwimmingController>();
                if (player.GetComponent<SwimmingAnimationController>() == null) player.gameObject.AddComponent<SwimmingAnimationController>();
                if (player.GetComponent<SwimmingCameraController>() == null) player.gameObject.AddComponent<SwimmingCameraController>();
                if (player.GetComponent<SwimmingWaterInteraction>() == null) player.gameObject.AddComponent<SwimmingWaterInteraction>();
                EditorUtility.SetDirty(player.gameObject);
            }
            if (players.Length > 0 && Object.FindFirstObjectByType<SwimmingDebugPanel>() == null) new GameObject("Swimming debug panel").AddComponent<SwimmingDebugPanel>();
            report.Add("Animator parameters: not used — the player rigs play legacy Animation clips through AuthoredMotion (the same path as walking); swimming drives AuthoredMotion.Override, so no Animator Controller is needed or created.");
            report.Add("Camera: SwimmingCameraController on the player (drives CafePlayer's camera target / distance / lens floor; Cinemachine unchanged).");
            var ladders = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var n = t.name.ToLowerInvariant();
                if (!n.Contains("ladder") || t.GetComponent<WaterExitPoint>() != null || t.GetComponentInParent<WaterExitPoint>() != null) continue;
                var point = t.gameObject.AddComponent<WaterExitPoint>();
                point.Kind = WaterExitPoint.ExitKind.Ladder;
                ladders++;
            }
            report.Add("Exit points: " + Object.FindObjectsByType<WaterExitPoint>(FindObjectsSortMode.None).Length + " (" + ladders + " ladders tagged now; pool edges, banks and docks without markers are detected automatically; shores need nothing)");
            // 9. Prefabs.
            report.AddRange(SamplePrefabs());
            // 12. Shaders.
            if (Shader.Find("ContextSpeak/Water Surface") == null) report.Add("MISSING shader: ContextSpeak/Water Surface");
            if (Shader.Find("Hidden/ContextSpeak/Water Ripple Sim") == null) report.Add("MISSING shader: Hidden/ContextSpeak/Water Ripple Sim");
            report.Add("Controls: WASD swim · Shift fast · Space up · C/Ctrl dive · F back float · E ladder/edge — rebind on SwimmingController (Input System actions). Debug panel: F5.");
            return report;
        }

        public static SwimmingProfile Profile(WaterKind kind)
        {
            Directory.CreateDirectory(ProfileFolder);
            var path = ProfileFolder + "/" + kind + " Swimming.asset";
            var profile = AssetDatabase.LoadAssetAtPath<SwimmingProfile>(path);
            if (profile != null) return profile;
            profile = SwimmingProfile.Create(kind);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        // ------------------------------------------------------------------ clips

        [MenuItem("ContextSpeak/Water/Import Swimming Animations")]
        public static void ImportMenu() => Debug.Log("Swimming animations:\n- " + string.Join("\n- ", ImportClips()));

        public static List<string> ImportClips()
        {
            var report = new List<string>();
            Directory.CreateDirectory(ClipFolder);
            Directory.CreateDirectory(ClipSetFolder);
            AssetDatabase.Refresh();
            if (!Directory.Exists(SwimSource)) { report.Add("MISSING: " + SwimSource + " (put <Model>-SwimToEdge.fbx files there)"); return report; }
            var files = Directory.GetFiles(SwimSource, "*.fbx").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();
            if (files.Length == 0) report.Add("MISSING: no FBX in " + SwimSource);
            var sets = new Dictionary<string, SwimClipSet>();
            var entries = new List<(string file, string model, string role)>();
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var dash = name.IndexOf('-');
                if (dash < 0) { report.Add("Skipped " + name + " (expected <Model>-<Role>.fbx)"); continue; }
                entries.Add((file, name.Substring(0, dash), name.Substring(dash + 1)));
            }
            var models = entries.Select(e => e.model).Distinct().ToArray();
            foreach (var (file, modelName, role) in entries)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(file);
                if (importer.animationType != ModelImporterAnimationType.Legacy || importer.animationCompression != ModelImporterAnimationCompression.Off || importer.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    importer.animationType = ModelImporterAnimationType.Legacy;
                    importer.importAnimation = true;
                    importer.animationCompression = ModelImporterAnimationCompression.Off;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                }
                var source = AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (source == null) { report.Add("MISSING: no animation in " + file); continue; }
                // Climbing clips are shared: every character uses the same Mixamo skeleton, so a clip supplied
                // for one is retargeted (bone names) to the others that have no clip of their own for that role.
                var shared = Shared.Contains(role);
                var targets = shared ? models.Where(m => m == modelName || !entries.Any(e => e.model == m && e.role == role)).ToArray() : new[] { modelName };
                foreach (var target in targets)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotionAssets.Folder + target + ".prefab");
                    if (prefab == null) { report.Add("MISSING: character prefab " + MotionAssets.Folder + target + ".prefab — build the characters first"); continue; }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    try { ImportRole(report, sets, file, source, modelName, target, role, instance); }
                    finally { Object.DestroyImmediate(instance); }
                }
            }
            foreach (var set in sets.Values)
            {
                var missing = new List<string>();
                if (set.TreadWater == null) missing.Add("TreadWater");
                if (set.BackFloat == null) missing.Add("BackFloat");
                if (set.UnderwaterSwim == null) missing.Add("UnderwaterSwim (swim cycle + pitch used)");
                if (set.ClimbLadder == null) missing.Add("ClimbPoolLadder");
                if (set.PullOut == null) missing.Add("pull-out over the edge");
                if (missing.Count == 0) { set.Notes = "All swimming clips authored."; continue; }
                set.Notes = "Missing authored clips (temporary procedural/substitute used): " + string.Join(", ", missing) +
                            ".\nAdd FBX files named " + set.ModelName + "-TreadWater.fbx, -BackFloat.fbx, -UnderwaterSwim.fbx, -ClimbLadder.fbx, -PullOut.fbx to " + SwimSource + " and run Import Swimming Animations.";
                report.Add("MISSING animations for " + set.ModelName + ": " + string.Join(", ", missing) + " — suggested Mixamo clips: Treading Water, Floating / Back float.");
            }
            AssetDatabase.SaveAssets();
            return report;
        }

        private static readonly HashSet<string> Shared = new HashSet<string> { "ClimbDown", "QuadClimbRight", "ClimbLadder", "PullOut" };

        private static void ImportRole(List<string> report, Dictionary<string, SwimClipSet> sets, string file, AnimationClip source, string sourceModel, string modelName, string role, GameObject instance)
        {
            var motion = instance.GetComponentInChildren<AuthoredMotion>(true);
            var rig = motion != null && motion.Player != null ? motion.Player.gameObject : instance;
            var (bindings, unresolved) = Remap(source, rig.transform);
            if (!sets.TryGetValue(modelName, out var set)) sets[modelName] = set = ClipSet(modelName);
            var from = sourceModel == modelName ? "" : " (retargeted from " + sourceModel + ")";
            switch (role)
            {
                case "SwimToEdge":
                {
                    var start = LoopStart; var end = BestLoopEnd(source, bindings, LoopStart, LoopEnd);
                    set.Swim = Save(Extract(source, bindings, start, end, true), modelName + "-Swim");
                    set.EdgeReach = Save(Extract(source, bindings, ReachStart, Mathf.Min(ReachEnd, source.length), false), modelName + "-EdgeReach");
                    Measure(set, rig, motion);
                    report.Add(modelName + ": Swim loop " + start.ToString("0.00") + "–" + end.ToString("0.00") + " s, EdgeReach " + ReachStart.ToString("0.00") + "–" + ReachEnd.ToString("0.00") + " s from " + Path.GetFileName(file) +
                               " · facing fix " + set.SwimYaw.ToString("0") + "° · body pitch " + set.SwimBodyPitch.ToString("0") + "°" + (unresolved > 0 ? " · " + unresolved + " unmatched curves dropped" : ""));
                    if (Mathf.Abs(set.SwimBodyPitch) > 35) report.Add("WARNING: " + modelName + " swim clip is not horizontal (" + set.SwimBodyPitch.ToString("0") + "°) — check the source animation.");
                    break;
                }
                case "ClimbDown":
                case "ClimbLadder":
                {
                    // One rung-to-rung cycle; the hips' descent is removed (the controller moves the body),
                    // and its size is kept so the climbing speed matches the hands and feet.
                    var rise = HipsDrop(source, bindings, rig);
                    set.ClimbLadder = Save(Extract(source, bindings, 0, source.length, true, true), modelName + "-ClimbLadder");
                    set.ClimbRise = Mathf.Abs(rise);
                    set.ClimbAuthoredDown = role == "ClimbDown" || rise > 0;
                    report.Add(modelName + ": ClimbLadder from " + Path.GetFileName(file) + from + " · " + set.ClimbRise.ToString("0.00") + " m per " + set.ClimbLadder.length.ToString("0.0") + " s cycle" + (set.ClimbAuthoredDown ? ", played backwards to climb up" : ""));
                    break;
                }
                case "QuadClimbRight":
                case "PullOut":
                {
                    // Quad Climb: hands planted on the ledge, the body pushes up (first ~0.5 s); sideways travel removed.
                    var end = role == "QuadClimbRight" ? Mathf.Min(source.length, 16 / 30f) : source.length;
                    set.PullOut = Save(Extract(source, bindings, 0, end, false, true), modelName + "-PullOut");
                    report.Add(modelName + ": PullOut (" + role + " 0–" + end.ToString("0.00") + " s) from " + Path.GetFileName(file) + from);
                    break;
                }
                default:
                {
                    var loop = role == "TreadWater" || role == "BackFloat" || role == "UnderwaterSwim";
                    var clip = Save(Extract(source, bindings, 0, source.length, loop), modelName + "-" + role);
                    switch (role)
                    {
                        case "TreadWater": set.TreadWater = clip; break;
                        case "BackFloat": set.BackFloat = clip; break;
                        case "UnderwaterSwim": set.UnderwaterSwim = clip; break;
                        default: report.Add("Skipped role '" + role + "' in " + Path.GetFileName(file) + " (known: SwimToEdge, TreadWater, BackFloat, UnderwaterSwim, ClimbDown, ClimbLadder, QuadClimbRight, PullOut)"); return;
                    }
                    report.Add(modelName + ": " + role + " imported" + from);
                    break;
                }
            }
            EditorUtility.SetDirty(set);
        }

        /// How far (m, world) the hips drop over the clip on this rig (positive = the clip climbs down).
        private static float HipsDrop(AnimationClip clip, List<(EditorCurveBinding from, EditorCurveBinding to)> bindings, GameObject rig)
        {
            foreach (var (fromBinding, toBinding) in bindings)
            {
                if (!fromBinding.path.EndsWith("Hips") || fromBinding.propertyName != "m_LocalPosition.y") continue;
                var curve = AnimationUtility.GetEditorCurve(clip, fromBinding);
                var hips = rig.transform.Find(toBinding.path);
                if (curve == null || hips == null) return 0;
                var scale = hips.parent != null ? hips.parent.lossyScale.y : 1;
                return (curve.Evaluate(0) - curve.Evaluate(clip.length)) * scale;
            }
            return 0;
        }

        private static SwimClipSet ClipSet(string model)
        {
            var path = ClipSetFolder + model + " Swim Clips.asset";
            var set = AssetDatabase.LoadAssetAtPath<SwimClipSet>(path);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<SwimClipSet>();
            set.ModelName = model;
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        /// Curve bindings rewritten to the rig's own hierarchy (matched by bone name), root motion removed.
        private static (List<(EditorCurveBinding from, EditorCurveBinding to)>, int) Remap(AnimationClip clip, Transform rig)
        {
            var byName = new Dictionary<string, string>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t != rig && !byName.ContainsKey(t.name)) byName[t.name] = AnimationUtility.CalculateTransformPath(t, rig);
            var result = new List<(EditorCurveBinding, EditorCurveBinding)>();
            var unresolved = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
                var target = binding;
                if (rig.Find(binding.path) == null)
                {
                    var last = binding.path.Substring(binding.path.LastIndexOf('/') + 1);
                    if (!byName.TryGetValue(last, out var path)) { unresolved++; continue; }
                    target.path = path;
                }
                result.Add((binding, target));
            }
            return (result, unresolved);
        }

        private static bool IsHipsXZ(EditorCurveBinding b) => b.path.EndsWith("Hips") && (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.z");

        /// Sub-range [from, to] as a new legacy clip; loops are made seamless by easing the last 30 %
        /// toward the first frame (rotations by slerp, so no invalid quaternions) — removes the pop
        /// without changing the stroke itself.
        private static AnimationClip Extract(AnimationClip source, List<(EditorCurveBinding from, EditorCurveBinding to)> bindings, float from, float to, bool loop, bool inPlaceVertical = false)
        {
            var clip = new AnimationClip { legacy = true, frameRate = 30, wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever };
            var duration = Mathf.Max(0.05f, to - from);
            var frames = Mathf.Max(2, Mathf.RoundToInt(duration * 30));
            float Ease(int i) { var u = Mathf.InverseLerp(0.7f, 1f, (float)i / frames); return u * u * (3 - 2 * u); }
            AnimationCurve Curve(Keyframe[] keys)
            {
                var curve = new AnimationCurve(keys);
                for (var i = 0; i < keys.Length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); }
                return curve;
            }
            foreach (var group in bindings.GroupBy(b => b.from.path))
            {
                var rotation = group.Where(b => b.from.propertyName.StartsWith("m_LocalRotation.")).ToDictionary(b => b.from.propertyName.Substring(b.from.propertyName.Length - 1));
                var hasRotation = rotation.Count == 4 && "xyzw".All(k => rotation.ContainsKey(k.ToString()));
                foreach (var (fromBinding, toBinding) in group)
                {
                    if (hasRotation && fromBinding.propertyName.StartsWith("m_LocalRotation.")) continue;
                    var curve = AnimationUtility.GetEditorCurve(source, fromBinding);
                    if (curve == null) continue;
                    var keys = new Keyframe[frames + 1];
                    var ramp = inPlaceVertical && fromBinding.path.EndsWith("Hips") && fromBinding.propertyName == "m_LocalPosition.y" ? curve.Evaluate(to) - curve.Evaluate(from) : 0;
                    var gap = curve.Evaluate(to) - curve.Evaluate(from) - ramp;
                    for (var i = 0; i <= frames; i++)
                    {
                        var t = duration * i / frames;
                        var value = IsHipsXZ(fromBinding) ? curve.Evaluate(from) : curve.Evaluate(from + t) - ramp * i / frames;
                        if (loop && !IsHipsXZ(fromBinding)) value -= gap * Ease(i);
                        keys[i] = new Keyframe(t, value);
                    }
                    AnimationUtility.SetEditorCurve(clip, toBinding, Curve(keys));
                }
                if (!hasRotation) continue;
                var source4 = "xyzw".Select(k => AnimationUtility.GetEditorCurve(source, rotation[k.ToString()].from)).ToArray();
                Quaternion At(float t) => new Quaternion(source4[0].Evaluate(t), source4[1].Evaluate(t), source4[2].Evaluate(t), source4[3].Evaluate(t)).normalized;
                var q0 = At(from); var q1 = At(to);
                var correction = q0 * Quaternion.Inverse(q1);
                var out4 = new Keyframe[4][];
                for (var k = 0; k < 4; k++) out4[k] = new Keyframe[frames + 1];
                var previous = Quaternion.identity;
                for (var i = 0; i <= frames; i++)
                {
                    var t = duration * i / frames;
                    var q = At(from + t);
                    if (loop) q = Quaternion.Slerp(Quaternion.identity, correction, Ease(i)) * q;
                    if (i > 0 && Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous = q;
                    out4[0][i] = new Keyframe(t, q.x); out4[1][i] = new Keyframe(t, q.y); out4[2][i] = new Keyframe(t, q.z); out4[3][i] = new Keyframe(t, q.w);
                }
                for (var k = 0; k < 4; k++) AnimationUtility.SetEditorCurve(clip, rotation["xyzw"[k].ToString()].to, Curve(out4[k]));
            }
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        /// The end time near `around` whose pose (all rotation curves) is closest to the start pose.
        private static float BestLoopEnd(AnimationClip source, List<(EditorCurveBinding from, EditorCurveBinding to)> bindings, float start, float around)
        {
            var curves = bindings.Where(b => b.from.propertyName.StartsWith("m_LocalRotation")).Select(b => AnimationUtility.GetEditorCurve(source, b.from)).Where(c => c != null).ToArray();
            var best = around; var bestScore = float.MaxValue;
            for (var t = around - 0.15f; t <= around + 0.15f; t += 1 / 60f)
            {
                if (t >= source.length) break;
                var score = 0f;
                foreach (var c in curves) score += Mathf.Abs(c.Evaluate(t) - c.Evaluate(start));
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        private static AnimationClip Save(AnimationClip clip, string name)
        {
            clip.name = name;
            var path = ClipFolder + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// Head direction of the swim pose in the rig root's space: yaw to the model's forward and pitch.
        private static void Measure(SwimClipSet set, GameObject rig, AuthoredMotion motion)
        {
            if (set.Swim == null) return;
            var root = motion != null ? motion.transform : rig.transform;
            set.Swim.SampleAnimation(rig, 0);
            Transform Find(string bone) => rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == bone || t.name.EndsWith(":" + bone));
            var hips = Find("Hips"); var head = Find("Head");
            if (hips == null || head == null) return;
            var direction = root.InverseTransformDirection(head.position - hips.position).normalized;
            set.SwimBodyPitch = Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            var flat = new Vector3(direction.x, 0, direction.z);
            set.SwimYaw = flat.sqrMagnitude > 0.01f ? -Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : 0;
            if (motion != null && motion.Idle != null) motion.Idle.SampleAnimation(rig, 0);
        }

        // ------------------------------------------------------------------ prefabs

        private static List<string> SamplePrefabs()
        {
            var report = new List<string>();
            Directory.CreateDirectory(PrefabFolder);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Pool Ladder.prefab") == null)
            {
                var ladder = Ladder(Vector3.zero, 0, 1.6f, 0.12f);
                PrefabUtility.SaveAsPrefabAsset(ladder, PrefabFolder + "Pool Ladder.prefab");
                Object.DestroyImmediate(ladder);
                report.Add("Prefab: " + PrefabFolder + "Pool Ladder.prefab (WaterExitPoint: hold in the water, stand on the deck)");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Dock Steps.prefab") == null)
            {
                var dock = new GameObject("Dock Steps");
                var point = dock.AddComponent<WaterExitPoint>();
                point.Kind = WaterExitPoint.ExitKind.Dock;
                point.Bottom = new GameObject("Bottom (in water)").transform; point.Bottom.SetParent(dock.transform, false); point.Bottom.localPosition = new Vector3(0, -0.15f, -0.7f);
                point.Top = new GameObject("Top (on dock)").transform; point.Top.SetParent(dock.transform, false); point.Top.localPosition = new Vector3(0, 0.3f, 0.6f);
                PrefabUtility.SaveAsPrefabAsset(dock, PrefabFolder + "Dock Steps.prefab");
                Object.DestroyImmediate(dock);
                report.Add("Prefab: " + PrefabFolder + "Dock Steps.prefab");
            }
            return report;
        }

        /// Pool ladder: rails and rungs (no colliders, so the capsule can climb), exit point at the top.
        /// origin = top of the pool wall (deck edge), facing (yaw) = from the deck into the pool.
        public static GameObject Ladder(Vector3 deckEdge, float yaw, float depth, float waterBelowDeck)
        {
            var root = new GameObject("Pool ladder");
            root.transform.SetPositionAndRotation(deckEdge, Quaternion.Euler(0, yaw, 0));
            var steel = Hex("C9D1D6");
            foreach (var side in new[] { -0.24f, 0.24f })
            {
                var rail = Shape("Ladder rail", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.045f, (depth + 0.9f) / 2, 0.045f), steel, false, root.transform);
                rail.transform.localPosition = new Vector3(side, (0.9f - depth) / 2, 0.16f);
                var hand = Shape("Ladder handrail", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.045f, 0.3f, 0.045f), steel, false, root.transform);
                hand.transform.localPosition = new Vector3(side, 0.9f, -0.12f);
                hand.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            for (var y = -depth + 0.3f; y < -0.05f; y += 0.3f)
            {
                var rung = Box("Ladder rung", Vector3.zero, new Vector3(0.48f, 0.035f, 0.08f), steel, false, root.transform);
                rung.transform.localPosition = new Vector3(0, y, 0.16f);
            }
            foreach (var r in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(r);
            var point = root.AddComponent<WaterExitPoint>();
            point.Kind = WaterExitPoint.ExitKind.Ladder;
            point.Bottom = new GameObject("Bottom (hold in the water)").transform;
            point.Bottom.SetParent(root.transform, false);
            point.Bottom.localPosition = new Vector3(0, -waterBelowDeck, 0.5f);
            point.Top = new GameObject("Top (stand on deck)").transform;
            point.Top.SetParent(root.transform, false);
            point.Top.localPosition = new Vector3(0, 0.02f, -0.55f);
            return root;
        }

        // ------------------------------------------------------------------ test scene

        [MenuItem("ContextSpeak/Water/Build Swimming Test Scene")]
        public static void BuildTestSceneMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildTestScene();
        }

        public static void BuildTestScene()
        {
            Missing.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Swimming test");
            Daylight("SwimTest", "E6EEF2");
            var ground = Box("Test ground lawn", new Vector3(0, -0.25f, 0), new Vector3(160, 0.5f, 160), Hex("7C8F5A"), true);
            var notes = new List<string>();
            var tile = Hex("6FB7CC");
            var earth = Hex("4A4236");

            // Shallow wading area (0.45 m): never swims.
            var shallow = new Vector3(-34, 0, 8);
            WaterInteractionSetup.HoleIn(ground, shallow, new Vector2(8, 8));
            Box("Shallow floor", shallow + Vector3.down * 0.65f, new Vector3(8, 0.2f, 8), Hex("8C9C8A"), true);
            Walls(shallow, new Vector2(8, 8), -0.55f, earth);
            WaterInteractionSetup.Water("Shallow water", shallow + Vector3.down * 0.1f, new Vector2(8, 8), WaterSettings.WaterType.Pond, "", 0);

            // Pool: shallow end 1.0 m → deep end 2.0 m (Blender model or boxes) with a ladder at the deep end.
            var pool = new Vector3(-14, 0, -8);
            var poolModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/water/Pool.glb");
            WaterInteractionSetup.HoleIn(ground, pool, poolModel != null ? new Vector2(15, 10) : new Vector2(10.2f, 5.2f));
            if (poolModel != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(poolModel);
                model.transform.position = pool;
                WaterInteractionSetup.Solid(model);
                WaterInteractionSetup.Retile(model);
            }
            else { WaterInteractionSetup.BoxPool(pool); notes.Add("Pool.glb not found — box pool used"); }
            WaterInteractionSetup.Water("Pool water", pool + Vector3.up * -0.12f, new Vector2(10, 5), WaterSettings.WaterType.Pool, "Assets/Art/water/PoolWater.glb", 0.6f);
            Ladder(pool + new Vector3(5.0f, 0, 1.2f), -90, 1.9f, 0.12f).name = "Pool ladder (deep end)";

            // Forest pond 1.7 m.
            var pond = new Vector3(14, 0, -8);
            WaterInteractionSetup.HoleIn(ground, pond, new Vector2(16, 16));
            var basin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/water/PondBasin.glb");
            if (basin != null) { var b = (GameObject)PrefabUtility.InstantiatePrefab(basin); b.transform.position = pond; WaterInteractionSetup.Solid(b); }
            else Box("Pond bed", pond + Vector3.down * 1.8f, new Vector3(16, 0.2f, 16), Hex("3A3226"), true);
            Walls(pond, new Vector2(16, 16), -2.4f, earth);
            WaterInteractionSetup.Water("Pond water", pond + Vector3.down * 0.15f, new Vector2(13, 13), WaterSettings.WaterType.Pond, "Assets/Art/water/PondWater.glb", 0.4f);

            // Lake: 2.25 m deep, a sloped sandy shore on the far side, and a dock.
            var lake = new Vector3(0, 0, 32);
            var lakeSize = new Vector2(30, 24);
            WaterInteractionSetup.HoleIn(ground, lake, lakeSize);
            Walls(lake, lakeSize, -2.4f, earth, skipNorth: true);
            var shoreStart = lake.z + 2; var shoreEnd = lake.z + lakeSize.y / 2;               // ramp from −2.4 m up to ground level
            var rise = 2.4f; var run = shoreEnd - shoreStart;
            var angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var ramp = Box("Lake shore slope", Vector3.zero, new Vector3(lakeSize.x, 0.3f, Mathf.Sqrt(rise * rise + run * run) + 0.4f), Hex("C8B68A"), true);
            ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0);
            ramp.transform.position = new Vector3(lake.x, -rise / 2, (shoreStart + shoreEnd) / 2) - ramp.transform.up * 0.15f;
            WaterInteractionSetup.Water("Lake water", lake + Vector3.down * 0.15f, lakeSize, WaterSettings.WaterType.Lake, "", 0);
            var dock = Box("Lake dock", new Vector3(lake.x + 11.5f, 0.2f, lake.z - 4), new Vector3(7, 0.15f, 2.4f), Hex("8A6A4A"), true);
            for (var x = -3f; x <= 3f; x += 2) foreach (var z in new[] { -1.1f, 1.1f })
                    Box("Dock post", dock.transform.position + new Vector3(x, -1.3f, z), new Vector3(0.14f, 2.6f, 0.14f), Hex("5E4630"), true);
            var dockExit = new GameObject("Dock steps").AddComponent<WaterExitPoint>();
            dockExit.Kind = WaterExitPoint.ExitKind.Dock;
            dockExit.transform.position = dock.transform.position + new Vector3(-3.5f, 0, 0);
            dockExit.Bottom = new GameObject("Bottom").transform; dockExit.Bottom.SetParent(dockExit.transform, false); dockExit.Bottom.localPosition = new Vector3(-0.75f, -0.35f, 0);
            dockExit.Top = new GameObject("Top").transform; dockExit.Top.SetParent(dockExit.transform, false); dockExit.Top.localPosition = new Vector3(0.7f, 0.08f, 0);

            // Ocean strip with a beach slope (existing ocean shader + Gerstner waves).
            var oceanMaterial = new Material(Shader.Find("ContextSpeak/Ocean") ?? Shader.Find("Universal Render Pipeline/Lit"));
            Directory.CreateDirectory(WaterInteractionSetup.Folder);
            Replace(oceanMaterial, WaterInteractionSetup.Folder + "/SwimTestOcean.mat");
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(ocean.GetComponent<Collider>());
            ocean.name = "Ocean surface";
            ocean.transform.position = new Vector3(0, OceanWaves.SeaLevel, -110);
            ocean.transform.localScale = new Vector3(16, 1, 6);
            ocean.GetComponent<Renderer>().sharedMaterial = oceanMaterial;
            var waves = ocean.AddComponent<OceanWaves>();
            waves.IslandCentre = new Vector3(0, 0, 60);
            waves.ShoreRadius = 120;
            var sand = Box("Beach sand slope", new Vector3(0, -1.46f, -92), new Vector3(160, 0.4f, 24), Hex("D9C9A0"), true);
            sand.transform.rotation = Quaternion.Euler(-6, 0, 0);
            Box("Sea bed", new Vector3(0, OceanWaves.SeaLevel - 6, -125), new Vector3(160, 0.4f, 40), Hex("BBA77E"), true);

            Label("SHALLOW (0.45 m)", shallow + new Vector3(0, 1.4f, -4.5f), 0, 0.45f, Color.white);
            Label("POOL + LADDER", pool + new Vector3(0, 1.4f, -5.5f), 0, 0.5f, Color.white);
            Label("FOREST POND", pond + new Vector3(0, 1.4f, -8.5f), 0, 0.5f, Color.white);
            Label("LAKE · SHORE · DOCK", lake + new Vector3(0, 1.4f, -12.5f), 0, 0.5f, Color.white);
            Label("OCEAN", new Vector3(0, 1.4f, -78), 0, 0.5f, Color.white);

            var (player, camera) = Player(pool + new Vector3(0, 0.05f, -7), 0);
            camera.farClipPlane = 600;
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
            WorldBuild.Configure(player, new Transform[0], "SwimTest");
            new GameObject("Systems").AddComponent<LifeUI>().LocationName = "Swimming test";
            var sky = SkyInstaller.Install();
            if (sky != null) { sky.SetTime(11f); EditorUtility.SetDirty(sky); }
            notes.AddRange(WaterInteractionSetup.Setup());
            var sea = waves.GetComponent<WaterBody>();
            if (sea != null) sea.Volume = new Bounds(new Vector3(0, OceanWaves.SeaLevel - 15, -110), new Vector3(160, 34, 60));   // only the ocean strip
            notes.AddRange(Setup(AssetDatabase.LoadAssetAtPath<SwimClipSet>(ClipSetFolder + "Academy Swim Clips.asset") == null));
            LifeBuild.Save(scene, TestScene);
            Debug.Log("Swimming test scene built: " + TestScene + "\n- " + string.Join("\n- ", notes) +
                      "\nAreas: shallow wading (0.45 m), pool 1–2 m with ladder, forest pond, lake with a sloped shore and dock, ocean with waves.\nAutomated: ContextSpeak > Checks > Swimming Play Mode Tests.");
        }

        /// Side walls of a basin dug into the test ground, down to `bottom`.
        private static void Walls(Vector3 c, Vector2 size, float bottom, Color colour, bool skipNorth = false)
        {
            var h = -bottom; var y = bottom / 2;
            Box("Basin wall", c + new Vector3(0, y, -size.y / 2 - 0.1f), new Vector3(size.x + 0.4f, h, 0.2f), colour, true);
            if (!skipNorth) Box("Basin wall", c + new Vector3(0, y, size.y / 2 + 0.1f), new Vector3(size.x + 0.4f, h, 0.2f), colour, true);
            Box("Basin wall", c + new Vector3(-size.x / 2 - 0.1f, y, 0), new Vector3(0.2f, h, size.y), colour, true);
            Box("Basin wall", c + new Vector3(size.x / 2 + 0.1f, y, 0), new Vector3(0.2f, h, size.y), colour, true);
        }
    }
}
