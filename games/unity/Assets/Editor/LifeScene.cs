using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.CafeScene;

namespace ContextSpeak.Editor
{
    // A compact, readable first-day neighborhood. Reuse verified CC0 props/materials.
    public static class LifeScene
    {
        private static readonly Color Cream = Hex("F1E4CC"), Wood = Hex("A27049"), Sage = Hex("64866D"), Clay = Hex("BE7354"), Ink = Hex("294B40");
        public static void Create()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("DCE7D1");
            RenderSettings.ambientEquatorColor = Hex("B3BDA2");
            RenderSettings.ambientGroundColor = Hex("8B765C");
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("D9E5CA");
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;
            Box("Town ground", new Vector3(0, -0.2f, -4), new Vector3(46, 0.35f, 40), Hex("91AA7B"), true);
            Box("Main walking lane", new Vector3(0, 0, -3), new Vector3(5, 0.04f, 33), Hex("D7C7AB"), false);
            foreach (var lane in new[] { 11f, 4f, -7f })
                Box("Neighborhood path", new Vector3(lane == 4 ? 6 : -6, 0.01f, lane), new Vector3(16, 0.04f, 2.5f), Hex("D7C7AB"), false);
            foreach (var x in new[] { -23f, 23f })
            {
                var boundary = Box("Town boundary", new Vector3(x, 1, -4), new Vector3(0.3f, 3, 40), Sage, true);
                boundary.GetComponent<Renderer>().enabled = false;
            }
            foreach (var z in new[] { -24f, 16f })
            {
                var boundary = Box("Town boundary", new Vector3(0, 1, z), new Vector3(46, 3, 0.3f), Sage, true);
                boundary.GetComponent<Renderer>().enabled = false;
            }
            Home();
            Market();
            Cafe();
            foreach (var point in new[] { new Vector3(5, 0, 9), new Vector3(18, 0, -12), new Vector3(-20, 0, 12), new Vector3(4, 0, -17), new Vector3(19, 0, 10), new Vector3(-19, 0, -20) }) Tree(point);
            var sun = new GameObject("Golden afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFE4B3");
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(43, -32, 0);
            RenderSettings.sun = sun;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 90;
            camera.fieldOfView = 55;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("D9E5CA");
            camera.transform.position = new Vector3(-12, 5, 13);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var player = new GameObject("Life player");
            player.transform.position = new Vector3(-12, 0.05f, 8);
            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 0.85f, 0);
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.2f;
            var movement = player.AddComponent<CafePlayer>();
            movement.View = camera;
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(player.transform, false);
            CozyCharacters.DressPlayer(avatar.transform);
            movement.Model = avatar.transform;
            var actors = LifeCatalog.Load().npcs.Select(npc => {
                var actor = NpcCastCatalog.CreateNpc("LifeTown", npc.id, npc.Position);
                actor.name = npc.id;
                return actor.transform;
            }).ToArray();
            var dinner = new GameObject("Family dinner");
            foreach (var x in new[] { -15.2f, -14.7f })
                Model("food/croissant.glb", new Vector3(x, 0.83f, 5), 0, 0.14f, false).transform.SetParent(dinner.transform, true);
            var drink = Model("food/cup-coffee.glb", new Vector3(-6, 0.83f, -11.9f), 0, 0.16f, false);
            var runtime = new GameObject("LifeRuntime").AddComponent<LifeGame>();
            runtime.Player = movement;
            runtime.Npcs = actors;
            runtime.Dinner = dinner;
            runtime.DrinkProp = drink;
            WorldBuild.Configure(movement, actors, "LifeTown");
        }
        private static void Room(string title, Vector3 center, float width, float depth)
        {
            Box(title + " floor", center + new Vector3(0, -0.02f, 0), new Vector3(width, 0.1f, depth), Wood, true);
            Box(title + " back wall", center + new Vector3(0, 1.6f, -depth / 2), new Vector3(width, 3.2f, 0.18f), Cream, true);
            foreach (var x in new[] { -width / 2, width / 2 })
            {
                Box("Cutaway side wall", center + new Vector3(x, 0.6f, 0), new Vector3(0.16f, 1.2f, depth), Cream, true);
                Box("Timber post", center + new Vector3(x, 1.55f, depth / 2), new Vector3(0.24f, 3.1f, 0.24f), Wood, true);
            }
            Box("Terracotta roof cap", center + new Vector3(0, 3.3f, -depth / 2), new Vector3(width + 0.5f, 0.25f, 1), Clay, false);
            Box("Shopfront sign", center + new Vector3(0, 2.7f, -depth / 2 + 0.12f), new Vector3(width * 0.7f, 0.65f, 0.08f), Ink, false);
            Sign(title, center + new Vector3(0, 2.7f, -depth / 2 + 0.2f), 0.8f);
        }
        private static void Home()
        {
            Room("FAMILY HOME", new Vector3(-12, 0, 6), 10, 8);
            Box("Family rug", new Vector3(-12, 0.08f, 6.5f), new Vector3(4.5f, 0.025f, 4), Hex("D4C198"), false);
            Model("furniture/tableRound.glb", new Vector3(-15, 0.05f, 5), 0, 0.8f, true);
            Model("furniture/chairCushion.glb", new Vector3(-16, 0.05f, 5), 90, 0.95f, true);
            Model("furniture/books.glb", new Vector3(-14.9f, 0.87f, 5), 0, 0.18f, false);
            Box("Sage family sofa", new Vector3(-8.5f, 0.4f, 5), new Vector3(1.2f, 0.8f, 2.8f), Sage, true);
            Box("Soft sofa back", new Vector3(-8, 0.9f, 5), new Vector3(0.25f, 0.9f, 2.8f), Sage, true);
            foreach (var z in new[] { 4.2f, 5.2f }) Box("Sofa cushion", new Vector3(-8.7f, 0.9f, z), new Vector3(0.5f, 0.4f, 0.7f), Cream, false);
            Model("furniture/pottedPlant.glb", new Vector3(-16, 0, 2.9f), 0, 1.5f, true);
        }
        private static void Market()
        {
            Room("GREEN MARKET", new Vector3(12, 0, -1), 10, 8);
            Box("Market stand", new Vector3(11, 0.52f, -0.9f), new Vector3(6, 1, 1.1f), Wood, true);
            for (var index = 0; index < 8; index++)
            {
                Box("Striped market awning", new Vector3(8 + index * 0.85f, 3, -0.9f), new Vector3(0.84f, 0.15f, 2.4f), index % 2 == 0 ? Cream : Sage, false);
                Primitive("Fresh apple", PrimitiveType.Sphere, new Vector3(8.8f + index * 0.23f, 1.13f, -0.65f), Vector3.one * 0.22f, Hex("CB654D"), false);
                var carrot = Primitive("Fresh carrot", PrimitiveType.Capsule, new Vector3(11.7f + index * 0.2f, 1.12f, -0.65f), new Vector3(0.1f, 0.21f, 0.1f), Hex("DC994F"), false);
                carrot.transform.rotation = Quaternion.Euler(65, 0, 10);
            }
            Model("furniture/pottedPlant.glb", new Vector3(16, 0, -3.7f), 0, 1.6f, true);
        }
        private static void Cafe()
        {
            Room("FOREST CAFE", new Vector3(-10, 0, -13), 14, 10);
            for (var index = 0; index < 3; index++)
                Model("kaykit/kitchencounter_straight_A.gltf", new Vector3(-14.2f + index * 1.1f, 0, -15.1f), 0, 1, true);
            Model("furniture/kitchenCoffeeMachine.glb", new Vector3(-14.2f, 1.05f, -15.1f), 0, 0.55f, false);
            Model("food/cookie.glb", new Vector3(-12.7f, 1.05f, -14.9f), 0, 0.1f, false);
            Model("furniture/tableRound.glb", new Vector3(-6, 0, -11.9f), 0, 0.8f, true);
            Model("furniture/chairCushion.glb", new Vector3(-6, 0, -13.05f), 0, 0.95f, false);
            Model("furniture/chairCushion.glb", new Vector3(-6, 0, -10.7f), 180, 0.95f, false);
            foreach (var point in new[] { new Vector3(-16, 0, -17), new Vector3(-4, 0, -17) })
                Model("furniture/pottedPlant.glb", point, 0, 1.6f, true);
        }
        private static void Tree(Vector3 point)
        {
            Primitive("Town tree trunk", PrimitiveType.Cylinder, point + Vector3.up * 1.3f, new Vector3(0.55f, 1.3f, 0.55f), Wood, true);
            Primitive("Soft tree canopy", PrimitiveType.Sphere, point + Vector3.up * 3.2f, new Vector3(3.2f, 2.6f, 3), Sage, false);
        }
    }
}
