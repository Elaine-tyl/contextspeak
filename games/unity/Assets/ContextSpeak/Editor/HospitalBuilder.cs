using ContextSpeak.Life;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// City Hospital outpatients, 45 × 35 m with a double-height atrium and an upper gallery.
    /// A Entrance + reception (south) · B Waiting area + queue board (west) · C Consultation rooms
    /// (north-west) · D Pharmacy (east) · E Nurse station, admin office, lifts (north-east).
    public static class HospitalBuilder
    {
        public const string ScenePath = Scenes + "/Hospital.unity";
        private const float W = 45, D = 35, Storey = 4.2f, Height = 8.4f;

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("City Hospital");
            Daylight("Hospital", "EEF2F5");
            var floor = Hex("E9ECEE"); var wall = Hex("F4F6F7"); var accent = Hex("2E7DA4"); var wood = Hex("B8875A");
            Box("Street plaza", new Vector3(0, -0.1f, -18), new Vector3(90, 0.2f, 80), Hex("BDB8AE"), true);
            Box("Hospital floor", new Vector3(0, 0.025f, 0), new Vector3(W, 0.05f, D), floor, true);
            Box("Accessible ramp edge", new Vector3(0, 0.025f, -D / 2 - 1.2f), new Vector3(6, 0.05f, 2.4f), Hex("D4D7D9"), true);

            // Shell: tall exterior walls (two-storey look), entrance gap on the south wall.
            var sw = new Vector3(-W / 2, 0, -D / 2); var se = new Vector3(W / 2, 0, -D / 2);
            var nw = new Vector3(-W / 2, 0, D / 2); var ne = new Vector3(W / 2, 0, D / 2);
            Wall("Exterior wall south", sw, se, Height, wall, new Vector2(W / 2, 3.4f));
            Wall("Exterior wall north", nw, ne, Height, wall);
            Wall("Exterior wall west", sw, nw, Height, wall);
            Wall("Exterior wall east", se, ne, Height, wall);
            Box("Roof", new Vector3(0, Height + 0.1f, 0), new Vector3(W + 0.4f, 0.2f, D + 0.4f), Hex("D9DDE0"), true);
            Box("Skylight", new Vector3(0, Height - 0.02f, -6), new Vector3(10, 0.05f, 8), new Color(0.85f, 0.95f, 1f, 0.5f), false);
            Box("Façade band", new Vector3(0, Storey, -D / 2 - 0.12f), new Vector3(W, 0.35f, 0.08f), accent, false);
            foreach (var x in new[] { -18f, -12f, -6f, 6f, 12f, 18f })
            {
                Window(new Vector3(x, 0, -D / 2 - 0.12f), 0, 3.6f);
                Window(new Vector3(x, Storey, -D / 2 - 0.12f), 0, 3.6f);
            }
            Board("CITY HOSPITAL  ·  城市医院", new Vector3(0, 3.4f, -D / 2 - 0.2f), 0, new Vector2(9, 0.9f), accent, Color.white);
            Shape("Red cross", PrimitiveType.Cube, new Vector3(-6.5f, 3.4f, -D / 2 - 0.2f), new Vector3(0.25f, 0.8f, 0.05f), Hex("D23A3A"), false);
            Shape("Red cross", PrimitiveType.Cube, new Vector3(-6.5f, 3.4f, -D / 2 - 0.2f), new Vector3(0.8f, 0.25f, 0.05f), Hex("D23A3A"), false);
            var entrance = SlidingDoor("Main entrance doors", new Vector3(0, 0, -D / 2), 0, 3.2f);
            entrance.Range = 3.6f;

            // Upper floor slab over the north rooms, with a glass gallery rail facing the atrium.
            Box("Upper floor slab", new Vector3(0, Storey, 11.75f), new Vector3(W, 0.25f, 11.5f), Hex("E1E4E6"), true);
            Box("Gallery glass rail", new Vector3(0, Storey + 0.6f, 6.05f), new Vector3(W, 1.0f, 0.05f), new Color(0.75f, 0.9f, 0.95f, 0.4f), false);
            Box("Gallery handrail", new Vector3(0, Storey + 1.12f, 6.05f), new Vector3(W, 0.06f, 0.1f), Hex("B9C0C4"), false);
            Label("2F  Wards · Radiology · Cafeteria (closed today)", new Vector3(8, Storey + 1.6f, 6.0f), 0, 0.12f, Hex("40566A"));

            // A — reception
            Box("Reception desk", new Vector3(0, 0.53f, -8), new Vector3(5, 1.05f, 0.8f), wood, true);
            Box("Reception desk top", new Vector3(0, 1.07f, -8.15f), new Vector3(5.2f, 0.04f, 0.45f), Hex("F7F3EC"), false);
            Box("Reception screen", new Vector3(-1, 1.3f, -7.8f), new Vector3(0.55f, 0.35f, 0.03f), Hex("20262B"), false);
            Board("RECEPTION  ·  接待处", new Vector3(0, 2.9f, -7.4f), 0, new Vector2(4.5f, 0.7f), accent, Color.white);
            Board("Please register here →  Take a ticket  →  Wait to be called", new Vector3(0, 1.7f, -12.5f), 0, new Vector2(5.5f, 0.5f), Hex("FFFFFF"), Hex("2E4057"));
            Npc("receptionist", "Grace", "Receptionist", "Secretary", new Vector3(0, 0.05f, -7.1f), 180);
            Exit(new Vector3(0, 1, -15.9f));

            // B — waiting area with queue board
            var waiting = Group("Waiting area", new Vector3(-13, 0, -8));
            for (var row = 0; row < 3; row++)
                for (var index = 0; index < 9; index++)
                {
                    var seat = Chair(new Vector3(-17.4f + index * 0.62f, 0.05f, -12 + row * 1.6f), 0, accent, "waiting");
                    seat.Prompt = "Sit and wait";
                }
            var board = Board("NOW SERVING\nA-000", new Vector3(-13, 3.0f, -3.4f), 0, new Vector2(3.6f, 1.6f), Hex("1D2A33"), Hex("7CF0B4"));
            Plant(new Vector3(-21.5f, 0.05f, -16.5f)); Plant(new Vector3(-21.5f, 0.05f, -2.5f)); Plant(new Vector3(-4.5f, 0.05f, -16.5f));
            Box("Water dispenser", new Vector3(-21.6f, 0.65f, -6), new Vector3(0.4f, 1.3f, 0.4f), Hex("D8E4EA"), true);
            Box("Magazine table", new Vector3(-9.5f, 0.25f, -8.4f), new Vector3(1.2f, 0.5f, 0.6f), wood, true);
            Npc("patient", "Tom", "Patient", "Plaid", new Vector3(-6, 0.05f, -5), 90, new Vector3(-6, 0.05f, -5), new Vector3(4, 0.05f, -13), new Vector3(9, 0.05f, -2));

            // C — consultation rooms (north-west), single storey under the slab
            var partition = Hex("E3EEF3");
            Wall("Consultation wall", new Vector3(-W / 2, 0, 6), new Vector3(-1, 0, 6), Storey, partition, new Vector2(6.5f, 1.1f), new Vector2(16.5f, 1.1f));
            Wall("Room divider", new Vector3(-11, 0, 6), new Vector3(-11, 0, D / 2), Storey, partition);
            Wall("Room wall east", new Vector3(-1, 0, 6), new Vector3(-1, 0, D / 2), Storey, partition);
            var room1 = HingedDoor("Consultation 1 door", new Vector3(-16, 0, 6), 0, 1.0f, Hex("8FB7C9"));
            room1.Locked = true;
            var room2 = HingedDoor("Consultation 2 door", new Vector3(-6, 0, 6), 0, 1.0f, Hex("8FB7C9"));
            room2.Locked = true;
            Board("CONSULTATION 1  ·  In use", new Vector3(-16, 2.85f, 5.85f), 0, new Vector2(2.6f, 0.4f), Hex("FFFFFF"), Hex("2E4057"));
            Board("CONSULTATION 2  ·  Dr Patel", new Vector3(-6, 2.85f, 5.85f), 0, new Vector2(2.6f, 0.4f), Hex("FFFFFF"), Hex("2E4057"));
            var consultation = Group("Consultation room 2", new Vector3(-6, 0, 9));
            Box("Doctor's desk", new Vector3(-6, 0.38f, 12), new Vector3(1.6f, 0.76f, 0.75f), wood, true);
            Box("Monitor", new Vector3(-6.4f, 1.0f, 12.2f), new Vector3(0.5f, 0.32f, 0.03f), Hex("20262B"), false);
            Chair(new Vector3(-6, 0.05f, 10.8f), 0, Hex("6E8FA0"), "consult").Prompt = "Sit with the doctor";
            Box("Examination bed", new Vector3(-9.6f, 0.35f, 14.5f), new Vector3(0.8f, 0.7f, 1.9f), Hex("DCE9EF"), true);
            Box("Privacy curtain", new Vector3(-8.4f, 1.1f, 14.5f), new Vector3(0.03f, 2.0f, 2.4f), Hex("A8C8D8"), false);
            Box("Desk room 1", new Vector3(-16, 0.38f, 12), new Vector3(1.6f, 0.76f, 0.75f), wood, true);
            Npc("doctor", "Dr Patel", "Doctor", "DrMorgan", new Vector3(-6, 0.05f, 13), 180);
            Lamp(new Vector3(-6, Storey - 0.3f, 12), 7); Lamp(new Vector3(-16, Storey - 0.3f, 12), 7);

            // D — pharmacy (east)
            Box("Pharmacy counter", new Vector3(15.5f, 0.53f, -6), new Vector3(7, 1.05f, 0.7f), Hex("F2F2F2"), true);
            Box("Pharmacy counter front", new Vector3(15.5f, 0.53f, -6.37f), new Vector3(7, 1.0f, 0.04f), Hex("3FA37A"), false);
            Box("Medicine shelves", new Vector3(15.5f, 1.1f, -2.8f), new Vector3(7, 2.2f, 0.45f), Hex("E4E1DA"), true);
            var colours = new[] { "E66B5B", "5BA4E6", "F2C14E", "7DCB86", "B07CE6", "F2F2F2" };
            for (var shelf = 0; shelf < 4; shelf++)
                for (var slot = 0; slot < 12; slot++)
                    Box("Medicine box", new Vector3(12.4f + slot * 0.55f, 0.35f + shelf * 0.5f, -3.1f), new Vector3(0.32f, 0.2f, 0.18f), Hex(colours[(slot + shelf) % colours.Length]), false);
            Board("PHARMACY  ·  药房", new Vector3(15.5f, 2.85f, -5.7f), 0, new Vector2(4, 0.6f), Hex("3FA37A"), Color.white);
            Board("Prescriptions collected here · Please have your name ready", new Vector3(15.5f, 1.55f, -6.42f), 0, new Vector2(4.8f, 0.35f), Hex("FFFFFF"), Hex("2E4057"));
            Npc("pharmacist", "Mr Wong", "Pharmacist", "Ali", new Vector3(15.5f, 0.05f, -5.1f), 180);
            Box("Queue line", new Vector3(15.5f, 0.055f, -8.6f), new Vector3(3, 0.01f, 0.08f), Hex("F2C14E"), false);

            // E — nurse station, admin office, lifts
            Box("Nurse station", new Vector3(8, 0.53f, 2.4f), new Vector3(3.4f, 1.05f, 0.8f), Hex("F7F7F7"), true);
            Box("Nurse station side", new Vector3(9.5f, 0.53f, 3.4f), new Vector3(0.8f, 1.05f, 1.8f), Hex("F7F7F7"), true);
            Board("NURSES' STATION", new Vector3(8, 2.6f, 2.0f), 0, new Vector2(3, 0.45f), accent, Color.white);
            Npc("nurse", "Nurse Lily", "Nurse", "NurseLily", new Vector3(8, 0.05f, 3.3f), 180);
            Wall("Admin office wall", new Vector3(10, 0, 6), new Vector3(W / 2, 0, 6), Storey, partition, new Vector2(6, 1.1f));
            Wall("Admin office side", new Vector3(10, 0, 6), new Vector3(10, 0, D / 2), Storey, partition);
            Board("ADMINISTRATION", new Vector3(16, 2.85f, 5.85f), 0, new Vector2(2.6f, 0.4f), Hex("FFFFFF"), Hex("2E4057"));
            Box("Admin desk", new Vector3(16, 0.38f, 12), new Vector3(1.8f, 0.76f, 0.8f), wood, true);
            Npc("admin", "Ms Grant", "Hospital administrator", "Rose", new Vector3(16, 0.05f, 13), 180, new Vector3(16, 0.05f, 13), new Vector3(16, 0.05f, 4.5f));
            Box("Lift doors", new Vector3(4.5f, 1.2f, D / 2 - 0.12f), new Vector3(2.2f, 2.4f, 0.05f), Hex("A9B2B8"), false);
            Board("LIFTS · 2F Wards", new Vector3(4.5f, 2.7f, D / 2 - 0.15f), 0, new Vector2(2, 0.35f), accent, Color.white);
            Box("Corridor floor stripe", new Vector3(4.5f, 0.055f, 4), new Vector3(1.2f, 0.01f, 20), Hex("A6CDE0"), false);
            Label("← Consultation    Pharmacy →", new Vector3(4.5f, 2.4f, -1), 0, 0.1f, Hex("2E4057"));

            for (var x = -15f; x <= 15f; x += 10)
                for (var z = -13f; z <= 1f; z += 7) Lamp(new Vector3(x, Height - 0.6f, z), 14, 1.8f);
            Lamp(new Vector3(4.5f, Storey - 0.3f, 12), 9);

            var (player, camera) = Player(new Vector3(1.5f, 0.1f, -14), 0);
            var flow = new GameObject("Hospital systems").AddComponent<HospitalFlow>();
            flow.QueueBoard = board;
            flow.ConsultationDoor = room2;
            flow.WaitingArea = waiting;
            flow.ConsultationRoom = consultation;
            Finish(player, camera, flow, "City Hospital", "Hospital");
            SkyInstaller.Install();
            LifeBuild.Save(scene, ScenePath);
        }

        private static void Exit(Vector3 position)
        {
            var exit = new GameObject("Exit to Metro City").AddComponent<PortalDoor>();
            exit.transform.position = position;
            exit.Exit = true;
            exit.TargetScene = "MetroCity";
            exit.Prompt = "Leave the hospital (back to Metro City)";
            exit.Range = 1.8f;
        }
    }
}
