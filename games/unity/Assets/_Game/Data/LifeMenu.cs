using System;
using System.Linq;

namespace ContextSpeak.Cafe
{
    /// One row of a printed menu or price board, mirrored to the native and browser UI.
    [Serializable] public class LifeMenuItem
    {
        public string id, name, price, note, phrase;
        public bool planned, affordable, plannable;
    }

    /// Real-world style menus for the town. Prices always come from the authored catalog.
    /// Choosing a drink here only makes a plan: buying still requires the spoken order,
    /// so the menu helps learners prepare without replacing the speaking goal.
    public static class LifeMenu
    {
        private static readonly (string id, string note, string phrase)[] Details =
        {
            ("coffee", "Freshly brewed house coffee · hot or iced", "Could I get a coffee, please?"),
            ("latte", "Espresso with steamed milk · hot or iced", "I'd like a latte, please."),
            ("tea", "English breakfast or green tea · hot or iced", "Can I have a tea, please?"),
            ("lemonade", "Homemade, lightly sweet · served iced", "One lemonade, please."),
            ("apple", "Crisp local apples · sold each", "Three apples, please."),
            ("carrot", "Fresh carrots · sold each", "Two carrots, please."),
        };

        public static string Money(int cents) => "$" + (cents / 100.0).ToString(cents % 100 == 0 ? "F0" : "F2");
        public static string Note(string id) => Details.FirstOrDefault(entry => entry.id == id).note ?? "";
        public static string Phrase(string id) => Details.FirstOrDefault(entry => entry.id == id).phrase ?? "";

        public static LifeMenuItem[] Items(LifeCatalog catalog, string category, string planned, int balance)
        {
            return catalog.items.Where(item => item.category == category).Select(item => new LifeMenuItem
            {
                id = item.id,
                name = item.name,
                price = (category == "drink" ? "$" + (item.price / 100.0).ToString("F2") : Money(item.price) + " each"),
                note = Note(item.id),
                phrase = Phrase(item.id),
                planned = item.id == planned,
                affordable = item.price <= balance,
                plannable = category == "drink",
            }).ToArray();
        }

        /// A plan line such as "Your pick · Latte $6.50 · $6.50 left after paying".
        public static string Plan(LifeCatalog catalog, string planned, int balance)
        {
            var item = catalog.items.FirstOrDefault(entry => entry.id == planned && entry.category == "drink");
            if (item == null) return "";
            if (item.price > balance)
                return $"Your pick · {item.name} {Money(item.price)}\nThat is more than your {Money(balance)}. Choose something cheaper.";
            return $"Your pick · {item.name} {Money(item.price)} · {Money(balance - item.price)} left after paying\nSay it to Maya: “{Phrase(item.id)}”";
        }
    }
}
