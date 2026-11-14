using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A place to get out of the water with E: pool ladder, dock/jetty steps or a marked pool edge.
    /// Bottom = where the swimmer takes hold (in the water), Top = where they stand afterwards.
    /// Unmarked pool edges and banks also work (SwimmingController finds climbable edges itself);
    /// shores and beaches need nothing — swimming into shallow water simply turns into wading.
    public sealed class WaterExitPoint : MonoBehaviour
    {
        public enum ExitKind { Ladder, Dock, Edge }
        public static readonly List<WaterExitPoint> All = new List<WaterExitPoint>();
        public ExitKind Kind = ExitKind.Ladder;
        public Transform Bottom, Top;
        [Tooltip("How close (m, horizontally) the swimmer must be to use it.")] public float Radius = 1.4f;

        public Vector3 BottomPoint => Bottom != null ? Bottom.position : transform.position;
        public Vector3 TopPoint => Top != null ? Top.position : transform.position + transform.forward * 0.9f + Vector3.up * 0.5f;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public static WaterExitPoint Nearest(Vector3 p)
        {
            WaterExitPoint best = null;
            var bestDistance = float.MaxValue;
            foreach (var point in All)
            {
                if (point == null || !point.isActiveAndEnabled) continue;
                var d = point.BottomPoint - p;
                d.y = 0;
                var distance = d.magnitude;
                if (distance < point.Radius && distance < bestDistance) { best = point; bestDistance = distance; }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.6f, 0.8f);
            Gizmos.DrawWireSphere(BottomPoint, 0.2f);
            Gizmos.DrawWireSphere(TopPoint, 0.2f);
            Gizmos.DrawLine(BottomPoint, TopPoint);
        }
    }
}
