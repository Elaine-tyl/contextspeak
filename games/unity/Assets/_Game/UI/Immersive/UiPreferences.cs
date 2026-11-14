using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    public enum HudMode { Off, Minimal, Full }

    public sealed class UiPreferences
    {
        // A separate PlayerPrefs namespace: never read or mutate any gameplay save.
        private const string Prefix = "ContextSpeak.UI.v1.";
        private static UiPreferences current;
        public static UiPreferences Current => current ?? (current = new UiPreferences());
        public static event Action Changed;
        private float scale;
        private int subtitleSize;
        private HudMode hud;
        private bool reduceMotion, colourBlind;

        private UiPreferences()
        {
            scale = UiPolicy.Scale(PlayerPrefs.GetFloat(Prefix + "scale", 1f));
            subtitleSize = UiPolicy.SubtitleSize(PlayerPrefs.GetInt(Prefix + "subtitle", 24));
            hud = (HudMode)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "hud", 1), 0, 2);
            reduceMotion = PlayerPrefs.GetInt(Prefix + "motion", 0) == 1;
            colourBlind = PlayerPrefs.GetInt(Prefix + "colour", 0) == 1;
        }
        public float Scale { get => scale; set { var next = UiPolicy.Scale(value); if (scale == next) return; scale = next; Save(); } }
        public int SubtitleSize { get => subtitleSize; set { var next = UiPolicy.SubtitleSize(value); if (subtitleSize == next) return; subtitleSize = next; Save(); } }
        public HudMode Hud { get => hud; set { var next = (HudMode)Mathf.Clamp((int)value, 0, 2); if (hud == next) return; hud = next; Save(); } }
        public bool ReduceMotion { get => reduceMotion; set { if (reduceMotion == value) return; reduceMotion = value; Save(); } }
        public bool ColourBlind { get => colourBlind; set { if (colourBlind == value) return; colourBlind = value; Save(); } }
        private void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "scale", scale);
            PlayerPrefs.SetInt(Prefix + "subtitle", subtitleSize);
            PlayerPrefs.SetInt(Prefix + "hud", (int)hud);
            PlayerPrefs.SetInt(Prefix + "motion", reduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "colour", colourBlind ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
        public static void AddControls(VisualElement parent)
        {
            var prefs = Current;
            var scaleControl = new Slider("UI scale", 0.8f, 1.4f) { value = prefs.Scale, showInputField = true };
            var subtitles = new SliderInt("Subtitle size", 18, 32) { value = prefs.SubtitleSize, showInputField = true };
            var hudControl = new DropdownField("HUD", new List<string> { "Off", "Minimal", "Full" }, (int)prefs.Hud);
            var motion = new Toggle("Reduce motion") { value = prefs.ReduceMotion };
            var colour = new Toggle("Colour-blind safe accents") { value = prefs.ColourBlind };
            scaleControl.RegisterValueChangedCallback(e => prefs.Scale = e.newValue);
            subtitles.RegisterValueChangedCallback(e => prefs.SubtitleSize = e.newValue);
            hudControl.RegisterValueChangedCallback(e => prefs.Hud = (HudMode)hudControl.index);
            motion.RegisterValueChangedCallback(e => prefs.ReduceMotion = e.newValue);
            colour.RegisterValueChangedCallback(e => prefs.ColourBlind = e.newValue);
            parent.Add(scaleControl); parent.Add(subtitles); parent.Add(hudControl); parent.Add(motion); parent.Add(colour);
            var note = new Label("Settings save automatically on this device.");
            note.AddToClassList("practice-note");
            parent.Add(note);
        }
    }
}
