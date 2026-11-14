using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum SkyWeather { Clear, Cloudy, Rain, Overcast }

    /// The seven looks the system is tuned and tested for (time of day + weather).
    public enum SkyPreset { ClearMorning, BrightAfternoon, GoldenSunset, Cloudy, Rainy, ClearNight, OvercastNight }

    /// What gameplay reads about the sky: time, light level and weather, updated by SkySystem.
    /// Systems without a SkySystem in the scene see a sensible daytime default.
    public static class SkyState
    {
        public static float Hour { get; internal set; } = 12;
        public static float SunElevation { get; internal set; } = 50;     // degrees
        /// 0 = full night, 1 = full daylight (civil twilight in between).
        public static float Daylight { get; internal set; } = 1;
        public static SkyWeather Weather { get; internal set; } = SkyWeather.Clear;
        public static float CloudCover { get; internal set; } = 0.3f;
        public static float Rain { get; internal set; }                   // 0..1 current rain intensity
        public static float Wetness { get; internal set; }                // 0..1 surfaces (dries slowly)
        public static float WindSpeed { get; internal set; } = 3;         // m/s
        public static Vector2 WindDirection { get; internal set; } = new Vector2(0.8f, 0.6f);

        public static bool IsNight => Daylight < 0.15f;
        public static bool Raining => Rain > 0.25f;
        /// Street lamps, headlights and window glow: dark, or very gloomy under heavy cloud.
        public static float LightsNeeded => Mathf.Clamp01(Mathf.Max(1 - Daylight * 1.6f, (CloudCover - 0.75f) * 2f + Rain * 0.5f - Daylight * 0.2f));
        /// Share of pedestrians outdoors (fewer at night and in the rain).
        public static float Crowd => Mathf.Clamp01(Mathf.Lerp(0.3f, 1f, Daylight) * (1 - Rain * 0.6f));
        /// NPCs with walking routines shelter (stay at their post) at night or in rain.
        public static bool Sheltering => IsNight || Raining;
        public static string Clock => ((int)Hour).ToString("00") + ":" + ((int)(Hour % 1 * 60)).ToString("00");

        public static event Action Changed;            // weather target changed or the hour turned
        internal static void Raise() => Changed?.Invoke();
        public static void ResetDefaults() { Hour = 12; SunElevation = 50; Daylight = 1; Weather = SkyWeather.Clear; CloudCover = 0.3f; Rain = 0; Wetness = 0; }
    }

    /// Pure sun/moon maths (unit-tested in SkyChecks).
    public static class SkyMath
    {
        /// Solar elevation in degrees: 0 at sunrise/sunset, MaxElevation at solar noon, negative at night.
        public static float SunElevation(float hour, float sunrise, float sunset, float maxElevation, float maxDepression = 38)
        {
            hour = Mathf.Repeat(hour, 24);
            if (hour >= sunrise && hour <= sunset)
                return maxElevation * Mathf.Sin(Mathf.PI * (hour - sunrise) / (sunset - sunrise));
            var nightLength = 24 - (sunset - sunrise);
            var sinceSunset = Mathf.Repeat(hour - sunset, 24);
            return -maxDepression * Mathf.Sin(Mathf.PI * sinceSunset / nightLength);
        }

        /// Compass azimuth (degrees): east at sunrise, south-ish at noon, west at sunset, continuing under the horizon.
        public static float SunAzimuth(float hour, float sunrise, float sunset)
        {
            hour = Mathf.Repeat(hour, 24);
            if (hour >= sunrise && hour <= sunset) return 90 + 180 * (hour - sunrise) / (sunset - sunrise);
            var nightLength = 24 - (sunset - sunrise);
            return 270 + 180 * Mathf.Repeat(hour - sunset, 24) / nightLength;
        }

        /// 0 night … 1 day from the solar elevation (civil twilight −6° … +6°).
        public static float Daylight(float elevation) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 6, elevation));
    }
}
