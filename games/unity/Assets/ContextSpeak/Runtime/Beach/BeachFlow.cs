using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Palm Bay resort gameplay: BEACH_DAY_001 and the optional quests, the café and juice bar,
    /// Coral Kitchen restaurant, the watersports desk (booking, safety briefing, activities), the
    /// marina, the hotel (reservation, check-in, room, key, sleep, room service, check-out),
    /// the sunset viewpoint and the IELTS conversations. Reuses the airport's panel/shop framework.
    public sealed class BeachFlow : AirportFlowBase
    {
        public override string QuestId => BeachQuest.Id;
        public override string QuestTitle => BeachQuest.Title;
        private BeachProgress Progress => BeachQuest.Progress;

        [Header("Scene wiring")]
        public Transform Spawn, CafePickup, SwimBuoy, RoomServiceTray, SurfStart;
        public Bounds CafeZone, WatersportsZone, RestaurantZone, ViewpointZone, HotelLobby;
        public WaterCraft[] Crafts = new WaterCraft[0];
        public BuoyCourse JetSkiCourse, KayakCourse;
        public GuidedRide[] Rides = new GuidedRide[0];
        public Swimming Swim;
        public MarineObserver Observer;
        public DoorController[] RoomDoors = new DoorController[0];      // named "Room 101" … "Room V3"
        public GameObject LostBag;
        public Transform Kitchen;

        private WaterActivity activity;
        private string restaurantState = "", seating = "";
        private Cart dinner;
        private float cookTimer = -1, serviceTimer = -1, sleepTimer = -1;
        private readonly List<FoodItem> dishes = new List<FoodItem>();
        private bool swimReachedBuoy;
        private int bookingChoice, roomTypeChoice, nightsChoice;
        private SurfBalance surf;
        private float fishTimer = -1, biteWindow = -1;
        private string fishMessage = "";

        protected override string[] AmbientAnnouncements => new string[0];

        protected override ScenarioDefinition CreateScenario(string name)
        {
            var tokens = new Dictionary<string, string>
            {
                { "activity", activity != null ? activity.Name : "the activity" },
                { "equipment", activity != null ? activity.Equipment : "" },
                { "safety", activity != null ? activity.Safety : "Follow the instructor." },
                { "objective", activity != null ? activity.Objective : "" },
                { "price", activity != null ? CurrencyManager.Format(activity.Cents) : "" },
                { "room", Progress.hotelRoom == "" ? "101" : Progress.hotelRoom },
                { "roomType", Progress.hotelRoomType == "" ? "Garden Room" : Progress.hotelRoomType },
                { "nights", Mathf.Max(1, Progress.hotelNights).ToString() },
            };
            return BeachScenarios.Create(name, tokens);
        }

        protected override void MarkDone(string scenario) => Progress.Mark(scenario);

        protected override bool IsOrder(string scenario) => scenario == "Beach Barista" || scenario == "Beach Waiter Order" || scenario == "Room Service Phone" || scenario == "Beach Club Bartender";

        protected override void Start()
        {
            base.Start();
            AirportUI.Begin(out _, out _);
            MapBounds = new Rect(-300, -360, 600, 360);
            if (Spawn != null && SaveManager.Data.returnEntrance != "") Player.Place(Spawn.position, Spawn.forward);
            if (Progress.stage == 0) AdvanceBeach(BeachStage.AtBeach);
            foreach (var craft in Crafts) if (craft != null) craft.Boarded += OnBoarded;
            if (JetSkiCourse != null) JetSkiCourse.Completed += course => Complete("jetski", "Course complete in " + course.Elapsed.ToString("0.0") + " s!");
            if (KayakCourse != null) KayakCourse.Completed += course => Complete(WaterCraft.Riding != null ? WaterCraft.Riding.ActivityId : "kayak", "Loop complete!");
            foreach (var ride in Rides) if (ride != null) ride.Finished += r => { Complete(r.ActivityId, "What a ride!"); if (r.ActivityId == "boattour" && Progress.Done("Boat Captain")) BeachQuest.CompleteOptional("BEACH_EXCURSION", 30); };
            if (Observer != null)
            {
                Observer.Observed += species =>
                {
                    if (Progress.observed.Count >= 3 && Progress.rentals.Any(id => id == "snorkel" || id == "scuba")) Complete(Progress.rentals.Contains("scuba") ? "scuba" : "snorkel", "Three species observed!");
                    if (Progress.observed.Count >= 3 && Progress.Done("Dive Instructor Marine Life")) BeachQuest.CompleteOptional("BEACH_MARINE", 30);
                };
                Observer.Photographed += _ => { if (Progress.photos >= 3) BeachQuest.CompleteOptional("BEACH_PHOTO", 30); };
            }
            for (var i = 0; i < RoomDoors.Length; i++) if (RoomDoors[i] != null) RoomDoors[i].Locked = true;
            if (LostBag != null) LostBag.SetActive(Progress.Done("Lost Bag Tourist") && !Progress.bagFound);
            Objective = BeachQuest.Next;
        }

        private void AdvanceBeach(BeachStage stage)
        {
            if (BeachQuest.Advance(stage) && (int)stage == Progress.stage) LifeUI.Toast("✔ " + BeachQuest.Objectives[(int)stage]);
            Objective = BeachQuest.Next;
        }

        protected override void Update()
        {
            base.Update();
            var player = Player.Transform;
            if (player == null) return;
            var position = player.position + Vector3.up;
            if (Progress.Stage == BeachStage.AtBeach && CafeZone.Contains(position)) AdvanceBeach(BeachStage.FoundCafe);
            if (Progress.Stage == BeachStage.OrderedDrink && WatersportsZone.Contains(position)) AdvanceBeach(BeachStage.VisitedWatersports);
            if (Progress.Stage == BeachStage.CompletedActivity && RestaurantZone.Contains(position)) AdvanceBeach(BeachStage.VisitedRestaurant);
            if (Progress.Stage == BeachStage.OrderedDinner && ViewpointZone.Contains(position) && SkyState.SunElevation > -4 && SkyState.SunElevation < 10 && SkyState.Hour > 12)
            {
                AdvanceBeach(BeachStage.WatchedSunset);
                LifeUI.Toast("The sun sinks into the sea… Beautiful.");
            }
            if (Progress.Stage == BeachStage.TalkedToTourist && HotelLobby.Contains(position)) { AdvanceBeach(BeachStage.ReturnedToHotel); Show("complete"); }

            // Swimming activity: reach the buoy, then come back to the sand.
            if (activity != null && activity.Id == "swim" && Swim != null)
            {
                if (!swimReachedBuoy && Swim.InWater && SwimBuoy != null && Vector3.Distance(Flat(player.position), Flat(SwimBuoy.position)) < 6) { swimReachedBuoy = true; LifeUI.Toast("Buoy reached! Now swim back to the beach."); }
                if (swimReachedBuoy && !Swim.InWater) { swimReachedBuoy = false; Complete("swim", "Great swim!"); }
            }
            // Restaurant kitchen timer and room service.
            if (cookTimer > 0 && (cookTimer -= Time.deltaTime) <= 0) Serve();
            if (serviceTimer > 0 && (serviceTimer -= Time.deltaTime) <= 0) DeliverRoomService();
            if (sleepTimer > 0 && (sleepTimer -= Time.deltaTime) <= 0) { Hide(); LifeUI.Toast("Good morning! You slept well."); }
            // Surfing and fishing mini-games.
            if (Panel == "surf" && surf != null)
            {
                var input = Keyboard.current == null ? 0 : (Keyboard.current.aKey.isPressed ? 1 : 0) + (Keyboard.current.dKey.isPressed ? -1 : 0);
                surf.Tick(Time.deltaTime, input, Mathf.Clamp01(SkyState.WindSpeed / 10f) + 0.3f);
                if (surf.Riding >= 15 && !surf.Fallen) { Progress.bestSurfScore = Mathf.Max(Progress.bestSurfScore, surf.Score); Hide(); Complete("surf", "You rode the wave! Score " + surf.Score); }
            }
            if (Panel == "fishing")
            {
                if (fishTimer > 0 && (fishTimer -= Time.deltaTime) <= 0) { biteWindow = 1.1f; fishMessage = "BITE! Press Space now!"; }
                if (biteWindow > 0 && (biteWindow -= Time.deltaTime) <= 0) fishMessage = "It got away… press Space to cast again.";
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    if (biteWindow > 0)
                    {
                        biteWindow = -1;
                        var fish = new[] { "Snapper", "Trevally", "Mackerel", "Sea bream" }[UnityEngine.Random.Range(0, 4)];
                        Progress.fishCaught++;
                        InventoryManager.Add("fish", "Fish: " + fish);
                        fishMessage = "You caught a " + fish + "!";
                        Complete("fishing", "Nice catch — a " + fish + "!");
                    }
                    else if (fishTimer <= 0) { fishTimer = UnityEngine.Random.Range(2f, 6f); fishMessage = "Waiting for a bite…"; }
                }
            }
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0, value.z);

        // ------------------------------------------------------------------ NPCs
        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            switch (npc.Key)
            {
                case "barista":
                    Shop(BeachMenus.Cafe(), npc, "Beach Barista", cart =>
                    {
                        PlaceDrinks(cart, CafePickup);
                        if (Progress.stage >= (int)BeachStage.FoundCafe) AdvanceBeach(BeachStage.OrderedDrink);
                        LifeUI.Toast("Collect your order at the pick-up counter.");
                    });
                    return;
                case "bartender": Shop(BeachMenus.Club(), npc, "Beach Club Bartender", cart => PlaceDrinks(cart, npc.transform)); return;
                case "souvenir": Shop(BeachMenus.Souvenirs(), npc, "Souvenir Seller", null); return;
                case "host":
                    if (restaurantState != "") { Say(npc, "Your table is ready — enjoy your meal!"); return; }
                    Speak("Beach Restaurant Host", npc, () => Show("seating"));
                    return;
                case "waiter": Waiter(npc); return;
                case "cashier":
                    if (restaurantState == "bill") PayDinner(); else Say(npc, "Hello! You can pay here when you've finished your meal.");
                    return;
                case "chef": Say(npc, "Tonight's catch is red snapper — I grill it over coconut husks. Ask your waiter what's in each dish!"); return;
                case "watersports":
                    if (activity != null && activity.Operator == "watersports" && Progress.rentals.Contains(activity.Id) && !Progress.Done("briefed-" + activity.Id)) { Brief(npc); return; }
                    Speak("Watersports Desk", npc, () => { AdvanceBeach(BeachStage.AskedActivities); Show("booking"); });
                    return;
                case "surf": case "dive": case "captain": case "lifeguard":
                    if (activity != null && activity.Operator == npc.Key && !Progress.Done("briefed-" + activity.Id) && Progress.rentals.Contains(activity.Id)) { Brief(npc); return; }
                    if (npc.Key == "dive") { Speak("Dive Instructor Marine Life", npc, null); return; }
                    if (npc.Key == "captain") { Speak("Boat Captain", npc, () => { Progress.excursionBooked = true; bookingChoice = Array.FindIndex(BeachActivities.All, a => a.Id == "boattour"); Show("booking"); }); return; }
                    if (npc.Key == "lifeguard") { Speak("Lifeguard", npc, null); return; }
                    Say(npc, "Book at the watersports desk and I'll give you a lesson!");
                    return;
                case "reception": Reception(npc); return;
                case "tourist": Speak("Beach Tourist Chat", npc, () => { if (Progress.stage == (int)BeachStage.WatchedSunset) AdvanceBeach(BeachStage.TalkedToTourist); LifeUI.Toast("IELTS discussion complete · +15 XP"); SaveManager.Data.xp += 15; }); return;
                case "tourist-lost":
                    if (Progress.bagFound && !Progress.Done("lost-bag-returned")) { Progress.Mark("lost-bag-returned"); Say(npc, "My bag! Thank you so much — you're a star!"); BeachQuest.CompleteOptional("BEACH_LOST_BAG", 30); return; }
                    if (Progress.Done("Lost Bag Tourist")) { Say(npc, "It's blue with a striped towel — near the palms by the dunes, I think."); return; }
                    Speak("Lost Bag Tourist", npc, () => { if (LostBag != null) LostBag.SetActive(true); });
                    return;
                case "tourist-directions":
                    if (Progress.Done("Directions Tourist")) { Say(npc, "Thanks again for the directions!"); return; }
                    Speak("Directions Tourist", npc, () => BeachQuest.CompleteOptional("BEACH_DIRECTIONS", 20));
                    return;
                case "volunteer":
                    if (Progress.Done("Beach Cleanup Volunteer")) { Say(npc, "You've collected " + Progress.litter + " of 10 pieces. Keep going!"); return; }
                    Speak("Beach Cleanup Volunteer", npc, null);
                    return;
                default: Say(npc, npc.Role == "Local resident" ? "Welcome to Palm Bay! The sunset from the headland is the best on the island." : "Lovely day, isn't it?"); return;
            }
        }

        private static void Say(NPCController npc, string line)
        {
            LifeUI.Toast(npc.DisplayName + ": " + line);
            if (ScenarioManager.Instance != null) ScenarioManager.Instance.Speech.Speak(line);
        }

        private void PlaceDrinks(Cart cart, Transform spot)
        {
            if (spot == null) return;
            var index = 0;
            foreach (var line in cart.Lines)
                for (var i = 0; i < line.quantity; i++, index++)
                {
                    var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(cup.GetComponent<Collider>());
                    cup.name = line.name;
                    cup.transform.position = spot.position + spot.right * (index * 0.18f) + Vector3.up * 0.08f;
                    cup.transform.localScale = new Vector3(0.09f, 0.08f, 0.09f);
                    var entry = BeachMenus.Cafe().Find(line.id) ?? BeachMenus.Club().Find(line.id);
                    cup.GetComponent<Renderer>().material.color = entry != null ? entry.colour : new Color(0.9f, 0.7f, 0.4f);
                    var food = cup.AddComponent<FoodItem>();
                    food.DisplayName = line.name;
                    food.Drink = entry == null || entry.category != "Desserts";
                }
        }

        // ------------------------------------------------------------------ restaurant
        private void Waiter(NPCController npc)
        {
            switch (restaurantState)
            {
                case "seated":
                    MenuController.Instance.Show(BeachMenus.Restaurant(), cart =>
                    {
                        dinner = cart;
                        ScenarioManager.Instance.Begin(OrderScenario("Beach Waiter Order", cart), npc, () =>
                        {
                            restaurantState = "cooking";
                            cookTimer = 18;
                            if (Progress.stage == (int)BeachStage.VisitedRestaurant) AdvanceBeach(BeachStage.OrderedDinner);
                            Objective = "Your food is being cooked…";
                            if (Kitchen != null) npc.GoTo(Kitchen.position);
                        });
                    });
                    return;
                case "eating":
                    Speak("Beach Waiter Bill", npc, () => { restaurantState = "bill"; PayDinner(); });
                    return;
                case "cooking": Say(npc, "It won't be long — the chef is plating it now."); return;
                case "":
                    Say(npc, "Please let the host seat you first.");
                    return;
                default: Say(npc, "Thank you, come back soon!"); return;
            }
        }

        private void Serve()
        {
            restaurantState = "eating";
            var seat = InteractionManager.Instance != null ? InteractionManager.Instance.Seat : null;
            var spot = seat != null ? seat.transform.position + seat.transform.forward * 0.55f + Vector3.up * 0.78f : (Player.Transform != null ? Player.Transform.position + Vector3.up : Vector3.zero);
            var index = 0;
            dishes.Clear();
            foreach (var line in dinner.Lines)
                for (var i = 0; i < line.quantity; i++, index++)
                {
                    var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(plate.GetComponent<Collider>());
                    plate.name = line.name;
                    plate.transform.position = spot + (seat != null ? seat.transform.right : Vector3.right) * ((index % 3) - 1) * 0.3f;
                    plate.transform.localScale = new Vector3(0.26f, 0.01f, 0.26f);
                    var entry = BeachMenus.Restaurant().Find(line.id);
                    var food = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(food.GetComponent<Collider>());
                    food.transform.SetParent(plate.transform, false);
                    food.transform.localPosition = new Vector3(0, 3f, 0);
                    food.transform.localScale = new Vector3(0.65f, 5f, 0.5f);
                    food.GetComponent<Renderer>().material.color = entry != null ? entry.colour : Color.white;
                    var item = plate.AddComponent<FoodItem>();
                    item.DisplayName = line.name;
                    item.Drink = entry != null && entry.category == "Drinks";
                    dishes.Add(item);
                }
            var waiter = Npc("waiter");
            waiter?.ReturnHome();
            Objective = "Enjoy your dinner, then ask the waiter for the bill.";
            LifeUI.Toast("Your food has arrived. Enjoy!");
        }

        private void PayDinner()
        {
            var lines = string.Join("\n", dinner.Lines.Select(line => line.quantity + " × " + line.name + "   " + CurrencyManager.Format(line.Total)));
            PaymentController.Instance.Show("Coral Kitchen — your bill", lines, dinner.Total, () =>
            {
                foreach (var dish in dishes) if (dish != null) Destroy(dish.gameObject);
                restaurantState = "paid";
                SaveManager.Data.xp += 20;
                InventoryManager.Add("receipt-coral", "Coral Kitchen receipt");
                LifeUI.Toast("Dinner complete · English task done · +20 XP");
                Objective = BeachQuest.Next;
            }, () => { }, true);
        }

        // ------------------------------------------------------------------ watersports
        public bool CanRide(WaterCraft craft)
        {
            if (!Progress.rentals.Contains(craft.ActivityId)) { LifeUI.Toast("Book " + craft.DisplayName + " at the watersports desk first."); return false; }
            if (!Progress.Done("briefed-" + craft.ActivityId)) { LifeUI.Toast("Your instructor will give you a safety briefing first — talk to them."); return false; }
            var closed = BeachActivities.Closed(BeachActivities.Find(craft.ActivityId), SkyState.WindSpeed, SkyState.Rain, SkyState.Daylight);
            if (closed != "") { LifeUI.Toast(craft.DisplayName + " is " + closed + "."); return false; }
            return true;
        }

        private void OnBoarded(WaterCraft craft)
        {
            LifeUI.Toast(BeachActivities.Find(craft.ActivityId)?.Controls ?? "W/S throttle · A/D steer");
            if (craft.ActivityId == "jetski" && JetSkiCourse != null) JetSkiCourse.Begin();
            if ((craft.ActivityId == "kayak" || craft.ActivityId == "paddleboard") && KayakCourse != null) { KayakCourse.ActivityId = craft.ActivityId; KayakCourse.Begin(); }
        }

        private void Brief(NPCController npc)
        {
            Speak("Safety Briefing", npc, () =>
            {
                Progress.Mark("briefed-" + activity.Id);
                SaveManager.Save();
                StartActivity();
            });
        }

        private void StartActivity()
        {
            switch (activity.Id)
            {
                case "swim": swimReachedBuoy = false; LifeUI.Toast("Swim out to the yellow buoy and back. " + activity.Controls); break;
                case "snorkel": Swim?.Equip(Swimming.Gear.Snorkel); LifeUI.Toast("Mask on! Find three kinds of reef fish off the rocks. " + activity.Controls); break;
                case "scuba": Swim?.Equip(Swimming.Gear.Tank); LifeUI.Toast("Tank on! Descend to the reef. " + activity.Controls); break;
                case "surf": surf = new SurfBalance(Environment.TickCount); if (SurfStart != null) Player.Place(SurfStart.position, SurfStart.forward); Show("surf"); break;
                case "fishing": fishTimer = -1; biteWindow = -1; fishMessage = "Press Space to cast."; Show("fishing"); break;
                case "kayak": case "jetski": case "paddleboard": LifeUI.Toast("Your " + activity.Name.ToLowerInvariant() + " is ready at the shore — walk up to it and press E."); break;
                default:
                    var ride = Rides.FirstOrDefault(item => item != null && item.ActivityId == activity.Id);
                    if (ride != null && InteractionManager.Instance != null) { ride.Begin(InteractionManager.Instance.Player); LifeUI.Toast("All aboard! Sit back and enjoy the " + activity.Name.ToLowerInvariant() + "."); }
                    else Complete(activity.Id, "Finished!");
                    break;
            }
        }

        private void Complete(string id, string message)
        {
            if (!Progress.completedActivities.Contains(id)) Progress.completedActivities.Add(id);
            SaveManager.Data.xp += 15;
            SaveManager.Save();
            LifeUI.Toast(message + " · " + (BeachActivities.Find(id)?.Name ?? id) + " complete · +15 XP");
            if (Progress.stage == (int)BeachStage.RentedEquipment) AdvanceBeach(BeachStage.CompletedActivity);
            if (WaterCraft.Riding != null && WaterCraft.Riding.ActivityId == id) WaterCraft.Riding.GetOff();
        }

        // ------------------------------------------------------------------ hotel
        private void Reception(NPCController npc)
        {
            if (!Progress.reserved) { Show("hotelbook"); return; }
            if (!Progress.checkedIn) { Speak("Beach Hotel Reception", npc, () => Show("rooms")); return; }
            Show("frontdesk");
        }

        public override void Use(string action)
        {
            if (action.StartsWith("room-door:"))
            {
                var room = action.Substring(10);
                var door = RoomDoors.FirstOrDefault(item => item != null && item.name == "Room " + room);
                if (Progress.checkedIn && Progress.hotelRoom == room && InventoryManager.Count("room-key") > 0)
                {
                    if (door != null) door.Locked = false;
                    LifeUI.Toast("Beep — the key card works. Welcome to room " + room + ".");
                }
                else LifeUI.Toast(Progress.checkedIn ? "This isn't your room — yours is " + Progress.hotelRoom + "." : "You need a key card from reception.");
                return;
            }
            switch (action)
            {
                case "sleep":
                    if (!Progress.checkedIn) { LifeUI.Toast("Check in at reception first."); return; }
                    SkySystem.Active?.SetTime(7.5f);
                    sleepTimer = 3;
                    Show("sleep");
                    return;
                case "wardrobe": Show("wardrobe"); return;
                case "roomservice":
                    if (!Progress.checkedIn) { LifeUI.Toast("Room service is for hotel guests."); return; }
                    MenuController.Instance.Show(BeachMenus.RoomService(), cart =>
                        ScenarioManager.Instance.Begin(OrderScenario("Room Service Phone", cart), null, () =>
                        {
                            Progress.roomServiceCents += cart.Total;
                            dinner = cart;
                            serviceTimer = 15;
                            LifeUI.Toast("Room service: about 15 minutes. It'll be added to your bill.");
                        }));
                    return;
                case "sunset":
                    if (SkyState.Hour < 17.5f || SkyState.Hour > 19.5f) { SkySystem.Active?.SetTime(18.15f, false); LifeUI.Toast("You sit and wait for the sunset…"); }
                    return;
                case "lostbag":
                    Progress.bagFound = true;
                    if (LostBag != null) LostBag.SetActive(false);
                    InventoryManager.Add("lost-bag", "Blue beach bag (not yours)");
                    LifeUI.Toast("You found the blue beach bag! Take it back to its owner.");
                    return;
                case "swimflag": LifeUI.Toast("Red-and-yellow flags: swim between them, where the lifeguards are watching."); return;
                case "map": Show("map"); return;
            }
        }

        private void DeliverRoomService()
        {
            var tray = GameObject.Find("Room service tray · " + Progress.hotelRoom);
            var spot = tray != null ? tray.transform : RoomServiceTray;
            if (spot == null || dinner == null) return;
            PlaceDrinks(dinner, spot);
            LifeUI.Toast("Knock knock — room service! Your tray is on the table.");
        }

        // ------------------------------------------------------------------ panels
        protected override bool CanClose(string panel) => panel != "surf" && panel != "sleep";

        protected override void DrawPanel(string panel, float width, float height)
        {
            AirportUI.Theme(new Color(0f, 0.5f, 0.55f));
            switch (panel)
            {
                case "booking": BookingUI(width, height); break;
                case "seating": SeatingUI(width, height); break;
                case "hotelbook": HotelBookingUI(width, height); break;
                case "rooms": RoomsUI(width, height); break;
                case "frontdesk": FrontDeskUI(width, height); break;
                case "surf": SurfUI(width, height); break;
                case "fishing": FishingUI(width, height); break;
                case "wardrobe": WardrobeUI(width, height); break;
                case "sleep":
                    GUI.Box(new Rect(0, 0, width, height), GUIContent.none, AirportUI.Board);
                    GUI.Label(new Rect(0, height / 2 - 40, width, 80), "Zzz… sleeping until morning", new GUIStyle(AirportUI.Amber) { alignment = TextAnchor.MiddleCenter, fontSize = 36 });
                    break;
                case "complete":
                {
                    var area = AirportUI.Window("A Day at the Seaside — complete!", 800, 460, width, height);
                    GUILayout.BeginArea(area);
                    GUILayout.Label("Café, watersports, dinner, sunset and a conversation with a fellow traveller — all in English.", AirportUI.InkBig);
                    GUILayout.Label("Quest " + BeachQuest.Id + " ✔  +200 XP", AirportUI.Good);
                    GUILayout.Label("Activities completed: " + string.Join(", ", Progress.completedActivities), AirportUI.Ink);
                    GUILayout.Label("Optional quests: lost bag · directions · marine life · fish photos · cleanup · boat excursion", AirportUI.Muted);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", AirportUI.Primary)) Hide();
                    GUILayout.EndArea();
                    break;
                }
            }
        }

        private void BookingUI(float width, float height)
        {
            var area = AirportUI.Window("Palm Bay Watersports — book an activity", 1100, 820, width, height);
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(470));
            for (var i = 0; i < BeachActivities.All.Length; i++)
            {
                var item = BeachActivities.All[i];
                var closed = BeachActivities.Closed(item, SkyState.WindSpeed, SkyState.Rain, SkyState.Daylight);
                var label = item.Name + "   ·   " + (item.Cents == 0 ? "free" : CurrencyManager.Format(item.Cents)) + (item.Minutes > 0 ? "   ·   " + item.Minutes + " min" : "") + (closed != "" ? "   ·   " + closed.ToUpperInvariant() : "") + (Progress.rentals.Contains(item.Id) ? "   ·   BOOKED" : "");
                GUI.enabled = closed == "";
                if (GUILayout.Toggle(bookingChoice == i, "  " + label, AirportUI.Secondary)) bookingChoice = i;
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            var chosen = BeachActivities.All[Mathf.Clamp(bookingChoice, 0, BeachActivities.All.Length - 1)];
            AirportUI.Divider();
            AirportUI.Row("Equipment", chosen.Equipment);
            AirportUI.Row("Instructor / operator", chosen.Operator == "watersports" ? "Watersports staff (at the shore)" : chosen.Operator == "dive" ? "Dive instructor (dive hut)" : chosen.Operator == "surf" ? "Surf instructor" : chosen.Operator == "captain" ? "Boat captain (pier)" : "Lifeguard (tower)");
            AirportUI.Row("Controls", chosen.Controls);
            AirportUI.Row("Objective", chosen.Objective);
            AirportUI.Row("Safety", chosen.Safety, AirportUI.Accent);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var closedNow = BeachActivities.Closed(chosen, SkyState.WindSpeed, SkyState.Rain, SkyState.Daylight);
            GUI.enabled = closedNow == "";
            if (GUILayout.Button(chosen.Cents == 0 ? "Choose (free)" : "Book and pay " + CurrencyManager.Format(chosen.Cents), AirportUI.Primary))
            {
                if (chosen.Cents == 0 || CurrencyManager.TrySpend(chosen.Cents, out var message) || Progress.rentals.Contains(chosen.Id))
                {
                    if (!Progress.rentals.Contains(chosen.Id)) Progress.rentals.Add(chosen.Id);
                    activity = chosen;
                    InventoryManager.Add("rental-" + chosen.Id, chosen.Name + " booking");
                    SaveManager.Save();
                    if (Progress.stage == (int)BeachStage.AskedActivities) AdvanceBeach(BeachStage.RentedEquipment);
                    Hide();
                    LifeUI.Toast("Booked! Talk to the " + (chosen.Operator == "watersports" ? "watersports instructor" : chosen.Operator == "dive" ? "dive instructor at the dive hut" : chosen.Operator == "surf" ? "surf instructor" : chosen.Operator == "captain" ? "boat captain on the pier" : "lifeguard at the tower") + " for your safety briefing.");
                }
                else LifeUI.Toast(message);
            }
            GUI.enabled = true;
            if (GUILayout.Button("Close", AirportUI.Secondary, GUILayout.Width(150))) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SeatingUI(float width, float height)
        {
            var area = AirportUI.Window("Coral Kitchen — where would you like to sit?", 760, 420, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("Inside by the open kitchen, or outside on the deck facing the sea.", AirportUI.Ink);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Inside", AirportUI.Primary)) ChooseSeating("beach-in");
            GUI.enabled = !SkyState.Raining;
            if (GUILayout.Button(SkyState.Raining ? "Deck (closed — rain)" : "Outside on the deck", AirportUI.Primary)) ChooseSeating("beach-out");
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ChooseSeating(string group)
        {
            seating = group;
            restaurantState = "choose";
            foreach (var seat in FindObjectsByType<SeatController>(FindObjectsSortMode.None).Where(seat => seat.Group == "beach-in" || seat.Group == "beach-out"))
            {
                var mine = seat.Group == group;
                seat.Allowed = () => mine && restaurantState == "choose";
                seat.Seated = () =>
                {
                    if (restaurantState != "choose") return;
                    restaurantState = "seated";
                    Objective = "Talk to the waiter to see the menu.";
                    var waiter = Npc("waiter");
                    if (waiter != null && InteractionManager.Instance.Seat != null) waiter.GoTo(InteractionManager.Instance.Seat.transform.position + InteractionManager.Instance.Seat.transform.right * 1.1f);
                };
            }
            Hide();
            LifeUI.Toast(group == "beach-out" ? "Take any free table on the deck." : "Take any free table inside.");
        }

        private void HotelBookingUI(float width, float height)
        {
            var area = AirportUI.Window("Palm Bay Resort — reservation", 860, 600, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("Choose a room (fictional prices, pay at check-out).", AirportUI.Ink);
            roomTypeChoice = GUILayout.SelectionGrid(roomTypeChoice, BeachHotel.Types.Select(type => type.Type + "  ·  " + CurrencyManager.Format(type.Cents) + "/night\n" + type.Description).ToArray(), 1, AirportUI.Secondary, GUILayout.Height(200));
            GUILayout.Label("Nights", AirportUI.Muted);
            nightsChoice = GUILayout.Toolbar(nightsChoice, new[] { "1 night", "2 nights", "3 nights" }, AirportUI.Secondary);
            var type = BeachHotel.Types[roomTypeChoice];
            GUILayout.Label("Total " + CurrencyManager.Format(BeachHotel.Total(type.Type, nightsChoice + 1, 0)), AirportUI.InkBig);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reserve", AirportUI.Primary))
            {
                Progress.reserved = true;
                Progress.hotelRoomType = type.Type;
                Progress.hotelNights = nightsChoice + 1;
                SaveManager.Save();
                Hide();
                Speak("Beach Hotel Reception", Npc("reception"), () => Show("rooms"));
            }
            GUILayout.EndArea();
        }

        private void RoomsUI(float width, float height)
        {
            var type = BeachHotel.Find(Progress.hotelRoomType) ?? BeachHotel.Types[0];
            var area = AirportUI.Window("Choose your room · " + type.Type, 700, 440, width, height);
            GUILayout.BeginArea(area);
            foreach (var room in type.Rooms)
                if (GUILayout.Button((room.StartsWith("V") ? "Villa " : "Room ") + room, AirportUI.Primary))
                {
                    Progress.hotelRoom = room;
                    Progress.checkedIn = true;
                    InventoryManager.Add("room-key", "Key card · room " + room);
                    SaveManager.Save();
                    Hide();
                    LifeUI.Toast("Checked in · room " + room + " · tap your key card on the door.");
                }
            GUILayout.EndArea();
        }

        private void FrontDeskUI(float width, float height)
        {
            var area = AirportUI.Window("Reception", 700, 440, width, height);
            GUILayout.BeginArea(area);
            AirportUI.Row("Room", Progress.hotelRoom + " · " + Progress.hotelRoomType + " · " + Progress.hotelNights + " nights");
            AirportUI.Row("Room service", CurrencyManager.Format(Progress.roomServiceCents));
            var total = BeachHotel.Total(Progress.hotelRoomType, Progress.hotelNights, Progress.roomServiceCents);
            AirportUI.Row("Total due", CurrencyManager.Format(total), AirportUI.InkBig);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Check out and pay", AirportUI.Primary))
            {
                Hide();
                Speak("Hotel Checkout", Npc("reception"), () => PaymentController.Instance.Show("Palm Bay Resort", Progress.hotelRoomType + " × " + Progress.hotelNights + " nights\nRoom service " + CurrencyManager.Format(Progress.roomServiceCents), total, () =>
                {
                    InventoryManager.Remove("room-key");
                    Progress.checkedIn = Progress.reserved = false;
                    Progress.hotelRoom = "";
                    Progress.roomServiceCents = 0;
                    SaveManager.Save();
                    foreach (var door in RoomDoors) if (door != null) door.Locked = true;
                    LifeUI.Toast("Checked out. Thank you for staying at Palm Bay!");
                }, () => { }));
            }
            if (GUILayout.Button("Close", AirportUI.Secondary)) Hide();
            GUILayout.EndArea();
        }

        private void SurfUI(float width, float height)
        {
            var box = new Rect(width / 2 - 420, height - 300, 840, 240);
            GUI.Box(box, GUIContent.none, AirportUI.Card);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 16, box.width - 48, box.height - 32));
            GUILayout.Label("Surfing — keep your balance with A / D", AirportUI.InkBig);
            var bar = GUILayoutUtility.GetRect(100, 36, GUILayout.ExpandWidth(true));
            GUI.Box(bar, GUIContent.none, AirportUI.Board);
            var previous = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 0.5f, 0.6f);
            GUI.DrawTexture(new Rect(bar.center.x - bar.width * 0.2f, bar.y, bar.width * 0.4f, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bar.center.x + surf.Lean * bar.width / 2 - 4, bar.y - 4, 8, bar.height + 8), Texture2D.whiteTexture);
            GUI.color = previous;
            GUILayout.Label("Ride time " + surf.Riding.ToString("0.0") + " / 15 s   ·   score " + surf.Score + "   ·   best " + Progress.bestSurfScore, AirportUI.Ink);
            GUILayout.BeginHorizontal();
            if (surf.Fallen)
            {
                GUILayout.Label("Wipe-out! You fell off.", AirportUI.Bad);
                if (GUILayout.Button("Paddle back and try again", AirportUI.Primary)) surf.Recover();
            }
            if (GUILayout.Button("Stop surfing", AirportUI.Secondary, GUILayout.Width(180))) Hide();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void FishingUI(float width, float height)
        {
            var area = AirportUI.Window("Pier fishing", 640, 320, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label(fishMessage, biteWindow > 0 ? AirportUI.Bad : AirportUI.InkBig);
            GUILayout.Label("Space: cast, then strike when you get a bite.   Fish caught: " + Progress.fishCaught, AirportUI.Ink);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Stop fishing (Esc)", AirportUI.Primary)) Hide();
            GUILayout.EndArea();
        }

        private void WardrobeUI(float width, float height)
        {
            var area = AirportUI.Window("Wardrobe", 700, 420, width, height);
            GUILayout.BeginArea(area);
            GUILayout.Label("Outfits: Beachwear · Resort casual · Evening", AirportUI.InkBig);
            GUILayout.Label("PLACEHOLDER — the project has no alternative outfit models for the two protagonists yet, so changing clothes is not visible. Add outfit meshes to enable this.", AirportUI.Bad);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", AirportUI.Primary)) Hide();
            GUILayout.EndArea();
        }
    }
}
