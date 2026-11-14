using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Airport: the travel rules and quest logic (EditMode, no save touched)
    /// and the wiring of the three airport scenes. Play-mode behaviour still needs a manual run.
    public static class AirportChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Airport")]
        public static void Menu()
        {
            var errors = Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak airport checks passed.");
            else Debug.LogError("ContextSpeak airport checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static string[] Run()
        {
            var errors = new List<string>();
            void Check(bool value, string message) { if (!value) errors.Add(message); }
            void Guard(string name, Action body)
            {
                try { body(); } catch (Exception error) { errors.Add(name + " threw " + error.GetType().Name + ": " + error.Message); }
            }
            var persist = SaveManager.Persist;
            SaveManager.Persist = false;
            try
            {
                Guard("Quest", () => Quest(Check));
                Guard("Booking & baggage", () => Booking(Check));
                Guard("Boarding", () => Boarding(Check));
                Guard("Security & exchange", () => Security(Check));
                Guard("Dialogue", () => Dialogue(Check));
                Guard("Save", () => SaveLoad(Check));
            }
            finally { SaveManager.UseInMemory(null); SaveManager.Persist = persist; }
            Guard("Airport scene", () => Scenes(Check));
            return errors.ToArray();
        }

        private static void Quest(Action<bool, string> check)
        {
            SaveManager.UseInMemory(new LifeProgress());
            TravelQuest.Restart();
            check(!TravelQuest.Advance(TravelStage.TalkedToCheckIn), "Quest refuses to skip ahead (check-in before arriving).");
            check(TravelQuest.Advance(TravelStage.AtAirport) && TravelQuest.Advance(TravelStage.InDepartureHall), "Quest advances in order.");
            check(!TravelQuest.Advance(TravelStage.Boarded), "Cannot board before checking in, security and passport control.");
            for (var stage = TravelStage.CheckedFlightInfo; stage <= TravelStage.AtHotel; stage++)
                check(TravelQuest.Advance(stage), "Objective " + (int)stage + " advances after its prerequisites.");
            check(QuestManager.Get(TravelQuest.Id, TravelQuest.Title).done && SaveManager.Data.xp >= 250, "Completing all 17 objectives completes AIRPORT_TRAVEL_001.");
            check(TravelQuest.Objectives.Length == 18, "17 objectives as specified.");
        }

        private static void Booking(Action<bool, string> check)
        {
            var booking = new TravelBooking();
            FlightSchedule.Book(booking, Destinations.Find("tyo"), 10, 1234);
            check(booking.booked && booking.flight.StartsWith("SL ") && booking.gate.StartsWith("G") && booking.bookingRef.Length == 6 && booking.bagWeights.Count == 2, "Booking generates flight, gate, reference and bags.");
            check(booking.departure == "12:00" && FlightSchedule.BoardingTime(booking) == "11:15", "Departure 2 h after booking; boarding 40 min before.");
            check(BaggageRules.Fee(new List<float> { 22.9f }, 1) == 0, "A bag within 23 kg is free.");
            check(BaggageRules.Fee(new List<float> { 24.2f }, 1) == 2 * BaggageRules.PerKilo, "Excess is charged per started kilo.");
            check(BaggageRules.Fee(new List<float> { 20f, 10f }, 2) == BaggageRules.ExtraBag, "A second bag costs the extra-bag fee.");
            check(!BaggageRules.Acceptable(new List<float> { 33f }, 1) && BaggageRules.Acceptable(new List<float> { 30f }, 1), "Bags over 32 kg must be repacked.");
            check(BaggageRules.Tag(booking, 0).Contains(booking.Where.Code), "Bag tags carry the destination code.");
        }

        private static void Boarding(Action<bool, string> check)
        {
            var booking = new TravelBooking();
            FlightSchedule.Book(booking, Destinations.Find("lon"), 9, 5);
            check(BoardingRules.Validate(booking, booking.gate, true) == BoardingCheck.NoBoardingPass, "No boarding pass → refused.");
            booking.boardingPass = true;
            booking.stage = (int)TravelStage.LuggageCheckedIn;
            check(BoardingRules.Validate(booking, booking.gate, true) == BoardingCheck.SecurityNotCleared, "Security not done → refused.");
            booking.stage = (int)TravelStage.SecurityCleared;
            check(BoardingRules.Validate(booking, booking.gate, true) == BoardingCheck.PassportNotCleared, "Passport control not done → refused.");
            booking.stage = (int)TravelStage.BoughtDrink;
            var wrong = booking.gate == "G1" ? "G2" : "G1";
            check(BoardingRules.Validate(booking, wrong, true) == BoardingCheck.WrongGate, "Wrong gate → refused.");
            check(BoardingRules.Validate(booking, booking.gate, false) == BoardingCheck.NotBoardingYet, "Before the announcement → refused.");
            check(BoardingRules.Validate(booking, booking.gate, true) == BoardingCheck.Valid, "Right gate, boarding open → valid.");
            booking.stage = (int)TravelStage.Boarded;
            check(BoardingRules.Validate(booking, booking.gate, true) == BoardingCheck.AlreadyBoarded, "Boarding pass cannot be used twice.");
        }

        private static void Security(Action<bool, string> check)
        {
            var correct = SecurityCheck.Items.Select(item => item.correct).ToList();
            check(SecurityCheck.Problems(correct).Count == 0 && !SecurityCheck.ArchBeeps(correct), "Correct trays pass the X-ray and the arch.");
            var pockets = correct.ToList();
            pockets[Array.FindIndex(SecurityCheck.Items, item => item.item == "Keys")] = SecurityCheck.Place.Pocket;
            check(SecurityCheck.ArchBeeps(pockets) && SecurityCheck.Problems(pockets).Count == 1, "Keys left in a pocket set off the arch.");
            var mel = Destinations.Find("mel");
            check(CurrencyExchange.Foreign(10000, mel) == Mathf.FloorToInt(100 * mel.Rate * 0.97f), "Exchange applies the rate and 3 % commission.");
        }

        private static void Dialogue(Action<bool, string> check)
        {
            foreach (var destination in Destinations.All)
            {
                var booking = new TravelBooking();
                FlightSchedule.Book(booking, destination, 10, 42);
                booking.seat = "14A";
                foreach (var name in AirportScenarios.Names)
                {
                    var scenario = AirportScenarios.Create(name, booking);
                    check(scenario != null && scenario.Steps.Count > 0, name + " exists.");
                    foreach (var step in scenario.Steps)
                    {
                        check(!step.meaning.Contains("{"), name + "/" + step.id + " has no unfilled tokens.");
                        check(DialogueStateMachine.Conveys(step, step.model), destination.City + " · " + name + "/" + step.id + " model answer is accepted.");
                        check(!DialogueStateMachine.Conveys(step, "purple elephant"), name + "/" + step.id + " rejects an unrelated answer.");
                    }
                }
                var passport = AirportScenarios.Create("Passport Control", booking);
                foreach (var other in Destinations.All.Where(item => item != destination))
                    check(!DialogueStateMachine.Conveys(passport.Steps[0], "I'm going to " + other.City), "Passport control for " + destination.City + " rejects " + other.City + " (itinerary check).");
                check(!DialogueStateMachine.Conveys(passport.Steps[2], "for three months"), "Stay length is checked against the booking.");
            }
            // IELTS repeat gate on an airport conversation: a wrong repetition does not unlock the next line.
            var gateBooking = new TravelBooking();
            FlightSchedule.Book(gateBooking, Destinations.Find("mel"), 10, 1);
            var machine = new DialogueStateMachine();
            machine.Start(AirportScenarios.Create("Passport Control", gateBooking), Slice.IeltsBand.Band5, true);
            machine.Answer("blue", true); machine.Answer("green", true); machine.Answer("red", true);
            check(machine.Phase == DialoguePhase.AwaitingRepeat && !machine.Repeat("I like bananas") && machine.Index == 0, "Repeat gate locks the next line until the corrected sentence is said.");
            check(machine.Repeat(machine.Target) && machine.Index == 1, "Saying the target sentence unlocks the next line.");
            var expert = new DialogueStateMachine();
            expert.Start(AirportScenarios.Create("IELTS Travel Practice", gateBooking), Slice.IeltsBand.Band9, true);
            var advanced = new DialogueStateMachine();
            advanced.Start(AirportScenarios.Create("IELTS Travel Practice", gateBooking), Slice.IeltsBand.Band7, true);
            check(expert.NpcLine != advanced.NpcLine, "Band 9 gets its own abstract questions.");
        }

        private static void SaveLoad(Action<bool, string> check)
        {
            var progress = new LifeProgress();
            FlightSchedule.Book(progress.travel, Destinations.Find("kul"), 15, 9);
            progress.travel.stage = (int)TravelStage.SecurityCleared;
            progress.travel.seat = "21F";
            progress.travel.bagTags.Add("SL208-KUL-001");
            progress.travel.Mark("Check-in Agent");
            progress.travel.foreignCash = 210;
            var copy = JsonUtility.FromJson<LifeProgress>(JsonUtility.ToJson(progress));
            check(copy.travel != null && copy.travel.stage == (int)TravelStage.SecurityCleared && copy.travel.seat == "21F" && copy.travel.destination == "kul"
                && copy.travel.bagTags.Count == 1 && copy.travel.Done("Check-in Agent") && copy.travel.foreignCash == 210, "Journey progress survives save/load.");
        }

        private static void Scenes(Action<bool, string> check)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AirportBuilder.ScenePath) == null) { check(false, "Airport.unity not built."); return; }
            EditorSceneManager.OpenScene(AirportBuilder.ScenePath, OpenSceneMode.Single);
            var flow = UnityEngine.Object.FindFirstObjectByType<DepartureFlow>();
            check(flow != null, "Departure flow exists.");
            if (flow != null)
                foreach (var (field, value) in new (string, UnityEngine.Object)[] { ("TrainSpawn", flow.TrainSpawn), ("TaxiSpawn", flow.TaxiSpawn), ("SecurityGate", flow.SecurityGate), ("PassportGate", flow.PassportGate), ("CheckInBelt", flow.CheckInBelt), ("ScaleReadout", flow.ScaleReadout) })
                    check(value != null, "Departure flow field wired: " + field);
            check(flow != null && flow.Gates.Length == 3 && flow.GateMonitors.Length == 3 && flow.JetBridgeDoors.Length == 3, "Three gates with monitors and jet bridges.");
            var keys = UnityEngine.Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None).Select(npc => npc.Key).ToArray();
            foreach (var key in new[] { "taxi", "info", "checkin", "security", "passport", "dutyfree", "barista", "waiter", "exchange", "lounge", "books", "souvenir", "convenience", "gate-G1", "gate-G2", "gate-G3", "pilot", "tutor", "cleaner" })
                check(keys.Contains(key), "Airport has NPC: " + key);
            check(UnityEngine.Object.FindObjectsByType<TravelPortal>(FindObjectsSortMode.None).Count(portal => portal.TargetScene == "AircraftCabin") == 3, "Each gate has an aircraft door.");
            check(UnityEngine.Object.FindObjectsByType<AirportSpot>(FindObjectsSortMode.None).Any(spot => spot.Action == "kiosk") && UnityEngine.Object.FindObjectsByType<AirportSpot>(FindObjectsSortMode.None).Any(spot => spot.Action == "board"), "Kiosks and departures boards are usable.");
            var movers = UnityEngine.Object.FindObjectsByType<PathMover>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            check(movers.Length >= 4, "Moving traffic, ground vehicles and the aircraft loop exist (found " + movers.Length + ": " + string.Join(", ", movers.Select(mover => mover.name)) + ").");
            check(UnityEngine.Object.FindFirstObjectByType<SkySystem>() != null, "Airport has the dynamic sky.");

            EditorSceneManager.OpenScene(AirportCabinBuilder.ScenePath, OpenSceneMode.Single);
            var seats = UnityEngine.Object.FindObjectsByType<SeatController>(FindObjectsSortMode.None).Select(seat => seat.Group).ToArray();
            check(Enumerable.Range(10, 15).All(row => "ABCDEF".All(letter => seats.Contains("seat-" + row + letter))), "Every seat offered at check-in (rows 10–24) exists in the cabin.");
            var cabin = UnityEngine.Object.FindFirstObjectByType<CabinFlow>();
            check(cabin != null && cabin.Outside != null && cabin.SeatbeltSigns.Length > 0, "Cabin flow wired (outside view, seatbelt signs).");
            check(UnityEngine.Object.FindObjectsByType<TravelPortal>(FindObjectsSortMode.None).Any(portal => portal.TargetScene == "ArrivalAirport"), "Cabin exits to the arrival airport.");

            EditorSceneManager.OpenScene(ArrivalAirportBuilder.ScenePath, OpenSceneMode.Single);
            var arrival = UnityEngine.Object.FindFirstObjectByType<ArrivalFlow>();
            check(arrival != null && arrival.Carousel != null && arrival.ImmigrationGate != null && arrival.CustomsGate != null && arrival.HotelSpawn != null, "Arrival flow wired (carousel, gates, hotel).");
            var arrivalKeys = UnityEngine.Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None).Select(npc => npc.Key).ToArray();
            foreach (var key in new[] { "arrival", "bagservice", "customs", "tourist", "taxidriver", "hotel" })
                check(arrivalKeys.Contains(key), "Arrival airport has NPC: " + key);
            foreach (var path in new[] { AirportBuilder.ScenePath, AirportCabinBuilder.ScenePath, ArrivalAirportBuilder.ScenePath })
                check(EditorBuildSettings.scenes.Any(scene => scene.path == path), "Registered for loading: " + path);
        }
    }
}
