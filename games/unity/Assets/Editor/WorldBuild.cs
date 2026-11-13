using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    public static class WorldBuild
    {
        public static void Configure(CafePlayer player, Transform[] actors, string district)
        {
            var folder = "Assets/Generated/Resources";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<LifeQuestDefinition>(folder + "/FirstDayQuest.asset") == null)
            {
                var quest = ScriptableObject.CreateInstance<LifeQuestDefinition>();
                quest.Catalog = Resources.Load<TextAsset>("LifeCatalog");
                AssetDatabase.CreateAsset(quest, folder + "/FirstDayQuest.asset");
            }
            Camera(player);
            foreach (var actor in actors) ConfigureNpc(actor, folder);
            BakeNavigation(district);
        }
        public static void BakeNavigation(string district)
        {
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>()
                ?? new GameObject("Walkable neighborhood").AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f;
            surface.BuildNavMesh();
            var navPath = "Assets/Generated/Navigation-" + district + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            else
            {
                // Retain the GUID so rebuilding another district cannot break saved scene references.
                var built = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(built, existing);
                UnityEngine.Object.DestroyImmediate(built);
                surface.navMeshData = existing;
                surface.AddData();
                EditorUtility.SetDirty(existing);
            }
        }
        private static void Camera(CafePlayer player)
        {
            player.View.gameObject.AddComponent<CinemachineBrain>();
            player.CameraPosition = new GameObject("Camera follow target").transform;
            player.CameraPosition.position = player.View.transform.position;
            player.CameraAim = new GameObject("Camera look target").transform;
            player.CameraAim.position = player.transform.position + Vector3.up;
            var camera = new GameObject("Third person camera").AddComponent<CinemachineCamera>();
            camera.Follow = player.CameraPosition;
            camera.LookAt = player.CameraAim;
            camera.Lens.FieldOfView = player.View.fieldOfView;
            var follow = camera.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = Vector3.zero;
            follow.TrackerSettings.PositionDamping = Vector3.one * 0.18f;
            camera.gameObject.AddComponent<CinemachineHardLookAt>();
        }
        private static void ConfigureNpc(Transform actor, string folder)
        {
            var id = actor.name.ToLowerInvariant();
            var path = folder + "/Npc-" + id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<NpcDefinition>();
                definition.Id = id;
                definition.DisplayName = actor.name;
                definition.Role = id == "mom" ? "Family" : id == "sam" ? "Market vendor" : id == "maya" ? "Barista" : "Friend";
                definition.Capabilities = id == "mom" ? new[] { NpcCapability.FamilyErrand }
                    : id == "sam" ? new[] { NpcCapability.BuyGroceries, NpcCapability.PayBill }
                    : id == "maya" ? new[] { NpcCapability.OrderDrink, NpcCapability.PayBill }
                    : new[] { NpcCapability.SocialChat };
                AssetDatabase.CreateAsset(definition, path);
            }
            var npc = actor.gameObject.AddComponent<NpcActor>();
            npc.Definition = definition;
            // Animate a neutral pivot, preserving each imported model's authored scale.
            var children = actor.Cast<Transform>().ToArray();
            var pivot = new GameObject("Conversation animation").transform;
            pivot.SetParent(actor, false);
            foreach (var child in children) child.SetParent(pivot, true);
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>()) renderer.gameObject.isStatic = false;
            npc.Animator = pivot.gameObject.AddComponent<Animator>();
            var controllerPath = "Assets/Generated/NpcConversation.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("Conversation", AnimatorControllerParameterType.Int);
                for (var index = 0; index < 4; index++)
                {
                    var state = controller.layers[0].stateMachine.AddState(((NpcState)index).ToString());
                    var clip = new AnimationClip { name = state.name };
                    var height = index == 1 ? 0.025f : 0.008f;
                    clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.EaseInOut(0, 0, 0.7f, height));
                    var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                    clipSettings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
                    AssetDatabase.AddObjectToAsset(clip, controller);
                    state.motion = clip;
                    var transition = controller.layers[0].stateMachine.AddAnyStateTransition(state);
                    transition.hasExitTime = false;
                    transition.duration = 0.2f;
                    transition.canTransitionToSelf = false;
                    transition.AddCondition(AnimatorConditionMode.Equals, index, "Conversation");
                }
            }
            npc.Animator.runtimeAnimatorController = controller;
        }
    }
}
