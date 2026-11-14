using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Everything the beach remembers (saved inside LifeProgress).
    [Serializable]
    public sealed class BeachProgress
    {
        public int stage;
        public List<string> rentals = new List<string>();       // activity ids paid for today
        public List<string> completedActivities = new List<string>();
        public List<string> observed = new List<string>();      // marine species seen
        public int photos, litter, bestSurfScore, fishCaught;
        public string hotelRoomType = "", hotelRoom = "";
        public int hotelNights, roomServiceCents;
        public bool reserved, checkedIn, bagFound, directionsGiven, excursionBooked;
        public List<string> done = new List<string>();          // finished conversations and optional quests
        public BeachStage Stage => (BeachStage)stage;
        public bool Done(string id) => done.Contains(id);
        public void Mark(string id) { if (!done.Contains(id)) done.Add(id); }
    }

    public enum BeachStage
    {
        NotStarted = 0, AtBeach = 1, FoundCafe = 2, OrderedDrink = 3, VisitedWatersports = 4, AskedActivities = 5,
        RentedEquipment = 6, CompletedActivity = 7, VisitedRestaurant = 8, OrderedDinner = 9, WatchedSunset = 10,
        TalkedToTourist = 11, ReturnedToHotel = 12,
    }

    /// A water activity: equipment, operator, price, controls and the objective that completes it.
    [Serializable]
    public sealed class WaterActivity
    {
        public string Id, Name, Operator, Equipment, Controls, Objective, Safety;
        public int Cents, Minutes;
        public bool Playable;          // full controls (vs a guided ride)
        public bool NeedsCalmSea;      // closed in rain / strong wind
    }

    public static class BeachActivities
    {
        public static readonly WaterActivity[] All =
        {
            A("swim", "Swimming", "lifeguard", "None — swim between the flags", 0, 0, true, false, "WASD swim · Shift faster", "Swim out to the yellow buoy and back.", "Swim between the red-and-yellow flags. Raise your arm if you need help."),
            A("kayak", "Kayaking", "watersports", "Sit-on-top kayak, paddle, life jacket", 1500, 30, true, true, "W/S paddle · A/D turn · E get off at the beach", "Paddle round the reef buoy and back to the beach.", "Wear the life jacket and stay inside the buoys."),
            A("jetski", "Jet skiing", "watersports", "Jet ski, life jacket, kill cord", 6000, 20, true, true, "W throttle · S brake/reverse · A/D steer · R reset", "Ride the buoy course: pass all the gates in order.", "Clip the kill cord to your life jacket. Keep 50 metres from swimmers."),
            A("snorkel", "Snorkeling", "dive", "Mask, snorkel, fins", 1200, 45, true, false, "WASD swim · C dive · Space surface · P photo", "Find three kinds of reef fish.", "Never touch the coral and stay with your buddy."),
            A("scuba", "Scuba diving (try dive)", "dive", "Tank, regulator, mask, fins", 9000, 45, true, false, "WASD swim · C descend · Space ascend · P photo", "Dive to the reef and observe three species.", "Breathe slowly, never hold your breath, equalise your ears as you descend."),
            A("paddleboard", "Paddleboarding", "watersports", "Paddleboard and paddle", 1500, 30, true, true, "W/S paddle · A/D turn", "Paddle to the reef buoy and back.", "Kneel first, then stand up slowly. Keep the leash on."),
            A("surf", "Surfing lesson", "surf", "Soft-top surfboard, rash vest", 4500, 60, true, true, "A/D keep your balance", "Ride a wave for 15 seconds.", "Paddle out away from the swimmers and always hold on to your board."),
            A("fishing", "Pier fishing", "captain", "Rod, bait, bucket", 800, 30, true, false, "Space cast / strike", "Catch a fish from the pier.", "Mind the hooks and keep the walkway clear."),
            A("banana", "Banana boat ride", "captain", "Banana boat, life jackets", 2500, 15, false, true, "Hold on!", "Finish the ride.", "Hold the handles with both hands and lean with the turns."),
            A("parasail", "Parasailing", "captain", "Harness, parasail, speedboat", 8000, 15, false, true, "Enjoy the view", "Finish the flight.", "Sit back in the harness and keep your arms by your sides when landing."),
            A("boattour", "Boat tour", "captain", "Speedboat with captain", 3500, 30, false, false, "Sit back", "Finish the tour round the bay.", "Stay seated while the boat is moving."),
            A("yacht", "Yacht sightseeing", "captain", "Motor yacht with crew", 12000, 45, false, true, "Sit back", "Finish the sunset cruise.", "Hold the rail on deck and wear soft shoes."),
        };

        private static WaterActivity A(string id, string name, string op, string equipment, int cents, int minutes, bool playable, bool calm, string controls, string objective, string safety) =>
            new WaterActivity { Id = id, Name = name, Operator = op, Equipment = equipment, Cents = cents, Minutes = minutes, Playable = playable, NeedsCalmSea = calm, Controls = controls, Objective = objective, Safety = safety };

        public static WaterActivity Find(string id) => All.FirstOrDefault(activity => activity.Id == id);

        /// Why an activity is closed right now ("" = open). Weather and darkness close the sea.
        public static string Closed(WaterActivity activity, float wind, float rain, float daylight)
        {
            if (daylight < 0.3f && activity.Id != "fishing") return "closed after dark";
            if (rain > 0.25f && activity.Id != "fishing") return "closed in the rain";
            if (activity.NeedsCalmSea && wind > 9f) return "closed — the wind is too strong";
            return "";
        }
    }

    public sealed class HotelRoom
    {
        public string Type, Description;
        public int Cents;
        public string[] Rooms;
    }

    public static class BeachHotel
    {
        public static readonly HotelRoom[] Types =
        {
            new HotelRoom { Type = "Garden Room", Description = "Queen bed, garden view, balcony", Cents = 12000, Rooms = new[] { "101", "102" } },
            new HotelRoom { Type = "Ocean View Room", Description = "King bed, sea-view balcony", Cents = 18000, Rooms = new[] { "103", "104" } },
            new HotelRoom { Type = "Beach Villa", Description = "Private villa steps from the sand", Cents = 32000, Rooms = new[] { "V1", "V2", "V3" } },
        };
        public static HotelRoom Find(string type) => Types.FirstOrDefault(room => room.Type == type);
        public static int Total(string type, int nights, int extras) => (Find(type)?.Cents ?? 0) * Mathf.Max(1, nights) + extras;
    }

    /// The surfing balance mini-game (pure logic, tested): keep the board level while the wave pushes.
    public sealed class SurfBalance
    {
        public float Lean { get; private set; }        // −1 … 1, fall beyond ±1
        public float Riding { get; private set; }
        public int Score { get; private set; }
        public bool Fallen { get; private set; }
        private readonly System.Random random;
        private float push;
        public SurfBalance(int seed) { random = new System.Random(seed); }

        public void Tick(float dt, float input, float waveSize)
        {
            if (Fallen) return;
            if (random.NextDouble() < dt * 1.5) push = ((float)random.NextDouble() * 2 - 1) * (0.6f + waveSize);
            Lean += (push * 0.8f - input * 1.6f + Lean * 0.5f) * dt;
            Riding += dt;
            Score = Mathf.RoundToInt(Riding * 10 * (1 + waveSize) * (1 - Mathf.Abs(Lean) * 0.5f));
            if (Mathf.Abs(Lean) >= 1) Fallen = true;
        }

        public void Recover() { Lean = 0; push = 0; Fallen = false; Riding = 0; Score = 0; }
    }

    /// BEACH_DAY_001 — A Day at the Seaside (12 objectives, strictly in order).
    public static class BeachQuest
    {
        public const string Id = "BEACH_DAY_001", Title = "A Day at the Seaside";
        public static readonly string[] Objectives =
        {
            "", "Travel to the beach", "Find the café", "Order a drink in English", "Visit the watersports desk",
            "Ask about available activities", "Rent equipment", "Complete a water activity", "Visit the restaurant",
            "Order dinner", "Watch the sunset", "Talk to a tourist", "Return to the hotel",
        };
        public static readonly (string id, string title)[] Optional =
        {
            ("BEACH_LOST_BAG", "Find a lost beach bag"), ("BEACH_DIRECTIONS", "Help a tourist with directions"),
            ("BEACH_MARINE", "Learn about marine life"), ("BEACH_PHOTO", "Photograph tropical fish"),
            ("BEACH_CLEANUP", "Join a beach cleanup"), ("BEACH_EXCURSION", "Book a boat excursion"),
        };

        public static BeachProgress Progress => SaveManager.Data.beach;

        public static bool Advance(BeachStage stage)
        {
            var progress = Progress;
            var number = (int)stage;
            if (number <= progress.stage) return true;
            if (number != progress.stage + 1) return false;
            progress.stage = number;
            QuestManager.Get(Id, Title);
            if (number >= Objectives.Length - 1) QuestManager.Complete(Id, 200, "A day at Palm Bay complete");
            else QuestManager.Advance(Id, number, Next);
            SaveManager.Save();
            return true;
        }

        public static string Next => Progress.stage + 1 < Objectives.Length ? Objectives[Progress.stage + 1] : "Day complete!";

        public static void CompleteOptional(string id, int xp)
        {
            var title = Optional.FirstOrDefault(item => item.id == id).title ?? id;
            QuestManager.Get(id, title);
            QuestManager.Complete(id, xp, title + " ✔");
        }
    }
}
