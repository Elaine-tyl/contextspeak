using UnityEngine;

namespace ContextSpeak.Life
{
    /// Weather sound, synthesised (no audio files): rain hiss with patter, wind that follows the
    /// wind speed, crickets at night and the odd distant thunder roll in heavy rain.
    public sealed class SkyAudio : MonoBehaviour
    {
        [Range(0, 1)] public float Volume = 0.8f;
        public bool Interior;      // muffled outdoor sounds inside buildings
        private AudioSource rain, wind, crickets, thunder;
        private float nextThunder = 40;
        private readonly System.Random random = new System.Random(7);

        private void Start()
        {
            rain = Loop("Rain", Noise(4f, 0.65f, 0.0f, 31, true));
            wind = Loop("Wind", Noise(7f, 0.012f, 0.5f, 5, false));
            crickets = Loop("Crickets", Crickets(3f));
            thunder = gameObject.AddComponent<AudioSource>();
            thunder.spatialBlend = 0;
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0;
            source.spatialBlend = 0;
            source.Play();
            return source;
        }

        private void Update()
        {
            var muffle = Interior ? 0.35f : 1f;
            var dt = Time.deltaTime * 0.8f;
            rain.volume = Mathf.MoveTowards(rain.volume, SkyState.Rain * 0.55f * Volume * muffle, dt);
            rain.pitch = Interior ? 0.7f : 1f;
            wind.volume = Mathf.MoveTowards(wind.volume, Mathf.Clamp01(SkyState.WindSpeed / 14f) * 0.4f * Volume * muffle, dt);
            wind.pitch = 0.8f + Mathf.Clamp01(SkyState.WindSpeed / 14f) * 0.4f;
            crickets.volume = Mathf.MoveTowards(crickets.volume, (SkyState.IsNight && !SkyState.Raining ? 0.18f : 0) * Volume * muffle, dt * 0.3f);
            if (SkyState.Rain > 0.7f && (nextThunder -= Time.deltaTime) <= 0)
            {
                nextThunder = 35 + (float)random.NextDouble() * 60;
                thunder.pitch = 0.7f + (float)random.NextDouble() * 0.3f;
                thunder.PlayOneShot(Noise(5f, 0.004f, 0.9f, random.Next(100), false, true), 0.9f * Volume * muffle);
            }
        }

        /// Filtered noise. smoothing small = rumble, large = hiss. patter adds random raindrop ticks.
        private static AudioClip Noise(float seconds, float smoothing, float swell, int seed, bool patter, bool decay = false)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            var random = new System.Random(seed);
            float low = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                low += ((float)random.NextDouble() * 2 - 1 - low) * smoothing;
                var value = low * (smoothing < 0.1f ? 4f : 0.9f);
                if (swell > 0) value *= 1 - swell + swell * (0.5f + 0.5f * Mathf.Sin(i * 2 * Mathf.PI / samples.Length * 3));
                if (patter && random.NextDouble() < 0.0025) value += ((float)random.NextDouble() - 0.5f) * 1.2f;
                if (decay) value *= Mathf.Exp(-3f * i / samples.Length) * Mathf.Clamp01(i / (rate * 0.3f));
                samples[i] = Mathf.Clamp(value, -1, 1);
            }
            if (!decay)
                for (var i = 0; i < rate / 10; i++)
                {
                    var k = i / (rate / 10f);
                    samples[i] = samples[i] * k + samples[samples.Length - rate / 10 + i] * (1 - k);
                }
            var clip = AudioClip.Create("Weather", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip Crickets(float seconds)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                var pulse = (t * 14) % 1 < 0.45f && (t * 0.9f) % 1 < 0.6f ? 1f : 0f;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 4300 * t) * pulse * 0.12f + Mathf.Sin(2 * Mathf.PI * 3900 * (t + 0.37f)) * ((t * 11 + 0.3f) % 1 < 0.4f ? 0.07f : 0);
            }
            var clip = AudioClip.Create("Crickets", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
