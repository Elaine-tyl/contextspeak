using System.Collections.Generic;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// In-world action HUD shared by driving and swimming, styled after current open-world games but in
    /// ContextSpeak's warm theme (paper / forest / amber, Source Sans):
    ///  • key-cap prompts ("[E] Drive the taxi") bottom-centre, fading in and out (RDR2 / GTA style);
    ///  • a round speedometer bottom-right with a sweeping arc, big digits, gear and lights indicators
    ///    (Forza Horizon style);
    ///  • a breath wheel beside the character that only appears when it matters (Zelda: BotW stamina
    ///    wheel), turning amber then red as it runs out;
    ///  • a small activity chip ("Swimming · Pool") top-centre.
    /// One instance per scene, created on demand; everything is drawn with UI Toolkit (Painter2D arcs).
    public sealed class ActionHud : MonoBehaviour
    {
        private static ActionHud instance;
        public static ActionHud Instance
        {
            get
            {
                if (instance == null && Application.isPlaying) instance = new GameObject("Action HUD").AddComponent<ActionHud>();
                return instance;
            }
        }

        private static readonly Color Paper = new Color32(0xF7, 0xF3, 0xEA, 0xFF);
        private static readonly Color Ink = new Color32(0x1F, 0x2A, 0x26, 0xFF);
        private static readonly Color Forest = new Color32(0x2E, 0x5E, 0x45, 0xFF);
        private static readonly Color Mint = new Color32(0x8F, 0xD3, 0xA8, 0xFF);
        private static readonly Color Amber = new Color32(0xE0, 0x9A, 0x3E, 0xFF);
        private static readonly Color Danger = new Color32(0xE0, 0x5A, 0x45, 0xFF);
        private static readonly Color Water = new Color32(0x7F, 0xD4, 0xF0, 0xFF);

        private VisualElement root, promptRow, gauge, breath, chip;
        private readonly Dictionary<string, (VisualElement row, float until)> prompts = new Dictionary<string, (VisualElement, float)>();
        private Label speedDigits, speedUnit, gear, lights, chipLabel;
        private float speedValue, speedMax = 16, breathValue = 1, breathShown, gaugeShown, chipUntil;
        private Vector3 breathAnchor;
        private bool breathVisible, gaugeVisible;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            root = GameUiRoot.Attach(gameObject);
            Build();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        // ------------------------------------------------------------------ public API

        /// Shows a key-cap prompt for this frame (call every frame while it applies). Several can stack.
        public void Prompt(string id, string key, string text)
        {
            if (!prompts.TryGetValue(id, out var entry))
            {
                entry.row = PromptRow(key, text);
                promptRow.Add(entry.row);
            }
            else Set(entry.row, key, text);
            entry.until = Time.unscaledTime + 0.15f;
            prompts[id] = entry;
        }

        /// A prompt that stays for a few seconds (e.g. "Slow down to get out").
        public void Notice(string id, string key, string text, float seconds = 2.5f)
        {
            Prompt(id, key, text);
            var entry = prompts[id];
            entry.until = Time.unscaledTime + seconds;
            prompts[id] = entry;
        }

        public void Speed(bool visible, float metresPerSecond = 0, float max = 16, string gearText = "D", bool headlights = false)
        {
            gaugeVisible = visible;
            if (!visible) return;
            speedValue = Mathf.Abs(metresPerSecond);
            speedMax = Mathf.Max(5, max);
            speedDigits.text = Mathf.RoundToInt(speedValue * 3.6f).ToString();
            gear.text = gearText;
            gear.style.color = gearText == "R" ? Amber : gearText == "P" ? new Color(1, 1, 1, 0.55f) : Mint;
            lights.style.opacity = headlights ? 1 : 0.22f;
            gauge.MarkDirtyRepaint();
        }

        /// Breath 0…1 next to the character's head (world point); hidden when full.
        public void Breath(bool visible, float value, Vector3 worldAnchor)
        {
            breathVisible = visible;
            breathValue = Mathf.Clamp01(value);
            breathAnchor = worldAnchor;
            breath.MarkDirtyRepaint();
        }

        public void Activity(string text, float seconds = 3)
        {
            chipLabel.text = text;
            chipUntil = Time.unscaledTime + seconds;
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            promptRow = new VisualElement { name = "hud-prompts", pickingMode = PickingMode.Ignore };
            promptRow.style.position = Position.Absolute;
            promptRow.style.left = 0; promptRow.style.right = 0; promptRow.style.bottom = 120;
            promptRow.style.flexDirection = FlexDirection.Row;
            promptRow.style.justifyContent = Justify.Center;
            root.Add(promptRow);

            // Speedometer.
            gauge = new VisualElement { name = "hud-speedometer", pickingMode = PickingMode.Ignore };
            gauge.style.position = Position.Absolute;
            gauge.style.right = 44; gauge.style.bottom = 40;
            gauge.style.width = 210; gauge.style.height = 210;
            gauge.style.alignItems = Align.Center; gauge.style.justifyContent = Justify.Center;
            gauge.style.opacity = 0;
            gauge.generateVisualContent += DrawGauge;
            speedDigits = Text("0", 54, Paper, FontStyle.Bold);
            speedDigits.style.marginTop = 6;
            speedUnit = Text("km/h", 15, new Color(1, 1, 1, 0.7f), FontStyle.Normal);
            speedUnit.style.marginTop = -6;
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.flexDirection = FlexDirection.Row; bottom.style.marginTop = 10; bottom.style.alignItems = Align.Center;
            gear = Text("D", 20, Mint, FontStyle.Bold);
            gear.style.paddingLeft = gear.style.paddingRight = 9;
            gear.style.borderTopLeftRadius = gear.style.borderTopRightRadius = gear.style.borderBottomLeftRadius = gear.style.borderBottomRightRadius = 6;
            gear.style.backgroundColor = new Color(1, 1, 1, 0.1f);
            lights = Text("◐", 18, Mint, FontStyle.Normal);
            lights.style.marginLeft = 10;
            bottom.Add(gear); bottom.Add(lights);
            gauge.Add(speedDigits); gauge.Add(speedUnit); gauge.Add(bottom);
            root.Add(gauge);

            // Breath wheel.
            breath = new VisualElement { name = "hud-breath", pickingMode = PickingMode.Ignore };
            breath.style.position = Position.Absolute;
            breath.style.width = 46; breath.style.height = 46;
            breath.style.opacity = 0;
            breath.generateVisualContent += DrawBreath;
            root.Add(breath);

            // Activity chip.
            chip = new VisualElement { name = "hud-activity", pickingMode = PickingMode.Ignore };
            chip.style.position = Position.Absolute;
            chip.style.top = 22; chip.style.left = 0; chip.style.right = 0;
            chip.style.alignItems = Align.Center;
            chip.style.opacity = 0;
            chipLabel = Text("", 15, Paper, FontStyle.Bold);
            Pill(chipLabel, new Color(0.12f, 0.16f, 0.15f, 0.72f));
            chipLabel.style.letterSpacing = 1;
            chip.Add(chipLabel);
            root.Add(chip);
        }

        private static Label Text(string text, int size, Color colour, FontStyle style)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size; label.style.color = colour; label.style.unityFontStyleAndWeight = style;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private static void Pill(VisualElement element, Color background)
        {
            element.style.backgroundColor = background;
            element.style.paddingLeft = element.style.paddingRight = 16; element.style.paddingTop = element.style.paddingBottom = 6;
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = 999;
            element.style.borderTopWidth = element.style.borderBottomWidth = element.style.borderLeftWidth = element.style.borderRightWidth = 1;
            element.style.borderTopColor = element.style.borderBottomColor = element.style.borderLeftColor = element.style.borderRightColor = new Color(1, 1, 1, 0.18f);
        }

        private VisualElement PromptRow(string key, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
            row.style.marginLeft = row.style.marginRight = 10;
            row.style.backgroundColor = new Color(0.12f, 0.16f, 0.15f, 0.74f);
            row.style.paddingLeft = 6; row.style.paddingRight = 18; row.style.paddingTop = row.style.paddingBottom = 6;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 12;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new Color(1, 1, 1, 0.16f);
            row.style.opacity = 0;
            row.style.translate = new Translate(0, 8);
            row.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
            row.style.transitionDuration = new List<TimeValue> { new TimeValue(160, TimeUnit.Millisecond) };
            var cap = Text(key, 17, Ink, FontStyle.Bold);
            cap.name = "cap";
            cap.style.minWidth = 34; cap.style.height = 34;
            cap.style.paddingLeft = cap.style.paddingRight = 8;
            cap.style.backgroundColor = Paper;
            cap.style.borderTopLeftRadius = cap.style.borderTopRightRadius = cap.style.borderBottomLeftRadius = cap.style.borderBottomRightRadius = 8;
            cap.style.borderBottomWidth = 3; cap.style.borderBottomColor = (Color)new Color32(0xC9, 0xC1, 0xAE, 0xFF);
            cap.style.marginRight = 12;
            var label = Text(text, 18, Paper, FontStyle.Normal);
            label.name = "text";
            row.Add(cap); row.Add(label);
            row.schedule.Execute(() => { row.style.opacity = 1; row.style.translate = new Translate(0, 0); }).StartingIn(16);
            return row;
        }

        private static void Set(VisualElement row, string key, string text)
        {
            var cap = row.Q<Label>("cap"); var label = row.Q<Label>("text");
            if (cap.text != key) cap.text = key;
            if (label.text != text) label.text = text;
        }

        // ------------------------------------------------------------------ drawing

        private void DrawGauge(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var size = gauge.contentRect.size;
            var centre = size / 2;
            var radius = Mathf.Min(size.x, size.y) / 2 - 12;
            const float start = 135, sweep = 270;
            // Dark glass disc.
            p.fillColor = new Color(0.08f, 0.11f, 0.1f, 0.72f);
            p.BeginPath(); p.Arc(centre, radius + 10, 0, 360); p.Fill();
            // Track.
            p.lineWidth = 9; p.lineCap = LineCap.Round;
            p.strokeColor = new Color(1, 1, 1, 0.12f);
            p.BeginPath(); p.Arc(centre, radius - 6, start, start + sweep); p.Stroke();
            // Ticks every 20 km/h, numbers every 40.
            var maxKmh = Mathf.Ceil(speedMax * 3.6f / 20f) * 20f;
            p.lineWidth = 2; p.lineCap = LineCap.Butt;
            for (var kmh = 0f; kmh <= maxKmh + 0.1f; kmh += 10)
            {
                var a = (start + sweep * kmh / maxKmh) * Mathf.Deg2Rad;
                var major = Mathf.Approximately(kmh % 20, 0);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.strokeColor = new Color(1, 1, 1, major ? 0.55f : 0.25f);
                p.BeginPath(); p.MoveTo(centre + dir * (radius + 2)); p.LineTo(centre + dir * (radius - (major ? 8 : 4) + 2)); p.Stroke();
            }
            // Value arc: forest→mint, amber near the top.
            var fraction = Mathf.Clamp01(speedValue * 3.6f / maxKmh);
            if (fraction > 0.003f)
            {
                p.lineWidth = 9; p.lineCap = LineCap.Round;
                p.strokeColor = fraction > 0.85f ? Amber : Color.Lerp(Mint, Water, fraction);
                p.BeginPath(); p.Arc(centre, radius - 6, start, start + sweep * fraction); p.Stroke();
                var tip = (start + sweep * fraction) * Mathf.Deg2Rad;
                p.fillColor = Paper;
                p.BeginPath(); p.Arc(centre + new Vector2(Mathf.Cos(tip), Mathf.Sin(tip)) * (radius - 6), 5.5f, 0, 360); p.Fill();
            }
        }

        private void DrawBreath(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var size = breath.contentRect.size;
            var centre = size / 2;
            var radius = Mathf.Min(size.x, size.y) / 2 - 5;
            p.fillColor = new Color(0.08f, 0.11f, 0.1f, 0.55f);
            p.BeginPath(); p.Arc(centre, radius + 4, 0, 360); p.Fill();
            p.lineWidth = 6; p.lineCap = LineCap.Round;
            p.strokeColor = new Color(1, 1, 1, 0.15f);
            p.BeginPath(); p.Arc(centre, radius, 0, 360); p.Stroke();
            var colour = breathValue > 0.5f ? Water : breathValue > 0.25f ? Amber : Danger;
            if (breathValue < 0.25f) colour.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8));
            p.strokeColor = colour;
            if (breathValue > 0.002f) { p.BeginPath(); p.Arc(centre, radius, -90, -90 + 360 * breathValue); p.Stroke(); }
            // Bubble glyph.
            p.fillColor = new Color(colour.r, colour.g, colour.b, 0.9f);
            p.BeginPath(); p.Arc(centre + new Vector2(-3, 3), 4.5f, 0, 360); p.Fill();
            p.BeginPath(); p.Arc(centre + new Vector2(4, -4), 2.6f, 0, 360); p.Fill();
        }

        // ------------------------------------------------------------------ per frame

        private readonly List<string> expired = new List<string>();

        private void LateUpdate()
        {
            var now = Time.unscaledTime;
            expired.Clear();
            foreach (var pair in prompts) if (now > pair.Value.until) expired.Add(pair.Key);
            foreach (var id in expired) { prompts[id].row.RemoveFromHierarchy(); prompts.Remove(id); }

            var dt = Time.unscaledDeltaTime;
            gaugeShown = Mathf.MoveTowards(gaugeShown, gaugeVisible ? 1 : 0, dt * 4);
            gauge.style.opacity = gaugeShown;
            gauge.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.92f, 1, gaugeShown));
            gaugeVisible = false;                                         // must be refreshed every frame

            breathShown = Mathf.MoveTowards(breathShown, breathVisible ? 1 : 0, dt * 3);
            breath.style.opacity = breathShown;
            if (breathShown > 0 && Camera.main != null && root.panel != null)
            {
                var screen = Camera.main.WorldToViewportPoint(breathAnchor);
                if (screen.z > 0)
                {
                    var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, breathAnchor, Camera.main);
                    var local = root.WorldToLocal(panelPoint);
                    breath.style.left = local.x + 40; breath.style.top = local.y - 60;
                }
                if (breathValue < 0.25f) breath.MarkDirtyRepaint();
            }
            breathVisible = false;

            chip.style.opacity = Mathf.MoveTowards(chip.resolvedStyle.opacity, now < chipUntil ? 1 : 0, dt * 3);
        }
    }
}
