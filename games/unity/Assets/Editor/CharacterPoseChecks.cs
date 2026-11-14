using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    /// Measure actual skinned vertices, not renderer bounds, which may contain stale animation bounds.
    public static class CharacterPoseChecks
    {
        [Serializable] private sealed class Sample { public string model, role; public float time, bottom, height, width, footGap, footRise; }
        [Serializable] private sealed class Report { public bool passed; public Sample[] samples; public string[] errors; }

        [MenuItem("ContextSpeak/Checks/Character poses and grounding")]
        public static void Verify()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var samples = new List<Sample>();
            var errors = new List<string>();
            try
            {
                foreach (var spec in MeshyCharacters.Specs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + spec.id + ".prefab");
                    var root = Object.Instantiate(prefab);
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    try
                    {
                        var motion = root.GetComponentInChildren<AuthoredMotion>();
                        foreach (var role in motion != null ? new[] { "Idle", "Walk", "Run" } : new[] { "Static" })
                        {
                            var clip = motion != null ? motion.Find(role) : null;
                            if (motion != null && clip == null) continue;
                            foreach (var fraction in role == "Static" || role == "Idle" ? new[] { 0f } : new[] { 0f, .25f, .5f, .75f })
                            {
                                if (clip != null) clip.SampleAnimation(motion.Player.gameObject, clip.length * fraction);
                                var bounds = Measure(root);
                                var left = CharacterFacing.Bone(root.transform, "LeftFoot");
                                var right = CharacterFacing.Bone(root.transform, "RightFoot");
                                var sample = new Sample { model = spec.id, role = role, time = fraction,
                                    bottom = bounds.min.y, height = bounds.size.y, width = bounds.size.x,
                                    footGap = left != null && right != null ? Vector3.ProjectOnPlane(left.position - right.position, Vector3.up).magnitude : -1,
                                    footRise = left != null && right != null ? Mathf.Abs(left.position.y - right.position.y) : -1 };
                                samples.Add(sample);
                                if (role == "Static" || role == "Idle")
                                {
                                    if (Mathf.Abs(sample.bottom) > .04f || Mathf.Abs(sample.height - spec.height) > .1f)
                                        errors.Add(spec.id + "/" + role + " floor=" + sample.bottom + " height=" + sample.height);
                                    if (role == "Idle" && (sample.footRise > .05f || sample.footGap < .01f))
                                        errors.Add(spec.id + "/Idle is mid-stride: foot rise=" + sample.footRise + " gap=" + sample.footGap);
                                }
                                // Running may briefly leave the ground; idle is checked more strictly above.
                                else if (sample.bottom < -.08f || sample.bottom > (role == "Run" ? .16f : .12f) || sample.height < spec.height * .7f || sample.height > spec.height * 1.2f)
                                    errors.Add(spec.id + "/" + role + "@" + fraction + " floor=" + sample.bottom + " height=" + sample.height);
                            }
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            Directory.CreateDirectory("../../.local/character-repair");
            File.WriteAllText("../../.local/character-repair/poses.json", JsonUtility.ToJson(new Report { passed = errors.Count == 0, samples = samples.ToArray(), errors = errors.ToArray() }, true));
            if (errors.Count > 0) throw new Exception("Character pose checks failed:\n" + string.Join("\n", errors));
            Debug.Log("Character pose checks passed: " + samples.Count + " native vertex samples.");
        }

        internal static Bounds Measure(GameObject root)
        {
            var points = root.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh != null)
                .SelectMany(filter => filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToList();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, false); // TransformPoint below applies scale exactly once.
                    points.AddRange(mesh.vertices.Select(skin.transform.TransformPoint));
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            if (points.Count == 0) throw new Exception("Character has no mesh vertices: " + root.name);
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points.Skip(1)) bounds.Encapsulate(point);
            return bounds;
        }
    }
}
