using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Contextual copies share imported meshes/materials; gameplay collision stays on existing roots.
    public static class EnvironmentArt
    {
        private const string Spruce = "World03c68813dd", Pine = "World73438ba0b7", Oak = "World334a09eb1f";
        private const string Grass = "Worlded6f92e1a2", Road = "World65b03a2587";
        private static Transform group;
        public static Transform Root => group;

        public static void Begin()
        {
            group = new GameObject("Applied downloaded environment").transform;
        }

        private static GameObject Copy(string id, string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Source + "Contextual/" + id + ".glb")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + id + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing applied environment source: " + id);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(group, false);
            instance.transform.position = position;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials) material.enableInstancing = true;
            return instance;
        }

        private static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        public static void Tree(Vector3 position, float height, bool landmark = false)
        {
            CreateTree(landmark ? Pine : Spruce, position, height);
        }

        public static void IndoorTree(Vector3 position, float height)
        {
            CreateTree(Oak, position, height);
        }

        private static void CreateTree(string source, Vector3 position, float height)
        {
            var instance = Copy(source, "Downloaded tree", position);
            var bounds = Bounds(instance);
            instance.transform.localScale *= height / bounds.size.y;
            var collider = instance.AddComponent<CapsuleCollider>();
            collider.height = height / instance.transform.localScale.y;
            collider.radius = 0.2f / instance.transform.localScale.x;
            collider.center = Vector3.up * collider.height / 2;
            var lod = instance.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.055f, instance.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
        }

        public static void Surface(GameObject ground)
        {
            var material = GroundMaterial();
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
        }

        private static Material GroundMaterial()
        {
            const string path = "Assets/Generated/DownloadedMeadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + Grass + ".prefab");
            // Grass blades may use alpha cutouts; the walkable ground must remain opaque.
            var create = material == null;
            if (create) material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            else material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", prefab.GetComponentInChildren<Renderer>().sharedMaterial.mainTexture);
            material.name = "Downloaded meadow surface";
            material.SetColor("_BaseColor", new Color(0.62f, 0.76f, 0.47f));
            material.SetFloat("_Smoothness", 0.08f);
            material.mainTextureScale = new Vector2(18, 18);
            if (create) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void RoadSurface(GameObject oldRoad)
        {
            var size = oldRoad.transform.localScale;
            var replacement = Copy(Road, "Downloaded asphalt road", oldRoad.transform.position + Vector3.up * 0.025f);
            var bounds = Bounds(replacement);
            replacement.transform.localScale = new Vector3(size.x / bounds.size.x, 0.04f / Mathf.Max(bounds.size.y, 0.001f), size.z / bounds.size.z);
            replacement.transform.rotation = oldRoad.transform.rotation;
            oldRoad.GetComponent<Renderer>().enabled = false;
        }

        public static void GrassPatch(Vector3 position, float yaw)
        {
            var patch = Copy(Grass, "Downloaded meadow plants", position);
            patch.transform.localScale = Vector3.one * 1.4f;
            patch.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var lod = patch.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.08f, patch.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
        }

        public static GameObject Furniture(string id, Vector3 position, float height, float yaw, bool solid)
        {
            var item = Copy(id, "Downloaded café furniture", position);
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            item.transform.localScale *= height / Bounds(item).size.y;
            // Exports keep their own pivot (often the middle of the mesh): stand the model on the floor.
            var bounds = Bounds(item);
            item.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            if (solid)
            {
                bounds = Bounds(item);
                var collider = item.AddComponent<BoxCollider>();
                var scale = item.transform.localScale.x;
                collider.center = item.transform.InverseTransformPoint(bounds.center);
                collider.size = new Vector3(1.1f, height, 1.1f) / scale;
            }
            return item;
        }
    }
}
