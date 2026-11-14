using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Smooth surface tracking: a filtered waterline that follows waves (by WaveFollow) without jitter,
    /// plus a gentle two-tone bob so a still swimmer is never frozen at a fixed height.
    [Serializable]
    public sealed class WaterSurfaceFollower
    {
        public float Smoothing = 0.25f, BobAmplitude = 0.035f, BobFrequency = 0.42f;
        [Range(0, 1)] public float WaveFollow = 0.4f;
        public float Height { get; private set; }
        public float Raw { get; private set; }
        private float velocity, phase;
        private bool started;

        public void Reset() { started = false; velocity = 0; }

        /// raw: sampled surface at the body; calm: the still-water level (sea level for the ocean).
        public float Update(float raw, float calm, float dt)
        {
            Raw = raw;
            var target = Mathf.Lerp(calm, raw, WaveFollow);
            if (!started) { Height = target; started = true; phase = UnityEngine.Random.value * 6.28f; }
            Height = Mathf.SmoothDamp(Height, target, ref velocity, Mathf.Max(0.01f, Smoothing), Mathf.Infinity, Mathf.Max(dt, 1e-5f));
            phase += dt * BobFrequency * Mathf.PI * 2;
            return Height + Bob;
        }

        public float Bob => BobAmplitude * (Mathf.Sin(phase) * 0.75f + Mathf.Sin(phase * 2.31f + 1.3f) * 0.25f);
    }
}
