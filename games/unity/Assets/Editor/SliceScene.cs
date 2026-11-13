using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.CafeScene;

namespace ContextSpeak.Editor
{
    /// Forest Café vertical slice: café + kitchen + terrace, pond with fishing pier, bridge, trail, picnic area,
    /// forest, NPCs (waiter, fishing instructor, customers), weather and lighting. Deterministic, like CafeScene.
    public static class SliceScene
    {
        private static readonly Color Cream = Hex("F3E5CE"), Wood = Hex("AC7449"), DarkWood = Hex("6D4C32"), Leaf = Hex("597B59"), Pine = Hex("3E5E45"),
            Ink = Hex("294238"), Grass = Hex("8DAA6E"), Mud = Hex("6B5A44"), Stone = Hex("A7A39A"), Gravel = Hex("C9BFA8"), Water = Hex("3F6F73"), Glass = Hex("CFE6EA");
        private static System.Random random;

        public static void Create()
        {
            random = new System.Random(20261111);
            EnvironmentArt.Begin();
            BuildingArt.Begin();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("E4E5CD");
            RenderSettings.ambientEquatorColor = Hex("A2B19B");
            RenderSettings.ambientGroundColor = Hex("7C654B");
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("DBE4CD");
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;

            Ground();
            var pond = Pond();
            var seats = new List<Seat>();
            var rootsBeforeCafe = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToHashSet();
            var environmentBeforeCafe = EnvironmentArt.Root.Cast<Transform>().ToHashSet();
            var cafe = Cafe(seats);
            var cafeInterior = new GameObject("Forest Café interior").transform;
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != cafeInterior.gameObject && !rootsBeforeCafe.Contains(item)) item.transform.SetParent(cafeInterior, true);
            foreach (var item in EnvironmentArt.Root.Cast<Transform>().Where(item => !environmentBeforeCafe.Contains(item)).ToArray())
                item.SetParent(cafeInterior, true);
            BuildingArt.RoomSkin("counter", cafeInterior, Vector3.zero, 18, 14);
            Terrace(seats);
            var pier = Pier();
            Bridge();
            Trail();
            Picnic(seats);
            Forest();
            CozyWorldScene.Build();
            BuildingArt.Finish();
            BlenderArt.Lodge();

            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.transform.root.name == "Applied downloaded environment") continue;
                var importedMesh = renderer.GetComponent<MeshFilter>();
                if (importedMesh != null && AssetDatabase.GetAssetPath(importedMesh.sharedMesh).Contains("/WorldAssets/")) continue;
                var name = renderer.gameObject.name.ToLowerInvariant();
                if (name.Contains("wood") || name.Contains("walnut") || name.Contains("roof beam"))
                    renderer.sharedMaterial = CozyMaterials.Wood(name.Contains("beam") ? DarkWood : Hex("B18C68"));
            }

            // Lighting.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFE5B0");
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(43, -38, 0);
            RenderSettings.sun = sun;
            var volume = new GameObject("Warm colour grade").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>().saturation.Override(-8); // cosy, not over-saturated
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.35f);
            AssetDatabase.CreateAsset(profile, Asset("SliceColorGrade.asset"));
            volume.sharedProfile = profile;

            // Camera and player.
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.backgroundColor = Hex("CFE0EA");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 240;
            camera.fieldOfView = 56;
            camera.useOcclusionCulling = true;
            camera.transform.position = new Vector3(0, 4.8f, 16);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            ForestRendering.Configure(camera, sun);
            var playerObject = new GameObject("Player");
            playerObject.transform.position = new Vector3(0, 0.1f, 10.5f);
            var controller = playerObject.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 0.85f, 0);
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 46;
            var player = playerObject.AddComponent<CafePlayer>();
            player.View = camera;
            playerObject.AddComponent<AudioSource>();
            playerObject.AddComponent<FootstepAudio>();
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(playerObject.transform, false);
            CozyCharacters.DressPlayer(avatar.transform);
            player.Model = avatar.transform;
            var hand = new GameObject("Hand").transform;
            hand.SetParent(avatar.transform, false);
            hand.localPosition = new Vector3(0.28f, 1.05f, 0.3f);

            // NPCs.
            var rowan = Walker("Rowan", cafe.counter.position + cafe.counter.forward * -0.1f);
            var waiter = rowan.gameObject.AddComponent<WaiterNpc>();
            waiter.Counter = cafe.counter;
            waiter.Pass = cafe.pass;
            var tray = Box("Serving tray", Vector3.zero, new Vector3(0.45f, 0.03f, 0.32f), DarkWood, false);
            tray.isStatic = false;
            tray.transform.SetParent(rowan, false);
            tray.transform.localPosition = new Vector3(0.3f, 1.1f, 0.25f);
            Model("food/cup-coffee.glb", Vector3.zero, 0, 0.14f, false).transform.SetParent(tray.transform, false);
            waiter.Tray = tray;
            var hazel = Walker("Hazel", pier.stand.position + new Vector3(-1.1f, 0, -0.6f));
            hazel.rotation = Quaternion.Euler(0, 20, 0);
            var hazelRod = Rod("Hazel's rod");
            hazelRod.transform.SetParent(hazel, false);
            hazelRod.transform.localPosition = new Vector3(0.25f, 1f, 0.2f);
            hazelRod.transform.localRotation = Quaternion.Euler(-40, 0, 0);

            var customers = new List<CafeCustomer>();
            var door = new GameObject("Café door").transform;
            door.position = new Vector3(0, 0, 9.5f);
            for (var index = 0; index < 6; index++)
            {
                var walker = Walker("Customer " + (index + 1), door.position + new Vector3(index - 2.5f, 0, 2));
                var customer = walker.gameObject.AddComponent<CafeCustomer>();
                customer.Door = door;
                customer.Counter = cafe.counter;
                customer.Seats = seats.Where(seat => seat.Table != "T1").ToArray(); // T1 stays free for the player
                customer.Speed = 1.3f + index * 0.08f;
                var cup = Model("food/cup-tea.glb", Vector3.zero, 0, 0.13f, false);
                cup.transform.SetParent(walker, false);
                cup.transform.localPosition = new Vector3(0.25f, 0.85f, 0.35f);
                customer.Cup = cup;
                customers.Add(customer);
            }
            for (var index = 0; index + 1 < customers.Count; index += 2) { customers[index].Friend = customers[index + 1]; customers[index + 1].Friend = customers[index]; }

            // Atmosphere.
            var atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            atmosphere.Sun = sun;
            atmosphere.WarmLights = cafe.lights;
            atmosphere.Pond = pond;
            atmosphere.Rain = Particles("Rain", new Vector3(0, 18, 10), new Vector3(70, 1, 70), Hex("C8D6E0"), 1400, 0.03f, new Vector3(0, -16, 0), 1.4f, true);
            atmosphere.Butterflies = Particles("Butterflies", new Vector3(-10, 1.2f, 22), new Vector3(16, 1.5f, 10), Hex("F2C14E"), 12, 0.12f, new Vector3(0, 0.1f, 0), 8, false);
            atmosphere.Fireflies = Particles("Fireflies", new Vector3(0, 1.4f, 20), new Vector3(30, 2, 20), Hex("FFF3A6"), 40, 0.06f, new Vector3(0, 0.05f, 0), 6, false);
            var birds = new GameObject("Birds").transform;
            birds.position = new Vector3(0, 0, 18);
            atmosphere.Birds = Enumerable.Range(0, 5).Select(index =>
            {
                var bird = Primitive("Bird", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.12f, 0.18f), Ink, false);
                bird.isStatic = false;
                bird.transform.SetParent(birds, false);
                return bird.transform;
            }).ToArray();
            for (var index = 0; index < 7; index++)
            {
                var fish = Primitive("Pond fish", PrimitiveType.Capsule, new Vector3(0, -0.75f, 20), new Vector3(0.12f, 0.12f, 0.36f), index == 0 ? Hex("E2A23B") : Hex("C46A3A"), false);
                fish.isStatic = false;
                fish.transform.rotation = Quaternion.Euler(90, 0, 0);
                var swim = fish.AddComponent<PondFish>();
                swim.Centre = new Vector3(0, -0.75f, 20);
                swim.Radius = new Vector2(7, 4.5f);
                swim.Phase = index * 0.9f;
                swim.Speed = 0.18f + index * 0.03f;
            }

            // Props for deliveries (templates, hidden).
            var drinks = new[] { "food/cup-coffee.glb", "food/cup-tea.glb" }.Select(file => Template(file, 0.15f)).ToArray();
            var food = new[] { "food/croissant.glb", "food/cookie.glb" }.Select(file => Template(file, 0.1f)).ToArray();

            var runtime = new GameObject("ForestCafeSlice").AddComponent<SliceGame>();
            runtime.Player = player;
            runtime.Rowan = rowan;
            runtime.Hazel = hazel;
            runtime.Counter = cafe.counter;
            runtime.Waiter = waiter;
            runtime.Seats = seats.ToArray();
            runtime.Pier = pier.spot;
            runtime.Atmosphere = atmosphere;
            runtime.DrinkProps = drinks;
            runtime.FoodProps = food;
            runtime.PlayerHand = hand;

            WorldBuild.Configure(player, new[] { rowan, hazel }, "ForestSlice");
            WorldAssetBuilder.Place();
            WorldBuild.BakeNavigation("ForestSlice");
            WorldAssetBuilder.ResolveApproaches();
            foreach (var resident in UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None))
                resident.GetComponent<CapsuleCollider>().enabled = true;
            // Solid bodies for the player to bump into, added after the NavMesh bake so they don't cut holes in it.
            foreach (var npc in new[] { rowan, hazel }.Concat(customers.Select(customer => customer.transform)))
            {
                var capsule = npc.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 0.85f, 0);
                capsule.height = 1.7f;
                capsule.radius = 0.28f;
            }
            try { StaticOcclusionCulling.Compute(); }
            catch (Exception error) { Debug.LogWarning("Occlusion culling bake skipped: " + error.Message); }
        }

        // ------------------------------------------------------------------ terrain and water
        private static void Ground()
        {
            // A frame of ground boxes leaves a hole for the pond basin (x -9..9, z 14..26).
            EnvironmentArt.Surface(Box("Meadow ground south", new Vector3(0, -0.25f, -3), new Vector3(60, 0.5f, 34), Grass, true));
            EnvironmentArt.Surface(Box("Meadow ground north", new Vector3(0, -0.25f, 33), new Vector3(60, 0.5f, 14), Grass, true));
            EnvironmentArt.Surface(Box("Meadow ground west", new Vector3(-19.5f, -0.25f, 20), new Vector3(21, 0.5f, 12), Grass, true));
            EnvironmentArt.Surface(Box("Meadow ground east", new Vector3(19.5f, -0.25f, 20), new Vector3(21, 0.5f, 12), Grass, true));
        }

        private static Renderer Pond()
        {
            Box("Pond bed mud", new Vector3(0, -1.7f, 20), new Vector3(18, 0.2f, 12), Mud, true);
            Box("Pond bank south stone", new Vector3(0, -0.8f, 14.05f), new Vector3(18, 1.6f, 0.1f), Stone, false);
            Box("Pond bank north stone", new Vector3(0, -0.8f, 25.95f), new Vector3(18, 1.6f, 0.1f), Stone, false);
            Box("Pond bank west stone", new Vector3(-8.95f, -0.8f, 20), new Vector3(0.1f, 1.6f, 12), Stone, false);
            Box("Pond bank east stone", new Vector3(8.95f, -0.8f, 20), new Vector3(0.1f, 1.6f, 12), Stone, false);
            var water = Primitive("Pond water", PrimitiveType.Plane, new Vector3(0, -0.35f, 20), new Vector3(1.8f, 1, 1.2f), Water, false);
            var waterMaterial = new Material(Shader.Find("ContextSpeak/Forest Water"));
            waterMaterial.SetColor("_BaseColor", Water);
            waterMaterial.SetFloat("_UseDepthFade", 1);
            waterMaterial.EnableKeyword("_WATER_DEPTH_FADE");
            AssetDatabase.CreateAsset(waterMaterial, Asset("ForestWater.mat"));
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            // Invisible boundary keeps the player out of the water, with gaps for the pier and the bridge.
            foreach (var (from, to) in new[] { (-9f, -6.9f), (-5.1f, 4.1f), (5.9f, 9f) })
                Invisible("Pond edge south", new Vector3((from + to) / 2, 0.6f, 13.9f), new Vector3(to - from, 1.2f, 0.2f));
            foreach (var (from, to) in new[] { (-9f, -6.9f), (-5.1f, 9f) })
                Invisible("Pond edge north", new Vector3((from + to) / 2, 0.6f, 26.1f), new Vector3(to - from, 1.2f, 0.2f));
            Invisible("Pond edge west", new Vector3(-9.1f, 0.6f, 20), new Vector3(0.2f, 1.2f, 12.4f));
            Invisible("Pond edge east", new Vector3(9.1f, 0.6f, 20), new Vector3(0.2f, 1.2f, 12.4f));
            // Reeds, rocks and lily pads soften the rectangular basin.
            foreach (var corner in new[] { new Vector3(-8.2f, 0, 14.8f), new Vector3(8.2f, 0, 14.8f), new Vector3(-8.2f, 0, 25.2f), new Vector3(8.2f, 0, 25.2f), new Vector3(-8.4f, 0, 20), new Vector3(8.4f, 0, 22) })
            {
                for (var i = 0; i < 9; i++)
                {
                    var offset = new Vector3((float)random.NextDouble() * 1.6f - 0.8f, 0, (float)random.NextDouble() * 1.6f - 0.8f);
                    var reed = Primitive("Reed", PrimitiveType.Cylinder, corner + offset + Vector3.up * 0.1f, new Vector3(0.05f, 0.5f + (float)random.NextDouble() * 0.4f, 0.05f), Color.Lerp(Leaf, Grass, 0.4f), false);
                    reed.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 10 - 5, 0, (float)random.NextDouble() * 10 - 5);
                }
                Primitive("Bank rock", PrimitiveType.Sphere, corner + new Vector3(0.6f, -0.1f, 0.4f), new Vector3(1.1f, 0.6f, 0.9f), Stone, false);
            }
            for (var i = 0; i < 10; i++)
                Primitive("Lily pad", PrimitiveType.Cylinder, new Vector3(-6 + (float)random.NextDouble() * 12, -0.32f, 16 + (float)random.NextDouble() * 9), new Vector3(0.6f, 0.01f, 0.6f), Leaf, false);
            return water.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------ café and kitchen
        private static (Transform counter, Transform pass, Light[] lights) Cafe(List<Seat> seats)
        {
            Box("Oak floor wood", new Vector3(0, 0.02f, 0), new Vector3(18, 0.06f, 14), Wood, true);
            Box("Back wall", new Vector3(-2.5f, 1.8f, -7), new Vector3(13, 3.6f, 0.2f), Cream, true);
            Box("Back wall right", new Vector3(8, 1.8f, -7), new Vector3(2, 3.6f, 0.2f), Cream, true);
            Box("Kitchen pass header", new Vector3(5.5f, 3.1f, -7), new Vector3(3, 1, 0.2f), Cream, true);
            Box("Kitchen pass counter", new Vector3(5.5f, 0.55f, -7), new Vector3(3, 1.1f, 0.4f), DarkWood, true);
            // Glass walls with timber mullions on both sides; a front wall with a wide door.
            foreach (var x in new[] { -9f, 9f })
            {
                Box("Lower wall", new Vector3(x, 0.35f, 0), new Vector3(0.24f, 0.7f, 14), Wood, true);
                var pane = Box("Glass wall", new Vector3(x, 2.05f, 0), new Vector3(0.06f, 2.7f, 14), Glass, true);
                pane.GetComponent<Renderer>().sharedMaterial = Transparent("Glass", Glass, 0.25f, 0.95f);
                Box("Window header", new Vector3(x, 3.5f, 0), new Vector3(0.26f, 0.24f, 14), Wood, true);
                for (var z = -6; z <= 6; z += 3) Box("Window mullion", new Vector3(x, 2, z), new Vector3(0.2f, 3, 0.16f), Wood, true);
            }
            foreach (var (from, to) in new[] { (-9f, -1.6f), (1.6f, 9f) })
            {
                var pane = Box("Front glass", new Vector3((from + to) / 2, 1.8f, 7), new Vector3(to - from, 3.6f, 0.06f), Glass, true);
                pane.GetComponent<Renderer>().sharedMaterial = Transparent("Glass", Glass, 0.25f, 0.95f);
                Box("Front sill", new Vector3((from + to) / 2, 0.3f, 7), new Vector3(to - from, 0.6f, 0.24f), Wood, true);
            }
            Box("Door frame left", new Vector3(-1.7f, 1.6f, 7), new Vector3(0.2f, 3.2f, 0.3f), DarkWood, true);
            Box("Door frame right", new Vector3(1.7f, 1.6f, 7), new Vector3(0.2f, 3.2f, 0.3f), DarkWood, true);
            Box("Door lintel", new Vector3(0, 3.3f, 7), new Vector3(3.6f, 0.4f, 0.3f), DarkWood, false);
            // Roof beams (open timber ceiling).
            for (var z = -6; z <= 6; z += 2) Box("Roof beam", new Vector3(0, 3.7f, z), new Vector3(18.4f, 0.22f, 0.26f), DarkWood, false);
            foreach (var side in new[] { -1, 1 })
            {
                var roof = Box("Greenhouse roof glass", new Vector3(side * 4.6f, 5.05f, 0), new Vector3(9.7f, 0.06f, 14.6f), Glass, false);
                roof.transform.rotation = Quaternion.Euler(0, 0, -side * 16.3f);
                roof.GetComponent<Renderer>().sharedMaterial = Transparent("Greenhouse glazing", Glass, 0.16f, 0.9f);
            }

            // Counter, barista station, coffee machines and bakery display.
            for (var index = 0; index < 3; index++) Model("kaykit/kitchencounter_straight_A.gltf", new Vector3(2.7f + index * 1.2f, 0, -4.2f), 0, 1.05f, true);
            Box("Walnut counter top", new Vector3(3.9f, 1.1f, -4.2f), new Vector3(3.9f, 0.14f, 1.4f), Wood, false);
            Model("furniture/kitchenCoffeeMachine.glb", new Vector3(4.9f, 1.18f, -4.4f), 0, 0.55f, false);
            Model("furniture/kitchenCoffeeMachine.glb", new Vector3(3.7f, 1.18f, -4.4f), 0, 0.55f, false);
            Model("kaykit/kitchencounter_sink.gltf", new Vector3(6.6f, 0, -6.2f), 180, 1.05f, true);
            Model("kaykit/dishrack_plates.gltf", new Vector3(6.6f, 1.08f, -6.2f), 180, 0.3f, false);
            var display = Box("Bakery display glass", new Vector3(1.4f, 1.1f, -4.2f), new Vector3(1.4f, 0.6f, 1), Glass, false);
            display.GetComponent<Renderer>().sharedMaterial = Transparent("Glass", Glass, 0.3f, 0.95f);
            Box("Bakery display base", new Vector3(1.4f, 0.4f, -4.2f), new Vector3(1.4f, 0.8f, 1), Wood, true);
            foreach (var x in new[] { 1.0f, 1.4f, 1.8f })
            {
                Model("food/croissant.glb", new Vector3(x, 0.85f, -4.05f), 0, 0.1f, false);
                Model("food/cookie.glb", new Vector3(x, 0.85f, -4.4f), 0, 0.06f, false);
            }
            foreach (var x in new[] { 2.7f, 3.0f, 3.3f }) Model("kaykit/jar_B_small.gltf", new Vector3(x, 1.18f, -4.55f), 0, 0.22f, false);
            Box("Menu board", new Vector3(3.6f, 2.4f, -6.85f), new Vector3(6, 1.6f, 0.12f), Ink, false);
            Sign("FOREST CAFÉ", new Vector3(3.6f, 2.95f, -6.72f), 0.2f);
            Sign("Coffee · Tea · Smoothies · Juice · Mocktails\nBreakfast · Lunch · Dinner · Desserts · Snacks\nAsk Rowan for the full menu", new Vector3(3.6f, 2.25f, -6.72f), 0.085f);

            // Kitchen behind the pass.
            Box("Kitchen floor stone", new Vector3(4.5f, 0.02f, -9.5f), new Vector3(9, 0.06f, 5), Stone, true);
            Box("Kitchen back wall", new Vector3(4.5f, 1.8f, -12), new Vector3(9, 3.6f, 0.2f), Cream, true);
            Box("Kitchen side wall", new Vector3(0, 1.8f, -9.5f), new Vector3(0.2f, 3.6f, 5), Cream, true);
            Box("Kitchen side wall east", new Vector3(9, 1.8f, -9.5f), new Vector3(0.2f, 3.6f, 5), Cream, true);
            Box("Kitchen roof", new Vector3(4.5f, 3.7f, -9.5f), new Vector3(9.4f, 0.2f, 5.4f), DarkWood, false);
            Box("Stove", new Vector3(2.5f, 0.5f, -11.3f), new Vector3(2.2f, 1, 1), Stone, true);
            Box("Stove top", new Vector3(2.5f, 1.02f, -11.3f), new Vector3(2.2f, 0.04f, 1), Ink, false);
            foreach (var x in new[] { 1.9f, 3.1f }) Primitive("Pot", PrimitiveType.Cylinder, new Vector3(x, 1.2f, -11.3f), new Vector3(0.5f, 0.17f, 0.5f), Stone, false);
            Box("Prep counter", new Vector3(6, 0.5f, -11.3f), new Vector3(3, 1, 1), Wood, true);
            Model("kaykit/jar_A_medium.gltf", new Vector3(5.4f, 1, -11.3f), 0, 0.35f, false);
            var chef = NpcCastCatalog.Create("ForestSlice", "Chef Theo", new Vector3(4, 0.04f, -10.4f), 180);
            chef.name = "Chef Theo";

            // Indoor seating: T1 (window table, kept for the player) to T4.
            Table(seats, "T1", new Vector3(-6, 0, 3.5f), false);
            Table(seats, "T2", new Vector3(-6, 0, -2.5f), false);
            Table(seats, "T3", new Vector3(-1.5f, 0, 1.5f), false);
            Table(seats, "T4", new Vector3(5.5f, 0, 3.5f), false);
            foreach (var position in new[] { new Vector3(-8.2f, 0, -6.2f), new Vector3(8.2f, 0, -6.2f), new Vector3(-8.2f, 0, 6.2f), new Vector3(8.2f, 0, 6.2f) })
                Model("furniture/pottedPlant.glb", position, 0, 1.6f, true);
            Model("furniture/books.glb", new Vector3(-8.5f, 0.75f, 0), 90, 0.3f, false);
            Model("furniture/lampRoundFloor.glb", new Vector3(-8.3f, 0, 1.2f), 0, 1.6f, true);
            IndoorTree();

            var lights = new List<Light>();
            foreach (var position in new[] { new Vector3(-6, 3.2f, 3.5f), new Vector3(-6, 3.2f, -2.5f), new Vector3(-1.5f, 3.2f, 1.5f), new Vector3(5.5f, 3.2f, 3.5f), new Vector3(3.9f, 3.2f, -4.2f), new Vector3(4.5f, 3.3f, -9.5f) })
            {
                Primitive("Pendant shade", PrimitiveType.Sphere, position, new Vector3(0.7f, 0.3f, 0.7f), Ink, false);
                var light = new GameObject("Warm café light").AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Hex("FFD596");
                light.intensity = 1.8f;
                light.range = 7;
                light.shadows = LightShadows.None;
                light.transform.position = position - Vector3.up * 0.25f;
                lights.Add(light);
            }

            var counter = new GameObject("Counter service point").transform;
            counter.position = new Vector3(3.9f, 0.05f, -5.2f);
            counter.rotation = Quaternion.identity; // forward faces the customers (+z)
            var pass = new GameObject("Kitchen pass pickup").transform;
            pass.position = new Vector3(5.5f, 0.05f, -6.3f);
            Interact("menu-board", "Read the menu", new Vector3(3.6f, 1, -5.6f));
            return (counter, pass, lights.ToArray());
        }

        private static void Terrace(List<Seat> seats)
        {
            Box("Terrace deck wood", new Vector3(0, 0.05f, 9.5f), new Vector3(18, 0.1f, 5), Wood, true);
            foreach (var x in new[] { -9f, 9f }) Box("Terrace railing", new Vector3(x, 0.55f, 9.5f), new Vector3(0.12f, 0.9f, 5), DarkWood, true);
            // Gaps lead to the bridge (x -6), the lawn path (x 0) and the pier (x 5).
            foreach (var (from, to) in new[] { (-9f, -7.1f), (-4.9f, -1.5f), (1.5f, 4f), (6f, 9f) })
                Box("Terrace railing north", new Vector3((from + to) / 2, 0.55f, 12), new Vector3(to - from, 0.9f, 0.12f), DarkWood, true);
            Table(seats, "T5", new Vector3(-6, 0.1f, 9.6f), true);
            Table(seats, "T6", new Vector3(-2.6f, 0.1f, 9.6f), true);
            Table(seats, "T7", new Vector3(6.6f, 0.1f, 9.6f), true);
            foreach (var x in new[] { -8.5f, -3f, 3.2f, 8.5f })
            {
                Box("Lantern post", new Vector3(x, 1.1f, 11.8f), new Vector3(0.1f, 2.2f, 0.1f), DarkWood, false);
                var lantern = new GameObject("Terrace lantern").AddComponent<Light>();
                lantern.type = LightType.Point;
                lantern.color = Hex("FFC774");
                lantern.intensity = 1.2f;
                lantern.range = 5;
                lantern.transform.position = new Vector3(x, 2.25f, 11.8f);
                Primitive("Lantern glass", PrimitiveType.Sphere, lantern.transform.position, Vector3.one * 0.22f, Hex("FFE7B0"), false);
            }
        }

        private static (Transform stand, FishingSpot spot) Pier()
        {
            const float x = 5;
            Box("Pier planks wood", new Vector3(x, 0.12f, 15.1f), new Vector3(1.6f, 0.12f, 6.2f), Wood, true);
            for (var z = 13.5f; z <= 18; z += 1.5f)
                foreach (var side in new[] { -0.75f, 0.75f }) Primitive("Pier post", PrimitiveType.Cylinder, new Vector3(x + side, -0.6f, z), new Vector3(0.14f, 0.8f, 0.14f), DarkWood, false);
            foreach (var side in new[] { -0.8f, 0.8f }) Box("Pier rail", new Vector3(x + side, 0.65f, 16), new Vector3(0.08f, 0.9f, 4), DarkWood, true);
            Box("Pier end rail", new Vector3(x, 0.65f, 18.15f), new Vector3(1.6f, 0.9f, 0.08f), DarkWood, true);
            Box("Bait bucket", new Vector3(x + 0.5f, 0.35f, 17.4f), new Vector3(0.3f, 0.35f, 0.3f), Hex("4E7C9A"), false);
            var stand = new GameObject("Fishing stand").transform;
            stand.position = new Vector3(x, 0.25f, 17.4f);
            var target = new GameObject("Float landing").transform;
            target.position = new Vector3(x - 0.5f, -0.33f, 21.5f);
            var spot = new GameObject("Fishing spot").AddComponent<FishingSpot>();
            spot.LineMaterial = Transparent("Fishing line", Color.white, 0.9f, 0.1f, true);
            spot.Stand = stand;
            spot.WaterTarget = target;
            spot.Rod = Rod("Player rod");
            spot.RodTip = spot.Rod.transform.Find("Rod tip");
            Sign("FISHING PIER\nAsk Hazel for tips", new Vector3(x - 1.2f, 1.4f, 12.6f), 0.08f);
            Box("Pier sign post", new Vector3(x - 1.2f, 0.7f, 12.65f), new Vector3(0.1f, 1.4f, 0.1f), DarkWood, false);
            return (stand, spot);
        }

        private static GameObject Rod(string name)
        {
            var rod = new GameObject(name);
            var pole = Primitive("Rod pole", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.03f, 1.2f, 0.03f), DarkWood, false);
            pole.isStatic = false;
            pole.transform.SetParent(rod.transform, false);
            pole.transform.localPosition = new Vector3(0, 0, 1.1f);
            pole.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var reel = Primitive("Reel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.03f, 0.1f), Stone, false);
            reel.isStatic = false;
            reel.transform.SetParent(rod.transform, false);
            reel.transform.localPosition = new Vector3(0, -0.06f, 0.15f);
            reel.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var tip = new GameObject("Rod tip").transform;
            tip.SetParent(rod.transform, false);
            tip.localPosition = new Vector3(0, 0, 2.3f);
            return rod;
        }

        private static void Bridge()
        {
            const float x = -6;
            // Arched footbridge across the west of the pond: ramp up, span, ramp down.
            Ramp("Bridge ramp south wood", new Vector3(x, 0.05f, 12.2f), new Vector3(x, 0.6f, 15.4f));
            Box("Bridge span wood", new Vector3(x, 0.6f, 20), new Vector3(1.8f, 0.12f, 9.2f), Wood, true);
            Ramp("Bridge ramp north wood", new Vector3(x, 0.6f, 24.6f), new Vector3(x, 0, 27.4f));
            foreach (var side in new[] { -0.9f, 0.9f })
            {
                Box("Bridge rail", new Vector3(x + side, 1.2f, 20), new Vector3(0.08f, 1, 9.2f), DarkWood, true);
                for (var z = 15.5f; z <= 24.5f; z += 1.5f) Box("Bridge baluster", new Vector3(x + side, 1, z), new Vector3(0.1f, 0.8f, 0.1f), DarkWood, false);
            }
        }

        private static void Ramp(string name, Vector3 from, Vector3 to)
        {
            var ramp = Box(name, (from + to) / 2 + Vector3.up * 0.06f, new Vector3(1.8f, 0.12f, Vector3.Distance(from, to)), Wood, true);
            ramp.transform.rotation = Quaternion.LookRotation(to - from);
        }

        private static void Trail()
        {
            // Gravel nature trail around the pond, linking terrace, pier, bridge and picnic area.
            var points = new[] { new Vector3(-2, 0, 13), new Vector3(-10.5f, 0, 13), new Vector3(-11, 0, 20), new Vector3(-10.5f, 0, 27.5f), new Vector3(0, 0, 28), new Vector3(10.5f, 0, 27.5f), new Vector3(11, 0, 20), new Vector3(10.5f, 0, 13), new Vector3(6, 0, 13) };
            for (var index = 0; index + 1 < points.Length; index++)
            {
                var from = points[index];
                var to = points[index + 1];
                var segment = Box("Gravel trail", (from + to) / 2 + Vector3.up * 0.015f, new Vector3(1.6f, 0.03f, Vector3.Distance(from, to) + 1.4f), Gravel, false);
                segment.transform.rotation = Quaternion.LookRotation(to - from);
            }
            Sign("NATURE TRAIL →", new Vector3(-10.5f, 1.3f, 12.6f), 0.08f);
        }

        private static void Picnic(List<Seat> seats)
        {
            foreach (var (centre, id) in new[] { (new Vector3(-15, 0, 23), "P1"), (new Vector3(-15, 0, 29), "P2"), (new Vector3(15, 0, 26), "P3") })
            {
                Box("Picnic table wood", centre + Vector3.up * 0.75f, new Vector3(2, 0.08f, 0.9f), Wood, true);
                foreach (var side in new[] { -0.8f, 0.8f })
                {
                    Box("Picnic bench wood", centre + new Vector3(0, 0.42f, side), new Vector3(2, 0.06f, 0.35f), Wood, true);
                    var seat = new GameObject("Seat " + id).AddComponent<Seat>();
                    seat.Table = id;
                    seat.Outdoor = true;
                    seat.transform.position = centre + new Vector3(0, 0.05f, side * 1.35f);
                    var top = new GameObject("Table top").transform;
                    top.position = centre + Vector3.up * 0.8f;
                    seat.TableTop = top;
                    seats.Add(seat);
                }
                for (var leg = -1; leg <= 1; leg += 2) Box("Picnic leg", centre + new Vector3(leg * 0.8f, 0.37f, 0), new Vector3(0.1f, 0.75f, 1.8f), DarkWood, false);
            }
            Box("Picnic blanket", new Vector3(-12, 0.02f, 31), new Vector3(2.4f, 0.02f, 1.8f), Hex("C46A3A"), false);
            Model("food/croissant.glb", new Vector3(-12, 0.05f, 31), 0, 0.1f, false);
        }

        private static void Forest()
        {
            // Pines and broadleaf trees ring the clearing. LOD groups cull distant trees.
            for (var index = 0; index < 64; index++)
            {
                var angle = index / 64f * Mathf.PI * 2 + (float)random.NextDouble() * 0.1f;
                var radius = 27 + (float)random.NextDouble() * 9;
                var position = new Vector3(Mathf.Cos(angle) * radius * 0.9f, 0, 9 + Mathf.Sin(angle) * radius);
                if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(-21, -9)) < 6) continue;
                if (Mathf.Abs(position.z + 16) < 3 || Mathf.Abs(position.z - 33) < 3 || Mathf.Abs(position.x) < 3) continue;
                if (position.z < -12 && Mathf.Abs(position.x) < 6) continue; // keep the view behind the kitchen open
                Tree(position, index % 3 == 0);
            }
            foreach (var position in new[] { new Vector3(-13, 0, 16), new Vector3(13, 0, 15), new Vector3(-14, 0, 5), new Vector3(14, 0, 4), new Vector3(-12.5f, 0, -5), new Vector3(12.5f, 0, -6) })
                Tree(position, false);
            for (var index = 0; index < 40; index++)
            {
                var position = new Vector3((float)random.NextDouble() * 50 - 25, 0, (float)random.NextDouble() * 50 - 15);
                if (Mathf.Abs(position.x) < 10 && position.z > -8 && position.z < 27) continue;
                EnvironmentArt.GrassPatch(position, index * 47);
            }
        }

        private static void Tree(Vector3 position, bool pine)
        {
            var height = 4 + (float)random.NextDouble() * 3;
            EnvironmentArt.Tree(position, height, !pine && Mathf.Abs(Mathf.RoundToInt(position.x + position.z)) % 9 == 0);
        }

        private static void IndoorTree()
        {
            Primitive("Tree planter", PrimitiveType.Cylinder, new Vector3(-1.5f, 0.2f, -3.8f), new Vector3(1.8f, 0.2f, 1.8f), Ink, true);
            EnvironmentArt.IndoorTree(new Vector3(-1.5f, 0.2f, -3.8f), 4.5f);
        }

        // ------------------------------------------------------------------ helpers
        private static void Table(List<Seat> seats, string id, Vector3 position, bool outdoor)
        {
            EnvironmentArt.Furniture("World930940d8f6", position, 0.78f, 0, true);
            var top = new GameObject("Table top " + id).transform;
            top.position = position + Vector3.up * 0.8f;
            foreach (var side in new[] { 1, -1 })
            {
                var chairPosition = position + new Vector3(0, 0, side * 1.15f);
                EnvironmentArt.Furniture("Worlddd317d3e66", chairPosition, 0.9f, side > 0 ? 180 : 0, false);
                var seat = new GameObject("Seat " + id).AddComponent<Seat>();
                seat.Table = id;
                seat.Outdoor = outdoor;
                seat.TableTop = top;
                seat.transform.position = chairPosition + Vector3.up * 0.05f;
                seat.transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, -side));
                seats.Add(seat);
            }
        }

        private static Transform Walker(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var body = NpcCastCatalog.Create("ForestSlice", name, position);
            body.transform.SetParent(root.transform, true);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.gameObject.isStatic = false;
            body.isStatic = false;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false; // the surface registers on scene enable; walkers attach at Start
            agent.radius = 0.3f;
            agent.height = 1.7f;
            agent.speed = 1.6f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            return root.transform;
        }

        private static GameObject Template(string file, float height)
        {
            var item = Model(file, new Vector3(0, -50, 0), 0, height, false);
            item.isStatic = false;
            item.SetActive(false);
            return item;
        }

        private static void Invisible(string name, Vector3 position, Vector3 size)
        {
            var wall = Box(name, position, size, Stone, true);
            wall.GetComponent<Renderer>().enabled = false;
        }

        private static void Interact(string id, string prompt, Vector3 position)
        {
            var point = new GameObject("Interact · " + prompt).AddComponent<Interactable>();
            point.Id = id;
            point.Prompt = prompt;
            point.transform.position = position;
        }

        private static ParticleSystem Particles(string name, Vector3 position, Vector3 area, Color color, int rate, float size, Vector3 velocity, float lifetime, bool stretch)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.position = position;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.startColor = color;
            main.startSize = size;
            main.startLifetime = lifetime;
            main.startSpeed = 0;
            main.maxParticles = rate * 3;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = system.emission;
            emission.rateOverTime = rate;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            var motion = system.velocityOverLifetime;
            motion.enabled = true;
            motion.space = ParticleSystemSimulationSpace.World;
            motion.x = new ParticleSystem.MinMaxCurve(-Mathf.Abs(velocity.y) * 0.05f - 0.2f, Mathf.Abs(velocity.y) * 0.05f + 0.2f);
            motion.y = new ParticleSystem.MinMaxCurve(velocity.y, velocity.y * 0.8f + 0.05f);
            motion.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            if (!stretch)
            {
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.4f;
            }
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 4;
            renderer.sharedMaterial = Transparent("Particle " + name, color, 0.75f, 0.1f, true);
            return system;
        }

        private static readonly Dictionary<string, Material> Transparents = new Dictionary<string, Material>();
        private static Material Transparent(string name, Color color, float alpha, float smoothness, bool unlit = false)
        {
            if (Transparents.TryGetValue(name, out var cached) && cached != null) return cached;
            var path = Asset("Slice-" + name.Replace(' ', '-') + ".mat");
            var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit"));
            color.a = alpha;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1); // transparent
            material.SetFloat("_Blend", 0);
            material.SetFloat("_Smoothness", smoothness);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            Transparents[name] = material;
            return material;
        }

        private static string Asset(string name)
        {
            var path = "Assets/Generated/" + name;
            AssetDatabase.DeleteAsset(path);
            return path;
        }
    }
}
