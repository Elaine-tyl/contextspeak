using System;
using ContextSpeak.Life;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Editor
{
    /// Native checks: these exercise real Toolkit elements, not substitutes for their controls.
    public static class LegacyToolkitChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Legacy Toolkit regression: " + message);
        }
        public static void Verify()
        {
            VerifyDialogue();
            VerifyCommerceClosure();
            VerifyNestedModals();
            // BaseField.value dispatches native ChangeEvents only when attached to a panel.
            // The runtime always has a UIDocument; give this test the same attachment.
            var host = new GameObject("Legacy Toolkit input check");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = settings;
                var root = document.rootVisualElement;
                Check(root.panel != null, "Native input regression requires an attached runtime panel.");
                var canvas = new ToolkitLayout.Canvas(root);
                var text = "initial";
                var chosen = false;
                var amount = 0.25f;
                Action draw = () =>
                {
                    ToolkitLayout.BeginArea(new Rect(20, 20, 400, 300));
                    ToolkitLayout.Label("Receipt");
                    ToolkitLayout.Button("Pay");
                    text = ToolkitLayout.TextField(text, 60);
                    chosen = ToolkitLayout.Toggle(chosen, "Split bill");
                    amount = ToolkitLayout.HorizontalSlider(amount, 0, 1);
                    ToolkitLayout.EndArea();
                };
                canvas.Render(draw);
                var button = root.Q<Button>();
                var field = root.Q<TextField>();
                var toggle = root.Q<Toggle>();
                var slider = root.Q<Slider>();
                Check(button != null && field != null && toggle != null && slider != null && root.Q<ScrollView>() != null,
                    "Receipt actions, entry, options, range and overflow are native Toolkit controls.");
                canvas.Render(draw);
                Check(ReferenceEquals(button, root.Q<Button>()) && ReferenceEquals(field, root.Q<TextField>()),
                    "Refreshing values retains control identity and focus.");
                field.value = "updated"; toggle.value = true; slider.value = 0.75f;
                canvas.Render(draw);
                Check(text == "updated" && chosen && Mathf.Approximately(amount, 0.75f),
                    "Native input changes reach the original presenter state.");
                canvas.Render(() => { });
                Check(root.Q<ScrollView>().style.display == DisplayStyle.None,
                    "Closing a presenter hides its retained controls.");
                canvas.Render(draw);
                Check(root.Q<ScrollView>().style.display == DisplayStyle.Flex,
                    "Reopening restores the pooled panel.");
                Debug.Log("ContextSpeak legacy Toolkit control checks passed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
        private static void VerifyDialogue()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            try
            {
                scenario.Steps.Add(new ScenarioStep { meaning = "coffee", model = "Can I have a coffee, please?" });
                foreach (var learning in new[] { false, true })
                {
                    var machine = new DialogueStateMachine();
                    machine.Start(scenario, ContextSpeak.Slice.IeltsBand.Band5, learning);
                    machine.Answer("Can I have a coffee, please?", false);
                    Check(machine.Index == 0 && machine.Phase == DialoguePhase.AwaitingAnswer, "Typed rehearsal never consumes a spoken answer.");
                }
                var spoken = new DialogueStateMachine();
                spoken.Start(scenario, ContextSpeak.Slice.IeltsBand.Band5, true);
                spoken.Answer("Can I has a coffee, please?", true);
                Check(spoken.Phase == DialoguePhase.AwaitingRepeat && spoken.Review.Corrected != "", "A spoken error shows feedback and correction.");
                Check(!spoken.Repeat(spoken.Target, false) && spoken.Index == 0, "Typed exact correction stays blocked.");
                Check(!spoken.Repeat("Can I has a coffee, please?", true) && spoken.Index == 0, "The known error cannot pass via word overlap.");
                Check(!spoken.Repeat("Can I have two coffees, please?", true) && spoken.Index == 0, "Changed order quantity stays blocked.");
                Check(!spoken.Repeat("purple elephants", true) && spoken.Index == 0, "Unrelated speech stays blocked.");
                Check(spoken.Repeat(spoken.Target, true) && spoken.Phase == DialoguePhase.Complete, "Correct spoken repeat advances.");
            }
            finally { UnityEngine.Object.DestroyImmediate(scenario); }
        }
        private static void VerifyCommerceClosure()
        {
            var holder = new GameObject("Legacy commerce checks");
            holder.SetActive(false);
            var catalog = ScriptableObject.CreateInstance<MenuCatalog>();
            var previous = LifeUI.PanelOpen;
            try
            {
                var menu = holder.AddComponent<MenuController>();
                var cancelled = 0;
                menu.Show(catalog, _ => { throw new Exception("Closing a menu must not confirm an order."); }, () => cancelled++);
                Check(menu.Open && LifeUI.PanelOpen, "Opening the menu enters its modal.");
                menu.Close(false); menu.Close(false);
                Check(!menu.Open && !LifeUI.PanelOpen && cancelled == 1, "Cancel closes the menu once without submitting or retaining its modal flag.");
                var payment = holder.AddComponent<PaymentController>();
                payment.Show("Receipt", "Coffee", 650, () => { throw new Exception("Cancelling must not pay."); }, () => cancelled++);
                Check(payment.Due == 650 && LifeUI.PanelOpen, "Receipt totals remain authoritative before payment.");
                Check(payment.Cancel() && !payment.Cancel() && !payment.Open && !LifeUI.PanelOpen && cancelled == 2,
                    "Backing out cancels payment once without purchasing or retaining its modal flag.");
            }
            finally
            {
                LifeUI.SetPanelOpen(previous);
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
        private static void VerifyNestedModals()
        {
            var holder = new GameObject("Nested modal checks");
            holder.SetActive(false);
            var world = new GameObject("Nested modal world");
            world.SetActive(false);
            var catalog = ScriptableObject.CreateInstance<MenuCatalog>();
            var previousInteraction = InteractionManager.Instance;
            var instanceProperty = typeof(InteractionManager).GetProperty("Instance");
            var ui = holder.AddComponent<LifeUI>();
            var menu = holder.AddComponent<MenuController>();
            var payment = holder.AddComponent<PaymentController>();
            var airport = holder.AddComponent<DepartureFlow>();
            var interaction = world.AddComponent<InteractionManager>();
            interaction.Player = world.AddComponent<ContextSpeak.Cafe.CafePlayer>();
            instanceProperty.SetValue(null, interaction);
            var settings = typeof(LifeUI).GetField("settingsOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var sync = typeof(LifeUI).GetMethod("SyncModal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Action openSettings = () => { settings.SetValue(ui, true); sync.Invoke(ui, null); };
            var showAirport = typeof(AirportFlowBase).GetMethod("Show", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var airportPanel = typeof(AirportFlowBase).GetProperty("Panel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            try
            {
                menu.Show(catalog, _ => { throw new Exception("Back must not confirm an order."); });
                openSettings();
                LifeUI.RequestBack(-41001);
                LifeUI.RequestBack(-41001); // Other presenters observe the same keyboard frame.
                Check(!(bool)settings.GetValue(ui) && menu.Open && interaction.Modal && interaction.Player.Blocked,
                    "Settings over a menu closes one layer and preserves the menu's world-input lock, regardless of handler order.");
                Check(LifeUI.CanUsePanel(menu) && !LifeUI.CanUsePanel(ui), "Only the remaining top modal accepts UI input.");
                LifeUI.RequestBack(-41002);
                Check(!menu.Open && !interaction.Modal && !interaction.Player.Blocked, "A new Back press closes the underlying menu and releases its own lock.");

                payment.Show("Required payment", "Practice receipt", 650, () => { throw new Exception("Back must not pay."); });
                openSettings();
                LifeUI.RequestBack(-41003); LifeUI.RequestBack(-41003);
                Check(payment.Open && interaction.Modal && interaction.Player.Blocked && !(bool)settings.GetValue(ui),
                    "Closing settings over required payment keeps the required payment and its input lock.");
                LifeUI.RequestBack(-41004);
                Check(payment.Open && !payment.Cancel() && interaction.Player.Blocked, "Required payment cannot be discarded by Back.");
                LifeUI.SetPanelOpen(payment, false);

                showAirport.Invoke(airport, new object[] { "map" });
                openSettings();
                LifeUI.RequestBack(-41005); LifeUI.RequestBack(-41005);
                Check((string)airportPanel.GetValue(airport) == "map" && interaction.Modal && interaction.Player.Blocked,
                    "Settings over an airport panel restores the underlying panel without closing it.");
                LifeUI.RequestBack(-41006);
                Check((string)airportPanel.GetValue(airport) == "" && !interaction.Modal && !interaction.Player.Blocked,
                    "A separate Back press closes the airport panel.");

                interaction.Player.Blocked = true; // An unrelated swimming/vehicle lock.
                openSettings();
                LifeUI.RequestBack(-41007);
                Check(interaction.Player.Blocked && !interaction.Modal, "Phone ownership must restore a pre-existing world-input lock.");
                interaction.Player.Blocked = false;
            }
            finally
            {
                LifeUI.SetPanelOpen(ui, false); LifeUI.SetPanelOpen(menu, false);
                LifeUI.SetPanelOpen(payment, false); LifeUI.SetPanelOpen(airport, false);
                instanceProperty.SetValue(null, previousInteraction);
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(world);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }
}
