using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Departure airport (Areas A–H + airfield view): arrive → departure hall → flight board →
    /// booking kiosk → check-in agent → seat selection → baggage scale/fees/tags → security trays
    /// → passport control → shops, café, restaurant, exchange, lounge → gate → boarding.
    public sealed class DepartureFlow : AirportFlowBase
    {
        [Header("Scene wiring")]
        public Transform TrainSpawn, TaxiSpawn;
        public Bounds DepartureHall = new Bounds(new Vector3(0, 5, -15), new Vector3(100, 12, 30));
        public Transform[] Gates = new Transform[0];             // named G1, G2, G3
        public TextMesh[] FlightBoards = new TextMesh[0];
        public TextMesh[] GateMonitors = new TextMesh[0];        // same order as Gates
        public TextMesh ScaleReadout;
        public Conveyor CheckInBelt;
        public Transform BeltStart;
        public DoorController SecurityGate, PassportGate;
        public DoorController[] JetBridgeDoors = new DoorController[0];   // same order as Gates
        public float BoardingDelay = 40f;

        private bool boardingOpen;
        private float boardingTimer = -1;
        private int bagsChosen = 1;
        private float carryOn = 5.5f;
        private readonly List<SecurityCheck.Place> trays = new List<SecurityCheck.Place>();
        private List<string> securityProblems = new List<string>();
        private bool scanned, beeped;
        private int bookingChoice = 2, purposeChoice, stayChoice;
        private static readonly string[] Purposes = { "holiday", "study", "business", "family" };
        private static readonly int[] Stays = { 7, 10, 14 };
        private int exchangeChoice = 1;
        private static readonly int[] ExchangeAmounts = { 2000, 5000, 10000 };
        private string lastReceipt = "";

        protected override string[] AmbientAnnouncements => new[]
        {
            "Please keep your belongings with you at all times. Unattended baggage may be removed.",
            "SkyLark Air flight SL 405 to Bangkok is delayed by about twenty minutes. We apologise for the inconvenience.",
            "Passengers are reminded that smoking is only permitted in the designated outdoor areas.",
            "Welcome to Metro City International. Free wi-fi is available throughout the terminal.",
        };

        protected override void Start()
        {
            base.Start();
            MapBounds = new Rect(-130, -95, 260, 260);
            if (InventoryManager.Count("passport") == 0) InventoryManager.Add("passport", "Passport (fictional)");
            if (Booking.stage == 0) Advance(TravelStage.AtAirport);
            var byTaxi = SaveManager.Data.returnEntrance.Contains("taxi");
            var spawn = byTaxi ? TaxiSpawn : TrainSpawn;
            if (spawn != null) Player.Place(spawn.position, spawn.forward);
            for (var i = 0; i < JetBridgeDoors.Length; i++) if (JetBridgeDoors[i] != null) JetBridgeDoors[i].Locked = true;
            if (SecurityGate != null) SecurityGate.Locked = Booking.stage < (int)TravelStage.SecurityCleared;
            if (PassportGate != null) PassportGate.Locked = Booking.stage < (int)TravelStage.PassportControlCleared;
            for (var i = 0; i < SecurityCheck.Items.Length; i++) trays.Add(SecurityCheck.Place.Bag);
            if (!Booking.booked) Objective = "Book a flight at a self-service kiosk (departure hall), then: " + TravelQuest.Next;
            RefreshBoards();
        }

        protected override void Update()
        {
            base.Update();
            var player = Player.Transform;
            if (player == null) return;
            if (Booking.stage == (int)TravelStage.AtAirport && DepartureHall.Contains(player.position + Vector3.up)) Advance(TravelStage.InDepartureHall);
            if (Booking.stage == (int)TravelStage.PassportControlCleared)
            {
                var gate = Gate(Booking.gate);
                if (gate != null && Vector3.Distance(gate.position, player.position) < 10)
                {
                    Advance(TravelStage.FoundGate);
                    boardingTimer = BoardingDelay;
                    if (Booking.Done("drink")) Advance(TravelStage.BoughtDrink);
                    Show("gate");
                }
            }
            if (boardingTimer > 0 && (boardingTimer -= Time.deltaTime) <= 0)
            {
                boardingOpen = true;
                Announce("SkyLark Air flight " + Booking.flight + " to " + Booking.Where.City + " is now boarding at gate " + Booking.gate + ". Please have your boarding pass and passport ready.");
                RefreshBoards();
            }
        }

        private Transform Gate(string id) => Gates.FirstOrDefault(gate => gate != null && gate.name == id);

        private string Status => !Booking.booked ? "" : boardingOpen ? "BOARDING" : Booking.stage >= (int)TravelStage.FoundGate ? "GATE OPEN" : "ON TIME";

        private void RefreshBoards()
        {
            var rows = AirportUI.Departures(Booking, Status);
            var text = "DEPARTURES                    " + SkyState.Clock + "\n" + string.Join("\n", rows.Select(row => row[0] + "  " + row[1].PadRight(7) + row[2].PadRight(12) + row[3].PadRight(4) + row[4]));
            foreach (var board in FlightBoards) if (board != null) board.text = text;
            for (var i = 0; i < GateMonitors.Length && i < Gates.Length; i++)
            {
                if (GateMonitors[i] == null || Gates[i] == null) continue;
                var mine = Booking.booked && Booking.gate == Gates[i].name;
                GateMonitors[i].text = "GATE " + Gates[i].name + "\n" + (mine ? Booking.flight + "  " + Booking.Where.City.ToUpperInvariant() + "\nDEP " + Booking.departure + "  BOARD " + FlightSchedule.BoardingTime(Booking) + "\n" + (boardingOpen ? "NOW BOARDING" : "PLEASE WAIT") : "SL " + (300 + i * 7) + "  SEOUL\nDEP LATER\nGATE CLOSED");
            }
        }

        // ------------------------------------------------------------------ NPCs
        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            var stage = Booking.Stage;
            switch (npc.Key)
            {
                case "taxi": Speak("Airport Taxi Drop-off", npc, () => { if (InventoryManager.Count("suitcase") == 0) InventoryManager.Add("suitcase", "Suitcase"); }); return;
                case "info": Speak("Airport Information", npc, null); return;
                case "tutor": Speak("IELTS Travel Practice", npc, () => LifeUI.Toast("IELTS practice complete · +20 XP")); return;
                case "pilot": Speak("Pilot Greeting", npc, null); return;
                case "lounge": Speak("Lounge Host", npc, null); return;
                case "checkin":
                    if (!Booking.booked) { Say(npc, "You don't have a booking yet. You can book a flight at the self-service kiosks behind you."); return; }
                    if (stage < TravelStage.CheckedFlightInfo) { Say(npc, "Before we start, please check your flight on the departures board — it's just over there."); return; }
                    if (stage == TravelStage.CheckedFlightInfo) { Speak("Check-in Agent", npc, () => { Advance(TravelStage.TalkedToCheckIn); Show("checkin"); }); return; }
                    if (stage == TravelStage.TalkedToCheckIn) { Show(Booking.seat == "" ? "checkin" : "baggage"); return; }
                    Show("boardingpass");
                    return;
                case "security":
                    if (stage < TravelStage.LuggageCheckedIn) { Say(npc, "Please check in first — you'll need a boarding pass to come through security."); return; }
                    if (stage == TravelStage.LuggageCheckedIn) { Speak("Security Officer", npc, () => { scanned = beeped = false; securityProblems.Clear(); Show("security"); }); return; }
                    Say(npc, "You're all clear. Have a good flight.");
                    return;
                case "passport":
                    if (stage < TravelStage.SecurityCleared) { Say(npc, "Security first, please — then come back to passport control."); return; }
                    if (stage == TravelStage.SecurityCleared) { Speak("Passport Control", npc, () => Show("passport")); return; }
                    Say(npc, "Enjoy your trip.");
                    return;
                case "dutyfree": Shop(AirportMenus.DutyFree(), npc, "Duty-Free Cashier", cart => LifeUI.Toast("Duty-free bag added to your inventory.")); return;
                case "books": Shop(AirportMenus.Books(), npc, "Duty-Free Cashier", null); return;
                case "souvenir": Shop(AirportMenus.Souvenirs(), npc, "Duty-Free Cashier", null); return;
                case "convenience": Shop(AirportMenus.Convenience(), npc, "Duty-Free Cashier", cart => { if (cart.Lines.Any(line => line.id == "water")) Drink(); }); return;
                case "barista": Shop(AirportMenus.Coffee(), npc, "Barista Order", cart => Drink()); return;
                case "waiter":
                    var seat = InteractionManager.Instance.Seat;
                    if (seat == null || seat.Group != "airport-dining") { Say(npc, "Please take any free seat in the restaurant and I'll bring you a menu."); return; }
                    Shop(AirportMenus.Dining(), npc, "Airport Restaurant", cart => Serve(cart, seat));
                    return;
                case "exchange": Speak("Currency Exchange", npc, () => Show("exchange")); return;
                default:
                    if (npc.Key.StartsWith("gate-")) { GateAgent(npc, npc.Key.Substring(5)); return; }
                    Say(npc, npc.Role == "Cleaner" ? "Careful, the floor's a bit wet just here!" : npc.Role == "Ground staff" ? "Busy morning on the apron!" : "Hi! Safe travels.");
                    return;
            }
        }

        private static void Say(NPCController npc, string line)
        {
            LifeUI.Toast(npc.DisplayName + ": " + line);
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.Speech.Speak(line);
        }

        private void Drink()
        {
            Booking.Mark("drink");
            if (Booking.stage == (int)TravelStage.FoundGate) Advance(TravelStage.BoughtDrink);
            SaveManager.Save();
        }

        private void Serve(Cart cart, SeatController seat)
        {
            var spot = seat.transform.position + seat.transform.forward * 0.55f + Vector3.up * 0.78f;
            var index = 0;
            foreach (var line in cart.Lines)
                for (var count = 0; count < line.quantity; count++, index++)
                {
                    var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(plate.GetComponent<Collider>());
                    plate.name = line.name;
                    plate.transform.position = spot + seat.transform.right * ((index % 3) - 1) * 0.3f;
                    plate.transform.localScale = new Vector3(0.24f, 0.01f, 0.24f);
                    var food = plate.AddComponent<FoodItem>();
                    food.DisplayName = line.name;
                    food.Drink = line.name.Contains("Water") || line.name.Contains("Lemonade") || line.name.Contains("Cola");
                }
            if (cart.Lines.Any(line => line.name.Contains("Water") || line.name.Contains("Lemonade") || line.name.Contains("Cola"))) Drink();
        }

        private void GateAgent(NPCController npc, string gate)
        {
            var check = BoardingRules.Validate(Booking, gate, boardingOpen && gate == Booking.gate);
            if (check != BoardingCheck.Valid) { Say(npc, BoardingRules.Explain(check, Booking)); return; }
            if (Booking.stage < (int)TravelStage.BoughtDrink)
            {
                Say(npc, "Boarding's just started and it's a long walk to your seat — grab a drink from Skyline Coffee or QuickStop first, there's time.");
                return;
            }
            Speak("Gate Agent", npc, () => Show("scan"));
        }

        // ------------------------------------------------------------------ kiosks and boards
        public override void Use(string action)
        {
            switch (action)
            {
                case "kiosk": Show(Booking.booked ? "boardingpass" : "booking"); return;
                case "board":
                    RefreshBoards();
                    Show("flights");
                    return;
                case "restroom": LifeUI.Toast("Restrooms (placeholder — no interior)."); return;
                case "egate": LifeUI.Toast("E-gates are for Metro City residents arriving home. Please see an officer at the desks."); return;
                case "trolley": LifeUI.Toast("You take a baggage trolley."); return;
            }
        }

        protected override bool CanClose(string panel) => panel == "map" || panel == "flights" || panel == "boardingpass" || panel == "gate" || panel == "booking" || panel == "exchange";

        protected override void DrawPanel(string panel, float width, float height)
        {
            switch (panel)
            {
                case "booking": FlightBookingUI(width, height); break;
                case "flights": FlightInformationBoard(width, height); break;
                case "checkin": CheckInUI(width, height); break;
                case "baggage": BaggageCheckUI(width, height); break;
                case "boardingpass": BoardingPassUI(width, height, false); break;
                case "security": SecurityCheckpointUI(width, height); break;
                case "passport": PassportControlUI(width, height); break;
                case "gate": GateInformationUI(width, height); break;
                case "scan": BoardingPassUI(width, height, true); break;
                case "exchange": ExchangeUI(width, height); break;
            }
        }

        private void FlightBookingUI(float width, float height)
        {
            var area = AirportUI.Window("Book a flight  ·  self-service", 980, 760, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("Where would you like to go? (Fictional flights and fares.)", AirportUI.Ink);
            for (var i = 0; i < Destinations.All.Length; i++)
            {
                var destination = Destinations.All[i];
                var label = destination.City + ", " + destination.Country + "   ·   " + FlightSchedule.FlightNumber(destination) + "   ·   " + destination.FlightHours + " h   ·   " + CurrencyManager.Format(destination.Fare);
                if (GUILayout.Toggle(bookingChoice == i, "  " + label, AirportUI.Secondary)) bookingChoice = i;
            }
            GUILayout.Space(8);
            GUILayout.Label("Purpose of the trip", AirportUI.Muted);
            purposeChoice = GUILayout.Toolbar(purposeChoice, Purposes, AirportUI.Secondary);
            GUILayout.Label("Length of stay", AirportUI.Muted);
            stayChoice = GUILayout.Toolbar(stayChoice, Stays.Select(days => days + " days").ToArray(), AirportUI.Secondary);
            GUILayout.FlexibleSpace();
            var destinationChoice = Destinations.All[bookingChoice];
            if (!Booking.scholarship) GUILayout.Label("First trip: your ContextSpeak travel grant adds " + CurrencyManager.Format(100000) + " to your wallet.", AirportUI.Accent);
            GUILayout.Label("Wallet " + CurrencyManager.Balance + "   ·   Economy fare " + CurrencyManager.Format(destinationChoice.Fare), AirportUI.InkBig);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Book and pay", AirportUI.Primary))
            {
                if (!Booking.scholarship) { CurrencyManager.Add(100000); Booking.scholarship = true; }
                if (CurrencyManager.TrySpend(destinationChoice.Fare, out var message))
                {
                    FlightSchedule.Book(Booking, destinationChoice, SkyState.Hour, Environment.TickCount);
                    Booking.purpose = Purposes[purposeChoice];
                    Booking.stayDays = Stays[stayChoice];
                    InventoryManager.Add("e-ticket", "E-ticket " + Booking.flight + " to " + destinationChoice.City);
                    SaveManager.Save();
                    Objective = TravelQuest.Next;
                    RefreshBoards();
                    LifeUI.Toast("Booked " + Booking.flight + " to " + destinationChoice.City + " · ref " + Booking.bookingRef);
                    Hide();
                }
                else LifeUI.Toast(message);
            }
            if (GUILayout.Button("Close", AirportUI.Secondary, GUILayout.Width(160))) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void FlightInformationBoard(float width, float height)
        {
            var box = new Rect(width / 2 - 520, height / 2 - 300, 1040, 600);
            GUI.Box(box, GUIContent.none, AirportUI.Board);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 18, box.width - 48, box.height - 36));
            GUILayout.Label("DEPARTURES                                             " + SkyState.Clock, AirportUI.Amber);
            GUILayout.Label("TIME   FLIGHT   DESTINATION      GATE  STATUS", AirportUI.BoardText);
            foreach (var row in AirportUI.Departures(Booking, Status))
            {
                var mine = Booking.booked && row[1] == Booking.flight;
                GUILayout.Label(row[0] + "  " + row[1].PadRight(8) + row[2].PadRight(17) + row[3].PadRight(6) + row[4], mine ? AirportUI.Amber : AirportUI.BoardText);
            }
            GUILayout.FlexibleSpace();
            if (!Booking.booked) GUILayout.Label("You have no booking yet — use a self-service kiosk.", AirportUI.BoardText);
            else GUILayout.Label("Your flight " + Booking.flight + " to " + Booking.Where.City + " departs " + Booking.departure + " from gate " + Booking.gate + ". Check in at SkyLark Air, row A.", AirportUI.Amber);
            if (GUILayout.Button(Booking.booked && Booking.stage == (int)TravelStage.InDepartureHall ? "Got it" : "Close", AirportUI.Primary))
            {
                if (Booking.booked && Booking.stage == (int)TravelStage.InDepartureHall) Advance(TravelStage.CheckedFlightInfo);
                Hide();
            }
            GUILayout.EndArea();
        }

        // CheckInUI + AirlineSeatSelectionUI
        private void CheckInUI(float width, float height)
        {
            var area = AirportUI.Window("SkyLark Air check-in", 1180, 820, width, height);
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(420));
            AirportUI.Row("Passenger", SaveManager.PlayerName);
            AirportUI.Row("Passport", "Fictional · checked ✔", AirportUI.Good);
            AirportUI.Row("Booking ref", Booking.bookingRef);
            AirportUI.Row("Flight", Booking.flight + " · " + Booking.airline);
            AirportUI.Row("Destination", Booking.Where.City + " (" + Booking.Where.Code + ")");
            AirportUI.Row("Departure", Booking.departure + " · gate " + Booking.gate);
            AirportUI.Row("Boarding", FlightSchedule.BoardingTime(Booking));
            AirportUI.Row("Allowance", "1 × " + BaggageRules.Allowance + " kg checked + " + BaggageRules.CarryOnLimit + " kg carry-on");
            AirportUI.Row("Seat", Booking.seat == "" ? "choose →" : Booking.seat, Booking.seat == "" ? AirportUI.Bad : AirportUI.Good);
            GUILayout.EndVertical();
            GUILayout.Space(20);
            GUILayout.BeginVertical();
            GUILayout.Label("Choose your seat (window = A/F, aisle = C/D)", AirportUI.Muted);
            var taken = new System.Random(Booking.bookingRef.GetHashCode());
            for (var row = 10; row <= 24; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(row.ToString(), AirportUI.Muted, GUILayout.Width(30));
                foreach (var letter in "ABCDEF")
                {
                    if (letter == 'D') GUILayout.Space(22);
                    var id = row.ToString() + letter;
                    var occupied = taken.NextDouble() < 0.45 && id != Booking.seat;
                    GUI.enabled = !occupied;
                    if (GUILayout.Toggle(Booking.seat == id, occupied ? "×" : letter.ToString(), AirportUI.Secondary, GUILayout.Width(44), GUILayout.Height(30))) Booking.seat = id;
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = Booking.seat != "";
            if (GUILayout.Button("Confirm seat and continue to baggage", AirportUI.Primary)) { SaveManager.Save(); Show("baggage"); }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void BaggageCheckUI(float width, float height)
        {
            var area = AirportUI.Window("Baggage drop", 880, 700, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("How many bags are you checking in?", AirportUI.Ink);
            bagsChosen = GUILayout.Toolbar(bagsChosen - 1, new[] { "1 bag", "2 bags" }, AirportUI.Secondary) + 1;
            for (var i = 0; i < bagsChosen; i++)
            {
                var weight = Booking.bagWeights[i];
                var style = weight > BaggageRules.Limit ? AirportUI.Bad : weight > BaggageRules.Allowance ? AirportUI.Accent : AirportUI.Good;
                AirportUI.Row("Bag " + (i + 1) + " on the scale", weight.ToString("0.0") + " kg" + (weight > BaggageRules.Allowance ? "  (over " + BaggageRules.Allowance + " kg)" : "  ✔"), style);
            }
            if (ScaleReadout != null) ScaleReadout.text = Booking.bagWeights[0].ToString("0.0") + " kg";
            AirportUI.Row("Carry-on", carryOn.ToString("0.0") + " / " + BaggageRules.CarryOnLimit + " kg");
            var fee = BaggageRules.Fee(Booking.bagWeights, bagsChosen) - Booking.excessPaid;
            AirportUI.Divider();
            AirportUI.Row("Excess baggage fee", fee > 0 ? CurrencyManager.Format(fee) : "none", fee > 0 ? AirportUI.Bad : AirportUI.Good);
            GUILayout.Label("Fictional fees: " + CurrencyManager.Format(BaggageRules.PerKilo) + " per kg over " + BaggageRules.Allowance + " kg, " + CurrencyManager.Format(BaggageRules.ExtraBag) + " for a second bag.", AirportUI.Muted);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var over = Booking.bagWeights[0] - BaggageRules.Allowance;
            GUI.enabled = over > 0 && carryOn + 0.5f <= BaggageRules.CarryOnLimit;
            if (GUILayout.Button("Repack: move items to carry-on", AirportUI.Secondary))
            {
                var move = Mathf.Min(over, BaggageRules.CarryOnLimit - carryOn);
                move = Mathf.Ceil(move * 10) / 10f;
                Booking.bagWeights[0] = Mathf.Round((Booking.bagWeights[0] - move) * 10) / 10f;
                carryOn += move;
            }
            GUI.enabled = fee > 0;
            if (GUILayout.Button("Pay " + CurrencyManager.Format(Mathf.Max(0, fee)), AirportUI.Secondary))
            {
                Hide();
                PaymentController.Instance.Show("Excess baggage", bagsChosen + " bag(s)", fee, () => { Booking.excessPaid += fee; Show("baggage"); }, () => Show("baggage"));
            }
            GUI.enabled = fee <= 0 && BaggageRules.Acceptable(Booking.bagWeights, bagsChosen);
            if (GUILayout.Button("Confirm bags", AirportUI.Primary))
            {
                Booking.bags = bagsChosen;
                Hide();
                Speak("Baggage Questions", Npc("checkin"), DropBags);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DropBags()
        {
            Booking.bagTags.Clear();
            for (var i = 0; i < Booking.bags; i++)
            {
                var tag = BaggageRules.Tag(Booking, i);
                Booking.bagTags.Add(tag);
                InventoryManager.Add("bag-tag", "Baggage claim tag " + tag);
                var suitcase = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(suitcase.GetComponent<Collider>());
                suitcase.name = "Your suitcase " + tag;
                suitcase.transform.localScale = new Vector3(0.45f, 0.7f, 0.28f);
                suitcase.GetComponent<Renderer>().material.color = i == 0 ? new Color(0.15f, 0.3f, 0.6f) : new Color(0.55f, 0.1f, 0.12f);
                if (CheckInBelt != null) CheckInBelt.Add(suitcase.transform); else Destroy(suitcase, 0.1f);
            }
            InventoryManager.Remove("suitcase");
            Booking.checkedIn = Booking.boardingPass = true;
            InventoryManager.Add("boarding-pass", "Boarding pass " + Booking.flight + " seat " + Booking.seat);
            Advance(TravelStage.LuggageCheckedIn);
            if (SecurityGate != null) SecurityGate.Locked = true;
            Show("boardingpass");
        }

        private void BoardingPassUI(float width, float height, bool scan)
        {
            var box = new Rect(width / 2 - 470, height / 2 - 190, 940, 380);
            AirportUI.BoardingPass(box, Booking, SaveManager.PlayerName, scan);
            GUILayout.BeginArea(new Rect(box.x, box.yMax + 14, box.width, 60));
            GUILayout.BeginHorizontal();
            if (scan)
            {
                if (GUILayout.Button("Walk to the jet bridge", AirportUI.Primary))
                {
                    Advance(TravelStage.Boarded);
                    var index = Array.FindIndex(Gates, gate => gate != null && gate.name == Booking.gate);
                    if (index >= 0 && index < JetBridgeDoors.Length && JetBridgeDoors[index] != null) JetBridgeDoors[index].Locked = false;
                    Hide();
                }
            }
            else if (GUILayout.Button(Booking.stage == (int)TravelStage.LuggageCheckedIn ? "Go to security (follow the signs)" : "Close", AirportUI.Primary)) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SecurityCheckpointUI(float width, float height)
        {
            var area = AirportUI.Window("Security — trays (fictional game rules)", 1000, 800, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("Where does each item go? Then send the trays through the X-ray and walk through the arch.", AirportUI.Ink);
            var names = Enum.GetNames(typeof(SecurityCheck.Place));
            for (var i = 0; i < SecurityCheck.Items.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(SecurityCheck.Items[i].item, AirportUI.Ink, GUILayout.Width(240));
                trays[i] = (SecurityCheck.Place)GUILayout.Toolbar((int)trays[i], names, AirportUI.Secondary);
                GUILayout.EndHorizontal();
            }
            AirportUI.Divider();
            if (beeped) GUILayout.Label("BEEP! The arch went off — you still have metal in a pocket.", AirportUI.Bad);
            foreach (var problem in securityProblems) GUILayout.Label("• " + problem, AirportUI.Bad);
            if (scanned && securityProblems.Count == 0) GUILayout.Label("✔ Bags and trays clear · arch clear. Collect your things.", AirportUI.Good);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (!scanned || securityProblems.Count > 0)
            {
                if (GUILayout.Button("Send trays through the X-ray and walk through", AirportUI.Primary))
                {
                    scanned = true;
                    securityProblems = SecurityCheck.Problems(trays);
                    beeped = SecurityCheck.ArchBeeps(trays);
                    if (securityProblems.Count == 0) AirportAudio.Instance?.ScannerOk(); else AirportAudio.Instance?.ScannerAlarm();
                }
            }
            else if (GUILayout.Button("Collect your items", AirportUI.Primary))
            {
                Advance(TravelStage.SecurityCleared);
                if (SecurityGate != null) SecurityGate.Locked = false;
                Hide();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void PassportControlUI(float width, float height)
        {
            var area = AirportUI.Window("Departure passport control (simulation)", 820, 600, width, height);
            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(AirportUI.Card);
            GUILayout.Label("PASSPORT  ·  METRO CITY (FICTIONAL)", AirportUI.Muted);
            AirportUI.Row("Name", SaveManager.PlayerName.ToUpperInvariant());
            AirportUI.Row("Passport no.", "MC" + Mathf.Abs(Booking.bookingRef.GetHashCode() % 9000000 + 1000000));
            AirportUI.Row("Travelling to", Booking.Where.City + ", " + Booking.Where.Country);
            AirportUI.Row("Purpose / stay", Booking.purpose + " · " + Booking.stayDays + " days");
            GUILayout.EndVertical();
            GUILayout.Label("DEPARTED · METRO CITY INTL · " + SkyState.Clock + " ✔", AirportUI.Good);
            GUILayout.Label("This is a language-practice simulation, not official border-control guidance.", AirportUI.Muted);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Take your passport", AirportUI.Primary))
            {
                Advance(TravelStage.PassportControlCleared);
                if (PassportGate != null) PassportGate.Locked = false;
                Hide();
                LifeUI.Toast("Gate " + Booking.gate + " is down the concourse past the shops.");
            }
            GUILayout.EndArea();
        }

        private void GateInformationUI(float width, float height)
        {
            var area = AirportUI.Window("Gate " + Booking.gate + "  ·  " + Booking.flight, 760, 500, width, height);
            GUILayout.BeginArea(area);
            AirportUI.Row("Destination", Booking.Where.City);
            AirportUI.Row("Departs", Booking.departure);
            AirportUI.Row("Boarding", FlightSchedule.BoardingTime(Booking) + (boardingOpen ? "  · NOW BOARDING" : "  · wait for the announcement"), boardingOpen ? AirportUI.Good : AirportUI.Ink);
            AirportUI.Row("Your seat", Booking.seat);
            GUILayout.Label(Booking.Done("drink") ? "You've got your drink — relax until boarding." : "There's time to buy a drink at Skyline Coffee or QuickStop.", AirportUI.Accent);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK", AirportUI.Primary)) Hide();
            GUILayout.EndArea();
        }

        private void ExchangeUI(float width, float height)
        {
            var destination = Booking.Where;
            var area = AirportUI.Window("Currency exchange", 720, 520, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("Today's rate: 1 : " + destination.Rate.ToString("0.###") + " " + destination.CurrencyName + " · commission " + (CurrencyExchange.Commission * 100) + "% (fictional)", AirportUI.Ink);
            exchangeChoice = GUILayout.Toolbar(exchangeChoice, ExchangeAmounts.Select(CurrencyManager.Format).ToArray(), AirportUI.Secondary);
            var amount = ExchangeAmounts[exchangeChoice];
            GUILayout.Label("You get " + destination.Currency + " " + CurrencyExchange.Foreign(amount, destination).ToString("N0"), AirportUI.InkBig);
            if (lastReceipt != "") GUILayout.Label(lastReceipt, AirportUI.Good);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Exchange", AirportUI.Primary))
            {
                if (CurrencyManager.TrySpend(amount, out var message))
                {
                    Booking.foreignCash += CurrencyExchange.Foreign(amount, destination);
                    lastReceipt = CurrencyExchange.Receipt(amount, destination);
                    InventoryManager.Add("fx-receipt", "Exchange receipt (" + destination.CurrencyName + ")");
                    SaveManager.Save();
                }
                else lastReceipt = message;
            }
            if (GUILayout.Button("Done", AirportUI.Secondary, GUILayout.Width(140))) { lastReceipt = ""; Hide(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
