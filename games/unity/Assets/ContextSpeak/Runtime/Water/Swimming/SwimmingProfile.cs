using UnityEngine;

namespace ContextSpeak.Life
{
    /// Swimming tuning for one kind of water. Values are gameplay starting points (not measured human
    /// swimming data) — tune them in Play mode with the swimming debug panel (F5).
    [CreateAssetMenu(menuName = "ContextSpeak/Water/Swimming Profile", fileName = "Swimming Profile")]
    public sealed class SwimmingProfile : ScriptableObject
    {
        public WaterKind Kind = WaterKind.Pond;

        [Header("Speeds (m/s)")]
        [Tooltip("Walking speed in waist-deep water.")] public float WadeSpeed = 1.6f;
        public float SurfaceSpeed = 1.9f;
        [Tooltip("Shift.")] public float FastSpeed = 3f;
        public float UnderwaterSpeed = 1.5f;
        public float BackFloatSpeed = 0.45f;
        [Tooltip("Treading water: drift when turning on the spot.")] public float TreadSpeed = 0.5f;
        public float AscentSpeed = 1.4f;
        public float DescentSpeed = 1.1f;
        [Tooltip("Horizontal acceleration while stroking (m/s²).")] public float Acceleration = 2.4f;
        [Tooltip("Water resistance: deceleration when not stroking (m/s²).")] public float Deceleration = 1.5f;
        [Tooltip("How quickly the body turns toward the swim direction (°/s).")] public float TurnRate = 160;

        [Header("Buoyancy (CharacterController — buoyancy-inspired vertical velocity)")]
        [Tooltip("Feet depth below the surface at equilibrium (m). 1.38 keeps the chin just above water for a 1.7 m body.")]
        public float FloatLine = 1.38f;
        [Tooltip("1 = settle at the float line; >1 floats higher, <1 lower.")] public float BuoyancyStrength = 1;
        [Tooltip("Vertical damping (1/s). Higher = less bobbing after a disturbance.")] public float BuoyancyDamping = 2.8f;
        [Tooltip("Speed limits for buoyancy alone (m/s).")] public float MaxRiseSpeed = 1.5f, MaxSinkSpeed = 1.6f;
        [Tooltip("Net buoyancy while actively swimming under water (0 = neutral, 1 = full).")] [Range(0, 1)] public float UnderwaterBuoyancy = 0.3f;
        [Tooltip("Thrust from Space / C (m/s²).")] public float VerticalThrust = 5.5f;

        [Header("Surface following")]
        [Tooltip("Smoothing of the sampled (wavy) surface height (s).")] public float SurfaceSmoothing = 0.25f;
        [Tooltip("0 = ignore waves (pool), 1 = ride the full wave height (ocean).")] [Range(0, 1)] public float WaveFollow = 0.4f;
        [Tooltip("Gentle bobbing amplitude in calm water (m).")] public float BobAmplitude = 0.035f;
        public float BobFrequency = 0.42f;

        [Header("Posture (animation-specific buoyancy offsets, metres below the surface)")]
        public float SwimHipsDepth = 0.08f;
        [Tooltip("Back float: face (head joint) height above the waterline.")] public float BackFloatFaceAbove = 0.03f;
        [Tooltip("Treading: chin height above the waterline.")] public float TreadChinAbove = 0.06f;

        [Header("Detection")]
        [Tooltip("Water must be at least this deep to swim — puddles and shallow ends are waded.")] public float MinSwimDepth = 1.25f;
        [Tooltip("Swimming starts when the water reaches this fraction of the body height…")] [Range(0.4f, 0.95f)] public float EnterImmersion = 0.72f;
        [Tooltip("…and ends (standing on the bottom) below this fraction.")] [Range(0.3f, 0.9f)] public float ExitImmersion = 0.6f;
        [Tooltip("Transitions must hold this long (s) — no flicker at the waterline.")] public float TransitionDelay = 0.2f;

        [Header("Water movement")]
        [Tooltip("How much currents (rivers, ocean) move the swimmer.")] public float CurrentInfluence = 1;
        [Tooltip("Horizontal surge from wave slopes (ocean), m/s per unit slope.")] public float WaveSurge;

        [Header("Effects")]
        public float WakeStrength = 1;
        public float SplashIntensity = 1;

        [Header("Breath (simplified gameplay mechanic)")]
        public bool UseBreath = true;
        public float BreathSeconds = 40;
        public float RefillSeconds = 3;

        [Header("Animation")]
        [Tooltip("Cross-fade time between swimming animations (s).")] public float AnimationBlend = 0.3f;

        public static SwimmingProfile Create(WaterKind kind)
        {
            var p = CreateInstance<SwimmingProfile>();
            p.Kind = kind;
            p.name = kind + " Swimming";
            switch (kind)
            {
                case WaterKind.Pool:
                    p.WaveFollow = 0; p.BobAmplitude = 0.02f; p.CurrentInfluence = 0; p.BuoyancyDamping = 3f;
                    p.SurfaceSpeed = 2f; p.FastSpeed = 3.2f;
                    break;
                case WaterKind.Pond:
                    p.WaveFollow = 0.3f; p.BobAmplitude = 0.03f; p.CurrentInfluence = 0.3f;
                    p.SurfaceSpeed = 1.8f; p.FastSpeed = 2.8f;
                    break;
                case WaterKind.Lake:
                    p.WaveFollow = 0.6f; p.BobAmplitude = 0.045f; p.CurrentInfluence = 0.6f;
                    break;
                case WaterKind.River:
                case WaterKind.Stream:
                    p.WaveFollow = 0.5f; p.BobAmplitude = 0.04f; p.CurrentInfluence = 1;
                    p.SurfaceSpeed = 1.7f; p.FastSpeed = 2.7f;
                    break;
                case WaterKind.Ocean:
                    p.WaveFollow = 1; p.BobAmplitude = 0.06f; p.CurrentInfluence = 1; p.WaveSurge = 1.1f;
                    p.SurfaceSmoothing = 0.35f; p.BuoyancyDamping = 2.4f;
                    p.SurfaceSpeed = 1.7f; p.FastSpeed = 2.7f; p.UnderwaterSpeed = 1.4f;
                    break;
            }
            return p;
        }
    }
}
