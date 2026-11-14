using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A fictional destination package. Arrival signs, currency, hotel and immigration answers come from here.
    [Serializable]
    public sealed class Destination
    {
        public string Id, City, Country, Airport, Code, Currency, CurrencyName, Hotel, Greeting, Landmark;
        public float Rate;          // destination currency units per 1 game dollar (fictional)
        public float FlightHours;
        public int Fare;            // cents, economy
        public string Words;        // regex alternatives a correct answer may use ("melbourne|australia")
    }

    public static class Destinations
    {
        public static readonly Destination[] All =
        {
            new Destination { Id = "kul", City = "Kuala Lumpur", Country = "Malaysia", Airport = "Kuala Lumpur International", Code = "KUL", Currency = "RM", CurrencyName = "ringgit", Rate = 4.4f, FlightHours = 2.5f, Fare = 18000, Hotel = "Petaling Garden Hotel", Greeting = "Selamat datang", Landmark = "the twin towers", Words = "kuala lumpur|kl|malaysia" },
            new Destination { Id = "lon", City = "London", Country = "United Kingdom", Airport = "London Riverside", Code = "LRS", Currency = "£", CurrencyName = "pounds", Rate = 0.78f, FlightHours = 13, Fare = 68000, Hotel = "Thames View Hotel", Greeting = "Welcome to London", Landmark = "the river and the old bridges", Words = "london|england|uk|united kingdom|britain" },
            new Destination { Id = "mel", City = "Melbourne", Country = "Australia", Airport = "Melbourne Bayside", Code = "MBY", Currency = "A$", CurrencyName = "Australian dollars", Rate = 1.52f, FlightHours = 8, Fare = 42000, Hotel = "Laneway Hotel", Greeting = "G'day and welcome", Landmark = "the laneways and trams", Words = "melbourne|australia|victoria" },
            new Destination { Id = "tyo", City = "Tokyo", Country = "Japan", Airport = "Tokyo Bayfront", Code = "TBF", Currency = "¥", CurrencyName = "yen", Rate = 150f, FlightHours = 6.5f, Fare = 38000, Hotel = "Sakura Station Hotel", Greeting = "Yōkoso", Landmark = "the shrines and the city lights", Words = "tokyo|japan" },
            new Destination { Id = "nyc", City = "New York", Country = "United States", Airport = "New York Harborside", Code = "NHS", Currency = "US$", CurrencyName = "US dollars", Rate = 1f, FlightHours = 15, Fare = 72000, Hotel = "Hudson Corner Hotel", Greeting = "Welcome to New York", Landmark = "the skyline and Central Park", Words = "new york|nyc|america|united states|usa|us" },
        };

        public static Destination Find(string id) => All.FirstOrDefault(item => item.Id == id) ?? All[2];
    }

    /// Journey stages (also the quest objective number, 1–17).
    public enum TravelStage
    {
        NotStarted = 0, AtAirport = 1, InDepartureHall = 2, CheckedFlightInfo = 3, TalkedToCheckIn = 4, LuggageCheckedIn = 5,
        SecurityCleared = 6, PassportControlCleared = 7, FoundGate = 8, BoughtDrink = 9, Boarded = 10, Seated = 11,
        SpokeToCrew = 12, Flown = 13, ArrivalCleared = 14, LuggageCollected = 15, FoundTaxi = 16, AtHotel = 17,
    }

    /// Everything the journey needs to remember (saved inside LifeProgress).
    [Serializable]
    public sealed class TravelBooking
    {
        public int stage;
        public bool booked, checkedIn, boardingPass, scholarship;
        public string bookingRef = "", airline = "SkyLark Air", flight = "", destination = "mel", departure = "", gate = "", seat = "", seatClass = "Economy";
        public int bags = 1, excessPaid;
        public List<float> bagWeights = new List<float>();
        public List<string> bagTags = new List<string>();
        public string purpose = "holiday";
        public int stayDays = 7;
        public bool carryOnStowed, seatbelt, bagCollected, bagReported, customsDeclared;
        public int foreignCash;                 // destination currency units (whole units)
        public List<string> completed = new List<string>();   // finished dialogue tasks

        public TravelStage Stage => (TravelStage)stage;
        public Destination Where => Destinations.Find(destination);
        public bool Done(string task) => completed.Contains(task);
        public void Mark(string task) { if (!completed.Contains(task)) completed.Add(task); }
    }

    /// Fictional flight schedule and booking.
    public static class FlightSchedule
    {
        public static string FlightNumber(Destination destination) => "SL " + (200 + Array.IndexOf(Destinations.All, destination) * 12 + 8);

        public static string Time(float hour) => ((int)Mathf.Repeat(hour, 24)).ToString("00") + ":" + ((int)(Mathf.Repeat(hour, 24) % 1 * 60) / 5 * 5).ToString("00");

        /// Departure about 2 h after booking, gate G1–G3, seeded bag weights (one may be overweight).
        public static void Book(TravelBooking booking, Destination destination, float hourNow, int seed)
        {
            var random = new System.Random(seed);
            booking.booked = true;
            booking.destination = destination.Id;
            booking.flight = FlightNumber(destination);
            booking.departure = Time(hourNow + 2);
            booking.gate = "G" + (1 + random.Next(3));
            booking.bookingRef = new string(Enumerable.Range(0, 6).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[random.Next(32)]).ToArray());
            booking.bagWeights = new List<float> { (float)Math.Round(20.5 + random.NextDouble() * 5.5, 1), (float)Math.Round(8 + random.NextDouble() * 6, 1) };
            booking.seat = "";
            booking.checkedIn = booking.boardingPass = false;
            booking.bags = 1;
            booking.bagTags.Clear();
        }

        public static string BoardingTime(TravelBooking booking)
        {
            var parts = booking.departure.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return "--:--";
            return Time(h + m / 60f - 0.67f);
        }
    }

    /// Fictional baggage allowance: one 23 kg bag included, extra bags and extra kilos cost money,
    /// anything over 32 kg must be repacked (it cannot be checked as one bag).
    public static class BaggageRules
    {
        public const float Allowance = 23f, Limit = 32f, CarryOnLimit = 7f;
        public const int PerKilo = 1200, ExtraBag = 4500;

        public static int Fee(IList<float> weights, int bags)
        {
            var fee = 0;
            for (var i = 0; i < Mathf.Min(bags, weights.Count); i++)
            {
                if (i >= 1) fee += ExtraBag;
                var over = weights[i] - Allowance;
                if (over > 0) fee += Mathf.CeilToInt(over) * PerKilo;
            }
            return fee;
        }

        public static bool Acceptable(IList<float> weights, int bags) => bags >= 1 && bags <= weights.Count && weights.Take(bags).All(weight => weight <= Limit);

        public static string Tag(TravelBooking booking, int index) => booking.flight.Replace(" ", "") + "-" + booking.Where.Code + "-" + (index + 1).ToString("000");
    }

    public enum BoardingCheck { Valid, NoBoardingPass, SecurityNotCleared, PassportNotCleared, WrongGate, NotBoardingYet, AlreadyBoarded }

    public static class BoardingRules
    {
        public static BoardingCheck Validate(TravelBooking booking, string gate, bool boardingOpen)
        {
            if (booking.stage >= (int)TravelStage.Boarded) return BoardingCheck.AlreadyBoarded;
            if (!booking.boardingPass) return BoardingCheck.NoBoardingPass;
            if (booking.stage < (int)TravelStage.SecurityCleared) return BoardingCheck.SecurityNotCleared;
            if (booking.stage < (int)TravelStage.PassportControlCleared) return BoardingCheck.PassportNotCleared;
            if (booking.gate != gate) return BoardingCheck.WrongGate;
            if (!boardingOpen) return BoardingCheck.NotBoardingYet;
            return BoardingCheck.Valid;
        }

        public static string Explain(BoardingCheck check, TravelBooking booking) => check switch
        {
            BoardingCheck.Valid => "Boarding pass accepted. Have a good flight!",
            BoardingCheck.NoBoardingPass => "No boarding pass — check in at the SkyLark Air counter first.",
            BoardingCheck.SecurityNotCleared => "This boarding pass hasn't been through security.",
            BoardingCheck.PassportNotCleared => "Please complete passport control first.",
            BoardingCheck.WrongGate => "Wrong gate — flight " + booking.flight + " leaves from gate " + booking.gate + ".",
            BoardingCheck.NotBoardingYet => "Boarding hasn't started yet. Please wait for the announcement.",
            _ => "You have already boarded.",
        };
    }

    /// Fictional currency exchange with a small commission.
    public static class CurrencyExchange
    {
        public const float Commission = 0.03f;
        public static int Foreign(int dollarCents, Destination destination) => Mathf.FloorToInt(dollarCents / 100f * destination.Rate * (1 - Commission));
        public static string Receipt(int dollarCents, Destination destination) =>
            "Exchanged " + CurrencyManager.Format(dollarCents) + " → " + destination.Currency + " " + Foreign(dollarCents, destination).ToString("N0")
            + "\nRate 1 : " + destination.Rate.ToString("0.###") + " · commission " + (Commission * 100).ToString("0") + "%";
    }

    /// The security-tray mini-game (fictional game rules, not real airport procedure).
    public static class SecurityCheck
    {
        public enum Place { Bag, Tray, Pocket, Bin }
        public static readonly (string item, Place correct, string why)[] Items =
        {
            ("Laptop", Place.Tray, "Large electronics go in their own tray."),
            ("Bag of small liquids", Place.Tray, "The clear liquids bag goes in a tray."),
            ("Phone", Place.Tray, "Phones go in the tray, not your pocket."),
            ("Keys", Place.Tray, "Metal in your pockets sets off the scanner."),
            ("Coins", Place.Tray, "Metal in your pockets sets off the scanner."),
            ("Belt", Place.Tray, "A metal buckle sets off the scanner."),
            ("Jacket", Place.Tray, "Coats and jackets go through the X-ray."),
            ("Full water bottle", Place.Bin, "Large drinks can't go through in this game — finish it or bin it."),
            ("Paperback book", Place.Bag, "Books can stay in your bag."),
        };

        public static List<string> Problems(IList<Place> choices)
        {
            var problems = new List<string>();
            for (var i = 0; i < Items.Length; i++)
                if (choices[i] != Items[i].correct) problems.Add(Items[i].item + ": " + Items[i].why);
            return problems;
        }

        public static bool ArchBeeps(IList<Place> choices) => choices.Where((choice, i) => choice == Place.Pocket && (Items[i].item == "Keys" || Items[i].item == "Coins" || Items[i].item == "Belt" || Items[i].item == "Phone")).Any();
    }

    /// AIRPORT_TRAVEL_001 — objectives advance strictly in order.
    public static class TravelQuest
    {
        public const string Id = "AIRPORT_TRAVEL_001", Title = "First International Trip";
        public static readonly string[] Objectives =
        {
            "", "Travel to the airport", "Find the departure hall", "Check the flight information", "Talk to the check-in agent",
            "Check in your luggage", "Complete security", "Complete departure passport control", "Find your boarding gate",
            "Buy a drink", "Board the aircraft", "Find your seat", "Speak with the flight attendant", "Complete the flight",
            "Pass arrival immigration", "Collect your luggage", "Find a taxi", "Travel to your hotel",
        };

        public static TravelBooking Booking => SaveManager.Data.travel;

        /// Mark objective `stage` done. Refused unless every earlier objective is done.
        public static bool Advance(TravelStage stage)
        {
            var booking = Booking;
            var number = (int)stage;
            if (number <= booking.stage) return true;
            if (number != booking.stage + 1) return false;
            booking.stage = number;
            QuestManager.Get(Id, Title);
            if (number >= Objectives.Length - 1) QuestManager.Complete(Id, 250, "Arrived at " + booking.Where.Hotel + ", " + booking.Where.City);
            else QuestManager.Advance(Id, number, Next);
            SaveManager.Save();
            return true;
        }

        public static string Next => Booking.stage + 1 < Objectives.Length ? Objectives[Booking.stage + 1] : "Trip complete!";

        public static void Restart()
        {
            var booking = new TravelBooking();
            SaveManager.Data.travel = booking;
            QuestManager.Restart(Id, Title);
            SaveManager.Save();
        }
    }
}
