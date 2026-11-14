using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Learning;
using ContextSpeak.Slice;
using UnityEngine;
namespace ContextSpeak.Proactive
{
    /// Voice-first adapter to V7. Only a successful spoken learning outcome reaches the quest policy.
    public sealed class NPCConversationInitiator : MonoBehaviour
    {
        public bool Open { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "";
        public string Feedback { get; private set; } = "";
        public AssessmentRecord Assessment { get; private set; }
        public string[] Hints => VoiceCoach.Hints(practice?.Scenario, director?.State.Learner, Stage, Exam);
        public bool AudioBusy => voice != null && voice.Occupied || replay != null && replay.isPlaying;
        public string Heard { get; private set; } = "";
        public string Prompt => exam.Active ? exam.Prompt : practice?.Scenario?.Prompt ?? "";
        public string Correction => practice?.VisibleCorrection ?? "";
        public LearningStage Stage => practice?.Stage ?? LearningStage.IndependentAnswer;
        public bool Connected => services != null && services.Available;
        public float InputLevel => recorder?.Level ?? 0;
        public string Recording => recorder?.State ?? "idle";
        public bool Exam => exam.Active;
        public bool Complete { get; private set; }
        public event Action Changed;
        private CafeClient client;
        private CafeRecorder recorder;
        private SliceVoice voice;
        private CompanionServices services;
        private IELTSPracticeManager practice;
        private readonly IELTSMockExamManager exam = new IELTSMockExamManager();
        private ProactiveLifeDirector director;
        private int generation;
        private string originalMeaning = "";
        private AudioSource replay;
        private AudioClip lastRecording;
        public bool Recall => !exam.Active && Stage == LearningStage.Recall;
        public bool CanReplay => lastRecording != null && !Recall && !exam.Active;
        private void Awake()
        {
            client = gameObject.AddComponent<CafeClient>(); recorder = gameObject.AddComponent<CafeRecorder>();
            voice = gameObject.AddComponent<SliceVoice>(); services = new CompanionServices(client);
            replay = gameObject.AddComponent<AudioSource>();
            recorder.Finished += Transcribe;
            recorder.Failed += Error;
            recorder.Changed += Notify;
            gameObject.AddComponent<VoicePushToTalk>().Bind(() => Open, () => Recording, Record);
        }
        public void Connect() { StartCoroutine(client.Connect(error => { Status = error == "" ? "Voice connected." : "MOCK / unassessed — " + error; Notify(); })); }
        public void Begin(ProactiveLifeDirector owner)
        {
            director = owner; Open = true; Complete = false; Heard = Feedback = ""; Assessment = null;
            if (director.State.Settings.Mode == GameMode.MockExam)
            {
                exam.Begin(DateTime.UtcNow, director.Band); recorder.MaxSeconds = 120;
                Status = "Speak without interruption. Feedback follows all three answers.";
            }
            else { recorder.MaxSeconds = 20; BeginStep(); }
            SpeakPrompt(); Notify();
        }
        private void BeginStep()
        {
            practice = new IELTSPracticeManager(director.State.Learner);
            practice.Begin(director.Quest.Scenario(director.Band), director.State.Settings.Mode, DateTime.UtcNow);
            Status = practice.Stage == LearningStage.Recall ? "Answer independently; the correction is hidden."
                : practice.Stage == LearningStage.Feedback ? "Resume your correction practice." : "Answer in your own words.";
            originalMeaning = "";
        }
        public void Record()
        {
            if (!Open || Busy || Complete || !exam.Active && Stage == LearningStage.Feedback) return;
            if (Recording == "recording") { recorder.Stop(); return; }
            if (Recording == "requesting") { recorder.Cancel(); return; }
            if (!Connected) { Error("Speech and AI are offline. Reconnect; your event stays saved."); return; }
            if (!VoiceCoach.CanCapture(voice.Occupied, replay.isPlaying, Busy)) { Status = "Listen first. Speak when playback finishes."; Notify(); return; }
            voice.Cancel();
            if (exam.Active) exam.StartRecording();
            recorder.Begin(); Notify();
        }
        private void Transcribe(byte[] wave)
        {
            if (!Open) return;
            exam.StopRecording();
            var current = ++generation; Busy = true; Status = "Listening to your reply…"; Notify();
            if (lastRecording != null) Destroy(lastRecording);
            var decoded = CafeWave.Decode(wave);
            lastRecording = AudioClip.Create("Last proactive reply", decoded.Samples.Length, 1, decoded.Rate, false);
            lastRecording.SetData(decoded.Samples, 0);
            StartCoroutine(services.Transcribe(wave, text => {
                if (!Open || current != generation) return;
                Heard = text;
                if (!exam.Active && practice.Stage == LearningStage.IndependentAnswer)
                { originalMeaning = text; director.Quest.RememberFirstMeaning(text); }
                Review(text, current);
            }, error => { if (current == generation) Error(error); }));
        }
        private void Review(string text, int current)
        {
            Status = exam.Active ? "Response received. Preparing next prompt…" : "Checking meaning and language…"; Notify();
            var scenario = practice?.Scenario;
            StartCoroutine(services.Review(new SliceReviewRequest {
                band = BandProfile.For((IeltsBand)director.Band).Label, npc = director.Actor.DisplayName,
                task = exam.Active ? "Three-part speaking rehearsal" : director.Quest.Definition(director.State.Run.Id).Title,
                npcLine = Prompt, goal = exam.Active ? "Respond meaningfully to the current speaking prompt." : scenario.Goal,
                text = text, reference = !exam.Active && Stage == LearningStage.Repetition ? practice.Reference : "",
                history = NPCMemoryService.Of(director.State, director.Actor.Id).Summaries.TakeLast(3).ToArray(),
            }, result => {
                if (!Open || current != generation) return;
                Busy = false;
                var evidence = result?.Evidence(text) ?? new UtteranceEvidence { Text = text, Spoken = true };
                if (exam.Active) AcceptExam(evidence);
                else AcceptPractice(evidence);
                director.Persist(); Notify();
            }, error => { if (current == generation) Error(error); }));
        }
        private void AcceptPractice(UtteranceEvidence evidence)
        {
            var outcome = practice.Submit(evidence, DateTime.UtcNow);
            Status = outcome.Status; Assessment = outcome.Assessment; Feedback = Assessment?.Feedback ?? "";
            if (practice.Stage == LearningStage.Feedback)
            {
                practice.ShowRepetition();
                HearModel();
                Status = "Listen to the correction, then record it in your own voice. Next is locked.";
            }
            if (!EventQuestAdapter.CanAdvance(outcome, evidence))
            {
                if (outcome.Advance) { BeginStep(); Status = "Try a response that addresses this encounter. Corrections are optional in Free Life."; }
                return;
            }
            // Meaning from the first answer selects the branch; repeating a model cannot rewrite it.
            director.Quest.Advance(true, originalMeaning, director.Context(director.Actor).Location, DateTime.UtcNow);
            director.ReconcileWorld();
            if (director.State.Run == null || director.State.Run.WaitingForTransfer)
            {
                Complete = true;
                Status = director.State.Run == null ? "Encounter complete. Thanks for helping!" : "Directions saved. Meet your new acquaintance at Forest Café.";
                voice.Say(services, director.Quest.Prompt(director.Band), .95f, Error);
                return;
            }
            BeginStep(); SpeakPrompt();
        }
        private void AcceptExam(UtteranceEvidence evidence)
        {
            if (!exam.MoveNext(evidence, DateTime.UtcNow)) { Status = exam.Status; return; }
            if (exam.Complete)
            {
                var report = exam.Result(DateTime.UtcNow);
                director.State.Learner.Exams.Add(report);
                if (director.State.Learner.Exams.Count > 20) director.State.Learner.Exams.RemoveAt(0);
                Feedback = report.Feedback; Complete = true;
                Status = "Rehearsal complete. No event reward or quest step was changed by exam answers.";
            }
            else SpeakPrompt();
        }
        public void RepeatCorrection() { if (!Busy && Recording == "idle" && !exam.Active) { practice.ShowRepetition(); Status = "Say the corrected meaning naturally."; director.Persist(); Notify(); } }
        public void SpeakPrompt() { if (!Recall && Recording == "idle" && !Busy) voice.Say(services, Prompt, BandProfile.For((IeltsBand)(director?.Band ?? 5)).SpeechRate, Error); }
        public void HearModel() { if (Recording == "idle" && !Busy && !Recall && !exam.Active && Correction != "") voice.Say(services, Correction, .9f, Error); }
        public void Replay() { if (Recording != "idle" || Busy || !CanReplay) return; voice.Cancel(); replay.clip = lastRecording; replay.Play(); }
        public void Ambient(string text) { if (!Open && Connected) voice.Say(services, text, .95f, _ => { }); }
        public void Close()
        {
            generation++; Open = Busy = Complete = false; exam.Cancel(); recorder.Cancel(); voice.Cancel(); client.Cancel(); replay.Stop();
            if (lastRecording != null) { Destroy(lastRecording); lastRecording = null; }
            Notify();
        }
        private void Error(string error) { Busy = false; exam.StopRecording(); Status = "MOCK / unassessed — " + error; Notify(); }
        private void Notify() { Changed?.Invoke(); }
    }
}
