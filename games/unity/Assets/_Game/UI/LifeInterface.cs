using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe
{
    public sealed class LifeInterface : MonoBehaviour
    {
        public event Action<string, string> Command;
        private Label objective, wallet, bag, speaker, line, repair, feedback, transcript, status, menu, travelStatus;
        private VisualElement dialogue, menuPanel;
        private TextField reply;
        private Button record, send, hear, hearRepair, nearby;
        private void Awake()
        {
            var root = CafeInterface.CreateRoot(gameObject);
            var hud = new VisualElement { name = "hud" };
            root.Add(hud);
            hud.Add(new Label("ContextSpeak Life"));
            hud.Add(new Label("YOUR FIRST DAY"));
            objective = new Label();
            wallet = new Label();
            bag = new Label();
            hud.Add(objective);
            hud.Add(wallet);
            hud.Add(bag);
            hud.Add(Button("Walk to objective", "walk"));
            travelStatus = new Label { enableRichText = false };
            hud.Add(travelStatus);
            hud.Add(new Label("Test stage · unearned practice"));
            var stages = new DropdownField(new List<string> { "Continue saved day", "Home · practice", "Market · practice", "Cafe · practice", "Friend · practice" }, 0);
            var ids = new[] { "continue", "home", "market", "cafe", "friend" };
            stages.RegisterValueChangedCallback(change => Command?.Invoke("stage", ids[stages.index]));
            hud.Add(stages);
            hud.Add(Button("Reconnect local AI", "connect"));
            hud.Add(Button("Original cafe", "cafe"));
            menuPanel = new VisualElement { name = "life-menu" };
            menu = new Label { enableRichText = false };
            menuPanel.Add(menu);
            root.Add(menuPanel);
            nearby = Button("Talk", "interact");
            nearby.AddToClassList("nearby");
            root.Add(nearby);
            dialogue = new ScrollView(ScrollViewMode.Vertical) { name = "dialogue" };
            root.Add(dialogue);
            speaker = new Label();
            speaker.AddToClassList("speaker");
            line = new Label { enableRichText = false };
            feedback = new Label { enableRichText = false };
            repair = new Label { name = "repair", enableRichText = false };
            transcript = new Label { enableRichText = false };
            dialogue.Add(speaker);
            dialogue.Add(line);
            dialogue.Add(feedback);
            dialogue.Add(repair);
            dialogue.Add(transcript);
            reply = new TextField("Typed rehearsal") { maxLength = 400 };
            reply.RegisterCallback<KeyDownEvent>(ev => {
                if (ev.keyCode != KeyCode.Return) return;
                Command?.Invoke("send", reply.value);
                ev.StopPropagation();
            });
            dialogue.Add(reply);
            var buttons = new VisualElement();
            buttons.AddToClassList("buttons");
            record = Button("Record reply", "record");
            send = new Button(() => Command?.Invoke("send", reply.value)) { text = "Check typed rehearsal" };
            hear = Button("Hear NPC", "hear");
            hearRepair = Button("Hear correction", "repair");
            foreach (var button in new[] { record, send, hear, hearRepair, Button("Close ×", "close") }) buttons.Add(button);
            dialogue.Add(buttons);
            status = new Label { enableRichText = false };
            dialogue.Add(status);
            var controls = new Label("WASD / arrows · E to talk · Right-drag to look · Esc to explore");
            controls.AddToClassList("controls");
            root.Add(controls);
        }
        private Button Button(string text, string action)
        {
            return new Button(() => Command?.Invoke(action, "")) { text = text };
        }
        public void Refresh(LifeSnapshot state)
        {
            objective.text = state.objective;
            wallet.text = "Wallet · " + state.wallet;
            bag.text = "Bag · " + state.bag;
            travelStatus.text = state.dialogue ? "" : state.status;
            nearby.text = state.nearby;
            nearby.style.display = state.nearby == "" ? DisplayStyle.None : DisplayStyle.Flex;
            menu.text = state.menu;
            menuPanel.style.display = state.menu == "" ? DisplayStyle.None : DisplayStyle.Flex;
            dialogue.style.display = state.dialogue ? DisplayStyle.Flex : DisplayStyle.None;
            speaker.text = state.speaker;
            line.text = state.line;
            feedback.text = state.feedback;
            feedback.style.display = string.IsNullOrEmpty(state.feedback) ? DisplayStyle.None : DisplayStyle.Flex;
            repair.text = state.repair == "" ? "" : "Try saying: " + state.repair;
            repair.style.display = state.repair == "" ? DisplayStyle.None : DisplayStyle.Flex;
            transcript.text = state.transcript;
            transcript.style.display = string.IsNullOrEmpty(state.transcript) ? DisplayStyle.None : DisplayStyle.Flex;
            status.text = state.status;
            var idle = !state.busy && state.recording == "idle";
            reply.SetEnabled(idle && !state.complete);
            send.SetEnabled(idle && !state.complete);
            record.text = state.recording == "recording" ? "Stop recording" : state.recording == "requesting" ? "Cancel request" : "Record reply";
            record.SetEnabled(!state.busy && !state.complete);
            hear.SetEnabled(idle);
            hearRepair.SetEnabled(idle);
            hearRepair.style.display = state.repair == "" ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void ClearInput()
        {
            reply.value = "";
        }
    }
}
