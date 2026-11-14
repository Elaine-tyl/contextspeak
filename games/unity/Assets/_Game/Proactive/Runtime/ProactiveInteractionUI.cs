using System;
using ContextSpeak.Cafe.UI;
using ContextSpeak.Learning;
using UnityEngine;
using UnityEngine.UIElements;
namespace ContextSpeak.Proactive
{
    public sealed class ProactiveInteractionUI : MonoBehaviour
    {
        private ProactiveLifeDirector director;
        private VisualElement root, invitation, conversation, settings;
        private Label offer, line, status, correction, phase, heard;
        private Button record, repeat, reference, replay, hear;
        private VoiceCoachPanel coach;
        private Label ambient;
        private Label settingsStatus;
        private float ambientUntil;
        public static VisualElement AttachOverlay(GameObject owner)
        {
            var overlay = GameUiRoot.Attach(owner);
            // Later-created world HUD documents otherwise intercept pointer hits on this older document.
            owner.GetComponent<UIDocument>().sortingOrder = 100;
            return overlay;
        }
        public void Bind(ProactiveLifeDirector value)
        {
            director = value;
            root = AttachOverlay(gameObject);
            root.AddToClassList("proactive-ui");
            root.styleSheets.Add(Resources.Load<StyleSheet>("ProactiveStyles"));
            var gear = new Button(ToggleSettings) { text = "N · NPCs", tooltip = "NPC frequency and speaking mode · N" };
            gear.AddToClassList("proactive-settings-button"); root.Add(gear);
            ambient = new Label { enableRichText = false, pickingMode = PickingMode.Ignore }; ambient.AddToClassList("proactive-ambient"); root.Add(ambient); ambient.style.display = DisplayStyle.None;
            invitation = Panel("proactive-offer");
            offer = Label(invitation, "");
            Button(invitation, "E · Respond", () => director.Accept(false));
            Button(invitation, "🎙 Respond", () => director.Accept(true));
            Button(invitation, "Not now", director.Decline);
            Label(invitation, "Keep walking to ignore. No penalty.");
            settings = Panel("proactive-settings"); Label(settings, "ContextSpeak · NPC encounters");
            settingsStatus = Label(settings, "");
            foreach (InteractionFrequency frequency in Enum.GetValues(typeof(InteractionFrequency)))
            {
                var selected = frequency;
                Button(settings, frequency.ToString(), () => { director.SetFrequency(selected); LabelState(); });
            }
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                var selected = mode;
                Button(settings, mode.ToString(), () => { director.SetMode(selected); LabelState(); });
            }
            Button(settings, "Resume saved encounter", () => { director.ResumeNearby(); HideSettings(); });
            Button(settings, "Reconnect speech", director.Conversation.Connect);
            Button(settings, "Close", HideSettings);
            conversation = Panel("proactive-dialogue");
            Label(conversation, "ContextSpeak · NPC encounter");
            Button(conversation, "×", director.Close).tooltip = "Close and keep encounter progress";
            line = Label(conversation, ""); phase = Label(conversation, "");
            coach = new VoiceCoachPanel(conversation);
            status = Label(conversation, ""); correction = Label(conversation, ""); heard = Label(conversation, "");
            var row = new VisualElement(); row.AddToClassList("proactive-actions"); conversation.Add(row);
            record = Button(row, "🎙 Speak", director.Conversation.Record);
            repeat = Button(row, "Practise correction", director.Conversation.RepeatCorrection);
            hear = Button(row, "🔊 NPC", director.Conversation.SpeakPrompt);
            reference = Button(row, "Hear model", director.Conversation.HearModel);
            replay = Button(row, "My recording", director.Conversation.Replay);
            director.Conversation.Changed += Refresh;
            LabelState(); HideSettings(); Refresh();
        }
        private VisualElement Panel(string name) { VisualElement panel = name == "proactive-dialogue" ? new ScrollView() : new VisualElement(); panel.name = name; panel.AddToClassList("proactive-panel"); root.Add(panel); return panel; }
        private static Label Label(VisualElement parent, string text) { var label = new Label(text) { enableRichText = false }; parent.Add(label); return label; }
        private static Button Button(VisualElement parent, string text, Action action) { var button = new Button(action) { text = text }; parent.Add(button); return button; }
        private void HideSettings() { settings.style.display = DisplayStyle.None; director.SettingsOpen = false; }
        public void ToggleSettings()
        {
            if (director.Conversation.Open || director.Player != null && director.Player.Blocked) return;
            settings.style.display = settings.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex;
            director.SettingsOpen = settings.style.display == DisplayStyle.Flex;
        }
        private void LabelState() { settingsStatus.text = "Current: " + director.State.Settings.Frequency + " · " + director.State.Settings.Mode; }
        public void Ambient(string name, string text) { ambient.text = name + ": " + text; ambient.style.display = DisplayStyle.Flex; ambientUntil = Time.unscaledTime + 4; }
        private void Update() { if (ambient != null && Time.unscaledTime > ambientUntil) ambient.style.display = DisplayStyle.None; }
        public void Refresh()
        {
            if (director == null) return;
            var chat = director.Conversation;
            invitation.style.display = director.Offered != null && !chat.Open ? DisplayStyle.Flex : DisplayStyle.None;
            offer.text = director.Offered != null ? director.Offered.DisplayName + ": " + director.OfferLine : "";
            conversation.style.display = chat.Open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!chat.Open) return;
            line.text = director.Actor.DisplayName + ": " + chat.Prompt;
            phase.text = (director.State.Settings.Mode == GameMode.MockExam ? "EXAM · feedback at the end" : "Target IELTS " + director.Band + " practice · " + director.State.Settings.Mode + " · " + chat.Stage);
            status.text = chat.Recording == "recording" ? "Recording · " + new string('▮', Mathf.Max(1, Mathf.RoundToInt(chat.InputLevel * 16))) : chat.Connected ? chat.Status : "MOCK / unassessed — speech service unavailable. Reconnect in NPC settings.";
            correction.text = chat.Correction;
            correction.style.display = chat.Recall || chat.Exam || chat.Correction == "" ? DisplayStyle.None : DisplayStyle.Flex;
            heard.text = "You said: " + chat.Heard;
            heard.style.display = chat.Recall || chat.Exam || chat.Heard == "" ? DisplayStyle.None : DisplayStyle.Flex;
            coach.Render(chat.Hints, chat.Assessment, chat.Recall || chat.Exam && !chat.Complete,
                report: chat.Exam && chat.Complete ? chat.Feedback : "");
            record.text = chat.Recording == "recording" ? "■ Stop" : chat.Recording == "requesting" ? "Cancel mic" : "🎙 Speak";
            record.SetEnabled(!chat.Busy && !chat.Complete && (chat.Exam || chat.Stage != LearningStage.Feedback));
            repeat.style.display = !chat.Exam && chat.Stage == LearningStage.Feedback ? DisplayStyle.Flex : DisplayStyle.None;
            repeat.SetEnabled(!chat.Busy);
            reference.style.display = chat.Correction != "" && !chat.Recall && !chat.Exam ? DisplayStyle.Flex : DisplayStyle.None;
            replay.style.display = chat.CanReplay ? DisplayStyle.Flex : DisplayStyle.None;
            hear.SetEnabled(!chat.Recall && !chat.Busy && chat.Recording == "idle");
            reference.SetEnabled(chat.Recording == "idle" && !chat.Busy);
            replay.SetEnabled(chat.Recording == "idle" && !chat.Busy);
        }
    }
}
