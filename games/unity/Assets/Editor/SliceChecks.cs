using System;
using System.Linq;
using ContextSpeak.Slice;

namespace ContextSpeak.Editor
{
    /// Logic checks for the Forest Café slice. They use no UnityEngine types, so they also run
    /// outside the Editor (see tools/unity/slice-checks.sh) as well as in CafeBuild.Verify.
    public static class SliceChecks
    {
        public static int Passed;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Slice regression: " + message);
            Passed++;
        }
        private static OrderIntent First(string text, IntentKind kind) => OrderIntentParser.Parse(text).FirstOrDefault(intent => intent.Kind == kind);
        private static bool Has(OrderIntent intent, string choice) => intent != null && intent.Options.Any(option => option.Value == choice);

        public static void Verify()
        {
            Passed = 0;
            Regressions();
            Bands();
            Lessons();
            Trail();
            Parser();
            Cart();
            Waiter();
            Coach();
            Fishing();
            Guide();
            Quests();
            Save();
            Audio();
        }

        private static void Trail()
        {
            var save = new SliceSave();
            var trail = SpeakingTrail.Create(save);
            Check(string.Join(",", trail.Stops.Select(place => place.Id)) == "home,market,cafe", "Beginner trails start with familiar home, market and café situations.");
            save.Band = IeltsBand.Band9;
            Check(string.Join(",", SpeakingTrail.Create(save).Stops.Select(place => place.Id)) == "market,fishing,mall", "Expert trails practise decisions, environmental trade-offs and consumer choices.");
            save.Band = IeltsBand.Band3;
            save.FocusVocabulary = "ticket, queue";
            trail = SpeakingTrail.Create(save);
            Check(trail.Stops[0].Id == "park" && trail.Stops.Select(place => place.Id).Distinct().Count() == 3, "Focus vocabulary prioritises a real, relevant NPC without duplicate destinations.");
            save.Lessons.Add(new SceneLesson { SceneId = "park", Band = IeltsBand.Band9, Step = 3 });
            Check(trail.Completed(save) == 0 && trail.Next(save).Id == "park", "Completion at another target cannot skip a beginner speaking task.");
            save.Lessons.Add(new SceneLesson { SceneId = "park", Band = IeltsBand.Band3, Step = 2 });
            Check(trail.Completed(save) == 0, "A partly completed conversation stays on the trail.");
            save.Lessons.Last().Step = 3;
            Check(trail.Completed(save) == 1 && trail.Next(save).Id == "home", "Passing a conversation points to the next uncompleted NPC.");
            save.Lessons.Add(new SceneLesson { SceneId = "home", Band = save.Band, Step = 3 });
            save.Lessons.Add(new SceneLesson { SceneId = "market", Band = save.Band, Step = 3 });
            Check(trail.Completed(save) == 3 && trail.Next(save) == null, "A completed trail ends instead of looping or inventing another stop.");
            Check(SpeakingTrail.Focus(null) == "" && SpeakingTrail.Focus(" Ticket, TICKET ; queue ") == "ticket, queue", "Saved focus is bounded, normalised and deduplicated.");
            Check(SpeakingTrail.Focus(new string('x', 250)).Length <= 120, "Oversized focus input cannot grow the save or UI indefinitely.");
            Check(SpeakingTrail.Focus(string.Join(";", Enumerable.Range(0, 8).Select(index => new string((char)('a' + index), 14)))).Length <= 120, "Normalising separators must not expand focus past its storage limit.");
            Check(SliceSave.Sanitize(new SliceSave { FocusVocabulary = null }).FocusVocabulary == "", "Old saves without vocabulary focus are supported.");
            save.FocusVocabulary = "";
            save.PracticeNeed = PracticeNeed.Travel;
            Check(string.Join(",", SpeakingTrail.Create(save).Stops.Select(place => place.Id)) == "park,fishing,japan", "Travel needs compose relevant scenes at the chosen speaking target.");
            Check(SliceSave.Sanitize(new SliceSave { PracticeNeed = (PracticeNeed)99 }).PracticeNeed == PracticeNeed.Everyday, "Unknown saved needs fall back to everyday practice.");
        }

