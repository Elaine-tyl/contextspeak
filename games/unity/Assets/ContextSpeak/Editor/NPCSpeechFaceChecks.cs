using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Reports, for every character in the open scene, how its face will move when it speaks (NPCSpeechFace):
    /// mouth blend shape, jaw bone, or only the head-emphasis fallback. Also self-tests the blend-shape picker.
    public static class NPCSpeechFaceChecks
    {
        [MenuItem("ContextSpeak/Checks/NPC Talking Faces")]
        public static void Run()
        {
            var lines = new List<string>();
            var ok = SelfTest(lines);
            var roots = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Select(a => a.transform)
                .Concat(Object.FindObjectsByType<NPCAnimationController>(FindObjectsSortMode.None).Select(a => a.transform))
                .Where(t => t.GetComponentInParent<ContextSpeak.Cafe.CafePlayer>() == null).Distinct()
                .Where(t => t.parent == null || (t.parent.GetComponentInParent<Animator>() == null && t.parent.GetComponentInParent<NPCAnimationController>() == null)).ToList();
            var counts = new Dictionary<string, int>();
            foreach (var root in roots)
            {
                var temp = root.GetComponent<NPCSpeechFace>();
                var added = temp == null;
                if (added) temp = root.gameObject.AddComponent<NPCSpeechFace>();
                temp.Scan();
                counts[temp.Method] = counts.TryGetValue(temp.Method, out var c) ? c + 1 : 1;
                lines.Add("  " + root.name + ": " + temp.Method);
                if (added) Object.DestroyImmediate(temp);
            }
            var summary = "NPC talking faces · " + roots.Count + " characters in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " · "
                + string.Join(", ", counts.Select(kv => kv.Value + " " + kv.Key)) + (ok ? " · picker self-test passed" : " · picker self-test FAILED");
            Debug.Log(summary + "\n" + string.Join("\n", lines.Take(80)));
        }

        private static bool SelfTest(List<string> lines)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            var delta = new Vector3[3];
            foreach (var name in new[] { "Wolf3D_Head.browInnerUp", "viseme_PP", "viseme_aa", "eyeBlinkLeft", "mouthSmileLeft", "A_test" })
                mesh.AddBlendShapeFrame(name, 100, delta, delta, delta);
            var open = NPCSpeechFace.Pick(mesh, NPCSpeechFace.OpenNames);
            var blink = NPCSpeechFace.Pick(mesh, NPCSpeechFace.BlinkNames);
            var smile = NPCSpeechFace.Pick(mesh, NPCSpeechFace.SmileNames);
            Object.DestroyImmediate(mesh);
            var pass = open == 2 && blink == 3 && smile == 4;
            if (!pass) lines.Add("  self-test: open=" + open + " blink=" + blink + " smile=" + smile + " (expected 2, 3, 4)");
            return pass;
        }
    }
}
