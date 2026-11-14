using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace ContextSpeak.Life
{
    /// Developer environment & lighting panel (F3). Sliders write EnvironmentTuning and the sky
    /// directly, so changes show immediately. Developer tuning is kept apart from the player's
    /// graphics preset (EnvironmentQualityManager) and from game progress; it is only stored when
    /// "Keep tuning" is pressed (PlayerPrefs key ContextSpeak.DevEnvironment).
    public sealed class EnvironmentDebugPanel : MonoBehaviour
    {
        public const string DevKey = "ContextSpeak.DevEnvironment";
        public bool Open;
        private bool registeredOpen;
        private Vector2 scroll;
        private GUIStyle box, title, small;
        private float frameTime = 16, worst;
        private float cloud = -1, rain = -1;
        private int reflectionResolution = 128;

        private void Start() => Load();

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) Open = !Open;
            if (registeredOpen != Open) SyncPanel();
            if (Open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LifeUI.RequestBack();
            frameTime = Mathf.Lerp(frameTime, Time.unscaledDeltaTime * 1000, 0.05f);
            worst = Mathf.Max(worst * 0.995f, Time.unscaledDeltaTime * 1000);
        }

        private float Slider(string label, float value, float min, float max, string format = "0.00")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, small, GUILayout.Width(150));
            var next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(190));
            GUILayout.Label(next.ToString(format), small, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            return next;
        }

        private void SyncPanel() { registeredOpen = Open; LifeUI.SetPanelOpen(this, Open, BackPanel, 100); }
        private bool BackPanel() { Open = false; SyncPanel(); return true; }
        private void OnDisable() { registeredOpen = false; LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (Open) SyncPanel(); }

        private void DrawToolkit()
        {
            if (!Open) return;
            box ??= new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var sky = SkySystem.Active;
            GUILayout.BeginArea(new Rect(12, 60, 430, Screen.height - 80), box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Environment & lighting (F3)", title);
            var phase = DayNightCycle.Classify(SkyState.Hour, SkyState.SunElevation);
            var zone = EnvironmentManager.Active != null && EnvironmentManager.Active.CurrentZone != null ? EnvironmentManager.Active.CurrentZone.name : "outdoors";
            GUILayout.Label(SkyState.Clock + "  ·  " + DayNightCycle.Describe(phase) + "  ·  sun " + SkyState.SunElevation.ToString("0") + "°  ·  " + EnvironmentTuning.SunKelvin(SkyState.SunElevation).ToString("0") + " K  ·  " + zone, small);
            GUILayout.Label("Frame " + frameTime.ToString("0.0") + " ms (" + (1000 / Mathf.Max(frameTime, 0.1f)).ToString("0") + " fps) · worst " + worst.ToString("0.0") + " ms · memory " + (Profiler.GetTotalAllocatedMemoryLong() / 1048576) + " MB"
#if UNITY_EDITOR
                + " · draw calls " + UnityEditor.UnityStats.drawCalls + " · batches " + UnityEditor.UnityStats.batches
#endif
                , small);

            GUILayout.Space(6);
            GUILayout.Label("Graphics preset (player setting)", title);
            var quality = EnvironmentQualityManager.Active;
            GUILayout.BeginHorizontal();
            foreach (EnvironmentQualityManager.Preset preset in System.Enum.GetValues(typeof(EnvironmentQualityManager.Preset)))
                if (GUILayout.Toggle(quality != null && quality.Current == preset, preset.ToString(), GUI.skin.button) && quality != null && quality.Current != preset)
                    quality.Apply(preset);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Time & weather", title);
            if (sky != null)
            {
                var hour = Slider("Time of day", sky.TimeOfDay, 0, 24, "00.00");
                if (!Mathf.Approximately(hour, sky.TimeOfDay)) sky.SetTime(hour);
            }
            var weather = WeatherManager.Active;
            if (weather != null)
            {
                var states = System.Enum.GetNames(typeof(WeatherManager.State));
                var chosen = GUILayout.SelectionGrid((int)weather.Current, states, 4, GUILayout.Height(48));
                if (chosen != (int)weather.Current) weather.Set((WeatherManager.State)chosen);
            }
            cloud = Slider("Cloud coverage (−1 auto)", cloud, -1, 1);
            EnvironmentTuning.CloudOverride = cloud < 0 ? (weather != null ? EnvironmentTuning.CloudOverride : -1) : cloud;
            rain = Slider("Rain intensity (−1 auto)", rain, -1, 1);
            EnvironmentTuning.RainOverride = rain < 0 ? -1 : rain;
            EnvironmentTuning.WindScale = Slider("Wind strength ×", EnvironmentTuning.WindScale, 0, 4);
            EnvironmentTuning.FogDensity = Slider("Fog density ×", EnvironmentTuning.FogDensity, 0, 5);
            EnvironmentTuning.WaveStrength = Slider("Water wave strength ×", EnvironmentTuning.WaveStrength, 0, 2.5f);

            GUILayout.Space(6);
            GUILayout.Label("Light", title);
            EnvironmentTuning.SunIntensity = Slider("Sun intensity ×", EnvironmentTuning.SunIntensity, 0, 2);
            EnvironmentTuning.SunTemperatureOffset = Slider("Sun colour temp. (K offset)", EnvironmentTuning.SunTemperatureOffset, -2000, 2000, "0");
            EnvironmentTuning.ShadowStrength = Slider("Shadow strength ×", EnvironmentTuning.ShadowStrength, 0, 1.2f);
            if (quality != null)
            {
                var distance = Slider("Shadow distance (m)", quality.ShadowDistance, 10, 250, "0");
                if (Mathf.Abs(distance - quality.ShadowDistance) > 0.5f) quality.SetShadowDistance(distance);
            }
            EnvironmentTuning.ExposureOffset = Slider("Exposure (EV)", EnvironmentTuning.ExposureOffset, -2, 2);
            EnvironmentTuning.BloomIntensity = Slider("Bloom", EnvironmentTuning.BloomIntensity, 0, 1);
            EnvironmentTuning.IndoorLights = Slider("Indoor light intensity ×", EnvironmentTuning.IndoorLights, 0, 2);
            EnvironmentTuning.GrassDensity = Slider("Grass density", EnvironmentTuning.GrassDensity, 0, 1);
            var resolution = Mathf.RoundToInt(Slider("Reflection quality (px)", reflectionResolution, 32, 512, "0"));
            resolution = Mathf.ClosestPowerOfTwo(resolution);
            if (resolution != reflectionResolution && sky != null && sky.SkyProbe != null)
            {
                reflectionResolution = resolution;
                sky.SkyProbe.resolution = resolution;
                sky.SkyProbe.RenderProbe();
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep tuning")) Save();
            if (GUILayout.Button("Reset to defaults")) { EnvironmentTuning.Reset(); cloud = rain = -1; PlayerPrefs.DeleteKey(DevKey); }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Save()
        {
            PlayerPrefs.SetString(DevKey, string.Join(";", new[]
            {
                EnvironmentTuning.SunIntensity, EnvironmentTuning.SunTemperatureOffset, EnvironmentTuning.ShadowStrength, EnvironmentTuning.ExposureOffset,
                EnvironmentTuning.FogDensity, EnvironmentTuning.WindScale, EnvironmentTuning.WaveStrength, EnvironmentTuning.BloomIntensity, EnvironmentTuning.IndoorLights,
            }));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            var text = PlayerPrefs.GetString(DevKey, "");
            if (text == "") return;
            var v = System.Array.ConvertAll(text.Split(';'), part => float.TryParse(part, out var value) ? value : float.NaN);
            if (v.Length < 9) return;
            float Pick(int i, float fallback) => float.IsNaN(v[i]) ? fallback : v[i];
            EnvironmentTuning.SunIntensity = Pick(0, 1); EnvironmentTuning.SunTemperatureOffset = Pick(1, 0); EnvironmentTuning.ShadowStrength = Pick(2, 1);
            EnvironmentTuning.ExposureOffset = Pick(3, 0); EnvironmentTuning.FogDensity = Pick(4, 1); EnvironmentTuning.WindScale = Pick(5, 1);
            EnvironmentTuning.WaveStrength = Pick(6, 1); EnvironmentTuning.BloomIntensity = Pick(7, 0.12f); EnvironmentTuning.IndoorLights = Pick(8, 1);
        }
    }
}
