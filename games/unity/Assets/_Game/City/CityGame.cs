using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using Screen = ContextSpeak.Life.ToolkitScreen;
using ContextSpeak.Cafe.UI;
using ContextSpeak.Life;
using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.City
{
    [Serializable]
    public sealed class CitySave
    {
        public int band = 5;
        public List<SceneLesson> lessons = new List<SceneLesson>();
        public List<float> scores = new List<float>();
        public List<string> finished = new List<string>();
    }

    /// Metro City: walk the streets, go into the home, school, hospital, bank, café and restaurant,
    /// and talk to the people working there. Every conversation follows the learner's IELTS band
    /// (LifeCurriculum), answers are spoken (local speech companion) and judged by the free,
    /// rule-based SpeakingCoach — no paid AI service is involved.
    public sealed class CityGame : MonoBehaviour
    {
        public CafePlayer Player;
        public string StreetName = "Main Street";

        private const string SaveKey = "contextspeak-metro-city-v1";
        private CitySave save;
        private LearningNpc[] npcs = new LearningNpc[0];
        private CafeClient client;
        private CafeRecorder recorder;
        private NpcVoice voice;
        private LearningNpc partner;
        private SceneLesson lesson;
        private SpeakingResult review;
        private string npcLine = "", heard = "", hint = "", status = "Connecting to the local speech companion…";
        private int attempts, epoch;
        private bool working, showPlaces, showProfile, ownsPanelBlock;
        private ToolkitView toolkit;
        private int drivingHintVisits;
        private Transform marker;
        private CityTransit[] transits = new CityTransit[0];
        private CityTransit riding;
        private float exitSide = 1;
        private CityDrivable[] cars = new CityDrivable[0];
        private CityDrivable driving;
        private CityEmotes emotes;
        private RoomClient room;
        private bool showRoom;
        private string roomCode = "";
        private ContextSpeak.Life.PortalDoor[] portals = new ContextSpeak.Life.PortalDoor[0];

        private IeltsBand Band => (IeltsBand)Mathf.Clamp(save.band, 3, 9);
        private bool Talking => partner != null;
        private bool PhoneOpen => showProfile || showPlaces || showRoom;
        public bool CanStartStory => save != null && !Talking && !working && riding == null && driving == null && !PhoneOpen
            && recorder != null && recorder.State != "recording" && recorder.State != "requesting";

        // ------------------------------------------------------------------ lifecycle
        private void OnEnable() { toolkit = ToolkitView.Attach(this, DrawToolkit); }
        private void OnDisable()
        {
            if (ownsPanelBlock && Player != null) Player.Blocked = Talking || riding != null || driving != null;
            ownsPanelBlock = false;
        }

        private void SyncPanelInput()
        {
            if (Player == null) return;
            if (PhoneOpen) { ownsPanelBlock = true; Player.Blocked = true; }
            else if (ownsPanelBlock)
            {
                ownsPanelBlock = false;
                Player.Blocked = Talking || riding != null || driving != null;
                Player.Controls?.Clear();
            }
        }

        private void Start()
        {
            save = Load();
            npcs = FindObjectsByType<LearningNpc>(FindObjectsSortMode.None).OrderBy(npc => npc.name).ToArray();
            transits = FindObjectsByType<CityTransit>(FindObjectsSortMode.None);
            cars = FindObjectsByType<CityDrivable>(FindObjectsSortMode.None);
            emotes = Player.Model != null ? Player.Model.GetComponent<CityEmotes>() : null;
            gameObject.AddComponent<CityAmbience>();
            portals = FindObjectsByType<ContextSpeak.Life.PortalDoor>(FindObjectsSortMode.None);
            // Coming back out of the hospital, restaurant or café: stand just outside the door used.
            if (ContextSpeak.Life.SceneTransitionManager.TakeReturnPoint("MetroCity", out var back, out var yaw))
            {
                var facing = Quaternion.Euler(0, yaw + 180, 0) * Vector3.forward;
                Player.Place(back + facing * 1.4f + Vector3.up * 0.1f);
                Player.Face(facing);
            }
            room = gameObject.AddComponent<RoomClient>();
            room.Player = Player;
            room.World = "MetroCity";
            room.Capture = () => {
                var position = Player.transform.position;
                return new RoomPose {
                    x = Mathf.Clamp(position.x, -129, 129), y = Mathf.Clamp(position.y, -4, 19), z = Mathf.Clamp(position.z, -129, 129),
                    yaw = Player.Model != null ? Player.Model.eulerAngles.y : 0,
                    moving = Player.Speed > 0.15f || (driving != null && Mathf.Abs(driving.Speed) > 0.5f),
                    activity = recorder.State == "recording" ? "recording" : Talking ? "talking" : "exploring",
                    npc = partner != null ? partner.DisplayName : "",
                };
            };
            client = gameObject.AddComponent<CafeClient>();
            recorder = gameObject.AddComponent<CafeRecorder>();
            voice = gameObject.AddComponent<NpcVoice>();
            recorder.Finished += Transcribe;
            recorder.Failed += error => { status = error; };
            Connect();
            var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gem.name = "Next conversation marker";
            Destroy(gem.GetComponent<Collider>());
            gem.transform.localScale = Vector3.one * 0.3f;
            var renderer = gem.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.95f, 0.55f, 0.12f);
            marker = gem.transform;
        }

        private void Connect()
        {
            StartCoroutine(client.Connect(error => {
                status = error == "" ? "Speech companion connected · press R to answer" : "Speech is offline. In the ContextSpeak folder run: pnpm world:dev (free, local), then press C.";
            }));
        }

        private void Update()
        {
            if (ContextSpeak.Proactive.ProactiveLifeDirector.OwnsInput) return;
            if (Player == null || save == null) return;
            var keyboard = Keyboard.current;
            var controls = Player.Controls;
            if (emotes != null) emotes.Airborne = !Player.Grounded && riding == null && driving == null;
            if (riding != null)
            {
                if (controls != null && (controls.Interact || controls.Talk) && riding.Dwelling) Alight();
                MoveMarker();
                return;
            }
            if (driving != null)
            {
                var movement = controls != null ? controls.Movement : Vector2.zero;
                driving.Drive(movement.y, movement.x, Time.deltaTime);
                if (controls != null && (controls.Interact || controls.Talk) && Mathf.Abs(driving.Speed) < 1.5f) GetOut();
                MoveMarker();
                return;
            }
            if (keyboard != null && !Talking && keyboard.escapeKey.wasPressedThisFrame && PhoneOpen)
            {
                if (showRoom) { showRoom = false; showProfile = true; }
                else if (showPlaces) { showPlaces = false; showProfile = true; }
                else showProfile = false;
                SyncPanelInput();
                return;
            }
            if (!Talking && (toolkit == null || !toolkit.TextInputFocused) && keyboard != null)
            {
                if (keyboard.oKey.wasPressedThisFrame) { showRoom = !showRoom; showPlaces = false; showProfile = false; }
                if (keyboard.pKey.wasPressedThisFrame) { showPlaces = !showPlaces; showRoom = false; showProfile = false; }
                if (keyboard.tabKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame) { showProfile = !showProfile; showRoom = false; showPlaces = false; }
            }
            SyncPanelInput();
            if (PhoneOpen)
            {
                UpdateActors();
                MoveMarker();
                return;
            }
            if (keyboard != null && !Talking && !showRoom)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) emotes?.Play(CityEmotes.Gesture.Wave);
                if (keyboard.digit2Key.wasPressedThisFrame) emotes?.Play(CityEmotes.Gesture.Cheer);
                if (keyboard.digit3Key.wasPressedThisFrame) emotes?.Play(CityEmotes.Gesture.Dance);
                if (keyboard.digit4Key.wasPressedThisFrame) emotes?.Play(CityEmotes.Gesture.Bow);
                if (keyboard.digit5Key.wasPressedThisFrame) emotes?.Play(CityEmotes.Gesture.Clap);
                if (Player.Speed > 0.5f && emotes != null && emotes.Current == CityEmotes.Gesture.Dance) emotes.Stop();
            }
            if (Talking)
            {
                if (controls != null && controls.Cancel) Close();
                else if (keyboard != null)
                {
                    if (keyboard.rKey.wasPressedThisFrame) Record();
                    if (keyboard.hKey.wasPressedThisFrame) Say(npcLine);
                    if (keyboard.nKey.wasPressedThisFrame) Skip();
                }
            }
            else
            {
                if (controls != null && (controls.Interact || controls.Talk))
                {
                    var vehicle = Boardable();
                    var car = Drivable();
                    var portal = Portal();
                    if (portal != null && Nearest() == null)
                    {
                        if (!ContextSpeak.Life.SceneTransitionManager.Enter(portal.TargetScene, portal.Entrance, Player.transform))
                            status = ContextSpeak.Life.SceneTransitionManager.LastError;
                    }
                    else if (vehicle != null && Nearest() == null) Board(vehicle);
                    else if (car != null && Nearest() == null) GetIn(car);
                    else Open(Nearest());
                }
                if (keyboard != null && !showRoom)
                {
                    if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) SetBand(save.band + 1);
                    if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) SetBand(save.band - 1);
                    if (keyboard.cKey.wasPressedThisFrame && !client.Connected) Connect();
                }
            }
            UpdateActors();
            MoveMarker();
        }

        // ------------------------------------------------------------------ public transport
        private CityTransit Boardable()
        {
            foreach (var vehicle in transits)
                if (vehicle != null && vehicle.Dwelling && vehicle.Beside(Player.transform.position)) return vehicle;
            return null;
        }

        private void Board(CityTransit vehicle)
        {
            riding = vehicle;
            exitSide = vehicle.transform.InverseTransformPoint(Player.transform.position).x >= 0 ? 1 : -1;
            vehicle.Riding = true;
            vehicle.SetSolid(false);
            Player.Blocked = true;
            if (Player.Model != null) Player.Model.gameObject.SetActive(false);
            status = "You boarded " + vehicle.LineName + ".";
        }

        private void Alight()
        {
            var vehicle = riding;
            riding = null;
            var offset = vehicle.ExitOffset;
            if (vehicle.BothSides) offset.x = Mathf.Abs(offset.x) * exitSide;
            Player.Place(vehicle.transform.TransformPoint(offset) + Vector3.up * 0.2f);
            vehicle.Riding = false;
            vehicle.SetSolid(true);
            Player.Blocked = false;
            if (Player.Model != null) Player.Model.gameObject.SetActive(true);
            Player.Controls?.Clear();
            status = "You got off at " + vehicle.StopName + ".";
        }

        private CityDrivable Drivable()
        {
            foreach (var car in cars)
            {
                if (car == null) continue;
                var collider = car.GetComponent<Collider>();
                var point = collider != null ? collider.ClosestPoint(Player.transform.position + Vector3.up * 0.8f) : car.transform.position;
                if (Vector3.Distance(point, Player.transform.position + Vector3.up * 0.8f) < 2.2f) return car;
            }
            return null;
        }

        private void GetIn(CityDrivable car)
        {
            driving = car;
            drivingHintVisits++;
            car.Stop();
            Ignore(car, true);
            Player.Blocked = true;
            if (Player.Model != null) Player.Model.gameObject.SetActive(false);
            emotes?.Stop();
            status = "W/S accelerate and brake · A/D steer · E to get out when stopped.";
        }

        private void GetOut()
        {
            var car = driving;
            driving = null;
            car.Stop();
            Player.Place(car.transform.TransformPoint(car.ExitLocal));
            Ignore(car, false);
            Player.Blocked = false;
            if (Player.Model != null) Player.Model.gameObject.SetActive(true);
            Player.Controls?.Clear();
            status = "You parked the " + car.Label + ".";
        }

        private void Ignore(CityDrivable car, bool ignore)
        {
            var body = Player.GetComponent<CharacterController>();
            if (body == null) return;
            foreach (var collider in car.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(body, collider, ignore);
        }

        private void LateUpdate()
        {
            if (riding != null && riding.Seat != null) Player.Place(riding.Seat.position);
            if (driving != null)
            {
                Player.Place(driving.transform.TransformPoint(driving.SeatLocal));
                Player.Face(driving.transform.forward);
            }
        }

        // ------------------------------------------------------------------ conversations
        private ContextSpeak.Life.PortalDoor Portal()
        {
            foreach (var portal in portals)
                if (portal != null && Vector3.Distance(portal.transform.position, Player.transform.position + Vector3.up) < portal.Range) return portal;
            return null;
        }

        private LearningNpc Nearest()
        {
            LearningNpc best = null;
            var distance = 2.8f;
            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                var candidate = Vector3.Distance(Player.transform.position, npc.transform.position);
                if (candidate < distance) { distance = candidate; best = npc; }
            }
            return best;
        }

        private SceneLesson LessonFor(LearningNpc npc)
        {
            var existing = save.lessons.FirstOrDefault(item => item.SceneId == npc.SceneId && item.Band == Band);
            if (existing != null) return existing;
            var created = new SceneLesson { SceneId = npc.SceneId, Band = Band, Step = 0 };
            save.lessons.Add(created);
            return created;
        }

        private void Open(LearningNpc npc)
        {
            if (npc == null || working) return;
            partner = npc;
            lesson = LessonFor(npc);
            attempts = 0;
            heard = hint = "";
            review = null;
            var greeting = lesson.Step == 0 && !lesson.Complete ? "Hi, I'm " + npc.DisplayName + ". " : "";
            npcLine = greeting + lesson.Prompt;
            Player.Blocked = true;
            Player.ConversationPartner = npc.transform;
            if (Player.Model != null) Player.Model.gameObject.SetActive(false);
            var toPlayer = Player.transform.position - npc.transform.position;
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude > 0.01f) npc.transform.rotation = Quaternion.LookRotation(toPlayer);
            Say(npcLine);
        }

        private void Close()
        {
            epoch++;
            working = false;
            if (recorder != null) recorder.Cancel();
            if (voice != null) voice.Cancel();
            if (client != null) client.Cancel();
            partner = null;
            lesson = null;
            Player.Blocked = false;
            Player.ConversationPartner = null;
            if (Player.Model != null) Player.Model.gameObject.SetActive(true);
            Player.Controls?.Clear();
            Save();
        }

        private void Say(string line)
        {
            if (string.IsNullOrEmpty(line) || client == null || !client.Connected || recorder.State != "idle") return;
            voice.Say(client, line, error => { status = error; });
        }

        private void Record()
        {
            if (!Talking || working || lesson == null || lesson.Complete) return;
            if (recorder.State == "recording") { recorder.Stop(); return; }
            if (recorder.State == "requesting") { recorder.Cancel(); return; }
            if (!client.Connected)
            {
                status = "Speech is offline. Run pnpm world:dev in the ContextSpeak folder (free, local), then press C.";
                Connect();
                return;
            }
            voice.Cancel();
            recorder.Begin();
        }

        private void Transcribe(byte[] wave)
        {
            if (!Talking) return;
            var current = epoch;
            working = true;
            status = "Listening back to your answer…";
            StartCoroutine(client.Transcribe(wave, text => {
                working = false;
                if (current == epoch && Talking) Evaluate(text);
            }, error => {
                working = false;
                if (current == epoch) status = error;
            }));
        }

        private void Evaluate(string text)
        {
            heard = text.Trim();
            var answer = lesson.Propose(heard);
            review = SpeakingCoach.Review(heard, lesson.Band, answer.Accepted, true);
            var gate = SpeakingCoach.Decide(BandProfile.For(lesson.Band).DefaultStrictness, review, attempts);
            save.scores.Add((float)review.Score);
            while (save.scores.Count > 12) save.scores.RemoveAt(0);
            if (answer.Accepted && gate != Gate.RequireRetry)
            {
                lesson.Accept(answer);
                attempts = 0;
                hint = review.Corrected != "" ? "Good answer. Even better: “" + review.Corrected + "”" : "";
                if (lesson.Complete)
                {
                    if (!save.finished.Contains(lesson.SceneId + "@" + (int)lesson.Band)) save.finished.Add(lesson.SceneId + "@" + (int)lesson.Band);
                    npcLine = Praise() + " That's all from me. Thanks for chatting!";
                }
                else npcLine = Praise() + " " + lesson.Prompt;
                status = "Answer accepted.";
                Say(npcLine);
            }
            else
            {
                attempts++;
                hint = answer.Accepted && review.Corrected != ""
                    ? "Nearly! Say it again with the correction: “" + review.Corrected + "”"
                    : answer.Hint;
                if (attempts >= 3) hint += "  (Stuck? Press N to move on.)";
                status = "Try again — press R.";
            }
            Save();
        }

        private string Praise()
        {
            var options = Band <= IeltsBand.Band4 ? new[] { "Great!", "Good job!", "Nice!" }
                : Band <= IeltsBand.Band6 ? new[] { "That makes sense.", "Good point.", "I see what you mean." }
                : new[] { "That's a thoughtful answer.", "Interesting perspective.", "Fair point — well argued." };
            return options[UnityEngine.Random.Range(0, options.Length)];
        }

        private void Skip()
        {
            if (!Talking || lesson == null || lesson.Complete) return;
            lesson.Step++;
            attempts = 0;
            hint = heard = "";
            review = null;
            npcLine = lesson.Complete ? "Let's leave it there for today. See you next time!" : "No problem, let's try another one. " + lesson.Prompt;
            Say(npcLine);
            Save();
        }

        private void SetBand(int band)
        {
            band = Mathf.Clamp(band, 3, 9);
            if (band == save.band) return;
            if (Talking) Close();
            save.band = band;
            save.scores.Clear();
            Save();
        }

        // ------------------------------------------------------------------ world feedback
        private bool Done(LearningNpc npc) => save.finished.Contains(npc.SceneId + "@" + save.band);

        private void UpdateActors()
        {
            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                var actor = npc.GetComponent<NpcActor>();
                if (actor == null) continue;
                var state = npc != partner ? NpcState.Idle
                    : working ? NpcState.Thinking
                    : recorder.State != "idle" ? NpcState.Listening
                    : voice.Playing ? NpcState.Talking : NpcState.Listening;
                actor.SetState(state);
            }
        }

        private void MoveMarker()
        {
            if (marker == null) return;
            LearningNpc next = null;
            var distance = float.MaxValue;
            if (!Talking)
                foreach (var npc in npcs)
                {
                    if (npc == null || Done(npc)) continue;
                    var candidate = Vector3.Distance(Player.transform.position, npc.transform.position);
                    if (candidate < distance) { distance = candidate; next = npc; }
                }
            marker.gameObject.SetActive(next != null);
            if (next == null) return;
            marker.position = next.transform.position + Vector3.up * (2.3f + Mathf.Sin(Time.time * 2.4f) * 0.1f);
            marker.rotation = Quaternion.Euler(45, Time.time * 90f, 45);
        }

        // ------------------------------------------------------------------ save
        private static CitySave Load()
        {
            var json = PlayerPrefs.GetString(SaveKey, "");
            if (json != "")
                try
                {
                    var loaded = JsonUtility.FromJson<CitySave>(json);
                    if (loaded != null && loaded.lessons != null && loaded.scores != null && loaded.finished != null) return loaded;
                }
                catch (ArgumentException) { }
            return new CitySave();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            if (save != null) Save();
            if (marker != null) Destroy(marker.gameObject);
            if (dark != null) Destroy(dark);
            if (paper != null) Destroy(paper);
        }

        // ------------------------------------------------------------------ interface
        private GUIStyle panel, title, body, small, line, button, chip;
        private Texture2D dark, paper;

        private void Styles()
        {
            if (panel != null) return;
            dark = new Texture2D(1, 1); dark.SetPixel(0, 0, new Color(0.07f, 0.09f, 0.12f, 0.86f)); dark.Apply();
            paper = new Texture2D(1, 1); paper.SetPixel(0, 0, new Color(0.96f, 0.93f, 0.86f, 0.97f)); paper.Apply();
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14) };
            panel.normal.background = dark;
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            title.normal.textColor = new Color(1f, 0.86f, 0.55f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true };
            body.normal.textColor = new Color(0.94f, 0.95f, 0.97f);
            small = new GUIStyle(body) { fontSize = 14 };
            small.normal.textColor = new Color(0.75f, 0.8f, 0.86f);
            line = new GUIStyle(GUI.skin.box) { fontSize = 21, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(16, 16, 12, 12), richText = true };
            line.normal.background = paper;
            line.normal.textColor = new Color(0.12f, 0.12f, 0.14f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 36 };
            chip = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedWidth = 34, fixedHeight = 30 };
        }

        private void DrawToolkit()
        {
            if (save == null || Player == null) return;
            Styles();
            var width = Screen.width;
            var height = Screen.height;
            var profile = BandProfile.For(Band);

            var place = CityBuilding.Current;
            if (!Talking && !PhoneOpen && UiPreferences.Current.Hud != HudMode.Off)
            {
                GUI.Box(new Rect(20, 16, 300, 32), SkyState.Clock + "  ·  " + SkyState.Weather, panel);
                GUI.Box(new Rect(20, 56, 460, 48), "Find a conversation · " + (place != null ? place.Title : StreetName), panel);
            }

            // Level, progress and accessibility live in the phone, opened with Tab or F1.
            if (showProfile)
            {
                GUILayout.BeginArea(new Rect(width - 464, 24, 440, height - 48), panel);
                GUILayout.Label("METRO CITY", title);
                GUILayout.Label(place != null ? place.Title + (place.TitleZh != "" ? "  " + place.TitleZh : "") : StreetName, body);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Level", small, GUILayout.Width(44));
                for (var band = 3; band <= 9; band++)
                {
                    var selected = band == save.band;
                    var previous = GUI.color;
                    if (selected) GUI.color = new Color(1f, 0.75f, 0.35f);
                    if (GUILayout.Button(band.ToString(), chip)) SetBand(band);
                    GUI.color = previous;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(profile.Label + " · " + profile.Summary, small);
                var finished = npcs.Count(Done);
                GUILayout.Label("Conversations at this level: " + finished + " / " + npcs.Length, small);
                var suggestion = BandProfile.Adjust(Band, save.scores.Count == 0 ? 0.6 : save.scores.Average(), save.scores.Count);
                if (suggestion != Band && GUILayout.Button("Suggested level: " + BandProfile.For(suggestion).Label, button)) SetBand((int)suggestion);
                GUILayout.Embed("city-accessibility", UiPreferences.AddControls);
                GUILayout.Label(status, small);
                if (!client.Connected && GUILayout.Button("Reconnect speech", button)) Connect();
                if (GUILayout.Button("People", button)) { showPlaces = true; showProfile = false; }
                if (GUILayout.Button("Friends", button)) { showRoom = true; showProfile = false; }
                if (GUILayout.Button("Close · Esc", button)) { showProfile = false; SyncPanelInput(); }
                GUILayout.EndArea();
            }

            // People are contextual phone content, hidden by default.
            if (showPlaces && !Talking && riding == null)
            {
                GUILayout.BeginArea(new Rect(width - 464, 24, 440, height - 48), panel);
                GUILayout.Label("Phone · People", title);
                if (GUILayout.Button("Back · Esc", button)) { showPlaces = false; showProfile = true; }
                foreach (var npc in npcs)
                    GUILayout.Label((Done(npc) ? "✓ " : "• ") + npc.DisplayName + " — " + npc.DisplayRole + " · " + npc.Place.Name, small);
                GUILayout.EndArea();
            }

            if (showRoom && room != null)
            {
                GUILayout.BeginArea(new Rect(width - 464, 24, 440, height - 48), panel);
                GUILayout.Label("Phone · Friends", title);
                if (GUILayout.Button("Back · Esc", button)) { showRoom = false; showProfile = true; }
                GUILayout.Label("Connect with a room code.", small);
                GUILayout.Label(room.Status, small);
                if (room.Connected)
                {
                    GUILayout.Label("Room code: " + room.Code + "   ·   " + room.Count + " in room", body);
                    if (GUILayout.Button("Leave room", button)) room.Leave();
                }
                else
                {
                    GUI.enabled = room.Ready && !room.Busy;
                    if (GUILayout.Button("Create a room", button)) room.Join();
                    roomCode = GUILayout.TextField(roomCode, 12);
                    if (GUILayout.Button("Join with code", button)) room.Join(roomCode);
                    GUI.enabled = true;
                }
                GUILayout.EndArea();
            }

            SyncPanelInput();

            // Contextual transit, driving, prompts and the spoken conversation.
            if (driving != null)
            {
                var kmh = Mathf.RoundToInt(Mathf.Abs(driving.Speed) * 3.6f);
                GUI.Label(new Rect(width / 2 - 420, height - 110, 840, 40), "Driving the " + driving.Label + " · " + kmh + " km/h", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
                if (drivingHintVisits <= 3) GUI.Label(new Rect(width / 2 - 360, height - 66, 720, 26), "W/S accelerate · brake · reverse    A/D steer    E get out (when stopped)", small);
                return;
            }
            if (riding != null)
            {
                var ride = riding.Dwelling
                    ? riding.LineName + " · " + riding.StopName + " — doors open. Press E to get off here."
                    : riding.LineName + " · Next stop: " + riding.NextStopName;
                GUI.Label(new Rect(width / 2 - 420, height - 110, 840, 40), ride, new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(width / 2 - 360, height - 66, 720, 26), status, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                return;
            }
            if (!Talking)
            {
                var near = Nearest();
                var vehicle = Boardable();
                var car = Drivable();
                var portal = Portal();
                var text = portal != null && near == null ? "E · " + portal.Prompt
                    : vehicle != null && near == null ? "E · Board " + vehicle.LineName + " (next stop: " + vehicle.NextStopName + ")"
                    : car != null && near == null ? "E · Drive the " + car.Label
                    : near != null ? "E · Talk to " + near.DisplayName : "";
                var target = near != null ? near.transform : portal != null ? portal.transform : vehicle != null ? vehicle.transform : car != null ? car.transform : null;
                if (!PhoneOpen && text != "" && target != null) DrawPrompt(text, target, width, height);
                return;
            }

            var plan = lesson.Plan;
            var boxWidth = Mathf.Min(960, width - 40);
            GUILayout.BeginSubtitleArea(new Rect(width / 2 - boxWidth / 2, Mathf.Max(24, height - 410), boxWidth, Mathf.Min(394, height - 48)));
            GUILayout.Label(partner.DisplayName + " · " + partner.DisplayRole + "     " + profile.Label + "   question " + Mathf.Min(lesson.Step + 1, 3) + "/3", title);
            GUILayout.Subtitle(npcLine);
            GUILayout.Label("Practice feedback only · not an official IELTS score. Pronunciation is not assessed.", small);
            GUILayout.Label("<b>Goal:</b> " + plan.Goal + "    <b>Useful words:</b> " + string.Join(", ", plan.Vocabulary), small);
            if (heard != "") GUILayout.Label("<b>You said:</b> " + heard, body);
            if (hint != "") GUILayout.Label("<color=#ffd27a>" + hint + "</color>", body);
            if (review != null)
            {
                var notes = review.Criteria.Where(note => note.Level != CriterionLevel.NotAssessed)
                    .Select(note => BandProfile.CriterionName(note.Criterion) + ": " + Level(note.Level) + " — " + note.Note);
                GUILayout.Label(string.Join("\n", notes), small);
            }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var recordLabel = recorder.State == "recording" ? "■ Stop (R)" : recorder.State == "requesting" ? "Waiting for mic…" : working ? "Checking…" : "● Speak your answer (R)";
            GUI.enabled = !lesson.Complete && !working;
            if (GUILayout.Button(recordLabel, button, GUILayout.Width(260))) Record();
            GUI.enabled = true;
            if (GUILayout.Button("Hear again (H)", button, GUILayout.Width(160))) Say(npcLine);
            GUI.enabled = !lesson.Complete;
            if (GUILayout.Button("Next question (N)", button, GUILayout.Width(180))) Skip();
            GUI.enabled = true;
            if (GUILayout.Button("Leave (Esc)", button, GUILayout.Width(140))) Close();
            GUILayout.EndHorizontal();
            GUILayout.Label(status, small);
            GUILayout.EndArea();
        }

        private void DrawPrompt(string text, Transform target, float width, float height)
        {
            var camera = Player.View != null ? Player.View : Camera.main;
            if (camera == null) return;
            var point = camera.WorldToViewportPoint(target.position + Vector3.up * 1.7f);
            if (point.z <= 0) return;
            GUI.Box(new Rect(Mathf.Clamp(point.x * width - 140, 12, width - 292),
                Mathf.Clamp((1 - point.y) * height, 12, height - 60), 280, 44), text, body);
        }

        private static string Level(CriterionLevel level) =>
            level == CriterionLevel.AboveTarget ? "above target" : level == CriterionLevel.OnTarget ? "on target" : "developing";
    }
}
