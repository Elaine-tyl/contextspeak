using System;
using System.Linq;
using ContextSpeak.Cafe;
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
            if (phase == 1)
            {
                view = new GameObject("Native UI regression").AddComponent<CafeInterface>();
                view.Refresh(new CafeSnapshot { dialogue = true, recording = "idle", speaker = "Maya", line = "What can I get for you?", objective = "Practice", status = "Your turn" });
                lifeView = new GameObject("Life UI regression").AddComponent<LifeInterface>();
                lifeView.Refresh(new LifeSnapshot { dialogue = true, recording = "idle", nearby = "", speaker = "Mom", line = "Could you pick up groceries?", objective = "Family errand", status = "Your turn", wallet = "$20", bag = "Empty", menu = "3 apples · 2 carrots", repair = "I want three apples." });
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
            Finish(visible && lifeVisible && arcadeVisible, $"Cafe {panel.worldBound}; input {reply.worldBound}; record {record.worldBound}; Life {lifePanel.worldBound}; input {lifeRoot.Q<TextField>().worldBound}; record {lifeRecord.worldBound}; arcade {arcadeVisible}; root {bounds}");
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
