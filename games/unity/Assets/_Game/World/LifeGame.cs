using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [Serializable] public class LifeSnapshot : CafeSnapshot
    {
        public string mode = "life", owner = "LifeRuntime", wallet, bag, menu, repair, place;
        public int step;
    }
    public sealed class LifeGame : MonoBehaviour
    {
        public CafePlayer Player;
        public Transform[] Npcs;
        public GameObject Dinner, DrinkProp;
        private const string SaveKey = "contextspeak-life-day-v1";
        private LifeMission mission;
        private LifeCatalog catalog;
        private CafeClient client;
        private CafeRecorder recorder;
        private LifeInterface ui;
        private NpcVoice voice;
        private int epoch, speakerIndex = -1;
        private bool dialogue, working, quitting;
        private string feedback = "", transcript = "", previewRepair = "", status = "Connecting local AI…";
        private float nextReport;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_State(string json);
#endif
        private void Start()
        {
            catalog = LifeCatalog.Load();
            mission = new LifeMission();
            mission.Restore(PlayerPrefs.GetString(SaveKey, ""));
            client = gameObject.AddComponent<CafeClient>();
            recorder = gameObject.AddComponent<CafeRecorder>();
            voice = gameObject.AddComponent<NpcVoice>();
            ui = gameObject.AddComponent<LifeInterface>();
            ui.Command += Command;
            recorder.Finished += Transcribe;
            recorder.Failed += Fail;
            recorder.Changed += Refresh;
            if (mission.Step > 0) Player.Place(mission.Destination.Stand);
            Refresh();
            Connect();
        }
        private void Connect()
        {
            if (working) return;
            StartCoroutine(client.Connect(error => { status = error == "" ? "Local AI connected" : error; Refresh(); }));
        }
        private int Near()
        {
            var nearest = -1;
            var distance = 3.1f;
            for (var index = 0; index < Npcs.Length; index++)
            {
                var candidate = Vector3.Distance(Player.transform.position, Npcs[index].position);
                if (candidate >= distance) continue;
                nearest = index;
                distance = candidate;
            }
            return nearest;
        }
        private bool CurrentNpc => speakerIndex >= 0 && catalog.npcs[speakerIndex].id == mission.Current.npc;
        private void Update()
        {
            if (mission == null) return;
            if (Player.Controls.Cancel && dialogue) Close();
            if (Player.Controls.Interact && !dialogue && !working) Open();
            if (Time.unscaledTime >= nextReport)
            {
                nextReport = Time.unscaledTime + 0.15f;
                Refresh();
            }
        }
        private void Open()
        {
            var near = Near();
            if (near < 0) return;
            Close();
            speakerIndex = near;
            dialogue = true;
            Player.Route.Clear();
            feedback = transcript = previewRepair = "";
            Refresh();
            Hear(false);
        }
        private string Line => CurrentNpc ? mission.Question
            : "Good to see you! " + (mission.Complete ? "Enjoy the rest of your day." : "You can catch up with " + mission.Destination.name + " at " + mission.Destination.place + " next.");
        private void Submit(string text, bool spoken)
        {
            text = (text ?? "").Trim();
            if (!dialogue || !CurrentNpc || working || mission.Complete || recorder.State != "idle" || text.Length == 0 || text.Length > 400) return;
            StopVoice();
            client.Cancel();
            transcript = (spoken ? "You said: " : "Rehearsal: ") + text;
            ui.ClearInput();
            StartCoroutine(Review(text, spoken, epoch, mission.Step));
        }
        private IEnumerator Review(string text, bool spoken, int current, int step)
        {
            working = true;
            status = "Checking meaning and wording…";
            Refresh();
            var failed = false;
            var advanced = false;
            yield return client.Json<LifeReview>("/life/review", new LifeReply { step = mission.Current.id, text = text, repair = mission.Repair, drink = mission.Drink }, result => {
                if (current != epoch || !dialogue) return;
                advanced = mission.Apply(step, text, result, spoken);
                previewRepair = spoken ? "" : result.correction ?? "";
                feedback = mission.Feedback;
            }, error => { failed = true; Fail(error); });
            if (current != epoch) yield break;
            working = false;
            if (failed) { Refresh(); yield break; }
            Save();
            status = mission.Complete ? "First day complete · spoken goals achieved"
                : advanced ? "Task updated. " + mission.Objective.Replace('\n', ' ')
                : spoken ? "Record a new answer to try again." : "Typed rehearsal only. Record your answer to continue.";
            Refresh();
            if (advanced && CurrentNpc) Hear(false);
            else if (!advanced && mission.Repair != "") Hear(true);
        }
        private void Record()
        {
            if (!dialogue || !CurrentNpc || working || mission.Complete) return;
            if (recorder.State == "recording") { recorder.Stop(); return; }
            if (recorder.State == "requesting") { recorder.Cancel(); return; }
            if (!client.Connected) { Fail("Start the local companion, then reconnect."); return; }
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
                Submit(text, true);
            }, error => { if (current == epoch) { working = false; Fail(error); } }));
        }
        private void Hear(bool correction)
        {
            if (!dialogue || working || recorder.State != "idle") return;
            var text = correction ? (mission.Repair != "" ? mission.Repair : previewRepair) : Line;
            if (string.IsNullOrEmpty(text)) return;
            StopVoice();
            voice.Say(client, text, Fail);
        }
        private void Walk()
        {
            Close();
            if (mission.Complete) return;
            Player.Route.Clear();
            foreach (var point in LifeRoutes.To(Player.transform.position, mission.Destination)) Player.Route.Enqueue(point);
            if (Player.Route.Count == 0) Fail("No clear route found. You can still explore with WASD.");
        }
        private void Stage(string stage)
        {
            if (!new[] { "continue", "home", "market", "cafe", "friend" }.Contains(stage)) return;
            Close();
            mission = new LifeMission();
            if (stage == "continue") mission.Restore(PlayerPrefs.GetString(SaveKey, ""));
            else mission.StartPractice(stage);
            Player.Place(mission.Destination.Stand);
            feedback = transcript = previewRepair = "";
            Refresh();
        }
        private void Save()
        {
            if (mission.Practice) return;
            PlayerPrefs.SetString(SaveKey, mission.Save());
            PlayerPrefs.Save();
        }
        private void Close()
        {
            Player.Controls.Clear();
            epoch++;
            dialogue = working = false;
            StopVoice();
            if (client != null) client.Cancel();
            if (recorder != null) recorder.Cancel();
            Refresh();
        }
        private void StopVoice()
        {
            if (voice != null) voice.Cancel();
        }

        private void Fail(string error)
        {
            status = error;
            Refresh();
        }
        private string Menu()
        {
            if (!dialogue || speakerIndex < 0) return "";
            var definition = Npcs[speakerIndex].GetComponent<NpcActor>().Definition;
            if (definition.Supports(NpcCapability.FamilyErrand)) return "FAMILY SHOPPING LIST\n3 apples · 2 carrots\n" + (mission.DinnerReady ? "Dinner ingredients delivered" : "Budget · $20");
            if (definition.Supports(NpcCapability.BuyGroceries)) return "GREEN MARKET\nApples · $1 each\nCarrots · $2 each\n\nCart · " + mission.Cart;
            if (!definition.Supports(NpcCapability.OrderDrink)) return "";
            return "FOREST CAFE MENU\n" + string.Join("\n", catalog.items.Where(item => item.category == "drink").Select(item => item.name + " · $" + (item.price / 100.0).ToString("F2")))
                + (mission.Receipt != "" ? "\n\n" + mission.Receipt : mission.Drink == "" ? "" : "\n\nYour order · 1 " + mission.Drink);
        }
        private void Refresh()
        {
            if (quitting || ui == null || recorder == null || mission == null) return;
            Player.Blocked = dialogue;
            // Close conversations frame the NPC, without the player's head clipping the camera.
            if (Player.Model != null) Player.Model.gameObject.SetActive(!dialogue);
            Player.ConversationPartner = dialogue && speakerIndex >= 0 ? Npcs[speakerIndex] : null;
            Dinner.SetActive(mission.DinnerReady);
            DrinkProp.SetActive(mission.Step >= 9);
            var near = Near();
            var snapshot = new LifeSnapshot {
                objective = (mission.Practice ? "PRACTICE · " : "") + mission.Objective,
                wallet = "$" + (mission.Balance / 100.0).ToString("F2"), bag = mission.Bag, menu = Menu(),
                repair = mission.Repair != "" ? mission.Repair : previewRepair,
                place = mission.Destination.place, step = mission.Step, phase = "life", stage = mission.Current.id,
                nearby = dialogue || near < 0 ? "" : "Talk to " + catalog.npcs[near].name + " · E",
                speaker = speakerIndex < 0 ? "" : catalog.npcs[speakerIndex].name + " · " + catalog.npcs[speakerIndex].role,
                line = dialogue ? Line : "", feedback = feedback, transcript = transcript, status = status, recording = recorder.State,
                dialogue = dialogue, busy = working, complete = mission.Complete || (dialogue && !CurrentNpc), practice = mission.Practice,
                x = Player.transform.position.x, z = Player.transform.position.z
            };
            for (var index = 0; index < Npcs.Length; index++)
            {
                var actor = Npcs[index].GetComponent<NpcActor>();
                var state = !dialogue || index != speakerIndex ? NpcState.Idle
                    : working ? NpcState.Thinking : recorder.State != "idle" ? NpcState.Listening
                    : voice.Playing ? NpcState.Talking : NpcState.Listening;
                actor?.SetState(state);
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
            if (command != null) Command(command.action, command.text);
        }
        private void Command(string action, string text)
        {
            switch (action)
            {
                case "send": Submit(text, false); break;
                case "record": Record(); break;
                case "hear": Hear(false); break;
                case "repair": Hear(true); break;
                case "close": Close(); break;
                case "interact": if (!dialogue && !working) Open(); break;
                case "walk": Walk(); break;
                case "stage": Stage(text); break;
                case "connect": Connect(); break;
                case "cafe": Close(); WorldScenes.Open("ForestCafe", Fail); break;
            }
        }
        private void OnApplicationFocus(bool focus)
        {
            // The OS permission sheet temporarily takes focus before capture starts.
            if (!focus && recorder != null && !recorder.WaitingForPermission) Close();
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
