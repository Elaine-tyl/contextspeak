using UnityEngine;

namespace ContextSpeak.City
{
    /// A car the player can get into and drive: W/S accelerate and brake (and reverse), A/D steer.
    /// Simple arcade handling at real-world speeds; it stops against walls, kerbs and other vehicles,
    /// and follows the ground. Traffic sees it through its collider like any other car.
    public sealed class CityDrivable : MonoBehaviour
    {
        public string Label = "car";
        public float MaxSpeed = 16f;      // ~58 km/h, a city speed limit
        public float Reverse = 4.5f, Accel = 5f, Brake = 11f, SteerRate = 70f;
        public Vector3 SeatLocal = new Vector3(-0.38f, 0.35f, 0.1f);
        public Vector3 ExitLocal = new Vector3(-2.3f, 0.2f, 0.1f);
        public float Speed { get; private set; }
        private BoxCollider box;

        private void Awake() { box = GetComponent<BoxCollider>(); }

        public void Drive(float throttle, float steer, float dt)
        {
            if (throttle > 0.05f)
                Speed = Speed < -0.2f ? Mathf.MoveTowards(Speed, 0, Brake * dt) : Mathf.MoveTowards(Speed, MaxSpeed * throttle, Accel * dt);
            else if (throttle < -0.05f)
                Speed = Speed > 0.2f ? Mathf.MoveTowards(Speed, 0, Brake * dt) : Mathf.MoveTowards(Speed, -Reverse, Accel * dt);
            else
                Speed = Mathf.MoveTowards(Speed, 0, 2.5f * dt);

            // Steering scales with speed, and reverses when backing up, like a real car.
            var turn = steer * SteerRate * Mathf.Clamp(Speed / 5f, -1f, 1f) * dt;
            transform.rotation = Quaternion.Euler(0, turn, 0) * transform.rotation;

            var move = transform.forward * Speed * dt;
            if (box != null && move.sqrMagnitude > 0.000001f)
            {
                var centre = transform.TransformPoint(box.center) + Vector3.up * 0.2f;
                var half = new Vector3(box.size.x * 0.47f, box.size.y * 0.3f, box.size.z * 0.47f);
                foreach (var hit in Physics.BoxCastAll(centre, half, move.normalized, transform.rotation, move.magnitude + 0.08f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(transform) || hit.collider is CharacterController) continue;
                    if (hit.distance <= 0 && hit.point == Vector3.zero) continue;   // already overlapping: don't get stuck
                    Speed = 0;
                    move = Vector3.zero;
                    break;
                }
            }
            transform.position += move;

            // Follow the road surface (and gentle ramps).
            var best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !(hit.collider is CharacterController) && hit.point.y > best) best = hit.point.y;
            if (!float.IsNegativeInfinity(best))
            {
                var position = transform.position;
                position.y = Mathf.Lerp(position.y, best + 0.01f, dt * 10f);
                transform.position = position;
            }
        }

        public void Stop() { Speed = 0; }
    }
}
