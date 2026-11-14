using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A road vehicle the player can drive (taxi, van, SUV, airport tug, fuel truck…). Arcade handling at
    /// real-world speeds, kinematic like the rest of the game's traffic (no Rigidbody fighting the
    /// scene): bicycle-model steering (turn radius from wheelbase and steering angle, less lock at
    /// speed), acceleration that tapers toward top speed, brakes, handbrake, coasting; it follows and
    /// tilts with the ground, stops for people, and glances off walls instead of passing through them.
    /// Wheels (child names containing "wheel") spin and the front ones steer; engine and horn sounds
    /// are synthesised; headlights come on at night. Works on any car model: the front is found from
    /// "Headlight" materials (or "Taillight" for the back), otherwise the long axis.
    [DisallowMultipleComponent]
    public sealed class DrivableVehicle : MonoBehaviour
    {
        public enum VehicleKind { Car, Van, Utility, Truck }

        public string Label = "car";
        public VehicleKind Kind = VehicleKind.Car;
        [Header("Handling")]
        [Tooltip("Top speed (m/s). 16 m/s ≈ 58 km/h, a city limit.")] public float MaxSpeed = 16;
        public float ReverseSpeed = 5;
        [Tooltip("m/s² from standstill (tapers toward top speed).")] public float Acceleration = 4.5f;
        public float BrakeDeceleration = 10;
        [Tooltip("Rolling + air resistance when off the pedals (m/s²).")] public float Coast = 1.6f;
        [Tooltip("Front wheel lock (°) at low speed.")] public float MaxSteer = 34;
        [Tooltip("Steering speed (°/s).")] public float SteerSpeed = 140;
        [Tooltip("Right-hand drive (driver on the right, as in Malaysia / Australia).")] public bool RightHandDrive = true;
        [Header("Measured (auto when zero)")]
        public Vector3 SeatLocal;
        public Vector3 ExitLocal;
        public float Wheelbase;

        public float Speed { get; private set; }
        public float SteerAngle { get; private set; }
        public bool Occupied { get; private set; }
        public bool HeadlightsOn { get; private set; }
        /// Unit vector (vehicle-local XZ) pointing out of the bonnet.
        public Vector3 NoseLocal { get; private set; } = Vector3.forward;
        public Vector3 Forward => transform.TransformDirection(NoseLocal);
        public Vector3 Right => Vector3.Cross(Vector3.up, Forward).normalized;
        public Bounds LocalBounds => local;
        public float Length => Mathf.Abs(Vector3.Dot(local.size, new Vector3(Mathf.Abs(NoseLocal.x), 0, Mathf.Abs(NoseLocal.z))));
        public float Width => Mathf.Abs(Vector3.Dot(local.size, new Vector3(Mathf.Abs(NoseLocal.z), 0, Mathf.Abs(NoseLocal.x))));
        public float Height => local.size.y;

        public static readonly List<DrivableVehicle> All = new List<DrivableVehicle>();

        private Bounds local;
        private readonly List<Collider> own = new List<Collider>();
        private readonly List<(Transform wheel, Quaternion rest, bool front, float radius)> wheels = new List<(Transform, Quaternion, bool, float)>();
        private readonly List<Behaviour> autopilots = new List<Behaviour>();
        private AudioSource engine, oneShots;
        private readonly List<Light> lamps = new List<Light>();
        private float spin, bumpCooldown, verticalSpeed;
        private bool measured;

        /// Measured speed while not driven by the player (AI traffic) — you can only get in when it is stopped.
        public float IdleSpeed { get; private set; }
        private Vector3 lastPosition;

        private void Update()
        {
            if (Occupied) { IdleSpeed = 0; lastPosition = transform.position; return; }
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            IdleSpeed = Mathf.Lerp(IdleSpeed, (transform.position - lastPosition).magnitude / dt, 0.3f);
            lastPosition = transform.position;
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); lastPosition = transform.position; }
        private void OnDisable() { All.Remove(this); }

        private void Awake() => Measure();

        /// Bounds, front direction, wheels, colliders, seat and exit — all from the model itself.
        public void Measure()
        {
            if (measured) return;
            measured = true;
            var renderers = GetComponentsInChildren<Renderer>();
            var first = true;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = transform.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (first) { local = new Bounds(corner, Vector3.zero); first = false; } else local.Encapsulate(corner);
                }
            }
            if (first) local = new Bounds(Vector3.up * 0.75f, new Vector3(1.8f, 1.5f, 4.4f));

            // Front: headlights (or tail lights) when the model marks them; otherwise the long axis.
            var alongZ = local.size.z >= local.size.x;
            var axis = alongZ ? Vector3.forward : Vector3.right;
            float? front = null;
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var n = m.name.ToLowerInvariant();
                    var c = transform.InverseTransformPoint(r.bounds.center) - local.center;
                    var along = Vector3.Dot(c, axis);
                    if (n.StartsWith("headlight")) front = (front ?? 0) + along;
                    else if (n.StartsWith("taillight") || n.StartsWith("brakelight")) front = (front ?? 0) - along;
                }
            if (front.HasValue && front.Value < 0) axis = -axis;
            NoseLocal = axis;

            foreach (var c in GetComponentsInChildren<Collider>()) if (!c.isTrigger) own.Add(c);
            if (own.Count == 0)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = local.size;
                own.Add(box);
            }
            if (Wheelbase <= 0) Wheelbase = Mathf.Max(1.6f, Length * 0.6f);
            var side = Vector3.Cross(Vector3.up, NoseLocal);
            var seatSide = RightHandDrive ? 1f : -1f;
            if (SeatLocal == Vector3.zero)
                SeatLocal = new Vector3(local.center.x, local.min.y, local.center.z) + side * (Width * 0.22f * seatSide) + Vector3.up * Mathf.Clamp(Height * 0.3f, 0.35f, 0.9f) - NoseLocal * (Length * 0.04f);
            if (ExitLocal == Vector3.zero)
                ExitLocal = new Vector3(local.center.x, local.min.y + 0.05f, local.center.z) + side * ((Width * 0.5f + 0.75f) * seatSide) - NoseLocal * (Length * 0.04f);

            foreach (var t in GetComponentsInChildren<Transform>())
            {
                var n = t.name.ToLowerInvariant();
                if (!n.Contains("wheel") || n.Contains("steering") || n.Contains("spare")) continue;
                var r = t.GetComponent<Renderer>();
                var radius = r != null ? Mathf.Max(0.2f, r.bounds.extents.y) : 0.35f;
                var isFront = Vector3.Dot(transform.InverseTransformPoint(t.position) - local.center, NoseLocal) > 0;
                wheels.Add((t, t.localRotation, isFront, radius));
            }
            // AI drivers (airport traffic, city traffic) are suspended while the player drives.
            foreach (var b in GetComponents<Behaviour>())
            {
                var type = b.GetType().Name;
                if (b != this && (type == "PathMover" || type == "CityCar" || type == "LivingCar" || type.Contains("Autopilot"))) autopilots.Add(b);
            }
        }

        public bool HasAutopilot => autopilots.Exists(a => a != null && a.enabled);

        // ------------------------------------------------------------------ driving

        public void Enter()
        {
            Measure();
            Occupied = true;
            foreach (var a in autopilots) if (a != null) a.enabled = false;
            Speed = 0;
            if (SkyState.IsNight || SkyState.Daylight < 0.35f) SetHeadlights(true);
            Sound(true);
        }

        public void Exit()
        {
            Occupied = false;
            Speed = 0;
            SteerAngle = 0;
            Sound(false);
            SetHeadlights(false);
        }

        private readonly RaycastHit[] hits = new RaycastHit[16];

        private bool Ignored(Collider c) => c == null || c.isTrigger || own.Contains(c) || c.transform.IsChildOf(transform);

        /// throttle −1…1 (negative = brake, then reverse), steer −1…1.
        public void Drive(float throttle, float steer, bool handbrake, float dt)
        {
            if (dt <= 0) return;
            var top = MaxSpeed;
            if (handbrake) Speed = Mathf.MoveTowards(Speed, 0, BrakeDeceleration * 1.3f * dt);
            else if (throttle > 0.05f)
                Speed = Speed < -0.2f ? Mathf.MoveTowards(Speed, 0, BrakeDeceleration * dt)
                    : Mathf.MoveTowards(Speed, top * throttle, Acceleration * Mathf.Lerp(1, 0.35f, Mathf.Clamp01(Speed / top)) * dt);
            else if (throttle < -0.05f)
                Speed = Speed > 0.2f ? Mathf.MoveTowards(Speed, 0, BrakeDeceleration * dt)
                    : Mathf.MoveTowards(Speed, -ReverseSpeed * -throttle, Acceleration * 0.7f * dt);
            else Speed = Mathf.MoveTowards(Speed, 0, Coast * dt);

            // Bicycle model: yaw rate = v / wheelbase · tan(steer); less lock at speed.
            var lockScale = Mathf.Lerp(1, 0.3f, Mathf.Clamp01(Mathf.Abs(Speed) / top));
            SteerAngle = Mathf.MoveTowards(SteerAngle, steer * MaxSteer * lockScale, SteerSpeed * dt);
            var yawRate = Speed / Wheelbase * Mathf.Tan(SteerAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            if (handbrake && Mathf.Abs(Speed) > 4) yawRate *= 1.35f;                  // a little rotation on the handbrake
            var turn = Quaternion.AngleAxis(yawRate * dt, Vector3.up);
            var flatForward = Vector3.ProjectOnPlane(Forward, Vector3.up).normalized;
            var rotation = turn * transform.rotation;
            var heading = turn * flatForward;

            // Move, sliding along obstacles; stop for people.
            var delta = heading * (Speed * dt);
            delta = Sweep(delta, rotation, true);
            transform.SetPositionAndRotation(transform.position + delta, rotation);
            FollowGround(dt);
            AnimateWheels(dt);
            if (engine != null)
            {
                var load = Mathf.Abs(Speed) / top;
                engine.pitch = Mathf.Lerp(engine.pitch, 0.55f + load * 1.3f + Mathf.Abs(throttle) * 0.12f, 1 - Mathf.Exp(-6 * dt));
                engine.volume = Mathf.Lerp(engine.volume, 0.16f + load * 0.22f + Mathf.Abs(throttle) * 0.08f, 1 - Mathf.Exp(-6 * dt));
            }
            bumpCooldown -= dt;
        }

        private Vector3 Sweep(Vector3 delta, Quaternion rotation, bool allowSlide)
        {
            var distance = delta.magnitude;
            if (distance < 1e-5f) return delta;
            var direction = delta / distance;
            var centre = transform.TransformPoint(local.center) + Vector3.up * Mathf.Min(0.25f, local.size.y * 0.15f);
            var half = Vector3.Scale(local.extents, new Vector3(0.94f, 0.55f, 0.94f));
            var count = Physics.BoxCastNonAlloc(centre, half, direction, hits, rotation, distance + 0.06f, ~0, QueryTriggerInteraction.Ignore);
            var nearest = float.MaxValue;
            RaycastHit block = default;
            for (var i = 0; i < count; i++)
            {
                var h = hits[i];
                if (Ignored(h.collider) || h.distance <= 0 && h.point == Vector3.zero) continue;
                if (h.normal.y > 0.6f) continue;                                         // ramps and kerb tops: drive over
                if (h.distance < nearest) { nearest = h.distance; block = h; }
            }
            if (nearest == float.MaxValue) return delta;
            var person = block.collider is CharacterController || block.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;
            var allowed = direction * Mathf.Max(0, nearest - 0.05f);
            var normal = Vector3.ProjectOnPlane(block.normal, Vector3.up).normalized;
            var headOn = Vector3.Dot(direction, -normal);
            if (person || headOn > 0.75f || !allowSlide)
            {
                if (Mathf.Abs(Speed) > 3 && bumpCooldown <= 0 && !person) Bump(Mathf.Abs(Speed));
                Speed = person ? Mathf.MoveTowards(Speed, 0, BrakeDeceleration * 2 * Time.deltaTime) : 0;
                return allowed;
            }
            // Glancing blow: keep the tangential part, lose speed.
            var remaining = delta - allowed;
            var slide = remaining - normal * Vector3.Dot(remaining, normal);
            Speed *= Mathf.Lerp(0.98f, 0.6f, headOn);
            if (headOn > 0.35f && bumpCooldown <= 0) Bump(Mathf.Abs(Speed) * headOn);
            return allowed + Sweep(slide, rotation, false);
        }

        /// Sit on the ground under the four corners: height from the average, pitch/roll from the differences.
        private void FollowGround(float dt)
        {
            var nose = Forward; var side = Right;
            var halfLength = Length * 0.38f; var halfWidth = Width * 0.4f;
            var centre = transform.TransformPoint(new Vector3(local.center.x, local.min.y, local.center.z));
            var heights = new float[4];
            var found = 0;
            for (var i = 0; i < 4; i++)
            {
                var corner = centre + nose * (i < 2 ? halfLength : -halfLength) + side * (i % 2 == 0 ? halfWidth : -halfWidth);
                heights[i] = Ground(corner, centre.y);
                if (!float.IsNaN(heights[i])) found++;
            }
            if (found < 3)
            {
                verticalSpeed -= 9.81f * dt;                                              // off an edge: fall
                transform.position += Vector3.up * verticalSpeed * dt;
                var g = Ground(centre, centre.y);
                if (!float.IsNaN(g) && transform.position.y + (centre.y - transform.position.y) < g) { transform.position += Vector3.up * (g - centre.y); verticalSpeed = 0; }
                return;
            }
            for (var i = 0; i < 4; i++) if (float.IsNaN(heights[i])) heights[i] = centre.y;
            verticalSpeed = 0;
            var avg = (heights[0] + heights[1] + heights[2] + heights[3]) / 4;
            var pitch = Mathf.Atan2((heights[0] + heights[1]) / 2 - (heights[2] + heights[3]) / 2, halfLength * 2) * Mathf.Rad2Deg;
            var roll = Mathf.Atan2((heights[0] + heights[2]) / 2 - (heights[1] + heights[3]) / 2, halfWidth * 2) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -20, 20); roll = Mathf.Clamp(roll, -15, 15);
            var flat = Vector3.ProjectOnPlane(nose, Vector3.up).normalized;
            var up = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, flat)) * (Quaternion.AngleAxis(roll, flat) * Vector3.up);
            var noseWorld = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, flat)) * flat;
            var wanted = Quaternion.LookRotation(noseWorld, up) * Quaternion.Inverse(Quaternion.LookRotation(NoseLocal, Vector3.up));
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1 - Mathf.Exp(-10 * dt));
            transform.position += Vector3.up * ((avg - centre.y) * (1 - Mathf.Exp(-14 * dt)));
        }

        private float Ground(Vector3 at, float reference)
        {
            var count = Physics.RaycastNonAlloc(at + Vector3.up * 1.6f, Vector3.down, hits, 3.6f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.NaN;
            for (var i = 0; i < count; i++)
            {
                var h = hits[i];
                if (Ignored(h.collider) || h.collider is CharacterController || h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                if (h.normal.y < 0.5f) continue;
                if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
            }
            return best;
        }

        private void AnimateWheels(float dt)
        {
            if (wheels.Count == 0) return;
            spin += Speed * dt;
            foreach (var (wheel, rest, front, radius) in wheels)
            {
                if (wheel == null) continue;
                var roll = Quaternion.AngleAxis(spin / radius * Mathf.Rad2Deg, Vector3.right);
                var steerRotation = front ? Quaternion.AngleAxis(SteerAngle, wheel.parent != null ? wheel.parent.InverseTransformDirection(transform.up) : Vector3.up) : Quaternion.identity;
                wheel.localRotation = steerRotation * rest * roll;
            }
        }

        // ------------------------------------------------------------------ sounds and lights

        public void Horn()
        {
            Sources();
            oneShots.PlayOneShot(VehicleSounds.Horn, 0.7f);
        }

        private void Bump(float strength)
        {
            bumpCooldown = 0.5f;
            Sources();
            oneShots.PlayOneShot(VehicleSounds.Bump, Mathf.Clamp01(strength / 8f));
        }

        private void Sources()
        {
            if (oneShots != null) return;
            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.spatialBlend = 0.8f; oneShots.playOnAwake = false; oneShots.maxDistance = 60; oneShots.rolloffMode = AudioRolloffMode.Linear;
        }

        private void Sound(bool on)
        {
            if (!on) { if (engine != null) engine.Stop(); return; }
            if (engine == null)
            {
                engine = gameObject.AddComponent<AudioSource>();
                engine.clip = Kind == VehicleKind.Truck ? VehicleSounds.Diesel : VehicleSounds.Engine;
                engine.loop = true; engine.spatialBlend = 0.6f; engine.playOnAwake = false; engine.volume = 0.15f; engine.pitch = 0.55f;
                engine.maxDistance = 70; engine.rolloffMode = AudioRolloffMode.Linear;
            }
            engine.Play();
        }

        public void SetHeadlights(bool on)
        {
            HeadlightsOn = on;
            if (on && lamps.Count == 0)
            {
                var side = Vector3.Cross(Vector3.up, NoseLocal);
                foreach (var s in new[] { -1f, 1f })
                {
                    var lamp = new GameObject("Headlight beam").AddComponent<Light>();
                    lamp.transform.SetParent(transform, false);
                    lamp.transform.localPosition = new Vector3(local.center.x, local.min.y + Height * 0.38f, local.center.z) + NoseLocal * (Length * 0.5f) + side * (Width * 0.32f * s);
                    lamp.transform.localRotation = Quaternion.LookRotation(NoseLocal - Vector3.up * 0.12f);
                    lamp.type = LightType.Spot; lamp.spotAngle = 70; lamp.range = 28; lamp.intensity = 2.2f; lamp.color = new Color(1f, 0.96f, 0.86f);
                    lamp.shadows = LightShadows.None;
                    lamps.Add(lamp);
                }
            }
            foreach (var lamp in lamps) if (lamp != null) lamp.enabled = on;
        }

        public void ToggleHeadlights() => SetHeadlights(!HeadlightsOn);

        /// World position of the driver's seat (hips) and the door the driver uses.
        public Vector3 SeatPosition => transform.TransformPoint(SeatLocal);
        public Vector3 ExitPosition => transform.TransformPoint(ExitLocal);
        public Vector3 OtherExitPosition
        {
            get
            {
                var side = Vector3.Cross(Vector3.up, NoseLocal);
                var offset = Vector3.Dot(ExitLocal - local.center, side);
                return transform.TransformPoint(ExitLocal - side * (2 * offset));
            }
        }

        /// Distance from p to the vehicle body (0 inside).
        public float DistanceTo(Vector3 p)
        {
            var q = transform.InverseTransformPoint(p);
            var d = Vector3.Max(Vector3.zero, Vector3.Max(local.min - q, q - local.max));
            return transform.TransformVector(d).magnitude;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.TransformPoint(SeatLocal), 0.15f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.TransformPoint(ExitLocal), 0.25f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.TransformDirection(NoseLocal) * 3);
        }
    }

    /// Synthesised vehicle sounds (replaced automatically if you assign recorded clips later).
    public static class VehicleSounds
    {
        private static AudioClip engine, diesel, horn, bump;
        public static AudioClip Engine => engine ??= Loop("Engine", 48, 0.55f, 4);
        public static AudioClip Diesel => diesel ??= Loop("Diesel", 34, 0.7f, 6);
        public static AudioClip Horn => horn ??= Tone("Horn", 0.45f);
        public static AudioClip Bump => bump ??= Thud();

        private static AudioClip Loop(string name, float fundamental, float roughness, int harmonics)
        {
            const int rate = 22050;
            var length = rate;                                   // 1 s loop: the pitch is scaled at runtime
            var data = new float[length];
            var random = new System.Random(7);
            var f = Mathf.Round(fundamental);                    // whole cycles per second → seamless loop
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / rate;
                var v = 0f;
                for (var h = 1; h <= harmonics; h++) v += Mathf.Sin(2 * Mathf.PI * f * h * t + h * 0.7f) / h;
                v += Mathf.Sin(2 * Mathf.PI * f * 0.5f * 2 * t) * 0.3f;                     // firing pulse
                v += ((float)random.NextDouble() * 2 - 1) * roughness * 0.12f;
                data[i] = v * 0.25f;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Tone(string name, float seconds)
        {
            const int rate = 22050;
            var length = (int)(rate * seconds);
            var data = new float[length];
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / rate;
                var env = Mathf.Min(1, t * 40) * Mathf.Min(1, (seconds - t) * 25);
                var v = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 420 * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 500 * t)) * 0.5f;
                data[i] = v * env * 0.25f;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Thud()
        {
            const int rate = 22050;
            var length = rate / 4;
            var data = new float[length];
            var random = new System.Random(3);
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / rate;
                data[i] = (Mathf.Sin(2 * Mathf.PI * 70 * t) * 0.8f + ((float)random.NextDouble() * 2 - 1) * 0.4f) * Mathf.Exp(-t * 22) * 0.6f;
            }
            var clip = AudioClip.Create("Bump", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
