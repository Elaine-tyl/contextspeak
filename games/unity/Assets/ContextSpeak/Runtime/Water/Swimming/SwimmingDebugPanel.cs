using System;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// Swimming debug panel (F5): live values from the real controller and sliders that change the
    /// live tuning multipliers the controller uses every frame (speed, buoyancy, damping, waterline,
    /// bobbing, animation blending, wakes, splashes).
    public sealed class SwimmingDebugPanel : MonoBehaviour
    {
        public Key Toggle = Key.F5;
        private VisualElement panel;
        private Label info;
        private float refresh;
        private SwimmingController swim;

        private SwimTuning T => swim != null ? swim.Tuning : null;

        private void Start()
        {
            var root = GameUiRoot.Attach(gameObject);
            panel = new VisualElement { name = "swim-debug" };
            panel.style.position = Position.Absolute;
            panel.style.left = 16; panel.style.top = 120; panel.style.width = 350;
            panel.style.backgroundColor = new Color(0.05f, 0.08f, 0.1f, 0.88f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 10;
            panel.style.paddingLeft = panel.style.paddingRight = 12; panel.style.paddingTop = panel.style.paddingBottom = 10;
            panel.style.display = DisplayStyle.None;
            var title = new Label("Swimming  ·  F5");
            title.style.color = Color.white; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginBottom = 6;
            panel.Add(title);
            info = new Label();
            info.style.color = new Color(0.85f, 0.93f, 0.96f); info.style.whiteSpace = WhiteSpace.Normal; info.style.fontSize = 12; info.style.marginBottom = 8;
            panel.Add(info);
            AddSlider("Swim speed ×", 0.3f, 2.5f, () => T?.SpeedScale ?? 1, v => { if (T != null) T.SpeedScale = v; });
            AddSlider("Buoyancy strength ×", 0.7f, 1.3f, () => T?.BuoyancyScale ?? 1, v => { if (T != null) T.BuoyancyScale = v; });
            AddSlider("Vertical damping ×", 0.2f, 3f, () => T?.DampingScale ?? 1, v => { if (T != null) T.DampingScale = v; });
            AddSlider("Waterline offset (m)", -0.3f, 0.3f, () => T?.WaterlineOffset ?? 0, v => { if (T != null) T.WaterlineOffset = v; });
            AddSlider("Bobbing ×", 0f, 3f, () => T?.BobScale ?? 1, v => { if (T != null) T.BobScale = v; });
            AddSlider("Animation blend ×", 0.2f, 3f, () => T?.AnimationBlendScale ?? 1, v => { if (T != null) T.AnimationBlendScale = v; });
            AddSlider("Wake strength ×", 0f, 3f, () => T?.WakeScale ?? 1, v => { if (T != null) T.WakeScale = v; });
            AddSlider("Splash intensity ×", 0f, 3f, () => T?.SplashScale ?? 1, v => { if (T != null) T.SplashScale = v; });
            panel.Add(new Button(() => { if (swim != null) swim.Tuning = new SwimTuning(); }) { text = "Reset tuning" });
            root.Add(panel);
        }

        private void AddSlider(string name, float min, float max, Func<float> get, Action<float> set)
        {
            var slider = new Slider(name, min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(e => set(e.newValue));
            slider.schedule.Execute(() => { if (panel.style.display == DisplayStyle.Flex && slider.panel?.focusController?.focusedElement != slider) slider.SetValueWithoutNotify(get()); }).Every(400);
            slider.style.marginBottom = 2;
            slider.Query<Label>().ForEach(label => { label.style.color = new Color(0.9f, 0.95f, 0.97f); label.style.fontSize = 12; label.style.minWidth = 150; });
            panel.Add(slider);
        }

        private void Update()
        {
            if (panel == null) return;
            if (Keyboard.current != null && Keyboard.current[Toggle].wasPressedThisFrame)
                panel.style.display = panel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (panel.style.display == DisplayStyle.None || (refresh -= Time.unscaledDeltaTime) > 0) return;
            refresh = 0.15f;
            if (swim == null) swim = FindFirstObjectByType<SwimmingController>();
            if (swim == null) { info.text = "No SwimmingController in this scene."; return; }
            var anim = swim.GetComponent<SwimmingAnimationController>();
            var d = swim.Detector;
            var pos = swim.transform.position;
            info.text =
                "State: " + swim.State + (swim.ExitPhase != "" ? "  (" + swim.ExitPhase + ")" : "") +
                "\nWater body: " + (swim.Body != null ? swim.Body.name + " (" + swim.Body.Kind + ")" : "none") + "  ·  profile " + (swim.Profile != null ? swim.Profile.name : "—") +
                "\nSurface: " + (d.Body != null ? d.RawSurface.ToString("0.00") + " m (smoothed " + swim.Surface.Height.ToString("0.00") + ")" : "—") + "  ·  depth " + d.Depth.ToString("0.00") + " m" +
                "\nPosition: " + pos.ToString("0.00") +
                "\nVertical velocity: " + swim.VerticalVelocity.ToString("0.00") + " m/s  ·  buoyancy accel " + swim.Buoyancy.Acceleration.ToString("0.00") + " m/s²" +
                "\nHorizontal speed: " + swim.HorizontalSpeed.ToString("0.00") + " m/s" +
                "\nImmersion: " + Mathf.RoundToInt(d.Immersion * 100) + "%  ·  head " + (d.HeadDepth > 0 ? "under by " + d.HeadDepth.ToString("0.00") : "above") +
                "\nAnimation: " + (anim != null ? anim.CurrentAnimation : "—") +
                "\nCamera underwater: " + (swim.CameraUnderwater ? "yes" : "no") + "  ·  breath " + Mathf.RoundToInt(swim.Breath * 100) + "%" +
                "\nCPU: " + swim.CpuMilliseconds.ToString("0.000") + " ms/frame";
        }
    }
}
