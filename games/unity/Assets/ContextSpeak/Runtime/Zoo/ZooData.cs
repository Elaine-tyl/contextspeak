using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Everything the zoo remembers (saved inside LifeProgress).
    [Serializable]
    public sealed class ZooProgress
    {
        public int stage;
        public string ticket = "";                       // "", "standard", "student", "family"
        public bool ticketValidated;
        public List<string> seen = new List<string>();    // species whose information board was read
        public List<string> done = new List<string>();    // finished conversations and optional quests
        public List<int> skipped = new List<int>();       // objectives whose area is not built yet
        public int photos, bestQuiz;
        public ZooStage Stage => (ZooStage)stage;
        public bool Done(string id) => done.Contains(id);
        public void Mark(string id) { if (!done.Contains(id)) done.Add(id); }
    }

    public enum ZooStage
    {
        NotStarted = 0, AtZoo = 1, BoughtTicket = 2, OpenedMap = 3, MetGuide = 4, AtElephants = 5, DescribedElephant = 6, AtGiraffes = 7,
        ComparedAnimals = 8, WatchedPenguins = 9, VisitedAviary = 10, OrderedDrink = 11, RodeTram = 12, ConservationTalk = 13, Rewarded = 14,
    }

    public sealed class ZooTicket { public string Id, Name, Description; public int Cents; }

    public static class ZooTickets
    {
        public static readonly ZooTicket[] All =
        {
            new ZooTicket { Id = "standard", Name = "Standard ticket", Description = "One adult, all habitats, valid today", Cents = 2400 },
            new ZooTicket { Id = "student", Name = "Student ticket", Description = "One student with ID, all habitats, valid today", Cents = 1800 },
            new ZooTicket { Id = "family", Name = "Family ticket", Description = "Two adults and two children, all habitats, valid today", Cents = 6000 },
        };
        public static ZooTicket Find(string id) => All.FirstOrDefault(ticket => ticket.Id == id);
    }

    /// ZOO_VISIT_001 — My First Zoo Adventure (14 objectives, in order). Objectives whose area has
    /// not been built yet are skipped automatically and recorded, so the quest can always be finished.
    public static class ZooQuest
    {
        public const string Id = "ZOO_VISIT_001", Title = "My First Zoo Adventure";
        public static readonly string[] Objectives =
        {
            "", "Travel to the zoo", "Buy an admission ticket", "Open the zoo map (M)", "Speak to the zoo guide", "Visit the elephant habitat",
            "Describe an elephant in English", "Visit the giraffe enclosure", "Compare giraffes and zebras", "Watch the penguins swimming",
            "Visit the bird aviary", "Order a drink at the café", "Join a guided tram tour", "Complete the wildlife conservation speaking task", "Receive your rewards",
        };
        /// Areas planned for later phases (Penguin Cove, the Aviary, the Safari Tram).
        public static readonly List<int> ComingSoon = new List<int> { (int)ZooStage.WatchedPenguins, (int)ZooStage.VisitedAviary, (int)ZooStage.RodeTram };
        public static readonly (string id, string title)[] Optional =
        {
            ("ZOO_PHOTO", "Wildlife photography challenge"), ("ZOO_QUIZ", "Educational animal quiz"), ("ZOO_VOCAB", "Animal vocabulary scavenger hunt"),
            ("ZOO_AWARE", "Zoo conservation awareness task"), ("ZOO_HELP", "Help a visitor find the right exhibit"),
        };

        public static ZooProgress Progress => SaveManager.Data.zoo;

        /// Moves to `stage` if it is the next one (earlier stages are accepted silently). Returns false if out of order.
        public static bool Advance(ZooStage stage)
        {
            var progress = Progress;
            var number = (int)stage;
            if (number <= progress.stage) return true;
            if (number != progress.stage + 1) return false;
            progress.stage = number;
            // Skip what cannot be done yet.
            while (ComingSoon.Contains(progress.stage + 1) && progress.stage + 1 < Objectives.Length)
            {
                progress.stage++;
                if (!progress.skipped.Contains(progress.stage)) progress.skipped.Add(progress.stage);
            }
            QuestManager.Get(Id, Title);
            if (progress.stage >= (int)ZooStage.ConservationTalk)
            {
                progress.stage = (int)ZooStage.Rewarded;
                QuestManager.Complete(Id, 250, "Zoo adventure complete");
                InventoryManager.Add("zoo-badge", "Junior Wildlife Ranger badge");
            }
            else QuestManager.Advance(Id, progress.stage, Next);
            SaveManager.Save();
            return true;
        }

        public static string Next => Progress.stage + 1 < Objectives.Length ? Objectives[Progress.stage + 1] : "Adventure complete!";

        public static void CompleteOptional(string id, int xp)
        {
            if (Progress.Done(id)) return;
            Progress.Mark(id);
            var title = Optional.FirstOrDefault(item => item.id == id).title ?? id;
            QuestManager.Get(id, title);
            QuestManager.Complete(id, xp, title + " ✔");
        }
    }

    public sealed class QuizQuestion { public string Question; public string[] Answers; public int Correct; public string Why; }

    public static class ZooQuiz
    {
        public static readonly QuizQuestion[] Questions =
        {
            new QuizQuestion { Question = "What does an elephant use its trunk for?", Answers = new[] { "Only for smelling", "Breathing, drinking, smelling and picking things up", "Only for making noise" }, Correct = 1, Why = "The trunk is a nose and a hand at the same time." },
            new QuizQuestion { Question = "What do giraffes usually eat?", Answers = new[] { "Leaves from tall trees", "Fish", "Small animals" }, Correct = 0, Why = "Giraffes browse leaves, especially from acacia trees." },
            new QuizQuestion { Question = "Why does every zebra look different?", Answers = new[] { "They are different colours", "Each one has its own pattern of stripes", "They have different numbers of legs" }, Correct = 1, Why = "Stripe patterns are unique, like fingerprints." },
            new QuizQuestion { Question = "What does 'endangered' mean?", Answers = new[] { "Dangerous to people", "Very common", "At risk of disappearing for ever" }, Correct = 2, Why = "An endangered species could become extinct." },
            new QuizQuestion { Question = "Which word means 'a group of elephants'?", Answers = new[] { "A herd", "A flock", "A school" }, Correct = 0, Why = "Herd: elephants, zebras, antelopes. Flock: birds and sheep. School: fish." },
        };
    }

    public static class ZooMenus
    {
        private static MenuEntry E(string id, string name, string category, int cents, string description, string icon, Color colour, string allergens = "", string options = "") =>
            new MenuEntry { id = id, name = name, category = category, cents = cents, description = description, icon = icon, colour = colour, allergens = allergens, options = options };

        private const string Size = "Size:Small/Regular+60/Large+110";
        private const string Sugar = "Sugar:Normal/Less sweet/No sugar";
        private const string Milk = "Milk:Whole/Oat+60/Soy+60/Skimmed";
        private const string Ice = "Ice:With ice/No ice";

        public static MenuCatalog Cafe()
        {
            var menu = ScriptableObject.CreateInstance<MenuCatalog>();
            menu.name = "Zoo Café"; menu.Title = "Savannah View Café"; menu.Categories = new[] { "Coffee", "Juice", "Snacks" };
            menu.Items = new List<MenuEntry>
            {
                E("latte", "Latte", "Coffee", 450, "Espresso with steamed milk.", "LATTE", new Color(0.8f, 0.65f, 0.45f), "dairy (unless plant milk)", Size + "\n" + Milk + "\n" + Sugar),
                E("americano", "Americano", "Coffee", 380, "Espresso and hot water.", "AMER", new Color(0.3f, 0.2f, 0.12f), "", Size + "\n" + Sugar),
                E("iced-coffee", "Iced coffee", "Coffee", 420, "Cold coffee over ice.", "ICED", new Color(0.45f, 0.3f, 0.2f), "dairy (unless plant milk)", Size + "\n" + Milk + "\n" + Sugar),
                E("orange", "Orange juice", "Juice", 400, "Freshly squeezed.", "ORANGE", new Color(1f, 0.65f, 0.15f), "", Size + "\n" + Ice),
                E("apple", "Apple juice", "Juice", 380, "Cloudy apple juice.", "APPLE", new Color(0.8f, 0.85f, 0.35f), "", Size + "\n" + Ice),
                E("mango", "Mango smoothie", "Juice", 520, "Mango, yoghurt and ice.", "MANGO", new Color(1f, 0.75f, 0.2f), "dairy", Size + "\n" + Sugar),
                E("water", "Still water", "Juice", 200, "A refillable bottle.", "WATER", new Color(0.7f, 0.85f, 0.95f)),
                E("muffin", "Banana muffin", "Snacks", 350, "Baked this morning.", "MUFFIN", new Color(0.85f, 0.65f, 0.35f), "gluten, dairy, egg"),
                E("sandwich", "Cheese sandwich", "Snacks", 550, "Cheddar, tomato and lettuce.", "SAND", new Color(0.95f, 0.85f, 0.55f), "gluten, dairy"),
            };
            return menu;
        }
    }
}