        private static void Regressions()
        {
            var failures = new System.Collections.Generic.List<string>();
            void Expect(bool condition, string message)
            {
                if (!condition) failures.Add(message);
                else Passed++;
            }
            bool Payment(string text) => OrderIntentParser.Parse(text).Any(intent => intent.Kind == IntentKind.PayCard || intent.Kind == IntentKind.PayCash);
            Expect(!Payment("I don't have any cash"), "Mentioning unavailable cash must not authorize payment.");
            Expect(!Payment("Do you take cash?"), "A payment question must not authorize a charge.");
            Expect(First("I can't pay by card, I'll use cash", IntentKind.PayCash) != null && First("I can't pay by card, I'll use cash", IntentKind.PayCard) == null, "Use the affirmative tender, not the declined alternative.");
            Expect(First("Card", IntentKind.PayCard) != null, "A bare card reply answers the payment question.");
            Expect(!Payment("Cash or card?"), "Ambiguous payment methods need clarification.");
            Expect(!SpeakingCoach.PracticePassed("I don't want a latte", "Can I have a latte?", IeltsBand.Band6), "Opposite intent must not pass a corrected order.");
            Expect(!SpeakingCoach.PracticePassed("Could I have three lattes?", "Could I have two lattes?", IeltsBand.Band7), "Practice must retain the requested quantity.");
            Expect(!SpeakingCoach.PracticePassed("Could I have a hot latte?", "Could I have an iced latte?", IeltsBand.Band7), "Practice must retain the drink options.");
            Expect(!SpeakingCoach.PracticePassed("Could I have an latte?", "Could I have a latte?", IeltsBand.Band7), "An uncorrected article must not pass a correction.");
            var queue = new KitchenQueue();
            var cart = new OrderCart();
            cart.Add("croissant");
            var ticket = queue.Add("saved-order", cart);
            queue.Tick(100);
            Expect(queue.Tick(0) == ticket, "An already-ready saved ticket must resume delivery.");
            var waiter = new WaiterBrain();
            Expect(waiter.Respond("Could I see the menu?", new OrderCart(), new Wallet(), 0).Progress, "An understood menu request is successful communication.");
            Expect(waiter.Respond("That's all, thanks", cart, new Wallet(), 0).Progress, "Order readback must not report a failed answer.");
            cart.Confirm();
            waiter.Stage = WaiterStage.Paying;
            waiter.Respond("Please remove the croissant", cart, new Wallet { Card = 0, Cash = 0 }, 0);
            Expect(cart.Empty && cart.Editable && waiter.Stage != WaiterStage.Paying, "An unpaid confirmed order remains editable after a declined payment.");
            var save = new SliceSave();
            var proposal = new WaiterProposal(save, "A croissant, please");
            Expect(save.Cart.Empty, "A proposed order must not mutate the live cart.");
            Expect(proposal.Commit(save) && !proposal.Commit(save) && save.Cart.Lines.Count == 1, "A proposal commits at most once.");
            var stale = new WaiterProposal(save, "A brownie, please");
            save.Cart.Add("croissant");
            Expect(!stale.Commit(save) && !stale.Commit(new SliceSave()), "Changed carts and replacement saves invalidate a proposal.");
            cart = new OrderCart();
            cart.Add("croissant", 9);
            cart.Confirm();
            new Wallet().TryPay(cart, "card", 0, out var receipt, out _);
            cart.Status = OrderStatus.Delivered;
            cart.ConsumeOne();
            Expect(cart.Lines[0].Quantity == 8 && receipt.Lines[0].StartsWith("9"), "Eating persists the remaining count without altering the receipt.");
            for (var i = 0; i < 8; i++) Expect(cart.ConsumeOne() != null, "All nine paid servings remain consumable, including beyond six visible props.");
            Expect(cart.Status == OrderStatus.Finished && cart.ConsumeOne() == null, "Only the final serving finishes an order.");
            if (failures.Count > 0) throw new Exception("Slice regressions:\n" + string.Join("\n", failures));
        }

