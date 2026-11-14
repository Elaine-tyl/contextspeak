using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Villa > Capture Visual Validation Shots
    /// Renders the villa from fixed cameras (same positions every run, so before/after compare 1:1) through
    /// the scene's real URP renderer, volumes and sky: front exterior, double-height living room, kitchen and
    /// dining, garden and pool, upper landing, night exterior. PNGs: Logs/VillaShots/<run>/<view>.png.
    public static class LuxuryVillaShots
    {
        // Blender coordinates (position, target, hour) — converted with LuxuryVillaBuilder.U.
        private static readonly (string name, float[] from, float[] to, float hour)[] Views =
        {
            ("1_front_exterior", new[] { 16f, -34f, 9f }, new[] { 0f, 6f, 4f }, 10.5f),
            ("2_living_room", new[] { -2.8f, 5.6f, 1.6f }, new[] { 1.5f, 15f, 1.6f }, 10.5f),
            ("3_kitchen_dining", new[] { 3.2f, 8.0f, 1.65f }, new[] { 9f, 14.5f, 1.0f }, 10.5f),
            ("4_garden_pool", new[] { -6f, 36.5f, 3.2f }, new[] { 3f, 18f, 2.5f }, 10.5f),
            ("5_upper_landing", new[] { 6.0f, 4.2f, 5.0f }, new[] { 0f, 7.5f, 4.0f }, 10.5f),
            ("6_night_exterior", new[] { -6f, 36.5f, 3.2f }, new[] { 3f, 18f, 2.5f }, 21.0f),
            ("7_pool_deck_player", new[] { -3f, 15.5f, 4.2f }, new[] { 2f, 23f, 0.5f }, 16.0f),
        };

        [MenuItem("ContextSpeak/Villa/Capture Visual Validation Shots")]
        public static void Capture()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first (the shots render the saved scene)."); return; }
            if (!File.Exists(LuxuryVillaBuilder.Scene)) { Debug.LogError("Build the villa first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(LuxuryVillaBuilder.Scene);
            var data = LuxuryVillaBuilder.Load();
            var door = Object.FindObjectsByType<VillaDoor>(FindObjectsSortMode.None).FirstOrDefault(d => d.Id == "Entrance");
            if (door != null)
            {
                var hinge = data.doors.First(d => d.name == "Door_Entrance").hinge;
                LuxuryVillaBuilder.Mirror = true;
                if ((door.transform.position - LuxuryVillaBuilder.U(hinge)).sqrMagnitude > 0.01f) LuxuryVillaBuilder.Mirror = false;
            }
            var run = System.DateTime.Now.ToString("yyyyMMdd-HHmm");
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "VillaShots", run);
            Directory.CreateDirectory(folder);
            var sky = Object.FindFirstObjectByType<SkySystem>();
            var startHour = SkyState.Hour;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Point).ToArray();
            var go = new GameObject("Villa shot camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62; cam.nearClipPlane = 0.05f; cam.farClipPlane = 400; cam.allowHDR = true;
            var add = cam.GetUniversalAdditionalCameraData();
            add.renderPostProcessing = true; add.requiresDepthTexture = true; add.requiresColorTexture = true;
            var main = Camera.main != null ? Camera.main.GetUniversalAdditionalCameraData() : null;
            if (main != null) { var so = new SerializedObject(add); var src = new SerializedObject(main); so.FindProperty("m_RendererIndex").intValue = src.FindProperty("m_RendererIndex").intValue; so.ApplyModifiedPropertiesWithoutUndo(); }
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var written = new List<string>();
            try
            {
                foreach (var v in Views)
                {
                    if (sky != null) sky.SetTime(v.hour);
                    // Interior/garden lights as VillaLighting would set them at this hour (edit mode has no Update).
                    var night = v.hour >= 19 || v.hour < 6.5f;
                    foreach (var l in lights) if (!l.name.StartsWith("Sun") && l.GetComponentInParent<SkySystem>() == null) l.enabled = night;
                    var from = LuxuryVillaBuilder.U(v.from); var to = LuxuryVillaBuilder.U(v.to);
                    go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
                    foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
                    cam.targetTexture = rt;
                    cam.Render(); cam.Render();              // second render: temporal effects / probes settled
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                    RenderTexture.active = null;
                    var file = Path.Combine(folder, v.name + ".png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    written.Add(file);
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
                foreach (var l in lights) if (l != null && l.name.StartsWith("Light ·")) l.enabled = false;
                if (sky != null) sky.SetTime(startHour);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
            Debug.Log("Villa validation shots (" + written.Count + "): " + folder);
            EditorUtility.RevealInFinder(folder);
        }
    }
}
