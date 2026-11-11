using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [Serializable] public class LifeItem
    {
        public string id, name, category;
        public int price;
    }
    [Serializable] public class LifeNpc
    {
        public string id, name, role, place;
        public float x, z, standX, standZ;
        public Vector3 Position => new Vector3(x, 0.05f, z);
        public Vector3 Stand => new Vector3(standX, 0.05f, standZ);
    }
    [Serializable] public class LifeStep { public string id, npc, question, goal, sample; }
    [Serializable] public class LifeCatalog
    {
        public LifeItem[] items;
        public LifeNpc[] npcs;
        public LifeStep[] steps;
        private static LifeCatalog cached;
        public static LifeCatalog Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("LifeCatalog");
            var quest = Resources.Load<LifeQuestDefinition>("FirstDayQuest");
            var data = quest != null ? quest.Read() : asset == null ? null : JsonUtility.FromJson<LifeCatalog>(asset.text);
            if (data == null || data.steps == null || data.steps.Length != 12 || data.items == null || data.items.Length != 6 || data.npcs == null || data.npcs.Length != 4)
                throw new FormatException("Rebuild the authored Life catalog.");
            if (data.steps.Any(step => step == null || string.IsNullOrWhiteSpace(step.id) || string.IsNullOrWhiteSpace(step.question) || step.question.Length > 240 || !data.npcs.Any(npc => npc.id == step.npc))
                || data.steps.Select(step => step.id).Distinct().Count() != 12
                || data.items.Any(item => item == null || item.price <= 0 || item.price > 2000)
                || data.items.Select(item => item.id).Distinct().Count() != 6)
                throw new FormatException("Life tasks need unique IDs, known NPCs and valid prices.");
            cached = data;
            return cached;
        }
    }
    [Serializable] public class LifeReview
    {
        public bool accepted;
        public string correction, feedback, acknowledgement, choice;
    }
    [Serializable] public class LifeReply
    {
        public string step, text, repair, drink;
    }
    [Serializable] internal class LifeSave
    {
        public int version, step;
        public string drink = "", temperature = "", service = "", repair = "", previous = "";
        public bool practice;
    }

    // Progress is the receipt: balance and inventory are derived from validated prerequisites.
    // No model-provided amount, action or saved balance can create money or purchases.
    public sealed class LifeMission
    {
        private readonly LifeCatalog catalog = LifeCatalog.Load();
        private LifeSave state = new LifeSave { version = 1 };
        public int Step => state.step;
        public bool Complete => Step == catalog.steps.Length;
        public bool Practice => state.practice;
        public string Repair => state.repair;
        public string Drink => state.drink;
        public string Temperature => state.temperature;
        public string Service => state.service;
        public string Feedback { get; private set; } = "";
        public bool DinnerReady => Step >= 5;
        public LifeStep Current => catalog.steps[Math.Min(Step, catalog.steps.Length - 1)];
        public LifeNpc Destination => catalog.npcs.First(npc => npc.id == Current.npc);
        public int DrinkPrice => catalog.items.FirstOrDefault(item => item.id == Drink)?.price ?? 0;
        public int Balance => 2000 - (Step >= 4 ? 700 : 0) - (Step >= 9 ? DrinkPrice : 0);
        public string Cart => Step == 2 ? "3 apples · $3" : Step == 3 ? "3 apples + 2 carrots · $7" : "Empty";
        public string Bag => Step == 4 ? "3 apples · 2 carrots" : Step >= 9 ? "1 " + Drink : "Empty";
        public string Receipt => Step >= 9 ? $"PAID · 1 {Temperature} {Drink}, {Service}\n${DrinkPrice / 100.0:F2} · Thank you!" : "";
        public string Objective => Complete ? "First day complete! Dinner is ready and your plans with Alex are made."
            : $"{Step + 1} / {catalog.steps.Length} · {Destination.place}\n{Current.goal}";
        public string Question => Complete ? "That was a lovely catch-up. See you soon!"
            : Step == 8 ? $"One {Temperature} {Drink}, {Service}. That's ${DrinkPrice / 100.0:F2}. Shall I put your order through?"
            : Current.question;
        private bool KnownDrink(string id)
        {
            return catalog.items.Any(item => item.id == id && item.category == "drink");
        }
        private static string Words(string text)
        {
            return Regex.Replace((text ?? "").ToLowerInvariant().Replace('’', '\''), @"[^a-z0-9' ]|\s+", " ").Trim();
        }
        public bool Apply(int expectedStep, string text, LifeReview review, bool spoken)
        {
            if (Complete || expectedStep != Step || string.IsNullOrWhiteSpace(text) || text.Length > 400 || review == null
                || review.correction == null || review.correction.Length > 240 || string.IsNullOrWhiteSpace(review.feedback) || review.feedback.Length > 240
                || string.IsNullOrWhiteSpace(review.acknowledgement) || review.acknowledgement.Length > 160) return false;
            Feedback = review.feedback;
            if (!spoken)
            {
                Feedback += " Typed rehearsal only. Record your answer to continue.";
                return false;
            }
            if (!review.accepted || !string.IsNullOrWhiteSpace(review.correction))
            {
                state.repair = string.IsNullOrWhiteSpace(review.correction) ? Current.sample : review.correction;
                state.previous = text.Trim();
                return false;
            }
            if (Repair != "" && Words(text) == Words(state.previous) && Words(Repair) != Words(state.previous))
            {
                Feedback = "Try the corrected expression in a new recording. The same wording still needs repair.";
                return false;
            }
            if (Step == 5)
            {
                if (!KnownDrink(review.choice) || catalog.items.First(item => item.id == review.choice).price > Balance || LifeChoices.Select("cafe_order", text) != review.choice) return false;
                state.drink = review.choice;
            }
            if (Step == 6)
            {
                var selected = LifeChoices.Select("cafe_temperature", text);
                if (selected == "") return false;
                state.temperature = selected;
            }
            if (Step == 7)
            {
                var selected = LifeChoices.Select("cafe_service", text);
                if (selected == "") return false;
                state.service = selected;
            }
            if (Step == 3 && LifeChoices.Select("market_pay", text) == "") return false;
            if (Step == 8 && !LifeChoices.Confirmed(text)) return false;
            state.step++;
            state.repair = state.previous = "";
            return true;
        }
        public string Save()
        {
            return JsonUtility.ToJson(state);
        }
        public bool Restore(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 4000) return false;
            try
            {
                var saved = JsonUtility.FromJson<LifeSave>(json);
                if (saved == null || saved.version != 1 || saved.practice || saved.step < 0 || saved.step > 12
                    || saved.drink == null || saved.temperature == null || saved.service == null || saved.repair == null || saved.previous == null
                    || saved.repair.Length > 240 || saved.previous.Length > 400 || (saved.repair == "") != (saved.previous == "")
                    || (saved.repair != "" && (string.IsNullOrWhiteSpace(saved.repair) || string.IsNullOrWhiteSpace(saved.previous)))
                    || (saved.step == 12 && saved.repair != "")
                    || (saved.step < 6 ? saved.drink != "" : !KnownDrink(saved.drink))
                    || (saved.step < 7 ? saved.temperature != "" : !new[] { "hot", "iced" }.Contains(saved.temperature))
                    || (saved.step < 8 ? saved.service != "" : !new[] { "for here", "to go" }.Contains(saved.service))) return false;
                state = saved;
                Feedback = "";
                return true;
            }
            catch (ArgumentException) { return false; }
        }
        public void StartPractice(string stage)
        {
            state = new LifeSave { version = 1, practice = true, step = stage == "market" ? 1 : stage == "cafe" ? 5 : stage == "friend" ? 9 : 0 };
            if (Step == 9) { state.drink = "coffee"; state.temperature = "hot"; state.service = "for here"; }
            Feedback = "";
        }
    }
}
