using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ContextSpeak.Cafe
{
    public sealed class CafeInterface : MonoBehaviour
    {
        public event Action<string> Submitted;
        public event Action Record, Hear, Close, Join, Reset, Reconnect, Life;
        public event Action<string> StageSelected;
        private Label objective, nearby, speaker, line, feedback, transcript, status;
        private VisualElement dialogue;
        private TextField input;
        private Button send, record, hear, join;
        private void Awake()
        {
            var root = CreateRoot(gameObject);
            Build(root);
        }
        public static VisualElement CreateRoot(GameObject owner)
        {
            // Input System needs an explicit persistent UI event router. Keep it across
            // additive district unloads, or the next scene's buttons lose their input.
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("ContextSpeak UI input");
                events.AddComponent<EventSystem>();
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
                if (Application.isPlaying) DontDestroyOnLoad(events);
            }
            var document = owner.AddComponent<UIDocument>();
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1440, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("CafeTheme");
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            // All children are positioned overlays, so the document must fill the panel explicitly.
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.styleSheets.Add(Resources.Load<StyleSheet>("CafeStyles"));
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Application.platform == RuntimePlatform.WebGLPlayer) root.style.display = DisplayStyle.None;
            return root;
        }
        private void Build(VisualElement root)
        {
            var hud = new VisualElement { name = "hud" };
            root.Add(hud);
            hud.Add(new Label("ContextSpeak  /  Forest Cafe"));
            objective = new Label();
            hud.Add(objective);
            var stages = new DropdownField("Test stage", new System.Collections.Generic.List<string> { "Continue saved task", "Counter · practice", "Friend chat · practice" }, 0);
            stages.RegisterValueChangedCallback(change => StageSelected?.Invoke(stages.index == 1 ? "counter" : stages.index == 2 ? "friend" : "continue"));
            hud.Add(stages);
            hud.Add(new Button(() => Reconnect?.Invoke()) { text = "Reconnect local AI" });
            hud.Add(new Button(() => Reset?.Invoke()) { text = "New task" });
            hud.Add(new Button(() => Life?.Invoke()) { text = "Explore Life town" });
            nearby = new Label();
            nearby.AddToClassList("nearby");
            root.Add(nearby);
            dialogue = new ScrollView(ScrollViewMode.Vertical) { name = "dialogue" };
            root.Add(dialogue);
            speaker = new Label();
            speaker.AddToClassList("speaker");
            line = new Label { enableRichText = false };
            feedback = new Label { enableRichText = false };
            transcript = new Label { enableRichText = false };
            dialogue.Add(speaker);
            dialogue.Add(line);
            dialogue.Add(feedback);
            dialogue.Add(transcript);
            input = new TextField { maxLength = 400, multiline = false };
            input.label = "Your reply";
            input.RegisterCallback<KeyDownEvent>(ev => {
                if (ev.keyCode != KeyCode.Return) return;
                Submitted?.Invoke(input.value);
                ev.StopPropagation();
            });
            dialogue.Add(input);
            var buttons = new VisualElement();
            buttons.AddToClassList("buttons");
            send = new Button(() => Submitted?.Invoke(input.value)) { text = "Send reply" };
            record = new Button(() => Record?.Invoke()) { text = "Record reply" };
            hear = new Button(() => Hear?.Invoke()) { text = "Hear NPC" };
            join = new Button(() => Join?.Invoke()) { text = "Join Alex" };
            foreach (var button in new[] { send, record, hear, join }) buttons.Add(button);
            buttons.Add(new Button(() => Close?.Invoke()) { text = "Close ×" });
            dialogue.Add(buttons);
            status = new Label();
            dialogue.Add(status);
            var controls = new Label("WASD / arrows · E to talk · Right-drag to look · Scroll to zoom · Esc to explore");
            controls.AddToClassList("controls");
            root.Add(controls);
        }
        public void Refresh(CafeSnapshot state)
        {
            objective.text = state.objective;
            nearby.text = state.nearby;
            dialogue.style.display = state.dialogue ? DisplayStyle.Flex : DisplayStyle.None;
            speaker.text = state.speaker;
            line.text = state.line;
            feedback.text = state.feedback;
            transcript.text = state.transcript;
            feedback.style.display = string.IsNullOrEmpty(state.feedback) ? DisplayStyle.None : DisplayStyle.Flex;
            transcript.style.display = string.IsNullOrEmpty(state.transcript) ? DisplayStyle.None : DisplayStyle.Flex;
            status.text = state.status;
            input.SetEnabled(!state.busy && state.recording == "idle");
            send.SetEnabled(!state.busy && !state.complete && state.recording == "idle");
            record.text = state.recording == "recording" ? "Stop recording" : state.recording == "requesting" ? "Cancel request" : "Record reply";
            record.SetEnabled(!state.busy && !state.complete);
            hear.SetEnabled(!state.busy && state.recording == "idle");
            join.style.display = state.join ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public void ClearInput()
        {
            input.value = "";
        }
    }
}
