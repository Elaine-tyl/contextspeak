using System.Collections.Generic;
using ContextSpeak.Life;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;
using static ContextSpeak.Editor.AirportKit;

namespace ContextSpeak.Editor
{
    /// AircraftCabin.unity: a narrow-body cabin (nose +Z): business rows 1–3 (2+2), economy rows
    /// 10–30 (3+3), overhead lockers, windows onto the moving ground/cloud deck, galley, lavatory,
    /// seatbelt signs, crew and captain. Real proportions: 3.7 m cabin, 0.79 m seat pitch.
    public static class AirportCabinBuilder
    {
        public const string ScenePath = Scenes + "/AircraftCabin.unity";
        private const float HalfWidth = 1.85f, Ceiling = 2.15f;

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("SkyLark Air SL cabin");
            Daylight("AircraftCabin", "DDE6EE");
            var flow = new GameObject("Cabin systems").AddComponent<CabinFlow>();
            var trim = Hex("E9E7E2"); var fabric = Hex("23476F"); var business = Hex("6E2B3A");
            Floor("Cabin floor", new Vector3(0, 0, 0), new Vector2(HalfWidth * 2, 36), Hex("4C5562"), 0.2f);
            Box("Aisle carpet", new Vector3(0, 0.01f, -2), new Vector3(0.55f, 0.01f, 30), Hex("3A4552"), false);
            Box("Ceiling", new Vector3(0, Ceiling + 0.05f, 0), new Vector3(HalfWidth * 2, 0.1f, 36), trim, true);
            Box("Cabin end (rear)", new Vector3(0, 1.1f, -18), new Vector3(HalfWidth * 2, 2.2f, 0.2f), trim, true);
            Box("Cockpit bulkhead", new Vector3(0, 1.1f, 18), new Vector3(HalfWidth * 2, 2.2f, 0.2f), trim, true);
            Box("Cockpit door", new Vector3(0, 1.0f, 17.88f), new Vector3(0.8f, 1.95f, 0.05f), Hex("B9BCC0"), false);
            // Side walls with window openings at seated eye height; glass shows the sky and the ground below.
            foreach (var side in new[] { -1, 1 })
            {
                var x = side * HalfWidth;
                Box("Side wall lower", new Vector3(x, 0.45f, 0), new Vector3(0.12f, 0.9f, 36), trim, true);
                Box("Side wall upper", new Vector3(x, 1.75f, 0), new Vector3(0.12f, 0.8f, 36), trim, true);
                for (var z = -17.6f; z <= 15.6f; z += 0.79f)
                {
                    Box("Window frame", new Vector3(x, 1.12f, z + 0.4f), new Vector3(0.12f, 0.45f, 0.48f), trim, true);
                    Box("Window", new Vector3(x + side * 0.03f, 1.12f, z), new Vector3(0.02f, 0.38f, 0.26f), new Color(0.8f, 0.9f, 1f, 0.15f), false);
                }
                Paint(Box("Overhead lockers", new Vector3(side * (HalfWidth - 0.4f), 1.85f, -2), new Vector3(0.8f, 0.5f, 31), Hex("F4F3F0"), true), Hex("F4F3F0"), 0.5f);
            }
            // Seats (SeatController groups "seat-<row><letter>"), seatbelt signs, locker spots.
            var signs = new List<Renderer>();
            var lights = new List<Light>();
            for (var row = 1; row <= 3; row++)
                foreach (var (letter, x) in new[] { ('A', -1.3f), ('C', -0.65f), ('D', 0.65f), ('F', 1.3f) })
                    Seat(row + letter.ToString(), new Vector3(x, 0, 13.4f - (row - 1) * 1.0f), business, 0.6f);
            Box("Business divider", new Vector3(0, 1.1f, 10.4f), new Vector3(HalfWidth * 2, 2.2f, 0.06f), new Color(0.9f, 0.9f, 0.9f, 0.4f), false);
            for (var row = 10; row <= 30; row++)
            {
                var z = 9.6f - (row - 10) * 0.79f;
                foreach (var (letter, x) in new[] { ('A', -1.45f), ('B', -0.97f), ('C', -0.49f), ('D', 0.49f), ('E', 0.97f), ('F', 1.45f) })
                    Seat(row + letter.ToString(), new Vector3(x, 0, z), fabric, 0.46f);
                if (row % 2 == 0)
                {
                    Spot("locker", "Put your carry-on in the overhead locker", new Vector3(-0.6f, 1.6f, z), 1.4f);
                    Spot("locker", "Put your carry-on in the overhead locker", new Vector3(0.6f, 1.6f, z), 1.4f);
                }
                if (row % 5 == 0)
                {
                    var sign = Box("Seatbelt sign", new Vector3(0, Ceiling - 0.04f, z), new Vector3(0.25f, 0.04f, 0.12f), Hex("3A3A3A"), false);
                    sign.GetComponent<Renderer>().sharedMaterial.EnableKeyword("_EMISSION");
                    signs.Add(sign.GetComponent<Renderer>());
                }
            }
            Board("ROW 10 ▸ 30", new Vector3(0, 1.95f, 10.0f), 180, new Vector2(1.2f, 0.25f), Hex("23476F"), Color.white);
            for (var z = -14f; z <= 14; z += 7)
            {
                Box("Cabin light strip", new Vector3(0, Ceiling - 0.02f, z), new Vector3(0.5f, 0.02f, 6), Hex("FFF8EC"), false);
                var light = Group("Cabin light", new Vector3(0, Ceiling - 0.15f, z)).gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.range = 6; light.intensity = 1.2f; light.color = Hex("FFF4E6"); light.shadows = LightShadows.None;
                lights.Add(light);
            }
            // Galley, lavatory, door, crew.
            Paint(Box("Galley units", new Vector3(1.0f, 1.0f, 16.6f), new Vector3(1.6f, 2.0f, 1.6f), Hex("C9CCD0"), true), Hex("C9CCD0"), 0.55f, 0.7f);
            Box("Trolley", new Vector3(0.95f, 0.5f, 15.4f), new Vector3(0.3f, 1.0f, 0.8f), Hex("D9DCE0"), true);
            Box("Lavatory", new Vector3(-1.1f, 1.0f, -16.6f), new Vector3(1.3f, 2.0f, 1.6f), Hex("E2E2DE"), true);
            Board("LAVATORY", new Vector3(-1.1f, 1.95f, -15.78f), 180, new Vector2(0.8f, 0.2f), Hex("2E4057"), Color.white);
            Spot("lavatory", "Lavatory", new Vector3(-1.1f, 1, -15.5f), 1.4f);
            Box("Front door", new Vector3(-HalfWidth - 0.02f, 1.0f, 15.0f), new Vector3(0.05f, 1.9f, 0.9f), Hex("B9BCC0"), false);
            var exit = new GameObject("Leave the aircraft").AddComponent<TravelPortal>();
            exit.transform.position = new Vector3(-1.4f, 1, 15.0f);
            exit.TargetScene = "ArrivalAirport";
            exit.RequiredStage = TravelStage.Flown;
            exit.Prompt = "Leave the aircraft";
            exit.Locked = "the aircraft hasn't landed yet";
            exit.Range = 1.4f;
            flow.Spawn = Group("Spawn · cabin door", new Vector3(-0.2f, 0.05f, 14.6f), 180);
            Npc("crew", "Isabel", "Flight attendant", "MidnightLace", new Vector3(0.6f, 0.05f, 14.8f), 270);
            Npc("pilot", "Captain Ross", "Captain", "UrbanTrench", new Vector3(0.2f, 0.05f, 17.3f), 180);
            Npc("passenger", "Mr Tan", "Passenger", "Contemplation", new Vector3(0.97f, 0.05f, 6.4f), 0);
            // Outside: patchwork farmland, a city grid and a lake under the windows; it drops away on climb.
            var outside = Group("Outside (ground below)", new Vector3(0, -3, 0));
            var random = new System.Random(5);
            for (var x = -12; x <= 12; x++)
                for (var z = -30; z <= 30; z++)
                {
                    var colour = (x + z) % 7 == 0 ? Hex("8A8F96") : Color.Lerp(Hex("5E7A42"), Hex("A39A5C"), (float)random.NextDouble());
                    Box("Field", new Vector3(x * 120, 0, z * 120), new Vector3(118, 0.5f, 118), colour, false, outside);
                }
            Box("Lake", new Vector3(-600, 0.3f, 400), new Vector3(500, 0.5f, 700), Hex("3E6B85"), false, outside);
            flow.Outside = outside;
            flow.SeatbeltSigns = signs.ToArray();
            flow.CabinLights = lights.ToArray();
            flow.MapBounds = new Rect(-4, -20, 8, 40);

