using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// Shared building blocks for the life-simulation location scenes: rooms, glass doors, seats,
    /// signs, NPCs, the player and the shared gameplay systems. Everything is real-world scale
    /// (metres; a person is 1.7 m, a table top 0.76 m, a chair seat 0.45 m).
    public static class LifeBuild
    {
        public const string Data = "Assets/ContextSpeak/Data";
        public const string Scenes = "Assets/ContextSpeak/Scenes";
        public static readonly List<string> Missing = new List<string>();
        private static readonly List<NPCController> npcs = new List<NPCController>();
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static Transform root;

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var value); return value; }

        public static void Begin(string title)
        {
            npcs.Clear();
            materials.Clear();
            root = new GameObject(title).transform;
        }

        /// Brightest plausible albedo (sRGB) for painted/flat surfaces; brighter colours blow out at noon.
        public const float MaxAlbedo = 0.86f;

        public static Material Surface(Color colour, float smooth = 0.25f, float metal = 0)
        {
            var peak = Mathf.Max(colour.r, colour.g, colour.b);
            if (peak > MaxAlbedo && colour.a >= 0.99f) colour = new Color(colour.r * MaxAlbedo / peak, colour.g * MaxAlbedo / peak, colour.b * MaxAlbedo / peak, colour.a);
            var key = ColorUtility.ToHtmlStringRGBA(colour) + "-" + Mathf.RoundToInt(smooth * 100) + (metal > 0 ? "-m" + Mathf.RoundToInt(metal * 100) : "");
            if (materials.TryGetValue(key, out var existing)) return existing;
            var path = Data + "/Materials/Life-" + key.Replace('.', '_') + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) { materials[key] = material; return material; }
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smooth);
            material.SetFloat("_Metallic", metal);
            material.enableInstancing = true;
            if (colour.a < 0.99f)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            Directory.CreateDirectory(Data + "/Materials");
            AssetDatabase.CreateAsset(material, path);
            materials[key] = material;
            return material;
        }

        /// Re-surface an object with a PBR look (smoothness/metalness), e.g. polished stone or brushed aluminium.
        public static GameObject Paint(GameObject item, Color colour, float smooth, float metal = 0)
        {
            item.GetComponent<Renderer>().sharedMaterial = Surface(colour, smooth, metal);
            return item;
        }

        public static GameObject Box(string name, Vector3 position, Vector3 size, Color colour, bool collide, Transform parent = null, float yaw = 0)
            => Shape(name, PrimitiveType.Cube, position, size, colour, collide, parent, yaw);

        public static GameObject Shape(string name, PrimitiveType shape, Vector3 position, Vector3 size, Color colour, bool collide, Transform parent = null, float yaw = 0)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent != null ? parent : root, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = Surface(colour, colour.a < 0.99f ? 0.9f : 0.25f);
            if (!collide) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            item.isStatic = collide;
            return item;
        }

        public static Transform Group(string name, Vector3 position = default, float yaw = 0, Transform parent = null)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent != null ? parent : root, false);
            group.localPosition = position;
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            return group;
        }

        /// A straight wall from a to b (on the floor) with optional door gaps (centre along the wall, width).
        public static void Wall(string name, Vector3 a, Vector3 b, float height, Color colour, params Vector2[] gaps)
        {
            var direction = b - a;
            var length = direction.magnitude;
            var along = direction / length;
            var yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg - 90;
            var cuts = gaps.OrderBy(gap => gap.x).ToList();
            var start = 0f;
            void Piece(float from, float to, float bottom, float top)
            {
                if (to - from < 0.02f || top - bottom < 0.02f) return;
                var middle = a + along * ((from + to) / 2) + Vector3.up * ((bottom + top) / 2);
                Box(name, middle, new Vector3(to - from, top - bottom, 0.2f), colour, true, null, yaw);
            }
            foreach (var gap in cuts)
            {
                Piece(start, gap.x - gap.y / 2, 0, height);
                Piece(gap.x - gap.y / 2, gap.x + gap.y / 2, 2.6f, height);   // lintel above a door
                start = gap.x + gap.y / 2;
            }
            Piece(start, length, 0, height);
        }

        public static void Window(Vector3 centre, float yaw, float width, float height = 1.6f, float sill = 0.9f)
        {
            Box("Window glass", centre + Vector3.up * (sill + height / 2), new Vector3(width, height, 0.05f), new Color(0.75f, 0.88f, 0.95f, 0.35f), false, null, yaw);
            Box("Window frame", centre + Vector3.up * sill, new Vector3(width + 0.1f, 0.08f, 0.26f), Hex("F4F4F2"), false, null, yaw);
        }

        /// Automatic sliding glass doors (hospital, restaurant entrance).
        public static DoorController SlidingDoor(string name, Vector3 centre, float yaw, float width)
        {
            var door = Group(name, centre, yaw);
            var leafWidth = width / 2;
            var glass = new Color(0.7f, 0.86f, 0.92f, 0.4f);
            var left = Box("Left leaf", new Vector3(-leafWidth / 2, 1.25f, 0), new Vector3(leafWidth, 2.5f, 0.05f), glass, true, door);
            var right = Box("Right leaf", new Vector3(leafWidth / 2, 1.25f, 0), new Vector3(leafWidth, 2.5f, 0.05f), glass, true, door);
            left.isStatic = right.isStatic = false;
            Box("Door track", new Vector3(0, 2.55f, 0), new Vector3(width * 2, 0.1f, 0.15f), Hex("8A9399"), false, door);
            var controller = door.gameObject.AddComponent<DoorController>();
            controller.LeftLeaf = left.transform;
            controller.RightLeaf = right.transform;
            controller.SlideDistance = leafWidth * 0.95f;
            controller.Range = 3.2f;
            return controller;
        }

        /// A hinged room door that opens when someone approaches (can start locked).
        public static DoorController HingedDoor(string name, Vector3 centre, float yaw, float width, Color colour)
        {
            var door = Group(name, centre, yaw);
            var hinge = Group("Hinge", new Vector3(-width / 2, 0, 0), 0, door);
            var leaf = Box("Door leaf", new Vector3(width / 2, 1.05f, 0), new Vector3(width - 0.04f, 2.1f, 0.05f), colour, true, hinge);
            leaf.isStatic = false;
            Box("Handle", new Vector3(width - 0.15f, 1.0f, 0.05f), new Vector3(0.12f, 0.03f, 0.05f), Hex("C9CCCF"), false, hinge).isStatic = false;
            var controller = door.gameObject.AddComponent<DoorController>();
            controller.Hinge = hinge;
            controller.Range = 2.2f;
            return controller;
        }

        public static TextMesh Label(string text, Vector3 position, float yaw, float size, Color colour, Transform parent = null)
        {
            var label = new GameObject("Sign · " + text.Split('\n')[0]).AddComponent<TextMesh>();
            label.transform.SetParent(parent != null ? parent : root, false);
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.characterSize = size * 0.1f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = colour;
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.gameObject.AddComponent<WorldSign>();
            return label;
        }

        /// A sign board with text on its front (yaw = direction the text faces + 180, like TextMesh).
        public static TextMesh Board(string text, Vector3 position, float yaw, Vector2 size, Color board, Color ink)
        {
            Box("Sign board", position, new Vector3(size.x, size.y, 0.06f), board, false, null, yaw);
            var facing = Quaternion.Euler(0, yaw, 0) * Vector3.back;
            var lines = text.Split('\n');
            // A line of 64-pt TextMesh is about 2.2 × scale metres tall; glyphs ≈ 0.55 × line height wide.
            var lineHeight = Mathf.Min(size.y / (lines.Length + 0.4f), size.x * 0.92f / (0.55f * lines.Max(line => line.Length) + 1));
            return Label(text, position + facing * 0.04f, yaw, lineHeight / 2.2f, ink);
        }

        public static SeatController Chair(Vector3 floor, float yaw, Color colour, string group, Transform parent = null)
        {
            var chair = Group("Chair · " + group, floor, yaw, parent);
            Box("Seat", new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), colour, false, chair);
            Box("Back", new Vector3(0, 0.72f, -0.21f), new Vector3(0.45f, 0.5f, 0.04f), colour, false, chair);
            foreach (var x in new[] { -0.19f, 0.19f })
                foreach (var z in new[] { -0.19f, 0.19f })
                    Box("Leg", new Vector3(x, 0.22f, z), new Vector3(0.035f, 0.44f, 0.035f), Hex("4A4A4A"), false, chair);
            var seat = chair.gameObject.AddComponent<SeatController>();
            seat.Group = group;
            seat.Prompt = "Sit down";
            seat.Range = 1.6f;
            return seat;
        }

        public static Transform Table(Vector3 floor, Vector2 top, Color colour, Transform parent = null, string name = "Table")
        {
            var table = Group(name, floor, 0, parent);
            Box("Table top", new Vector3(0, 0.74f, 0), new Vector3(top.x, 0.04f, top.y), colour, true, table);
            foreach (var x in new[] { -1, 1 })
                foreach (var z in new[] { -1, 1 })
                    Box("Table leg", new Vector3(x * (top.x / 2 - 0.06f), 0.36f, z * (top.y / 2 - 0.06f)), new Vector3(0.05f, 0.72f, 0.05f), Hex("3D3D3D"), false, table);
            return table;
        }

        public static void Plant(Vector3 floor, float height = 1.4f)
        {
            Shape("Planter", PrimitiveType.Cylinder, floor + Vector3.up * 0.2f, new Vector3(0.5f, 0.2f, 0.5f), Hex("6B6B6B"), true);
            Shape("Plant leaves", PrimitiveType.Sphere, floor + Vector3.up * (0.4f + height / 2), new Vector3(0.8f, height, 0.8f), Hex("4E8B4E"), false);
        }

        public static void Lamp(Vector3 position, float range = 9, float intensity = 1.6f, string colour = "FFF4E2")
        {
            var lamp = new GameObject("Ceiling light").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = position;
            lamp.type = LightType.Point;
            lamp.range = range;
            lamp.intensity = intensity;
            lamp.color = Hex(colour);
            lamp.shadows = LightShadows.None;
            Box("Light panel", position + Vector3.up * 0.18f, new Vector3(0.9f, 0.03f, 0.9f), Hex("FFFDF5"), false);
        }

        /// A working NPC: Meshy body (capsule fallback), schedule and a "Talk to" interaction.
        public static NPCController Npc(string key, string displayName, string role, string model, Vector3 position, float yaw, params Vector3[] schedule)
        {
            var person = new GameObject("NPC · " + displayName);
            person.transform.SetParent(root, false);
            person.transform.position = position;
            person.transform.rotation = Quaternion.Euler(0, yaw, 0);
            GameObject body;
            try { body = MeshyCharacters.Create(model, position); }
            catch (Exception error)
            {
                Missing.Add("Character model " + model + " (" + error.Message + ") — used a placeholder capsule for " + displayName);
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
                body.transform.position = position + Vector3.up * 0.85f;
            }
            body.transform.SetParent(person.transform, true);
            body.transform.localRotation = Quaternion.identity;
            foreach (var renderer in person.GetComponentsInChildren<Renderer>()) renderer.gameObject.isStatic = false;
            var npc = person.AddComponent<NPCController>();
            npc.Key = key;
            npc.DisplayName = displayName;
            npc.Role = role;
            npc.Schedule = schedule;
            var talk = person.AddComponent<NPCDialogueController>();
            talk.Npc = npc;
            talk.Range = 2.6f;
            npcs.Add(npc);
            return npc;
        }

        public static (CafePlayer, Camera) Player(Vector3 position, float yaw)
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 220;
            camera.fieldOfView = 58;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = position + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 2.4f, -3.5f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var body = new GameObject("Player");
            body.transform.position = position;
            body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var controller = body.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 0.85f, 0);
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 46;
            var player = body.AddComponent<CafePlayer>();
            player.View = camera;
            body.AddComponent<AudioSource>();
            body.AddComponent<ContextSpeak.Slice.FootstepAudio>();
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(body.transform, false);
            try { CozyCharacters.DressPlayer(avatar.transform); }
            catch (Exception error) { Missing.Add("Player avatar (" + error.Message + ")"); }
            avatar.AddComponent<CityEmotes>();
            player.Model = avatar.transform;
            return (player, camera);
        }

        public static void Daylight(string district, string ambient = "E9ECEF")
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFF1DD");
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            RenderSettings.sun = sun;
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_Exposure", 1.2f);
            Directory.CreateDirectory(Data + "/Materials");
            Replace(sky, Data + "/Materials/LifeSky-" + district + ".mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(ambient);
            RenderSettings.ambientEquatorColor = Hex("C9CCC8");
            RenderSettings.ambientGroundColor = Hex("8F877C");
            RenderSettings.fog = false;
            var volume = new GameObject("Colour grade").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>().postExposure.Override(0f);   // calibrated with the sky (realistic lighting upgrade)
            Replace(profile, Data + "/Materials/LifeGrade-" + district + ".asset");
            volume.sharedProfile = profile;
        }

        /// Player, camera, NavMesh, NPC agents and the shared systems; returns the systems object.
        public static GameObject Finish(CafePlayer player, Camera camera, LocationFlow flow, string location, string district)
        {
            WorldBuild.Configure(player, new Transform[0], district);
            // Agents and solid bodies after the bake, so people don't cut holes in the walkable floor.
            foreach (var npc in npcs)
            {
                var agent = npc.gameObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.28f;
                agent.height = 1.7f;
                agent.enabled = false;
                var capsule = npc.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 0.85f, 0);
                capsule.height = 1.7f;
                capsule.radius = 0.28f;
            }
            var systems = flow.gameObject;
            systems.AddComponent<InteractionManager>().Player = player;
            systems.AddComponent<ScenarioManager>();
            systems.AddComponent<LifeUI>().LocationName = location;
            systems.AddComponent<MenuController>();
            systems.AddComponent<PaymentController>();
            flow.Scenarios = ScenarioLibrary.Names.Select(name => AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(Data + "/Scenarios/" + name + ".asset"))
                .Where(asset => asset != null).ToArray();
            return systems;
        }

        public static void Replace(UnityEngine.Object asset, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        public static void Save(UnityEngine.SceneManagement.Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(item => item.path == path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
