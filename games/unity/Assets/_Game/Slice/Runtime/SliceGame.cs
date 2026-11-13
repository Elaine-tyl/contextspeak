using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Slice
{
    public enum Partner { None, Rowan, Hazel, Resident }

    [Serializable]
    public sealed class ChatLine { public string Speaker, Text; public bool Player; }

    /// Forest Café vertical slice runtime: talking to the waiter, ordering, paying, kitchen and delivery,
    /// seating, eating and drinking, fishing with an instructor, quests, progress and saving.
    public sealed class SliceGame : MonoBehaviour
    {
        [Header("Scene references (set by the scene builder)")]
        public CafePlayer Player;
        public Transform Rowan, Hazel, Counter;
        public WaiterNpc Waiter;
        public Seat[] Seats = new Seat[0];
        public FishingSpot Pier;
        public Atmosphere Atmosphere;
        public GameObject[] DrinkProps = new GameObject[0], FoodProps = new GameObject[0];
        public Transform PlayerHand;
        [Tooltip("Talk / sit / fish range in metres.")]
        public float TalkRange = 3f, SeatRange = 1.6f;
        [Tooltip("Across-counter conversation range; the navigation agent stands behind the service furniture.")]
        public float CounterTalkRange = 4f;

        public SliceSave Save { get; private set; }
        public Partner Talking { get; private set; }
        public string Toast { get; private set; } = "";

        private const string FileName = "forest-cafe-slice.json";
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_State(string json);
        [DllImport("__Internal")] private static extern void CS_SyncSave(string owner, int generation);
#endif
        private CafeClient client;
        private CafeRecorder recorder;
        private WorldScenery scenery;
        private string sceneryFocus = "";
        private string lastInspected = "";
        private SliceVoice voice;
        private RoomClient room;
        private SliceInterface ui;
        private CompanionServices companion;
        private IPronunciationAssessor pronunciation = new NoPronunciationAssessor();
        private FishingGuide guide;
        private FishingSession fishing;
        private readonly List<ChatLine> history = new List<ChatLine>();
        private readonly List<GameObject> servedProps = new List<GameObject>();
        private readonly System.Random random = new System.Random();
        private string npcLine = "", recognized = "", status = "", aiTip = "";
        private SpeakingResult lastReview;
        private Gate gate = Gate.Continue;
        private string practiceTarget = "", modelAnswer = "";
        private WaiterProposal pendingOrder;
        private WaiterTurn pendingTurn;
        private LearningNpc[] residents = new LearningNpc[0];
        private LearningNpc resident;
        private SceneLesson lesson;
        private LessonAnswer lessonAnswer;
        private SliceReviewReply semanticReview;
        private string correctionQuestion = "";
        private int attempts, epoch;
        private bool working, menuOpen, quitting;
        private float fishingTime, toastUntil, nextSave, nextRender;
        private Seat seat;
        private string panel = ""; // "", inventory, journal, map, pause
        private string menuCategory = "Coffee", selectedItem = "";
        private PlayerAnimState activity = PlayerAnimState.Idle;
        private float activityTimer;
        private int saveVersion, syncingVersion;
        private bool syncing, notifySave;
        private string saveState = "";

        private BandProfile Profile => BandProfile.For(Save.Band);
        private bool SpeechAvailable => companion != null && companion.Available;
        public Vector3 OrderArrival => Counter.position - Counter.right * 1.8f + Counter.forward * 2.7f + Vector3.up * 0.1f;
        private string Speaker => Talking == Partner.Rowan ? "Rowan" : Talking == Partner.Hazel ? "Hazel" : Talking == Partner.Resident && resident != null ? resident.DisplayName : "";

        private Interactable[] interactables = new Interactable[0];
        private BuildingSkin[] buildings = new BuildingSkin[0];

        private void Start()
        {
            interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            buildings = FindObjectsByType<BuildingSkin>(FindObjectsSortMode.None);
            residents = FindObjectsByType<LearningNpc>(FindObjectsSortMode.None);
            Save = Load();
            client = gameObject.AddComponent<CafeClient>();
            recorder = gameObject.AddComponent<CafeRecorder>();
            scenery = FindFirstObjectByType<WorldScenery>();
            voice = gameObject.AddComponent<SliceVoice>();
            ui = gameObject.AddComponent<SliceInterface>();
            room = gameObject.AddComponent<RoomClient>();
            room.Player = Player;
            room.Capture = () => {
                var position = Player.transform.position;
                return new RoomPose {
                    x = position.x, y = position.y, z = position.z, yaw = Player.Model.eulerAngles.y,
                    moving = Player.Speed > 0.15f,
                    activity = recorder.State == "recording" ? "recording" : Talking == Partner.None ? "exploring" : "talking",
                    npc = Speaker,
                };
            };
            companion = new CompanionServices(client);
            guide = new FishingGuide { Band = Save.Band };
            fishing = new FishingSession(Environment.TickCount);
            Save.Waiter.Band = Save.Band;
            ui.Command += Command;
            recorder.Finished += Transcribe;
            recorder.Failed += error => Status(error);
            recorder.Changed += Render;
            if (Player.GetComponent<FootstepAudio>() == null) Player.gameObject.AddComponent<FootstepAudio>();
            Player.MouseSensitivity = Save.MouseSensitivity;
            if (Save.PlayerY != 0)
                Player.Place(WorldScenery.SafeSpawn(new Vector3(Save.PlayerX, Save.PlayerY, Save.PlayerZ), Player.transform.position));
            // Orders that were paid before quitting are still being prepared after loading.
            if (Save.Cart.Status == OrderStatus.Delivered) ServeProps();
            Connect();
            ShowToast("Welcome to Forest Café · " + Profile.Label + " · F talk · E use · J journal · Esc settings");
        }

        private void Connect()
        {
            StartCoroutine(client.Connect(error => {
                status = error == "" ? "Voice and AI feedback connected." : "Offline mode: type your replies. Start pnpm world:dev for voice.";
                Render();
            }));
        }

        // ------------------------------------------------------------------ main loop
        private void Update()
        {
            var dt = Time.deltaTime;
            Save.Clock.Tick(dt, random);
            if (Atmosphere != null) Atmosphere.Apply(Save.Clock);
            Needs(dt);
            Kitchen(dt);
            Fish(dt);
            Activity(dt);
            HandleInput();
            if (Time.unscaledTime > nextSave) { nextSave = Time.unscaledTime + 30; Persist(); }
            if (Time.unscaledTime > nextRender) { nextRender = Time.unscaledTime + 0.1f; Render(); }
        }

        private void HandleInput()
        {
            var controls = Player.Controls;
            var typing = ui != null && ui.Typing;
            controls.Suspended = typing || panel != "" || menuOpen && Talking == Partner.None;
            var exploring = Talking == Partner.None && panel == "" && !menuOpen;
            controls.AlwaysLook = exploring && ui != null && ui.AlwaysLook && seat == null;
            Cursor.lockState = controls.AlwaysLook ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !controls.AlwaysLook;
            if (typing) return;

            if (controls.Cancel)
            {
                if (panel != "") panel = "";
                else if (menuOpen) menuOpen = false;
                else if (Talking != Partner.None) EndConversation();
                else if (fishing.State != FishingState.Idle) StopFishing();
                else panel = "pause";
                Render();
                return;
            }
            if (controls.Inventory) { panel = panel == "inventory" ? "" : "inventory"; Render(); }
            if (controls.Journal) { panel = panel == "journal" ? "" : "journal"; Render(); }
            if (controls.Menu && Talking == Partner.None) { panel = panel == "map" ? "" : "map"; Render(); }
            if (panel != "") return;

            if (controls.Talk && Talking == Partner.None && !working)
            {
                var near = NearestPartner();
                if (near != Partner.None) BeginConversation(near);
            }
            if (controls.Interact && Talking == Partner.None) Use();
            if (fishing.State == FishingState.Bite && controls.PrimaryPressed) fishing.Strike();
        }

        private Partner NearestPartner()
        {
            var position = Player.transform.position;
            var rowan = Rowan != null ? Vector3.Distance(position, Rowan.position) : float.PositiveInfinity;
            var hazel = Hazel != null ? Vector3.Distance(position, Hazel.position) : float.PositiveInfinity;
            if (rowan > CounterTalkRange) rowan = float.PositiveInfinity;
            if (hazel > TalkRange) hazel = float.PositiveInfinity;
            resident = residents.OrderBy(npc => Vector3.Distance(position, npc.transform.position)).FirstOrDefault();
            var other = resident != null ? Vector3.Distance(position, resident.transform.position) : 99;
            if (other < Mathf.Min(rowan, hazel) && other <= TalkRange) return Partner.Resident;
            if (float.IsPositiveInfinity(rowan) && float.IsPositiveInfinity(hazel)) return Partner.None;
            return rowan <= hazel ? Partner.Rowan : Partner.Hazel;
        }

        private Seat NearestSeat() => Seats.Where(candidate => candidate != null && (candidate.Free || candidate.Occupant == this))
            .OrderBy(candidate => Vector3.Distance(candidate.transform.position, Player.transform.position))
            .FirstOrDefault(candidate => Vector3.Distance(candidate.transform.position, Player.transform.position) < SeatRange);

        private Interactable NearestInteractable()
        {
            var nearest = Interactable.Nearest(interactables, Player.transform.position, sceneryFocus);
            if (nearest == null || nearest.Id != sceneryFocus) sceneryFocus = "";
            return nearest;
        }

        private bool AtPier => Pier != null && Vector3.Distance(Player.transform.position, Pier.Stand.position) < Pier.Range;

        /// E: sit / stand, eat or drink, fish (cast / strike).
        private void Use()
        {
            if (fishing.State != FishingState.Idle) { if (fishing.State == FishingState.Caught || fishing.State == FishingState.Escaped) fishing.Cast(); else fishing.Strike(); return; }
            if (seat != null)
            {
                if (servedProps.Count > 0 && ServedSeat != null && ServedSeat.Table == seat.Table) { Consume(); return; }
                Stand();
                if (servedProps.Count > 0) ShowToast("Your order is waiting on table " + Save.Cart.Table + ".");
                return;
            }
            if (AtPier) { StartFishing(); return; }
            var thing = NearestInteractable();
            if (thing != null && thing.Id.StartsWith("building:")) { Visit(thing.Id.Substring(9)); return; }
            if (thing != null && thing.Id.StartsWith("exit:")) { Visit("building:" + thing.Id.Substring(5)); return; }
            if (thing != null && (thing.Id == "wardrobe" || thing.Id == "fitting-room")) { EditCharacter(); return; }
            if (thing != null && thing.Id.StartsWith("World"))
            {
                lastInspected = thing.Id;
                var item = scenery?.Entries.FirstOrDefault(entry => entry.id == thing.Id);
                ShowToast(item == null ? thing.Prompt : item.Label + " · " + item.purpose);
                return;
            }
            if (thing != null && thing.Id == "timetable") { panel = "school"; Render(); return; }
            if (thing != null && thing.Id == "speaking-board") { panel = "map"; Render(); return; }
            if (thing != null && thing.Id == "menu-board")
            {
                menuOpen = true;
                ShowToast("Browse the menu, then press F near Rowan and order out loud.");
                return;
            }
            var near = NearestSeat();
            if (near != null) Sit(near);
        }

        private string Hint()
        {
            if (Talking != Partner.None || panel != "") return "";
            if (fishing.State == FishingState.Bite) return "Click or press E · Strike!";
            if (fishing.State == FishingState.Reeling) return "Hold left mouse to reel · release when the line is red";
            if (fishing.State != FishingState.Idle) return "E · " + (fishing.State == FishingState.Caught || fishing.State == FishingState.Escaped ? "Cast again" : "Strike") + " · Esc · Put the rod away";
            if (seat != null) return servedProps.Count > 0 && ServedSeat != null && ServedSeat.Table == seat.Table ? "E · " + (servedProps[0].name.StartsWith("Food") ? "Eat" : "Drink") : "E · Stand up";
            var hints = new List<string>();
            var partner = NearestPartner();
            if (partner == Partner.Rowan) hints.Add("F · Talk to Rowan (waiter)");
            if (partner == Partner.Hazel) hints.Add("F · Talk to Hazel (fishing instructor)");
            if (partner == Partner.Resident && resident != null) hints.Add("F · Talk to " + resident.DisplayName + " · " + resident.Place.Name);
            if (AtPier) hints.Add("E · Fish");
            else
            {
                var thing = NearestInteractable();
                if (thing != null) hints.Add("E · " + thing.Prompt);
                else if (NearestSeat() != null) hints.Add("E · Sit");
            }
            return string.Join("   ", hints);
        }

        // ------------------------------------------------------------------ conversation
        private void BeginConversation(Partner partner)
        {
            StopFishing();
            Talking = partner;
            epoch++;
            attempts = 0;
            gate = Gate.Continue;
            practiceTarget = modelAnswer = recognized = aiTip = "";
            pendingOrder = null;
            pendingTurn = null;
            lessonAnswer = null;
            lastReview = null;
            correctionQuestion = "";
            Player.Route.Clear();
            Player.Blocked = true;
            Player.Activity = PlayerAnimState.Talk;
            Player.ConversationPartner = partner == Partner.Rowan ? Rowan : partner == Partner.Hazel ? Hazel : resident.transform;
            if (partner == Partner.Resident)
            {
                lesson = Save.Lessons.FirstOrDefault(item => item.SceneId == resident.SceneId && item.Band == Save.Band);
                if (lesson == null)
                {
                    lesson = new SceneLesson { SceneId = resident.SceneId, Band = Save.Band };
                    Save.Lessons.Add(lesson);
                }
                Say(Speaker, lesson.Prompt);
                Render();
                return;
            }
            if (partner == Partner.Rowan)
            {
                Waiter?.Talk(true);
                var line = Save.Waiter.Stage == WaiterStage.Preparing || Save.Waiter.Stage == WaiterStage.Served
                    ? (Save.Band <= IeltsBand.Band5 ? "Hi again! Your order is coming." : "Hey again! Your order's on its way — anything else I can help with?")
                    : Save.Waiter.Greeting();
                Say("Rowan", line);
            }
            else Say("Hazel", guide.Greeting(Save.Catches.Count));
            Render();
        }

        private void EndConversation()
        {
            epoch++;
            voice.Cancel();
            client.Cancel();
            recorder.Cancel();
            if (Talking == Partner.Rowan) Waiter?.Talk(false);
            Talking = Partner.None;
            working = false;
            pendingOrder = null;
            pendingTurn = null;
            lessonAnswer = null;
            practiceTarget = "";
            menuOpen = false;
            Player.Blocked = seat != null; // still sitting after the chat
            Player.Activity = seat != null ? PlayerAnimState.Sit : PlayerAnimState.Idle;
            Player.ConversationPartner = null;
            Player.Controls.Clear();
            Persist();
        }

        private void Say(string speaker, string line)
        {
            npcLine = line;
            history.Add(new ChatLine { Speaker = speaker, Text = line });
            if (history.Count > 60) history.RemoveAt(0);
            voice.Say(companion, line, Profile.SpeechRate, error => Status("Voice unavailable — read the subtitles. " + error));
        }

        /// A learner turn. Spoken turns drive the conversation; while voice is available, typed turns are
        /// rehearsals that get feedback but do not move the task (the game is about speaking).
        /// uiAction marks menu/checkout buttons and "use model answer": they progress but earn no objective.
        public void Submit(string text, bool spoken, bool uiAction = false, double? pronunciationScore = null)
        {
            text = (text ?? "").Trim();
            if (Talking == Partner.None || working || text.Length == 0 || text.Length > 400) return;
            if (Talking != Partner.Resident || !SpeechAvailable || !spoken || uiAction)
            {
                SubmitTurn(text, spoken, uiAction, pronunciationScore);
                return;
            }
            var current = epoch;
            var step = lesson.Step;
            working = true;
            recognized = text;
            status = "Checking your answer…";
            voice.Cancel();
            var request = LessonRequest(text);
            StartCoroutine(companion.Review(request, result => {
                if (current != epoch || step != lesson.Step) return;
                working = false;
                if (result == null || !result.assessedGoal)
                {
                    Status("The AI did not assess this question. Please retry; your progress is unchanged.");
                    return;
                }
                semanticReview = result;
                status = "";
                SubmitTurn(text, spoken, uiAction, pronunciationScore);
                semanticReview = null;
            }, error => {
                if (current != epoch) return;
                working = false;
                Status("Answer check unavailable. Please retry; your progress is unchanged. " + error);
            }));
            Render();
        }

        private SliceReviewRequest LessonRequest(string text)
        {
            var optionalPractice = practiceTarget != "" && pendingTurn == null && correctionQuestion != "";
            return new SliceReviewRequest
            {
                band = Profile.Label, npc = Speaker, task = resident.Place.Name,
                npcLine = optionalPractice ? correctionQuestion : lesson.Prompt,
                goal = lesson.Plan.Goal, text = text,
            };
        }

        private void SubmitTurn(string text, bool spoken, bool uiAction, double? pronunciationScore)
        {
            text = (text ?? "").Trim();
            if (Talking == Partner.None || working || text.Length == 0 || text.Length > 400) return;
            voice.Cancel();
            ui.ClearInput();
            if (!uiAction) recognized = text;

            if (!uiAction && !spoken && SpeechAvailable)
            {
                if (pendingTurn != null && practiceTarget != "")
                {
                    status = "Rehearsal only — say the corrected reply to continue.";
                    Render();
                    return;
                }
                var understood = Talking == Partner.Resident ? lesson.Propose(text).Accepted : OrderIntentParser.Parse(text).Any(intent => intent.Kind != IntentKind.Unknown);
                lastReview = SpeakingCoach.Review(text, Save.Band, understood, false);
                gate = Gate.OfferRetry;
                status = "Rehearsal only — now press the microphone and say it to continue.";
                Render();
                return;
            }
            history.Add(new ChatLine { Speaker = "You", Text = text, Player = true });
            aiTip = semanticReview == null ? "" : semanticReview.lexicalTip;

            // Repeat-and-practise: the next attempt is checked against the corrected sentence.
            if (practiceTarget != "" && !uiAction)
            {
                attempts++;
                var practicePassed = Talking == Partner.Resident && semanticReview != null
                    ? semanticReview.relevant && semanticReview.goalMet && string.IsNullOrWhiteSpace(semanticReview.correction)
                    : SpeakingCoach.PracticePassed(text, practiceTarget, Save.Band);
                if (practicePassed)
                {
                    ShowToast("Nice — that sounds natural. Practice complete.");
                    Save.Progress.Unlock("first-correction");
                    practiceTarget = "";
                    gate = Gate.Continue;
                    status = "";
                    lastReview = SpeakingCoach.Review(text, Save.Band, true, spoken, pronunciationScore);
                    var objective = pendingTurn?.Objective ?? "practice";
                    var accepted = pendingTurn == null || CompletePending(true);
                    if (accepted) Record(objective, text, lastReview, spoken);
                    if (objective == "practice") Say(Speaker, "Great! " + npcLine);
                }
                else
                {
                    if (Talking == Partner.Resident && !string.IsNullOrWhiteSpace(semanticReview?.correction)) practiceTarget = semanticReview.correction;
                    var limit = Save.Strictness == Strictness.Challenge ? 3 : 2;
                    gate = attempts >= limit ? Gate.ShowModelAndContinue : Gate.RequireRetry;
                    status = gate == Gate.ShowModelAndContinue ? "Here's the model — listen, then carry on." : SpeakingCoach.Hint(practiceTarget, attempts);
                    if (gate == Gate.ShowModelAndContinue) modelAnswer = practiceTarget;
                }
                Persist();
                Render();
                return;
            }

            var proposal = Talking == Partner.Rowan ? new WaiterProposal(Save, text) : null;
            var turn = Talking == Partner.Resident ? LessonTurn(text) : proposal != null
                ? proposal.Turn
                : guide.Respond(text, Save.Clock.Weather, Save.Clock.MinuteOfDay, Save.Catches.Count);
            var taskMet = turn.Progress && turn.Problem == "";
            modelAnswer = turn.ModelAnswer;
            if (uiAction)
            {
                lastReview = null;
                status = "";
            }
            else
            {
                lastReview = SpeakingCoach.Review(text, Save.Band, taskMet, spoken, pronunciationScore);
                if (semanticReview != null && !string.IsNullOrWhiteSpace(semanticReview.correction))
                {
                    lastReview.Corrected = semanticReview.correction;
                    lastReview.Explanation = semanticReview.explanation;
                    lastReview.SignificantError = true;
                }
                if (Talking == Partner.Resident && lastReview.Corrected != "") correctionQuestion = lesson.Prompt;
                gate = SpeakingCoach.Decide(Save.Strictness, lastReview, taskMet ? 0 : attempts);
                attempts = taskMet ? 0 : attempts + 1;
                if (taskMet && lastReview.Corrected != "" && gate == Gate.RequireRetry)
                {
                    pendingOrder = proposal;
                    pendingTurn = turn;
                    practiceTarget = lastReview.Corrected;
                    attempts = 0;
                    Record("", text, lastReview, spoken);
                    Review(text);
                    Say(Speaker, "Before we continue, try the corrected sentence below.");
                    status = "Practise: say the corrected sentence (or your own natural version) to continue.";
                    Persist();
                    Render();
                    return;
                }
                status = !taskMet && turn.ModelAnswer != "" ? SpeakingCoach.Hint(turn.ModelAnswer, attempts) : "";
                if (gate == Gate.ShowModelAndContinue && !taskMet) status = "Try the model answer below, or press “Use this answer” to carry on.";
                Review(text);
            }
            if (ApplyTurn(turn, proposal, !uiAction && taskMet) && !uiAction)
                Record(taskMet ? turn.Objective : "", text, lastReview, spoken);
            Persist();
            Render();
        }

        private bool ApplyTurn(WaiterTurn turn, WaiterProposal proposal, bool credit)
        {
            if (Talking == Partner.Resident && lessonAnswer != null)
            {
                if (turn.Progress && lesson.Accept(lessonAnswer) && lesson.Complete)
                {
                    Save.Progress.Unlock("lesson-" + lesson.SceneId + "-" + (int)lesson.Band);
                    var title = resident.Place.Name + " · " + Profile.Label;
                    if (!Save.Progress.Scenarios.Contains(title)) Save.Progress.Scenarios.Add(title);
                }
                lessonAnswer = null;
            }
            if (proposal != null && !proposal.Commit(Save))
            {
                status = "Your order changed. Please say your request again.";
                return false;
            }
            if (credit) foreach (var objective in turn.Objectives) EarnObjective(objective);
            Act(turn);
            Say(Speaker, turn.Line);
            if ((turn.Actions & WaiterAction.EndConversation) != 0) StartCoroutine(EndAfterVoice());
            return true;
        }

        private WaiterTurn LessonTurn(string text)
        {
            lessonAnswer = lesson.Propose(text, semanticReview?.relevant, semanticReview?.goalMet);
            return new WaiterTurn
            {
                Progress = lessonAnswer.Accepted,
                Line = lessonAnswer.Accepted ? lessonAnswer.Prompt : lessonAnswer.Hint + " " + lesson.Prompt,
                Problem = lessonAnswer.Accepted ? "" : "goal_not_met",
                Objective = lessonAnswer.Accepted ? "scene-" + resident.SceneId : "",
            };
        }

        private bool CompletePending(bool credit)
        {
            var turn = pendingTurn;
            var proposal = pendingOrder;
            pendingTurn = null;
            pendingOrder = null;
            practiceTarget = "";
            gate = Gate.Continue;
            return turn == null || ApplyTurn(turn, proposal, credit);
        }

        private IEnumerator EndAfterVoice()
        {
            var current = epoch;
            yield return new WaitForSeconds(0.5f);
            while (voice.Playing) yield return null;
            yield return new WaitForSeconds(1.2f);
            if (current == epoch) EndConversation();
        }

        /// Optional AI reviewer: adds a second opinion on wording without touching game state.
        private void Review(string text)
        {
            if (!SpeechAvailable || Talking == Partner.Resident) return;
            var current = epoch;
            var reviewed = lastReview;
            var request = new SliceReviewRequest { band = Profile.Label, npc = Speaker, task = Talking == Partner.Rowan ? "ordering at a café" : "talking about fishing", npcLine = npcLine, text = text };
            StartCoroutine(companion.Review(request, result => {
                if (current != epoch || lastReview != reviewed || lastReview == null || result == null) return;
                if (lastReview.Corrected == "" && !string.IsNullOrWhiteSpace(result.correction) && !string.Equals(result.correction.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                    aiTip = "AI tip: " + result.correction + (string.IsNullOrWhiteSpace(result.explanation) ? "" : " — " + result.explanation);
                else if (!string.IsNullOrWhiteSpace(result.lexicalTip)) aiTip = "AI tip: " + result.lexicalTip;
                Render();
            }, _ => { }));
        }

        private void Record(string objective, string text, SpeakingResult review, bool spoken)
        {
            Save.Progress.Record(new TurnRecord
            {
                Objective = objective, Utterance = text, Corrected = review.Corrected, Explanation = review.Explanation, Score = (float)review.Score, Spoken = spoken,
                Day = Save.Clock.Day, Minute = Save.Clock.MinuteOfDay,
                Fluency = review.Criteria[0].Level, Lexical = review.Criteria[1].Level, Grammar = review.Criteria[2].Level, Pronunciation = review.Criteria[3].Level,
            }, review);
            var suggested = BandProfile.Adjust(Save.Band, Save.Progress.Average(), Save.Progress.Turns.Count);
            if (suggested > Save.Band && Save.Progress.Unlock("band-up-" + suggested)) ShowToast("You're consistently on target — try " + BandProfile.For(suggested).Label + " in Settings.");
        }

        private void EarnObjective(string objective)
        {
            if (string.IsNullOrEmpty(objective) || Save.Earned.Contains(objective)) return;
            Save.Earned.Add(objective);
            foreach (var quest in QuestBook.Completed(Save.QuestsDone, Save.Earned).ToArray())
            {
                Save.QuestsDone.Add(quest.Id);
                Save.Wallet.Card += quest.RewardMoney;
                Save.FishingXp += quest.Id.StartsWith("pond") ? quest.RewardXp : 0;
                Save.Progress.Scenarios.Add(quest.Title);
                Save.Progress.Unlock(quest.Id);
                foreach (var used in quest.Objectives) Save.Earned.Remove(used);
                ShowToast("Quest complete: " + quest.Title + " · " + quest.Reward);
            }
        }

        private void Act(WaiterTurn turn)
        {
            if ((turn.Actions & WaiterAction.OpenMenu) != 0) menuOpen = true;
            if ((turn.Actions & WaiterAction.CloseMenu) != 0) menuOpen = false;
            if ((turn.Actions & WaiterAction.Highlight) != 0 && turn.HighlightItem != "")
            {
                selectedItem = turn.HighlightItem;
                menuCategory = CafeMenu.Find(selectedItem).Category.ToString();
                menuOpen = true;
            }
            if ((turn.Actions & WaiterAction.SendToKitchen) != 0)
            {
                if (Save.Waiter.LastReceipt != null) Save.Receipts.Add(Save.Waiter.LastReceipt);
                Save.Cart.Status = OrderStatus.Preparing;
                Save.Cart.Table = seat != null ? seat.Table : "";
                Save.Kitchen.Add("T" + Save.Receipts.Count, Save.Cart);
                ShowToast("Order sent to the barista · receipt saved in your inventory (Tab)");
            }
        }

        // ------------------------------------------------------------------ voice
        private void Record()
        {
            if (Talking == Partner.None || working) return;
            if (recorder.State == "recording") { recorder.Stop(); return; }
            if (recorder.State == "requesting") { recorder.Cancel(); return; }
            if (!SpeechAvailable) { Status("Voice is offline. Type your reply, or start pnpm world:dev and press Reconnect."); return; }
            voice.Cancel();
            recorder.Begin();
        }

        private void Transcribe(byte[] wave)
        {
            if (Talking == Partner.None) return;
            working = true;
            var current = epoch;
            status = "Recognising speech…";
            Render();
            StartCoroutine(companion.Transcribe(wave, text => {
                if (current != epoch) return;
                if (!pronunciation.Available) { working = false; Submit(text, true); return; }
                status = "Assessing pronunciation…";
                StartCoroutine(pronunciation.Assess(wave, text, score => {
                    if (current != epoch) return;
                    working = false;
                    Submit(text, true, false, score);
                }));
            }, error => { if (current == epoch) { working = false; Status(error + " You can retry or type."); } }));
        }

        // ------------------------------------------------------------------ café service
        private void Kitchen(float dt)
        {
            var ready = Save.Kitchen.Tick(dt);
            if (ready == null || Waiter != null && Waiter.Busy
                || Save.Cart.Status != OrderStatus.Preparing && Save.Cart.Status != OrderStatus.Ready) return;
            Save.Cart.Status = OrderStatus.Ready;
            var orderSave = Save;
            var orderCart = Save.Cart;
            // Deliver to the player's table, or to the reserved window table (T1) if they are not sitting.
            var table = seat != null ? seat : Seats.FirstOrDefault(candidate => candidate.Table == "T1") ?? Seats.FirstOrDefault();
            Save.Cart.Table = table != null ? table.Table : "";
            var target = table != null && table.TableTop != null ? table.TableTop.position + (table.transform.position - table.TableTop.position).normalized * 0.6f : Counter.position + Counter.forward;
            target.y = 0;
            ShowToast(seat != null ? "Your order is ready — Rowan is bringing it to your table." : "Your order is ready — Rowan is putting it on the window table (T1).");
            Action onDelivered = () =>
            {
                // A new game or a replaced order invalidates any waiter job still in flight.
                if (quitting || !ReferenceEquals(Save, orderSave) || !ReferenceEquals(Save.Cart, orderCart) || Save.Cart.Status != OrderStatus.Ready) return;
                Save.Kitchen.Collect(ready.Id);
                var turn = Save.Waiter.Deliver(Save.Cart);
                foreach (var objective in turn.Objectives) EarnObjective(objective);
                ServeProps();
                if (Vector3.Distance(Player.transform.position, Rowan.position) < 6) { npcLine = turn.Line; voice.Say(companion, turn.Line, Profile.SpeechRate, _ => { }); ShowToast("Rowan: " + turn.Line); }
                Persist();
            };
            if (Waiter != null) Waiter.Deliver(target, onDelivered); else onDelivered();
            Persist();
        }

        private Seat ServedSeat => Seats.FirstOrDefault(candidate => candidate.Table == Save.Cart.Table) ?? Seats.FirstOrDefault(candidate => candidate.Table == "T1");

        private void ServeProps()
        {
            ClearProps();
            var table = ServedSeat?.TableTop;
            if (table == null) return;
            var offset = 0;
            foreach (var line in Save.Cart.Lines)
            {
                var menu = line.Menu;
                var pool = menu.Food ? FoodProps : DrinkProps;
                if (pool.Length == 0) continue;
                for (var i = 0; i < line.Quantity && offset < 6; i++, offset++)
                {
                    var prop = Instantiate(pool[Math.Abs(menu.Id.GetHashCode()) % pool.Length], table.position + new Vector3(-0.3f + (offset % 3) * 0.3f, 0.02f, offset < 3 ? -0.15f : 0.15f), Quaternion.identity);
                    prop.name = (menu.Food ? "Food · " : "Drink · ") + menu.Name;
                    prop.SetActive(true);
                    servedProps.Add(prop);
                }
            }
        }

        private void ClearProps()
        {
            foreach (var prop in servedProps) if (prop != null) Destroy(prop);
            servedProps.Clear();
        }

        private void Consume()
        {
            var serving = Save.Cart.ConsumeOne();
            if (serving == null) return;
            var food = serving.Menu.Food;
            activity = food ? PlayerAnimState.Eat : PlayerAnimState.Drink;
            activityTimer = 2.5f;
            if (food) Save.Hunger = Mathf.Max(0, Save.Hunger - 0.45f); else Save.Thirst = Mathf.Max(0, Save.Thirst - 0.5f);
            Save.Energy = Mathf.Min(1, Save.Energy + 0.1f);
            if (Save.Cart.Empty)
            {
                Save.Cart = new OrderCart();
                Save.Waiter.Stage = WaiterStage.Idle;
                Save.Progress.Unlock("first-meal");
                ShowToast("All finished! Rowan clears the table. Talk to him again to order more.");
            }
            ServeProps(); // replenish the six visible places until every paid serving has been consumed
            Persist();
        }

        private void Sit(Seat target)
        {
            seat = target;
            seat.Occupant = this;
            Player.Place(seat.SitPoint);
            Player.Face(seat.Facing);
            Player.Activity = PlayerAnimState.Sit;
            Player.Blocked = true;
            Save.Seat = seat.Table;
            Save.Energy = Mathf.Min(1, Save.Energy + 0.05f);
        }

        private void Stand()
        {
            if (seat != null && seat.Occupant == this) seat.Occupant = null;
            seat = null;
            Save.Seat = "";
            Player.Activity = PlayerAnimState.Idle;
            Player.Blocked = false;
            Player.Place(Player.transform.position + Player.Model.forward * -0.6f + Vector3.up * 0.1f);
        }

        private void Activity(float dt)
        {
            if (activity == PlayerAnimState.Idle) return;
            activityTimer -= dt;
            Player.Activity = activityTimer > 0 ? activity : seat != null ? PlayerAnimState.Sit : PlayerAnimState.Idle;
            if (activityTimer <= 0) activity = PlayerAnimState.Idle;
        }

        private void Needs(float dt)
        {
            // Lightweight: about one "meal" per in-game afternoon, never blocking play.
            var minutes = dt * WorldClock.MinutesPerSecond;
            Save.Hunger = Mathf.Min(1, Save.Hunger + minutes / 600f);
            Save.Thirst = Mathf.Min(1, Save.Thirst + minutes / 420f);
            Save.Energy = Mathf.Max(0.1f, Save.Energy - minutes / 1400f);
        }

        // ------------------------------------------------------------------ fishing
        private void StartFishing()
        {
            if (Pier == null) return;
            Player.Place(Pier.Stand.position);
            Player.Face(Pier.WaterTarget.position - Pier.Stand.position);
            Player.Activity = PlayerAnimState.Fish;
            Player.Blocked = true;
            fishing.Cast();
            fishingTime = 0;
            if (Save.Catches.Count == 0) ShowToast("Tip: talk to Hazel (F) first — she'll explain the float and the tension bar.");
        }

        private void StopFishing()
        {
            if (fishing.State == FishingState.Idle) return;
            fishing.State = FishingState.Idle;
            Pier?.Hide();
            Player.Activity = PlayerAnimState.Idle;
            Player.Blocked = Talking != Partner.None;
        }

        private void Fish(float dt)
        {
            if (fishing.State == FishingState.Idle) return;
            fishingTime += dt;
            var before = fishing.State;
            var level = FishingSession.Level(Save.FishingXp);
            fishing.Tick(dt, Player.Controls.Primary, Save.Clock.Weather, Save.Clock.MinuteOfDay, level);
            Pier?.Show(fishing, PlayerHand != null ? PlayerHand : Player.transform, fishingTime);
            if (before != FishingState.Caught && fishing.State == FishingState.Caught)
            {
                var species = FishingSession.Find(fishing.Last.Species);
                fishing.Last.Day = Save.Clock.Day;
                fishing.Last.Minute = Save.Clock.MinuteOfDay;
                Save.Catches.Add(fishing.Last);
                Save.Give(species.Id);
                Save.FishingXp += species.Xp;
                if (FishingSession.Level(Save.FishingXp) > level) ShowToast("Fishing level " + FishingSession.Level(Save.FishingXp) + "!");
                else ShowToast(fishing.Message + " " + species.Fact);
                EarnObjective("catch_fish");
                if (species.Rarity == "Legendary") Save.Progress.Unlock("golden-carp");
                Persist();
            }
            if (before != FishingState.Escaped && fishing.State == FishingState.Escaped) ShowToast(fishing.Message + " Tell Hazel: “That one nearly got away!”");
        }

        // ------------------------------------------------------------------ UI commands
        public void OnBrowserCommand(string json)
        {
            if (json == null || json.Length > 2000) return;
            BrowserCommand command;
            try { command = JsonUtility.FromJson<BrowserCommand>(json); }
            catch (ArgumentException) { return; }
            if (command != null) Command(command.action, command.text);
        }

        private void Command(string action, string argument)
        {
            var capturing = recorder.State == "recording" || recorder.State == "requesting";
            if ((working || capturing) && new[] { "continue", "use-model", "practice", "visit", "retry-lesson", "plan-route", "practice-need", "wardrobe", "motion-preview" }.Contains(action))
            {
                Status("Finish the recording and answer check first, or close the conversation to cancel.");
                return;
            }
            if (gate == Gate.RequireRetry && (pendingTurn != null || practiceTarget != "")
                && new[] { "visit", "retry-lesson", "plan-route", "practice-need", "wardrobe", "motion-preview" }.Contains(action))
            {
                Status("Say the corrected reply first, or close this conversation to cancel it.");
                return;
            }
            if (pendingTurn != null && new[] { "add", "option", "quantity", "checkout" }.Contains(action))
            {
                Status("Finish the corrected reply before changing your order.");
                return;
            }
            switch (action)
            {
                case "send": Submit(argument, false); break;
                case "record": Record(); break;
                case "practice":
                    if (practiceTarget == "") { practiceTarget = lastReview?.Corrected ?? ""; attempts = 0; }
                    if (practiceTarget != "") Record();
                    break;
                case "repeat": voice.Play(Profile.SpeechRate); break;
                case "slower": voice.Play(Profile.SpeechRate * 0.78f); break;
                case "hear-correction":
                    var correction = practiceTarget != "" ? practiceTarget : lastReview?.Corrected ?? modelAnswer;
                    if (!string.IsNullOrEmpty(correction)) voice.Say(companion, correction, Profile.SpeechRate * 0.92f, error => Status(error));
                    break;
                case "continue":
                    if (pendingTurn != null && gate == Gate.RequireRetry) break;
                    CompletePending(false);
                    status = "";
                    break;
                case "use-model":
                    if (pendingTurn != null && gate == Gate.ShowModelAndContinue) CompletePending(false);
                    else if (pendingTurn == null && !string.IsNullOrEmpty(modelAnswer)) { var model = modelAnswer; modelAnswer = ""; gate = Gate.Continue; Submit(model, false, true); }
                    break;
                case "close": if (Talking != Partner.None) EndConversation(); menuOpen = false; break;
                case "talk":
                    if (Talking != Partner.None) break;
                    var near = NearestPartner();
                    if (near != Partner.None) BeginConversation(near);
                    break;
                case "menu": menuOpen = !menuOpen; break;
                case "category": menuCategory = argument; break;
                case "select": selectedItem = argument; break;
                case "add": AddFromMenu(argument); break;
                case "option": SetOption(argument); break;
                case "quantity": ChangeQuantity(argument); break;
                case "checkout": if (Talking == Partner.Rowan && !working && Save.Cart.Ready) Submit("That's everything, thanks.", false, true); break;
                case "panel": panel = panel == argument ? "" : argument; break;
                case "visit": Visit(argument); break;
                case "motion-preview": PreviewMotion(argument); break;
                case "plan-route":
                    Save.FocusVocabulary = SpeakingTrail.Focus(argument);
                    Persist();
                    ShowToast("Speaking route updated. Your completed conversations are kept.");
                    break;
                case "practice-need":
                    if (!Enum.TryParse(argument, out PracticeNeed need) || !Enum.IsDefined(typeof(PracticeNeed), need)) break;
                    Save.PracticeNeed = need;
                    Persist();
                    ShowToast("Practice situation: " + need + ". Your route now matches this goal.");
                    break;
                case "retry-lesson":
                    if (working || recorder.State == "recording" || recorder.State == "requesting") break;
                    var retry = Save.Lessons.FirstOrDefault(item => item.SceneId == argument && item.Band == Save.Band);
                    if (retry != null) { retry.Step = 0; retry.LastAnswer = ""; }
                    Visit(argument);
                    break;
                case "wardrobe": EditCharacter(); break;
                case "character":
                    if (Enum.TryParse(argument, out PlayerLook look)) Player.Model.GetComponent<PlayerAppearance>()?.Choose(look);
                    break;
                case "band": SetBand(argument); break;
                case "strictness": if (Enum.TryParse(argument, out Strictness strictness)) Save.Strictness = strictness; break;
                case "sensitivity": if (float.TryParse(argument, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) { Save.MouseSensitivity = Mathf.Clamp(value, 0.2f, 3f); Player.MouseSensitivity = Save.MouseSensitivity; } break;
                case "room-create": room.Join(); break;
                case "room-join": room.Join(argument); break;
                case "room-leave": room.Leave(); break;
                case "room-server": room.SetEndpoint(argument); break;
                case "toggle-mute": GameAudio.SetMuted(!GameAudio.Muted); break;
                case "mute": if (bool.TryParse(argument, out var muted)) GameAudio.SetMuted(muted); break;
                case "volume": if (float.TryParse(argument, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var volume)) GameAudio.SetVolume(volume); break;
                case "translation": Save.Translation = !Save.Translation; break;
                case "subtitles": Save.Subtitles = !Save.Subtitles; break;
                case "connect": Connect(); break;
                case "save": notifySave = true; Persist(); break;
                case "reset": if (argument == "confirm") ResetGame(); break;
                case "life": Persist(); WorldScenes.Open("LifeTown", error => Status(error)); break;
            }
            Persist(false);
            Render();
        }

        private void EditCharacter()
        {
            if (working || recorder.State == "recording" || recorder.State == "requesting") return;
            if (Talking != Partner.None) EndConversation();
            if (seat != null) Stand();
            StopFishing();
            Persist();
            CharacterCreationController.Open("ForestSlice");
        }

        private void ResetGame()
        {
            EndConversation();
            Waiter?.CancelDelivery();
            StopFishing();
            if (seat != null) Stand();
            Save = new SliceSave { Band = Save.Band, Strictness = Save.Strictness };
            Save.Waiter.Band = Save.Band;
            guide.Band = Save.Band;
            history.Clear();
            lastReview = null;
            gate = Gate.Continue;
            modelAnswer = recognized = aiTip = npcLine = status = "";
            attempts = 0;
            panel = "";
            ClearProps();
            Persist();
            ShowToast("New game started.");
        }

        private void SetBand(string value)
        {
            if (!Enum.TryParse(value, out IeltsBand band) || !BandProfile.Valid(band)) return;
            if (Talking != Partner.None) EndConversation();
            Save.Band = band;
            // Practice target and correction mode are independent learner settings.
            Save.Waiter.Band = band;
            guide.Band = band;
            ShowToast("Difficulty: " + BandProfile.For(band).Label + " · " + BandProfile.For(band).Summary);
        }

        private void PreviewMotion(string value)
        {
            if (working || recorder.State == "recording" || recorder.State == "requesting" || Talking != Partner.None || seat != null) return;
            var parts = value.Split('|');
            if (parts.Length != 2) return;
            if (parts[0] == "Academy" || parts[0] == "Daydream")
            {
                if (Player.Model.GetComponent<PlayerAppearance>()?.PreviewMotion(parts[0], parts[1]) == true)
                { panel = ""; ShowToast("Supplied animation · " + parts[0] + " · " + parts[1]); }
                return;
            }
            var actors = new[] { Rowan, Hazel }.Where(actor => actor != null).Concat(residents.Select(npc => npc.transform));
            var motion = actors.Select(actor => actor.GetComponentInChildren<AuthoredMotion>())
                .FirstOrDefault(candidate => candidate != null && candidate.ModelName == parts[0]);
            if (motion == null) return;
            if (motion.GetComponentInParent<CafePlayer>() == null)
            {
                var arrival = motion.transform.position + Vector3.back * 2.5f;
                if (UnityEngine.AI.NavMesh.SamplePosition(arrival, out var ground, 2, UnityEngine.AI.NavMesh.AllAreas)) Player.Place(ground.position);
                Player.Face(motion.transform.position - Player.transform.position);
            }
            panel = "";
            if (motion.Preview(parts[1])) ShowToast("Supplied animation · " + parts[0] + " · " + parts[1]);
        }

        private void Visit(string id)
        {
            sceneryFocus = id.StartsWith("World") ? id : "";
            var target = residents.FirstOrDefault(npc => npc.SceneId == id);
            var lodge = interactables.FirstOrDefault(item => item.Id == (id == "lodge" ? "speaking-board" : id));
            if ((target == null && id != "counter" && lodge?.Arrival == null) || working) return;
            if (Talking != Partner.None) EndConversation();
            if (seat != null) Stand();
            StopFishing();
            panel = "";
            menuOpen = false;
            Player.Route.Clear();
            Player.Controls.Clear();
            if (lodge != null)
            {
                Player.Place(lodge.Arrival.position);
                Player.Face(lodge.transform.position - Player.transform.position);
                ShowToast(lodge.Prompt + " · E to use");
                return;
            }
            if (id == "counter")
            {
                Player.Place(OrderArrival);
                Player.Face(Counter.position - Player.transform.position);
                ShowToast("Order counter · F to speak to Rowan");
                return;
            }
            Player.Place(target.Arrival);
            Player.Face(target.transform.position - target.Arrival);
            ShowToast(target.Place.Name + " · F to talk to " + target.DisplayName);
        }

        private void AddFromMenu(string id)
        {
            if (Talking != Partner.Rowan) { ShowToast("Talk to Rowan (F) to order — the menu shows what you can say."); return; }
            if (!Save.Cart.Editable || CafeMenu.Find(id) == null) return;
            var line = Save.Cart.Add(id);
            foreach (var choice in ui.PendingChoices(id)) line.Set(choice.Key, choice.Value);
            var turn = Save.Waiter.MenuChanged(Save.Cart, line);
            Say("Rowan", turn.Line);
        }

        private void SetOption(string argument)
        {
            // "index|Group|choice" for a cart line.
            var parts = argument.Split('|');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var index) || index < 0 || index >= Save.Cart.Lines.Count || !Save.Cart.Editable) return;
            if (!Enum.TryParse(parts[1], out OptionGroup group)) return;
            Save.Cart.Lines[index].Set(group, parts[2]);
        }

        private void ChangeQuantity(string argument)
        {
            var parts = argument.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var index) || !int.TryParse(parts[1], out var delta) || index < 0 || index >= Save.Cart.Lines.Count) return;
            Save.Cart.ChangeQuantity(Save.Cart.Lines[index], Save.Cart.Lines[index].Quantity + delta);
        }

        // ------------------------------------------------------------------ persistence
        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        private SliceSave Load()
        {
            try
            {
                if (File.Exists(SavePath)) return SliceSave.Sanitize(JsonUtility.FromJson<SliceSave>(File.ReadAllText(SavePath)));
            }
            catch (Exception error) { Debug.LogWarning("Forest Café save could not be read; starting fresh. " + error.Message); }
            return new SliceSave { Band = (IeltsBand)CharacterProfiles.Draft().Band };
        }

        private float lastPersist;
        private void CapturePlayerPosition()
        {
            // Scene teardown may destroy the player before its save owner.
            if (Player == null) return;
            var position = Player.transform.position;
            Save.PlayerX = position.x; Save.PlayerY = position.y; Save.PlayerZ = position.z;
        }

        private void Persist(bool force = true)
        {
            if (Save == null || quitting) return;
            if (!force && Time.unscaledTime - lastPersist < 2) return;
            lastPersist = Time.unscaledTime;
            CapturePlayerPosition();
            try
            {
                var temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Save));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(temporary, SavePath);
                saveVersion++;
#if UNITY_WEBGL && !UNITY_EDITOR
                saveState = "saving";
                SyncSave();
#else
                saveState = "saved";
                if (notifySave) { ShowToast("Game saved."); notifySave = false; }
#endif
            }
            catch (Exception error) { saveState = "error"; ShowToast("Could not save. Keep the game open and try Save again."); Debug.LogWarning("Forest Café save failed: " + error.Message); }
        }

        private void SyncSave()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (syncing) return;
            syncing = true;
            syncingVersion = saveVersion;
            CS_SyncSave("ForestCafeSlice", syncingVersion);
