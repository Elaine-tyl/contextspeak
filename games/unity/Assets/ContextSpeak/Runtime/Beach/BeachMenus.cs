using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Palm Bay menus (fictional prices). Café items and customisations follow the brief exactly.
    public static class BeachMenus
    {
        private static MenuEntry E(string id, string name, string category, int cents, string description, string icon, Color colour, string allergens = "", string options = "") =>
            new MenuEntry { id = id, name = name, category = category, cents = cents, description = description, icon = icon, colour = colour, allergens = allergens, options = options };

        private static MenuCatalog Catalog(string name, string title, string[] categories, List<MenuEntry> items)
        {
            var menu = ScriptableObject.CreateInstance<MenuCatalog>();
            menu.name = name; menu.Title = title; menu.Categories = categories; menu.Items = items;
            return menu;
        }

        private const string Size = "Size:Small/Regular+60/Large+110";
        private const string Sugar = "Sugar:Normal/Less sweet/No sugar/Extra sweet";
        private const string Milk = "Milk:Whole/Oat+60/Coconut+60/Skimmed";

        public static MenuCatalog Cafe() => Catalog("Palm Bay Café", "Driftwood Café & Juice Bar", new[] { "Coffee", "Tea", "Tropical Drinks", "Desserts" }, new List<MenuEntry>
        {
            E("espresso", "Espresso", "Coffee", 300, "A short, strong coffee.", "ESP", new Color(0.25f, 0.15f, 0.08f), "", Sugar),
            E("latte", "Latte", "Coffee", 450, "Espresso with steamed milk.", "LATTE", new Color(0.8f, 0.65f, 0.45f), "dairy (unless plant milk)", Size + "\n" + Milk + "\n" + Sugar),
            E("cappuccino", "Cappuccino", "Coffee", 450, "Espresso, milk and thick foam.", "CAPP", new Color(0.75f, 0.58f, 0.4f), "dairy (unless plant milk)", Size + "\n" + Milk + "\n" + Sugar),
            E("iced-americano", "Iced Americano", "Coffee", 400, "Espresso and cold water over ice.", "ICE", new Color(0.3f, 0.2f, 0.12f), "", Size + "\n" + Sugar),
            E("iced-mocha", "Iced Mocha", "Coffee", 520, "Espresso, chocolate and cold milk over ice.", "MOCHA", new Color(0.45f, 0.28f, 0.18f), "dairy", Size + "\n" + Milk + "\n" + Sugar),
            E("matcha", "Matcha Latte", "Tea", 500, "Japanese green tea whisked with milk.", "MATCHA", new Color(0.55f, 0.75f, 0.35f), "dairy (unless plant milk)", Size + "\n" + Milk + "\n" + Sugar),
            E("lemon-tea", "Iced Lemon Tea", "Tea", 380, "Black tea, fresh lemon, ice.", "LEMON", new Color(0.95f, 0.8f, 0.3f), "", Size + "\n" + Sugar),
            E("chai", "Chai Tea", "Tea", 420, "Spiced tea with milk.", "CHAI", new Color(0.75f, 0.55f, 0.35f), "dairy", Size + "\n" + Sugar),
            E("coconut", "Fresh Coconut Water", "Tropical Drinks", 450, "Served in the coconut.", "COCO", new Color(0.9f, 0.95f, 0.85f)),
            E("mango", "Mango Smoothie", "Tropical Drinks", 550, "Fresh mango, yoghurt, ice.", "MANGO", new Color(1f, 0.7f, 0.15f), "dairy", Size + "\n" + Sugar),
            E("watermelon", "Watermelon Juice", "Tropical Drinks", 480, "Pressed to order.", "MELON", new Color(0.95f, 0.3f, 0.35f), "", Size + "\n" + Sugar),
            E("pineapple", "Pineapple Juice", "Tropical Drinks", 480, "Sweet and tangy.", "PINE", new Color(1f, 0.85f, 0.3f), "", Size + "\n" + Sugar),
            E("passion", "Passion Fruit Cooler", "Tropical Drinks", 520, "Passion fruit, lime, soda water.", "PASS", new Color(0.85f, 0.55f, 0.2f), "", Size + "\n" + Sugar),
            E("coconut-cake", "Coconut Cake", "Desserts", 550, "Light sponge with toasted coconut.", "CAKE", new Color(0.95f, 0.92f, 0.85f), "gluten, dairy, egg"),
            E("cheesecake", "Mango Cheesecake", "Desserts", 620, "Baked cheesecake, mango glaze.", "CHEESE", new Color(1f, 0.8f, 0.4f), "gluten, dairy, egg"),
            E("croissant", "Croissant", "Desserts", 350, "Baked this morning.", "CROIS", new Color(0.9f, 0.7f, 0.35f), "gluten, dairy", "Warm:Warmed/Not warmed"),
            E("ice-cream", "Ice Cream", "Desserts", 400, "Two scoops.", "ICE", new Color(0.98f, 0.9f, 0.85f), "dairy", "Flavour:Coconut/Mango/Vanilla/Chocolate"),
        });

        public static MenuCatalog Restaurant() => Catalog("Coral Kitchen", "Coral Kitchen — Beachfront Dining", new[] { "Starters", "Seafood", "Mains", "Desserts", "Drinks" }, new List<MenuEntry>
        {
            E("prawns", "Garlic Butter Prawns", "Starters", 1450, "Grilled king prawns, garlic butter, lime.", "PRAWN", new Color(0.95f, 0.5f, 0.35f), "shellfish, dairy"),
            E("ceviche", "Fish Ceviche", "Starters", 1300, "Raw white fish cured in lime, chilli, coriander.", "CEVI", new Color(0.9f, 0.9f, 0.75f), "fish"),
            E("salad", "Papaya Salad", "Starters", 1050, "Green papaya, tomato, peanuts, chilli dressing.", "SALAD", new Color(0.55f, 0.8f, 0.4f), "peanuts, fish sauce", "Spice:Mild/Medium/Hot"),
            E("snapper", "Whole Grilled Red Snapper", "Seafood", 3200, "Catch of the day, sambal, rice.", "FISH", new Color(0.85f, 0.4f, 0.3f), "fish", "Side:Rice/Fries/Salad"),
            E("lobster", "Half Lobster", "Seafood", 4800, "Grilled with herb butter.", "LOBST", new Color(0.9f, 0.3f, 0.2f), "shellfish, dairy"),
            E("curry", "Coconut Fish Curry", "Mains", 2400, "Mild yellow curry, coconut milk, jasmine rice.", "CURRY", new Color(0.95f, 0.75f, 0.3f), "fish, may contain nuts", "Spice:Mild/Medium/Hot"),
            E("chicken", "Lemongrass Chicken", "Mains", 2200, "Grilled chicken thigh, lemongrass, salad.", "CHKN", new Color(0.85f, 0.65f, 0.35f), ""),
            E("veg", "Vegetable Laksa", "Mains", 1900, "Spicy coconut noodle soup. Vegan.", "LAKSA", new Color(0.9f, 0.55f, 0.25f), "soy, gluten"),
            E("pudding", "Mango Sticky Rice", "Desserts", 900, "Coconut sticky rice, fresh mango.", "MANGO", new Color(1f, 0.8f, 0.3f), ""),
            E("lime", "Fresh Lime Soda", "Drinks", 450, "Sweet or salty.", "LIME", new Color(0.7f, 0.9f, 0.4f), "", "Style:Sweet/Salty"),
            E("water", "Sparkling Water", "Drinks", 350, "500 ml.", "H2O", new Color(0.5f, 0.75f, 0.95f)),
        });

        public static MenuCatalog RoomService() => Catalog("Room Service", "Palm Bay Resort — In-Room Dining", new[] { "All day" }, new List<MenuEntry>
        {
            E("club", "Club Sandwich", "All day", 1600, "With fries.", "CLUB", new Color(0.9f, 0.75f, 0.45f), "gluten, egg"),
            E("noodles", "Fried Noodles", "All day", 1500, "Wok-fried with vegetables and egg.", "MEE", new Color(0.85f, 0.6f, 0.3f), "gluten, egg, soy"),
            E("fruit", "Tropical Fruit Plate", "All day", 1100, "Mango, papaya, pineapple, dragon fruit.", "FRUIT", new Color(0.95f, 0.5f, 0.55f)),
            E("juice", "Orange Juice", "All day", 500, "Freshly squeezed.", "OJ", new Color(1f, 0.65f, 0.15f)),
        });

        public static MenuCatalog Souvenirs() => Catalog("Shell & Sand", "Shell & Sand Souvenirs", new[] { "Gifts" }, new List<MenuEntry>
        {
            E("bracelet", "Shell Bracelet", "Gifts", 800, "Handmade by local artists.", "SHELL", new Color(0.95f, 0.85f, 0.75f)),
            E("sarong", "Batik Sarong", "Gifts", 2200, "Hand-dyed cotton.", "BATIK", new Color(0.2f, 0.45f, 0.7f), "", "Colour:Blue/Orange/Green"),
            E("hat", "Straw Sun Hat", "Gifts", 1800, "Wide brim.", "HAT", new Color(0.85f, 0.75f, 0.5f)),
            E("postcard", "Palm Bay Postcards (5)", "Gifts", 500, "Sunsets and turtles.", "POST", new Color(0.95f, 0.6f, 0.4f)),
        });

        public static MenuCatalog Club() => Catalog("Tide Beach Club", "Tide Beach Club — Mocktails", new[] { "Mocktails" }, new List<MenuEntry>
        {
            E("cooler", "Passion Fruit Cooler", "Mocktails", 650, "Passion fruit, lime, soda.", "PASS", new Color(0.85f, 0.55f, 0.2f)),
            E("colada", "Virgin Piña Colada", "Mocktails", 700, "Pineapple and coconut cream.", "COLADA", new Color(0.98f, 0.95f, 0.8f), "dairy"),
            E("sunset", "Sunset Spritz (alcohol-free)", "Mocktails", 650, "Orange, grenadine, soda.", "SPRITZ", new Color(1f, 0.45f, 0.3f)),
        });
    }
}
