using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Palm Bay conversations. Bands 3–4 / 5–6 / 7–8 / 9 each get their own line. Tokens:
    /// {activity} {equipment} {safety} {objective} {price} {room} {roomType} {nights}.
    public static class BeachScenarios
    {
        private const string Yes = "yes|yeah|yep|sure|of course|okay|ok|that'?s right|correct|i do|i will|i understand|understood|got it";
        private const string No = "no|nope|not|nothing|none|never|i don'?t";
        private const string Thanks = "thank|thanks|cheers|great|perfect|lovely|okay|ok|fine|bye|appreciate|wonderful";
        private const string Numbers = @"\d|one|two|three|four|five|six|seven|eight|nine|ten";
        private const string Order = "i'?d like|can i|could i|please|have|get|i'?ll take";

        public static readonly string[] Names =
        {
            "Beach Barista", "Beach Restaurant Host", "Beach Waiter Order", "Beach Waiter Bill", "Watersports Desk", "Safety Briefing",
            "Lifeguard", "Dive Instructor Marine Life", "Boat Captain", "Beach Hotel Reception", "Room Service Phone", "Hotel Checkout",
            "Souvenir Seller", "Lost Bag Tourist", "Directions Tourist", "Beach Cleanup Volunteer", "Beach Tourist Chat", "Beach Club Bartender",
        };

        private static ScenarioStep S(string id, string speaker, string simple, string standard, string advanced, string goal, string meaning, string model,
            string vocabulary, string expert = "", string onComplete = "") => new ScenarioStep
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
            scenario.name = title; scenario.Title = title; scenario.Location = "Palm Bay"; scenario.Steps = steps;
            return scenario;
        }

        private static List<ScenarioStep> Steps(string title)
        {
            switch (title)
            {
                case "Beach Barista": return new List<ScenarioStep>
                {
                    S("order", "barista", "Hi! What drink?", "Hi there! What can I get you today?", "Hey! What can I make for you — something cold to cool down, or a coffee?",
                        "Order what you chose on the menu (say the size).", Order, "Can I have a large mango smoothie, please?", "large / regular / small: sizes|less sugar: not too sweet"),
                    S("ice", "barista", "Ice?", "Would you like ice with that?", "Do you want it over ice, or straight up?",
                        "Answer about ice.", Yes + "|" + No + "|ice|no ice|without|with|little", "Yes, with ice, please.", "over ice: with ice"),
                    S("name", "barista", "Your name?", "Can I get a name for the order?", "And what name shall I call out?",
                        "Tell them your name.", "i'?m|it'?s|my name|name is|call me|for", "It's Emma.", "", "", "ordered"),
                };
                case "Beach Restaurant Host": return new List<ScenarioStep>
                {
                    S("table", "host", "Hello! A table?", "Good evening, welcome to Coral Kitchen! Would you like a table?",
                        "Good evening and welcome to Coral Kitchen. Are you dining with us tonight — and do you have a booking?",
                        "Ask for a table.", "table|yes|please|book|reservation|dinner|for one|for two|eat|can i|could i|we'?d like|i'?d like", "Yes, a table for one, please.", "dining: eating a meal"),
                    S("where", "host", "Inside or outside?", "Would you prefer to sit inside or out on the deck?",
                        "We've got tables inside by the open kitchen, or out on the deck facing the sea. Which do you prefer?",
                        "Choose indoor or outdoor.", "inside|indoor|in|outside|outdoor|out|deck|sea|view|beach|either|don'?t mind", "Outside on the deck, please.", "deck: a wooden floor outside", "", "seat"),
                };
                case "Beach Waiter Order": return new List<ScenarioStep>
                {
                    S("order", "waiter", "What would you like?", "Are you ready to order?", "Have you decided, or would you like to hear tonight's catch?",
                        "Order what you chose on the menu.", Order, "I'd like the grilled fish, please.", "catch of the day: the freshest fish"),
                    S("ingredients", "waiter", "Any questions about the food?", "Do you have any questions about the dishes?",
                        "Anything you'd like to know about the dishes — how they're cooked, or what's in them?",
                        "Ask about ingredients or allergens.", "what'?s in|ingredient|allerg|nuts|peanut|gluten|dairy|shellfish|spicy|how is|cooked|contain|vegetarian|vegan|is there|does it",
                        "Does the curry contain any nuts?", "allergen: something that can cause an allergy|contain: have inside"),
                    S("allergy", "waiter", "Any allergies?", "Do you have any allergies I should tell the chef about?",
                        "Should I let the chef know about any allergies or dietary requirements?",
                        "Answer about allergies.", "allerg|" + No + "|yes|nut|peanut|gluten|dairy|shellfish|vegetarian|vegan|fine", "No allergies, thank you.", "dietary requirements: food you can't eat", "", "order-placed"),
                };
                case "Beach Waiter Bill": return new List<ScenarioStep>
                {
                    S("how", "waiter", "Was the food good?", "How was everything?", "How was your meal? Was the fish cooked how you like it?",
                        "Say how the meal was.", "good|great|delicious|nice|lovely|fine|amazing|tasty|excellent|perfect|bit|too|enjoy", "It was delicious, thank you.", "delicious: very tasty"),
                    S("bill", "waiter", "Anything else?", "Can I get you anything else?", "Can I tempt you with dessert, or shall I bring the bill?",
                        "Ask for the bill.", "bill|check|pay|that'?s all|no|just", "Could I have the bill, please?", "bill: the paper with the price", "", "bill"),
                };
                case "Watersports Desk": return new List<ScenarioStep>
                {
                    S("ask", "watersports", "Hello! Want to do a water sport?", "Hi! What would you like to do today?", "Hi there! Are you after something relaxing, or something with a bit more adrenaline?",
                        "Ask what activities are available.", "what|which|activit|available|can i|do you have|options|price|how much|offer|recommend",
                        "What activities do you have today?", "available: you can do it now|adrenaline: excitement"),
                    S("level", "watersports", "Have you done it before?", "Have you tried any water sports before?", "What's your experience in the water — beginner, or have you done this before?",
                        "Say your experience level.", "beginner|first time|never|before|yes|no|a few times|experience|once|twice|confident|nervous|good swimmer", "I'm a beginner, but I'm a good swimmer.",
                        "beginner: someone just starting"),
                    S("choose", "watersports", "Which one?", "Which one would you like to book?", "So, what takes your fancy?",
                        "Name the activity you want (you'll book it on the screen).", "kayak|jet ski|jetski|snorkel|scuba|div|surf|paddle|fishing|banana|parasail|boat|tour|yacht|swim",
                        "I'd like to try kayaking, please.", "take your fancy: you would like", "", "booking"),
                };
                case "Safety Briefing": return new List<ScenarioStep>
                {
                    S("brief", "instructor", "Safety first: {safety} OK?", "Before we start: {safety} Is that clear?",
                        "Quick safety briefing: {safety} Any questions so far?",
                        "Show you understood the safety rules.", Yes + "|clear|question|what if|how", "Yes, that's clear. Thank you.", "safety briefing: rules to stay safe"),
                    S("help", "instructor", "If you need help, what do you do?", "What will you do if you get into trouble out there?",
                        "And if something goes wrong — you feel tired or you drift too far — what do you do?",
                        "Explain how you would ask for help.", "raise|arm|wave|help|signal|whistle|shout|call|stay|hold|float|board|kayak|lifeguard",
                        "I'll raise my arm and wave to the lifeguard.", "raise your arm: put your arm up", "", "briefed"),
                };
                case "Lifeguard": return new List<ScenarioStep>
                {
                    S("flags", "lifeguard", "Hi! Swim between the flags, OK?", "Hi! Please swim between the red-and-yellow flags today. Are you a confident swimmer?",
                        "Afternoon! There's a slight rip by the rocks, so please stay between the flags. How confident are you in the sea?",
                        "Answer the lifeguard.", Yes + "|" + No + "|confident|good|swimmer|not very|okay|flags|between|careful", "Yes, I'm a confident swimmer. I'll stay between the flags.",
                        "rip (current): water pulling away from the beach|flags: markers for the safe area"),
                };
                case "Dive Instructor Marine Life": return new List<ScenarioStep>
                {
                    S("why", "dive", "Do you like fish?", "Do you know why coral reefs are important?", "Why do you think coral reefs matter so much, both for fish and for people?",
                        "Give a reason reefs matter.", "home|fish|food|protect|coast|waves|tourism|species|life|habitat|oxygen|beautiful|important|because",
                        "Coral reefs are home to lots of fish, and they protect the coast.", "habitat: the natural home of an animal", "",
                        ""),
                    S("rule", "dive", "Can you touch coral?", "Should you touch the coral when you snorkel?", "What's the one rule you should never break around a reef?",
                        "Say you won't touch the coral.", "never|don'?t|not|touch|stand|kick|careful|distance", "No, you should never touch the coral.", "", "", "marine-learned"),
                };
                case "Boat Captain": return new List<ScenarioStep>
                {
                    S("hello", "captain", "Hello! Boat trip?", "Ahoy! Are you interested in a boat trip today?", "Afternoon! Fancy getting out on the water — a tour of the bay, or the sunset cruise?",
                        "Say what you're interested in.", "tour|trip|boat|cruise|yacht|sunset|fishing|banana|parasail|yes|how much|when|price|book", "Yes, I'd love a tour of the bay. How much is it?",
                        "bay: a curved part of the coast|cruise: a relaxing boat trip"),
                    S("time", "captain", "We go now. OK?", "We leave in five minutes from the end of the pier. Is that OK?",
                        "Next departure is in five minutes from the end of the pier — does that work for you?",
                        "Confirm.", Yes + "|" + Thanks + "|works|fine|great|perfect", "That works, thank you!", "departure: when it leaves", "", "excursion"),
                };
                case "Beach Hotel Reception": return new List<ScenarioStep>
                {
                    S("hello", "reception", "Hello! Check in?", "Good afternoon, welcome to the Palm Bay Resort. Are you checking in?",
                        "Good afternoon and welcome to Palm Bay Resort. Do you have a reservation with us, or are you looking for a room?",
                        "Say you want to check in (or need a room).", "check in|checking in|reservation|booking|room|stay|book|yes|night", "Yes, I'd like to check in. I have a reservation.", "check in: register at a hotel"),
                    S("name", "reception", "Your name, please?", "Could I have your name, please?", "May I take the name on the reservation, and could I see some ID?",
                        "Give your name.", "i'?m|it'?s|my name|name is|under|here", "It's under Emma Lee.", "ID: identification, e.g. a passport"),
                    S("room", "reception", "{roomType}, {nights} nights. OK?", "I have you in a {roomType} for {nights} nights. Is that right?",
                        "You're in room {room}, a {roomType}, for {nights} nights, with breakfast from seven. Does that all sound right?",
                        "Confirm the booking.", Yes + "|" + Numbers + "|night|room|right|perfect|great", "Yes, that's right. Thank you.", "", "", "checked-in"),
                };
                case "Room Service Phone": return new List<ScenarioStep>
                {
                    S("order", "reception", "Room service. What would you like?", "Room service, good evening. What can I bring you?", "Good evening, in-room dining. What can I get for you tonight?",
                        "Order what you chose.", Order, "Could I have a club sandwich and a juice, please?", "room service: food brought to your room"),
                    S("room", "reception", "Room number?", "And your room number?", "Lovely. Which room are you in?",
                        "Give your room number ({room}).", "{room}|room|" + Numbers + "|villa|v", "Room {room}.", "", "", "room-service"),
                };
                case "Hotel Checkout": return new List<ScenarioStep>
                {
                    S("out", "reception", "Check out?", "Good morning! Are you checking out today?", "Good morning — checking out? I hope you enjoyed your stay.",
                        "Say you're checking out.", "check out|checking out|leave|leaving|yes|key|bill|pay", "Yes, I'm checking out. Here's my key.", "check out: leave a hotel"),
                    S("stay", "reception", "Was it good?", "How was your stay?", "Was everything to your satisfaction during your stay?",
                        "Say how your stay was.", "good|great|lovely|wonderful|amazing|nice|fine|perfect|enjoy|relax|bit|noisy|beautiful", "It was wonderful, thank you.", "to your satisfaction: as you wanted", "", "checkout"),
                };
                case "Souvenir Seller": return new List<ScenarioStep>
                {
                    S("look", "souvenir", "Hello! Looking for a gift?", "Hi! Are you looking for anything special?", "Hello! Browsing, or looking for a gift for someone back home?",
                        "Say what you're looking for.", "gift|present|souvenir|looking|just|browsing|for my|bracelet|shell|shirt|hat|something|how much", "I'm looking for a gift for my friend.", "souvenir: something to remember a trip"),
                    S("deal", "souvenir", "This one is good. OK?", "This shell bracelet is popular. Would you like it?", "These are handmade by local artists. Can I interest you in one?",
                        "Accept, decline or ask the price.", Yes + "|" + No + "|how much|price|discount|cheaper|maybe|think|look", "How much is it?", "handmade: made by hand"),
                };
                case "Lost Bag Tourist": return new List<ScenarioStep>
                {
                    S("problem", "tourist", "I lost my bag!", "Excuse me, have you seen a blue beach bag? I can't find mine.",
                        "Sorry to bother you — I've lost my beach bag somewhere between the café and the water. Have you seen it?",
                        "Ask about the bag (colour, what's inside, where).", "what|where|colour|color|inside|look like|when|last|help|which|blue|seen",
                        "What does it look like, and where did you last see it?", "last see: the latest time you saw it"),
                    S("help", "tourist", "Blue, with a towel. Can you help?", "It's blue with a striped towel inside. I think I left it near the palm trees. Could you help me look?",
                        "It's blue, with a striped towel and my sunglasses inside — probably near the palms by the dunes. Would you mind helping me look?",
                        "Offer to help.", Yes + "|help|look|find|sure|no problem|of course|happy", "Of course, I'll help you look.", "", "", "lost-bag"),
                };
                case "Directions Tourist": return new List<ScenarioStep>
                {
                    S("ask", "tourist", "Where is the café?", "Excuse me, how do I get to the beach café?", "Sorry, could you tell me the quickest way to the beach café from here?",
                        "Give directions to the café.", "left|right|straight|past|next to|along|behind|opposite|towards|toward|near|follow|turn|walk|minutes|beach|café|cafe",
                        "Walk straight along the beach and it's next to the volleyball court.", "opposite: on the other side|along: following the line of"),
                    S("thanks", "tourist", "Thank you!", "Thanks so much! Is it far?", "Thanks — is it a long walk from here?",
                        "Reply.", "not|no|far|close|minutes|near|five|two|short|welcome|pleasure|" + Thanks, "No, it's only about two minutes.", "", "", "directions"),
                };
                case "Beach Cleanup Volunteer": return new List<ScenarioStep>
                {
                    S("join", "volunteer", "We clean the beach. Help?", "We're doing a beach cleanup — would you like to join?", "We're running a cleanup this afternoon to stop plastic reaching the reef. Fancy joining us?",
                        "Agree to help.", Yes + "|help|join|happy|sure|of course|what|how", "Sure, I'd love to help. What should I do?", "cleanup: removing rubbish"),
                    S("task", "volunteer", "Pick up 10 bits of rubbish.", "Great! Please pick up ten pieces of rubbish and bring them to the bin.",
                        "Brilliant. Collect ten pieces of litter — bottles, cans, plastic — and drop them in the recycling bins.",
                        "Confirm the task.", Yes + "|ten|10|okay|got it|will do|sure", "Okay, ten pieces of rubbish. Got it.", "litter: rubbish on the ground", "", "cleanup"),
                };
                case "Beach Tourist Chat": return new List<ScenarioStep>
                {
                    S("q1", "tourist", "Do you like swimming?", "Describe your ideal beach holiday.", "How does tourism affect coastal communities?",
                        "Answer the question with a reason or an example.",
                        "i like|i love|i enjoy|i don'?t|because|swim|sea|sun|relax|beach|ideal|holiday|would|island|quiet|jobs|economy|money|prices|local|crowded|pollution|culture|community",
                        "Yes, I love swimming because the sea is so relaxing.", "relaxing: makes you calm|livelihood: the way people earn money",
                        "To what extent does luxury tourism undermine coastal ecological sustainability?"),
                    S("q2", "tourist", "Would you like a drink?", "Why do people enjoy seaside destinations?", "Should governments restrict development near beaches?",
                        "Answer and give a reason.",
                        "yes|no|please|thank|because|relax|nature|sun|fresh air|water|escape|fun|government|restrict|limit|protect|develop|build|hotel|law|rule|should|economy|balance",
                        "People enjoy the seaside because they can relax and escape the city.", "escape: get away from|restrict: limit",
                        "How can sustainable tourism policy balance economic growth with marine conservation?"),
                    S("q3", "tourist", "What is your favourite beach activity?", "What are the benefits of outdoor activities?", "How can beaches be protected from pollution?",
                        "Answer and add one detail.",
                        "swim|snorkel|surf|kayak|walk|read|sunbath|volleyball|health|fit|exercise|stress|mood|fresh|nature|plastic|rubbish|litter|recycl|clean|bin|banning|ban plastic|fine|educat|protect",
                        "My favourite is snorkelling, because I can see colourful fish.", "pollution: dirty, harmful things in nature",
                        "Is individual behaviour or regulation more effective at protecting marine ecosystems, and why?", "tourist-chat"),
                };
                case "Beach Club Bartender": return new List<ScenarioStep>
                {
                    S("order", "bartender", "Hi! A drink?", "Evening! What are you drinking?", "Good evening! What can I mix for you — something fruity?",
                        "Order what you chose on the menu.", Order, "Could I have a passion fruit cooler, please?", "mocktail: a drink without alcohol"),
                };
            }
            return null;
        }
    }
}
