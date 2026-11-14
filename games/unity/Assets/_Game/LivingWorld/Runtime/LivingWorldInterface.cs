using System;
using System.Linq;
using ContextSpeak.Learning;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.LivingWorld
{
    /// Stable controls preserve microphone focus; only the small choice list changes with the turn.
    public sealed class LivingWorldInterface : MonoBehaviour
    {
        private DynamicLifeEventManager director;
        private VisualElement root, overlay, choices, memories;
        private ScrollView journal;
        private Label title, prompt, goal, feedback, repair, transcript, explanation, summary;
        private Button record, repeat, model, reconnect, close;
        private VoiceCoachPanel coach;
        private DropdownField bands;
        private string signature = "", journalSignature = "";

        public void Bind(DynamicLifeEventManager owner)
        {
            director = owner;
            root = CafeInterface.CreateRoot(gameObject);
            root.style.display = DisplayStyle.Flex; root.pickingMode = PickingMode.Ignore;
            GetComponent<UIDocument>().sortingOrder = 1000;
            var sheet = Resources.Load<StyleSheet>("LivingWorld/Styles");
            if (sheet != null) root.styleSheets.Add(sheet);
            var toolbar = new VisualElement { name = "story-toolbar" }; root.Add(toolbar);
            toolbar.Add(Button("✦ Life stories", director.OpenJournal));
            // In the café, stories live in the phone so this launcher cannot cover the minimap.
            if (FindFirstObjectByType<ContextSpeak.Slice.SliceGame>() != null)
                toolbar.style.display = DisplayStyle.None;
            overlay = new VisualElement { name = "story-overlay" }; root.Add(overlay);
            var card = new ScrollView { name = "story-card" }; card.AddToClassList("subtitle-card"); overlay.Add(card);
            var header = new VisualElement(); header.AddToClassList("story-row"); card.Add(header);
            title = new Label(); title.AddToClassList("story-title"); header.Add(title);
            close = Button("×", director.Close); close.tooltip = "Close and save for later"; close.AddToClassList("story-close"); header.Add(close);
            prompt = new Label(); prompt.AddToClassList("story-prompt"); card.Add(prompt);
            goal = new Label(); goal.AddToClassList("story-muted"); card.Add(goal);
            choices = new VisualElement { name = "story-choices" }; card.Add(choices);
            transcript = new Label(); transcript.AddToClassList("story-muted"); card.Add(transcript);
            repair = new Label(); repair.AddToClassList("story-repair"); card.Add(repair);
            explanation = new Label(); explanation.AddToClassList("story-muted"); card.Add(explanation);
            feedback = new Label(); feedback.AddToClassList("story-feedback"); card.Add(feedback);
            var actions = new VisualElement(); actions.AddToClassList("story-row"); card.Add(actions);
            record = Button("🎙 Speak", director.Record); actions.Add(record);
            repeat = Button("Hear NPC", director.SpeakPrompt); actions.Add(repeat);
            model = Button("Hear corrected reply", director.HearCorrection); actions.Add(model);
            reconnect = Button("Reconnect AI", director.Connect); card.Add(reconnect);
            coach = new VoiceCoachPanel(card);
            var disclaimer = new Label("Speaking practice feedback · not an official IELTS score.");
            disclaimer.AddToClassList("practice-note"); card.Add(disclaimer);
            card.Add(new Label("Recordings are processed by your configured local speech companion. Only confirmed story facts are saved. Pronunciation is not scored from text.") { name = "story-note" });

            journal = new ScrollView { name = "story-journal" }; journal.AddToClassList("phone"); overlay.Add(journal);
            var top = new VisualElement(); top.AddToClassList("story-row"); journal.Add(top);
            top.Add(new Label("Life stories & people") { name = "story-journal-title" });
            top.Add(Button("×", director.Close));
            bands = new DropdownField("Speaking practice target", Enumerable.Range(3, 7).Select(band => "IELTS " + band).ToList(), 2);
            bands.RegisterValueChangedCallback(change => director.SetBand(int.Parse(change.newValue.Substring(6)))); journal.Add(bands);
            journal.Add(new Label("Practice goals, not an official IELTS score.") { name = "story-target-note" });
            summary = new Label(); journal.Add(summary);
            journal.Add(Button("Wait until evening", director.WaitForEvening));
            journal.Add(Button("Rainy-day practice", director.PreviewRain));
            journal.Add(Button("Return to Forest Café", director.ReturnToCafe));
            memories = new VisualElement(); journal.Add(memories);
            director.Changed += Refresh;
            Refresh();
        }
        private static Button Button(string text, Action action) => new Button(action) { text = text };
        private static void Show(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        private void Refresh()
        {
            if (director == null || root == null) return;
            // This persistent presenter is created in Bootstrap, before the additive district.
            // Re-evaluate ownership after each BindScene notification, not just in Bind().
            Show(root.Q("story-toolbar"), !director.HasPhone);
            Show(root, director.Ready);
            Show(overlay, director.DialogueOpen || director.JournalOpen);
            var card = root.Q<ScrollView>("story-card"); Show(card, director.DialogueOpen); Show(journal, director.JournalOpen);
            if (director.JournalOpen) { RefreshJournal(); return; }
            if (!director.DialogueOpen) return;
            var engine = director.Stories; var current = engine.Current;
            var complete = !engine.Active;
            title.text = current.NpcName + " · " + current.Role;
            prompt.text = complete ? "Thanks for talking. Your choice is saved." : director.Scenario.Prompt;
            goal.text = director.Recall ? "Independent recall · answer without the correction or hints." : complete ? current.Title + " · complete" : "IELTS " + director.Band + " practice · " + director.Scenario.Goal
                + "\nUseful language: " + string.Join(" · ", director.Scenario.Vocabulary);
            foreach (var label in card.Query<Label>().ToList()) label.enableRichText = false;
            coach.Render(director.Hints, director.Assessment, director.Recall);
            transcript.text = director.Heard == "" ? "" : "You said: " + director.Heard;
            repair.text = director.Correction == "" ? "" : "Say it again: " + director.Correction;
            explanation.text = director.Explanation; feedback.text = director.Recording == "recording" ? "Recording · " + new string('▮', Mathf.Max(1, Mathf.RoundToInt(director.InputLevel * 16))) : director.Feedback;
            Show(transcript, !director.Recall && director.Heard != ""); Show(repair, !director.Recall && director.Correction != "");
            Show(explanation, !director.Recall && director.Explanation != ""); Show(feedback, !director.Recall);
            Show(choices, !director.Recall);
            var key = engine.Ticket + "/" + director.SelectedChoice + "/" + director.Busy + "/" + director.Recording + "/" + complete;
            if (key != signature)
            {
                signature = key; choices.Clear();
                if (!complete)
                    foreach (var choice in current.Steps[engine.Step].Choices)
                    {
                        var id = choice.Id;
                        var button = Button(choice.Label, () => director.Choose(id));
                        button.EnableInClassList("selected", director.SelectedChoice == id);
                        button.SetEnabled(!director.Busy && director.Recording == "idle" && director.Correction == "");
                        choices.Add(button);
                    }
            }
            record.text = director.Recording == "recording" ? "Stop recording" : director.Recording == "requesting" ? "Cancel microphone" : "Record reply";
            record.SetEnabled(!director.Busy && !complete && director.SelectedChoice != "" && director.Connected);
            repeat.SetEnabled(!director.Busy && !complete && !director.Recall && director.Recording == "idle");
            
            Show(model, !director.Recall && director.Correction != ""); Show(reconnect, !director.Connected);
        }
        private void RefreshJournal()
        {
            bands.SetValueWithoutNotify("IELTS " + director.Band);
            var state = director.Stories.State;
            summary.text = state.Completed.Count + " conversations complete · " + state.Receipts.Count + " confirmed speaking steps\n" + director.Feedback;
            var key = JsonUtility.ToJson(state) + ":" + director.Band;
            if (key == journalSignature) return;
            journalSignature = key; memories.Clear();
            foreach (var item in director.Stories.Templates)
            {
                var row = new VisualElement(); row.AddToClassList("story-entry"); memories.Add(row);
                var done = state.Completed.Contains(item.Id);
                var locked = item.Prerequisite != "" && !state.Flags.Contains(item.Prerequisite);
                row.Add(new Label((done ? "✓ " : "✦ ") + item.Title));
                row.Add(new Label(item.Location + " · " + (done ? "Your choice is saved" : locked ? "Follow the earlier invitation first" : item.OnlyRain ? "During rain" : item.StartMinute >= 1080 ? "After 6 pm" : "Available")));
                var buttons = new VisualElement(); buttons.AddToClassList("story-row"); row.Add(buttons);
                var track = Button("Track destination", () => director.Track(item.Id)); track.SetEnabled(!locked && !done); buttons.Add(track);
                var begin = Button("Talk when nearby", () => director.Begin(item.Id)); begin.SetEnabled(!locked && !done); buttons.Add(begin);
            }
            memories.Add(new Label("People who remember you"));
            foreach (var memory in state.Memories)
            {
                var row = new VisualElement(); row.AddToClassList("story-entry"); memories.Add(row);
                row.Add(new Label(memory.Name + " · connection " + memory.Relationship + "/100"));
                foreach (var fact in memory.Facts.TakeLast(3)) row.Add(new Label("• " + fact));
                foreach (var promise in memory.Promises) row.Add(new Label("Promise: " + promise));
            }
            if (state.Itinerary != "") memories.Add(new Label("Travel outcome: " + state.Itinerary));
        }
        private void OnDestroy() { if (director != null) director.Changed -= Refresh; }
    }
}
