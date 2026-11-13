using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.Slice
{
    public enum WaiterStage { Idle, Taking, Detail, Confirming, Paying, Preparing, Served, Farewell }

    [Flags]
    public enum WaiterAction { None = 0, OpenMenu = 1, CloseMenu = 2, AskPayment = 4, SendToKitchen = 8, Highlight = 16, EndConversation = 32 }

    public sealed class WaiterTurn
    {
        public string Line = "";
        public WaiterAction Actions;
        public bool Progress;          // the learner moved the task forward
        public List<string> Objectives = new List<string>();
        /// Learning objectives this turn achieved. Setting adds to the list.
        public string Objective
        {
            get => Objectives.LastOrDefault() ?? "";
            set { if (!string.IsNullOrEmpty(value) && !Objectives.Contains(value)) Objectives.Add(value); }
        }
        public string ModelAnswer = "";// a natural way to say what the learner needed to say
        public string HighlightItem = "";
        public string Problem = "";
    }

    /// The waiter's conversation state machine. It always stays on the active task (ordering, paying,
    /// serving) and adapts wording to the IELTS band. Money and order changes go through OrderCart/Wallet.
    [Serializable]
    public sealed class WaiterBrain
    {
        public WaiterStage Stage = WaiterStage.Idle;
        public IeltsBand Band = IeltsBand.Band6;
        public string Name = "Rowan";
        public string[] LastOrder = new string[0]; // serialized CartLines (item|qty|choices) from the previous visit
        public int Turn;
        public int Visits;
        public Receipt LastReceipt;

        private static string Pick(int turn, params string[] options) => options[Math.Abs(turn) % options.Length];
        private string B(string b5, string b6, string b7, string b8)
        {
            switch (Band)
            {
                case IeltsBand.Band3:
                case IeltsBand.Band4:
                case IeltsBand.Band5: return b5;
                case IeltsBand.Band6: return b6;
                case IeltsBand.Band7: return b7;
                default: return b8;
            }
        }

        public string Greeting()
        {
            // Coming back mid-order: pick up where the conversation stopped.
            if (Stage == WaiterStage.Paying)
                return B("Hello again! Card or cash?", "Welcome back! Ready to pay? Card or cash?", "There you are! Shall we settle up — card or cash?", "Ah, the return of the customer. Shall we settle up?");
            if (Stage == WaiterStage.Confirming)
                return B("Hello again! Is your order OK?", "Welcome back! Is that everything on your order?", "Back again — so, is that the lot, or anything to add?", "Right where we left off — final answer on the order?");
            Stage = WaiterStage.Taking;
            Turn++;
            if (LastOrder.Length > 0 && Band >= IeltsBand.Band6)
            {
                var usual = Restore(LastOrder).Select(line => line.Describe()).FirstOrDefault() ?? "usual";
                return B("",
                    $"Hey, good to see you again! Would you like to stick with your usual {usual}, or are you in the mood to try something different?",
                    $"Oh hey, you're back! Same again — the {usual} — or shall I talk you into something new today?",
                    $"Well, look who it is! I could have your {usual} on the go before you've even sat down — unless you're feeling adventurous?");
            }
            return B(Pick(Turn, "Hi! Welcome to Forest Café. What would you like to drink today?", "Hello! Welcome. What can I get for you today?"),
                Pick(Turn, "Hi, welcome in! Have you had a chance to look at the menu, or do you need a minute?", "Hey there! What can I get started for you — something to drink, or are you eating with us too?"),
                Pick(Turn, "Hi there, welcome to the Forest! Grab a menu if you like — or if you already know what you're after, fire away.", "Afternoon! Take your time — the terrace isn't going anywhere. What are you in the mood for?"),
                Pick(Turn, "Hello, hello! You've timed it perfectly — the lunch rush has just died down, so you've got my undivided attention. Eating, drinking, or just here for the view?", "Welcome! Fair warning: everyone who orders 'just a coffee' ends up eyeing the cheesecake. What'll it be?"));
        }

        /// Handle one learner utterance. "spoken" is false for typed rehearsal.
        public WaiterTurn Respond(string text, OrderCart cart, Wallet wallet, int minute)
        {
            Turn++;
            var turn = new WaiterTurn();
            var intents = OrderIntentParser.Parse(text);
            var kinds = new HashSet<IntentKind>(intents.Select(intent => intent.Kind));
            var said = new List<string>();

            if (Stage == WaiterStage.Idle) Stage = WaiterStage.Taking;
            if (Stage == WaiterStage.Preparing || Stage == WaiterStage.Served)
                return AfterPayment(intents, cart, turn);

            if (kinds.Contains(IntentKind.Greet) && kinds.Count == 1)
            {
                turn.Line = B("Hi! I'm good, thanks. What would you like?", "I'm good, thanks for asking! So, what can I get you?", "Not bad at all, cheers! Now — what can I tempt you with?", "Living the dream, as they say. Right, what are we having?");
                turn.Objective = "greet";
                turn.Progress = true;
                return turn;
            }

            // Questions first: they never change the order.
            if (kinds.Contains(IntentKind.AskMenu))
            {
                turn.Actions |= WaiterAction.OpenMenu;
                turn.Progress = true;
                said.Add(B("Here's the menu. Take your time.", "Sure, here you go. Shout when you're ready.", "Of course — here you go. The seasonal stuff is on the right.", "Here you are. The specials are on the board — the berry pour-over is the one people keep raving about."));
            }
            if (kinds.Contains(IntentKind.AskRecommendation))
            {
                var pick = cart.Lines.Any(line => CafeMenu.Find(line.Item)?.Food == false) ? CafeMenu.Find("cheesecake") : CafeMenu.Find("seasonal-blend");
                turn.HighlightItem = pick.Id;
                turn.Actions |= WaiterAction.Highlight;
                turn.Objective = "ask_recommendation";
                turn.Progress = true;
                said.Add(pick.Id == "cheesecake"
                    ? B("I recommend the blueberry cheesecake. It's very popular.", "You can't go wrong with the blueberry cheesecake — it goes really well with coffee.", "Honestly? The blueberry cheesecake. It's dangerously good with anything from the coffee side.", "If I'm being completely honest, the cheesecake is the reason half our regulars keep coming back — the coffee's just an excuse.")
                    : B("I recommend the wild berry pour-over. It is our new coffee. It tastes fruity.", "Our new wild berry pour-over is really popular — it's a bit fruity and not too bitter.", "We've actually just introduced a new seasonal blend. It's a little on the pricey side, but if you're into something bold and slightly fruity, it might be right up your alley.", "Between you and me, the new wild berry pour-over is the star of the show — not cheap, mind you, but it's the kind of cup that makes you rethink your usual."));
            }
            foreach (var intent in intents.Where(intent => intent.Kind == IntentKind.AskDietary))
            {
                intent.Detail = System.Text.RegularExpressions.Regex.Replace(intent.Detail, @" free$", "-free");
                var items = Dietary(intent.Detail);
                said.Add(items.Length == 0
                    ? "I'm sorry, I'm not sure we have anything for that — let me check with the kitchen."
                    : B("Yes. These are " + intent.Detail + ": " + List(items) + ".",
                        "Sure — " + List(items) + " are all " + intent.Detail + "." + (intent.Detail.Contains("dairy") ? " We can also do oat, almond or soy milk in any coffee." : ""),
                        "Absolutely. For " + intent.Detail + " you've got " + List(items) + (intent.Detail.Contains("dairy") ? " — and any coffee can be made with oat, almond or soy." : "."),
                        "Plenty, actually: " + List(items) + (intent.Detail.Contains("dairy") ? ". And we'll happily swap in oat, almond or soy — no judgement, the oat is better anyway." : ".")));
                turn.Objective = "ask_dietary";
                turn.Progress = true;
            }
            foreach (var intent in intents.Where(intent => intent.Kind == IntentKind.AskIngredients))
            {
                var item = CafeMenu.Find(intent.Item);
                if (item == null) continue;
                said.Add($"The {item.Name.ToLowerInvariant().Replace("english", "English")} has {List(item.Ingredients)}. " + (item.Allergens.Length > 0 ? "It contains " + List(item.Allergens) + "." : "No common allergens.") + " " + B("Do you want one?", "Would you like to try it?", "Want me to add one?", "Shall I put one down for you?"));
                turn.HighlightItem = item.Id;
                turn.Actions |= WaiterAction.Highlight;
                turn.Objective = "ask_ingredients";
                turn.Progress = true;
            }

            // Order changes.
            CartLine touched = null;
            if (cart.Status == OrderStatus.Confirmed && intents.Any(intent => intent.Kind == IntentKind.Remove || intent.Kind == IntentKind.Modify || intent.Kind == IntentKind.Order || intent.Kind == IntentKind.Deny || intent.Kind == IntentKind.CancelOrder))
            {
                cart.Reopen();
                Stage = WaiterStage.Taking;
            }
            if (kinds.Contains(IntentKind.CancelOrder) && cart.Editable)
            {
                cart.Lines.Clear();
                cart.Payers = 1;
                Stage = WaiterStage.Taking;
                turn.Progress = true;
                turn.Line = "No problem — I've cancelled that order. What would you like instead?";
                return turn;
            }
            if (cart.Editable)
            {
                if (Stage == WaiterStage.Confirming && intents.Any(intent => intent.Kind == IntentKind.Order || intent.Kind == IntentKind.Modify || intent.Kind == IntentKind.Remove)) Stage = WaiterStage.Taking;
                foreach (var intent in intents)
                {
                    if (intent.Kind == IntentKind.Order && intent.Detail == "usual")
                    {
                        if (LastOrder.Length == 0) { said.Add(B("This is your first visit, so I don't know your usual yet!", "I don't think I know your usual yet — what would you like?", "Hmm, I don't think we've got a usual for you yet. Let's make one!", "A usual? You'll have to earn that title first — what are we starting with?")); continue; }
                        foreach (var line in Restore(LastOrder)) { cart.Lines.Add(line); touched = line; }
                        said.Add(B("OK, the same as last time.", "Coming right up — same as last time.", "The usual it is. Creature of habit — I respect it.", "The classic. Some things just don't need fixing."));
                        turn.Progress = true;
                        turn.Objective = "order_item";
                        continue;
                    }
                    if (intent.Kind == IntentKind.Order)
                    {
                        var line = cart.Add(intent.Item, intent.Quantity);
                        foreach (var option in intent.Options) line.Set(option.Key, option.Value);
                        touched = line;
                        turn.Progress = true;
                        turn.Objective = "order_item";
                        said.Add(Pick(Turn, "Sure", "Great", "Lovely", "Perfect") + " — " + line.Describe() + ".");
                    }
                    else if (intent.Kind == IntentKind.Modify)
                    {
                        var line = touched ?? cart.NeedsDetail() ?? cart.Lines.LastOrDefault();
                        if (line == null) continue;
                        var applied = intent.Options.Where(option => line.Menu.Has(option.Key)).ToArray();
                        foreach (var option in applied) line.Set(option.Key, option.Value);
                        if (applied.Length == 0)
                            said.Add($"Sorry, I can't do that with the {line.Menu.Name.ToLowerInvariant().Replace("english", "English")}.");
                        else
                        {
                            said.Add(Pick(Turn, "No problem", "Of course", "Sure thing") + " — " + string.Join(", ", applied.Select(option => option.Value)) + ".");
                            turn.Progress = true;
                            turn.Objective = "customise";
                        }
                        touched = line;
                    }
                    else if (intent.Kind == IntentKind.Remove)
                    {
                        var line = cart.Lines.LastOrDefault(entry => entry.Item == intent.Item);
                        if (line == null) continue;
                        cart.Remove(line);
                        said.Add(B("OK, no " + line.Menu.Name.ToLowerInvariant().Replace("english", "English") + ".", "No worries, I've taken the " + line.Menu.Name.ToLowerInvariant().Replace("english", "English") + " off.", "Done — the " + line.Menu.Name.ToLowerInvariant().Replace("english", "English") + " is off the list.", "Consider the " + line.Menu.Name.ToLowerInvariant().Replace("english", "English") + " forgotten."));
                        turn.Progress = true;
                        turn.Objective = "change_order";
                    }
                }
            }

            if (kinds.Contains(IntentKind.SplitBill))
            {
                var payers = intents.First(intent => intent.Kind == IntentKind.SplitBill).Detail;
                cart.Payers = payers == "three" || payers == "3" ? 3 : payers == "four" || payers == "4" ? 4 : 2;
                said.Add(cart.Empty ? "Sure, we can split the bill." : $"No problem — split {cart.Payers} ways, that's {CafeMenu.Money(cart.Split(cart.Payers)[0])} for you.");
                turn.Objective = "split_bill";
                turn.Progress = true;
            }
            if (kinds.Contains(IntentKind.AskPrice))
            {
                said.Add(cart.Empty ? "Prices are on the menu next to each item." : B("It is " + CafeMenu.Money(cart.Total) + ".", "That comes to " + CafeMenu.Money(cart.Total) + " so far.", "You're at " + CafeMenu.Money(cart.Total) + " so far.", "We're sitting at " + CafeMenu.Money(cart.Total) + " — still very reasonable, I'd say."));
                turn.Objective = "ask_price";
                turn.Progress = true;
            }

            // Payment.
            if (Stage == WaiterStage.Paying || (kinds.Contains(IntentKind.PayCard) || kinds.Contains(IntentKind.PayCash)) && cart.Status == OrderStatus.Confirmed)
            {
                if (kinds.Contains(IntentKind.PayCard) || kinds.Contains(IntentKind.PayCash))
                {
                    var method = kinds.Contains(IntentKind.PayCash) ? "cash" : "card";
                    if (wallet.TryPay(cart, method, minute, out var receipt, out var problem))
                    {
                        LastOrder = Save(cart.Lines);
                        LastReceipt = receipt;
                        Visits++;
                        Stage = WaiterStage.Preparing;
                        turn.Actions |= WaiterAction.SendToKitchen | WaiterAction.CloseMenu;
                        turn.Progress = true;
                        turn.Objective = "pay";
                        said.Add(B("Thank you! Here is your receipt. Please sit down. I will bring it to you.",
                            "Thanks! Here's your receipt. Grab a seat anywhere and I'll bring it over when it's ready.",
                            "Lovely, that's gone through. Find yourself a spot — the terrace is nice right now — and I'll bring everything over.",
                            "All sorted. Pick a table with a view — I'll come and find you, no need to hover by the counter."));
                    }
                    else
                    {
                        turn.Problem = problem;
                        said.Add(problem + " " + B("Card or cash?", "Would you like to try another way?", "Want to try the other option?", "Shall we try plan B?"));
                    }
                    return Finish(turn, said);
                }
                if (Stage == WaiterStage.Paying && !said.Any())
                {
                    said.Add(B("How do you want to pay? Card or cash?", "How would you like to pay — card or cash?", "Card or cash today?", "And how are we settling up — card, cash, or IOU? Kidding about the IOU."));
                    turn.ModelAnswer = "By card, please.";
                    return Finish(turn, said);
                }
            }

            // Confirmation.
            if (Stage == WaiterStage.Confirming)
            {
                if (kinds.Contains(IntentKind.Confirm) && cart.Confirm())
                {
                    Stage = WaiterStage.Paying;
                    turn.Actions |= WaiterAction.AskPayment;
                    turn.Progress = true;
                    turn.Objective = "confirm";
                    said.Add((cart.Payers > 1 ? $"That's {CafeMenu.Money(cart.Split(cart.Payers)[0])} for your share. " : $"That's {CafeMenu.Money(cart.Total)}. ")
                        + B("Card or cash?", "How would you like to pay?", "How would you like to pay — card or cash?", "Card, cash, or are we splitting it some other creative way?"));
                    turn.ModelAnswer = "By card, please.";
                    return Finish(turn, said);
                }
                if (kinds.Contains(IntentKind.Deny))
                {
                    Stage = WaiterStage.Taking;
                    said.Add(B("OK. What do you want to change?", "No problem — what would you like to change?", "Sure, what are we changing?", "Second thoughts? Totally allowed. What are we swapping?"));
                    turn.Progress = true;
                    turn.ModelAnswer = "Actually, could I change the latte to a large?";
                    return Finish(turn, said);
                }
            }

            // Follow-up questions for the line that still needs details.
            var pending = cart.NeedsDetail();
            if (cart.Editable && pending != null)
            {
                Stage = WaiterStage.Detail;
                said.Add(Ask(pending));
                return Finish(turn, said);
            }

            if (cart.Editable && !cart.Empty && (turn.Progress || kinds.Contains(IntentKind.Confirm) || kinds.Contains(IntentKind.Deny)))
            {
                if (kinds.Contains(IntentKind.Confirm))
                {
                    Stage = WaiterStage.Confirming;
                    turn.Progress = true;
                    said.Add(B("So, ", "So that's ", "Let me read that back: ", "Right, so we've got ") + List(cart.Lines.Select(line => line.Describe()).ToArray()) + $". That's {CafeMenu.Money(cart.Total)}. " + B("Is that right?", "Is that everything?", "Have I got that right?", "Anything I've missed, or are we good?"));
                    turn.ModelAnswer = "Yes, that's everything, thanks.";
                    return Finish(turn, said);
                }
                Stage = WaiterStage.Taking;
                said.Add(B("Anything else?", "Anything else, or is that everything?", "Anything to go with that, or are you all set?", "Can I tempt you with something sweet on the side, or is that the lot?"));
                turn.ModelAnswer = "That's all, thanks.";
                return Finish(turn, said);
            }

            if (said.Count == 0)
            {
                if (kinds.Contains(IntentKind.Thanks) || kinds.Contains(IntentKind.Goodbye))
                    said.Add(B("You're welcome!", "No worries!", "Any time!", "My pleasure — truly."));
                else if (kinds.Contains(IntentKind.Confirm) && cart.Empty)
                    said.Add(B("Great! What do you want?", "Great — what can I get you?", "Lovely. So what are we having?", "Fantastic. And what would 'yes' like to drink?"));
                else
                {
                    said.Add(B("Sorry, I didn't understand. Can you say it again? For example: “A latte, please.”",
                        "Sorry, could you say that again? You can order a drink or ask about the menu.",
                        "Sorry, I didn't quite catch that — are you after a drink, something to eat, or both?",
                        "Sorry — you lost me there for a second. Was that an order, or a philosophical statement?"));
                    turn.ModelAnswer = "Could I get an iced latte with oat milk, please?";
                    turn.Problem = "not_understood";
                }
            }
            return Finish(turn, said);
        }

        private WaiterTurn AfterPayment(List<OrderIntent> intents, OrderCart cart, WaiterTurn turn)
        {
            var kinds = intents.Select(intent => intent.Kind).ToList();
            if (kinds.Contains(IntentKind.AskWait))
            {
                turn.Line = B("It will be ready soon.", "It shouldn't be long — just a few minutes.", "Won't be long — the barista's on it now.", "Any minute now. Good things, waiting, all that.");
                turn.Objective = "ask_wait";
                turn.Progress = true;
            }
            else if (kinds.Contains(IntentKind.Order))
                turn.Line = B("Your order is paid. Please order more after you get it.", "Let's get this order to you first, then I'm happy to take another.", "Let me bring this lot over first, then we can talk round two.", "One order at a time — I'm good, but I'm not that good.");
            else if (kinds.Contains(IntentKind.Thanks) || kinds.Contains(IntentKind.Goodbye))
            {
                turn.Line = B("You're welcome! Enjoy!", "No problem — enjoy!", "Any time — enjoy!", "Pleasure's all mine. Enjoy!");
                turn.Actions |= WaiterAction.EndConversation;
            }
            else turn.Line = B("Your order is coming. Please sit down.", "It's on its way — grab a seat and I'll bring it over.", "It's in the works — find a spot and I'll come to you.", "It's on its way; sit back and enjoy the view.");
            return turn;
        }

        /// The player changed the cart with the menu UI. Rowan reacts and asks what is still missing.
        public WaiterTurn MenuChanged(OrderCart cart, CartLine added)
        {
            Turn++;
            if (Stage == WaiterStage.Idle || Stage == WaiterStage.Confirming) Stage = WaiterStage.Taking;
            var said = new List<string>();
            if (added != null) said.Add(Pick(Turn, "Great choice", "Lovely", "Nice one") + " — " + added.Describe() + ".");
            var pending = cart.NeedsDetail();
            if (pending != null) { Stage = WaiterStage.Detail; said.Add(Ask(pending)); }
            else if (!cart.Empty) said.Add(B("Anything else?", "Anything else, or is that everything?", "Anything to go with that, or are you all set?", "Can I tempt you with something sweet on the side, or is that the lot?"));
            return Finish(new WaiterTurn(), said);
        }

        private string Ask(CartLine line)
        {
            var name = line.Menu.Name.ToLowerInvariant().Replace("english", "English");
            var group = line.Missing().First();
            if (group == OptionGroup.Temperature)
                return B($"Hot or iced?", $"Would you like the {name} hot or iced?", $"Are we going hot or iced on the {name}?", $"Hot or iced on the {name}? Iced is the sensible choice in this weather, but I won't judge.");
            return B($"What size? Small, medium or large?", $"What size would you like — small, medium or large?", $"And what size are we thinking — small, medium or large?", $"Size-wise: small and civilised, medium, or large and unapologetic?");
        }

        private WaiterTurn Finish(WaiterTurn turn, List<string> said)
        {
            turn.Line = string.Join(" ", said.Where(line => line != "").Distinct());
            return turn;
        }

        public WaiterTurn Deliver(OrderCart cart)
        {
            Stage = WaiterStage.Served;
            cart.Status = OrderStatus.Delivered;
            return new WaiterTurn
            {
                Line = B("Here you are. Enjoy!", "Here you go — ", "There we are: ", "Ta-da: ") + (Band == IeltsBand.Band5 ? "" : List(cart.Lines.Select(line => line.Describe()).ToArray()) + ". " + B("", "Enjoy!", "Enjoy — give me a wave if you need anything.", "Enjoy, and do report back on the cheesecake situation.")),
                Progress = true,
                Objective = "receive",
            };
        }

        public static string[] Save(IEnumerable<CartLine> lines) => lines.Select(line => line.Item + "|" + line.Quantity + "|" + string.Join(";", line.Choices)).ToArray();
        public static List<CartLine> Restore(IEnumerable<string> saved)
        {
            var lines = new List<CartLine>();
            foreach (var entry in saved ?? new string[0])
            {
                var parts = entry.Split('|');
                if (parts.Length < 2 || CafeMenu.Find(parts[0]) == null) continue;
                lines.Add(new CartLine { Item = parts[0], Quantity = int.TryParse(parts[1], out var quantity) ? Math.Max(1, Math.Min(quantity, 9)) : 1, Choices = parts.Length > 2 && parts[2] != "" ? parts[2].Split(';') : new string[0] });
            }
            return lines;
        }

        private static string[] Dietary(string detail)
        {
            IEnumerable<MenuItem> items;
            if (detail.Contains("dairy") || detail.Contains("lactose")) items = CafeMenu.Items.Where(item => item.Tagged("dairy-free"));
            else if (detail.Contains("vegan")) items = CafeMenu.Items.Where(item => item.Tagged("vegan"));
            else if (detail.Contains("vegetarian")) items = CafeMenu.Items.Where(item => item.Tagged("vegetarian") || item.Tagged("vegan"));
            else if (detail.Contains("gluten")) items = CafeMenu.Items.Where(item => item.Food && Array.IndexOf(item.Allergens, "gluten") < 0);
            else if (detail.Contains("nut")) items = CafeMenu.Items.Where(item => item.Food && Array.IndexOf(item.Allergens, "tree nuts") < 0);
            else if (detail.Contains("caffeine") || detail.Contains("decaf")) items = CafeMenu.Items.Where(item => item.Tagged("caffeine-free") || item.Category == MenuCategory.Juice || item.Category == MenuCategory.Smoothies);
            else items = Enumerable.Empty<MenuItem>();
            return items.Take(4).Select(item => "the " + item.Name.ToLowerInvariant().Replace("english", "English")).ToArray();
        }
        private static string List(string[] items)
        {
            if (items.Length <= 1) return items.FirstOrDefault() ?? "";
            // Order lines may already contain "and" ("with oat milk and no sugar"), so join them with "plus".
            var joiner = items.Any(item => item.Contains(" and ")) ? ", plus " : " and ";
            return string.Join(", ", items.Take(items.Length - 1)) + joiner + items.Last();
        }
    }
}
