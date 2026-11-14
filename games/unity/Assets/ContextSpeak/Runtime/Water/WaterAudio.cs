using UnityEngine;

namespace ContextSpeak.Life
{
    /// Water sounds built at start-up from filtered noise and Minnaert bubble chirps (the "plip" of a
    /// collapsing air bubble), so the project needs no recordings: shallow sloshes, run splashes,
    /// a big jump splash, a swim stroke, a drip and a muffled underwater bed. Recorded clips assigned
    /// on CharacterWaterInteraction replace these.
    public static class WaterAudio
    {
        private static AudioClip slosh, splash, plunge, stroke, drip, underwater, exit;
        private const int Rate = 44100;

        public static AudioClip Slosh => slosh ??= Make("Water slosh", 0.35f, 0.5f, 900, 3, 0.12f, 7);
        public static AudioClip Splash => splash ??= Make("Water splash", 0.7f, 0.8f, 2600, 9, 0.05f, 11);
        public static AudioClip Plunge => plunge ??= Make("Water plunge", 1.6f, 1f, 3400, 26, 0.02f, 13);
        public static AudioClip Stroke => stroke ??= Make("Swim stroke", 0.5f, 0.45f, 1400, 5, 0.1f, 17);
        public static AudioClip Drip => drip ??= Make("Water drip", 0.25f, 0.1f, 600, 1, 0.01f, 19);
        public static AudioClip Exit => exit ??= Make("Leave water", 0.9f, 0.35f, 1100, 6, 0.25f, 23);
        public static AudioClip Underwater => underwater ??= Bed("Underwater", 4f);

        /// noise burst (attack → exponential decay) through a one-pole low-pass, plus `bubbles` chirps.
        private static AudioClip Make(string name, float seconds, float noise, float cutoff, int bubbles, float attack, int seed)
        {
            var random = new System.Random(seed);
            var n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];
            var alpha = 1 - Mathf.Exp(-2 * Mathf.PI * cutoff / Rate);
            float low = 0;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var env = Mathf.Clamp01(t / Mathf.Max(0.002f, attack)) * Mathf.Exp(-t * 5.5f / seconds);
                low += alpha * ((float)random.NextDouble() * 2 - 1 - low);
                data[i] = low * noise * env;
            }
            for (var b = 0; b < bubbles; b++)
            {
                var start = (float)random.NextDouble() * seconds * 0.7f;
                var radius = Mathf.Lerp(0.6f, 4f, (float)random.NextDouble()) * 0.001f;      // 0.6–4 mm
                var f0 = 3.26f / radius;                                                       // Minnaert: f ≈ 3.26 / r(m) Hz
                var amp = Mathf.Lerp(0.05f, 0.22f, (float)random.NextDouble());
                var length = Mathf.Min(seconds - start, 0.09f);
                var phase = 0f;
                for (var i = 0; i < length * Rate; i++)
                {
                    var t = i / (float)Rate;
                    var k = Mathf.RoundToInt(start * Rate) + i;
                    if (k >= n) break;
                    var f = f0 * (1 + t * 12);                                                 // rising chirp as the bubble collapses
                    phase += 2 * Mathf.PI * f / Rate;
                    data[k] += Mathf.Sin(phase) * amp * Mathf.Exp(-t * 60);
                }
            }
            Normalise(data, 0.8f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Bed(string name, float seconds)
        {
            var random = new System.Random(29);
            var n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];
            float a = 0, b = 0;
            for (var i = 0; i < n; i++)
            {
                a += 0.004f * ((float)random.NextDouble() * 2 - 1 - a);
                b += 0.02f * (a - b);
                var t = i / (float)Rate;
                data[i] = b * (0.8f + 0.2f * Mathf.Sin(t * 0.7f * Mathf.PI * 2 / seconds * 4));
            }
            // Seamless loop: cross-fade the ends.
            var fade = Rate / 2;
            for (var i = 0; i < fade; i++) { var w = i / (float)fade; data[i] = data[i] * w + data[n - fade + i] * (1 - w); }
            Normalise(data, 0.5f);
            var clip = AudioClip.Create(name, n - fade, 1, Rate, false);
            var trimmed = new float[n - fade];
            System.Array.Copy(data, trimmed, n - fade);
            clip.SetData(trimmed, 0);
            return clip;
        }

        private static void Normalise(float[] data, float peak)
        {
            var max = 0.0001f;
            foreach (var v in data) max = Mathf.Max(max, Mathf.Abs(v));
            for (var i = 0; i < data.Length; i++) data[i] *= peak / max;
        }
    }
}
