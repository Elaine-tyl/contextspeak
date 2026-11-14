using UnityEngine;

namespace ContextSpeak.Life
{
    /// Lights that respond to the sky: street lamps and vehicle headlights switch on at dusk and in
    /// gloomy rain, interior lights get relatively brighter at night, and windows glow warmly.
    public sealed class SkyLightSwitch : MonoBehaviour
    {
        public enum Kind { Street, Headlight, Interior, Window }
        public Kind Mode = Kind.Street;
        public Light[] Lights = new Light[0];
        public Renderer[] Glow = new Renderer[0];
        public Color GlowColour = new Color(1f, 0.82f, 0.55f);
        [SerializeField] private float[] baseIntensity = new float[0];
        private Color[] baseColour = new Color[0];
        private MaterialPropertyBlock block;
        private float level = -1, timer, scale = 1;
        private Color[] lightColour = new Color[0];
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor"), Emissive = Shader.PropertyToID("emissiveFactor"), Base = Shader.PropertyToID("_BaseColor");

        private void Start() => Init();

        private void Init()
        {
            block = new MaterialPropertyBlock();
            if (baseIntensity.Length != Lights.Length)
            {
                baseIntensity = new float[Lights.Length];
                for (var i = 0; i < Lights.Length; i++) baseIntensity[i] = Lights[i] != null ? Lights[i].intensity : 1;
            }
            lightColour = new Color[Lights.Length];
            for (var i = 0; i < Lights.Length; i++) lightColour[i] = Lights[i] != null ? Lights[i].color : Color.white;
            baseColour = new Color[Glow.Length];
            for (var i = 0; i < Glow.Length; i++)
                baseColour[i] = Glow[i] != null && Glow[i].sharedMaterial != null && Glow[i].sharedMaterial.HasProperty(Base) ? Glow[i].sharedMaterial.GetColor(Base) : Color.white;
            timer = Random.value * 0.3f;   // spread updates across frames
        }

        /// Apply the current sky immediately (used by editor test renders).
        public void Refresh()
        {
            Init();
            timer = 0;
            Tick(true);
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            Tick(false);
        }

        private void Tick(bool force)
        {
            timer = 0.3f;
            // Lamps come on one by one rather than all on the same frame.
            var threshold = Mode == Kind.Street ? 0.18f + Mathf.Abs(transform.position.x * 0.013f + transform.position.z * 0.007f) % 0.12f : 0.12f;
            var target = Mode == Kind.Interior || Mode == Kind.Window ? SkyState.LightsNeeded : SkyState.LightsNeeded > threshold ? 1f : 0f;
            var next = level < 0 || force ? target : Mathf.MoveTowards(level, target, 0.3f);
            if (!force && Mathf.Abs(next - level) < 0.01f && Mathf.Approximately(scale, EnvironmentTuning.IndoorLights)) return;
            scale = EnvironmentTuning.IndoorLights;
            level = next;
            for (var i = 0; i < Lights.Length; i++)
            {
                var light = Lights[i];
                if (light == null) continue;
                if (Mode == Kind.Interior)
                {
                    // Evenings: interiors read warmer (≈2700 K) and relatively brighter than at noon.
                    light.intensity = baseIntensity[i] * (1 + 0.6f * level) * scale;
                    light.color = Color.Lerp(lightColour[i], lightColour[i] * Mathf.CorrelatedColorTemperatureToRGB(2700) * 1.15f, level * 0.6f);
                    continue;
                }
                light.enabled = level > 0.02f;
                light.intensity = baseIntensity[i] * level * (Mode == Kind.Headlight ? 1 : scale);
            }
            for (var i = 0; i < Glow.Length; i++)
            {
                var renderer = Glow[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                if (Mode == Kind.Window)
                {
                    var lit = new Color(GlowColour.r, GlowColour.g, GlowColour.b, Mathf.Max(baseColour[i].a, 0.72f));
                    block.SetColor(Base, Color.Lerp(baseColour[i], lit, level * 0.8f));
                }
                else
                {
                    var hdr = GlowColour * Mathf.Lerp(0.15f, Mode == Kind.Headlight ? 5f : 3.5f, level);
                    block.SetColor(Emission, hdr);
                    block.SetColor(Emissive, hdr);
                }
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
