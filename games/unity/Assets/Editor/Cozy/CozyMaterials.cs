using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class CozyMaterials
    {
        public static Material Glass()
        {
            const string path = "Assets/Generated/CozyGlass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.75f, 0.87f, 0.88f, 0.12f));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material Glow()
        {
            const string path = "Assets/Generated/CozyLamp.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(1, 0.83f, 0.5f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1, 0.65f, 0.22f) * 1.4f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material Wood(Color colour)
        {
            var path = "Assets/Generated/CozyWood-" + ColorUtility.ToHtmlStringRGB(colour) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("ContextSpeak/Cozy wood"));
            material.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
