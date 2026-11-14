using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Fictional airport shop catalogues (AirportShopUI uses the shared menu panel).
    public static class AirportMenus
    {
        private static MenuEntry E(string id, string name, string category, int cents, string description, string icon, Color colour, string allergens = "", string options = "") =>
            new MenuEntry { id = id, name = name, category = category, cents = cents, description = description, icon = icon, colour = colour, allergens = allergens, options = options };

        private static MenuCatalog Catalog(string name, string title, string[] categories, List<MenuEntry> items)
        {
            var menu = ScriptableObject.CreateInstance<MenuCatalog>();
            menu.name = name; menu.Title = title; menu.Categories = categories; menu.Items = items;
            return menu;
        }

        public static MenuCatalog DutyFree() => Catalog("Duty Free", "Skyline Duty Free", new[] { "Fragrance", "Chocolate", "Electronics", "Travel" }, new List<MenuEntry>
        {
            E("perfume", "Cedar & Rain Eau de Parfum 50 ml", "Fragrance", 6400, "Fresh woody scent. Duty-free price.", "EDP", new Color(0.55f, 0.65f, 0.75f)),
            E("chocolate", "Alpine Milk Chocolate Box", "Chocolate", 1800, "24 pralines — a popular gift.", "CHOC", new Color(0.4f, 0.25f, 0.15f), "milk, nuts, soy"),
            E("headphones", "Noise-Cancelling Headphones", "Electronics", 14900, "Long battery life for long flights.", "AUDIO", new Color(0.15f, 0.15f, 0.18f), "", "Colour:Black/Silver"),
            E("pillow", "Memory-Foam Travel Pillow", "Travel", 2500, "Fits in carry-on.", "ZZZ", new Color(0.35f, 0.5f, 0.75f), "", "Colour:Navy/Grey"),
            E("adapter", "Universal Plug Adapter", "Travel", 2200, "Works in most countries.", "PLUG", new Color(0.85f, 0.85f, 0.85f)),
        });

        public static MenuCatalog Coffee()
        {
            var menu = MenuLibrary.Cafe();
            menu.name = "Skyline Coffee"; menu.Title = "Skyline Coffee — Gate Side";
            return menu;
        }

        public static MenuCatalog Dining()
        {
            var menu = MenuLibrary.Restaurant();
            menu.name = "Runway Kitchen"; menu.Title = "Runway Kitchen — Quick Meals";
            menu.Items.RemoveAll(item => item.id == "steak" || item.id == "calamari");
            return menu;
        }

        public static MenuCatalog Books() => Catalog("Gate Books", "Gate Books", new[] { "Books", "Magazines" }, new List<MenuEntry>
        {
            E("novel", "The Night Train (paperback)", "Books", 1400, "A mystery set on a sleeper train.", "BOOK", new Color(0.3f, 0.35f, 0.55f)),
            E("phrasebook", "Travel English Phrasebook", "Books", 1100, "Useful phrases for airports, hotels and taxis.", "ABC", new Color(0.1f, 0.5f, 0.45f)),
            E("guide", "City Guide: Your Destination", "Books", 1900, "Maps, food and neighbourhoods.", "MAP", new Color(0.75f, 0.45f, 0.15f)),
            E("magazine", "World Travel Magazine", "Magazines", 900, "This month: slow travel by train.", "MAG", new Color(0.8f, 0.2f, 0.25f)),
        });

        public static MenuCatalog Souvenirs() => Catalog("Metro Souvenirs", "Metro Souvenirs", new[] { "Gifts" }, new List<MenuEntry>
        {
            E("magnet", "Metro City Fridge Magnet", "Gifts", 600, "The tram and the skyline.", "MC", new Color(0.2f, 0.5f, 0.8f)),
            E("tote", "Metro City Tote Bag", "Gifts", 1500, "Organic cotton.", "BAG", new Color(0.85f, 0.8f, 0.7f)),
            E("model", "Model SkyLark Airliner", "Gifts", 3200, "1:200 scale die-cast model.", "SL", new Color(0.9f, 0.9f, 0.95f)),
        });

        public static MenuCatalog Convenience() => Catalog("QuickStop", "QuickStop Convenience", new[] { "Snacks", "Drinks", "Essentials" }, new List<MenuEntry>
        {
            E("water", "Bottled Water 500 ml", "Drinks", 350, "Buy after security to take on board.", "H2O", new Color(0.5f, 0.75f, 0.95f)),
            E("sandwich", "Chicken Sandwich", "Snacks", 750, "Freshly made today.", "SAND", new Color(0.9f, 0.75f, 0.5f), "gluten, egg"),
            E("gum", "Mint Chewing Gum", "Snacks", 250, "Helps with ear pressure.", "GUM", new Color(0.6f, 0.9f, 0.7f)),
            E("charger", "USB-C Charging Cable", "Essentials", 1600, "1 metre.", "USB", new Color(0.2f, 0.2f, 0.22f)),
        });
    }
}
