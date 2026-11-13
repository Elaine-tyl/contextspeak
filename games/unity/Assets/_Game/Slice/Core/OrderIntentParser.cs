using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Slice
{
    public enum IntentKind
    {
        Unknown, Greet, AskMenu, AskRecommendation, AskDietary, AskIngredients, AskPrice, Order, Modify, Remove,
        Confirm, Deny, SplitBill, PayCard, PayCash, Thanks, Goodbye, AskWait, CancelOrder,
    }

    [Serializable]
    public sealed class OrderIntent
    {
        public IntentKind Kind;
        public string Item = "";
        public int Quantity = 1;
        public List<KeyValuePair<OptionGroup, string>> Options = new List<KeyValuePair<OptionGroup, string>>();
        public string Detail = ""; // dietary keyword, payer count, etc.
        public override string ToString() => Kind + (Item != "" ? " " + Quantity + "×" + Item : "") + (Options.Count > 0 ? " [" + string.Join(", ", Options.Select(o => o.Value)) + "]" : "") + (Detail != "" ? " (" + Detail + ")" : "");
    }

    /// Offline understanding of natural café English. It works without any AI service and is the
    /// only thing allowed to change the order; an AI reviewer may add language feedback on top.
    public static class OrderIntentParser
    {
        private static readonly Dictionary<string, int> Numbers = new Dictionary<string, int>
        {
            { "a", 1 }, { "an", 1 }, { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 }, { "six", 6 },
            { "a couple of", 2 }, { "couple of", 2 }, { "a pair of", 2 }, { "both", 2 },
        };

        // Longest phrases first so "less sugar" wins over "sugar".
        private static readonly (string pattern, OptionGroup group, string choice)[] OptionPhrases =
        {
            (@"(?:no|without|zero|skip the|hold the) sugar|sugar[- ]free|unsweetened|not sweet", OptionGroup.Sugar, "no sugar"),
            (@"(?:less|half|a little|light|low) sugar|not too sweet|less sweet", OptionGroup.Sugar, "less sugar"),
            (@"(?:extra|more) (?:sugar|sweet)|extra sweet|sweeter", OptionGroup.Sugar, "extra sugar"),
            (@"oat(?:ly)?(?: milk)?", OptionGroup.Milk, "oat milk"),
            (@"almond(?: milk)?", OptionGroup.Milk, "almond milk"),
            (@"soy(?:a)?(?: milk)?", OptionGroup.Milk, "soy milk"),
            (@"dairy free|non dairy|plant milk|plant based milk", OptionGroup.Milk, "oat milk"),
            (@"(?:no|without) milk|black", OptionGroup.Milk, "no milk"),
            (@"(?:whole|full[- ]cream|regular|normal|cow'?s) milk", OptionGroup.Milk, "whole milk"),
            (@"(?:extra|additional|another|double) (?:espresso )?shot|extra espresso|double shot|make it strong(?:er)?|stronger", OptionGroup.ExtraShot, "extra shot"),
            (@"(?:no|without|hold the|skip the) ice", OptionGroup.Ice, "no ice"),
            (@"(?:less|light|easy on the|not much|a little) ice", OptionGroup.Ice, "less ice"),
            (@"(?:less|not too|not very|not) spicy|no chilli|mild", OptionGroup.Spice, "mild"),
            (@"medium spicy|medium heat|a bit spicy|bit of heat", OptionGroup.Spice, "medium spicy"),
            (@"(?:extra|very|really|super) spicy|extra hot sauce|as spicy as", OptionGroup.Spice, "extra spicy"),
            (@"iced|cold|on ice|over ice", OptionGroup.Temperature, "iced"),
            (@"hot|warm", OptionGroup.Temperature, "hot"),
            (@"small|short|little|regular size", OptionGroup.Size, "small"),
            (@"medium|regular|tall", OptionGroup.Size, "medium"),
            (@"large|big|grande|venti", OptionGroup.Size, "large"),
        };

        private static readonly (MenuItem item, string phrase)[] ItemPhrases = CafeMenu.Items
            .SelectMany(item => item.Aliases.Append(item.Name.ToLowerInvariant()).Select(alias => (item, alias.ToLowerInvariant())))
            .OrderByDescending(pair => pair.Item2.Length).ToArray();

        public static string Normalize(string text)
        {
            text = (text ?? "").ToLowerInvariant().Replace('’', '\'').Replace('-', ' ');
            text = Regex.Replace(text, @"[^a-z0-9' ]+", " ");
            text = Regex.Replace(text, @"\b(lattes|cappuccinos|americanos|croissants|brownies|teas|coffees|juices|smoothies|pancake|sandwiches|lemonades)\b",
                match => match.Value == "pancake" ? "pancakes" : match.Value.EndsWith("ches") ? match.Value.Substring(0, match.Value.Length - 2) : match.Value.TrimEnd('s'));
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        public static List<OrderIntent> Parse(string raw)
        {
            var text = Normalize(raw);
            var intents = new List<OrderIntent>();
            if (text.Length == 0) return intents;

            bool Has(string pattern) => Regex.IsMatch(text, @"\b(?:" + pattern + @")\b");

            // Questions and conversational moves. Several can appear in one utterance.
            if (Has("hi|hello|hey|good (?:morning|afternoon|evening)|how are you|how's it going")) intents.Add(new OrderIntent { Kind = IntentKind.Greet });
            if (Has("(?:see|look at|have|get|check|show me|read) (?:the|a|your) menu|what (?:do you have|have you got|do you serve|can i get|'s on the menu|is on the menu)|menu please|the menu"))
                intents.Add(new OrderIntent { Kind = IntentKind.AskMenu });
            if (Has("recommend|recommendation|suggest|what'?s good|what is good|what'?s popular|your favou?rite|best seller|bestseller|what should i (?:get|have|try)|surprise me"))
                intents.Add(new OrderIntent { Kind = IntentKind.AskRecommendation });
            var dietary = Regex.Match(text, @"\b(dairy free|non dairy|lactose|vegan|vegetarian|gluten free|gluten|nut free|nut allergy|nuts|allerg(?:y|ic|ies)|caffeine free|decaf)\b");
            if (dietary.Success && Has("do you have|anything|something|is there|are there|which|what|options?|any|i'?m|i am|allerg"))
                intents.Add(new OrderIntent { Kind = IntentKind.AskDietary, Detail = dietary.Groups[1].Value });
            if (Has("split the bill|split it|pay separately|separate bills?|go halves|go dutch|each pay|split (?:the )?(?:check|cheque)"))
                intents.Add(new OrderIntent { Kind = IntentKind.SplitBill, Detail = Regex.Match(text, @"\b(two|three|four|2|3|4)\b").Value });
            if (Has("how much|what'?s the (?:price|total|damage)|what does it come to|price|cost")) intents.Add(new OrderIntent { Kind = IntentKind.AskPrice });
            if (Has("how long|how much longer|is it ready|when will|wait time|will it take")) intents.Add(new OrderIntent { Kind = IntentKind.AskWait });
            var tender = PaymentMethod(raw);
            if (tender != IntentKind.Unknown) intents.Add(new OrderIntent { Kind = tender });
            if (Has("cancel (?:my |the |this )?order|start (?:again|over)")) intents.Add(new OrderIntent { Kind = IntentKind.CancelOrder });
            if (Has("thanks|thank you|cheers|appreciate it")) intents.Add(new OrderIntent { Kind = IntentKind.Thanks });
            if (Has("bye|goodbye|see you|have a (?:good|nice|great) (?:day|one|evening)|take care")) intents.Add(new OrderIntent { Kind = IntentKind.Goodbye });

            if (Has("(?:my|the) usual|same as (?:last time|before|usual)|same again|what i had last time"))
                intents.Add(new OrderIntent { Kind = IntentKind.Order, Detail = "usual" });

            var ingredients = Regex.Match(text, @"\bwhat(?:'s| is| are) (?:in|inside) (?:the |a |an )?(.+)$");

            // Find item mentions in reading order.
            var mentions = new List<(int start, int end, MenuItem item, bool negated, int quantity)>();
            var taken = new bool[text.Length];
            foreach (var (item, phrase) in ItemPhrases)
            {
                foreach (Match match in Regex.Matches(text, @"\b" + Regex.Escape(phrase) + @"\b"))
                {
                    if (Enumerable.Range(match.Index, match.Length).Any(index => taken[index])) continue;
                    for (var index = match.Index; index < match.Index + match.Length; index++) taken[index] = true;
                    var before = text.Substring(0, match.Index);
                    var negated = Regex.IsMatch(before, @"\b(?:no|not|don'?t want|do not want|cancel|remove|take off|skip|instead of|scratch|forget(?: about)?)(?: the| that| my| a| an| any| one| two)? $");
                    mentions.Add((match.Index, match.Index + match.Length, item, negated, QuantityBefore(before)));
                }
            }
            mentions.Sort((a, b) => a.start.CompareTo(b.start));

            if (ingredients.Success && mentions.Count > 0)
                intents.Add(new OrderIntent { Kind = IntentKind.AskIngredients, Item = mentions[0].item.Id });

            var questionOnly = intents.Any(intent => intent.Kind == IntentKind.AskIngredients || intent.Kind == IntentKind.AskPrice && !Has("i'?ll have|i'?d like|can i (?:get|have)|could i (?:get|have)|i want|give me|let me (?:get|have)"));
            if (!questionOnly)
            {
                for (var index = 0; index < mentions.Count; index++)
                {
                    var mention = mentions[index];
                    var scopeStart = index == 0 ? 0 : Boundary(text, mentions[index - 1].end, mention.start, true);
                    var scopeEnd = index == mentions.Count - 1 ? text.Length : Boundary(text, mention.end, mentions[index + 1].start, false);
                    var scope = text.Substring(scopeStart, Math.Max(0, scopeEnd - scopeStart));
                    var intent = new OrderIntent { Kind = mention.negated ? IntentKind.Remove : IntentKind.Order, Item = mention.item.Id, Quantity = mention.quantity };
                    if (!mention.negated) intent.Options = Options(scope.Replace(text.Substring(mention.start, mention.end - mention.start), " "), mention.item);
                    // "Do you have matcha?" is a question, but in a café it is also a request; treat as an order only with a request verb.
                    if (!mention.negated && Regex.IsMatch(text, @"^(?:do|does|is|are) ") && !Has("i'?ll|i'?d like|can i|could i|may i|i want")) intent.Kind = IntentKind.AskIngredients;
                    intents.Add(intent);
                }
            }

            if (mentions.Count == 0 && !intents.Any(intent => intent.Kind == IntentKind.AskDietary))
            {
                var options = Options(text, null);
                if (options.Count > 0) intents.Add(new OrderIntent { Kind = IntentKind.Modify, Options = options });
            }

            // Yes / no only when nothing more specific was said.
            if (!intents.Any(intent => intent.Kind == IntentKind.Order || intent.Kind == IntentKind.Remove))
            {
                var done = Regex.IsMatch(text, @"\b(?:that'?s (?:all|it|everything)|nothing else|i'?m (?:good|fine|all set|ok|okay)|all set|that'?ll (?:be|do)(?: it| all)?|no thanks|no thank you|that will (?:be|do))\b");
                var deny = !done && Regex.IsMatch(text, @"^(?:no|nope|nah|not yet|wait|hold on|hang on|actually|sorry no)\b|\b(?:that'?s (?:not|wrong)|not right|change (?:it|my order)|i changed my mind)\b");
                var confirm = done || !deny && Regex.IsMatch(text, @"^(?:yes|yeah|yep|yup|sure|ok|okay|alright|correct|exactly|perfect|sounds (?:good|great|perfect)|that'?s (?:right|correct|it|all|perfect|everything)|go ahead|that'?ll be all|that will be all|that'?s all|nothing else|all good)\b|\b(?:that'?s (?:right|correct|everything|all)|please go ahead|that'?ll be (?:it|all)|confirm)\b");
                if (deny) intents.Add(new OrderIntent { Kind = IntentKind.Deny });
                else if (confirm) intents.Add(new OrderIntent { Kind = IntentKind.Confirm });
            }
            if (intents.Count == 0) intents.Add(new OrderIntent { Kind = IntentKind.Unknown });
            return intents;
        }

        /// A mention is not authorization: ignore negated tenders and questions, and clarify ambiguity.
        private static IntentKind PaymentMethod(string raw)
        {
            var methods = new HashSet<IntentKind>();
            var clauses = Regex.Split((raw ?? "").ToLowerInvariant().Replace('’', '\''),
                @"[,;.!?]|\b(?:but|instead)\b|(?=\bi(?:'ll| will)\b)");
            foreach (var clause in clauses)
            {
                var text = Normalize(clause);
                if (Regex.IsMatch(text, @"\b(?:not|no|never|can't|cannot|don't|won't|couldn't|do not|will not|unable|without)\b")
                    || Regex.IsMatch(text, @"^(?:do|does|is|are)\b|^(?:can|could|would) you\b")) continue;
                var card = Regex.IsMatch(text, @"\b(?:card|credit|debit|tap|contactless|apple pay|pay by phone)\b");
                var cash = Regex.IsMatch(text, @"\bcash\b");
                if (card) methods.Add(IntentKind.PayCard);
                if (cash) methods.Add(IntentKind.PayCash);
            }
            return methods.Count == 1 ? methods.First() : IntentKind.Unknown;
        }

        private static int QuantityBefore(string before)
        {
            var tail = Regex.Match(before, @"(?:\b(a couple of|couple of|a pair of|both|[0-9]|one|two|three|four|five|six|a|an)\b)(?: \w+){0,3} $");
            if (!tail.Success) return 1;
            var word = tail.Groups[1].Value;
            if (int.TryParse(word, out var number)) return Math.Max(1, Math.Min(number, 9));
            return Numbers.TryGetValue(word, out var value) ? value : 1;
        }

        /// Split the text between two items at the last "and" or comma; returns an index in that gap.
        private static int Boundary(string text, int from, int to, bool forNext)
        {
            var gap = text.Substring(from, Math.Max(0, to - from));
            var split = Regex.Matches(gap, @"\b(?:and|also|plus|then)\b|,").Cast<Match>().LastOrDefault();
            if (split == null) return forNext ? to : from + gap.Length; // no separator: the previous item keeps "with oat milk" style tails
            return from + (forNext ? split.Index + split.Length : split.Index);
        }

        public static List<KeyValuePair<OptionGroup, string>> Options(string scope, MenuItem item)
        {
            var found = new List<KeyValuePair<OptionGroup, string>>();
            var claimed = new bool[scope.Length];
            foreach (var (pattern, group, choice) in OptionPhrases)
            {
                if (found.Any(entry => entry.Key == group)) continue;
                if (item != null && !item.Has(group)) continue;
                foreach (Match match in Regex.Matches(scope, @"\b(?:" + pattern + @")\b"))
                {
                    if (Enumerable.Range(match.Index, match.Length).Any(index => claimed[index])) continue;
                    // "hot" next to "spicy"/"sauce" is about spice, not temperature.
                    if (group == OptionGroup.Temperature && choice == "hot" && Regex.IsMatch(scope, @"\bhot (?:sauce|chilli)|spicy hot\b")) continue;
                    for (var index = match.Index; index < match.Index + match.Length; index++) claimed[index] = true;
                    found.Add(new KeyValuePair<OptionGroup, string>(group, choice));
                    break;
                }
            }
            return found;
        }
    }
}