        private static void Bands()
        {
            Check(BandProfile.All.Length == 7, "Seven distinct training bands from 3 to 9.");
            Check(BandProfile.For((IeltsBand)3).SpeechRate < BandProfile.For(IeltsBand.Band5).SpeechRate, "A new learner must not receive the old intermediate profile.");
            Check(BandProfile.For(IeltsBand.Band5).SpeechRate < BandProfile.For(IeltsBand.Band8).SpeechRate, "Lower bands speak more slowly.");
            Check(BandProfile.Adjust(IeltsBand.Band6, 0.9, 8) == IeltsBand.Band7 && BandProfile.Adjust(IeltsBand.Band6, 0.9, 3) == IeltsBand.Band6, "Bands move up only after enough strong turns.");
            Check(BandProfile.Adjust((IeltsBand)3, 0.2, 10) == (IeltsBand)3 && BandProfile.Adjust((IeltsBand)9, 0.95, 10) == (IeltsBand)9, "Bands stay within 3–9.");
            Check(BandProfile.Adjust(IeltsBand.Band5, 0.2, 10) == (IeltsBand)4 && BandProfile.Adjust(IeltsBand.Band8, 0.95, 10) == (IeltsBand)9, "The old boundary bands are no longer clamped.");
            Check(SliceSave.Sanitize(new SliceSave { Band = (IeltsBand)100 }).Band == (IeltsBand)3, "An invalid saved band falls back to beginner.");
            Check(SliceSave.Sanitize(new SliceSave { Band = IeltsBand.Band7 }).Band == IeltsBand.Band7, "Existing saved numeric levels survive.");
            var greeting = new WaiterBrain { Band = (IeltsBand)3 }.Greeting();
            Check(greeting.Length < 110 && !greeting.Contains("tempt"), "Beginner NPC speech is short and concrete, not the expert fallback.");
        }

        private static void Parser()
        {
            var latte = First("Could I get an iced latte with oat milk?", IntentKind.Order);
            Check(latte != null && latte.Item == "latte" && Has(latte, "iced") && Has(latte, "oat milk"), "Iced latte with oat milk is understood.");
            var modify = First("Can I have that without sugar?", IntentKind.Modify);
            Check(modify != null && Has(modify, "no sugar"), "Without sugar modifies the previous item.");
            Check(First("What would you recommend?", IntentKind.AskRecommendation) != null, "Recommendation request.");
            Check(First("Could we split the bill?", IntentKind.SplitBill) != null, "Split the bill.");
            var dairy = First("Do you have anything dairy-free?", IntentKind.AskDietary);
            Check(dairy != null && dairy.Detail == "dairy free" && First("Do you have anything dairy-free?", IntentKind.Modify) == null, "Dairy-free is a question, not an order change.");
            var two = OrderIntentParser.Parse("Two large cappuccinos and a croissant, please").Where(intent => intent.Kind == IntentKind.Order).ToArray();
            Check(two.Length == 2 && two[0].Item == "cappuccino" && two[0].Quantity == 2 && Has(two[0], "large") && two[1].Item == "croissant" && two[1].Quantity == 1, "Two items with quantity and size in one sentence.");
            var mixed = OrderIntentParser.Parse("A latte without sugar and a large orange juice with no ice").Where(intent => intent.Kind == IntentKind.Order).ToArray();
            Check(mixed.Length == 2 && Has(mixed[0], "no sugar") && !Has(mixed[0], "large") && Has(mixed[1], "large") && Has(mixed[1], "no ice"), "Options attach to the right item.");
            Check(First("A latte without sugar and a croissant", IntentKind.Remove) == null, "Without sugar does not cancel the next item.");
            Check(First("Actually, I don't want the croissant", IntentKind.Remove)?.Item == "croissant", "Removing an item.");
            Check(First("No, that's all, thanks", IntentKind.Confirm) != null && First("No, that's all, thanks", IntentKind.Deny) == null, "'No, that's all' finishes the order.");
            Check(First("Wait, that's not right", IntentKind.Deny) != null, "Correcting the waiter.");
            Check(First("I'll pay by card", IntentKind.PayCard) != null && First("Cash, please", IntentKind.PayCash) != null, "Payment methods.");
            var curry = First("Could I get the curry, not too spicy?", IntentKind.Order);
            Check(curry != null && curry.Item == "curry" && Has(curry, "mild"), "Spice level for meals.");
            Check(First("What's in the forest mocha?", IntentKind.AskIngredients)?.Item == "forest-mocha" && First("What's in the forest mocha?", IntentKind.Order) == null, "Ingredient questions do not order.");
            Check(First("Same as last time, please", IntentKind.Order)?.Detail == "usual", "Ordering the usual.");
            Check(OrderIntentParser.Parse("blah blah purple").Single().Kind == IntentKind.Unknown, "Nonsense is unknown.");
        }

