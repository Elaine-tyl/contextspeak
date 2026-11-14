using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum WaterContact { Dry, FeetSubmerged, KneeSubmerged, WaistSubmerged, Swimming, Underwater, ExitingWater }

    /// Immersion state with hysteresis and a minimum dwell time, so standing at a waterline never
    /// flickers between states. Depth = water surface − feet height (m); headDepth = surface − head.
    [Serializable]
    public sealed class ImmersionStateMachine
    {
        public float Height = 1.7f, Hysteresis = 0.08f, MinDwell = 0.2f, ExitSeconds = 1.2f;
        public WaterContact State { get; private set; } = WaterContact.Dry;
        private float dwell = 99, exitTimer;

        public float FeetLevel => 0.03f;
        public float KneeLevel => 0.27f * Height;
        public float WaistLevel => 0.55f * Height;
        public float ChestLevel => 0.74f * Height;

        public static int Level(WaterContact state) => state switch
        {
            WaterContact.FeetSubmerged => 1, WaterContact.KneeSubmerged => 2, WaterContact.WaistSubmerged => 3,
            WaterContact.Swimming => 4, WaterContact.Underwater => 5, _ => 0,
        };

        public void Reset() { State = WaterContact.Dry; dwell = 99; exitTimer = 0; }

        public WaterContact Update(float depth, float headDepth, bool swimming, float dt)
        {
            dwell += dt;
            var current = Level(State);
            float At(int level, float threshold) => current >= level ? threshold - Hysteresis : threshold;
            var level = 0;
            if (depth > At(1, FeetLevel)) level = 1;
            if (depth > At(2, KneeLevel)) level = 2;
            if (depth > At(3, WaistLevel)) level = 3;
            if (swimming || depth > At(4, ChestLevel)) level = Mathf.Max(level, 4);
            if (level >= 4 && headDepth > At(5, 0.12f)) level = 5;
            WaterContact wanted;
            if (level == 0)
            {
                if (State == WaterContact.Dry) wanted = WaterContact.Dry;
                else if (State == WaterContact.ExitingWater) { exitTimer -= dt; wanted = exitTimer <= 0 ? WaterContact.Dry : WaterContact.ExitingWater; }
                else wanted = WaterContact.ExitingWater;
            }
            else wanted = level switch { 1 => WaterContact.FeetSubmerged, 2 => WaterContact.KneeSubmerged, 3 => WaterContact.WaistSubmerged, 4 => WaterContact.Swimming, _ => WaterContact.Underwater };
            if (wanted == State || dwell < MinDwell && !(State == WaterContact.ExitingWater && wanted == WaterContact.Dry)) return State;
            if (wanted == WaterContact.ExitingWater) exitTimer = ExitSeconds;
            State = wanted;
            dwell = 0;
            return State;
        }
    }

    /// One foot meeting the water: an event when the foot crosses the surface on the way down, or
    /// drags quickly through it — never while it stands still (velocity-aware fallback for rigs
    /// without animation events).
    [Serializable]
    public sealed class FootContactTracker
    {
        private float since = 99;
        private bool under, started;
        private Vector3 last;

        /// Returns 0 (nothing) or the event strength 0…1.
        public float Update(Vector3 foot, float surface, float dt, float cooldown)
        {
            since += dt;
            var speed = started && dt > 0 ? (foot - last).magnitude / dt : 0;
            last = foot;
            started = true;
            var nowUnder = foot.y < surface - 0.01f;
            var strength = 0f;
            if (nowUnder && !under && since >= cooldown && speed > 0.15f) strength = Mathf.Clamp01(speed / 4f) * 0.8f + 0.2f;
            else if (nowUnder && since >= cooldown * 2.5f && speed > 0.9f) strength = Mathf.Clamp01(speed / 6f) * 0.6f;
            under = nowUnder;
            if (strength > 0) since = 0;
            return strength;
        }
    }

    /// Finds the body markers of a character (feet bones when the rig has them, otherwise points at
    /// the right fractions of the character's height) and samples the water against them.
    public sealed class WaterContactDetector
    {
        public Transform Root, LeftFoot, RightFoot, Head;
        public float Height = 1.7f;
        public WaterBody Body { get; private set; }
        public float Surface { get; private set; } = float.MinValue;
        public float Depth { get; private set; } = -1;
        public float HeadDepth { get; private set; } = -2;

        public WaterContactDetector(Transform root, float height)
        {
            Root = root;
            Height = height;
            FindBones();
        }

        public void FindBones()
        {
            LeftFoot = RightFoot = Head = null;
            foreach (var bone in Root.GetComponentsInChildren<Transform>(true))
            {
                var name = bone.name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace(":", "");
                if (LeftFoot == null && (name.EndsWith("leftfoot") || name.EndsWith("footl") || name.EndsWith("lfoot") || name == "foot.l")) LeftFoot = bone;
                else if (RightFoot == null && (name.EndsWith("rightfoot") || name.EndsWith("footr") || name.EndsWith("rfoot") || name == "foot.r")) RightFoot = bone;
                else if (Head == null && name.EndsWith("head") && !name.Contains("top")) Head = bone;
            }
        }

        public bool HasFeet => LeftFoot != null && RightFoot != null;
        public Vector3 HeadPoint => Head != null ? Head.position + Vector3.up * 0.1f : Root.position + Vector3.up * Height * 0.95f;
        public Vector3 BodyPoint(float fraction) => Root.position + Vector3.up * Height * fraction;

        public void Sample()
        {
            var p = Root.position;
            Body = WaterBody.Find(p, 2.5f);
            if (Body == null) { Surface = float.MinValue; Depth = -1; HeadDepth = -2; return; }
            Surface = Body.SurfaceHeight(p);
            Depth = Surface - p.y;
            HeadDepth = Surface - HeadPoint.y;
        }
    }
}
