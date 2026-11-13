using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Shared bevelled mesh: broad flat faces and softly rounded edges for furniture and cloth details.
    public static class CozyGeometry
    {
        private static Mesh rounded;
        public static Mesh RoundedBox()
        {
            if (rounded != null) return rounded;
            const string path = "Assets/Generated/CozyRoundedBox.asset";
            rounded = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (rounded != null) return rounded;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            const int grid = 8;
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(normal, u);
                var first = vertices.Count;
                for (var y = 0; y <= grid; y++)
                    for (var x = 0; x <= grid; x++)
                    {
                        var point = normal * 0.5f + u * (x / (float)grid - 0.5f) + v * (y / (float)grid - 0.5f);
                        var inner = new Vector3(Mathf.Clamp(point.x, -0.4f, 0.4f), Mathf.Clamp(point.y, -0.4f, 0.4f), Mathf.Clamp(point.z, -0.4f, 0.4f));
                        var outward = (point - inner).normalized;
                        vertices.Add(inner + outward * 0.1f);
                        normals.Add(outward);
                        if (x == grid || y == grid) continue;
                        var a = first + y * (grid + 1) + x;
                        indices.AddRange(new[] { a, a + 1, a + grid + 1, a + 1, a + grid + 2, a + grid + 1 });
                    }
            }
            rounded = new Mesh { name = "Soft bevelled box" };
            rounded.SetVertices(vertices);
            rounded.SetNormals(normals);
            rounded.SetTriangles(indices, 0);
            rounded.RecalculateBounds();
            AssetDatabase.CreateAsset(rounded, path);
            return rounded;
        }
    }
}
