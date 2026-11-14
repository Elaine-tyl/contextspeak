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
            var root = ContextSpeak.Cafe.UI.GameUiRoot.Attach(owner);
            var styles = Resources.Load<StyleSheet>("CafeStyles");
            if (styles != null && !root.styleSheets.Contains(styles)) root.styleSheets.Add(styles);
            return root;
        }
        private void Build(VisualElement root)
        {
            var hud = new VisualElement { name = "hud" };
            root.Add(hud);
            objective = new Label();
            hud.Add(objective);
            var actions = new Foldout { text = "Café · options", value = false };
            actions.AddToClassList("context-actions");
            hud.Add(actions);
            var stages = new DropdownField("Test stage", new System.Collections.Generic.List<string> { "Continue saved task", "Counter · practice", "Friend chat · practice" }, 0);
            stages.RegisterValueChangedCallback(change => StageSelected?.Invoke(stages.index == 1 ? "counter" : stages.index == 2 ? "friend" : "continue"));
            actions.Add(stages);
            actions.Add(new Button(() => Reconnect?.Invoke()) { text = "Reconnect local AI" });
            actions.Add(new Button(() => Reset?.Invoke()) { text = "New task" });
            actions.Add(new Button(() => Life?.Invoke()) { text = "Explore Life town" });
            nearby = new Label();
            nearby.AddToClassList("nearby");
            root.Add(nearby);
            dialogue = new ScrollView(ScrollViewMode.Vertical) { name = "dialogue" };
            dialogue.AddToClassList("subtitle-card");
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
            var practice = new Label("Speaking practice feedback · not an official IELTS score.") { enableRichText = false };
            practice.AddToClassList("practice-note");
            dialogue.Add(practice);
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

        }
        public void Refresh(CafeSnapshot state)
        {
            objective.text = state.objective;
            nearby.text = state.nearby;
            nearby.style.display = string.IsNullOrEmpty(state.nearby) ? DisplayStyle.None : DisplayStyle.Flex;
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
