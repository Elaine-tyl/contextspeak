using System;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Runs one scenario with one NPC: speaks the line, takes the answer, applies the repeat gate,
    /// raises each step's gameplay event and updates XP. The UI reads its state.
    public sealed class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }
        public DialogueStateMachine Machine { get; } = new DialogueStateMachine();
        public NPCController Npc { get; private set; }
        public SpeechEvaluationManager Speech { get; private set; }
        public bool Active { get; private set; }
        public string Heard { get; private set; } = "";
        public bool HeardSpoken { get; private set; }
        public event Action<string> Event;        // step onComplete keys
        public event Action<ScenarioDefinition> Finished;
        private Action done;
        private CafePlayer player;

        private void Awake()
        {
            Instance = this;
            Speech = gameObject.AddComponent<SpeechEvaluationManager>();
            Speech.Heard += OnHeard;
            Machine.StepCompleted += step =>
            {
                SaveManager.Data.xp += 5;
                SaveManager.Save();
                if (!string.IsNullOrEmpty(step.onComplete)) Event?.Invoke(step.onComplete);
            };
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Begin(ScenarioDefinition scenario, NPCController npc, Action onDone = null)
        {
            if (scenario == null || scenario.Steps.Count == 0) { onDone?.Invoke(); return; }
            End(false);
            player = FindFirstObjectByType<CafePlayer>();
            Npc = npc;
            done = onDone;
            Heard = "";
            Machine.Start(scenario, SaveManager.Band, SaveManager.Data.learningMode);
            Active = true;
            if (Npc != null) Npc.Talking = true;
            if (player != null) { player.Blocked = true; player.ConversationPartner = Npc != null ? Npc.transform : null; }
            if (InteractionManager.Instance != null) InteractionManager.Instance.Modal = true;
            SpeakCurrent();
        }

        public void SpeakCurrent(bool slow = false) => Speech.Speak(Machine.NpcLine, slow || SaveManager.Band <= Slice.IeltsBand.Band4);
        public void SpeakTarget(bool slow = false) => Speech.Speak(Machine.Target, slow);

        private void OnHeard(string text, bool spoken)
        {
            if (!Active) return;
            Heard = text;
            HeardSpoken = spoken;
            var before = Machine.Index;
            if (Machine.Phase == DialoguePhase.AwaitingRepeat) Machine.Repeat(text, spoken);
            else Machine.Answer(text, spoken);
            if (Machine.Phase == DialoguePhase.AwaitingRepeat) SpeakTarget(true);
            else if (Machine.Index != before) Advance();
        }

        private void Advance()
        {
            if (Machine.Phase == DialoguePhase.Complete)
            {
                var scenario = Machine.Scenario;
                End(true);
                Finished?.Invoke(scenario);
                return;
            }
            Heard = "";
            SpeakCurrent();
        }

        public void Skip()
        {
            var before = Machine.Index;
            if (Machine.Skip() && Machine.Index != before) Advance();
        }

        /// Leave the conversation (progress inside the scenario is not kept).
        public void End(bool completed)
        {
            if (!Active) return;
            Active = false;
            Speech.Cancel();
            if (Npc != null) Npc.Talking = false;
            if (player != null) { player.Blocked = false; player.ConversationPartner = null; player.Controls?.Clear(); }
            LifeUI.RefreshModalState();
            var callback = done;
            done = null;
            if (completed) callback?.Invoke();
        }
    }
}
