using System;
using System.Reflection;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// Exercise real Submit/Command transitions without starting a scene, network or player save.
    public static class SliceRuntimeChecks
    {
        private static void Set(object instance, string name, object value)
        {
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Slice runtime regression: " + message);
        }
        public static void Verify()
        {
            var host = new GameObject("Isolated slice transactions");
            var game = host.AddComponent<SliceGame>();
            GameObject bar = null;
            game.enabled = false;
            Set(game, "quitting", true); // no rendering, Start or persistence in this isolated fixture
            Set(game, "ui", host.AddComponent<SliceInterface>());
            Set(game, "voice", host.AddComponent<SliceVoice>());
            Set(game, "client", host.AddComponent<CafeClient>());
            Set(game, "recorder", host.AddComponent<CafeRecorder>());
            Set(game, "guide", new FishingGuide());
            Set(game, "fishing", new FishingSession(1));
            game.Player = host.AddComponent<CafePlayer>();
            game.Player.enabled = false;
            // AddComponent invokes Awake in Play mode, but not from an Edit-mode executeMethod.
            // Initialize the real controller's dependencies without starting the scene/game loop.
            // Invoke only this component: SendMessage("Awake") would also awaken SliceGame.
            if (game.Player.Controls == null)
                typeof(CafePlayer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.Player, null);
            Check(game.Player.Controls == host.GetComponent<WorldInput>(), "The isolated player must have its actual WorldInput before exercising reset/back navigation.");
            game.Counter = game.Rowan = host.transform;
            game.Waiter = host.AddComponent<WaiterNpc>();
            game.Waiter.enabled = false;
            var save = new SliceSave { Strictness = Strictness.Guided };
            Set(game, "<Save>k__BackingField", save);
            Set(game, "<Talking>k__BackingField", Partner.Rowan);
            try
            {
                Set(game, "<Talking>k__BackingField", Partner.None);
                foreach (var band in new[] { "Band3", "Band9", "Band3" })
                {
                    game.OnBrowserCommand("{\"action\":\"band\",\"text\":\"" + band + "\"}");
                    Check(save.Strictness == Strictness.Guided, "Changing the practice target must preserve the chosen correction mode.");
                }
                Set(game, "<Talking>k__BackingField", Partner.Rowan);
                game.OnBrowserCommand("{\"action\":\"plan-route\",\"text\":\"ticket\"}");
                Check(save.FocusVocabulary == "ticket", "Planning a route saves the learner's vocabulary focus.");
                game.OnBrowserCommand("{\"action\":\"practice-need\",\"text\":\"Dining\"}");
                Check(save.PracticeNeed == PracticeNeed.Dining, "Choosing a situation selects a matching scene route.");
                Set(game, "working", true);
                game.OnBrowserCommand("{\"action\":\"plan-route\",\"text\":\"latte\"}");
                Check(save.FocusVocabulary == "ticket", "An active recording assessment cannot be replaced by a new route.");
                game.OnBrowserCommand("{\"action\":\"practice-need\",\"text\":\"Travel\"}");
                Check(save.PracticeNeed == PracticeNeed.Dining, "Choosing a new situation cannot replace an active speech assessment.");
                Set(game, "working", false);
                game.Submit("Can I has a croissant?", true);
                Check(save.Cart.Empty && !save.Earned.Contains("order_item") && !save.Progress.Objectives.Contains("order_item"), "Required correction must not add an item or award an objective.");
                game.OnBrowserCommand("{\"action\":\"plan-route\",\"text\":\"latte\"}");
                Check(save.FocusVocabulary == "ticket", "Planning cannot replace a required correction between recording attempts.");
                game.OnBrowserCommand("{\"action\":\"visit\",\"text\":\"counter\"}");
                Check(game.Talking == Partner.Rowan && !string.IsNullOrEmpty((string)typeof(SliceGame).GetField("practiceTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)), "Travel cannot discard a required correction without explicit Close.");
                Set(game, "working", true);
                Set(game, "gate", Gate.ShowModelAndContinue);
                game.OnBrowserCommand("{\"action\":\"use-model\"}");
                Check(save.Cart.Empty, "A model-answer action cannot commit an order while a recording is being assessed.");
                Set(game, "working", false);
                Set(game, "gate", Gate.RequireRetry);
                var connected = host.GetComponent<CafeClient>();
                Set(connected, "token", "isolated-session");
                Set(game, "companion", new CompanionServices(connected));
                game.Submit("Can I have a croissant?", false);
                game.OnBrowserCommand("{\"action\":\"continue\"}");
                Check(save.Cart.Empty, "A typed rehearsal cannot unlock Continue on a required spoken correction.");
                Set(connected, "token", "");
                game.Submit("Can I have a croissant?", true);
                Check(save.Cart.Lines.Count == 1 && save.Earned.Contains("order_item"), "Accepted correction commits its order once.");
                game.Submit("That's all, thanks", true);
                game.Submit("Yes, that's everything, thanks", true);
                var balance = save.Wallet.Card;
                game.Submit("Can I has that by card?", true);
                Check(save.Wallet.Card == balance && save.Receipts.Count == 0 && save.Kitchen.Tickets.Count == 0, "Payment waits until its correction is accepted.");
                game.Submit("Can I have that by card?", true);
                Check(save.Wallet.Card == balance - save.Cart.Total && save.Receipts.Count == 1 && save.Kitchen.Tickets.Count == 1, "One accepted payment produces one charge, receipt and kitchen ticket.");
                game.Submit("Can I have that by card?", true);
                Check(save.Receipts.Count == 1 && save.Kitchen.Tickets.Count == 1, "Repeating after payment cannot charge twice.");

                save = new SliceSave { Band = IeltsBand.Band5, Strictness = Strictness.Practice };
                Set(game, "<Save>k__BackingField", save);
                game.Submit("Can I has a croissant?", true);
                Check(save.Cart.Lines.Count == 1, "Optional practice allows the first understood order.");
                game.OnBrowserCommand("{\"action\":\"practice\"}");
                game.Submit("Can I have a croissant?", true);
                Check(save.Cart.Lines.Count == 1, "Practising an accepted order must not place another order.");

                save.Cart.Status = OrderStatus.Ready;
                save.Kitchen.Add("ready-on-reload", save.Cart).Remaining = 0;
                typeof(SliceGame).GetMethod("Kitchen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { 0f });
                Check(game.Waiter.Busy, "A saved ready order restarts its waiter delivery.");
                var callback = (Action)typeof(WaiterNpc).GetField("delivered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Waiter);
                game.OnBrowserCommand("{\"action\":\"reset\",\"text\":\"confirm\"}");
                Set(game, "quitting", false);
                callback();
                Set(game, "quitting", true);
                Check(game.Save.Cart.Empty && game.Save.Cart.Status == OrderStatus.Browsing && !game.Waiter.Busy, "Old delivery callbacks cannot corrupt a new game.");
                var savedLesson = new SceneLesson { SceneId = "market", Band = game.Save.Band, Step = 2 };
                game.Save.Lessons.Add(savedLesson);
                Set(game, "working", true);
                game.OnBrowserCommand("{\"action\":\"retry-lesson\",\"text\":\"market\"}");
                Check(savedLesson.Step == 2, "Retry cannot reset a lesson while its recording is being assessed.");
                Set(game, "working", false);

                var client = host.GetComponent<CafeClient>();
                Set(client, "token", "old-session");
                var reconnect = client.Connect(_ => { });
                reconnect.MoveNext(); // nested request is not sent; the stale session must already be cleared
                Check(!client.Connected, "Reconnecting must clear the old token before any transport failure.");
                Set(client, "token", "valid-session");
                try { CafeClient.ReadTranscript(System.Text.Encoding.UTF8.GetBytes("{\"text\":\"\"}")); }
                catch (FormatException) { }
                Check(client.Connected, "An unclear recording must not disconnect a healthy companion.");
                var rigRoot = new GameObject("Imported character pose");
                rigRoot.transform.SetParent(host.transform);
                var head = new GameObject("Head").transform;
                head.SetParent(rigRoot.transform);
                var bindRotation = Quaternion.Euler(0, 0, 90);
                head.localRotation = bindRotation;
                var arm = new GameObject("Arm").transform;
                arm.SetParent(rigRoot.transform, false);
                arm.localRotation = bindRotation;
                var avatar = rigRoot.AddComponent<CozyAvatar>();
                avatar.Head = head;
                avatar.LeftArm = arm;
                typeof(CozyAvatar).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(avatar, null);
                Check(Quaternion.Angle(bindRotation, head.localRotation) < 5, "Procedural idle motion preserves an imported character's bind rotation.");
                // A returning customer hides and shows its body, without changing its authored pose.
                var idle = typeof(CozyAvatar).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                for (var cycle = 0; cycle < 6; cycle++)
                {
                    rigRoot.SetActive(false);
                    rigRoot.SetActive(true);
                    idle.Invoke(avatar, null);
                }
                Check(Quaternion.Angle(bindRotation, arm.localRotation) < 8, "Repeated customer visits do not accumulate animated arm offsets.");
                var graphicsRoot = new GameObject("Isolated browser quality");
                graphicsRoot.transform.SetParent(host.transform);
                var graphicsCamera = graphicsRoot.AddComponent<Camera>();
                graphicsCamera.enabled = false;
                var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                pipeline.renderScale = 1;
                pipeline.shadowCascadeCount = 4;
                var quality = graphicsRoot.AddComponent<ForestBrowserQuality>();
                quality.Pipeline = pipeline;
                quality.Camera = graphicsCamera.GetUniversalAdditionalCameraData();
                quality.Camera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                quality.UseBrowserPreset();
                quality.UseBrowserPreset(); // Reapplying must not replace the saved desktop settings.
                Check(pipeline.renderScale < 1 && pipeline.shadowCascadeCount == 2, "The browser uses a smaller rendering budget.");
                UnityEngine.Object.DestroyImmediate(quality);
                Check(pipeline.renderScale == 1 && pipeline.shadowCascadeCount == 4
                    && graphicsCamera.GetUniversalAdditionalCameraData().antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    "Leaving the forest restores desktop render settings for the next scene.");
                UnityEngine.Object.DestroyImmediate(pipeline);
                game.Player.Place(new Vector3(0, -30, 0));
                typeof(CafePlayer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.Player, null);
                Check(game.Player.transform.position.y > -1, "Falling outside a district must recover to its entry point.");
                var partner = new GameObject("Camera partner");
                partner.transform.SetParent(host.transform);
                partner.transform.localPosition = new Vector3(0, 0, -3);
                var body = partner.AddComponent<CapsuleCollider>();
                body.center = Vector3.up * 0.85f;
                body.height = 1.7f;
                partner.AddComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.transform.position = new Vector3(0, 0.55f, -1.4f);
                bar.transform.localScale = new Vector3(3, 1.1f, 0.6f);
                game.Player.ConversationPartner = partner.transform;
                game.Player.View = host.AddComponent<Camera>();
                game.Player.CameraPosition = new GameObject("Camera target").transform;
                game.Player.CameraPosition.SetParent(host.transform);
                game.Player.CameraAim = new GameObject("Camera aim").transform;
                game.Player.CameraAim.SetParent(host.transform);
                Physics.SyncTransforms();
                typeof(CafePlayer).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.Player, null);
                Check(Vector3.Distance(game.Player.CameraPosition.position, game.Player.CameraAim.position) > 3,
                    "A counter and nearby characters must not force the conversation camera into an NPC's face.");
                UnityEngine.Object.DestroyImmediate(partner.GetComponent<UnityEngine.AI.NavMeshAgent>());
                partner.AddComponent<LearningNpc>().SceneId = "market";
                typeof(CafePlayer).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.Player, null);
                Check(Vector3.Distance(game.Player.CameraPosition.position, game.Player.CameraAim.position) > 3,
                    "Resident actors without navigation agents must not collapse the conversation camera either.");
                bar.transform.localPosition = new Vector3(0, 1.5f, -1.4f);
                bar.transform.localScale = new Vector3(3, 3, 0.6f);
                Physics.SyncTransforms();
                typeof(CafePlayer).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.Player, null);
                Check(Vector3.Distance(game.Player.CameraPosition.position, game.Player.CameraAim.position) < 2.5f,
                    "Walls still pull the camera forward to prevent seeing through the environment.");
                var resident = host.AddComponent<LearningNpc>();
                Set(client, "token", ""); // feed provider results directly; this isolated check sends no network request
                resident.SceneId = "home";
                var conversation = new SceneLesson { SceneId = "home", Band = IeltsBand.Band6 };
                Set(game, "resident", resident);
                Set(game, "lesson", conversation);
                Set(game, "<Talking>k__BackingField", Partner.Resident);
                game.Save.Band = IeltsBand.Band6;
                game.Save.Strictness = Strictness.Guided;
                Set(game, "semanticReview", new SliceReviewReply { assessedGoal = true, goalMet = true, relevant = true, correction = "We like cooking dinner because it saves money." });
                game.Submit("We are like cooking dinner because it saves money.", true);
                Check(conversation.Step == 0, "A resident's grammatical correction holds the current scene question.");
                Set(game, "semanticReview", new SliceReviewReply { assessedGoal = true, goalMet = true, relevant = true });
                game.Submit("We enjoy preparing the evening meal since it cuts our expenses.", true);
                Check(conversation.Step == 1, "A semantically correct grammatical paraphrase can complete a resident correction.");
                resident.SceneId = "cafe";
                conversation = new SceneLesson { SceneId = "cafe", Band = IeltsBand.Band3 };
                Set(game, "lesson", conversation);
                game.Save.Band = IeltsBand.Band3;
                game.Save.Strictness = Strictness.Practice;
                Set(game, "semanticReview", new SliceReviewReply { assessedGoal = true, goalMet = true, relevant = true, correction = "I want tea." });
                game.Submit("I am want tea.", true);
                Check(conversation.Step == 1, "Optional correction may move on to the next question.");
                Set(game, "working", true);
                game.OnBrowserCommand("{\"action\":\"practice\"}");
                Check((string)typeof(SliceGame).GetField("practiceTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game) == "", "Practice cannot change mode during an active assessment.");
                Set(game, "working", false);
                game.OnBrowserCommand("{\"action\":\"practice\"}");
                var request = (SliceReviewRequest)typeof(SliceGame).GetMethod("LessonRequest", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { "I want tea." });
                Check(request.npcLine == "What would you like to drink?", "Optional practice keeps the corrected answer's original question, not the next one.");

                var capturePosition = typeof(SliceGame).GetMethod("CapturePlayerPosition", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(capturePosition != null, "Player position capture must tolerate scene teardown.");
                host.transform.position = new Vector3(4, 1, 7);
                capturePosition.Invoke(game, null);
                UnityEngine.Object.DestroyImmediate(game.Player);
                capturePosition.Invoke(game, null);
                Check(game.Save.PlayerX == 4 && game.Save.PlayerY == 1 && game.Save.PlayerZ == 7,
                    "Destroying the player before the game preserves the last known save position.");
            }
            finally
            {
                Set(game, "quitting", true);
                if (bar != null) UnityEngine.Object.DestroyImmediate(bar);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
