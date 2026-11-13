using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [Serializable] public class CafeSnapshot
    {
        public string objective, nearby, speaker, line, feedback, transcript, status, recording, phase, stage;
        public bool dialogue, busy, complete, join, practice;
        public int turn, total;
        public float x, z;
    }
    [Serializable] public class BrowserCommand { public string action; public string text; }
    public sealed class CafeGame : MonoBehaviour
    {
        public CafePlayer Player;
        public Transform Maya, Alex;
        public GameObject Drinks;
        private const string SaveKey = "contextspeak-unity-cafe-v1";
        private CafeMission mission = new CafeMission();
        private CafeClient client;
        private CafeRecorder recorder;
        private CafeInterface ui;
        private NpcVoice voice;
        private int epoch;
        private bool dialogue, working, quitting;
        private string line = "", feedback = "", transcript = "", status = "Connecting local AI…";
        private float nextReport;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_State(string json);
#endif
        private void Start()
        {
            client = gameObject.AddComponent<CafeClient>();
            recorder = gameObject.AddComponent<CafeRecorder>();
            ui = gameObject.AddComponent<CafeInterface>();
            voice = gameObject.AddComponent<NpcVoice>();
            ui.Submitted += Submit;
            ui.Record += Record;
            ui.Hear += Hear;
            ui.Close += Close;
            ui.Join += JoinFriend;
            ui.Reset += NewTask;
            ui.Reconnect += Connect;
            ui.StageSelected += SelectStage;
            ui.Life += () => { Close(); WorldScenes.Open("LifeTown", Fail); };
            recorder.Finished += Transcribe;
            recorder.Failed += Fail;
            recorder.Changed += Refresh;
            mission.Restore(PlayerPrefs.GetString(SaveKey, ""));
            if (mission.Phase == "seated" || mission.Complete) Player.Place(new Vector3(-4, 0.05f, 2.8f));
            Refresh();
            Connect();
        }
        private void Connect()
        {
            StartCoroutine(client.Connect(error => { status = error == "" ? "Local AI connected" : error; Refresh(); }));
        }
        private string Near()
        {
            if (Vector3.Distance(Player.transform.position, Maya.position) < 2.9f && !mission.OrderComplete) return "Maya · Press E to order";
            if (Vector3.Distance(Player.transform.position, Alex.position) < 3 && mission.OrderComplete) return "Alex · Press E to sit and chat";
            return "";
        }
        private void Update()
        {
            if (Player.Controls.Cancel && dialogue) Close();
            if (!dialogue && !working && (Player.Controls.Interact || Player.Controls.Talk) && Near() != "") Open();
            if (Time.unscaledTime >= nextReport)
            {
                nextReport = Time.unscaledTime + 0.15f;
                Refresh();
            }
        }
        private void Open()
        {
            Player.Controls.Clear();
            epoch++;
            if (mission.OrderComplete)
            {
                if (mission.Phase == "counter") mission.BeginWalk();
                if (mission.Phase == "walking") mission.Sit();
                Player.Place(new Vector3(-4, 0.05f, 2.8f));
                line = mission.FriendLine;
            }
            else line = mission.Prompt;
            dialogue = true;
            Player.Route.Clear();
            Refresh();
            Hear();
        }
        public void Submit(string text)
        {
            text = (text ?? "").Trim();
            if (!dialogue || working || mission.Complete || recorder.State != "idle" || text.Length == 0 || text.Length > 400) return;
            StopVoice();
            client.Cancel();
            transcript = "You said: " + text;
            ui.ClearInput();
            StartCoroutine(Answer(text, epoch));
        }
        private IEnumerator Answer(string text, int current)
        {
            working = true;
            status = "Checking your reply…";
            Refresh();
            if (mission.Phase == "seated")
            {
                var failed = false;
                yield return client.Json<FriendReview>("/conversation", new Conversation { text = text, turn = mission.Turn, order = mission.Order, history = mission.History }, review => {
                    if (current != epoch) return;
                    var accepted = mission.ReviewFriend(text, review);
                    feedback = mission.Feedback;
                    line = accepted ? mission.FriendLine : feedback + " " + mission.FriendPrompt;
                }, error => { failed = true; Fail(error); });
                if (current != epoch) yield break;
                if (failed) { working = false; Refresh(); yield break; }
            }
            else
            {
                var intent = mission.Parse(text);
                if (intent.action == "unknown")
                {
                    var failed = false;
                    yield return client.Json<CafeIntent>("/interpret", new Interpretation { text = text, stage = mission.Stage }, value => intent = value, error => { failed = true; Fail(error); });
                    if (current != epoch) yield break;
                if (failed) { working = false; Refresh(); yield break; }
                }
                line = mission.Apply(intent);
                feedback = mission.OrderComplete ? "Order complete. Take your drinks to Alex's table." : $"Budget $12 · Current order ${mission.Total / 100.0:F2}";
            }
            if (current != epoch) yield break;
            working = false;
            status = mission.Complete ? "Conversation complete · communication goals achieved" : "Your turn";
            Save();
            Refresh();
            Hear();
        }
        private void Hear()
        {
            if (!dialogue || working || recorder.State != "idle" || line == "") return;
            StopVoice();
            status = "Preparing local AI voice…";
            Refresh();
            voice.Say(client, line, Fail, () => {
                status = "Listen, then reply in your own words.";
                Refresh();
            });
        }
        private void Record()
        {
            if (!dialogue || working || mission.Complete) return;
            if (recorder.State == "recording") { recorder.Stop(); return; }
            if (recorder.State == "requesting") { recorder.Cancel(); return; }
            if (!client.Connected) { Fail("Start pnpm world:dev and reconnect before recording."); return; }
            StopVoice();
            client.Cancel();
            recorder.Begin();
        }
        private void Transcribe(byte[] wave)
        {
            if (!dialogue || working) return;
            var current = epoch;
            working = true;
            status = "Transcribing your recording locally…";
            Refresh();
            StartCoroutine(client.Transcribe(wave, text => {
                if (current != epoch || !dialogue) return;
                working = false;
                Submit(text);
            }, error => { if (current == epoch) { working = false; Fail(error); } }));
        }
        private void JoinFriend()
        {
            if (!mission.OrderComplete || mission.Complete || working) return;
            var destination = new LifeNpc { standX = -4, standZ = 2.8f };
            var route = new System.Collections.Generic.List<Vector3>(LifeRoutes.To(Player.transform.position, destination));
            if (route.Count == 0)
            {
                Fail("No clear route to Alex. Move away from the counter and try Join Alex again.");
                return;
            }
            Close();
            mission.BeginWalk();
            foreach (var point in route) Player.Route.Enqueue(point);
            Save();
        }
        private void Close()
        {
            Player.Controls.Clear();
            epoch++;
            dialogue = false;
            working = false;
            StopVoice();
            if (client != null) client.Cancel();
            if (recorder != null) recorder.Cancel();
            Refresh();
        }
        private void StopVoice()
        {
            if (voice != null) voice.Cancel();
        }

        private void NewTask()
        {
            Close();
            mission = new CafeMission();
            PlayerPrefs.DeleteKey(SaveKey);
            Player.Place(new Vector3(0, 0.05f, 5));
            feedback = transcript = "";
            Refresh();
        }
        private void SelectStage(string stage)
        {
            Close();
            mission = new CafeMission();
            if (stage == "continue") mission.Restore(PlayerPrefs.GetString(SaveKey, ""));
            else mission.StartPractice(stage);
            Player.Place(mission.Phase == "seated" || mission.Complete ? new Vector3(-4, 0.05f, 2.8f) : new Vector3(3.8f, 0.05f, -2.5f));
            feedback = transcript = "";
            Refresh();
        }
        private void Save()
        {
            if (mission.Practice) return;
            PlayerPrefs.SetString(SaveKey, mission.Save());
            PlayerPrefs.Save();
        }
        private void Fail(string error)
        {
            status = error;
            Refresh();
        }
        private void Refresh()
        {
            if (quitting || ui == null || recorder == null) return;
            Player.Blocked = dialogue;
            Player.ConversationPartner = dialogue ? (mission.Phase == "seated" || mission.Complete ? Alex : Maya) : null;
            if (Drinks != null) Drinks.SetActive(mission.OrderComplete);
            var snapshot = new CafeSnapshot {
                objective = (mission.Practice ? "Practice stage · " : "") + (mission.Complete ? "Cafe visit complete!" : !mission.OrderComplete ? "Order two drinks within $12, then catch up with Alex." : mission.Phase == "counter" || mission.Phase == "walking" ? "Join Alex at the table, then press E." : "Chat with Alex · " + mission.Turn + " of 3 goals"),
                nearby = dialogue ? "" : Near(), speaker = mission.Phase == "seated" || mission.Complete ? "Alex · Your friend" : "Maya · Barista",
                line = line, feedback = feedback, transcript = transcript, status = status, recording = recorder.State,
                dialogue = dialogue, busy = working, complete = mission.Complete, join = mission.OrderComplete && mission.Phase == "counter",
                phase = mission.Phase, stage = mission.Stage, practice = mission.Practice, turn = mission.Turn, total = mission.Total,
                x = Player.transform.position.x, z = Player.transform.position.z
            };
            foreach (var actor in new[] { Maya.GetComponent<NpcActor>(), Alex.GetComponent<NpcActor>() })
            {
                if (actor == null) continue;
                var active = dialogue && actor.transform == (mission.OrderComplete ? Alex : Maya);
                actor.SetState(!active ? NpcState.Idle : working ? NpcState.Thinking
                    : voice.Playing ? NpcState.Talking : NpcState.Listening);
            }
            ui.Refresh(snapshot);
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_State(JsonUtility.ToJson(snapshot));
#endif
        }
        public void OnBrowserCommand(string json)
        {
            if (json == null || json.Length > 2000) return;
            BrowserCommand command;
            try { command = JsonUtility.FromJson<BrowserCommand>(json); } catch (ArgumentException) { return; }
            if (command == null) return;
            switch (command.action)
            {
                case "send": Submit(command.text); break;
                case "record": Record(); break;
                case "hear": Hear(); break;
                case "close": Close(); break;
                case "join": JoinFriend(); break;
                case "connect": Connect(); break;
                case "reset": NewTask(); break;
                case "stage": if (command.text == "counter" || command.text == "friend" || command.text == "continue") SelectStage(command.text); break;
                case "interact": if (!dialogue && Near() != "") Open(); break;
                case "life": Close(); WorldScenes.Open("LifeTown", Fail); break;
                case "slice": Close(); WorldScenes.Open("ForestSlice", Fail); break;
            }
        }
        private void OnApplicationFocus(bool focus)
        {
            // The OS permission sheet temporarily takes focus before capture starts.
            if (!focus && recorder != null && !recorder.WaitingForPermission) Close();
        }
        private void OnApplicationQuit()
        {
            quitting = true;
        }
        private void OnDestroy()
        {
            quitting = true;
            if (recorder != null) { recorder.Changed -= Refresh; recorder.Cancel(); }
            if (client != null) client.Cancel();
            StopVoice();
        }
    }
}
