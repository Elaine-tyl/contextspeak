using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Editor
{
    /// Native checks run against rendered Play-mode panels, not reconstructed HTML or mock rectangles.
    public static class ImmersiveUiChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/UI/Check speaking repeat gate")]
        public static void VerifyDomain()
        {
            const string target = "Can I have a croissant?";
            Require(!SpeakingCoach.PracticePassed("Can I has a croissant?", target, IeltsBand.Band5), "An uncorrected repeat must be blocked.");
            Require(!SpeakingCoach.PracticePassed("Can I have two croissants?", target, IeltsBand.Band5), "A changed quantity must be blocked.");
            Require(SpeakingCoach.PracticePassed(target, target, IeltsBand.Band5), "A correct repeat must pass.");
            // Uses the real SliceGame.Submit/Command flow in an isolated object. In particular this
            // checks that typed rehearsal cannot place an order and spoken correction commits once.
            VerifySpeakingTransactions();
            VerifyRoutePause();
            VerifyStoryLauncherBinding();
            VerifyTheme();
            Debug.Log("Immersive UI domain checks passed (synthetic input, not microphone/AI evidence).");
        }

        private static void VerifySpeakingTransactions()
        {
            // A focused fixture uses real SliceGame transactions. It starts no scene, makes no
            // requests, and suppresses rendering/persistence via the existing teardown guard.
            // Keep art/render-target regression checks in SliceRuntimeChecks, independently.
            var host = new GameObject("Isolated immersive speaking checks") { hideFlags = HideFlags.DontSave };
            var game = host.AddComponent<SliceGame>();
            game.enabled = false;
            SetField(game, "quitting", true);
            var client = host.AddComponent<CafeClient>();
            SetField(game, "client", client);
            SetField(game, "ui", host.AddComponent<SliceInterface>());
            SetField(game, "voice", host.AddComponent<SliceVoice>());
            SetField(game, "recorder", host.AddComponent<CafeRecorder>());
            var save = new SliceSave { Strictness = Strictness.Guided };
            SetField(game, "<Save>k__BackingField", save);
            SetField(game, "<Talking>k__BackingField", Partner.Rowan);
            try
            {
                // Availability is used only for the rehearsal branch; no coroutine or transport
                // is started while the sentinel token is present.
                SetField(client, "token", "isolated-rehearsal-availability");
                SetField(game, "companion", new CompanionServices(client));
                game.Submit("Can I have a croissant?", false);
                Require(save.Cart.Empty && !save.Earned.Contains("order_item"), "Typed rehearsal cannot place an initial order.");
                SetField(client, "token", "");
                game.Submit("Can I has a croissant?", true);
                Require(save.Cart.Empty && !save.Earned.Contains("order_item") && !save.Progress.Objectives.Contains("order_item"), "Feedback/correction must hold the order and its objective.");
                game.Submit("Can I has a croissant?", true);
                Require(save.Cart.Empty && !save.Earned.Contains("order_item"), "A wrong spoken repeat cannot advance the order.");
                SetField(client, "token", "isolated-rehearsal-availability");
                game.Submit("Can I have a croissant?", false);
                game.OnBrowserCommand("{\"action\":\"continue\"}");
                Require(save.Cart.Empty && !save.Earned.Contains("order_item"), "Typed correction and Continue cannot unlock the spoken-repeat gate.");
                SetField(client, "token", "");
                game.Submit("Can I have a croissant?", true);
                Require(save.Cart.Lines.Count == 1 && save.Earned.Contains("order_item"), "The correct spoken repeat must commit its pending order.");
                game.OnBrowserCommand("{\"action\":\"continue\"}");
                game.OnBrowserCommand("{\"action\":\"continue\"}");
                Require(save.Cart.Lines.Count == 1 && save.Progress.Turns.Count(turn => turn.Objective == "order_item") == 1, "Repeated Continue must not commit or credit the accepted order twice.");
                game.Submit("That's all, thanks", true);
                game.Submit("Yes, that's everything, thanks", true);
                var balance = save.Wallet.Card;
                game.Submit("Can I has that by card?", true);
                Require(save.Wallet.Card == balance && save.Receipts.Count == 0, "Payment remains blocked until its spoken correction.");
                game.Submit("Can I have that by card?", true);
                Require(save.Wallet.Card == balance - save.Cart.Total && save.Receipts.Count == 1 && save.Kitchen.Tickets.Count == 1, "Accepted payment must produce one charge, receipt and ticket.");
                game.Submit("Can I have that by card?", true);
                Require(save.Receipts.Count == 1 && save.Kitchen.Tickets.Count == 1, "Replaying payment cannot charge the same order twice.");
            }
            finally
            {
                SetField(client, "token", "");
                SetField(game, "quitting", true);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
        private static void SetField(object instance, string name, object value) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);

        private static void VerifyRoutePause()
        {
            var host = new GameObject("Isolated phone route pause check");
            try
            {
                var player = host.AddComponent<CafePlayer>();
                typeof(CafePlayer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                player.Route.Enqueue(host.transform.position);
                player.Controls.Suspended = true;
                typeof(CafePlayer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                Require(player.Route.Count == 1, "Opening the phone must pause route advancement as well as manual input.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void VerifyStoryLauncherBinding()
        {
            // The persistent story presenter exists before the additive café runtime appears.
            var host = new GameObject("Isolated additive phone ownership check");
            try
            {
                var director = host.AddComponent<ContextSpeak.LivingWorld.DynamicLifeEventManager>();
                var presenter = host.AddComponent<ContextSpeak.LivingWorld.LivingWorldInterface>();
                presenter.Bind(director);
                var toolbar = host.GetComponent<UIDocument>().rootVisualElement.Q("story-toolbar");
                Require(toolbar.style.display.value == DisplayStyle.Flex, "Standalone story presenter needs its launcher.");
                var game = host.AddComponent<SliceGame>();
                SetField(game, "quitting", true);
                SetField(director, "slice", game);
                typeof(ContextSpeak.LivingWorld.LivingWorldInterface).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);
                Require(toolbar.style.display.value == DisplayStyle.None, "Binding a café phone after Bootstrap must hide the old story launcher.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void VerifyTheme()
        {
            var sheet = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/UI/Theme.uss"));
            foreach (var selector in new[] { ".unity-button", ".unity-text-field__input", ".unity-base-popup-field__input", ".unity-toggle__checkmark", ".unity-base-slider__dragger", ".unity-scroller", ".unity-tab" })
                Require(sheet.Contains(selector), "Shared theme has no control style for " + selector);
            Require(sheet.Contains(":focus"), "Shared theme must provide visible keyboard focus.");
            Require(sheet.Contains("--paper: #F7F3EA") && sheet.Contains("--ink: #1F2A26"), "Shared theme tokens are absent.");
        }

        public static string Inspect(UIDocument document, string screen, bool resting)
        {
            Require(Application.isPlaying, "Layout acceptance requires Play mode.");
            Require(document != null && document.panelSettings != null, "Missing runtime UIDocument.");
            var settings = document.panelSettings;
            Require(settings.referenceResolution == new Vector2Int(1920, 1080), "UI reference resolution must be 1920 x 1080.");
            Require(settings.scaleMode == PanelScaleMode.ScaleWithScreenSize, "UI must scale with screen size.");
            Require(settings.themeStyleSheet != null, "A runtime theme stylesheet must be assigned.");
            var root = document.rootVisualElement;
            var bounds = root.worldBound;
            Require(bounds.width > 100 && bounds.height > 100 && !float.IsNaN(bounds.height), "Panel layout is not ready.");
            var checkedControls = 0;
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                if (!Visible(element) || !bounds.Overlaps(element.worldBound)) continue;
                // Content legitimately extends beyond a ScrollView viewport. Its viewport, rather
                // than its scrollable content, is the acceptance boundary.
                var scrollContent = WithinScrollContent(element);
                if (element is Button || element is TextField || element is DropdownField || element is Slider || element is Toggle)
                {
                    var rect = element.worldBound;
                    Require(rect.xMin >= bounds.xMin - 1 && rect.xMax <= bounds.xMax + 1
                        && (scrollContent || (rect.yMin >= bounds.yMin - 1 && rect.yMax <= bounds.yMax + 1)),
                        screen + " control is outside the safe viewport: " + Describe(element));
                    Require(rect.height >= 24, screen + " control is too short: " + Describe(element));
                    checkedControls++;
                }
                if (element is Label label && !string.IsNullOrWhiteSpace(label.text))
                    Require(label.resolvedStyle.fontSize >= 13, screen + " text is smaller than 13 reference pixels: " + Describe(label));
            }
            // Restrict overlap checks to HUD labels. Dialogue/phone scroll contents can overlap
            // their clipped viewport normally; HUD labels must never obscure each other.
            var hud = root.Q<VisualElement>("slice-hud");
            if (hud != null)
            {
                var labels = hud.Query<Label>().ToList().Where(label => Visible(label) && !string.IsNullOrWhiteSpace(label.text)).ToArray();
                for (var a = 0; a < labels.Length; a++)
                    for (var b = a + 1; b < labels.Length; b++)
                        Require(!Inset(labels[a].worldBound).Overlaps(Inset(labels[b].worldBound)), "HUD labels overlap: " + Describe(labels[a]) + " / " + Describe(labels[b]));
            }
            var mapLabels = root.Query<Label>(className: "map-pin-label").ToList().Where(Visible).ToArray();
            for (var a = 0; a < mapLabels.Length; a++)
                for (var b = a + 1; b < mapLabels.Length; b++)
                    Require(!Inset(mapLabels[a].worldBound).Overlaps(Inset(mapLabels[b].worldBound)), "Map labels overlap: " + Describe(mapLabels[a]) + " / " + Describe(mapLabels[b]));
            var coverage = 0f;
            if (resting)
            {
                var panels = new[] { hud, root.Q<VisualElement>("slice-map") }.Where(item => item != null && Visible(item)).ToArray();
                Require(panels.Length > 0, "Resting HUD coverage cannot be measured: named panels are absent.");
                foreach (var panel in panels) coverage += panel.worldBound.width * panel.worldBound.height;
                coverage /= bounds.width * bounds.height;
                Require(coverage < .08f, "Resting HUD covers " + (coverage * 100).ToString("F2") + "% (limit 8%).");
            }
            return screen + ": " + checkedControls + " controls checked; HUD coverage " + (coverage * 100).ToString("F2") + "%";
        }
        private static Rect Inset(Rect rect) => new Rect(rect.x + 1, rect.y + 1, Mathf.Max(0, rect.width - 2), Mathf.Max(0, rect.height - 2));
        private static bool WithinScrollContent(VisualElement element)
        {
            for (var parent = element.parent; parent != null; parent = parent.parent)
                if (parent is ScrollView || parent.ClassListContains("unity-scroll-view__content-container")) return true;
            return false;
        }
        private static bool Visible(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden || current.resolvedStyle.opacity <= .01f) return false;
            return element.worldBound.width > 0 && element.worldBound.height > 0;
        }
        private static string Describe(VisualElement element) => element.GetType().Name + " #" + element.name + " " + element.worldBound;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Immersive UI acceptance: " + message); }
    }
}
