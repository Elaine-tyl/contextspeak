using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Sky & Weather: sun path, day/night, presets, smooth transitions,
    /// gameplay signals (lamps, crowds, sheltering, terrace closure) and the WorldClock bridge.
    public static class SkyChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Sky & Weather")]
        public static void Menu()
        {
            var errors = Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak sky & weather checks passed.");
            else Debug.LogError("ContextSpeak sky & weather checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static string[] Run()
        {
            var errors = new List<string>();
            void Check(bool value, string message) { if (!value) errors.Add(message); }
            // Sun path.
            Check(Mathf.Abs(SkyMath.SunElevation(6.2f, 6.2f, 18.6f, 62)) < 0.01f, "Sun is on the horizon at sunrise.");
            Check(Mathf.Abs(SkyMath.SunElevation(12.4f, 6.2f, 18.6f, 62) - 62) < 0.01f, "Sun peaks at solar noon.");
            Check(SkyMath.SunElevation(0, 6.2f, 18.6f, 62) < -30, "Sun is well below the horizon at midnight.");
            Check(SkyMath.SunElevation(18.3f, 6.2f, 18.6f, 62) is var dusk && dusk > 0 && dusk < 6, "Golden hour sun is low (0–6°).");
            Check(Mathf.Abs(SkyMath.SunAzimuth(6.2f, 6.2f, 18.6f) - 90) < 0.01f && Mathf.Abs(SkyMath.SunAzimuth(18.6f, 6.2f, 18.6f) - 270) < 0.01f, "Sun rises in the east and sets in the west.");
            Check(SkyMath.Daylight(-10) == 0 && SkyMath.Daylight(10) == 1 && SkyMath.Daylight(0) > 0.3f && SkyMath.Daylight(0) < 0.7f, "Twilight blends day and night.");
            // Weather profiles and blending.
            var clear = WeatherProfile.Default(SkyWeather.Clear);
            var rain = WeatherProfile.Default(SkyWeather.Rain);
            Check(rain.Cover > WeatherProfile.Default(SkyWeather.Cloudy).Cover && rain.Cover > clear.Cover && rain.Rain == 1 && clear.Rain == 0, "Weather profiles are ordered sensibly.");
            var half = WeatherValues.Lerp(clear, rain, 0.5f);
            Check(half.Cover > clear.Cover && half.Cover < rain.Cover && Mathf.Approximately(half.Rain, 0.5f), "Weather blends smoothly (no instant switch).");

            // Live system in a throwaway scene: presets, gameplay signals, edit-mode apply.
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                RenderSettings.sun = sun;
                var sky = new GameObject("Sky").AddComponent<SkySystem>();
                sky.Sun = sun;
                sky.SyncWithForestClock = false;
                foreach (SkyPreset preset in Enum.GetValues(typeof(SkyPreset)))
                {
                    sky.ApplyPreset(preset, true);
                    var night = preset == SkyPreset.ClearNight || preset == SkyPreset.OvercastNight;
                    Check(SkyState.IsNight == night, preset + ": night flag " + SkyState.IsNight);
                    Check(SkyState.Raining == (preset == SkyPreset.Rainy), preset + ": rain flag " + SkyState.Raining);
                    Check(night ? !sun.enabled || sun.intensity < 0.05f : sun.intensity > 0.2f, preset + ": sun intensity " + sun.intensity);
                    Check(night ? SkyState.LightsNeeded > 0.9f : preset == SkyPreset.BrightAfternoon ? SkyState.LightsNeeded < 0.05f : true, preset + ": street lights " + SkyState.LightsNeeded);
                    Check(RenderSettings.fog && RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight, preset + ": ambient and fog are driven.");
                }
                sky.ApplyPreset(SkyPreset.BrightAfternoon, true);
                var dayCrowd = SkyState.Crowd;
                var dayFog = RenderSettings.fogDensity;
                var dayAmbient = RenderSettings.ambientSkyColor.grayscale;
                sky.ApplyPreset(SkyPreset.Rainy, true);
                Check(SkyState.Crowd < dayCrowd && SkyState.Sheltering && RenderSettings.fogDensity > dayFog * 2, "Rain empties streets, NPCs shelter, fog thickens.");
                sky.ApplyPreset(SkyPreset.ClearNight, true);
                Check(RenderSettings.ambientSkyColor.grayscale < dayAmbient * 0.3f, "Night ambient light is much darker than day.");
                sky.PreviewBlend(SkyWeather.Clear, SkyWeather.Rain, 0.5f);
                Check(SkyState.Rain > 0.2f && SkyState.Rain < 0.8f, "A weather change passes through in-between states.");
            }
            catch (Exception error) { errors.Add("Live sky check threw " + error.GetType().Name + ": " + error.Message); }
            finally
            {
                if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
                SkyState.ResetDefaults();
            }

            // Forest Café clock: nights now happen (and can be switched off again).
            var allow = ContextSpeak.Slice.WorldClock.AllowNight;
            try
            {
                ContextSpeak.Slice.WorldClock.AllowNight = true;
                var clock = new ContextSpeak.Slice.WorldClock { Minute = 18 * 60 + 30, NextWeather = 9999 };
                clock.Tick(120, new System.Random(1));
                Check(clock.Minute > 20 * 60, "WorldClock runs into the evening when night is allowed.");
                ContextSpeak.Slice.WorldClock.AllowNight = false;
                clock = new ContextSpeak.Slice.WorldClock { Minute = 18 * 60 + 30, NextWeather = 9999 };
                clock.Tick(1, new System.Random(1));
                Check(clock.Minute < 9 * 60, "Daytime-only mode still skips the night.");
            }
            finally { ContextSpeak.Slice.WorldClock.AllowNight = allow; }
            return errors.ToArray();
        }
    }
}
