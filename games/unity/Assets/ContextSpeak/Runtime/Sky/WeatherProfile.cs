using UnityEngine;

namespace ContextSpeak.Life
{
    /// How one kind of weather looks and sounds. Values blend smoothly when the weather changes.
    [CreateAssetMenu(menuName = "ContextSpeak/Weather Profile", fileName = "Weather")]
    public sealed class WeatherProfile : ScriptableObject
    {
        public SkyWeather Weather = SkyWeather.Clear;
        [Range(0, 1)] public float CloudCover = 0.25f;
        [Range(0, 1.5f)] public float CloudDensity = 0.85f;
        [Range(0, 1)] public float CloudDarkness = 0.1f;      // grey underside of rain clouds
        [Range(0, 1.5f)] public float SunIntensity = 1;        // multiplies the clear-sky sun
        [Range(0, 1)] public float SkyDesaturation = 0;        // overcast greys out the blue
        [Range(0, 6)] public float FogMultiplier = 1;
        [Range(0, 1)] public float Rain;
        [Range(0, 3)] public float WindMultiplier = 1;
        [Range(0, 1)] public float StarVisibility = 1;
        [Range(-1, 1)] public float ExposureOffset;

        public WeatherValues Values => new WeatherValues
        {
            Cover = CloudCover, Density = CloudDensity, Darkness = CloudDarkness, Sun = SunIntensity, Desaturate = SkyDesaturation,
            Fog = FogMultiplier, Rain = Rain, Wind = WindMultiplier, Stars = StarVisibility, Exposure = ExposureOffset,
        };

        public static WeatherValues Default(SkyWeather weather)
        {
            switch (weather)
            {
                case SkyWeather.Cloudy: return new WeatherValues { Cover = 0.62f, Density = 0.95f, Darkness = 0.25f, Sun = 0.7f, Desaturate = 0.25f, Fog = 1.4f, Rain = 0, Wind = 1.4f, Stars = 0.35f, Exposure = 0.1f };
                case SkyWeather.Rain: return new WeatherValues { Cover = 0.95f, Density = 1.25f, Darkness = 0.7f, Sun = 0.22f, Desaturate = 0.75f, Fog = 3.2f, Rain = 1, Wind = 2.0f, Stars = 0, Exposure = 0.25f };
                case SkyWeather.Overcast: return new WeatherValues { Cover = 0.88f, Density = 1.1f, Darkness = 0.45f, Sun = 0.35f, Desaturate = 0.6f, Fog = 2.0f, Rain = 0, Wind = 1.2f, Stars = 0.05f, Exposure = 0.2f };
                default: return new WeatherValues { Cover = 0.22f, Density = 0.8f, Darkness = 0.05f, Sun = 1, Desaturate = 0, Fog = 1, Rain = 0, Wind = 1, Stars = 1, Exposure = 0 };
            }
        }
    }

    [System.Serializable]
    public struct WeatherValues
    {
        public float Cover, Density, Darkness, Sun, Desaturate, Fog, Rain, Wind, Stars, Exposure;

        public static WeatherValues Lerp(WeatherValues a, WeatherValues b, float t) => new WeatherValues
        {
            Cover = Mathf.Lerp(a.Cover, b.Cover, t), Density = Mathf.Lerp(a.Density, b.Density, t), Darkness = Mathf.Lerp(a.Darkness, b.Darkness, t),
            Sun = Mathf.Lerp(a.Sun, b.Sun, t), Desaturate = Mathf.Lerp(a.Desaturate, b.Desaturate, t), Fog = Mathf.Lerp(a.Fog, b.Fog, t),
            Rain = Mathf.Lerp(a.Rain, b.Rain, t), Wind = Mathf.Lerp(a.Wind, b.Wind, t), Stars = Mathf.Lerp(a.Stars, b.Stars, t), Exposure = Mathf.Lerp(a.Exposure, b.Exposure, t),
        };
    }
}
