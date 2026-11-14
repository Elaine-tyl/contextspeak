using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Shared helpers for the small animals (tools/blender/villa_fauna.py → Art/Fauna/*.glb). The models
    /// have separate moving parts: WingL/WingR (birds, butterflies), Tail (koi), Leg_*/Claw_* (crab).
    /// Behaviours move the whole animal by script; nothing is a static decoration.
    public static class Wild
    {
        public static readonly List<Transform> Disturbers = new List<Transform>();
        private static float refreshAt;

        /// The player and people (NPCs) that make animals react.
        public static float NearestDisturber(Vector3 p, out Vector3 from)
        {
            if (Time.time > refreshAt)
            {
                refreshAt = Time.time + 2;
                Disturbers.Clear();
                var player = Object.FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
                if (player != null) Disturbers.Add(player.transform);
                foreach (var n in LivingNPC.All) if (n != null && !n.Hidden) Disturbers.Add(n.transform);
            }
            var best = float.MaxValue; from = p;
            foreach (var t in Disturbers)
            {
                if (t == null) continue;
                var d = (t.position - p).sqrMagnitude;
                if (d < best) { best = d; from = t.position; }
            }
            return Mathf.Sqrt(best);
        }

        private static readonly RaycastHit[] hits = new RaycastHit[8];

        /// Ground height under a point (ignores triggers, water surfaces without colliders and the animals).
        public static bool Ground(Vector3 p, out float y, float up = 3, float down = 12)
        {
            y = p.y;
            var n = Physics.RaycastNonAlloc(p + Vector3.up * up, Vector3.down, hits, up + down, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MinValue;
            for (var i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider is CharacterController || h.normal.y < 0.5f) continue;
                if (h.point.y > best) best = h.point.y;
            }
            if (best == float.MinValue) return false;
            y = best;
            return true;
        }

        /// Is the camera far away? Animals beyond this update less often.
        public static bool Far(Vector3 p, float distance = 70)
        {
            var c = Camera.main;
            return c != null && (c.transform.position - p).sqrMagnitude > distance * distance;
        }

        public static Transform Child(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name || t.name.StartsWith(name + ".")) return t;
            return null;
        }

        public static Vector3 RandomIn(Bounds b, System.Random r) =>
            new Vector3(Mathf.Lerp(b.min.x, b.max.x, (float)r.NextDouble()), b.center.y, Mathf.Lerp(b.min.z, b.max.z, (float)r.NextDouble()));
    }

    /// Wing beat on the hinged WingL/WingR parts (rotation about the body's forward axis).
    public sealed class WingFlap
    {
        private readonly Transform left, right;
        private readonly Quaternion l0, r0;
        private float phase;
        public WingFlap(Transform root)
        {
            left = Wild.Child(root, "WingL"); right = Wild.Child(root, "WingR");
            if (left != null) l0 = left.localRotation;
            if (right != null) r0 = right.localRotation;
        }
        /// rate: beats per second; amplitude: degrees; bias: + lifts both wings (glide = small amplitude).
        public void Tick(float dt, float rate, float amplitude, float bias = 0)
        {
            phase += dt * rate * Mathf.PI * 2;
            var a = Mathf.Sin(phase) * amplitude + bias;
            if (left != null) left.localRotation = l0 * Quaternion.AngleAxis(a, Vector3.forward);
            if (right != null) right.localRotation = r0 * Quaternion.AngleAxis(-a, Vector3.forward);
        }
    }

    /// A bird: hops and pecks on the ground, takes off when someone comes close, flies to a perch or another
    /// spot in its area (flapping, banking, gliding), lands again. Gulls (Soar) mostly circle high and only
    /// sometimes come down to the shore.
    public sealed class WildBird : MonoBehaviour
    {
        public Bounds Area = new Bounds(Vector3.zero, new Vector3(20, 0, 20));
        public Vector3[] Perches = new Vector3[0];
        public float FleeDistance = 4f, FlySpeed = 6f, Size = 1;
        public bool Soar;
        private enum State { Ground, Fly, Perch, Circle }
        private State state;
        private Vector3 target, velocity;
        private float wait, circleAngle, circleRadius, circleHeight;
        private WingFlap wings;
        private System.Random random;
        private float hop;

        private void Start()
        {
            random = new System.Random(GetInstanceID());
            wings = new WingFlap(transform);
            circleRadius = 6 + (float)random.NextDouble() * 10; circleHeight = 9 + (float)random.NextDouble() * 8;
            circleAngle = (float)random.NextDouble() * 360;
            if (Soar) state = State.Circle;
            else if (Wild.Ground(transform.position, out var y)) { transform.position = new Vector3(transform.position.x, y, transform.position.z); state = State.Ground; }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Wild.Far(transform.position, 90) && Time.frameCount % 4 != 0) return;
            switch (state)
            {
                case State.Ground: case State.Perch: Idle(dt); break;
                case State.Fly: Fly(dt); break;
                case State.Circle: Circle(dt); break;
            }
        }

        private void Idle(float dt)
        {
            wings.Tick(dt, 0.2f, 2f, -4);
            var near = Wild.NearestDisturber(transform.position, out var from);
            if (near < FleeDistance * (state == State.Perch ? 0.6f : 1f)) { TakeOff(from); return; }
            wait -= dt;
            if (hop > 0)
            {
                hop -= dt * 3.5f;
                var t = 1 - Mathf.Clamp01(hop);
                transform.position += transform.forward * 0.25f * Size * dt * 3.5f;
                if (Wild.Ground(transform.position, out var g)) transform.position = new Vector3(transform.position.x, g + Mathf.Sin(t * Mathf.PI) * 0.06f * Size, transform.position.z);
            }
            if (wait > 0) return;
            var roll = random.NextDouble();
            if (state == State.Ground && roll < 0.45) { hop = 1; transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + (float)(random.NextDouble() - 0.5) * 120, 0); }
            else if (roll < 0.75) StartCoroutine(Peck());
            else if (roll < 0.85 && !Soar) TakeOff(transform.position);              // a short flight for no reason, like real birds
            wait = 0.5f + (float)random.NextDouble() * 2.5f;
        }

        private System.Collections.IEnumerator Peck()
        {
            var y = transform.eulerAngles.y;
            for (var t = 0f; t < 0.5f; t += Time.deltaTime) { transform.rotation = Quaternion.Euler(Mathf.Sin(t / 0.5f * Mathf.PI) * 35, y, 0); yield return null; }
            transform.rotation = Quaternion.Euler(0, y, 0);
        }

        private void TakeOff(Vector3 from)
        {
            var away = transform.position - from; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere; away.y = 0;
            if (Perches.Length > 0 && random.NextDouble() < 0.5) target = Perches[random.Next(Perches.Length)];
            else
            {
                target = Wild.RandomIn(Area, random) + away.normalized * 3;
                if (Wild.Ground(target + Vector3.up * 20, out var gy, 2, 60)) target.y = gy; else target.y = Area.center.y;
            }
            velocity = (away.normalized + Vector3.up * 1.2f).normalized * FlySpeed * 0.6f;
            state = Soar ? State.Circle : State.Fly;
        }

        private void Fly(float dt)
        {
            var to = target - transform.position;
            var dist = to.magnitude;
            var cruise = to; if (dist > 3) cruise.y = Mathf.Max(to.y, 0) + Mathf.Min(4, dist * 0.3f);
            velocity = Vector3.Lerp(velocity, cruise.normalized * Mathf.Min(FlySpeed, 1 + dist * 1.5f), 1 - Mathf.Exp(-2.5f * dt));
            transform.position += velocity * dt;
            if (velocity.sqrMagnitude > 0.01f)
            {
                var flat = new Vector3(velocity.x, 0, velocity.z);
                var bank = Vector3.SignedAngle(transform.forward, flat, Vector3.up);
                if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat) * Quaternion.Euler(-velocity.y * 4, 0, -bank * 0.5f), 1 - Mathf.Exp(-5 * dt));
            }
            var gliding = velocity.y < -0.3f;
            wings.Tick(dt, gliding ? 1.2f : 9f / Mathf.Max(0.5f, Size), gliding ? 6 : 55, gliding ? 8 : 5);
            if (dist < 0.25f)
            {
                transform.position = target;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                state = Perches.Length > 0 && System.Array.Exists(Perches, p => (p - target).sqrMagnitude < 0.01f) ? State.Perch : State.Ground;
                wait = 1 + (float)random.NextDouble() * 3;
            }
        }

        private void Circle(float dt)
        {
            circleAngle += dt * FlySpeed / circleRadius * Mathf.Rad2Deg;
            var c = Area.center;
            var wind = SkyState.WindSpeed * 0.15f;
            var p = new Vector3(c.x + Mathf.Cos(circleAngle * Mathf.Deg2Rad) * circleRadius, c.y + circleHeight + Mathf.Sin(Time.time * 0.3f + circleRadius) * 1.5f, c.z + Mathf.Sin(circleAngle * Mathf.Deg2Rad) * circleRadius);
            var v = (p - transform.position) / Mathf.Max(dt, 1e-3f);
            transform.position = Vector3.Lerp(transform.position, p, 1 - Mathf.Exp(-2 * dt));
            var flat = new Vector3(v.x, 0, v.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat) * Quaternion.Euler(0, 0, 18), 1 - Mathf.Exp(-3 * dt));
            var glide = Mathf.PerlinNoise(Time.time * 0.2f, circleRadius) > 0.45f;
            wings.Tick(dt, glide ? 0.5f : 3.2f, glide ? 4 + wind : 35, glide ? 6 : 0);
            // Now and then a gull comes down to the shore and walks a little.
            if (random.NextDouble() < dt * 0.01f && Area.size.x > 0)
            {
                target = Wild.RandomIn(Area, random);
                if (Wild.Ground(target + Vector3.up * 20, out var gy, 2, 60)) { target.y = gy; velocity = transform.forward * FlySpeed; state = State.Fly; }
            }
        }

        private void LateUpdate()
        {
            // Soaring birds that landed take off again after a while.
            if (Soar && state == State.Ground && random.NextDouble() < Time.deltaTime * 0.05f) state = State.Circle;
        }
    }

    /// A butterfly: erratic fluttering flight from flower to flower; rests on a bloom with slowly opening wings;
    /// startled when someone is very close.
    public sealed class WildButterfly : MonoBehaviour
    {
        public Vector3[] Flowers = new Vector3[0];
        public Bounds Area = new Bounds(Vector3.zero, new Vector3(10, 0, 10));
        private WingFlap wings;
        private Vector3 target;
        private float rest, seed;
        private System.Random random;

        private void Start()
        {
            random = new System.Random(GetInstanceID());
            wings = new WingFlap(transform);
            seed = (float)random.NextDouble() * 100;
            Pick();
        }

        private void Pick()
        {
            if (Flowers.Length > 0) target = Flowers[random.Next(Flowers.Length)] + Vector3.up * 0.05f;
            else { target = Wild.RandomIn(Area, random); target.y += 0.6f; }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Wild.Far(transform.position, 45)) return;
            if (rest > 0)
            {
                rest -= dt;
                wings.Tick(dt, 0.4f, 30, 45);                                   // resting: wings slowly open and close
                if (Wild.NearestDisturber(transform.position, out _) < 1.2f) rest = 0;
                if (rest <= 0) Pick();
                return;
            }
            var t = Time.time + seed;
            var wobble = new Vector3(Mathf.PerlinNoise(t * 1.3f, seed) - 0.5f, Mathf.PerlinNoise(seed, t * 1.7f) - 0.5f, Mathf.PerlinNoise(t * 1.1f, seed + 3) - 0.5f) * 2.2f;
            var to = target - transform.position;
            var v = (to.normalized * Mathf.Min(1.4f, to.magnitude * 2) + wobble);
            transform.position += v * dt;
            if (v.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(v.x, v.y * 0.3f, v.z)), 1 - Mathf.Exp(-6 * dt));
            wings.Tick(dt, 7, 70, 10);
            if (to.magnitude < 0.12f) { rest = 2 + (float)random.NextDouble() * 6; transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0); }
            if (random.NextDouble() < dt * 0.08f) Pick();
        }
    }

    /// A crab: short sideways scuttles on the sand, claws twitching, legs stepping; runs sideways away from
    /// people and digs in (hides under the sand) for a while before coming out again.
    public sealed class WildCrab : MonoBehaviour
    {
        public Bounds Area = new Bounds(Vector3.zero, new Vector3(10, 0, 10));
        private readonly List<(Transform t, Quaternion rest, float phase)> legs = new List<(Transform, Quaternion, float)>();
        private System.Random random;
        private float move, hidden, wait, dir = 1, gait;
        private Vector3 surface;

        private void Start()
        {
            random = new System.Random(GetInstanceID());
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Leg_") || t.name.StartsWith("Claw_")) legs.Add((t, t.localRotation, t.name.EndsWith("1") || t.name.EndsWith("3") ? Mathf.PI : 0));
            Snap();
        }

        private void Snap() { if (Wild.Ground(transform.position, out var y)) transform.position = new Vector3(transform.position.x, y, transform.position.z); }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Wild.Far(transform.position, 40)) return;
            if (hidden > 0)
            {
                hidden -= dt;
                if (hidden <= 0) { transform.position = surface; }
                return;
            }
            var near = Wild.NearestDisturber(transform.position, out var from);
            if (near < 3f && move <= 0)
            {
                var away = transform.position - from; away.y = 0;
                dir = Vector3.Dot(away, transform.right) >= 0 ? 1 : -1;
                move = 1.2f; gait = 3.2f;
                if (random.NextDouble() < 0.35) { surface = transform.position; StartCoroutine(Dig()); return; }
            }
            if (move > 0)
            {
                move -= dt;
                var step = transform.right * dir * gait * 0.18f * dt;
                var next = transform.position + step;
                if (Area.size.x > 0 && !Area.Contains(new Vector3(next.x, Area.center.y, next.z))) { dir = -dir; next = transform.position; }
                transform.position = next; Snap();
                Legs(dt, gait * 8, 25);
                return;
            }
            Legs(dt, 1, 4);
            wait -= dt;
            if (wait > 0) return;
            wait = 1 + (float)random.NextDouble() * 4;
            if (random.NextDouble() < 0.55) { move = 0.4f + (float)random.NextDouble() * 1.2f; gait = 1; dir = random.NextDouble() < 0.5 ? 1 : -1; }
            else transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + (float)(random.NextDouble() - 0.5) * 60, 0);
        }

        private void Legs(float dt, float speed, float amp)
        {
            gaitTime += dt * speed;
            foreach (var (t, rest, phase) in legs)
            {
                if (t == null) continue;
                var a = Mathf.Sin(gaitTime + phase) * amp * (t.name.StartsWith("Claw") ? 0.4f : 1);
                t.localRotation = rest * Quaternion.Euler(a * 0.4f, 0, a);
            }
        }
        private float gaitTime;

        private System.Collections.IEnumerator Dig()
        {
            for (var t = 0f; t < 0.8f; t += Time.deltaTime) { transform.position = surface - Vector3.up * 0.09f * (t / 0.8f); Legs(Time.deltaTime, 30, 30); yield return null; }
            hidden = 6 + (float)random.NextDouble() * 10;
            transform.position = surface - Vector3.up * 0.3f;
        }
    }

    /// A small school of fish in a pond/pool volume: each fish wanders inside the bounds at its own depth,
    /// keeps a little apart from the others, wags its tail with speed, and scatters when someone comes to
    /// the edge or swims in.
    public sealed class KoiSchool : MonoBehaviour
    {
        public Bounds Water = new Bounds(Vector3.zero, new Vector3(4, 0.6f, 4));
        public Transform[] Fish = new Transform[0];
        private Vector3[] targets, velocities;
        private (Transform tail, Quaternion rest)[] tails;
        private float[] phases;
        private System.Random random;

        private void Start()
        {
            random = new System.Random(GetInstanceID());
            targets = new Vector3[Fish.Length]; velocities = new Vector3[Fish.Length]; tails = new (Transform, Quaternion)[Fish.Length]; phases = new float[Fish.Length];
            for (var i = 0; i < Fish.Length; i++)
            {
                targets[i] = Point();
                var tail = Fish[i] != null ? Wild.Child(Fish[i], "Tail") : null;
                tails[i] = (tail, tail != null ? tail.localRotation : Quaternion.identity);
                phases[i] = (float)random.NextDouble() * 6;
            }
        }

        private Vector3 Point()
        {
            var b = Water;
            return new Vector3(Mathf.Lerp(b.min.x + 0.3f, b.max.x - 0.3f, (float)random.NextDouble()), Mathf.Lerp(b.min.y + 0.12f, b.max.y - 0.1f, (float)random.NextDouble()), Mathf.Lerp(b.min.z + 0.3f, b.max.z - 0.3f, (float)random.NextDouble()));
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Wild.Far(Water.center, 50)) return;
            var near = Wild.NearestDisturber(Water.center, out var from);
            var scare = near < Mathf.Max(Water.extents.x, Water.extents.z) + 1.2f;
            for (var i = 0; i < Fish.Length; i++)
            {
                var f = Fish[i]; if (f == null) continue;
                var to = targets[i] - f.position;
                if (to.magnitude < 0.25f || random.NextDouble() < dt * 0.1f) targets[i] = Point();
                var desired = to.normalized * (scare ? 0.9f : 0.28f);
                if (scare) { var away = f.position - from; away.y = 0; desired += away.normalized * 0.6f; }
                for (var j = 0; j < Fish.Length; j++)
                    if (j != i && Fish[j] != null) { var d = f.position - Fish[j].position; if (d.sqrMagnitude < 0.09f) desired += d.normalized * 0.3f; }
                velocities[i] = Vector3.Lerp(velocities[i], desired, 1 - Mathf.Exp(-1.6f * dt));
                var p = f.position + velocities[i] * dt;
                p = new Vector3(Mathf.Clamp(p.x, Water.min.x + 0.15f, Water.max.x - 0.15f), Mathf.Clamp(p.y, Water.min.y + 0.08f, Water.max.y - 0.06f), Mathf.Clamp(p.z, Water.min.z + 0.15f, Water.max.z - 0.15f));
                f.position = p;
                if (velocities[i].sqrMagnitude > 1e-4f) f.rotation = Quaternion.Slerp(f.rotation, Quaternion.LookRotation(new Vector3(velocities[i].x, velocities[i].y * 0.3f, velocities[i].z)), 1 - Mathf.Exp(-3 * dt));
                phases[i] += dt * (4 + velocities[i].magnitude * 18);
                if (tails[i].tail != null) tails[i].tail.localRotation = tails[i].rest * Quaternion.AngleAxis(Mathf.Sin(phases[i]) * 25, Vector3.up);
            }
        }
    }
}
