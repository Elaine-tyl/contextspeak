using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Explicit GLB exports keep Blender optional when building from a clean checkout.
    public static class BlenderArt
    {
        private const string Folder = "Assets/_Game/Art/Blender/";

        private static GameObject Import(string name)
        {
            var path = Folder + name + ".glb";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing Blender export: " + path + ". Run pnpm art:blender.");
            return (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }

        public static GameObject CharacterPrefab(string name, string path)
        {
            var model = Import(name);
            var joints = model.GetComponentsInChildren<Transform>();
            var rig = model.AddComponent<CozyAvatar>();
            rig.Head = joints.First(joint => joint.name == "Head");
            rig.LeftArm = joints.First(joint => joint.name == "LeftArm");
            rig.RightArm = joints.First(joint => joint.name == "RightArm");
            rig.LeftLeg = joints.First(joint => joint.name == "LeftLeg");
            rig.RightLeg = joints.First(joint => joint.name == "RightLeg");
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.quality = SkinQuality.Bone4;
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, path);
            UnityEngine.Object.DestroyImmediate(model);
            return prefab;
        }

        public static void Lodge()
        {
            var model = Import("SpeakingLodge");
            model.name = "Speaking lodge";
            model.transform.position = new Vector3(-21, 0, -9);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.gameObject.isStatic = true;
            // Use simple physical boundaries rather than expensive per-prop mesh colliders.
            Boundary(model, "Deck", new Vector3(0, 0.1f, 0), new Vector3(7.4f, 0.2f, 5.5f));
            Boundary(model, "Back wall", new Vector3(0, 1.55f, -2.15f), new Vector3(6.5f, 2.7f, 0.18f));
            var counter = model.GetComponentsInChildren<MeshFilter>().First(part => part.name == "Welcome counter");
            var counterBody = counter.gameObject.AddComponent<BoxCollider>();
            counterBody.center = counter.sharedMesh.bounds.center;
            counterBody.size = counter.sharedMesh.bounds.size;
            foreach (var x in new[] { -3.15f, 3.15f })
                foreach (var z in new[] { -2.15f, 2.15f })
                    Boundary(model, "Post", new Vector3(x, 1.55f, z), new Vector3(0.22f, 2.9f, 0.22f));
            // Anchor to imported geometry: glTF coordinate conversion can mirror the source X axis.
            var board = model.GetComponentsInChildren<Transform>().First(part => part.name == "Mission board").gameObject;
            var interaction = board.AddComponent<Interactable>();
            interaction.Id = "speaking-board";
            interaction.Prompt = "Plan your speaking trail";
            interaction.Range = 3.2f;
            var arrival = new GameObject("Lodge arrival").transform;
            arrival.SetParent(model.transform, false);
            arrival.position = new Vector3(board.transform.position.x, model.transform.position.y + 0.3f, board.transform.position.z + 2.5f);
            interaction.Arrival = arrival;
        }

        private static void Boundary(GameObject parent, string name, Vector3 centre, Vector3 size)
        {
            var part = new GameObject(name + " boundary");
            part.transform.SetParent(parent.transform, false);
            var collider = part.AddComponent<BoxCollider>();
            collider.center = centre;
            collider.size = size;
        }
    }
}
