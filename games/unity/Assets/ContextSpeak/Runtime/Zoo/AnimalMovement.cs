using UnityEngine;

namespace ContextSpeak.Life
{
    /// Moves an animal like an animal: it turns before it walks, speeds up and slows down gently, keeps
    /// its feet on the ground, walks around ponds and rocks, keeps a little distance from the others and
    /// never crosses the fence of its habitat.
    public sealed class AnimalMovement : MonoBehaviour
    {
        public AnimalHabitat Habitat;
        public float TurnSpeed = 60, Radius = 1, Acceleration = 0.8f;
        public float Speed { get; private set; }
        public bool Arrived { get; private set; } = true;
        public Vector3 Target { get; private set; }
        private float wanted;
        private Vector3? look;
        private float stuck;

        public void GoTo(Vector3 point, float speed)
        {
            Target = Habitat != null ? Habitat.Clamp(point) : point;
            wanted = speed;
            Arrived = false;
            look = null;
            stuck = 0;
        }

        public void Stop() { Arrived = true; wanted = 0; }
        public void Face(Vector3 point) { look = point; }

        public static float GroundY(Vector3 point, float fallback)
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (point.x < origin.x || point.z < origin.z || point.x > origin.x + size.x || point.z > origin.z + size.z) continue;
                return terrain.SampleHeight(point) + origin.y;
            }
            return Physics.Raycast(new Vector3(point.x, fallback + 20, point.z), Vector3.down, out var hit, 60, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
        }

        public void Tick(float dt)
        {
            var position = transform.position;
            var to = Target - position;
            to.y = 0;
            var distance = to.magnitude;
            if (!Arrived && distance < Mathf.Max(0.35f, Radius * 0.35f)) Arrived = true;
            var goal = Arrived ? 0 : wanted * Mathf.Clamp01(distance / (Radius * 1.5f) + 0.25f);

            var direction = Arrived ? Vector3.zero : to / Mathf.Max(distance, 0.001f);
            // Keep a body's width from the others (stronger when nearly touching).
            if (Habitat != null && !Arrived)
                foreach (var other in Habitat.Animals)
                {
                    if (other == null || other.transform == transform || other.Movement == null) continue;
                    var away = position - other.transform.position;
                    away.y = 0;
                    var gap = away.magnitude - Radius - other.Movement.Radius;
                    if (gap < 1.5f && away.sqrMagnitude > 0.0001f) direction += away.normalized * Mathf.Clamp01(1 - gap / 1.5f) * 1.4f;
                }
            var facing = direction.sqrMagnitude > 0.001f ? direction.normalized : look.HasValue ? Vector3.ProjectOnPlane(look.Value - position, Vector3.up).normalized : Vector3.zero;
            var aligned = 1f;
            if (facing.sqrMagnitude > 0.001f)
            {
                var rotation = Quaternion.LookRotation(facing, Vector3.up);
                var yaw = Quaternion.RotateTowards(Quaternion.Euler(0, transform.eulerAngles.y, 0), rotation, TurnSpeed * dt * (Speed < 0.2f ? 1.4f : 1));
                transform.rotation = Quaternion.Slerp(transform.rotation, yaw, 1);
                aligned = Mathf.InverseLerp(75, 15, Quaternion.Angle(yaw, rotation));      // turn first, then walk
            }
            Speed = Mathf.MoveTowards(Speed, goal * aligned, Acceleration * dt * (goal < Speed ? 2 : 1));
            if (Speed > 0.001f)
            {
                var next = position + transform.forward * Speed * dt;
                if (Habitat != null) next = Habitat.Clamp(next);
                var moved = (next - position).magnitude;
                stuck = moved < Speed * dt * 0.3f ? stuck + dt : 0;
                if (stuck > 3) Arrived = true;        // blocked (pond edge, fence): give up, the brain picks something else
                position = next;
            }
            position.y = Mathf.Lerp(position.y, GroundY(position, position.y), Mathf.Clamp01(dt * 8));
            transform.position = position;
        }
    }
}
