using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    [Serializable] public sealed class InventoryEntry { public string id, name; public int count; }
    [Serializable] public sealed class QuestEntry { public string id, title, note = ""; public int step; public bool done; }

    /// Everything that travels between the city and the locations: money, bag, quests, XP and the
    /// door you came in by. JsonUtility-friendly (public fields and lists only).
    [Serializable]
    public sealed class LifeProgress
    {
        public int version = 1;
        public int cents = 10000;          // $100.00 starting money (fictional)
        public int xp;
        public List<InventoryEntry> inventory = new List<InventoryEntry>();
        public List<QuestEntry> quests = new List<QuestEntry>();
        public string returnScene = "", returnEntrance = "";
        public float returnX, returnY, returnZ, returnYaw;
        public bool learningMode = true;
        public bool forceMock;
        public TravelBooking travel = new TravelBooking();     // airport journey (see Airport/TravelData.cs)
        public BeachProgress beach = new BeachProgress();      // Palm Bay resort (see Beach/BeachData.cs)
        public ZooProgress zoo = new ZooProgress();            // Wildlife zoo (see Zoo/ZooData.cs)
    }

    /// Single save for the life simulation, on top of the existing character profile (name, look, band).
    public static class SaveManager
    {
        public const string Key = "contextspeak-life-progress-v1";
        private static LifeProgress current;
        public static event Action Changed;
        /// False during automated checks so tests never overwrite the player's real save.
        public static bool Persist = true;

        public static LifeProgress Data
        {
            get
            {
                if (current != null) return current;
                var json = PlayerPrefs.GetString(Key, "");
                if (json != "")
                    try { current = JsonUtility.FromJson<LifeProgress>(json); }
                    catch (ArgumentException) { current = null; }
                if (current == null || current.inventory == null || current.quests == null) current = new LifeProgress();
                if (current.travel == null) current.travel = new TravelBooking();
                if (current.beach == null) current.beach = new BeachProgress();
                if (current.zoo == null) current.zoo = new ZooProgress();
                return current;
            }
        }

        public static CharacterProfile Profile => CharacterProfiles.Draft();
        public static string PlayerName => string.IsNullOrWhiteSpace(Profile.Name) ? "Emma" : Profile.Name;
        public static Slice.IeltsBand Band
        {
            get => (Slice.IeltsBand)Mathf.Clamp(Profile.Band, 3, 9);
            set
            {
                var profile = Profile;
                profile.Band = Mathf.Clamp((int)value, 3, 9);
                CharacterProfiles.TrySave(profile, out _);
                Save();
            }
        }

        public static void Save()
        {
            if (Persist)
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
        }

        public static void Reset()
        {
            current = new LifeProgress();
            Save();
        }

        /// Testing hook: replace the in-memory save without touching PlayerPrefs.
        public static void UseInMemory(LifeProgress progress) { current = progress; }
    }

    public static class CurrencyManager
    {
        public static int Cents => SaveManager.Data.cents;
        public static string Format(int cents) => "$" + (cents / 100f).ToString("0.00");
        public static string Balance => Format(Cents);

        public static bool TrySpend(int cents, out string message)
        {
            if (cents < 0) { message = "Invalid amount."; return false; }
            if (cents > SaveManager.Data.cents) { message = "Not enough money: you have " + Balance + "."; return false; }
            SaveManager.Data.cents -= cents;
            SaveManager.Save();
            message = "Paid " + Format(cents) + ". Balance " + Balance + ".";
            return true;
        }

        public static void Add(int cents)
        {
            SaveManager.Data.cents += Mathf.Max(0, cents);
            SaveManager.Save();
        }
    }

    public static class InventoryManager
    {
        public static IReadOnlyList<InventoryEntry> Items => SaveManager.Data.inventory;
        public static int Count(string id) => SaveManager.Data.inventory.Where(item => item.id == id).Sum(item => item.count);

        public static void Add(string id, string name, int count = 1)
        {
            var entry = SaveManager.Data.inventory.FirstOrDefault(item => item.id == id);
            if (entry == null) SaveManager.Data.inventory.Add(entry = new InventoryEntry { id = id, name = name });
            entry.count += Mathf.Max(1, count);
            SaveManager.Save();
        }

        public static bool Remove(string id, int count = 1)
        {
            var entry = SaveManager.Data.inventory.FirstOrDefault(item => item.id == id);
            if (entry == null || entry.count < count) return false;
            entry.count -= count;
            if (entry.count == 0) SaveManager.Data.inventory.Remove(entry);
            SaveManager.Save();
            return true;
        }
    }

    public static class QuestManager
    {
        public static QuestEntry Get(string id, string title)
        {
            var quest = SaveManager.Data.quests.FirstOrDefault(item => item.id == id);
            if (quest == null) { SaveManager.Data.quests.Add(quest = new QuestEntry { id = id, title = title }); SaveManager.Save(); }
            return quest;
        }

        public static void Advance(string id, int step, string note)
        {
            var quest = SaveManager.Data.quests.FirstOrDefault(item => item.id == id);
            if (quest == null || quest.done) return;
            quest.step = Mathf.Max(quest.step, step);
            quest.note = note;
            SaveManager.Save();
        }

        public static void Complete(string id, int xp, string note)
        {
            var quest = SaveManager.Data.quests.FirstOrDefault(item => item.id == id);
            if (quest == null) return;
            if (!quest.done) SaveManager.Data.xp += xp;
            quest.done = true;
            quest.note = note;
            SaveManager.Save();
        }

        /// Replaying a location restarts its quest; XP is only awarded once per completion.
        public static void Restart(string id, string title)
        {
            var quest = Get(id, title);
            quest.done = false;
            quest.step = 0;
            quest.note = "";
            SaveManager.Save();
        }
    }
}
