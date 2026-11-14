using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    [Serializable]
    public struct GerstnerWave
    {
        public float Direction;     // degrees
        public float Wavelength;    // metres
        [Range(0, 0.5f)] public float Steepness;
        public GerstnerWave(float direction, float wavelength, float steepness) { Direction = direction; Wavelength = wavelength; Steepness = steepness; }
    }

    /// The ocean's wave model, shared by the shader (visual displacement) and C# (buoyancy, boats,
    /// floating props). Swells and chop grow with the sky's wind and rain. Swimming and collision use
    /// a flat sea level instead, so gameplay never depends on visual displacement.
    [ExecuteAlways]
    public sealed class OceanWaves : MonoBehaviour
    {
        public static OceanWaves Active { get; private set; }
        public const float SeaLevel = 0;
        public List<GerstnerWave> Waves = new List<GerstnerWave>
        {
            new GerstnerWave(200, 48, 0.12f), new GerstnerWave(215, 31, 0.10f), new GerstnerWave(185, 19, 0.09f),
            new GerstnerWave(240, 11, 0.07f), new GerstnerWave(160, 7, 0.05f), new GerstnerWave(205, 4.2f, 0.04f),
        };
        public Vector3 IslandCentre;
        public float ShoreRadius = 270;
        [Range(0, 2)] public float BaseAmplitude = 0.8f;
        public bool FollowWeather = true;
        [Tooltip("0 = performance (3 waves), 1 = quality (all waves)")] [Range(0, 1)] public int Quality = 1;
        public float Amplitude { get; private set; } = 1;
        private static readonly int WaveId = Shader.PropertyToID("_OceanWave"), CountId = Shader.PropertyToID("_OceanWaveCount"),
            TimeId = Shader.PropertyToID("_OceanTime"), AmpId = Shader.PropertyToID("_OceanAmplitude"), ShoreId = Shader.PropertyToID("_OceanShore");
        private readonly Vector4[] packed = new Vector4[8];

        private void OnEnable() { Active = this; Push(); }
        private void OnDisable() { if (Active == this) Active = null; }

        public int Count => Mathf.Min(Waves.Count, Quality == 0 ? 3 : 8);
        public float Time => Application.isPlaying ? UnityEngine.Time.time : 12.3f;

        private void Update()
        {
            var target = BaseAmplitude * EnvironmentTuning.WaveStrength;
            if (FollowWeather) target *= 0.55f + Mathf.Clamp(SkyState.WindSpeed, 0, 15) / 8f + SkyState.Rain * 0.5f;
            Amplitude = Application.isPlaying ? Mathf.MoveTowards(Amplitude, target, UnityEngine.Time.deltaTime * 0.05f) : target;
            Push();
        }

        private void Push()
        {
            for (var i = 0; i < packed.Length; i++)
            {
                if (i >= Waves.Count) { packed[i] = new Vector4(1, 0, 0, 10); continue; }
                var wave = Waves[i];
                var radians = wave.Direction * Mathf.Deg2Rad;
                packed[i] = new Vector4(Mathf.Sin(radians), Mathf.Cos(radians), wave.Steepness, Mathf.Max(0.5f, wave.Wavelength));
            }
            Shader.SetGlobalVectorArray(WaveId, packed);
            Shader.SetGlobalFloat(CountId, Count);
            Shader.SetGlobalFloat(TimeId, Time);
            Shader.SetGlobalFloat(AmpId, Amplitude);
            Shader.SetGlobalVector(ShoreId, new Vector4(IslandCentre.x, IslandCentre.y, IslandCentre.z, ShoreRadius));
        }

        public float ShoreFactor(Vector2 xz)
        {
            var distance = Vector2.Distance(xz, new Vector2(IslandCentre.x, IslandCentre.z));
            return Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ShoreRadius - 8, ShoreRadius + 90, distance)));
        }

        /// Gerstner displacement of the water particle that starts at (x, z).
        public Vector3 Displacement(Vector2 xz, float time)
        {
            var offset = Vector3.zero;
            var shore = ShoreFactor(xz) * Amplitude;
            for (var i = 0; i < Count; i++)
            {
                var wave = Waves[i];
                var k = 2 * Mathf.PI / Mathf.Max(0.5f, wave.Wavelength);
                var c = Mathf.Sqrt(9.81f / k);
                var radians = wave.Direction * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
                var f = k * (Vector2.Dot(d, xz) - c * time);
                var a = wave.Steepness / k * shore;
                offset += new Vector3(d.x * a * Mathf.Cos(f), a * Mathf.Sin(f), d.y * a * Mathf.Cos(f));
            }
            return offset;
        }

        /// Water surface height at a world position (inverts the horizontal Gerstner shift).
        public float Height(Vector3 position) => Height(position, Time);

        public float Height(Vector3 position, float time)
        {
            var target = new Vector2(position.x, position.z);
            var guess = target;
            for (var i = 0; i < 3; i++)
            {
                var shift = Displacement(guess, time);
                guess = target - new Vector2(shift.x, shift.z);
            }
            return SeaLevel + Displacement(guess, time).y;
        }

        public Vector3 Normal(Vector3 position)
        {
            const float e = 0.6f;
            var h = Height(position);
            var hx = Height(position + Vector3.right * e);
            var hz = Height(position + Vector3.forward * e);
            return Vector3.Cross(new Vector3(0, hz - h, e), new Vector3(e, hx - h, 0)).normalized;
        }
    }

    /// Bobs and tilts with the waves (moored boats, buoys, floating props).
    public sealed class Floater : MonoBehaviour
    {
        public float Draft = 0.1f, Tilt = 0.8f, Smoothing = 3f;
        public bool KeepYaw = true;
        private float yaw;

        private void Start() { yaw = transform.eulerAngles.y; }

        private void LateUpdate()
        {
            var ocean = OceanWaves.Active;
            if (ocean == null) return;
            var position = transform.position;
            position.y = Mathf.Lerp(position.y, ocean.Height(position) - Draft, Time.deltaTime * Smoothing);
            transform.position = position;
            var normal = Vector3.Slerp(Vector3.up, ocean.Normal(position), Tilt);
            var facing = KeepYaw ? Quaternion.Euler(0, yaw, 0) : Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.FromToRotation(Vector3.up, normal) * facing, Time.deltaTime * Smoothing);
        }
    }

    /// Feeds the sky's wind to the foliage shader (palms sway harder in a breeze or a storm).
    public sealed class FoliageWind : MonoBehaviour
    {
        private float gust;
        private static readonly int Strength = Shader.PropertyToID("_WindStrength"), Gust = Shader.PropertyToID("_WindGust"), Direction = Shader.PropertyToID("_WindDirection");

        private void Update()
        {
            if (GlobalWindController.Active != null) return;     // the scene-wide wind owns these globals
            gust = Mathf.PerlinNoise(Time.time * 0.15f, 0.3f);
            Shader.SetGlobalFloat(Strength, Mathf.Clamp(SkyState.WindSpeed, 0.5f, 16f) * (1 + SkyState.Rain * 0.5f));
            Shader.SetGlobalFloat(Gust, gust);
            var wind = SkyState.WindDirection;
            Shader.SetGlobalVector(Direction, new Vector4(wind.x, wind.y, 0, 0));
        }
    }
}
