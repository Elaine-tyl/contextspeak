using UnityEngine;

namespace ContextSpeak.Life
{
    /// Water reacting to the swimmer's actual motion — all through the height-field ripple simulation
    /// (normal-based shading, refraction/reflection distortion in the water shaders) and pooled
    /// particles; no ring sprites or line renderers:
    ///  • stroke-synchronised: a hand or foot bone crossing the surface makes a small ripple sized by
    ///    the bone's speed (and a splash only when it is fast) — driven by the anim itself;
    ///  • a V-wake behind the body whose length grows with swim speed (CharacterWaterInteraction adds
    ///    the bow/stern displacement);
    ///  • treading: small continuous disturbances around the shoulders; back floating: very small ones;
    ///  • dive: entry splash + bubbles; trailing bubbles under water; a break-through splash when surfacing.
    [DefaultExecutionOrder(230)]
    [RequireComponent(typeof(SwimmingController))]
    public sealed class SwimmingWaterInteraction : MonoBehaviour
    {
        [Tooltip("Bone speed (m/s) needed for a splash rather than only a ripple.")] public float SplashSpeed = 1.6f;
        public int Strokes { get; private set; }
        public int Disturbances { get; private set; }

        private SwimmingController swim;
        private SwimmingAnimationController anim;
        private CharacterWaterInteraction character;
        private AudioSource source;
        private readonly Limb[] limbs = { new Limb("LeftHand"), new Limb("RightHand"), new Limb("LeftFoot"), new Limb("RightFoot") };
        private float wakeTimer, ambientTimer, bubbleTimer;
        private bool headWasUnder;
        private SwimState lastState;

        private sealed class Limb
        {
            public readonly string Bone;
            public Vector3 Last;
            public bool Started, Under, WasFast;
            public float Cooldown;
            public Limb(string bone) { Bone = bone; }
        }

        private void Awake()
        {
            swim = GetComponent<SwimmingController>();
            anim = GetComponent<SwimmingAnimationController>();
            character = GetComponent<CharacterWaterInteraction>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0.7f; source.rolloffMode = AudioRolloffMode.Linear; source.maxDistance = 25;
        }

        private float Scale(WaterInteractionProfile water) =>
            swim.Tuning.SplashScale * (swim.Profile != null ? swim.Profile.SplashIntensity : 1) * (water != null ? water.RippleStrength : 1);

        private void Disturb(Vector3 at, float surface, float radius, float amount)
        {
            WaterRippleSimulation.Disturb(new Vector3(at.x, surface, at.z), radius, amount);
            Disturbances++;
        }

