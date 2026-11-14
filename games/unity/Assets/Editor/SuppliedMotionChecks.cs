using System;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class SuppliedMotionChecks
    {
        public static void Verify()
        {
            var type = typeof(CafeClient).Assembly.GetType("ContextSpeak.Cafe.AuthoredMotion");
            if (type == null) throw new Exception("Supplied motions are not connected to a runtime player.");
            foreach (var name in MotionAssets.Names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/Motion/" + name + ".prefab");
                if (prefab == null) throw new Exception("Missing animated gameplay prefab: " + name);
                var root = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var motion = root.GetComponent(type);
                    var animation = root.GetComponentInChildren<Animation>();
                    if (motion == null || animation == null) throw new Exception("Native clip playback is absent: " + name);
                    var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (skin == null || skin.bones.Length < 20) throw new Exception("Supplied skin/bones were lost: " + name);
                    foreach (var role in MotionAssets.Roles(name))
                    {
                        var clip = ((AuthoredMotion)motion).Find(role);
                        if (clip == null || !clip.legacy || clip.length <= 0.1f) throw new Exception("Missing supplied clip: " + name + "/" + role);
                        if (AnimationUtility.GetCurveBindings(clip).Any(binding => animation.transform.Find(binding.path) == null))
                            throw new Exception("Clip has an unresolved bone path: " + name + "/" + role);
                        var a = new Mesh(); var b = new Mesh();
                        try
                        {
                            clip.SampleAnimation(animation.gameObject, clip.length * 0.1f);
                            skin.BakeMesh(a, true);
                            clip.SampleAnimation(animation.gameObject, clip.length * 0.6f);
                            skin.BakeMesh(b, true);
                            if (!a.vertices.Zip(b.vertices, (first, second) => Vector3.Distance(skin.transform.TransformPoint(first), skin.transform.TransformPoint(second))).Any(distance => distance > 0.005f))
                                throw new Exception("Clip does not deform actual vertices: " + name + "/" + role);
                            if (Vector3.Distance(root.transform.position, Vector3.zero) > 0.001f)
                                throw new Exception("A motion clip moved the gameplay root: " + name);
                            if (name == "Academy" && (role == "Walk" || role == "Run"))
                            {
                                var points = b.vertices.Select(skin.transform.TransformPoint).ToArray();
                                var bottom = points.Min(point => point.y);
                                var height = points.Max(point => point.y) - bottom;
                                if (bottom < -0.2f || bottom > 0.3f || height < 1.3f || height > 2.1f)
                                    throw new Exception("Female motion distorted scale/floor: " + role + " bottom " + bottom + " height " + height);
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Debug.Log("All supplied motion clips deform their gameplay meshes");
        }
    }
}
