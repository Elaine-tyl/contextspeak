using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Shared behaviour for the three airport scenes: the multi-scene travel quest, dialogue with
    /// itinerary-aware scenarios, kiosk panels, announcements, shops and the airport map (M).
    public abstract class AirportFlowBase : LocationFlow
    {
        public override string QuestId => TravelQuest.Id;
        public override string QuestTitle => TravelQuest.Title;
        public Rect MapBounds = new Rect(-150, -100, 300, 300);       // world x/z shown on the map
        public List<string> MapLabels = new List<string>();            // "name|x|z"
        protected TravelBooking Booking => TravelQuest.Booking;
        protected string Panel { get; private set; } = "";
        protected Vector2 scroll;
        protected CafeLikePlayer Player => new CafeLikePlayer(InteractionManager.Instance != null ? InteractionManager.Instance.Player : null);
        private float nextAnnouncement = 40;
        protected virtual string[] AmbientAnnouncements => new string[0];
        protected virtual string MapTitle => "Airport map";

        public override bool Busy => base.Busy || Panel != "";

        protected override void Start()
        {
            // The quest spans several scenes: never restart it here.
            QuestManager.Get(QuestId, QuestTitle);
            Objective = TravelQuest.Next;
        }

        /// The conversation library for this place (airport by default; the beach overrides it).
        protected virtual ScenarioDefinition CreateScenario(string name) => AirportScenarios.Create(name, Booking);

        protected void Speak(string scenario, NPCController npc, Action done)
        {
            var definition = CreateScenario(scenario);
            if (definition == null) { LifeUI.Toast("Missing airport scenario: " + scenario); done?.Invoke(); return; }
            ScenarioManager.Instance.Begin(definition, npc, () => { MarkDone(scenario); SaveManager.Save(); done?.Invoke(); });
        }

        /// Records a finished conversation (travel booking by default; the beach keeps its own list).
        protected virtual void MarkDone(string scenario) => Booking.Mark(scenario);

        protected bool Advance(TravelStage stage)
        {
            var ok = TravelQuest.Advance(stage);
            Objective = TravelQuest.Next;
            if (ok) LifeUI.Toast("✔ " + TravelQuest.Objectives[(int)stage]);
            return ok;
        }

        protected void Show(string name)
        {
            Panel = name;
            scroll = Vector2.zero;
            LifeUI.SetPanelOpen(this, true, BackPanel);
        }

        protected void Hide()
        {
            Panel = "";
            LifeUI.SetPanelOpen(this, false);
        }

        public void Announce(string text)
        {
            LifeUI.Toast("♪ " + text);
            AirportAudio.Instance?.Chime();
            if (ScenarioManager.Instance != null && !ScenarioManager.Instance.Active) ScenarioManager.Instance.Speech.Speak(text);
        }

        /// Interactable spots (kiosks, boards, bins, call buttons) route here.
        public virtual void Use(string action) { }

        protected virtual void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame && LifeUI.CanUsePanel(this) && (ScenarioManager.Instance == null || !ScenarioManager.Instance.Active))
            {
                if (Panel == "map") Hide(); else if (Panel == "") Show("map");
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Panel != "") LifeUI.RequestBack();
            var lines = AmbientAnnouncements;
            if (lines.Length > 0 && (nextAnnouncement -= Time.deltaTime) <= 0)
            {
                nextAnnouncement = 75 + UnityEngine.Random.value * 40;
                Announce(lines[UnityEngine.Random.Range(0, lines.Length)]);
            }
        }

        protected virtual bool CanClose(string panel) => true;

        private bool BackPanel()
        {
            if (!CanClose(Panel)) return false;
            Hide();
            return true;
        }
        private void OnDisable() { LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (Panel != "") LifeUI.SetPanelOpen(this, true, BackPanel); }

        private void DrawToolkit()
        {
            if (Panel == "") return;
            AirportUI.Begin(out var width, out var height);
            if (Panel == "map") { Map(width, height); return; }
            DrawPanel(Panel, width, height);
        }

        protected abstract void DrawPanel(string panel, float width, float height);

        /// AirportMapUI: a schematic plan with labelled areas and "you are here".
        private void Map(float width, float height)
        {
            var area = AirportUI.Window(MapTitle, 900, 720, width, height);
            var plan = new Rect(area.x, area.y, area.width, area.height - 60);
            GUI.Box(plan, GUIContent.none, AirportUI.Board);
            Vector2 ToMap(float x, float z) => new Vector2(plan.x + (x - MapBounds.x) / MapBounds.width * plan.width, plan.yMax - (z - MapBounds.y) / MapBounds.height * plan.height);
            foreach (var label in MapLabels)
            {
                var parts = label.Split('|');
                if (parts.Length < 3 || !float.TryParse(parts[1], out var x) || !float.TryParse(parts[2], out var z)) continue;
                var point = ToMap(x, z);
                GUI.Label(new Rect(point.x - 80, point.y - 12, 160, 24), parts[0], new GUIStyle(AirportUI.BoardText) { alignment = TextAnchor.MiddleCenter, fontSize = 15 });
            }
            var player = InteractionManager.Instance != null ? InteractionManager.Instance.Player : null;
            if (player != null)
            {
                var here = ToMap(player.transform.position.x, player.transform.position.z);
                GUI.Label(new Rect(here.x - 60, here.y - 14, 120, 28), "● YOU", new GUIStyle(AirportUI.Amber) { alignment = TextAnchor.MiddleCenter });
            }
            GUILayout.BeginArea(new Rect(area.x, area.yMax - 50, area.width, 50));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Next: " + (string.IsNullOrEmpty(Objective) ? TravelQuest.Next : Objective), AirportUI.Ink);
            if (GUILayout.Button("Close (M)", AirportUI.Secondary, GUILayout.Width(160))) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// The ordering step for a menu purchase must mention what is in the cart.
        protected ScenarioDefinition OrderScenario(string name, Cart cart)
        {
            var scenario = CreateScenario(name);
            var words = cart.Lines.SelectMany(line => Regex.Matches(line.name.ToLowerInvariant(), "[a-z]{4,}").Cast<Match>().Select(match => match.Value))
                .Where(word => word != "with").Distinct().ToArray();
            var step = scenario.Steps[0];
            if (words.Length > 0) step.meaning = string.Join("|", words);
            step.model = (name.Contains("Barista") || name.Contains("Café") ? "Can I have " : "I'd like ") + string.Join(" and ", cart.Lines.Select(line => (line.quantity > 1 ? line.quantity + " " : "a ") + line.name.ToLowerInvariant())) + ", please.";
            step.goal = "Order what you chose: " + cart.Describe() + ".";
            return scenario;
        }

        protected virtual bool IsOrder(string scenario) => scenario == "Barista Order" || scenario == "Airport Restaurant";

        /// AirportShopUI: browse → cart → cashier conversation → pay → items in the inventory.
        protected void Shop(MenuCatalog menu, NPCController cashier, string scenario, Action<Cart> bought)
        {
            MenuController.Instance.Show(menu, cart =>
            {
                var definition = IsOrder(scenario) ? OrderScenario(scenario, cart) : CreateScenario(scenario);
                ScenarioManager.Instance.Begin(definition, cashier, () =>
                    PaymentController.Instance.Show(menu.Title, string.Join("\n", cart.Lines.Select(line => line.quantity + " × " + line.name + "   " + CurrencyManager.Format(line.Total))), cart.Total, () =>
                    {
                        foreach (var line in cart.Lines) InventoryManager.Add(line.id, line.name + (line.choices != "" ? " (" + line.choices + ")" : ""), line.quantity);
                        bought?.Invoke(cart);
                    }, () => LifeUI.Toast("Purchase cancelled.")));
            });
        }
    }

    /// Tiny wrapper so flows can block the player without referencing CafePlayer everywhere.
    public readonly struct CafeLikePlayer
    {
        private readonly ContextSpeak.Cafe.CafePlayer player;
        public CafeLikePlayer(ContextSpeak.Cafe.CafePlayer player) { this.player = player; }
        public Transform Transform => player != null ? player.transform : null;
        public void Block(bool blocked) { if (player == null) return; player.Blocked = blocked; if (!blocked) player.Controls?.Clear(); }
        public void Place(Vector3 position, Vector3 facing) { if (player == null) return; player.Place(position); player.Face(facing); }
    }

    /// Kiosks, departure boards, call buttons, overhead lockers, carousels… → the scene's flow.
    public sealed class AirportSpot : InteractableObject
    {
        public string Action = "";
        public override bool CanInteract => base.CanInteract && LocationFlow.Current is AirportFlowBase flow && !flow.Busy;
        public override void Interact(InteractionManager manager) { if (LocationFlow.Current is AirportFlowBase flow) flow.Use(Action); }
    }
}