            var (player, camera) = Player(flow.Spawn.position + Vector3.up * 0.05f, 180);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 4000;
            Finish(player, camera, flow, "SkyLark Air cabin", "AircraftCabin");
            flow.gameObject.AddComponent<AirportAudio>().Ambience = AirportAudio.Mode.Cabin;
            SkyInstaller.Install();
            LifeBuild.Save(scene, ScenePath);
        }

        private static void Seat(string id, Vector3 floor, Color fabric, float width)
        {
            var seat = Group("Seat " + id, floor);
            Box("Cushion", new Vector3(0, 0.44f, 0), new Vector3(width, 0.12f, 0.46f), fabric, false, seat);
            Box("Backrest", new Vector3(0, 0.82f, -0.25f), new Vector3(width, 0.72f, 0.1f), fabric, true, seat);
            Box("Headrest cover", new Vector3(0, 1.13f, -0.24f), new Vector3(width * 0.8f, 0.12f, 0.11f), Hex("E9E7E2"), false, seat);
            Box("Seat frame", new Vector3(0, 0.2f, 0), new Vector3(width * 0.9f, 0.35f, 0.05f), Hex("6B6F75"), false, seat);
            var controller = Group("Sit point", new Vector3(0, 0.04f, 0.02f), 0, seat).gameObject.AddComponent<SeatController>();
            controller.Group = "seat-" + id;
            controller.Range = 1.2f;
            Label(id, new Vector3(0, 1.0f, -0.31f), 0, 0.012f, Hex("FFFFFF"), seat);
        }
    }
}
