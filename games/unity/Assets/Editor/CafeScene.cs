using System;
using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    // Deterministic scene composition; generated geometry and imported art are rebuilt together.
    public static class CafeScene
    {
        private static readonly Color Cream = Hex("F3E5CE"), Wood = Hex("AC7449"), Green = Hex("597B59"), Ink = Hex("294238");
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        // Create() built the retired old Forest Café scene (ForestCafe); the shared helpers below are still used by other builders.
        private static void Table(Vector3 position, bool friend)
        {
            Model("furniture/tableRound.glb", position, 0, 0.78f, true);
            foreach (var offset in new[] { new Vector3(0, 0, 1.2f), new Vector3(0, 0, -1.2f) })
            {
                if (friend) continue;
                Model("furniture/chairCushion.glb", position + offset, offset.z > 0 ? 180 : 0, 0.9f, true);
            }
            if (friend)
            {
                Model("furniture/chairCushion.glb", position + new Vector3(0, 0, 1.2f), 180, 0.9f, false);
                Model("furniture/chairCushion.glb", position + new Vector3(0, 0, -1.2f), 0, 0.9f, false);
            }
        }
        private static void Tree()
        {
            Primitive("Tree planter", PrimitiveType.Cylinder, new Vector3(0, 0.17f, -2), new Vector3(2.3f, 0.17f, 2.3f), Ink, true);
            Primitive("Tree trunk", PrimitiveType.Cylinder, new Vector3(0, 1.9f, -2), new Vector3(0.7f, 1.8f, 0.7f), Wood, true);
            for (var index = 0; index < 6; index++)
            {
                var angle = index * Mathf.PI / 3;
                Primitive("Forest canopy", PrimitiveType.Sphere, new Vector3(Mathf.Cos(angle) * 1.3f, 3.7f + (index % 2) * 0.45f, -2 + Mathf.Sin(angle) * 1.3f), new Vector3(2.8f, 1.5f, 2.5f), Color.Lerp(Green, Cream, index * 0.035f), false);
            }
        }
        private static string UniqueAsset(string name)
        {
            var path = "Assets/Generated/" + name;
            AssetDatabase.DeleteAsset(path);
            return path;
        }
        private static Material Surface(Color color)
        {
            var name = ColorUtility.ToHtmlStringRGB(color);
            if (Materials.TryGetValue(name, out var cached)) return cached;
            var path = "Assets/Generated/Surface-" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            material.enableInstancing = true;
            Materials[name] = material;
            return material;
        }
        internal static GameObject Box(string name, Vector3 position, Vector3 size, Color color, bool collide)
        {
            return Primitive(name, PrimitiveType.Cube, position, size, color, collide);
        }
        internal static GameObject Primitive(string name, PrimitiveType shape, Vector3 position, Vector3 size, Color color, bool collide)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = Surface(color);
            if (!collide) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            item.isStatic = true;
            return item;
        }
        internal static GameObject Model(string file, Vector3 position, float yaw, float height, bool collide)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/" + file);
            if (prefab == null) throw new Exception("Missing verified cafe model: " + file + ". Run the Unity preparation script.");
            foreach (var mesh in prefab.GetComponentsInChildren<Renderer>())
                foreach (var material in mesh.sharedMaterials)
                    if (!HasUniversalShader(material))
                        throw new Exception("Imported art needs URP materials: " + file + " / " + material?.shader.name);
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var bounds = Bounds(item);
            if (bounds.size.y < 0.001f) throw new Exception("Model has no measurable geometry: " + file);
            item.transform.localScale *= height / bounds.size.y;
            bounds = Bounds(item);
            item.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            if (collide)
            {
                bounds = Bounds(item);
                var obstacle = new GameObject("Collision · " + item.name);
                obstacle.transform.position = bounds.center;
                obstacle.AddComponent<BoxCollider>().size = bounds.size;
                obstacle.transform.SetParent(item.transform, true);
            }
            return item;
        }
        private static bool HasUniversalShader(Material material)
        {
            if (material == null || material.shader == null) return false;
            // Headless Editors have no active GPU subshader; inspect all declared targets.
            for (var index = 0; index < material.shader.subshaderCount; index++)
                if (material.shader.FindSubshaderTagValue(index, new ShaderTagId("RenderPipeline")).name == "UniversalPipeline") return true;
            return false;
        }
        private static Bounds Bounds(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("Imported model has no renderer: " + item.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        internal static void Sign(string text, Vector3 position, float scale)
        {
            var sign = new GameObject("Cafe sign").AddComponent<TextMesh>();
            sign.text = text;
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.fontSize = 64;
            sign.characterSize = scale * 0.1f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = Cream;
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.Euler(0, 180, 0);
            sign.GetComponent<Renderer>().sharedMaterial = sign.font.material;
            sign.gameObject.AddComponent<WorldSign>();
        }
        internal static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var value);
            return value;
        }
    }
}
