using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    public static class BuildingArtChecks
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new Exception("Architecture: " + message);
        }

        public static void Verify()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var skins = UnityEngine.Object.FindObjectsByType<BuildingSkin>(FindObjectsSortMode.None);
            Require(skins.Length == 34 && skins.Select(item => item.SourceId).Distinct().Count() == 34,
                "All 34 inspected architecture files need distinct contextual placements.");
            var triangles = skins.Sum(item => item.Exterior.GetComponentsInChildren<MeshFilter>(true)
                .Sum(mesh => (long)mesh.sharedMesh.triangles.Length / 3));
            Require(triangles <= 600000, "Permanent architecture exceeds its 600k pre-cull budget: " + triangles);
            var widths = new Dictionary<string, float> { ["counter"] = 18, ["home"] = 17, ["school"] = 17,
                ["market"] = 21, ["restaurant"] = 17, ["japan"] = 17, ["mall"] = 25 };
            var anchors = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            var rooms = skins.Where(item => item.Kind == "room").ToArray();
            Require(rooms.Length == 7, "Seven learning buildings need imported exteriors.");
            foreach (var room in rooms)
            {
                var exterior = room.Exterior;
                var meshes = exterior.GetComponentsInChildren<MeshFilter>(true);
                Require(meshes.Length > 0 && meshes.All(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh).Contains(room.SourceId)),
                    "Wrong imported building geometry: " + room.Scene);
                var renderers = exterior.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Require(bounds.size.x >= widths[room.Scene] - .1f, "Exterior is narrower than the functional room: " + room.Scene);
                Require(bounds.size.z >= (room.Scene == "counter" ? 21 : room.Scene == "mall" ? 17 : room.Scene == "market" ? 14 : 12) - .1f,
                    "Exterior does not cover the room depth: " + room.Scene);
                foreach (var prefix in new[] { "building:", "exit:" })
                {
                    var entry = anchors.Single(item => item.Id == prefix + room.Scene);
                    var path = new NavMeshPath();
                    Require(NavMesh.CalculatePath(new Vector3(7.5f, .1f, -.5f), entry.Arrival.position, NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete, "Entrance/exit is unreachable: " + entry.Id);
                    var blockers = Physics.OverlapCapsule(entry.Arrival.position + Vector3.up * .4f,
                        entry.Arrival.position + Vector3.up * 1.4f, .3f)
                        .Where(item => item.GetComponentInParent<CafePlayer>() == null).ToArray();
                    Require(blockers.Length == 0, "Doorway capsule blocked: " + entry.Id + " " + string.Join(",", blockers.Select(item => item.name)));
                }
                room.Refresh(room.Plot.center + Vector3.up * 100);
                Require(!room.IsInside && (room.UsesAuthoredInterior
                    ? !exterior.activeSelf
                    : exterior.activeSelf && room.Interior.Length > 0 && room.Interior.All(renderer => !renderer.enabled)),
                    "Outside visibility does not match the authored room mode: " + room.Scene);
                room.Refresh(new Vector3(room.Plot.center.x, .1f, room.Plot.center.z));
                Require(!exterior.activeSelf && room.Interior.All(renderer => renderer.enabled) && room.gameObject.activeSelf,
                    "Cutaway must reveal interior without disabling gameplay: " + room.Scene);
                room.Refresh(room.Plot.center + Vector3.up * 100);
            }
            Debug.Log("All 34 contextual architecture assignments, seven entrances/exits and cutaways verified; triangles=" + triangles);
        }
    }
}
