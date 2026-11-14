using UnityEngine;

namespace ContextSpeak.Life
{
    /// Water reflects the sky's mood (dark and steely under rain clouds, deep at night, bright and
    /// glossy in sunshine); roads, pavements and decks darken and turn glossy when wet.
    public sealed class SkySurfaces : MonoBehaviour
    {
        public Renderer[] Water = new Renderer[0];
        public Renderer[] Ground = new Renderer[0];
        private Color[] waterBase = new Color[0], groundBase = new Color[0];
        private float[] groundSmooth = new float[0];
        private MaterialPropertyBlock block;
        private float lastWet = -1, lastLook = -1, timer;
        private static readonly int Base = Shader.PropertyToID("_BaseColor"), Smooth = Shader.PropertyToID("_Smoothness");

        private void Start() => Init();

        private void Init()
        {
            block = new MaterialPropertyBlock();
            waterBase = Capture(Water);
            groundBase = Capture(Ground);
            groundSmooth = new float[Ground.Length];
            for (var i = 0; i < Ground.Length; i++)
                groundSmooth[i] = Ground[i] != null && Ground[i].sharedMaterial != null && Ground[i].sharedMaterial.HasProperty(Smooth) ? Ground[i].sharedMaterial.GetFloat(Smooth) : 0.2f;
        }

        private static Color[] Capture(Renderer[] renderers)
        {
            var colours = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
                colours[i] = renderers[i] != null && renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty(Base) ? renderers[i].sharedMaterial.GetColor(Base) : Color.white;
            return colours;
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
            timer = 0.5f;
            var look = SkyState.Daylight * 10 + SkyState.CloudCover * 3 + SkyState.Rain;
            if (force || Mathf.Abs(look - lastLook) > 0.05f)
            {
                lastLook = look;
                var mood = Color.Lerp(new Color(0.55f, 0.6f, 0.68f), Color.white, 1 - SkyState.CloudCover * 0.6f) * Mathf.Lerp(0.35f, 1f, SkyState.Daylight);
                for (var i = 0; i < Water.Length; i++)
                {
                    if (Water[i] == null) continue;
                    Water[i].GetPropertyBlock(block);
                    var colour = waterBase[i] * mood;
                    colour.a = waterBase[i].a;
                    block.SetColor(Base, colour);
                    block.SetFloat(Smooth, Mathf.Lerp(0.96f, 0.82f, SkyState.Rain));
                    Water[i].SetPropertyBlock(block);
                }
            }
            if (!force && Mathf.Abs(SkyState.Wetness - lastWet) < 0.02f) return;
            lastWet = SkyState.Wetness;
            for (var i = 0; i < Ground.Length; i++)
            {
                if (Ground[i] == null) continue;
                Ground[i].GetPropertyBlock(block);
                var colour = groundBase[i] * Mathf.Lerp(1f, 0.66f, lastWet);
                colour.a = groundBase[i].a;
                block.SetColor(Base, colour);
                block.SetFloat(Smooth, Mathf.Lerp(groundSmooth[i], 0.78f, lastWet));
                Ground[i].SetPropertyBlock(block);
            }
        }
    }
}
