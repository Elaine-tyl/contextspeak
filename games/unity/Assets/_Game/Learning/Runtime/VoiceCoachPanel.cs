using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Learning
{
    /// Shared view only: hints and feedback cannot advance a quest or submit a reply.
    public sealed class VoiceCoachPanel
    {
        public VisualElement Root { get; }
        private readonly VisualElement hints;
        private readonly Label marks, corrected, improved;
        private readonly Foldout details;
        private readonly Label notes;
        private string hintKey = "";
        public VoiceCoachPanel(VisualElement parent)
        {
            Root = new VisualElement { name = "voice-coach" };
            Root.AddToClassList("voice-coach");
            var sheet = Resources.Load<StyleSheet>("VoiceCoach");
            if (sheet != null) Root.styleSheets.Add(sheet);
            hints = new VisualElement { name = "voice-hints" }; hints.AddToClassList("voice-hints"); Root.Add(hints);
            marks = Text("voice-marks");
            corrected = Text("voice-corrected"); improved = Text("voice-improved");
            details = new Foldout { text = "Feedback", value = false };
            var scroll = new ScrollView(); scroll.style.maxHeight = 100;
            notes = new Label { enableRichText = false }; scroll.Add(notes); details.Add(scroll); Root.Add(details);
            parent.Add(Root);
        }
        private Label Text(string name)
        {
            var label = new Label { name = name, enableRichText = false };
            label.AddToClassList(name); Root.Add(label); return label;
        }
        public void Render(string[] words, AssessmentRecord assessment, bool concealed, bool visible = true, string report = "")
        {
            Root.style.display = visible && !concealed ? DisplayStyle.Flex : DisplayStyle.None;
            if (concealed) { details.style.display = DisplayStyle.None; hints.Clear(); hintKey = ""; marks.text = corrected.text = improved.text = notes.text = ""; return; }
            var key = string.Join("|", words ?? Array.Empty<string>());
            if (key != hintKey)
            {
                hintKey = key; hints.Clear();
                foreach (var word in words ?? Array.Empty<string>())
                {
                    var chip = new Label(word) { enableRichText = false };
                    chip.AddToClassList("voice-word"); hints.Add(chip);
                }
            }
            var assessed = assessment != null && assessment.Assessed;
            marks.text = assessment == null ? "" : VoiceCoach.Marks(assessment);
            corrected.text = assessed && assessment.Correction != "" ? "Correction: " + assessment.Correction : "";
            improved.text = assessed && !string.IsNullOrWhiteSpace(assessment.ImprovedExpression)
                ? "Another way (optional): " + assessment.ImprovedExpression : "";
            notes.text = report != "" ? report : assessed ? assessment.Feedback : "";
            foreach (var label in new[] { marks, corrected, improved }) label.style.display = label.text == "" ? DisplayStyle.None : DisplayStyle.Flex;
            details.style.display = assessed || report != "" ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
