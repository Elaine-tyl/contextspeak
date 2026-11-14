using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// The day as gameplay sees it: a facade over SkySystem's clock (which moves the sun, moon, sky,
    /// ambient light, fog and reflections together) that names the phase of the day and raises an
    /// event when it changes. Other systems should read time from here or from SkyState.
    public sealed class DayNightCycle : MonoBehaviour
    {
        public enum Phase { Night, BlueHourMorning, Sunrise, Morning, Midday, Afternoon, GoldenHour, Sunset, BlueHourEvening }

        public static DayNightCycle Active { get; private set; }
        public static event Action<Phase> PhaseChanged;
        public Phase Current { get; private set; } = Phase.Midday;

        public float TimeOfDay
        {
            get => SkySystem.Active != null ? SkySystem.Active.TimeOfDay : SkyState.Hour;
            set { if (SkySystem.Active != null) SkySystem.Active.SetTime(value, false); }
        }

        public float DayLengthMinutes
        {
            get => SkySystem.Active != null ? SkySystem.Active.DayLengthMinutes : 24;
            set { if (SkySystem.Active != null) SkySystem.Active.DayLengthMinutes = Mathf.Max(0.5f, value); }
        }

        public bool Paused
        {
            get => SkySystem.Active != null && SkySystem.Active.Paused;
            set { if (SkySystem.Active != null) SkySystem.Active.Paused = value; }
        }

        private void OnEnable() { Active = this; Current = Classify(SkyState.Hour, SkyState.SunElevation); }
        private void OnDisable() { if (Active == this) Active = null; }

        private void Update()
        {
            var next = Classify(SkyState.Hour, SkyState.SunElevation);
            if (next == Current) return;
            Current = next;
            PhaseChanged?.Invoke(next);
        }

        /// Phase from the clock and the sun's elevation (degrees).
        public static Phase Classify(float hour, float elevation)
        {
            var morning = hour < 12;
            if (elevation < -6) return Phase.Night;
            if (elevation < 0) return morning ? Phase.BlueHourMorning : Phase.BlueHourEvening;
            if (elevation < 6) return morning ? Phase.Sunrise : Phase.Sunset;
            if (elevation < 15) return morning ? Phase.Morning : Phase.GoldenHour;
            if (hour >= 11 && hour < 14) return Phase.Midday;
            return morning ? Phase.Morning : Phase.Afternoon;
        }

        public static string Describe(Phase phase)
        {
            switch (phase)
            {
                case Phase.BlueHourMorning: return "Blue hour (dawn)";
                case Phase.BlueHourEvening: return "Blue hour (dusk)";
                case Phase.GoldenHour: return "Golden hour";
                default: return phase.ToString();
            }
        }
    }
}
