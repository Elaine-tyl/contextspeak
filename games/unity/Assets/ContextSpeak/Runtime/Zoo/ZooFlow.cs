using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Metro City Wildlife Zoo gameplay: ZOO_VISIT_001 and the optional quests, the ticket office and
    /// gate, the zoo map (M), animal information boards, the guide and keepers (IELTS speaking with the
    /// shared feedback-and-repeat dialogue), the café, the animal quiz and wildlife photos (P).
    /// Reuses the airport/beach panel, shop, payment and dialogue framework.
    public sealed class ZooFlow : AirportFlowBase
    {
        public override string QuestId => ZooQuest.Id;
        public override string QuestTitle => ZooQuest.Title;
        protected override string MapTitle => "Zoo map";
        private ZooProgress Progress => ZooQuest.Progress;

        [Header("Scene wiring")]
        public Transform Spawn;
        public GameObject Barrier;                  // blocks the gate until a ticket is validated
        public Bounds ElephantZone, GiraffeZone;
        public AnimalHabitat Savannah, Farm;

        private int ticketChoice, quizIndex, quizScore, quizPicked = -1;
        private AnimalSpecies shown;
        private AnimalHabitat shownHabitat;
        private float photoCooldown;

        protected override string[] AmbientAnnouncements => new[]
        {
            "Welcome to Metro City Wildlife Zoo. Please do not feed the animals, and keep to the visitor paths.",
            "The keeper talk at the elephant waterhole starts shortly. Everyone is welcome.",
            "Did you know? Every zebra has its own pattern of stripes. Find out more at the savannah viewing decks.",
        };

        protected override ScenarioDefinition CreateScenario(string name)
        {
            var ticket = ZooTickets.Find(Progress.ticket) ?? ZooTickets.All[Mathf.Clamp(ticketChoice, 0, ZooTickets.All.Length - 1)];
            return ZooScenarios.Create(name, new Dictionary<string, string> { { "ticket", ticket.Name }, { "price", CurrencyManager.Format(ticket.Cents) }, { "animal", shown != null ? shown.Name : "elephant" } });
        }

        protected override void MarkDone(string scenario) => Progress.Mark(scenario);
        protected override bool IsOrder(string scenario) => scenario == "Zoo Café Barista";

        protected override void Start()
        {
            base.Start();
            AirportUI.Begin(out _, out _);
            if (Spawn != null && SaveManager.Data.returnEntrance != "") Player.Place(Spawn.position, Spawn.forward);
            if (Progress.stage == 0) AdvanceZoo(ZooStage.AtZoo);
            if (Barrier != null) Barrier.SetActive(!Progress.ticketValidated);
            Objective = ZooQuest.Next;
        }

        private void AdvanceZoo(ZooStage stage)
        {
            var before = Progress.stage;
            if (ZooQuest.Advance(stage) && Progress.stage != before)
            {
                LifeUI.Toast("✔ " + ZooQuest.Objectives[(int)stage]);
                foreach (var skipped in ZooQuest.ComingSoon.Where(number => number > before && number <= Progress.stage))
                    LifeUI.Toast("“" + ZooQuest.Objectives[skipped] + "” opens in a later phase of the zoo — skipped for now.");
                if (Progress.Stage == ZooStage.Rewarded) Show("complete");
            }
            Objective = ZooQuest.Next;
        }

        protected override void Update()
        {
            base.Update();
            var player = Player.Transform;
            if (player == null) return;
            if (Panel == "map" && Progress.Stage == ZooStage.BoughtTicket) AdvanceZoo(ZooStage.OpenedMap);
            var position = player.position + Vector3.up;
            if (Progress.Stage == ZooStage.MetGuide && ElephantZone.Contains(position)) AdvanceZoo(ZooStage.AtElephants);
            if (Progress.Stage == ZooStage.DescribedElephant && GiraffeZone.Contains(position)) AdvanceZoo(ZooStage.AtGiraffes);
            photoCooldown -= Time.deltaTime;
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame && Panel == "" && !Busy && photoCooldown <= 0) Photograph(player);
        }

        // ------------------------------------------------------------------ photos
        private void Photograph(Transform player)
        {
            var camera = Camera.main;
            if (camera == null || !Progress.ticketValidated) return;
            photoCooldown = 1.5f;
            AnimalController best = null;
            var bestAngle = 14f;
            foreach (var habitat in new[] { Savannah, Farm })
            {
                if (habitat == null) continue;
                foreach (var animal in habitat.Animals)
                {
                    if (animal == null) continue;
                    var to = animal.transform.position + Vector3.up * animal.Species.Height * 0.5f - camera.transform.position;
                    if (to.magnitude > 70) continue;
                    var angle = Vector3.Angle(camera.transform.forward, to);
                    if (angle < bestAngle) { bestAngle = angle; best = animal; }
                }
            }
            if (best == null) { LifeUI.Toast("No animal in the middle of the picture — point the camera at one and press P."); return; }
            Progress.photos++;
            SaveManager.Save();
            LifeUI.Toast("📷 Photo " + Progress.photos + ": " + best.Species.Name + " " + best.Activity + ".");
            if (Progress.photos >= 3) { var first = !Progress.Done("ZOO_PHOTO"); ZooQuest.CompleteOptional("ZOO_PHOTO", 30); if (first) LifeUI.Toast("Wildlife photography challenge complete · +30 XP"); }
        }

        // ------------------------------------------------------------------ NPCs
        private static void Say(NPCController npc, string line)
        {
            LifeUI.Toast(npc.DisplayName + ": " + line);
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.Speech.Speak(line);
        }

        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            switch (npc.Key)
            {
                case "ticket":
                    if (Progress.ticket != "") { Say(npc, "You already have your " + (ZooTickets.Find(Progress.ticket)?.Name ?? "ticket").ToLowerInvariant() + ". Scan it at the gate — enjoy your visit!"); return; }
                    Speak("Ticket Officer", npc, () => Show("ticket"));
                    return;
                case "guide":
                    if (!Progress.ticketValidated) { Say(npc, "Welcome! Buy a ticket at the ticket office and scan it at the gate first."); return; }
                    if (Progress.stage < (int)ZooStage.OpenedMap) { Say(npc, "Have a look at the zoo map first — press M — then come and talk to me."); return; }
                    if (Progress.Stage == ZooStage.OpenedMap) { Speak("Zoo Guide Welcome", npc, () => AdvanceZoo(ZooStage.MetGuide)); return; }
                    if (Progress.stage >= (int)ZooStage.RodeTram && Progress.Stage != ZooStage.Rewarded) { Speak("Conservation Discussion", npc, () => AdvanceZoo(ZooStage.ConservationTalk)); return; }
                    if (Progress.Stage == ZooStage.Rewarded) { Speak("Conservation Discussion", npc, () => LifeUI.Toast("Great discussion!")); return; }
                    Say(npc, "Next on your list: " + ZooQuest.Next.ToLowerInvariant() + ". Come back to me for the conservation discussion at the end.");
                    return;
                case "keeper-elephant":
                    if (Progress.stage < (int)ZooStage.AtElephants) { Say(npc, "The herd comes to the waterhole several times a day. Talk to the zoo guide near the gate first — she'll show you round."); return; }
                    Speak("Elephant Keeper", npc, () => { if (Progress.Stage == ZooStage.AtElephants) AdvanceZoo(ZooStage.DescribedElephant); });
                    return;
                case "keeper-giraffe":
                    if (Progress.stage < (int)ZooStage.AtGiraffes) { Say(npc, "The giraffes love the tall trees on this side. Visit the elephants first, then come back and tell me what you notice."); return; }
                    Speak("Giraffe Keeper", npc, () => { if (Progress.Stage == ZooStage.AtGiraffes) AdvanceZoo(ZooStage.ComparedAnimals); });
                    return;
                case "barista":
                    Shop(ZooMenus.Cafe(), npc, "Zoo Café Barista", cart =>
                    {
                        LifeUI.Toast("Here you are — " + cart.Describe() + ". Enjoy!");
                        if (Progress.stage >= (int)ZooStage.VisitedAviary) AdvanceZoo(ZooStage.OrderedDrink);
                    });
                    return;
                case "visitor-lost":
                    Speak("Lost Visitor", npc, () => { var first = !Progress.Done("ZOO_HELP"); ZooQuest.CompleteOptional("ZOO_HELP", 20); if (first) LifeUI.Toast("You helped a visitor · +20 XP"); });
                    return;
                case "keeper-farm":
                    Speak("Farm Keeper", npc, () => LifeUI.Toast("The keeper lets you help scatter hay for the sheep."));
                    return;
                case "volunteer":
                    Speak("Conservation Volunteer", npc, () => { var first = !Progress.Done("ZOO_AWARE"); ZooQuest.CompleteOptional("ZOO_AWARE", 25); if (first) LifeUI.Toast("Conservation awareness task complete · +25 XP"); });
                    return;
                case "tram":
                    Speak("Tram Operator", npc, () => Say(npc, "The safari tram opens in a later phase: four stops, a guided tour of the savannah and live commentary. See you then!"));
                    return;
                default:
                    Say(npc, VisitorLines[Mathf.Abs(npc.DisplayName.GetHashCode()) % VisitorLines.Length]);
                    return;
            }
        }

        private static readonly string[] VisitorLines =
        {
            "Have you seen the baby elephant? It follows its mother everywhere.", "The antelopes never stop looking around, do they?",
            "We come here every month. The waterhole is the best place to watch.", "I could watch them drink all day.",
            "My daughter wants to see the pony at the children's farm.", "It's so peaceful here in the morning.",
        };

        // ------------------------------------------------------------------ spots
        public override void Use(string action)
        {
            switch (action)
            {
                case "gate":
                    if (Progress.ticket == "") { LifeUI.Toast("You need a ticket. The ticket office is just to the left of the gate."); return; }
                    if (!Progress.ticketValidated)
                    {
                        Progress.ticketValidated = true;
                        SaveManager.Save();
                        if (Barrier != null) Barrier.SetActive(false);
                        LifeUI.Toast("Beep! Ticket valid — welcome to the zoo. Press M for the map.");
                    }
                    else LifeUI.Toast("Your ticket has already been scanned. Go on in!");
                    return;
                case "map": Show("map"); return;
                case "quiz": quizIndex = 0; quizScore = 0; quizPicked = -1; Show("quiz"); return;
                case "fountain": LifeUI.Toast("You take a drink of cool water."); return;
                case "comingsoon": LifeUI.Toast("This area is under construction: the Rainforest, Big Cats, Penguin Cove, the Aviary, the Reptile House and the Safari Tram open in later phases."); return;
            }
        }

        public void ShowAnimal(string speciesId, AnimalHabitat habitat)
        {
            shown = AnimalCatalog.Find(speciesId);
            shownHabitat = habitat;
            if (shown == null) return;
            if (!Progress.seen.Contains(speciesId)) { Progress.seen.Add(speciesId); SaveManager.Save(); }
            if (Progress.seen.Count >= 5) { var first = !Progress.Done("ZOO_VOCAB"); ZooQuest.CompleteOptional("ZOO_VOCAB", 30); if (first) LifeUI.Toast("Animal vocabulary scavenger hunt complete · +30 XP"); }
            Show("animal");
        }

        // ------------------------------------------------------------------ panels
        protected override void DrawPanel(string panel, float width, float height)
        {
            switch (panel)
            {
                case "ticket": TicketUI(width, height); break;
                case "animal": AnimalUI(width, height); break;
                case "quiz": QuizUI(width, height); break;
                case "complete":
                {
                    var area = AirportUI.Window(ZooQuest.Title + " — complete!", 820, 480, width, height);
                    GUILayout.BeginArea(area);
                    GUILayout.Label("You bought a ticket, read the map, met the guide and the keepers, described and compared animals and discussed conservation — all in English.", AirportUI.InkBig);
                    GUILayout.Label("Quest " + ZooQuest.Id + " ✔  +250 XP  ·  Junior Wildlife Ranger badge", AirportUI.Good);
                    if (Progress.skipped.Count > 0) GUILayout.Label("Not open yet (skipped): " + string.Join(", ", Progress.skipped.Select(number => ZooQuest.Objectives[number].ToLowerInvariant())), AirportUI.Muted);
                    GUILayout.Label("Optional: photos (P) · animal quiz · vocabulary hunt · conservation volunteer · help a visitor", AirportUI.Muted);
                    GUILayout.Label("This is practice feedback, not an official IELTS score.", AirportUI.Muted);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", AirportUI.Primary)) Hide();
                    GUILayout.EndArea();
                    break;
                }
            }
        }

        private void TicketUI(float width, float height)
        {
            var area = AirportUI.Window("Ticket office — admission", 820, 520, width, height);
            GUILayout.BeginArea(area);
            for (var i = 0; i < ZooTickets.All.Length; i++)
            {
                var ticket = ZooTickets.All[i];
                if (GUILayout.Toggle(ticketChoice == i, "  " + ticket.Name + "   ·   " + CurrencyManager.Format(ticket.Cents) + "\n  " + ticket.Description, AirportUI.Secondary, GUILayout.Height(64))) ticketChoice = i;
            }
            AirportUI.Divider();
            AirportUI.Row("Your balance", CurrencyManager.Balance);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var chosen = ZooTickets.All[Mathf.Clamp(ticketChoice, 0, ZooTickets.All.Length - 1)];
            if (GUILayout.Button("Buy " + chosen.Name.ToLowerInvariant() + " · " + CurrencyManager.Format(chosen.Cents), AirportUI.Primary))
            {
                Hide();
                PaymentController.Instance.Show("Metro City Wildlife Zoo", "1 × " + chosen.Name + "   " + CurrencyManager.Format(chosen.Cents), chosen.Cents, () =>
                {
                    Progress.ticket = chosen.Id;
                    InventoryManager.Add("zoo-ticket", "Zoo " + chosen.Name.ToLowerInvariant());
                    SaveManager.Save();
                    AdvanceZoo(ZooStage.BoughtTicket);
                    LifeUI.Toast("Ticket bought. Scan it at the gate.");
                }, () => LifeUI.Toast("Purchase cancelled."));
            }
            if (GUILayout.Button("Cancel", AirportUI.Secondary, GUILayout.Width(150))) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void AnimalUI(float width, float height)
        {
            if (shown == null) { Hide(); return; }
            var temporary = shownHabitat != null && shownHabitat.Animals.Any(animal => animal != null && animal.SpeciesId == shown.Id && animal.PlaceholderModel);
            var area = AirportUI.Window(shown.Name + (temporary ? "  (temporary model)" : ""), 900, 640, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label(shown.Describe, AirportUI.InkBig);
            AirportUI.Divider();
            AirportUI.Row("Eats", shown.Diet);
            AirportUI.Row("Lives in", shown.Home);
            AirportUI.Row("Conservation status", shown.Status, AirportUI.Accent);
            if (shownHabitat != null)
            {
                var animals = shownHabitat.Animals.Where(animal => animal != null && animal.SpeciesId == shown.Id).ToArray();
                if (animals.Length > 0) AirportUI.Row("Right now", string.Join(", ", animals.GroupBy(animal => animal.Activity).Select(group => group.Count() + " " + group.Key)));
            }
            AirportUI.Divider();
            GUILayout.Label("Did you know?  " + shown.Fact, AirportUI.Ink);
            GUILayout.Space(8);
            GUILayout.Label("Useful words", AirportUI.Muted);
            foreach (var word in shown.Vocabulary) GUILayout.Label("•  " + word, AirportUI.Ink);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Boards read: " + Progress.seen.Count + " / 5 for the vocabulary hunt", AirportUI.Muted);
            if (GUILayout.Button("Close", AirportUI.Primary, GUILayout.Width(180))) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void QuizUI(float width, float height)
        {
            var area = AirportUI.Window("Animal quiz", 860, 560, width, height);
            GUILayout.BeginArea(area);
            if (quizIndex >= ZooQuiz.Questions.Length)
            {
                GUILayout.Label("You scored " + quizScore + " out of " + ZooQuiz.Questions.Length + ".", AirportUI.InkBig);
                GUILayout.Label(quizScore >= 4 ? "Excellent — animal quiz complete!" : "Read the information boards at the viewing decks and try again.", quizScore >= 4 ? AirportUI.Good : AirportUI.Ink);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", AirportUI.Primary)) Hide();
            }
            else
            {
                var question = ZooQuiz.Questions[quizIndex];
                GUILayout.Label("Question " + (quizIndex + 1) + " of " + ZooQuiz.Questions.Length, AirportUI.Muted);
                GUILayout.Label(question.Question, AirportUI.InkBig);
                GUILayout.Space(10);
                for (var i = 0; i < question.Answers.Length; i++)
                {
                    GUI.enabled = quizPicked < 0;
                    if (GUILayout.Button((quizPicked >= 0 && i == question.Correct ? "✔  " : quizPicked == i ? "✘  " : "    ") + question.Answers[i], AirportUI.Secondary, GUILayout.Height(52)) && quizPicked < 0)
                    {
                        quizPicked = i;
                        if (i == question.Correct) quizScore++;
                    }
                    GUI.enabled = true;
                }
                GUILayout.FlexibleSpace();
                if (quizPicked >= 0)
                {
                    GUILayout.Label(question.Why, quizPicked == question.Correct ? AirportUI.Good : AirportUI.Accent);
                    if (GUILayout.Button(quizIndex + 1 < ZooQuiz.Questions.Length ? "Next question" : "See my score", AirportUI.Primary))
                    {
                        quizIndex++;
                        quizPicked = -1;
                        if (quizIndex >= ZooQuiz.Questions.Length)
                        {
                            Progress.bestQuiz = Mathf.Max(Progress.bestQuiz, quizScore);
                            SaveManager.Save();
                            if (quizScore >= 4) ZooQuest.CompleteOptional("ZOO_QUIZ", 30);
                        }
                    }
                }
            }
            GUILayout.EndArea();
        }
    }
}