        private static void Lessons()
        {
            var visitor = new SceneLesson { SceneId = "park:Kai", Band = IeltsBand.Band3 };
            Check(visitor.Prompt == LifeCurriculum.Create("park", IeltsBand.Band3).Prompts[0],
                "Named town residents use the correct contextual lesson, not the fallback topic.");
            var reply = visitor.Propose("I like the carousel.");
            Check(reply.Accepted, "A resident's independent lesson accepts a relevant beginner answer.");
            var neighbour = new SceneLesson { SceneId = "park:Jay", Band = IeltsBand.Band3 };
            var stored = new SliceSave();
            stored.Lessons.Add(visitor);
            stored.Lessons.Add(neighbour);
            visitor.Accept(reply);
            Check(SliceSave.Sanitize(stored).Lessons.Single(lesson => lesson.SceneId == "park:Jay").Step == 0
                && stored.Lessons.Single(lesson => lesson.SceneId == "park:Kai").Step == 1,
                "Neighbouring NPCs keep independent saved steps, despite sharing a curriculum topic.");
            var beginner = LifeCurriculum.Create("market", IeltsBand.Band3);
            var expert = LifeCurriculum.Create("market", IeltsBand.Band9);
            Check(beginner.Prompts[0].Length < expert.Prompts[0].Length && beginner.Vocabulary.Contains("fresh"), "Market tasks are generated for the selected learner target.");
            Check(expert.Goal.Contains("qualify") && expert.Vocabulary.Contains("supply chain"), "Expert tasks practise nuanced reasoning and useful collocations.");
            var session = new SceneLesson { SceneId = "market", Band = IeltsBand.Band3 };
            var unrelated = session.Propose("My spaceship is purple.");
            Check(!unrelated.Accepted && !session.Accept(unrelated) && session.Step == 0, "Off-topic answers do not advance a scene.");
            var first = session.Propose("Some apples, please.");
            Check(first.Accepted && session.Step == 0, "Assessment previews do not advance before correction has passed.");
            Check(session.Accept(first) && !session.Accept(first) && session.Step == 1, "One accepted answer advances once.");
            Check(session.Accept(session.Propose("They are fresh.")), "A natural relevant reason passes without copying a model sentence.");
            Check(session.Accept(session.Propose("I like carrots too.")) && session.Complete, "Three relevant turns finish a lesson.");
            Check(!session.Accept(session.Propose("Apples please")) && session.Step == 3, "Completed lessons cannot advance or reward repeatedly.");
            var cafe = new SceneLesson { SceneId = "cafe", Band = IeltsBand.Band3 };
            Check(cafe.Accept(cafe.Propose("Water, please.")), "A valid drink outside the suggested vocabulary is accepted.");
            Check(!cafe.Propose("Coffee, please.").Accepted && cafe.Step == 1, "A drink name cannot answer the temperature question.");
            Check(cafe.Accept(cafe.Propose("Hot, please.")), "Natural temperature replies answer the current question.");
            Check(cafe.Accept(cafe.Propose("No milk, thanks.")), "Milk preference answers the third question.");
            var fruit = new SceneLesson { SceneId = "market", Band = IeltsBand.Band3 };
            Check(fruit.Accept(fruit.Propose("Bananas, please.")), "Optional study vocabulary is not an answer whitelist.");
            Check(!fruit.Propose("Bananas, please.").Accepted, "Repeating a purchase request does not answer why.");
            var hobby = new SceneLesson { SceneId = "fishing", Band = IeltsBand.Band3 };
            Check(hobby.Accept(hobby.Propose("Yes, very much.")), "A first-question yes/no answer is accepted when appropriate.");
            var debate = new SceneLesson { SceneId = "market", Band = IeltsBand.Band9 };
            Check(!debate.Propose("Apples.").Accepted && debate.Step == 0, "Expert discussion needs developed reasoning, not a topic word.");
            Check(debate.Accept(debate.Propose("Local food may be fresher, but imported produce can be cheaper because the climate affects supply.")), "A qualified comparison can meet the expert communicative goal.");
            var reviewed = new SceneLesson { SceneId = "market", Band = IeltsBand.Band6 };
            Check(reviewed.Accept(reviewed.Propose("I recommend broccoli. It cooks quickly.", true, true)), "A semantic assessment accepts reasoning expressed without prescribed vocabulary or connectors.");
            Check(!reviewed.Propose("My spaceship could deliver pizza, but I love apples.", false, false).Accepted, "An AI relevance rejection cannot be overridden by keyword overlap.");
            foreach (var place in LifeCurriculum.Places)
                foreach (var profile in BandProfile.All)
                {
                    var lesson = LifeCurriculum.Create(place.Id, profile.Band);
                    Check(lesson.Prompts.Length == 3 && lesson.Prompts.Distinct().Count() == 3 && lesson.Vocabulary.Length >= 3, "Each place and target has three distinct tasks with vocabulary support.");
                }
        }

