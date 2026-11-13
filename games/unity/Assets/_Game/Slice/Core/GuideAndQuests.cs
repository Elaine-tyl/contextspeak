using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Slice
{
    /// Hazel, the fishing instructor on the pier. She stays on fishing topics and adapts to the band.
    [Serializable]
    public sealed class FishingGuide
    {
        public IeltsBand Band = IeltsBand.Band6;
        public int Turn;

        private string B(string b5, string b6, string b7, string b8) => Band <= IeltsBand.Band5 ? b5 : Band == IeltsBand.Band6 ? b6 : Band == IeltsBand.Band7 ? b7 : b8;

        public string Greeting(int catches) => catches == 0
            ? B("Hello! I'm Hazel. I teach fishing here. Do you want to learn?",
                "Hi, I'm Hazel! First time fishing here? I can show you the basics if you like.",
                "Oh, hello! Hazel — I more or less live on this pier. Fancy a quick lesson before you cast?",
                "Afternoon! Hazel. Fair warning: I'll talk your ear off about fish if you let me. Want the crash course?")
            : B("Hello again! Did you catch more fish?", "Hey, welcome back! Any luck since last time?", "Back for more? Careful, it's addictive.", "The pond's favourite rival returns. Come to settle the score?");

        public WaiterTurn Respond(string raw, Weather weather, int minuteOfDay, int catches)
        {
            Turn++;
            var text = OrderIntentParser.Normalize(raw);
            bool Has(string pattern) => Regex.IsMatch(text, @"\b(?:" + pattern + @")\b");
            var turn = new WaiterTurn { Progress = true };
            var hour = minuteOfDay / 60;
            if (Has("caught anything|catch anything|any luck|caught any|how'?s the fishing|biting today"))
            {
                turn.Objective = "fish_smalltalk";
                turn.Line = B("Yes! I caught two perch this morning.", "Yeah, a couple of perch this morning — nothing huge, but they put up a fight.", "A couple of perch, and one trout that I'll swear was the size of my arm — but it got away, so you'll have to take my word for it.", "Two perch and a trout of truly mythical proportions — which, conveniently, escaped before anyone could verify it.");
            }
            else if (Has("what kind|which fish|what fish|types? of fish|kinds? of fish|what lives|what'?s in (?:this|the) pond"))
            {
                turn.Objective = "fish_types";
                turn.Line = B("There are bluegill, perch, trout, koi and pike. Some people say there is a golden carp.",
                    "Mostly bluegill and perch, but there are trout and koi too. If you're lucky you might hook a pike.",
                    "Bluegill and perch are your bread and butter. Trout turn up around dawn and dusk, and there's a pike lurking in the weeds. Oh — and the legendary golden carp, if you believe in that sort of thing.",
                    "The usual suspects — bluegill, perch, the odd trout at dusk — plus a pike with a serious attitude problem. And then there's the golden carp, which half the town swears only surfaces in the rain.");
            }
            else if (Has("how long have you|been fishing|when did you start|how did you learn"))
            {
                turn.Objective = "fish_smalltalk";
                turn.Line = B("I have been fishing for twenty years. My father taught me.", "About twenty years now — my dad taught me on this exact pier.", "Twenty-odd years. My dad brought me here when I was knee-high, and I've been hooked ever since — pun very much intended.", "Two decades, give or take. My father taught me patience here; the fish taught me humility.");
            }
            else if (Has("bait|lure|worm|what should i use|what do i use"))
            {
                turn.Objective = "fish_bait";
                turn.Line = B("Use worms. Fish like worms. Bread is OK for carp.", "Worms work for almost everything here. Bread's good for carp, and a lure is better if you're after a pike.", "Worms are the safe bet. Bread if you're after carp, and if you fancy your chances with the pike, switch to a lure — they go for movement.", "Worms, unless you're feeling ambitious — then a lure for the pike. Bread for carp, though the golden one's apparently got more refined tastes.");
            }
            else if (Has("how do i|how to|teach me|show me|what do i do|help me|yes|sure|ok|okay|please"))
            {
                turn.Objective = "fish_lesson";
                turn.Line = B("Press E to cast. Wait. When the float goes down, press E again. Then hold the mouse button to reel. Do not pull too hard.",
                    "Press E to cast, then watch the float. A little wobble is just a nibble — wait until it goes right under, then press E. Hold the left mouse button to reel, and let go when the line turns red.",
                    "Cast with E and keep an eye on the float. Don't jump at the first wobble — wait for it to properly go under, then strike with E. Reel with the left mouse button, and ease off when the tension creeps into the red.",
                    "Cast with E, then the hard part: patience. Ignore the teasing wobbles; strike only when the float plunges. Reel with the left mouse button and treat the tension bar like a temperamental friend — push, but know when to back off.");
            }
            else if (Has("nearly got away|almost got away|got away|escaped|lost it|so close"))
            {
                turn.Objective = "fish_smalltalk";
                turn.Line = B("Oh no! Try again. Let go when the line is red.", "Ah, so close! Next time ease off a bit when the line goes red.", "Ah, the one that got away — every angler's got a story like that. Ease off a touch sooner next time.", "Ha! Welcome to the club — we meet on Thursdays and exaggerate. Ease off a little sooner and it's yours.");
            }
            else if (Has("weather|rain|best time|when is the best|time of day"))
            {
                turn.Objective = "fish_types";
                turn.Line = B("Fish bite more in the rain. Morning and evening are good too.", "They bite faster in the rain, and trout like early morning and evening.", "Rain gets them feeding, and dusk is prime time for trout. Right now it's " + (weather == Weather.Rain ? "raining — perfect." : hour >= 17 && hour < 20 ? "dusk, so your timing's spot on." : "not bad, but come back at dusk."), "Rain's your friend, dusk is your best friend. Honestly, " + (weather == Weather.Rain ? "you couldn't have picked a better moment." : "come back when the sky turns orange."));
            }
            else if (Has("thanks|thank you|cheers|bye|see you"))
            {
                turn.Objective = "";
                turn.Actions = WaiterAction.EndConversation;
                turn.Line = B("You're welcome! Good luck!", "No problem — tight lines!", "Any time. Tight lines!", "Tight lines — and do come back to brag.");
            }
            else
            {
                turn.Progress = false;
                turn.Problem = "off_topic";
                turn.ModelAnswer = "What kind of fish are in this pond?";
                turn.Line = B("Sorry, I don't understand. You can ask me about fish or bait.", "Sorry, I didn't quite get that. Ask me anything about the fish or the bait!", "Hmm, not sure I followed. I'm best on fish, bait and the one that got away.", "You've lost me — but if it's about fish, bait, or tall tales, I'm your woman.");
            }
            return turn;
        }
    }

    [Serializable]
    public sealed class QuestDefinition
    {
        public string Id, Title, Location, Summary, Reward;
        public string[] Npcs = new string[0], Prerequisites = new string[0], Objectives = new string[0], ObjectiveLabels = new string[0];
        public int RewardMoney, RewardXp;
    }

    /// Scenario quests for the café slice. Objectives are learning goals completed by speaking turns.
    public static class QuestBook
    {
        public static readonly QuestDefinition[] Quests =
        {
            new QuestDefinition
            {
                Id = "cafe-first-order", Title = "Your first order", Location = "Forest Café", Npcs = new[] { "Rowan" },
                Summary = "Order a drink, customise it, and pay.",
                Objectives = new[] { "order_item", "confirm", "pay", "receive" },
                ObjectiveLabels = new[] { "Order something to drink", "Confirm your order", "Pay by card or cash", "Get your order delivered" },
                Reward = "$5 café credit", RewardMoney = 500, RewardXp = 20,
            },
            new QuestDefinition
            {
                Id = "cafe-dietary", Title = "Something dairy-free", Location = "Forest Café", Npcs = new[] { "Rowan" }, Prerequisites = new[] { "cafe-first-order" },
                Summary = "Ask about dairy-free options and customise your drink.",
                Objectives = new[] { "ask_dietary", "customise", "pay" },
                ObjectiveLabels = new[] { "Ask what is dairy-free", "Change the milk or another option", "Pay for the order" },
                Reward = "$5 café credit", RewardMoney = 500, RewardXp = 25,
            },
            new QuestDefinition
            {
                Id = "cafe-recommend-split", Title = "Treat a friend", Location = "Forest Café", Npcs = new[] { "Rowan" }, Prerequisites = new[] { "cafe-first-order" },
                Summary = "Ask for a recommendation, then split the bill.",
                Objectives = new[] { "ask_recommendation", "split_bill", "pay" },
                ObjectiveLabels = new[] { "Ask what Rowan recommends", "Ask to split the bill", "Pay your share" },
                Reward = "Wild berry pour-over badge", RewardMoney = 300, RewardXp = 30,
            },
            new QuestDefinition
            {
                Id = "pond-first-catch", Title = "Tight lines", Location = "Fishing pier", Npcs = new[] { "Hazel" },
                Summary = "Ask Hazel about the pond, then catch your first fish.",
                Objectives = new[] { "fish_types", "fish_bait", "catch_fish" },
                ObjectiveLabels = new[] { "Ask what fish live in the pond", "Ask which bait to use", "Catch a fish" },
                Reward = "Fishing XP and a cooked-fish voucher", RewardMoney = 0, RewardXp = 40,
            },
        };

        public static bool Available(QuestDefinition quest, IList<string> done) => !done.Contains(quest.Id) && quest.Prerequisites.All(done.Contains);

        /// Objectives earned during the current attempt; a quest completes once all are met.
        public static IEnumerable<QuestDefinition> Completed(IList<string> done, ICollection<string> earned) =>
            Quests.Where(quest => Available(quest, done) && quest.Objectives.All(earned.Contains));

        public static QuestDefinition Active(IList<string> done) => Quests.FirstOrDefault(quest => Available(quest, done));
    }
}
