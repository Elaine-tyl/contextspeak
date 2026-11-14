using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ContextSpeak.Editor
{
    /// Renders the sky looks to PNG files (project folder SkyTestShots/<scene>/) so they can be
    /// inspected: sunrise, the seven presets, midday and a clear→rain transition at 50 %.
    /// Each look is rendered from the scene camera and from a higher, sky-facing viewpoint.
    public static class SkyTestShots
    {
        [UnityEditor.MenuItem("ContextSpeak/Sky & Weather/Render Test Shots (open scene)")]
        public static void RenderOpen() => Render(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        [UnityEditor.MenuItem("ContextSpeak/Sky & Weather/Render Test Shots (all life scenes)")]
        public static void RenderAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var path in SkyInstaller.LifeScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Render(scene.name);
            }
        }

        public static string Render(string sceneName)
        {
            var sky = Object.FindFirstObjectByType<SkySystem>();
            if (sky == null) sky = SkyInstaller.Install();
            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera == null) { Debug.LogError("Sky test shots: no camera in " + sceneName); return null; }
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "SkyTestShots", sceneName);
            Directory.CreateDirectory(folder);
            var player = Object.FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            var focus = player != null ? player.transform.position : camera.transform.position + camera.transform.forward * 8;
            var street = (camera.transform.position, camera.transform.rotation);
            // A wide view: behind and above the player looking across the scene and up at the sky.
            var wideRotation = Quaternion.Euler(-8, camera.transform.eulerAngles.y, 0);
            var wide = (focus + Vector3.up * 3.2f - (wideRotation * Vector3.forward) * 7f, wideRotation);
            var savedPosition = camera.transform.position;
            var savedRotation = camera.transform.rotation;
            var savedTime = sky.TimeOfDay;
            var savedWeather = sky.Weather;
            var shots = new List<(string name, System.Action setup)>
            {
                ("01-sunrise-0620", () => { sky.ApplyPreset(SkyPreset.ClearMorning, true); sky.SetTime(6.33f); }),
                ("02-clear-morning", () => sky.ApplyPreset(SkyPreset.ClearMorning, true)),
                ("03-midday", () => { sky.ApplyPreset(SkyPreset.BrightAfternoon, true); sky.SetTime(12.4f); }),
                ("04-bright-afternoon", () => sky.ApplyPreset(SkyPreset.BrightAfternoon, true)),
                ("05-golden-sunset", () => sky.ApplyPreset(SkyPreset.GoldenSunset, true)),
                ("06-cloudy", () => sky.ApplyPreset(SkyPreset.Cloudy, true)),
                ("07-transition-clear-to-rain-50", () => { sky.ApplyPreset(SkyPreset.Rainy, true); sky.PreviewBlend(SkyWeather.Clear, SkyWeather.Rain, 0.5f); }),
                ("08-rainy", () => sky.ApplyPreset(SkyPreset.Rainy, true)),
                ("09-clear-night", () => sky.ApplyPreset(SkyPreset.ClearNight, true)),
                ("10-overcast-night", () => sky.ApplyPreset(SkyPreset.OvercastNight, true)),
            };
            var written = new List<string>();
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 2 };
            try
            {
                foreach (var (name, setup) in shots)
                {
                    setup();
                    foreach (var item in Object.FindObjectsByType<SkyLightSwitch>(FindObjectsSortMode.None)) item.Refresh();
                    foreach (var item in Object.FindObjectsByType<SkySurfaces>(FindObjectsSortMode.None)) item.Refresh();
                    foreach (var (suffix, pose) in new[] { ("street", street), ("wide", wide) })
                    {
                        camera.transform.SetPositionAndRotation(pose.Item1, pose.Item2);
                        var file = Path.Combine(folder, name + "-" + suffix + ".png");
                        Capture(camera, texture, file);
                        written.Add(file);
                    }
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                sky.SetTime(savedTime);
                sky.Weather = savedWeather;
                sky.ApplyNow();
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
            }
            Debug.Log("Sky test shots for " + sceneName + " written to " + folder + " (" + written.Count + " images).");
            return folder;
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
