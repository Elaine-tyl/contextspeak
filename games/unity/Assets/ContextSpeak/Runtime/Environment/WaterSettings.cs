using UnityEngine;

namespace ContextSpeak.Life
{
    /// Per-body-of-water settings for the "ContextSpeak/Water Surface" shader (pools, rivers,
    /// streams, lakes, ponds, waterfall sheets). The ocean has its own system (OceanWaves).
    /// Colours are physically plausible water tints (absorption), not opaque saturated blue.
    [ExecuteAlways]
    public sealed class WaterSettings : MonoBehaviour
    {
        public enum WaterType { Pool, River, Stream, Lake, Pond, Waterfall }
        public enum ReflectionQuality { SkyOnly, LocalProbe }

        public WaterType Type = WaterType.Pond;
        public Renderer[] Surfaces = new Renderer[0];
        [Tooltip("River/stream current in m/s, world XZ.")] public Vector2 Flow;
        public ReflectionQuality Reflections = ReflectionQuality.SkyOnly;
        public ReflectionProbe LocalProbe;
        [Tooltip("Multiplies the preset ripple strength.")] [Range(0, 3)] public float RippleScale = 1;

        private MaterialPropertyBlock block;
        private static readonly int Shallow = Shader.PropertyToID("_Shallow"), Deep = Shader.PropertyToID("_Deep"), Absorption = Shader.PropertyToID("_Absorption"),
            Clarity = Shader.PropertyToID("_Clarity"), RippleSize = Shader.PropertyToID("_RippleScale"), Ripple = Shader.PropertyToID("_RippleStrength"),
            FlowId = Shader.PropertyToID("_Flow"), Turbulence = Shader.PropertyToID("_Turbulence"), Foam = Shader.PropertyToID("_ContactFoam"),
            Caustics = Shader.PropertyToID("_Caustics"), Vertical = Shader.PropertyToID("_Vertical"), Refraction = Shader.PropertyToID("_Refraction");

        public struct Preset
        {
            public Color Shallow, Deep;
            public float Absorption, Clarity, RippleScale, Ripple, Turbulence, Foam, Caustics, Refraction;
        }

        public static Preset For(WaterType type)
        {
            switch (type)
            {
                case WaterType.Pool: return new Preset { Shallow = new Color(0.78f, 0.95f, 0.95f), Deep = new Color(0.12f, 0.45f, 0.55f), Absorption = 0.35f, Clarity = 0.25f, RippleScale = 2.4f, Ripple = 0.18f, Turbulence = 0, Foam = 0.03f, Caustics = 1.2f, Refraction = 0.02f };
                case WaterType.River: return new Preset { Shallow = new Color(0.62f, 0.72f, 0.6f), Deep = new Color(0.07f, 0.14f, 0.12f), Absorption = 0.9f, Clarity = 1.6f, RippleScale = 1.1f, Ripple = 0.55f, Turbulence = 0.8f, Foam = 0.45f, Caustics = 0.3f, Refraction = 0.03f };
                case WaterType.Stream: return new Preset { Shallow = new Color(0.72f, 0.8f, 0.68f), Deep = new Color(0.1f, 0.18f, 0.14f), Absorption = 0.8f, Clarity = 1.0f, RippleScale = 2.0f, Ripple = 0.7f, Turbulence = 1.4f, Foam = 0.35f, Caustics = 0.6f, Refraction = 0.035f };
                case WaterType.Lake: return new Preset { Shallow = new Color(0.55f, 0.68f, 0.62f), Deep = new Color(0.04f, 0.11f, 0.13f), Absorption = 0.55f, Clarity = 1.3f, RippleScale = 0.7f, Ripple = 0.25f, Turbulence = 0, Foam = 0.15f, Caustics = 0.2f, Refraction = 0.02f };
                case WaterType.Waterfall: return new Preset { Shallow = new Color(0.85f, 0.9f, 0.9f), Deep = new Color(0.4f, 0.5f, 0.5f), Absorption = 0.3f, Clarity = 0.6f, RippleScale = 1.6f, Ripple = 1.0f, Turbulence = 1.5f, Foam = 0.6f, Caustics = 0, Refraction = 0.05f };
                default: return new Preset { Shallow = new Color(0.5f, 0.6f, 0.45f), Deep = new Color(0.05f, 0.1f, 0.07f), Absorption = 1.1f, Clarity = 2.2f, RippleScale = 1.4f, Ripple = 0.2f, Turbulence = 0, Foam = 0.12f, Caustics = 0.15f, Refraction = 0.02f };
            }
        }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            block ??= new MaterialPropertyBlock();
            var preset = For(Type);
            foreach (var surface in Surfaces)
            {
                if (surface == null) continue;
                surface.GetPropertyBlock(block);
                block.SetColor(Shallow, preset.Shallow);
                block.SetColor(Deep, preset.Deep);
                block.SetFloat(Absorption, preset.Absorption);
                block.SetFloat(Clarity, preset.Clarity);
                block.SetFloat(RippleSize, preset.RippleScale);
                block.SetFloat(Ripple, preset.Ripple * RippleScale);
                block.SetVector(FlowId, new Vector4(Flow.x, Flow.y, 0, 0));
                block.SetFloat(Turbulence, preset.Turbulence);
                block.SetFloat(Foam, preset.Foam);
                block.SetFloat(Caustics, preset.Caustics);
                block.SetFloat(Refraction, preset.Refraction);
                block.SetFloat(Vertical, Type == WaterType.Waterfall ? 1 : 0);
                surface.SetPropertyBlock(block);
                surface.reflectionProbeUsage = Reflections == ReflectionQuality.LocalProbe && LocalProbe != null ? UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox : UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            }
        }
    }
}
