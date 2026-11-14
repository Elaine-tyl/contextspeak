using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Buoyancy for a CharacterController body (no Rigidbody, so no competing physics):
    /// a vertical velocity integrated from  a = g·(submerged / equilibrium)·strength − g − damping·v + thrust.
    /// `submerged` is the displaced fraction of the body (water level over body height), so the force
    /// is ρ·g·V_displaced scaled such that the body is in balance when the feet are FloatLine below the
    /// surface. That gives a damped spring around the waterline (period ≈ 2π·√(FloatLine/g) ≈ 2.4 s):
    /// the body settles and bobs, never sinks without input, and rides waves because the surface it
    /// is measured against moves. Rise/sink speeds are capped so it can never shoot out of the water.
    [Serializable]
    public sealed class CharacterBuoyancyController
    {
        public float Gravity = 9.81f;
        public float QuadraticDrag = 0.3f;
        /// Last net acceleration (debug).
        public float Acceleration { get; private set; }
        /// Last displaced fraction 0…1 (debug).
        public float Submerged { get; private set; }

        /// Advances vertical velocity by dt (sub-stepped for stability) and returns it.
        /// surface: smoothed waterline; feetY: bottom of the body; netScale: scales (buoyancy − gravity)
        /// (e.g. near-neutral when swimming under water); thrust: player input (m/s²).
        public float Step(float velocity, float feetY, float surface, float height, float floatLine, float strength,
                          float damping, float maxRise, float maxSink, float thrust, float netScale, float dt)
        {
            if (dt <= 0) return velocity;
            var incoming = velocity;
            var steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 8);
            var h = dt / steps;
            var equilibrium = Mathf.Clamp(floatLine / Mathf.Max(0.5f, height), 0.2f, 0.98f);
            for (var i = 0; i < steps; i++)
            {
                // Within one frame the body moves by velocity·h; use the predicted feet height so the
                // spring is evaluated where the body actually is in this sub-step.
                var y = feetY + velocity * h * i;
                Submerged = Mathf.Clamp01((surface - y) / height);
                var net = Gravity * strength * Submerged / equilibrium - Gravity;
                if (Submerged <= 0) net = -Gravity;                       // in the air above the water: fall
                else net *= netScale;
                // Linear damping plus quadratic form drag (a plunge from a jump is braked hard).
                Acceleration = net - damping * velocity - (Submerged > 0 ? QuadraticDrag * velocity * Mathf.Abs(velocity) : 0) + thrust;
                velocity += Acceleration * h;
            }
            // Buoyancy alone is capped; thrust may push a little beyond (diving, kicking up).
            var riseCap = thrust > 0 ? maxRise * 1.25f : maxRise;
            var sinkCap = thrust < 0 ? maxSink * 1.25f : maxSink;
            // Caps limit what buoyancy/thrust can build up; faster incoming motion (a jump) decays by drag.
            if (Submerged > 0) velocity = Mathf.Clamp(velocity, Mathf.Min(-sinkCap, incoming), Mathf.Max(riseCap, incoming));
            return velocity;
        }
    }
}
