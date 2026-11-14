using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// One mandatory exchange: the NPC's line at three difficulty levels, what a good answer must
    /// convey (meaning, not exact words), a natural model answer, and vocabulary help.
    [Serializable]
    public sealed class ScenarioStep
    {
        public string id = "step";
        public string speaker = "";          // NPC key in the scene (e.g. "receptionist")
        [TextArea] public string simple = "";    // IELTS 3–4: short, slow, concrete
        [TextArea] public string standard = "";  // IELTS 5–6
        [TextArea] public string advanced = "";  // IELTS 7–8: follow-ups, explanation, abstraction
        [TextArea] public string expert = "";    // IELTS 9 (optional): abstract, nuanced, spontaneous
        public string goal = "";             // shown to the learner
        public string meaning = "";          // regex alternatives that show the task was done
        public string model = "";            // a natural answer
        public string vocabulary = "";       // "word: explanation|word: explanation"
        public string onComplete = "";       // event raised for the location's gameplay
    }

    /// An editable conversation script (created by the location builder; edit in the Inspector).
    [CreateAssetMenu(menuName = "ContextSpeak/Scenario", fileName = "Scenario")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        public string Title = "Scenario";
        public string Location = "";
        public List<ScenarioStep> Steps = new List<ScenarioStep>();
    }
}
