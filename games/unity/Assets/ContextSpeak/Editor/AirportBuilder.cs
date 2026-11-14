using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;
using static ContextSpeak.Editor.AirportKit;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Build International Airport: Airport.unity (exterior, departure hall, check-in,
    /// baggage, security, passport control, shopping concourse, three gates, airfield),
    /// AircraftCabin.unity and ArrivalAirport.unity, registered for loading; missing assets reported.
    public static class AirportBuilder
    {
        public const string ScenePath = Scenes + "/Airport.unity";
        private const string Plane = "Assets/Art/airport/SkyLarkAirliner.glb";
        private const float H = 18f;   // terminal hall height

        [UnityEditor.MenuItem("ContextSpeak/Build International Airport")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Missing.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("ContextSpeak", "Checking dependencies…", 0.05f);
                CafeBuild.ConfigurePipeline();
                foreach (var dependency in new[] { Plane, "Assets/Art/city/CityTaxi.glb", "Assets/Art/city/CityBus.glb", "Assets/Art/city/CityTram.glb", "Assets/Art/airport/BaggageTug.glb", "Assets/Art/airport/FuelTruck.glb", "Assets/Art/airport/PushbackTug.glb" })
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(dependency) == null) Missing.Add(dependency + " (not imported — placeholders will be used)");
                Directory.CreateDirectory(Data + "/Materials");
                Directory.CreateDirectory(Scenes);
                AssetDatabase.Refresh();
                EditorUtility.DisplayProgressBar("ContextSpeak", "Building the departure airport…", 0.15f);
                Build();
                EditorUtility.DisplayProgressBar("ContextSpeak", "Building the aircraft cabin…", 0.6f);
                AirportCabinBuilder.Build();
                EditorUtility.DisplayProgressBar("ContextSpeak", "Building the arrival airport…", 0.8f);
                ArrivalAirportBuilder.Build();
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            var report = "International airport built: Airport, AircraftCabin, ArrivalAirport (registered in Build Settings). Rebuild Metro City to add the Airport Express entrance at Central Station.";
            if (Missing.Count > 0) Debug.LogWarning(report + "\nMissing assets / placeholders:\n- " + string.Join("\n- ", Missing.Distinct()));
            else Debug.Log(report);
            var errors = AirportChecks.Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak airport checks passed.");
            else Debug.LogError("ContextSpeak airport checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Metro City International Airport");
            Daylight("Airport", "E8EEF4");
            var flow = new GameObject("Airport systems").AddComponent<DepartureFlow>();

            Exterior(flow);
            Terminal(flow);
            Concourse(flow);
            Airfield();

            var (player, camera) = Player(flow.TrainSpawn.position + Vector3.up * 0.1f, 0);
            camera.farClipPlane = 1500;
            Finish(player, camera, flow, "Metro City International Airport", "Airport");
            var audio = flow.gameObject.AddComponent<AirportAudio>();
            audio.Ambience = AirportAudio.Mode.Terminal;
            audio.Hums = Object.FindObjectsByType<Conveyor>(FindObjectsSortMode.None).Select(conveyor => conveyor.transform).ToArray();
            audio.Cafes = new[] { GameObject.Find("Skyline Coffee")?.transform }.Where(item => item != null).ToArray();
            flow.MapLabels = new List<string>
            {
                "Rail station|-110|-72", "Drop-off|-25|-48", "Bus|40|-46", "Parking|100|-90", "Check-in A|-30|-8", "Kiosks|0|-20", "Departures board|0|-12",
                "Information|0|-24", "Security|0|1", "Passport control|-3|11", "Duty free|-11|40", "Coffee|11|39", "Restaurant|11|52",
                "Exchange|-11|64", "Lounge|-11|72", "Gate G1|-10|100", "Gate G2|10|120", "Gate G3|-10|145", "Control tower|110|80",
            };
            SkyInstaller.Install();
            LifeBuild.Save(scene, ScenePath);
        }

        // ------------------------------------------------------------------ Area A: exterior
        private static void Exterior(DepartureFlow flow)
        {
            Floor("Landside ground", new Vector3(0, 0, -110), new Vector2(420, 160), Hex("5E7A4A"), 0.1f);
            Floor("Airside apron", new Vector3(0, 0, 130), new Vector2(420, 320), Hex("9C9A94"), 0.2f);
            // Multi-lane access road (4 lanes) and the departures kerb lane.
            Floor("Access road", new Vector3(0, 0.01f, -64), new Vector2(380, 15), Asphalt, 0.25f);
            Floor("Drop-off lane", new Vector3(0, 0.01f, -50), new Vector2(160, 7), Asphalt, 0.25f);
            for (var x = -185f; x < 185; x += 6) for (var lane = -1; lane <= 1; lane++) Box("Lane marking", new Vector3(x, 0.02f, -64 + lane * 3.7f), new Vector3(3, 0.01f, 0.15f), LinePaint, false);
            Box("Kerb", new Vector3(0, 0.08f, -46.4f), new Vector3(160, 0.16f, 0.3f), Kerb, true);
            Box("Kerb median", new Vector3(0, 0.08f, -56.6f), new Vector3(160, 0.16f, 1.2f), Kerb, true);
            Floor("Sidewalk", new Vector3(0, 0.15f, -38.2f), new Vector2(160, 16.4f), Hex("C9C5BC"), 0.35f);
            for (var x = -4f; x <= 4; x += 1.2f) Box("Crosswalk", new Vector3(x, 0.03f, -64), new Vector3(0.6f, 0.01f, 15), LinePaint, false);
            foreach (var x in new[] { -6.5f, 6.5f })
            {
                var pole = Group("Traffic light", new Vector3(x, 0, x < 0 ? -72.5f : -55.5f), x < 0 ? 0 : 180);
                Paint(Shape("Pole", PrimitiveType.Cylinder, new Vector3(0, 2.2f, 0), new Vector3(0.12f, 2.2f, 0.12f), Hex("2B3036"), true, pole), Hex("2B3036"), 0.5f, 0.5f);
                Box("Signal head", new Vector3(0, 4.6f, 0.15f), new Vector3(0.35f, 0.95f, 0.3f), Hex("1C1F22"), false, pole);
                Paint(Shape("Red", PrimitiveType.Sphere, new Vector3(0, 4.9f, 0.32f), Vector3.one * 0.2f, Hex("FF3B2F"), false, pole), Hex("FF3B2F"), 0.8f);
                Shape("Green", PrimitiveType.Sphere, new Vector3(0, 4.3f, 0.32f), Vector3.one * 0.2f, Hex("1F5F36"), false, pole);
            }
            Board("DEPARTURES ↑     ARRIVALS →     PARKING  P", new Vector3(-40, 6.2f, -71), 0, new Vector2(16, 1.3f), Hex("1D4F91"), Color.white);
            Paint(Box("Sign gantry", new Vector3(-40, 3.2f, -71), new Vector3(0.3f, 6.4f, 0.3f), Steel, true), Steel, 0.5f, 0.8f);
            for (var x = -70f; x <= 70; x += 14) StreetLamp(new Vector3(x, 0.15f, -45.4f), 180);
            for (var x = -63f; x <= 63; x += 14)
            {
                Box("Planter", new Vector3(x, 0.55f, -41), new Vector3(3, 0.8f, 1.4f), Concrete, true);
                Shape("Planter shrubs", PrimitiveType.Sphere, new Vector3(x, 1.2f, -41), new Vector3(2.6f, 0.9f, 1.2f), Grass, false);
            }
            // Drop-off: parked taxi and driver; arrival pick-up to the east.
            flow.TaxiSpawn = Group("Spawn · taxi", new Vector3(-22, 0.2f, -44.5f), 0);
            Model("Assets/Art/city/CityTaxi.glb", new Vector3(-25, 0.02f, -50), 90, 1.5f, "Taxi (drop-off)");
            Npc("taxi", "Raj", "Taxi driver", "Plaid", new Vector3(-23.5f, 0.15f, -46), 200);
            Board("DEPARTURES DROP-OFF · 5 MIN", new Vector3(-25, 3, -45.5f), 0, new Vector2(5, 0.5f), Hex("1D4F91"), Color.white);
            Board("ARRIVALS PICK-UP →", new Vector3(55, 3, -45.5f), 0, new Vector2(4.5f, 0.5f), Hex("F2C14E"), Hex("1C1F22"));
            Board("BUS 12 · AIRPORT LOOP", new Vector3(40, 2.6f, -45.5f), 0, new Vector2(4, 0.5f), Hex("C0392B"), Color.white);
            Paint(Box("Bus shelter roof", new Vector3(40, 2.9f, -43.5f), new Vector3(6, 0.1f, 2.4f), Glass, false), Glass, 0.9f);
            // Moving traffic on the access road.
            Traffic("Assets/Art/city/CityTaxi.glb", 1.5f, -66, 9, 0);
            Traffic("Assets/Art/city/CityTaxi.glb", 1.5f, -62.3f, 8, 120);
            Traffic("Assets/Art/city/CityBus.glb", 3.2f, -60, 7, 60, new Vector3(40, 0, -50));
            // Rail station (Airport Express from Metro City Central).
            Floor("Rail platform", new Vector3(-110, 0.9f, -80), new Vector2(60, 6), Hex("BDB8AE"), 0.3f);
            Box("Platform edge", new Vector3(-110, 0.92f, -83.1f), new Vector3(60, 0.02f, 0.25f), Yellow, false);
            Paint(Box("Station canopy", new Vector3(-110, 5, -80), new Vector3(60, 0.25f, 9), Aluminium, false), Aluminium, 0.6f, 0.8f);
            for (var x = -135f; x <= -85; x += 10) Steelwork("Canopy column", new Vector3(x, 2.5f, -77), new Vector3(0.3f, 5, 0.3f));
            Model("Assets/Art/city/CityTram.glb", new Vector3(-110, 0.05f, -87), 90, 3.6f, "Airport Express train");
            Board("AIRPORT EXPRESS · Metro City Central 18 min", new Vector3(-110, 3.8f, -76), 0, new Vector2(12, 0.8f), Hex("1D4F91"), Color.white);
            Box("Station steps", new Vector3(-110, 0.45f, -75.5f), new Vector3(8, 0.9f, 3), Concrete, true);
            flow.TrainSpawn = Group("Spawn · train", new Vector3(-110, 1.0f, -79), 0);
            // Parking garage (three decks).
            for (var level = 0; level < 3; level++)
                Box("Parking deck", new Vector3(100, 0.3f + level * 3.2f, -92), new Vector3(60, 0.3f, 36), Concrete, true);
            for (var x = 72f; x <= 128; x += 8) for (var z = -108f; z <= -76; z += 8) Box("Garage column", new Vector3(x, 4.8f, z), new Vector3(0.5f, 9.6f, 0.5f), Concrete, true);
            Board("P  PARKING · 1,200 SPACES", new Vector3(100, 10.5f, -73.8f), 0, new Vector2(9, 1), Hex("1D4F91"), Color.white);
            // Keep the player landside: boundaries around the forecourt.
            Invisible("Landside boundary west", new Vector3(-142, 3, -60), new Vector3(1, 6, 70));
            Invisible("Landside boundary east", new Vector3(142, 3, -60), new Vector3(1, 6, 70));
            Invisible("Landside boundary south", new Vector3(0, 3, -96), new Vector3(286, 6, 1));
            Invisible("Landside to airside west", new Vector3(-96, 3, -27), new Vector3(92, 6, 1));
            Invisible("Landside to airside east", new Vector3(96, 3, -27), new Vector3(92, 6, 1));
        }

        private static void Traffic(string file, float height, float laneZ, float speed, float startX, Vector3? stop = null)
        {
            var vehicle = Model(file, new Vector3(-180 + startX, 0.02f, laneZ), 90, height, "Traffic");
            var mover = vehicle.gameObject.AddComponent<PathMover>();
            mover.Points = new List<PathPoint>
            {
                new PathPoint(new Vector3(-180, 0.02f, laneZ), speed, 0, true),
                new PathPoint(stop ?? new Vector3(0, 0.02f, laneZ), speed, stop.HasValue ? 12 : 0),
                new PathPoint(new Vector3(180, 0.02f, laneZ), speed),
            };
            if (stop.HasValue) mover.Points.Insert(1, new PathPoint(new Vector3(stop.Value.x - 20, 0.02f, laneZ), speed));
            mover.Points.Add(new PathPoint(new Vector3(-180, 0.02f, laneZ), speed, 2, true));
        }

        // ------------------------------------------------------------------ Areas B–F: terminal
        private static void Terminal(DepartureFlow flow)
        {
            Floor("Terminal floor (polished stone)", new Vector3(0, 0.05f, 0), new Vector2(100, 60), Stone, 0.82f);
            for (var x = -45f; x <= 45; x += 5) Box("Floor joint", new Vector3(x, 0.06f, 0), new Vector3(0.04f, 0.005f, 60), DarkStone, false);
            Curtain("Glass facade", new Vector3(-50, 0.05f, -30), new Vector3(50, 0.05f, -30), H, new Vector2(30, 3.4f), new Vector2(70, 3.4f));
            flow.DepartureHall = new Bounds(new Vector3(0, 5, -15), new Vector3(100, 12, 30));
            SlidingDoor("Entrance doors west", new Vector3(-20, 0.05f, -30), 0, 3.2f).Range = 3.4f;
            SlidingDoor("Entrance doors east", new Vector3(20, 0.05f, -30), 0, 3.2f).Range = 3.4f;
            Wall("Terminal wall west", new Vector3(-50, 0, -30), new Vector3(-50, 0, 30), H, Concrete);
            Wall("Terminal wall east", new Vector3(50, 0, -30), new Vector3(50, 0, 30), H, Concrete);
            Curtain("Airside glass west", new Vector3(-50, 0.05f, 30), new Vector3(-15, 0.05f, 30), H);
            Curtain("Airside glass east", new Vector3(15, 0.05f, 30), new Vector3(50, 0.05f, 30), H);
            // Roof: steel trusses, skylights.
            Box("Roof", new Vector3(0, H + 0.2f, 0), new Vector3(101, 0.4f, 61), Hex("E4E6E8"), true);
            for (var x = -40f; x <= 40; x += 10) Steelwork("Roof beam", new Vector3(x, H - 0.5f, 0), new Vector3(0.5f, 1, 60));
            for (var z = -20f; z <= 20; z += 20) Box("Skylight", new Vector3(0, H - 0.05f, z), new Vector3(90, 0.05f, 4), new Color(0.85f, 0.95f, 1f, 0.5f), false);
            for (var x = -40f; x <= 40; x += 20) for (var z = -20f; z <= 20; z += 20) Paint(Shape("Steel column", PrimitiveType.Cylinder, new Vector3(x, H / 2, z), new Vector3(0.8f, H / 2, 0.8f), Steel, true), Steel, 0.6f, 0.85f);
            for (var x = -40f; x <= 40; x += 20) for (var z = -24f; z <= 24; z += 12) Lamp(new Vector3(x, H - 1.2f, z), 22, 1.4f, "F4F8FF");

            // B — departure hall: departures board, kiosks, information desk, seating, trolleys, restrooms.
            var board = ScreenBoard("Departures board", "DEPARTURES", new Vector3(0, 4.2f, -12), 0, new Vector2(9, 4.4f), Hex("FFC83D"));
            var boardBack = ScreenBoard("Departures board (west)", "DEPARTURES", new Vector3(-49.7f, 5, -14), -90, new Vector2(8, 4), Hex("FFC83D"));
            flow.FlightBoards = new[] { board, boardBack };
            Paint(Box("Board pillar", new Vector3(0, 1, -11.8f), new Vector3(1.2f, 2, 0.6f), Aluminium, true), Aluminium, 0.6f, 0.9f);
            Spot("board", "Check the departures board", new Vector3(0, 1.5f, -13), 4);
            Spot("board", "Check the departures board", new Vector3(-48, 1.5f, -14), 4);
            for (var i = 0; i < 4; i++)
            {
                var x = -9 + i * 6;
                Paint(Box("Self-service kiosk", new Vector3(x, 0.8f, -20), new Vector3(0.7f, 1.6f, 0.5f), Hex("F4F6F8"), true), Hex("F4F6F8"), 0.5f);
                ScreenBoard("Kiosk screen", "SkyLark Air\nBook · Check in", new Vector3(x, 1.45f, -20.27f), 0, new Vector2(0.6f, 0.45f), Color.white);
                Spot("kiosk", "Use the self-service kiosk", new Vector3(x, 1, -21), 1.8f);
            }
            Paint(Shape("Information desk", PrimitiveType.Cylinder, new Vector3(0, 0.55f, -25), new Vector3(4, 0.55f, 4), Hex("F4F6F8"), true), Hex("F4F6F8"), 0.6f);
            Board("i  INFORMATION", new Vector3(0, 3.2f, -25), 0, new Vector2(3.4f, 0.6f), Teal, Color.white);
            Npc("info", "Mei", "Information officer", "VirtualGuide", new Vector3(0, 0.05f, -24.4f), 180);
            for (var i = 0; i < 3; i++) Bench(new Vector3(36 + i * 0, 0.05f, -25 + i * 3), 180, 6, "hall", Hex("2E4E6E"));
            Board("RESTROOMS  ·  ♿", new Vector3(49.6f, 3, -10), 90, new Vector2(3.4f, 0.6f), Hex("2E4057"), Color.white);
            Spot("restroom", "Restrooms", new Vector3(48.8f, 1, -10), 2);
            for (var i = 0; i < 6; i++) Paint(Box("Baggage trolley", new Vector3(-44 + i * 0.4f, 0.55f, -27), new Vector3(0.6f, 1.1f, 1.1f), Steel, false), Steel, 0.5f, 0.85f);
            Spot("trolley", "Take a trolley", new Vector3(-43, 1, -26), 2);
            Npc("cleaner", "Ana", "Cleaner", "QuietMorning", new Vector3(20, 0.05f, -18), 0, new Vector3(20, 0.05f, -18), new Vector3(-20, 0.05f, -16), new Vector3(-10, 0.05f, -26));
            Npc("traveller", "Lucas", "Traveller", "SkateboardBoy", new Vector3(-12, 0.05f, -26), 0, new Vector3(-12, 0.05f, -26), new Vector3(-30, 0.05f, -12));
            Npc("traveller", "Priya", "Traveller", "Schoolgirl", new Vector3(14, 0.05f, -24), 180, new Vector3(14, 0.05f, -24), new Vector3(30, 0.05f, -8));

            // C/D — SkyLark Air check-in row A with belt, scale and queue.
            Paint(Box("Check-in island A", new Vector3(-30, 0.55f, -6), new Vector3(28, 1.1f, 1.0f), Hex("EEF0F2"), true), Hex("EEF0F2"), 0.55f);
            Box("Check-in island A front", new Vector3(-30, 0.55f, -6.52f), new Vector3(28, 1.0f, 0.04f), Brand, false);
            for (var i = 0; i < 6; i++)
            {
                var x = -42.5f + i * 5;
                Board("A" + (i + 1), new Vector3(x, 2.6f, -6.3f), 0, new Vector2(0.9f, 0.5f), Brand, Color.white);
                Box("Desk screen", new Vector3(x + 0.6f, 1.35f, -5.9f), new Vector3(0.5f, 0.35f, 0.04f), ScreenBlack, false);
                Box("Bag scale", new Vector3(x - 1.2f, 0.12f, -7.0f), new Vector3(0.9f, 0.24f, 1.0f), Steel, true);
            }
            Board("SKYLARK AIR  ·  CHECK-IN A1–A6", new Vector3(-30, 4.4f, -6.2f), 0, new Vector2(12, 1.1f), Brand, Color.white);
            flow.ScaleReadout = Label("0.0 kg", new Vector3(-31.2f, 1.45f, -6.6f), 0, 0.04f, Hex("7CF0B4"));
            var belt = Group("Check-in belt", new Vector3(-30, 0, -4.6f));
            Paint(Box("Belt", new Vector3(-30, 0.45f, -4.6f), new Vector3(28, 0.1f, 0.8f), Hex("2A2C2E"), false), Hex("2A2C2E"), 0.4f);
            var beltStart = Group("Belt start", new Vector3(-31.2f, 0.75f, -4.6f));
            var beltTurn = Group("Belt end", new Vector3(-45.5f, 0.75f, -4.6f));
            var beltHole = Group("Belt hatch", new Vector3(-45.5f, 0.75f, -2.4f));
            Box("Baggage hatch", new Vector3(-45.5f, 1.2f, -3.2f), new Vector3(1.4f, 1.6f, 0.2f), Hex("1C1F22"), false);
            var conveyor = belt.gameObject.AddComponent<Conveyor>();
            conveyor.Route = new[] { beltStart, beltTurn, beltHole };
            conveyor.Speed = 1.1f;
            flow.CheckInBelt = conveyor;
            flow.BeltStart = beltStart;
            Npc("checkin", "Hannah", "SkyLark Air check-in agent", "Secretary", new Vector3(-30, 0.05f, -5.3f), 180);
            Npc("staff", "Omar", "SkyLark Air agent", "Executive", new Vector3(-40, 0.05f, -5.3f), 180);
            var slots = Serpentine("Check-in queue", new Vector3(-36, 0.05f, -17), Vector3.forward, 4, 8);
            Queue(slots, new[] { ("Ali", "Ali"), ("Rose", "Rose"), ("Dawn", "Dawn"), ("Tom", "SoccerCaptain") }, new Vector3(-24, 0.05f, -20));
            // Other airlines (row B).
            Paint(Box("Check-in island B", new Vector3(30, 0.55f, -6), new Vector3(28, 1.1f, 1.0f), Hex("EEF0F2"), true), Hex("EEF0F2"), 0.55f);
            Box("Check-in island B front", new Vector3(30, 0.55f, -6.52f), new Vector3(28, 1.0f, 0.04f), Hex("6B2D5C"), false);
            Board("NIMBUS AIRWAYS  ·  AURORA AIR  ·  B1–B6", new Vector3(30, 4.4f, -6.2f), 0, new Vector2(14, 1.1f), Hex("6B2D5C"), Color.white);
            Npc("staff", "Grace", "Nimbus Airways agent", "BlueLace", new Vector3(26, 0.05f, -5.3f), 180);
            Box("Oversized baggage", new Vector3(-46, 1.1f, 1.5f), new Vector3(3, 2.2f, 2), Hex("4A4F54"), true);
            Board("OVERSIZED BAGGAGE", new Vector3(-46, 2.8f, 0.4f), 0, new Vector2(3, 0.4f), Yellow, Hex("1C1F22"));

            // E — security (landside, z −4…4), gate in the glass partition at z = 4.
            Curtain("Security partition", new Vector3(-50, 0.05f, 4), new Vector3(50, 0.05f, 4), 4.5f, new Vector2(50, 4));
            flow.SecurityGate = SlidingDoor("Security exit gate", new Vector3(0, 0.05f, 4), 0, 3.6f);
            flow.SecurityGate.Locked = true;
            Board("SECURITY  ·  ALL DEPARTING PASSENGERS", new Vector3(0, 5.6f, 3.8f), 0, new Vector2(10, 1), Hex("1C1F22"), Hex("FFC83D"));
            var securitySlots = Serpentine("Security queue", new Vector3(-12, 0.05f, -4), Vector3.right, 2, 9);
            Queue(securitySlots, new[] { ("Ken", "Contemplation"), ("Sara", "OpenArms") }, new Vector3(-14, 0.05f, -10));
            Box("Tray table", new Vector3(-4, 0.45f, 1.5f), new Vector3(3, 0.9f, 0.8f), Steel, true);
            for (var i = 0; i < 4; i++) Box("Grey tray", new Vector3(-5.2f + i * 0.8f, 0.95f, 1.5f), new Vector3(0.6f, 0.1f, 0.45f), Hex("8A9399"), false);
            Paint(Box("X-ray machine", new Vector3(-0.5f, 1.0f, 1.5f), new Vector3(2.2f, 2.0f, 1.2f), Hex("DADDE0"), true), Hex("DADDE0"), 0.5f);
            Box("X-ray tunnel", new Vector3(-0.5f, 0.85f, 0.85f), new Vector3(1.2f, 0.7f, 0.02f), Hex("1C1F22"), false);
            var xray = Group("X-ray belt", new Vector3(0, 0, 1.5f));
            var xrayConveyor = xray.gameObject.AddComponent<Conveyor>();
            xrayConveyor.Route = new[] { Group("X-ray in", new Vector3(-3, 0.95f, 0.6f)), Group("X-ray out", new Vector3(2, 0.95f, 0.6f)) };
            Paint(Box("X-ray belt surface", new Vector3(-0.5f, 0.88f, 0.6f), new Vector3(5, 0.06f, 0.7f), Hex("2A2C2E"), false), Hex("2A2C2E"), 0.4f);
            foreach (var x in new[] { 3.6f, 4.6f }) Paint(Box("Walk-through scanner side", new Vector3(x, 1.1f, 1.5f), new Vector3(0.2f, 2.2f, 0.6f), Hex("DADDE0"), true), Hex("DADDE0"), 0.5f);
            Box("Walk-through scanner top", new Vector3(4.1f, 2.25f, 1.5f), new Vector3(1.2f, 0.2f, 0.6f), Hex("DADDE0"), false);
            Box("Inspection table", new Vector3(7.5f, 0.45f, 2.6f), new Vector3(2, 0.9f, 0.9f), Steel, true);
            Npc("security", "Officer Brooks", "Security officer", "OfficerJames", new Vector3(5.6f, 0.05f, 0.6f), 200);
            Npc("staff", "Officer Diaz", "Security officer", "UrbanTrench", new Vector3(-0.5f, 0.05f, 2.7f), 180);

            // F — departure passport control (z 4…14), gate at z = 14; e-gates.
            Curtain("Passport partition", new Vector3(-50, 0.05f, 14), new Vector3(50, 0.05f, 14), 4.5f, new Vector2(47, 3));
            flow.PassportGate = SlidingDoor("Passport control gate", new Vector3(-3, 0.05f, 14), 0, 2.8f);
            flow.PassportGate.Locked = true;
            Paint(Box("Passport booth", new Vector3(-5.6f, 1.1f, 12), new Vector3(1.6f, 2.2f, 2.4f), Hex("E8EBEE"), true), Hex("E8EBEE"), 0.5f);
            Box("Booth glass", new Vector3(-4.75f, 1.6f, 12), new Vector3(0.05f, 1.1f, 2.2f), Glass, false);
            Box("Passport scanner", new Vector3(-4.6f, 1.05f, 11.4f), new Vector3(0.3f, 0.12f, 0.3f), Hex("1C1F22"), false);
            Npc("passport", "Officer Lim", "Passport officer", "Executive", new Vector3(-4.0f, 0.05f, 11.2f), 270);
            Board("PASSPORT CONTROL  ·  DEPARTURES", new Vector3(-3, 5.4f, 13.8f), 0, new Vector2(9, 0.9f), Hex("1C1F22"), Hex("FFC83D"));
            for (var i = 0; i < 4; i++)
            {
                var x = 8 + i * 2.2f;
                Box("E-gate", new Vector3(x, 0.6f, 12), new Vector3(0.35f, 1.2f, 1.8f), Hex("DADDE0"), true);
                Box("E-gate screen", new Vector3(x, 1.3f, 11.2f), new Vector3(0.3f, 0.25f, 0.05f), ScreenBlack, false);
            }
            Board("E-GATES · METRO CITY RESIDENTS", new Vector3(11.3f, 3, 13.8f), 0, new Vector2(6, 0.5f), Teal, Color.white);
            Spot("egate", "Use an e-gate", new Vector3(10, 1, 10.5f), 2.5f);
            var passportSlots = Serpentine("Passport queue", new Vector3(-9, 0.05f, 6), Vector3.right, 2, 5);
            Queue(passportSlots, new[] { ("Leo", "SmilingBoy") }, new Vector3(-12, 0.05f, 8));
            Board("ALL GATES  ↑", new Vector3(0, 4, 29.6f), 0, new Vector2(4, 0.6f), Yellow, Hex("1C1F22"));
        }

        private static void Queue(Transform[] slots, (string name, string model)[] people, Vector3 exit)
        {
            var npcs = new List<NPCController>();
            for (var i = 0; i < people.Length && i < slots.Length; i++)
                npcs.Add(Npc("traveller", people[i].name, "Passenger", people[i].model, slots[i].position, slots[i].eulerAngles.y));
            var line = new GameObject("Queue behaviour").AddComponent<QueueLine>();
            line.Slots = slots.Take(Mathf.Max(people.Length, 1)).ToArray();
            line.People = npcs.ToArray();
            line.Exit = Group("Queue exit", exit);
        }

        // ------------------------------------------------------------------ Areas G–H: concourse and gates
        private static void Concourse(DepartureFlow flow)
        {
            Floor("Concourse floor", new Vector3(0, 0.05f, 90), new Vector2(30, 120), Stone, 0.82f);
            Box("Concourse carpet (gates)", new Vector3(0, 0.08f, 120), new Vector3(29, 0.01f, 56), Hex("4C5A6E"), false);
            Curtain("Concourse glass west", new Vector3(-15, 0.05f, 30), new Vector3(-15, 0.05f, 150), 10, new Vector2(70, 2.2f), new Vector2(115, 2.2f));
            Curtain("Concourse glass east", new Vector3(15, 0.05f, 30), new Vector3(15, 0.05f, 150), 10, new Vector2(90, 2.2f));
            Wall("Concourse end", new Vector3(-15, 0, 150), new Vector3(15, 0, 150), 10, Concrete);
            Box("Concourse roof", new Vector3(0, 10.2f, 90), new Vector3(31, 0.4f, 121), Hex("E4E6E8"), true);
            for (var z = 35f; z <= 145; z += 10) Steelwork("Concourse arch", new Vector3(0, 9.6f, z), new Vector3(30, 0.6f, 0.4f));
            Box("Concourse skylight", new Vector3(0, 9.95f, 90), new Vector3(6, 0.05f, 116), new Color(0.85f, 0.95f, 1f, 0.5f), false);
            for (var z = 36f; z <= 146; z += 12) Lamp(new Vector3(0, 9, z), 16, 1.3f, "F4F8FF");

            // G — shops (west), food and drink (east); each is a shopfront with a counter, shelves, sign and staff.
            Shop("Skyline Duty Free", new Vector3(-8, 0, 40), 12, 90, "dutyfree", "Sofia", "Duty-free cashier", "MaidHall", Hex("6B2D5C"));
            Shop("Gate Books", new Vector3(-8, 0, 50), 8, 90, "books", "Daniel", "Bookshop cashier", "Elliot", Hex("2E5E4E"));
            Shop("Metro Souvenirs", new Vector3(-8, 0, 58), 8, 90, "souvenir", "Yuki", "Souvenir shop assistant", "PeachGala", Hex("C0392B"));
            Shop("Currency Exchange", new Vector3(-8, 0, 64), 4, 90, "exchange", "Mr Haddad", "Currency exchange clerk", "Ali", Hex("D4A017"));
            Shop("SkyLark Lounge", new Vector3(-8, 0, 72), 12, 90, "lounge", "Clara", "Lounge host", "BlueLace", Hex("0F3D8C"));
            Shop("Skyline Coffee", new Vector3(8, 0, 39), 10, -90, "barista", "Jamie", "Barista", "SmilingBoy", Hex("6F4E37"));
            Shop("Runway Kitchen", new Vector3(8, 0, 52), 16, -90, "waiter", "Marco", "Restaurant waiter", "Elliot", Hex("8E3B2E"), true);
            Shop("QuickStop", new Vector3(8, 0, 64), 8, -90, "convenience", "Nia", "Convenience store cashier", "Dawn", Hex("2E7DA4"));

            // H — gates G1 (west), G2 (east), G3 (west) with seating, desk, monitor, queue, jet bridge and aircraft.
            var gates = new List<Transform>(); var monitors = new List<TextMesh>(); var doors = new List<DoorController>();
            foreach (var (id, z, side) in new[] { ("G1", 100f, -1), ("G2", 120f, 1), ("G3", 145f, -1) })
            {
                var gate = Group(id, new Vector3(side * 8, 0.05f, z));
                gates.Add(gate);
                var x = side * 8;
                Paint(Box("Gate desk", new Vector3(x + side * 3, 0.55f, z - 6), new Vector3(2.4f, 1.1f, 0.8f), Hex("F4F6F8"), true), Hex("F4F6F8"), 0.55f);
                monitors.Add(ScreenBoard("Gate monitor " + id, "GATE " + id, new Vector3(x + side * 3, 3.2f, z - 6.5f), 0, new Vector2(2.6f, 1.5f), Color.white));
                Board("GATE " + id, new Vector3(x, 6.4f, z), side < 0 ? -90 : 90, new Vector2(3, 1), Yellow, Hex("1C1F22"));
                Npc("gate-" + id, id == "G1" ? "Aisha" : id == "G2" ? "Ben" : "Chloe", "Gate agent", id == "G2" ? "UrbanTrench" : "Secretary", new Vector3(x + side * 3, 0.05f, z - 5.3f), 180);
                for (var row = 0; row < 4; row++) Bench(new Vector3(x - side * 1 + side * row * 0.0f, 0.05f, z - 2 + row * 2.2f), side < 0 ? 90 : -90, 6, "gate", Hex("2E4E6E"));
                Serpentine("Boarding lane " + id, new Vector3(x + side * 1.5f, 0.05f, z - 9), Vector3.forward, 1, 4, 1);
                // Jet bridge from the glass wall out to the aircraft door.
                var bridgeX = side * 15;
                var door = HingedDoor("Jet bridge door " + id, new Vector3(bridgeX, 0.05f, z), 90, 2.0f, Aluminium);
                door.Locked = true;
                doors.Add(door);
                const float bridgeLength = 22;
                Paint(Box("Jet bridge floor", new Vector3(bridgeX + side * bridgeLength / 2, 0.08f, z), new Vector3(bridgeLength, 0.15f, 2.6f), Hex("5A5F66"), true), Hex("5A5F66"), 0.4f);
                Paint(Box("Jet bridge roof", new Vector3(bridgeX + side * bridgeLength / 2, 2.9f, z), new Vector3(bridgeLength, 0.2f, 3), Aluminium, false), Aluminium, 0.6f, 0.85f);
                foreach (var edge in new[] { -1.4f, 1.4f })
                    Paint(Box("Jet bridge side", new Vector3(bridgeX + side * bridgeLength / 2, 1.45f, z + edge), new Vector3(bridgeLength, 2.9f, 0.1f), Aluminium, true), Aluminium, 0.6f, 0.85f);
                Board("SKYLARK AIR", new Vector3(bridgeX + side * bridgeLength / 2, 2.4f, z - 1.47f), 0, new Vector2(4, 0.5f), Brand, Color.white);
                var portal = new GameObject("Aircraft door " + id).AddComponent<TravelPortal>();
                portal.transform.position = new Vector3(bridgeX + side * (bridgeLength - 0.6f), 1, z);
                portal.TargetScene = "AircraftCabin";
                portal.RequiredStage = TravelStage.Boarded;
                portal.Gate = id;
                portal.Prompt = "Board the aircraft";
                portal.Locked = "Boarding pass not scanned yet";
                portal.Range = 1.8f;
                Invisible("Jet bridge end", new Vector3(bridgeX + side * (bridgeLength + 0.2f), 1.5f, z), new Vector3(0.2f, 3, 2.6f));
                // Parked parallel to the concourse, nose +Z; its forward door (13.2 m ahead of centre) meets the bridge.
                var plane = Model(Plane, new Vector3(bridgeX + side * (bridgeLength + 2.0f), 0.05f, z - 13.2f), 0, 11.8f, "SkyLark aircraft at " + id);
                plane.name = "Aircraft at " + id;
                Model("Assets/Art/airport/BaggageTug.glb", new Vector3(bridgeX + side * 32, 0.05f, z - 2), 0, 1.6f, "Baggage tug");
            }
            Model("Assets/Art/airport/FuelTruck.glb", new Vector3(54, 0.05f, 104), 0, 3.4f, "Fuel truck");
            Model("Assets/Art/airport/PushbackTug.glb", new Vector3(-39, 0.05f, 109), 0, 1.7f, "Pushback tug");
            flow.Gates = gates.ToArray();
            flow.GateMonitors = monitors.ToArray();
            flow.JetBridgeDoors = doors.ToArray();
            Npc("tutor", "Dr Amara Osei", "Traveller (IELTS examiner)", "ProfessorReed", new Vector3(-9, 0.05f, 100), 90);
            Npc("pilot", "Captain Ross", "SkyLark Air captain", "UrbanTrench", new Vector3(10.5f, 0.05f, 96), -90);
            Npc("staff", "Kai", "Ground staff", "SoccerCaptain", new Vector3(5, 0.05f, 125), 0, new Vector3(5, 0.05f, 125), new Vector3(-5, 0.05f, 80));
            Npc("cleaner", "Joy", "Cleaner", "QuietMorning", new Vector3(0, 0.05f, 70), 0, new Vector3(0, 0.05f, 70), new Vector3(0, 0.05f, 135));
            Npc("traveller", "Noah", "Traveller", "Contemplation", new Vector3(2, 0.05f, 60), 180, new Vector3(2, 0.05f, 60), new Vector3(-3, 0.05f, 128));
        }

        /// A shopfront set into the concourse wall: side walls, counter, shelves, sign, staff and stock.
        private static void Shop(string title, Vector3 centre, float width, float yaw, string key, string staff, string role, string model, Color brand, bool dining = false)
        {
            var shop = Group(title, centre, yaw);
            const float depth = 7f;
            var floor = Floor(title + " floor", Vector3.zero, new Vector2(width, depth), dining ? Hex("8A6446") : Hex("ECE7DF"), 0.6f);
            floor.transform.SetParent(shop, false);
            floor.transform.localPosition = new Vector3(0, 0.06f, -depth / 2 + 0.25f);
            foreach (var side in new[] { -1, 1 }) Box("Shop wall", new Vector3(side * width / 2, 2.5f, -depth / 2 + 0.5f), new Vector3(0.15f, 5, depth), Hex("F2F2EF"), true, shop);
            Box("Shop back wall", new Vector3(0, 2.5f, -depth + 0.3f), new Vector3(width, 5, 0.2f), Hex("F2F2EF"), true, shop);
            Box("Fascia", new Vector3(0, 4.3f, 0.5f), new Vector3(width, 1.0f, 0.2f), brand, false, shop);
            var sign = Label(title.ToUpperInvariant(), new Vector3(0, 4.3f, 0.62f), 180, Mathf.Min(0.18f, width / (title.Length * 0.55f + 2) / 2.2f), Color.white, shop);
            Paint(Box("Counter", new Vector3(width / 2 - 2.2f, 0.55f, -2.2f), new Vector3(2.6f, 1.1f, 0.8f), brand, true, shop), brand, 0.5f);
            for (var i = 0; i < (dining ? 0 : 3); i++)
            {
                Box("Shelf", new Vector3(-width / 2 + 1.6f + i * 2.2f, 1.1f, -depth + 1.0f), new Vector3(1.8f, 2.2f, 0.5f), Hex("D9D2C5"), true, shop);
                for (var j = 0; j < 8; j++) Box("Product", new Vector3(-width / 2 + 0.9f + i * 2.2f + (j % 4) * 0.4f, 0.6f + (j / 4) * 0.6f, -depth + 0.75f), new Vector3(0.28f, 0.3f, 0.2f), Color.HSVToRGB((j * 0.13f + i * 0.31f) % 1f, 0.55f, 0.85f), false, shop);
            }
            var person = Npc(key, staff, role, model, shop.TransformPoint(new Vector3(width / 2 - 2.2f, 0, -3.0f)), yaw);
            if (dining)
                for (var i = 0; i < 4; i++)
                {
                    var table = Table(shop.TransformPoint(new Vector3(-width / 2 + 2.5f + i * 3.2f, 0.05f, -3.8f)), new Vector2(0.8f, 0.8f), Hex("FBF7EE"));
                    table.SetParent(shop, true);
                    foreach (var s in new[] { -1, 1 }) Chair(table.position + shop.forward * s * 0.78f, yaw + (s > 0 ? 180 : 0), Hex("5B3A26"), "airport-dining");
                }
            if (title == "Skyline Coffee")
                for (var i = 0; i < 2; i++)
                {
                    var table = Table(shop.TransformPoint(new Vector3(-width / 2 + 2 + i * 3, 0.05f, -4.5f)), new Vector2(0.7f, 0.7f), Hex("C9A27A"));
                    table.SetParent(shop, true);
                }
        }

        // ------------------------------------------------------------------ Area K: airfield
        private static void Airfield()
        {
            Floor("Runway", new Vector3(-170, 0.03f, 150), new Vector2(45, 700), Hex("2F3134"), 0.3f);
            for (var z = -180f; z <= 480; z += 30) Box("Runway centreline", new Vector3(-170, 0.05f, z), new Vector3(0.9f, 0.01f, 15), LinePaint, false);
            for (var z = -190f; z <= 490; z += 15)
                foreach (var side in new[] { -1, 1 })
                    Box("Runway edge light", new Vector3(-170 + side * 22.5f, 0.15f, z), new Vector3(0.25f, 0.25f, 0.25f), Hex("FFF6D0"), false);
            Floor("Taxiway", new Vector3(-100, 0.03f, 150), new Vector2(23, 480), Hex("3A3C40"), 0.3f);
            for (var z = -80f; z <= 380; z += 12) Box("Taxiway edge light", new Vector3(-111.5f, 0.15f, z), new Vector3(0.22f, 0.22f, 0.22f), Hex("2F7BFF"), false);
            Floor("Taxiway link", new Vector3(-135, 0.03f, 380), new Vector2(70, 23), Hex("3A3C40"), 0.3f);
            Floor("Taxiway link south", new Vector3(-135, 0.03f, -80), new Vector2(70, 23), Hex("3A3C40"), 0.3f);
            for (var z = -60f; z <= 360; z += 6) Box("Taxiway line", new Vector3(-100, 0.05f, z), new Vector3(0.25f, 0.01f, 3), Yellow, false);
            // Control tower.
            Paint(Shape("Control tower shaft", PrimitiveType.Cylinder, new Vector3(110, 22, 80), new Vector3(5, 22, 5), Concrete, true), Concrete, 0.4f);
            Paint(Shape("Control tower cab", PrimitiveType.Cylinder, new Vector3(110, 46, 80), new Vector3(11, 2.5f, 11), Glass, false), Glass, 0.95f);
            Paint(Shape("Control tower roof", PrimitiveType.Cylinder, new Vector3(110, 48.8f, 80), new Vector3(12, 0.4f, 12), Aluminium, false), Aluminium, 0.6f, 0.8f);
            // An aircraft that lands, taxis to a remote stand, waits, taxis out and takes off — on a loop.
            var plane = Model(Plane, new Vector3(-170, 260, 900), 180, 11.8f, "Animated SkyLark aircraft");
            plane.name = "Animated aircraft (landing → taxi → take-off)";
            var mover = plane.gameObject.AddComponent<PathMover>();
            mover.BankAndPitch = true;
            mover.Points = new List<PathPoint>
            {
                new PathPoint(new Vector3(-170, 260, 1100), 70, 0, true),   // approach from the north
                new PathPoint(new Vector3(-170, 0.05f, 420), 70),           // touchdown
                new PathPoint(new Vector3(-170, 0.05f, 380), 20),           // roll-out
                new PathPoint(new Vector3(-100, 0.05f, 380), 8),            // exit to the taxiway
                new PathPoint(new Vector3(-100, 0.05f, 200), 8),
                new PathPoint(new Vector3(-70, 0.05f, 200), 4, 45),         // remote stand: park and wait
                new PathPoint(new Vector3(-100, 0.05f, 180), 6),
                new PathPoint(new Vector3(-100, 0.05f, -80), 9),            // taxi south
                new PathPoint(new Vector3(-170, 0.05f, -80), 6),
                new PathPoint(new Vector3(-170, 0.05f, -60), 3, 8),         // line up, hold
                new PathPoint(new Vector3(-170, 0.05f, 250), 75),           // take-off roll
                new PathPoint(new Vector3(-170, 60, 520), 80),              // rotate and climb
                new PathPoint(new Vector3(-170, 400, 1300), 90),
            };
            var tug = Model("Assets/Art/airport/BaggageTug.glb", new Vector3(-40, 0.05f, 60), 0, 1.6f, "Baggage tug (moving)");
            var tugMover = tug.gameObject.AddComponent<PathMover>();
            tugMover.Points = new List<PathPoint> { new PathPoint(new Vector3(-40, 0.05f, 60), 5), new PathPoint(new Vector3(-40, 0.05f, 145), 5, 6), new PathPoint(new Vector3(40, 0.05f, 145), 5), new PathPoint(new Vector3(40, 0.05f, 60), 5, 6) };
        }
    }
}
