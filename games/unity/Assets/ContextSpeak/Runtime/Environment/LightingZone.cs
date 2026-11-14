using UnityEngine;

namespace ContextSpeak.Life
{
    /// An indoor (or sheltered outdoor) lighting zone: a box in world space. While the camera is
    /// inside, the eye adapts — interiors lit by daylight through windows open up a little by day,
    /// stay at their artificial level at night — and the sky's ambient fill is reduced, since only
    /// windows let it in. Transitions are gradual (EnvironmentManager.AdaptationRate).
    public sealed class LightingZone : MonoBehaviour
    {
        public enum Kind { Interior, Covered }
        public Kind Type = Kind.Interior;
        public Vector3 Size = new Vector3(10, 4, 10);
        [Tooltip("EV added at noon inside (daylight interiors are ~2–3 EV darker than outside; the eye compensates part of it).")]
        public float DaylightExposure = 0.6f;
        [Tooltip("EV added at night inside (lamps are on; keep 0 so interiors aren't unnaturally bright).")]
        public float NightExposure = 0;
        [Range(0, 1)] public float AmbientScale = 0.55f;
        [Tooltip("Optional local reflection probe so glass, floors and metal reflect the room, not the sky.")]
        public ReflectionProbe Probe;

        public float Volume => Size.x * Size.y * Size.z;

        private void OnEnable() => EnvironmentManager.Register(this);
        private void OnDisable() => EnvironmentManager.Unregister(this);

        public bool Contains(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return Mathf.Abs(local.x) <= Size.x / 2 && Mathf.Abs(local.z) <= Size.z / 2 && local.y >= 0 && local.y <= Size.y;
        }

        public float TargetExposure() => Mathf.Lerp(NightExposure, DaylightExposure, SkyState.Daylight) * (Type == Kind.Covered ? 0.4f : 1);

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 0.8f, 0.3f, 0.35f);
            Gizmos.DrawWireCube(new Vector3(0, Size.y / 2, 0), Size);
        }
    }
}
