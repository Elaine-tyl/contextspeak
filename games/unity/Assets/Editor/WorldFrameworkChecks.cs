using System;
using ContextSpeak.Cafe;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    public static class WorldFrameworkChecks
    {
        public static void VerifyScene()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<CafePlayer>();
            Check(player != null && player.GetComponent("WorldInput") != null,
                "The walking player needs the shared Input System controller.");
            Check(player.View.GetComponent("CinemachineBrain") != null,
                "The real camera must use Cinemachine, not a second competing camera driver.");
            var path = new NavMeshPath();
            var game = UnityEngine.Object.FindFirstObjectByType<LifeGame>();
            if (game != null) VerifyNavigation("LifeTown");
            if (game != null)
            {
                foreach (var npc in LifeCatalog.Load().npcs)
                {
                    Check(NavMesh.CalculatePath(new Vector3(0, 0, 12), npc.Stand, NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete,
                        "Each spoken objective needs a complete baked navigation route: " + npc.id);
                }
            }
            Debug.Log("ContextSpeak shared world framework checks passed");
        }
        public static void VerifyNavigation(string district)
        {
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            Check(surface != null && surface.navMeshData != null
                && AssetDatabase.GetAssetPath(surface.navMeshData).EndsWith("Navigation-" + district + ".asset"),
                "Each district must keep its own navigation asset: " + district);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("World framework regression: " + message);
        }
    }
}
