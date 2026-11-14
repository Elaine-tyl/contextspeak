using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Harbour Table restaurant: arrive → host (reservation, party size, table preference) → choose a
    /// table → follow the host and sit → waiter takes drinks → menu (categories, allergens, quantity,
    /// customisation, cart) → say the order → allergy check → kitchen → waiter serves → food appears
    /// → eat → "how was everything" → bill (tip, split) → pay → leave. Optional: complaint/refund
    /// with the manager and a takeaway order at the counter.
    public sealed class RestaurantFlow : LocationFlow
    {
        public enum Stage { Arrive, ChooseTable, ToTable, Drinks, Menu, Ordering, Cooking, Serving, Eating, Bill, Paying, Done }
        public Stage Phase { get; private set; } = Stage.Arrive;
        public override string QuestId => "restaurant-dinner";
        public override string QuestTitle => "Have dinner at Harbour Table";
        public MenuCatalog Menu;
        public Transform Kitchen;
        public float CookSeconds = 15f;

        public RestaurantTable Table { get; private set; }
        public Cart Order { get; private set; } = new Cart();
        public int PartySize { get; private set; } = 2;
        private RestaurantTable[] tables = new RestaurantTable[0];
        private readonly List<FoodItem> served = new List<FoodItem>();
        private float readyAt;
        private int discount;
        private bool complained, takeaway;
        private string area = "Window";

        protected override void Start()
        {
            base.Start();
            if (Menu == null) Menu = MenuLibrary.Restaurant();
            tables = FindObjectsByType<RestaurantTable>(FindObjectsSortMode.None).OrderBy(table => table.Id).ToArray();
            foreach (var seat in FindObjectsByType<SeatController>(FindObjectsSortMode.None).Where(seat => seat.Group.StartsWith("table-")))
            {
                var id = seat.Group.Substring(6);
                seat.Allowed = () => Table != null && Table.Id == id && Phase >= Stage.ToTable;
                seat.Seated += () => { if (Phase == Stage.ToTable) WaiterComes(); };
            }
            Step(0, "Talk to the host at the entrance.");
        }

        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            switch (npc.Key)
            {
                case "host" when Phase == Stage.Arrive:
                    Run("Restaurant Host", npc, () => Open(Stage.ChooseTable));
                    return;
                case "host":
                    LifeUI.Toast(npc.DisplayName + ": " + (Phase == Stage.Done ? "Thank you for coming. Have a lovely evening!" : "Enjoy your meal!"));
                    return;
                case "waiter" when Phase == Stage.Eating && served.All(item => item.Done):
                    AskForBill(npc);
                    return;
                case "waiter" when Phase == Stage.Menu:
                    OpenMenu(npc);
                    return;
                case "waiter":
                    LifeUI.Toast(npc.DisplayName + ": " + (Phase == Stage.Cooking ? "Your food is on its way!" : Phase < Stage.ToTable ? "The host will seat you." : "I'll be right with you."));
                    return;
                case "manager" when !complained && Phase >= Stage.Eating:
                    Run("Restaurant Complaint", npc, () =>
                    {
                        complained = true;
                        discount = Mathf.Min(Order.Total, Order.Lines.Count > 0 ? Order.Lines[0].unitCents : 0);
                        LifeUI.Toast("The manager took " + CurrencyManager.Format(discount) + " off your bill.");
                    });
                    return;
                case "manager":
                    LifeUI.Toast(npc.DisplayName + ": Good evening! Let me know if anything isn't right with your meal.");
                    return;
                case "counter" when !takeaway:
                    Run("Restaurant Takeaway", npc, () => PaymentController.Instance.Show("Takeaway", "Mushroom Risotto (to go) × 1", 1700, () =>
                    {
                        takeaway = true;
                        InventoryManager.Add("takeaway-risotto", "Takeaway risotto");
                        LifeUI.Toast("Takeaway bag added to your inventory.");
                    }, () => { }));
                    return;
                default:
                    LifeUI.Toast(npc.DisplayName + ": Hello! Enjoy your evening.");
                    return;
            }
        }

        private void Open(Stage stage)
        {
            Phase = stage;
            ownsPanel = true;
            LifeUI.SetPanelOpen(this, true);
        }

        private void Close(Stage next)
        {
            Phase = next;
            ownsPanel = false;
            LifeUI.SetPanelOpen(this, false);
            InteractionManager.Instance?.Player?.Controls?.Clear();
        }

        /// Tables that fit the party, are free and match the chosen area (the panel lists all areas).
        public IEnumerable<RestaurantTable> Available(int party) => tables.Where(table => !table.Reserved && table.Capacity >= party && !(table.Area == "Terrace" && SkyState.Raining));

        public bool Choose(RestaurantTable table)
        {
            if (table == null || table.Reserved || table.Capacity < PartySize) return false;
            Table = table;
            Close(Stage.ToTable);
            Step(1, "Follow the host to table " + table.Id + " and sit down (E at a chair).");
            var host = Npc("host");
            if (host != null) host.GoTo(table.ServicePoint, () => host.ReturnHome());
            return true;
        }

        private void WaiterComes()
        {
            var waiter = Npc("waiter");
            Phase = Stage.Drinks;
            Step(2, "The waiter is coming to your table.");
            if (waiter == null) { Drinks(null); return; }
            waiter.GoTo(Table.ServicePoint, () => Drinks(waiter));
        }

        private void Drinks(NPCController waiter)
        {
            Run("Restaurant Drinks", waiter, () => OpenMenu(waiter));
        }

        private void OpenMenu(NPCController waiter)
        {
            Phase = Stage.Menu;
            Step(3, "Read the menu and build your order.");
            MenuController.Instance.Show(Menu, cart =>
            {
                Order = cart;
                Phase = Stage.Ordering;
                Step(4, "Tell the waiter what you would like.");
                ScenarioManager.Instance.Begin(OrderScenario(cart), waiter, () =>
                {
                    Phase = Stage.Cooking;
                    readyAt = Time.time + CookSeconds;
                    Step(5, "The kitchen is cooking. Relax at your table…");
                    if (waiter != null && Kitchen != null) waiter.GoTo(Kitchen.position);
                });
            }, () =>
            {
                Step(3, "Talk to the waiter when you're ready to see the menu again.");
                LifeUI.Toast("Menu closed. Talk to the waiter when you're ready.");
            });
        }

        /// The order step must mention what is in the cart (meaning), with a matching model answer.
        public ScenarioDefinition OrderScenario(Cart cart)
        {
            var source = Scenario("Restaurant Order");
            var copy = ScriptableObject.Instantiate(source);
            copy.name = source.name;
            var words = cart.Lines.SelectMany(line => Regex.Matches(line.name.ToLowerInvariant(), "[a-z]{4,}").Cast<Match>().Select(match => match.Value))
                .Where(word => word != "with").Distinct().ToArray();
            var step = copy.Steps[0];
            step.meaning = words.Length > 0 ? string.Join("|", words) : step.meaning;
            step.model = "I'd like " + string.Join(" and ", cart.Lines.Select(line => (line.quantity > 1 ? line.quantity + " " : "the ") + line.name.ToLowerInvariant())) + ", please.";
            step.goal = "Order what you chose: " + cart.Describe() + ".";
            return copy;
        }

        private void Update()
        {
            if (Phase != Stage.Cooking || Time.time < readyAt) return;
            Phase = Stage.Serving;
            var waiter = Npc("waiter");
            if (waiter == null) { Serve(null); return; }
            waiter.GoTo(Table.ServicePoint, () => Serve(waiter));
        }

        private void Serve(NPCController waiter)
        {
            served.Clear();
            var anchor = Table.PlateAnchor != null ? Table.PlateAnchor : Table.transform;
            var index = 0;
            foreach (var line in Order.Lines)
                for (var count = 0; count < line.quantity; count++, index++)
                    served.Add(Dish(Menu.Find(line.id), line, anchor, index));
            Run("Restaurant Delivery", waiter, () =>
            {
                Phase = Stage.Eating;
                Step(6, "Enjoy your meal (press E on each plate or glass).");
                waiter?.ReturnHome();
            });
        }

        private FoodItem Dish(MenuEntry entry, CartLine line, Transform anchor, int index)
        {
            var drink = entry != null && entry.category == "Drinks";
            var offset = new Vector3((index % 3 - 1) * 0.32f, 0, (index / 3) * 0.3f);
            var root = new GameObject(line.name);
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = offset;
            var plate = GameObject.CreatePrimitive(drink ? PrimitiveType.Cylinder : PrimitiveType.Cylinder);
            plate.transform.SetParent(root.transform, false);
            plate.transform.localScale = drink ? new Vector3(0.08f, 0.07f, 0.08f) : new Vector3(0.26f, 0.008f, 0.26f);
            plate.transform.localPosition = new Vector3(0, drink ? 0.07f : 0.01f, 0);
            Paint(plate, drink ? new Color(0.8f, 0.9f, 1f, 1f) : Color.white);
            Object.Destroy(plate.GetComponent<Collider>());
            if (!drink)
            {
                var food = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                food.transform.SetParent(root.transform, false);
                food.transform.localScale = new Vector3(0.17f, 0.06f, 0.13f);
                food.transform.localPosition = new Vector3(0, 0.04f, 0);
                Paint(food, entry != null ? entry.colour : new Color(0.8f, 0.5f, 0.3f));
                Object.Destroy(food.GetComponent<Collider>());
            }
            var item = root.AddComponent<FoodItem>();
            item.DisplayName = line.name;
            item.Drink = drink;
            item.Bites = drink ? 2 : 3;
            item.Finished += _ =>
            {
                if (served.All(dish => dish.Done)) Step(7, "All finished! Talk to the waiter to ask for the bill.");
            };
            return item;
        }

        private static void Paint(GameObject target, Color colour)
        {
            var renderer = target.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.material = new Material(shader) { color = colour };
        }

        private void AskForBill(NPCController waiter)
        {
            Phase = Stage.Bill;
            Run("Restaurant Bill", waiter, () =>
            {
                Phase = Stage.Paying;
                var lines = string.Join("\n", Order.Lines.Select(line => line.quantity + " × " + line.name + (line.choices != "" ? " (" + line.choices + ")" : "") + "   " + CurrencyManager.Format(line.Total)));
                if (discount > 0) lines += "\nManager's discount   −" + CurrencyManager.Format(discount);
                PaymentController.Instance.Show("Harbour Table — Bill, table " + Table.Id, lines, Mathf.Max(0, Order.Total - discount), () =>
                {
                    foreach (var dish in served) if (dish != null) Destroy(dish.gameObject);
                    served.Clear();
                    InventoryManager.Add("restaurant-receipt", "Harbour Table receipt");
                    Phase = Stage.Done;
                    Objective = "Dinner complete! Stand up and leave through the front door.";
                    QuestManager.Complete(QuestId, 60, Objective);
                    LifeUI.Toast("Dinner complete · +60 XP");
                }, () => Phase = Stage.Eating, true);
            });
        }

        public void SetParty(int size) => PartySize = Mathf.Clamp(size, 1, 8);

        private Vector2 scroll;
        private bool ownsPanel;
        private void OnDisable() { LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (ownsPanel) LifeUI.SetPanelOpen(this, true); }

        private void DrawToolkit()
        {
            if (Phase != Stage.ChooseTable) return;
            LifeUI.Begin(out var width, out var height);
            var box = new Rect(width / 2 - 360, height / 2 - 300, 720, 600);
            GUI.Box(box, GUIContent.none, LifeUI.Panel);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 18, box.width - 48, box.height - 36));
            GUILayout.Label("Choose a table", LifeUI.Title);
            GUILayout.Label("Party size (what you told the host)", LifeUI.Body);
            PartySize = GUILayout.Toolbar(PartySize - 1, new[] { "1", "2", "3", "4", "5", "6" }, LifeUI.Tab) + 1;
            GUILayout.Label("Area", LifeUI.Body);
            var areas = new[] { "Window", "Inside", "Terrace" };
            area = areas[Mathf.Max(0, GUILayout.Toolbar(System.Array.IndexOf(areas, area), areas, LifeUI.Tab))];
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(300));
            var options = Available(PartySize).Where(table => table.Area == area).ToArray();
            if (area == "Terrace" && SkyState.Raining) GUILayout.Label("The terrace is closed while it's raining.", LifeUI.Warning);
            else if (options.Length == 0) GUILayout.Label("No free table for " + PartySize + " in this area. Try another area.", LifeUI.Warning);
            foreach (var table in options)
                if (GUILayout.Button("Table " + table.Id + " · " + table.Area + " · seats " + table.Capacity, LifeUI.Button)) Choose(table);
            foreach (var table in tables.Where(table => table.Reserved && table.Area == area))
                GUILayout.Label("Table " + table.Id + " — RESERVED", LifeUI.DarkSmall);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
