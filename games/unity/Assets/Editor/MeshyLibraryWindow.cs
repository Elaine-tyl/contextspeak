using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Search/select high-poly source assets without loading an entire gallery into the scene.
    public class MeshyLibraryWindow : EditorWindow
    {
        private string search = "";
        private Vector2 scroll;
        private MeshyLibrary.Entry[] items = Array.Empty<MeshyLibrary.Entry>();

        [MenuItem("ContextSpeak/Meshy/Open FBX library")]
        public static void Open() => GetWindow<MeshyLibraryWindow>("Meshy FBX Library");
        private void OnEnable()
        {
            minSize = new Vector2(760, 430);
            items = MeshyLibrary.Read();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ContextSpeak · Meshy FBX library", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{items.Length} unique models · original geometry and supplied animation clips");
            search = EditorGUILayout.TextField("Search", search);
            EditorGUILayout.HelpBox("Choose Prefab to use an asset. Preview opens one model at a time; scale changes apply only to the preview scene.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var item in items.Where(i => i.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(item.name.Replace('_', ' '), GUILayout.MinWidth(220));
                if (GUILayout.Button("FBX", GUILayout.Width(45))) Select(MeshyLibrary.Root + item.fbx);
                if (GUILayout.Button("Prefab", GUILayout.Width(60))) Select(MeshyLibrary.PrefabPath(item));
                // Loading a scene resets IMGUI layout; wait until this draw pass has finished.
                if (GUILayout.Button("Preview", GUILayout.Width(65)))
                    EditorApplication.delayCall += () => Preview(item);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Show asset folder")) Select(MeshyLibrary.Root.TrimEnd('/'));
        }

        private static void Select(string path)
        {
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static void Preview(MeshyLibrary.Entry item)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyLibrary.PrefabPath(item));
            if (prefab == null) { Debug.LogError("Build the FBX library first."); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            instance.transform.localScale *= 4 / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.001f);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            instance.transform.position -= bounds.center;
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.17f);
            camera.transform.position = new Vector3(5, 3, 7);
            camera.transform.LookAt(Vector3.zero);
            camera.fieldOfView = 35;
            var light = new GameObject("Soft key").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.color = new Color(1, 0.94f, 0.85f);
            light.transform.rotation = Quaternion.Euler(35, 155, 0);
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            const string path = "Assets/Generated/Meshy/LibraryPreview.unity";
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, Vector3.one * 5), false);
        }
    }
}
