using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Life;
using ContextSpeak.Learning;
using ContextSpeak.Slice;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ContextSpeak.LivingWorld
{
    /// One persistent director; the game owns actions and existing adapters own speech/AI.
    public sealed class DynamicLifeEventManager : MonoBehaviour
    {
        public const string SaveKey = "contextspeak.living-stories.v1";
        public static DynamicLifeEventManager Instance { get; private set; }
        public BranchingQuestSystem Stories { get; private set; }
        public string Feedback { get; private set; } = "";
        public string Heard { get; private set; } = "";
        public string Explanation { get; private set; } = "";
        public string SelectedChoice { get; private set; } = "";
        public bool DialogueOpen { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool Busy { get; private set; }
        public int Band { get; private set; } = 5;
        public bool Connected => reviewer != null && reviewer.Available;
        public float InputLevel => recorder?.Level ?? 0;
        public string Recording => recorder != null ? recorder.State : "idle";
        public bool Ready => player != null && Catalog != null;
        public bool HasPhone => slice != null;
        public LivingWorldCatalog Catalog { get; private set; }
        public GeneratedScenario Scenario => Stories.Active ? ScenarioGenerator.Generate(Stories.Current, Stories.Step, Band) : null;
        public event Action Changed;
        private LearnerMemory learner = new LearnerMemory();
        private IELTSPracticeManager practice;
        public AssessmentRecord Assessment { get; private set; }
        public bool Recall => practice?.Stage == LearningStage.Recall;
        public string Correction => practice?.VisibleCorrection ?? Stories.State.Run.Repair;
        public string[] Hints => VoiceCoach.Hints(CoachScenario(), learner, practice?.Stage ?? LearningStage.IndependentAnswer);

        private CafePlayer player;
        private SliceGame slice;
        private CityGame city;
        private CafeClient client;
        private CafeRecorder recorder;
        private SliceVoice voice;
        private CompanionServices services;
        private IConversationReviewer reviewer;
        private LivingWorldInterface view;
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<(VisualElement Root, StyleEnum<DisplayStyle> Display)> hiddenHud =
            new List<(VisualElement, StyleEnum<DisplayStyle>)>();
        private StoryLocationAnchor[] anchors = Array.Empty<StoryLocationAnchor>();
        private bool oldBlocked, oldModal, oldSuspended, ownsControls;
        private int generation;
        private float nextOffer, clockRemainder;
        private ParticleSystem practiceRain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("ContextSpeak Living World").AddComponent<DynamicLifeEventManager>();
        }
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Catalog = Resources.Load<LivingWorldCatalog>("LivingWorld/Catalog");
            var events = Catalog != null && ScenarioGenerator.Valid(Catalog.Events) ? Catalog.Events : LivingWorldTemplates.All();
            LivingWorldState saved = null;
            try { saved = JsonUtility.FromJson<LivingWorldState>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { }
            if (saved == null && CharacterProfiles.TryLoad(out var profile))
                saved = new LivingWorldState { Band = profile.Band };
            Stories = new BranchingQuestSystem(saved, events);
            Band = Stories.State.Band;
            try { learner = LearningMemoryService.Sanitize(JsonUtility.FromJson<LearnerMemory>(Stories.State.VoiceLearningJson)); }
            catch (ArgumentException) { learner = new LearnerMemory(); }
            client = gameObject.AddComponent<CafeClient>();
            recorder = gameObject.AddComponent<CafeRecorder>();
            voice = gameObject.AddComponent<SliceVoice>();
            services = new CompanionServices(client);
            reviewer = services;
            view = gameObject.AddComponent<LivingWorldInterface>();
            view.Bind(this);
            recorder.Finished += Transcribe;
            recorder.Failed += SetFeedback;
            recorder.Changed += Notify;
            gameObject.AddComponent<VoicePushToTalk>().Bind(() => DialogueOpen, () => Recording, Record);
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            Connect();
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == SceneManager.GetActiveScene()) BindScene(scene);
        }
        private void ActiveSceneChanged(Scene previous, Scene next) => BindScene(next);
        private static T InScene<T>(Scene scene) where T : Component => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault() : null;
        private void BindScene(Scene scene)
        {
            CancelTurn(); ReleaseControls(); DialogueOpen = JournalOpen = false;
            player = InScene<CafePlayer>(scene);
            slice = InScene<SliceGame>(scene); city = InScene<CityGame>(scene);
            anchors = FindObjectsByType<StoryLocationAnchor>(FindObjectsSortMode.None).Where(anchor => anchor.gameObject.scene == scene).ToArray();
            InstallActors(scene.name);
            if (practiceRain != null)
            {
                if (SkySystem.Active != null || !Stories.State.Raining) practiceRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else practiceRain.Play();
            }
            nextOffer = Time.unscaledTime + 8;
            Notify();
        }
        private void InstallActors(string scene)
        {
            if (Catalog == null || player == null) return;
            foreach (var venue in Catalog.Venues.Where(item => item.Scene == scene && !anchors.Any(anchor => anchor.Id == item.Id)))
            {
                var prefab = Resources.Load<GameObject>("LivingWorld/Actors/" + venue.Model);
                if (prefab == null) continue;
                var point = venue.Position;
                if (UnityEngine.AI.NavMesh.SamplePosition(point, out var ground, 4, UnityEngine.AI.NavMesh.AllAreas)) point = ground.position;
                var actor = Instantiate(prefab, point, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(actor, SceneManager.GetActiveScene());
                actor.name = venue.Title + " · Story NPC";
                var anchor = actor.AddComponent<StoryLocationAnchor>(); anchor.Id = venue.Id; anchor.Npc = actor.transform;
                if (UnityEngine.AI.NavMesh.SamplePosition(point, out _, .3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    var agent = actor.AddComponent<UnityEngine.AI.NavMeshAgent>(); agent.height = 1.7f; agent.radius = .25f;
                    var npc = actor.AddComponent<NPCController>(); npc.DisplayName = venue.Title;
                    npc.Schedule = new[] { point }; // Keep the service post; full schedules can be authored later.
                }
                var label = new GameObject("Story invitation").AddComponent<TextMesh>();
                label.transform.SetParent(actor.transform, false); label.transform.localPosition = Vector3.up * 2;
                label.text = "✦ " + venue.Title; label.fontSize = 40; label.characterSize = .035f;
                label.anchor = TextAnchor.MiddleCenter; label.color = new Color(.95f, .85f, .56f);
            }
            anchors = FindObjectsByType<StoryLocationAnchor>(FindObjectsSortMode.None).Where(anchor => anchor.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
        }
        private bool CanInvite => player != null && !player.Blocked && CharacterProfiles.Exists
            && (slice == null || slice.CanStartStory) && (city == null || city.CanStartStory)
            && (ScenarioManager.Instance == null || !ScenarioManager.Instance.Active)
            && (InteractionManager.Instance == null || !InteractionManager.Instance.Modal) && !LifeUI.PanelOpen
            && !FindObjectsByType<LocationFlow>(FindObjectsSortMode.None).Any(flow => flow.Busy);

        private void Update()
        {
            if (Stories == null || player == null) return;
            if (practiceRain != null) practiceRain.transform.position = player.transform.position + Vector3.up * 10;
            if ((DialogueOpen || JournalOpen) && player.Controls != null && player.Controls.Cancel) { Close(); return; }
            if (!DialogueOpen && !JournalOpen)
            {
                clockRemainder += Time.deltaTime;
                if (clockRemainder >= 1)
                {
                    Stories.State.Minute++;
                    clockRemainder -= 1;
                    if (Stories.State.Minute >= 1440) { Stories.State.Minute = 0; Stories.State.Day++; }
                }
                if (Time.unscaledTime >= nextOffer && CanInvite && ContextSpeak.Proactive.ProactiveLifeDirector.Instance == null)
                {
                    nextOffer = Time.unscaledTime + 2;
                    var anchor = anchors.Where(item => item != null && Vector3.Distance(player.transform.position, item.transform.position) < 4.5f)
                        .OrderBy(item => Vector3.Distance(player.transform.position, item.transform.position)).FirstOrDefault();
                    if (anchor != null)
                    {
                        var offer = Stories.Offer(Context(anchor.Id));
                        if (offer != null) Begin(offer.Id);
                    }
                }
            }
            foreach (var anchor in anchors.Where(item => item != null))
            {
                var label = anchor.GetComponentInChildren<TextMesh>();
                if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
            }
        }
        public StoryContext Context(string location) => new StoryContext
        {
            Location = location, Day = slice != null && slice.Save != null ? slice.Save.Clock.Day : Stories.State.Day,
            Minute = slice != null && slice.Save != null ? slice.Save.Clock.MinuteOfDay
                : SkySystem.Active != null ? Mathf.FloorToInt(SkyState.Hour * 60) : Stories.State.Minute,
            Band = Band, Raining = slice != null && slice.Save != null ? slice.Save.Clock.Weather == Weather.Rain : SkySystem.Active != null ? SkyState.Raining : Stories.State.Raining,
        };
        public void Connect()
        {
            StartCoroutine(client.Connect(error => SetFeedback(error == "" ? "AI speech and answer feedback connected." : error)));
        }
        public void SetBand(int band) { if (DialogueOpen || Busy) return; Band = Mathf.Clamp(band, 3, 9); Stories.State.Band = Band; Persist(); Notify(); }
        public void OpenJournal()
        {
            if (DialogueOpen) return;
            if (!CharacterProfiles.Exists) { CharacterCreationController.Open("ForestSlice"); return; }
            if (!CanInvite) return;
            HoldControls(); JournalOpen = true; Notify();
        }
        public bool Begin(string id)
        {
            if (Busy || DialogueOpen) return false;
            var item = Stories.Templates.FirstOrDefault(template => template.Id == id);
            var anchor = item == null ? null : anchors.FirstOrDefault(point => point != null && point.Id == item.Location);
            if (anchor == null || player == null || Vector3.Distance(player.transform.position, anchor.transform.position) > 5.5f)
            { SetFeedback("Walk to this NPC first, or track the story destination."); return false; }
            if (!JournalOpen && !CanInvite) return false;
            if (!Stories.Begin(id, Context(item.Location))) { SetFeedback("This story is not available yet. Check its time and earlier choices."); return false; }
            if (!JournalOpen) HoldControls();
            JournalOpen = false; DialogueOpen = true;
            var npc = anchor.Npc != null ? anchor.Npc.GetComponent<NPCController>() : null;
            if (npc != null) npc.Talking = true;
            player.ConversationPartner = anchor.Npc; player.Activity = PlayerAnimState.Talk;
            SelectedChoice = !string.IsNullOrEmpty(Stories.State.Run.VoiceChoice) ? Stories.State.Run.VoiceChoice : Stories.State.Run.RepairChoice ?? "";
            practice = null; Assessment = null;
            if (SelectedChoice != "") BeginCoaching();
            Feedback = Heard = Explanation = "";
            SpeakPrompt(); Persist(); Notify(); return true;
        }
        public void Choose(string id)
        {
            if (practice != null && (practice.Stage == LearningStage.Repetition || practice.Stage == LearningStage.Recall)) return;
            if (!DialogueOpen || !Stories.Active || Busy || Recording == "recording" || Recording == "requesting" || Stories.State.Run.Repair != "") return;
            if (!Stories.Current.Steps[Stories.Step].Choices.Any(choice => choice.Id == id)) return;
            SelectedChoice = id; Stories.State.Run.VoiceChoice = id; BeginCoaching(); generation++; Persist(); Notify();
        }
        public void Record()
        {
            if (!DialogueOpen || !Stories.Active || Busy) return;
            if (Recording == "recording") { recorder.Stop(); return; }
            if (Recording == "requesting") { recorder.Cancel(); return; }
            if (SelectedChoice == "") { SetFeedback("Choose what you want to communicate, then answer in your own words."); return; }
            if (!Connected) { SetFeedback("Start the local speech companion and reconnect. Your story is saved."); return; }
            if (!VoiceCoach.CanCapture(voice.Occupied, false, Busy)) { SetFeedback("Listen first. Speak when playback finishes."); return; }
            voice.Cancel(); recorder.Begin();
        }
        private void Transcribe(byte[] wave)
        {
            if (!DialogueOpen) return;
            var current = ++generation; Busy = true; SetFeedback("Listening to your reply…");
            StartCoroutine(services.Transcribe(wave, text => {
                if (current != generation || !DialogueOpen) return;
                Busy = false; Submit(text, true);
            }, error => { if (current == generation) { Busy = false; SetFeedback(error); } }));
        }
        public void Rehearse(string text) => Submit(text, false);
        private void Submit(string text, bool spoken)
        {
            if (!DialogueOpen || !Stories.Active || Busy || SelectedChoice == "" || string.IsNullOrWhiteSpace(text) || text.Length > 400) return;
            var choice = Stories.Current.Steps[Stories.Step].Choices.FirstOrDefault(item => item.Id == SelectedChoice);
            if (choice == null) return;
            if (!Connected) { SetFeedback("Answer assessment is offline. Reconnect; no story progress has changed."); return; }
            var current = ++generation; var ticket = Stories.Ticket; var selected = SelectedChoice;
            var scenario = Scenario; var repair = Correction;
            var goal = scenario.Goal + " " + choice.Goal;
            if (repair != "") goal = "Give a grammatical, relevant version of this reply: " + repair;
            if (goal.Length > 240) goal = goal.Substring(0, 240);
            Heard = text; Busy = true; SetFeedback("Checking meaning and wording…"); voice.Cancel();
            StartCoroutine(reviewer.Review(new SliceReviewRequest
            {
                band = BandProfile.For((IeltsBand)Band).Label, npc = Stories.Current.NpcName, task = Stories.Current.Title,
                goal = goal, npcLine = scenario.Prompt, text = text, reference = practice?.Stage == LearningStage.Repetition ? practice.Reference : "",
            }, result => {
                if (current != generation || !DialogueOpen || ticket != Stories.Ticket) return;
                Busy = false;
                var assessed = new StoryAssessment { Assessed = result != null && result.assessedGoal,
                    Relevant = result != null && result.relevant, GoalMet = result != null && result.goalMet,
                    Correction = result?.correction ?? "", Explanation = result?.explanation ?? "" };
                if (spoken)
                {
                    if (practice == null) BeginCoaching();
                    var outcomeLearning = practice.Submit(result?.Evidence(text) ?? new UtteranceEvidence { Text = text, Spoken = true }, DateTime.UtcNow);
                    Assessment = outcomeLearning.Assessment;
                    Feedback = outcomeLearning.Status;
                    Explanation = Assessment?.Grammar ?? "";
                    if (!outcomeLearning.Advance)
                    {
                        if (practice.Stage == LearningStage.Feedback) { practice.ShowRepetition(); HearCorrection(); }
                        Persist(); Notify(); return;
                    }
                }
                var outcome = Stories.Answer(ticket, selected, text, spoken, assessed);
                Feedback = outcome.Feedback; Explanation = assessed.Explanation;
                if (!spoken && assessed.Correction != "") Explanation = assessed.Correction + "\n" + assessed.Explanation;
                if (outcome.Status == StoryStatus.Advanced || outcome.Status == StoryStatus.Completed)
                {
                    var actor = anchors.FirstOrDefault(point => point != null && point.Id == Stories.Current.Location);
                    actor?.GetComponentInChildren<AuthoredMotion>()?.Preview("Agree_Gesture");
                }
                if (outcome.Status == StoryStatus.Advanced) { SelectedChoice = ""; Stories.State.Run.VoiceChoice = ""; practice = null; SpeakPrompt(); }
                else if (outcome.Status == StoryStatus.Repeat) HearCorrection();
                else if (outcome.Status == StoryStatus.Completed) voice.Say(services, "Thanks! " + outcome.Feedback, 1, SetFeedback);
                Persist(); Notify();
            }, error => { if (current == generation) { Busy = false; SetFeedback("Answer check unavailable. Progress is kept. " + error); } }));
        }
        public void SpeakPrompt()
        {
            if (!Recall && Recording == "idle" && !Busy && Scenario != null) voice.Say(services, Scenario.Prompt, Band <= 4 ? .85f : 1, SetFeedback);
        }
        public void HearCorrection()
        {
            var target = Correction;
            if (!Recall && Recording == "idle" && !Busy && target != "") voice.Say(services, target, .85f, SetFeedback);
        }
        public void Close()
        {
            if (DialogueOpen && Stories.Current != null) Stories.Defer(Context(Stories.Current.Location));
            CancelTurn(); ReleaseControls(); DialogueOpen = JournalOpen = false; Persist(); Notify();
        }
        private void CancelTurn() { generation++; Busy = false; client?.Cancel(); recorder?.Cancel(); voice?.Cancel(); }
        private void HoldControls()
        {
            if (player == null || ownsControls) return;
            ownsControls = true;
            oldBlocked = player.Blocked; oldSuspended = player.Controls != null && player.Controls.Suspended;
            player.Route.Clear(); player.Blocked = true; player.Controls?.Clear();
            if (player.Controls != null) player.Controls.Suspended = true;
            oldModal = InteractionManager.Instance != null && InteractionManager.Instance.Modal;
            if (InteractionManager.Instance != null) InteractionManager.Instance.Modal = true;
            foreach (var host in new Behaviour[] { slice, city, LifeUI.Instance }.Concat(FindObjectsByType<LocationFlow>(FindObjectsSortMode.None)))
                if (host != null && host.enabled) { suspended.Add(host); host.enabled = false; }
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document.gameObject == gameObject || document.gameObject.scene != player.gameObject.scene) continue;
                var root = document.rootVisualElement;
                if (root == null) continue;
                hiddenHud.Add((root, root.style.display));
                root.style.display = DisplayStyle.None;
            }
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
        }
        private void ReleaseControls()
        {
            if (!ownsControls) return;
            ownsControls = false;
            foreach (var host in suspended.Where(host => host != null)) host.enabled = true;
            suspended.Clear();
            foreach (var hud in hiddenHud) hud.Root.style.display = hud.Display;
            hiddenHud.Clear();
            if (player != null)
            {
                var npc = player.ConversationPartner != null ? player.ConversationPartner.GetComponent<NPCController>() : null;
                if (npc != null) npc.Talking = false;
                player.Blocked = oldBlocked; player.Activity = PlayerAnimState.Idle; player.ConversationPartner = null;
                player.Controls?.Clear(); if (player.Controls != null) player.Controls.Suspended = oldSuspended;
            }
            if (InteractionManager.Instance != null) InteractionManager.Instance.Modal = oldModal;
        }
        public void Track(string id)
        {
            var item = Stories.Templates.FirstOrDefault(template => template.Id == id);
            var venue = item == null || Catalog == null ? null : Catalog.Venues.FirstOrDefault(point => point.Id == item.Location);
            if (venue == null) { SetFeedback("This destination is not installed in this build."); return; }
            if (!DialogueOpen && !JournalOpen) return;
            Close();
            if (venue.Scene != SceneManager.GetActiveScene().name)
            {
                Travel(venue.Scene);
                return;
            }
            var anchor = anchors.FirstOrDefault(point => point != null && point.Id == venue.Id);
            if (anchor != null)
            {
                var destination = anchor.transform.position + Vector3.back * 2.5f;
                var path = new UnityEngine.AI.NavMeshPath();
                if (UnityEngine.AI.NavMesh.CalculatePath(player.transform.position, destination, UnityEngine.AI.NavMesh.AllAreas, path)
                    && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    foreach (var corner in path.corners.Skip(1)) player.Route.Enqueue(corner);
                SetFeedback("Head to " + venue.Title + ". The NPC will approach you when you are nearby.");
            }
        }
        public void WaitForEvening()
        {
            if (!JournalOpen) return;
            Stories.State.Minute = 18 * 60 + 5;
            if (slice != null && slice.Save != null) slice.Save.Clock.Minute = Stories.State.Minute;
            SkySystem.Active?.SetTime(18.1f);
            Persist(); Notify();
        }
        public void ReturnToCafe()
        {
            if (Busy || Recording == "recording" || Recording == "requesting") return;
            Close();
            Travel("ForestSlice");
        }
        private void Travel(string scene)
        {
            // The café is intentionally Addressable, so it is absent from ordinary build scenes.
            if (scene == "ForestSlice") { WorldScenes.Open(scene, TravelFailed); return; }
            if (!SceneTransitionManager.Enter(scene, "living-story", player != null ? player.transform : null))
                TravelFailed(SceneTransitionManager.LastError);
        }
        private void TravelFailed(string error)
        {
            if (CanInvite) { HoldControls(); JournalOpen = true; }
            SetFeedback(error + " Your story is saved. Retry tracking the destination.");
        }
        public void PreviewRain()
        {
            if (!JournalOpen) return;
            Stories.State.Raining = true;
            if (slice != null && slice.Save != null) slice.Save.Clock.Weather = Weather.Rain;
            if (SkySystem.Active != null) SkySystem.Active.SetWeather(SkyWeather.Rain, 1);
            else StartPracticeRain();
            Persist();
            SetFeedback("Rainy-day practice selected. Visit Sam at the camping ground.");
        }
        private void StartPracticeRain()
        {
            if (practiceRain == null)
            {
                var cloud = new GameObject("Rainy-day practice");
                cloud.transform.SetParent(transform, false);
                cloud.transform.position = player.transform.position + Vector3.up * 10;
                practiceRain = cloud.AddComponent<ParticleSystem>();
                var main = practiceRain.main; main.startSpeed = 12; main.startLifetime = 1.1f; main.startSize = .05f;
                main.maxParticles = 300; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = practiceRain.emission; emission.rateOverTime = 180;
                var shape = practiceRain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24, 1, 24);
                cloud.transform.rotation = Quaternion.Euler(90, 0, 0);
                var renderer = cloud.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2; renderer.velocityScale = .04f;
                renderer.sharedMaterial = Resources.Load<Material>("LivingWorld/Rain");
            }
            practiceRain.Play();
        }
        public void SetFeedback(string text) { Feedback = text; Notify(); }
        private void Notify() => Changed?.Invoke();
        private void Persist()
        {
            Stories.State.VoiceLearningJson = JsonUtility.ToJson(learner);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Stories.State)); PlayerPrefs.Save();
        }
        private PracticeScenario CoachScenario()
        {
            var scenario = Scenario;
            if (scenario == null) return null;
            var choice = Stories.Current.Steps[Stories.Step].Choices.FirstOrDefault(item => item.Id == SelectedChoice);
            return new PracticeScenario { Id = "story:" + Stories.Current.Id + ":" + Stories.Step + ":" + SelectedChoice,
                ItemId = "story:" + Stories.Current.Id + ":" + Stories.Step, Topic = Stories.Current.Title,
                Npc = Stories.Current.NpcName, Prompt = scenario.Prompt, Goal = scenario.Goal + " " + choice?.Goal,
                Model = Stories.State.Run.Repair != "" ? Stories.State.Run.Repair : "",
                Vocabulary = scenario.Vocabulary, Band = Band };
        }
        private void BeginCoaching()
        {
            practice = new IELTSPracticeManager(learner);
            practice.Begin(CoachScenario(), GameMode.GuidedLearning, DateTime.UtcNow);
        }
        private void OnApplicationPause(bool paused) { if (paused && Stories != null) Persist(); }
        private void OnApplicationQuit() { if (Stories != null) Persist(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            Close(); SceneManager.sceneLoaded -= SceneLoaded; SceneManager.activeSceneChanged -= ActiveSceneChanged; Instance = null;
        }
    }
}
