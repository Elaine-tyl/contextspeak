using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    /// Captures actual Play-mode UIToolkit rendering. Synthetic fixtures are deliberately separate
    /// from the current scene's live HUD; fixture controls have no gameplay Command subscriber.
    [InitializeOnLoad]
    public static class ImmersiveUiCapture
    {
        private const string Key = "ContextSpeak.UIShots.";
        private static readonly string[] PreferenceKeys = {
            "contextspeak-unity-cafe-v1", "contextspeak-life-day-v1", "contextspeak-metro-city-v1",
            "contextspeak.living-stories.v1", CharacterProfiles.Key, SaveManager.Key
        };
        private static readonly Vector2Int[] Sizes = {
            new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
            new Vector2Int(1920, 1200), new Vector2Int(2520, 1080)
        };
        private static readonly string[] Screens = { "rest", "phone", "map", "speaking", "places", "journal", "inventory", "pause", "friends", "wallet", "messages", "dialogue", "translation", "correction", "menu" };
        private static readonly string[] AspectScreens = { "rest", "map", "dialogue", "correction" };
        private static string CurrentScreen => sizeIndex < 2 ? Screens[screenIndex] : AspectScreens[screenIndex];
        [Serializable] private sealed class Backup { public string path; public bool existed; public string data; }
        [Serializable] private sealed class Backups { public List<Backup> files = new List<Backup>(); public List<Backup> prefs = new List<Backup>(); }
        [Serializable] private sealed class Evidence
        {
            public bool passed;
            public string scene, method = "Play mode: scene camera + runtime UI Toolkit render texture composite", error = "";
            public string limitation = "Fixture screenshots prove rendered layout, not live speech, provider output, camera framing, or gameplay progression. Live HUD captures use the current saved scene only. Visual inspection still required.";
            public List<string> captures = new List<string>();
            public List<string> checks = new List<string>();
            public List<string> failures = new List<string>();
        }
        private static Evidence evidence;
        private static UIDocument live, fixtureDocument;
        private static SliceInterface fixture;
        private static readonly List<UIDocument> hidden = new List<UIDocument>();
        private static RenderTexture uiTexture;
        private static PanelSettings redirected;
        private static RenderTexture previousTexture;
        private static Color previousClear;
        private static bool previousClearColor;
        private static Label badge;
        private static int sizeIndex, lightIndex, screenIndex, frames;
        private static bool fixtures, prepared;
        private static double nextFrame, deadline;
        private static string folder;

        static ImmersiveUiCapture()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
        }
        [UnityEditor.MenuItem("ContextSpeak/UI/Capture UI Screenshots")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Capture starts from Edit mode after compilation finishes.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("Capture requires the current scene to be saved first; it never saves or replaces authored scenes.");
            }
            folder = Path.Combine(Directory.GetCurrentDirectory(), "UIShots", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            SessionState.SetString(Key + "Folder", folder);
            BackupProgress();
            SessionState.SetBool(Key + "Passed", false);
            SessionState.SetInt(Key + "Phase", 1);
            SessionState.SetString(Key + "Scene", SceneManager.GetActiveScene().path);
            EditorApplication.isPlaying = true;
        }
        [UnityEditor.MenuItem("ContextSpeak/UI/Cancel UI Screenshot Capture")]
        public static void Cancel()
        {
            if (SessionState.GetInt(Key + "Phase", 0) != 0) Finish("Capture cancelled; partial screenshots remain labelled.");
        }
        private static void Changed(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key + "Phase", 0) != 0)
            {
                RestoreProgress();
                SessionState.SetInt(Key + "Phase", 0);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + "Passed", false) ? 0 : 1);
            }
        }
        private static void Tick()
        {
            var phase = SessionState.GetInt(Key + "Phase", 0);
            if (phase == 0 || phase == 3 || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (phase == 1)
                {
                    folder = SessionState.GetString(Key + "Folder", "");
                    fixtures = prepared = false; sizeIndex = lightIndex = screenIndex = frames = 0;
                    live = fixtureDocument = null; fixture = null; hidden.Clear();
                    evidence = new Evidence { scene = SessionState.GetString(Key + "Scene", "") };
                    deadline = EditorApplication.timeSinceStartup + 240;
                    // Wait for the authored scene's Start methods and first runtime UI layout.
                    nextFrame = EditorApplication.timeSinceStartup + 2;
                    SessionState.SetInt(Key + "Phase", 2);
                    return;
                }
                if (evidence == null) { Finish("Scripts reloaded during capture; rerun after compilation settles."); return; }
                if (EditorApplication.timeSinceStartup > deadline) { Finish("Timed out waiting for runtime rendering."); return; }
                if (EditorApplication.timeSinceStartup < nextFrame || Camera.main == null) return;
                if (!prepared)
                {
                    // Keep a raw native GameView overview in addition to exact-size composites.
                    var overview = ScreenCapture.CaptureScreenshotAsTexture();
                    if (overview != null)
                    {
                        File.WriteAllBytes(Path.Combine(folder, "live-gameview-overview.png"), overview.EncodeToPNG());
                        Object.DestroyImmediate(overview);
                    }
                    live = Object.FindFirstObjectByType<SliceInterface>()?.GetComponent<UIDocument>();
                    if (live == null)
                    {
                        evidence.checks.Add("LIVE HUD unavailable: current scene has no SliceInterface. Fixture screenshots are not live HUD proof.");
                        StartFixtures();
                    }
                    else BeginSurface(live, "LIVE HUD · current saved scene");
                    prepared = true;
                    Configure();
                    return;
                }
                // Runtime panels paint asynchronously. Allow two complete frame updates after any
                // target/size/screen change before reading GPU pixels or inspecting worldBound.
                if (++frames < 3) return;
                Capture();
                if (Advance()) Configure();
                else Finish("");
            }
            catch (Exception error) { Finish(error.ToString()); }
        }
        private static void StartFixtures()
        {
            EndSurface();
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (document.enabled) { hidden.Add(document); document.enabled = false; }
            fixture = new GameObject("UI SCREENSHOT FIXTURE - no gameplay commands").AddComponent<SliceInterface>();
            fixtureDocument = fixture.GetComponent<UIDocument>();
            fixtures = true; sizeIndex = lightIndex = screenIndex = 0;
            BeginSurface(fixtureDocument, "DETERMINISTIC UI FIXTURE · no progress / no provider");
        }
        private static void BeginSurface(UIDocument document, string label)
        {
            redirected = document.panelSettings;
            previousTexture = redirected.targetTexture;
            previousClear = redirected.colorClearValue;
            previousClearColor = redirected.clearColor;
            redirected.clearColor = true;
            redirected.colorClearValue = Color.clear;
            badge = new Label(label) { name = "capture-evidence-label", pickingMode = PickingMode.Ignore };
            badge.style.position = Position.Absolute; badge.style.top = 8; badge.style.left = Length.Percent(30);
            badge.style.fontSize = 13; badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(.06f, .1f, .08f, .95f);
            badge.style.paddingLeft = badge.style.paddingRight = 12;
            badge.style.paddingTop = badge.style.paddingBottom = 4;
            document.rootVisualElement.Add(badge);
        }
        private static void EndSurface()
        {
            badge?.RemoveFromHierarchy(); badge = null;
            if (redirected != null)
            {
                redirected.targetTexture = previousTexture;
                redirected.colorClearValue = previousClear;
                redirected.clearColor = previousClearColor;
            }
            redirected = null;
            if (uiTexture != null) { uiTexture.Release(); Object.DestroyImmediate(uiTexture); uiTexture = null; }
        }
        private static void Configure()
        {
            var size = Sizes[sizeIndex];
            if (uiTexture == null || uiTexture.width != size.x || uiTexture.height != size.y)
            {
                var old = uiTexture;
                uiTexture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { name = "UIShots runtime panel", antiAliasing = 1 };
                uiTexture.Create(); redirected.targetTexture = uiTexture;
                if (old != null) { old.Release(); Object.DestroyImmediate(old); }
            }
            var sky = Object.FindFirstObjectByType<SkySystem>();
            if (sky != null) { sky.AllowNight = true; sky.Paused = true; sky.AutoWeather = false; sky.ApplyPreset(lightIndex == 0 ? SkyPreset.BrightAfternoon : SkyPreset.ClearNight, true); sky.SetTime(lightIndex == 0 ? 12f : 22f); sky.ApplyNow();
                foreach (var item in Object.FindObjectsByType<SkyLightSwitch>(FindObjectsSortMode.None)) item.Refresh();
                foreach (var item in Object.FindObjectsByType<SkySurfaces>(FindObjectsSortMode.None)) item.Refresh(); }
            else throw new InvalidOperationException("Current saved scene needs SkySystem for genuine day/night captures; no artificial light substitute is used.");
            if (fixtures) fixture.Render(View(CurrentScreen, lightIndex != 0));
            badge?.BringToFront();
            frames = 0; nextFrame = EditorApplication.timeSinceStartup + .15;
        }
        private static bool Advance()
        {
            if (!fixtures)
            {
                if (++sizeIndex < 2) return true;
                sizeIndex = 0;
                if (++lightIndex < 2) return true;
                StartFixtures(); return true;
            }
            var count = sizeIndex < 2 ? Screens.Length : AspectScreens.Length; // extra HUD/map/dialogue/repeat aspect checks
            if (++screenIndex < count) return true;
            screenIndex = 0;
            if (++lightIndex < 2) return true;
            lightIndex = 0;
            return ++sizeIndex < Sizes.Length;
        }
        private static void Capture()
        {
            var screen = fixtures ? CurrentScreen : "hud";
            var name = (fixtures ? "fixture-" : "live-") + screen + "-" + (lightIndex == 0 ? "day-" : "night-") + Sizes[sizeIndex].x + "x" + Sizes[sizeIndex].y + ".png";
            var doc = fixtures ? fixtureDocument : live;
            // Native live input/state may already have an overlay open. Only the deterministic
            // resting fixture can assert the resting-HUD area in that case.
            try { evidence.checks.Add(ImmersiveUiChecks.Inspect(doc, name, fixtures && screen == "rest")); }
            catch (Exception failure) { evidence.failures.Add(name + ": " + failure.Message); }
            var camera = Camera.main;
            var world = RenderTexture.GetTemporary(uiTexture.width, uiTexture.height, 24, RenderTextureFormat.ARGB32);
            var original = camera.targetTexture;
            var originalAspect = camera.aspect;
            camera.aspect = (float)uiTexture.width / uiTexture.height;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = world };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else { camera.targetTexture = world; camera.Render(); }
                var background = Read(world);
                var overlay = Read(uiTexture);
                try
                {
                    var bottom = background.GetPixels32(); var top = overlay.GetPixels32();
                    // UI Toolkit render targets contain premultiplied-alpha colour.
                    for (var index = 0; index < bottom.Length; index++)
                    {
                        var inverse = 255 - top[index].a;
                        bottom[index] = new Color32((byte)Math.Min(255, top[index].r + bottom[index].r * inverse / 255),
                            (byte)Math.Min(255, top[index].g + bottom[index].g * inverse / 255),
                            (byte)Math.Min(255, top[index].b + bottom[index].b * inverse / 255), 255);
                    }
                    background.SetPixels32(bottom); background.Apply();
                    File.WriteAllBytes(Path.Combine(folder, name), background.EncodeToPNG());
                    evidence.captures.Add(name);
                }
                finally { Object.DestroyImmediate(background); Object.DestroyImmediate(overlay); }
            }
            finally { camera.targetTexture = original; camera.aspect = originalAspect; RenderTexture.ReleaseTemporary(world); }
        }
        private static Texture2D Read(RenderTexture texture)
        {
            var active = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply(); return image;
            }
            finally { RenderTexture.active = active; }
        }
        private static SliceView View(string screen, bool night)
        {
            var save = new SliceSave();
            var talk = screen == "dialogue" || screen == "correction" || screen == "menu" || screen == "translation";
            var correction = screen == "correction";
            return new SliceView {
                Place = "Forest Café", Time = night ? "22:00" : "12:00", Weather = "Clear", Cash = "$30", Card = "$60",
                Band = "IELTS 5 practice", BandId = "Band5", Strictness = "Guided", Hint = "", Toast = "",
                Energy = .85f, Hunger = .15f, Thirst = .2f, Quest = QuestBook.Quests[0], QuestEarned = Array.Empty<string>(),
                Talking = talk ? Partner.Rowan : Partner.None, Speaker = "Rowan", Role = "Waiter · Forest Café",
                Line = "Welcome back. What would you like to drink today?", Mic = "idle", Recognized = correction ? "Can I has a croissant?" : "",
                Status = correction ? "Repeat the corrected sentence to continue." : "Your turn", PracticeTarget = correction ? "Can I have a croissant?" : "",
                ModelAnswer = "", AiTip = "", Translation = screen == "translation",
                Glossary = screen == "translation" ? new[] { new GlossaryEntry { Phrase = "the usual", Chinese = "老样子；照常点餐", Meaning = "what you normally order", Example = "Can I get the usual?" } } : Array.Empty<GlossaryEntry>(),
                Subtitles = true, CanReplay = true,
                History = Array.Empty<ChatLine>(), Review = correction ? SpeakingCoach.Review("Can I has a croissant?", IeltsBand.Band5, true, true) : null,
                Gate = correction ? Gate.RequireRetry : Gate.Continue, MenuOpen = screen == "menu", MenuCategory = "Coffee", SelectedItem = "latte",
                Cart = save.Cart, Panel = talk || screen == "rest" ? "" : screen, Recommendation = "Ask Rowan about the daily menu.",
                Weaknesses = Array.Empty<string>(), Save = save, CharacterName = "Alex", Character = PlayerLook.Boy,
                Markers = new[] { new Vector4(1, 1, 0, 0), new Vector4(22, -6, 0, 1) }
            };
        }
        private static void Finish(string error)
        {
            if (SessionState.GetInt(Key + "Phase", 0) == 0) return;
            try
            {
                EndSurface();
                if (fixture != null) Object.DestroyImmediate(fixture.gameObject);
                foreach (var document in hidden) if (document != null) document.enabled = true;
                hidden.Clear();
                if (evidence == null) evidence = new Evidence();
                evidence.passed = error == "" && evidence.failures.Count == 0; evidence.error = error;
                SessionState.SetBool(Key + "Passed", evidence.passed);
                if (!string.IsNullOrEmpty(folder)) File.WriteAllText(Path.Combine(folder, "acceptance.json"), JsonUtility.ToJson(evidence, true));
                if (error == "" && evidence.failures.Count != 0) Debug.LogError("UI screenshots captured with " + evidence.failures.Count + " native acceptance failures. Inspect " + folder + "/acceptance.json.");
                else if (error == "") Debug.Log("UI screenshots captured: " + folder + ". Review acceptance.json and visually inspect PNGs before claiming visual acceptance.");
                else Debug.LogError("UI screenshots incomplete: " + error);
            }
            finally { SessionState.SetInt(Key + "Phase", 3); EditorApplication.isPlaying = false; }
        }
        private static void BackupProgress()
        {
            var backups = new Backups();
            foreach (var key in PreferenceKeys)
                backups.prefs.Add(new Backup { path = key, existed = PlayerPrefs.HasKey(key), data = PlayerPrefs.GetString(key, "") });
            foreach (var name in new[] { "forest-cafe-slice.json" })
            {
                var path = Path.Combine(Application.persistentDataPath, name);
                backups.files.Add(new Backup { path = path, existed = File.Exists(path), data = File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "" });
            }
            SessionState.SetBool(Key + "HadLook", PlayerPrefs.HasKey(PlayerAppearance.Preference));
            SessionState.SetInt(Key + "Look", PlayerPrefs.GetInt(PlayerAppearance.Preference, 0));
            SessionState.SetString(Key + "Progress", JsonUtility.ToJson(backups));
        }
        private static void RestoreProgress()
        {
            var backups = JsonUtility.FromJson<Backups>(SessionState.GetString(Key + "Progress", "{}"));
            if (backups == null) return;
            foreach (var item in backups.prefs)
                if (item.existed) PlayerPrefs.SetString(item.path, item.data); else PlayerPrefs.DeleteKey(item.path);
            foreach (var item in backups.files)
                if (item.existed) File.WriteAllBytes(item.path, Convert.FromBase64String(item.data)); else if (File.Exists(item.path)) File.Delete(item.path);
            if (SessionState.GetBool(Key + "HadLook", false)) PlayerPrefs.SetInt(PlayerAppearance.Preference, SessionState.GetInt(Key + "Look", 0));
            else PlayerPrefs.DeleteKey(PlayerAppearance.Preference);
            PlayerPrefs.Save();
        }
    }
}
