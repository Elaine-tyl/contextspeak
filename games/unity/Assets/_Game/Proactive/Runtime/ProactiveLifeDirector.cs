using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Learning;
using ContextSpeak.Life;
using ContextSpeak.LivingWorld;
using ContextSpeak.Slice;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.InputSystem;
namespace ContextSpeak.Proactive
{
    [DefaultExecutionOrder(-90)]
    public sealed class ProactiveLifeDirector : MonoBehaviour
    {
        public static ProactiveLifeDirector Instance { get; private set; }
        public static bool OwnsInput => Instance != null && (Instance.Conversation != null && Instance.Conversation.Open || Instance.ConsumedFrame == Time.frameCount);
        public static bool HasOverlay => Instance != null && (Instance.Offered != null || Instance.SettingsOpen || Instance.Conversation.Open);
        public ProactiveSave State { get; private set; }
        public EventQuestAdapter Quest { get; private set; }
        public NPCConversationInitiator Conversation { get; private set; }
        public CafePlayer Player { get; private set; }
        public ProactiveNPCController Offered { get; private set; }
        public ProactiveNPCController Actor { get; private set; }
        public string OfferLine { get; private set; } = "";
        public bool SettingsOpen;
        public int ConsumedFrame { get; private set; } = -1;
        public int Band => CharacterProfiles.TryLoad(out var profile) ? profile.Band : 5;
        private ProactiveInteractionUI ui;
        private EventCooldownManager cooldown;
        private ContextualEventSelector selector;
        private ProactiveNPCController[] actors = Array.Empty<ProactiveNPCController>();
        private SliceGame slice;
        private CityGame city;
        private LifeEventDefinition offeredEvent;
        private float expires, nextScan, nextBind, ambientAfter;
        private bool oldBlocked, oldSuspended;
        private bool oldLook, oldCursorVisible;
        private CursorLockMode oldCursorLock;
        private string sceneName = "";
        private InputAction preferences;
        private string PathName => System.IO.Path.Combine(Application.persistentDataPath, "proactive_npc_v71.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("ContextSpeak Proactive NPC V7.1").AddComponent<ProactiveLifeDirector>();
        }
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            var assets = Resources.LoadAll<ProactiveEventAsset>("ProactiveV71/Events");
            var definitions = ProactiveEventContent.All().Select(fallback => assets.FirstOrDefault(a => a.Definition.Id == fallback.Id)?.Definition ?? fallback).ToArray();
            State = Load(); Quest = new EventQuestAdapter(State, definitions); cooldown = new EventCooldownManager(State);
            selector = new ContextualEventSelector(definitions);
            Conversation = gameObject.AddComponent<NPCConversationInitiator>();
            // Separate owner avoids reusing a stale UI document from another district.
            ui = new GameObject("ContextSpeak proactive UI").AddComponent<ProactiveInteractionUI>();
            DontDestroyOnLoad(ui.gameObject); ui.Bind(this);
            preferences = new InputAction("NPC preferences", InputActionType.Button, "<Keyboard>/n");
            preferences.Enable();
            SceneManager.activeSceneChanged += SceneChanged;
            nextBind = Time.unscaledTime + 1;
            Conversation.Connect();
        }
        private ProactiveSave Load()
        {
            foreach (var path in new[] { PathName, PathName + ".bak" })
            {
                try { if (File.Exists(path)) return NPCMemoryService.Sanitize(JsonUtility.FromJson<ProactiveSave>(File.ReadAllText(path))); }
                catch (Exception error) when (error is IOException || error is ArgumentException) { Debug.LogWarning("Proactive save recovery: " + error.Message); }
            }
            return new ProactiveSave();
        }
        public void Persist()
        {
            try
            {
                LearningSaveFile.Write(PathName, JsonUtility.ToJson(State));
                // Reuse the existing WebGL persistence flush without changing the original save schema.
                slice?.SaveProactiveEvidence(State.Learner);
            }
            catch (IOException error) { Debug.LogWarning("Proactive save retained: " + error.Message); }
        }
        private void SceneChanged(Scene oldScene, Scene newScene) { Close(); nextBind = Time.unscaledTime + 1; Player = null; }
        private void BindScene()
        {
            sceneName = SceneManager.GetActiveScene().name;
            Player = FindFirstObjectByType<CafePlayer>();
            if (Player == null) return;
            slice = FindFirstObjectByType<SliceGame>(); city = FindFirstObjectByType<CityGame>();
            ConfigureScene();
            nextScan = Time.unscaledTime + 5;
        }
        public void ConfigureScene()
        {
            if (Player == null) return;
            if (Player.GetComponent<NPCPlayerCollisionInteraction>() == null) Player.gameObject.AddComponent<NPCPlayerCollisionInteraction>();
            var roots = new HashSet<GameObject>();
            var profiles = Resources.LoadAll<ProactiveNPCProfile>("ProactiveV71/Profiles");
            foreach (var npc in FindObjectsByType<LivingNPC>(FindObjectsSortMode.None)) roots.Add(npc.gameObject);
            foreach (var npc in FindObjectsByType<LearningNpc>(FindObjectsSortMode.None)) roots.Add(npc.gameObject);
            foreach (var npc in FindObjectsByType<NPCController>(FindObjectsSortMode.None)) roots.Add(npc.gameObject);
            foreach (var npc in FindObjectsByType<CityPedestrian>(FindObjectsSortMode.None)) roots.Add(npc.gameObject);
            foreach (var root in roots)
            {
                if (root.GetComponentInParent<CafePlayer>() != null || root.scene != Player.gameObject.scene) continue;
                var npc = root.GetComponent<ProactiveNPCController>() ?? root.AddComponent<ProactiveNPCController>();
                if (npc.Profile == null && string.IsNullOrEmpty(npc.Social.Id)) Describe(npc);
                if (npc.Profile == null) npc.Profile = profiles.FirstOrDefault(p => p.Definition.Id == npc.Social.Id);
                EnsureBody(root);
            }
            actors = FindObjectsByType<ProactiveNPCController>(FindObjectsSortMode.None).Where(n => n.gameObject.scene == Player.gameObject.scene).ToArray();
            // Retire the old proximity-driven bump/invitation loop. Its daily-life simulation stays intact.
            foreach (var old in FindObjectsByType<NPCEventDirector>(FindObjectsSortMode.None)) old.enabled = false;
            RestoreMeeting();
        }
        public static void Describe(ProactiveNPCController npc)
        {
            var living = npc.GetComponent<LivingNPC>(); var learner = npc.GetComponent<LearningNpc>(); var legacy = npc.GetComponent<NPCController>();
            var stable = npc.transform.name + ":" + npc.transform.GetSiblingIndex();
            npc.Social = new NPCSocialProfile {
                Id = living != null ? living.Id : learner != null ? "resident:" + learner.SceneId : SceneManager.GetActiveScene().name + ":" + stable,
                Name = living != null ? living.DisplayName : learner != null ? learner.DisplayName : legacy != null ? legacy.DisplayName : "Jamie",
                Occupation = living?.Profile?.Occupation ?? legacy?.Role ?? "Resident",
                Sociability = living?.Profile?.Personality.Sociability ?? .6f,
            };
        }
        public static void EnsureBody(GameObject actor)
        {
            if (actor.GetComponentsInChildren<Collider>().Any(c => !c.isTrigger && !(c is MeshCollider))) return;
            var body = actor.AddComponent<CapsuleCollider>(); body.radius = .27f; body.height = 1.65f;
            body.center = Vector3.up * .825f;
            // Actor roots are metre-scaled by the existing art pipeline. No model rescaling here.
        }
        public EventContext Context(ProactiveNPCController actor)
        {
            var learner = actor != null ? actor.GetComponent<LearningNpc>() : null;
            var location = actor != null && actor.MeetingLocation != "" ? actor.MeetingLocation : SceneContexts.Location(learner != null ? learner.SceneId : sceneName);
            var clock = slice?.Save?.Clock;
            var memory = actor == null ? null : NPCMemoryService.Of(State, actor.Id);
            return new EventContext { Location = location, Band = Band,
                Hour = clock != null ? clock.MinuteOfDay / 60 : Mathf.FloorToInt(SkyState.Hour),
                Raining = clock != null ? clock.Weather == Weather.Rain : SkyState.Raining,
                Familiar = memory != null && memory.Relationship > 0,
                Activity = actor?.GetComponent<LivingNPC>()?.Activity ?? "exploring",
            };
        }
        public bool Available()
        {
            var availability = new InteractionAvailability {
                Menu = SettingsOpen || LifeUI.PanelOpen || (InteractionManager.Instance != null && InteractionManager.Instance.Modal),
                Conversation = Player == null || Player.Blocked || Conversation.Open,
                VehicleTransition = Player != null && Player.LocomotionOverride != null && Player.LocomotionOverride.Active,
                Swimming = Player != null && Player.LocomotionOverride != null && Player.LocomotionOverride.Active,
                Cutscene = ScenarioManager.Instance != null && ScenarioManager.Instance.Active,
                MockExam = State.Settings.Mode == GameMode.MockExam || slice != null && slice.LearningExamActive,
                QuestConflict = slice != null && !slice.CanStartStory || city != null && !city.CanStartStory
                    || DynamicLifeEventManager.Instance != null && (DynamicLifeEventManager.Instance.DialogueOpen || DynamicLifeEventManager.Instance.JournalOpen),
            };
            return CharacterProfiles.Exists && availability.CanOffer;
        }
        private void Update()
        {
            if (Player == null)
            {
                if (Time.unscaledTime >= nextBind) { nextBind = Time.unscaledTime + 2; BindScene(); }
                return;
            }
            if (preferences.WasPressedThisFrame()) ui.ToggleSettings();
            if (Conversation.Open)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Player.Controls.AlwaysLook = false;
                Player.Blocked = true; Player.Controls.Suspended = true;
                if (Player.Controls.Cancel) { ConsumedFrame = Time.frameCount; Close(); }
                return;
            }
            if (Offered != null)
            {
                if (!Available() || Time.unscaledTime > expires || (Offered.transform.position - Player.transform.position).sqrMagnitude > 36) { Decline(); return; }
                if (Player.Controls.Interact || Player.Controls.Talk) { ConsumedFrame = Time.frameCount; Accept(false); }
                return;
            }
            if (Time.unscaledTime < nextScan || !Available()) return;
            nextScan = Time.unscaledTime + 2;
            // Bounded scene cache and a two-second scan; no per-frame scene searches or AI calls.
            foreach (var npc in actors.Where(n => n != null && n.Available).OrderBy(n => (n.transform.position - Player.transform.position).sqrMagnitude))
            {
                if (!npc.Detector.ClearPath(Player, 5)) continue;
                if (State.Run != null)
                {
                    if (npc.Id == State.Run.NpcId && cooldown.Ready(npc.Id, State.Run.Id, DateTime.UtcNow, true)
                        && (!State.Run.WaitingForTransfer || Context(npc).Location == State.Run.TargetLocation))
                    { Offer(npc, Quest.Definition(State.Run.Id), true); break; }
                    continue;
                }
                var definition = selector.Select(Context(npc), npc.Definition, State, false);
                if (definition != null && cooldown.Ready(npc.Id, definition.Id, DateTime.UtcNow)) { Offer(npc, definition); break; }
                if (Time.unscaledTime > ambientAfter && State.Settings.Frequency != InteractionFrequency.Off)
                {
                    ambientAfter = Time.unscaledTime + 40;
                    var line = ContextualEventSelector.Ambient(Context(npc));
                    ui.Ambient(npc.DisplayName, line); Conversation.Ambient(line);
                    break;
                }
            }
        }
        public void Contact(ProactiveNPCController npc, ContactEvidence contact)
        {
            if (!CollisionPolicy.Candidate(contact) || !Available() || Offered != null || !cooldown.Ready(npc.Id, "collision", DateTime.UtcNow)) return;
            Offer(npc, Quest.Definition("collision"));
        }
        private void Offer(ProactiveNPCController npc, LifeEventDefinition definition, bool resume = false)
        {
            if (Offered != null || !Available()) return;
            oldLook = Player.Controls.AlwaysLook; oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
            Offered = npc; offeredEvent = definition; expires = Time.unscaledTime + 12;
            npc.Hold(Player.transform, definition.Id);
            if (definition.Id != "collision" && (npc.transform.position - Player.transform.position).sqrMagnitude > 6)
                npc.Approach(Player.transform);
            cooldown.Reserve(npc.Id, definition.Id, DateTime.UtcNow, SceneContexts.Density(Context(npc).Location), npc.Definition.FrequencyMultiplier);
            OfferLine = resume ? "Good to see you! Shall we pick up where we left off?" : definition.Prompt;
            if (definition.Prop != "" && !resume) npc.Reaction.CarryAndDrop(definition.Prop);
            Conversation.Ambient(OfferLine); Persist(); ui.Refresh();
        }
        public void Accept(bool record)
        {
            if (Offered == null || !Available()) return;
            var candidate = Offered;
            if (State.Run == null && !Quest.Begin(offeredEvent.Id, candidate.Id, Context(candidate).Location, DateTime.UtcNow)) return;
            if (State.Run.WaitingForTransfer && !Quest.Resume(Context(candidate).Location)) return;
            State.Run.DisplayName = candidate.DisplayName;
            State.Run.ModelName = candidate.GetComponentInChildren<AuthoredMotion>()?.ModelName ?? "TrenchElder";
            Actor = candidate; Offered = null;
            oldBlocked = Player.Blocked; oldSuspended = Player.Controls.Suspended;
            Player.Blocked = true; Player.Controls.Suspended = true; Player.Route.Clear();
            Player.Activity = PlayerAnimState.Talk; Player.ConversationPartner = Actor.transform;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Player.Controls.AlwaysLook = false;
            ConsumedFrame = Time.frameCount; Conversation.Begin(this); Persist(); ui.Refresh();
            if (record) Conversation.Record();
        }
        public void Decline()
        {
            if (Offered != null)
            {
                Offered.Release(); ArrangeMeeting(Offered);
                if (Player != null) Player.Controls.AlwaysLook = oldLook;
                Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
            }
            Offered = null; offeredEvent = null;
            nextScan = Time.unscaledTime + 8; ui?.Refresh();
        }
        public void Close()
        {
            if (Conversation == null) return;
            var held = Conversation.Open;
            Conversation.Close();
            if (Actor != null) { Actor.Release(); ArrangeMeeting(Actor); }
            Actor = null;
            if (held && Player != null)
            {
                Player.Blocked = oldBlocked; Player.Controls.Suspended = oldSuspended;
                Player.Activity = PlayerAnimState.Idle; Player.ConversationPartner = null; Player.Controls.Clear();
                Player.Controls.AlwaysLook = oldLook;
                Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
            }
            Decline(); Persist();
        }
        private Vector3? MeetingPoint()
        {
            var cafe = FindObjectsByType<LearningNpc>(FindObjectsSortMode.None).FirstOrDefault(n => SceneContexts.Location(n.SceneId) == "cafe");
            if (cafe != null) return cafe.Arrival + Vector3.right * 1.5f;
            if (SceneContexts.Location(sceneName) == "cafe" && Player != null) return Player.transform.position + Player.transform.right * 3;
            return null;
        }
        private void ArrangeMeeting(ProactiveNPCController npc)
        {
            if (Player == null || State.Run == null || State.Run.NpcId != npc.Id || State.Run.TargetLocation != "cafe") return;
            var point = MeetingPoint();
            if (point.HasValue) npc.MeetAt(point.Value, Player.transform, "cafe");
        }
        private void RestoreMeeting()
        {
            if (State.Run == null || State.Run.TargetLocation != "cafe") return;
            var actor = actors.FirstOrDefault(n => n.Id == State.Run.NpcId);
            if (actor == null && MeetingPoint().HasValue)
            {
                var prefab = Resources.Load<GameObject>("LivingWorld/Actors/" + State.Run.ModelName);
                if (prefab == null) { Debug.LogWarning("Proactive reunion needs actor model: " + State.Run.ModelName); return; }
                var point = MeetingPoint().Value;
                if (NavMesh.SamplePosition(point, out var ground, 3, NavMesh.AllAreas)) point = ground.position;
                var root = Instantiate(prefab, point, Quaternion.identity);
                root.name = "Returning visitor · " + State.Run.DisplayName;
                actor = root.AddComponent<ProactiveNPCController>();
                actor.Social = new NPCSocialProfile { Id = State.Run.NpcId, Name = State.Run.DisplayName, Occupation = "Returning visitor" };
                EnsureBody(root); actors = actors.Concat(new[] { actor }).ToArray();
            }
            if (actor != null) ArrangeMeeting(actor);
        }
        public void ReconcileWorld()
        {
            if (Actor == null) return;
            if (State.Run == null || State.Run.Step >= 2) Actor.Reaction.PickUp();
            // Existing relationship memory receives a bounded authored fact; no transcript or LLM command.
            var memory = NPCMemoryService.Of(State, Actor.Id);
            var delta = Math.Max(0, memory.Relationship - memory.MirroredRelationship);
            if (delta > 0) NPCRelationshipMemory.Relate(Actor.Id, delta, "Helped during a verified life encounter.");
            memory.MirroredRelationship = memory.Relationship;
            NPCRelationshipMemory.Remember(Actor.Id, "proactive-help", "Met during a life encounter.");
            NPCRelationshipMemory.Save();
        }
        public void ResumeNearby()
        {
            SettingsOpen = false;
            if (State.Run == null || !Available()) return;
            var npc = actors.FirstOrDefault(n => n != null && n.Id == State.Run.NpcId && n.Detector.ClearPath(Player, 6));
            if (npc != null) Offer(npc, Quest.Definition(State.Run.Id), true);
        }
        public void SetFrequency(InteractionFrequency frequency) { State.Settings.Frequency = frequency; Decline(); Persist(); }
        public void SetMode(GameMode mode) { if (Conversation.Open) return; State.Settings.Mode = mode; Persist(); }
        private void OnApplicationQuit() { Persist(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            preferences?.Dispose();
            Close(); SceneManager.activeSceneChanged -= SceneChanged; Instance = null;
            if (ui != null) Destroy(ui.gameObject);
        }
    }
}
