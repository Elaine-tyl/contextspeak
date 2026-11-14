using System.Collections.Generic;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// Harbour Table, 35 × 25 m: entrance + host stand (south-west), dining room, window tables
    /// (west), open kitchen with pass (north), beverage station + manager, takeaway counter, and a
    /// covered terrace (east) behind folding glass.
    public static class RestaurantBuilder
    {
        public const string ScenePath = Scenes + "/Restaurant.unity";
        private const float W = 35, D = 25, H = 4.2f;
        private static readonly List<RestaurantTable> tables = new List<RestaurantTable>();

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            tables.Clear();
            Begin("Harbour Table Restaurant");
            Daylight("Restaurant", "F1E7DA");
            var brick = Hex("8E3B2E"); var plaster = Hex("F1E2C8"); var wood = Hex("77502F"); var oak = Hex("B98A5B"); var linen = Hex("FBF7EE");
            Box("Harbour promenade", new Vector3(0, -0.1f, -10), new Vector3(80, 0.2f, 70), Hex("B7AFA2"), true);
            Box("Restaurant floor", new Vector3(-3.75f, 0.025f, 0), new Vector3(27.5f, 0.05f, D), Hex("8A6446"), true);
            Box("Terrace deck", new Vector3(13.75f, 0.025f, 0), new Vector3(7.5f, 0.05f, D), Hex("A9825B"), true);

            // Shell: brick outside, plaster inside feel. Interior spans x −17.5 … 10; terrace 10 … 17.5.
            var sw = new Vector3(-W / 2, 0, -D / 2); var nw = new Vector3(-W / 2, 0, D / 2);
            var s10 = new Vector3(10, 0, -D / 2); var n10 = new Vector3(10, 0, D / 2);
            Wall("Front wall", sw, s10, H, brick, new Vector2(7.5f, 2.4f));
            Wall("Back wall", nw, n10, H, brick);
            Wall("West wall", sw, nw, H, brick);
            Wall("Terrace wall", s10, n10, H, plaster, new Vector2(6.5f, 3.5f), new Vector2(15.5f, 3.5f));
            Box("Ceiling", new Vector3(-3.75f, H + 0.1f, 0), new Vector3(27.5f, 0.2f, D), Hex("E9DCC6"), true);
            Box("Terrace pergola roof", new Vector3(13.75f, 3.1f, 0), new Vector3(7.5f, 0.06f, D), new Color(0.85f, 0.93f, 0.95f, 0.45f), false);
            foreach (var z in new[] { -12f, -6f, 0f, 6f, 12f })
            {
                Box("Pergola post", new Vector3(17.3f, 1.55f, z), new Vector3(0.14f, 3.1f, 0.14f), wood, true);
                Box("Pergola beam", new Vector3(13.75f, 3.05f, z), new Vector3(7.5f, 0.12f, 0.12f), wood, false);
            }
            Box("Terrace rail", new Vector3(17.4f, 0.5f, 0), new Vector3(0.08f, 1.0f, D), wood, true);
            Box("Terrace rail south", new Vector3(13.75f, 0.5f, -12.45f), new Vector3(7.5f, 1.0f, 0.08f), wood, true);
            Box("Terrace rail north", new Vector3(13.75f, 0.5f, 12.45f), new Vector3(7.5f, 1.0f, 0.08f), wood, true);
            Box("Harbour water", new Vector3(30, -0.4f, 0), new Vector3(20, 0.1f, 60), new Color(0.25f, 0.45f, 0.55f, 1f), false);
            foreach (var z in new[] { -8f, -2f, 4f, 10f }) Window(new Vector3(-W / 2 + 0.12f, 0, z), 90, 3.2f, 1.9f, 0.7f);
            foreach (var x in new[] { -14f, -2f, 4f }) Window(new Vector3(x, 0, -D / 2 - 0.12f), 0, 2.6f, 1.8f, 0.8f);
            Board("HARBOUR TABLE  ·  海港餐厅", new Vector3(-10, 3.3f, -D / 2 - 0.2f), 0, new Vector2(7, 0.8f), Hex("1F2A2E"), Hex("FFCE92"));
            Box("Awning", new Vector3(-10, 2.75f, -D / 2 - 0.7f), new Vector3(4, 0.08f, 1.3f), Hex("2F5D50"), false);
            var entrance = SlidingDoor("Entrance doors", new Vector3(-10, 0, -D / 2), 0, 2.4f);
            entrance.Range = 3f;
            Exit(new Vector3(-10, 1, -12.1f));

            // Host stand
            Box("Host stand", new Vector3(-10, 0.6f, -9.2f), new Vector3(1.2f, 1.2f, 0.6f), wood, true);
            Label("Please wait to be seated", new Vector3(-10, 1.35f, -9.55f), 0, 0.06f, Hex("FFCE92"));
            Npc("host", "Sofia", "Host", "PeachGala", new Vector3(-10, 0.05f, -8.5f), 180);

            // Window tables (west), dining room (centre), terrace (east). One reserved.
            AddTable("W1", "Window", 2, new Vector3(-15.2f, 0, -7), linen);
            AddTable("W2", "Window", 4, new Vector3(-15.2f, 0, -1.5f), linen);
            AddTable("W3", "Window", 2, new Vector3(-15.2f, 0, 4), linen).Reserved = true;
            AddTable("W4", "Window", 4, new Vector3(-15.2f, 0, 9.2f), linen);
            AddTable("I1", "Inside", 2, new Vector3(-8, 0, -3.5f), linen);
            AddTable("I2", "Inside", 4, new Vector3(-8, 0, 2.5f), linen);
            AddTable("I3", "Inside", 2, new Vector3(-2, 0, -3.5f), linen);
            AddTable("I4", "Inside", 6, new Vector3(-2, 0, 2.5f), linen);
            AddTable("I5", "Inside", 4, new Vector3(4, 0, -1), linen);
            AddTable("T1", "Terrace", 2, new Vector3(13.5f, 0, -8), oak);
            AddTable("T2", "Terrace", 4, new Vector3(13.5f, 0, -2), oak);
            AddTable("T3", "Terrace", 2, new Vector3(13.5f, 0, 4), oak);
            Label("RESERVED", new Vector3(-15.2f, 0.9f, 4), 90, 0.05f, Hex("C0392B"));

            // Open kitchen + pass (north)
            Box("Kitchen pass", new Vector3(4, 0.55f, 7.2f), new Vector3(10, 1.1f, 0.7f), Hex("D6D6D2"), true);
            Box("Pass heat lamp", new Vector3(4, 1.9f, 7.2f), new Vector3(9, 0.1f, 0.4f), Hex("2B2B2B"), false);
            Box("Stove line", new Vector3(4, 0.45f, 11.6f), new Vector3(10, 0.9f, 0.9f), Hex("8D9297"), true);
            Box("Extractor hood", new Vector3(4, 2.6f, 11.6f), new Vector3(10, 0.5f, 1.1f), Hex("B9BEC2"), false);
            foreach (var x in new[] { 0.5f, 2.5f, 4.5f, 6.5f }) Shape("Pot", PrimitiveType.Cylinder, new Vector3(x, 1.0f, 11.6f), new Vector3(0.35f, 0.12f, 0.35f), Hex("4A4F54"), false);
            Board("OPEN KITCHEN", new Vector3(4, 3.2f, 7.0f), 0, new Vector2(3.2f, 0.45f), Hex("1F2A2E"), Hex("FFCE92"));
            var kitchen = Group("Kitchen pickup", new Vector3(4, 0, 6.3f));
            Npc("chef", "Chef Marco", "Head chef", "Executive", new Vector3(3, 0.05f, 9.6f), 180, new Vector3(3, 0.05f, 9.6f), new Vector3(7, 0.05f, 9.6f));

            // Beverage station + manager (north-west), takeaway counter (south)
            Box("Beverage counter", new Vector3(-10, 0.55f, 10.8f), new Vector3(6, 1.1f, 0.7f), wood, true);
            Box("Bottle shelf", new Vector3(-10, 1.6f, 12.2f), new Vector3(6, 1.6f, 0.3f), Hex("4F3B33"), false);
            for (var i = 0; i < 14; i++) Shape("Bottle", PrimitiveType.Cylinder, new Vector3(-12.6f + i * 0.4f, 1.15f + (i % 2) * 0.55f, 12.1f), new Vector3(0.08f, 0.14f, 0.08f), Hex(i % 3 == 0 ? "3E7C4F" : i % 3 == 1 ? "B5651D" : "D9E6EA"), false);
            Board("BEVERAGES", new Vector3(-10, 2.75f, 12.0f), 0, new Vector2(2.6f, 0.4f), Hex("1F2A2E"), Hex("FFCE92"));
            Npc("manager", "Mr Davies", "Restaurant manager", "OfficerJames", new Vector3(-10, 0.05f, 9.6f), 180);
            Box("Takeaway counter", new Vector3(-3, 0.55f, -10.6f), new Vector3(3, 1.1f, 0.7f), oak, true);
            Board("TAKEAWAY", new Vector3(-3, 2.4f, -10.4f), 0, new Vector2(2.2f, 0.4f), Hex("2F5D50"), Color.white);
            Npc("counter", "Leo", "Takeaway counter", "SmilingBoy", new Vector3(-3, 0.05f, -9.8f), 180);
            Npc("waiter", "Daniel", "Waiter", "Elliot", new Vector3(1, 0.05f, 6.0f), 180);
            Npc("guest", "Mrs Okafor", "Diner", "Dawn", new Vector3(-2.8f, 0.05f, 4.0f), 180);

            Plant(new Vector3(-16.6f, 0.05f, -11.6f)); Plant(new Vector3(9.2f, 0.05f, -11.6f)); Plant(new Vector3(-16.6f, 0.05f, 11.6f));
            for (var x = -14f; x <= 7f; x += 7)
                for (var z = -8f; z <= 8f; z += 8) Lamp(new Vector3(x, H - 0.35f, z), 10, 1.4f, "FFE0B8");
            Box("Bar floor stripe", new Vector3(-3.75f, 0.055f, 6.5f), new Vector3(27, 0.01f, 0.1f), Hex("C9A27A"), false);

            var (player, camera) = Player(new Vector3(-8.8f, 0.1f, -10.6f), 0);
            var flow = new GameObject("Restaurant systems").AddComponent<RestaurantFlow>();
            flow.Menu = AssetDatabase.LoadAssetAtPath<MenuCatalog>(Data + "/Menus/Harbour Table Menu.asset");
            flow.Kitchen = kitchen;
            Finish(player, camera, flow, "Harbour Table Restaurant", "Restaurant");
            SkyInstaller.Install();
            LifeBuild.Save(scene, ScenePath);
        }

        private static RestaurantTable AddTable(string id, string area, int capacity, Vector3 floor, Color cloth)
        {
            var top = capacity <= 2 ? new Vector2(0.8f, 0.8f) : capacity <= 4 ? new Vector2(1.4f, 0.9f) : new Vector2(2.0f, 0.9f);
            var root = Table(floor, top, cloth, null, "Table " + id);
            var table = root.gameObject.AddComponent<RestaurantTable>();
            table.Id = id;
            table.Area = area;
            table.Capacity = capacity;
            var anchor = Group("Plates", new Vector3(0, 0.765f, 0), 0, root);
            table.PlateAnchor = anchor;
            Shape("Candle", PrimitiveType.Cylinder, new Vector3(0, 0.82f, 0.25f), new Vector3(0.05f, 0.05f, 0.05f), Hex("FFF3D6"), false, root);
            var group = "table-" + id;
            var perSide = capacity <= 2 ? 1 : capacity <= 4 ? 2 : 3;
            for (var side = -1; side <= 1; side += 2)
                for (var index = 0; index < perSide; index++)
                {
                    var x = perSide == 1 ? 0 : (index - (perSide - 1) / 2f) * (top.x / perSide);
                    Chair(new Vector3(x, 0.05f, side * (top.y / 2 + 0.38f)), side < 0 ? 0 : 180, Hex("5B3A26"), group, root).Prompt = "Sit at table " + id;
                }
            Label(id, new Vector3(top.x / 2 - 0.05f, 0.80f, -top.y / 2 + 0.05f), 0, 0.02f, Hex("6D4C32"), root).transform.localRotation = Quaternion.Euler(90, 0, 0);
            tables.Add(table);
            return table;
        }

        private static void Exit(Vector3 position)
        {
            var exit = new GameObject("Exit to Metro City").AddComponent<PortalDoor>();
            exit.transform.position = position;
            exit.Exit = true;
            exit.TargetScene = "MetroCity";
            exit.Prompt = "Leave the restaurant (back to Metro City)";
            exit.Range = 1.6f;
        }
    }
}
