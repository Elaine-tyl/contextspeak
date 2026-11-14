using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Arrival airport (Area J), dressed for the booked destination: arrival immigration → baggage
    /// claim (report a missing bag) → customs → arrival hall (tourist info, transport) → taxi rank →
    /// taxi ride → hotel reception. The destination itself is a small hotel lobby for now.
    public sealed class ArrivalFlow : AirportFlowBase
    {
        [Header("Scene wiring")]
        public Transform Spawn, HotelSpawn;
        public DoorController ImmigrationGate, CustomsGate;
        public Conveyor Carousel;
        public TextMesh[] DestinationSigns = new TextMesh[0];   // text may contain {city} {country} {airport} {greeting}
        public TextMesh ClaimBoard;
        public Bounds TaxiRank = new Bounds(new Vector3(0, 1, -40), new Vector3(20, 4, 8));
        public float BagDelay = 35;
        private float clearedAt = -1, rideTimer = -1;
        private bool bagOnBelt, demo, ridden, payCash;
        private string[] signTemplates = new string[0];

        private int FareLocal => Mathf.Max(10, Mathf.RoundToInt(18 * Booking.Where.Rate));
        private int FareCents => Mathf.RoundToInt(FareLocal / Booking.Where.Rate * 100);

        protected override string[] AmbientAnnouncements => new[]
        {
            "Welcome to " + Booking.Where.Airport + ". Please keep your belongings with you at all times.",
            "Baggage from flight " + Booking.flight + " is arriving on carousel 2.",
            "Taxis, trains and buses to " + Booking.Where.City + " city centre are on the ground floor.",
        };

        protected override void Start()
        {
            base.Start();
            MapBounds = new Rect(-60, -60, 120, 120);
            if (!Booking.booked || Booking.stage < (int)TravelStage.Flown)
            {
                demo = true;
                if (!Booking.booked) { FlightSchedule.Book(Booking, Destinations.Find("mel"), SkyState.Hour, 11); Booking.seat = "14A"; Booking.boardingPass = true; }
                Booking.bags = Mathf.Max(1, Booking.bags);
                if (Booking.bagTags.Count == 0) Booking.bagTags.Add(BaggageRules.Tag(Booking, 0));
                Booking.stage = (int)TravelStage.Flown;
                LifeUI.Toast("DEMO BOOKING — the arrival airport was opened without flying.");
            }
            if (Spawn != null) Player.Place(Spawn.position, Spawn.forward);
            // Local time at the destination: departure time plus the flight.
            var parts = Booking.departure.Split(':');
            if (SkySystem.Active != null && parts.Length == 2 && int.TryParse(parts[0], out var hour) && int.TryParse(parts[1], out var minute))
                SkySystem.Active.SetTime(hour + minute / 60f + Booking.Where.FlightHours);
            signTemplates = DestinationSigns.Select(sign => sign != null ? sign.text : "").ToArray();
            for (var i = 0; i < DestinationSigns.Length; i++)
                if (DestinationSigns[i] != null)
                    DestinationSigns[i].text = signTemplates[i].Replace("{city}", Booking.Where.City).Replace("{country}", Booking.Where.Country)
                        .Replace("{airport}", Booking.Where.Airport.ToUpperInvariant()).Replace("{greeting}", Booking.Where.Greeting).Replace("{code}", Booking.Where.Code).Replace("{hotel}", Booking.Where.Hotel);
            if (ClaimBoard != null) ClaimBoard.text = "BAGGAGE CLAIM\n" + Booking.flight + "  METRO CITY   2\nSL 119  SINGAPORE    1\nNW 404  DUBAI        3";
            if (ImmigrationGate != null) ImmigrationGate.Locked = Booking.stage < (int)TravelStage.ArrivalCleared;
            if (CustomsGate != null) CustomsGate.Locked = !Booking.Done("customs");
            if (Booking.stage >= (int)TravelStage.ArrivalCleared) clearedAt = Time.time - BagDelay;
            Objective = TravelQuest.Next;
        }

        protected override void Update()
        {
            base.Update();
            var player = Player.Transform;
            if (player == null) return;
            var delay = Booking.bagReported ? 8 : BagDelay;
            if (!bagOnBelt && clearedAt >= 0 && Time.time - clearedAt > delay && Booking.stage < (int)TravelStage.LuggageCollected) PutBagOnCarousel();
            if (Booking.stage == (int)TravelStage.LuggageCollected && Booking.Done("customs") && TaxiRank.Contains(player.position + Vector3.up))
            {
                Advance(TravelStage.FoundTaxi);
                Objective = "Talk to the taxi driver at the front of the rank.";
            }
            if (rideTimer > 0 && (rideTimer -= Time.deltaTime) <= 0)
            {
                Hide();
                if (HotelSpawn != null) Player.Place(HotelSpawn.position, HotelSpawn.forward);
                ridden = true;
                Objective = "Check in at the " + Booking.Where.Hotel + " reception.";
                LifeUI.Toast("You arrive at the " + Booking.Where.Hotel + ".");
            }
        }

        private void PutBagOnCarousel()
        {
            bagOnBelt = true;
            var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bag.name = "Your suitcase (" + (Booking.bagTags.FirstOrDefault() ?? "tag") + ")";
            bag.transform.localScale = new Vector3(0.45f, 0.28f, 0.7f);
            bag.GetComponent<Renderer>().material.color = new Color(0.15f, 0.3f, 0.6f);
            Destroy(bag.GetComponent<Collider>());
            var spot = bag.AddComponent<AirportSpot>();
            spot.Action = "collect";
            spot.Prompt = "Take your suitcase (tag " + (Booking.bagTags.FirstOrDefault() ?? "") + ")";
            spot.Range = 2.4f;
            if (Carousel != null) Carousel.Add(bag.transform);
            Announce("Baggage from SkyLark Air flight " + Booking.flight + " is now arriving on carousel 2.");
        }

        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            var stage = Booking.Stage;
            switch (npc.Key)
            {
                case "arrival":
                    if (stage == TravelStage.Flown) { Speak("Arrival Officer", npc, () => Show("stamp")); return; }
                    LifeUI.Toast(npc.DisplayName + ": Enjoy your stay!");
                    return;
                case "bagservice":
                    if (stage < TravelStage.ArrivalCleared || stage >= TravelStage.LuggageCollected) { LifeUI.Toast(npc.DisplayName + ": Baggage services — let us know if anything is missing."); return; }
                    Speak("Baggage Services", npc, () =>
                    {
                        Booking.bagReported = true;
                        InventoryManager.Add("pir", "Baggage report reference " + Booking.Where.Code + "-" + Booking.bookingRef);
                        LifeUI.Toast(npc.DisplayName + ": Found it — it was on the next cart. It'll be on carousel 2 in a moment.");
                    });
                    return;
                case "customs":
                    if (stage < TravelStage.LuggageCollected) { LifeUI.Toast(npc.DisplayName + ": Please collect your luggage first."); return; }
                    Speak("Customs Officer", npc, () =>
                    {
                        Booking.Mark("customs");
                        if (CustomsGate != null) CustomsGate.Locked = false;
                        Objective = "Go out to the arrival hall and find the taxi rank.";
                        SaveManager.Save();
                    });
                    return;
                case "tourist": Speak("Tourist Information", npc, null); return;
                case "taxidriver":
                    if (stage == TravelStage.FoundTaxi && !ridden) { Show("taxi"); return; }
                    LifeUI.Toast(npc.DisplayName + ": " + (stage < TravelStage.FoundTaxi ? "Taxis are this way when you're ready!" : "Enjoy your stay!"));
                    return;
                case "hotel":
                    if (stage == TravelStage.FoundTaxi && ridden) { Speak("Hotel Reception", npc, () => { InventoryManager.Add("room-key", "Room key card · " + Booking.Where.Hotel); Advance(TravelStage.AtHotel); Show("complete"); }); return; }
                    LifeUI.Toast(npc.DisplayName + ": Welcome! Let me know if you need anything.");
                    return;
                default: LifeUI.Toast(npc.DisplayName + ": Hello, welcome to " + Booking.Where.City + "!"); return;
            }
        }

        public override void Use(string action)
        {
            switch (action)
            {
                case "collect":
                    if (!Advance(TravelStage.LuggageCollected)) { LifeUI.Toast("That's not yet possible."); return; }
                    var bag = FindObjectsByType<AirportSpot>(FindObjectsSortMode.None).FirstOrDefault(spot => spot.Action == "collect");
                    if (bag != null) Destroy(bag.gameObject);
                    InventoryManager.Add("suitcase", "Suitcase");
                    Objective = "Go through customs (talk to the officer).";
                    return;
                case "claimboard": Show("claim"); return;
                case "transport": LifeUI.Toast("To the city: TAXI (left, 25 min) · EXPRESS TRAIN (every 15 min) · BUS 901 (40 min)"); return;
            }
        }

        protected override bool CanClose(string panel) => panel == "map" || panel == "claim";

        protected override void DrawPanel(string panel, float width, float height)
        {
            var where = Booking.Where;
            switch (panel)
            {
                case "stamp":
                {
                    var area = AirportUI.Window("Arrival immigration (simulation)", 760, 480, width, height);
                    GUILayout.BeginArea(area);
                    AirportUI.Row("Name", SaveManager.PlayerName.ToUpperInvariant());
                    AirportUI.Row("Arriving from", "Metro City · " + Booking.flight);
                    AirportUI.Row("Purpose / stay", Booking.purpose + " · " + Booking.stayDays + " days");
                    AirportUI.Row("Address", where.Hotel);
                    GUILayout.Label("ADMITTED · " + where.Code + " · " + SkyState.Clock + " ✔", AirportUI.Good);
                    GUILayout.Label("Fictional scenario for language practice — not official entry guidance.", AirportUI.Muted);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Take your passport", AirportUI.Primary))
                    {
                        Advance(TravelStage.ArrivalCleared);
                        clearedAt = Time.time;
                        if (ImmigrationGate != null) ImmigrationGate.Locked = false;
                        Hide();
                        Objective = "Find carousel 2 and collect your suitcase.";
                    }
                    GUILayout.EndArea();
                    break;
                }
                case "claim":
                {
                    var box = new Rect(width / 2 - 420, height / 2 - 220, 840, 440);
                    GUI.Box(box, GUIContent.none, AirportUI.Board);
                    GUILayout.BeginArea(new Rect(box.x + 24, box.y + 18, box.width - 48, box.height - 36));
                    GUILayout.Label("BAGGAGE CLAIM", AirportUI.Amber);
                    GUILayout.Label("FLIGHT   FROM          BELT  STATUS", AirportUI.BoardText);
                    GUILayout.Label(Booking.flight.PadRight(9) + "METRO CITY    2     " + (bagOnBelt ? "ON BELT" : clearedAt >= 0 ? "EXPECTED" : "LANDED"), AirportUI.Amber);
                    GUILayout.Label("SL 119   SINGAPORE     1     LAST BAGS", AirportUI.BoardText);
                    GUILayout.Label("NW 404   DUBAI         3     ON BELT", AirportUI.BoardText);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Your tag: " + string.Join(", ", Booking.bagTags) + "   ·   Missing a bag? Baggage services is next to belt 3.", AirportUI.BoardText);
                    if (GUILayout.Button("Close", AirportUI.Primary)) Hide();
                    GUILayout.EndArea();
                    break;
                }
                case "taxi":
                {
                    var area = AirportUI.Window("Taxi booking", 760, 540, width, height);
                    GUILayout.BeginArea(area);
                    AirportUI.Row("From", where.Airport);
                    AirportUI.Row("To", where.Hotel + ", " + where.City);
                    AirportUI.Row("Time", "about 25 minutes");
                    AirportUI.Row("Fare (fixed)", where.Currency + " " + FareLocal + "  (≈ " + CurrencyManager.Format(FareCents) + ")");
                    AirportUI.Row("Your cash", where.Currency + " " + Booking.foreignCash.ToString("N0"));
                    payCash = GUILayout.Toggle(payCash && Booking.foreignCash >= FareLocal, " Pay in " + where.CurrencyName + " cash", AirportUI.Ink);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Book this taxi", AirportUI.Primary))
                    {
                        Hide();
                        Speak("Taxi Rank", Npc("taxidriver"), () =>
                        {
                            if (payCash && Booking.foreignCash >= FareLocal) { Booking.foreignCash -= FareLocal; StartRide(); }
                            else PaymentController.Instance.Show("Taxi to " + where.Hotel, "Fixed fare " + where.Currency + " " + FareLocal, FareCents, StartRide, () => Show("taxi"));
                        });
                    }
                    GUILayout.EndArea();
                    break;
                }
                case "ride":
                {
                    var box = new Rect(0, 0, width, height);
                    GUI.Box(box, GUIContent.none, AirportUI.Board);
                    GUI.Label(new Rect(0, height / 2 - 60, width, 120), "Driving into " + where.City + "…\n<size=22>past " + where.Landmark + "</size>", new GUIStyle(AirportUI.Amber) { alignment = TextAnchor.MiddleCenter, fontSize = 40 });
                    break;
                }
                case "complete":
                {
                    var area = AirportUI.Window("Trip complete!", 820, 560, width, height);
                    GUILayout.BeginArea(area);
                    GUILayout.Label("You travelled from Metro City to " + where.City + " entirely in English.", AirportUI.InkBig);
                    GUILayout.Label("Quest " + TravelQuest.Id + " · " + TravelQuest.Title + " ✔  +250 XP", AirportUI.Good);
                    GUILayout.Label("Conversations completed: " + Booking.completed.Count, AirportUI.Ink);
                    GUILayout.Label(where.City + " itself is a small hotel lobby for now — more of the city is planned.", AirportUI.Muted);
                    if (demo) GUILayout.Label("Demo booking was used in this scene.", AirportUI.Bad);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Fly home to Metro City", AirportUI.Primary)) { Hide(); if (!SceneTransitionManager.Leave("MetroCity")) LifeUI.Toast(SceneTransitionManager.LastError); }
                    if (GUILayout.Button("Plan another trip (resets this journey)", AirportUI.Secondary)) { TravelQuest.Restart(); Hide(); if (!SceneTransitionManager.Leave("MetroCity")) LifeUI.Toast(SceneTransitionManager.LastError); }
                    if (GUILayout.Button("Stay here", AirportUI.Secondary)) Hide();
                    GUILayout.EndArea();
                    break;
                }
            }
        }

        private void StartRide()
        {
            SaveManager.Save();
            rideTimer = 4;
            Show("ride");
        }
    }
}
