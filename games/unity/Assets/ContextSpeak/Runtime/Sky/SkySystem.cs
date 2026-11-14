using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Life
{
    /// Time of day, sun and moon, procedural sky, clouds (sky + cloud shadows), ambient light, fog,
    /// exposure, sky reflections, rain and wetness for one scene (URP). Weather changes blend
    /// smoothly. In the Forest Café it follows the existing WorldClock so fishing and the café's
    /// weather logic stay in step with what you see.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SkySystem : MonoBehaviour
    {
        public static SkySystem Active { get; private set; }
        /// Set while something else owns the fog (e.g. the camera is under water at the beach).
        public static bool FogLocked;

        [Header("Time")]
        [Range(0, 24)] public float TimeOfDay = 8.5f;
        [Tooltip("Real minutes for one 24-hour game day (24 = one game minute per real second).")]
        [Min(0.5f)] public float DayLengthMinutes = 24;
        public bool Paused;
        public bool AllowNight = true;
        [Range(4, 8)] public float SunriseHour = 6.2f;
        [Range(16, 21)] public float SunsetHour = 18.6f;
        [Range(20, 85)] public float MaxSunElevation = 62;
        [Tooltip("Compass rotation of the sun path for this scene.")] public float NorthOffset = -38;
        [Range(0, 1)] public float MoonPhase = 0.82f;
        [Tooltip("Follow the Forest Café WorldClock (time + weather) when present.")] public bool SyncWithForestClock = true;

        [Header("Weather")]
        public SkyWeather Weather = SkyWeather.Clear;
        [Min(0.1f)] public float TransitionSeconds = 25;
        public bool AutoWeather = true;
        public Vector2 WeatherHoldHours = new Vector2(2, 6);
        [Range(-1, 1), Tooltip("-1 = from the weather; otherwise forces the cloud coverage.")] public float CloudCoverageOverride = -1;
        [Min(0)] public float WindSpeed = 4;      // m/s at ground level
        [Range(0, 360)] public float WindDirection = 60;
        public WeatherProfile[] Profiles = new WeatherProfile[0];

        [Header("Scene wiring (set by ContextSpeak > Sky & Weather > Install)")]
        public Light Sun;
        public Light Moon;
        public Material SkyMaterial;
        public ParticleSystem Rain;
        public ReflectionProbe SkyProbe;
        public Volume ExposureVolume;
        public Texture[] CloudCookies = new Texture[0];   // light → heavy cloud shadows
        public float CookieSize = 220;
        public float BaseFogDensity = 0.004f;
        public float BaseExposure;
        public bool Interior;                             // hospital/restaurant: softer outdoor fog
        [Header("Optional HDRI skies (cubemaps from .hdr files)")]
        public Cubemap HdriDay, HdriSunset, HdriNight;
        [Range(0, 1)] public float HdriStrength = 0.85f;
        [Range(0, 360)] public float HdriRotation;

        [Header("Colour by sun elevation (−18° … 60°)")]
        public Gradient Zenith, Horizon, SunColour, AmbientSky, AmbientEquator, AmbientGround;
        [SerializeField, HideInInspector] private bool gradientsReady;
        [SerializeField, HideInInspector] private int lookVersion;
        private const int LookVersion = 2;   // 2: calibrated ambient (realistic lighting upgrade)

        private WeatherValues from, to, current;
        private float blend = 1, nextWeatherHour = -1, probeTimer, wetness;
        private Vector4 cloudOffset;
        private Vector2 cookieOffset;
        private float targetTime = -1, tweenStart, tweenLength, tweenElapsed;
        private SkyWeather appliedWeather = (SkyWeather)(-1);
        private int lastHour = -1;
        private ColorAdjustments exposure;
        private ContextSpeak.Slice.SliceGame forestGame;
        private System.Random random = new System.Random(20261113);

        private static readonly int ZenithId = Shader.PropertyToID("_ZenithColor"), HorizonId = Shader.PropertyToID("_HorizonColor"), GroundId = Shader.PropertyToID("_GroundColor"),
            SunDirId = Shader.PropertyToID("_SunDir"), SunColorId = Shader.PropertyToID("_SunColor"), SunGlowId = Shader.PropertyToID("_SunGlow"), SunsetGlowId = Shader.PropertyToID("_SunsetGlow"),
            MoonDirId = Shader.PropertyToID("_MoonDir"), MoonColorId = Shader.PropertyToID("_MoonColor"), MoonPhaseId = Shader.PropertyToID("_MoonPhase"), StarsId = Shader.PropertyToID("_StarIntensity"),
            CoverId = Shader.PropertyToID("_CloudCover"), DensityId = Shader.PropertyToID("_CloudDensity"), LitId = Shader.PropertyToID("_CloudLit"), ShadeId = Shader.PropertyToID("_CloudShade"),
            OffsetId = Shader.PropertyToID("_CloudOffset"), ExposureId = Shader.PropertyToID("_Exposure"), HdriId = Shader.PropertyToID("_Hdri"), HdriBlendId = Shader.PropertyToID("_HdriBlend"),
            HdriRotationId = Shader.PropertyToID("_HdriRotation");

        public WeatherValues Current => current;
        public float Wetness => wetness;

        private void OnEnable()
        {
            Active = this;
            EnsureGradients();
            from = to = current = Values(Weather);
            appliedWeather = Weather;
            blend = 1;
            if (ExposureVolume != null && ExposureVolume.sharedProfile != null) ExposureVolume.sharedProfile.TryGet(out exposure);
            ApplyNow();
        }

        private void OnDisable() { if (Active == this) Active = null; }

        private void OnValidate()
        {
            EnsureGradients();
            if (!Application.isPlaying && isActiveAndEnabled) { from = to = current = Values(Weather); appliedWeather = Weather; blend = 1; ApplyNow(); }
        }

        // ------------------------------------------------------------------ public control
        public void SetWeather(SkyWeather weather, float seconds = -1)
        {
            if (weather == appliedWeather && blend >= 1) return;
            Weather = weather;
            from = current;
            to = Values(weather);
            appliedWeather = weather;
            blend = 0;
            TransitionSeconds = seconds > 0 ? seconds : TransitionSeconds;
            SkyState.Weather = weather;
            SkyState.Raise();
            if (Application.isPlaying) WriteClock();
        }

        public static (float hour, SkyWeather weather) Describe(SkyPreset preset)
        {
            switch (preset)
            {
                case SkyPreset.ClearMorning: return (8.0f, SkyWeather.Clear);
                case SkyPreset.BrightAfternoon: return (14.5f, SkyWeather.Clear);
                case SkyPreset.GoldenSunset: return (18.25f, SkyWeather.Clear);
                case SkyPreset.Cloudy: return (12.5f, SkyWeather.Cloudy);
                case SkyPreset.Rainy: return (15.5f, SkyWeather.Rain);
                case SkyPreset.ClearNight: return (23.0f, SkyWeather.Clear);
                default: return (1.5f, SkyWeather.Overcast);
            }
        }

        /// Move to one of the seven looks: smoothly (time runs forward, weather blends) or instantly.
        public void ApplyPreset(SkyPreset preset, bool instant = false)
        {
            var (hour, weather) = Describe(preset);
            if (instant)
            {
                TimeOfDay = hour;
                targetTime = -1;
                Weather = weather;
                from = to = current = Values(weather);
                appliedWeather = weather;
                blend = 1;
                wetness = current.Rain > 0 ? 0.9f : 0;
                ApplyNow();
                WriteClock();
                return;
            }
            SetTime(hour, false);
            SetWeather(weather);
        }

        /// Editor/test hook: show the look part-way through a weather change.
        public void PreviewBlend(SkyWeather a, SkyWeather b, float t)
        {
            from = Values(a);
            to = Values(b);
            blend = Mathf.Clamp(t, 0, 0.999f);
            current = WeatherValues.Lerp(from, to, Mathf.SmoothStep(0, 1, blend));
            Weather = b;
            appliedWeather = b;
            wetness = current.Rain * 0.8f;
            Apply();
            UpdateRain(0.5f);
            if (SkyProbe != null) SkyProbe.RenderProbe();
        }

        public void SetTime(float hour, bool instant = true)
        {
            hour = Mathf.Repeat(hour, 24);
            if (instant) { TimeOfDay = hour; targetTime = -1; ApplyNow(); WriteClock(); return; }
            targetTime = hour;
            tweenStart = TimeOfDay;
            tweenLength = Mathf.Repeat(hour - TimeOfDay, 24);
            tweenElapsed = 0;
        }

        // ------------------------------------------------------------------ update
        private void Update()
        {
            if (!Application.isPlaying) return;
            var dt = Time.deltaTime;
            if (targetTime >= 0)
            {
                tweenElapsed += dt;
                var k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(tweenElapsed / Mathf.Max(0.1f, TransitionSeconds)));
                TimeOfDay = Mathf.Repeat(tweenStart + tweenLength * k, 24);
                if (k >= 1) targetTime = -1;
                WriteClock(false);
            }
            else if (!ReadClock())
            {
                if (!Paused) TimeOfDay = Mathf.Repeat(TimeOfDay + dt * 24f / (DayLengthMinutes * 60f), 24);
                if (!AllowNight && (TimeOfDay > SunsetHour - 0.2f || TimeOfDay < SunriseHour + 0.8f)) TimeOfDay = SunriseHour + 0.8f;
                AutoWeatherTick();
            }
            if (Weather != appliedWeather) SetWeather(Weather);
            if (blend < 1)
            {
                blend = Mathf.Min(1, blend + dt / Mathf.Max(0.1f, TransitionSeconds));
                current = WeatherValues.Lerp(from, to, Mathf.SmoothStep(0, 1, blend));
            }
            // Rain wets surfaces within a minute; drying takes several game hours.
            wetness = current.Rain > 0.05f ? Mathf.MoveTowards(wetness, 1, dt * current.Rain / 40f) : Mathf.MoveTowards(wetness, 0, dt / 180f);
            var wind = WindSpeed * current.Wind;
            var direction = new Vector2(Mathf.Sin(WindDirection * Mathf.Deg2Rad), Mathf.Cos(WindDirection * Mathf.Deg2Rad));
            cloudOffset += new Vector4(direction.x, direction.y, direction.x * 1.6f, direction.y * 1.6f) * wind * dt * 0.0035f;
            cookieOffset += direction * wind * dt * 0.9f;
            Apply();
            UpdateRain(dt);
            probeTimer -= dt;
            if (SkyProbe != null && probeTimer <= 0) { probeTimer = 6; SkyProbe.RenderProbe(); }
        }

        private void AutoWeatherTick()
        {
            if (!AutoWeather) return;
            if (nextWeatherHour < 0) nextWeatherHour = Mathf.Repeat(TimeOfDay + Random(WeatherHoldHours.x, WeatherHoldHours.y), 24);
            if (Mathf.Abs(Mathf.DeltaAngle(TimeOfDay * 15, nextWeatherHour * 15)) > 1.5f) return;
            var roll = random.NextDouble();
            var next = roll < 0.5 ? SkyWeather.Clear : roll < 0.75 ? SkyWeather.Cloudy : roll < 0.9 ? SkyWeather.Rain : SkyWeather.Overcast;
            SetWeather(next);
            nextWeatherHour = Mathf.Repeat(TimeOfDay + Random(WeatherHoldHours.x, WeatherHoldHours.y), 24);
        }

        private float Random(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ------------------------------------------------------------------ Forest Café WorldClock bridge
        private ContextSpeak.Slice.WorldClock Clock()
        {
            if (!SyncWithForestClock) return null;
            if (forestGame == null)
            {
                forestGame = FindFirstObjectByType<ContextSpeak.Slice.SliceGame>();
                if (forestGame == null) { SyncWithForestClock = false; return null; }
            }
            ContextSpeak.Slice.WorldClock.AllowNight = AllowNight;
            return forestGame.Save != null ? forestGame.Save.Clock : null;
        }

        private bool ReadClock()
        {
            var clock = Clock();
            if (clock == null) return false;
            TimeOfDay = Mathf.Repeat(clock.Minute / 60f, 24);
            var mapped = clock.Weather == ContextSpeak.Slice.Weather.Rain ? SkyWeather.Rain
                : clock.Weather == ContextSpeak.Slice.Weather.Cloudy ? (SkyMath.Daylight(Elevation) < 0.2f ? SkyWeather.Overcast : SkyWeather.Cloudy) : SkyWeather.Clear;
            if (mapped != appliedWeather) SetWeather(mapped);
            return true;
        }

        private void WriteClock(bool weather = true)
        {
            var clock = Clock();
            if (clock == null) return;
            clock.Minute = TimeOfDay * 60f;
            if (weather) clock.Weather = Weather == SkyWeather.Rain ? ContextSpeak.Slice.Weather.Rain : Weather == SkyWeather.Clear ? ContextSpeak.Slice.Weather.Clear : ContextSpeak.Slice.Weather.Cloudy;
        }

        // ------------------------------------------------------------------ apply
        private float Elevation => SkyMath.SunElevation(TimeOfDay, SunriseHour, SunsetHour, MaxSunElevation);

        /// Push the current time and weather into lights, sky, fog, ambient and exposure (edit mode too).
        /// Re-create the default sky/ambient ramps (after a look upgrade).
        public void ResetLook() { gradientsReady = false; lookVersion = 0; EnsureGradients(); ApplyNow(); }

        public void ApplyNow()
        {
            EnsureGradients();
            if (blend >= 1) current = to = Values(appliedWeather == (SkyWeather)(-1) ? Weather : appliedWeather);
            Apply();
            UpdateRain(0.5f);
            if (SkyProbe != null) SkyProbe.RenderProbe();
        }

        private void Apply()
        {
            var elevation = Elevation;
            var azimuth = SkyMath.SunAzimuth(TimeOfDay, SunriseHour, SunsetHour) + NorthOffset;
            var daylight = SkyMath.Daylight(elevation);
            var t = Mathf.InverseLerp(-18, 60, elevation);
            var cover = EnvironmentTuning.CloudOverride >= 0 ? EnvironmentTuning.CloudOverride : CloudCoverageOverride >= 0 ? CloudCoverageOverride : current.Cover;
            var rain = EnvironmentTuning.RainOverride >= 0 ? EnvironmentTuning.RainOverride : Mathf.Clamp01(current.Rain * EnvironmentTuning.RainScale);
            var grey = Mathf.Clamp01(current.Desaturate);

            Color Desat(Color colour, float amount, float bright = 1)
            {
                var luminance = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
                return Color.Lerp(colour, new Color(luminance, luminance, luminance * 1.04f), amount) * bright;
            }

            var sunRotation = Quaternion.Euler(elevation, azimuth, 0);
            var toSun = sunRotation * Vector3.back;
            var moonElevation = Mathf.Clamp(-elevation * 0.9f + 8, -30, 70);
            var moonRotation = Quaternion.Euler(moonElevation, azimuth + 165, 0);
            var toMoon = moonRotation * Vector3.back;
            // Physically based sun colour (blackbody by elevation) instead of a hand-painted orange ramp,
            // so sunsets are warm on lit surfaces without tinting the whole world.
            var sunColour = Color.Lerp(SunColour.Evaluate(t), Mathf.CorrelatedColorTemperatureToRGB(EnvironmentTuning.SunKelvin(elevation)), 0.85f);
            var sunStrength = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-3, 22, elevation)) * 1.65f * current.Sun * (1 - cover * 0.35f) * EnvironmentTuning.SunIntensity;
            var moonStrength = (1 - daylight) * 0.22f * Mathf.Lerp(0.3f, 1, MoonPhase) * (1 - cover * 0.75f);

            if (Sun != null)
            {
                Sun.transform.rotation = sunRotation;
                Sun.color = Desat(sunColour, grey * 0.6f);
                Sun.intensity = sunStrength;
                Sun.shadowStrength = Mathf.Clamp01(Mathf.Lerp(0.95f, 0.35f, cover) * EnvironmentTuning.ShadowStrength);
                Sun.enabled = elevation > -4;
                var cookies = CloudCookies.Where(item => item != null).ToArray();
                Sun.cookie = cookies.Length == 0 || cover < 0.18f ? null : cookies[Mathf.Clamp(Mathf.FloorToInt(cover * cookies.Length), 0, cookies.Length - 1)];
                var data = Sun.GetComponent<UniversalAdditionalLightData>();
                if (data != null && Sun.cookie != null) { data.lightCookieSize = Vector2.one * CookieSize; data.lightCookieOffset = cookieOffset; }
            }
            if (Moon != null)
            {
                Moon.transform.rotation = moonRotation;
                Moon.intensity = moonStrength;
                Moon.color = new Color(0.74f, 0.8f, 0.95f);     // moonlight reads slightly cool, not saturated blue
                Moon.enabled = elevation <= -4 && moonStrength > 0.005f;
            }
            RenderSettings.sun = Sun != null && Sun.enabled ? Sun : Moon != null ? Moon : Sun;

            var dim = Mathf.Lerp(1, 0.55f, current.Darkness * daylight);
            var zenith = Desat(Zenith.Evaluate(t), grey, Mathf.Lerp(1, 0.75f, grey));
            var horizon = Desat(Horizon.Evaluate(t), grey * 0.85f, Mathf.Lerp(1, 0.85f, grey));
            if (SkyMaterial != null)
            {
                SkyMaterial.SetColor(ZenithId, zenith);
                SkyMaterial.SetColor(HorizonId, horizon);
                SkyMaterial.SetColor(GroundId, AmbientGround.Evaluate(t) * 0.8f);
                SkyMaterial.SetVector(SunDirId, toSun);
                SkyMaterial.SetColor(SunColorId, Desat(sunColour, grey * 0.7f) * (elevation > -3 ? 1 : 0));
                var golden = Mathf.Clamp01(1 - Mathf.Abs(elevation - 2) / 12f) * (1 - cover * 0.7f);
                SkyMaterial.SetFloat(SunGlowId, Mathf.Lerp(0.4f, 1.1f, daylight) * (1 - cover * 0.6f));
                SkyMaterial.SetFloat(SunsetGlowId, golden * 1.6f);
                SkyMaterial.SetVector(MoonDirId, toMoon);
                SkyMaterial.SetColor(MoonColorId, new Color(0.88f, 0.92f, 1f) * Mathf.Lerp(1, 0.25f, daylight));
                SkyMaterial.SetFloat(MoonPhaseId, MoonPhase);
                SkyMaterial.SetFloat(StarsId, (1 - daylight) * current.Stars * 1.4f);
                SkyMaterial.SetFloat(CoverId, cover);
                SkyMaterial.SetFloat(DensityId, current.Density);
                var lit = Color.Lerp(Color.white * 1.05f, sunColour * 1.2f, Mathf.Clamp01(1 - elevation / 18f)) * Mathf.Lerp(0.06f, 1, daylight) * dim;
                SkyMaterial.SetColor(LitId, Desat(lit, grey * 0.6f));
                var shade = Color.Lerp(horizon * 0.85f, new Color(0.32f, 0.34f, 0.38f) * Mathf.Lerp(0.08f, 1, daylight), 0.35f + current.Darkness * 0.6f) * dim;
                SkyMaterial.SetColor(ShadeId, shade);
                SkyMaterial.SetVector(OffsetId, cloudOffset);
                SkyMaterial.SetFloat(ExposureId, 1);
                var hdri = elevation > 10 ? HdriDay : elevation > -6 ? (HdriSunset != null ? HdriSunset : HdriDay) : HdriNight;
                SkyMaterial.SetFloat(HdriBlendId, hdri != null ? HdriStrength * (1 - cover * 0.5f) : 0);
                if (hdri != null) SkyMaterial.SetTexture(HdriId, hdri);
                SkyMaterial.SetFloat(HdriRotationId, HdriRotation);
                if (RenderSettings.skybox != SkyMaterial) RenderSettings.skybox = SkyMaterial;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            var ambientDim = Mathf.Lerp(1, 0.8f, current.Darkness) * EnvironmentTuning.AmbientScale;
            RenderSettings.ambientSkyColor = Desat(AmbientSky.Evaluate(t), grey, ambientDim);
            RenderSettings.ambientEquatorColor = Desat(AmbientEquator.Evaluate(t), grey, ambientDim);
            RenderSettings.ambientGroundColor = Desat(AmbientGround.Evaluate(t), grey * 0.5f, ambientDim);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.25f, 1f, daylight);
            if (!FogLocked)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                // Haze takes the horizon's brightness but little of its sunset hue: a strong orange fog is what
                // made every object look filtered at golden hour.
                var haze = Color.Lerp(Desat(horizon, 0.55f), Desat(zenith, 0.4f, 1.6f), 0.2f);
                RenderSettings.fogColor = Color.Lerp(haze, Desat(horizon, 1, 0.9f), cover * 0.7f);
                RenderSettings.fogDensity = BaseFogDensity * (current.Fog + EnvironmentTuning.ExtraFog) * EnvironmentTuning.FogDensity * (Interior ? 0.6f : 1) * Mathf.Lerp(1.15f, 1, daylight);
            }
            if (exposure != null) exposure.postExposure.Override(BaseExposure + current.Exposure + (1 - daylight) * 0.45f + EnvironmentTuning.ExposureOffset + EnvironmentTuning.ZoneExposure);

            SkyState.Hour = TimeOfDay;
            SkyState.SunElevation = elevation;
            SkyState.Daylight = daylight;
            SkyState.Weather = appliedWeather == (SkyWeather)(-1) ? Weather : appliedWeather;
            SkyState.CloudCover = cover;
            SkyState.Rain = rain;
            SkyState.Wetness = wetness;
            SkyState.WindSpeed = WindSpeed * current.Wind * EnvironmentTuning.WindScale;
            SkyState.WindDirection = new Vector2(Mathf.Sin(WindDirection * Mathf.Deg2Rad), Mathf.Cos(WindDirection * Mathf.Deg2Rad));
            if ((int)TimeOfDay != lastHour) { lastHour = (int)TimeOfDay; SkyState.Raise(); }
        }

        private void UpdateRain(float dt)
        {
            if (Rain == null) return;
            var camera = Camera.main;
            if (camera != null) Rain.transform.position = camera.transform.position + Vector3.up * 11 + camera.transform.forward * 6;
            var emission = Rain.emission;
            emission.rateOverTime = SkyState.Rain * 3200;
            var velocity = Rain.velocityOverLifetime;
            velocity.enabled = true;
            var wind = SkyState.WindDirection * WindSpeed * current.Wind * 0.35f;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x);
            velocity.y = new ParticleSystem.MinMaxCurve(-13);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.y);
            if (SkyState.Rain > 0.02f && !Rain.isPlaying) Rain.Play();
            if (SkyState.Rain <= 0.02f && Rain.isPlaying) Rain.Stop(true, Application.isPlaying ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!Application.isPlaying && SkyState.Rain <= 0.02f) Rain.Clear(true);   // test shots: no leftover drops
            if (!Application.isPlaying && SkyState.Rain > 0.02f) Rain.Simulate(1.5f, true, true);
        }

        private WeatherValues Values(SkyWeather weather)
        {
            var profile = Profiles != null ? Profiles.FirstOrDefault(item => item != null && item.Weather == weather) : null;
            return profile != null ? profile.Values : WeatherProfile.Default(weather);
        }

        // ------------------------------------------------------------------ default look
        private void EnsureGradients()
        {
            if (gradientsReady && Zenith != null && lookVersion >= LookVersion) return;
            lookVersion = LookVersion;
            // Keys at t = InverseLerp(−18°, 60°, elevation): 0 night · .17 blue hour · .25 sunrise · .33 golden · .5 day · 1 noon.
            Zenith = Make((0f, "05070F"), (0.17f, "1B2A4E"), (0.25f, "3D5F95"), (0.33f, "4E78B8"), (0.5f, "3D72C4"), (1f, "2A62C2"));
            Horizon = Make((0f, "0B1020"), (0.17f, "5A5C7A"), (0.23f, "F08A4B"), (0.29f, "F6B77A"), (0.4f, "BFD5EA"), (1f, "B7D4F0"));
            SunColour = Make((0f, "3A2A2A"), (0.2f, "FF6A2B"), (0.26f, "FF9A4D"), (0.33f, "FFC98A"), (0.48f, "FFF0DA"), (1f, "FFF8EE"));
            AmbientSky = Make((0f, "0C1220"), (0.18f, "323C58"), (0.26f, "6C7590"), (0.4f, "8EA3BE"), (1f, "A3B8D2"));
            AmbientEquator = Make((0f, "0A0D15"), (0.18f, "343846"), (0.26f, "8A7768"), (0.4f, "9EA4A2"), (1f, "ADB2AD"));
            AmbientGround = Make((0f, "06070A"), (0.2f, "26221E"), (0.4f, "5E564D"), (1f, "6E655A"));
            gradientsReady = true;
        }

        private static Gradient Make(params (float time, string hex)[] keys)
        {
            var gradient = new Gradient { mode = GradientMode.Blend };
            gradient.SetKeys(keys.Select(key => { ColorUtility.TryParseHtmlString("#" + key.hex, out var colour); return new GradientColorKey(colour, key.time); }).ToArray(),
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return gradient;
        }
    }
}
