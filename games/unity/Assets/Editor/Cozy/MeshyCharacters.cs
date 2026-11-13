using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// User-supplied models: textured, metre-scaled visual prefabs. Gameplay stays on the actor root.
    public static class MeshyCharacters
    {
        public const string Source = "Assets/_Game/Art/Meshy/";
        public const string Folder = "Assets/Generated/Meshy/";
        [Serializable] public sealed class Spec { public string id, rigSource; public float height; }
        [Serializable] private sealed class Catalog { public Spec[] models; }
        public static Spec[] Specs => JsonUtility.FromJson<Catalog>("{\"models\":" + File.ReadAllText(Source + "manifest.json") + "}").models;
        public static string[] Names => Specs.Select(model => model.id).ToArray();

        [MenuItem("ContextSpeak/Meshy/Open character preview")]
        public static void OpenPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Academy.prefab");
            var rows = Mathf.CeilToInt(Names.Length / 8f);
            SceneView.lastActiveSceneView?.Frame(new Bounds(
                new Vector3(0, (rows - 1) * 1.4f + 0.8f, 0), new Vector3(18, rows * 2.8f + 1, 3)), false);
        }

        public static void ImportAndVerify()
        {
            BuildAssets();
            MeshyChecks.Verify();
        }

        public static void BuildAssets()
        {
            CafeBuild.ConfigurePipeline();
            // Pinned glTFast 6.20 caches its generator across pipeline changes in the first Editor session.
            var generator = typeof(GLTFast.Materials.MaterialGenerator).GetField(
                "s_DefaultMaterialGenerator", BindingFlags.Static | BindingFlags.NonPublic);
            if (generator == null) throw new InvalidOperationException("The pinned glTFast material generator changed; review the Meshy importer.");
            generator.SetValue(null, new GLTFast.Materials.UniversalRPMaterialGenerator(
                (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline));
            foreach (var source in Directory.GetFiles(Source, "*.glb"))
                AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceUpdate);
            MotionAssets.Build();
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            foreach (var name in Names) CharacterPrefab(name, Folder + name + ".prefab");
            PreviewScene();
            AssetDatabase.SaveAssets();
        }

        public static GameObject Create(string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            if (prefab == null) prefab = CharacterPrefab(name, Folder + name + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            return instance;
        }

        public static GameObject CharacterPrefab(string name, string path)
        {
            if (MotionAssets.Has(name))
            {
                var animated = AssetDatabase.LoadAssetAtPath<GameObject>(MotionAssets.Folder + name + ".prefab");
                if (animated == null) { MotionAssets.Build(); animated = AssetDatabase.LoadAssetAtPath<GameObject>(MotionAssets.Folder + name + ".prefab"); }
                var copy = UnityEngine.Object.Instantiate(animated);
                copy.name = name;
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
                UnityEngine.Object.DestroyImmediate(copy);
                return prefab;
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + name + ".glb");
            if (source == null) throw new InvalidOperationException("Missing Meshy export " + name + ". Run python3 tools/meshy/prepare.py.");
            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            NormalizeVisual(root, Specs.Single(spec => spec.id == name).height);
            foreach (var animation in model.GetComponentsInChildren<Animation>())
            {
                animation.playAutomatically = false;
                animation.enabled = false;
            }
            foreach (var animator in model.GetComponentsInChildren<Animator>())
            {
                animator.enabled = false;
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
            }
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.quality = SkinQuality.Bone4;
            if (model.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                // The supplied vault remains an opt-in clip. Use the existing subtle motion for normal play.
                var joints = model.GetComponentsInChildren<Transform>();
                var rig = root.AddComponent<CozyAvatar>();
                rig.Head = Bone(joints, "Head");
                rig.LeftArm = Bone(joints, "LeftArm");
                rig.RightArm = Bone(joints, "RightArm");
                rig.LeftLeg = Bone(joints, "LeftUpLeg");
                rig.RightLeg = Bone(joints, "RightUpLeg");
            }
            var result = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return result;
        }

        private static void NormalizeVisual(GameObject root, float height)
        {
            var points = root.GetComponentsInChildren<MeshFilter>()
                .SelectMany(filter => filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToList();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                skin.BakeMesh(baked);
                points.AddRange(baked.vertices.Select(skin.transform.TransformPoint));
                UnityEngine.Object.DestroyImmediate(baked);
            }
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points.Skip(1)) bounds.Encapsulate(point);
            var visual = root.transform.GetChild(0);
            var scale = height / bounds.size.y;
            visual.localScale *= scale;
            visual.localPosition = (visual.localPosition - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)) * scale;
        }

        private static Transform Bone(Transform[] joints, string name)
        {
            return joints.FirstOrDefault(joint => joint.name == name || joint.name == "mixamorig:" + name || joint.name == "mixamorig_" + name);
        }

        private static void PreviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.84f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.23f);
            var light = new GameObject("Warm key light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            light.color = new Color(1, 0.92f, 0.82f);
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(35, 155, 0);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.17f, 0.19f);
            camera.orthographic = true;
            var names = Names;
            var rows = Mathf.CeilToInt(names.Length / 8f);
            var center = new Vector3(0, (rows - 1) * 1.4f + 0.8f, 0);
            camera.orthographicSize = Mathf.Max(4.5f, rows * 1.7f);
            camera.transform.position = center + new Vector3(0, 1, 20);
            camera.transform.LookAt(center);
            camera.gameObject.AddComponent<AudioListener>();
            foreach (var spec in Specs.Select((value, index) => new { value, index }))
            {
                var x = (spec.index % 8 - 3.5f) * 2.15f;
                var y = spec.index / 8 * 2.8f;
                Create(spec.value.id, new Vector3(x, y, 0));
                Label(spec.value.id, new Vector3(x, y - 0.22f, 0), 0.022f);
                Label(spec.value.rigSource, new Vector3(x, y - 0.46f, 0), 0.011f);
            }
            Label("ContextSpeak · Complete character roster", new Vector3(0, rows * 2.8f, 0), 0.034f);
            EditorSceneManager.SaveScene(scene, Folder + "CharacterPreview.unity");
        }

        private static void Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.text = text;
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(0, 180, 0);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.fontSize = 60;
            label.characterSize = size;
            label.color = new Color(0.95f, 0.91f, 0.82f);
        }
    }
}
