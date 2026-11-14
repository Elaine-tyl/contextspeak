using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Learning;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    [InitializeOnLoad]
    public static class AdaptiveLearningChecks
    {
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.local/v7"));
        static AdaptiveLearningChecks() => EditorApplication.update += Tick;
        private static void Tick()
        {
            var export = Path.Combine(Folder, "export.request");
            if (File.Exists(export) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(export);
                try { Verify(); AuthoredWebBuild.Build(); File.WriteAllText(Path.Combine(Folder, "export-result.txt"), "PASS browser export"); }
                catch (Exception error) { File.WriteAllText(Path.Combine(Folder, "export-result.txt"), error.ToString()); Debug.LogException(error); }
                return;
            }
            var request = Path.Combine(Folder, "verify.request");
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Verify(); File.WriteAllText(Path.Combine(Folder, "native-result.txt"), "PASS V7 assets, JsonUtility round-trip and native recall/exam UI"); }
            catch (Exception error) { File.WriteAllText(Path.Combine(Folder, "native-result.txt"), error.ToString()); Debug.LogException(error); }
        }

        [UnityEditor.MenuItem("ContextSpeak/Checks/Adaptive IELTS V7")]
        public static void Verify()
        {
            var assets = Resources.LoadAll<LearningScenarioAsset>("LearningV7");
            if (assets.Length != 9 || new[] { "cafe", "school", "home" }.Any(location => !assets.Any(asset => asset.Definition.Id == location)))
                throw new Exception("All nine authored Café/School/Home learning assets must import successfully.");
            if (CafeClient.ReadTranscript(System.Text.Encoding.UTF8.GetBytes("{\"text\":\"" + new string('a', 1400) + "\"}"), 2400).Length != 1400)
                throw new Exception("Complete exam transcripts must reach assessment.");
            var memory = new LearnerMemory();
            var practice = new IELTSPracticeManager(memory);
            practice.Begin(assets.First(asset => asset.Definition.Id == "cafe").AtBand(3), GameMode.GuidedLearning, DateTime.UtcNow);
            practice.Submit(new UtteranceEvidence { Text = "Can I has a latte?", Correction = "Can I have a latte?", Spoken = true,
                ProviderAvailable = true, GoalAssessed = true, Relevant = true, GoalMet = true }, DateTime.UtcNow);
            var save = new SliceSave { Learner = memory };
            var copy = SliceSave.Sanitize(JsonUtility.FromJson<SliceSave>(JsonUtility.ToJson(save)));
            if (copy.Learner.Pending.Count != 1 || copy.Learner.History.Count != 1 || copy.Wallet.Card != save.Wallet.Card)
                throw new Exception("Native save round-trip lost learning or legacy wallet fields.");
            var owner = new GameObject("V7 UI regression (no gameplay subscriber)");
            try
            {
                var ui = owner.AddComponent<SliceInterface>();
                typeof(SliceInterface).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ui, null);
                var view = new SliceView {
                    Place = "Forest Café", Time = "10:00", Weather = "Clear", Cash = "$30", Card = "$60", Band = "IELTS 5 practice",
                    Talking = Partner.Resident, Speaker = "Maya", Role = "Barista", Line = "What would you like?", Subtitles = true,
                    Mic = "idle", Status = "", Recognized = "Can I have a latte?", AiTip = "", Panel = "", PracticeTarget = "", ModelAnswer = "",
                    Save = save, Cart = save.Cart, QuestEarned = Array.Empty<string>(), Markers = Array.Empty<Vector4>(),
                    Glossary = Array.Empty<GlossaryEntry>(), History = Array.Empty<ChatLine>(), CharacterName = "Alex",
                    Adaptive = true, LearningStage = LearningStage.Recall, LearningFeedback = "Hidden correction", LearningTarget = "",
                    LearningStatus = "Independent recall", CanReplay = true,
                };
                var root = owner.GetComponent<UIDocument>().rootVisualElement;
                foreach (var character in new[] { PlayerLook.Boy, PlayerLook.Girl })
                {
                    view.Character = character; ui.Render(view);
                    if (root.Q("slice-dialogue").Query<TextField>().ToList().Count != 0) throw new Exception("Speaking must remain voice-only.");
                    if (root.Q("slice-history").style.display.value != DisplayStyle.None) throw new Exception("Recall history must conceal the corrected sentence.");
                    if (root.Query<Button>().ToList().Any(button => button.text == "Repeat" && button.enabledSelf)) throw new Exception("Recall cannot replay a correction.");
                }
                view.LearningExam = true; view.LearningComplete = false; ui.Render(view);
                var foldout = root.Q("learning-v7").Q<Foldout>();
                if (foldout.style.display.value != DisplayStyle.None) throw new Exception("Exam feedback must stay hidden.");
                view.Panel = "learning"; ui.Render(view);
                if (!root.Query<Button>().ToList().Any(button => button.text == "School · Ms Taylor")) throw new Exception("Phone must expose the three-location learning route.");
                Debug.Log("V7 native checks passed: 9 assets, native save round-trip, both characters, hidden recall, deferred exam feedback and phone route.");
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
