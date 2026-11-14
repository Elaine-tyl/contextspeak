using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Editor
{
    // Run the real runtime panel in an isolated, empty Editor Play Mode scene.
    [InitializeOnLoad]
    public static class CafeNativeUiChecks
    {
        private const string PhaseKey = "ContextSpeak.NativeUiCheck.Phase";
        private const string StartedKey = "ContextSpeak.NativeUiCheck.Started";
        private static CafeInterface view;
        private static LifeInterface lifeView;
        private static CharacterCreationView creationView;
        private static SliceInterface sliceView;
        private static SliceView sliceSnapshot;
        private static ContextSpeak.Arcade.ArcadeBoard arcadeView;
        static CafeNativeUiChecks()
        {
            if (Environment.GetCommandLineArgs().Contains("-contextspeak-ui-test"))
                EditorApplication.update += Tick;
        }
        public static void Begin()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetInt(PhaseKey, 1);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            var phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0) > 40)
            {
                Finish(false, "Runtime UI did not become ready.");
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            // Editor packages may reload scripts after Play Mode starts. Static references disappear,
            // but SessionState survives; recreate only this isolated check's temporary UI objects.
            if (phase > 1 && view == null)
            {
                foreach (var name in new[] { "Native UI regression", "Life UI regression", "Forest slice UI regression", "Arcade UI regression", "Character UI regression" })
                {
                    var temporary = GameObject.Find(name);
                    if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
                }
                phase = 1;
                SessionState.SetInt(PhaseKey, phase);
            }
            if (phase == 1)
            {
                try { SliceRuntimeChecks.Verify(); }
                catch (Exception error) { Finish(false, error.ToString()); return; }
                view = new GameObject("Native UI regression").AddComponent<CafeInterface>();
                view.Refresh(new CafeSnapshot { dialogue = true, recording = "idle", speaker = "Maya", line = "What can I get for you?", objective = "Practice", status = "Your turn" });
                lifeView = new GameObject("Life UI regression").AddComponent<LifeInterface>();
                lifeView.Refresh(new LifeSnapshot { dialogue = true, recording = "idle", nearby = "", speaker = "Mom", line = "Could you pick up groceries?", objective = "Family errand", status = "Your turn", wallet = "$20", bag = "Empty", menu = "3 apples · 2 carrots", repair = "I want three apples." });
                sliceView = new GameObject("Forest slice UI regression").AddComponent<SliceInterface>();
                sliceSnapshot = new SliceView
                {
                    Place = "Forest Café", Time = "10:00", Weather = "Clear",
                    Cash = "$30", Card = "$60", Band = "IELTS 6.0–6.5",
                    Talking = Partner.Rowan, Speaker = "Rowan", Role = "Waiter · Forest Café",
                    Line = "What would you like to drink today?", Subtitles = true,
                    Mic = "idle", Status = "Your turn", Recognized = "", AiTip = "",
                    Panel = "", PracticeTarget = "", ModelAnswer = "",
                    Save = new SliceSave(), Cart = new OrderCart(),
                    QuestEarned = Array.Empty<string>(), Markers = Array.Empty<Vector4>(),
                    Glossary = Array.Empty<GlossaryEntry>(), History = Array.Empty<ChatLine>(),
                };
                sliceView.Render(sliceSnapshot);
                creationView = new GameObject("Character UI regression").AddComponent<CharacterCreationView>();
                creationView.Render(new CharacterProfile { Look = PlayerLook.Boy, Name = "Alex" }, 45, true, false, "");
                arcadeView = new GameObject("Arcade UI regression").AddComponent<ContextSpeak.Arcade.ArcadeBoard>();
                const string question = "{\"round\":{\"id\":\"round-a\",\"kind\":\"dictation\",\"track\":\"listening\",\"title\":\"Listen\",\"instruction\":\"Write what you hear\"},\"played\":true,\"recording\":\"idle\"}";
                arcadeView.OnHostState(question);
                arcadeView.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("response").value = "saved draft";
                arcadeView.OnHostState(question);
                SessionState.SetInt(PhaseKey, 2);
                return;
            }
            var document = view.GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            if (float.IsNaN(root.worldBound.height)) return;
            var panel = root.Q<VisualElement>("dialogue");
            var reply = root.Q<TextField>();
            var record = root.Query<Button>().ToList().First(button => button.text == "Record reply");
            var bounds = root.worldBound;
            if (panel.worldBound.width > 640 || lifeView.GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("dialogue").worldBound.width > 640)
            {
                Finish(false, "Conversation cards must stay compact instead of covering most of the scene.");
                return;
            }
            var visible = document.panelSettings.themeStyleSheet != null
                && document.panelSettings.scaleMode == PanelScaleMode.ScaleWithScreenSize
                && bounds.width > 100 && bounds.height > 100
                && panel.worldBound.yMin >= bounds.yMin && panel.worldBound.yMax <= bounds.yMax
                && reply.worldBound.height >= 30 && record.worldBound.height >= 40;
            var lifeRoot = lifeView.GetComponent<UIDocument>().rootVisualElement;
            var lifePanel = lifeRoot.Q<VisualElement>("dialogue");
            var lifeRecord = lifeRoot.Query<Button>().ToList().First(button => button.text == "Record reply");
            var lifeVisible = lifeRoot.worldBound.height > 100 && lifePanel.worldBound.yMin >= lifeRoot.worldBound.yMin
                && lifePanel.worldBound.yMax <= lifeRoot.worldBound.yMax && lifeRoot.Q<TextField>().worldBound.height >= 30
                && lifeRecord.worldBound.height >= 40;
            var arcadeRoot = arcadeView.GetComponent<UIDocument>().rootVisualElement;
            var draft = arcadeRoot.Q<TextField>("response");
            var arcadeVisible = arcadeRoot.worldBound.width > 100 && draft.worldBound.height >= 48
                && draft.value == "saved draft" && arcadeRoot.Q<Button>("check_answer").enabledSelf;
            var sliceRoot = sliceView.GetComponent<UIDocument>().rootVisualElement;
            var slicePanel = sliceRoot.Q<VisualElement>("slice-dialogue");
            var header = slicePanel.Q<VisualElement>(className: "dialogue-header");
            if (header.Query<Button>().ToList().Any(button => button.worldBound.xMin < slicePanel.worldBound.xMin
                || button.worldBound.xMax > slicePanel.worldBound.xMax))
            {
                Finish(false, "Dialogue header buttons must fit inside the card with the full NPC role and training target.");
                return;
            }
            var sliceVisible = slicePanel.worldBound.width > 100 && slicePanel.worldBound.width <= 640
                && slicePanel.worldBound.yMin >= sliceRoot.worldBound.yMin
                && slicePanel.worldBound.yMax <= sliceRoot.worldBound.yMax
                && slicePanel.Query<TextField>().ToList().Count == 0
                && slicePanel.Query<Button>().ToList().Any(button => button.ClassListContains("mic") && button.enabledSelf)
                && sliceRoot.Query<Label>().ToList().Any(label => label.text.StartsWith("ContextSpeak"));
            sliceSnapshot.Panel = "map";
            sliceView.Render(sliceSnapshot);
            var trailVisible = sliceRoot.Q<TextField>("trail-focus") != null
                && sliceRoot.Query<Button>().ToList().Any(button => button.text.StartsWith("Next stop"));
            sliceSnapshot.Markers = new[] { new Vector4(52, 2, 4, 1) };
            sliceView.Render(sliceSnapshot);
            var target = sliceRoot.Q<VisualElement>(className: "target");
            if (target == null || !target.ClassListContains("rowan") || Math.Abs(target.style.left.value.value - 100) > 0.1f)
            {
                Finish(false, "Replanning while stationary must refresh a target marker with separate CSS classes.");
                return;
            }
            var completed = new SceneLesson { SceneId = "home", Band = IeltsBand.Band3, Step = 3 };
            sliceSnapshot.Lesson = completed;
            sliceSnapshot.Save.Lessons.Add(completed);
            sliceView.Render(sliceSnapshot);
            var nextStop = sliceRoot.Q<VisualElement>("suggestions").Query<Button>().ToList().First(button => button.text.StartsWith("Next stop"));
            sliceView.Render(sliceSnapshot);
            if (nextStop != sliceRoot.Q<VisualElement>("suggestions").Query<Button>().ToList().First(button => button.text.StartsWith("Next stop")))
            {
                Finish(false, "Refreshing an unchanged completed lesson must retain its Next stop button and keyboard focus.");
                return;
            }
            sliceSnapshot.Panel = "pause";
            sliceSnapshot.Character = PlayerLook.Boy;
            sliceView.Render(sliceSnapshot);
            var characterButtons = sliceRoot.Query<Button>().ToList().Where(button => button.text == "Girl · Academy" || button.text == "Boy · Daydream").ToArray();
            if (characterButtons.Length != 2 || !characterButtons.Single(button => button.text == "Boy · Daydream").ClassListContains("selected"))
            {
                Finish(false, "Settings must show both protagonists and the current selection.");
                return;
            }
            var creationRoot = creationView.GetComponent<UIDocument>().rootVisualElement;
            var choices = creationRoot.Query<Button>().ToList();
            var girlChoice = choices.Single(button => button.text == "Female");
            var boyChoice = choices.Single(button => button.text == "Male");
            if (girlChoice.resolvedStyle.backgroundColor == boyChoice.resolvedStyle.backgroundColor)
            {
                Finish(false, "Selected and unselected protagonists must have visibly different buttons.");
                return;
            }
            var characterName = creationRoot.Q<TextField>();
            var enterWorld = creationRoot.Query<Button>().ToList().Single(button => button.text == "Save & enter world");
            if (characterName.value != "Alex" || characterName.worldBound.height < 30 || !enterWorld.enabledSelf
                || !creationRoot.Query<Button>().ToList().Single(button => button.text == "Male").ClassListContains("selected"))
            {
                Finish(false, "Character creation must render a usable name input and selected protagonist.");
                return;
            }
            Finish(visible && lifeVisible && arcadeVisible && sliceVisible && trailVisible, $"Cafe {panel.worldBound}; input {reply.worldBound}; record {record.worldBound}; Life {lifePanel.worldBound}; input {lifeRoot.Q<TextField>().worldBound}; record {lifeRecord.worldBound}; arcade {arcadeVisible}; slice {slicePanel.worldBound}, visible {sliceVisible}; root {bounds}");
        }
        private static void Finish(bool passed, string detail)
        {
            SessionState.SetInt(PhaseKey, 0);
            if (passed) Debug.Log("ContextSpeak native UI checks passed: " + detail);
            else Debug.LogError("ContextSpeak native UI checks failed: " + detail);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
