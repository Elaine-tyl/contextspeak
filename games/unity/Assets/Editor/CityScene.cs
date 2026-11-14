using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.CafeScene;

namespace ContextSpeak.Editor
{
    /// Metro City: a real-scale city block grid with streets, signals, traffic, passers-by and six
    /// enterable buildings (home, school, hospital, bank, café, restaurant) plus a market and a park.
    /// Everyone you can talk to runs the IELTS 3–9 curriculum for their place.
    /// Menu: ContextSpeak → Build Metro City. Deterministic, like the other scene builders.
    public static class CityScene
    {
        public const string ScenePath = "Assets/Generated/MetroCity.unity";
        private static readonly float[] Lines = { -110f, -55f, 0f, 55f, 110f };   // road centre lines, both axes; z = 0 is the tram boulevard
        private const float RoadHalf = 5f, Edge = 119f, Boundary = 122f, Wall = 0.3f, DoorWidth = 2.2f;
        private static readonly Color Asphalt = Hex("3A3D42"), Paving = Hex("BEB9AF"), CurbStone = Hex("8F8B84"), PlazaStone = Hex("D2CCBE"),
            Lawn = Hex("7E9F68"), Paint = Hex("EEECE4"), PaintYellow = Hex("E3BC48"), Dark = Hex("1E2124"), Steel = Hex("9AA2A8"), White = Hex("F4F4F0");
        private static Transform props;
        private static List<Transform> talkers;
        private static Material lampMaterial;

        private sealed class Shell
        {
            public Transform Root;
            public float W, D, H;
            public readonly List<GameObject> Hide = new List<GameObject>();
            public readonly List<Light> Lights = new List<Light>();
        }

        [UnityEditor.MenuItem("ContextSpeak/Build Metro City (city life)")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CafeBuild.ConfigurePipeline();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + "World784ed44e61.prefab") == null) WorldAssetBuilder.Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Create();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(item => item.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Metro City built at " + ScenePath + ". Press Play, walk with WASD and press E next to someone to talk.");
        }

