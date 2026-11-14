using System;
using ContextSpeak.Cafe;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Slice
{
    public sealed partial class SliceInterface
    {
        private readonly ContextNeeds contextNeeds = new ContextNeeds();
        private VisualElement[] needFills;
        private VisualElement letterboxTop, letterboxBottom;
        private ToastPresenter toasts;
        private WorldPrompt worldPrompt;
        private WorldMapCapture mapCapture;
        private SliceMapSurface minimap, phoneMap;
        private string lastToast = "", lastHint = "";
        private string promptKey = "", promptText = "";
        private FrostedGlass phoneGlass;
        private Label phoneStatusClock, phoneHomeClock, phoneHomeWeather;

        private void InitializeImmersive()
        {
            letterboxTop = Add(root, "letterbox-top"); letterboxBottom = Add(root, "letterbox-bottom");
            letterboxTop.pickingMode = letterboxBottom.pickingMode = PickingMode.Ignore;
            Show(letterboxTop, false); Show(letterboxBottom, false);
            toasts = new ToastPresenter(root);
            worldPrompt = WorldPrompt.Attach(gameObject, root);
            mapCapture = gameObject.AddComponent<WorldMapCapture>();
            mapCapture.Captured += RefreshPhoneGlass;
            minimap = new SliceMapSurface(mapCapture, false, CommandMapPin); mapDots.Add(minimap);
        }
        private void OnDestroy()
        {
            toasts?.Dispose(); phoneGlass?.Dispose();
            if (mapCapture != null) mapCapture.Captured -= RefreshPhoneGlass;
        }
        public void QueueToast(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            lastToast = message; toasts?.Enqueue(message);
        }
        private void RenderContext(SliceView view)
        {
            if (!string.IsNullOrEmpty(view.Toast) && view.Toast != lastToast) QueueToast(view.Toast);
            if (string.IsNullOrEmpty(view.Toast)) lastToast = "";
            var showHud = UiPreferences.Current.Hud != HudMode.Off && view.Talking == Partner.None;
            Show(root.Q("slice-hud"), showHud);
            Show(root.Q("slice-map"), showHud && view.Panel == "");
            Show(root.Q("hud-details"), UiPreferences.Current.Hud == HudMode.Full);
            line.style.fontSize = UiPreferences.Current.SubtitleSize;
            if (view.Hint != lastHint)
            {
                lastHint = view.Hint ?? "";
                var end = lastHint.IndexOf("   ", StringComparison.Ordinal);
                var text = end >= 0 ? lastHint.Substring(0, end) : lastHint;
                var separator = text.IndexOf(" · ", StringComparison.Ordinal);
                promptKey = separator >= 0 ? text.Substring(0, separator) : "";
                promptText = separator >= 0 ? text.Substring(separator + 3) : text;
            }
            if (view.HintAnchor != null && view.Panel == "" && view.Talking == Partner.None)
                worldPrompt.Show(view.HintAnchor, lastHint, promptText, promptKey);
            else worldPrompt.Hide();
            mapCapture.CapturingPhone = view.Panel != "";
            if (phoneStatusClock != null) phoneStatusClock.text = view.Time + " · " + view.Weather;
            if (phoneHomeClock != null) phoneHomeClock.text = view.Time;
            if (phoneHomeWeather != null) phoneHomeWeather.text = view.Weather;
        }
        private void RefreshPhoneGlass() { if (mapCapture.CapturingPhone) phoneGlass?.Refresh(mapCapture.WorldTexture); }
        private void CommandMapPin(string id) => Command?.Invoke("visit", id);
        private void BuildPhoneHeader(VisualElement card, SliceView view)
        {
            phoneGlass?.Dispose();
            phoneGlass = new FrostedGlass(card);
            var statusBar = Add(card, null, "phone-status");
            Text(statusBar, "ContextSpeak Life"); phoneStatusClock = Text(statusBar, view.Time + " · " + view.Weather, "push-right");
            var header = Add(card, null, "row", "phone-header");
            Action(header, "‹ Back", "phone-back", "", "ghost");
            Text(header, PhoneTitle(view.Panel), "phone-title");
            Action(header, "Home", "phone-home", "", "ghost", "push-right");
        }
        private static string PhoneTitle(string panel)
        {
            switch (panel)
            {
                case "inventory": return "Bag";
                case "journal": return "Journal & quests";
                case "pause": return "Settings";
                case "map": case "speaking": case "places": return "Map & explore";
                case "phone": return "Your phone";
                default: return panel.Length > 0 ? char.ToUpperInvariant(panel[0]) + panel.Substring(1) : "";
            }
        }
        private void PhoneHome(VisualElement body)
        {
            phoneHomeClock = Text(body, last.Time, "phone-clock"); phoneHomeWeather = Text(body, last.Weather, "hud-muted");
            var apps = Add(body, null, "phone-apps");
            foreach (var app in new[] { ("Map", "map"), ("Learning", "learning"), ("Quests", "journal"), ("Messages", "messages"), ("Wallet", "wallet"), ("Bag", "inventory"), ("Friends", "friends"), ("Settings", "pause") })
                Action(apps, app.Item1, "panel", app.Item2, "phone-app");
            if (FindFirstObjectByType<ContextSpeak.LivingWorld.DynamicLifeEventManager>() != null)
                Action(apps, "Life stories", "life-stories", "", "phone-app");
        }
        private void MapTabs(VisualElement body, string current)
        {
            var tabs = Add(body, null, "row", "wrap", "phone-tabs");
            foreach (var tab in new[] { ("Map", "map"), ("Speaking practice", "speaking"), ("Life scenes", "places") })
            {
                var button = Action(tabs, tab.Item1, "panel", tab.Item2, "tab");
                button.EnableInClassList("selected", current == tab.Item2);
            }
        }
        private void PhoneMap(VisualElement body, SliceView view)
        {
            MapTabs(body, "map");
            var paper = Add(body, null, "paper", "phone-map-paper");
            phoneMap = new SliceMapSurface(mapCapture, true, CommandMapPin); paper.Add(phoneMap); phoneMap.Refresh(view);
            Text(paper, "FOREST CAFÉ · neighbourhood map", "section-title");
            Text(paper, "Pins mark people and places. The line points towards your current speaking stop.", "practice-note");
            var next = SpeakingTrail.Create(view.Save).Next(view.Save);
            if (next != null) Action(body, "Next stop · " + next.Name, "visit", next.Id);
            Action(body, "See speaking practice", "panel", "speaking", "ghost");
        }
        private void Wallet(VisualElement body, SliceSave save)
        {
            Text(body, "Cash", "section-title"); Text(body, CafeMenu.Money(save.Wallet.Cash), "wallet-balance");
            Text(body, "Card", "section-title"); Text(body, CafeMenu.Money(save.Wallet.Card), "wallet-balance");
            Text(body, "Practice wallet · fictional money", "practice-note");
            foreach (var receipt in save.Receipts)
            {
                var paper = Add(body, null, "paper", "receipt");
                Text(paper, "Forest Café · " + receipt.Id);
                foreach (var item in receipt.Lines) Text(paper, item);
                Text(paper, CafeMenu.Money(receipt.Total) + " · " + receipt.Method, "receipt-total");
            }
        }
        private void Messages(VisualElement body, SliceView view)
        {
            Text(body, "Your conversations", "section-title");
            if (view.History.Length == 0) Text(body, "No conversations yet. Say hello to someone nearby.");
            foreach (var entry in view.History)
                Text(body, entry.Speaker + "\n" + entry.Text, entry.Player ? "message-you" : "message-npc");
        }
    }
}
