using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Conversations in the living neighbourhood, through the existing IELTS pipeline (ScenarioManager:
    /// NPC speaks → player answers by microphone → recognition → evaluation → correction → corrected
    /// model → player repeats → verified → next line; in Learning Mode the repeat cannot be skipped).
    ///  • Barista: order a drink (the barista then really makes it, and remembers the usual);
    ///  • Tourist: give directions to the tram stop (the tourist remembers being helped);
    ///  • Anyone else: neighbourly small talk.
    public sealed class NeighborhoodFlow : LocationFlow
    {
        public override string QuestId => "living_neighbourhood";
        public override string QuestTitle => "A day in the neighbourhood";
        public int CompletedConversations { get; private set; }
        private string drink = "flat white";
        private LivingNPC talking;

        protected override void Start()
        {
            base.Start();
            Step(1, "Explore the neighbourhood — order at Forest Café, or help someone who looks lost.");
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.Event += OnEvent;
        }

        protected override void OnDestroy()
        {
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.Event -= OnEvent;
            base.OnDestroy();
        }

        private void Update()
        {
            // The player's drink waiting at the pickup counter.
            var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            if (player == null) return;
            foreach (var place in LivingPlace.All)
            {
                var service = place != null ? place.Service : null;
                if (service == null) continue;
                foreach (var order in service.Waiting.ToArray())
                {
                    if (!order.ForPlayer || !order.Ready || order.Product == null) continue;
                    if (Vector3.Distance(player.transform.position, order.Product.transform.position) > 2f) continue;
                    ActionHud.Instance?.Prompt("take-drink", "E", "Take your " + order.Item);
                    if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        Destroy(order.Product);
                        service.Finish(order);
                        LifeUI.Toast("Enjoy your " + order.Item + "!");
                        Step(4, "Find a seat, or keep exploring the neighbourhood.");
                    }
                }
            }
        }

        private void OnEvent(string key)
        {
            if (key != "drink" || ScenarioManager.Instance == null) return;
            var heard = ScenarioManager.Instance.Heard.ToLowerInvariant();
            foreach (var item in new[] { "flat white", "long black", "iced latte", "chai latte", "hot chocolate", "cappuccino", "latte", "mocha", "espresso", "tea" })
                if (heard.Contains(item)) { drink = item; break; }
        }

        public override void Talk(NPCController npc)
        {
            var living = npc != null ? npc.GetComponent<LivingNPC>() : null;
            if (living == null) return;
            var occupation = living.Profile != null ? living.Profile.Occupation.ToLowerInvariant() : "";
            if (occupation.Contains("barista") && living.Place != null && living.Place.Service != null) Order(living);
            else if (occupation.Contains("tourist")) Directions(living);
            else Converse(living, "Neighbourhood Small Talk", () =>
            {
                NPCRelationshipMemory.Relate(living.Id, 3, "Chatted with the player about the neighbourhood.");
                living.Say("Nice talking to you!");
            });
        }

        private void Order(LivingNPC barista)
        {
            var service = barista.Place.Service;
            Converse(barista, "Neighbourhood Café Order", () =>
            {
                NPCRelationshipMemory.Remember(barista.Id, "preferredDrink", drink);
                NPCRelationshipMemory.Relate(barista.Id, 4, "The player ordered a " + drink + ".");
                service.JoinPlayer(drink);
                Step(2, "Wait for your " + drink + " at the pickup counter.");
            });
        }

        public void Directions(LivingNPC tourist)
        {
            Converse(tourist, "Neighbourhood Lost Tourist", () =>
            {
                NPCRelationshipMemory.Relate(tourist.Id, 10, "The player gave me directions to the tram stop.");
                NPCRelationshipMemory.Remember(tourist.Id, "helped", "directions to the tram stop");
                tourist.Say("Thank you so much — you're a lifesaver!");
                Step(3, "You helped a tourist. Maybe you'll see them again.");
            });
        }

        private void Converse(LivingNPC npc, string title, System.Action done)
        {
            if (ScenarioManager.Instance == null || npc.Legacy == null) { npc.Say("Hi!"); return; }
            var scenario = NeighborhoodScenarios.Create(title);
            talking = npc;
            npc.Interrupt(Hold(npc));
            ScenarioManager.Instance.Begin(scenario, npc.Legacy, () => { CompletedConversations++; done?.Invoke(); });
        }

        /// The NPC stays with the player for the conversation: faces them, listens, gestures when speaking.
        private IEnumerator Hold(LivingNPC npc)
        {
            npc.InConversation = true;
            npc.Navigation.Stop();
            var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            yield return null;
            while (ScenarioManager.Instance != null && ScenarioManager.Instance.Active && ScenarioManager.Instance.Npc == npc.Legacy)
            {
                if (player != null)
                {
                    npc.Navigation.Face(player.transform.position - npc.transform.position, Time.deltaTime);
                    npc.Animation.LookAt(player.transform.position + Vector3.up * 1.6f);
                }
                var phase = ScenarioManager.Instance.Machine.Phase;
                npc.Animation.Play(phase == DialoguePhase.AwaitingAnswer || phase == DialoguePhase.AwaitingRepeat ? NPCAction.Listen : NPCAction.Talk);
                yield return null;
            }
            npc.Animation.Play(NPCAction.None);
            npc.Animation.LookAt(null);
            npc.InConversation = false;
            talking = null;
        }
    }

    /// The neighbourhood's conversation scripts (Band 3–4 / 5–6 / 7–8 lines, meaning patterns, models).
    public static class NeighborhoodScenarios
    {
        public static readonly string[] Names = { "Neighbourhood Café Order", "Neighbourhood Lost Tourist", "Neighbourhood Small Talk" };

        private static ScenarioStep S(string id, string speaker, string simple, string standard, string advanced, string goal, string meaning, string model, string vocabulary, string onComplete = "")
            => new ScenarioStep { id = id, speaker = speaker, simple = simple, standard = standard, advanced = advanced, goal = goal, meaning = meaning, model = model, vocabulary = vocabulary, onComplete = onComplete };

        public static ScenarioDefinition Create(string title)
        {
            var steps = Steps(title);
            if (steps == null) return null;
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.name = title; scenario.Title = title; scenario.Location = "Neighbourhood"; scenario.Steps = steps;
            return scenario;
        }

        private static List<ScenarioStep> Steps(string title)
        {
            switch (title)
            {
                case "Neighbourhood Café Order": return new List<ScenarioStep>
                {
                    S("order", "barista", "Hi! What would you like?", "Hi there, welcome to Forest Café! What can I get for you today?",
                        "Morning! Welcome to Forest Café — what are you in the mood for today? The single origin on filter is lovely this week.",
                        "Order a drink politely.", "latte|flat white|cappuccino|long black|coffee|tea|chai|mocha|espresso|hot chocolate|iced|can i (get|have)|i'?d like|could i",
                        "Hi, could I have a flat white, please?", "flat white: espresso with velvety steamed milk|long black: espresso with hot water|single origin: beans from one farm or region", "drink"),
                    S("size", "barista", "Small or large?", "Sure. Would you like a small or a large?", "Lovely choice. Regular or large — and any milk preference? We've got oat and almond too.",
                        "Choose a size (and milk if you like).", "small|large|regular|medium|big|oat|almond|soy|full cream|skim",
                        "A small one with oat milk, please.", "regular: the standard size|oat milk: a plant-based milk"),
                    S("here", "barista", "For here or to go?", "Is that for here or to take away?", "Are you having that here, or would you like it to take away?",
                        "Say if you will stay or take it away.", "here|to go|take ?away|takeaway|stay|sit|have it in",
                        "For here, please.", "take away: to drink somewhere else"),
                    S("pay", "barista", "That's five fifty. Card or cash?", "That'll be five fifty. How would you like to pay?", "That comes to five fifty — card or cash? We're contactless if that's easier.",
                        "Pay and thank the barista.", "card|cash|tap|pay|phone|contactless|thank",
                        "By card, please. Thanks!", "contactless: tap the card or phone to pay"),
                };
                case "Neighbourhood Lost Tourist": return new List<ScenarioStep>
                {
                    S("where", "tourist", "Hello. Where is the tram stop?", "Excuse me, could you help me? I'm looking for the tram stop.",
                        "Sorry to bother you — I seem to have got a bit turned around. Would you happen to know how to get to the tram stop from here?",
                        "Offer to help and give the first direction.", "go|walk|straight|turn|left|right|cross|past|along|down|up|it'?s|sure|of course",
                        "Sure! Go straight down this street and cross at the lights.", "cross at the lights: use the pedestrian crossing|straight down: continue in the same direction"),
                    S("then", "tourist", "And then?", "Okay, cross at the lights… and then?", "Right, cross at the lights — and after that, do I keep going or turn?",
                        "Give the next direction or a landmark.", "left|right|straight|opposite|next to|corner|past|on your|you'?ll see|in front|beside|near",
                        "Then turn left. The tram stop is opposite the café.", "opposite: on the other side, facing|landmark: a place that is easy to see"),
                    S("far", "tourist", "Is it far?", "Thank you so much! Is it far?", "That's really helpful, thank you. Roughly how long would it take on foot?",
                        "Say how far it is.", "not far|minutes?|close|near|five|two|three|ten|short|walk|just|around|about",
                        "No, it's only about a two-minute walk.", "on foot: walking", "helped_tourist"),
                };
                case "Neighbourhood Small Talk": return new List<ScenarioStep>
                {
                    S("new", "neighbour", "Hi! Are you new here?", "Hi! Lovely day, isn't it? Are you new around here?",
                        "Hello! I don't think we've met — have you just moved into the neighbourhood?",
                        "Answer and say a little about yourself.", "yes|no|new|moved|live|been|here|years?|months?|weeks?|just|i'?m",
                        "Yes, I moved here a few weeks ago.", "move in: start living somewhere"),
                    S("opinion", "neighbour", "Do you like it here?", "Oh nice! What do you think of the neighbourhood so far?",
                        "How are you finding it so far? It can take a while to get to know the place.",
                        "Give your opinion with a reason.", "like|love|nice|great|quiet|friendly|good|busy|cafe|park|it'?s|because",
                        "I really like it — it's quiet, and the café is great.", "get to know: become familiar with"),
                    S("bye", "neighbour", "See you!", "Well, it was nice to meet you. See you around!", "Lovely to meet you — I'm sure we'll bump into each other again.",
                        "Say goodbye politely.", "see you|bye|thanks|nice|you too|take care|have a",
                        "Nice to meet you too. See you around!", "bump into someone: meet by chance"),
                };
            }
            return null;
        }
    }
}
