using System.Collections;
using System.Collections.Generic;
using ContextSpeak.Cafe;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// World-anchored speech bubbles for ambient NPC lines (cheap — no AI call), in the game's warm theme.
    public sealed class SpeechBubbles : MonoBehaviour
    {
        private static SpeechBubbles instance;
        private VisualElement root;
        private readonly List<(LivingNPC npc, Label label, float until)> live = new List<(LivingNPC, Label, float)>();

        public static void Show(LivingNPC npc, string text, float seconds)
        {
            if (npc == null || string.IsNullOrEmpty(text) || npc.Tier == 3) return;
            var camera = Camera.main;
            if (camera != null && (camera.transform.position - npc.transform.position).sqrMagnitude > 22 * 22) return;
            if (instance == null) instance = new GameObject("NPC speech bubbles").AddComponent<SpeechBubbles>();
            instance.Add(npc, text, seconds);
        }

        private void Awake() { root = GameUiRoot.Attach(gameObject); }

        private void Add(LivingNPC npc, string text, float seconds)
        {
            for (var i = live.Count - 1; i >= 0; i--) if (live[i].npc == npc) { live[i].label.RemoveFromHierarchy(); live.RemoveAt(i); }
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.maxWidth = 260;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 15;
            label.style.color = (Color)new Color32(0x1F, 0x2A, 0x26, 0xFF);
            label.style.backgroundColor = new Color(0.97f, 0.95f, 0.92f, 0.93f);
            label.style.paddingLeft = label.style.paddingRight = 12; label.style.paddingTop = label.style.paddingBottom = 7;
            label.style.borderTopLeftRadius = label.style.borderTopRightRadius = label.style.borderBottomRightRadius = 14;
            label.style.borderBottomLeftRadius = 4;
            label.style.borderBottomWidth = 2; label.style.borderBottomColor = new Color(0, 0, 0, 0.12f);
            label.style.opacity = 0;
            label.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            label.style.transitionDuration = new List<TimeValue> { new TimeValue(180, TimeUnit.Millisecond) };
            label.schedule.Execute(() => label.style.opacity = 1).StartingIn(16);
            root.Add(label);
            live.Add((npc, label, Time.time + seconds));
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            for (var i = live.Count - 1; i >= 0; i--)
            {
                var (npc, label, until) = live[i];
                if (npc == null || Time.time > until || camera == null || root.panel == null) { label.RemoveFromHierarchy(); live.RemoveAt(i); continue; }
                var head = npc.Animation != null && npc.Animation.Bone("Head") != null ? npc.Animation.Bone("Head").position : npc.transform.position + Vector3.up * 1.7f;
                var anchor = head + Vector3.up * 0.42f;
                if (camera.WorldToViewportPoint(anchor).z <= 0) { label.style.display = DisplayStyle.None; continue; }
                label.style.display = DisplayStyle.Flex;
                var local = root.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, anchor, camera));
                label.style.left = local.x - 10; label.style.top = local.y - (float.IsNaN(label.layout.height) ? 34 : label.layout.height);
            }
        }
    }

    /// NPCEventReactionController / proactive interactions with the player — never forced: each one
    /// can be accepted (E) or ignored by walking on. Cooldowns per NPC and globally keep them rare;
    /// they only happen when the player is free (not talking, driving, swimming, in a menu).
    ///  • a passer-by bumps into the player and drops something → help pick it up, or don't;
    ///  • a tourist asks for directions → a guided IELTS conversation (NeighborhoodFlow);
    ///  • familiar NPCs (helped before / met often) greet the player by recognising them.
    /// Also: E near any living NPC starts a conversation through the location's flow.
    public sealed class NPCEventDirector : MonoBehaviour
    {
        public float GlobalCooldown = 75, BumpCooldown = 600, DirectionsCooldown = 400, GreetCooldown = 180;
        public int BumpsTriggered { get; private set; }
        public int BumpsSuppressed { get; private set; }
        public string Active { get; private set; } = "";
        private CafePlayer player;
        private float next = 20;
        private readonly Dictionary<string, float> npcCooldown = new Dictionary<string, float>();
        private InputAction interact;
        private GameObject dropped;
        private LivingNPC pending;
        private string pendingKind = "";
        private float pendingUntil;

        private void Awake()
        {
            interact = new InputAction("NPC interact", InputActionType.Button, "<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonWest");
            interact.Enable();
        }

        private void OnDestroy() => interact.Dispose();

        public bool PlayerFree()
        {
            if (player == null) return false;
            if (player.Blocked || player.LocomotionOverride != null && player.LocomotionOverride.Active) return false;
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.Active) return false;
            if (LifeUI.PanelOpen) return false;
            return true;
        }

        private bool Ready(LivingNPC npc, string kind, float cooldown)
        {
            var key = npc.Id + ":" + kind;
            if (npcCooldown.TryGetValue(key, out var until) && Time.time < until) return false;
            npcCooldown[key] = Time.time + cooldown;
            return true;
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<CafePlayer>();
            if (player == null) return;
            HandlePending();
            TalkPrompt();
            if (!PlayerFree() || pending != null) return;
            if ((next -= Time.deltaTime) > 0) return;
            next = 8;
            // Familiar greeting (cheap, frequent-ish).
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null || npc.Tier != 1 || npc.InConversation || !npc.Interruptible) continue;
                var d = Vector3.Distance(npc.transform.position, player.transform.position);
                var memory = NPCRelationshipMemory.Of(npc.Id);
                if (d < 4.5f && (memory.Relationship >= 5 || memory.Encounters >= 3) && Ready(npc, "greet", GreetCooldown))
                {
                    var line = memory.Get("helped") != null ? "Hey, it's you again — thanks again for the help!" : memory.Get("preferredDrink") != null ? "Hi! See you at the café?" : "Hi again!";
                    npc.Interrupt(Wave(npc, line));
                    next = 20;
                    return;
                }
            }
            next = Random.Range(GlobalCooldown * 0.6f, GlobalCooldown);
            // Tourist asking for directions.
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null || npc.Tier != 1 || npc.Profile == null || !npc.Profile.Occupation.ToLowerInvariant().Contains("tourist") || npc.InConversation) continue;
                if (Vector3.Distance(npc.transform.position, player.transform.position) > 14) continue;
                if (!Ready(npc, "directions", DirectionsCooldown)) continue;
                npc.Interrupt(AskDirections(npc));
                return;
            }
            // Accidental bump: a walking passer-by heading the player's way.
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null || npc.Tier != 1 || !npc.Interruptible || npc.InConversation) continue;
                var d = Vector3.Distance(npc.transform.position, player.transform.position);
                if (d < 4 || d > 12) continue;
                if (!Ready(npc, "bump", BumpCooldown)) { BumpsSuppressed++; continue; }
                npc.Interrupt(Bump(npc));
                return;
            }
        }

        /// For tests: try to start a bump with this NPC now (respects cooldowns).
        public bool TryBump(LivingNPC npc)
        {
            if (pending != null || !Ready(npc, "bump", BumpCooldown)) { BumpsSuppressed++; return false; }
            npc.Interrupt(Bump(npc));
            return true;
        }

        public void TryDirections(LivingNPC npc) { if (pending == null) npc.Interrupt(AskDirections(npc)); }

        private IEnumerator Wave(LivingNPC npc, string line)
        {
            npc.InConversation = true;
            npc.Navigation.Stop();
            npc.Animation.LookAt(player.transform.position + Vector3.up * 1.6f);
            npc.Say(line);
            npc.Animation.Play(NPCAction.Wave);
            yield return new WaitForSeconds(1.8f);
            npc.Animation.Play(NPCAction.None);
            npc.Animation.LookAt(null);
            npc.InConversation = false;
        }

        private IEnumerator Bump(LivingNPC npc)
        {
            Active = "bump · " + npc.DisplayName;
            npc.Navigation.Enable(true);
            // Walk into the player's path while looking at the phone.
            npc.Animation.Play(NPCAction.Phone);
            var t = 0f;
            while (t < 12 && Vector3.Distance(npc.transform.position, player.transform.position) > 0.85f && PlayerFree())
            {
                t += Time.deltaTime;
                npc.Navigation.Go(player.transform.position + player.transform.forward * 0.4f, 0.1f);
                yield return null;
            }
            npc.Navigation.Stop();
            npc.Animation.Play(NPCAction.None);
            if (Vector3.Distance(npc.transform.position, player.transform.position) > 1.2f) { Active = ""; yield break; }
            BumpsTriggered++;
            NPCLifeSimulationManager.Instance?.CountBump();
            npc.InConversation = true;
            npc.Animation.LookAt(player.transform.position + Vector3.up * 1.6f);
            npc.Say("Oh! I'm so sorry — are you alright?");
            dropped = Props.Make(npc.Profile != null && npc.Profile.Occupation.ToLowerInvariant().Contains("tourist") ? "map" : "bag");
            dropped.transform.position = npc.transform.position + npc.transform.forward * 0.6f + Vector3.up * 0.9f;
            var body = dropped.AddComponent<Rigidbody>(); body.mass = 0.3f;
            dropped.AddComponent<BoxCollider>().size = new Vector3(0.35f, 0.1f, 0.3f);
            WorldEvents.Raise("drop", dropped.transform.position, 8, npc);
            yield return new WaitForSeconds(1.6f);
            npc.Say("Oh no, I dropped my " + (dropped.name.Contains("map") ? "map" : "shopping") + "…");
            pending = npc; pendingKind = "bump"; pendingUntil = Time.time + 9;
            while (pending == npc) yield return null;
            npc.InConversation = false;
            Active = "";
        }

        private IEnumerator AskDirections(LivingNPC npc)
        {
            Active = "directions · " + npc.DisplayName;
            npc.Navigation.Enable(true);
            var t = 0f;
            while (t < 15 && Vector3.Distance(npc.transform.position, player.transform.position) > 1.6f && PlayerFree())
            {
                t += Time.deltaTime;
                npc.Navigation.Go(player.transform.position, 1.3f);
                yield return null;
            }
            npc.Navigation.Stop();
            if (!PlayerFree() || Vector3.Distance(npc.transform.position, player.transform.position) > 2.5f) { Active = ""; yield break; }
            npc.InConversation = true;
            npc.Animation.LookAt(player.transform.position + Vector3.up * 1.6f);
            npc.Animation.Play(NPCAction.Map);
            npc.Say("Excuse me — sorry to bother you. Could you help me find the tram stop?", 5);
            pending = npc; pendingKind = "directions"; pendingUntil = Time.time + 10;
            while (pending == npc) yield return null;
            npc.Animation.Play(NPCAction.None);
            npc.InConversation = false;
            Active = "";
        }

        private void HandlePending()
        {
            if (pending == null) return;
            var hud = ActionHud.Instance;
            var far = Vector3.Distance(pending.transform.position, player.transform.position) > 6;
            if (Time.time > pendingUntil || far)
            {
                // Ignored: the NPC copes on their own (no forced conversation).
                if (pendingKind == "bump") { pending.Say("Never mind, I've got it."); if (dropped != null) Destroy(dropped, 1.5f); }
                else pending.Say("Oh, okay. Thanks anyway!");
                pending = null;
                return;
            }
            hud?.Prompt("npc-event", "E", pendingKind == "bump" ? "Help pick it up" : "Help with directions");
            if (!interact.WasPressedThisFrame() || !PlayerFree()) return;
            var npc = pending;
            pending = null;
            if (pendingKind == "bump")
            {
                if (dropped != null) Destroy(dropped);
                npc.Say("Thank you so much! That's really kind of you.");
                NPCRelationshipMemory.Relate(npc.Id, 8, "The player helped pick up what I dropped after we bumped into each other.");
                NPCRelationshipMemory.Remember(npc.Id, "helped", "picked up dropped " + (dropped != null && dropped.name.Contains("map") ? "map" : "shopping"));
            }
            else
            {
                var flow = LocationFlow.Current as NeighborhoodFlow;
                if (flow != null) flow.Directions(npc);
                else npc.Say("The tram stop? Thank you!");
            }
        }

        /// "E — Talk to Emma" near any free living NPC; the location flow chooses the conversation.
        private void TalkPrompt()
        {
            if (pending != null || !PlayerFree()) return;
            LivingNPC best = null; var d = 2.3f;
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null || npc.Tier != 1 || npc.Hidden || npc.Riding) continue;
                var dd = Vector3.Distance(npc.transform.position, player.transform.position);
                if (dd < d) { d = dd; best = npc; }
            }
            if (best == null || InteractionManager.Instance != null && InteractionManager.Instance.Focus != null) return;
            var busy = best.Profile != null && best.Profile.Personality.Busyness > 0.6f && best.Step.StartsWith("making");
            ActionHud.Instance?.Prompt("npc-talk", "E", "Talk to " + best.DisplayName.Split(' ')[0] + (busy ? " (busy)" : ""));
            if (!interact.WasPressedThisFrame()) return;
            var flow = LocationFlow.Current;
            if (flow != null && best.Legacy != null) flow.Talk(best.Legacy);
            else best.Say(busy ? "Sorry — one sec, I'm just making an order!" : LivingNPC.Pick(best.Profile != null ? best.Profile.Greetings : null, "Hi!"));
            NPCRelationshipMemory.Encounter(best.Id, LivingClock.Day);
        }
    }
}