        private static void Cart()
        {
            var cart = new OrderCart();
            var latte = cart.Add("latte");
            Check(latte.Missing().SequenceEqual(new[] { OptionGroup.Temperature, OptionGroup.Size }), "Temperature and size must be asked.");
            latte.Set(OptionGroup.Temperature, "iced");
            latte.Set(OptionGroup.Size, "large");
            latte.Set(OptionGroup.Milk, "oat milk");
            Check(latte.UnitPrice == 550 + 100 + 60, "Large and oat milk surcharges.");
            Check(latte.Describe() == "large iced café latte with oat milk", "Readable order line: " + latte.Describe());
            cart.Add("croissant", 2);
            Check(cart.Total == 710 + 900 && cart.Ready, "Total and readiness.");
            Check(cart.Split(3).Sum() == cart.Total && cart.Split(3)[0] >= cart.Split(3)[1], "Split shares always add up.");
            var wallet = new Wallet { Cash = 1000, Card = 0 };
            Check(!wallet.TryPay(cart, "card", 0, out _, out _), "Cannot pay before confirming.");
            Check(cart.Confirm() && !cart.Editable, "Confirming locks the cart.");
            Check(!wallet.TryPay(cart, "card", 0, out _, out var declined) && declined.Contains("declined"), "An empty card is declined.");
            Check(!wallet.TryPay(cart, "cash", 0, out _, out _) && wallet.Cash == 1000, "Not enough cash leaves money untouched.");
            cart.Payers = 2;
            Check(wallet.TryPay(cart, "cash", 600, out var receipt, out _) && wallet.Cash == 1000 - cart.Split(2)[0] && receipt.Paid == cart.Split(2)[0], "Split bill pays only your share.");
            Check(!wallet.TryPay(cart, "cash", 600, out _, out _), "Payment never happens twice.");
            var hot = new OrderCart().Add("latte");
            hot.Set(OptionGroup.Ice, "no ice");
            hot.Set(OptionGroup.Temperature, "hot");
            Check(hot.Choice(OptionGroup.Ice) == null, "Hot drinks drop the ice setting.");
        }

        private static void Waiter()
        {
            foreach (var band in new[] { IeltsBand.Band5, IeltsBand.Band6, IeltsBand.Band7, IeltsBand.Band8 })
            {
                var brain = new WaiterBrain { Band = band };
                var cart = new OrderCart();
                var wallet = new Wallet();
                var greeting = brain.Greeting();
                Check(greeting.Length > 0, band + " greets.");
                var turn = brain.Respond("Could I get a latte with oat milk?", cart, wallet, 900);
                Check(turn.Progress && cart.Lines.Count == 1 && brain.Stage == WaiterStage.Detail && turn.Line.Contains("hot or iced") || turn.Line.Contains("Hot or iced"), band + " asks hot or iced: " + turn.Line);
                turn = brain.Respond("Iced, please.", cart, wallet, 901);
                Check(brain.Stage == WaiterStage.Detail && turn.Line.ToLowerInvariant().Contains("size"), band + " then asks size: " + turn.Line);
                turn = brain.Respond("A medium one.", cart, wallet, 902);
                Check(brain.Stage == WaiterStage.Taking && cart.Ready, band + " asks if there is anything else: " + turn.Line);
                turn = brain.Respond("What would you recommend?", cart, wallet, 903);
                Check(turn.Objectives.Contains("ask_recommendation") && turn.HighlightItem != "", band + " recommends.");
                turn = brain.Respond("No, that's all, thanks.", cart, wallet, 904);
                Check(brain.Stage == WaiterStage.Confirming && turn.Line.Contains(CafeMenu.Money(cart.Total)), band + " reads the order back: " + turn.Line);
                turn = brain.Respond("Yes, that's right.", cart, wallet, 905);
                Check(brain.Stage == WaiterStage.Paying && cart.Status == OrderStatus.Confirmed, band + " asks for payment.");
                var before = wallet.Card;
                turn = brain.Respond("By card, please.", cart, wallet, 906);
                Check((turn.Actions & WaiterAction.SendToKitchen) != 0 && wallet.Card == before - cart.Total && brain.Stage == WaiterStage.Preparing, band + " charges once and sends to the kitchen.");
                Check(brain.LastOrder.Length == 1, band + " remembers the usual.");
                turn = brain.Deliver(cart);
                Check(cart.Status == OrderStatus.Delivered && turn.Objectives.Contains("receive"), band + " delivers.");
            }
            var c1 = new WaiterBrain { Band = IeltsBand.Band7 };
            var recommend = c1.Respond("What would you recommend?", new OrderCart(), new Wallet(), 0).Line;
            Check(recommend.Contains("right up your alley"), "C1-level idiom from the spec appears at band 7.");
            var b5 = new WaiterBrain { Band = IeltsBand.Band5 };
            Check(b5.Greeting() == "Hi! Welcome to Forest Café. What would you like to drink today?" || b5.Greeting().Length < 70, "Band 5 greeting is short and clear.");
            var regular = new WaiterBrain { Band = IeltsBand.Band6, LastOrder = new[] { "latte|1|Temperature:iced;Size:medium" } };
            Check(regular.Greeting().Contains("stick with your usual"), "Band 6 regulars get the spec's multi-part question.");
            var usualCart = new OrderCart();
            regular.Respond("Same as last time, please", usualCart, new Wallet(), 0);
            Check(usualCart.Lines.Count == 1 && usualCart.Ready, "The usual is restored with its options.");
            var dietary = new WaiterBrain { Band = IeltsBand.Band6 }.Respond("Do you have anything dairy-free?", new OrderCart(), new Wallet(), 0);
            Check(dietary.Line.Contains("oat") && dietary.Objectives.Contains("ask_dietary"), "Dairy-free answer mentions milk swaps.");
            var split = new WaiterBrain { Band = IeltsBand.Band6 };
            var splitCart = new OrderCart();
            splitCart.Add("americano").Set(OptionGroup.Temperature, "hot");
            splitCart.Lines[0].Set(OptionGroup.Size, "small");
            split.Respond("Could we split the bill?", splitCart, new Wallet(), 0);
            Check(splitCart.Payers == 2, "Split bill sets two payers.");
            var lost = new WaiterBrain().Respond("purple elephants", new OrderCart(), new Wallet(), 0);
            Check(lost.Problem == "not_understood" && lost.ModelAnswer != "", "Unclear replies stay on task and offer a model answer.");
            var paidBrain = new WaiterBrain { Stage = WaiterStage.Preparing };
            var paidCart = new OrderCart { Status = OrderStatus.Paid };
            paidBrain.Respond("And a brownie please", paidCart, new Wallet(), 0);
            Check(paidCart.Lines.Count == 0, "Paid orders cannot be silently changed.");
        }