        private void LateUpdate()
        {
            if (character == null) character = GetComponent<CharacterWaterInteraction>();
            if (character != null) character.StrokesExternal = swim.IsSwimming;
            var body = swim.Body;
            var state = swim.State;
            if (!swim.IsSwimming || body == null)
            {
                foreach (var limb in limbs) limb.Started = false;
                lastState = state;
                headWasUnder = false;
                return;
            }
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var water = body.ActiveProfile;
            var effects = WaterSplashController.Instance;
            var scale = Scale(water);
            var position = transform.position;
            var surface = body.SurfaceHeight(position);
            var speed = swim.HorizontalSpeed;
            var forward = swim.Heading;

            // Stroke-synchronised entries from the animated limbs.
            foreach (var limb in limbs)
            {
                var bone = anim != null ? anim.Bone(limb.Bone) : null;
                if (bone == null) continue;
                var p = bone.position;
                var level = body.SurfaceHeight(p);
                var under = p.y < level - 0.02f;
                limb.Cooldown -= dt;
                if (limb.Started)
                {
                    var v = (p - limb.Last) / dt;
                    var limbSpeed = v.magnitude;
                    var foot = limb.Bone.EndsWith("Foot");
                    var fast = limbSpeed > 0.9f;
                    var entry = under && !limb.Under && limbSpeed > 0.4f;                          // breaking the surface on the way in
                    var sweep = !foot && under && fast && !limb.WasFast && level - p.y < 0.4f;      // a stroke sweeping just under the surface
                    if ((entry || sweep) && limb.Cooldown <= 0 && swim.Machine.AtSurface)
                    {
                        var s = Mathf.Clamp01(limbSpeed / 4f);
                        var depthFade = entry ? 1 : Mathf.Lerp(1, 0.35f, (level - p.y) / 0.4f);
                        Disturb(p, level, Mathf.Lerp(0.1f, 0.24f, s), -Mathf.Lerp(0.004f, foot ? 0.012f : 0.018f, s) * depthFade * scale);
                        if (entry && limbSpeed > SplashSpeed && effects != null) effects.Splash(new Vector3(p.x, level, p.z), level, new Vector3(v.x, 0, v.z), Mathf.Lerp(0.08f, 0.32f, s) * scale * (foot ? 0.6f : 1), water);
                        limb.Cooldown = 0.2f;
                        if (!foot) { Strokes++; Play(WaterAudio.Stroke, Mathf.Lerp(0.15f, 0.5f, s) * depthFade); }
                    }
                    else if (!under && limb.Under && limbSpeed > 1.2f && swim.Machine.AtSurface && effects != null)
                        effects.Drip(new Vector3(p.x, level + 0.05f, p.z));               // water running off a recovering limb
                    limb.WasFast = fast;
                }
                limb.Last = p;
                limb.Under = under;
                limb.Started = true;
            }

            // V-wake: two arms behind the swimmer, longer and stronger with speed.
            if (state == SwimState.SurfaceSwimming && speed > 0.45f && (wakeTimer -= dt) <= 0)
            {
                wakeTimer = 0.07f;
                var right = Vector3.Cross(Vector3.up, forward);
                var wake = swim.Tuning.WakeScale * (swim.Profile != null ? swim.Profile.WakeStrength : 1) * water.WakeLength;
                var length = Mathf.Lerp(0.6f, 2.2f, Mathf.InverseLerp(0.45f, 3f, speed)) * wake;
                var d = Random.Range(0.5f, 0.5f + length);
                var spread = d * 0.36f;                                      // ≈ Kelvin angle
                var amount = -0.0022f * Mathf.Clamp(speed, 0, 3) * scale * Mathf.Lerp(1, 0.4f, (d - 0.5f) / Mathf.Max(0.01f, length));
                Disturb(position - forward * d + right * spread, surface, 0.16f, amount);
                Disturb(position - forward * d - right * spread, surface, 0.16f, amount);
            }

            // Treading / floating: small continuous disturbances (no large rings).
            if ((state == SwimState.TreadingWater || state == SwimState.BackFloating) && (ambientTimer -= dt) <= 0)
            {
                var tread = state == SwimState.TreadingWater;
                ambientTimer = tread ? Random.Range(0.3f, 0.5f) : Random.Range(1.2f, 2f);
                var angle = Random.Range(0, Mathf.PI * 2);
                var around = position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (tread ? 0.32f : 0.55f);
                Disturb(around, surface, tread ? 0.18f : 0.14f, (tread ? -0.0032f : -0.0016f) * scale);
            }

            // Dive entry.
            if (state == SwimState.Diving && lastState != SwimState.Diving)
            {
                var head = anim != null && anim.HeadBone != null ? anim.HeadBone.position : position + Vector3.up * swim.FloatLine;
                Disturb(head, surface, 0.45f, -0.035f * scale);
                effects?.Splash(new Vector3(head.x, surface, head.z), surface, forward * 0.6f, 0.45f * scale, water);
                effects?.Bubbles(new Vector3(head.x, surface - 0.4f, head.z), surface, Mathf.RoundToInt(20 * water.BubbleIntensity), water);
                Play(WaterAudio.Splash, 0.6f);
            }
            // Trailing bubbles while swimming under water.
            if (swim.Machine.Underwater && (bubbleTimer -= dt) <= 0)
            {
                bubbleTimer = speed > 0.4f ? 0.18f : 0.9f;
                var from = anim != null && anim.Bone("LeftFoot") != null ? anim.Bone(Random.value < 0.5f ? "LeftFoot" : "RightFoot").position : position;
                effects?.Bubbles(from, surface, speed > 0.4f ? 2 : 3, water);
            }
            // Head breaking through the surface.
            var headUnder = swim.HeadUnder;
            if (headWasUnder && !headUnder)
            {
                var head = anim != null && anim.HeadBone != null ? anim.HeadBone.position : position + Vector3.up * swim.FloatLine;
                Disturb(head, surface, 0.32f, -0.016f * scale);
                effects?.Splash(new Vector3(head.x, surface, head.z), surface, Vector3.up, 0.22f * scale, water);
                effects?.Drip(new Vector3(head.x, surface + 0.2f, head.z));
            }
            headWasUnder = headUnder;
            lastState = state;
        }

        private float soundCooldown;

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || Time.time < soundCooldown) return;
            soundCooldown = Time.time + 0.1f;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
