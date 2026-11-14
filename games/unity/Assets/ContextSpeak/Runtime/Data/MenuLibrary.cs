using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Built-in fictional menus (saved as editable assets by the location builder).
    public static class MenuLibrary
    {
        private static MenuEntry E(string id, string name, string category, int cents, string description, string allergens, string icon, Color colour, string options = "") =>
            new MenuEntry { id = id, name = name, category = category, cents = cents, description = description, allergens = allergens, icon = icon, colour = colour, options = options };

        public static MenuCatalog Restaurant()
        {
            var menu = ScriptableObject.CreateInstance<MenuCatalog>();
            menu.name = "Harbour Table Menu";
            menu.Title = "Harbour Table — Dinner Menu";
            menu.Categories = new[] { "Appetizers", "Main Courses", "Desserts", "Drinks" };
            menu.Items = new List<MenuEntry>
            {
                E("tomato-soup", "Tomato & Basil Soup", "Appetizers", 750, "Roasted tomato soup with fresh basil and warm bread.", "gluten (bread)", "SOUP", new Color(0.85f, 0.3f, 0.2f), "Bread:With bread/No bread"),
                E("calamari", "Crispy Calamari", "Appetizers", 950, "Lightly fried squid with lemon and garlic mayonnaise.", "shellfish, egg, gluten", "SQUID", new Color(0.9f, 0.75f, 0.45f)),
                E("garden-salad", "Garden Salad", "Appetizers", 800, "Mixed leaves, cucumber, cherry tomatoes, lemon dressing.", "", "SALAD", new Color(0.45f, 0.75f, 0.35f), "Add:Nothing/Chicken+300/Halloumi+350"),
                E("fish-chips", "Fish & Chips", "Main Courses", 1850, "Beer-battered cod, chips, mushy peas and tartare sauce.", "fish, gluten, egg", "FISH", new Color(0.95f, 0.8f, 0.4f), "Side:Mushy peas/Side salad"),
                E("steak", "Sirloin Steak", "Main Courses", 2600, "250 g sirloin with peppercorn sauce and fries.", "dairy (sauce)", "STEAK", new Color(0.55f, 0.3f, 0.2f), "Cooked:Medium/Rare/Medium-rare/Well done\nSauce:Peppercorn/Garlic butter/No sauce"),
                E("risotto", "Mushroom Risotto", "Main Courses", 1700, "Creamy arborio rice with wild mushrooms and parmesan. Vegetarian.", "dairy", "RICE", new Color(0.85f, 0.8f, 0.65f), "Cheese:Parmesan/No cheese (vegan)"),
                E("burger", "Harbour Burger", "Main Courses", 1650, "Beef burger, cheddar, lettuce, tomato, house sauce, fries.", "gluten, dairy, egg", "BURGER", new Color(0.7f, 0.45f, 0.25f), "Cheese:Cheddar/No cheese\nBun:Brioche/Gluten-free+150"),
                E("brownie", "Chocolate Brownie", "Desserts", 750, "Warm brownie with vanilla ice cream.", "gluten, dairy, egg, nuts", "CAKE", new Color(0.35f, 0.2f, 0.12f)),
                E("sorbet", "Lemon Sorbet", "Desserts", 600, "Two scoops of lemon sorbet. Dairy-free.", "", "ICE", new Color(0.95f, 0.92f, 0.5f)),
                E("water", "Sparkling Water", "Drinks", 300, "500 ml bottle.", "", "H2O", new Color(0.5f, 0.75f, 0.95f), "Size:Small/Large+200"),
                E("lemonade", "Homemade Lemonade", "Drinks", 450, "Fresh lemons, mint, a little sugar.", "", "LEMON", new Color(0.95f, 0.9f, 0.45f), "Ice:With ice/No ice"),
                E("cola", "Cola", "Drinks", 350, "Classic or zero sugar.", "", "COLA", new Color(0.3f, 0.12f, 0.08f), "Type:Classic/Zero sugar"),
            };
            return menu;
        }

        public static MenuCatalog Cafe()
        {
            var menu = ScriptableObject.CreateInstance<MenuCatalog>();
            menu.name = "Forest Cafe Menu";
            menu.Title = "Forest Café — Drinks & Bakes";
            menu.Categories = new[] { "Hot Drinks", "Cold Drinks", "Bakery" };
            var milk = "Milk:Whole/Oat+60/Soy+60/Skimmed";
            var size = "Size:Small/Regular+50/Large+90";
            menu.Items = new List<MenuEntry>
            {
                E("latte", "Latte", "Hot Drinks", 420, "Espresso with steamed milk.", "dairy (unless plant milk)", "LATTE", new Color(0.8f, 0.65f, 0.45f), size + "\n" + milk + "\nSugar:No sugar/One sugar/Two sugars"),
                E("cappuccino", "Cappuccino", "Hot Drinks", 400, "Espresso, milk and thick foam.", "dairy (unless plant milk)", "CAPP", new Color(0.75f, 0.58f, 0.4f), size + "\n" + milk),
                E("americano", "Americano", "Hot Drinks", 320, "Espresso with hot water.", "", "AMER", new Color(0.3f, 0.2f, 0.12f), size + "\nMilk:Black/A little milk"),
                E("mint-tea", "Fresh Mint Tea", "Hot Drinks", 350, "Mint leaves from the forest garden.", "", "TEA", new Color(0.45f, 0.75f, 0.45f), size),
                E("iced-latte", "Iced Latte", "Cold Drinks", 460, "Espresso and cold milk over ice.", "dairy (unless plant milk)", "ICED", new Color(0.85f, 0.75f, 0.6f), size + "\n" + milk),
                E("berry-smoothie", "Berry Smoothie", "Cold Drinks", 520, "Strawberry, blueberry, banana and yoghurt.", "dairy", "BERRY", new Color(0.7f, 0.25f, 0.45f), size),
                E("croissant", "Butter Croissant", "Bakery", 300, "Baked this morning.", "gluten, dairy", "CROIS", new Color(0.9f, 0.7f, 0.35f), "Warm:Warmed/Not warmed"),
                E("brownie", "Walnut Brownie", "Bakery", 350, "Dark chocolate and walnuts.", "gluten, dairy, egg, nuts", "BROWN", new Color(0.35f, 0.2f, 0.12f)),
            };
            return menu;
        }
    }
}
