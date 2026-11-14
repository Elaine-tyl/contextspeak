using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum WaterKind { Pool, Pond, River, Stream, Lake, Ocean }

    /// How a kind of water reacts to people and objects. The numbers are artistic starting points —
    /// tune them per water body, movement speed, body size and simulation resolution.
    [CreateAssetMenu(menuName = "ContextSpeak/Water Interaction Profile", fileName = "WaterInteractionProfile")]
    public sealed class WaterInteractionProfile : ScriptableObject
    {
        public WaterKind Kind = WaterKind.Pond;

        [Header("Ripple simulation")]
        [Tooltip("Wave propagation speed of the local height field (m/s).")] public float PropagationSpeed = 1.2f;
        [Tooltip("Height kept per simulation step at the reference rate below. Converted for other step rates — it is NOT timestep-independent.")]
        [Range(0.9f, 0.9999f)] public float DampingPerStep = 0.985f;
        public float ReferenceStepHz = 60;
        [Tooltip("Multiplies every ripple amplitude.")] public float RippleStrength = 1;
        [Tooltip("Ripples and wakes drift with the current (rivers, streams).")] public bool AdvectWithFlow;
        [Tooltip("Banks and pool walls reflect ripples.")] public bool WallReflection = true;

        [Header("Ripple size (m)")]
        public Vector2 WalkRippleRadius = new Vector2(0.12f, 0.3f);
        public Vector2 RunRippleRadius = new Vector2(0.25f, 0.5f);
        public Vector2 JumpRippleRadius = new Vector2(0.6f, 1.5f);

        [Header("Splashes (0–1)")]
        public Vector2 WalkSplash = new Vector2(0.15f, 0.35f);
        public Vector2 RunSplash = new Vector2(0.4f, 0.7f);
        public Vector2 JumpSplash = new Vector2(0.7f, 1f);
        public float SplashScale = 1, ParticleScale = 1, FoamAmount = 1, BubbleIntensity = 1, WakeLength = 1;

        [Header("Timing")]
        public Vector2 FootstepCooldown = new Vector2(0.15f, 0.35f);
        public Vector2 BubbleLifetime = new Vector2(0.4f, 2f);

        [Header("Character")]
        [Tooltip("Movement drag multiplier (1 = calm water; higher = surf, strong current).")] public float Drag = 1;
        public Color DropletTint = new Color(0.86f, 0.93f, 0.96f, 0.85f);
        public Color FoamTint = new Color(0.95f, 0.97f, 0.97f, 0.8f);

        /// Damping for an arbitrary step length: d_ref ^ (dt × refHz).
        public float DampingFor(float dt) => Mathf.Pow(DampingPerStep, dt * ReferenceStepHz);

        public static WaterInteractionProfile Create(WaterKind kind)
        {
            var p = CreateInstance<WaterInteractionProfile>();
            p.name = kind + " (default)";
            p.Kind = kind;
            switch (kind)
            {
                case WaterKind.Pool:   // clear, fine ripples, walls reflect, small splashes
                    p.PropagationSpeed = 1.0f; p.DampingPerStep = 0.99f; p.RippleStrength = 0.85f; p.SplashScale = 0.85f; p.FoamAmount = 0.25f;
                    p.WallReflection = true; p.DropletTint = new Color(0.9f, 0.96f, 1f, 0.85f);
                    break;
                case WaterKind.Pond:   // gentle, low amplitude, quickly calm
                    p.PropagationSpeed = 0.9f; p.DampingPerStep = 0.982f; p.RippleStrength = 0.65f; p.SplashScale = 0.9f; p.FoamAmount = 0.45f;
                    p.DropletTint = new Color(0.82f, 0.88f, 0.84f, 0.85f); p.FoamTint = new Color(0.88f, 0.9f, 0.86f, 0.7f);
                    break;
                case WaterKind.River: case WaterKind.Stream:   // current carries ripples downstream, more foam, more drag
                    p.PropagationSpeed = 1.2f; p.DampingPerStep = 0.975f; p.RippleStrength = 0.9f; p.FoamAmount = 1.3f; p.AdvectWithFlow = true;
                    p.WallReflection = false; p.Drag = 1.3f; p.DropletTint = new Color(0.85f, 0.9f, 0.88f, 0.85f);
                    break;
                case WaterKind.Lake:
                    p.PropagationSpeed = 1.1f; p.DampingPerStep = 0.985f; p.RippleStrength = 0.8f; p.FoamAmount = 0.5f; p.WallReflection = false;
                    break;
                case WaterKind.Ocean:  // layered on top of the ocean waves: stronger splashes, surf turbulence, more drag
                    p.PropagationSpeed = 1.6f; p.DampingPerStep = 0.97f; p.RippleStrength = 1.2f; p.SplashScale = 1.4f; p.FoamAmount = 1.6f;
                    p.WallReflection = false; p.Drag = 1.4f; p.WalkRippleRadius = new Vector2(0.2f, 0.4f); p.RunRippleRadius = new Vector2(0.35f, 0.7f);
                    p.JumpRippleRadius = new Vector2(0.9f, 2f);
                    break;
            }
            return p;
        }
    }

    /// Pure maths shared by the interaction components and the automated checks.
    public static class WaterInteractionMath
    {
        /// 0 at walking pace … 1 at running pace and above.
        public static float RunAmount(float speed, float walkSpeed, float runSpeed) => Mathf.Clamp01((speed - walkSpeed * 0.5f) / Mathf.Max(0.1f, runSpeed - walkSpeed * 0.5f));

        public static float StepSplash(WaterInteractionProfile p, float speed, float walkSpeed, float runSpeed)
        {
            if (speed < 0.2f) return 0;
            var run = RunAmount(speed, walkSpeed, runSpeed);
            var walk = Mathf.Lerp(p.WalkSplash.x, p.WalkSplash.y, Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed)));
            var running = Mathf.Lerp(p.RunSplash.x, p.RunSplash.y, run);
            return Mathf.Lerp(walk, running, run) * p.SplashScale;
        }

        public static float StepRadius(WaterInteractionProfile p, float speed, float walkSpeed, float runSpeed)
        {
            var run = RunAmount(speed, walkSpeed, runSpeed);
            var walk = Mathf.Lerp(p.WalkRippleRadius.x, p.WalkRippleRadius.y, Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed)));
            return Mathf.Lerp(walk, Mathf.Lerp(p.RunRippleRadius.x, p.RunRippleRadius.y, run), run);
        }

        /// Impact 0…1 from the vertical entry speed (m/s, positive = down) and how much body area meets the water.
        public static float Impact(float downSpeed, float horizontalSpeed, float areaFactor = 1)
        {
            var energy = Mathf.Max(0, downSpeed - 1.2f) + horizontalSpeed * 0.15f;
            return Mathf.Clamp01(energy / 8f * Mathf.Lerp(0.7f, 1.3f, Mathf.Clamp01(areaFactor)));
        }

        public static float JumpRadius(WaterInteractionProfile p, float impact) => Mathf.Lerp(p.JumpRippleRadius.x, p.JumpRippleRadius.y, impact);
        public static float JumpSplash(WaterInteractionProfile p, float impact) => Mathf.Lerp(p.JumpSplash.x, p.JumpSplash.y, impact) * p.SplashScale;

        public static float Cooldown(WaterInteractionProfile p, float run) => Mathf.Lerp(p.FootstepCooldown.y, p.FootstepCooldown.x, run);

        /// Courant number of the explicit wave step; must stay below ~0.5 for a stable 2D step.
        public static float Courant(float waveSpeed, float stepSeconds, float cellSize) => waveSpeed * stepSeconds / Mathf.Max(1e-5f, cellSize);
        public static int SubSteps(float waveSpeed, float stepSeconds, float cellSize) => Mathf.Clamp(Mathf.CeilToInt(Courant(waveSpeed, stepSeconds, cellSize) / 0.45f), 1, 8);
    }
}
