using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Speaking practice at home: eight IELTS topics (Part 1 → Part 2 cue → Part 3 discussion), each run
    /// through the existing ScenarioManager pipeline — the family member asks, the player answers by
    /// microphone, the answer is evaluated and corrected, and in Learning Mode the corrected model must
    /// be repeated before moving on (no skipping). Topics start by talking to a family member (the topic
    /// that fits the room you are in) or with T after using something in the house (TV, stove, bed…).
    public sealed class VillaFlow : LocationFlow
    {
        public override string QuestId => "luxury_villa";
        public override string QuestTitle => "Life at home";

        public static readonly (string id, string title, string room)[] Topics =
        {
            ("home", "Your Home", "living"), ("bedroom", "Your Bedroom", "bedroom"), ("cooking", "Cooking and Food", "kitchen"),
            ("routine", "Daily Routine", "dining"), ("leisure", "Free Time and TV", "lounge"), ("study", "Studying and Working from Home", "study"),
            ("pets", "Pets and Animals", "garden"), ("garden", "Gardens and Outdoor Spaces", "garden"),
        };

        public static string TopicTitle(string id) { foreach (var t in Topics) if (t.id == id) return t.title; return id; }
        public int Completed => Topics.Count(t => VillaState.TopicDone(t.id));

        protected override void Start()
        {
            base.Start();
            if (!VillaState.Loaded) VillaState.Load();
            Step(1, Completed >= Topics.Length ? "All 8 home topics done — keep practising any time." : "Talk to your family, or use things in the house and press T to practise (" + Completed + "/8 topics).");
        }

        public override void Talk(NPCController npc)
        {
            var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            var room = player != null ? VillaRoom.At(player.transform.position) : null;
            var kind = room != null ? room.Kind : "garden";
            kind = kind == "media" ? "lounge" : kind == "gym" ? "dining" : kind == "terrace" ? "garden" : kind;      // map extra rooms to topics
            // The topic for this room first, otherwise the next one not yet done, otherwise "home".
            var pick = Topics.FirstOrDefault(t => t.room == kind && !VillaState.TopicDone(t.id)).id
                       ?? Topics.FirstOrDefault(t => !VillaState.TopicDone(t.id)).id
                       ?? Topics.FirstOrDefault(t => t.room == kind).id ?? "home";
            Practise(pick, npc);
        }

        /// Starts a topic with the given family member (or the nearest one).
        public void Practise(string topic, NPCController with = null)
        {
            if (Busy || ScenarioManager.Instance == null) return;
            var living = with != null ? with.GetComponent<LivingNPC>() : Nearest();
            var legacy = with ?? (living != null ? living.Legacy : null);
            if (legacy == null) { LifeUI.Toast("Nobody is home to talk to right now."); return; }
            var scenario = VillaScenarios.Create(topic);
            if (scenario == null) return;
            if (living != null) living.Interrupt(Hold(living));
            ScenarioManager.Instance.Begin(scenario, legacy, () =>
            {
                VillaState.CompleteTopic(topic);
                if (living != null) NPCRelationshipMemory.Relate(living.Id, 3, "Talked with the player about " + TopicTitle(topic).ToLowerInvariant() + ".");
                Step(1 + Completed, Completed >= Topics.Length ? "All 8 home topics done!" : "Topic done: " + TopicTitle(topic) + " (" + Completed + "/8). Find another!");
            });
        }

        private LivingNPC Nearest()
        {
            var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            LivingNPC best = null; var d = float.MaxValue;
            foreach (var n in LivingNPC.All)
            {
                if (n == null || n.Hidden || n.Legacy == null || n.Profile == null || n.Profile.Home != "villa") continue;
                var dd = player != null ? (n.transform.position - player.transform.position).sqrMagnitude : 0;
                if (dd < d) { d = dd; best = n; }
            }
            return best;
        }

        private IEnumerator Hold(LivingNPC npc)
        {
            npc.InConversation = true;
            npc.Navigation.Stop();
            var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            yield return null;
            while (ScenarioManager.Instance != null && ScenarioManager.Instance.Active && ScenarioManager.Instance.Npc == npc.Legacy)
            {
                if (player != null && (player.transform.position - npc.transform.position).sqrMagnitude < 36)
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
        }
    }

    /// The eight home topics. Each line has Band 3–4 / 5–6 / 7–8 wording, a goal, a meaning pattern the
    /// answer must match, a model answer to repeat and vocabulary notes.
    public static class VillaScenarios
    {
        private static ScenarioStep S(string id, string simple, string standard, string advanced, string goal, string meaning, string model, string vocabulary)
            => new ScenarioStep { id = id, speaker = "family", simple = simple, standard = standard, advanced = advanced, goal = goal, meaning = meaning, model = model, vocabulary = vocabulary };

        public static ScenarioDefinition Create(string topic)
        {
            var steps = Steps(topic);
            if (steps == null) return null;
            var s = ScriptableObject.CreateInstance<ScenarioDefinition>();
            s.name = "Villa · " + VillaFlow.TopicTitle(topic); s.Title = s.name; s.Location = "Luxury Villa"; s.Steps = steps;
            return s;
        }

        private const string Reason = "because|so|since|as|which|that'?s why|the reason";

        private static List<ScenarioStep> Steps(string topic)
        {
            switch (topic)
            {
                case "home": return new List<ScenarioStep>
                {
                    S("p1", "Do you like this house?", "So, what do you like most about living in this house?", "What would you say is the best thing about living here, compared with other places you've lived?",
                        "Say what you like and give a reason.", "like|love|favourite|best|enjoy|great|spacious|light|bright|quiet|big|garden|pool",
                        "I love how bright and spacious it is, especially the living room with its tall windows.", "spacious: with a lot of room|double-height: two floors tall"),
                    S("p2", "Tell me about a room you like.", "Describe your favourite room in the house — what's in it and what do you do there?", "Describe the room where you spend most of your time: how it's furnished, what you do there and why it suits you.",
                        "Describe a room: what is in it and what you do there.", "room|there'?s|there are|sofa|bed|desk|table|window|i (usually|often)|spend|relax|read|watch",
                        "My favourite room is the living room. There's a big sofa facing the garden, and I usually read there in the afternoon.", "furnished: with furniture in it|facing: looking towards"),
                    S("p3", "Is a big house better?", "Do you think people need a big house to be happy?", "Some say a smaller home is easier to live in. Do you think a large house really makes people happier?",
                        "Give your opinion with a reason or example.", "think|believe|depends|not necessarily|yes|no|happy|space|clean|cost|family|" + Reason,
                        "Not necessarily. A big house is comfortable, but it takes more time to clean, so a smaller home can be happier.", "not necessarily: not always true|upkeep: the work of keeping something in good condition"),
                };
                case "bedroom": return new List<ScenarioStep>
                {
                    S("p1", "Is your bedroom nice?", "What's your bedroom like?", "How would you describe your bedroom to someone who has never seen it?",
                        "Describe your bedroom.", "big|small|cosy|bright|quiet|bed|window|desk|wardrobe|it'?s|it has|there'?s",
                        "It's quite cosy. It has a big bed, a wardrobe and a window that looks onto the garden.", "cosy: warm and comfortable|look onto: have a view of"),
                    S("p2", "What do you do in your room?", "Apart from sleeping, what do you do in your bedroom?", "Besides sleeping, how do you use your bedroom during the day?",
                        "Say what you do there.", "study|read|relax|music|phone|watch|rest|change|work|homework|listen",
                        "I usually study at my desk and listen to music before I go to sleep.", "unwind: relax after being busy"),
                    S("p3", "Should kids share rooms?", "Do you think children should have their own bedroom?", "Is it important for children to have a room of their own, in your view?",
                        "Give your opinion with a reason.", "think|yes|no|depends|privacy|share|learn|own|space|independent|" + Reason,
                        "I think it helps, because children need some privacy, but sharing can also teach them to cooperate.", "privacy: being alone without being disturbed|cooperate: work together"),
                };
                case "cooking": return new List<ScenarioStep>
                {
                    S("p1", "Do you cook?", "Do you enjoy cooking?", "How much do you enjoy cooking, and how often do you actually do it?",
                        "Say if you cook and how often.", "yes|no|sometimes|often|every|weekend|never|love|enjoy|cook|not really",
                        "Yes, I cook most evenings, and I really enjoy making pasta.", "most evenings: on nearly every evening"),
                    S("p2", "What is your favourite dish?", "What's a dish you like to make? How do you make it?", "Talk me through a dish you're good at making — the ingredients and the main steps.",
                        "Name a dish and describe how you make it.", "first|then|after|add|fry|boil|bake|chop|mix|cook|ingredient|garlic|rice|egg|minutes",
                        "I make fried rice. First I chop the vegetables, then I fry them with garlic and add the rice and eggs.", "chop: cut into pieces|stir-fry: fry quickly while stirring"),
                    S("p3", "Is home cooking better?", "Is it better to eat at home or at a restaurant?", "Many families eat out more these days. Is home cooking still important?",
                        "Compare and give your view.", "home|restaurant|healthier|cheaper|expensive|time|together|family|think|" + Reason,
                        "I think home cooking is better, because it's healthier and cheaper, and the family eats together.", "eat out: eat at a restaurant"),
                };
                case "routine": return new List<ScenarioStep>
                {
                    S("p1", "When do you get up?", "What time do you usually get up?", "Talk me through how your mornings usually start.",
                        "Say when you get up and what you do first.", "o'?clock|am|six|seven|eight|half|early|late|get up|wake|first|shower|breakfast",
                        "I usually get up at seven and have a shower before breakfast.", "get up: leave your bed"),
                    S("p2", "What do you do in the day?", "What does a normal weekday look like for you?", "Describe a typical weekday, from the morning until the evening.",
                        "Describe your day in order.", "morning|afternoon|evening|then|after|before|usually|work|study|school|lunch|dinner|finally",
                        "In the morning I study, after lunch I go for a walk, and in the evening I have dinner with my family.", "typical: normal, usual"),
                    S("p3", "Are routines good?", "Do you think it's good to have a routine?", "Some people love routines and others find them boring. What's your view?",
                        "Give your opinion with a reason.", "good|helpful|boring|organised|productive|think|depends|habit|" + Reason,
                        "Yes, a routine keeps me organised, but at weekends I like to be more relaxed.", "organised: planning things well|productive: getting a lot done"),
                };
                case "leisure": return new List<ScenarioStep>
                {
                    S("p1", "Do you watch TV?", "How often do you watch TV?", "How much of your free time goes on TV or streaming these days?",
                        "Say how often you watch.", "every|day|night|week|often|sometimes|rarely|never|hour|evening|weekend",
                        "I watch TV most evenings, usually for about an hour.", "streaming: watching shows online"),
                    S("p2", "What do you like to watch?", "What kind of programmes do you enjoy, and why?", "Tell me about a programme or series you've enjoyed recently and what made it good.",
                        "Name a programme type and say why you like it.", "documentar|comed|drama|news|series|film|movie|show|cooking|sport|funny|interesting|" + Reason,
                        "I enjoy nature documentaries, because they're relaxing and I learn something new.", "documentary: a factual programme"),
                    S("p3", "Is TV bad for kids?", "Do you think children watch too much TV?", "Should parents limit how much time children spend on screens?",
                        "Give your view with a reason.", "think|yes|no|limit|too much|healthy|outside|play|eyes|learn|depends|" + Reason,
                        "I think they should be limited, because children also need time to play outside.", "screen time: time spent on TV, phones or computers"),
                };
                case "study": return new List<ScenarioStep>
                {
                    S("p1", "Do you study at home?", "Do you study or work from home?", "How much of your studying or work do you do from home?",
                        "Say if and how often you study/work at home.", "yes|no|sometimes|every|most|usually|home|desk|online|study|work",
                        "Yes, I study at home most days, at the desk in the study.", "work from home: do your job at home"),
                    S("p2", "Is it easy?", "What's good and bad about studying at home?", "What are the advantages and drawbacks of working from home, in your experience?",
                        "Give one advantage and one disadvantage.", "but|however|although|advantage|easy|quiet|comfortable|distract|lonely|focus|commute|time",
                        "It's quiet and I save time, but sometimes I get distracted by my phone.", "drawback: a disadvantage|distracted: unable to focus"),
                    S("p3", "Will people work at home more?", "Do you think more people will work from home in the future?", "Is working from home likely to become the norm? Why or why not?",
                        "Predict and explain.", "will|think|future|more|less|probably|technology|internet|office|meet|" + Reason,
                        "I think more people will, because the internet makes it easy, but offices will still be useful for meetings.", "the norm: what is usual"),
                };
                case "pets": return new List<ScenarioStep>
                {
                    S("p1", "Do you like animals?", "Do you like having a pet?", "How do you feel about having a pet in the house?",
                        "Say how you feel about pets.", "like|love|yes|no|cute|friendly|fun|company|pet|cat|dog",
                        "Yes, I love it — our pet is great company.", "company: being with someone so you aren't alone"),
                    S("p2", "How do you look after it?", "What do you do to look after a pet?", "What does looking after a pet properly involve, day to day?",
                        "Describe how to care for a pet.", "feed|walk|water|clean|brush|vet|play|every|day|food|exercise",
                        "We feed it twice a day, give it fresh water and take it to the vet once a year.", "look after: take care of|vet: an animal doctor"),
                    S("p3", "Should kids have pets?", "Is it good for children to have pets?", "What can children learn from looking after an animal?",
                        "Give your view with a reason.", "think|yes|learn|responsib|care|kind|friend|" + Reason,
                        "Yes, because it teaches them to be responsible and kind.", "responsible: someone who can be trusted to do what they should"),
                };
                case "garden": return new List<ScenarioStep>
                {
                    S("p1", "Do you like the garden?", "Do you spend much time in the garden?", "How often do you make use of the garden and the pool?",
                        "Say how often and what you do.", "yes|no|often|sometimes|weekend|swim|sit|relax|plants|read|garden|pool",
                        "Yes, at weekends I swim in the pool and read by the garden.", "make use of: use"),
                    S("p2", "What grows there?", "What would you like to grow in the garden?", "If you could plant anything here, what would you choose, and why?",
                        "Name plants and give a reason.", "flower|tree|vegetable|herb|tomato|rose|fruit|grow|plant|would|" + Reason,
                        "I'd like to grow herbs and tomatoes, because we could use them in cooking.", "herb: a plant used to flavour food"),
                    S("p3", "Are green spaces important?", "Why are parks and gardens important in cities?", "How important are green spaces for people who live in cities?",
                        "Explain why with an example.", "important|health|relax|air|stress|nature|children|play|exercise|think|" + Reason,
                        "They're very important, because they reduce stress and give children a safe place to play.", "green space: an area with grass and trees"),
                };
            }
            return null;
        }
    }
}