        private static void Coach()
        {
            var a = SpeakingCoach.Review("Can I has a latte?", IeltsBand.Band6, true, true);
            Check(a.SignificantError && a.Corrected == "Can I have a latte?", "Can I has → Can I have.");
            var b = SpeakingCoach.Review("I would like to a latte, please.", IeltsBand.Band6, true, true);
            Check(b.Corrected == "I would like a latte, please.", "Would like to + noun.");
            var c = SpeakingCoach.Review("Could I get two latte?", IeltsBand.Band6, true, true);
            Check(!c.SignificantError && c.Corrected == "Could I get two lattes?", "Plural is a minor fix: " + c.Corrected);
            var d = SpeakingCoach.Review("Give me a latte", IeltsBand.Band7, true, true);
            Check(d.PragmaticTip && d.Corrected == "Could I get a latte, please?", "Abrupt requests get a politeness tip: " + d.Corrected);
            var e = SpeakingCoach.Review("Give me a latte", IeltsBand.Band5, true, true);
            Check(!e.PragmaticTip, "Band 5 is not pushed on register.");
            var good = SpeakingCoach.Review("Could I get an iced latte with oat milk, please?", IeltsBand.Band6, true, true);
            Check(good.Corrected == "" && !good.SignificantError && good.GoodLanguage.Contains("could i get"), "A natural answer is praised, not rewritten.");
            Check(good.Criteria.Single(note => note.Criterion == SpeakingCriterion.Pronunciation).Level == CriterionLevel.NotAssessed, "Pronunciation is never guessed from a transcript.");
            var pron = SpeakingCoach.Review("Could I get a latte?", IeltsBand.Band6, true, true, 0.85);
            Check(pron.Criteria.Single(note => note.Criterion == SpeakingCriterion.Pronunciation).Level == CriterionLevel.AboveTarget, "A pronunciation provider score is used when present.");
            var alt = SpeakingCoach.Review("I'll go with the flat white, thanks.", IeltsBand.Band7, true, true);
            Check(alt.Corrected == "" && !alt.SignificantError, "Different natural wording is accepted.");
            Check(SpeakingCoach.Decide(Strictness.Practice, a, 0) == Gate.OfferRetry, "Practice: retry is optional.");
            Check(SpeakingCoach.Decide(Strictness.Guided, a, 0) == Gate.RequireRetry && SpeakingCoach.Decide(Strictness.Guided, a, 2) == Gate.ShowModelAndContinue, "Guided: finite retries.");
            Check(SpeakingCoach.Decide(Strictness.Challenge, c, 0) == Gate.RequireRetry && SpeakingCoach.Decide(Strictness.Challenge, c, 3) == Gate.ShowModelAndContinue, "Challenge: even minor errors, then a model answer.");
            Check(SpeakingCoach.Decide(Strictness.Guided, good, 0) == Gate.Continue, "Correct answers continue.");
            Check(SpeakingCoach.PracticePassed("Can I have a latte?", "Can I have a latte?", IeltsBand.Band6), "Repeating the correction passes.");
            Check(SpeakingCoach.PracticePassed("Could I have a latte, please?", "Can I have a latte?", IeltsBand.Band6), "A natural rewording passes.");
            Check(!SpeakingCoach.PracticePassed("Can I has a latte?", "Can I have a latte?", IeltsBand.Band6), "Repeating the error fails.");
            Check(!SpeakingCoach.PracticePassed("Yes", "Can I have a latte?", IeltsBand.Band6), "Dropping the content fails.");
            Check(Glossary.In("It might be right up your alley, a little on the pricey side").Length == 2 && Glossary.Entries.All(entry => entry.Chinese != ""), "Glossary finds idioms and has Chinese glosses.");
            var ui = new WaiterBrain { Band = IeltsBand.Band6 };
            var uiCart = new OrderCart();
            var uiLine = uiCart.Add("latte");
            Check(ui.MenuChanged(uiCart, uiLine).Line.Contains("hot or iced") && ui.Stage == WaiterStage.Detail, "Menu selections get the same follow-up questions.");
            Check(SpeakingCoach.Hint("Could I get a latte, please?", 1).Contains("Could I") && SpeakingCoach.Hint("Could I get a latte, please?", 3).Contains("latte"), "Hints become more specific.");
        }

