using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Airport conversations (IELTS bands 3–4 / 5–6 / 7–8, plus Band 9 where it matters).
    /// Tokens {city} {country} {flight} {gate} {seat} {hotel} {days} {dest} {stay} {purpose} {cur} are filled from
    /// the player's booking so answers are checked against the real itinerary, not a fixed script.
    public static class AirportScenarios
    {
        private const string Yes = "yes|yeah|yep|sure|of course|okay|ok|that'?s right|correct|i did|i have";
        private const string No = "no|nope|not|nothing|none|never|i don'?t|i haven'?t";
        private const string Thanks = "thank|thanks|cheers|great|perfect|lovely|okay|ok|fine|bye|appreciate";
        private const string Numbers = @"\d|one|two|three|four|five|six|seven|eight|nine|ten|twenty|thirty";

        public static readonly string[] Names =
        {
            "Airport Taxi Drop-off", "Airport Information", "Check-in Agent", "Baggage Questions", "Security Officer",
            "Passport Control", "Duty-Free Cashier", "Barista Order", "Airport Restaurant", "Currency Exchange", "Lounge Host",
            "Gate Agent", "Flight Attendant Boarding", "Flight Attendant Service", "Pilot Greeting", "Arrival Officer",
            "Baggage Services", "Customs Officer", "Tourist Information", "Taxi Rank", "Hotel Reception", "IELTS Travel Practice",
        };

        private static ScenarioStep S(string id, string speaker, string simple, string standard, string advanced, string goal,
            string meaning, string model, string vocabulary, string onComplete = "", string expert = "") => new ScenarioStep
        {
            id = id, speaker = speaker, simple = simple, standard = standard, advanced = advanced, expert = expert, goal = goal,
            meaning = meaning, model = model, vocabulary = vocabulary, onComplete = onComplete,
        };

        public static string DaysWords(int days) => days switch
        {
            7 => "7|seven|a week|one week|week",
            14 => "14|fourteen|two weeks|fortnight",
            10 => "10|ten",
            _ => days + "|" + days + " days",
        };

        public static string PurposeWords(string purpose) => purpose switch
        {
            "study" => "study|studying|course|university|exchange|student|learn",
            "business" => "business|work|meeting|conference|job",
            "family" => "family|relatives|visit|visiting|parents|cousin",
            _ => "holiday|vacation|tourism|tourist|travel|sightseeing|leisure|trip|visit|visiting",
        };

        /// Build a scenario for this booking (null if the name is unknown).
        public static ScenarioDefinition Create(string title, TravelBooking booking)
        {
            var steps = Steps(title);
            if (steps == null) return null;
            booking ??= new TravelBooking();
            var where = booking.Where;
            string Fill(string text) => string.IsNullOrEmpty(text) ? text : text
                .Replace("{city}", where.City).Replace("{country}", where.Country).Replace("{flight}", booking.flight)
                .Replace("{gate}", booking.gate).Replace("{seat}", booking.seat).Replace("{hotel}", where.Hotel)
                .Replace("{days}", booking.stayDays.ToString()).Replace("{dest}", where.Words).Replace("{stay}", DaysWords(booking.stayDays))
                .Replace("{purpose}", PurposeWords(booking.purpose)).Replace("{purposeName}", booking.purpose).Replace("{cur}", where.CurrencyName)
                .Replace("{hotelWords}", string.Join("|", where.Hotel.ToLowerInvariant().Split(' ').Where(word => word.Length > 3)) + "|hotel");
            foreach (var step in steps)
            {
                step.simple = Fill(step.simple); step.standard = Fill(step.standard); step.advanced = Fill(step.advanced); step.expert = Fill(step.expert);
                step.goal = Fill(step.goal); step.meaning = Fill(step.meaning); step.model = Fill(step.model);
            }
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.name = title;
            scenario.Title = title;
            scenario.Location = "Airport";
            scenario.Steps = steps;
            return scenario;
        }

        private static List<ScenarioStep> Steps(string title)
        {
            switch (title)
            {
                case "Airport Taxi Drop-off": return new List<ScenarioStep>
                {
                    S("where", "taxi", "Here is the airport. Which terminal?", "Here we are at the airport. Departures, right? Which airline are you flying with?",
                        "Here we are. Departures level — which airline are you with, so I can drop you closest to the right doors?",
                        "Say you are flying with SkyLark Air (departures).", "skylark|departure|airline|flying with|international", "Departures, please. I'm flying with SkyLark Air.",
                        "departures: the area for leaving flights|terminal: an airport building"),
                    S("bags", "taxi", "Do you want help with your bag?", "Shall I get your suitcase out of the boot for you?",
                        "Let me grab your suitcase from the boot — anything fragile in there I should be careful with?",
                        "Accept or decline help with your bag.", Yes + "|" + No + "|please|thank|help|careful|fragile|myself", "Yes, please. Thank you very much.",
                        "boot: the back of a car (US: trunk)", "arrived"),
                };
                case "Airport Information": return new List<ScenarioStep>
                {
                    S("help", "info", "Hello. Can I help you?", "Good morning, how can I help you?", "Good morning. You look like you're searching for something — how can I help?",
                        "Ask where the SkyLark Air check-in counters are, or about your flight.", "where|check in|counter|skylark|flight|find|looking|desk|which|gate",
                        "Excuse me, where can I check in for SkyLark Air?", "check-in counter: where you drop bags and get a boarding pass|row: a line of counters"),
                    S("thanks", "info", "Row A, on the left. OK?", "SkyLark Air is in row A, on your left. The flight information screens are just behind me.",
                        "SkyLark Air checks in at row A, on your left. If you want to confirm your departure time, the large screens behind me update every minute.",
                        "Thank them (or ask one more thing).", Thanks + "|where|how long|toilet|restroom|trolley", "Thank you so much, that's really helpful.", "trolley: a cart for bags", "directed"),
                };
                case "Check-in Agent": return new List<ScenarioStep>
                {
                    S("passport", "checkin", "Hello. Passport, please.", "Good morning. May I see your passport and booking reference, please?",
                        "Good morning, welcome to SkyLark Air. Could I have your passport and booking reference, please?",
                        "Hand over your passport (say it politely).", "here|passport|of course|sure|yes|booking|reference",
                        "Of course, here's my passport. My booking reference is on my phone.", "booking reference: the code for your ticket"),
                    S("destination", "checkin", "Where are you flying today?", "Thank you. And where are you flying to today?",
                        "Thank you. Just to confirm — what's your final destination today, and are you connecting anywhere?",
                        "Confirm your destination: {city}.", "{dest}", "I'm flying to {city}, a direct flight.", "final destination: where your journey ends|connecting: changing planes"),
                    S("seat", "checkin", "Window or aisle seat?", "Would you prefer a window or an aisle seat?",
                        "There are still a few seats left. Do you have a preference — window, aisle, or something near the front?",
                        "Choose a seat type.", "window|aisle|middle|front|back|don'?t mind|any|exit row|near",
                        "A window seat, please, if possible.", "aisle: the walkway between seats", "seat-panel"),
                    S("bags", "checkin", "How many bags?", "How many bags are you checking in today?",
                        "And how many bags will you be checking in? Your fare includes one bag of up to twenty-three kilos.",
                        "Say how many bags you are checking in.", Numbers + "|just|only|single|a bag|my suitcase|bag",
                        "Just one suitcase, please.", "check in a bag: give it to the airline to put in the hold", "baggage-panel"),
                };
                case "Baggage Questions": return new List<ScenarioStep>
                {
                    S("packed", "checkin", "Did you pack your bag?", "Did you pack this bag yourself?",
                        "Did you pack this bag yourself, and has it been with you since you packed it?",
                        "Answer the question about packing.", Yes + "|" + No + "|myself|packed|with me", "Yes, I packed it myself, and it's been with me the whole time.",
                        "pack: put things in a bag"),
                    S("fragile", "checkin", "Anything that can break?", "Do you have any fragile items in it?",
                        "Is there anything fragile or valuable in there — electronics, glass, anything we should label?",
                        "Say if there is anything fragile.", Yes + "|" + No + "|fragile|glass|break|camera|gift|laptop|souvenir",
                        "No, nothing fragile. My laptop is in my hand luggage.", "fragile: easy to break|hand luggage: the bag you carry onto the plane"),
                    S("batteries", "checkin", "Any batteries in the bag?", "Any spare batteries or power banks in the checked bag?",
                        "One more — no spare batteries or power banks in the hold bag? In this game those travel in the cabin.",
                        "Answer about batteries.", Yes + "|" + No + "|power bank|battery|batteries|hand luggage|carry",
                        "No, my power bank is in my hand luggage.", "power bank: a portable battery|hold: the space under the cabin", "tagged"),
                };
                case "Security Officer": return new List<ScenarioStep>
                {
                    S("pockets", "security", "Empty your pockets, please.", "Please empty your pockets and put your laptop in a separate tray.",
                        "Could you empty your pockets, take your jacket off and put your laptop and liquids in separate trays, please?",
                        "Acknowledge the instructions.", Yes + "|okay|sure|tray|laptop|pockets|jacket|liquids|of course",
                        "Sure, I'll put my laptop and liquids in separate trays.", "tray: a plastic box for your things|liquids: drinks, creams, gels"),
                    S("question", "security", "Anything else in your bag?", "Do you have anything else in your bag I should know about?",
                        "Before your bag goes through, is there anything in it that might need a closer look — sharp items, large liquids?",
                        "Answer the officer.", No + "|" + Yes + "|just|clothes|books|sharp|nothing else|only", "No, just clothes and a book.",
                        "sharp items: knives, scissors", "security-panel"),
                };
                case "Passport Control": return new List<ScenarioStep>
                {
                    S("where", "passport", "Where are you going?", "Good afternoon. Where are you travelling to today?",
                        "Good afternoon. Where are you travelling today, and is this your first time there?",
                        "Say your destination ({city}).", "{dest}", "I'm travelling to {city}, in {country}.", "travel to: go to a place"),
                    S("why", "passport", "Why are you going?", "What's the purpose of your trip?",
                        "And what's the main purpose of your trip — business, study, or pleasure?",
                        "Give your trip purpose ({purposeName}).", "{purpose}", "It's a {purposeName} trip.", "purpose: the reason|pleasure: holiday, fun"),
                    S("long", "passport", "How long will you stay?", "How long will you be staying?",
                        "How long do you intend to stay, and where will you be staying?",
                        "Say how long you will stay ({days} days).", "{stay}", "For {days} days, at the {hotel}.", "intend: plan", "stamped"),
                };
                case "Duty-Free Cashier": return new List<ScenarioStep>
                {
                    S("pass", "dutyfree", "Boarding pass, please.", "Can I see your boarding pass, please?",
                        "Could I scan your boarding pass, please? Duty-free prices depend on your destination.",
                        "Show your boarding pass.", Yes + "|here|boarding pass|sure|of course", "Sure, here's my boarding pass.", "duty-free: without local tax"),
                    S("else", "dutyfree", "Anything else?", "Would you like anything else today?",
                        "Will that be everything, or can I tempt you with anything else for the flight?",
                        "Finish the purchase.", No + "|" + Thanks + "|that'?s all|everything|just", "No, that's everything, thanks.", "tempt: make you want", "pay"),
                };
                case "Barista Order": return new List<ScenarioStep>
                {
                    S("order", "barista", "Hi! What would you like?", "Hi there! What can I get for you?", "Hi! What can I get you before your flight?",
                        "Order what you chose on the menu.", "i'?d like|can i|could i|please|have|get", "Can I have a latte, please?", "regular: medium size"),
                    S("name", "barista", "Your name?", "Can I get a name for the order?", "And a name for the cup?",
                        "Tell them your name.", "i'?m|it'?s|my name|name is|call me|for", "It's Emma.", "", "ordered"),
                };
                case "Airport Restaurant": return new List<ScenarioStep>
                {
                    S("order", "waiter", "What would you like?", "Are you ready to order?", "Have you decided? Bear in mind your boarding time — the grill takes about fifteen minutes.",
                        "Order what you chose on the menu.", "i'?d like|can i|could i|please|have|get", "I'd like the soup, please.", ""),
                    S("time", "waiter", "When is your flight?", "What time is your flight? I'll make sure it's quick.",
                        "When do you need to be at your gate? I can ask the kitchen to prioritise it.",
                        "Say when your flight is.", Numbers + "|hour|minutes|soon|o'?clock|boarding|gate|plenty of time|half", "My flight boards in about an hour.", "prioritise: do first", "order-placed"),
                };
                case "Currency Exchange": return new List<ScenarioStep>
                {
                    S("rate", "exchange", "Hello. Money change?", "Hello! Which currency would you like?",
                        "Good afternoon. Which currency are you after, and would you like to hear today's rate first?",
                        "Ask for {cur} and the exchange rate.", "{cur}|rate|exchange|change|currency|how much|{dest}", "I'd like some {cur}. What's the rate today?",
                        "exchange rate: how much foreign money you get|commission: a fee"),
                    S("amount", "exchange", "How much?", "How much would you like to change?",
                        "The rate's on the board. There's a three percent commission. How much would you like to change?",
                        "Say an amount.", Numbers + "|hundred|fifty|dollars|all|half", "Fifty dollars, please.", "", "exchange-panel"),
                };
                case "Lounge Host": return new List<ScenarioStep>
                {
                    S("access", "lounge", "Hello. Business class ticket?", "Good afternoon. May I see your boarding pass? The lounge is for business class.",
                        "Good afternoon. Access today is for business-class passengers and members — may I see your boarding pass?",
                        "Show your pass / ask about access.", "here|boarding pass|economy|can i|could i|pay|day pass|member|ask|is it possible",
                        "Here's my boarding pass. I'm in economy — is there a day pass?", "lounge: a quiet waiting room|day pass: paid entry for one day"),
                    S("decline", "lounge", "Sorry. Economy can't come in. OK?", "I'm sorry, we're full with members today. There are quiet seats near gate G1.",
                        "I'm afraid we're at capacity, so day passes aren't available this afternoon. The quiet zone by gate G1 is free — would that work?",
                        "Respond politely (accept or ask another way).", Thanks + "|understand|no problem|alternative|anywhere|other|shame|okay|that works|instead",
                        "No problem, I understand. I'll try the quiet zone, thanks.", "at capacity: full"),
                };
                case "Gate Agent": return new List<ScenarioStep>
                {
                    S("pass", "gate", "Pass and passport, please.", "Boarding pass and passport, please.", "Good afternoon. Boarding pass and passport, please — and is that your only carry-on?",
                        "Hand them over.", Yes + "|here|boarding pass|passport|sure|of course|carry on|only", "Here you are. Yes, just this one bag.", "carry-on: a bag you take on board"),
                    S("seat", "gate", "Seat {seat}. Down the bridge. OK?", "You're in seat {seat}. Please go down the jet bridge. Enjoy your flight.",
                        "Seat {seat}, boarding from the front door — mind the step at the end of the jet bridge. Have a pleasant flight.",
                        "Thank the agent.", Thanks, "Thank you very much.", "jet bridge: the tunnel to the plane", "boarded"),
                };
                case "Flight Attendant Boarding": return new List<ScenarioStep>
                {
                    S("welcome", "crew", "Welcome. Your seat?", "Welcome aboard! What's your seat number?", "Good afternoon and welcome aboard. May I see your seat number?",
                        "Tell her your seat number ({seat}).", "{seat}|seat|row|it'?s|number", "It's {seat}.", "aboard: on the plane"),
                    S("bag", "crew", "Bag up there, please.", "Your seat's on the left. Please put your bag in the overhead locker.",
                        "Your seat's halfway down on the left. Larger bags in the overhead locker, please, and anything small under the seat in front.",
                        "Acknowledge.", Yes + "|okay|thank|overhead|locker|under|sure", "Okay, thank you.", "overhead locker: storage above the seats"),
                };
                case "Flight Attendant Service": return new List<ScenarioStep>
                {
                    S("drink", "crew", "Drink? Water, juice, tea?", "Would you like something to drink? We have water, juice, tea and coffee.",
                        "Can I offer you something to drink? There's water, orange juice, tea, coffee or a soft drink.",
                        "Choose a drink.", "water|juice|tea|coffee|cola|soft drink|nothing|just|please|could i|i'?d like", "Could I have an orange juice, please?", "soft drink: a fizzy drink"),
                    S("meal", "crew", "Chicken or pasta?", "For lunch we have chicken with rice or vegetable pasta. Which would you prefer?",
                        "For the meal service, there's chicken with rice or a vegetarian pasta. Any dietary requirements I should know about?",
                        "Choose a meal.", "chicken|pasta|vegetarian|either|rice|no thanks|not hungry|please", "The vegetable pasta, please.", "dietary requirements: food you can't eat"),
                    S("else", "crew", "Anything else?", "Is there anything else I can get you?", "Is there anything else I can help with — a blanket, a pillow, headphones?",
                        "Ask for a blanket (or for help).", "blanket|pillow|headphones|help|cold|could i|can i|please|water|thank|no", "Could I have a blanket, please? It's a bit cold.",
                        "blanket: a warm cover", "served"),
                };
                case "Pilot Greeting": return new List<ScenarioStep>
                {
                    S("hello", "pilot", "Hello! First flight?", "Hello there! Is this your first international trip?", "Good afternoon! Off somewhere exciting — is this your first trip abroad?",
                        "Chat with the captain.", Yes + "|" + No + "|first|before|excited|nervous|abroad|trip", "Yes, it's my first trip abroad. I'm a bit nervous!", "abroad: in another country"),
                    S("bye", "pilot", "Do not worry. Enjoy!", "Don't worry, the weather looks smooth today. Enjoy the flight!",
                        "No need to worry — we're expecting a smooth ride most of the way. Enjoy the flight.",
                        "Reply.", Thanks + "|good|glad|relief|nice", "That's good to hear. Thank you!", "smooth: calm, not bumpy"),
                };
                case "Arrival Officer": return new List<ScenarioStep>
                {
                    S("purpose", "arrival", "Why are you here?", "Welcome to {country}. What's the purpose of your visit?",
                        "Welcome to {country}. What brings you here, and is this your first visit?",
                        "Say why you are visiting ({purposeName}).", "{purpose}", "I'm here on {purposeName}. It's my first visit.", "purpose of your visit: why you came"),
                    S("long", "arrival", "How many days?", "How long are you planning to stay?", "How long do you plan to stay, and do you have a return ticket?",
                        "Say how long ({days} days).", "{stay}", "For {days} days. I have a return ticket.", "return ticket: a ticket home"),
                    S("hotel", "arrival", "Where will you stay?", "Where will you be staying?", "And where will you be staying during your visit?",
                        "Name your hotel ({hotel}).", "{hotelWords}", "At the {hotel}.", "", "arrival-stamped"),
                };
                case "Baggage Services": return new List<ScenarioStep>
                {
                    S("problem", "bagservice", "Hello. Problem with a bag?", "Hello, can I help? Is there a problem with your luggage?",
                        "Good afternoon. Has something gone wrong with your baggage?",
                        "Explain your bag hasn't arrived.", "bag|suitcase|luggage|missing|arrive|hasn'?t|lost|didn'?t|can'?t find|come out|carousel",
                        "My suitcase hasn't come out on the carousel.", "carousel: the moving belt for bags"),
                    S("describe", "bagservice", "What colour? Flight?", "Which flight were you on, and what does the bag look like?",
                        "Could you give me your flight number and describe the bag — colour, size, anything distinctive?",
                        "Give the flight ({flight}) and describe your bag.", "{flight}|sl|flight|blue|black|red|grey|green|large|small|medium|suitcase|tag|colour|color",
                        "Flight {flight}. It's a large blue suitcase with a SkyLark tag.", "distinctive: easy to recognise", "reported"),
                };
                case "Customs Officer": return new List<ScenarioStep>
                {
                    S("declare", "customs", "Anything to declare?", "Do you have anything to declare?",
                        "Do you have anything to declare — goods over your allowance, food, plants, or large amounts of cash?",
                        "Answer honestly about your purchases.", No + "|" + Yes + "|declare|duty free|gift|chocolate|perfume|only|just|personal",
                        "No, just a small gift from duty-free.", "declare: tell customs about goods|allowance: what you may bring without tax"),
                    S("go", "customs", "OK. Go ahead.", "That's fine. Welcome, enjoy your stay.", "That's within the allowance. Enjoy your stay.",
                        "Thank the officer.", Thanks, "Thank you!", "", "customs-cleared"),
                };
                case "Tourist Information": return new List<ScenarioStep>
                {
                    S("ask", "tourist", "Hello! Need help?", "Welcome to {city}! How can I help you?", "Welcome to {city}! Are you looking for transport, a map, or ideas for things to see?",
                        "Ask about getting to your hotel or what to see.", "how|where|get to|taxi|train|bus|map|see|visit|recommend|hotel|city|centre|center",
                        "How can I get to the city centre?", "city centre: the middle of a city"),
                    S("thanks", "tourist", "Taxi is outside. Train too.", "Taxis are just outside on the left, and the express train leaves every fifteen minutes.",
                        "Taxis are outside on the left — the most direct option with luggage. The express train is cheaper. And don't miss {landmark}.",
                        "Thank them / choose an option.", Thanks + "|taxi|train|bus|i'?ll take|best|cheaper", "Thanks! I'll take a taxi with my luggage.", ""),
                };
                case "Taxi Rank": return new List<ScenarioStep>
                {
                    S("where", "taxidriver", "Where to?", "Hi! Where are you heading?", "Afternoon! Where can I take you?",
                        "Tell the driver your hotel ({hotel}).", "{hotelWords}", "To the {hotel}, please.", "heading: going"),
                    S("pay", "taxidriver", "Card or cash?", "That's about thirty-five {cur}. Card or cash?",
                        "Traffic's light, so about twenty-five minutes and roughly thirty-five {cur}. Will you pay by card or cash?",
                        "Say how you'll pay.", "card|cash|pay|tap|either|credit", "By card, please.", "fare: the price of a ride", "taxi-booked"),
                };
                case "Hotel Reception": return new List<ScenarioStep>
                {
                    S("checkin", "hotel", "Hello. Name, please?", "Good evening, welcome to the {hotel}. Do you have a reservation?",
                        "Good evening and welcome to the {hotel}. Checking in? May I have the name on the reservation?",
                        "Check in and give your name.", "i'?m|name|reservation|booking|check in|under|my name|it'?s", "Yes, I have a reservation under Emma Lee.", "reservation: a booking"),
                    S("nights", "hotel", "How many nights?", "And it's for {days} nights, is that right?", "I have you down for {days} nights with breakfast — is that correct?",
                        "Confirm the length of stay.", "{stay}|" + Yes, "Yes, that's right, {days} nights.", "", "hotel-checked-in"),
                };
                case "IELTS Travel Practice": return new List<ScenarioStep>
                {
                    S("p1", "tutor", "Do you like to travel?", "Do you enjoy travelling? Why or why not?",
                        "Do you enjoy travelling, and has the way you travel changed as you've got older?",
                        "IELTS Part 1 — travel: give an answer with a reason.", "i like|i love|i enjoy|i don'?t|because|since|new places|culture|food|relax",
                        "Yes, I really enjoy travelling because I love trying new food and meeting people.", "broaden: make wider|routine: what you usually do", "",
                        "To what extent does travelling change who we are, rather than just what we've seen?"),
                    S("transport", "tutor", "Bus or train? Which do you like?", "How do most people get around in your city?",
                        "How has public transport in your city changed in recent years, and is it adequate?",
                        "IELTS Part 1 — transport.", "bus|train|tram|car|metro|subway|walk|bike|cycle|taxi|public transport", "Most people take the train, because it's fast and cheap.",
                        "commute: daily journey to work|congestion: traffic", "",
                        "Should cities prioritise public transport over private cars, even if it limits personal freedom?"),
                    S("tourism", "tutor", "Do many tourists come to your country?", "Is tourism important in your country?",
                        "What are the benefits and drawbacks of mass tourism for local communities?",
                        "IELTS Part 3 — tourism.", "tourism|tourist|economy|jobs|money|crowded|environment|benefit|problem|local|culture|prices",
                        "Tourism brings jobs, but it can make popular places crowded and expensive.", "drawback: disadvantage|mass tourism: very large numbers of tourists", "",
                        "Is it possible to travel ethically, or is all tourism a form of consumption that changes what it touches?"),
                    S("culture", "tutor", "Is food different in other countries?", "What cultural differences have surprised you when meeting people from abroad?",
                        "How can travellers avoid misunderstandings caused by cultural differences?",
                        "IELTS Part 3 — cultural differences.", "culture|different|custom|polite|tip|greet|respect|learn|research|language|food|surprise",
                        "People should learn a few local customs, like how to greet someone politely.", "custom: a traditional way of doing something", "",
                        "Do you think globalisation is making cultures more alike, and is that a loss?"),
                    S("education", "tutor", "Do students study in other countries?", "Why do many students choose to study abroad?",
                        "What are the advantages and disadvantages of international education for students and for their home countries?",
                        "IELTS Part 3 — international education.", "study|students|abroad|university|degree|experience|language|career|expensive|independent|brain drain",
                        "Studying abroad helps students become independent and improve their English.", "brain drain: skilled people leaving a country", "",
                        "Is international education mainly a privilege for the wealthy, and how could that change?"),
                    S("aviation", "tutor", "Are planes bad for the air?", "What can people do to reduce the environmental impact of flying?",
                        "Should governments limit air travel to protect the environment? Explain your view.",
                        "IELTS Part 3 — environmental impact of aviation.", "fly less|train|carbon|pollution|emission|tax|offset|environment|climate|limit|holiday|fuel|sustainable",
                        "People could fly less and take trains for shorter trips.", "carbon emissions: gases that warm the planet|offset: pay to balance out", "",
                        "Who should bear the cost of aviation's environmental impact — passengers, airlines or governments?"),
                };
            }
            return null;
        }
    }
}
