using System;
using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    // Deterministic scene composition; generated geometry and imported art are rebuilt together.
    public static class CafeScene
    {
        private static readonly Color Cream = Hex("F3E5CE"), Wood = Hex("AC7449"), Green = Hex("597B59"), Ink = Hex("294238");
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public static void Create()
        {
            Materials.Clear();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("E4E5CD");
            RenderSettings.ambientEquatorColor = Hex("A2B19B");
            RenderSettings.ambientGroundColor = Hex("7C654B");
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("DBE4CD");
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            Box("Floor foundation", new Vector3(0, -0.18f, 0), new Vector3(18, 0.3f, 14), Wood, true);
            for (var index = 0; index < 24; index++)
                Box("Oak floor", new Vector3(-8.6f + index * 0.75f, 0, 0), new Vector3(0.73f, 0.045f, 14), Color.Lerp(Wood, Cream, (index % 3) * 0.06f), false);
            var entrance = Box("Entrance boundary", new Vector3(0, 1.5f, 7.2f), new Vector3(18, 3, 0.2f), Cream, true);
            entrance.GetComponent<Renderer>().enabled = false;
            Box("Back wall", new Vector3(0, 1.8f, -7), new Vector3(18, 3.6f, 0.2f), Cream, true);
            foreach (var x in new[] { -9f, 9f })
            {
                Box("Lower wall", new Vector3(x, 0.5f, 0), new Vector3(0.2f, 1, 14), Cream, true);
                Box("Window header", new Vector3(x, 3.4f, 0), new Vector3(0.24f, 0.26f, 14), Wood, true);
                for (var z = -6; z <= 6; z += 3)
                    Box("Window mullion", new Vector3(x, 2, z), new Vector3(0.22f, 3, 0.18f), Wood, true);
                for (var z = -5; z < 7; z += 3)
                {
                    var bush = Primitive("Garden foliage", PrimitiveType.Sphere, new Vector3(x * 1.23f, 1, z), new Vector3(2.8f, 2.1f, 2.3f), Color.Lerp(Green, Cream, 0.15f), false);
                    bush.isStatic = true;
                }
            }
            Box("Back timber beam", new Vector3(0, 3.35f, -6.7f), new Vector3(18, 0.28f, 0.32f), Wood, false);
            Box("Welcome mat", new Vector3(0, 0.05f, 5), new Vector3(2.6f, 0.04f, 1.6f), Green, false);
            for (var index = 0; index < 3; index++)
                Model("kaykit/kitchencounter_straight_A.gltf", new Vector3(2.7f + index * 1.2f, 0, -4.2f), 0, 1.05f, true);
            Box("Walnut counter top", new Vector3(3.9f, 1.1f, -4.2f), new Vector3(3.9f, 0.14f, 1.4f), Wood, false);
            Model("furniture/kitchenCoffeeMachine.glb", new Vector3(4.9f, 1.18f, -4.3f), 0, 0.55f, false);
            Model("food/croissant.glb", new Vector3(3, 1.19f, -4), 0, 0.13f, false);
            Model("food/cookie.glb", new Vector3(3.4f, 1.19f, -4), 0, 0.08f, false);
            Box("Menu board", new Vector3(4, 2.25f, -6.8f), new Vector3(5.6f, 1.45f, 0.12f), Ink, false);
            Sign("FOREST CAFE", new Vector3(4, 2.65f, -6.68f), 0.19f);
            Sign("Coffee $4   Latte $6.50\nTea $3   Lemonade $3.50", new Vector3(4, 2.1f, -6.67f), 0.1f);
            Table(new Vector3(-4, 0, 1.5f), true);
            Table(new Vector3(-5, 0, -4.4f), false);
            Table(new Vector3(6, 0, 3), false);
            foreach (var position in new[] { new Vector3(-7, 0, -6), new Vector3(7.5f, 0, -5.6f), new Vector3(-7.7f, 0, 4.5f), new Vector3(7.8f, 0, 5.4f) })
                Model("furniture/pottedPlant.glb", position, 0, 1.6f, true);
            Tree();
            foreach (var x in new[] { -4f, 4f })
            {
                var lamp = Primitive("Pendant shade", PrimitiveType.Sphere, new Vector3(x, 3.3f, -2), new Vector3(0.8f, 0.35f, 0.8f), Ink, false);
                Box("Pendant cord", new Vector3(x, 3.75f, -2), new Vector3(0.035f, 0.6f, 0.035f), Ink, false);
                var light = new GameObject("Warm cafe light").AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Hex("FFDC9A");
                light.intensity = 2;
                light.range = 8;
                light.transform.position = lamp.transform.position - Vector3.up * 0.25f;
            }
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFE5B0");
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(43, -38, 0);
            RenderSettings.sun = sun;
            var volume = new GameObject("Warm color grade").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>().saturation.Override(-5);
            AssetDatabase.CreateAsset(profile, UniqueAsset("ColorGrade.asset"));
            volume.sharedProfile = profile;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.backgroundColor = Hex("D9E3CB");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 70;
            camera.fieldOfView = 54;
            camera.transform.position = new Vector3(0, 4.8f, 11);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var playerObject = new GameObject("Player");
            playerObject.transform.position = new Vector3(0, 0.05f, 5);
            var controller = playerObject.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 0.85f, 0);
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.2f;
            var player = playerObject.AddComponent<CafePlayer>();
            player.View = camera;
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(playerObject.transform, false);
            CozyCharacters.DressPlayer(avatar.transform);
            player.Model = avatar.transform;
            var maya = NpcCastCatalog.CreateNpc("ForestCafe", "Maya", new Vector3(3.8f, 0.04f, -5.1f));
            maya.name = "Maya";
            var alex = NpcCastCatalog.CreateNpc("ForestCafe", "Alex", new Vector3(-4, 0.04f, 0.25f));
            alex.name = "Alex";
            var drinks = new GameObject("Earned drinks");
            foreach (var x in new[] { -4.3f, -3.7f })
                Model("food/cup-coffee.glb", new Vector3(x, 0.8f, 1.5f), 0, 0.16f, false).transform.SetParent(drinks.transform, true);
            drinks.SetActive(false);
            var runtime = new GameObject("CafeRuntime").AddComponent<CafeGame>();
            runtime.Player = player;
            runtime.Maya = maya.transform;
            runtime.Alex = alex.transform;
            runtime.Drinks = drinks;
            WorldBuild.Configure(player, new[] { maya.transform, alex.transform }, "ForestCafe");
        }
        private static void Table(Vector3 position, bool friend)
        {
            Model("furniture/tableRound.glb", position, 0, 0.78f, true);
            foreach (var offset in new[] { new Vector3(0, 0, 1.2f), new Vector3(0, 0, -1.2f) })
            {
                if (friend) continue;
                Model("furniture/chairCushion.glb", position + offset, offset.z > 0 ? 180 : 0, 0.9f, true);
            }
            if (friend)
            {
                Model("furniture/chairCushion.glb", position + new Vector3(0, 0, 1.2f), 180, 0.9f, false);
                Model("furniture/chairCushion.glb", position + new Vector3(0, 0, -1.2f), 0, 0.9f, false);
            }
        }
        private static void Tree()
        {
            Primitive("Tree planter", PrimitiveType.Cylinder, new Vector3(0, 0.17f, -2), new Vector3(2.3f, 0.17f, 2.3f), Ink, true);
            Primitive("Tree trunk", PrimitiveType.Cylinder, new Vector3(0, 1.9f, -2), new Vector3(0.7f, 1.8f, 0.7f), Wood, true);
            for (var index = 0; index < 6; index++)
            {
                var angle = index * Mathf.PI / 3;
                Primitive("Forest canopy", PrimitiveType.Sphere, new Vector3(Mathf.Cos(angle) * 1.3f, 3.7f + (index % 2) * 0.45f, -2 + Mathf.Sin(angle) * 1.3f), new Vector3(2.8f, 1.5f, 2.5f), Color.Lerp(Green, Cream, index * 0.035f), false);
            }
        }
        private static string UniqueAsset(string name)
        {
            var path = "Assets/Generated/" + name;
            AssetDatabase.DeleteAsset(path);
            return path;
        }
        private static Material Surface(Color color)
        {
            var name = ColorUtility.ToHtmlStringRGB(color);
            if (Materials.TryGetValue(name, out var cached)) return cached;
            var path = "Assets/Generated/Surface-" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            material.enableInstancing = true;
            Materials[name] = material;
            return material;
        }
        internal static GameObject Box(string name, Vector3 position, Vector3 size, Color color, bool collide)
        {
            return Primitive(name, PrimitiveType.Cube, position, size, color, collide);
        }
        internal static GameObject Primitive(string name, PrimitiveType shape, Vector3 position, Vector3 size, Color color, bool collide)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = Surface(color);
            if (!collide) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            item.isStatic = true;
            return item;
        }
        internal static GameObject Model(string file, Vector3 position, float yaw, float height, bool collide)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/" + file);
            if (prefab == null) throw new Exception("Missing verified cafe model: " + file + ". Run the Unity preparation script.");
            foreach (var mesh in prefab.GetComponentsInChildren<Renderer>())
                foreach (var material in mesh.sharedMaterials)
                    if (!HasUniversalShader(material))
                        throw new Exception("Imported art needs URP materials: " + file + " / " + material?.shader.name);
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var bounds = Bounds(item);
            if (bounds.size.y < 0.001f) throw new Exception("Model has no measurable geometry: " + file);
            item.transform.localScale *= height / bounds.size.y;
            bounds = Bounds(item);
            item.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            if (collide)
            {
                bounds = Bounds(item);
                var obstacle = new GameObject("Collision · " + item.name);
                obstacle.transform.position = bounds.center;
                obstacle.AddComponent<BoxCollider>().size = bounds.size;
                obstacle.transform.SetParent(item.transform, true);
            }
            return item;
        }
        private static bool HasUniversalShader(Material material)
        {
            if (material == null || material.shader == null) return false;
            // Headless Editors have no active GPU subshader; inspect all declared targets.
            for (var index = 0; index < material.shader.subshaderCount; index++)
                if (material.shader.FindSubshaderTagValue(index, new ShaderTagId("RenderPipeline")).name == "UniversalPipeline") return true;
            return false;
        }
        private static Bounds Bounds(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("Imported model has no renderer: " + item.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        internal static void Sign(string text, Vector3 position, float scale)
        {
            var sign = new GameObject("Cafe sign").AddComponent<TextMesh>();
            sign.text = text;
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.fontSize = 64;
            sign.characterSize = scale * 0.1f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = Cream;
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.Euler(0, 180, 0);
            sign.GetComponent<Renderer>().sharedMaterial = sign.font.material;
            sign.gameObject.AddComponent<WorldSign>();
        }
        internal static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var value);
            return value;
        }
    }
}
