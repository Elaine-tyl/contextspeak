using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Base for a location's gameplay (hospital, restaurant, café): owns the quest and decides what
    /// each NPC says at each stage. Concrete flows configure the shared systems.
    public abstract class LocationFlow : MonoBehaviour
    {
        public static LocationFlow Current { get; private set; }
        public ScenarioDefinition[] Scenarios = new ScenarioDefinition[0];
        public abstract string QuestId { get; }
        public abstract string QuestTitle { get; }
        public virtual bool Busy => ScenarioManager.Instance != null && ScenarioManager.Instance.Active || LifeUI.PanelOpen;
        public string Objective { get; protected set; } = "";

        protected virtual void Awake() { Current = this; }
        protected virtual void OnDestroy() { if (Current == this) Current = null; }

        protected virtual void Start()
        {
            QuestManager.Restart(QuestId, QuestTitle);
        }

        public abstract void Talk(NPCController npc);

        private readonly System.Collections.Generic.Dictionary<string, ScenarioDefinition> fallbacks = new System.Collections.Generic.Dictionary<string, ScenarioDefinition>();

        /// The scene's edited asset if present, otherwise the built-in script.
        protected ScenarioDefinition Scenario(string title)
        {
            var asset = Scenarios.FirstOrDefault(item => item != null && (item.name == title || item.Title == title));
            if (asset != null) return asset;
            if (!fallbacks.TryGetValue(title, out var built)) fallbacks[title] = built = ScenarioLibrary.Create(title);
            return built;
        }

        protected NPCController Npc(string key) => NPCController.All.FirstOrDefault(item => item.Key == key);

        protected void Step(int step, string objective)
        {
            Objective = objective;
            QuestManager.Advance(QuestId, step, objective);
        }

        protected void Run(string scenario, NPCController npc, System.Action done)
        {
            var definition = Scenario(scenario);
            if (definition == null) { LifeUI.Toast("Missing scenario asset: " + scenario + ". Rebuild the locations."); done?.Invoke(); return; }
            ScenarioManager.Instance.Begin(definition, npc, done);
        }
    }
}
