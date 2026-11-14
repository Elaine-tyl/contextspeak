using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Learning;
using UnityEngine;

namespace ContextSpeak.Slice
{
    /// Additive V7 adapter: the original waiter, wallet, character and world systems keep ownership.
    public sealed partial class SliceGame
    {
        private LearningScenarioAsset[] learningContent;
        private IELTSPracticeManager learning;
        private readonly IELTSMockExamManager mockExam = new IELTSMockExamManager();
        private bool adaptiveActive, journalPractice;
        private int adaptiveTurns;
        private string learningFeedback = "", learningStatus = "";
        private AudioSource replaySource;
        private AudioClip recordedReply;
        private AssessmentRecord learningAssessment;

        public bool LearningExamActive => mockExam.Active;

        // Merge only new event evidence, leaving vocabulary schedules and unrelated save fields intact.
        public void SaveProactiveEvidence(LearnerMemory source)
        {
            if (Save == null || source == null) return;
            var added = false;
            foreach (var entry in source.History)
                if (!Save.Learner.History.Any(existing => existing.Id == entry.Id))
                { Save.Learner.History.Add(entry); added = true; }
            if (!added) return;
            while (Save.Learner.History.Count > 500) Save.Learner.History.RemoveAt(0);
            Persist();
        }

        private void InitializeAdaptive()
        {
            Save.Learner = LearningMemoryService.Sanitize(Save.Learner);
            learningContent = Resources.LoadAll<LearningScenarioAsset>("LearningV7");
            replaySource = gameObject.AddComponent<AudioSource>();
            replaySource.spatialBlend = 0;
            gameObject.AddComponent<VoicePushToTalk>().Bind(() => adaptiveActive && Talking != Partner.None, () => recorder.State, Record);
        }

        private LearningScenarioAsset Content(string location)
        {
            var band = AdaptiveDialogueManager.RecommendedBand(Save.Learner, (int)Save.Band);
            return learningContent?.FirstOrDefault(asset => asset.Definition.Id == location && band >= asset.MinBand && band <= asset.MaxBand);
        }

        private bool BeginAdaptiveResident()
        {
            var content = Content(resident.SceneId);
            if (content == null) return false;
            adaptiveActive = true;
            adaptiveTurns = 0;
            learningFeedback = ""; learningAssessment = null;
            if (Save.Learner.Mode == GameMode.MockExam)
            {
                mockExam.Begin(DateTime.UtcNow, (int)Save.Band);
                learningStatus = mockExam.Status;
                Say(Speaker, mockExam.Prompt);
            }
            else
            {
                BeginLearningQuestion(content.AtBand(AdaptiveDialogueManager.RecommendedBand(Save.Learner, (int)Save.Band)), content.ReviewDays);
                Say(Speaker, learning.Scenario.Prompt);
            }
            Render();
            return true;
        }

        private void BeginLearningQuestion(PracticeScenario scenario, int[] intervals)
        {
            learning = new IELTSPracticeManager(Save.Learner, intervals);
            learning.Begin(scenario, Save.Learner.Mode, DateTime.UtcNow);
            learningStatus = learning.Stage == LearningStage.IndependentAnswer ? "Answer in your own words. Vocabulary hints are optional."
                : learning.Stage == LearningStage.Recall ? "Independent recall: the correction and history are hidden." : "Resume your correction practice before continuing.";
            Save.Learner = learning.Memory;
        }

