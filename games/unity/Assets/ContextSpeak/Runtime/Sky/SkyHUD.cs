using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// A small clock/weather chip at the top of the screen; F2 opens the sky panel (time, day
    /// length, clouds, wind, weather and the seven presets).
    public sealed class SkyHUD : MonoBehaviour
    {
        public bool ShowChip = true;
        private bool open;
        private GUIStyle chip, label, button;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame) SetOpen(!open);
            if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LifeUI.RequestBack();
        }

        private static string Icon()
        {
            if (SkyState.Raining) return "☂";
            if (SkyState.IsNight) return SkyState.CloudCover > 0.7f ? "☁" : "☾";
            return SkyState.CloudCover > 0.55f ? "☁" : "☀";
        }

        private void SetOpen(bool value) { open = value; LifeUI.SetPanelOpen(this, open, BackPanel, 100); }
        private bool BackPanel() { SetOpen(false); return true; }
        private void OnDisable() { LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (open) SetOpen(true); }

        private void DrawToolkit()
        {
            var sky = SkySystem.Active;
            if (sky == null) return;
            if (chip == null)
            {
                chip = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                chip.normal.textColor = Color.white;
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                label.normal.textColor = Color.white;
                button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            }
            if (ShowChip && LifeUI.Instance != null && ContextSpeak.Cafe.UI.UiPreferences.Current.Hud != ContextSpeak.Cafe.UI.HudMode.Off) GUI.Box(new Rect(20, 16, 300, 32), Icon() + "  " + SkyState.Clock + "  ·  " + SkyState.Weather, chip);
            if (!open) return;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 230, 40, 460, 470), GUI.skin.box);
            GUILayout.Label("Sky & weather", label);
            GUILayout.Label("Time " + SkyState.Clock + "   sun " + SkyState.SunElevation.ToString("0") + "°", label);
            var hour = GUILayout.HorizontalSlider(sky.TimeOfDay, 0, 23.99f);
            if (Mathf.Abs(hour - sky.TimeOfDay) > 0.01f) sky.SetTime(hour);
            GUILayout.Label("Day length: " + sky.DayLengthMinutes.ToString("0") + " real minutes", label);
            sky.DayLengthMinutes = Mathf.Round(GUILayout.HorizontalSlider(sky.DayLengthMinutes, 2, 120));
            GUILayout.Label("Cloud coverage: " + (sky.CloudCoverageOverride < 0 ? "from weather" : sky.CloudCoverageOverride.ToString("0.00")), label);
            sky.CloudCoverageOverride = GUILayout.HorizontalSlider(sky.CloudCoverageOverride, -1, 1);
            GUILayout.Label("Wind: " + sky.WindSpeed.ToString("0.0") + " m/s", label);
            sky.WindSpeed = GUILayout.HorizontalSlider(sky.WindSpeed, 0, 15);
            GUILayout.BeginHorizontal();
            foreach (SkyWeather weather in System.Enum.GetValues(typeof(SkyWeather)))
                if (GUILayout.Button(weather.ToString(), button)) sky.SetWeather(weather);
            GUILayout.EndHorizontal();
            GUILayout.Label("Presets (smooth transition)", label);
            var presets = (SkyPreset[])System.Enum.GetValues(typeof(SkyPreset));
            for (var i = 0; i < presets.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + 3, presets.Length); j++)
                    if (GUILayout.Button(presets[j].ToString(), button)) sky.ApplyPreset(presets[j]);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            sky.Paused = GUILayout.Toggle(sky.Paused, " Pause time", label);
            sky.AutoWeather = GUILayout.Toggle(sky.AutoWeather, " Random weather", label);
            sky.AllowNight = GUILayout.Toggle(sky.AllowNight, " Night", label);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
