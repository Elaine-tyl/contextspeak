using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Slice
{
    public sealed class LessonPlace
    {
        public string Id, Name, Npc, Role, Topics;
        public string[] BasicWords, PreciseWords, Simple, Everyday, Discussion;

        public LessonPlace(string id, string name, string npc, string role, string topics, string basic, string precise, string[] simple, string[] everyday, string[] discussion)
        {
            Id = id; Name = name; Npc = npc; Role = role; Topics = topics;
            BasicWords = basic.Split('|'); PreciseWords = precise.Split('|');
            Simple = simple; Everyday = everyday; Discussion = discussion;
        }
    }

    public sealed class LessonPlan
    {
        public string Goal;
        public string[] Prompts, Vocabulary;
    }

    /// Original learning material, not an official IELTS word list or exam question bank.
    public static class LifeCurriculum
    {
        // These are answer concepts for each beginner question, not required study words.
        private static readonly Dictionary<string, string[]> BasicAnswers = new Dictionary<string, string[]>
        {
            ["school"] = new[] { "english|math|science|art|history|geography|music|subject|study|language", "teacher|help|ask|listen|read|write|practice|practise|learn|explain|repeat", "friend|classmate|partner|together|alone|group|team|yes|no" },
            ["home"] = new[] { "pasta|rice|soup|salad|chicken|fish|pizza|dinner|sandwich|curry|noodle|egg|anything|food|meal", "cook|make|eat|yes|no|sometimes|often|never|usually", "i can|i will|i'll|we can|we'll|me|mom|mum|dad|mother|father|parent|friend|brother|sister|family" },
            ["cafe"] = new[] { "coffee|tea|water|juice|latte|cappuccino|espresso|chocolate|mocha|drink|smoothie|decaf|matcha", "hot|iced|cold|warm|either", "milk|oat|soy|almond|cream|dairy|yes|no|without|none" },
            ["fishing"] = new[] { "yes|no|like|love|enjoy|fish|never|sometimes|not really", "fish|water|pond|duck|bird|lily|bridge|tree|boat|float|rod|plant|reeds", "yes|no|quiet|peaceful|noisy|calm|busy|relax|not really" },
            ["market"] = new[] { "apple|banana|orange|pear|grape|melon|berry|berries|fruit|peach|plum|mango|pineapple|kiwi|cherry|cherries|lemon|lime", "fresh|healthy|taste|tasty|nutri|crisp|sweet|good|ripe|local|vitamin|flavour|flavor", "carrot|tomato|potato|onion|pea|bean|broccoli|cabbage|lettuce|spinach|pepper|cucumber|corn|vegetable|mushroom" },
            ["restaurant"] = new[] { "food|meal|rice|pasta|soup|salad|chicken|fish|steak|pizza|burger|noodle|sandwich|curry|dish|vegetarian|vegan", "spicy|mild|hot|yes|no|not really|a little", "table|outside|inside|terrace|patio|yes|no|either" },
            ["japan"] = new[] { "rice|noodle|ramen|either|both", "tea|water|yes|no|like|love|prefer", "sushi|ramen|rice|fish|noodle|dish|food|tempura|miso|tofu|curry|chicken|vegetarian" },
            ["park"] = new[] { "ride|wheel|carousel|coaster|train|bumper|water|teacup|swing", "yes|no|scared|afraid|height|fear|fine|not really|a little", "friend|family|child|parent|mom|mum|dad|partner|brother|sister|my|we|alone|i" },
            ["mall"] = new[] { "shirt|jumper|sweater|jacket|jeans|dress|skirt|trouser|pants|cloth|coat|shoe|sock|hoodie|tee|t shirt", "blue|red|green|black|white|cream|brown|pink|purple|yellow|orange|grey|gray|beige|navy|colour|color", "small|medium|large|size|extra|xl|xs|[0-9]+|not sure|don'?t know" },
        };

        public static bool AnswersQuestion(string scene, IeltsBand band, int step, string normalized)
        {
            var topics = band <= IeltsBand.Band4 ? BasicAnswers[Find(scene).Id][Math.Max(0, Math.Min(2, step))] : Find(scene).Topics;
            return Regex.IsMatch(normalized, @"\b(?:" + topics + @")");
        }

        private static readonly string[] Goals =
        {
            "Give a short, clear answer using familiar words.",
            "Link two simple ideas with and, but or because.",
            "Describe a personal experience and explain a preference.",
            "Explain a choice, compare alternatives and paraphrase when needed.",
            "Develop a viewpoint with a relevant example and a conditional.",
            "Weigh trade-offs, use precise collocations and acknowledge another view.",
            "Explore an unfamiliar angle, qualify claims and defend a nuanced position.",
        };

        public static readonly LessonPlace[] Places =
        {
            new LessonPlace("school", "Community classroom", "Ms Taylor", "English teacher", "school|learn|study|subject|class|teacher|education|skill|student|exam|test|homework|course|practice|practise|feedback|language|friend|group|team|independ|discuss|technology",
                "learn|subject|teacher|classmate", "constructive feedback|independent learning|practical skills|equal access",
                new[] { "What's your favourite subject?", "What do you do when you don't understand something?", "Do you like learning with classmates?" },
                new[] { "Tell me about a class that taught you something useful.", "How would you ask a teacher to explain something another way?", "Would you rather study with a partner or on your own? Why?" },
                new[] { "How should schools balance exam preparation with practical skills?", "Can technology give all students equal access to good teaching?", "When should a teacher step in rather than let learners solve a problem themselves?" }),
            new LessonPlace("home", "Family home", "Alex", "Your friend", "cook|meal|food|dinner|home|family|live|house|share|help|chore|room|clean|kitchen|pasta|rice|make|eat|together|responsib|parent|privacy|rent|work",
                "home|cook|dinner|help", "share the chores|household budget|work-life balance|independence",
                new[] { "What would you like for dinner?", "Do you cook at home?", "Who can help with dinner?" },
                new[] { "I'm making dinner. What do you usually cook when you're short on time?", "How would you split the cooking and cleaning if we shared this place?", "Tell me about a meal at home that you remember fondly." },
                new[] { "Do shared meals still matter when everyone has a different schedule?", "How would you negotiate an unequal division of household responsibilities?", "Should young adults prioritise independence or living with family?" }),
            new LessonPlace("cafe", "Forest Café", "Maya", "Barista", "coffee|tea|drink|latte|milk|cup|cafe|café|order|taste|sweet|bitter|price|cost|sugar|oat|decaf|friend|social|space|local|chain|business|community|serve|prefer",
                "coffee|milk|hot|cup", "locally roasted|dietary preference|independent business|community space",
                new[] { "What would you like to drink?", "Hot or iced?", "Would you like milk?" },
                new[] { "What makes a café somewhere you'd want to come back to?", "Your usual drink is sold out. What would you choose instead, and why?", "Tell me about a time you met someone over coffee." },
                new[] { "Can an independent café compete with a chain without losing its identity?", "Should cafés charge more if they source ingredients responsibly?", "How do places like this influence a neighbourhood's sense of community?" }),
            new LessonPlace("fishing", "Lakeside pier", "Finn", "Fishing hobbyist", "fish|pond|water|river|lake|bait|rod|catch|quiet|nature|outdoor|relax|release|patien|animal|wildlife|protect|conserv|ecosystem|touris|environment|habitat",
                "fish|water|rod|quiet", "catch and release|natural habitat|conservation|biodiversity",
                new[] { "Do you like fishing?", "What can you see in the pond?", "Is it quiet here?" },
                new[] { "What do you enjoy doing outdoors when you need to switch off?", "Would you keep a fish or release it? Tell me why.", "Describe a peaceful place you have visited." },
                new[] { "How can a town balance outdoor tourism with protecting wildlife?", "Is catch-and-release fishing compatible with animal welfare?", "Who should be responsible for protecting a shared natural habitat?" }),
            new LessonPlace("market", "Town market", "Rose", "Produce vendor", "apple|carrot|tomato|fruit|vegetable|fresh|buy|price|cost|local|food|farm|season|organic|ripe|waste|shop|grow|import|supply|cheap|market|quality|packag|afford|trade",
                "fresh|apple|carrot|price", "seasonal produce|food waste|supply chain|value for money",
                new[] { "What fruit would you like?", "Why do you like fresh food?", "What vegetables do you buy?" },
                new[] { "I'm choosing vegetables for dinner. What would you recommend, and why?", "Would you pay a little more for locally grown produce?", "Tell me about a market you enjoy visiting." },
                new[] { "How would you weigh the price of imported food against supporting local growers?", "What could this market realistically do to reduce food waste?", "Is buying seasonal produce always the more sustainable option?" }),
            new LessonPlace("restaurant", "Garden restaurant", "June", "Waitress", "food|meal|eat|dinner|table|menu|dish|pasta|rice|soup|salad|allerg|serve|tip|service|bill|diet|vegan|vegetarian|restaurant|hospital|culture|ingredient|taste|cook|prefer|spicy",
                "menu|table|soup|bill", "dietary requirement|service charge|locally sourced|hospitality",
                new[] { "What would you like to eat?", "Do you like spicy food?", "Would you like a table outside?" },
                new[] { "What do you look for when choosing a restaurant with friends?", "The dish you wanted contains an ingredient you can't eat. How would you handle that?", "Tell me about a memorable meal out." },
                new[] { "Does a compulsory service charge improve hospitality or undermine choice?", "How should a restaurant balance dietary requirements with a traditional menu?", "What makes a dining experience feel authentic rather than staged?" }),
            new LessonPlace("japan", "Sakura kitchen", "Ren", "Restaurant host", "sushi|rice|fish|noodle|ramen|tea|chopstick|food|dish|menu|japan|culture|tradition|local|taste|travel|respect|etiquette|custom|authentic|adapt|meal|vegetarian|ingredient|restaurant",
                "rice|tea|noodles|fish", "dining etiquette|cultural exchange|regional cuisine|adapt a recipe",
                new[] { "Would you like rice or noodles?", "Do you like tea?", "What food would you like to try?" },
                new[] { "You're trying a new cuisine. How would you ask the host for advice?", "What would you do if you weren't sure about the dining customs?", "Describe a dish from another country that you enjoyed." },
                new[] { "When restaurants adapt a traditional dish, what should they preserve?", "Can food tourism encourage cultural understanding as well as stereotypes?", "How would you respond respectfully to an unfamiliar dining custom?" }),
            new LessonPlace("park", "Meadow amusement park", "Sky", "Ride attendant", "ride|ticket|wheel|queue|wait|friend|park|fun|scared|safe|safety|access|crowd|family|child|cost|price|risk|thrill|height|fair|disab|refund|close|weather|rain|plan",
                "ticket|ride|wait|safe", "queue management|accessible attraction|safety precaution|value for money",
                new[] { "Which ride would you like to try?", "Are you scared of heights?", "Who is coming with you?" },
                new[] { "We've only got an hour. Which attractions would you prioritise?", "Your friend's nervous about this ride. What would you say?", "A ride closes after you bought a ticket. How would you ask for help?" },
                new[] { "How should a park balance excitement with accessibility and safety?", "Is it fair to charge visitors extra to skip a queue?", "How would you handle competing preferences when planning a group day out?" }),
            new LessonPlace("mall", "Willow shopping gallery", "Ellie", "Clothing adviser", "shirt|jumper|sweater|jacket|jeans|cloth|size|fit|wear|colour|color|buy|shop|price|cost|return|refund|quality|fabric|fashion|sustain|repair|brand|consumer|durab|budget|prefer|dress",
                "shirt|size|fit|price", "durable fabric|return policy|fast fashion|impulse purchase",
                new[] { "What clothes are you looking for?", "What colour do you like?", "What size do you need?" },
                new[] { "You're buying a jacket for a trip. What matters most to you?", "It fits, but it's over your budget. What would you do?", "Tell me about something you bought that lasted a long time." },
                new[] { "How do you balance affordability, durability and environmental impact when shopping?", "Should brands or consumers bear more responsibility for fast fashion?", "How could this shop encourage repair without undermining its business?" }),
        };

        // Residents share an authored topic but keep independent progress, e.g. park:Kai.
        public static LessonPlace Find(string id) => Places.FirstOrDefault(place => place.Id == id?.Split(':')[0]) ?? Places[0];

        public static LessonPlan Create(string scene, IeltsBand band)
        {
            band = BandProfile.For(band).Band;
            var place = Find(scene);
            var questions = band <= IeltsBand.Band4 ? place.Simple : band <= IeltsBand.Band6 ? place.Everyday : place.Discussion;
            string FollowUp(int index)
            {
                if (band == IeltsBand.Band4) return index == 1 ? " Give a simple reason." : " Add one detail.";
                if (band == IeltsBand.Band6) return index == 2 ? " Explain why it mattered." : " Explain your choice.";
                if (band == IeltsBand.Band8) return index == 1 ? " Consider a different point of view." : " Explain a possible trade-off.";
                if (band == IeltsBand.Band9) return index == 2 ? " Under what circumstances would your view change?" : " Can you think of a counterexample?";
                return "";
            }
            return new LessonPlan
            {
                Goal = Goals[(int)band - 3],
                Vocabulary = band <= IeltsBand.Band4 ? place.BasicWords : place.BasicWords.Take(1).Concat(place.PreciseWords).ToArray(),
                Prompts = questions.Select((question, index) => question + FollowUp(index)).ToArray(),
            };
        }
    }

    public sealed class LessonAnswer
    {
        public int Step;
        public bool Accepted;
        public string Prompt, Hint, Answer;
    }

    /// Preview/commit prevents a corrected or duplicate reply from skipping a question.
    [Serializable]
    public sealed class SceneLesson
    {
        public string SceneId;
        public IeltsBand Band;
        public int Step;
        public string LastAnswer = "";
        public bool Complete => Step >= 3;
        public LessonPlan Plan => LifeCurriculum.Create(SceneId, Band);
        public string Prompt => Complete ? "Thanks for the chat! You completed this scene. Try another place, or retry from the map." : Plan.Prompts[Math.Max(0, Step)];

        public LessonAnswer Propose(string text, bool? relevantAssessment = null, bool? goalAssessment = null)
        {
            var normalized = OrderIntentParser.Normalize(text ?? "");
            var relevant = relevantAssessment ?? (normalized.Length >= 2 && LifeCurriculum.AnswersQuestion(SceneId, Band, Step, normalized));
            var developed = Band < IeltsBand.Band6 || Regex.IsMatch(normalized, @"\b(because|since|although|whereas|while|however|but|for example|for instance|on the other hand|so that|in contrast|depends on)\b");
            var qualified = Band < IeltsBand.Band9 || Regex.IsMatch(normalized, @"\b(might|could|may|tend|depends|unless|sometimes|not always|in some|provided|to an extent|although|even if)\b");
            var goalMet = goalAssessment ?? (developed && qualified);
            var accepted = !Complete && relevant && goalMet && normalized != LastAnswer;
            return new LessonAnswer
            {
                Step = Step, Accepted = accepted, Answer = normalized,
                Prompt = accepted ? Step == 2 ? "Thanks — you've completed this conversation!" : Plan.Prompts[Step + 1] : Prompt,
                Hint = accepted ? "" : !relevant ? "Answer this question in your own words. The vocabulary hints are optional."
                    : !goalMet ? "Develop your answer to address the question and the speaking goal. Give a reason, example or another perspective."
                    : "Try answering the new question rather than repeating your previous reply.",
            };
        }

        public bool Accept(LessonAnswer answer)
        {
            if (answer == null || !answer.Accepted || Complete || answer.Step != Step) return false;
            LastAnswer = answer.Answer;
            Step++;
            return true;
        }
    }
}
