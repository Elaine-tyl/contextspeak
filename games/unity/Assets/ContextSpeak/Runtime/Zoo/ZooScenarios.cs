using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Wildlife zoo conversations. Each line exists at Bands 3–4 / 5–6 / 7–8 / 9. `meaning` lists what a
    /// good answer must convey (any reasonable wording passes — never exact text matching). The shared
    /// dialogue state machine then gives feedback, shows a corrected sentence and requires the learner
    /// to repeat it before the next line. Tokens: {ticket} {price} {animal}.
    public static class ZooScenarios
    {
        private const string Yes = "yes|yeah|yep|sure|of course|okay|ok|that'?s right|correct|i do|i will|i understand|got it|please";
        private const string Thanks = "thank|thanks|cheers|great|perfect|lovely|okay|ok|fine|bye|appreciate|wonderful";
        private const string Order = "i'?d like|can i|could i|please|have|get|i'?ll take";
        private const string Because = "because|since|so that|as |due to|that'?s why|which means|therefore";

        public static readonly string[] Names =
        {
            "Ticket Officer", "Zoo Guide Welcome", "Elephant Keeper", "Giraffe Keeper", "Zoo Café Barista", "Conservation Discussion",
            "Lost Visitor", "Farm Keeper", "Conservation Volunteer", "Tram Operator",
        };

        private static ScenarioStep S(string id, string speaker, string simple, string standard, string advanced, string expert, string goal, string meaning, string model,
            string vocabulary, string onComplete = "") => new ScenarioStep
        {
            id = id, speaker = speaker, simple = simple, standard = standard, advanced = advanced, expert = expert, goal = goal,
            meaning = meaning, model = model, vocabulary = vocabulary, onComplete = onComplete,
        };

        public static ScenarioDefinition Create(string title, IDictionary<string, string> tokens = null)
        {
            var steps = Steps(title);
            if (steps == null) return null;
            string Fill(string text)
            {
                if (string.IsNullOrEmpty(text) || tokens == null) return text;
                foreach (var pair in tokens) text = text.Replace("{" + pair.Key + "}", pair.Value);
                return text;
            }
            foreach (var step in steps)
            {
                step.simple = Fill(step.simple); step.standard = Fill(step.standard); step.advanced = Fill(step.advanced); step.expert = Fill(step.expert);
                step.goal = Fill(step.goal); step.meaning = Fill(step.meaning); step.model = Fill(step.model);
            }
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.name = title; scenario.Title = title; scenario.Location = "Metro City Wildlife Zoo"; scenario.Steps = steps;
            return scenario;
        }

        private static List<ScenarioStep> Steps(string title)
        {
            switch (title)
            {
                case "Ticket Officer": return new List<ScenarioStep>
                {
                    S("hello", "ticket", "Hello! One ticket?", "Good morning, welcome to the zoo! How many tickets would you like?",
                        "Good morning and welcome to Metro City Wildlife Zoo. How many of you are visiting today?",
                        "Good morning, welcome. Is it just yourself today, or are you part of a larger group I should know about?",
                        "Say how many tickets you need.", "one|two|three|four|a ticket|ticket|just me|myself|alone|only me|family|friends|adult|student",
                        "Just one ticket, please.", "admission: the price to go in|adult: a grown-up"),
                    S("type", "ticket", "Standard ticket or family ticket?", "Would you like a standard ticket or a family ticket?",
                        "We have standard, student and family tickets. Which suits you best?",
                        "We offer standard, student and family admission — which would represent the best value for your visit?",
                        "Choose a ticket type (you will pay on the screen).", "standard|family|student|adult|normal|regular|single|one",
                        "A standard ticket, please.", "standard: the normal kind|student: someone who studies", "choose-ticket"),
                };
                case "Zoo Guide Welcome": return new List<ScenarioStep>
                {
                    S("first", "guide", "Hello! Which animal do you want to see?", "Hi, I'm your guide! Which animals would you like to see first?",
                        "Welcome! I'm one of the guides here. Which animals are you most looking forward to seeing, and why?",
                        "Welcome to the zoo. If you could only spend your morning with one species, which would it be, and what draws you to it?",
                        "Say which animals you want to see first.", "elephant|giraffe|zebra|antelope|rhino|lion|tiger|penguin|bird|monkey|animal|sheep|pony|all|everything|savannah|farm",
                        "I'd like to see the elephants first.", "look forward to: feel happy about something in the future|species: a kind of animal"),
                    S("route", "guide", "Elephants are that way. Left. OK?", "The elephants are on the left side of the savannah. Follow the gravel path. Is that clear?",
                        "The savannah is straight ahead: elephants gather by the waterhole on the west side and giraffes browse on the east. Shall I mark them on your map?",
                        "The savannah lies straight ahead; the herd tends to congregate at the western waterhole around this hour. Would it help if I pointed that out on your map?",
                        "Show you understood the directions.", Yes + "|left|west|path|straight|map|clear|" + Thanks,
                        "Yes, that's clear. I'll follow the path on the left. Thank you!", "gravel: small stones|waterhole: a pool where animals drink|browse: eat leaves", "guide-met"),
                };
                case "Elephant Keeper": return new List<ScenarioStep>
                {
                    S("describe", "keeper-elephant", "Look at the elephant. Is it big or small?", "Can you describe what an elephant looks like?",
                        "Have a good look at the herd. How would you describe an elephant to someone who had never seen one?",
                        "Watch them for a moment. How would you convey the physical presence of an elephant to someone who had never encountered one?",
                        "Describe an elephant (size, colour, trunk, ears, tusks).", "big|huge|large|enormous|grey|gray|trunk|ears|tusk|heavy|tall|long nose|wrinkl|massive|strong",
                        "An elephant is a huge grey animal with a long trunk and big ears.", "trunk: an elephant's long nose|tusk: a long curved tooth|wrinkled: with many lines in the skin"),
                    S("doing", "keeper-elephant", "What is the elephant doing?", "What are the elephants doing right now?",
                        "What can you see the herd doing at the moment? Why do you think they do that?",
                        "Describe what the herd is doing at the moment, and speculate about the reasons behind that behaviour.",
                        "Say what the elephants are doing.", "drink|eat|walk|stand|rest|sleep|play|graz|flap|water|look|follow|trunk|together|baby|calf|hot|cool",
                        "They are walking to the water to drink.", "herd: a group of elephants|calf: a baby elephant", "elephant-described"),
                };
                case "Giraffe Keeper": return new List<ScenarioStep>
                {
                    S("eat", "keeper-giraffe", "What do giraffes eat?", "Can you describe what giraffes usually eat?",
                        "Giraffes spend most of the day feeding. What do they usually eat, and how does their body help them?",
                        "Giraffes devote most of their waking hours to feeding. What does their diet consist of, and how are they adapted to it?",
                        "Say what giraffes eat.", "lea(f|ves)|tree|acacia|plant|branch|shoot|twig|vegetarian|herbivore|green",
                        "Giraffes usually eat leaves from tall trees.", "leaves: the green parts of a tree|acacia: an African tree|herbivore: an animal that eats plants"),
                    S("compare", "keeper-giraffe", "Giraffe and zebra: which is taller?", "How are giraffes and zebras different?",
                        "Look at the giraffes and the zebras. How would you compare them?",
                        "Compare the giraffes and the zebras — in appearance, in how they feed, and in how they avoid predators.",
                        "Compare giraffes and zebras (use -er, more, than, but, while).", "taller|shorter|longer|smaller|bigger|faster|than|but|while|whereas|stripe|neck|both|different|more|less",
                        "Giraffes are much taller than zebras, but zebras have stripes.", "taller than: more tall than|whereas: but, in contrast|stripes: long lines of colour", "animals-compared"),
                };
                case "Zoo Café Barista": return new List<ScenarioStep>
                {
                    S("order", "barista", "Hi! What drink?", "Hi there! What can I get you?", "Hello! What can I get for you today — something hot, or a cold juice?",
                        "Hello there. What can I get started for you — a coffee, or perhaps something cold after all that walking?",
                        "Order what you chose on the menu (say the size).", Order, "Can I have a large orange juice, please?", "large / regular / small: sizes|freshly squeezed: made from fresh fruit"),
                    S("here", "barista", "For here or to take away?", "Is that to have here or to take away?", "Will you be sitting in, or taking it with you round the zoo?",
                        "Are you planning to sit in, or would you rather take it with you while you look around?",
                        "Say if you will drink it here or take it away.", "here|sit|stay|table|take ?away|to go|with me|carry|walk",
                        "To have here, please.", "take away: to drink somewhere else", "ordered"),
                };
                case "Conservation Discussion": return new List<ScenarioStep>
                {
                    S("why", "guide", "Some animals are in danger. Why?", "Why do you think some animals are endangered?",
                        "Many species here are endangered in the wild. What do you think the main reasons are?",
                        "A number of the species you've seen today are in steep decline in the wild. What do you see as the underlying causes?",
                        "Give at least one reason (use 'because').", "hunt|poach|habitat|forest|cut|trees|home|climate|pollut|farm|city|cities|people|humans|plastic|ivory|food|water|land|" + Because,
                        "Some animals are endangered because people destroy their habitat.", "endangered: at risk of disappearing|habitat: an animal's natural home|poaching: illegal hunting"),
                    S("help", "guide", "How can people help animals?", "What can ordinary people do to help protect wildlife?",
                        "What practical things could people like us do to help protect these animals?",
                        "What realistic steps could individuals take, and how much difference do you think they actually make?",
                        "Suggest a way to help.", "protect|donate|money|recycle|plastic|learn|teach|visit|support|plant|stop|buy|save|volunteer|reduce|respect|law|park|reserve|should|could|can",
                        "People can support wildlife parks and stop buying products made from wild animals.", "protect: keep safe|donate: give money to help|reserve: land kept for wildlife"),
                    S("zoos", "guide", "Are zoos good for animals? Why?", "Do you think zoos are good for animals? Why or why not?",
                        "Some people say zoos help conservation; others worry about animal welfare. What is your opinion?",
                        "Do you believe zoos contribute more to conservation than they do to animal welfare concerns? Justify your view.",
                        "Give your opinion with a reason.", "i think|i believe|in my opinion|i feel|agree|disagree|good|bad|help|protect|learn|educat|breed|space|free|wild|cage|welfare|conservation|research|" + Because,
                        "I think good zoos help because they protect endangered animals and teach people about them.",
                        "welfare: health and happiness|conservation: protecting nature|breeding programme: helping rare animals have babies", "conservation-done"),
                };
                case "Lost Visitor": return new List<ScenarioStep>
                {
                    S("ask", "visitor-lost", "Excuse me. Where are the giraffes?", "Excuse me, do you know where the giraffes are?",
                        "Sorry to bother you — I'm trying to find the giraffes and I'm completely lost. Could you point me in the right direction?",
                        "Forgive the interruption — I've managed to lose my bearings entirely. Would you happen to know how I get to the giraffes?",
                        "Give directions to the giraffes (east side of the savannah).", "right|east|straight|path|follow|past|next to|behind|along|turn|caf[eé]|deck|savannah|sign|minutes|over there|that way",
                        "Follow the path to the right, past the café. The giraffes are on the east side.", "past: going beyond|follow: go along"),
                    S("thanks", "visitor-lost", "Thank you! Is it far?", "Thanks! Is it far from here?", "That's really helpful, thank you. Is it much of a walk?",
                        "That's most helpful. Is it much of a trek from here?",
                        "Answer.", "not far|no|near|close|minute|short|two|three|five|just|welcome|pleasure|far", "No, it's only about three minutes.", "", "visitor-helped"),
                };
                case "Farm Keeper": return new List<ScenarioStep>
                {
                    S("which", "keeper-farm", "Hello! Do you like farm animals?", "Hi! Which farm animal do you like best?",
                        "Welcome to the children's farm! Which of our animals do you like most, and why?",
                        "Welcome to the farm. Which of these animals appeals to you most, and what is it about them?",
                        "Name a farm animal you like.", "sheep|pony|ponies|pig|chicken|goat|rabbit|horse|lamb|like|love|favourite|favorite|cute|all",
                        "I like the pony best because it's very gentle.", "gentle: calm and kind|favourite: the one you like most"),
                    S("feed", "keeper-farm", "We feed the sheep hay. No bread! OK?", "We only give the animals special food from the keepers. Can you tell me why?",
                        "Visitors mustn't give the animals their own food — only the feed we provide. Why do you think that rule matters?",
                        "We never allow visitors to feed the animals anything of their own. Why do you suppose that rule is so strictly enforced?",
                        "Explain the feeding rule (or agree to it).", Yes + "|sick|ill|healthy|health|safe|wrong food|bad for|stomach|diet|danger|harm|only|special|rule|" + Because,
                        "Because the wrong food can make the animals sick.", "feed: give food to|hay: dry grass for animals|keeper: a person who looks after zoo animals", "farm-learned"),
                };
                case "Conservation Volunteer": return new List<ScenarioStep>
                {
                    S("aware", "volunteer", "Rhinos are in danger. Do you know why?", "Do you know why rhinos are in danger in the wild?",
                        "We're raising awareness about rhinos today. Do you know what the biggest threat to them is?",
                        "We're highlighting the plight of rhinos today. Are you aware of what's driving their decline?",
                        "Say why rhinos are in danger.", "horn|hunt|poach|kill|illegal|money|sell|habitat|people|medicine|" + Because,
                        "Rhinos are in danger because people hunt them for their horns.", "poaching: illegal hunting|threat: a danger"),
                    S("promise", "volunteer", "Will you tell a friend?", "Will you tell someone else what you learned today?",
                        "Would you be willing to share what you've learned with someone else?",
                        "Might you be prepared to pass on what you've learned to others?",
                        "Agree to share.", Yes + "|tell|share|friend|family|post|talk|teach|class|definitely|certainly|happy to", "Yes, I'll tell my friends about it.", "", "awareness-done"),
                };
                case "Tram Operator": return new List<ScenarioStep>
                {
                    S("ask", "tram", "Hello! The tram is not ready.", "Hello! The safari tram isn't running yet. Would you like to know when it opens?",
                        "Hello! I'm afraid the safari tram is still being built. Would you like to hear what the tour will include?",
                        "Good day. Regrettably the safari tram has yet to enter service. Might I tell you what the tour will cover once it does?",
                        "Ask when it opens or what the tour includes.", "when|open|what|include|see|tour|how long|how much|price|ticket|stops|yes|tell me",
                        "Yes, please. When does the tram open?", "tour: a guided trip|include: have as a part"),
                };
            }
            return null;
        }
    }
}
