using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Eight gameplay weather states on top of SkySystem's four visual weathers. Each state is a
    /// sky weather plus multipliers (rain amount, wind, fog, cloud cover) that blend smoothly over
    /// TransitionSeconds — clouds, light, fog, wetness, rain audio, wind on plants and water, and
    /// NPC sheltering all follow because they read SkyState.
    public sealed class WeatherManager : MonoBehaviour
    {
        public enum State { Clear, PartlyCloudy, Overcast, LightRain, HeavyRain, Windy, FoggyMorning, ClearNight }

        [Serializable]
        public struct Look
        {
            public SkyWeather Sky;
            public float Cloud;      // −1 = from the sky weather
            public float RainScale, WindScale, ExtraFog;
            public Look(SkyWeather sky, float cloud, float rain, float wind, float fog) { Sky = sky; Cloud = cloud; RainScale = rain; WindScale = wind; ExtraFog = fog; }
        }

        public static WeatherManager Active { get; private set; }
        public static event Action<State> Changed;

        public State Current = State.Clear;
        [Min(1)] public float TransitionSeconds = 30;
        [Tooltip("Pick weather by itself every few in-game hours (off: only scripts and the developer panel change it).")]
        public bool Automatic;
        public Vector2 HoldHours = new Vector2(2, 6);

        private Look from, to;
        private float blend = 1, nextChangeHour = -1;

        public static Look Describe(State state)
        {
            switch (state)
            {
                case State.PartlyCloudy: return new Look(SkyWeather.Cloudy, 0.45f, 1, 1.1f, 0);
                case State.Overcast: return new Look(SkyWeather.Overcast, -1, 1, 1.0f, 0.3f);
                case State.LightRain: return new Look(SkyWeather.Rain, 0.85f, 0.4f, 1.0f, 0);
                case State.HeavyRain: return new Look(SkyWeather.Rain, -1, 1.0f, 1.5f, 0.8f);
                case State.Windy: return new Look(SkyWeather.Cloudy, 0.35f, 1, 2.6f, 0);
                case State.FoggyMorning: return new Look(SkyWeather.Overcast, 0.7f, 1, 0.35f, 6f);
                case State.ClearNight: return new Look(SkyWeather.Clear, 0.1f, 1, 0.7f, 0);
                default: return new Look(SkyWeather.Clear, -1, 1, 1, 0);
            }
        }

        private void OnEnable()
        {
            Active = this;
            from = to = Describe(Current);
            blend = 1;
            Write(to);
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            EnvironmentTuning.CloudOverride = -1; EnvironmentTuning.RainScale = 1; EnvironmentTuning.WindScale = 1; EnvironmentTuning.ExtraFog = 0;
        }

        /// Change weather smoothly (or instantly for tests).
        public void Set(State state, bool instant = false)
        {
            var look = Describe(state);
            Current = state;
            from = Interpolated();
            to = look;
            blend = instant ? 1 : 0;
            if (SkySystem.Active != null)
            {
                if (state == State.ClearNight && SkyState.Daylight > 0.2f) SkySystem.Active.SetTime(22.5f, instant);
                if (state == State.FoggyMorning && (SkyState.Hour < 5 || SkyState.Hour > 10)) SkySystem.Active.SetTime(6.8f, instant);
                if (instant) SkySystem.Active.Weather = look.Sky;
                SkySystem.Active.SetWeather(look.Sky, instant ? 0.01f : TransitionSeconds);
                if (instant) SkySystem.Active.ApplyNow();
            }
            Write(instant ? to : from);
            if (instant && SkySystem.Active != null) SkySystem.Active.ApplyNow();
            Changed?.Invoke(state);
        }

        private static float Cover(Look look) => look.Cloud >= 0 ? look.Cloud : WeatherProfile.Default(look.Sky).Cover;

        private Look Interpolated()
        {
            var t = Mathf.SmoothStep(0, 1, blend);
            if (t >= 1) return to;
            return new Look(to.Sky, Mathf.Lerp(Cover(from), Cover(to), t), Mathf.Lerp(from.RainScale, to.RainScale, t),
                Mathf.Lerp(from.WindScale, to.WindScale, t), Mathf.Lerp(from.ExtraFog, to.ExtraFog, t));
        }

        private static void Write(Look look)
        {
            EnvironmentTuning.CloudOverride = look.Cloud;
            EnvironmentTuning.RainScale = look.RainScale;
            EnvironmentTuning.WindScale = look.WindScale;
            EnvironmentTuning.ExtraFog = look.ExtraFog;
        }

        private void Update()
        {
            if (blend < 1)
            {
                blend = Mathf.MoveTowards(blend, 1, Time.deltaTime / TransitionSeconds);
                Write(Interpolated());
            }
            if (!Automatic || SkySystem.Active == null) return;
            if (nextChangeHour < 0) nextChangeHour = Mathf.Repeat(SkyState.Hour + UnityEngine.Random.Range(HoldHours.x, HoldHours.y), 24);
            if (Mathf.Abs(Mathf.DeltaAngle(SkyState.Hour * 15, nextChangeHour * 15)) < 2)
            {
                nextChangeHour = -1;
                var roll = UnityEngine.Random.value;
                var night = SkyState.IsNight;
                var morning = SkyState.Hour > 5 && SkyState.Hour < 8.5f;
                Set(morning && roll < 0.2f ? State.FoggyMorning : roll < 0.35f ? (night ? State.ClearNight : State.Clear) : roll < 0.55f ? State.PartlyCloudy
                    : roll < 0.7f ? State.Overcast : roll < 0.83f ? State.LightRain : roll < 0.92f ? State.Windy : State.HeavyRain);
            }
        }
    }
}
