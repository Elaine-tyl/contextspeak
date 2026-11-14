using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// The built-in conversation scripts. The location builder saves them as editable assets in
    /// Assets/ContextSpeak/Data/Scenarios; flows fall back to these if an asset is missing.
    /// `meaning` patterns run on lower-case text without punctuation (apostrophes kept).
    public static class ScenarioLibrary
    {
        private const string Yes = "yes|yeah|yep|sure|of course|okay|ok|please|that'?s right|correct";
        private const string No = "no|nope|not|never|none|nothing|i don'?t";
        private const string Numbers = @"\d|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|zero|oh|twenty|thirty";
        private const string Thanks = "thank|thanks|cheers|great|perfect|lovely|okay|ok|fine|bye|that'?s all";

        public static readonly string[] Names =
        {
            "Hospital Registration", "Nurse Call", "Doctor Consultation", "Pharmacy Pickup", "Ask for Directions",
            "Restaurant Host", "Restaurant Drinks", "Restaurant Order", "Restaurant Delivery", "Restaurant Bill",
            "Restaurant Complaint", "Restaurant Takeaway",
        };

        private static ScenarioStep S(string id, string speaker, string simple, string standard, string advanced,
            string goal, string meaning, string model, string vocabulary, string onComplete = "") => new ScenarioStep
        {
            id = id, speaker = speaker, simple = simple, standard = standard, advanced = advanced, goal = goal,
            meaning = meaning, model = model, vocabulary = vocabulary, onComplete = onComplete,
        };

        public static ScenarioDefinition Create(string title)
        {
            var steps = Steps(title);
            if (steps == null) return null;
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.name = title;
            scenario.Title = title;
            scenario.Location = title.StartsWith("Restaurant") ? "Restaurant" : "Hospital";
            scenario.Steps = steps;
            return scenario;
        }

        private static List<ScenarioStep> Steps(string title)
        {
            switch (title)
            {
                case "Hospital Registration": return new List<ScenarioStep>
                {
                    S("reason", "receptionist", "Hello. How can I help you?",
                        "Good morning, welcome to City Hospital. How can I help you today?",
                        "Good morning and welcome to City Hospital outpatients. What brings you in today, and have you booked an appointment?",
                        "Say why you have come (you want to see a doctor).",
                        "doctor|appointment|see someone|sick|unwell|ill|pain|hurt|check|feel|fever|sore",
                        "Hello, I'd like to see a doctor, please. I don't have an appointment.",
                        "appointment: a booked time|walk-in: a visit without a booking|outpatients: patients who go home the same day"),
                    S("name", "receptionist", "What is your name?", "Can I take your full name, please?",
                        "Could I take your full name, please, and could you spell your surname for me?",
                        "Tell her your name.", "i'?m|i am|it'?s|my name|name is|call me|this is",
                        "My name is Emma Lee. That's L-E-E.", "surname: family name|spell: say each letter"),
                    S("birth", "receptionist", "When were you born?", "And your date of birth?",
                        "Thank you. And what's your date of birth? Day, month and year, please.",
                        "Say your date of birth (any practice date is fine).",
                        Numbers + "|january|february|march|april|may|june|july|august|september|october|november|december|born|birthday",
                        "It's the fourteenth of March, nineteen ninety-eight.", "date of birth: the day you were born"),
                    S("visit", "receptionist", "Is this your first time here?", "Have you visited this hospital before?",
                        "Have you been treated here before, or will we need to create a new patient record for you?",
                        "Say whether this is your first visit.", Yes + "|" + No + "|first|before|new|been here|once",
                        "No, this is my first visit.", "patient record: the hospital's file about you"),
                    S("phone", "receptionist", "What is your phone number?", "Could I have a contact phone number?",
                        "Could I have a phone number we can reach you on in case the doctor needs to follow up?",
                        "Give a contact number (make one up).", Numbers + "|number|mobile|phone",
                        "Sure, it's oh-seven-seven, one-two-three, four-five-six.", "contact number: a phone number to call you|follow up: contact you later"),
                    S("slot", "receptionist", "Do you want to wait now or come back later?",
                        "We can see you as a walk-in today, or I can book a time later. Which would you prefer?",
                        "We have walk-in slots this morning, though the wait is around twenty minutes, or I could book you a fixed time this afternoon. What would suit you better?",
                        "Choose: wait now (walk-in) or book a time.",
                        "wait|walk in|now|today|book|later|afternoon|time|o'?clock|" + Numbers + "|prefer|fine",
                        "I'll wait now, please. Twenty minutes is fine.", "slot: a free time for an appointment|suit: be good for you", "registration-form"),
                };
                case "Nurse Call": return new List<ScenarioStep>
                {
                    S("confirm", "nurse", "Hello. What is your name?", "Hi there, number called. Can you confirm your name for me?",
                        "Hello, you're next. Before we go through, can you confirm your full name and date of birth?",
                        "Confirm who you are.", "i'?m|i am|it'?s|my name|name is|yes|that'?s me|me|" + Numbers,
                        "Yes, that's me. I'm Emma Lee.", "confirm: say it is correct"),
                    S("walk", "nurse", "Please come with me. Are you okay to walk?",
                        "The doctor will see you in room two. Are you okay to walk there with me?",
                        "Doctor Patel is ready for you in consultation room two. Are you comfortable walking, or would you like a wheelchair?",
                        "Answer the nurse.", Yes + "|" + No + "|walk|fine|wheelchair|can|need|help|thank",
                        "Yes, I can walk, thank you.", "wheelchair: a chair with wheels for people who cannot walk", "to-consultation"),
                };
                case "Doctor Consultation": return new List<ScenarioStep>
                {
                    S("problem", "doctor", "Hello. What is the problem?", "Hello, I'm Doctor Patel. What seems to be the problem today?",
                        "Good morning, I'm Doctor Patel. Tell me what's been going on and what made you decide to come in today.",
                        "Describe your main symptom (use your practice case card).",
                        "sore|throat|fever|temperature|headache|head|cough|ankle|wrist|hurt|pain|ache|sick|dizzy|twist|swollen|cold|flu|stomach|tired|rash",
                        "I've had a sore throat and a high temperature.",
                        "symptom: a sign that you are ill|sore: painful|temperature: fever, a hot body"),
                    S("since", "doctor", "When did it start?", "When did it start?",
                        "How long have you been feeling like this, and has it been getting better or worse?",
                        "Say how long you have had it.",
                        "yesterday|ago|since|day|week|morning|night|last|today|start|began|for|hour|" + Numbers,
                        "It started two days ago, and it's getting worse.", "getting worse: becoming more serious"),
                    S("severity", "doctor", "Is it bad? One to ten?", "On a scale of one to ten, how bad is the pain?",
                        "If you had to rate it on a scale from one to ten, how severe is it, and does anything make it better or worse?",
                        "Rate how bad it is.", Numbers + "|bad|mild|terrible|awful|not too|quite|very|really|worse|better|when",
                        "About six out of ten. It's worse when I swallow.", "scale: a range of numbers|severe: very bad|swallow: move food down your throat"),
                    S("allergy", "doctor", "Are you allergic to any medicine?", "Do you have any allergies to medication?",
                        "Before I prescribe anything, are you allergic to any medication, and are you taking anything at the moment?",
                        "Answer about allergies and medicine.", "allerg|" + No + "|yes|penicillin|aspirin|ibuprofen|taking|medicine|medication|vitamin",
                        "No, I'm not allergic to anything, and I'm not taking any medicine.",
                        "allergic: your body reacts badly to something|prescribe: write an order for medicine"),
                    S("questions", "doctor", "Rest and drink water. Any questions?",
                        "It looks like a mild infection. Rest, drink plenty of water and take the medicine I'm prescribing. Do you have any questions?",
                        "It looks like a mild throat infection. I'll prescribe something, but rest and fluids matter most. If it isn't better in five days, come back. Is there anything you'd like to ask me?",
                        "Ask one question, or say you have none and thank the doctor.",
                        "how|what|when|can i|should i|is it|will|do i|how long|work|school|" + No + "|" + Thanks + "|question",
                        "Can I go to work tomorrow?", "infection: an illness caused by germs|fluids: drinks", "consult-done"),
                };
                case "Pharmacy Pickup": return new List<ScenarioStep>
                {
                    S("prescription", "pharmacist", "Hello. Do you have a prescription?", "Hi, are you collecting a prescription?",
                        "Hi there. Are you here to collect a prescription, or would you like to buy something over the counter?",
                        "Say you are collecting a prescription.", Yes + "|prescription|doctor|collect|pick up|here|have|got",
                        "Yes, I'm collecting a prescription from Doctor Patel.",
                        "prescription: the doctor's order for medicine|over the counter: without a prescription"),
                    S("identity", "pharmacist", "What is your name and birthday?", "Can you confirm your name and date of birth?",
                        "Just to make sure I give this to the right person, can you confirm your name and date of birth?",
                        "Confirm your name and date of birth.", "i'?m|i am|it'?s|my name|name is|born|" + Numbers,
                        "Emma Lee, fourteenth of March, nineteen ninety-eight.", ""),
                    S("dosage", "pharmacist", "One tablet, three times a day, after food. Can you say it back?",
                        "Take one tablet three times a day after meals, for five days. Can you tell me how you'll take it?",
                        "These are taken one tablet three times a day after meals, and please finish the full five-day course even if you feel better. Could you repeat the instructions back to me?",
                        "Repeat the instructions in your own words.",
                        "three times|3 times|after|meal|food|one tablet|1 tablet|a day|five days|5 days|course|finish",
                        "One tablet three times a day after meals, for five days.", "tablet: a small pill|course: the full time you take a medicine"),
                    S("pay", "pharmacist", "That is eighteen fifty. Card or cash?", "That comes to eighteen dollars fifty. How would you like to pay?",
                        "Your total today is eighteen dollars fifty. Would you like to pay by card or cash, and would you like a bag?",
                        "Say how you will pay.", "card|cash|pay|credit|debit|phone|tap|bag|" + Yes,
                        "By card, please. And yes, a bag, thanks.", "tap: pay by touching your card on the machine", "pay"),
                };
                case "Ask for Directions": return new List<ScenarioStep>
                {
                    S("where", "staff", "Hello. Can I help you?", "Hi, you look a bit lost. Where are you trying to get to?",
                        "Hello, can I help you find something? This building can be a bit of a maze.",
                        "Ask where something is (toilets, the pharmacy, the exit, the café...).",
                        "where|toilet|restroom|bathroom|pharmacy|exit|way out|room|reception|cafe|lift|elevator|find|looking|stairs|waiting",
                        "Excuse me, where is the pharmacy?", "lift: elevator|corridor: a long hall|a maze: a confusing place"),
                    S("thanks", "staff", "Go straight, then left. Okay?",
                        "Go straight down this corridor and it's on your left, next to the lifts. Okay?",
                        "Head straight down this corridor, past the waiting area, and you'll see it on your left, just next to the lifts. Anything else I can help with?",
                        "Thank them (or ask one more question).", Thanks + "|" + Yes + "|" + No + "|where|left|right|next to",
                        "Great, thank you so much.", "head: go"),
                };
                case "Restaurant Host": return new List<ScenarioStep>
                {
                    S("booking", "host", "Hello. Do you have a booking?", "Good evening, welcome to Harbour Table. Do you have a reservation?",
                        "Good evening and welcome to Harbour Table. Have you booked with us tonight, or are you hoping for a walk-in table?",
                        "Say if you have a reservation.", Yes + "|" + No + "|reservation|book|under|name|walk in|table|hoping",
                        "No, I don't have a reservation. Do you have a table?", "reservation: a booked table|walk-in: without booking"),
                    S("party", "host", "How many people?", "No problem. How many people are in your party?",
                        "That shouldn't be a problem. How many will be dining with you this evening?",
                        "Say how many people.", Numbers + "|just me|myself|alone|people|of us|party|person|couple",
                        "Just two of us, please.", "party: your group|dining: eating a meal"),
                    S("table", "host", "Inside or outside?", "Would you prefer a table inside, by the window, or out on the terrace?",
                        "We have a quiet corner inside, a window table overlooking the harbour, or a seat out on the terrace. Do you have a preference?",
                        "Choose where to sit.", "window|inside|indoors|outside|terrace|quiet|corner|booth|anywhere|any|near|don'?t mind|prefer|harbour",
                        "A window table would be lovely, please.", "terrace: an outdoor area|overlooking: with a view of", "choose-table"),
                };
                case "Restaurant Drinks": return new List<ScenarioStep>
                {
                    S("drinks", "waiter", "Here is the menu. A drink?", "Here's the menu. Can I get you something to drink while you decide?",
                        "Here are your menus. While you're deciding, can I start you off with something to drink?",
                        "Order a drink (or say just water).",
                        "water|juice|cola|soda|lemonade|tea|coffee|drink|sparkling|still|tap|" + No + "|just|please|i'?d like|can i|could i",
                        "Could I have a sparkling water, please?", "sparkling: with bubbles|still: without bubbles", "open-menu"),
                };
                case "Restaurant Order": return new List<ScenarioStep>
                {
                    S("order", "waiter", "What would you like?", "Are you ready to order?",
                        "Have you had a chance to look at the menu? What can I get for you?",
                        "Say what you chose from the menu.", "i'?d like|i will|i'?ll have|can i|could i|please|have|get",
                        "I'd like the soup to start, and then the fish, please.", "to start: as the first course|main: the main dish"),
                    S("allergy", "waiter", "Any allergies?", "Do you have any allergies or dietary requirements?",
                        "Before I send that to the kitchen, does anyone have allergies or dietary requirements I should let the chef know about?",
                        "Answer about allergies.", "allerg|" + No + "|yes|nut|peanut|gluten|dairy|milk|shellfish|egg|vegetarian|vegan|fine",
                        "No allergies, thank you.", "dietary requirements: food you cannot or do not eat"),
                    S("confirm", "waiter", "Okay. Anything else?", "Lovely. Anything else for now?",
                        "Perfect, I'll put that through for you. Is there anything else I can get while you wait?",
                        "Finish the order politely.", Thanks + "|" + No + "|" + Yes + "|bread|water|that'?s it|for now",
                        "No, that's everything for now, thanks.", "put through: send the order", "order-placed"),
                };
                case "Restaurant Delivery": return new List<ScenarioStep>
                {
                    S("serve", "waiter", "Here is your food. Enjoy!", "Here you are. Can I get you anything else?",
                        "Here we are. Careful, the plate's hot. Can I bring you any sauces or anything else?",
                        "Thank the waiter, or ask for something.", Thanks + "|" + No + "|" + Yes + "|sauce|ketchup|pepper|salt|napkin|water|looks|smells",
                        "Thank you, it looks delicious.", "", "served"),
                };
                case "Restaurant Bill": return new List<ScenarioStep>
                {
                    S("how", "waiter", "Was it good?", "How was everything?",
                        "How was everything this evening? Was the food as you expected?",
                        "Say how the meal was.", "good|great|delicious|nice|lovely|fine|amazing|tasty|excellent|bit|too|cold|salty|ok|okay|enjoy",
                        "It was delicious, thank you.", "delicious: very tasty"),
                    S("next", "waiter", "Dessert or the bill?", "Would you like to see the dessert menu, or shall I bring the bill?",
                        "Can I tempt you with dessert or a coffee, or would you like the bill?",
                        "Ask for the bill.", "bill|check|pay|dessert|no|just|full|that'?s all",
                        "Could we have the bill, please?", "bill: the paper that shows what you must pay|tempt: make you want something", "bill"),
                };
                case "Restaurant Complaint": return new List<ScenarioStep>
                {
                    S("problem", "manager", "Hello. Is something wrong?", "Hi, I'm the manager. Is everything all right with your meal?",
                        "Good evening, I'm the duty manager. I noticed you called me over. Is there a problem with your meal?",
                        "Politely explain a problem (cold, wrong dish, too salty...).",
                        "cold|wrong|salty|late|not|ordered|hair|raw|burnt|too|problem|sorry|afraid|actually|mistake|long",
                        "I'm sorry, but my soup is cold.", "I'm afraid...: a polite way to start bad news"),
                    S("fix", "manager", "Sorry. New dish or money back?",
                        "I'm so sorry about that. Would you like a new one, or a refund?",
                        "I do apologise. I can have the kitchen prepare a fresh one right away, or remove it from your bill as a refund. Which would you prefer?",
                        "Choose a replacement or a refund.", "new|another|fresh|replace|refund|money|remove|bill|prefer|please|either",
                        "A fresh one would be great, thank you.", "refund: money given back|replacement: a new one instead", "complaint-solved"),
                };
                case "Restaurant Takeaway": return new List<ScenarioStep>
                {
                    S("order", "counter", "Takeaway? What would you like?", "Hi, is this for takeaway? What can I get you?",
                        "Hi there, are you ordering to take away? What would you like?",
                        "Order something to take away.", "i'?d like|can i|could i|please|takeaway|take away|to go|have|get|soup|burger|pasta|salad|fish|chips",
                        "Could I get the pasta to take away, please?", "takeaway: food you eat somewhere else"),
                    S("wait", "counter", "Ten minutes. Okay?", "That'll be about ten minutes. Is that okay?",
                        "That'll take about ten minutes. Would you like to wait here or come back?",
                        "Say if that is fine.", Yes + "|" + Thanks + "|wait|come back|fine|no problem", "That's fine, I'll wait here.", "", "takeaway-ordered"),
                };
            }
            return null;
        }
    }
}
