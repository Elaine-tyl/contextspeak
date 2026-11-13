using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.Slice
{
    public enum MenuCategory { Coffee, Tea, Smoothies, Juice, Mocktails, Breakfast, Lunch, Dinner, Desserts, Snacks }

    /// Customisation groups an item can offer. Each group has a fixed list of choices.
    public enum OptionGroup { Temperature, Size, Sugar, Milk, ExtraShot, Ice, Spice }

    [Serializable]
    public sealed class MenuItem
    {
        public string Id, Name, Description, Image;
        public MenuCategory Category;
        public int Price; // cents
        public string[] Ingredients = new string[0], Allergens = new string[0], Aliases = new string[0];
        public OptionGroup[] Options = new OptionGroup[0];
        public string[] Tags = new string[0]; // "dairy-free", "vegetarian", "seasonal", "popular", "spicy"
        public int PrepSeconds = 40;
        public bool Food => Category >= MenuCategory.Breakfast;
        public bool Has(OptionGroup group) => Array.IndexOf(Options, group) >= 0;
        public bool Tagged(string tag) => Array.IndexOf(Tags, tag) >= 0;
        /// Dairy-free when it has no milk allergen, or when its milk can be swapped.
        public bool CanBeDairyFree => Array.IndexOf(Allergens, "milk") < 0 || Has(OptionGroup.Milk);
    }

    /// Choice lists and price changes for each customisation group.
    public static class MenuOptions
    {
        public static readonly Dictionary<OptionGroup, string[]> Choices = new Dictionary<OptionGroup, string[]>
        {
            { OptionGroup.Temperature, new[] { "hot", "iced" } },
            { OptionGroup.Size, new[] { "small", "medium", "large" } },
            { OptionGroup.Sugar, new[] { "no sugar", "less sugar", "regular sugar", "extra sugar" } },
            { OptionGroup.Milk, new[] { "whole milk", "oat milk", "almond milk", "soy milk", "no milk" } },
            { OptionGroup.ExtraShot, new[] { "no extra shot", "extra shot" } },
            { OptionGroup.Ice, new[] { "regular ice", "less ice", "no ice" } },
            { OptionGroup.Spice, new[] { "mild", "medium spicy", "extra spicy" } },
        };
        /// Default used when the player says nothing about a group (null = the waiter must ask).
        public static string Default(OptionGroup group)
        {
            switch (group)
            {
                case OptionGroup.Sugar: return "regular sugar";
                case OptionGroup.Milk: return "whole milk";
                case OptionGroup.ExtraShot: return "no extra shot";
                case OptionGroup.Ice: return "regular ice";
                case OptionGroup.Spice: return "mild";
                default: return null; // temperature and size are always asked
            }
        }
        public static int Surcharge(string choice)
        {
            switch (choice)
            {
                case "medium": return 50;
                case "large": return 100;
                case "oat milk": case "almond milk": case "soy milk": return 60;
                case "extra shot": return 80;
                default: return 0;
            }
        }
        public static string GroupLabel(OptionGroup group)
        {
            switch (group)
            {
                case OptionGroup.Temperature: return "Hot or iced";
                case OptionGroup.ExtraShot: return "Extra espresso shot";
                case OptionGroup.Spice: return "Spice level";
                default: return group.ToString();
            }
        }
    }

    /// The Forest Café menu. Authored content; prices never come from AI output.
    public static class CafeMenu
    {
        private static readonly OptionGroup[] Espresso = { OptionGroup.Temperature, OptionGroup.Size, OptionGroup.Milk, OptionGroup.Sugar, OptionGroup.ExtraShot, OptionGroup.Ice };
        private static readonly OptionGroup[] Brewed = { OptionGroup.Temperature, OptionGroup.Size, OptionGroup.Sugar, OptionGroup.Ice };
        private static readonly OptionGroup[] Cold = { OptionGroup.Size, OptionGroup.Sugar, OptionGroup.Ice };

        private static MenuItem Item(string id, string name, MenuCategory category, int price, string description, string[] ingredients, string[] allergens, OptionGroup[] options, string[] tags = null, string[] aliases = null, int prep = 40)
        {
            return new MenuItem
            {
                Id = id, Name = name, Category = category, Price = price, Description = description, Image = "menu/" + id,
                Ingredients = ingredients, Allergens = allergens, Options = options ?? new OptionGroup[0],
                Tags = tags ?? new string[0], Aliases = aliases ?? new string[0], PrepSeconds = prep,
            };
        }

        public static readonly MenuItem[] Items =
        {
            Item("americano", "Americano", MenuCategory.Coffee, 420, "Double espresso topped with hot water. Smooth and bold.", new[] { "espresso", "water" }, new string[0], Brewed, new[] { "dairy-free" }, new[] { "black coffee", "long black", "coffee" }, 35),
            Item("latte", "Café latte", MenuCategory.Coffee, 550, "Espresso with silky steamed milk and a thin layer of foam.", new[] { "espresso", "milk" }, new[] { "milk" }, Espresso, new[] { "popular" }, new[] { "latte", "cafe latte" }, 45),
            Item("cappuccino", "Cappuccino", MenuCategory.Coffee, 520, "Espresso, steamed milk and a thick cap of foam with cocoa.", new[] { "espresso", "milk", "cocoa" }, new[] { "milk" }, Espresso, null, new[] { "cappuccino", "cappucino" }, 45),
            Item("flat-white", "Flat white", MenuCategory.Coffee, 530, "Ristretto shots with velvety micro-foam.", new[] { "espresso", "milk" }, new[] { "milk" }, Espresso, null, new[] { "flat white" }, 45),
            Item("forest-mocha", "Forest mocha", MenuCategory.Coffee, 620, "Espresso, dark chocolate, milk and a hint of hazelnut.", new[] { "espresso", "dark chocolate", "milk", "hazelnut" }, new[] { "milk", "tree nuts" }, Espresso, null, new[] { "mocha" }, 50),
            Item("seasonal-blend", "Wild berry pour-over", MenuCategory.Coffee, 780, "Seasonal single-origin pour-over with bright, fruity notes.", new[] { "single-origin coffee" }, new string[0], new[] { OptionGroup.Size }, new[] { "seasonal", "dairy-free" }, new[] { "pour over", "pour-over", "seasonal blend", "filter coffee" }, 90),
            Item("english-breakfast", "English breakfast tea", MenuCategory.Tea, 380, "Strong black tea. Milk on the side if you like.", new[] { "black tea" }, new string[0], Brewed, new[] { "dairy-free" }, new[] { "black tea", "english breakfast", "tea" }, 30),
            Item("matcha", "Matcha latte", MenuCategory.Tea, 590, "Ceremonial matcha whisked with milk.", new[] { "matcha", "milk" }, new[] { "milk" }, new[] { OptionGroup.Temperature, OptionGroup.Size, OptionGroup.Milk, OptionGroup.Sugar, OptionGroup.Ice }, null, new[] { "matcha", "green tea latte" }, 45),
            Item("chamomile", "Chamomile honey tea", MenuCategory.Tea, 400, "Caffeine-free herbal tea with local honey.", new[] { "chamomile", "honey" }, new string[0], new[] { OptionGroup.Size, OptionGroup.Sugar }, new[] { "dairy-free", "caffeine-free" }, new[] { "chamomile", "herbal tea" }, 30),
            Item("berry-smoothie", "Forest berry smoothie", MenuCategory.Smoothies, 650, "Blueberries, raspberries, banana and yoghurt.", new[] { "blueberries", "raspberries", "banana", "yoghurt" }, new[] { "milk" }, Cold, null, new[] { "berry smoothie", "smoothie" }, 60),
            Item("green-smoothie", "Green garden smoothie", MenuCategory.Smoothies, 650, "Spinach, mango, pineapple and coconut water.", new[] { "spinach", "mango", "pineapple", "coconut water" }, new string[0], Cold, new[] { "dairy-free", "vegan" }, new[] { "green smoothie" }, 60),
            Item("orange-juice", "Fresh orange juice", MenuCategory.Juice, 480, "Squeezed to order.", new[] { "oranges" }, new string[0], new[] { OptionGroup.Size, OptionGroup.Ice }, new[] { "dairy-free", "vegan" }, new[] { "orange juice", "oj" }, 40),
            Item("apple-ginger", "Apple & ginger juice", MenuCategory.Juice, 520, "Cold-pressed apple with a kick of ginger.", new[] { "apple", "ginger" }, new string[0], new[] { OptionGroup.Size, OptionGroup.Ice }, new[] { "dairy-free", "vegan" }, new[] { "apple juice", "apple ginger" }, 40),
            Item("lemonade", "Elderflower lemonade", MenuCategory.Mocktails, 450, "Homemade lemonade with elderflower and mint.", new[] { "lemon", "elderflower", "mint", "sparkling water" }, new string[0], new[] { OptionGroup.Size, OptionGroup.Sugar, OptionGroup.Ice }, new[] { "dairy-free", "vegan" }, new[] { "lemonade" }, 35),
            Item("forest-spritz", "Pine & cucumber spritz", MenuCategory.Mocktails, 690, "Alcohol-free spritz with cucumber, lime and pine syrup.", new[] { "cucumber", "lime", "pine syrup", "soda" }, new string[0], new[] { OptionGroup.Sugar, OptionGroup.Ice }, new[] { "dairy-free", "seasonal" }, new[] { "spritz", "mocktail" }, 45),
            Item("avocado-toast", "Avocado sourdough toast", MenuCategory.Breakfast, 1250, "Smashed avocado, feta, chilli flakes and a poached egg.", new[] { "sourdough", "avocado", "feta", "egg", "chilli" }, new[] { "gluten", "milk", "egg" }, new[] { OptionGroup.Spice }, new[] { "vegetarian", "popular" }, new[] { "avocado toast", "avo toast" }, 300),
            Item("pancakes", "Buttermilk pancakes", MenuCategory.Breakfast, 1180, "Three pancakes with berries, maple syrup and cream.", new[] { "flour", "buttermilk", "egg", "berries", "maple syrup", "cream" }, new[] { "gluten", "milk", "egg" }, null, new[] { "vegetarian" }, new[] { "pancakes", "pancake" }, 320),
            Item("forest-bowl", "Mushroom grain bowl", MenuCategory.Lunch, 1490, "Roasted forest mushrooms, quinoa, greens and miso dressing.", new[] { "mushrooms", "quinoa", "greens", "miso", "sesame" }, new[] { "soy", "sesame" }, new[] { OptionGroup.Spice }, new[] { "vegan", "dairy-free" }, new[] { "grain bowl", "mushroom bowl", "bowl" }, 360),
            Item("chicken-sandwich", "Grilled chicken sandwich", MenuCategory.Lunch, 1420, "Chicken, pesto, tomato and rocket on ciabatta.", new[] { "chicken", "ciabatta", "pesto", "tomato", "rocket" }, new[] { "gluten", "milk", "tree nuts" }, new[] { OptionGroup.Spice }, null, new[] { "chicken sandwich", "sandwich" }, 300),
            Item("salmon", "Cedar-roasted salmon", MenuCategory.Dinner, 2650, "Salmon with lemon butter, new potatoes and greens.", new[] { "salmon", "butter", "lemon", "potatoes", "greens" }, new[] { "fish", "milk" }, null, null, new[] { "salmon" }, 480),
            Item("curry", "Pumpkin & lentil curry", MenuCategory.Dinner, 2190, "Slow-cooked curry with jasmine rice and coriander.", new[] { "pumpkin", "lentils", "coconut milk", "rice", "spices" }, new string[0], new[] { OptionGroup.Spice }, new[] { "vegan", "dairy-free", "spicy" }, new[] { "curry", "lentil curry" }, 420),
            Item("cheesecake", "Blueberry cheesecake", MenuCategory.Desserts, 790, "Baked cheesecake with a wild blueberry swirl.", new[] { "cream cheese", "biscuit", "blueberries", "egg" }, new[] { "milk", "gluten", "egg" }, null, new[] { "popular" }, new[] { "cheesecake", "cheese cake" }, 60),
            Item("brownie", "Dark chocolate brownie", MenuCategory.Desserts, 650, "Warm, fudgy and served with vanilla ice cream.", new[] { "dark chocolate", "butter", "flour", "egg", "ice cream" }, new[] { "milk", "gluten", "egg" }, null, null, new[] { "brownie" }, 90),
            Item("croissant", "Butter croissant", MenuCategory.Snacks, 450, "Flaky, all-butter and baked this morning.", new[] { "flour", "butter" }, new[] { "gluten", "milk" }, null, new[] { "vegetarian" }, new[] { "croissant" }, 30),
            Item("banana-bread", "Banana bread", MenuCategory.Snacks, 480, "Toasted, with salted butter on the side.", new[] { "banana", "flour", "egg", "walnuts", "butter" }, new[] { "gluten", "egg", "tree nuts", "milk" }, null, null, new[] { "banana bread" }, 60),
        };

        public static MenuItem Find(string id) => Items.FirstOrDefault(item => item.Id == id);
        public static IEnumerable<MenuItem> In(MenuCategory category) => Items.Where(item => item.Category == category);
        public static string Money(int cents) => "$" + (cents / 100.0).ToString("0.00");
        public static string Category(MenuCategory category) => category.ToString();
    }
}
