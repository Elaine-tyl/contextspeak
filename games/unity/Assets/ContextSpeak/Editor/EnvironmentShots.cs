using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static ContextSpeak.Editor.LifeBuild;
using static ContextSpeak.Editor.AirportKit;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Environment > Build Environment Benchmark: a compact test scene with grass,
    /// trees, flowers, dirt, rocks, a pond, a modern glass building, a road, a café corner, a
    /// character and a vehicle, set up with the realistic environment.
    /// ContextSpeak > Environment > Render Lighting Test Shots: renders the benchmark and the main
    /// locations at 07:00, 12:00, 17:00, sunset, 21:00 and a rainy afternoon from collision-safe
    /// eye-level and wide cameras into EnvironmentShots/<scene>/ (for inspection — not a pass/fail).
    public static class EnvironmentShots
    {
        public const string BenchmarkPath = Scenes + "/EnvironmentBenchmark.unity";
        public static readonly string[] ShotScenes =
        {
            BenchmarkPath, "Assets/Generated/ForestSlice.unity", "Assets/ContextSpeak/Scenes/TropicalBeach.unity", "Assets/Generated/MetroCity.unity",
            "Assets/ContextSpeak/Scenes/Restaurant.unity", "Assets/ContextSpeak/Scenes/Hospital.unity", "Assets/ContextSpeak/Scenes/Airport.unity", "Assets/ContextSpeak/Scenes/WildlifeZoo.unity",
        };

        [UnityEditor.MenuItem("ContextSpeak/Environment/Build Environment Benchmark")]
        public static void BuildBenchmark()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Missing.Clear();
            CafeBuild.ConfigurePipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Environment benchmark");
            Daylight("EnvironmentBenchmark");
            Paint(Box("Benchmark lawn", new Vector3(0, -0.25f, 0), new Vector3(90, 0.5f, 90), Hex("5E7A3A"), true), Hex("5E7A3A"), 0.15f);
            Paint(Box("Benchmark dirt path", new Vector3(-6, 0.01f, 0), new Vector3(2.2f, 0.04f, 60), Hex("7A6248"), true), Hex("7A6248"), 0.1f);
            Paint(Box("Benchmark road", new Vector3(0, 0.02f, -24), new Vector3(90, 0.05f, 8), Hex("3A3A3B"), true), Hex("3A3A3B"), 0.2f);
            Paint(Box("Benchmark pavement", new Vector3(0, 0.1f, -18.5f), new Vector3(90, 0.2f, 3), Hex("A8A59F"), true), Hex("A8A59F"), 0.2f);
            for (var x = -40f; x <= 40; x += 6) Box("Lane line", new Vector3(x, 0.05f, -24), new Vector3(3, 0.01f, 0.15f), Hex("E8E6DF"), false);
            // Pond: a shallow basin with water a little below the lawn.
            Paint(Box("Pond bed", new Vector3(10, -0.9f, 8), new Vector3(10, 0.2f, 7), Hex("4A3E30"), true), Hex("4A3E30"), 0.3f);
            Box("Pond water", new Vector3(10, -0.12f, 8), new Vector3(9.6f, 0.04f, 6.6f), Hex("3C5A50"), false);
            Invisible("Pond edge", new Vector3(10, 0.5f, 8), new Vector3(10, 1, 7));
            // Modern glass building with a café corner.
            Paint(Box("Building core", new Vector3(-24, 6, 14), new Vector3(14, 12, 12), Hex("C9C6BF"), true), Hex("C9C6BF"), 0.35f);
            Curtain("Building glass", new Vector3(-31, 0, 7.9f), new Vector3(-17, 0, 7.9f), 11.5f);
            Paint(Box("Café terrace pavement", new Vector3(-24, 0.06f, 4), new Vector3(14, 0.12f, 7), Hex("B8B2A6"), true), Hex("B8B2A6"), 0.25f);
            for (var i = 0; i < 3; i++)
            {
                Table(new Vector3(-29 + i * 5, 0.12f, 4), new Vector2(0.8f, 0.8f), Hex("E9E2D3"));
                Chair(new Vector3(-29 + i * 5, 0.12f, 3.2f), 0, Hex("2F5E5B"), "bench");
                Chair(new Vector3(-29 + i * 5, 0.12f, 4.8f), 180, Hex("2F5E5B"), "bench");
            }
            Npc("benchmark", "Mia", "Visitor", "BlueLace", new Vector3(-4, 0.02f, -8), 160);
            Model("Assets/Art/city/CityTaxi.glb", new Vector3(6, 0.04f, -24), 90, 1.5f, "Taxi");
            StreetLamp(new Vector3(-10, 0.2f, -17.5f), 180);
            StreetLamp(new Vector3(14, 0.2f, -17.5f), 180);
            var (player, camera) = Player(new Vector3(0, 0.02f, -12), 0);
            camera.transform.position = new Vector3(0, 1.7f, -16);
            camera.transform.rotation = Quaternion.Euler(4, 0, 0);
            SkyInstaller.Install();
            LifeBuild.Save(scene, BenchmarkPath);
            EnvironmentSetup.ConfigureCurrentScene(true);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Environment benchmark built: " + BenchmarkPath + (Missing.Count > 0 ? "\nMissing: " + string.Join(", ", Missing.Distinct()) : ""));
        }

        [UnityEditor.MenuItem("ContextSpeak/Environment/Render Lighting Test Shots")]
        public static void RenderAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetActiveScene().path;
            var written = 0;
            foreach (var path in ShotScenes)
            {
                if (!File.Exists(path)) { Debug.LogWarning("Lighting shots: " + path + " not built — skipped."); continue; }
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                written += Render(Path.GetFileNameWithoutExtension(path));
            }
            if (previous != "" && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("Lighting test shots: " + written + " images in " + Path.Combine(Directory.GetCurrentDirectory(), "EnvironmentShots") + ". (No home interior scene exists yet — not rendered.)");
        }

        [UnityEditor.MenuItem("ContextSpeak/Environment/Render Forest Café Shots")]
        public static void RenderForestCafe()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ForestGreenhouse.ScenePath)) { Debug.LogError("Forest café scene not built yet."); return; }
            EditorSceneManager.OpenScene(ForestGreenhouse.ScenePath, OpenSceneMode.Single);
            Render(Path.GetFileNameWithoutExtension(ForestGreenhouse.ScenePath));
        }

        private static (Vector3, Quaternion) SafePose(Vector3 target, Vector3 back, float height, float distance)
        {
            // Pull the camera in until it is out of walls, ceilings and vehicles.
            for (var d = distance; d > 1.2f; d -= 0.5f)
            {
                var position = target + Vector3.up * height - back * d;
                if (Physics.CheckSphere(position, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(target + Vector3.up * 1.4f, position, ~0, QueryTriggerInteraction.Ignore)) continue;
                return (position, Quaternion.LookRotation(target + Vector3.up * (1.2f + height * 0.1f) + back * 8 - position));
            }
            return (target + Vector3.up * 1.7f - back * 1.2f, Quaternion.LookRotation(back));
        }

        public static int Render(string sceneName)
        {
            Physics.SyncTransforms();
            var sky = Object.FindFirstObjectByType<SkySystem>();
            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (sky == null || camera == null) { Debug.LogError("Lighting shots: " + sceneName + " has no sky or camera."); return 0; }
            var weather = Object.FindFirstObjectByType<WeatherManager>();
            var manager = Object.FindFirstObjectByType<EnvironmentManager>();
            var wind = Object.FindFirstObjectByType<GlobalWindController>();
            var player = Object.FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            var target = player != null ? player.transform.position : camera.transform.position + camera.transform.forward * 4;
            var forward = player != null ? player.transform.forward : Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            // Player colliders would block the safety tests.
            var playerColliders = player != null ? player.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray() : new Collider[0];
            foreach (var c in playerColliders) c.enabled = false;
            var eye = SafePose(target, forward, 1.6f, 3.4f);
            var wide = SafePose(target, forward, 5.5f, 13f);
            foreach (var c in playerColliders) c.enabled = true;
            var poses = new List<(string, (Vector3, Quaternion))> { ("eye", eye), ("wide", wide) };
            foreach (var mark in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Shot · ")).OrderBy(t => t.name))
                poses.Add((mark.name.Substring(7).ToLowerInvariant().Replace(' ', '-'), (mark.position, mark.rotation)));

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "EnvironmentShots", sceneName);
            Directory.CreateDirectory(folder);
            var saved = (camera.transform.position, camera.transform.rotation, sky.TimeOfDay, sky.Weather);
            var shots = new (string name, float hour, WeatherManager.State state)[]
            {
                ("1-0700", 7.0f, WeatherManager.State.Clear), ("2-1200", 12.0f, WeatherManager.State.Clear), ("3-1700", 17.0f, WeatherManager.State.Clear),
                ("4-sunset", 18.35f, WeatherManager.State.Clear), ("5-2100", 21.0f, WeatherManager.State.ClearNight), ("6-rainy-afternoon", 15.0f, WeatherManager.State.HeavyRain),
            };
            var texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var count = 0;
            try
            {
                foreach (var (name, hour, state) in shots)
                {
                    if (weather != null) weather.Set(state, true);
                    else sky.ApplyPreset(state == WeatherManager.State.HeavyRain ? SkyPreset.Rainy : SkyPreset.ClearMorning, true);
                    sky.SetTime(hour);
                    if (state == WeatherManager.State.HeavyRain) { sky.PreviewBlend(SkyWeather.Rain, SkyWeather.Rain, 0.999f); }
                    if (manager != null) manager.ApplyNow();
                    if (wind != null) wind.Push(0);
                    Shader.SetGlobalFloat("_EnvWetness", SkyState.Wetness);
                    Shader.SetGlobalFloat("_EnvRain", SkyState.Rain);
                    foreach (var item in Object.FindObjectsByType<SkyLightSwitch>(FindObjectsSortMode.None)) item.Refresh();
                    foreach (var item in Object.FindObjectsByType<SkySurfaces>(FindObjectsSortMode.None)) item.Refresh();
                    foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) if (probe.mode == ReflectionProbeMode.Realtime) probe.RenderProbe();
                    foreach (var (suffix, pose) in poses)
                    {
                        camera.transform.SetPositionAndRotation(pose.Item1, pose.Item2);
                        Capture(camera, texture, Path.Combine(folder, name + "-" + suffix + ".png"));
                        count++;
                    }
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(saved.position, saved.rotation);
                if (weather != null) weather.Set(WeatherManager.State.Clear, true);
                sky.SetTime(saved.TimeOfDay);
                sky.Weather = saved.Weather;
                sky.ApplyNow();
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
            }
            Debug.Log("Lighting shots for " + sceneName + ": " + count + " images in " + folder);
            return count;
        }

        private static void Capture(Camera camera, RenderTexture texture, string file)
        {
            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else { camera.targetTexture = texture; camera.Render(); camera.targetTexture = null; }
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(file, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }
}
