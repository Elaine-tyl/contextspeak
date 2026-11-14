using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Automated checks for the life-simulation locations (ContextSpeak > Checks > Life Locations).
    /// Logic checks run without touching the player's save; scene checks open the built scenes.
    public static class LifeLocationChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Life Locations")]
        public static void Menu()
        {
            var errors = Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak life-location checks passed.");
            else Debug.LogError("ContextSpeak life-location checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static string[] Run()
        {
            var errors = new List<string>();
            void Check(bool value, string message) { if (!value) errors.Add(message); }
            void Guard(string name, Action body)
            {
                try { body(); }
                catch (Exception error) { errors.Add(name + " threw " + error.GetType().Name + ": " + error.Message); }
            }
            var persist = SaveManager.Persist;
            SaveManager.Persist = false;
            try
            {
                Guard("Dialogue", () => Dialogue(Check));
                Guard("Commerce", () => Commerce(Check));
                Guard("Save", () => SaveLoad(Check));
                Guard("Mock mode", () => Mock(Check));
            }
            finally
            {
                SaveManager.UseInMemory(null);
                SaveManager.Persist = persist;
            }
            Guard("Hospital scene", () => Hospital(Check));
            Guard("Restaurant scene", () => Restaurant(Check));
            return errors.ToArray();
        }

        private static void Dialogue(Action<bool, string> check)
        {
            foreach (var name in ScenarioLibrary.Names)
            {
                var scenario = ScenarioLibrary.Create(name);
                check(scenario != null && scenario.Steps.Count > 0, "Scenario has steps: " + name);
                foreach (var step in scenario.Steps)
                {
                    check(step.simple != "" && step.standard != "" && step.advanced != "", name + "/" + step.id + " has three band variants.");
                    check(DialogueStateMachine.Conveys(step, step.model), name + "/" + step.id + " model answer satisfies its own meaning.");
                }
            }
            var registration = ScenarioLibrary.Create("Hospital Registration");
            var low = new DialogueStateMachine(); low.Start(registration, Slice.IeltsBand.Band3, true);
            var high = new DialogueStateMachine(); high.Start(registration, Slice.IeltsBand.Band8, true);
            check(low.NpcLine != high.NpcLine && low.NpcLine.Length < high.NpcLine.Length, "Band 3 and Band 8 NPC lines differ (simpler at Band 3).");

            var machine = new DialogueStateMachine();
            machine.Start(registration, Slice.IeltsBand.Band5, true);
            machine.Answer("banana", true);
            check(machine.Index == 0 && machine.Phase == DialoguePhase.AwaitingAnswer, "An answer that does not do the task does not advance.");
            machine.Answer("purple elephant", true);
            machine.Answer("blue sky", true);
            check(machine.Phase == DialoguePhase.AwaitingRepeat && machine.Target == registration.Steps[0].model, "Third miss in learning mode shows the model answer to repeat.");
            check(!machine.Repeat("I like pizza tonight") && machine.Index == 0, "Repeat gate: a wrong repetition does NOT advance the dialogue.");
            check(machine.Repeat(machine.Target) && machine.Index == 1, "Repeat gate: repeating the target advances.");
            check(!machine.Skip(), "Skip is not available in learning mode.");
            var practice = new DialogueStateMachine();
            practice.Start(registration, Slice.IeltsBand.Band5, false);
            check(practice.Skip() && practice.Index == 1, "Skip works in free-practice mode.");
            var completed = 0;
            var full = new DialogueStateMachine();
            full.StepCompleted += _ => completed++;
            full.Start(ScenarioLibrary.Create("Pharmacy Pickup"), Slice.IeltsBand.Band6, false);
            foreach (var step in ScenarioLibrary.Create("Pharmacy Pickup").Steps)
            {
                full.Answer(step.model, true);
                if (full.Phase == DialoguePhase.AwaitingRepeat) full.Repeat(full.Target);
            }
            check(full.Phase == DialoguePhase.Complete && completed == 4, "Model answers complete the pharmacy scenario (" + completed + "/4).");
        }

        private static void Commerce(Action<bool, string> check)
        {
            var menu = MenuLibrary.Cafe();
            var latte = menu.Find("latte");
            check(latte != null && latte.OptionGroups.Count() == 3, "Latte has size, milk and sugar options.");
            var cart = new Cart();
            var line = cart.Add(latte, new[] { "Regular+50", "Oat+60", "No sugar" });
            check(line.unitCents == 420 + 50 + 60, "Customisation prices add to the unit price.");
            cart.Add(latte, new[] { "Regular+50", "Oat+60", "No sugar" });
            check(cart.Lines.Count == 1 && line.quantity == 2 && cart.Total == 2 * 530, "Same item + choices increases quantity.");
            cart.Add(latte, new[] { "Small", "Whole", "No sugar" });
            check(cart.Lines.Count == 2, "Different customisation is a separate cart line.");
            cart.Change(line, -2);
            check(cart.Lines.Count == 1 && cart.Count == 1, "Quantity to zero removes the line.");
            check(MenuLibrary.Restaurant().Items.Any(item => item.allergens.Contains("nuts")), "Restaurant menu lists allergens.");

            SaveManager.UseInMemory(new LifeProgress { cents = 1000 });
            check(!CurrencyManager.TrySpend(1500, out _) && CurrencyManager.Cents == 1000, "Cannot pay more than the wallet holds.");
            check(CurrencyManager.TrySpend(700, out _) && CurrencyManager.Cents == 300, "Payment deducts the wallet.");
            InventoryManager.Add("receipt", "Receipt", 2);
            check(InventoryManager.Count("receipt") == 2 && InventoryManager.Remove("receipt") && InventoryManager.Count("receipt") == 1, "Inventory add/remove.");
        }

        private static void SaveLoad(Action<bool, string> check)
        {
            var progress = new LifeProgress { cents = 4321, xp = 77, returnScene = "MetroCity", returnEntrance = "metro-hospital", returnX = 27.5f };
            progress.inventory.Add(new InventoryEntry { id = "ticket", name = "Ticket", count = 1 });
            progress.quests.Add(new QuestEntry { id = "hospital-visit", title = "Hospital", step = 3 });
            var copy = JsonUtility.FromJson<LifeProgress>(JsonUtility.ToJson(progress));
            check(copy.cents == 4321 && copy.xp == 77 && copy.inventory.Count == 1 && copy.quests[0].step == 3, "Life progress survives save/load.");
            SaveManager.UseInMemory(copy);
            check(SceneTransitionManager.TakeReturnPoint("MetroCity", out var position, out _) && Mathf.Approximately(position.x, 27.5f), "Return point brings you back to the door you used.");
            check(!SceneTransitionManager.TakeReturnPoint("MetroCity", out _, out _), "Return point is used only once.");
            QuestManager.Restart("q", "Quest");
            QuestManager.Advance("q", 2, "note");
            QuestManager.Complete("q", 10, "done");
            QuestManager.Complete("q", 10, "done");
            check(SaveManager.Data.xp == 87 && QuestManager.Get("q", "Quest").done, "Quest completion awards XP once.");
        }

        private static void Mock(Action<bool, string> check)
        {
            SaveManager.UseInMemory(new LifeProgress { forceMock = true });
            var host = new GameObject("Mock check");
            try
            {
                var speech = host.AddComponent<SpeechEvaluationManager>();
                check(speech.Mock, "Forced MOCK mode is reported as mock.");
                string heard = null; var spoken = true;
                speech.Heard += (text, wasSpoken) => { heard = text; spoken = wasSpoken; };
                speech.SubmitTyped("  I'd like to see a doctor  ");
                check(heard == "I'd like to see a doctor" && !spoken, "Typed MOCK answers are trimmed and marked as not spoken.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Hospital(Action<bool, string> check)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HospitalBuilder.ScenePath) == null) { check(false, "Hospital.unity has not been built."); return; }
            EditorSceneManager.OpenScene(HospitalBuilder.ScenePath, OpenSceneMode.Single);
            var flow = UnityEngine.Object.FindFirstObjectByType<HospitalFlow>();
            check(flow != null && flow.QueueBoard != null && flow.ConsultationDoor != null && flow.ConsultationDoor.Locked, "Hospital flow wired; consultation door starts locked.");
            var keys = UnityEngine.Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None).Select(npc => npc.Key).ToArray();
            check(keys.Length >= 6 && new[] { "receptionist", "nurse", "doctor", "pharmacist", "patient", "admin" }.All(keys.Contains), "Hospital has its six NPCs.");
            check(UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None).Length >= 3, "Hospital has entrance and room doors.");
            check(UnityEngine.Object.FindObjectsByType<SeatController>(FindObjectsSortMode.None).Count(seat => seat.Group == "waiting") >= 20, "Waiting area has seats.");
            check(UnityEngine.Object.FindObjectsByType<PortalDoor>(FindObjectsSortMode.None).Any(portal => portal.Exit), "Hospital has an exit to Metro City.");
            var interaction = UnityEngine.Object.FindFirstObjectByType<InteractionManager>();
            check(interaction != null && interaction.Player != null && UnityEngine.Object.FindFirstObjectByType<LifeUI>() != null && UnityEngine.Object.FindFirstObjectByType<PaymentController>() != null, "Hospital has the shared systems.");
            check(flow != null && flow.Scenarios.Length >= 5, "Hospital references the scenario assets.");
            check(UnityEngine.Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>() != null, "Hospital has a baked NavMesh.");
        }

        private static void Restaurant(Action<bool, string> check)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RestaurantBuilder.ScenePath) == null) { check(false, "Restaurant.unity has not been built."); return; }
            EditorSceneManager.OpenScene(RestaurantBuilder.ScenePath, OpenSceneMode.Single);
            var flow = UnityEngine.Object.FindFirstObjectByType<RestaurantFlow>();
            check(flow != null && flow.Menu != null && flow.Kitchen != null, "Restaurant flow has a menu and a kitchen.");
            var tables = UnityEngine.Object.FindObjectsByType<RestaurantTable>(FindObjectsSortMode.None);
            var seats = UnityEngine.Object.FindObjectsByType<SeatController>(FindObjectsSortMode.None);
            check(tables.Length >= 10 && tables.Any(table => table.Reserved) && new[] { "Window", "Inside", "Terrace" }.All(area => tables.Any(table => table.Area == area)), "Restaurant has window, inside, terrace and reserved tables.");
            check(tables.All(table => seats.Count(seat => seat.Group == "table-" + table.Id) == table.Capacity && table.PlateAnchor != null), "Every table has one chair per place and a plate anchor.");
            var keys = UnityEngine.Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None).Select(npc => npc.Key).ToArray();
            check(new[] { "host", "waiter", "manager", "counter", "chef" }.All(keys.Contains), "Restaurant has host, waiter, manager, counter staff and chef.");
            check(UnityEngine.Object.FindObjectsByType<PortalDoor>(FindObjectsSortMode.None).Any(portal => portal.Exit), "Restaurant has an exit to Metro City.");
            var order = new Cart();
            order.Add(MenuLibrary.Restaurant().Find("fish-chips"), new[] { "Mushy peas" });
            var scenario = flow != null ? flow.OrderScenario(order) : null;
            check(scenario != null && DialogueStateMachine.Conveys(scenario.Steps[0], "Can I have the fish and chips please") && !DialogueStateMachine.Conveys(scenario.Steps[0], "I'd like the steak"),
                "Saying the order must mention what is in the cart.");
        }
    }
}
