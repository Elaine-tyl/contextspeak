using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Proactive;
using ContextSpeak.Learning;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;
namespace ContextSpeak.Editor
{
    public static class ProactiveNPCChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Proactive NPC V7.1")]
        public static void Verify()
        {
            ProactiveNPCSetup.CreateContent();
            var assets = Resources.LoadAll<ProactiveEventAsset>("ProactiveV71/Events");
            Require(assets.Length >= 12 && assets.Any(a => a.Definition.Id == "cafe-spill"), "event assets");
            var state = new ProactiveSave(); var quest = new EventQuestAdapter(state);
            quest.Begin("collision", "jamie", "city", DateTime.UtcNow);
            quest.Advance(true, "Are you okay?", "city", DateTime.UtcNow);
            var restored = NPCMemoryService.Sanitize(JsonUtility.FromJson<ProactiveSave>(JsonUtility.ToJson(state)));
            Require(restored.Run.Step == 1 && restored.Settings.Frequency == InteractionFrequency.High && restored.Settings.Mode == GameMode.GuidedLearning, "native save and defaults");
            Require(new EventQuestAdapter(restored).Prompt(5).Contains("kind"), "native contextual follow-up");
            var original = new SliceSave(); original.Wallet.Cash = 987;
            Require(SliceSave.Sanitize(JsonUtility.FromJson<SliceSave>(JsonUtility.ToJson(original))).Wallet.Cash == 987, "existing save intact");
            var actor = new GameObject("Proactive contact fixture");
            try
            {
                ProactiveLifeDirector.EnsureBody(actor);
                var body = actor.GetComponent<CapsuleCollider>();
                Require(body != null && !body.isTrigger && body.radius < .4f, "safe physical body");
                ProactiveLifeDirector.EnsureBody(actor);
                Require(actor.GetComponents<Collider>().Length == 1, "idempotent body setup");
                var detector = actor.AddComponent<NPCInteractionDetector>();
                Require(!detector.ClearPath(null), "no invented contact/player");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
            var overlayOwner = new GameObject("Proactive UI layer fixture");
            var existingOwner = new GameObject("Existing UI layer fixture");
            // Sorting tests need a panel, not input actions. Avoid leaking default action references
            // into the following scene/UI fixture in this same headless editor process.
            var fixtureEvents = new GameObject("Proactive UI event fixture");
            fixtureEvents.AddComponent<UnityEngine.EventSystems.EventSystem>();
            try
            {
                ContextSpeak.Cafe.UI.GameUiRoot.Attach(existingOwner);
                var overlay = ProactiveInteractionUI.AttachOverlay(overlayOwner);
                Require(overlayOwner.GetComponent<UnityEngine.UIElements.UIDocument>().sortingOrder
                    > existingOwner.GetComponent<UnityEngine.UIElements.UIDocument>().sortingOrder,
                    "invitation receives pointer above the existing world HUD");
                Require(overlay.pickingMode == UnityEngine.UIElements.PickingMode.Ignore, "empty overlay does not block world input");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayOwner);
                UnityEngine.Object.DestroyImmediate(existingOwner);
                UnityEngine.Object.DestroyImmediate(fixtureEvents);
            }
            Debug.Log("Proactive V7.1 native checks passed: imported events, native save/defaults, branch memory, existing save and contact components. Live approach, microphone, NavMesh stability and authored animation playback require Play-mode validation.");
        }
        private static void Require(bool value, string name) { if (!value) throw new InvalidOperationException("V7.1 check failed: " + name); }
    }
}
