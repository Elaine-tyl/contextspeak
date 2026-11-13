using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class EnvironmentUpgradeChecks
    {
        public static void Verify()
        {
            var errors = new List<string>();
            void Check(bool value, string message) { if (!value) errors.Add(message); }
            var girl = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + "Academy.prefab");
            var motion = girl.GetComponent<AuthoredMotion>();
            Check(motion.Walk != null && motion.Run != null, "Female protagonist needs separate supplied walk and run clips.");
            Check(motion.Locomotion(PlayerAnimState.Walk) == motion.Walk && motion.Locomotion(PlayerAnimState.Run) == motion.Run
                && motion.Locomotion(PlayerAnimState.Sprint) == motion.Run && motion.Locomotion(PlayerAnimState.Idle) == motion.Idle,
                "Walking, running, sprinting and idle must select their own motion.");
            var selection = typeof(AuthoredMotion).GetMethod("Locomotion", new[] { typeof(PlayerAnimState), typeof(float) });
            Check(selection != null, "Crouch motion must distinguish stationary and moving input.");
            if (selection != null)
            {
                Check((AnimationClip)selection.Invoke(motion, new object[] { PlayerAnimState.Crouch, 0f }) == motion.Idle,
                    "Stationary crouching cannot walk in place.");
                Check((AnimationClip)selection.Invoke(motion, new object[] { PlayerAnimState.Crouch, 1f }) == motion.Walk,
                    "Moving crouch must still animate.");
            }
            foreach (var spec in MeshyCharacters.Specs.Where(spec => spec.id != "WhispersPiano" && spec.id != "Rider"))
            {
                var body = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + spec.id + ".prefab");
                var points = body.GetComponentsInChildren<MeshFilter>().SelectMany(filter => filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToList();
                foreach (var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh();
                    skin.BakeMesh(baked, body.GetComponent<AuthoredMotion>() != null);
                    points.AddRange(baked.vertices.Select(skin.transform.TransformPoint));
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var point in points.Skip(1)) bounds.Encapsulate(point);
                Check(Mathf.Abs(bounds.size.y - 1.7f) < 0.06f, "Inconsistent standing height: " + spec.id + " " + bounds.size.y);
            }
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var player = UnityEngine.Object.FindFirstObjectByType<CafePlayer>();
            var speedSelection = typeof(CafePlayer).GetMethod("InputSpeed");
            Check(speedSelection != null, "Walk modifiers must retain walking under either default gait.");
            if (speedSelection != null)
            {
                var oldGait = player.DefaultGait;
                foreach (var gait in new[] { Gait.Walk, Gait.Run })
                {
                    player.DefaultGait = gait;
                    Check((float)speedSelection.Invoke(player, new object[] { false, false, true }) == player.WalkSpeed,
                        "Ctrl/Caps requested walking but selected running.");
                    Check((float)speedSelection.Invoke(player, new object[] { false, true, false }) == player.SprintSpeed,
                        "Shift must request running/sprinting.");
                }
                player.DefaultGait = oldGait;
            }
            foreach (var id in new[] { "World9209b63ea9", "Worldfcb6bfff5e" })
            {
                var piano = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + id + ".prefab");
                var renderers = piano.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(bounds.size.y - 1.35f) < 0.05f, "Piano/composite height must fit a 1.7m person: " + bounds.size.y);
            }
            Check(GameObject.Find("Applied downloaded environment") != null, "Downloaded art must replace the environment, not only sit in exhibit rows.");
            var trees = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            Check(trees.Length >= 40 && trees.All(tree => tree.GetComponentsInChildren<MeshFilter>()
                .All(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh).Contains("/WorldAssets/"))), "Forest trees still contain primitive placeholder geometry.");
            var surfaces = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name.Contains("ground") && (renderer.name.Contains("Meadow") || renderer.name.Contains("village"))).ToArray();
            Check(surfaces.Length == 8 && surfaces.All(renderer => renderer.sharedMaterial.name == "Downloaded meadow surface"),
                "Meadow and village surfaces must use downloaded grass material.");
            Check(surfaces.All(renderer => renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"
                && renderer.sharedMaterial.mainTexture != null && renderer.sharedMaterial.GetFloat("_AlphaClip") == 0),
                "Grass cutouts must not make holes in the solid ground.");
            var applied = GameObject.Find("Applied downloaded environment");
            if (applied != null)
            {
                var roads = applied.transform.Cast<Transform>().Where(item => item.name == "Downloaded asphalt road").ToArray();
                Check(roads.Length == 7 && roads.All(road => road.GetComponentsInChildren<MeshFilter>()
                    .All(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh).Contains("World65b03a2587.glb"))),
                    "All seven road visuals must use the downloaded asphalt mesh.");
                var meshes = applied.GetComponentsInChildren<MeshFilter>();
                var triangles = meshes.Sum(mesh => (long)mesh.sharedMesh.triangles.Length / 3);
                Check(triangles <= 2000000, "Contextual environment exceeds its separate 2m pre-cull triangle cap: " + triangles);
                var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
                var oldPosition = camera.transform.position;
                var oldRotation = camera.transform.rotation;
                foreach (var point in new[] { new Vector3(0, 4.8f, 16), new Vector3(0, 3, -40), new Vector3(18, 4, 33) })
                {
                    camera.transform.position = point;
                    camera.transform.LookAt(point + new Vector3(0, -1, point.z < 0 ? 20 : -20));
                    var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                    var visible = meshes.Where(mesh => GeometryUtility.TestPlanesAABB(planes, mesh.GetComponent<Renderer>().bounds))
                        .Sum(mesh => (long)mesh.sharedMesh.triangles.Length / 3);
                    Check(visible <= 1000000, "Contextual draw estimate exceeds 1m at " + point + ": " + visible);
                    Debug.Log("Contextual pre-LOD draw estimate at " + point + ": " + visible);
                }
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
            Check(UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name == "Village walking path").All(renderer => !renderer.enabled), "Old road surfaces must be hidden.");
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            Debug.Log("Environment replacement, female locomotion and common character scale verified");
        }
    }
}
