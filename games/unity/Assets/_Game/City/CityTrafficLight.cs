using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.City
{
    /// One traffic light head. Lamp colours follow CitySignals.
    public sealed class CityTrafficLight : MonoBehaviour
    {
        public bool AlongX;
        public Renderer Red, Yellow, GreenLamp;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private int last = -1;

        private void Update()
        {
            var state = CitySignals.State(AlongX);
            if (state == last) return;
            last = state;
            Set(Red, state == 0, new Color(1f, 0.15f, 0.12f));
            Set(Yellow, state == 1, new Color(1f, 0.72f, 0.1f));
            Set(GreenLamp, state == 2, new Color(0.15f, 1f, 0.4f));
        }

        private void Set(Renderer lamp, bool on, Color colour)
        {
            if (lamp == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            block.SetColor(BaseColor, on ? colour : colour * 0.12f);
            block.SetColor(Emission, on ? colour * 3f : Color.black);
            lamp.SetPropertyBlock(block);
        }
    }
}
