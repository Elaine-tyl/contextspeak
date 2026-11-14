#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.LivingWorld
{
    public sealed class LivingWorldPlayRunner : MonoBehaviour
    {
        public Action<string> Finished;
        public bool LivePreview;
        private sealed class FixtureReviewer : IConversationReviewer
        {
            public bool Available => true;
            public SliceReviewReply Next = new SliceReviewReply { relevant = true, assessedGoal = true, goalMet = true };
            public IEnumerator Review(SliceReviewRequest request, Action<SliceReviewReply> success, Action<string> failure)
            { yield return null; success(Next); }
        }
        private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Check(bool condition, string why) { if (!condition) throw new Exception(why); }
        public IEnumerator Run()
        {
            var pending = new System.Collections.Generic.Stack<IEnumerator>();
            pending.Push(Checks());
            while (pending.Count > 0)
            {
                object next = null; string error = "";
                try
                {
                    if (!pending.Peek().MoveNext()) { pending.Pop(); continue; }
                    next = pending.Peek().Current;
                }
                catch (Exception failure) { error = failure.ToString(); }
                if (error != "") { Finished?.Invoke(error); yield break; }
                if (next is IEnumerator nested) pending.Push(nested);
                else yield return next;
            }
            Finished?.Invoke("");
        }
        private IEnumerator Checks()
        {
            Application.runInBackground = true;
            var director = DynamicLifeEventManager.Instance;
            var reviewer = new FixtureReviewer();
            var type = typeof(DynamicLifeEventManager);
            var player = (CafePlayer)type.GetField("player", Flags).GetValue(director);
            var marker = FindObjectsByType<StoryLocationAnchor>(FindObjectsSortMode.None).First(item => item.Id == "night-market");
            var saved = new LivingWorldState(); saved.Flags.Add("market-invitation"); saved.Minute = 19 * 60;
            saved.Runs.Add(new StoryRun { Event = "tourist-market", Step = 0, Repair = "A saved correction.", RepairChoice = "recommend" });
            saved.Run = saved.Runs[0];
            var roundTrip = new BranchingQuestSystem(JsonUtility.FromJson<LivingWorldState>(JsonUtility.ToJson(saved)), LivingWorldTemplates.All());
            Check(roundTrip.State.Run == roundTrip.State.Runs[0] && roundTrip.State.Run.Repair == "A saved correction.", "Unity JSON roundtrip lost the active saved correction.");
            saved.Run.Repair = saved.Run.RepairChoice = "";
            typeof(DynamicLifeEventManager).GetProperty("Stories").SetValue(director, new BranchingQuestSystem(saved, LivingWorldTemplates.All()));
            type.GetField("reviewer", Flags).SetValue(director, reviewer);
            yield return null;
            string error = "";
            try
            {
                player.Place(marker.transform.position + Vector3.back * 2);
                Check(director.Begin("tourist-market"), "Nearby unlocked story did not open.");
                Check(player.Blocked && player.Controls.Suspended, "Story UI did not own movement input.");
                director.Choose("recommend"); director.Rehearse("I'd try the dumplings because they smell great.");
            }
            catch (Exception failure) { error = failure.ToString(); }
            if (error != "") throw new Exception(error);
            yield return new WaitForSeconds(.2f);
            try
            {
                Check(director.Stories.Step == 0 && director.Feedback.Contains("Rehearsal"), "Typed rehearsal advanced a speaking task.");
                reviewer.Next.correction = "I'd recommend the dumplings because they're freshly made.";
                type.GetMethod("Submit", Flags).Invoke(director, new object[] { "I recommend dumplings fresh.", true });
            }
            catch (Exception failure) { error = failure.ToString(); }
            if (error != "") throw new Exception(error);
            yield return new WaitForSeconds(.2f);
            try
            {
                Check(director.Stories.Step == 0 && director.Stories.State.Run.Repair != "", "Correction did not hold the story step.");
                reviewer.Next.correction = "";
                type.GetMethod("Submit", Flags).Invoke(director, new object[] { "I'd go for the dumplings. They smell amazing.", true });
            }
            catch (Exception failure) { error = failure.ToString(); }
            if (error != "") throw new Exception(error);
            yield return new WaitForSeconds(.2f);
            try
            {
                Check(director.Stories.Step == 1, "Verified natural paraphrase did not advance.");
                director.Choose("join");
                type.GetMethod("Submit", Flags).Invoke(director, new object[] { "I'd love to come. What time should we meet?", true });
            }
            catch (Exception failure) { error = failure.ToString(); }
            if (error != "") throw new Exception(error);
            yield return new WaitForSeconds(.2f);
            try
            {
                Check(!director.Stories.Active, "Verified terminal answer did not complete the story.");
                director.Rehearse("Check rehearsal after completion."); // Must safely ignore completed turns.
                director.Close(); Check(!player.Blocked && !player.Controls.Suspended, "Closing left movement blocked.");
                var firstScene = SceneManager.GetActiveScene();
                var other = SceneManager.CreateScene("LivingWorldBindingCheck");
                var second = new GameObject("Second scene player"); SceneManager.MoveGameObjectToScene(second, other);
                second.AddComponent<CharacterController>(); second.AddComponent<WorldInput>(); second.AddComponent<CafePlayer>();
                SceneManager.SetActiveScene(other);
                Check((CafePlayer)type.GetField("player", Flags).GetValue(director) == second.GetComponent<CafePlayer>(), "Additive switch retained the previous scene's player.");
                SceneManager.SetActiveScene(firstScene); SceneManager.UnloadSceneAsync(other);
                Check((CafePlayer)type.GetField("player", Flags).GetValue(director) == player, "Returning to the scene did not restore its player binding.");
            }
            catch (Exception failure) { error = failure.ToString(); }
            if (error != "") throw new Exception(error);
            yield return FullChain();
            if (LivePreview) yield return LiveSpeech();
        }
        private IEnumerator LiveSpeech()
        {
            var director = DynamicLifeEventManager.Instance;
            var type = typeof(DynamicLifeEventManager);
            type.GetProperty("Stories").SetValue(director, new BranchingQuestSystem(new LivingWorldState(), LivingWorldTemplates.All()));
            type.GetField("reviewer", Flags).SetValue(director, type.GetField("services", Flags).GetValue(director));
            BeginAt(director, "tourist-cafe"); director.Choose("unable");
            director.SetFeedback("Isolated microphone test. Explain you are unsure and suggest asking the barista. Stop recording to check automatically.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("../../.local/living-world/story-preview.png");
            while (director.Stories.Step == 0)
            {
                System.IO.File.WriteAllText("../../.local/living-world/live-status.json", JsonUtility.ToJson(new LiveStatus
                {
                    step = director.Stories.Step, recording = director.Recording, heard = director.Heard,
                    feedback = director.Feedback, correction = director.Stories.State.Run.Repair, connected = director.Connected,
                }));
                yield return new WaitForSeconds(.5f);
            }
            Check(director.Stories.State.Receipts.Count == 1 && director.Heard != "", "Live microphone reply was not independently assessed.");
            System.IO.File.WriteAllText("../../.local/living-world/live-status.json", JsonUtility.ToJson(new LiveStatus
            { step = director.Stories.Step, heard = director.Heard, feedback = director.Feedback, connected = director.Connected }));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("../../.local/living-world/live-result.png");
            yield return new WaitForSeconds(2);
        }
        [Serializable] private sealed class LiveStatus
        { public int step; public string recording, heard, feedback, correction; public bool connected; }
        private static IEnumerator WaitForScene(string name)
        {
            var until = Time.realtimeSinceStartup + 40;
            while ((SceneManager.GetActiveScene().name != name || !DynamicLifeEventManager.Instance.Ready || SceneManager.sceneCount != 1)
                && Time.realtimeSinceStartup < until) yield return null;
            Check(SceneManager.GetActiveScene().name == name && SceneManager.sceneCount == 1, "Scene route failed or retained an old scene: " + name);
            yield return new WaitForSeconds(.5f);
        }
        private static CafePlayer Player(DynamicLifeEventManager director) =>
            (CafePlayer)typeof(DynamicLifeEventManager).GetField("player", Flags).GetValue(director);
        private static void BeginAt(DynamicLifeEventManager director, string id)
        {
            var template = director.Stories.Templates.First(item => item.Id == id);
            var anchor = FindObjectsByType<StoryLocationAnchor>(FindObjectsSortMode.None).First(item => item.Id == template.Location);
            Player(director).Place(anchor.transform.position + Vector3.back * 2);
            Check(director.Begin(id), "Could not start the story in its real scene: " + id);
        }
        private static IEnumerator Answer(DynamicLifeEventManager director, string choice)
        {
            director.Choose(choice);
            typeof(DynamicLifeEventManager).GetMethod("Submit", Flags).Invoke(director, new object[] { "An independently assessed synthetic reply.", true });
            yield return new WaitForSeconds(.2f);
            Check(!director.Busy && director.Stories.State.Run.Repair == "", "The fixture answer did not settle.");
        }
        private IEnumerator FullChain()
        {
            var director = DynamicLifeEventManager.Instance;
            ContextSpeak.Life.SaveManager.Persist = false;
            typeof(DynamicLifeEventManager).GetProperty("Stories").SetValue(director,
                new BranchingQuestSystem(new LivingWorldState(), LivingWorldTemplates.All()));
            director.ReturnToCafe();
            yield return WaitForScene("ForestSlice");
            BeginAt(director, "tourist-cafe");
            var cafeHud = FindFirstObjectByType<ContextSpeak.Slice.SliceGame>().GetComponent<UnityEngine.UIElements.UIDocument>();
            Check(cafeHud.rootVisualElement.style.display.value == UnityEngine.UIElements.DisplayStyle.None,
                "The ordinary cafe HUD remains visible over the story dialogue.");
            yield return Answer(director, "directions"); yield return Answer(director, "meet");
            Check(director.Stories.State.Completed.Contains("tourist-cafe"), "Cafe invitation did not complete.");
            director.Close();
            Check(cafeHud.rootVisualElement.style.display.value != UnityEngine.UIElements.DisplayStyle.None,
                "Closing the story did not restore the ordinary cafe HUD.");
            director.OpenJournal(); director.WaitForEvening(); director.Track("tourist-market");
            yield return WaitForScene("LivingWorldDistrict");
            BeginAt(director, "tourist-market");
            yield return Answer(director, "recommend"); yield return Answer(director, "join");
            Check(director.Stories.State.Completed.Contains("tourist-market"), "Night-market invitation did not complete.");
            director.Close(); director.OpenJournal(); director.Track("tourist-airport");
            yield return WaitForScene("Airport");
            BeginAt(director, "tourist-airport");
            yield return Answer(director, "options"); yield return Answer(director, "morning");
            Check(director.Stories.State.Completed.Count == 3 && director.Stories.State.Itinerary == "Morning flight requested",
                "The three-location chain did not preserve its outcome.");
            director.Close(); director.ReturnToCafe();
            yield return WaitForScene("ForestSlice"); // The earlier Addressables handle was released by Single loading.
            Check(director.Stories.State.Completed.Count == 3 && !Player(director).Blocked, "Return to cafe lost state or movement.");
        }
    }
}

#endif