        private static void Fishing()
        {
            var caught = 0;
            var escaped = 0;
            for (var seed = 0; seed < 40; seed++)
            {
                var session = new FishingSession(seed);
                Check(session.Cast(), "Can cast.");
                var guard = 0;
                while (session.State != FishingState.Caught && session.State != FishingState.Escaped && guard++ < 4000)
                {
                    if (session.State == FishingState.Bite) session.Strike();
                    // A skilled player keeps tension in the safe band.
                    session.Tick(0.02f, session.Tension < 0.6f, Weather.Clear, 15 * 60, 1);
                }
                if (session.State == FishingState.Caught) caught++; else escaped++;
                if (session.State == FishingState.Caught) Check(FishingSession.Find(session.Last.Species) != null && session.Last.Length > 0, "A catch has species and size.");
            }
            Check(caught > 25, "Good timing catches most fish: " + caught + "/40");
            var greedy = new FishingSession(3);
            greedy.Cast();
            for (var step = 0; step < 4000 && greedy.State != FishingState.Caught && greedy.State != FishingState.Escaped; step++)
            {
                if (greedy.State == FishingState.Bite) greedy.Strike();
                greedy.Tick(0.02f, true, Weather.Clear, 600, 1);
            }
            Check(greedy.State == FishingState.Escaped && greedy.Message.Contains("Snap"), "Reeling non-stop snaps the line.");
            var early = new FishingSession(1);
            early.Cast();
            for (var step = 0; step < 2000 && early.State != FishingState.Nibble && early.State != FishingState.Bite; step++) early.Tick(0.02f, false, Weather.Clear, 600, 1);
            if (early.State == FishingState.Nibble) { early.Strike(); Check(early.State == FishingState.Escaped, "Striking a nibble scares the fish."); }
            var rain = new FishingSession(9);
            Check(Enumerable.Range(0, 400).Any(_ => rain.Choose(Weather.Rain, 22 * 60, 5).Id == "golden-carp"), "Legendary carp appears in rain.");
            var dry = new FishingSession(9);
            Check(Enumerable.Range(0, 400).All(_ => dry.Choose(Weather.Clear, 12 * 60, 5).Id != "golden-carp"), "Never in dry weather.");
            Check(FishingSession.Level(0) == 1 && FishingSession.Level(40) == 2 && FishingSession.Level(160) == 3, "Fishing levels.");
        }

