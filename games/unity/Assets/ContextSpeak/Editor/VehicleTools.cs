using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Vehicles > List Drivable Vehicles — which vehicles in the open scene the player
    /// will be able to drive (the same detection the game runs when a scene loads) and why others are skipped.
    /// ContextSpeak > Vehicles > Clear Static Flags On Vehicles — lets static-batched cars move.
    public static class VehicleTools
    {
        [MenuItem("ContextSpeak/Vehicles/List Drivable Vehicles")]
        public static void List()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var found = VehicleRegistry.Detect(scene, out var skipped);
            Debug.Log("Drivable in " + scene.name + ": " + found.Count + "\n- " + string.Join("\n- ", found.Select(f => f.label + " · " + Path(f.root.transform) + " · " + f.kind)) +
                      (skipped.Count > 0 ? "\nSkipped:\n- " + string.Join("\n- ", skipped) : ""));
        }

        [MenuItem("ContextSpeak/Vehicles/Clear Static Flags On Vehicles")]
        public static void ClearStatic()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var count = 0;
            foreach (var top in scene.GetRootGameObjects())
                foreach (var t in top.GetComponentsInChildren<Transform>(true))
                {
                    if (!VehicleRegistry.LooksLikeVehicle(t.name)) continue;
                    foreach (var child in t.GetComponentsInChildren<Transform>(true))
                        if (child.gameObject.isStatic) { child.gameObject.isStatic = false; count++; }
                }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Cleared the static flag on " + count + " vehicle objects in " + scene.name + " (save the scene).");
        }

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
