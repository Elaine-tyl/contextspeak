using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Live multipliers and overrides shared by the sky, weather, wind, water and vegetation.
    /// Written by WeatherManager, EnvironmentManager (lighting zones) and the developer panel;
    /// read by SkySystem every frame. Defaults leave the look exactly as authored.
    public static class EnvironmentTuning
    {
        public static float SunIntensity = 1;          // × sun
        public static float SunTemperatureOffset;      // Kelvin added to the physically based sun colour
        public static float ShadowStrength = 1;        // × shadow strength
        public static float ExposureOffset;            // EV, developer
        public static float ZoneExposure;              // EV, indoor/outdoor adaptation (EnvironmentManager)
        public static float FogDensity = 0.4f;            // × fog
        public static float CloudOverride = -1;        // −1 = from weather
        public static float RainScale = 1;             // × rain (weather states: light / heavy)
        public static float RainOverride = -1;         // −1 = from weather (developer slider)
        public static float WindScale = 1;             // × wind
        public static float WaveStrength = 1;          // × ocean amplitude
        public static float GrassDensity = 1;          // 0…1 share of small plants drawn
        public static float IndoorLights = 1;          // × interior and decorative lights
        public static float BloomIntensity = 0.12f;
        public static float AmbientScale = 1;          // × ambient light (zones)
        public static float ExtraFog;                  // added fog multiplier (foggy morning)

        public static event Action Changed;
        public static void Raise() => Changed?.Invoke();

        public static void Reset()
        {
            SunIntensity = 1; SunTemperatureOffset = 0; ShadowStrength = 1; ExposureOffset = 0; ZoneExposure = 0; FogDensity = 0.4f;
            CloudOverride = -1; RainScale = 1; RainOverride = -1; WindScale = 1; WaveStrength = 1; GrassDensity = 1; IndoorLights = 1;
            BloomIntensity = 0.12f; AmbientScale = 1; ExtraFog = 0;
            Raise();
        }

        /// Sunlight colour temperature from the sun's elevation: ~2000 K on the horizon, ~4300 K at
        /// 10°, ~5600 K at 25° and ~6000 K high in the sky (direct beam, before atmospheric haze).
        public static float SunKelvin(float elevation)
        {
            var e = Mathf.Max(elevation, -2);
            var k = e < 10 ? Mathf.Lerp(2000, 4300, Mathf.InverseLerp(-2, 10, e))
                : e < 25 ? Mathf.Lerp(4300, 5600, Mathf.InverseLerp(10, 25, e))
                : Mathf.Lerp(5600, 6000, Mathf.InverseLerp(25, 60, e));
            return Mathf.Clamp(k + SunTemperatureOffset, 1500, 12000);
        }
    }
}
