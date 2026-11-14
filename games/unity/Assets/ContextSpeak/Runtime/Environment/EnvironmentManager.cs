using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Life
{
    /// Hub of the realistic-environment layer in a scene. Owns the shared post-processing (bloom,
    /// white balance, subtle vignette — ACES tonemapping comes from the scene's colour grade),
    /// publishes rain/wetness to shaders, and adapts exposure and ambient light gradually when the
    /// camera moves between indoor and outdoor LightingZones (no abrupt brightness jumps).
    [DisallowMultipleComponent]
    public sealed class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Active { get; private set; }

        [Header("Post-processing (shared Global Volume)")]
        public Volume PostVolume;
        [Range(0, 1)] public float BloomIntensity = 0.12f;
        [Range(-30, 30)] public float WhiteBalanceTemperature;
        [Range(0, 0.4f)] public float VignetteIntensity = 0.12f;
        [Header("Indoor / outdoor adaptation")]
        [Tooltip("EV per second while the eye adapts between zones.")] public float AdaptationRate = 0.6f;

        private static readonly List<LightingZone> zones = new List<LightingZone>();
        private Bloom bloom;
        private WhiteBalance whiteBalance;
        private Vignette vignette;
        private float zoneExposure, zoneAmbient = 1;
        private static readonly int WetnessId = Shader.PropertyToID("_EnvWetness"), RainId = Shader.PropertyToID("_EnvRain");

        public static void Register(LightingZone zone) { if (!zones.Contains(zone)) zones.Add(zone); }
        public static void Unregister(LightingZone zone) => zones.Remove(zone);
        public LightingZone CurrentZone { get; private set; }

        private void OnEnable()
        {
            Active = this;
            if (PostVolume != null && PostVolume.sharedProfile != null)
            {
                PostVolume.sharedProfile.TryGet(out bloom);
                PostVolume.sharedProfile.TryGet(out whiteBalance);
                PostVolume.sharedProfile.TryGet(out vignette);
            }
            EnvironmentTuning.BloomIntensity = BloomIntensity;
            Tick(0);
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            EnvironmentTuning.ZoneExposure = 0;
            EnvironmentTuning.AmbientScale = 1;
        }

        private void Update() => Tick(Application.isPlaying ? Time.deltaTime : 0);

        private void Tick(float dt)
        {
            Shader.SetGlobalFloat(WetnessId, SkyState.Wetness);
            Shader.SetGlobalFloat(RainId, SkyState.Rain);
            if (bloom != null) bloom.intensity.Override(EnvironmentTuning.BloomIntensity);
            if (whiteBalance != null) whiteBalance.temperature.Override(WhiteBalanceTemperature);
            if (vignette != null) vignette.intensity.Override(VignetteIntensity);

            // Which zone is the camera in? The smallest containing zone wins (a room inside a building).
            var camera = Camera.main;
            LightingZone inside = null;
            if (camera != null)
            {
                var position = camera.transform.position;
                var best = float.MaxValue;
                foreach (var zone in zones)
                {
                    if (zone == null || !zone.isActiveAndEnabled || !zone.Contains(position)) continue;
                    var volume = zone.Volume;
                    if (volume < best) { best = volume; inside = zone; }
                }
            }
            CurrentZone = inside;
            var targetExposure = inside != null ? inside.TargetExposure() : 0;
            var targetAmbient = inside != null ? inside.AmbientScale : 1;
            if (dt <= 0) { zoneExposure = targetExposure; zoneAmbient = targetAmbient; }
            else
            {
                zoneExposure = Mathf.MoveTowards(zoneExposure, targetExposure, AdaptationRate * dt);
                zoneAmbient = Mathf.MoveTowards(zoneAmbient, targetAmbient, AdaptationRate * 0.5f * dt);
            }
            EnvironmentTuning.ZoneExposure = zoneExposure;
            EnvironmentTuning.AmbientScale = zoneAmbient;
        }

        /// Recompute immediately (editor test renders).
        public void ApplyNow() => Tick(0);
    }
}
