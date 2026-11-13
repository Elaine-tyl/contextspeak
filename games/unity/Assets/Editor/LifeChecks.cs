using System;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Editor
{
    public static class LifeChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Life regression: " + message);
        }
        private static LifeReview Good(string choice = "")
        {
            return new LifeReview { accepted = true, correction = "", feedback = "Clear request.", acknowledgement = "Sure.", choice = choice };
        }
        public static void Verify()
        {
            var mission = new LifeMission();
            Check(mission.Balance == 2000 && mission.Step == 0, "A new day starts with twenty dollars.");
            Check(!mission.Apply(0, "Sure, I'll help.", Good(), false) && mission.Step == 0, "Typing rehearses but earns no spoken task.");
            var repair = Good();
            repair.correction = "Sure, I'll help.";
            Check(!mission.Apply(0, "I helps.", repair, true), "A grammar repair requires a new answer.");
            Check(mission.Step == 0 && mission.Repair == "Sure, I'll help.", "The question remains until the repair is spoken.");
            Check(!mission.Apply(0, "I helps.", Good(), true), "Repeating the same known error cannot advance even if the model accepts it.");
            var saved = mission.Save();
            var recovered = new LifeMission();
            Check(recovered.Restore(saved) && recovered.Repair == mission.Repair, "An unfinished correction survives a reload.");
            Check(mission.Apply(0, "Of course, I can pick those up.", Good(), true), "A fresh equivalent spoken answer can resolve a repair.");
            Check(!mission.Apply(0, "Sure.", Good(), true) && mission.Step == 1, "An old assessment cannot advance a second step.");
            Check(mission.Apply(1, "Three apples, please.", Good(), true), "The first order reserves apples.");
            Check(mission.Balance == 2000 && mission.Cart.Contains("3 apples"), "Adding groceries to a cart does not pay yet.");
            Check(mission.Apply(2, "Two carrots, please.", Good(), true), "The second order reserves carrots.");
            Check(mission.Apply(3, "Card, please.", Good(), true), "Payment completes the purchase.");
            Check(mission.Balance == 1300 && mission.Bag.Contains("3 apples") && mission.Bag.Contains("2 carrots"), "Exactly seven dollars buys the actual groceries.");
            Check(!mission.Apply(3, "Card, please.", Good(), true) && mission.Balance == 1300, "Replayed payment never charges twice.");
            Check(mission.Apply(4, "Here are the apples and carrots.", Good(), true), "Groceries can be handed to the family.");
            Check(!mission.Bag.Contains("apples") && mission.DinnerReady, "Handoff removes groceries and prepares dinner once.");
            Check(!mission.Apply(5, "One latte.", Good("gold"), true), "A model cannot invent a purchasable item.");
            Check(mission.Apply(5, "One latte, please.", Good("latte"), true), "A real menu choice is reserved.");
            Check(mission.Apply(6, "Not iced. Hot, please.", Good(), true) && mission.Temperature == "hot", "Only the positive temperature choice is recorded.");
            Check(mission.Apply(7, "Not for here. To go, please.", Good(), true) && mission.Service == "to go", "Only the positive service choice is recorded.");
            Check(mission.Balance == 1300, "The cafe does not charge before confirmation.");
            Check(mission.Apply(8, "Yes, go ahead.", Good(), true), "Explicit reviewed confirmation commits the drink.");
            Check(mission.Balance == 650 && mission.Bag.Contains("latte") && mission.Receipt.Contains("PAID") && mission.Receipt.Contains("$6.50"), "The selected drink has the correct price, inventory entry and paid receipt.");
            for (var index = 9; index < 12; index++)
                Check(mission.Apply(index, "An on-topic reviewed reply.", Good(), true), "Each friend goal advances once.");
            Check(mission.Complete && !mission.Apply(12, "Again", Good(), true), "Completing the day cannot award extra progress.");
            Check(recovered.Restore(mission.Save()) && recovered.Complete && recovered.Balance == 650, "Completed inventory and wallet restore consistently.");
            foreach (var corrupt in new[] {
                "garbage", "{}", "{\"version\":9,\"step\":0}",
                "{\"version\":1,\"step\":-1}", "{\"version\":1,\"step\":13}",
                "{\"version\":1,\"step\":9,\"drink\":\"gold\"}",
                "{\"version\":1,\"step\":9,\"drink\":\"coffee\",\"temperature\":\"\",\"service\":\"here\"}",
            }) Check(!new LifeMission().Restore(corrupt), "Impossible or incomplete saves must be rejected.");
            var practice = new LifeMission();
            practice.StartPractice("friend");
            Check(practice.Practice && practice.Step == 9 && practice.Balance == 900, "A stage jump is clearly separate practice.");
            Check(!new LifeMission().Restore(practice.Save()), "Practice saves cannot be loaded as earned progress.");
            var noop = new LifeMission();
            noop.Apply(0, "Sure, I'll help.", new LifeReview { accepted = false, correction = "Sure, I'll help.", feedback = "Please try again.", acknowledgement = "Try again.", choice = "" }, true);
            Check(noop.Apply(0, "Sure, I'll help.", Good(), true), "A new recording can resolve an identical fallback correction.");
            var ambiguous = new LifeMission();
            ambiguous.StartPractice("cafe");
            Check(!ambiguous.Apply(5, "A latte, not a coffee.", Good("coffee"), true), "A rejected drink cannot be purchased.");
            Check(ambiguous.Apply(5, "A latte, not a coffee.", Good("latte"), true), "An unambiguous positive alternative is preserved.");
            Check(!ambiguous.Apply(6, "Hot or iced.", Good(), true), "Alternatives need clarification instead of a silent default.");
            Check(ambiguous.Apply(6, "Hot.", Good(), true) && ambiguous.Apply(7, "For here.", Good(), true), "Clarified options can proceed.");
            Check(!ambiguous.Apply(8, "Yes, cancel the order.", Good(), true), "A cancellation cannot charge for the drink.");
            Check(ambiguous.Apply(8, "Yes, go ahead?", Good(), true), "Transcription punctuation alone is not a spoken error.");
            Check(!new LifeMission().Restore("{\"version\":1,\"step\":0,\"repair\":\" \",\"previous\":\" \"}"), "Whitespace cannot impersonate a pending repair.");
            var catalog = LifeCatalog.Load();
            var drinks = LifeMenu.Items(catalog, "drink", "latte", 1300);
            Check(drinks.Length == 4 && Array.Exists(drinks, item => item.id == "latte" && item.planned && item.price == "$6.50"), "The cafe menu shows catalog prices and the picked drink.");
            Check(Array.TrueForAll(drinks, item => item.note != "" && item.phrase != ""), "Every drink has a description and a phrase to say.");
            Check(!Array.Exists(LifeMenu.Items(catalog, "drink", "", 300), item => item.id == "latte" && item.affordable), "Drinks above the wallet are marked over budget.");
            Check(LifeMenu.Plan(catalog, "latte", 1300).Contains("$6.50 left"), "A plan shows the wallet after paying.");
            Check(LifeMenu.Plan(catalog, "apple", 1300) == "", "Only drinks can be picked from the cafe menu.");
            var planned = new LifeMission();
            planned.StartPractice("cafe");
            var before = planned.Balance;
            LifeMenu.Plan(catalog, "latte", planned.Balance);
            Check(planned.Balance == before && planned.Drink == "", "Picking from the menu never buys; the spoken order does.");
            Debug.Log("ContextSpeak Life mission checks passed");
        }
        public static void VerifyScene()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<LifeGame>();
            Check(game != null && game.Player != null && game.Npcs.Length == 4, "The town has a player and four reachable NPCs.");
            Physics.SyncTransforms();
            foreach (var npc in LifeCatalog.Load().npcs)
            {
                Check(Vector3.Distance(npc.Stand, npc.Position) < 3.1f, "Authored interaction points are in speaking range.");
                var overlap = Physics.OverlapCapsule(new Vector3(npc.standX, 0.45f, npc.standZ), new Vector3(npc.standX, 1.3f, npc.standZ), 0.4f);
                Check(!Array.Exists(overlap, collider => !(collider is CharacterController)), "Interaction point needs room for a player: " + npc.id + " / " + string.Join(", ", Array.ConvertAll(overlap, collider => collider.name + " at " + collider.transform.position)));
                var previous = new Vector3(0, 0.9f, 12);
                foreach (var point in LifeRoutes.To(previous, npc))
                {
                    var next = new Vector3(point.x, 0.9f, point.z);
                    var blockers = Physics.RaycastAll(previous, (next - previous).normalized, Vector3.Distance(previous, next));
                    Check(!Array.Exists(blockers, hit => !(hit.collider is CharacterController)), "The guided route must not cross furniture or walls: " + npc.id);
                    previous = next;
                }
            }
            foreach (var start in new[] { new Vector3(-12, 0, 1), new Vector3(-18, 0, -14), new Vector3(18, 0, -2) })
            {
                var route = new System.Collections.Generic.List<Vector3>(LifeRoutes.To(start, LifeCatalog.Load().npcs[0]));
                Check(route.Count > 0 && Vector3.Distance(route[route.Count - 1], LifeCatalog.Load().npcs[0].Stand) < 0.1f, "Exploration positions still have a route to the next NPC.");
                var previous = start;
                foreach (var next in route)
                {
                    var direction = next - previous;
                    var hits = Physics.CapsuleCastAll(previous + Vector3.up * 0.45f, previous + Vector3.up * 1.3f, 0.4f, direction.normalized, direction.magnitude);
                    Check(!Array.Exists(hits, hit => !(hit.collider is CharacterController)), "Routes need character-width clearance: " + previous + " -> " + next + " / " + string.Join(", ", Array.ConvertAll(hits, hit => hit.collider.name + " at " + hit.point)));
                    previous = next;
                }
            }
            Check(Physics.Linecast(new Vector3(21, 1, 0), new Vector3(24, 1, 0)), "The town has a physical outer boundary.");
            Debug.Log("ContextSpeak Life scene checks passed in " + SceneManager.GetActiveScene().name);
        }
    }
}
