using System;
using ContextSpeak.Learning;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Editor
{
    public static class VoiceCoachNativeChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Voice coach V9")]
        public static void Verify()
        {
            var reply = JsonUtility.FromJson<SliceReviewReply>("{\"assessedGoal\":true,\"goalMet\":true,\"relevant\":true,\"lexicalTip\":\"Could I have a latte, please?\",\"languageScores\":{\"coherence\":80,\"lexical\":75,\"grammar\":90,\"evidence\":\"I'd like a latte.\"}}");
            var assessment = SpeakingAssessmentService.Assess(reply.Evidence("I'd like a latte."), IeltsBand.Band3);
            Require(assessment.GrammarScore == 90 && assessment.Pronunciation == null, "native provider marks and no acoustic invention");
            var root = new VisualElement();
            var panel = new VoiceCoachPanel(root);
            panel.Render(new[] { "latte", "medium", "oat milk" }, assessment, false);
            Require(root.Q("voice-hints").childCount == 3, "context chips");
            Require(root.Q<Label>("voice-improved").text.Contains("optional"), "natural alternative is explicitly optional");
            Require(root.Query<TextField>().ToList().Count == 0, "voice panel has no typed answer");
            panel.Render(new[] { "secret" }, assessment, true);
            Require(panel.Root.style.display.value == DisplayStyle.None && root.Q("voice-hints").childCount == 0
                && root.Q<Label>("voice-improved").text == "" && root.Q<Foldout>().style.display.value == DisplayStyle.None, "recall/exam conceal all answer-bearing content");
            panel.Render(Array.Empty<string>(), null, false, report: "Part 1: assessed feedback");
            Require(root.Q<Foldout>().style.display.value == DisplayStyle.Flex
                && root.Query<Label>().ToList().Exists(label => label.text.Contains("Part 1")), "completed exam report is visible without per-turn assessment");
            var save = new LearnerMemory();
            save.History.Add(new LearningEvidence { Id = "test", ImprovedExpression = assessment.ImprovedExpression, LanguageScoresAssessed = true, GrammarScore = 90 });
            var restored = JsonUtility.FromJson<LearnerMemory>(JsonUtility.ToJson(save));
            Require(restored.History[0].GrammarScore == 90 && restored.History[0].ImprovedExpression != "", "native feedback save/reload");
            Debug.Log("V9 native checks passed: scores, hints, optional alternatives, voice-only panel, concealed recall/exam and save/reload.");
        }
        public static void VerifyAndExport()
        {
            // Native fixtures may dirty their host. Start from a named scene, never save fixture changes.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Generated/CharacterCreation.unity");
            Verify();
            ProactiveNPCChecks.Verify();
            AdaptiveLearningChecks.Verify();
            AuthoredWebBuild.Build();
        }
        private static void Require(bool value, string name)
        { if (!value) throw new Exception("Voice coach V9: " + name); }
    }
}
