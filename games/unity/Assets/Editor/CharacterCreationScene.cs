using ContextSpeak.Cafe;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    public static class CharacterCreationScene
    {
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var manager = new GameObject("CharacterCreator").AddComponent<CharacterCreationController>();
            var avatar = new GameObject("Preview");
            avatar.transform.position = new Vector3(-0.65f, 0, 0);
            CozyCharacters.DressPlayer(avatar.transform);
            manager.Preview = avatar.GetComponent<PlayerAppearance>();
            manager.Preview.PreviewOnly = true;
            foreach (var motion in avatar.GetComponentsInChildren<CozyAvatar>(true)) motion.enabled = false;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            manager.PreviewCamera = camera;
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1.05f, 4.5f);
            camera.transform.LookAt(new Vector3(0, 0.95f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 1.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.79f, 0.73f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.86f, 0.87f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.59f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.27f, 0.22f);
            var light = new GameObject("Soft portrait light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.color = new Color(1, 0.94f, 0.86f);
            light.transform.rotation = Quaternion.Euler(35, 155, 0);
            EditorSceneManager.SaveScene(scene, "Assets/Generated/CharacterCreation.unity");
        }
    }
}
