using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Environment: lighting maths, weather states, water presets, the URP
    /// quality presets and SSAO renderer, shader compilation, vegetation prefabs and the wiring of
    /// every life scene. Visual quality and frame time still need rendered/profiled verification.
    public static class EnvironmentChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Environment")]
        public static void Menu()
        {
            var errors = Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak environment checks passed.");
            else Debug.LogError("ContextSpeak environment checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static string[] Run()
        {
            var errors = new List<string>();
            void Check(bool ok, string message) { if (!ok) errors.Add(message); }
            void Guard(string name, Action body) { try { body(); } catch (Exception e) { errors.Add(name + " threw " + e.GetType().Name + ": " + e.Message); } }
            Guard("Lighting maths", () =>
            {
                Check(Mathf.Abs(EnvironmentTuning.SunKelvin(0) - 2350) < 400, "Sun on the horizon ≈ 2000–2700 K.");
                Check(EnvironmentTuning.SunKelvin(45) > 5500 && EnvironmentTuning.SunKelvin(45) < 6500, "High sun is neutral daylight (5500–6500 K).");
                var previous = 0f;
                for (var e = -2f; e <= 60; e += 2) { var k = EnvironmentTuning.SunKelvin(e); Check(k >= previous, "Sun colour temperature rises monotonically with elevation."); previous = k; }
                Check(DayNightCycle.Classify(12, 55) == DayNightCycle.Phase.Midday && DayNightCycle.Classify(18.3f, 3) == DayNightCycle.Phase.Sunset
                      && DayNightCycle.Classify(19, -3) == DayNightCycle.Phase.BlueHourEvening && DayNightCycle.Classify(23, -30) == DayNightCycle.Phase.Night
                      && DayNightCycle.Classify(17, 12) == DayNightCycle.Phase.GoldenHour, "Day phases classify correctly.");
            });
            Guard("Weather", () =>
            {
                var states = (WeatherManager.State[])Enum.GetValues(typeof(WeatherManager.State));
                Check(states.Length == 8, "Eight weather states.");
                Check(WeatherManager.Describe(WeatherManager.State.HeavyRain).RainScale > WeatherManager.Describe(WeatherManager.State.LightRain).RainScale, "Heavy rain is heavier than light rain.");
                Check(WeatherManager.Describe(WeatherManager.State.Windy).WindScale > 2, "Windy weather raises the wind.");
                Check(WeatherManager.Describe(WeatherManager.State.FoggyMorning).ExtraFog > 2, "Foggy morning thickens the fog.");
            });
            Guard("Water presets", () =>
            {
                foreach (WaterSettings.WaterType type in Enum.GetValues(typeof(WaterSettings.WaterType)))
                {
                    var preset = WaterSettings.For(type);
                    Color.RGBToHSV(preset.Deep, out _, out var saturation, out _);
                    Check(saturation < 0.95f && preset.Clarity > 0 && preset.Absorption > 0, type + " water uses an absorption tint, not an opaque saturated colour.");
                }
            });
            Guard("Shaders", () =>
            {
                foreach (var name in new[] { "ContextSpeak/Vegetation Lit", "ContextSpeak/Ground Lit", "ContextSpeak/Water Surface", "ContextSpeak/Ocean", "ContextSpeak/Dynamic Sky" })
                {
                    var shader = Shader.Find(name);
                    Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader " + name + " compiles.");
                }
            });
            Guard("Quality presets", () =>
            {
                var high = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Generated/Env-High.asset");
                var ultra = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Generated/Env-Ultra.asset");
                var performance = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Generated/Env-Performance.asset");
                Check(high != null && ultra != null && performance != null, "Performance / High / Ultra URP assets exist (run Setup Realistic World or Lighting).");
                if (high == null || ultra == null || performance == null) return;
                Check(performance.shadowDistance < high.shadowDistance && high.shadowDistance < ultra.shadowDistance, "Shadow distance grows Performance → High → Ultra.");
                Check(ultra.mainLightShadowmapResolution > high.mainLightShadowmapResolution, "Ultra has the larger shadow map.");
                Check(high.shadowCascadeCount == 4 && ultra.shadowCascadeCount == 4 && performance.shadowCascadeCount == 2, "Cascades 2 / 4 / 4.");
                Check(high.supportsHDR && high.supportsCameraDepthTexture && high.supportsCameraOpaqueTexture, "HDR + depth + opaque textures for water.");
                var env = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(EnvironmentSetup.EnvRendererPath);
                Check(env != null && env.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Any(f => f.isActive), "SSAO is configured as a Renderer Feature on the environment renderer.");
                var defaultRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Generated/Renderer.asset");
                Check(defaultRenderer == null || !defaultRenderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Any(), "The default renderer stays SSAO-free (2D/arcade cameras).");
            });
            Guard("Vegetation", () =>
            {
                var prefabs = Directory.Exists(EnvironmentSetup.VegetationFolder) ? Directory.GetFiles(EnvironmentSetup.VegetationFolder, "*.prefab") : new string[0];
                Check(prefabs.Length >= 10, "At least 10 vegetation prefabs (" + prefabs.Length + ").");
                foreach (var path in prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var lod = prefab.GetComponent<LODGroup>();
                    // The authored pots have only 418–444 triangles: one culled LOD is intentional.
                    // Keep the multi-LOD requirement for vegetation and denser props.
                    var triangles = prefab.GetComponentsInChildren<MeshFilter>(true)
                        .Sum(filter => filter.sharedMesh != null ? filter.sharedMesh.triangles.Length / 3 : 0);
                    var smallPot = prefab.name.StartsWith("Terracotta", StringComparison.Ordinal) && triangles <= 512;
                    Check(lod != null && lod.lodCount >= (smallPot ? 1 : 2), Path.GetFileName(path) + " has appropriate LODs.");
                    Check(prefab.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader.name == "ContextSpeak/Vegetation Lit" && m.enableInstancing)),
                        Path.GetFileName(path) + " uses the instanced vegetation shader.");
                }
            });
            Guard("Scenes", () =>
            {
                foreach (var path in SkyInstaller.LifeScenes.Where(File.Exists))
                {
                    var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                    var opened = !loaded.isLoaded;
                    var scene = opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : loaded;
                    try
                    {
                        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
                        var name = Path.GetFileNameWithoutExtension(path);
                        Check(All<EnvironmentManager>().Length == 1 && All<GlobalWindController>().Length == 1 && All<WeatherManager>().Length == 1 && All<DayNightCycle>().Length == 1
                              && All<EnvironmentQualityManager>().Length == 1 && All<EnvironmentDebugPanel>().Length == 1, name + ": environment controllers installed once.");
                        var sky = All<SkySystem>().FirstOrDefault();
                        Check(sky != null && Mathf.Abs(sky.BaseExposure) < 0.01f, name + ": exposure calibrated (base 0 EV).");
                        var camera = All<Camera>().Select(c => c.GetUniversalAdditionalCameraData()).FirstOrDefault(d => d != null && d.renderType == CameraRenderType.Base);
                        Check(camera != null && camera.requiresDepthTexture && camera.requiresColorTexture, name + ": camera provides depth + opaque textures.");
                        if (name == "Hospital" || name == "Restaurant") Check(All<LightingZone>().Length > 0, name + ": indoor lighting zone.");
                    }
                    finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                }
            });
            return errors.ToArray();
        }
    }
}