        private bool SubmitAdaptive(string text, bool spoken, bool uiAction, double? acoustic)
        {
            if (!adaptiveActive) return false;
            if (working || string.IsNullOrWhiteSpace(text) || text.Length > 2400) return true;
            if (!spoken || uiAction) { learningStatus = "MOCK / rehearsal only — a real spoken answer is required. No learning credit."; Render(); return true; }
            if (!SpeechAvailable) { learningStatus = "MOCK / unassessed — reconnect speech and AI. Progress is unchanged."; Render(); return true; }
            if (learning?.Stage == LearningStage.Feedback && !mockExam.Active)
            { learningStatus = "Choose Practise correction, then record your reply."; Render(); return true; }
            if (!mockExam.Active && AdaptiveDialogueManager.Clarification(text))
            {
                recognized = text;
                Say(Speaker, "Of course. " + learning.Scenario.Prompt);
                learningStatus = "Clarification requested — no failed attempt or learning credit.";
                Render(); return true;
            }
            voice.Cancel();
            working = true;
            recognized = text;
            var current = epoch;
            var request = new SliceReviewRequest {
                band = mockExam.Active ? Profile.Label : BandProfile.For((IeltsBand)learning.Scenario.Band).Label, npc = Speaker, task = journalPractice ? "daily spoken journal" : resident.Place.Name,
                npcLine = mockExam.Active ? mockExam.Prompt : learning.Scenario.Prompt,
                goal = mockExam.Active ? "Answer the speaking prompt in your own words. Feedback is deferred until all parts finish." : learning.Scenario.Goal,
                text = text,
                reference = !mockExam.Active && learning.Stage == LearningStage.Repetition ? learning.Reference : "",
                history = Save.Learner.History.Where(entry => entry.Scenario == resident.SceneId).TakeLast(2).Select(entry => "Earlier answer: " + entry.Text)
                    .Concat(history.TakeLast(4).Select(entry => entry.Speaker + ": " + entry.Text))
                    .Select(value => value.Length > 300 ? value.Substring(0, 300) : value).ToArray(),
            };
            learningStatus = mockExam.Active ? "Response received. Preparing the next prompt…" : "Checking meaning and language…";
            StartCoroutine(companion.Review(request, result => {
                if (current != epoch || !adaptiveActive) return;
                working = false;
                var evidence = result?.Evidence(text) ?? new UtteranceEvidence { Text = text, Spoken = spoken };
                evidence.AcousticScore = acoustic.HasValue ? acoustic * 100 : null;
                evidence.AcousticProvider = acoustic.HasValue && pronunciation.Available ? "configured acoustic assessor" : "";
                history.Add(new ChatLine { Speaker = "You", Text = text, Player = true });
                if (mockExam.Active) AcceptExamAnswer(evidence);
                else AcceptLearningAnswer(evidence);
                Persist(); Render();
            }, error => {
                if (current != epoch) return;
                working = false;
                learningStatus = "MOCK / unassessed — AI review unavailable. No credit or task changes. " + error;
                Render();
            }));
            Render();
            return true;
        }

        private void AcceptLearningAnswer(UtteranceEvidence evidence)
        {
            var outcome = learning.Submit(evidence, DateTime.UtcNow);
            learningStatus = outcome.Status;
            learningFeedback = outcome.Assessment?.Feedback ?? "";
            learningAssessment = outcome.Assessment;
            if (learning.Stage == LearningStage.Feedback)
            {
                learning.ShowRepetition();
                voice.Say(companion, learning.VisibleCorrection, Profile.SpeechRate, error => Status(error));
                learningStatus = "Listen to the correction, then record it. Independent recall comes next.";
            }
            if (!outcome.Advance) return;
            if (journalPractice)
            {
                Save.Learner.JournalDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                learningStatus = "Journal saved. Your own words stay in learning history. Close when ready.";
                return;
            }
            adaptiveTurns++;
            var next = AdaptiveDialogueManager.Next(learning.Scenario, evidence, Save.Learner.Mode == GameMode.FreeLife ? 1 + (adaptiveTurns - 1) % 2 : adaptiveTurns,
                AdaptiveDialogueManager.RecommendedBand(Save.Learner, (int)Save.Band));
            if (next.Kind == FollowUpKind.EndConversation)
            {
                var transfer = ScenarioTransferManager.Next(Save.Learner, resident.SceneId, DateTime.UtcNow);
                learningStatus = "Conversation complete. " + (transfer == null ? "Review is scheduled for a later day; try another NPC now." : "A due review is ready in " + transfer.Location + ".");
                Say(Speaker, next.Question);
                return;
            }
            var content = Content(resident.SceneId);
            var scenario = content.AtBand(AdaptiveDialogueManager.RecommendedBand(Save.Learner, (int)Save.Band));
            scenario.Prompt = next.Question;
            scenario.Goal = next.Kind == FollowUpKind.Comparison ? "Compare two perspectives relevant to the previous answer. Accept any reasonable opinion." : "Give a relevant concrete example based on the previous answer; no prescribed vocabulary.";
            // Only the first task claims this expression. Follow-ups track a separate discourse skill.
            scenario.Model = next.Kind == FollowUpKind.Comparison
                ? "Some people prefer working alone, whereas I find working together helpful because we can share ideas."
                : resident.SceneId == "cafe" ? "For example, I asked for oat milk last week because my friend cannot have dairy."
                : resident.SceneId == "school" ? "For example, I asked my teacher to explain a task and then tried it by myself."
                : "For example, my friend helped me cook dinner when I was busy.";
            scenario.ItemId = next.Kind == FollowUpKind.Comparison ? "compare-perspectives" : "give-an-example";
            if (next.Kind == FollowUpKind.TopicChange)
            { scenario.Goal = next.Goal; scenario.Model = next.Model; scenario.ItemId = next.ItemId; scenario.Topic = next.ItemId.Substring(6); }
            BeginLearningQuestion(scenario, content.ReviewDays);
            Say(Speaker, next.Question);
        }

