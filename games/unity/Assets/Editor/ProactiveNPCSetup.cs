using System;
using System.Linq;
using System.IO;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Life;
using ContextSpeak.Proactive;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ContextSpeak.Editor
{
    public static class ProactiveNPCSetup
    {
        [UnityEditor.MenuItem("ContextSpeak/NPC/Setup Proactive Interaction System")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before saving NPC setup."); return; }
            CreateContent();
            var group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Setup proactive NPC interactions");
            var roots = UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None).Select(n => n.gameObject)
                .Concat(UnityEngine.Object.FindObjectsByType<LivingNPC>(FindObjectsSortMode.None).Select(n => n.gameObject))
                .Concat(UnityEngine.Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None).Select(n => n.gameObject))
                .Concat(UnityEngine.Object.FindObjectsByType<CityPedestrian>(FindObjectsSortMode.None).Select(n => n.gameObject)).Distinct().ToArray();
            var missing = 0;
            foreach (var root in roots)
            {
                if (root.GetComponentInParent<CafePlayer>() != null) continue;
                var npc = root.GetComponent<ProactiveNPCController>() ?? Undo.AddComponent<ProactiveNPCController>(root);
                Undo.RecordObject(npc, "Configure NPC profile");
                ProactiveLifeDirector.Describe(npc);
                if (root.GetComponent<NPCInteractionDetector>() == null) Undo.AddComponent<NPCInteractionDetector>(root);
                if (root.GetComponent<NPCReactionAnimator>() == null) Undo.AddComponent<NPCReactionAnimator>(root);
                if (!root.GetComponentsInChildren<Collider>().Any(c => !c.isTrigger && !(c is MeshCollider)))
                {
                    var body = Undo.AddComponent<CapsuleCollider>(root); body.height = 1.65f; body.radius = .27f; body.center = Vector3.up * .825f;
                }
                var path = "Assets/Resources/ProactiveV71/Profiles/" + Hash128.Compute(npc.Social.Id) + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<ProactiveNPCProfile>(path);
                if (profile == null) { profile = ScriptableObject.CreateInstance<ProactiveNPCProfile>(); profile.Definition = npc.Social; AssetDatabase.CreateAsset(profile, path); }
                npc.Profile = profile;
                var model = root.GetComponentInChildren<AuthoredMotion>();
                if (model == null || model.Extras.Length == 0) missing++;
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
            }
            foreach (var player in UnityEngine.Object.FindObjectsByType<CafePlayer>(FindObjectsSortMode.None))
                if (player.GetComponent<NPCPlayerCollisionInteraction>() == null) Undo.AddComponent<NPCPlayerCollisionInteraction>(player.gameObject);
            foreach (var old in UnityEngine.Object.FindObjectsByType<NPCEventDirector>(FindObjectsSortMode.None))
            { Undo.RecordObject(old, "Retire proximity-only bumps"); old.enabled = false; }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            // Save only this explicitly requested active-scene setup; never regenerate world scenes.
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Proactive setup: " + roots.Length + " existing actors; " + missing + " lack authored gesture clips (procedural fallback). Existing rig adapters handle available clips. Bake NavMesh for dependable approaches. Speech companion required; acoustic pronunciation unavailable by default. Undo supports scene component changes; new reusable assets are retained.");
        }
        public static void CreateContent()
        {
            Directory.CreateDirectory("Assets/Resources/ProactiveV71/Events");
            Directory.CreateDirectory("Assets/Resources/ProactiveV71/Profiles");
            AssetDatabase.Refresh();
            foreach (var definition in ProactiveEventContent.All())
            {
                var path = "Assets/Resources/ProactiveV71/Events/" + definition.Id + ".asset";
                if (AssetDatabase.LoadAssetAtPath<ProactiveEventAsset>(path) != null) continue;
                var asset = ScriptableObject.CreateInstance<ProactiveEventAsset>(); asset.Definition = definition;
                AssetDatabase.CreateAsset(asset, path);
            }
            AssetDatabase.SaveAssets();
        }
        [UnityEditor.MenuItem("ContextSpeak/NPC/Create Proactive Interaction Test Scene")]
        public static void CreateTestScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Test pavement";
            floor.transform.localScale = new Vector3(24, .2f, 24); floor.transform.position = new Vector3(0, -.1f, 0);
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule); npc.name = "Jamie · contact fixture";
            npc.transform.position = new Vector3(0, .85f, 3); npc.GetComponent<CapsuleCollider>().height = 1.7f;
            var controller = npc.AddComponent<ProactiveNPCController>();
            controller.Social = new NPCSocialProfile { Id = "fixture-tourist", Name = "Jamie", Occupation = "Tourist" };
            var player = new GameObject("Player contact fixture"); player.AddComponent<CafePlayer>();
            player.AddComponent<NPCPlayerCollisionInteraction>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 3, -5); camera.transform.LookAt(npc.transform.position);
            player.GetComponent<CafePlayer>().View = camera;
            var light = new GameObject("Test daylight").AddComponent<Light>(); light.type = LightType.Directional;
            Directory.CreateDirectory("Assets/Tests/Proactive");
            EditorSceneManager.SaveScene(scene, "Assets/Tests/Proactive/ProactiveInteractions.unity");
            Debug.Log("Proactive test scene saved. Capsule is a contact fixture, not production character art. Walk into it to verify contact callbacks; use existing world actors for actual rig testing.");
        }
    }
}
