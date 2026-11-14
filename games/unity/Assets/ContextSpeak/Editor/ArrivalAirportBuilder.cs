using System.Collections.Generic;
using ContextSpeak.Life;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;
using static ContextSpeak.Editor.AirportKit;

namespace ContextSpeak.Editor
{
    /// ArrivalAirport.unity, dressed at runtime for the booked destination: arrival immigration,
    /// baggage claim (three carousels, baggage services), customs, arrival hall (tourist information,
    /// transport board), taxi rank outside, and the destination hotel lobby.
    public static class ArrivalAirportBuilder
    {
        public const string ScenePath = Scenes + "/ArrivalAirport.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Arrival airport");
            Daylight("ArrivalAirport", "E8EEF4");
            var flow = new GameObject("Arrival systems").AddComponent<ArrivalFlow>();
            var signs = new List<TextMesh>();
            Floor("Arrival floor", new Vector3(0, 0.05f, 0), new Vector2(60, 40), Stone, 0.82f);
            Floor("Forecourt", new Vector3(0, 0, -40), new Vector2(120, 40), Hex("C9C5BC"), 0.35f);
            Floor("Taxi lane", new Vector3(0, 0.01f, -47), new Vector2(120, 7), Asphalt, 0.25f);
            Wall("Wall north", new Vector3(-30, 0, 20), new Vector3(30, 0, 20), 9, Concrete);
            Wall("Wall west", new Vector3(-30, 0, -20), new Vector3(-30, 0, 20), 9, Concrete);
            Wall("Wall east", new Vector3(30, 0, -20), new Vector3(30, 0, 20), 9, Concrete);
            Curtain("Arrival facade", new Vector3(-30, 0.05f, -20), new Vector3(30, 0.05f, -20), 9, new Vector2(30, 3.4f));
            SlidingDoor("Arrival exit doors", new Vector3(0, 0.05f, -20), 0, 3.2f).Range = 3.4f;
            Box("Roof", new Vector3(0, 9.2f, 0), new Vector3(61, 0.4f, 41), Hex("E4E6E8"), true);
            for (var x = -20f; x <= 20; x += 10) Steelwork("Roof beam", new Vector3(x, 8.6f, 0), new Vector3(0.4f, 0.8f, 40));
            for (var x = -20f; x <= 20; x += 20) for (var z = -12f; z <= 12; z += 12) Lamp(new Vector3(x, 8, z), 16, 1.4f, "F4F8FF");
            signs.Add(Board("{greeting}  ·  WELCOME TO {city}", new Vector3(0, 6.5f, 19.7f), 0, new Vector2(16, 1.4f), Brand, Color.white));
            signs.Add(Board("{airport}  ({code})", new Vector3(0, 7.5f, -20.3f), 0, new Vector2(14, 1), Brand, Color.white));
            // Immigration (north, z 8…20): booths and the gate in the partition at z = 8.
            flow.Spawn = Group("Spawn · from the aircraft", new Vector3(-24, 0.1f, 17), 180);
            Board("← FROM GATES", new Vector3(-24, 3, 19.7f), 0, new Vector2(3, 0.5f), Yellow, Hex("1C1F22"));
            Curtain("Immigration partition", new Vector3(-30, 0.05f, 8), new Vector3(30, 0.05f, 8), 4, new Vector2(26, 2.8f));
            flow.ImmigrationGate = SlidingDoor("Immigration gate", new Vector3(-4, 0.05f, 8), 0, 2.6f);
            Paint(Box("Arrival booth", new Vector3(-6.4f, 1.1f, 10), new Vector3(1.6f, 2.2f, 2.4f), Hex("E8EBEE"), true), Hex("E8EBEE"), 0.5f);
            Npc("arrival", "Officer Grant", "Immigration officer", "OfficerJames", new Vector3(-5.0f, 0.05f, 9.6f), 270);
            signs.Add(Board("ARRIVALS  ·  IMMIGRATION  ·  {country}", new Vector3(-4, 5.2f, 8.2f), 180, new Vector2(10, 0.8f), Hex("1C1F22"), Hex("FFC83D")));
            Serpentine("Immigration queue", new Vector3(-10, 0.05f, 16), Vector3.right, 2, 6);
            Npc("traveller", "Ama", "Passenger", "OpenArms", new Vector3(-9, 0.05f, 15), 90);
            // Baggage claim (z −8…8): carousel 2 is a loop the bags ride on.
            var carousel = Group("Carousel 2", new Vector3(0, 0, 0));
            var corners = new[] { new Vector3(-6, 0.75f, -2.5f), new Vector3(6, 0.75f, -2.5f), new Vector3(6, 0.75f, 2.5f), new Vector3(-6, 0.75f, 2.5f) };
            var route = new List<Transform>();
            foreach (var corner in corners) route.Add(Group("Carousel point", corner, 0, carousel));
            Paint(Box("Carousel belt", new Vector3(0, 0.45f, 0), new Vector3(13.4f, 0.4f, 6.4f), Hex("2A2C2E"), true), Hex("2A2C2E"), 0.4f);
            Paint(Box("Carousel centre", new Vector3(0, 0.6f, 0), new Vector3(11, 0.7f, 4), Aluminium, true), Aluminium, 0.6f, 0.85f);
            var conveyor = carousel.gameObject.AddComponent<Conveyor>();
            conveyor.Route = route.ToArray();
            conveyor.Loop = true;
            conveyor.Speed = 0.7f;
            flow.Carousel = conveyor;
            for (var i = 0; i < 9; i++)
            {
                var bag = Box("Other passenger's bag", Vector3.zero, new Vector3(0.45f, 0.28f, 0.68f), Color.HSVToRGB(i * 0.11f % 1, 0.5f, 0.45f), false);
                bag.isStatic = false;
                bag.AddComponent<ConveyorSeed>().Delay = i * 3.2f;
            }
            flow.ClaimBoard = ScreenBoard("Baggage claim board", "BAGGAGE CLAIM", new Vector3(0, 3.6f, 7.6f), 0, new Vector2(5, 2), Hex("FFC83D"));
            Spot("claimboard", "Check the baggage claim screen", new Vector3(0, 1.5f, 5.5f), 3);
            foreach (var x in new[] { -18f, 18f })
            {
                Paint(Box("Carousel (other)", new Vector3(x, 0.45f, 0), new Vector3(7, 0.4f, 4), Hex("2A2C2E"), true), Hex("2A2C2E"), 0.4f);
                Board(x < 0 ? "1" : "3", new Vector3(x, 3, 3), 0, new Vector2(1, 1), Yellow, Hex("1C1F22"));
            }
            Board("2", new Vector3(0, 3, 3.6f), 0, new Vector2(1, 1), Yellow, Hex("1C1F22"));
            Paint(Box("Baggage services desk", new Vector3(24, 0.55f, 0), new Vector3(1, 1.1f, 3.4f), Hex("F4F6F8"), true), Hex("F4F6F8"), 0.55f);
            Board("BAGGAGE SERVICES", new Vector3(24.5f, 2.8f, 0), 90, new Vector2(3.4f, 0.5f), Teal, Color.white);
            Npc("bagservice", "Ms Patel", "Baggage services", "Rose", new Vector3(25.0f, 0.05f, 0), -90);
            // Customs (z −12…−8): green/red channels, gate at z = −12.
            Curtain("Customs partition", new Vector3(-30, 0.05f, -12), new Vector3(30, 0.05f, -12), 4, new Vector2(30, 3));
            flow.CustomsGate = SlidingDoor("Customs exit", new Vector3(0, 0.05f, -12), 0, 2.8f);
            Board("NOTHING TO DECLARE", new Vector3(-3, 3.2f, -11.8f), 180, new Vector2(4, 0.5f), Hex("2E8B57"), Color.white);
            Board("GOODS TO DECLARE", new Vector3(8, 3.2f, -11.8f), 180, new Vector2(4, 0.5f), Hex("C0392B"), Color.white);
            Box("Customs bench", new Vector3(3, 0.45f, -9.5f), new Vector3(3, 0.9f, 0.8f), Steel, true);
            Npc("customs", "Officer Novak", "Customs officer", "Executive", new Vector3(3.2f, 0.05f, -8.6f), 180);
            // Arrival hall (z −20…−12): tourist information and transport board.
            Paint(Box("Tourist information", new Vector3(-16, 0.55f, -16), new Vector3(4, 1.1f, 1), Hex("F4F6F8"), true), Hex("F4F6F8"), 0.55f);
            signs.Add(Board("i  {city} TOURIST INFORMATION", new Vector3(-16, 2.9f, -16.6f), 0, new Vector2(6, 0.5f), Teal, Color.white));
            Npc("tourist", "Leah", "Tourist information officer", "VirtualGuide", new Vector3(-16, 0.05f, -15.2f), 180);
            ScreenBoard("Transport board", "TO THE CITY\nTAXI · TRAIN · BUS", new Vector3(16, 2.6f, -19.6f), 180, new Vector2(4, 2), Color.white);
            Spot("transport", "Read the transport board", new Vector3(16, 1.4f, -18), 3);
            // Taxi rank outside.
            flow.TaxiRank = new Bounds(new Vector3(0, 1, -40), new Vector3(24, 4, 10));
            Board("TAXI", new Vector3(-2, 3, -42), 180, new Vector2(2, 0.6f), Yellow, Hex("1C1F22"));
            for (var i = 0; i < 3; i++) Model("Assets/Art/city/CityTaxi.glb", new Vector3(-10 + i * 7, 0.02f, -47), 90, 1.5f, "Taxi");
            Npc("taxidriver", "Sam", "Taxi driver", "Plaid", new Vector3(-6, 0.05f, -43.5f), 0);
            for (var x = -40f; x <= 40; x += 16) StreetLamp(new Vector3(x, 0.05f, -43.5f), 180);
            Invisible("Forecourt boundary", new Vector3(0, 2, -52), new Vector3(120, 4, 1));
            Invisible("Forecourt west", new Vector3(-50, 2, -36), new Vector3(1, 4, 32));
            Invisible("Forecourt east", new Vector3(50, 2, -36), new Vector3(1, 4, 32));
            // The hotel lobby (destination district, small for now).
            var hotel = new Vector3(200, 0, 0);
            Floor("Hotel lobby floor", hotel, new Vector2(20, 16), Hex("D9CBB5"), 0.7f);
            Wall("Hotel wall", hotel + new Vector3(-10, 0, 8), hotel + new Vector3(10, 0, 8), 5, Hex("EFE6D8"));
            Wall("Hotel wall", hotel + new Vector3(-10, 0, -8), hotel + new Vector3(10, 0, -8), 5, Hex("EFE6D8"));
            Wall("Hotel wall", hotel + new Vector3(-10, 0, -8), hotel + new Vector3(-10, 0, 8), 5, Hex("EFE6D8"));
            Wall("Hotel wall", hotel + new Vector3(10, 0, -8), hotel + new Vector3(10, 0, 8), 5, Hex("EFE6D8"));
            Box("Hotel ceiling", hotel + new Vector3(0, 5.1f, 0), new Vector3(20, 0.2f, 16), Hex("F6F1E8"), true);
            Paint(Box("Reception desk", hotel + new Vector3(0, 0.55f, 4.5f), new Vector3(5, 1.1f, 0.9f), Hex("6D4C32"), true), Hex("6D4C32"), 0.6f);
            signs.Add(Board("{hotel}", hotel + new Vector3(0, 3.4f, 7.8f), 0, new Vector2(8, 0.9f), Hex("2F2A24"), Hex("E8C987")));
            Npc("hotel", "Mr Rahman", "Hotel receptionist", "Secretary", hotel + new Vector3(0, 0.05f, 5.4f), 180);
            for (var i = 0; i < 2; i++) Box("Sofa", hotel + new Vector3(-6 + i * 12, 0.4f, -4), new Vector3(2.4f, 0.8f, 0.9f), Hex("4A6B5C"), true);
            Plant(hotel + new Vector3(-9, 0, 7)); Plant(hotel + new Vector3(9, 0, 7));
            Lamp(hotel + new Vector3(0, 4.6f, 0), 14, 1.6f, "FFE6C2");
            flow.HotelSpawn = Group("Spawn · hotel", hotel + new Vector3(0, 0.1f, -5), 0);
            flow.DestinationSigns = signs.ToArray();
            flow.MapLabels = new List<string> { "Immigration|-4|12", "Carousel 2|0|0", "Baggage services|24|0", "Customs|3|-10", "Tourist info|-16|-16", "Taxi rank|0|-42" };

            var (player, camera) = Player(flow.Spawn.position, 180);
            Finish(player, camera, flow, "Arrival airport", "ArrivalAirport");
            flow.gameObject.AddComponent<AirportAudio>().Ambience = AirportAudio.Mode.Terminal;
            SkyInstaller.Install();
            LifeBuild.Save(scene, ScenePath);
        }
    }
}