        private static void Guide()
        {
            var hazel = new FishingGuide { Band = IeltsBand.Band6 };
            Check(hazel.Respond("What kind of fish are in this pond?", Weather.Clear, 600, 0).Objectives.Contains("fish_types"), "Fish types.");
            Check(hazel.Respond("What's the best bait to use?", Weather.Clear, 600, 0).Objectives.Contains("fish_bait"), "Bait.");
            Check(hazel.Respond("Have you caught anything today?", Weather.Clear, 600, 0).Progress, "Small talk.");
            Check(hazel.Respond("How long have you been fishing?", Weather.Clear, 600, 0).Line.Contains("twenty"), "Her history.");
            Check(hazel.Respond("That one nearly got away!", Weather.Clear, 600, 0).Line.Length > 0, "Reacts to escapes.");
            Check(hazel.Respond("Tell me about stocks", Weather.Clear, 600, 0).Problem == "off_topic", "Stays on topic.");
        }

        private static void Quests()
        {
            var done = new System.Collections.Generic.List<string>();
            Check(QuestBook.Active(done).Id == "cafe-first-order", "First quest is ordering.");
            var earned = new System.Collections.Generic.HashSet<string> { "order_item", "confirm", "pay", "receive" };
            Check(QuestBook.Completed(done, earned).Single().Id == "cafe-first-order", "All objectives complete the quest.");
            done.Add("cafe-first-order");
            Check(QuestBook.Available(QuestBook.Quests[1], done), "Prerequisites unlock the next quest.");
            Check(QuestBook.Quests.Select(quest => quest.Id).Distinct().Count() == QuestBook.Quests.Length && QuestBook.Quests.All(quest => quest.Objectives.Length == quest.ObjectiveLabels.Length), "Unique quest IDs and labelled objectives.");
        }

        private static void Audio()
        {
            const int rate = 16000;
            var tone = new float[rate];
            for (var i = 0; i < tone.Length; i++) tone[i] = (float)Math.Sin(2 * Math.PI * 440 * i / rate) * 0.5f;
            int Crossings(float[] samples) { var count = 0; for (var i = 1; i < samples.Length; i++) if (samples[i - 1] < 0 && samples[i] >= 0) count++; return count; }
            var slow = AudioStretch.Stretch(tone, rate, 0.8f);
            Check(Math.Abs(slow.Length - rate / 0.8) < rate * 0.06, "Slower playback lasts longer: " + slow.Length);
            var pitch = Crossings(slow) / (slow.Length / (double)rate);
            Check(Math.Abs(pitch - 440) < 15, "Slower playback keeps the pitch: " + pitch);
            var fast = AudioStretch.Stretch(tone, rate, 1.1f);
            Check(fast.Length < tone.Length && Math.Abs(Crossings(fast) / (fast.Length / (double)rate) - 440) < 15, "Faster speech keeps the pitch.");
        }

        private static void Save()
        {
            var save = new SliceSave();
            save.Give("bluegill", 2);
            save.Give("bluegill");
            Check(save.Count("bluegill") == 3, "Inventory counts.");
            save.Give("bluegill", -3);
            Check(save.Count("bluegill") == 0 && save.Inventory.Count == 0, "Inventory removal.");
            Check(SliceSave.Sanitize(null).Version == 1 && SliceSave.Sanitize(new SliceSave { Version = 99 }).Wallet.Balance == new Wallet().Balance, "Unknown saves start fresh.");
            var broken = new SliceSave { Cart = new OrderCart() };
            broken.Cart.Lines.Add(new CartLine { Item = "not-real" });
            Check(SliceSave.Sanitize(broken).Cart.Lines.Count == 0, "Removed menu items are dropped from saves.");
            var queue = new KitchenQueue();
            var cart = new OrderCart();
            cart.Add("croissant");
            var ticket = queue.Add("T1", cart);
            Check(ticket.Total >= 8 && queue.Tick(ticket.Total + 1) == ticket, "Kitchen tickets become ready.");
            var clock = new WorldClock { Minute = 23 * 60 + 59 };
            clock.Tick(2, new Random(1));
            Check(clock.Day == 2 && clock.Minute < 5, "Clock rolls over to the next day.");
            var progress = new LearningProgress();
            progress.Record(new TurnRecord { Objective = "order_item", Score = 0.9f }, SpeakingCoach.Review("Could I get a latte?", IeltsBand.Band6, true, true));
            Check(progress.Objectives.Contains("order_item") && progress.Phrases.Contains("could i get"), "Progress records objectives and phrases.");
        }
    }
}
