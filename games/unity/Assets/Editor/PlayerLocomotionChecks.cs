using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    [InitializeOnLoad]
    public static class PlayerLocomotionChecks
    {
        private const string Folder = "../../.local/locomotion/";
        [Serializable] private sealed class ClipFrame { public string clip; public float time, hipsY, footY; }
        [Serializable] private sealed class Result { public bool passed; public string[] errors; public ClipFrame[] frames; }
        static PlayerLocomotionChecks() => EditorApplication.update += Tick;
        private static void Tick()
        {
            if (!File.Exists(Folder + "check.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "check.request");
            Verify();
        }
        private static Bounds MeshBounds(GameObject model)
        {
            var bounds = new Bounds(); var first = true;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    var point = skin.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
                Object.DestroyImmediate(mesh);
            }
            return bounds;
        }
        [MenuItem("ContextSpeak/Checks/Player locomotion")]
        public static void Verify()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var errors = new List<string>();
            var frames = new List<ClipFrame>();
            try
            {
                var actor = new GameObject("Locomotion check");
                var controller = actor.AddComponent<CafePlayer>();
                var visual = new GameObject("Visual"); visual.transform.SetParent(actor.transform, false);
                controller.Model = visual.transform;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + "Academy.prefab");
                var model = Object.Instantiate(prefab, visual.transform);
                var motion = model.GetComponent<AuthoredMotion>();
                var animate = typeof(CafePlayer).GetMethod("Animate", BindingFlags.Instance | BindingFlags.NonPublic);
                typeof(CafePlayer).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, actor.GetComponent<CharacterController>());
                var horizontal = typeof(CafePlayer).GetField("horizontal", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    horizontal.SetValue(controller, direction * controller.WalkSpeed);
                    animate.Invoke(controller, new object[] { 1f, direction });
                    foreach (var clip in new[] { motion.Walk, motion.Run })
                    {
                        clip.SampleAnimation(motion.Player.gameObject, .2f);
                        var left = CharacterFacing.Bone(model.transform, "LeftShoulder");
                        var right = CharacterFacing.Bone(model.transform, "RightShoulder");
                        var heading = Vector3.Cross(right.position - left.position, Vector3.up).normalized;
                        if (Vector3.Dot(heading, direction) < .8f) errors.Add(clip.name + " faces away from travel: " + direction + " dot=" + Vector3.Dot(heading, direction));
                    }
                }
                horizontal.SetValue(controller, Vector3.forward * controller.WalkSpeed);
                typeof(CafePlayer).GetField("squash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, .5f);
                animate.Invoke(controller, new object[] { .01f, Vector3.forward });
                if (Vector3.Distance(visual.transform.localScale, Vector3.one) > .001f || Mathf.Abs(visual.transform.localPosition.y) > .001f)
                    errors.Add("Authored skeleton receives procedural bob/squash.");
                // Reversing input must not rotate the body away from its remaining momentum.
                visual.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                horizontal.SetValue(controller, Vector3.forward * controller.WalkSpeed);
                animate.Invoke(controller, new object[] { 1f, Vector3.back });
                if (Vector3.Dot(visual.transform.forward, Vector3.forward) < .8f)
                    errors.Add("Reversal faces input rather than current travel.");
                controller.Activity = PlayerAnimState.Sit;
                animate.Invoke(controller, new object[] { 1f, Vector3.zero });
                if (Mathf.Abs(visual.transform.localPosition.y + .42f) > .001f)
                    errors.Add("Seating lost its rigid visual offset.");
                controller.Activity = PlayerAnimState.Idle;
                visual.transform.localPosition = Vector3.zero;
                var crouch = motion.Find("Crouch");
                if (crouch == null) errors.Add("Authored rig has no crouch pose for its shortened collider.");
                else
                {
                    crouch.SampleAnimation(motion.Player.gameObject, 0);
                    var head = CharacterFacing.Bone(model.transform, "Head");
                    var hips = CharacterFacing.Bone(model.transform, "Hips");
                    if (head.position.y <= hips.position.y + .2f) errors.Add("Crouch selected a roll/inverted torso rather than an upright squat.");
                    var leftFoot = CharacterFacing.Bone(model.transform, "LeftFoot");
                    var rightFoot = CharacterFacing.Bone(model.transform, "RightFoot");
                    if (Mathf.Abs(leftFoot.position.y - rightFoot.position.y) > .08f
                        || Mathf.Max(leftFoot.position.y, rightFoot.position.y) >= hips.position.y - .1f)
                        errors.Add("Crouch must keep both feet planted below the hips.");
                    var bounds = MeshBounds(model);
                    if (bounds.size.y > controller.CrouchHeight + .1f || bounds.min.y < -.1f || bounds.min.y > .12f)
                        errors.Add("Crouch mesh does not fit the shortened collider: " + bounds);
                }
                var jump = motion.Find("Jump");
                if (jump == null) errors.Add("No in-place jump derived from the supplied leap.");
                if (motion.Locomotion(PlayerAnimState.Jump) == motion.Idle || motion.Locomotion(PlayerAnimState.Fall) == motion.Idle)
                    errors.Add("Jump/fall still selects Idle instead of the supplied jump take.");
                if (jump != null)
                {
                    motion.Idle.SampleAnimation(motion.Player.gameObject, 0);
                    var hips = CharacterFacing.Bone(model.transform, "Hips");
                    var idleHipY = hips.localPosition.y;
                    var rootPosition = actor.transform.position;
                    foreach (var phase in new[] { 0f, .25f, .5f, .75f, 1f, 2f })
                    {
                        motion.SampleAirborne(phase);
                        var head = CharacterFacing.Bone(model.transform, "Head");
                        if (head.position.y <= hips.position.y) errors.Add("Ordinary jump selected an inverted pose.");
                        if (Mathf.Abs(hips.localPosition.y - idleHipY) > .001f)
                            errors.Add("Jump translates the skeleton vertically as well as physics: " + phase);
                        if (actor.transform.position != rootPosition) errors.Add("Animation moved the physics root.");
                    }
                    typeof(AuthoredMotion).GetMethod("Play", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(motion, new object[] { motion.Idle });
                    if (!motion.Player.enabled || motion.CurrentClip != motion.Idle.name)
                        errors.Add("Landing does not resume normal playback.");
                    var vertical = typeof(CafePlayer).GetField("vertical", BindingFlags.Instance | BindingFlags.NonPublic);
                    var impulse = Mathf.Sqrt(controller.JumpHeight * -2f * controller.Gravity);
                    foreach (var pair in new[] { (impulse, 0f), (0f, .5f), (-impulse, 1f), (-impulse * 2, 1f) })
                    {
                        vertical.SetValue(controller, pair.Item1);
                        if (Mathf.Abs(controller.AirProgress - pair.Item2) > .001f) errors.Add("Jump phase disagrees with gravity.");
                    }
                }
                foreach (var role in new[] { "Walk", "Run", "Run_and_Jump", "Run_and_Leap", "Back_Jump", "Jump" })
                {
                    var clip = motion.Find(role);
                    if (clip == null) continue;
                    for (var i = 0; i <= 16; i++)
                    {
                        var t = clip.length * i / 16f;
                        clip.SampleAnimation(motion.Player.gameObject, t);
                        var hips = CharacterFacing.Bone(model.transform, "Hips");
                        var left = CharacterFacing.Bone(model.transform, "LeftFoot");
                        var right = CharacterFacing.Bone(model.transform, "RightFoot");
                        frames.Add(new ClipFrame { clip = role, time = t, hipsY = hips.position.y, footY = Mathf.Min(left.position.y, right.position.y) });
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "checks.json", JsonUtility.ToJson(new Result { passed = errors.Count == 0, errors = errors.ToArray(), frames = frames.ToArray() }, true));
            if (errors.Count > 0) throw new Exception("Player locomotion checks failed: " + string.Join("; ", errors));
            Debug.Log("Player locomotion checks: " + (errors.Count == 0 ? "passed" : string.Join("; ", errors)));
        }
    }
}
