using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// An enclosure: an ellipse on the ground that its animals never leave, with the places they use
    /// (water, feeding, rest/shade, browse trees) and areas they walk around (the pond, rocks, trees).
    public sealed class AnimalHabitat : MonoBehaviour
    {
        public string Title = "Habitat";
        public Vector2 Radii = new Vector2(30, 20);      // x / z half-sizes in metres
        public float Margin = 4;                          // animals keep this far inside the fence
        public Transform[] Water = new Transform[0], Food = new Transform[0], Rest = new Transform[0], Browse = new Transform[0];
        public Vector4[] Avoid = new Vector4[0];          // x, z, radius, 0 — ponds, rocks, tree trunks
        public readonly List<AnimalController> Animals = new List<AnimalController>();

        public static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);

        public static float Ellipse(Vector2 centre, Vector2 radii, Vector2 point)
        {
            var d = point - centre;
            return (d.x * d.x) / (radii.x * radii.x) + (d.y * d.y) / (radii.y * radii.y);
        }

        public bool Contains(Vector3 point, float margin)
        {
            var radii = new Vector2(Mathf.Max(1, Radii.x - margin), Mathf.Max(1, Radii.y - margin));
            return Ellipse(Flat(transform.position), radii, Flat(point)) <= 1;
        }

        /// The nearest point that is inside the fence (minus the margin) and outside the avoided areas.
        public Vector3 Clamp(Vector3 point)
        {
            var centre = Flat(transform.position);
            var radii = new Vector2(Mathf.Max(1, Radii.x - Margin), Mathf.Max(1, Radii.y - Margin));
            var p = Flat(point);
            var e = Ellipse(centre, radii, p);
            if (e > 1) p = centre + (p - centre) / Mathf.Sqrt(e) * 0.98f;
            foreach (var area in Avoid)
            {
                var c = new Vector2(area.x, area.y);
                var d = p - c;
                if (d.magnitude < area.z) p = c + (d.sqrMagnitude < 0.0001f ? Vector2.right : d.normalized) * (area.z + 0.2f);
            }
            return new Vector3(p.x, point.y, p.y);
        }

        public Vector3 RandomPoint(System.Random random, Vector3 around, float range)
        {
            var angle = (float)random.NextDouble() * Mathf.PI * 2;
            var distance = Mathf.Sqrt((float)random.NextDouble()) * range;
            return Clamp(around + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance);
        }

        /// Where to stand to use a place (a ring around it, so a herd does not pile onto one point).
        public Vector3 Slot(Transform place, int index, float radius)
        {
            var angle = index * 2.399963f;      // golden angle: evenly spread slots
            var outward = place.position - transform.position;
            outward.y = 0;
            var facing = outward.sqrMagnitude > 0.01f ? Mathf.Atan2(outward.z, outward.x) : 0;
            var a = facing + Mathf.PI + Mathf.Sin(angle) * 1.2f;     // the habitat side of the place
            return Clamp(place.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius);
        }

        public Transform Nearest(Transform[] places, Vector3 from)
        {
            Transform best = null;
            var distance = float.MaxValue;
            foreach (var place in places)
            {
                if (place == null) continue;
                var d = (place.position - from).sqrMagnitude;
                if (d < distance) { distance = d; best = place; }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.8f);
            var previous = Vector3.zero;
            for (var i = 0; i <= 64; i++)
            {
                var a = i / 64f * Mathf.PI * 2;
                var point = transform.position + new Vector3(Mathf.Cos(a) * Radii.x, 0.3f, Mathf.Sin(a) * Radii.y);
                if (i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
