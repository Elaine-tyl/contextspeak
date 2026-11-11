using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [Serializable] public class DrinkOrder { public string drink; public int quantity; }
    [Serializable] public class CafeIntent
    {
        public string action;
        public DrinkOrder[] items;
        public string temperature;
        public string service;
    }
    [Serializable] public class FriendAnswer { public string text; public string feedback; }
    [Serializable] public class FriendReview
    {
        public bool accepted;
        public string acknowledgement;
        public string feedback;
    }
    [Serializable] public class CafeSave
    {
        public int version = 1;
        public DrinkOrder[] items = Array.Empty<DrinkOrder>();
        public string temperature = "";
        public string service = "";
        public bool ordered;
        public string phase = "counter";
        public FriendAnswer[] answers = Array.Empty<FriendAnswer>();
        public bool practice;
    }

    // Pure mission rules: neither a model response nor a saved stage can bypass prerequisites.
    public sealed class CafeMission
    {
        public const int Budget = 1200;
        private CafeSave state = new CafeSave();
        private string acknowledgement = "";
        public string Feedback { get; private set; } = "";
        public bool Practice => state.practice;
        public bool OrderComplete => state.ordered;
        public bool Complete => state.phase == "complete";
        public string Phase => state.phase;
        public int Turn => state.answers.Length;
        public string Temperature => state.temperature;
        public int Total => state.items.Sum(item => Price(item.drink) * item.quantity);
        public FriendAnswer[] History => state.answers;
        public string Stage => OrderComplete ? "complete" : !ValidOrder() ? "order" : state.temperature == "" ? "temperature" : state.service == "" ? "service" : "confirm";
        public string Order => string.Join(", ", state.items.Select(item => item.quantity + " " + item.drink + (item.quantity == 2 ? "s" : "")));
        public static readonly string[] Questions = {
            "We finally have our drinks! What do you like to drink, and why?",
            "What would you like to do this weekend?",
            "Could you invite me to join you and suggest a time?"
        };
        public string FriendPrompt => Complete ? "That sounds lovely! Thanks for inviting me. Let's catch up again soon." : Questions[Math.Min(Turn, 2)];
        public string FriendLine => (acknowledgement == "" ? "" : acknowledgement + " ") + FriendPrompt;
        public string Prompt
        {
            get
            {
                switch (Stage)
                {
                    case "temperature": return "Would you like your drinks hot or iced?";
                    case "service": return "Are those for here or to go?";
                    case "confirm": return $"That's {Order}, {state.temperature}, {state.service}. Your total is ${Total / 100.0:F2}. Shall I place your order?";
                    case "complete": return "Here are your drinks! Alex is waiting at the table. Enjoy!";
                    default: return "Hey! I'm Maya. What can I get for you and your friend? You have twelve dollars to spend.";
                }
            }
        }
        private static int Price(string drink)
        {
            switch (drink)
            {
                case "coffee": return 400;
                case "latte": return 650;
                case "tea": return 300;
                case "lemonade": return 350;
                default: return 0;
            }
        }
        private bool ValidOrder()
        {
            return state.items.Length > 0 && state.items.All(item => item != null && Price(item.drink) > 0 && item.quantity >= 1 && item.quantity <= 2)
                && state.items.Sum(item => item.quantity) == 2 && Total <= Budget;
        }
        public string Apply(CafeIntent intent)
        {
            if (OrderComplete) return Prompt;
            switch (intent?.action)
            {
                case "ask_price": return "Coffee is four dollars, latte six fifty, tea three, and lemonade three fifty. " + Prompt;
                case "cancel": state = new CafeSave { practice = Practice }; return "Sure, let's start again. " + Prompt;
                case "repeat": return Prompt;
                case "order":
                    if (intent.items == null || intent.items.Length < 1 || intent.items.Length > 2 || intent.items.Any(item => item == null || Price(item.drink) == 0 || item.quantity < 1 || item.quantity > 2))
                        return "Please order two drinks from the menu.";
                    var merged = intent.items.GroupBy(item => item.drink).Select(group => new DrinkOrder { drink = group.Key, quantity = group.Sum(item => item.quantity) }).ToArray();
                    state = new CafeSave { items = merged, practice = Practice };
                    if (Total > Budget) return $"That comes to ${Total / 100.0:F2}, over your budget. What would you like instead?";
                    if (!ValidOrder()) return "You'll need two drinks, one for each of you. What's your complete order?";
                    if (intent.temperature == "hot" || intent.temperature == "iced") state.temperature = intent.temperature;
                    if (intent.service == "here" || intent.service == "takeaway") state.service = intent.service;
                    break;
                case "temperature":
                    if (ValidOrder() && (intent.temperature == "hot" || intent.temperature == "iced")) state.temperature = intent.temperature;
                    break;
                case "service":
                    if (ValidOrder() && state.temperature != "" && (intent.service == "here" || intent.service == "takeaway")) state.service = intent.service;
                    break;
                case "confirm":
                    if (Stage == "confirm") state.ordered = true;
                    break;
                default: return "Sorry, I didn't catch that. You can ask a price, place an order, or ask me to repeat. " + Prompt;
            }
            return Prompt;
        }
        public CafeIntent Parse(string text)
        {
            var words = (text ?? "").ToLowerInvariant().Trim().Replace('’', '\'');
            if (Regex.IsMatch(words, "how much|price|cost")) return Intent("ask_price");
            if (Regex.IsMatch(words, "repeat|say that again")) return Intent("repeat");
            if (Regex.IsMatch(words, "cancel|start over")) return Intent("cancel");
            var positive = Regex.Replace(words, @"\b(?:not|no|don't want|do not want|don't like|do not like)\s+(?:hot|iced|cold|for here|here|to go|take away|takeaway)\b", "");
            if (Regex.IsMatch(positive, @"\b(no|not|incorrect|wrong|don't)\b|n't")) return Intent("repeat");
            if (Regex.IsMatch(words, @"\b(?:zero|three|four|five|six|seven|eight|nine|ten|eleven|twelve|dozen|[03-9]|[0-9]{2,})\s+(?:(?:hot|iced)\s+)?(?:coffees?|lattes?|teas?|lemonades?)\b"))
                return new CafeIntent { action = "order", items = Array.Empty<DrinkOrder>() };
            var matches = Regex.Matches(words, @"\b(one|two|1|2|a|an)\s+(?:(hot|iced)\s+)?(coffees?|lattes?|teas?|lemonades?)\b");
            var temperature = Regex.IsMatch(positive, @"\b(iced|cold)\b") ? "iced" : Regex.IsMatch(positive, @"\bhot\b") ? "hot" : "";
            var service = Regex.IsMatch(positive, "take away|takeaway|to go") ? "takeaway" : Regex.IsMatch(positive, @"\bhere\b") ? "here" : "";
            if (matches.Count > 0)
                return new CafeIntent { action = "order", temperature = temperature, service = service,
                    items = matches.Cast<Match>().Select(match => new DrinkOrder { drink = match.Groups[3].Value.TrimEnd('s'), quantity = match.Groups[1].Value == "two" || match.Groups[1].Value == "2" ? 2 : 1 }).ToArray() };
            if (Stage != "order" && temperature != "") return new CafeIntent { action = "temperature", temperature = temperature };
            if ((Stage == "service" || Stage == "confirm") && service != "") return new CafeIntent { action = "service", service = service };
            if (Stage == "confirm") return Intent(!words.Contains("?") && !Regex.IsMatch(words, @"\b(but|wait|hold on|maybe|actually|unless|instead|if|guess|unsure|later)\b") && Regex.IsMatch(words, @"^(yes|yeah|yep|correct|that is correct|that's correct|go ahead|sounds good)\b") ? "confirm" : "repeat");
            return Intent("unknown");
        }
        private static CafeIntent Intent(string action)
        {
            return new CafeIntent { action = action };
        }
        public bool BeginWalk()
        {
            if (!OrderComplete || Phase != "counter") return false;
            state.phase = "walking";
            return true;
        }
        public bool Sit()
        {
            if (Phase != "walking") return false;
            state.phase = "seated";
            return true;
        }
        public bool ReviewFriend(string text, FriendReview review)
        {
            if (Phase != "seated" || string.IsNullOrWhiteSpace(text) || text.Length > 400 || review == null || string.IsNullOrWhiteSpace(review.acknowledgement) || review.acknowledgement.Length > 220 || string.IsNullOrWhiteSpace(review.feedback) || review.feedback.Length > 240) return false;
            acknowledgement = review.acknowledgement;
            Feedback = review.feedback;
            if (!review.accepted) return false;
            state.answers = state.answers.Concat(new[] { new FriendAnswer { text = text.Trim(), feedback = review.feedback } }).ToArray();
            if (Turn == 3) state.phase = "complete";
            return true;
        }
        public void StartPractice(string stage)
        {
            state = new CafeSave { practice = true };
            if (stage != "friend") return;
            state.items = new[] { new DrinkOrder { drink = "coffee", quantity = 1 }, new DrinkOrder { drink = "tea", quantity = 1 } };
            state.temperature = "hot";
            state.service = "here";
            state.ordered = true;
            state.phase = "seated";
        }
        public string Save()
        {
            return JsonUtility.ToJson(state);
        }
        public bool Restore(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 16000) return false;
            try
            {
                var saved = JsonUtility.FromJson<CafeSave>(json);
                if (saved == null || saved.version != 1 || saved.items == null || saved.items.Length > 2 || saved.answers == null || saved.answers.Length > 3
                    || !new[] { "counter", "walking", "seated", "complete" }.Contains(saved.phase)
                    || !new[] { "", "hot", "iced" }.Contains(saved.temperature) || !new[] { "", "here", "takeaway" }.Contains(saved.service)
                    || saved.items.Any(item => item == null || Price(item.drink) == 0 || item.quantity < 1 || item.quantity > 2)
                    || saved.items.Select(item => item.drink).Distinct().Count() != saved.items.Length
                    || saved.answers.Any(answer => answer == null || string.IsNullOrWhiteSpace(answer.text) || answer.text.Length > 400 || answer.feedback == null || answer.feedback.Length > 240)) return false;
                var candidate = new CafeMission { state = saved };
                if ((saved.ordered && (!candidate.ValidOrder() || saved.temperature == "" || saved.service == ""))
                    || (saved.phase != "counter" && !saved.ordered) || (saved.phase == "complete") != (saved.answers.Length == 3)
                    || (saved.phase == "counter" || saved.phase == "walking") && saved.answers.Length != 0) return false;
                state = saved;
                acknowledgement = "";
                Feedback = Turn > 0 ? state.answers.Last().feedback : "";
                return true;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
