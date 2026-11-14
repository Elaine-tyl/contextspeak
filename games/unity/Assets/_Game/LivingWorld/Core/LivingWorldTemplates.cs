using System;

namespace ContextSpeak.LivingWorld
{
    /// Original conversations. A location's art and actor can change without changing its story IDs.
    public static class LivingWorldTemplates
    {
        private static StoryChoice Choice(string id, string label, string goal, int next, string fact,
            StoryAction action = StoryAction.None, string promise = "") => new StoryChoice
            { Id = id, Label = label, Goal = goal, Next = next, Fact = fact, Action = action, Promise = promise };
        private static StoryStep Step(string simple, string everyday, string advanced, string vocabulary, params StoryChoice[] choices) =>
            new StoryStep { Simple = simple, Everyday = everyday, Advanced = advanced, Vocabulary = vocabulary.Split('|'), Choices = choices };
        private static StoryTemplate Activity(string id, string title, string location, string npc, string name, string role,
            StoryStep first, StoryStep second, bool rain = false) => new StoryTemplate
            { Id = id, Title = title, Location = location, NpcId = npc, NpcName = name, Role = role, Steps = new[] { first, second }, OnlyRain = rain };

        public static StoryTemplate[] All() => new[]
        {
            new StoryTemplate
            {
                Id = "tourist-cafe", Title = "A new face in town", Location = "cafe", NpcId = "jordan", NpcName = "Jordan", Role = "Visitor from Canada",
                Steps = new[]
                {
                    Step("Hey! Where's Pine House Hotel? I'm a bit lost.",
                        "Hey, sorry to bother you. I'm staying at Pine House Hotel, but my map's acting up. Could you point me in the right direction?",
                        "Sorry to interrupt. My map keeps sending me round in circles. Could you suggest a straightforward route to Pine House Hotel?",
                        "turn left|across from|landmark|within walking distance",
                        Choice("directions", "Give directions", "Explain a plausible route using a direction and a landmark.", 1, "You helped Jordan find the hotel."),
                        Choice("unable", "Offer another way to help", "Politely explain you are unsure and suggest asking staff or checking a map.", 1, "You offered another way to find the hotel.")),
                    Step("Thanks! Want to meet at the night market at six?",
                        "Thanks, that really helps. I'm checking out the night market after six. Want to meet me there?",
                        "I appreciate the help. Would you be up for the night market this evening, or have you already got plans?",
                        "meet up|sounds good|I'm afraid|what time",
                        Choice("meet", "Accept the invitation", "Accept meeting at the night market and confirm a practical detail.", -1, "You agreed to meet Jordan at the night market.", StoryAction.MeetMarket, "Meet at the night market"),
                        Choice("details", "Ask for details", "Ask a relevant question about the night market before deciding.", 2, "You asked Jordan about the evening plans."),
                        Choice("decline", "Politely decline", "Politely decline the invitation, optionally giving a reason.", -1, "You politely declined tonight's invitation.", StoryAction.DeclineMarket)),
                    Step("It's by the market square. Shall we meet at six?",
                        "It's the food stalls by the market square, from six onwards. We can keep it low-key. Does that work for you?",
                        "The stalls open around six in the market square. There's no booking or commitment; we can head off whenever we like. Would that suit you?",
                        "from six onwards|keep it low-key|that works|another time",
                        Choice("meet", "Confirm the plan", "Accept the plan and confirm when or where to meet.", -1, "You confirmed the night-market meeting.", StoryAction.MeetMarket, "Meet at the night market"),
                        Choice("decline", "Suggest another time", "Politely decline tonight and suggest another time or wish them a good evening.", -1, "You suggested catching up another time.", StoryAction.DeclineMarket)),
                }
            },
            new StoryTemplate
            {
                Id = "tourist-market", Title = "See you at the night market", Location = "night-market", NpcId = "jordan", NpcName = "Jordan", Role = "Your new friend",
                Prerequisite = "market-invitation", StartMinute = 18 * 60, EndMinute = 24 * 60,
                Steps = new[]
                {
                    Step("Hey, you made it! What food should we try?",
                        "Hey, good to see you again! I found the hotel, thanks to you. What looks good here?",
                        "Good to see you again! Thanks again for helping me find the hotel. If we only try a couple of things, what would you recommend and why?",
                        "food stall|mild|ingredients|worth trying",
                        Choice("recommend", "Recommend something", "Recommend a food or drink and give a relevant reason or preference.", 1, "You and Jordan explored the food stalls.")),
                    Step("Some friends are going to Seattle this weekend. Want to come?",
                        "A few friends and I are heading to Seattle this weekend. You'd be welcome to come along. What do you think?",
                        "We're planning a weekend in Seattle. Would joining a small group appeal to you, or do you prefer travelling independently?",
                        "come along|budget|itinerary|I'd rather",
                        Choice("join", "Join the trip", "Accept the trip and ask or confirm a practical travel detail.", -1, "You accepted Jordan's weekend-trip invitation.", StoryAction.JoinTrip, "Discuss travel plans at the airport"),
                        Choice("decline", "Decline the trip", "Politely decline the trip or explain why the timing does not work.", -1, "You declined the trip while staying on friendly terms.", StoryAction.DeclineTrip)),
                }
            },
            new StoryTemplate
            {
                Id = "tourist-airport", Title = "A change of plans", Location = "airport", NpcId = "airline-casey", NpcName = "Casey", Role = "Airline service agent",
                Prerequisite = "trip-invitation",
                Steps = new[]
                {
                    Step("Your flight is delayed. Do you want another flight, or refund information?",
                        "I'm sorry, your Seattle flight's delayed because of the earlier weather. Would you like me to check other flights, or explain the refund options?",
                        "The earlier weather disruption is still affecting departures. We can look at rerouting you or discuss the applicable refund options. What would work best for you?",
                        "flight delay|rebook|available seats|refund options",
                        Choice("options", "Ask about another flight", "Ask about alternative flights and a relevant constraint such as timing or the group booking.", 1, "You asked Casey for alternative flight options."),
                        Choice("refund", "Ask about a refund", "Request refund information politely and explain what you need to know.", -1, "You requested refund information rather than accepting a new flight.", StoryAction.Refund)),
                    Step("There's a flight tomorrow morning. Would that work?",
                        "I can check seats on tomorrow morning's flight. Before I request the change, does that work for everyone in your group?",
                        "Tomorrow morning may keep your group together, but it shortens your weekend. Would you like me to request those seats, or would you prefer the refund route?",
                        "subject to availability|keep the group together|trade-off|confirm",
                        Choice("morning", "Request the morning flight", "Request the morning alternative and confirm a relevant detail or trade-off.", -1, "You requested the morning flight, subject to availability.", StoryAction.MorningFlight),
                        Choice("refund", "Choose refund information", "Explain why the alternative does not suit you and request refund information.", -1, "You decided the alternative did not fit your plans.", StoryAction.Refund)),
                }
            },
            Activity("camp-rain", "Rain on the camping trip", "camping", "ranger-sam", "Sam", "Camping friend",
                Step("It's raining. Where can we stay dry?", "Well, so much for clear skies! How should we change our plans?", "The weather's turned. How would you balance keeping the trip enjoyable with keeping everyone safe?", "shelter|forecast|put off|backup plan",
                    Choice("shelter", "Suggest a safe plan", "Suggest a practical safe response to the rain.", 1, "You helped Sam make a rainy-day camping plan.")),
                Step("What should we bring inside first?", "What would you keep dry first, and why?", "If we can only move a few things under cover, how would you prioritise them?", "keep dry|first-aid kit|essential|prioritise",
                    Choice("priorities", "Explain your priorities", "Prioritise camping supplies and explain a sensible reason.", -1, "You agreed which camping supplies to protect.", StoryAction.ActivityComplete)), true),
            Activity("missing-parcel", "The missing parcel", "detective", "detective-lee", "Lee", "Neighbourhood investigator",
                Step("A parcel is missing. What should I ask the neighbour?", "A parcel's gone missing from the lobby. What would you ask the neighbour who saw the delivery?", "Two neighbours remember the delivery differently. How would you clarify the timeline without accusing either of them?", "delivery|witness|timeline|clarify",
                    Choice("question", "Ask a witness question", "Ask a useful non-accusatory question about the missing delivery.", 1, "You helped Lee question a witness fairly.")),
                Step("The delivery photo shows a different door. What now?", "The photo shows the wrong door number. What should we do next?", "The delivery photo contradicts the original assumption. How should that change our next steps?", "evidence|assumption|check with|follow up",
                    Choice("evidence", "Use the new evidence", "Use the wrong-door evidence to suggest a reasonable next step.", -1, "You followed evidence instead of blaming a neighbour.", StoryAction.ActivityComplete))),
            Activity("pet-help", "A home for a rescue pet", "pet-centre", "rescue-noor", "Noor", "Animal rescue volunteer",
                Step("What pet would suit your home?", "Before we talk about adoption, what would a normal day look like for a pet in your home?", "How would you judge whether your lifestyle can meet a rescue animal's needs over the long term?", "adopt|routine|exercise|long-term care",
                    Choice("routine", "Describe a realistic routine", "Describe a realistic home routine and a pet-care responsibility.", 1, "You discussed responsible pet care with Noor.")),
                Step("What would you do when you travel?", "Who could care for the pet while you're away?", "How would you plan for travel, unexpected costs and changes in your routine?", "pet sitter|vet|backup care|responsibility",
                    Choice("care", "Plan backup care", "Explain a responsible backup-care plan.", -1, "You planned backup care; no adoption was made automatically.", StoryAction.ActivityComplete))),
            Activity("recipe-swap", "A missing ingredient", "cooking", "chef-theo", "Theo", "Cooking instructor",
                Step("We're out of milk. What could we use?", "We've run out of milk just as we're about to start. What substitute would you suggest?", "We need a substitute that works for both the recipe and a guest's dietary needs. How would you choose one?", "substitute|dietary needs|texture|check the label",
                    Choice("substitute", "Suggest a substitute", "Suggest a plausible substitute and explain a relevant reason.", 1, "You suggested a recipe substitution to Theo.")),
                Step("How should we check it's safe for our guest?", "What would you ask the guest before we use it?", "How would you check the guest's needs without making assumptions about allergies or preferences?", "ingredient|allergy|preference|double-check",
                    Choice("check", "Check with the guest", "Ask a careful ingredient or dietary-needs question; do not guarantee allergy safety.", -1, "You checked the guest's needs before changing the recipe.", StoryAction.ActivityComplete))),
            Activity("festival-seats", "The ticket mix-up", "music-festival", "usher-robin", "Robin", "Festival usher",
                Step("Is there a problem with your seat?", "Hey, can I help? It looks like someone else has your seat number too.", "It seems two tickets show the same seat. How would you explain the problem and suggest a fair resolution?", "seat number|booking confirmation|mix-up|resolve",
                    Choice("explain", "Explain the ticket issue", "Explain the duplicate-seat problem politely and ask for help.", 1, "You explained the ticket mix-up to Robin.")),
                Step("We can move you together a little farther back. Is that okay?", "There's space for your group a few rows back. Would that work, or do you need a different option?", "We can keep the group together farther back, or split you into closer seats. Which would you prioritise and why?", "a few rows back|together|alternative|priority",
                    Choice("resolve", "Discuss the alternative", "Accept or decline the seating alternative and explain your preference.", -1, "You discussed a seating solution without changing a real ticket.", StoryAction.ActivityComplete))),
            Activity("cruise-weather", "A different day ashore", "cruise-terminal", "host-avery", "Avery", "Cruise excursion host",
                Step("Our boat trip is cancelled. What would you like to do instead?", "The weather's cancelled today's boat trip. What kind of alternative would you enjoy?", "Today's excursion is cancelled. How would you weigh an on-board activity against a shorter visit ashore?", "excursion|cancelled|alternative|shore",
                    Choice("alternative", "Suggest another activity", "Suggest a realistic alternative activity and a preference.", 1, "You discussed a weather-safe excursion alternative with Avery.")),
                Step("What do you need to know before choosing?", "What would you want me to confirm before you decide?", "What information would help you make an informed choice without assuming a refund or replacement is guaranteed?", "departure time|included|availability|conditions",
                    Choice("confirm", "Ask a practical question", "Ask a relevant practical question before agreeing to the alternative.", -1, "You checked the excursion conditions before making a decision.", StoryAction.ActivityComplete))),
        };
    }
}
