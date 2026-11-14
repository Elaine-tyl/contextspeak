using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    /// Optional true backdrop blur for a renderer that provides a world-only camera colour texture.
    /// Call Refresh after the surface layout or camera image changes. Never captures UI into itself.
    /// Without a source, Theme.uss deliberately retains the readable glass-tint fallback.
    public sealed class FrostedGlass : IDisposable
    {
        private readonly VisualElement surface;
        private readonly Material material;
        private RenderTexture horizontal, vertical, cropped;
        private static readonly int Direction = Shader.PropertyToID("_Direction");
        private static readonly int UvRect = Shader.PropertyToID("_UvRect");
        public FrostedGlass(VisualElement surface)
        {
            this.surface = surface;
            var shader = Resources.Load<Shader>("UI/FrostedGlass");
            if (shader != null && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        public bool Refresh(RenderTexture worldColour)
        {
            if (material == null || worldColour == null || surface.panel == null) return false;
            var bounds = surface.worldBound;
            var panel = surface.panel.visualTree.worldBound;
            if (bounds.width < 1 || bounds.height < 1 || panel.width < 1 || panel.height < 1) return false;
            var width = Mathf.Max(1, worldColour.width / 4);
            var height = Mathf.Max(1, worldColour.height / 4);
            Ensure(ref horizontal, width, height);
            Ensure(ref vertical, width, height);
            Ensure(ref cropped, Mathf.Max(1, Mathf.CeilToInt(bounds.width / panel.width * width)), Mathf.Max(1, Mathf.CeilToInt(bounds.height / panel.height * height)));
            // A separable 9-tap Gaussian at quarter resolution gives a roughly 20px soft blur.
            material.SetVector(Direction, new Vector4(5f / worldColour.width, 0, 0, 0));
            Graphics.Blit(worldColour, horizontal, material, 0);
            material.SetVector(Direction, new Vector4(0, 5f / worldColour.height, 0, 0));
            Graphics.Blit(horizontal, vertical, material, 0);
            material.SetVector(UvRect, new Vector4(bounds.xMin / panel.width, 1f - bounds.yMax / panel.height, bounds.width / panel.width, bounds.height / panel.height));
            Graphics.Blit(vertical, cropped, material, 1);
            surface.style.backgroundImage = Background.FromRenderTexture(cropped);
            return true;
        }
        private static void Ensure(ref RenderTexture texture, int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height) return;
            Release(ref texture);
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "ContextSpeak frosted backdrop", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            texture.Create();
        }
        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }
        public void Dispose()
        {
            surface.style.backgroundImage = StyleKeyword.None;
            Release(ref horizontal); Release(ref vertical); Release(ref cropped);
            if (material == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(material);
            else UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
