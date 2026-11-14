using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Connects the existing Forest Café (SliceGame: menu, customisation, waiter, payment, seats,
    /// eating) to the shared life progress: each served order goes into the inventory and earns XP,
    /// the café visit is a quest, and the forest train stop takes you back to Metro City (B).
    public sealed class ForestCafeLife : MonoBehaviour
    {
        public const string QuestId = "forest-cafe";
        public Transform TrainStop;
        public float StopRange = 3.5f;
        private ContextSpeak.Slice.SliceGame game;
        private ContextSpeak.Slice.WaiterStage last;
        private string note = "";
        private float noteUntil;
        private GUIStyle style;

        private void Start()
        {
            game = FindFirstObjectByType<ContextSpeak.Slice.SliceGame>();
            QuestManager.Get(QuestId, "Order a drink at the Forest Café");
            if (game != null && game.Save != null) last = game.Save.Waiter.Stage;
        }

        private void Update()
        {
            if (game == null || game.Save == null) return;
            var stage = game.Save.Waiter.Stage;
            if (stage != last)
            {
                if (stage == ContextSpeak.Slice.WaiterStage.Served)
                {
                    foreach (var line in game.Save.Cart.Lines)
                    {
                        var menu = line.Menu;
                        InventoryManager.Add("forest-" + line.Item, (menu != null ? menu.Name : line.Item) + " (Forest Café)", line.Quantity);
                    }
                    QuestManager.Complete(QuestId, 20, "Served at the Forest Café");
                    Note("Order served · added to your inventory · +20 life XP");
                }
                last = stage;
            }
            var player = game.Player != null ? game.Player.transform : null;
            if (TrainStop != null && player != null && Vector3.Distance(player.position, TrainStop.position) < StopRange
                && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                if (!SceneTransitionManager.Leave("MetroCity")) Note(SceneTransitionManager.LastError);
            }
        }

        private void Note(string text) { note = text; noteUntil = Time.unscaledTime + 4; Debug.Log("[ContextSpeak Life] " + text); }

        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); }

        private void DrawToolkit()
        {
            if (style == null) { style = new GUIStyle(GUI.skin.box) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter }; style.normal.textColor = Color.white; }
            var player = game != null && game.Player != null ? game.Player.transform : null;
            if (TrainStop != null && player != null && Vector3.Distance(player.position, TrainStop.position) < StopRange)
                GUI.Box(new Rect(Screen.width / 2f - 230, Screen.height * 0.22f, 460, 40), "B · Take the train back to Metro City", style);
            if (Time.unscaledTime < noteUntil)
                GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height * 0.16f, 520, 40), note, style);
        }
    }
}
