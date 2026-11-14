using UnityEngine;

namespace ContextSpeak.Life
{
    /// Where the water reaches on the body. State logic uses points on the (always upright) physics
    /// capsule — feet, knees, hips, chest, head — so posture changes in the animation never feed back
    /// into the state machine. The animated head bone is used separately for breathing and bubbles.
    public sealed class CharacterWaterDetector
    {
        public static readonly float[] Fractions = { 0.03f, 0.28f, 0.53f, 0.72f, 0.93f };
        public static readonly string[] Names = { "Feet", "Knees", "Hips", "Chest", "Head" };

        public Transform Root, HeadBone;
        public float Height = 1.7f;
        public WaterBody Body { get; private set; }
        public float RawSurface { get; private set; } = float.NegativeInfinity;
        /// Water above the feet (m).
        public float WaterLevel { get; private set; }
        /// Water level as a fraction of body height (0 … 1).
        public float Immersion { get; private set; }
        /// Surface minus bed below the character (m).
        public float Depth { get; private set; }
        public float Bed { get; private set; }
        /// Positive = the (animated) head is under water by that much.
        public float HeadDepth { get; private set; } = -2;
        public readonly bool[] Submerged = new bool[5];
        public bool InWater => Body != null && WaterLevel > 0.005f;

        public CharacterWaterDetector(Transform root, float height) { Root = root; Height = height; }

        public void Sample()
        {
            var p = Root.position;
            Body = WaterSurfaceSampler.BodyAt(p, 3);
            if (Body == null)
            {
                RawSurface = float.NegativeInfinity; WaterLevel = 0; Immersion = 0; Depth = 0; HeadDepth = -2;
                for (var i = 0; i < Submerged.Length; i++) Submerged[i] = false;
                return;
            }
            RawSurface = Body.SurfaceHeight(p);
            Bed = Body.BedHeight(p);
            Depth = Mathf.Max(0, RawSurface - Bed);
            WaterLevel = RawSurface - p.y;
            Immersion = Mathf.Clamp01(WaterLevel / Height);
            for (var i = 0; i < Submerged.Length; i++) Submerged[i] = p.y + Fractions[i] * Height < RawSurface;
            var head = HeadBone != null ? HeadBone.position + Vector3.up * 0.06f : p + Vector3.up * Height * Fractions[4];
            HeadDepth = Body.SurfaceHeight(head) - head.y;
        }
    }
}
