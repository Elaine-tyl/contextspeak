using System;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Editor
{
    /// Native regression: the authored greenhouse must not be covered by an imported LOD shell.
    public static class UploadedDesignChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Uploaded design visibility")]
        public static void Verify()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("Isolated room visibility test");
                var skin = root.AddComponent<BuildingSkin>();
                skin.Kind = "room"; skin.Plot = new Bounds(Vector3.zero, Vector3.one * 10);
                skin.Exterior = GameObject.CreatePrimitive(PrimitiveType.Cube);
                skin.Exterior.transform.SetParent(root.transform);
                var inside = GameObject.CreatePrimitive(PrimitiveType.Cube);
                inside.transform.SetParent(root.transform);
                var renderer = inside.GetComponent<Renderer>(); skin.Interior = new[] { renderer };
                skin.Refresh(Vector3.one * 100);
                Check(skin.Exterior.activeSelf && !renderer.enabled, "Normal room exterior is visible outdoors.");
                skin.Refresh(Vector3.zero);
                Check(!skin.Exterior.activeSelf && renderer.enabled, "Normal room interior is visible indoors.");
                skin.Refresh(Vector3.one * 100);
                Check(skin.Exterior.activeSelf && !renderer.enabled, "Normal room can switch back outdoors.");

                var serialized = new SerializedObject(skin);
                var option = serialized.FindProperty("interiorOnly");
                if (option != null) { option.boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                skin.Refresh(Vector3.one * 100);
                Check(!skin.Exterior.activeSelf && renderer.enabled, "Authored greenhouse keeps the imported shell inactive outdoors.");
                Check(!skin.IsInside, "An outdoor player must not count as being inside the cafe.");
                skin.Exterior.GetComponent<Renderer>().enabled = true; // LOD can re-enable a renderer, but not an inactive parent.
                skin.Refresh(Vector3.zero); skin.Refresh(Vector3.one * -100);
                Check(!skin.Exterior.activeInHierarchy && renderer.enabled, "LOD updates cannot restore the old shell.");
                Check(option != null, "The authored-room mode survives scene serialization.");
                var json = JsonUtility.ToJson(skin);
                option.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
                skin.Refresh(Vector3.one * 100);
                JsonUtility.FromJsonOverwrite(json, skin);
                skin.Refresh(Vector3.one * 100);
                Check(!skin.Exterior.activeSelf && !skin.IsInside, "Serialized mode restores visibility without changing occupancy.");
                skin.Refresh(Vector3.zero);
                Check(skin.IsInside && !skin.Exterior.activeSelf, "Physical occupancy still changes inside the authored cafe.");
                Debug.Log("Uploaded design visibility: 9 native checks passed.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }
        private static void Check(bool ok, string why)
        {
            if (!ok) throw new Exception("Uploaded design visibility: " + why);
        }
    }
}
