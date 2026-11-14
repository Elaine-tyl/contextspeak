using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Wet skin and clothes after contact with water: colours darken a little and get a soft sheen
    /// (never a mirror), more when more of the body was under water; the character drips and leaves
    /// wet footprints for a while, then dries — faster in sunshine, not at all in the rain.
    /// Works on URP Lit (_BaseColor/_Smoothness) and glTFast (baseColorFactor/roughnessFactor) materials.
    public sealed class WaterWetnessController : MonoBehaviour
    {
        [Range(0, 1)] public float Wetness;
        public float DrySeconds = 70;
        public float MaxDarken = 0.28f, MaxSheen = 0.3f;
        public Transform Body;
        public float Height = 1.7f;

        private struct Slot { public Renderer Renderer; public int Index; public bool Gltf; public Color Colour; public float Smooth; }
        private readonly List<Slot> slots = new List<Slot>();
        private MaterialPropertyBlock block;
        private float applied = -1, dripTimer, inWaterLevel;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Smoothness = Shader.PropertyToID("_Smoothness"),
            GltfColor = Shader.PropertyToID("baseColorFactor"), GltfRoughness = Shader.PropertyToID("roughnessFactor");

        public void Bind(Transform body, float height)
        {
            Body = body; Height = height;
            slots.Clear();
            foreach (var renderer in body.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m == null) continue;
                    if (m.HasProperty(BaseColor)) slots.Add(new Slot { Renderer = renderer, Index = i, Colour = m.GetColor(BaseColor), Smooth = m.HasProperty(Smoothness) ? m.GetFloat(Smoothness) : 0.3f });
                    else if (m.HasProperty(GltfColor)) slots.Add(new Slot { Renderer = renderer, Index = i, Gltf = true, Colour = m.GetColor(GltfColor), Smooth = m.HasProperty(GltfRoughness) ? 1 - m.GetFloat(GltfRoughness) : 0.3f });
                }
            }
            applied = -1;
        }

        /// level 0…1 = how much of the body is in the water right now.
        public void Soak(float level)
        {
            inWaterLevel = Mathf.Clamp01(level);
            Wetness = Mathf.Max(Wetness, inWaterLevel);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (inWaterLevel <= 0.01f)
            {
                var rate = SkyState.Rain > 0.3f ? -0.01f : (1 + SkyState.Daylight * 0.8f) / DrySeconds;
                Wetness = Mathf.Clamp01(Wetness - rate * dt);
                if (SkyState.Rain > 0.3f) Wetness = Mathf.Max(Wetness, SkyState.Rain * 0.6f);
                if (Wetness > 0.2f && Body != null && (dripTimer -= dt) <= 0)
                {
                    dripTimer = Mathf.Lerp(0.6f, 0.06f, Wetness);
                    var at = Body.position + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(0.4f, Height * 0.8f) * Wetness, Random.Range(-0.12f, 0.12f));
                    WaterSplashController.Instance?.Drip(at);
                }
            }
            inWaterLevel = 0;
            if (Mathf.Abs(Wetness - applied) > 0.01f) Apply();
        }

        private void Apply()
        {
            applied = Wetness;
            block ??= new MaterialPropertyBlock();
            var w = Wetness * Wetness * (3 - 2 * Wetness);
            foreach (var slot in slots)
            {
                if (slot.Renderer == null) continue;
                slot.Renderer.GetPropertyBlock(block, slot.Index);
                var dark = 1 - MaxDarken * w;
                var colour = new Color(slot.Colour.r * dark, slot.Colour.g * dark, slot.Colour.b * dark, slot.Colour.a);
                var smooth = Mathf.Lerp(slot.Smooth, Mathf.Min(slot.Smooth + MaxSheen, 0.62f), w);      // satin, never a mirror
                if (slot.Gltf) { block.SetColor(GltfColor, colour); block.SetFloat(GltfRoughness, 1 - smooth); }
                else { block.SetColor(BaseColor, colour); block.SetFloat(Smoothness, smooth); }
                slot.Renderer.SetPropertyBlock(block, slot.Index);
            }
        }
    }
}
