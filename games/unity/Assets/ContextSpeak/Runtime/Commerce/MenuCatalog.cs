using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A fictional menu item. Options use "Group:Choice/Choice+cents/..." per line, e.g.
    /// "Size:Small/Regular+50/Large+90" — the first choice is the default.
    [Serializable]
    public sealed class MenuEntry
    {
        public string id = "item", name = "Item", category = "Main Courses";
        [TextArea] public string description = "";
        public int cents = 1000;
        public string allergens = "";
        public string icon = "";              // short label for the card image ("☕", "SOUP", …)
        public Color colour = new Color(0.85f, 0.7f, 0.5f);
        [TextArea] public string options = "";

        public IEnumerable<(string group, string[] choices)> OptionGroups =>
            (options ?? "").Split('\n').Select(line => line.Trim()).Where(line => line.Contains(':'))
                .Select(line => (line.Substring(0, line.IndexOf(':')), line.Substring(line.IndexOf(':') + 1).Split('/')));

        public static (string label, int cents) Choice(string choice)
        {
            var plus = choice.LastIndexOf('+');
            if (plus > 0 && int.TryParse(choice.Substring(plus + 1), out var extra)) return (choice.Substring(0, plus).Trim(), extra);
            return (choice.Trim(), 0);
        }
    }

    [CreateAssetMenu(menuName = "ContextSpeak/Menu", fileName = "Menu")]
    public sealed class MenuCatalog : ScriptableObject
    {
        public string Title = "Menu";
        public string[] Categories = { "Appetizers", "Main Courses", "Desserts", "Hot Drinks", "Cold Drinks" };
        public List<MenuEntry> Items = new List<MenuEntry>();
        public MenuEntry Find(string id) => Items.FirstOrDefault(item => item.id == id);
    }

    [Serializable]
    public sealed class CartLine
    {
        public string id, name, choices = "";
        public int quantity = 1, unitCents;
        public int Total => quantity * unitCents;
    }

    /// The order being built: quantities, customisations and totals. Pure C# (verified by checks).
    [Serializable]
    public sealed class Cart
    {
        public List<CartLine> Lines = new List<CartLine>();
        public int Total => Lines.Sum(line => line.Total);
        public int Count => Lines.Sum(line => line.quantity);

        public CartLine Add(MenuEntry entry, string[] choices, int quantity = 1)
        {
            var extra = (choices ?? new string[0]).Sum(choice => MenuEntry.Choice(choice).cents);
            var label = string.Join(", ", (choices ?? new string[0]).Select(choice => MenuEntry.Choice(choice).label));
            var line = Lines.FirstOrDefault(item => item.id == entry.id && item.choices == label);
            if (line == null) Lines.Add(line = new CartLine { id = entry.id, name = entry.name, choices = label, unitCents = entry.cents + extra, quantity = 0 });
            line.quantity += Mathf.Max(1, quantity);
            return line;
        }

        public void Change(CartLine line, int delta)
        {
            if (line == null) return;
            line.quantity += delta;
            if (line.quantity <= 0) Lines.Remove(line);
        }

        public string Describe() => string.Join(", ", Lines.Select(line => line.quantity + " × " + line.name + (line.choices != "" ? " (" + line.choices + ")" : "")));
    }
}
