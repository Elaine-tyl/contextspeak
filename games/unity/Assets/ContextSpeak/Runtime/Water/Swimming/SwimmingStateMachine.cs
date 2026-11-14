using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum SwimState
    {
        Grounded, Wading, EnteringWater, SurfaceSwimming, TreadingWater, BackFloating,
        Diving, UnderwaterSwimming, Surfacing, ExitingWater,
    }

    /// What the state machine needs to know this frame (filled by SwimmingController from the detector,
    /// the controller and input). Pure data so the machine can be tested without a scene.
    public struct SwimSense
    {
        public bool InWater;
        /// Water level above the feet as a fraction of body height.
        public float Immersion;
        /// Surface minus bed under the character (m).
        public float Depth;
        /// Positive = head under water by that much (m).
        public float HeadDepth;
        /// Feet height above the bed (m).
        public float FeetAboveBed;
        public bool Grounded;
        public float VerticalSpeed;
        public Vector2 Move;
        public bool Up, Down, ToggleBackFloat;
        /// Out of breath: the body swims up on its own.
        public bool ForceSurface;
    }

    /// Ten swimming states with explicit transition rules. Every rule that depends on a measured value
    /// near a threshold has hysteresis (different enter/exit levels) and must hold for `Delay` seconds,
    /// so standing or floating at the waterline never flickers between states.
    [Serializable]
    public sealed class SwimmingStateMachine
    {
        public float EnterImmersion = 0.72f, ExitImmersion = 0.6f, MinSwimDepth = 1.25f;
        public float Delay = 0.2f, MoveDelay = 0.25f, EnterSeconds = 0.5f, DiveSeconds = 0.55f, SurfaceSeconds = 0.5f;
        public float MoveThreshold = 0.15f;
        [Tooltip("Head this far under water (m) counts as under water; back above it at half of it.")] public float HeadUnderLevel = 0.3f;

        public SwimState State { get; private set; } = SwimState.Grounded;
        public SwimState Previous { get; private set; } = SwimState.Grounded;
        public float TimeInState { get; private set; }
        public event Action<SwimState, SwimState> Changed;

        private SwimState pending;
        private float pendingTime;

        public bool IsSwimming => State >= SwimState.EnteringWater && State <= SwimState.Surfacing;
        /// The swimming controller (not the walking controller) moves the character in these states.
        public bool OwnsMovement => IsSwimming || State == SwimState.ExitingWater;
        public bool AtSurface => State == SwimState.SurfaceSwimming || State == SwimState.TreadingWater || State == SwimState.BackFloating || State == SwimState.EnteringWater || State == SwimState.Surfacing;
        public bool Underwater => State == SwimState.Diving || State == SwimState.UnderwaterSwimming;

        public void Configure(SwimmingProfile profile)
        {
            if (profile == null) return;
            EnterImmersion = profile.EnterImmersion;
            ExitImmersion = Mathf.Min(profile.ExitImmersion, profile.EnterImmersion - 0.05f);
            MinSwimDepth = profile.MinSwimDepth;
            Delay = profile.TransitionDelay;
        }

        /// Scripted transitions (ladder / edge exits, tests).
        public void Force(SwimState state)
        {
            pendingTime = 0;
            Set(state);
        }

        public SwimState Update(in SwimSense s, float dt)
        {
            TimeInState += dt;
            var moving = s.Move.sqrMagnitude > MoveThreshold * MoveThreshold;
            var deepEnough = s.Depth >= MinSwimDepth;
            var shallow = s.Depth < MinSwimDepth - 0.1f;          // hysteresis on depth
            var standable = shallow && (s.FeetAboveBed < 0.15f || s.Immersion < ExitImmersion);
            switch (State)
            {
                case SwimState.Grounded:
                    if (s.InWater && deepEnough && s.Immersion > EnterImmersion) Want(SwimState.EnteringWater, s.Grounded ? Delay : 0, dt);
                    else if (s.InWater && s.Immersion > 0.02f) Want(SwimState.Wading, Delay * 0.5f, dt);
                    else Hold();
                    break;
                case SwimState.Wading:
                    if (!s.InWater || s.Immersion < 0.01f) Want(SwimState.Grounded, Delay, dt);
                    else if (deepEnough && s.Immersion > EnterImmersion) Want(SwimState.EnteringWater, s.Grounded ? Delay : 0, dt);
                    else Hold();
                    break;
                case SwimState.EnteringWater:
                    if (s.Down) Want(SwimState.Diving, 0, dt);
                    else if (TimeInState >= EnterSeconds) Want(moving ? SwimState.SurfaceSwimming : SwimState.TreadingWater, 0, dt);
                    else Hold();
                    break;
                case SwimState.SurfaceSwimming:
                case SwimState.TreadingWater:
                case SwimState.BackFloating:
                    if (!s.InWater) Want(SwimState.ExitingWater, Delay, dt);
                    else if (standable && !s.ForceSurface) Want(SwimState.ExitingWater, Delay, dt);
                    else if (s.Down && !s.ForceSurface) Want(SwimState.Diving, 0, dt);
                    else if (s.ToggleBackFloat) Want(State == SwimState.BackFloating ? SwimState.TreadingWater : SwimState.BackFloating, 0, dt);
                    else if (State == SwimState.SurfaceSwimming && !moving) Want(SwimState.TreadingWater, MoveDelay, dt);
                    else if (State == SwimState.TreadingWater && moving) Want(SwimState.SurfaceSwimming, MoveDelay * 0.5f, dt);
                    else if (State != SwimState.BackFloating && s.HeadDepth > HeadUnderLevel + 0.2f) Want(SwimState.UnderwaterSwimming, Delay, dt); // pushed under by a wave
                    else Hold();
                    break;
                case SwimState.Diving:
                    if (s.HeadDepth > HeadUnderLevel || TimeInState >= DiveSeconds) Want(SwimState.UnderwaterSwimming, 0, dt);
                    else Hold();
                    break;
                case SwimState.UnderwaterSwimming:
                    if (!s.InWater) Want(SwimState.ExitingWater, Delay, dt);
                    else if (s.HeadDepth < HeadUnderLevel * 0.5f && !s.Down && s.VerticalSpeed > -0.2f) Want(SwimState.Surfacing, Delay * 0.5f, dt);
                    else if (standable && s.HeadDepth < 0) Want(SwimState.ExitingWater, Delay, dt);
                    else Hold();
                    break;
                case SwimState.Surfacing:
                    if (s.Down && !s.ForceSurface) Want(SwimState.Diving, 0, dt);
                    else if (TimeInState >= SurfaceSeconds) Want(moving ? SwimState.SurfaceSwimming : SwimState.TreadingWater, 0, dt);
                    else Hold();
                    break;
                case SwimState.ExitingWater:
                    // Left by the controller (Release) once the body stands; back to swimming if it gets deep again.
                    if (s.InWater && deepEnough && s.Immersion > EnterImmersion + 0.05f && !s.Grounded) Want(SwimState.TreadingWater, Delay, dt);
                    else Hold();
                    break;
            }
            return State;
        }

        /// The walking controller takes over again (standing in shallow water or on land).
        public void Release(bool inWater) => Force(inWater ? SwimState.Wading : SwimState.Grounded);

        private void Hold() { pendingTime = 0; }

        private void Want(SwimState state, float delay, float dt)
        {
            if (state == State) { Hold(); return; }
            if (pending != state) { pending = state; pendingTime = 0; }
            pendingTime += dt;
            if (pendingTime >= delay) Set(state);
        }

        private void Set(SwimState state)
        {
            if (state == State) return;
            Previous = State;
            State = state;
            TimeInState = 0;
            pendingTime = 0;
            Changed?.Invoke(Previous, State);
        }
    }
}