#endif
        }

        [Serializable] private sealed class SaveSyncReply { public int generation; public string error; }
        public void OnSaveSynced(string json)
        {
            var reply = JsonUtility.FromJson<SaveSyncReply>(json);
            if (quitting || reply == null || reply.generation != syncingVersion) return;
            syncing = false;
            if (!string.IsNullOrEmpty(reply.error)) { saveState = "error"; ShowToast(reply.error); }
            else if (saveVersion != reply.generation) SyncSave();
            else
            {
                saveState = "saved";
                if (notifySave) { ShowToast("Game saved."); notifySave = false; }
            }
            Render();
        }

        // ------------------------------------------------------------------ view
        private void ShowToast(string message)
        {
            Toast = message;
            toastUntil = Time.unscaledTime + 6;
        }
        private void Status(string message)
        {
            status = message;
            Render();
        }

        private void Render()
        {
            if (ui == null || Save == null || quitting) return;
            if (Time.unscaledTime > toastUntil) Toast = "";
            var quest = QuestBook.Active(Save.QuestsDone);
            var trail = SpeakingTrail.Create(Save);
            var view = new SliceView
            {
                Room = room,
                CharacterName = CharacterProfiles.Draft().Name,
                Character = Player.Model.GetComponent<PlayerAppearance>()?.Selected ?? PlayerLook.Girl,
                Place = CurrentPlace(),
                Time = Save.Clock.Time + " · Day " + Save.Clock.Day,
                Weather = Save.Clock.Weather.ToString(),
                Cash = CafeMenu.Money(Save.Wallet.Cash), Card = CafeMenu.Money(Save.Wallet.Card),
                Band = Profile.Label, Strictness = Save.Strictness.ToString(), BandId = Save.Band.ToString(),
                Energy = Save.Energy, Hunger = Save.Hunger, Thirst = Save.Thirst,
                Hint = Hint(), Toast = Toast,
                Quest = quest, QuestEarned = Save.Earned.ToArray(),
                Talking = Talking, Speaker = Speaker,
                Role = Talking == Partner.Resident ? resident.DisplayRole : Talking == Partner.Rowan ? "Waiter · Forest Café" : "Fishing instructor",
                Lesson = Talking == Partner.Resident ? lesson : null,
                Line = npcLine, Glossary = Glossary.In(npcLine), Translation = Save.Translation, Subtitles = Save.Subtitles,
                Mic = SpeechAvailable ? recorder.State : "offline", Recognized = recognized, Status = status, Busy = working,
                History = history.ToArray(), Review = lastReview, Gate = gate, PracticeTarget = practiceTarget, ModelAnswer = modelAnswer, AiTip = aiTip,
                Attempts = attempts, CanReplay = voice.HasLine, Paying = Talking == Partner.Rowan && Save.Waiter.Stage == WaiterStage.Paying,
                MenuOpen = menuOpen, MenuCategory = menuCategory, SelectedItem = selectedItem, Cart = Save.Cart,
                Fishing = fishing.State != FishingState.Idle ? fishing : null, FishingLevel = FishingSession.Level(Save.FishingXp), FishingXp = Save.FishingXp,
                Panel = panel, Save = Save, Recommendation = Save.Progress.Recommendation(Save.Band), Weaknesses = Save.Progress.Weaknesses(),
                PlayerX = Player.transform.position.x, PlayerZ = Player.transform.position.z,
                Markers = Markers(),
            };
            ui.Render(view);
#if UNITY_WEBGL && !UNITY_EDITOR
            // The host switches legacy overlays off when the native slice UI owns the screen.
            CS_State(JsonUtility.ToJson(new BrowserState
            {
                panel = panel,
                inspected = lastInspected,
                buildingCount = buildings.Length,
                insideBuilding = buildings.FirstOrDefault(building => building.Kind == "room" && building.IsInside)?.Scene ?? "",
                exteriorBuildings = buildings.Count(building => building.Kind == "room" && building.Exterior.activeSelf),
                interaction = NearestInteractable()?.Id ?? "",
                loadedScenery = scenery?.LoadedIds ?? new string[0],
                sourceCount = scenery?.Entries.Length ?? 0,
                sceneryCount = scenery?.Entries.Count(entry => entry.usage == "world") ?? 0,
                visibleScenery = scenery?.LoadedCount ?? 0,
                loadingScenery = scenery?.LoadingCount ?? 0,
                sceneryTriangles = scenery?.LoadedTriangleBudget ?? 0,
                animation = Player.GetComponentInChildren<AuthoredMotion>()?.CurrentClip ?? "",
                animationTime = Player.GetComponentInChildren<AuthoredMotion>()?.ClipTime ?? 0,
                roomCode = room.Code,
                roomStatus = room.Status,
                roomReady = room.Ready,
                roomBusy = room.Busy,
                roomCount = room.Count,
                roomPlayers = room.Players,
                muted = GameAudio.Muted,
                volume = GameAudio.Volume,
                listenerVolume = AudioListener.volume,
                dialogue = Talking != Partner.None,
                speaker = view.Speaker,
                line = npcLine,
                transcript = recognized,
                recording = view.Mic,
                busy = working,
                stage = Save.Waiter.Stage.ToString(),
                order = Save.Cart.Status.ToString(),
                band = Save.Band.ToString(),
                saveState = saveState,
                strictness = Save.Strictness.ToString(),
                practiceTarget = practiceTarget,
                gate = gate.ToString(),
                receipts = Save.Receipts.Count,
                servings = Save.Cart.Lines.Sum(line => line.Quantity),
                cash = Save.Wallet.Cash,
                card = Save.Wallet.Card,
                total = Save.Cart.Total,
                feedback = aiTip,
                focusVocabulary = Save.FocusVocabulary,
                practiceNeed = Save.PracticeNeed.ToString(),
                character = view.Character.ToString(),
                characterName = view.CharacterName,
                trailNext = trail.Next(Save)?.Id ?? "",
                trailCompleted = trail.Completed(Save),
                lesson = Talking == Partner.Resident ? resident.SceneId : "",
                lessonStep = Talking == Partner.Resident ? lesson.Step : 0,
                x = view.PlayerX,
                z = view.PlayerZ,
            }));
#endif
        }

        [Serializable]
        private sealed class BrowserState
        {
            public string mode = "slice", panel, speaker, line, transcript, recording, stage, order, band, strictness, practiceTarget, gate, saveState, lesson, feedback;
            public string focusVocabulary, trailNext, practiceNeed, character, characterName;
            public int trailCompleted, sourceCount, sceneryCount, visibleScenery, loadingScenery, sceneryTriangles;
            public string animation, inspected, interaction;
            public string insideBuilding;
            public int buildingCount, exteriorBuildings;
            public string[] loadedScenery;
            public float animationTime;
            public int receipts, servings, cash, card, lessonStep;
            public bool dialogue, busy, muted, roomReady, roomBusy;
            public string roomCode, roomStatus;
            public int roomCount;
            public RoomPlayer[] roomPlayers;
            public float total, x, z, volume, listenerVolume;
        }

        private Vector4[] Markers()
        {
            // x, z, kind (0 Rowan, 1 Hazel, 2 pier, 3 seat), 1 = objective target
            var target = QuestBook.Active(Save.QuestsDone)?.Npcs.FirstOrDefault() ?? "";
            var list = new List<Vector4>();
            if (Rowan != null) list.Add(new Vector4(Rowan.position.x, Rowan.position.z, 0, target == "Rowan" ? 1 : 0));
            if (Hazel != null) list.Add(new Vector4(Hazel.position.x, Hazel.position.z, 1, target == "Hazel" ? 1 : 0));
            if (Pier != null) list.Add(new Vector4(Pier.Stand.position.x, Pier.Stand.position.z, 2, 0));
            var next = SpeakingTrail.Create(Save).Next(Save);
            foreach (var npc in residents) list.Add(new Vector4(npc.transform.position.x, npc.transform.position.z, 4, npc.SceneId == next?.Id ? 1 : 0));
            foreach (var chair in Seats.Where(chair => chair != null).Take(16)) list.Add(new Vector4(chair.transform.position.x, chair.transform.position.z, 3, 0));
            return list.ToArray();
        }

        private string CurrentPlace()
        {
            if (AtPier || fishing.State != FishingState.Idle) return "Fishing pier";
            var nearest = residents.OrderBy(npc => Vector3.Distance(npc.transform.position, Player.transform.position)).FirstOrDefault();
            if (nearest != null && Vector3.Distance(nearest.transform.position, Player.transform.position) < 15) return nearest.Place.Name;
            return "Village trail";
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && recorder != null && !recorder.WaitingForPermission && Talking != Partner.None)
                EndConversation();
        }

        private void OnApplicationQuit() { Persist(); quitting = true; }
        private void OnDestroy()
        {
            if (!quitting) Persist();
            quitting = true;
            ClearProps();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (recorder != null) { recorder.Changed -= Render; recorder.Cancel(); }
            if (client != null) client.Cancel();
        }
    }

    /// Everything the interface needs for one frame. The UI never changes game state directly.
    public sealed class SliceView
    {
        public string Place, Time, Weather, Cash, Card, Band, BandId, Strictness, Hint, Toast;
        public float Energy, Hunger, Thirst;
        public QuestDefinition Quest;
        public string[] QuestEarned;
        public Partner Talking;
        public string Speaker, Role, Line, Mic, Recognized, Status, PracticeTarget, ModelAnswer, AiTip;
        public GlossaryEntry[] Glossary;
        public bool Translation, Subtitles, Busy, CanReplay, Paying, MenuOpen;
        public ChatLine[] History;
        public SpeakingResult Review;
        public Gate Gate;
        public string MenuCategory, SelectedItem;
        public OrderCart Cart;
        public FishingSession Fishing;
        public int FishingLevel, FishingXp, Attempts;
        public string Panel, Recommendation;
        public string[] Weaknesses;
        public SliceSave Save;
        public RoomClient Room;
        public SceneLesson Lesson;
        public string CharacterName;
        public PlayerLook Character;
        public float PlayerX, PlayerZ;
        public Vector4[] Markers;
    }
}
