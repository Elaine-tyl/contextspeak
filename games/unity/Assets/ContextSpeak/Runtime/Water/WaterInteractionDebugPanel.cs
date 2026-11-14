using System;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// In-game water interaction tuning (F4): live values from the real simulation and the player,
    /// and sliders that change the actual ripple simulation, the active water profile and the effects.
    /// Profile changes made in Play mode are written to the profile asset (that is the point: tune, keep).
    public sealed class WaterInteractionDebugPanel : MonoBehaviour
    {
        public Key Toggle = Key.F4;
        private VisualElement panel;
        private Label info;
        private float refresh;
        private CharacterWaterInteraction character;

        private void Start()
        {
            var root = GameUiRoot.Attach(gameObject);
            panel = new VisualElement { name = "water-debug" };
            panel.style.position = Position.Absolute;
            panel.style.right = 16; panel.style.top = 120; panel.style.width = 340;
            panel.style.backgroundColor = new Color(0.06f, 0.08f, 0.1f, 0.86f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 10;
            panel.style.paddingLeft = panel.style.paddingRight = 12; panel.style.paddingTop = panel.style.paddingBottom = 10;
            panel.style.display = DisplayStyle.None;
            var title = new Label("Water interaction  ·  F4");
            title.style.color = Color.white; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginBottom = 6;
            panel.Add(title);
            info = new Label();
            info.style.color = new Color(0.85f, 0.92f, 0.95f); info.style.whiteSpace = WhiteSpace.Normal; info.style.marginBottom = 8; info.style.fontSize = 12;
            panel.Add(info);
            AddSlider("Ripple size", 0.3f, 3, () => character != null ? character.RippleSize : 1, v => { if (character != null) character.RippleSize = v; });
            AddSlider("Ripple strength", 0, 3, () => Sim?.NormalStrength ?? 1, v => { if (Sim) { Sim.Override = true; Sim.NormalStrength = v; } });
            AddSlider("Propagation speed (m/s)", 0.3f, 3, () => Sim?.WaveSpeed ?? 1.2f, v => { if (Sim) { Sim.Override = true; Sim.WaveSpeed = v; } });
            AddSlider("Damping / step", 0.95f, 0.999f, () => Sim?.Damping ?? 0.985f, v => { if (Sim) { Sim.Override = true; Sim.Damping = v; } });
            AddSlider("Splash intensity", 0, 3, () => Profile?.SplashScale ?? 1, v => { if (Profile) Profile.SplashScale = v; });
            AddSlider("Foam amount", 0, 3, () => Sim?.FoamAmount ?? 1, v => { if (Sim) { Sim.Override = true; Sim.FoamAmount = v; } if (Profile) Profile.FoamAmount = v; });
            AddSlider("Particle count", 0.2f, 2.5f, () => WaterSplashController.Instance?.ParticleScale ?? 1, v => { if (WaterSplashController.Instance) WaterSplashController.Instance.ParticleScale = v; });
            AddSlider("Wake length", 0, 3, () => Profile?.WakeLength ?? 1, v => { if (Profile) Profile.WakeLength = v; });
            AddSlider("Underwater bubbles", 0, 3, () => Profile?.BubbleIntensity ?? 1, v => { if (Profile) Profile.BubbleIntensity = v; });
            var resolution = new DropdownField("Disturbance resolution", new System.Collections.Generic.List<string> { "256", "512", "1024" }, 1);
            resolution.RegisterValueChangedCallback(e => { if (Sim) Sim.Allocate(int.Parse(e.newValue)); });
            Style(resolution);
            panel.Add(resolution);
            var reset = new Button(() => { if (Sim) Sim.Override = false; }) { text = "Use profile values" };
            panel.Add(reset);
            root.Add(panel);
        }

        private static WaterRippleSimulation Sim => WaterRippleSimulation.Instance;
        private WaterInteractionProfile Profile => character != null && character.Body != null ? character.Body.ActiveProfile : Sim != null && Sim.Body != null ? Sim.Body.ActiveProfile : null;

        private static void Style(VisualElement element)
        {
            element.style.color = Color.white;
            element.style.marginBottom = 2;
            element.Query<Label>().ForEach(label => { label.style.color = new Color(0.9f, 0.95f, 0.97f); label.style.fontSize = 12; label.style.minWidth = 150; });
        }

        private void AddSlider(string name, float min, float max, Func<float> get, Action<float> set)
        {
            var slider = new Slider(name, min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(e => set(e.newValue));
            slider.schedule.Execute(() => { if (panel.style.display == DisplayStyle.Flex && slider.panel?.focusController?.focusedElement != slider) slider.SetValueWithoutNotify(get()); }).Every(500);
            Style(slider);
            panel.Add(slider);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[Toggle].wasPressedThisFrame)
                panel.style.display = panel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (panel == null || panel.style.display == DisplayStyle.None || (refresh -= Time.unscaledDeltaTime) > 0) return;
            refresh = 0.2f;
            if (character == null) character = FindFirstObjectByType<CharacterWaterInteraction>();
            var body = character != null ? character.Body : Sim != null ? Sim.Body : null;
            info.text =
                "Water body: " + (body != null ? body.name + " (" + body.Kind + ")" : "none") +
                "\nPlayer state: " + (character != null ? character.State.ToString() : "no CharacterWaterInteraction") +
                "\nSurface height: " + (character != null && character.Detector != null && character.Detector.Body != null ? character.Detector.Surface.ToString("0.00") + " m  ·  depth " + character.Detector.Depth.ToString("0.00") + " m" : "—") +
                "\nHorizontal speed: " + (character != null ? character.HorizontalSpeed.ToString("0.0") : "—") + " m/s  ·  entry speed " + (character != null ? character.EntrySpeed.ToString("0.0") : "—") + " m/s" +
                "\nActive ripple sources: " + (Sim != null ? Sim.ActiveSources.ToString() : "—") + "  ·  sim " + (Sim != null ? Sim.CurrentResolution + "² · " + Sim.LastStepMilliseconds.ToString("0.00") + " ms CPU" : "off") +
                "\nParticles emitted: " + (WaterSplashController.Instance != null ? WaterSplashController.Instance.ParticlesEmitted.ToString() : "—");
        }
    }
}
