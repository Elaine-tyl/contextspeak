using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Slice
{
    /// Native UI Toolkit interface for the Forest Café slice: HUD, mini-map, dialogue with corrections,
    /// menu with cart, fishing gauge, inventory, journal (quests + learning dashboard), map and settings.
    public sealed class SliceInterface : MonoBehaviour
    {
        public event Action<string, string> Command;
        public bool Typing { get; private set; }
        public bool AlwaysLook = true;

        // World bounds for the mini-map (metres): x from -22 to 22, z from -16 to 34.
        private const float MinX = -52, MaxX = 52, MinZ = -60, MaxZ = 72;

        private ScrollView history;
        private VisualElement root, dialogue, correction, menu, menuItems, menuDetail, cartRows, fishingPanel, modal, mapDots, glossaryRow, criteriaRow, suggestions, questList, needs;
        private Label place, clock, money, band, questTitle, hint, toast, speaker, role, portrait, line, recognized, status, aiTip, original, corrected, explanation, menuTitle, cartTotal, fishingMessage, fishingLevel;
        private Label trailHint;
        private Button mic, repeat, slower, menuToggle, checkout, translate, historyToggle, hearCorrection, practise, carryOn, useModel;
        private TextField reply;
        private VisualElement tension, tensionFill, progressFill;
        private string questSignature, menuSignature, detailSignature, cartSignature, historySignature, modalSignature, glossarySignature, criteriaSignature, mapSignature;
        private readonly Dictionary<string, Dictionary<OptionGroup, string>> pending = new Dictionary<string, Dictionary<OptionGroup, string>>();
        private SliceView last;
        private bool showHistory;
        private Button soundToggle, createRoom, joinRoom, leaveRoom, useRoomServer;
        private Label friendsStatus, friendsRoster, socialStatus;
        private TextField inviteCode, joinCodeField, roomServerField;
        private string joinCode = "", serverInput = "", shownEndpoint = "";
        private string suggestionSignature;

        private void Awake()
        {
            root = CafeInterface.CreateRoot(gameObject);
            root.style.display = DisplayStyle.Flex; // this slice uses native UI on every platform
            // OS font with CJK fallback so the Chinese glossary renders on desktop builds.
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica Neue", "Arial", "Microsoft YaHei", "PingFang SC", "Hiragino Sans GB", "Noto Sans CJK SC" }, 18);
            if (font != null) root.style.unityFont = font;
            var styles = Resources.Load<StyleSheet>("SliceStyles");
            if (styles != null) root.styleSheets.Add(styles);
            root.AddToClassList("slice");
            BuildHud();
            BuildDialogue();
            BuildMenu();
            BuildFishing();
            modal = Add(root, "slice-modal");
            modal.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ layout helpers
        private static VisualElement Add(VisualElement parent, string name = null, params string[] classes)
        {
            var element = new VisualElement();
            if (name != null) element.name = name;
            foreach (var c in classes) element.AddToClassList(c);
            parent.Add(element);
            return element;
        }
        private static Label Text(VisualElement parent, string text = "", params string[] classes)
        {
            var label = new Label(text) { enableRichText = false };
            foreach (var c in classes) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }
        private Button Action(VisualElement parent, string text, string action, string argument = "", params string[] classes)
        {
            var button = new Button(() => Command?.Invoke(action, argument)) { text = text };
            foreach (var c in classes) button.AddToClassList(c);
            parent.Add(button);
            return button;
        }
        private static void Show(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private void BuildHud()
        {
            var hud = Add(root, "slice-hud", "card");
            place = Text(hud, "", "hud-place");
            clock = Text(hud, "", "hud-muted");
            money = Text(hud, "", "hud-money");
            band = Text(hud, "", "chip", "chip-blue");
            trailHint = Text(hud, "", "hud-muted");
            needs = Add(hud, "needs");
            questTitle = Text(hud, "", "hud-quest");
            questList = Add(hud, "quest-list");
            var row = Add(hud, null, "row");
            Action(row, "Journal · J", "panel", "journal", "ghost");
            Action(row, "Bag · Tab", "panel", "inventory", "ghost");
            Action(row, "Settings · Esc", "panel", "pause", "ghost");
            var social = Add(hud, null, "row", "wrap");
            Action(social, "Friends", "panel", "friends", "ghost");
            soundToggle = Action(social, "Sound on", "toggle-mute", "", "ghost");
            socialStatus = Text(hud, "Solo play", "hud-muted");
            var map = Add(root, "slice-map", "card");
            Text(map, "Forest Café", "map-title");
            mapDots = Add(map, "map-area");
            hint = Text(root, "", "slice-hint");
            toast = Text(root, "", "slice-toast");
            Text(root, "WASD move · Shift sprint · Ctrl/Caps walk · Space jump · C crouch · F talk · E use · M map · J journal · Tab bag · Esc settings", "slice-controls");
        }

        private void BuildDialogue()
        {
            dialogue = Add(root, "slice-dialogue", "card");
            var header = Add(dialogue, null, "row", "dialogue-header");
            portrait = Text(header, "", "portrait");
            var names = Add(header, null, "names");
            speaker = Text(names, "", "speaker");
            role = Text(names, "", "hud-muted");
            var tools = Add(header, null, "row", "push-right");
            translate = Action(tools, "中文 glossary", "translation", "", "ghost");
            historyToggle = new Button(() => { showHistory = !showHistory; if (last != null) Render(last); }) { text = "History" };
            historyToggle.AddToClassList("ghost");
            tools.Add(historyToggle);
            Action(tools, "Close · Esc", "close", "", "ghost");
            line = Text(dialogue, "", "npc-line");
            glossaryRow = Add(dialogue, "glossary", "row", "wrap");
            recognized = Text(dialogue, "", "you-said");
            correction = Add(dialogue, "slice-correction");
            Text(correction, "A more natural way to say it", "correction-title");
            original = Text(correction, "", "original");
            corrected = Text(correction, "", "corrected");
            explanation = Text(correction, "", "explanation");
            criteriaRow = Add(correction, "criteria", "row", "wrap");
            var correctionButtons = Add(correction, null, "row", "wrap");
            hearCorrection = Action(correctionButtons, "Hear it", "hear-correction", "", "secondary");
            practise = Action(correctionButtons, "● Practise again", "practice", "", "speak");
            carryOn = Action(correctionButtons, "Continue", "continue", "", "ghost");
            useModel = Action(correctionButtons, "Use this answer", "use-model", "", "ghost");
            aiTip = Text(dialogue, "", "ai-tip");
            status = Text(dialogue, "", "status");
            suggestions = Add(dialogue, "suggestions", "row", "wrap");
            history = new ScrollView(ScrollViewMode.Vertical) { name = "slice-history" };
            dialogue.Add(history);
            var controls = Add(dialogue, null, "row", "wrap", "dialogue-controls");
            mic = Action(controls, "● Hold to talk", "record", "", "speak", "mic");
            repeat = Action(controls, "Repeat", "repeat", "", "secondary");
            slower = Action(controls, "Slower", "slower", "", "secondary");
            menuToggle = Action(controls, "Menu", "menu", "", "secondary");
            checkout = Action(controls, "Checkout", "checkout", "", "review");
            reply = new TextField("Rehearse by typing (Enter)") { maxLength = 400 };
            reply.AddToClassList("reply");
            reply.RegisterCallback<FocusInEvent>(_ => Typing = true);
            reply.RegisterCallback<FocusOutEvent>(_ => Typing = false);
            reply.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                Command?.Invoke("send", reply.value);
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            dialogue.Add(reply);
            Show(dialogue, false);
        }

        private void BuildMenu()
        {
            menu = Add(root, "slice-menu", "card");
            var header = Add(menu, null, "row");
            menuTitle = Text(header, "FOREST CAFÉ · MENU", "menu-title");
            Action(header, "×", "menu", "", "ghost", "push-right");
            var tabs = Add(menu, "menu-tabs", "row", "wrap");
            foreach (MenuCategory category in Enum.GetValues(typeof(MenuCategory)))
                Action(tabs, category.ToString(), "category", category.ToString(), "tab");
            var body = Add(menu, null, "menu-body");
            var list = new ScrollView(ScrollViewMode.Vertical) { name = "menu-items" };
            body.Add(list);
            menuItems = list;
            menuDetail = Add(body, "menu-detail");
            Text(menu, "YOUR ORDER", "section-title");
            cartRows = Add(menu, "cart-rows");
            cartTotal = Text(menu, "", "cart-total");
            Show(menu, false);
        }

        private void BuildFishing()
        {
            fishingPanel = Add(root, "slice-fishing", "card");
            fishingMessage = Text(fishingPanel, "", "fishing-message");
            Text(fishingPanel, "Line tension", "hud-muted");
            tension = Add(fishingPanel, null, "bar");
            Add(tension, null, "bar-danger");
            tensionFill = Add(tension, null, "bar-fill");
            Text(fishingPanel, "Reel in", "hud-muted");
            var progress = Add(fishingPanel, null, "bar");
            progressFill = Add(progress, null, "bar-fill", "progress");
            fishingLevel = Text(fishingPanel, "", "hud-muted");
            Show(fishingPanel, false);
        }

        // ------------------------------------------------------------------ public API
        public void ClearInput()
        {
            if (reply != null) reply.value = "";
        }

        /// Options picked on the detail card before pressing "Add to order".
        public IEnumerable<KeyValuePair<OptionGroup, string>> PendingChoices(string item)
        {
            if (!pending.TryGetValue(item, out var choices)) return Enumerable.Empty<KeyValuePair<OptionGroup, string>>();
            var copy = choices.ToArray();
            pending.Remove(item);
            return copy;
        }

        public void Render(SliceView view)
        {
            soundToggle.text = GameAudio.Muted || GameAudio.Volume == 0 ? "Sound off" : "Sound on";
            socialStatus.text = view.Room != null && view.Room.Connected ? "Friends · " + view.Room.Count + "/4" : "Solo play";
            last = view;
            // HUD
            place.text = "ContextSpeak · " + view.Place;
            clock.text = view.Time + " · " + view.Weather;
            money.text = "Cash " + view.Cash + "  ·  Card " + view.Card;
            band.text = view.Band + " · " + view.Strictness;
            var trail = SpeakingTrail.Create(view.Save);
            var nextStop = trail.Next(view.Save);
            trailHint.text = $"Speaking trail {trail.Completed(view.Save)}/3 · " + (nextStop == null ? "Complete! Choose a new goal in Map." : "Next: " + nextStop.Npc + " · M for route");
            RenderNeeds(view);
            RenderQuest(view);
            RenderMap(view);
            hint.text = view.Hint;
            Show(hint, view.Hint != "");
            toast.text = view.Toast;
            Show(toast, view.Toast != "");

            // Dialogue
            var talking = view.Talking != Partner.None;
            Show(dialogue, talking);
            if (talking) RenderDialogue(view);
            Show(menu, view.MenuOpen);
            if (view.MenuOpen) RenderMenu(view);
            Show(fishingPanel, view.Fishing != null);
            if (view.Fishing != null) RenderFishing(view);
            RenderModal(view);
        }

        private string needsSignature;
        private void RenderNeeds(SliceView view)
        {
            var signature = $"{view.Energy:0.00}{view.Hunger:0.00}{view.Thirst:0.00}";
            if (signature == needsSignature) return;
            needsSignature = signature;
            needs.Clear();
            foreach (var (label, value, inverse) in new[] { ("Energy", view.Energy, false), ("Hunger", view.Hunger, true), ("Thirst", view.Thirst, true) })
            {
                var row = Add(needs, null, "need");
                Text(row, label, "need-label");
                var bar = Add(row, null, "bar", "small");
                var fill = Add(bar, null, "bar-fill");
                fill.style.width = Length.Percent((inverse ? 1 - value : value) * 100);
                if (inverse && value > 0.75f) fill.AddToClassList("warn");
            }
        }

        private void RenderQuest(SliceView view)
        {
            var signature = (view.Quest?.Id ?? "none") + string.Join(",", view.QuestEarned);
            if (signature == questSignature) return;
            questSignature = signature;
            questList.Clear();
            if (view.Quest == null) { questTitle.text = "All café quests complete — explore freely!"; return; }
            questTitle.text = "QUEST · " + view.Quest.Title;
            for (var i = 0; i < view.Quest.Objectives.Length; i++)
            {
                var done = view.QuestEarned.Contains(view.Quest.Objectives[i]);
                Text(questList, (done ? "■ " : "□ ") + view.Quest.ObjectiveLabels[i], done ? "objective-done" : "objective");
            }
        }

        private void RenderMap(SliceView view)
        {
            var signature = $"{view.PlayerX:0.0},{view.PlayerZ:0.0}|" + string.Join("|", view.Markers.Select(marker => marker.ToString("F1")));
            if (signature == mapSignature) return;
            mapSignature = signature;
            mapDots.Clear();
            void Dot(float x, float z, string kind, string label = "", bool target = false)
            {
                var dot = Add(mapDots, null, "dot", kind);
                dot.EnableInClassList("target", target);
                dot.style.left = Length.Percent(Mathf.InverseLerp(MinX, MaxX, x) * 100);
                dot.style.top = Length.Percent((1 - Mathf.InverseLerp(MinZ, MaxZ, z)) * 100);
                if (label != "") Text(dot, label, "dot-label");
            }
            foreach (var marker in view.Markers)
            {
                var kind = marker.z == 0 ? "rowan" : marker.z == 1 ? "hazel" : marker.z == 2 ? "pier" : marker.z == 4 ? "rowan" : "seat";
                Dot(marker.x, marker.y, kind, marker.z == 0 ? "Rowan" : marker.z == 1 ? "Hazel" : marker.z == 2 ? "Pier" : "", marker.w > 0);
            }
            Dot(view.PlayerX, view.PlayerZ, "player", "You");
        }

        private void RenderDialogue(SliceView view)
        {
            portrait.text = view.Speaker.Length > 0 ? view.Speaker.Substring(0, 1) : "";
            portrait.EnableInClassList("hazel", view.Talking == Partner.Hazel);
            speaker.text = view.Speaker;
            role.text = view.Role + " · training target " + view.Band;
            line.text = view.Subtitles ? view.Line : "(Subtitles off — press Repeat to listen again)";
            translate.text = view.Translation ? "Hide 中文" : "中文 glossary";

            var glossary = string.Join("|", view.Glossary.Select(entry => entry.Phrase)) + view.Translation;
            if (glossary != glossarySignature)
            {
                glossarySignature = glossary;
                glossaryRow.Clear();
                foreach (var entry in view.Glossary)
                    Text(glossaryRow, entry.Phrase + " — " + entry.Meaning + (view.Translation ? "（" + entry.Chinese + "）" : ""), "glossary-chip");
            }
            recognized.text = view.Recognized != "" ? "You said: “" + view.Recognized + "”" : "";
            Show(recognized, view.Recognized != "");

            var review = view.Review;
            var hasCorrection = review != null && (review.Corrected != "" || view.PracticeTarget != "" || (view.ModelAnswer != "" && (view.Gate == Gate.ShowModelAndContinue || view.Attempts >= 2)));
            Show(correction, hasCorrection || review != null && review.Criteria.Length > 0);
            if (review != null)
            {
                original.text = hasCorrection ? "You: " + review.Original : "";
                var natural = view.PracticeTarget != "" ? view.PracticeTarget : review.Corrected != "" ? review.Corrected : view.ModelAnswer;
                corrected.text = hasCorrection && natural != "" ? "Natural: " + natural : review.GoodLanguage.Length > 0 ? "Nice phrase: “" + review.GoodLanguage[0] + "”" : "Clear and natural.";
                explanation.text = review.Explanation;
                Show(original, hasCorrection);
                Show(explanation, review.Explanation != "");
                var criteria = string.Join(",", review.Criteria.Select(note => note.Level));
                if (criteria + review.Original != criteriaSignature)
                {
                    criteriaSignature = criteria + review.Original;
                    criteriaRow.Clear();
                    foreach (var note in review.Criteria)
                    {
                        var chip = Text(criteriaRow, BandProfile.CriterionName(note.Criterion) + ": " + Level(note.Level), "criterion", "level-" + note.Level.ToString().ToLowerInvariant());
                        chip.tooltip = note.Note;
                    }
                }
            }
            Show(hearCorrection, hasCorrection);
            Show(practise, view.PracticeTarget != "" || view.Gate == Gate.OfferRetry);
            Show(carryOn, view.PracticeTarget != "" && view.Gate != Gate.RequireRetry || view.Gate == Gate.OfferRetry);
            Show(useModel, view.ModelAnswer != "" && (view.Gate == Gate.ShowModelAndContinue || view.PracticeTarget == "" && view.Attempts >= 2));
            aiTip.text = view.AiTip;
            Show(aiTip, view.AiTip != "");
            status.text = view.Status;
            Show(status, view.Status != "");

            RenderSuggestions(view);

            mic.text = view.Mic == "recording" ? "■ Stop & send" : view.Mic == "requesting" ? "Allow microphone…" : view.Mic == "offline" ? "Voice offline" : view.PracticeTarget != "" ? "● Say it again" : "● Speak";
            mic.SetEnabled(!view.Busy && view.Mic != "offline" && view.Lesson?.Complete != true);
            mic.EnableInClassList("recording", view.Mic == "recording");
            repeat.SetEnabled(view.CanReplay);
            slower.SetEnabled(view.CanReplay);
            Show(menuToggle, view.Talking == Partner.Rowan);
            Show(checkout, view.Talking == Partner.Rowan && view.Cart.Ready && view.Cart.Editable);
            reply.label = view.Mic == "offline" ? "Type your reply (Enter) — voice is offline" : "Rehearse by typing (Enter), then say it";

            var historyKey = view.History.Length + "" + showHistory;
            if (historyKey != historySignature)
            {
                historySignature = historyKey;
                history.Clear();
                foreach (var entry in view.History.Skip(Math.Max(0, view.History.Length - 30)))
                    Text(history, entry.Speaker + ": " + entry.Text, entry.Player ? "history-you" : "history-npc");
                history.scrollOffset = new Vector2(0, 99999);
            }
            Show(history, showHistory);
            historyToggle.text = showHistory ? "Hide history" : "History";
        }

        private static string Level(CriterionLevel level) => level == CriterionLevel.NotAssessed ? "not scored" : level == CriterionLevel.Developing ? "developing" : level == CriterionLevel.OnTarget ? "on target" : "above target";

        private void RenderSuggestions(SliceView view)
        {
            var nextStop = view.Lesson?.Complete == true ? SpeakingTrail.Create(view.Save).Next(view.Save)?.Id : "";
            var signature = $"{view.Lesson?.SceneId}/{view.Lesson?.Band}/{view.Lesson?.Step}/{nextStop}/{view.Paying}/{view.Save.FocusVocabulary}";
            if (signature == suggestionSignature) return;
            suggestionSignature = signature;
            suggestions.Clear();
            if (view.Lesson != null)
            {
                Text(suggestions, "Goal: " + view.Lesson.Plan.Goal, "suggestion");
                Text(suggestions, "Optional vocabulary: " + string.Join(" · ", view.Lesson.Plan.Vocabulary), "suggestion");
                Text(suggestions, "Conversation " + Math.Min(3, view.Lesson.Step + 1) + "/3 · use your own words", "suggestion");
                if (view.Lesson.Complete)
                {
                    var next = SpeakingTrail.Create(view.Save).Next(view.Save);
                    if (next != null) Action(suggestions, "Next stop → " + next.Name, "visit", next.Id, "secondary");
                    else Text(suggestions, "Trail complete · plan a new goal from Map", "suggestion");
                    Action(suggestions, "Retry conversation", "retry-lesson", view.Lesson.SceneId, "ghost");
                }
            }
            if (view.Paying) foreach (var phrase in new[] { "By card, please.", "I'll pay in cash.", "Could we split the bill?" }) Text(suggestions, "Say: “" + phrase + "”", "suggestion");
        }

        private void RenderMenu(SliceView view)
        {
            var signature = view.MenuCategory + view.SelectedItem;
            if (signature != menuSignature)
            {
                menuSignature = signature;
                menuItems.Clear();
                if (!Enum.TryParse(view.MenuCategory, out MenuCategory category)) category = MenuCategory.Coffee;
                foreach (var item in CafeMenu.In(category))
                {
                    var card = new Button(() => Command?.Invoke("select", item.Id));
                    card.AddToClassList("menu-card");
                    card.EnableInClassList("selected", item.Id == view.SelectedItem);
                    Picture(card, item, "thumb");
                    var text = Add(card, null, "menu-card-text");
                    Text(text, item.Name, "menu-name");
                    Text(text, item.Description, "menu-desc");
                    Text(card, CafeMenu.Money(item.Price), "menu-price");
                    menuItems.Add(card);
                }
            }
            RenderDetail(view);
            RenderCart(view);
        }

        private void RenderDetail(SliceView view)
        {
            var item = CafeMenu.Find(view.SelectedItem);
            var choices = item != null && pending.TryGetValue(item.Id, out var picked) ? picked : null;
            var signature = (item?.Id ?? "") + string.Join(",", choices?.Values.ToArray() ?? new string[0]) + view.Talking;
            if (signature == detailSignature) return;
            detailSignature = signature;
            menuDetail.Clear();
            if (item == null) { Text(menuDetail, "Choose a dish or drink to see ingredients, allergens and options. Ask Rowan anything about it out loud!", "hud-muted"); return; }
            Picture(menuDetail, item, "hero");
            Text(menuDetail, item.Name + " · " + CafeMenu.Money(item.Price), "detail-title");
            Text(menuDetail, item.Description);
            Text(menuDetail, "Ingredients: " + string.Join(", ", item.Ingredients), "hud-muted");
            Text(menuDetail, item.Allergens.Length == 0 ? "Allergens: none of the common ones" : "Allergens: " + string.Join(", ", item.Allergens), "allergens");
            if (item.Tags.Length > 0) Text(menuDetail, string.Join(" · ", item.Tags), "chip", "chip-green");
            foreach (var group in item.Options)
            {
                Text(menuDetail, MenuOptions.GroupLabel(group), "option-label");
                var row = Add(menuDetail, null, "row", "wrap");
                foreach (var choice in MenuOptions.Choices[group])
                {
                    var button = new Button(() =>
                    {
                        if (!pending.TryGetValue(item.Id, out var map)) pending[item.Id] = map = new Dictionary<OptionGroup, string>();
                        map[group] = choice;
                        detailSignature = "";
                        if (last != null) Render(last);
                    }) { text = choice + (MenuOptions.Surcharge(choice) > 0 ? " +" + CafeMenu.Money(MenuOptions.Surcharge(choice)) : "") };
                    button.AddToClassList("option");
                    button.EnableInClassList("selected", choices != null && choices.TryGetValue(group, out var current) && current == choice);
                    row.Add(button);
                }
            }
            var say = "Could I get " + (item.Name.StartsWith("A", StringComparison.OrdinalIgnoreCase) || item.Name.StartsWith("E", StringComparison.OrdinalIgnoreCase) ? "an " : "a ") + CartLine.Spoken(item.Name) + ", please?";
            Text(menuDetail, "Say it: “" + say + "”", "say-it");
            var add = Action(menuDetail, view.Talking == Partner.Rowan ? "Add to order" : "Talk to Rowan to order", "add", item.Id, "primary");
            add.SetEnabled(view.Talking == Partner.Rowan && view.Cart.Editable);
        }

        private void RenderCart(SliceView view)
        {
            var cart = view.Cart;
            var signature = string.Join(";", cart.Lines.Select(entry => entry.Item + entry.Quantity + string.Join(",", entry.Choices))) + cart.Status + cart.Payers;
            if (signature == cartSignature) return;
            cartSignature = signature;
            cartRows.Clear();
            if (cart.Empty) Text(cartRows, "Nothing yet. Ask for the menu, or just tell Rowan what you'd like.", "hud-muted");
            for (var index = 0; index < cart.Lines.Count; index++)
            {
                var entry = cart.Lines[index];
                var row = Add(cartRows, null, "cart-row");
                Text(row, entry.Describe(), "cart-name");
                var missing = entry.Missing().ToArray();
                if (missing.Length > 0) Text(row, "Rowan will ask: " + string.Join(", ", missing.Select(MenuOptions.GroupLabel)).ToLowerInvariant(), "cart-missing");
                var controls = Add(row, null, "row");
                if (cart.Editable)
                {
                    Action(controls, "−", "quantity", index + "|-1", "ghost", "small");
                    Action(controls, "+", "quantity", index + "|1", "ghost", "small");
                }
                Text(controls, CafeMenu.Money(entry.Total), "cart-price", "push-right");
            }
            var status = cart.Status == OrderStatus.Browsing ? "" : " · " + cart.Status;
            cartTotal.text = "Total " + CafeMenu.Money(cart.Total) + (cart.Payers > 1 ? " · your share " + CafeMenu.Money(cart.Split(cart.Payers)[0]) : "") + status;
        }

        private static void Picture(VisualElement parent, MenuItem item, string size)
        {
            var frame = Add(parent, null, "picture", size, "cat-" + item.Category.ToString().ToLowerInvariant());
            var texture = Resources.Load<Texture2D>("SliceMenu/" + item.Id);
            if (texture != null) frame.style.backgroundImage = new StyleBackground(texture);
            else Text(frame, item.Name.Substring(0, 1), "picture-initial"); // fallback when art is missing
        }

        private void RenderFishing(SliceView view)
        {
            var session = view.Fishing;
            fishingMessage.text = session.Message;
            tensionFill.style.width = Length.Percent(Mathf.Clamp01(session.Tension) * 100);
            tensionFill.EnableInClassList("danger", session.Tension > 0.8f);
            progressFill.style.width = Length.Percent(Mathf.Clamp01(session.Progress) * 100);
            var next = FishingSession.XpForLevel(view.FishingLevel + 1);
            fishingLevel.text = $"Fishing level {view.FishingLevel} · {view.FishingXp}/{next} XP";
        }

        private void RenderModal(SliceView view)
        {
            Show(modal, view.Panel != "");
            if (view.Panel == "") { modalSignature = ""; return; }
            var save = view.Save;
            var signature = view.Panel + save.Inventory.Count + save.Receipts.Count + save.QuestsDone.Count + save.Earned.Count + save.Progress.Turns.Count + save.Band + save.Strictness + save.Translation + save.Subtitles + save.MouseSensitivity + AlwaysLook + GameAudio.Muted + view.Character + view.CharacterName + save.FocusVocabulary + save.PracticeNeed;
            if (view.Panel == "friends") signature = "friends";
            if (signature == modalSignature) { if (view.Panel == "friends") RefreshFriends(view.Room); return; }
            modalSignature = signature;
            modal.Clear();
            var card = Add(modal, null, "card", "modal-card");
            var header = Add(card, null, "row");
            Text(header, view.Panel == "inventory" ? "BAG" : view.Panel == "journal" ? "JOURNAL" : view.Panel == "map" ? "MAP" : view.Panel == "friends" ? "FRIENDS" : view.Panel == "scenery" ? "SCENERY" : "SETTINGS", "menu-title");
            Action(header, "Close · Esc", "panel", view.Panel, "ghost", "push-right");
            var body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("modal-body");
            card.Add(body);
            switch (view.Panel)
            {
                case "scenery": Scenery(body); break;
                case "friends": Friends(body, view.Room); break;
                case "inventory": Inventory(body, save); break;
                case "journal": Journal(body, view); break;
                case "map": Map(body); break;
                case "school":
                    Text(body, "CONVERSATION CLUB · CLASS TIMETABLE", "section-title");
                    Text(body, "09:00 · Warm-up: familiar subjects and learning habits\n10:00 · Story time: describe an experience\n11:00 · Discussion: explain and defend your opinion", "hud-muted");
                    Text(body, "Tasks adapt to your current IELTS 3–9 practice target. This is original practice material, not an official exam.", "hud-muted");
                    Action(body, "Meet Ms Taylor", "visit", "school", "secondary");
                    break;
                default: Settings(body, view); break;
            }
        }

        private void Inventory(VisualElement body, SliceSave save)
        {
            Text(body, "Wallet: cash " + CafeMenu.Money(save.Wallet.Cash) + " · card " + CafeMenu.Money(save.Wallet.Card), "detail-title");
            Text(body, "ITEMS", "section-title");
            if (save.Inventory.Count == 0) Text(body, "Empty. Catch some fish at the pier!", "hud-muted");
            foreach (var entry in save.Inventory)
            {
                var parts = entry.Split(':');
                var fish = FishingSession.Find(parts[0]);
                Text(body, (fish != null ? fish.Name + " (" + fish.Rarity + ")" : parts[0]) + " × " + parts[1]);
            }
            Text(body, "CATCH LOG", "section-title");
            foreach (var fish in save.Catches.Skip(Math.Max(0, save.Catches.Count - 8)).Reverse())
                Text(body, $"Day {fish.Day} · {FishingSession.Find(fish.Species)?.Name} · {fish.Length:0.0} cm", "hud-muted");
            Text(body, "RECEIPTS", "section-title");
            if (save.Receipts.Count == 0) Text(body, "No receipts yet.", "hud-muted");
            foreach (var receipt in save.Receipts.Skip(Math.Max(0, save.Receipts.Count - 5)).Reverse())
            {
                var box = Add(body, null, "receipt");
                Text(box, "Forest Café · " + receipt.Id + " · paid by " + receipt.Method, "receipt-head");
                foreach (var entry in receipt.Lines) Text(box, entry, "receipt-line");
                Text(box, "Total " + CafeMenu.Money(receipt.Total) + (receipt.Payers > 1 ? $" · split {receipt.Payers} ways · you paid {CafeMenu.Money(receipt.Paid)}" : ""), "receipt-total");
            }
        }

        private void Journal(VisualElement body, SliceView view)
        {
            var save = view.Save;
            Text(body, "QUESTS", "section-title");
            foreach (var quest in QuestBook.Quests)
            {
                var done = save.QuestsDone.Contains(quest.Id);
                var available = QuestBook.Available(quest, save.QuestsDone);
                var box = Add(body, null, "quest", done ? "done" : available ? "active" : "locked");
                Text(box, (done ? "■ Done · " : available ? "● " : "Locked · ") + quest.Title + " · " + quest.Location, "detail-title");
                Text(box, quest.Summary + " Reward: " + quest.Reward, "hud-muted");
                if (available)
                    for (var i = 0; i < quest.Objectives.Length; i++)
                        Text(box, (save.Earned.Contains(quest.Objectives[i]) ? "■ " : "□ ") + quest.ObjectiveLabels[i], "objective");
            }
            Text(body, "LEARNING DASHBOARD", "section-title");
            Text(body, "Targets and feedback guide practice; they are not official IELTS scores. Pronunciation needs audio assessment.", "hud-muted");
            Text(body, "Level: " + BandProfile.For(save.Band).Label + " · strictness " + save.Strictness);
            Text(body, $"Speaking turns: {save.Progress.Turns.Count} · recent average {save.Progress.Average():P0}");
            var recent = save.Progress.Turns.Skip(Math.Max(0, save.Progress.Turns.Count - 20)).ToArray();
            foreach (var criterion in new[] { SpeakingCriterion.FluencyCoherence, SpeakingCriterion.LexicalResource, SpeakingCriterion.GrammaticalRange, SpeakingCriterion.Pronunciation })
            {
                var levels = recent.Select(turn => criterion == SpeakingCriterion.FluencyCoherence ? turn.Fluency : criterion == SpeakingCriterion.LexicalResource ? turn.Lexical : criterion == SpeakingCriterion.GrammaticalRange ? turn.Grammar : turn.Pronunciation).Where(level => level != CriterionLevel.NotAssessed).ToArray();
                var share = levels.Length == 0 ? -1 : levels.Count(level => level != CriterionLevel.Developing) / (float)levels.Length;
                var row = Add(body, null, "need");
                Text(row, BandProfile.CriterionName(criterion), "need-label", "wide");
                if (share < 0) { Text(row, criterion == SpeakingCriterion.Pronunciation ? "needs a pronunciation provider" : "no data yet", "hud-muted"); continue; }
                var bar = Add(row, null, "bar");
                Add(bar, null, "bar-fill").style.width = Length.Percent(share * 100);
            }
            Text(body, "Weaknesses: " + (view.Weaknesses.Length == 0 ? "none spotted yet" : string.Join(" ", view.Weaknesses)));
            Text(body, "Phrases you've used well: " + (save.Progress.Phrases.Count == 0 ? "—" : string.Join(", ", save.Progress.Phrases.Take(12))));
            Text(body, "Completed scenarios: " + (save.Progress.Scenarios.Count == 0 ? "—" : string.Join(", ", save.Progress.Scenarios)));
            Text(body, "Achievements: " + (save.Progress.Achievements.Count == 0 ? "—" : string.Join(", ", save.Progress.Achievements)));
            Text(body, view.Recommendation, "say-it");
            Text(body, "RECENT CORRECTIONS", "section-title");
            foreach (var turn in save.Progress.Turns.Where(turn => !string.IsNullOrEmpty(turn.Corrected)).Reverse().Take(5))
                Text(body, "“" + turn.Utterance + "” → “" + turn.Corrected + "”", "hud-muted");
        }

        private void Map(VisualElement body)
        {
            Action(body, "Explore downloaded scenery", "panel", "scenery", "secondary");
            var save = last.Save;
            var trail = SpeakingTrail.Create(save);
            Text(body, $"YOUR SPEAKING TRAIL · {trail.Completed(save)}/3 places complete", "section-title");
            Action(body, "Visit your wardrobe", "visit", "wardrobe", "ghost");
            Action(body, "Visit the speaking lodge", "visit", "lodge", "ghost");
            Text(body, BandProfile.For(save.Band).Summary, "hud-muted");
            var need = new DropdownField("Practice situation", Enum.GetNames(typeof(PracticeNeed)).ToList(), (int)save.PracticeNeed) { name = "trail-need" };
            need.RegisterValueChangedCallback(change => Command?.Invoke("practice-need", change.newValue));
            body.Add(need);
            var focus = new TextField("Focus vocabulary, separated by commas") { name = "trail-focus", value = save.FocusVocabulary, maxLength = 120 };
            focus.RegisterCallback<FocusInEvent>(_ => Typing = true);
            focus.RegisterCallback<FocusOutEvent>(_ => Typing = false);
            body.Add(focus);
            var plan = new Button(() => Command?.Invoke("plan-route", focus.value)) { text = "Plan route" };
            plan.AddToClassList("secondary");
            body.Add(plan);
            foreach (var stop in trail.Stops)
                Text(body, (trail.Done(save, stop.Id) ? "✓ " : "○ ") + stop.Name + " · " + stop.Npc, "hud-muted");
            var next = trail.Next(save);
            if (next != null) Action(body, "Next stop → " + next.Name, "visit", next.Id, "secondary");
            else Text(body, "Trail complete! Try another target or retry a conversation below.", "say-it");
            Text(body, "LIFE SCENES · choose a place to practise", "section-title");
            Action(body, "Order at the café counter · Rowan", "visit", "counter", "secondary");
            foreach (var place in LifeCurriculum.Places)
            {
                var row = Add(body, null, "row", "wrap");
                Action(row, place.Name + " · " + place.Npc, "visit", place.Id, "secondary");
                Action(row, "Retry conversation", "retry-lesson", place.Id, "ghost", "small");
            }
            Text(body, "The same places offer different vocabulary and speaking goals at each target. Change IELTS 3–9 in Settings.", "hud-muted");
            Text(body, "You are in the Forest Café district.", "detail-title");
            foreach (var place in new[]
            {
                "Forest Café · counter, bakery display and barista station (Rowan takes orders)",
                "Indoor tables and the glass-walled dining room",
                "Outdoor terrace overlooking the pond",
                "Fishing pier · Hazel teaches fishing here",
                "Small bridge and nature trail around the pond",
                "Picnic area under the pines",
            }) Text(body, "• " + place);
            Text(body, "The original first-day errands remain available in Life town.", "hud-muted");
            Action(body, "Go to Life town", "life", "", "secondary");
        }

        private void Friends(VisualElement body, RoomClient room)
        {
            Text(body, "Explore with friends", "section-title");
            Text(body, "Up to four players in this district. See each other move and practise with NPCs. Your learning progress stays yours.", "hud-muted");
            friendsStatus = Text(body);
            inviteCode = new TextField("Room code · share with friends") { isReadOnly = true };
            body.Add(inviteCode);
            createRoom = Action(body, "Create room", "room-create", "", "secondary");
            joinCodeField = new TextField("Friend's room code") { value = joinCode };
            joinCodeField.RegisterValueChangedCallback(change => { joinCode = change.newValue; RefreshFriends(room); });
            body.Add(joinCodeField);
            joinRoom = new Button(() => Command?.Invoke("room-join", joinCode)) { text = "Join room" };
            joinRoom.AddToClassList("secondary");
            body.Add(joinRoom);
            leaveRoom = Action(body, "Leave / cancel connection", "room-leave", "", "ghost");
            friendsRoster = Text(body, "", "hud-muted");
            serverInput = room?.Endpoint ?? "http://127.0.0.1:4330";
            shownEndpoint = serverInput;
            roomServerField = new TextField("Room server") { value = serverInput };
            roomServerField.RegisterValueChangedCallback(change => serverInput = change.newValue);
            body.Add(roomServerField);
            useRoomServer = new Button(() => Command?.Invoke("room-server", serverInput)) { text = "Use server" };
            body.Add(useRoomServer);
            Text(body, "A shared server address is needed for different computers. Microphone audio is not sent to friends. Changing district leaves the room.", "hud-muted");
            RefreshFriends(room);
        }

        private void RefreshFriends(RoomClient room)
        {
            if (room == null) return;
            friendsStatus.text = room.Status;
            inviteCode.SetValueWithoutNotify(room.Code);
            createRoom.SetEnabled(room.Ready && !room.Busy && !room.Connected);
            joinRoom.SetEnabled(room.Ready && !room.Busy && !room.Connected && joinCode.Trim().Length == 12);
            joinCodeField.SetEnabled(!room.Busy && !room.Connected);
            leaveRoom.SetEnabled(room.Connected || room.Ready && room.Busy);
            var nativeSettings = Application.platform != RuntimePlatform.WebGLPlayer || Application.isEditor;
            roomServerField.SetEnabled(nativeSettings && room.Ready && !room.Connected && !room.Busy);
            useRoomServer.SetEnabled(nativeSettings && room.Ready && !room.Connected && !room.Busy);
            if (shownEndpoint != room.Endpoint)
            {
                shownEndpoint = serverInput = room.Endpoint;
                roomServerField.SetValueWithoutNotify(serverInput);
            }
            friendsRoster.text = string.Join("\n", room.Players.Select(player => player.name + " · "
                + (player.activity == "exploring" ? "Exploring" : (player.activity == "recording" ? "Speaking to " : "Practising with ") + player.npc)));
        }

        private void Scenery(VisualElement body)
        {
            var scenery = FindFirstObjectByType<WorldScenery>();
            if (scenery == null) { Text(body, "Scenery is available in the Forest Café district."); return; }
            Text(body, "DOWNLOADED SCENERY", "section-title");
            Text(body, "Choose a model to visit its safe approach. Nearby models load automatically.", "hud-muted");
            var list = Add(body);
            var zones = new[] { "All areas" }.Concat(scenery.Entries.Where(entry => entry.usage == "world").Select(entry => entry.zone).Distinct().OrderBy(zone => zone)).ToList();
            var filter = new DropdownField("Area", zones, 0);
            body.Insert(body.childCount - 1, filter);
            void Show(string zone)
            {
                list.Clear();
                foreach (var entry in scenery.Entries.Where(entry => entry.usage == "world" && (zone == "All areas" || entry.zone == zone)))
                    Action(list, entry.Label + " · " + entry.zone, "visit", entry.id, "secondary", "wide");
            }
            filter.RegisterValueChangedCallback(change => Show(change.newValue));
            Show(zones[0]);
        }

        private void Settings(VisualElement body, SliceView view)
        {
            var save = view.Save;
            Action(body, "Play with friends", "panel", "friends", "secondary");
            Text(body, "SOUND", "section-title");
            var mute = new Toggle("Mute all sound") { value = GameAudio.Muted, name = "master-mute" };
            mute.RegisterValueChangedCallback(change => Command?.Invoke("mute", change.newValue.ToString()));
            body.Add(mute);
            var volume = new Slider("Master volume", 0, 1) { value = GameAudio.Volume, name = "master-volume" };
            volume.RegisterValueChangedCallback(change => Command?.Invoke("volume", change.newValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            body.Add(volume);
            Text(body, "Mute silences NPC voices and effects. You can still record replies.", "hud-muted");
            Text(body, "YOUR CHARACTER · " + view.CharacterName, "section-title");
            Action(body, "Edit name and appearance", "wardrobe", "", "secondary");
            var characters = Add(body, null, "row", "wrap");
            foreach (var character in new[] { PlayerLook.Girl, PlayerLook.Boy })
            {
                var choice = Action(characters, character == PlayerLook.Girl ? "Girl · Academy" : "Boy · Daydream", "character", character.ToString(), "option");
                choice.EnableInClassList("selected", character == view.Character);
            }
            Text(body, "SUPPLIED ANIMATIONS", "section-title");
            var scenery = FindFirstObjectByType<WorldScenery>();
            foreach (var motion in (scenery != null ? scenery.Entries : Array.Empty<SceneryEntry>()).Where(entry => !string.IsNullOrEmpty(entry.motion))
                .Select(entry => entry.model + "|" + entry.motion))
                Action(body, motion.Replace("|", " · "), "motion-preview", motion, "secondary");
            Text(body, "Vault is an in-place animation preview; it does not bypass world collisions.", "hud-muted");
            Text(body, "TRAINING TARGET · IELTS 3–9", "section-title");
            Text(body, "Choose your practice target freely. These are original learning goals informed by IELTS descriptors, not a predicted exam score.", "hud-muted");
            foreach (var profile in BandProfile.All)
            {
                var button = Action(body, profile.Label + " — " + profile.Summary, "band", profile.Band.ToString(), "option", "wide");
                button.EnableInClassList("selected", profile.Band == save.Band);
            }
            Text(body, "CORRECTIONS", "section-title");
            foreach (var (mode, label) in new[] { (Strictness.Practice, "Practice — corrections are optional"), (Strictness.Guided, "Guided — retry important errors"), (Strictness.Challenge, "Challenge — retry until assessed goals are met") })
            {
                var button = Action(body, label, "strictness", mode.ToString(), "option", "wide");
                button.EnableInClassList("selected", mode == save.Strictness);
            }
            Text(body, "CONTROLS AND DISPLAY", "section-title");
            var slider = new Slider("Mouse sensitivity", 0.2f, 3f) { value = save.MouseSensitivity };
            slider.RegisterValueChangedCallback(change => Command?.Invoke("sensitivity", change.newValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            body.Add(slider);
            var look = new Toggle("Mouse look without holding right-click") { value = AlwaysLook };
            look.RegisterValueChangedCallback(change => AlwaysLook = change.newValue);
            body.Add(look);
            var subtitles = new Toggle("Subtitles") { value = save.Subtitles };
            subtitles.RegisterValueChangedCallback(_ => Command?.Invoke("subtitles", ""));
            body.Add(subtitles);
            var translation = new Toggle("Chinese glossary for idioms") { value = save.Translation };
            translation.RegisterValueChangedCallback(_ => Command?.Invoke("translation", ""));
            body.Add(translation);
            var row = Add(body, null, "row", "wrap");
            Action(row, "Reconnect voice / AI", "connect", "", "secondary");
            Action(row, "Save now", "save", "", "secondary");
            Action(row, "Back to Life town", "life", "", "secondary");
            Action(row, "New game", "reset", "confirm", "ghost");
            Text(body, "Voice uses the local companion (pnpm world:dev). Without it you can type, and pronunciation is not scored — it needs a dedicated pronunciation provider.", "hud-muted");
        }
    }
}
