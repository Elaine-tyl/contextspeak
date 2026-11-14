using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using ContextSpeak.Cafe.UI;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Shared UI Toolkit life HUD: contextual interaction prompt, quest
    /// tracker, wallet/XP, mini-map, conversation panel with microphone controls, feedback,
    /// correction and repeat panels, MOCK typing box, inventory (I) and settings (F1).
    public sealed class LifeUI : MonoBehaviour
    {
        private static readonly LegacyModalStack modals = new LegacyModalStack();
        private static readonly object compatibilityOwner = new object();
        public static bool PanelOpen => modals.Open;
        private static bool inheritedBlocked, inheritedModal;
        public static LifeUI Instance { get; private set; }
        private static readonly Queue<string> toasts = new Queue<string>();
        private static string toast = "";
        private static float toastUntil;

        public static GUIStyle Panel, Paper, Title, Heading, Body, Dark, DarkSmall, Small, Button, Tab, Warning, Plate, Chip, Good;
        private static Texture2D panelTexture, paperTexture, chipTexture;

        public string LocationName = "";
        private bool inventoryOpen, settingsOpen, typing;
        private Vector2 dialogueScroll;
        private ToolkitView toolkit;

        public static void Toast(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Debug.Log("[ContextSpeak Life] " + message);
            toasts.Enqueue(message);
        }

        private void Awake() { Instance = this; }
        private void OnEnable() { toolkit = ToolkitView.Attach(this, DrawToolkit); SyncModal(); }
        private void OnDisable() { ReleaseModal(); }
        private void ReleaseModal()
        {
            SetPanelOpen(this, false);
        }
        private bool BackPhone()
        {
            if (settingsOpen) settingsOpen = false;
            else if (inventoryOpen) inventoryOpen = false;
            SyncModal();
            return true;
        }
        private void SyncModal()
        {
            var open = inventoryOpen || settingsOpen;
            SetPanelOpen(this, open, BackPhone, 100);
            if (open && CanUsePanel(this)) toolkit?.BringToFront();
        }
        private void OnDestroy() { ReleaseModal(); if (Instance == this) Instance = null; }

        private static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }

        private static GUIStyle Text(int size, Color colour, FontStyle style = FontStyle.Normal)
        {
            var result = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, richText = true, fontStyle = style };
            result.normal.textColor = colour;
            return result;
        }

        /// Compatibility styles for location presenters; dimensions come from the shared safe area.
        public static void Begin(out float width, out float height)
        {
            width = Screen.width;
            height = Screen.height;
            if (Panel != null && panelTexture != null) return;
            panelTexture = Solid(new Color(0.08f, 0.11f, 0.12f, 0.92f));
            paperTexture = Solid(new Color(0.98f, 0.95f, 0.88f, 1f));
            chipTexture = Solid(new Color(0.25f, 0.45f, 0.4f, 1f));
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 12, 12) };
            Panel.normal.background = panelTexture;
            Paper = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(4, 4, 4, 4) };
            Paper.normal.background = paperTexture;
            Title = Text(30, new Color(1f, 0.93f, 0.75f), FontStyle.Bold);
            Heading = Text(21, new Color(0.95f, 0.85f, 0.6f), FontStyle.Bold);
            Body = Text(19, Color.white);
            Dark = Text(18, new Color(0.15f, 0.12f, 0.1f));
            DarkSmall = Text(15, new Color(0.3f, 0.27f, 0.24f));
            Small = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true, richText = true };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 19, fixedHeight = 42 };
            Tab = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 36 };
            Warning = Text(16, new Color(1f, 0.55f, 0.35f), FontStyle.Bold);
            Good = Text(17, new Color(0.6f, 0.95f, 0.65f));
            Plate = Text(22, Color.white, FontStyle.Bold);
            Plate.alignment = TextAnchor.MiddleCenter;
            Chip = Text(16, Color.white);
            Chip.normal.background = chipTexture;
            Chip.padding = new RectOffset(8, 8, 3, 3);
            Chip.margin = new RectOffset(3, 3, 3, 3);
            Chip.wordWrap = false;
        }

        /// Compatibility entry point for external integrations. Runtime presenters use an owner.
        public static void SetPanelOpen(bool open) => SetPanelOpen(compatibilityOwner, open);
        public static void SetPanelOpen(object owner, bool open, System.Func<bool> back = null, int priority = 0)
        {
            if (open && !modals.Open)
            {
                var interaction = InteractionManager.Instance;
                inheritedModal = interaction != null && interaction.Modal && !Talking;
                inheritedBlocked = interaction != null && interaction.Player != null && interaction.Player.Blocked && !Talking;
            }
            if (modals.Set(owner, open, back, priority)) RefreshModalState();
        }
        public static bool CanUsePanel(object owner) => !modals.Open || ReferenceEquals(modals.TopOwner, owner);
        public static void RefreshModalState()
        {
            var interaction = InteractionManager.Instance;
            var blocked = modals.Open || Talking || inheritedBlocked;
            if (interaction != null)
            {
                interaction.Modal = modals.Open || Talking || inheritedModal;
                if (interaction.Player != null)
                {
                    interaction.Player.Blocked = blocked;
                    if (!blocked) interaction.Player.Controls?.Clear();
                }
            }
            if (!modals.Open) inheritedBlocked = inheritedModal = false;
        }
        public static bool RequestBack() => RequestBack(Time.frameCount);
        public static bool RequestBack(int press)
        {
            var consumed = modals.Back(press, Talking ? (System.Action)(() => Scenario.End(false)) : null);
            if (consumed && modals.Open) RefreshModalState();
            return consumed;
        }

        private static ScenarioManager Scenario => ScenarioManager.Instance;
        private static bool Talking => Scenario != null && Scenario.Active;

        private void Update()
        {
            if (Time.unscaledTime >= toastUntil && toasts.Count > 0) { toast = toasts.Dequeue(); toastUntil = Time.unscaledTime + 3.5f; }
            if (Time.unscaledTime >= toastUntil) toast = "";
            var keyboard = Keyboard.current;
            typing = toolkit != null && toolkit.TextInputFocused;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                RequestBack();
                return;
            }
            if (typing) return;
            if (keyboard.f1Key.wasPressedThisFrame) { settingsOpen = !settingsOpen; SyncModal(); }
            if ((keyboard.iKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) && !Talking) { inventoryOpen = !inventoryOpen; SyncModal(); }
            if (!Talking || inventoryOpen || settingsOpen) return;
            var speech = Scenario.Speech;
            if (keyboard.rKey.wasPressedThisFrame) speech.ToggleRecording();
            if (keyboard.hKey.wasPressedThisFrame) Repeat(false);
            if (keyboard.jKey.wasPressedThisFrame) Repeat(true);
            if (keyboard.nKey.wasPressedThisFrame && !Scenario.Machine.LearningMode) Scenario.Skip();

        }

        private static void Repeat(bool slow)
        {
            if (Scenario.Machine.Phase == DialoguePhase.AwaitingRepeat) Scenario.SpeakTarget(slow);
            else Scenario.SpeakCurrent(slow);
        }

        private void DrawToolkit()
        {
            Begin(out var width, out var height);
            if (UiPreferences.Current.Hud != HudMode.Off && !Talking && !PanelOpen) Tracker();
            if (inventoryOpen || MenuController.Instance != null && MenuController.Instance.Open || PaymentController.Instance != null && PaymentController.Instance.Open) Wallet(width);
            if (UiPreferences.Current.Hud == HudMode.Full && !Talking && !PanelOpen) MiniMap(width);
            if (Talking) Dialogue(width, height);
            else Prompt(width, height);
            if (inventoryOpen) Inventory(width);
            if (settingsOpen) Settings(width, height);
            SyncModal();
            if (toast != "")
                GUI.Label(new Rect(width / 2 - 360, 46, 720, 60), "<b>" + toast + "</b>", Centered(Panel, 19));
        }

        private static readonly Dictionary<int, GUIStyle> centredStyles = new Dictionary<int, GUIStyle>();
        private static GUIStyle Centered(GUIStyle style, int size)
        {
            if (centredStyles.TryGetValue(size, out var cached)) return cached;
            var result = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = size, richText = true, wordWrap = true };
            result.normal.textColor = Color.white;
            centredStyles[size] = result;
            return result;
        }

        private void Tracker()
        {
            var flow = LocationFlow.Current;
            if (flow == null) return;
            GUILayout.BeginArea(new Rect(20, 54, 460, 70), Panel);
            var quest = QuestManager.Get(flow.QuestId, flow.QuestTitle);
            GUILayout.Label((quest.done ? "✔ Complete — " : "Objective: ") + flow.Objective, Body);
            GUILayout.EndArea();
        }

        private static void Wallet(float width)
        {
            GUI.Label(new Rect(width - 330, 20, 310, 58),
                "<b>" + SaveManager.PlayerName + "</b>  ·  IELTS " + ((int)SaveManager.Band).ToString() +
                "\nWallet " + CurrencyManager.Balance + "  ·  XP " + SaveManager.Data.xp, Centered(Panel, 17));
        }

        private static PortalDoor[] portals = new PortalDoor[0];
        private static float portalRescan;
        private static void MiniMap(float width)
        {
            var player = InteractionManager.Instance != null ? InteractionManager.Instance.Player : null;
            if (player == null) return;
            if (Time.unscaledTime >= portalRescan) { portals = Object.FindObjectsByType<PortalDoor>(FindObjectsSortMode.None); portalRescan = Time.unscaledTime + 2; }
            var area = new Rect(width - 190, 20, 170, 170);
            GUI.Box(area, GUIContent.none, Panel);
            var centre = area.center;
            const float range = 22f;
            var forward = player.transform.forward;
            var yaw = Mathf.Atan2(forward.x, forward.z);
            Vector2 Map(Vector3 world)
            {
                var offset = world - player.transform.position;
                var x = offset.x * Mathf.Cos(yaw) - offset.z * Mathf.Sin(yaw);
                var z = offset.x * Mathf.Sin(yaw) + offset.z * Mathf.Cos(yaw);
                return centre + new Vector2(x, -z) * (area.width / 2 - 8) / range;
            }
            var previous = GUI.color;
            foreach (var npc in NPCController.All)
            {
                if (npc == null || Vector3.Distance(npc.transform.position, player.transform.position) > range) continue;
                var point = Map(npc.transform.position);
                GUI.color = npc.Talking ? new Color(1f, 0.8f, 0.3f) : new Color(0.5f, 0.85f, 1f);
                GUI.DrawTexture(new Rect(point.x - 4, point.y - 4, 8, 8), Texture2D.whiteTexture);
            }
            foreach (var portal in portals)
            {
                if (portal == null || Vector3.Distance(portal.transform.position, player.transform.position) > range) continue;
                var point = Map(portal.transform.position);
                GUI.color = new Color(0.55f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(point.x - 5, point.y - 5, 10, 10), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(centre.x - 5, centre.y - 5, 10, 10), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Prompt(float width, float height)
        {
            var interaction = InteractionManager.Instance;
            if (interaction == null || PanelOpen) return;
            string text = null;
            if (interaction.Seat != null) text = interaction.Seat.CanStand ? "[E] Stand up" : "Seated";
            else if (interaction.Focus != null) text = "[E] " + interaction.Focus.Label;
            if (text == null) return;
            var camera = Camera.main;
            var target = interaction.Focus;
            if (camera != null && target != null)
            {
                var point = camera.WorldToViewportPoint(target.Point + Vector3.up * 0.35f);
                if (point.z <= 0) return;
                var x = Mathf.Clamp(point.x * width - 140, 12, width - 292);
                var y = Mathf.Clamp((1 - point.y) * height, 12, height - 60);
                GUI.Label(new Rect(x, y, 280, 44), text, Centered(Panel, 15));
            }
            else GUI.Label(new Rect(width / 2 - 140, height - 100, 280, 44), text, Centered(Panel, 15));
        }

        private void Dialogue(float width, float height)
        {
            var machine = Scenario.Machine;
            var speech = Scenario.Speech;
            var step = machine.Current;
            var npc = Scenario.Npc;
            var box = new Rect(width / 2 - 500, Mathf.Max(80, height - 420), 1000, Mathf.Min(400, height - 100));
            GUILayout.BeginSubtitleArea(box);
            GUILayout.BeginHorizontal();
            GUILayout.Label((npc != null ? npc.DisplayName + "  <size=15>(" + npc.Role + ")</size>" : machine.Scenario.Title)
                + "   <size=15>step " + (machine.Index + 1) + "/" + machine.Scenario.Steps.Count + "</size>", Heading);
            GUILayout.FlexibleSpace();
            if (speech.Mock) GUILayout.Label("VOICE OFFLINE · unassessed", Warning);
            GUILayout.EndHorizontal();
            dialogueScroll = GUILayout.BeginScrollView(dialogueScroll, GUILayout.Height(210));
            GUILayout.Subtitle("“" + machine.NpcLine + "”");
            if (step != null && !string.IsNullOrWhiteSpace(step.goal)) GUILayout.Label("Your goal: " + step.goal, Good);
            var words = machine.Vocabulary;
            if (words.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Useful words:", Small, GUILayout.Width(110));
                foreach (var word in words) GUILayout.Label(word, Chip);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (Scenario.Heard != "") GUILayout.Label("You said" + (Scenario.HeardSpoken ? "" : " (typed)") + ": " + Scenario.Heard, Body);
            if (machine.Feedback != "") GUILayout.Label(machine.Feedback, Good);
            GUILayout.Label("Practice feedback only · not an official IELTS score. Pronunciation is not assessed.", Small);
            var review = machine.Review;
            if (review != null && review.Corrected != "" && review.Corrected != review.Original)
                GUILayout.Label("Better: <b>" + review.Corrected + "</b>" + (review.Explanation != "" ? "\n<size=15>" + review.Explanation + "</size>" : ""), Body);
            if (machine.Phase == DialoguePhase.AwaitingRepeat)
                GUILayout.Label("Say it again before we continue:\n<b>" + machine.Target + "</b>", Heading);
            if (machine.Hint != "") GUILayout.Label("Hint: " + machine.Hint, Warning);
            GUILayout.EndScrollView();
            GUILayout.Label(speech.Status, Small);
            if (speech.Mock) GUILayout.Label("Voice service unavailable. Reconnect the speech companion to practise; no speaking credit is awarded offline.", Warning);
            GUILayout.BeginHorizontal();
            if (!speech.Mock && GUILayout.Button(speech.RecorderState == "recording" ? "■ Stop (R)" : "● Speak (R)", Button)) speech.ToggleRecording();
            if (GUILayout.Button("Hear again (H)", Button)) Repeat(false);
            if (GUILayout.Button("Slowly (J)", Button)) Repeat(true);
            if (!machine.LearningMode && GUILayout.Button("Skip line (N)", Button)) Scenario.Skip();
            if (GUILayout.Button("Leave (Esc)", Button)) Scenario.End(false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Inventory(float width)
        {
            var height = Screen.height;
            var phoneWidth = Mathf.Min(440, width - 32);
            GUILayout.BeginArea(new Rect(width - phoneWidth - 24, 24, phoneWidth, height - 48), Paper);
            GUILayout.Label("CONTEXT  ·  " + SkyState.Clock + "  ·  " + SkyState.Weather, Heading);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Settings", Small)) settingsOpen = true;
            if (GUILayout.Button("Close · Esc", Small)) inventoryOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Label("Wallet  " + CurrencyManager.Balance + "  ·  XP " + SaveManager.Data.xp, Body);
            GUILayout.Label("Inventory", Heading);
            if (InventoryManager.Items.Count == 0) GUILayout.Label("Nothing yet.", Body);
            foreach (var item in InventoryManager.Items) GUILayout.Label(item.count + " × " + item.name, Body);
            GUILayout.Space(8);
            GUILayout.Label("Quests", Heading);
            foreach (var quest in SaveManager.Data.quests) GUILayout.Label((quest.done ? "✔ " : "• ") + quest.title, Body);
            GUILayout.EndArea();
        }

        private static void Settings(float width, float height)
        {
            GUILayout.BeginArea(new Rect(width / 2 - 320, 24, 640, height - 48), Panel);
            GUILayout.Label("Phone · Settings", Title);
            GUILayout.Embed("accessibility-preferences", UiPreferences.AddControls);
            GUILayout.Label("IELTS speaking band for NPCs: " + ((int)SaveManager.Band).ToString(), Body);
            GUILayout.BeginHorizontal();
            for (var band = 3; band <= 9; band++)
                if (GUILayout.Toggle((int)SaveManager.Band == band, " " + band + " ", Tab)) if ((int)SaveManager.Band != band) SaveManager.Band = (Slice.IeltsBand)band;
            GUILayout.EndHorizontal();
            var data = SaveManager.Data;
            var learning = GUILayout.Toggle(data.learningMode, " Learning mode (hints, model answers, repeat-after-correction; off = free practice with skip)", Body);
            var mock = GUILayout.Toggle(data.forceMock, " Typed rehearsal (does not advance spoken tasks)", Body);
            if (learning != data.learningMode || mock != data.forceMock) { data.learningMode = learning; data.forceMock = mock; SaveManager.Save(); }
            GUILayout.Space(10);
            if (GUILayout.Button("Reconnect speech companion", Button) && Scenario != null) Scenario.Speech.Connect();
            if (GUILayout.Button("Reset life progress (money, XP, quests)", Button)) { SaveManager.Reset(); Toast("Progress reset."); }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (F1)", Button)) Instance.settingsOpen = false;
            GUILayout.EndArea();
        }
    }
}
