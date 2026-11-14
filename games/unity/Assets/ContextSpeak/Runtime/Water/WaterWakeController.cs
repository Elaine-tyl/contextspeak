using UnityEngine;

namespace ContextSpeak.Life
{
    /// Continuous disturbance from a body moving through water: a raised bow wave in front and a
    /// trough behind (the wave equation turns these into a V-shaped wake), broader with depth and
    /// faster movement; small foam at the bow when pushing hard. Swimming adds alternating
    /// shoulder/arm disturbances synchronised with strokes. Stops naturally when movement stops.
    public sealed class WaterWakeController
    {
        public float Interval = 0.05f;
        private float timer, strokeTimer, idleTimer;
        private bool leftArm;

        /// depthFactor 0…1 (knee … chest); speed m/s; returns true when a stroke happened this frame.
        public bool Tick(Vector3 position, Vector3 velocity, float surface, float depthFactor, bool swimming, bool underwater, float dt, WaterInteractionProfile profile, WaterSplashController effects, float bodyRadius)
        {
            var flat = new Vector3(velocity.x, 0, velocity.z);
            var speed = flat.magnitude;
            var forward = speed > 0.05f ? flat / speed : Vector3.zero;
            var strength = profile.RippleStrength;
            var stroke = false;
            timer -= dt;
            if (speed > 0.25f && !underwater && timer <= 0)
            {
                timer = Interval;
                var width = Mathf.Lerp(0.18f, 0.42f, depthFactor) * bodyRadius / 0.3f;
                var amount = Mathf.Clamp(speed, 0, 3) * Mathf.Lerp(0.0015f, 0.0045f, depthFactor) * strength * profile.WakeLength;
                var bow = position + forward * width * 1.1f;
                var stern = position - forward * width * 0.9f;
                WaterRippleSimulation.Disturb(new Vector3(bow.x, surface, bow.z), width, amount);
                WaterRippleSimulation.Disturb(new Vector3(stern.x, surface, stern.z), width * 1.2f, -amount * 0.9f);
                if (speed > 1.6f && Random.value < 0.25f * depthFactor * profile.FoamAmount && effects != null) effects.Foam(bow, surface, width, 2, profile);
            }
            if (swimming && !underwater)
            {
                if (speed > 0.3f)
                {
                    strokeTimer -= dt * Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01(speed / 2.4f));
                    if (strokeTimer <= 0)
                    {
                        strokeTimer = 0.55f;
                        leftArm = !leftArm;
                        var side = Vector3.Cross(Vector3.up, forward) * (leftArm ? -0.32f : 0.32f);
                        var hand = position + forward * 0.55f + side;
                        WaterRippleSimulation.Disturb(new Vector3(hand.x, surface, hand.z), 0.22f, -0.016f * strength);
                        if (effects != null) effects.Splash(new Vector3(hand.x, surface, hand.z), surface, forward, Mathf.Lerp(0.12f, 0.3f, speed / 2.4f), profile);
                        stroke = true;
                    }
                }
                else if ((idleTimer -= dt) <= 0)
                {
                    // Floating / treading water: soft rings around the shoulders.
                    idleTimer = Random.Range(1.6f, 2.6f);
                    WaterRippleSimulation.Disturb(new Vector3(position.x, surface, position.z), 0.4f, -0.006f * strength);
                }
            }
            else if (!swimming && depthFactor > 0.5f && speed < 0.2f && (idleTimer -= dt) <= 0)
            {
                // Standing waist deep: the body moves slightly with breathing and sends out faint rings.
                idleTimer = Random.Range(2.5f, 4f);
                WaterRippleSimulation.Disturb(new Vector3(position.x, surface, position.z), bodyRadius * 1.3f, -0.003f * strength);
            }
            return stroke;
        }
    }

    /// Any moving object that should disturb water it touches: boats, NPCs, fish, a fishing bobber,
    /// a dropped object. Emits small ripples proportional to its speed, and a splash when it enters.
    public sealed class WaterRippleEmitter : MonoBehaviour
    {
        public float Radius = 0.25f, Strength = 1, Interval = 0.08f;
        [Tooltip("Only disturb when within this distance of the surface (m).")] public float Reach = 0.35f;
        public bool SplashOnEntry = true;
        private Vector3 last;
        private float timer;
        private bool wasIn, started;

        private void Update()
        {
            var p = transform.position;
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var velocity = started ? (p - last) / dt : Vector3.zero;
            last = p; started = true;
            var body = WaterBody.Find(p, Reach);
            if (body == null) { wasIn = false; return; }
            var surface = body.SurfaceHeight(p);
            var near = Mathf.Abs(p.y - surface) < Reach;
            var profile = body.ActiveProfile;
            if (near && !wasIn && SplashOnEntry && velocity.y < -1.5f)
            {
                var impact = WaterInteractionMath.Impact(-velocity.y, new Vector2(velocity.x, velocity.z).magnitude, Radius / 0.5f);
                WaterRippleSimulation.Disturb(new Vector3(p.x, surface, p.z), Mathf.Max(Radius, WaterInteractionMath.JumpRadius(profile, impact) * Radius), -0.04f * impact * Strength);
                WaterSplashController.Instance?.Splash(p, surface, velocity, impact * 0.7f, profile);
                WaterSplashController.Instance?.Bubbles(new Vector3(p.x, surface - 0.3f, p.z), surface, Mathf.RoundToInt(10 * impact), profile);
            }
            wasIn = near;
            if (!near || (timer -= dt) > 0) return;
            timer = Interval;
            var speed = new Vector2(velocity.x, velocity.z).magnitude;
            if (speed < 0.08f) return;
            WaterRippleSimulation.Disturb(new Vector3(p.x, surface, p.z), Radius, -Mathf.Min(speed, 4) * 0.003f * Strength * profile.RippleStrength);
        }
    }
}

namespace ContextSpeak.Life
{
    /// Test helper: drifts an object slowly around a circle on the water (a moored boat swinging, a
    /// floating toy) so its ripple emitter can be checked without a boat controller.
    public sealed class TestDrift : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Vector3 Centre;
        public float Radius = 3, Speed = 0.6f;
        private float angle;
        private void Update()
        {
            angle += Speed / UnityEngine.Mathf.Max(0.5f, Radius) * UnityEngine.Time.deltaTime;
            var p = Centre + new UnityEngine.Vector3(UnityEngine.Mathf.Cos(angle), 0, UnityEngine.Mathf.Sin(angle)) * Radius;
            var body = WaterBody.Find(p, 1);
            if (body != null) p.y = body.SurfaceHeight(p) - 0.03f;
            transform.position = p;
            transform.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(-UnityEngine.Mathf.Sin(angle), 0, UnityEngine.Mathf.Cos(angle)));
        }
    }
}
