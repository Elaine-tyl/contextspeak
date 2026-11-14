using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Life
{
    /// Performance / High / Ultra graphics presets. Each maps to a Unity quality level whose URP
    /// asset (created by Setup Realistic World) carries render scale, shadow resolution, distance,
    /// cascades, soft shadows, HDR and anti-aliasing; SSAO lives in the High/Ultra renderer only.
    /// This component adds the runtime parts: LOD bias (vegetation distance), grass density, ocean
    /// wave quality and reflection-probe refresh. The player's choice is saved in PlayerPrefs,
    /// separately from game progress.
    [DisallowMultipleComponent]
    public sealed class EnvironmentQualityManager : MonoBehaviour
    {
        public enum Preset { Performance, High, Ultra }

        public const string PrefsKey = "ContextSpeak.GraphicsPreset";
        public static readonly string[] LevelNames = { "Medium", "Very High", "Ultra" };   // Unity quality levels used per preset
        public static EnvironmentQualityManager Active { get; private set; }
        public Preset Current { get; private set; } = Preset.High;

        private float originalShadowDistance = -1;
        private UniversalRenderPipelineAsset shadowAsset;

        private void OnEnable()
        {
            Active = this;
            var saved = PlayerPrefs.GetInt(PrefsKey, (int)Preset.High);
            Apply((Preset)Mathf.Clamp(saved, 0, 2), false);
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            RestoreShadowDistance();
        }

        public static int LevelIndex(Preset preset)
        {
            var names = QualitySettings.names;
            for (var i = 0; i < names.Length; i++) if (names[i] == LevelNames[(int)preset]) return i;
            return Mathf.Clamp(new[] { 2, 4, 5 }[(int)preset], 0, names.Length - 1);
        }

        public void Apply(Preset preset, bool save = true)
        {
            RestoreShadowDistance();
            Current = preset;
            if (Application.isPlaying) QualitySettings.SetQualityLevel(LevelIndex(preset), true);
            QualitySettings.lodBias = preset == Preset.Performance ? 0.8f : preset == Preset.High ? 1.4f : 2.0f;
            EnvironmentTuning.GrassDensity = preset == Preset.Performance ? 0.45f : preset == Preset.High ? 0.85f : 1f;
            if (OceanWaves.Active != null) OceanWaves.Active.Quality = preset == Preset.Performance ? 0 : 1;
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                if (probe.mode == ReflectionProbeMode.Realtime)
                    probe.timeSlicingMode = preset == Preset.Ultra ? ReflectionProbeTimeSlicingMode.AllFacesAtOnce : ReflectionProbeTimeSlicingMode.IndividualFaces;
            // One anti-aliasing method at a time: MSAA (set in the High/Ultra URP assets) handles thin
            // grass and wires well in forward rendering; Performance has MSAA off and uses SMAA instead.
            var camera = Camera.main;
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.antialiasing = preset == Preset.Performance ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                data.antialiasingQuality = AntialiasingQuality.Medium;
                camera.allowMSAA = preset != Preset.Performance;
            }
            if (save) { PlayerPrefs.SetInt(PrefsKey, (int)preset); PlayerPrefs.Save(); }
            EnvironmentTuning.Raise();
        }

        /// Developer override of the main-light shadow distance (restored when the preset changes).
        public void SetShadowDistance(float metres)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            if (shadowAsset != asset) { RestoreShadowDistance(); shadowAsset = asset; originalShadowDistance = asset.shadowDistance; }
            asset.shadowDistance = Mathf.Clamp(metres, 10, 250);
        }

        public float ShadowDistance => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset ? asset.shadowDistance : 0;

        private void RestoreShadowDistance()
        {
            if (shadowAsset != null && originalShadowDistance > 0) shadowAsset.shadowDistance = originalShadowDistance;
            shadowAsset = null;
            originalShadowDistance = -1;
        }
    }
}
