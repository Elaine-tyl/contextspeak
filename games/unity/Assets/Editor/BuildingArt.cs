using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Authored model assignments; functional room roots and saved positions are retained.
    public static class BuildingArt
    {
        public const string CatalogPath = "Assets/_Game/Art/architecture.json";
        [Serializable] public sealed class Entry
        {
            public string id, label, kind, scene;
            public float height, yaw;
            public float[] position;
        }
        [Serializable] private sealed class Catalog { public Entry[] items; }
        private sealed class Room
        {
            public Transform Root;
            public BuildingSkin Skin;
            public Vector3 Origin;
        }
        private static Transform group;
        private static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        public static Entry[] Read() => JsonUtility.FromJson<Catalog>("{\"items\":" + File.ReadAllText(CatalogPath) + "}").items;

        public static void Begin()
        {
            rooms.Clear();
            group = new GameObject("Imported architecture").transform;
        }

        private static Bounds Geometry(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static BuildingSkin Create(Entry entry, Vector3 position)
        {
            var root = new GameObject(entry.label + " [" + entry.id + "]");
            root.transform.SetParent(group, false);
            root.transform.position = position;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Source + "Contextual/" + entry.id + ".glb");
            if (source == null) throw new InvalidOperationException("Missing contextual building: " + entry.id);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            visual.transform.SetParent(root.transform, false);
            var skin = root.AddComponent<BuildingSkin>();
            skin.SourceId = entry.id;
            skin.Kind = entry.kind;
            skin.Scene = entry.scene;
            skin.Exterior = visual;
            return skin;
        }

        public static void RoomSkin(string scene, Transform interior, Vector3 origin, float width, float depth)
        {
            var entry = Read().Single(item => item.kind == "room" && item.scene == scene);
            var skin = Create(entry, origin);
            var bounds = Geometry(skin.Exterior);
            // Cover the existing room so the facade never leaves its side walls outside the model.
            var scale = Mathf.Max(width / bounds.size.x, (scene == "counter" ? 21 : depth) / bounds.size.z);
            skin.Exterior.transform.localScale *= scale;
            skin.Exterior.transform.localRotation = Quaternion.Euler(0, entry.yaw, 0);
            bounds = Geometry(skin.Exterior);
            var front = origin.z + depth / 2;
            skin.Exterior.transform.position += Vector3.forward * (front - bounds.max.z);
            bounds = Geometry(skin.Exterior);
            skin.Plot = new Bounds(bounds.center, bounds.size + new Vector3(.8f, 2, .8f));
            rooms.Add(scene, new Room { Root = interior, Skin = skin, Origin = origin });

            var entryPoint = new GameObject("Enter " + entry.label);
            entryPoint.transform.SetParent(skin.transform, false);
            entryPoint.transform.position = origin + new Vector3(0, .1f, depth / 2 + 3);
            var use = entryPoint.AddComponent<Interactable>();
            use.Id = "building:" + scene;
            use.Prompt = "Enter " + entry.label;
            use.Range = 2.2f;
            use.Arrival = entryPoint.transform;
            var exit = new GameObject("Leave " + entry.label);
            exit.transform.SetParent(skin.transform, false);
            exit.transform.position = origin + new Vector3(0, .1f, depth / 2 - 1);
            var leave = exit.AddComponent<Interactable>();
            leave.Id = "exit:" + scene;
            leave.Prompt = "Leave " + entry.label;
            leave.Range = 1.4f;
            leave.Arrival = exit.transform;
        }

        public static void Finish()
        {
            DistrictGround();
            foreach (var entry in Read().Where(item => item.kind == "interior"))
            {
                var room = rooms[entry.scene];
                var local = new Vector3(entry.position[0], entry.position[1], entry.position[2]);
                var item = Create(entry, room.Origin + local);
                item.Exterior.transform.localScale *= entry.height / Geometry(item.Exterior).size.y;
                item.Exterior.transform.localRotation = Quaternion.Euler(0, entry.yaw, 0);
                var bounds = Geometry(item.Exterior);
                ClearProps(room.Root, bounds);
                // These source interiors are static geometry, not approximate invisible furniture boxes.
                foreach (var mesh in item.Exterior.GetComponentsInChildren<MeshFilter>())
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                item.transform.SetParent(room.Root, true);
            }
            foreach (var entry in Read().Where(item => item.kind == "district"))
            {
                var item = Create(entry, new Vector3(entry.position[0], entry.position[1], entry.position[2]));
                item.Exterior.transform.localScale *= entry.height / Geometry(item.Exterior).size.y;
                item.Exterior.transform.localRotation = Quaternion.Euler(0, entry.yaw, 0);
                foreach (var mesh in item.Exterior.GetComponentsInChildren<MeshFilter>())
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                var lod = item.Exterior.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.035f, item.Exterior.GetComponentsInChildren<Renderer>()) });
                lod.RecalculateBounds();
            }
            foreach (var room in rooms.Values)
            {
                room.Skin.Interior = room.Root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled
                    && !(renderer is SkinnedMeshRenderer) && renderer.GetComponentInParent<LearningNpc>() == null
                    && renderer.GetComponentInParent<NpcActor>() == null).ToArray();
                room.Skin.Refresh(Vector3.up * 100);
            }
            ClearDoorApproaches();
        }

        private static void ClearDoorApproaches()
        {
            Physics.SyncTransforms();
            var residents = UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None);
            foreach (var door in group.GetComponentsInChildren<Interactable>())
            {
                var origin = door.transform.position;
                var found = false;
                foreach (var offset in new[] { 0f, -1.2f, 1.2f, -2.4f, 2.4f })
                {
                    var point = origin + Vector3.right * offset;
                    if (residents.Any(npc => Vector3.Distance(npc.transform.position, point) < 1)) continue;
                    if (Physics.CheckCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.4f, .3f)) continue;
                    door.transform.position = point;
                    found = true;
                    break;
                }
                if (!found) throw new InvalidOperationException("No clear building doorway: " + door.Id);
            }
        }

        public static void CafeFacade(Transform details)
        {
            details.SetParent(rooms["counter"].Root, true);
        }

        private static void ClearProps(Transform room, Bounds region)
        {
            region.Expand(.1f);
            foreach (var renderer in room.GetComponentsInChildren<Renderer>())
            {
                var name = renderer.name.ToLowerInvariant();
                if (renderer.GetComponentInParent<LearningNpc>() != null || renderer.GetComponentInParent<Interactable>() != null) continue;
                if (renderer.GetComponentInParent<BuildingSkin>() != null) continue;
                if (new[] { "floor", "wall", "roof", "beam", "window", "post", "ridge", "pendant", "curtain" }.Any(name.Contains)) continue;
                if (!region.Contains(renderer.bounds.center)) continue;
                renderer.enabled = false;
                foreach (var collider in renderer.GetComponents<Collider>()) collider.enabled = false;
            }
        }

        private static void DistrictGround()
        {
            foreach (var plot in new[] {
                (new Vector3(-150, -.3f, 0), new Vector3(160, .4f, 460)),
                (new Vector3(150, -.3f, 0), new Vector3(160, .4f, 460)),
                (new Vector3(0, -.3f, -150), new Vector3(140, .4f, 160)),
                (new Vector3(0, -.3f, 156), new Vector3(140, .4f, 148)) })
            {
                var ground = CafeScene.Box("District landscape", plot.Item1, plot.Item2, new Color(.46f, .57f, .38f), true);
                ground.transform.SetParent(group, true);
                EnvironmentArt.Surface(ground);
            }
        }
    }
}