        private void AcceptExamAnswer(UtteranceEvidence evidence)
        {
            mockExam.StopRecording();
            if (!mockExam.MoveNext(evidence, DateTime.UtcNow)) { learningStatus = mockExam.Status; return; }
            if (mockExam.Complete)
            {
                var report = mockExam.Result(DateTime.UtcNow);
                Save.Learner.Exams.Add(report);
                if (Save.Learner.Exams.Count > 20) Save.Learner.Exams.RemoveAt(0);
                learningFeedback = report.Feedback;
                learningStatus = "Three-part rehearsal saved. This is not an official IELTS assessment.";
            }
            else learningStatus = "Answer the next part. Feedback remains hidden until completion.";
            Say(Speaker, mockExam.Prompt);
        }

        private void ObserveLearningObject(string label)
        {
            if (Save == null || string.IsNullOrEmpty(label)) return;
            foreach (var word in new[] { "book", "cup", "table", "counter", "chair", "menu", "kitchen", "timetable" })
                if (label.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LearningMemoryService.ObserveObject(Save.Learner, CurrentPlace(), word);
                    ShowToast(word + " · noticed in the world. Use it in a spoken answer to build evidence.");
                    Persist();
                    break;
                }
        }

        private void CaptureAdaptiveAudio(byte[] wave)
        {
            mockExam.StopRecording();
            if (recordedReply != null) Destroy(recordedReply);
            var decoded = CafeWave.Decode(wave);
            recordedReply = AudioClip.Create("Your last reply", decoded.Samples.Length, 1, decoded.Rate, false);
            recordedReply.SetData(decoded.Samples, 0);
        }

        private void EndAdaptive()
        {
            adaptiveActive = journalPractice = false;
            mockExam.Cancel();
            replaySource?.Stop();
            if (recordedReply != null) { Destroy(recordedReply); recordedReply = null; }
        }

        private bool AdaptiveCommand(string action, string argument)
        {
            if (!action.StartsWith("learn-", StringComparison.Ordinal)) return false;
            if (working || recorder.State == "recording" || recorder.State == "requesting") return true;
            if (action == "learn-mode" && Enum.TryParse(argument, out GameMode mode) && Enum.IsDefined(typeof(GameMode), mode))
            {
                if (Talking != Partner.None) EndConversation();
                Save.Learner.Mode = mode;
                ShowToast("Mode: " + mode + ". Existing quests and learning evidence are kept.");
            }
            if (action == "learn-repeat" && learning != null && adaptiveActive)
            { learning.ShowRepetition(); learningStatus = "Say the corrected meaning naturally. Exact wording is not required."; }
            if (recorder.State != "idle" && (action == "learn-audio" || action == "learn-reference")) return true;
            if (action == "learn-audio" && recordedReply != null) { voice.Cancel(); replaySource.clip = recordedReply; replaySource.Play(); }
            if (action == "learn-reference" && learning != null && learning.VisibleCorrection != "")
                voice.Say(companion, learning.VisibleCorrection, Profile.SpeechRate, error => Status(error));
            if (action == "learn-start" || action == "learn-review" || action == "learn-exam" || action == "learn-journal")
            {
                var transfer = action == "learn-review" ? ScenarioTransferManager.Next(Save.Learner, Save.Learner.LastLocation, DateTime.UtcNow) : null;
                var location = action == "learn-exam" ? "school" : action == "learn-journal" ? "home" : transfer?.Location ?? argument;
                var actor = residents.FirstOrDefault(npc => npc.SceneId == location);
                if (actor == null) { ShowToast("This saved scene has no " + location + " learning NPC. Open Forest Café from Life scenes."); return true; }
                if (Talking != Partner.None) EndConversation();
                if (action == "learn-exam") Save.Learner.Mode = GameMode.MockExam;
                else if (Save.Learner.Mode == GameMode.MockExam) Save.Learner.Mode = GameMode.GuidedLearning;
                Visit(location);
                resident = actor;
                BeginConversation(Partner.Resident);
                if (action == "learn-journal")
                {
                    journalPractice = true;
                    var scene = Content("home").AtBand((int)Save.Band);
                    scene.ItemId = "spoken-journal";
                    scene.Prompt = "Tell me about one thing that happened today, how you felt and what you might do differently tomorrow.";
                    scene.Goal = "Describe a personal experience and reflection. Any real or imagined safe experience is acceptable.";
                    BeginLearningQuestion(scene, Content("home").ReviewDays);
                    Say(Speaker, scene.Prompt);
                }
                if (transfer != null && transfer.Item.Id != "polite-requests")
                {
                    var scene = Content(location).AtBand((int)Save.Band);
                    scene = ScenarioTransferManager.Prepare(transfer, scene);
                    BeginLearningQuestion(scene, Content(location).ReviewDays);
                    Say(Speaker, scene.Prompt);
                }
            }
            Persist(); Render();
            return true;
        }
    }
}