        public static void Create()
        {
            EnvironmentArt.Begin();
            props = new GameObject("City props").transform;
            talkers = new List<Transform>();
            lampMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lampMaterial.EnableKeyword("_EMISSION");
            lampMaterial.SetFloat("_Smoothness", 0.7f);
            SaveAsset(lampMaterial, "Assets/Generated/CitySignalLamp.mat");

            Sky();
            Streets();
            var junctions = (from x in Lines from z in Lines select new Vector3(x, 0, z)).ToArray();
            foreach (var junction in junctions) Signals(junction);
            StreetFurniture();
            Home();
            School();
            Hospital();
            Bank();
            Cafe();
            Restaurant();
            Market();
            Park();
            Station();
            Library();
            Cinema();
            Supermarket();
            Police();
            Pharmacy();
            Gym();
            Ramen();
            Hotel();
            PostOffice();
            Mall();
            Apartments();
            Tramway();
            // Pocket parks behind the outer buildings: grass, flowers, trees, benches and people.
            Greenery(new Vector3(27.5f, 0, 92), new Vector2(34, 14), 21, 3, 2);
            Greenery(new Vector3(82.5f, 0, 90), new Vector2(34, 16), 22, 3, 2);
            Greenery(new Vector3(-82.5f, 0, 92), new Vector2(34, 14), 23, 3, 1);
            Greenery(new Vector3(-27.5f, 0, 89), new Vector2(34, 20), 24, 4, 2);
            Greenery(new Vector3(27.5f, 0, -90), new Vector2(34, 18), 25, 3, 2);
            Greenery(new Vector3(82.5f, 0, -93), new Vector2(30, 12), 26, 2, 1);
            Greenery(new Vector3(-27.5f, 0, -92), new Vector2(34, 14), 27, 3, 2);
            Greenery(new Vector3(-78, 0, -72), new Vector2(26, 14), 28, 2, 2);
            Skyline();
            foreach (var side in new[] { -1, 1 })
            {
                Invisible("City boundary", new Vector3(side * Boundary, 2, 0), new Vector3(0.4f, 5, Boundary * 2));
                Invisible("City boundary", new Vector3(0, 2, side * Boundary), new Vector3(Boundary * 2, 5, 0.4f));
            }

            var sun = Sun();
            var (player, camera) = Player();
            ForestRendering.Configure(camera, sun);
            Grade();
            WorldBuild.Configure(player, talkers.ToArray(), "MetroCity");
            // Solid bodies after the NavMesh bake so they don't cut holes in it.
            foreach (var talker in talkers)
            {
                var capsule = talker.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 0.85f, 0);
                capsule.height = 1.7f;
                capsule.radius = 0.28f;
            }
            Traffic(junctions);
            PublicTransport(junctions);
            Pedestrians();
            var runtime = new GameObject("MetroCity").AddComponent<CityGame>();
            runtime.Player = player;
            runtime.StreetName = "Metro City streets";
            SkyInstaller.Install();
        }

        // ------------------------------------------------------------------ sky, light, camera, player
        private static void Sky()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetFloat("_Exposure", 1.2f);
            SaveAsset(sky, "Assets/Generated/CitySky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("CDDAEA");
            RenderSettings.ambientEquatorColor = Hex("B8BCB6");
            RenderSettings.ambientGroundColor = Hex("7D776E");
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("C9D5E1");
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.004f;
        }

        private static Light Sun()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFF0D9");
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(47, -38, 0);
            RenderSettings.sun = sun;
            return sun;
        }

        private static void Grade()
        {
            var volume = new GameObject("City colour grade").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var colour = profile.Add<ColorAdjustments>();
            colour.postExposure.Override(0.15f);
            colour.contrast.Override(8f);
            colour.saturation.Override(4f);
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.1f);
            SaveAsset(profile, "Assets/Generated/CityColorGrade.asset");
            volume.sharedProfile = profile;
        }

        private static (CafePlayer, Camera) Player()
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 450;
            camera.fieldOfView = 56;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(-30, 4.5f, -2);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var body = new GameObject("City player");
            body.transform.position = new Vector3(-30, 0.25f, -7.2f);
            body.transform.rotation = Quaternion.Euler(0, 180, 0);
            var controller = body.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 0.85f, 0);
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 46;
            var player = body.AddComponent<CafePlayer>();
            player.View = camera;
            body.AddComponent<AudioSource>();
            body.AddComponent<FootstepAudio>();
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(body.transform, false);
            CozyCharacters.DressPlayer(avatar.transform);
            avatar.AddComponent<CityEmotes>();
            player.Model = avatar.transform;
            return (player, camera);
        }

        // ------------------------------------------------------------------ streets
        private static IEnumerable<(float start, float end)> Spans(float pad)
        {
            // Stretches of a road side between crossings, from the city edge to the city edge.
            var stops = new List<float> { -Edge };
            foreach (var line in Lines) { stops.Add(line - pad); stops.Add(line + pad); }
            stops.Add(Edge);
            for (var index = 0; index + 1 < stops.Count; index += 2)
                if (stops[index + 1] - stops[index] > 0.5f) yield return (stops[index], stops[index + 1]);
        }

        private static GameObject Strip(string name, bool alongX, float line, float offset, float start, float end, float y, float thickness, float width, Color colour, bool collide)
        {
            var length = end - start;
            var middle = (start + end) / 2;
            var position = alongX ? new Vector3(middle, y, line + offset) : new Vector3(line + offset, y, middle);
            var size = alongX ? new Vector3(length, thickness, width) : new Vector3(width, thickness, length);
            return Box(name, position, size, colour, collide);
        }

        private static void Streets()
        {
            Box("City ground", new Vector3(0, -0.1f, 0), new Vector3(Boundary * 2 + 40, 0.2f, Boundary * 2 + 40), PlazaStone, true);
            foreach (var alongX in new[] { true, false })
                foreach (var line in Lines)
                {
                    Strip("Road", alongX, line, 0, -Edge, Edge, 0.025f, 0.05f, RoadHalf * 2, Asphalt, true);
                    foreach (var side in new[] { -1, 1 })
                        foreach (var (start, end) in Spans(RoadHalf))
                        {
                            Strip("Pavement", alongX, line, side * 7f, start, end, 0.075f, 0.15f, 4f, Paving, true);
                            Strip("Kerb", alongX, line, side * (RoadHalf + 0.13f), start, end, 0.085f, 0.17f, 0.26f, CurbStone, false);
                            Strip("Edge line", alongX, line, side * (RoadHalf - 0.35f), start + 2.5f, end - 2.5f, 0.053f, 0.01f, 0.12f, Paint, false);
                        }
                    // Dashed centre line (the tram boulevard has track instead).
                    if (alongX && line == 0) continue;
                    foreach (var (start, end) in Spans(RoadHalf + 3))
                        for (var dash = start; dash + 3 <= end; dash += 6)
                            Strip("Centre dash", alongX, line, 0, dash, dash + 3, 0.053f, 0.01f, 0.14f, PaintYellow, false);
                }
            // Zebra crossings and stop lines on every arm of every junction.
            foreach (var jx in Lines)
                foreach (var jz in Lines)
                    foreach (var arm in new[] { -1, 1 })
                    {
                        for (var stripe = -4; stripe <= 4; stripe++)
                        {
                            Box("Zebra stripe", new Vector3(jx + arm * 7.2f, 0.054f, jz + stripe * 1.05f), new Vector3(2.8f, 0.01f, 0.5f), Paint, false);
                            Box("Zebra stripe", new Vector3(jx + stripe * 1.05f, 0.054f, jz + arm * 7.2f), new Vector3(0.5f, 0.01f, 2.8f), Paint, false);
                        }
                        // Stop lines on the approaching lane (drive on the right).
                        Box("Stop line", new Vector3(jx + arm * 9f, 0.054f, jz + arm * 2.5f), new Vector3(0.35f, 0.01f, 4.6f), Paint, false);
                        Box("Stop line", new Vector3(jx - arm * 2.5f, 0.054f, jz + arm * 9f), new Vector3(4.6f, 0.01f, 0.35f), Paint, false);
                    }
        }

        private static void Signals(Vector3 junction)
        {
            // One head for each axis, on opposite corners so both directions can see a signal.
            SignalHead(junction + new Vector3(6.3f, 0.15f, -6.3f), true, 270);
            SignalHead(junction + new Vector3(-6.3f, 0.15f, 6.3f), true, 90);
            SignalHead(junction + new Vector3(6.3f, 0.15f, 6.3f), false, 180);
            SignalHead(junction + new Vector3(-6.3f, 0.15f, -6.3f), false, 0);
        }

        private static void SignalHead(Vector3 foot, bool alongX, float yaw)
        {
            var root = new GameObject(alongX ? "Signal (east–west)" : "Signal (north–south)").transform;
            root.position = foot;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            Part(root, "Signal pole", new Vector3(0, 1.8f, 0), new Vector3(0.14f, 1.8f, 0.14f), Dark, true, PrimitiveType.Cylinder);
            Part(root, "Signal housing", new Vector3(0, 3.35f, 0.18f), new Vector3(0.38f, 1.1f, 0.26f), Dark, false);
            Renderer Lamp(float y)
            {
                var lamp = Part(root, "Signal lamp", new Vector3(0, y, 0.33f), Vector3.one * 0.24f, Dark, false, PrimitiveType.Sphere);
                lamp.isStatic = false;
                var renderer = lamp.GetComponent<Renderer>();
                renderer.sharedMaterial = lampMaterial;
                return renderer;
            }
            var light = root.gameObject.AddComponent<CityTrafficLight>();
            light.AlongX = alongX;
            light.Red = Lamp(3.7f);
            light.Yellow = Lamp(3.35f);
            light.GreenLamp = Lamp(3.0f);
        }

        private static void StreetFurniture()
        {
            foreach (var alongX in new[] { true, false })
                foreach (var line in Lines)
                    foreach (var side in new[] { -1, 1 })
                    {
                        for (var along = -Edge + 6; along < Edge - 4; along += 24)
                        {
                            if (Lines.Any(other => Mathf.Abs(along - other) < 11)) continue;
                            var tree = alongX ? new Vector3(along, 0.15f, line + side * 8.1f) : new Vector3(line + side * 8.1f, 0.15f, along);
                            EnvironmentArt.Tree(tree, 7.5f);
                        }
                        for (var along = -Edge + 18; along < Edge - 4; along += 24)
                        {
                            if (Lines.Any(other => Mathf.Abs(along - other) < 10)) continue;
                            var post = alongX ? new Vector3(along, 0.15f, line + side * 5.6f) : new Vector3(line + side * 5.6f, 0.15f, along);
                            StreetLamp(post, alongX ? (side > 0 ? 180 : 0) : (side > 0 ? 270 : 90));
                        }
                    }
        }

        private static void StreetLamp(Vector3 foot, float yaw)
        {
            var root = new GameObject("Street lamp").transform;
            root.position = foot;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            Part(root, "Lamp post", new Vector3(0, 2.4f, 0), new Vector3(0.12f, 2.4f, 0.12f), Hex("2B3036"), true, PrimitiveType.Cylinder);
            Part(root, "Lamp arm", new Vector3(0, 4.75f, 0.55f), new Vector3(0.08f, 0.08f, 1.1f), Hex("2B3036"), false);
            var head = Part(root, "Lamp head", new Vector3(0, 4.65f, 1.05f), new Vector3(0.45f, 0.12f, 0.3f), White, false);
            head.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
        }

        // ------------------------------------------------------------------ enterable buildings
        /// A one-storey building with a glazed shopfront, an automatic door, a sign and a roof that
        /// lifts away while you are inside. Local +Z is the street side.
        private static Shell Building(string title, string titleZh, Vector3 centre, float yaw, float w, float d, float h,
            Color wall, Color trim, Color floor, Color roof, float yard, Color lightColour, string style = "flat", int floors = 0)
        {
            var root = new GameObject(title).transform;
            root.position = centre;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            var shell = new Shell { Root = root, W = w, D = d, H = h };
            var front = d / 2 - Wall / 2;
            Part(root, "Floor", new Vector3(0, 0.08f, 0), new Vector3(w, 0.16f, d), floor, true);
            Part(root, "Back wall", new Vector3(0, h / 2, -d / 2 + Wall / 2), new Vector3(w, h, Wall), wall, true);
            foreach (var side in new[] { -1, 1 })
                Part(root, "Side wall", new Vector3(side * (w / 2 - Wall / 2), h / 2, 0), new Vector3(Wall, h, d), wall, true);

            // Shopfront: solid base, glazing between mullions, solid band with the sign above.
            var segment = (w - DoorWidth) / 2;
            foreach (var side in new[] { -1, 1 })
            {
                var x = side * (DoorWidth / 2 + segment / 2);
                Part(root, "Shopfront base", new Vector3(x, 0.5f, front), new Vector3(segment, 1f, Wall), wall, true);
                Part(root, "Shopfront head", new Vector3(x, (2.9f + h) / 2, front), new Vector3(segment, h - 2.9f, Wall), wall, true);
                Glass(root, new Vector3(x, 1.95f, front), new Vector3(segment - 0.05f, 1.9f, 0.06f), true);
                var bays = Mathf.Max(1, Mathf.RoundToInt(segment / 2.2f));
                for (var index = 0; index <= bays; index++)
                    Part(root, "Mullion", new Vector3(x - segment / 2 + index * segment / bays, 1.95f, front + 0.03f), new Vector3(0.1f, 1.9f, Wall + 0.08f), trim, false);
            }
            Part(root, "Door head", new Vector3(0, (2.6f + h) / 2, front), new Vector3(DoorWidth, h - 2.6f, Wall), wall, true);
            foreach (var side in new[] { -1, 1 })
                Part(root, "Door frame", new Vector3(side * (DoorWidth / 2 + 0.05f), 1.3f, front + 0.04f), new Vector3(0.1f, 2.6f, Wall + 0.1f), trim, false);
            // Windows on the other three sides (seen from the street).
            for (var z = -d / 2 + 2; z <= d / 2 - 2; z += 3)
                foreach (var side in new[] { -1, 1 })
                    Glass(root, new Vector3(side * (w / 2 + 0.02f), 1.95f, z), new Vector3(0.04f, 1.6f, 1.6f), false);
            for (var x = -w / 2 + 2; x <= w / 2 - 2; x += 3)
                Glass(root, new Vector3(x, 1.95f, -d / 2 - 0.02f), new Vector3(1.6f, 1.6f, 0.04f), false);
            Part(root, "Plinth", new Vector3(0, 0.2f, d / 2 + 0.02f), new Vector3(w + 0.04f, 0.4f, 0.04f), trim, false);

            // Door: the leaf hangs from a hinge and swings inwards (+Y rotation turns +X towards -Z).
            var door = new GameObject("Entrance door").transform;
            door.SetParent(root, false);
            door.localPosition = new Vector3(0, 0.16f, front);
            var hinge = new GameObject("Door hinge").transform;
            hinge.SetParent(door, false);
            hinge.localPosition = new Vector3(-DoorWidth / 2 + 0.05f, 0, 0);
            var leaf = Part(hinge, "Door leaf", new Vector3(DoorWidth / 2 - 0.05f, 1.22f, 0), new Vector3(DoorWidth - 0.1f, 2.44f, 0.06f), trim, false);
            leaf.isStatic = false;
            var pane = Glass(hinge, new Vector3(DoorWidth / 2 - 0.05f, 1.45f, 0), new Vector3(DoorWidth - 0.5f, 1.6f, 0.08f), false);
            pane.isStatic = false;
            door.gameObject.AddComponent<CityDoor>().Leaf = hinge;
            if (yard > 0.05f) Part(root, "Entrance path", new Vector3(0, 0.075f, d / 2 + yard / 2), new Vector3(2.8f, 0.15f, yard), Paving, true);

            // Sign above the door.
            Part(root, "Sign board", new Vector3(0, (2.9f + h) / 2, d / 2 + 0.06f), new Vector3(Mathf.Min(w * 0.62f, 9f), 0.8f, 0.08f), trim, false);
            Label(root, title.ToUpperInvariant(), new Vector3(0, (2.9f + h) / 2, d / 2 + 0.11f), 180, 0.3f, Hex("FFF6E6"));

            // Roof (hidden while you're inside) and ceiling lights.
            var roofGroup = new GameObject("Roof").transform;
            roofGroup.SetParent(root, false);
            Part(roofGroup, "Roof slab", new Vector3(0, h + 0.12f, 0), new Vector3(w + 0.4f, 0.24f, d + 0.4f), roof, false);
            foreach (var side in new[] { -1, 1 })
            {
                Part(roofGroup, "Parapet", new Vector3(0, h + 0.45f, side * (d / 2 + 0.1f)), new Vector3(w + 0.4f, 0.45f, 0.2f), trim, false);
                Part(roofGroup, "Parapet", new Vector3(side * (w / 2 + 0.1f), h + 0.45f, 0), new Vector3(0.2f, 0.45f, d + 0.4f), trim, false);
            }
            shell.Hide.Add(roofGroup.gameObject);
            Architecture(root, roofGroup, style, floors, w, d, h, wall, trim, roof);
            var columns = w > 13 ? 3 : 2;
            for (var column = 0; column < columns; column++)
                foreach (var z in new[] { -d / 4, d / 4 })
                {
                    var x = -w / 2 + (column + 0.5f) * w / columns;
                    var lamp = new GameObject("Ceiling light").AddComponent<Light>();
                    lamp.transform.SetParent(root, false);
                    lamp.transform.localPosition = new Vector3(x, h - 0.35f, z);
                    lamp.type = LightType.Point;
                    lamp.color = lightColour;
                    lamp.range = Mathf.Max(w, d) * 0.55f;
                    lamp.intensity = 1.6f;
                    lamp.shadows = LightShadows.None;
                    shell.Lights.Add(lamp);
                    var fitting = Part(root, "Light fitting", new Vector3(x, h - 0.2f, z), new Vector3(0.7f, 0.06f, 0.7f), White, false);
                    fitting.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
                }

            var inside = new Bounds(root.TransformPoint(new Vector3(0, h / 2, 0)), Vector3.zero);
            foreach (var x in new[] { -1, 1 })
                foreach (var z in new[] { -1, 1 })
                    inside.Encapsulate(root.TransformPoint(new Vector3(x * (w / 2 - Wall), h, z * (d / 2 - Wall))));
            inside.Encapsulate(root.TransformPoint(new Vector3(0, -0.2f, 0)));
            var building = root.gameObject.AddComponent<CityBuilding>();
            building.Title = title;
            building.TitleZh = titleZh;
            building.Inside = inside;
            building.HideWhenInside = shell.Hide.ToArray();
            building.Lights = shell.Lights.ToArray();
            return shell;
        }


        /// The parts that make each building look like its real-world type: upper storeys with
        /// windows, pitched roofs, classical porticoes, glass curtain walls, balconies. Everything
        /// above the ground floor sits in the roof group, so it lifts away while you are inside.
        private static void Architecture(Transform root, Transform upper, string style, int floors, float w, float d, float h,
            Color wall, Color trim, Color roof)
        {
            const float storey = 3.2f;
            var baseY = h + 0.24f;
            var top = baseY + floors * storey;
            if (floors > 0)
            {
                Part(upper, "Upper storeys", new Vector3(0, baseY + floors * storey / 2, 0), new Vector3(w, floors * storey, d), wall, false);
                for (var floor = 0; floor < floors; floor++)
                {
                    var y = baseY + floor * storey + 1.55f;
                    if (style == "glass" || style == "modern")
                    {
                        // Ribbon glazing: one band of glass per floor all round, with a solid band between.
                        foreach (var side in new[] { -1, 1 })
                        {
                            Glass(upper, new Vector3(0, y, side * (d / 2 + 0.03f)), new Vector3(w - 0.5f, style == "glass" ? 2.7f : 1.7f, 0.04f), false);
                            Glass(upper, new Vector3(side * (w / 2 + 0.03f), y, 0), new Vector3(0.04f, style == "glass" ? 2.7f : 1.7f, d - 0.5f), false);
                        }
                        Part(upper, "Floor band", new Vector3(0, baseY + floor * storey, 0), new Vector3(w + 0.12f, 0.22f, d + 0.12f), trim, false);
                        if (style == "glass")
                            for (var x = -w / 2 + 1.5f; x < w / 2 - 0.5f; x += 1.5f)
                                foreach (var side in new[] { -1, 1 })
                                    Part(upper, "Mullion", new Vector3(x, y, side * (d / 2 + 0.06f)), new Vector3(0.06f, 2.7f, 0.06f), trim, false);
                    }
                    else
                    {
                        // Punched windows with sills: brick terraces, houses, balconied flats.
                        for (var x = -w / 2 + 1.4f; x <= w / 2 - 1.3f; x += 2.4f)
                            foreach (var side in new[] { -1, 1 })
                            {
                                Glass(upper, new Vector3(x, y, side * (d / 2 + 0.03f)), new Vector3(1.3f, 1.6f, 0.04f), false);
                                Part(upper, "Window sill", new Vector3(x, y - 0.88f, side * (d / 2 + 0.08f)), new Vector3(1.5f, 0.1f, 0.14f), trim, false);
                                Part(upper, "Window head", new Vector3(x, y + 0.88f, side * (d / 2 + 0.06f)), new Vector3(1.5f, 0.12f, 0.1f), trim, false);
                                if (style == "balcony" && side > 0)
                                {
                                    Part(upper, "Balcony", new Vector3(x, y - 0.95f, d / 2 + 0.6f), new Vector3(1.9f, 0.12f, 1.1f), trim, false);
                                    Glass(upper, new Vector3(x, y - 0.45f, d / 2 + 1.12f), new Vector3(1.9f, 0.9f, 0.04f), false);
                                }
                            }
                        for (var z = -d / 2 + 1.4f; z <= d / 2 - 1.3f; z += 2.4f)
                            foreach (var side in new[] { -1, 1 })
                                Glass(upper, new Vector3(side * (w / 2 + 0.03f), y, z), new Vector3(0.04f, 1.6f, 1.3f), false);
                    }
                }
            }
            switch (style)
            {
                case "house":
                case "pagoda":
                {
                    // Pitched roof with gables and a chimney.
                    var pitch = style == "pagoda" ? 24f : 33f;
                    var run = d / 2 + 0.5f;
                    var rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * run;
                    var slope = run / Mathf.Cos(pitch * Mathf.Deg2Rad);
                    foreach (var side in new[] { -1, 1 })
                    {
                        var plane = Part(upper, "Roof slope", new Vector3(0, top + rise / 2 + 0.1f, side * run / 2), new Vector3(w + 0.7f, 0.16f, slope + 0.1f), roof, false);
                        plane.transform.localRotation = Quaternion.Euler(side * pitch, 0, 0);
                    }
                    var steps = 6;
                    for (var index = 0; index < steps; index++)
                    {
                        var height = rise * (1 - (index + 0.5f) / steps);
                        Part(upper, "Gable", new Vector3(0, top + 0.1f + height / 2, 0), new Vector3(w - 0.05f, height, (d - 0.1f) * (1 - (float)index / steps)), wall, false);
                    }
                    if (style == "house") Part(upper, "Chimney", new Vector3(w / 2 - 1.4f, top + rise * 0.8f, -d / 4), new Vector3(0.7f, rise * 1.1f, 0.7f), Hex("8E4A3A"), false);
                    break;
                }
                case "classic":
                {
                    // Portico: steps, columns, entablature and a pediment over the entrance.
                    var span = Mathf.Min(w * 0.7f, 12f);
                    var columns = Mathf.Max(4, Mathf.RoundToInt(span / 2.2f) / 2 * 2);
                    for (var index = 0; index < columns; index++)
                    {
                        var x = -span / 2 + index * span / (columns - 1);
                        if (Mathf.Abs(x) < 1.3f) continue;
                        Part(root, "Column", new Vector3(x, (h + 0.5f) / 2 + 0.16f, d / 2 + 0.9f), new Vector3(0.55f, (h + 0.5f) / 2, 0.55f), Hex("ECE6D8"), true, PrimitiveType.Cylinder);
                        Part(root, "Column base", new Vector3(x, 0.26f, d / 2 + 0.9f), new Vector3(0.8f, 0.2f, 0.8f), Hex("DDD5C4"), false);
                    }
                    Part(upper, "Entablature", new Vector3(0, h + 0.75f, d / 2 + 0.75f), new Vector3(span + 1.2f, 0.7f, 1.8f), Hex("ECE6D8"), false);
                    for (var index = 0; index < 5; index++)
                    {
                        var height = 0.32f;
                        Part(upper, "Pediment", new Vector3(0, h + 1.26f + index * height, d / 2 + 0.75f), new Vector3((span + 1.2f) * (1 - index / 5f), height, 1.6f), Hex("ECE6D8"), false);
                    }
                    Part(upper, "Cornice", new Vector3(0, top + 0.18f, 0), new Vector3(w + 0.5f, 0.36f, d + 0.5f), Hex("ECE6D8"), false);
                    break;
                }
                case "glass":
                case "modern":
                    Part(upper, "Roof plant", new Vector3(w / 4, top + 0.7f, -d / 4), new Vector3(3, 1.2f, 2.2f), Hex("8E959B"), false);
                    Part(upper, "Parapet cap", new Vector3(0, top + 0.12f, 0), new Vector3(w + 0.3f, 0.24f, d + 0.3f), trim, false);
                    break;
                case "brick":
                case "balcony":
                    Part(upper, "Cornice", new Vector3(0, top + 0.2f, 0), new Vector3(w + 0.45f, 0.4f, d + 0.45f), trim, false);
                    Part(upper, "Roof plant", new Vector3(-w / 4, top + 0.6f, -d / 4), new Vector3(1.6f, 0.8f, 1.6f), Hex("8E959B"), false);
                    break;
            }
        }

        private static void Home()
        {
            var home = Building("Your home", "家", new Vector3(-30, 0, -17.5f), 0, 12, 11, 3.6f,
                Hex("E8DBC4"), Hex("6B4A34"), Hex("B58659"), Hex("6E5446"), 3f, Hex("FFE1BA"), "house", 1);
            var r = home.Root;
            Part(r, "Living room rug", new Vector3(-2.5f, 0.17f, 0.4f), new Vector3(3.6f, 0.02f, 2.8f), Hex("C7AE88"), false);
            Wood(r, "TV cabinet", new Vector3(-2.5f, 0.41f, -4.95f), new Vector3(2.2f, 0.5f, 0.45f), Hex("5E4130"), true);
            Part(r, "Television", new Vector3(-2.5f, 1.3f, -5.08f), new Vector3(1.9f, 1.05f, 0.06f), Dark, false);
            Prop(r, "World7296399367", new Vector3(-2.5f, 0.16f, 2.3f), 180, 2.1f, true);
            Prop(r, "World32a2d6275e", new Vector3(-2.5f, 0.16f, -0.6f), 0, 0.9f, true, 0.45f);
            foreach (var z in new[] { -4.6f, -3.6f })
                Art(r, "kaykit/kitchencounter_straight_A.gltf", new Vector3(5.3f, 0.16f, z), -90, 0.95f, true);
            Art(r, "kaykit/kitchencounter_sink.gltf", new Vector3(5.3f, 0.16f, -2.6f), -90, 0.95f, true);
            Art(r, "furniture/kitchenCoffeeMachine.glb", new Vector3(5.3f, 1.12f, -4.6f), -90, 0.42f, false);
            Part(r, "Fridge", new Vector3(5.25f, 1.06f, -1.3f), new Vector3(0.8f, 1.8f, 0.75f), Hex("E6E6E3"), true);
            Art(r, "furniture/tableRound.glb", new Vector3(2.2f, 0.16f, 2.4f), 0, 0.76f, true);
            Art(r, "furniture/chairCushion.glb", new Vector3(2.2f, 0.16f, 1.25f), 0, 0.95f, false);
            Art(r, "furniture/chairCushion.glb", new Vector3(2.2f, 0.16f, 3.55f), 180, 0.95f, false);
            Art(r, "furniture/chairCushion.glb", new Vector3(1.05f, 0.16f, 2.4f), 90, 0.95f, false);
            Art(r, "food/croissant.glb", new Vector3(2.2f, 0.93f, 2.4f), 0, 0.09f, false);
            Wood(r, "Bookshelf", new Vector3(-5.55f, 1.06f, -2.4f), new Vector3(0.4f, 1.8f, 1.8f), Hex("6B4A34"), true);
            foreach (var y in new[] { 0.75f, 1.45f }) Art(r, "furniture/books.glb", new Vector3(-5.5f, y, -2.4f), 90, 0.25f, false);
            Prop(r, "World5ec5331391", new Vector3(-5.1f, 0.16f, -4.6f), 90, 2.1f, true);
            Art(r, "furniture/pottedPlant.glb", new Vector3(-5.2f, 0.16f, 4.6f), 0, 1.3f, true);
            Art(r, "furniture/pottedPlant.glb", new Vector3(5.0f, 0.16f, 4.6f), 0, 1.3f, true);
            Art(r, "furniture/lampRoundFloor.glb", new Vector3(-4.7f, 0.16f, 3.4f), 0, 1.6f, false);
            Talker(r, "home:Mum", "Mum", "Your mum", "BlueLace", new Vector3(3.6f, 0.16f, -3.3f), 0);
            // Front garden and driveway.
            Greenery(new Vector3(-35.5f, 0, -10.5f), new Vector2(8.6f, 2.6f), 12, 0, 0);
            foreach (var x in new[] { -38.5f, -35.5f, -32.5f }) Prop(props, "World7cf1a18178", new Vector3(x, 0.02f, -10.6f), 0, 0.6f, false);
            Box("Driveway", new Vector3(-20.5f, 0.02f, -15), new Vector3(3.6f, 0.04f, 12), Hex("A9A49B"), false);
            Drivable(Car(props, "World9137831506", new Vector3(-20.5f, 0.04f, -16), 180), "family car");
        }

        private static void School()
        {
            var school = Building("Riverside School", "学校", new Vector3(-27.5f, 0, 18.5f), 180, 20, 14, 4.2f,
                Hex("BE6E52"), Hex("F0E5D0"), Hex("C6B699"), Hex("5B4D45"), 2.5f, Hex("FFF4E0"), "brick", 2);
            var r = school.Root;
            Part(r, "Whiteboard", new Vector3(0, 1.75f, -6.8f), new Vector3(6, 1.5f, 0.05f), Hex("F7F7F2"), false);
            Label(r, "Speaking practice: daily life", new Vector3(0, 2.15f, -6.76f), 180, 0.28f, Hex("2B4F7A"));
            Wood(r, "Teacher's desk", new Vector3(0, 0.54f, -4.6f), new Vector3(1.8f, 0.76f, 0.8f), Hex("7A5539"), true);
            Part(r, "Laptop", new Vector3(-0.3f, 0.96f, -4.6f), new Vector3(0.36f, 0.02f, 0.26f), Dark, false);
            foreach (var x in new[] { -6f, -2.5f, 1f, 4.5f })
                foreach (var z in new[] { -1.8f, 0.6f, 3f })
                    StudentDesk(r, new Vector3(x, 0, z));
            foreach (var z in new[] { -4f, -1.5f })
            {
                Wood(r, "Classroom shelf", new Vector3(9.5f, 1.06f, z), new Vector3(0.4f, 1.8f, 1.8f), Hex("7A5539"), true);
                Art(r, "furniture/books.glb", new Vector3(9.45f, 1.45f, z), -90, 0.25f, false);
            }
            Art(r, "furniture/pottedPlant.glb", new Vector3(-9.2f, 0.16f, -6.1f), 0, 1.4f, true);
            Talker(r, "school:Reed", "Mr Reed", "English teacher", "ProfessorReed", new Vector3(2.6f, 0.16f, -4.8f), 0);
            Talker(r, "school:Nora", "Nora", "Classmate", "Schoolgirl", new Vector3(-8.3f, 0.16f, 4.4f), 90);
            // Flagpole in the forecourt.
            Primitive("Flagpole", PrimitiveType.Cylinder, new Vector3(-40, 4, 10.4f), new Vector3(0.12f, 4, 0.12f), Steel, true);
            Box("Flag", new Vector3(-39.3f, 7.4f, 10.4f), new Vector3(1.3f, 0.8f, 0.03f), Hex("2C6CB0"), false);
        }

        private static void StudentDesk(Transform room, Vector3 at)
        {
            Wood(room, "Desk top", at + new Vector3(0, 0.88f, 0), new Vector3(1.2f, 0.05f, 0.62f), Hex("C99E6E"), true);
            foreach (var x in new[] { -0.55f, 0.55f })
                Part(room, "Desk leg", at + new Vector3(x, 0.52f, 0), new Vector3(0.05f, 0.7f, 0.55f), Steel, false);
            Part(room, "Chair seat", at + new Vector3(0, 0.62f, 0.62f), new Vector3(0.46f, 0.05f, 0.44f), Hex("3D6E8F"), false);
            Part(room, "Chair back", at + new Vector3(0, 0.93f, 0.84f), new Vector3(0.46f, 0.5f, 0.04f), Hex("3D6E8F"), false);
            Part(room, "Chair frame", at + new Vector3(0, 0.39f, 0.62f), new Vector3(0.4f, 0.46f, 0.04f), Steel, false);
        }

        private static void Hospital()
        {
            var hospital = Building("City Hospital", "医院", new Vector3(27.5f, 0, 18.5f), 180, 20, 14, 4.2f,
                Hex("EEF1F2"), Hex("2E7DA4"), Hex("DCE2E2"), Hex("8C9599"), 2.5f, Hex("F3F9FF"), "modern", 5);
            var r = hospital.Root;
            Portal(r, 14, "Hospital", "metro-hospital", "Enter City Hospital (full visit: register, see a doctor, pharmacy)");
            // Red cross on the façade.
            Part(r, "Red cross", new Vector3(-7, 3.55f, 7.06f), new Vector3(0.3f, 0.9f, 0.04f), Hex("D23A3A"), false);
            Part(r, "Red cross", new Vector3(-7, 3.55f, 7.06f), new Vector3(0.9f, 0.3f, 0.04f), Hex("D23A3A"), false);
            // Reception.
            Part(r, "Reception desk", new Vector3(5.5f, 0.71f, 3.2f), new Vector3(4, 1.1f, 0.8f), White, true);
            Part(r, "Reception top", new Vector3(5.5f, 1.29f, 3.2f), new Vector3(4.2f, 0.06f, 0.9f), Hex("8FB6C8"), false);
            Part(r, "Reception screen", new Vector3(5f, 1.55f, 2.95f), new Vector3(0.55f, 0.35f, 0.04f), Dark, false);
            Label(r, "RECEPTION", new Vector3(5.5f, 2.8f, 3.25f), 180, 0.3f, Hex("2E7DA4"));
            Talker(r, "hospital:Lily", "Nurse Lily", "Reception nurse", "NurseLily", new Vector3(5.5f, 0.16f, 2.3f), 0);
            // Waiting area.
            foreach (var z in new[] { 2.2f, 4.0f })
                for (var x = -8f; x <= -3f; x += 1f)
                {
                    Part(r, "Waiting seat", new Vector3(x, 0.6f, z), new Vector3(0.55f, 0.08f, 0.55f), Hex("4F86B5"), true);
                    Part(r, "Waiting seat back", new Vector3(x, 0.93f, z - 0.26f), new Vector3(0.55f, 0.6f, 0.05f), Hex("4F86B5"), false);
                    Part(r, "Seat frame", new Vector3(x, 0.36f, z), new Vector3(0.45f, 0.42f, 0.04f), Steel, false);
                }
            Extra(r, "Contemplation", new Vector3(-3.2f, 0.16f, 5.4f), 200);
            Art(r, "furniture/pottedPlant.glb", new Vector3(-9.2f, 0.16f, 6.1f), 0, 1.4f, true);
            Part(r, "Water cooler", new Vector3(-9.3f, 0.75f, 0.6f), new Vector3(0.4f, 1.2f, 0.4f), Hex("D9E7EE"), true);
            // Consultation room at the back, behind a partition with a doorway.
            var wall = Hex("D7E5EA");
            Part(r, "Partition", new Vector3(2f, 1.56f, -1.2f), new Vector3(3f, 2.8f, 0.12f), wall, true);
            Part(r, "Partition", new Vector3(7.43f, 1.56f, -1.2f), new Vector3(4.85f, 2.8f, 0.12f), wall, true);
            Part(r, "Partition", new Vector3(0.5f, 1.56f, -4.03f), new Vector3(0.12f, 2.8f, 5.65f), wall, true);
            Label(r, "CONSULTATION", new Vector3(4.25f, 2.6f, -1.13f), 180, 0.22f, Hex("2E7DA4"));
            Part(r, "Bed base", new Vector3(7.6f, 0.38f, -5.4f), new Vector3(1.9f, 0.45f, 0.8f), Steel, true);
            Part(r, "Mattress", new Vector3(7.6f, 0.68f, -5.4f), new Vector3(1.95f, 0.15f, 0.85f), White, false);
            Part(r, "Pillow", new Vector3(8.35f, 0.8f, -5.4f), new Vector3(0.4f, 0.1f, 0.6f), Hex("FAFAFA"), false);
            Part(r, "Privacy curtain", new Vector3(6.3f, 1.2f, -4.3f), new Vector3(0.04f, 1.8f, 2.0f), Hex("A9D3E4"), false);
            Wood(r, "Doctor's desk", new Vector3(2.6f, 0.54f, -5.9f), new Vector3(1.6f, 0.76f, 0.75f), Hex("C8BFAF"), true);
            Part(r, "Doctor's screen", new Vector3(2.6f, 1.15f, -6.1f), new Vector3(0.6f, 0.38f, 0.04f), Dark, false);
            Part(r, "Medicine cabinet", new Vector3(9.4f, 1.0f, -3.0f), new Vector3(0.6f, 1.7f, 1.4f), White, true);
            Talker(r, "hospital:Chen", "Dr Chen", "Doctor", "DrChen", new Vector3(2.6f, 0.16f, -4.7f), 0);
            Car(props, "Worldccb87cb38d", new Vector3(42, 0.04f, 25), 0);
            // Helipad on the roof of the ward block, and a red cross high on the façade.
            var upper = r.Find("Roof");
            var roofTop = 4.2f + 0.24f + 5 * 3.2f;
            Part(upper, "Helipad", new Vector3(0, roofTop + 0.1f, -1.5f), new Vector3(9, 0.12f, 9), Hex("4A5A4F"), false);
            Part(upper, "Helipad H", new Vector3(-1.1f, roofTop + 0.17f, -1.5f), new Vector3(0.6f, 0.02f, 4f), White, false);
            Part(upper, "Helipad H", new Vector3(1.1f, roofTop + 0.17f, -1.5f), new Vector3(0.6f, 0.02f, 4f), White, false);
            Part(upper, "Helipad H", new Vector3(0, roofTop + 0.17f, -1.5f), new Vector3(2.2f, 0.02f, 0.6f), White, false);
            Part(upper, "High cross", new Vector3(6.5f, roofTop - 2f, 7.06f), new Vector3(0.6f, 2f, 0.06f), Hex("D23A3A"), false);
            Part(upper, "High cross", new Vector3(6.5f, roofTop - 2f, 7.06f), new Vector3(2f, 0.6f, 0.06f), Hex("D23A3A"), false);
        }

        private static void Bank()
        {
            var bank = Building("Metro Bank", "银行", new Vector3(21, 0, 39.5f), 0, 12, 9, 3.8f,
                Hex("D9D0C1"), Hex("23395B"), Hex("CDC4B2"), Hex("6F6A66"), 2f, Hex("FFF1DA"), "classic", 2);
            var r = bank.Root;
            Part(r, "Bank counter", new Vector3(0, 0.71f, -1.0f), new Vector3(9, 1.1f, 0.7f), Hex("23395B"), true);
            Wood(r, "Counter top", new Vector3(0, 1.29f, -1.0f), new Vector3(9.2f, 0.06f, 0.8f), Hex("D7C29C"), false);
            Glass(r, new Vector3(0, 1.75f, -1.0f), new Vector3(9, 0.85f, 0.03f), false);
            Talker(r, "bank:Patel", "Mr Patel", "Bank teller", "Executive", new Vector3(0.8f, 0.16f, -2.1f), 0);
            Extra(r, "Secretary", new Vector3(-2.8f, 0.16f, -2.1f), 0);
            for (var x = -2.5f; x <= 2.5f; x += 1.25f)
                Part(r, "Queue post", new Vector3(x, 0.6f, 1.4f), new Vector3(0.08f, 0.5f, 0.08f), Steel, false, PrimitiveType.Cylinder);
            Part(r, "Queue belt", new Vector3(0, 0.95f, 1.4f), new Vector3(5, 0.05f, 0.03f), Hex("8B1E2D"), false);
            Prop(r, "Worldd3083043ca", new Vector3(-4.6f, 0.16f, 2.6f), 90, 1.0f, true);
            Art(r, "furniture/pottedPlant.glb", new Vector3(5.1f, 0.16f, 3.6f), 0, 1.3f, true);
            Prop(props, "World137e0654fe", new Vector3(29.5f, 0.02f, 44.4f), 180, 0, true, 2.2f);
            Label(props, "ATM", new Vector3(29.5f, 2.6f, 45.4f), 180, 0.25f, Hex("23395B"));
        }

        private static void Cafe()
        {
            var cafe = Building("Sunny Cup Cafe", "咖啡店", new Vector3(18, 0, -17), 0, 12, 10, 3.6f,
                Hex("F1E2C8"), Hex("2E5E4E"), Hex("A67750"), Hex("54443A"), 3f, Hex("FFD8A4"), "brick", 2);
            var r = cafe.Root;
            Wood(r, "Coffee counter", new Vector3(0, 0.71f, -3.0f), new Vector3(6, 1.1f, 0.8f), Hex("6B4A34"), true);
            Part(r, "Counter top", new Vector3(0, 1.29f, -3.0f), new Vector3(6.2f, 0.06f, 0.9f), Hex("E9E2D5"), false);
            Art(r, "furniture/kitchenCoffeeMachine.glb", new Vector3(-1.6f, 1.32f, -3.1f), 0, 0.5f, false);
            Art(r, "food/cup-coffee.glb", new Vector3(0.2f, 1.32f, -2.8f), 0, 0.11f, false);
            Art(r, "food/cup-tea.glb", new Vector3(0.6f, 1.32f, -2.8f), 0, 0.11f, false);
            Art(r, "food/croissant.glb", new Vector3(1.8f, 1.32f, -2.9f), 0, 0.08f, false);
            Art(r, "food/cookie.glb", new Vector3(2.2f, 1.32f, -2.9f), 0, 0.03f, false);
            Part(r, "Menu board", new Vector3(0, 2.35f, -4.82f), new Vector3(3.2f, 1.2f, 0.05f), Hex("1F2A26"), false);
            Label(r, "COFFEE  3.50\nLATTE   4.20\nTEA     3.00\nSMOOTHIE  5.00", new Vector3(0, 2.35f, -4.78f), 180, 0.2f, Hex("F5EBD8"));
            Talker(r, "cafe:Maya", "Maya", "Barista", "CafeCharm", new Vector3(0.6f, 0.16f, -3.9f), 0);
            foreach (var table in new[] { new Vector3(-3.6f, 0.16f, 0.4f), new Vector3(-3.6f, 0.16f, 3.3f), new Vector3(3.3f, 0.16f, 0.9f), new Vector3(3.3f, 0.16f, 3.6f) })
            {
                Art(r, "furniture/tableRound.glb", table, 0, 0.76f, true);
                Art(r, "furniture/chairCushion.glb", table + new Vector3(0, 0, -1.1f), 0, 0.95f, false);
                Art(r, "furniture/chairCushion.glb", table + new Vector3(0, 0, 1.1f), 180, 0.95f, false);
            }
            Prop(r, "Worlda6733bd590", new Vector3(-3.6f, 0.93f, 0.4f), 0, 0.12f, false);
            Talker(r, "cafe:Alex", "Alex", "Regular customer", "Elliot", new Vector3(-1.6f, 0.16f, 2.0f), 90);
            Art(r, "furniture/pottedPlant.glb", new Vector3(-5.2f, 0.16f, -4.2f), 0, 1.4f, true);
            Art(r, "furniture/pottedPlant.glb", new Vector3(5.2f, 0.16f, -4.2f), 0, 1.4f, true);
            for (var index = 0; index < 10; index++)
                Part(r, "Awning stripe", new Vector3(-4.5f + index, 2.95f, 5.7f), new Vector3(1f, 0.08f, 1.4f), index % 2 == 0 ? Hex("2E5E4E") : Hex("F1E2C8"), false);
        }

        private static void Restaurant()
        {
            var restaurant = Building("Harbour Table Restaurant", "餐厅", new Vector3(36, 0, -17.5f), 0, 15, 11, 3.8f,
                Hex("8E3B2E"), Hex("F1E2C8"), Hex("77502F"), Hex("4F3B33"), 3f, Hex("FFCE92"), "brick", 2);
            var r = restaurant.Root;
            Portal(r, 11, "Restaurant", "metro-restaurant", "Enter Harbour Table (full dinner: host, menu, order, bill)");
            var dishes = new[] { "World469a8db931", "Worldec5fff3a67", "World9910d1884c" };
            var dish = 0;
            foreach (var x in new[] { -4.5f, 0f, 4.5f })
                foreach (var z in new[] { 0.2f, 3.2f })
                {
                    var table = new Vector3(x, 0.16f, z);
                    Art(r, "furniture/tableRound.glb", table, 0, 0.76f, true);
                    Art(r, "furniture/chairCushion.glb", table + new Vector3(0, 0, -1.1f), 0, 0.95f, false);
                    Art(r, "furniture/chairCushion.glb", table + new Vector3(0, 0, 1.1f), 180, 0.95f, false);
                    if ((x + z) % 2 < 1) Prop(r, dishes[dish++ % dishes.Length], table + new Vector3(0, 0.77f, 0), 0, dish % 2 == 0 ? 0.3f : 0.25f, false);
                    var pendant = Part(r, "Pendant lamp", table + new Vector3(0, 2.3f, 0), Vector3.one * 0.3f, White, false, PrimitiveType.Sphere);
                    pendant.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
                }
            Part(r, "Kitchen pass", new Vector3(0, 0.71f, -3.7f), new Vector3(8, 1.1f, 0.7f), Hex("B9B2A6"), true);
            Part(r, "Steel pass top", new Vector3(0, 1.29f, -3.7f), new Vector3(8.2f, 0.06f, 0.8f), Hex("C9CDD1"), false);
            Art(r, "kaykit/dishrack_plates.gltf", new Vector3(-2f, 1.32f, -3.7f), 0, 0.35f, false);
            Label(r, "KITCHEN", new Vector3(0, 2.6f, -5.32f), 180, 0.3f, Hex("F1E2C8"));
            Prop(r, "Worldfcb6bfff5e", new Vector3(5.6f, 0.16f, -3.2f), 200, 2.0f, true);
            Talker(r, "restaurant:June", "June", "Waitress", "MidnightLace", new Vector3(-2.2f, 0.16f, -1.6f), 0);
            Talker(r, "restaurant:Sam", "Sam", "Diner", "Plaid", new Vector3(6.3f, 0.16f, 3.2f), -90);
        }

        // ------------------------------------------------------------------ outdoor places
        private static void Market()
        {
            Box("Market square", new Vector3(-15, 0.01f, -21), new Vector3(10, 0.02f, 18), Hex("C9B99E"), false);
            Prop(props, "Worlda96c51bc37", new Vector3(-16, 0.02f, -15.6f), 0, 2.5f, true);
            Prop(props, "World9a2c4cefe5", new Vector3(-13, 0.02f, -23), 0, 3.0f, true);
            Prop(props, "World4e0d38de4b", new Vector3(-18.5f, 0.02f, -21), 0, 1.0f, false);
            Talker(props, "market:Rose", "Rose", "Fruit seller", "Rose", new Vector3(-16, 0.05f, -13.3f), 0);
        }

        private static void Park()
        {
            Greenery(new Vector3(-27.5f, 0, 37), new Vector2(36, 16), 11, 0, 0);
            Primitive("Fountain basin", PrimitiveType.Cylinder, new Vector3(-30, 0.25f, 37), new Vector3(6, 0.25f, 6), Hex("B7B1A6"), true);
            Primitive("Fountain water", PrimitiveType.Cylinder, new Vector3(-30, 0.5f, 37), new Vector3(5.4f, 0.02f, 5.4f), Hex("5B8FA8"), false);
            Primitive("Fountain column", PrimitiveType.Cylinder, new Vector3(-30, 1.2f, 37), new Vector3(0.5f, 1.2f, 0.5f), Hex("B7B1A6"), true);
            Primitive("Fountain bowl", PrimitiveType.Cylinder, new Vector3(-30, 2.2f, 37), new Vector3(1.8f, 0.1f, 1.8f), Hex("B7B1A6"), false);
            foreach (var (x, z, yaw) in new[] { (-22f, 33f, 0f), (-22f, 41f, 180f), (-38f, 33f, 0f), (-38f, 41f, 180f) })
            {
                var bench = new GameObject("Park bench").transform;
                bench.position = new Vector3(x, 0, z);
                bench.rotation = Quaternion.Euler(0, yaw, 0);
                Wood(bench, "Bench seat", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.06f, 0.45f), Hex("8A6244"), true);
                Wood(bench, "Bench back", new Vector3(0, 0.75f, -0.22f), new Vector3(1.8f, 0.4f, 0.05f), Hex("8A6244"), false);
                foreach (var side in new[] { -0.8f, 0.8f }) Part(bench, "Bench leg", new Vector3(side, 0.22f, 0), new Vector3(0.06f, 0.45f, 0.4f), Dark, false);
            }
            foreach (var tree in new[] { new Vector3(-42, 0, 43), new Vector3(-14, 0, 43), new Vector3(-42, 0, 31), new Vector3(-13, 0, 31) })
                EnvironmentArt.Tree(tree, 9f, true);
            Prop(props, "World91344a2923", new Vector3(-27.6f, 0.52f, 37.6f), 30, 0.5f, false);
            Prop(props, "Worlde693d06e6c", new Vector3(-31.8f, 0.52f, 36.2f), 200, 0.5f, false);
            Prop(props, "World8017170c0c", new Vector3(-20.5f, 0.02f, 35.5f), 140, 0.5f, false);
            Talker(props, "park:Kai", "Kai", "Skateboarder", "SkateboardBoy", new Vector3(-19.5f, 0.05f, 38.5f), 180);
            Label(props, "CENTRAL PARK", new Vector3(-27.5f, 2.2f, 45.3f), 0, 0.3f, Hex("2E5E3A"));
        }

        private static void Skyline()
        {
            // Real-scale towers around the edge of the city, plus a few apartment blocks inside the blocks.
            var towers = new[] { ("World43ca06749a", 90f), ("Worldd4b0331077", 38f), ("World6402de282c", 46f), ("World05569d27b2", 70f),
                ("Worldd415a7bf75", 18f), ("World5280fc283a", 17f), ("World8ee8934c46", 32f), ("Worldfe4fdca1c3", 20f) };
            var index = 0;
            foreach (var side in new[] { -1, 1 })
                for (var along = -130f; along <= 130f; along += 26f)
                {
                    var (id, height) = towers[index++ % towers.Length];
                    Prop(props, id, new Vector3(along, 0, side * 140f), side > 0 ? 180 : 0, 24f, false, height);
                    var (otherId, otherHeight) = towers[index++ % towers.Length];
                    Prop(props, otherId, new Vector3(side * 140f, 0, along + 13f), side > 0 ? 270 : 90, 24f, false, otherHeight);
                }
            Prop(props, "Worldd415a7bf75", new Vector3(28, 0, -37), 0, 16f, true, 16f);
            Prop(props, "World5280fc283a", new Vector3(-30, 0, -37), 0, 16f, true, 16f);
            Prop(props, "Worldfe4fdca1c3", new Vector3(40, 0, 38.5f), 0, 11f, true, 14f);
        }

        // ------------------------------------------------------------------ more of the city
        private static readonly Color[] Goods = { Hex("D9534F"), Hex("F0AD4E"), Hex("5CB85C"), Hex("5BC0DE"), Hex("F7F1E3"), Hex("8E6BBF"), Hex("E8D35A") };

        /// Shop shelving with rows of coloured goods (supermarket, pharmacy, library books, post office parcels).
        private static void Shelf(Transform room, Vector3 at, float length, float yaw, float height, Color frame, Color[] goods, float depth = 0.5f)
        {
            var shelf = new GameObject("Shelf").transform;
            shelf.SetParent(room, false);
            shelf.localPosition = at;
            shelf.localRotation = Quaternion.Euler(0, yaw, 0);
            Wood(shelf, "Shelf back", new Vector3(0, height / 2 + 0.16f, -depth / 2 + 0.03f), new Vector3(length, height, 0.05f), frame, true);
            var rows = Mathf.Max(2, Mathf.RoundToInt(height / 0.45f));
            for (var row = 0; row < rows; row++)
            {
                var y = 0.2f + row * height / rows;
                Wood(shelf, "Shelf board", new Vector3(0, y, 0), new Vector3(length, 0.04f, depth), frame, false);
                var index = row * 3;
                for (var x = -length / 2 + 0.3f; x < length / 2 - 0.2f; x += 0.55f)
                    Part(shelf, "Goods", new Vector3(x, y + 0.15f, 0.02f), new Vector3(0.48f, 0.26f, depth * 0.7f), goods[index++ % goods.Length], false);
            }
        }

        private static void Counter(Transform room, Vector3 at, float length, Color body, Color top)
        {
            Part(room, "Counter", at + new Vector3(0, 0.55f, 0), new Vector3(length, 1.1f, 0.75f), body, true);
            Part(room, "Counter top", at + new Vector3(0, 1.13f, 0), new Vector3(length + 0.15f, 0.06f, 0.85f), top, false);
        }

        private static void Board(Transform room, Vector3 at, float yaw, Vector2 size, string text, Color glow)
        {
            var root = new GameObject("Display").transform;
            root.SetParent(room, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(root, "Display panel", Vector3.zero, new Vector3(size.x, size.y, 0.06f), Dark, false);
            Label(root, text, new Vector3(0, 0, 0.04f), 180, 0.16f, glow);
        }

        private static void Station()
        {
            var station = Building("Central Station", "中央车站", new Vector3(-82.5f, 0, 19), 180, 22, 15, 5.2f,
                Hex("B9B4A8"), Hex("2E4057"), Hex("CFC9BD"), Hex("5D6064"), 2.5f, Hex("F4F6FF"), "classic", 1);
            var r = station.Root;
            Portal(r, 15, "ForestSlice", "metro-station", "Take the train to the Forest Café", -4.5f);
            Portal(r, 15, "Airport", "metro-station-airport", "Take the Airport Express to Metro City International Airport", 4.5f);
            Portal(r, 15, "TropicalBeach", "metro-station-beach", "Take the Coastal Line to Palm Bay beach resort", 0.01f);
            Portal(r, 15, "WildlifeZoo", "metro-station-zoo", "Take the Zoo Line to Metro City Wildlife Zoo", 8.5f);
            Counter(r, new Vector3(5, 0.16f, -3.5f), 6, Hex("2E4057"), Hex("D9D2C5"));
            Glass(r, new Vector3(5, 1.9f, -3.5f), new Vector3(6, 0.9f, 0.03f), false);
            Talker(r, "transport:Ben", "Ben", "Station assistant", "UrbanTrench", new Vector3(5.6f, 0.16f, -4.5f), 0);
            foreach (var x in new[] { -2.5f, -1.2f })
            {
                Part(r, "Ticket machine", new Vector3(x, 0.96f, -6.7f), new Vector3(0.9f, 1.6f, 0.6f), Hex("1F6FB2"), true);
                var screen = Part(r, "Ticket screen", new Vector3(x, 1.25f, -6.38f), new Vector3(0.5f, 0.35f, 0.02f), White, false);
                screen.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
            }
            Board(r, new Vector3(-6.5f, 3.2f, -7.1f), 0, new Vector2(5.5f, 1.6f), "DEPARTURES\nT1 Harbour .... 2 min\nBUS 12 Ring ... 5 min\nT1 Central .... 9 min", Hex("FFB347"));
            foreach (var z in new[] { 0.5f, 3f })
                for (var x = -8f; x <= -2f; x += 3f)
                    Wood(r, "Waiting bench", new Vector3(x, 0.6f, z), new Vector3(2.4f, 0.08f, 0.5f), Hex("8A6244"), true);
            Label(r, "TRAM  T1  ·  BUS 12", new Vector3(0, 4.3f, 7.62f), 180, 0.3f, Hex("FFD27A"));
            // Clock tower beside the entrance hall.
            Part(r, "Clock tower", new Vector3(13.3f, 9f, 3.5f), new Vector3(3.6f, 18f, 3.6f), Hex("A9A294"), true);
            Part(r, "Tower cap", new Vector3(13.3f, 18.4f, 3.5f), new Vector3(4.2f, 0.8f, 4.2f), Hex("2E4057"), false);
            var face = Part(r, "Clock face", new Vector3(13.3f, 15.2f, 5.33f), new Vector3(2.4f, 0.05f, 2.4f), Hex("F5F1E6"), false, PrimitiveType.Cylinder);
            face.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var hour = Part(r, "Hour hand", new Vector3(13.3f, 15.45f, 5.4f), new Vector3(0.1f, 0.6f, 0.04f), Dark, false);
            hour.transform.localRotation = Quaternion.Euler(0, 0, 60);
            Part(r, "Minute hand", new Vector3(13.3f, 15.6f, 5.42f), new Vector3(0.08f, 0.9f, 0.04f), Dark, false);
        }

        private static void Library()
        {
            var library = Building("City Library", "图书馆", new Vector3(82.5f, 0, 18.5f), 180, 18, 14, 4.6f,
                Hex("C9B79C"), Hex("3B2F2A"), Hex("A97F55"), Hex("4A3C34"), 2.5f, Hex("FFE9C4"), "classic", 1);
            var r = library.Root;
            var books = new[] { Hex("7A2E2E"), Hex("2E4A7A"), Hex("2E6B4A"), Hex("C9A227"), Hex("5A3E7A"), Hex("B5651D") };
            foreach (var x in new[] { -6f, -2f, 2f, 6f })
                Shelf(r, new Vector3(x, 0, -4.6f), 3.2f, 0, 2.3f, Hex("5E4130"), books, 0.4f);
            Shelf(r, new Vector3(-8.6f, 0, -2.5f), 4f, 90, 2.3f, Hex("5E4130"), books, 0.4f);
            foreach (var x in new[] { -3.5f, 3.5f })
            {
                Wood(r, "Reading table", new Vector3(x, 0.92f, 1.2f), new Vector3(3, 0.06f, 1.2f), Hex("8A6244"), true);
                Part(r, "Table legs", new Vector3(x, 0.54f, 1.2f), new Vector3(2.7f, 0.7f, 0.08f), Hex("5E4130"), false);
                foreach (var dx in new[] { -0.9f, 0.9f })
                    foreach (var side in new[] { -1, 1 })
                        Art(r, "furniture/chairCushion.glb", new Vector3(x + dx, 0.16f, 1.2f + side * 0.95f), side < 0 ? 0 : 180, 0.95f, false);
                Art(r, "furniture/books.glb", new Vector3(x, 0.96f, 1.2f), 0, 0.2f, false);
            }
            Counter(r, new Vector3(6, 0.16f, 3.6f), 3, Hex("5E4130"), Hex("D9C6A2"));
            Talker(r, "library:Grace", "Ms Grace", "Librarian", "QuietMorning", new Vector3(6, 0.16f, 2.8f), 0);
            Art(r, "furniture/lampRoundFloor.glb", new Vector3(-8, 0.16f, 5.5f), 0, 1.6f, false);
        }

        private static void Cinema()
        {
            var cinema = Building("Starlight Cinema", "电影院", new Vector3(82.5f, 0, -18), 0, 18, 13, 5f,
                Hex("2B2D42"), Hex("E8B04B"), Hex("5A2A2A"), Hex("1E1F2B"), 3f, Hex("FFD9A0"), "modern", 1);
            var r = cinema.Root;
            Counter(r, new Vector3(-3, 0.16f, -2.5f), 5, Hex("8B1E2D"), Hex("E8B04B"));
            Part(r, "Popcorn machine", new Vector3(-5, 1.6f, -2.6f), new Vector3(0.7f, 0.9f, 0.6f), Hex("C0392B"), false);
            Part(r, "Popcorn", new Vector3(-5, 1.55f, -2.6f), new Vector3(0.6f, 0.4f, 0.5f), Hex("F5E6B3"), false);
            Talker(r, "cinema:Leo", "Leo", "Ticket seller", "SmilingBoy", new Vector3(-2.6f, 0.16f, -3.4f), 0);
            Board(r, new Vector3(-3, 3.3f, -6.3f), 0, new Vector2(6, 1.4f), "NOW SHOWING\n18:30  Ocean Light\n20:45  The Last Stop", Hex("FFD27A"));
            var posters = new[] { Hex("1F6FB2"), Hex("C0392B"), Hex("27AE60"), Hex("8E44AD") };
            for (var index = 0; index < 4; index++)
                Part(r, "Film poster", new Vector3(8.8f, 2f, -4.5f + index * 2.4f), new Vector3(0.05f, 1.6f, 1.1f), posters[index], false);
            Part(r, "Screen 1 doors", new Vector3(4.5f, 1.3f, -6.35f), new Vector3(2.2f, 2.4f, 0.1f), Hex("5A2A2A"), true);
            Label(r, "SCREEN 1", new Vector3(4.5f, 2.9f, -6.28f), 180, 0.22f, Hex("E8B04B"));
            foreach (var x in new[] { 2f, 5f })
                Prop(r, "World7f1f92cd97", new Vector3(x, 0.16f, 2.5f), 180, 1.5f, true);
            // Marquee canopy and a vertical blade sign, like a real picture house.
            Part(r, "Marquee", new Vector3(0, 3.1f, 7.9f), new Vector3(9, 0.35f, 2.6f), Hex("1E1F2B"), false);
            var bulbs = Part(r, "Marquee lights", new Vector3(0, 2.9f, 9.2f), new Vector3(9, 0.12f, 0.06f), White, false);
            bulbs.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
            Label(r, "NOW SHOWING", new Vector3(0, 3.1f, 9.23f), 180, 0.2f, Hex("E8B04B"));
            Part(r, "Blade sign", new Vector3(7.8f, 6.2f, 6.9f), new Vector3(0.3f, 4.2f, 1.3f), Hex("8B1E2D"), false);
            Label(r, "S\nT\nA\nR\nS", new Vector3(7.62f, 6.2f, 6.9f), 90, 0.26f, Hex("FFE7A3"));
        }

        private static void Supermarket()
        {
            var market = Building("FreshMart Supermarket", "超市", new Vector3(-82.5f, 0, -18), 0, 22, 13, 4.4f,
                Hex("EDEDE8"), Hex("2E8B57"), Hex("D8D8D2"), Hex("6E7377"), 3f, Hex("F7FBFF"), "modern", 0);
            var r = market.Root;
            foreach (var x in new[] { -6f, -1f, 4f })
            {
                Shelf(r, new Vector3(x, 0, -1.6f), 4.2f, 90, 1.8f, Hex("C9CDD1"), Goods);
                Shelf(r, new Vector3(x + 0.55f, 0, -1.6f), 4.2f, 270, 1.8f, Hex("C9CDD1"), Goods);
            }
            Shelf(r, new Vector3(0, 0, -6.0f), 18, 0, 2.0f, Hex("C9CDD1"), Goods);
            Prop(r, "Worlda96c51bc37", new Vector3(8.5f, 0.16f, -3.5f), 270, 2.5f, true);
            foreach (var x in new[] { -6f, -2.5f })
            {
                Counter(r, new Vector3(x, 0.16f, 3.6f), 2.2f, Hex("2E8B57"), Hex("D8D8D2"));
                Part(r, "Till", new Vector3(x + 0.6f, 1.35f, 3.6f), new Vector3(0.35f, 0.3f, 0.3f), Dark, false);
            }
            Talker(r, "market:Mia", "Mia", "Cashier", "PeachGala", new Vector3(-6, 0.16f, 2.7f), 0);
            for (var index = 0; index < 4; index++)
                Part(r, "Trolley", new Vector3(8.5f + (index % 2) * 0.1f, 0.55f, 3.5f + index * 0.25f), new Vector3(0.6f, 0.7f, 0.9f), Steel, false);
        }

        private static void Police()
        {
            var police = Building("Police Station", "警察局", new Vector3(-82.5f, 0, 72.5f), 180, 18, 14, 4.4f,
                Hex("D5DCE3"), Hex("1C3D6E"), Hex("BFC6CC"), Hex("44505C"), 1.5f, Hex("F2F6FF"), "brick", 2);
            var r = police.Root;
            Counter(r, new Vector3(0, 0.16f, -1.5f), 7, Hex("1C3D6E"), Hex("D9D2C5"));
            Glass(r, new Vector3(0, 1.9f, -1.5f), new Vector3(7, 0.9f, 0.03f), false);
            Talker(r, "police:James", "Officer James", "Police officer", "OfficerJames", new Vector3(0.8f, 0.16f, -2.4f), 0);
            Part(r, "Notice board", new Vector3(-8.75f, 1.8f, 2), new Vector3(0.05f, 1.2f, 2.4f), Hex("B08A5A"), false);
            for (var index = 0; index < 5; index++)
                Part(r, "Notice", new Vector3(-8.72f, 1.6f + (index % 2) * 0.45f, 1.1f + index * 0.42f), new Vector3(0.02f, 0.35f, 0.3f), White, false);
            for (var x = -6f; x <= -3f; x += 1f)
                Part(r, "Waiting seat", new Vector3(x, 0.6f, 4.5f), new Vector3(0.55f, 0.08f, 0.55f), Hex("1C3D6E"), true);
            Car(props, "Worldccb87cb38d", new Vector3(-96, 0.04f, 70), 90);
            Label(r, "POLICE", new Vector3(5.5f, 3.7f, 7.12f), 180, 0.3f, White);
        }

        private static void Pharmacy()
        {
            var pharmacy = Building("City Pharmacy", "药房", new Vector3(-27.5f, 0, 70.5f), 180, 12, 10, 3.8f,
                Hex("F2F4F2"), Hex("1E8A4C"), Hex("E2E5E2"), Hex("6E7377"), 1.5f, Hex("F4FFF6"), "balcony", 3);
            var r = pharmacy.Root;
            var boxes = new[] { White, Hex("A8D5BA"), Hex("F2C6C2"), Hex("BFD7EA"), Hex("F7E7A1") };
            Shelf(r, new Vector3(0, 0, -4.5f), 9, 0, 2.1f, White, boxes, 0.35f);
            Shelf(r, new Vector3(-5.5f, 0, -0.5f), 4, 90, 1.6f, White, boxes, 0.35f);
            Counter(r, new Vector3(1.5f, 0.16f, -2.2f), 4, White, Hex("1E8A4C"));
            Talker(r, "pharmacy:Morgan", "Ms Morgan", "Pharmacist", "DrMorgan", new Vector3(1.5f, 0.16f, -3.1f), 0);
            // Green cross outside.
            Part(r, "Green cross", new Vector3(4.5f, 3.4f, 5.12f), new Vector3(0.25f, 0.75f, 0.06f), Hex("27C46A"), false).GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
            Part(r, "Green cross", new Vector3(4.5f, 3.4f, 5.12f), new Vector3(0.75f, 0.25f, 0.06f), Hex("27C46A"), false);
        }

        private static void Gym()
        {
            var gym = Building("FitLife Gym", "健身房", new Vector3(27.5f, 0, 72), 180, 18, 13, 4.4f,
                Hex("3A3F44"), Hex("F05A28"), Hex("2F3337"), Hex("23272B"), 1.5f, Hex("F7FBFF"), "glass", 2);
            var r = gym.Root;
            for (var x = -7f; x <= -1f; x += 2f)
            {
                Part(r, "Treadmill base", new Vector3(x, 0.3f, -4.5f), new Vector3(0.8f, 0.25f, 1.9f), Dark, true);
                Part(r, "Treadmill console", new Vector3(x, 1.25f, -5.3f), new Vector3(0.8f, 0.35f, 0.1f), Hex("4A4F55"), false);
                Part(r, "Treadmill rail", new Vector3(x, 0.8f, -5.25f), new Vector3(0.75f, 0.9f, 0.06f), Steel, false);
            }
            Glass(r, new Vector3(0, 1.6f, -6.33f), new Vector3(16, 2.2f, 0.03f), false); // mirror wall
            for (var x = 2f; x <= 7f; x += 2.5f)
            {
                Part(r, "Weight bench", new Vector3(x, 0.45f, -1.5f), new Vector3(0.4f, 0.12f, 1.4f), Hex("F05A28"), true);
                Part(r, "Barbell", new Vector3(x, 1.25f, -1.9f), new Vector3(1.8f, 0.05f, 0.05f), Steel, false, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            Part(r, "Dumbbell rack", new Vector3(8.4f, 0.55f, 2), new Vector3(0.5f, 0.8f, 3), Dark, true);
            for (var z = 0.8f; z < 3.3f; z += 0.4f)
                Part(r, "Dumbbell", new Vector3(8.4f, 1.0f, z), new Vector3(0.12f, 0.12f, 0.3f), Hex("4A4F55"), false);
            foreach (var x in new[] { -6f, -3.5f })
                Part(r, "Yoga mat", new Vector3(x, 0.17f, 2.5f), new Vector3(0.65f, 0.01f, 1.8f), Hex("3BB273"), false);
            Talker(r, "gym:Max", "Coach Max", "Fitness coach", "SoccerCaptain", new Vector3(-4.5f, 0.16f, 0.5f), 0);
        }

        private static void Ramen()
        {
            var ramen = Building("Sakura Ramen", "日本料理", new Vector3(82.5f, 0, 71), 180, 14, 11, 3.8f,
                Hex("E9DCC6"), Hex("7A1F1F"), Hex("8A6244"), Hex("3A2F2A"), 1.5f, Hex("FFD8A0"), "pagoda", 0);
            var r = ramen.Root;
            Wood(r, "Ramen bar", new Vector3(0, 0.55f, -3), new Vector3(8, 1.1f, 0.7f), Hex("C9A36B"), true);
            for (var x = -3.5f; x <= 3.5f; x += 1.15f)
            {
                Part(r, "Bar stool", new Vector3(x, 0.42f, -2.2f), new Vector3(0.38f, 0.06f, 0.38f), Hex("7A1F1F"), false, PrimitiveType.Cylinder);
                Part(r, "Stool post", new Vector3(x, 0.25f, -2.2f), new Vector3(0.06f, 0.2f, 0.06f), Steel, false, PrimitiveType.Cylinder);
            }
            Prop(r, "Worldec5fff3a67", new Vector3(-1, 1.1f, -3), 0, 0.25f, false);
            Prop(r, "Worldec5fff3a67", new Vector3(1.5f, 1.1f, -3), 0, 0.25f, false);
            foreach (var x in new[] { -3.5f, 3.5f })
            {
                Art(r, "furniture/tableRound.glb", new Vector3(x, 0.16f, 2), 0, 0.76f, true);
                Art(r, "furniture/chairCushion.glb", new Vector3(x, 0.16f, 0.9f), 0, 0.95f, false);
                Art(r, "furniture/chairCushion.glb", new Vector3(x, 0.16f, 3.1f), 180, 0.95f, false);
            }
            for (var x = -5f; x <= 5f; x += 2.5f)
            {
                var lantern = Part(r, "Paper lantern", new Vector3(x, 2.9f, 0), new Vector3(0.45f, 0.6f, 0.45f), Hex("E8443A"), false, PrimitiveType.Sphere);
                lantern.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
            }
            Talker(r, "japan:Ren", "Ren", "Restaurant host", "OpenArms", new Vector3(-5, 0.16f, 0.5f), 0);
        }

        private static void Hotel()
        {
            var hotel = Building("Grand Plaza Hotel", "酒店", new Vector3(-27.5f, 0, -72), 0, 18, 13, 5f,
                Hex("E6DCCB"), Hex("8C6A3F"), Hex("D8CBB4"), Hex("4F4A44"), 1.5f, Hex("FFE6C2"), "balcony", 8);
            var r = hotel.Root;
            Counter(r, new Vector3(-3, 0.16f, -3.8f), 5, Hex("8C6A3F"), Hex("F2EAD9"));
            Talker(r, "hotel:Sophie", "Sophie", "Receptionist", "VirtualGuide", new Vector3(-3, 0.16f, -4.7f), 0);
            foreach (var x in new[] { 4.5f, 7.2f })
            {
                Part(r, "Lift doors", new Vector3(x, 1.3f, -6.33f), new Vector3(1.4f, 2.4f, 0.06f), Steel, false);
                Part(r, "Lift light", new Vector3(x, 2.75f, -6.32f), new Vector3(0.5f, 0.15f, 0.04f), White, false).GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
            }
            Prop(r, "Worlde31deb29a7", new Vector3(-5.5f, 0.16f, 2.5f), 90, 2.1f, true);
            Prop(r, "Worldd3083043ca", new Vector3(-2.5f, 0.16f, 3.5f), 210, 1.0f, true);
            Prop(r, "World32a2d6275e", new Vector3(-3.6f, 0.16f, 2.4f), 0, 0.9f, true, 0.45f);
            Part(r, "Lobby rug", new Vector3(-4, 0.17f, 2.6f), new Vector3(4.5f, 0.02f, 3.4f), Hex("8B3A3A"), false);
            Part(r, "Luggage", new Vector3(1.8f, 0.5f, -2.8f), new Vector3(0.45f, 0.7f, 0.25f), Hex("2E4057"), false);
            Art(r, "furniture/pottedPlant.glb", new Vector3(8.2f, 0.16f, 5.5f), 0, 1.6f, true);
        }

        private static void PostOffice()
        {
            var post = Building("Post Office", "邮局", new Vector3(27.5f, 0, -70.5f), 0, 13, 10, 3.8f,
                Hex("E9D9B6"), Hex("C0392B"), Hex("D3C7AC"), Hex("5A4E44"), 1.5f, Hex("FFF1DA"), "classic", 1);
            var r = post.Root;
            Counter(r, new Vector3(0, 0.16f, -2), 7, Hex("C0392B"), Hex("E9E2D5"));
            Glass(r, new Vector3(0, 1.85f, -2), new Vector3(7, 0.85f, 0.03f), false);
            Talker(r, "post:Omar", "Omar", "Postal clerk", "Ali", new Vector3(0.7f, 0.16f, -2.9f), 0);
            var cardboard = new[] { Hex("B98B57"), Hex("A67C4E"), Hex("C79C68") };
            Shelf(r, new Vector3(0, 0, -4.5f), 8, 0, 2.0f, Hex("7A6A5A"), cardboard, 0.45f);
            Part(r, "Scales", new Vector3(-2, 1.2f, -2), new Vector3(0.45f, 0.08f, 0.45f), Steel, false);
            // Red post box on the pavement.
            Primitive("Post box", PrimitiveType.Cylinder, new Vector3(33.5f, 0.75f, -63.2f), new Vector3(0.55f, 0.6f, 0.55f), Hex("C0392B"), true);
            Primitive("Post box cap", PrimitiveType.Sphere, new Vector3(33.5f, 1.35f, -63.2f), new Vector3(0.58f, 0.3f, 0.58f), Hex("C0392B"), false);
        }

        private static void Mall()
        {
            var mall = Building("Willow Mall", "购物中心", new Vector3(82.5f, 0, -73), 0, 22, 14, 5.2f,
                Hex("DCD6CC"), Hex("6B5B95"), Hex("EFEBE4"), Hex("4F4A55"), 1.5f, Hex("FFF4E6"), "glass", 2);
            var r = mall.Root;
            var clothes = new[] { Hex("2E4A7A"), Hex("C0392B"), Hex("F2E6D0"), Hex("3A3A3A"), Hex("8E6BBF"), Hex("D4A017") };
            for (var x = -8f; x <= -1f; x += 3.5f)
            {
                Part(r, "Clothes rail", new Vector3(x, 1.45f, -1.5f), new Vector3(2.4f, 0.04f, 0.04f), Steel, false);
                foreach (var post in new[] { -1.15f, 1.15f }) Part(r, "Rail post", new Vector3(x + post, 0.8f, -1.5f), new Vector3(0.04f, 1.3f, 0.04f), Steel, false);
                for (var dx = -1f; dx <= 1f; dx += 0.25f)
                    Part(r, "Garment", new Vector3(x + dx, 1.05f, -1.5f), new Vector3(0.06f, 0.75f, 0.5f), clothes[Mathf.Abs(Mathf.RoundToInt(dx * 4 + x)) % clothes.Length], false);
            }
            Prop(r, "Worlde1dbf59367", new Vector3(-6, 0.16f, -6.2f), 0, 2.0f, true);
            Prop(r, "World895cd8babc", new Vector3(-2.5f, 0.16f, -6.2f), 0, 2.2f, true);
            foreach (var x in new[] { 5f, 7.5f })
            {
                Part(r, "Fitting room", new Vector3(x, 1.2f, -5.2f), new Vector3(2.2f, 2.2f, 0.06f), Hex("6B5B95"), true);
                Part(r, "Fitting curtain", new Vector3(x, 1.1f, -4.0f), new Vector3(2.0f, 2.0f, 0.04f), Hex("B9A6D9"), false);
            }
            Counter(r, new Vector3(5, 0.16f, 2.5f), 3.5f, Hex("6B5B95"), Hex("EFEBE4"));
            Talker(r, "mall:Ellie", "Ellie", "Clothing adviser", "BraidedSchoolgirl", new Vector3(-4.5f, 0.16f, 1), 180);
        }

        private static void Apartments()
        {
            Prop(props, "Worldfe4fdca1c3", new Vector3(-92, 0, -92), 0, 18f, true, 18f);
            Prop(props, "Worldd415a7bf75", new Vector3(-71, 0, -94), 0, 14f, true, 22f);
            Box("Playground", new Vector3(-74, 0.01f, -70), new Vector3(12, 0.02f, 8), Hex("D9A86C"), false);
            Primitive("Slide", PrimitiveType.Cube, new Vector3(-74, 0.9f, -70), new Vector3(0.8f, 0.1f, 3.5f), Hex("E8443A"), true).transform.rotation = Quaternion.Euler(28, 0, 0);
            foreach (var x in new[] { -78f, -70f })
            {
                Primitive("Swing frame", PrimitiveType.Cube, new Vector3(x, 2.2f, -72), new Vector3(2.6f, 0.1f, 0.1f), Hex("1F6FB2"), false);
                foreach (var side in new[] { -1.25f, 1.25f }) Primitive("Swing post", PrimitiveType.Cube, new Vector3(x + side, 1.1f, -72), new Vector3(0.1f, 2.2f, 0.1f), Hex("1F6FB2"), true);
            }
        }

        // ------------------------------------------------------------------ tram and bus
        private static readonly (float x, string name)[] TramStops =
        {
            (-91f, "Central Station"), (-21f, "Home & Market"), (10f, "Hospital & Cafe"), (95f, "Library & Cinema"),
        };

        private static void Tramway()
        {
            // Embedded track in the middle of Main Street, with overhead line poles.
            Box("Tram track bed", new Vector3(0, 0.054f, 0), new Vector3(Edge * 2, 0.01f, 3.0f), Hex("8E8A84"), false);
            foreach (var z in new[] { -0.72f, 0.72f })
                Box("Tram rail", new Vector3(0, 0.07f, z), new Vector3(Edge * 2, 0.03f, 0.08f), Hex("6D7378"), false);
            for (var x = -Edge + 10; x < Edge; x += 32)
            {
                if (Lines.Any(line => Mathf.Abs(x - line) < 7)) continue;
                foreach (var z in new[] { -4.4f, 4.4f })
                    Primitive("Overhead line pole", PrimitiveType.Cylinder, new Vector3(x, 3.1f, z), new Vector3(0.18f, 3.1f, 0.18f), Hex("3A4046"), true);
                Box("Span wire", new Vector3(x, 6.1f, 0), new Vector3(0.03f, 0.03f, 8.8f), Hex("2A2E33"), false);
            }
            Box("Contact wire", new Vector3(0, 6.05f, 0), new Vector3(Edge * 2, 0.02f, 0.02f), Hex("2A2E33"), false);
            foreach (var (x, name) in TramStops)
                foreach (var side in new[] { -1, 1 })
                    Shelter(new Vector3(x, 0.15f, side * 7.6f), side > 0 ? 180 : 0, "T1 TRAM", name, Hex("1E6B3A"));
        }

        /// A glass shelter with a bench and a stop sign; it faces the road (local +Z).
        private static void Shelter(Vector3 at, float yaw, string line, string name, Color colour)
        {
            var stop = new GameObject("Stop · " + name).transform;
            stop.position = at;
            stop.rotation = Quaternion.Euler(0, yaw, 0);
            Glass(stop, new Vector3(0, 1.3f, -0.9f), new Vector3(3.6f, 2.2f, 0.05f), true);
            Part(stop, "Shelter roof", new Vector3(0, 2.5f, -0.3f), new Vector3(3.9f, 0.1f, 1.5f), Steel, false);
            foreach (var x in new[] { -1.85f, 1.85f }) Part(stop, "Shelter post", new Vector3(x, 1.25f, -0.85f), new Vector3(0.08f, 2.5f, 0.08f), Steel, true);
            Wood(stop, "Shelter bench", new Vector3(0, 0.45f, -0.55f), new Vector3(2.6f, 0.08f, 0.45f), Hex("8A6244"), true);
            Part(stop, "Stop pole", new Vector3(2.3f, 1.4f, 0.4f), new Vector3(0.06f, 2.8f, 0.06f), Steel, true, PrimitiveType.Cylinder);
            Part(stop, "Stop plate", new Vector3(2.3f, 2.6f, 0.4f), new Vector3(1.2f, 0.6f, 0.04f), colour, false);
            Label(stop, line + "\n" + name, new Vector3(2.3f, 2.6f, 0.43f), 180, 0.1f, White);
        }

        /// One of the Blender-made vehicles (Assets/Art/city), already at real size.
        private static GameObject Vehicle(string file, Transform parent, Vector3 position, Vector3 forward)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/city/" + file);
            if (prefab == null) { Debug.LogWarning("Metro City: " + file + " is missing from Assets/Art/city."); return null; }
            var root = new GameObject(file.Replace(".glb", ""));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(root.transform, false);
            // The headlights mark the front: make sure it points along local +Z whatever the importer's axis convention.
            var lights = model.GetComponentsInChildren<Renderer>().Where(item => item.sharedMaterials.Any(material => material != null && material.name.StartsWith("Headlight"))).ToArray();
            if (lights.Length > 0 && lights.Average(item => root.transform.InverseTransformPoint(item.bounds.center).z) < 0)
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var bounds = Measure(model);
            model.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            bounds = Measure(model);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
            root.transform.rotation = Quaternion.LookRotation(forward);
            foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
            root.AddComponent<Rigidbody>().isKinematic = true;
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0, 0.45f, 1f);
            return root;
        }

        private static void PublicTransport(Vector3[] junctions)
        {
            var group = new GameObject("Public transport").transform;
            // Tram T1 along the boulevard, out and back.
            var tramPath = new[] { new Vector3(-104, 0.06f, 0), new Vector3(104, 0.06f, 0) };
            var tramObject = Vehicle("CityTram.glb", group, new Vector3(-60, 0.06f, 0), Vector3.right);
            if (tramObject != null)
            {
                var tram = tramObject.AddComponent<CityTransit>();
                tram.LineName = "Tram T1";
                tram.Shuttle = true;
                tram.Path = tramPath;
                tram.Stops = TramStops.Select(stop => new Vector3(stop.x, 0.06f, 0)).ToArray();
                tram.StopNames = TramStops.Select(stop => stop.name).ToArray();
                tram.ObeySignals = false;   // trams have priority at the crossings
                tram.Cruise = 9f;
                tram.Dwell = 10f;
                tram.HalfLength = 12f;
                tram.ExitOffset = new Vector3(7f, 0, 0);
                tram.BothSides = true;
                tram.Seat = tramObject.transform.Find("Seat");
            }
            // Bus 12 around the inner ring, anticlockwise, with eight stops on the kerb side.
            var corners = new[] { new Vector2(-55, -55), new Vector2(55, -55), new Vector2(55, 55), new Vector2(-55, 55) };
            var busPath = Lane(corners);
            var stops = new List<Vector3>();
            var names = new List<string>();
            void BusStop(int side, float along, string name)
            {
                var a = corners[side];
                var b = corners[(side + 1) % corners.Length];
                var direction = (b - a).normalized;
                var right = new Vector3(direction.y, 0, -direction.x);
                var centre = new Vector3(a.x, 0.06f, a.y) + new Vector3(direction.x, 0, direction.y) * along;
                var stop = centre + right * 2.5f;
                stops.Add(stop);
                names.Add(name);
                var shelter = stop + right * 5.1f;
                var facing = Quaternion.LookRotation(-right).eulerAngles.y;
                Shelter(new Vector3(shelter.x, 0.15f, shelter.z), facing, "BUS 12", name, Hex("B22222"));
            }
            BusStop(0, 15f, "Grand Plaza Hotel");
            BusStop(0, 70f, "Post Office");
            BusStop(1, 15f, "Starlight Cinema");
            BusStop(1, 70f, "City Library");
            BusStop(2, 15f, "FitLife Gym");
            BusStop(2, 70f, "City Pharmacy");
            BusStop(3, 15f, "Central Station");
            BusStop(3, 70f, "FreshMart Supermarket");
            foreach (var fraction in new[] { 0.1f, 0.6f })
            {
                var start = Along(busPath, fraction);
                var heading = Along(busPath, fraction + 0.005f) - start;
                var busObject = Vehicle("CityBus.glb", group, start, heading.normalized);
                if (busObject == null) continue;
                var bus = busObject.AddComponent<CityTransit>();
                bus.LineName = "Bus 12";
                bus.Path = busPath;
                bus.Stops = stops.ToArray();
                bus.StopNames = names.ToArray();
                bus.Junctions = junctions;
                bus.Cruise = 8f;
                bus.Dwell = 9f;
                bus.HalfLength = 6f;
                bus.ExitOffset = new Vector3(4.6f, 0, 0);
                bus.Seat = busObject.transform.Find("Seat");
            }
        }

        // ------------------------------------------------------------------ traffic and people
        private static Vector3[] Lane(params Vector2[] corners)
        {
            // Drive on the right: offset each road 2.5 m to the right of the direction of travel.
            Vector3 Right(Vector2 direction) => new Vector3(direction.y, 0, -direction.x);
            var points = new Vector3[corners.Length];
            for (var index = 0; index < corners.Length; index++)
            {
                var previous = corners[(index + corners.Length - 1) % corners.Length];
                var current = corners[index];
                var next = corners[(index + 1) % corners.Length];
                var arriving = (current - previous).normalized;
                var leaving = (next - current).normalized;
                points[index] = new Vector3(current.x, 0.06f, current.y) + Right(arriving) * 2.5f + Right(leaving) * 2.5f;
            }
            return points;
        }

        private static Vector3 Along(Vector3[] path, float fraction)
        {
            var lengths = path.Select((point, index) => Vector3.Distance(point, path[(index + 1) % path.Length])).ToArray();
            var target = lengths.Sum() * fraction;
            for (var index = 0; index < path.Length; index++)
            {
                if (target <= lengths[index]) return Vector3.Lerp(path[index], path[(index + 1) % path.Length], target / lengths[index]);
                target -= lengths[index];
            }
            return path[0];
        }

        private static void Traffic(Vector3[] junctions)
        {
            // Cars use the ring roads and the cross streets, never the tram boulevard (z = 0).
            var outer = Lane(new Vector2(-110, -110), new Vector2(110, -110), new Vector2(110, 110), new Vector2(-110, 110));
            var outerBack = Lane(new Vector2(-110, 110), new Vector2(110, 110), new Vector2(110, -110), new Vector2(-110, -110));
            var innerBack = Lane(new Vector2(-55, 55), new Vector2(55, 55), new Vector2(55, -55), new Vector2(-55, -55));
            var north = Lane(new Vector2(0, 55), new Vector2(0, 110), new Vector2(-55, 110), new Vector2(-55, 55));
            var south = Lane(new Vector2(0, -55), new Vector2(0, -110), new Vector2(55, -110), new Vector2(55, -55));
            var cars = new[]
            {
                ("Worldb32547cd57", outer, 0.05f), ("World5bb93c86ab", outer, 0.45f), ("Worldccb87cb38d", outer, 0.75f),
                ("Worldd1bcc6966b", outerBack, 0.2f), ("World97c2bb4723", outerBack, 0.65f),
                ("Worlda4706333a0", innerBack, 0.3f), ("Worldaa90aa5c09", innerBack, 0.8f),
                ("World9137831506", north, 0.4f), ("World90827b67d0", south, 0.1f),
            };
            var group = new GameObject("Traffic").transform;
            foreach (var (id, path, fraction) in cars)
            {
                var start = Along(path, fraction);
                var car = Car(group, id, start, 0);
                if (car == null) continue;
                car.isStatic = false;
                foreach (var child in car.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
                var body = car.AddComponent<Rigidbody>();
                body.isKinematic = true;
                var drive = car.AddComponent<CityCar>();
                drive.Path = path;
                drive.Junctions = junctions;
                drive.Cruise = 6.5f + fraction * 2;
            }
            // Blender-made vehicles with livery: two city cabs and the FreshMart delivery van.
            foreach (var (file, path, fraction) in new[] { ("CityTaxi.glb", outer, 0.3f), ("CityTaxi.glb", innerBack, 0.55f), ("FreshMartVan.glb", outerBack, 0.42f) })
            {
                var start = Along(path, fraction);
                var heading = (Along(path, fraction + 0.004f) - start).normalized;
                var vehicle = Vehicle(file, group, start, heading);
                if (vehicle == null) continue;
                var drive = vehicle.AddComponent<CityCar>();
                drive.Path = path;
                drive.Junctions = junctions;
                drive.Cruise = 7.5f;
            }
            // Parked: a van at the supermarket loading bay, a taxi rank outside the station.
            Drivable(Vehicle("FreshMartVan.glb", group, new Vector3(-97, 0.02f, -28), Vector3.forward), "delivery van");
            Drivable(Vehicle("CityTaxi.glb", group, new Vector3(-66, 0.02f, 22), Vector3.back), "taxi");
            Drivable(Vehicle("CityTaxi.glb", group, new Vector3(-66, 0.02f, 28), Vector3.back), "taxi");
            // A small car park by the mall with cars you can borrow.
            Box("Car park", new Vector3(99, 0.03f, -83.5f), new Vector3(8, 0.04f, 28), Hex("55595E"), false);
            var parked = new[] { "Worldec2199483e", "Worldf412ab1c19", "Worldce470abc08", "World0a9f786f02" };
            for (var index = 0; index < parked.Length; index++)
            {
                Box("Parking line", new Vector3(99, 0.055f, -90.5f + index * 7), new Vector3(7, 0.01f, 0.12f), Paint, false);
                Drivable(Car(group, parked[index], new Vector3(99, 0.06f, -94 + index * 7), 90), "car");
            }
        }

        private static void Pedestrians()
        {
            Vector3[] Loop(float x0, float x1, float z0, float z1) => new[]
            {
                new Vector3(x0, 0.2f, z0), new Vector3(x1, 0.2f, z0), new Vector3(x1, 0.2f, z1), new Vector3(x0, 0.2f, z1),
            };
            var walks = new[]
            {
                ("Autumn", Loop(-48, -7, -48, -7), 0), ("Urban", Loop(-48, -7, -48, -7), 2),
                ("TrenchElder", Loop(-48, -7, 7, 48), 1), ("AnimeDetective", Loop(-48, -7, 7, 48), 3),
                ("Adrian", Loop(7, 48, 7, 48), 0), ("MaidStone", Loop(7, 48, 7, 48), 2),
                ("OfficerMason", Loop(7, 48, -48, -7), 1), ("OldCardigan", Loop(7, 48, -48, -7), 3),
                ("OfficerTaylor", Loop(-62, 62, -62, 62), 0),
                ("Plaid", Loop(-103, -62, 7, 48), 1), ("SkaterBoy", Loop(62, 103, 7, 48), 2),
                ("Hooded", Loop(62, 103, -48, -7), 0), ("SchoolgirlCasual", Loop(-103, -62, -48, -7), 3),
                ("Archeologist", Loop(-48, -7, 62, 103), 1), ("ConfidentBoy", Loop(7, 48, -103, -62), 2),
                ("Rider", Loop(-117, 117, -117, 117), 1),
            };
            var group = new GameObject("Passers-by").transform;
            var number = 0;
            foreach (var (model, path, start) in walks)
            {
                var route = number++ % 2 == 0 ? path : path.Reverse().ToArray();
                var position = Vector3.Lerp(route[start % route.Length], route[(start + 1) % route.Length], 0.35f);
                var root = new GameObject("Passer-by · " + model);
                root.transform.SetParent(group, false);
                root.transform.position = position;
                var body = MeshyCharacters.Create(model, position);
                body.transform.SetParent(root.transform, true);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.gameObject.isStatic = false;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.enabled = false; // enabled by the walker at Start, once the NavMesh is registered
                agent.radius = 0.3f;
                agent.height = 1.7f;
                agent.speed = 1.25f;
                var walker = root.AddComponent<CityPedestrian>();
                walker.Speed = 1.15f + (number % 3) * 0.12f;
                walker.Path = route;
            }
        }


        private static void Drivable(GameObject car, string label)
        {
            if (car == null) return;
            car.isStatic = false;
            foreach (var child in car.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
            if (car.GetComponent<Rigidbody>() == null) car.AddComponent<Rigidbody>().isKinematic = true;
            var drive = car.AddComponent<CityDrivable>();
            drive.Label = label;
            if (label == "delivery van") { drive.MaxSpeed = 13; drive.SeatLocal = new Vector3(-0.45f, 0.5f, -1.9f); drive.ExitLocal = new Vector3(-2.5f, 0.2f, -1.9f); }
        }

        private static readonly string[] Flowers = { "World7cf1a18178", "World150e01e147", "Worldd15bbc1f46", "World9639cbf96b", "World78400ab10b", "World72bd7bfe04", "Worldc9433b7e59", "World809335ca37" };
        private static readonly (string id, float height)[] Trees = { ("World334a09eb1f", 10f), ("Worldeb1977a5e5", 9f), ("World3b20cbbf8c", 7f) };
        private static readonly string[] Loungers = { "Dawn", "Hooded", "SkaterBoy", "Contemplation", "QuietMorning", "SchoolgirlCasual", "SmilingBoy", "Amael", "WhispersPiano" };
        private static int lounger;

        /// A lawn planted with the downloaded grass, flowers and trees, with benches and people relaxing.
        private static void Greenery(Vector3 centre, Vector2 size, int seed, int trees, int people)
        {
            var random = new System.Random(seed);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
            Box("Lawn", centre + new Vector3(0, 0.012f, 0), new Vector3(size.x, 0.02f, size.y), Lawn, false);
            for (var x = -size.x / 2 + 3; x < size.x / 2 - 2; x += 6)
                for (var z = -size.y / 2 + 3; z < size.y / 2 - 2; z += 6)
                    EnvironmentArt.GrassPatch(centre + new Vector3(x + Range(-1.5f, 1.5f), 0.02f, z + Range(-1.5f, 1.5f)), Range(0, 360));
            var flowers = Mathf.RoundToInt(size.x * size.y / 45f);
            for (var index = 0; index < flowers; index++)
            {
                var id = Flowers[random.Next(Flowers.Length)];
                var spot = centre + new Vector3(Range(-size.x / 2 + 1, size.x / 2 - 1), 0.02f, Range(-size.y / 2 + 1, size.y / 2 - 1));
                for (var cluster = 0; cluster < 3; cluster++)
                    Prop(props, id, spot + new Vector3(Range(-0.7f, 0.7f), 0, Range(-0.7f, 0.7f)), Range(0, 360), id == "World809335ca37" ? 0.9f : 0.55f, false);
            }
            for (var index = 0; index < trees; index++)
            {
                var (id, height) = Trees[random.Next(Trees.Length)];
                var spot = centre + new Vector3(Range(-size.x / 2 + 3, size.x / 2 - 3), 0.02f, Range(-size.y / 2 + 3, size.y / 2 - 3));
                if (Prop(props, id, spot, Range(0, 360), height * 0.9f, false, height) != null)
                    Primitive("Tree trunk", PrimitiveType.Cylinder, spot + Vector3.up * 1.5f, new Vector3(0.45f, 1.5f, 0.45f), Hex("6B4A34"), true).GetComponent<Renderer>().enabled = false;
            }
            for (var index = 0; index < people; index++)
            {
                var spot = centre + new Vector3(Range(-size.x / 3, size.x / 3), 0.03f, Range(-size.y / 3, size.y / 3));
                var bench = new GameObject("Lawn bench").transform;
                bench.position = spot;
                bench.rotation = Quaternion.Euler(0, Range(0, 360), 0);
                Wood(bench, "Bench seat", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.06f, 0.45f), Hex("8A6244"), true);
                Wood(bench, "Bench back", new Vector3(0, 0.75f, -0.22f), new Vector3(1.8f, 0.4f, 0.05f), Hex("8A6244"), false);
                foreach (var side in new[] { -0.8f, 0.8f }) Part(bench, "Bench leg", new Vector3(side, 0.22f, 0), new Vector3(0.06f, 0.45f, 0.4f), Dark, false);
                Extra(bench, Loungers[lounger++ % Loungers.Length], new Vector3(1.6f, 0, 0.6f), 0);
            }
            Butterflies(centre + Vector3.up * 1.2f, new Vector3(size.x * 0.8f, 1.6f, size.y * 0.8f));
        }

        private static void Butterflies(Vector3 centre, Vector3 area)
        {
            var system = new GameObject("Butterflies").AddComponent<ParticleSystem>();
            system.transform.position = centre;
            var main = system.main;
            main.startLifetime = 9f;
            main.startSpeed = 0.25f;
            main.startSize = 0.11f;
            main.maxParticles = 30;
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("F2C14E"), Hex("E8F0FF"));
            var emission = system.emission;
            emission.rateOverTime = 3f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.6f;
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = CozyMaterials.Glow();
        }

        // ------------------------------------------------------------------ helpers
        private static GameObject Part(Transform parent, string name, Vector3 local, Vector3 size, Color colour, bool collide, PrimitiveType shape = PrimitiveType.Cube)
        {
            var item = Primitive(name, shape, Vector3.zero, size, colour, collide);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = local;
            item.transform.localRotation = Quaternion.identity;
            return item;
        }

        private static GameObject Wood(Transform parent, string name, Vector3 local, Vector3 size, Color colour, bool collide)
        {
            var item = Part(parent, name, local, size, colour, collide);
            item.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Wood(colour);
            return item;
        }

        private static GameObject Glass(Transform parent, Vector3 local, Vector3 size, bool collide)
        {
            var item = Part(parent, "Glass", local, size, Hex("CFE6EA"), collide);
            item.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glass();
            return item;
        }

        private static void Invisible(string name, Vector3 position, Vector3 size)
        {
            var wall = Box(name, position, size, CurbStone, true);
            wall.GetComponent<Renderer>().enabled = false;
        }

        private static void Label(Transform parent, string text, Vector3 local, float localYaw, float scale, Color colour)
        {
            var sign = new GameObject("Sign · " + text.Split('\n')[0]).AddComponent<TextMesh>();
            sign.text = text;
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.fontSize = 64;
            sign.characterSize = scale * 0.1f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.color = colour;
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = local;
            sign.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
            sign.GetComponent<Renderer>().sharedMaterial = sign.font.material;
            sign.gameObject.AddComponent<WorldSign>();
        }

        /// A verified asset from Assets/Art, placed in a building's local frame.
        private static GameObject Art(Transform parent, string file, Vector3 local, float localYaw, float height, bool collide)
        {
            var item = Model(file, parent.TransformPoint(local), parent.eulerAngles.y + localYaw, height, collide);
            item.transform.SetParent(parent, true);
            return item;
        }

        private static Bounds Measure(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(item.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// A downloaded (Meshy) model at real-world size: `size` is its largest dimension in metres,
        /// or, with `height` set, its height (still capped by `size` when both are given).
        /// The model stands on `local` whatever its export pivot.
        private static GameObject Prop(Transform parent, string id, Vector3 local, float localYaw, float size, bool solid, float height = 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + id + ".prefab");
            if (prefab == null) { Debug.LogWarning("Metro City: scenery " + id + " is missing; skipped."); return null; }
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = local;
            item.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
            var bounds = Measure(item);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest < 0.0001f) return item;
            var scale = height > 0 ? height / Mathf.Max(0.0001f, bounds.size.y) : size / largest;
            if (height > 0 && size > 0) scale = Mathf.Min(scale, size / Mathf.Max(bounds.size.x, bounds.size.z));
            item.transform.localScale *= scale;
            bounds = Measure(item);
            var target = parent.TransformPoint(local);
            item.transform.position += new Vector3(target.x - bounds.center.x, target.y - bounds.min.y, target.z - bounds.center.z);
            if (solid)
            {
                bounds = Measure(item);
                var blocker = new GameObject("Collision · " + id);
                blocker.transform.position = bounds.center;
                blocker.AddComponent<BoxCollider>().size = bounds.size * 0.9f;
                blocker.transform.SetParent(item.transform, true);
                blocker.isStatic = true;
            }
            return item;
        }

        /// A car at real length (4.6 m) with its long axis along local Z, standing on the road.
        internal static GameObject Car(Transform parent, string id, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + id + ".prefab");
            if (prefab == null) { Debug.LogWarning("Metro City: car " + id + " is missing; skipped."); return null; }
            var root = new GameObject("Car · " + id);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(root.transform, false);
            var bounds = Measure(model);
            if (bounds.size.x > bounds.size.z) model.transform.localRotation = Quaternion.Euler(0, 90, 0);
            bounds = Measure(model);
            var length = Mathf.Max(bounds.size.x, bounds.size.z);
            if (length > 0.0001f) model.transform.localScale *= 4.6f / length;
            bounds = Measure(model);
            model.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            bounds = Measure(model);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return root;
        }

        private static void Talker(Transform parent, string sceneId, string name, string role, string model, Vector3 local, float localYaw)
        {
            var position = parent.TransformPoint(local);
            var root = new GameObject("city-" + sceneId.Replace(':', '-').ToLowerInvariant());
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0, parent.eulerAngles.y + localYaw, 0);
            var body = MeshyCharacters.Create(model, position);
            body.transform.SetParent(root.transform, true);
            body.transform.localRotation = Quaternion.identity;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.gameObject.isStatic = false;
            var npc = root.AddComponent<LearningNpc>();
            npc.SceneId = sceneId;
            npc.NameOverride = name;
            npc.RoleOverride = role;
            npc.Arrival = position + root.transform.forward * 1.4f;
            talkers.Add(root.transform);
        }

        private static void Extra(Transform parent, string model, Vector3 local, float localYaw)
        {
            var position = parent.TransformPoint(local);
            var body = MeshyCharacters.Create(model, position);
            body.name = "Visitor · " + model;
            body.transform.rotation = Quaternion.Euler(0, parent.eulerAngles.y + localYaw, 0);
        }

        /// A walk-in door to a full location scene: stand at the entrance and press E.
        private static void Portal(Transform root, float depth, string scene, string entrance, string prompt, float along = 0)
        {
            var portal = new GameObject("Portal → " + scene).AddComponent<ContextSpeak.Life.PortalDoor>();
            portal.transform.position = root.TransformPoint(new Vector3(along, 1, depth / 2 + 0.9f));
            portal.transform.rotation = root.rotation;
            portal.TargetScene = scene;
            portal.Entrance = entrance;
            portal.Prompt = prompt;
            portal.Range = along != 0 ? 1.8f : 2.2f;
            var mat = Part(root, "Entrance mat", new Vector3(along, 0.02f, depth / 2 + 0.9f), new Vector3(2.2f, 0.03f, 1.2f), Hex("3E6B57"), false);
            mat.name = "Entrance mat · " + scene;
        }

        private static void SaveAsset(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
