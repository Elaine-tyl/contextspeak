using UnityEngine;

namespace ContextSpeak.City
{
    /// The sound of a city in daytime, generated at start (no audio files): a soft traffic hum,
    /// a light breeze and birdsong now and then.
    public sealed class CityAmbience : MonoBehaviour
    {
        private AudioSource hum, birds;
        private float nextChirp = 3f;
        private readonly System.Random random = new System.Random(7);

        private void Start()
        {
            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = Hum(8f);
            hum.loop = true;
            hum.volume = 0.16f;
            hum.Play();
            birds = gameObject.AddComponent<AudioSource>();
            birds.volume = 0.22f;
        }

        private void Update()
        {
            nextChirp -= Time.deltaTime;
            if (nextChirp > 0) return;
            nextChirp = 4f + (float)random.NextDouble() * 9f;
            if (ContextSpeak.Life.SkyState.IsNight || ContextSpeak.Life.SkyState.Raining) return;   // birds roost at night and in rain
            birds.PlayOneShot(Chirp(2 + random.Next(4)));
        }

        private AudioClip Hum(float seconds)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            float brown = 0, wind = 0;
            for (var index = 0; index < samples.Length; index++)
            {
                var white = (float)random.NextDouble() * 2 - 1;
                brown = Mathf.Clamp(brown + white * 0.02f, -1, 1) * 0.998f;      // distant traffic rumble
                wind = wind * 0.995f + white * 0.005f;                           // breeze
                var swell = 0.75f + 0.25f * Mathf.Sin(index / (float)rate * 0.7f);
                samples[index] = (brown * 0.6f + wind * 1.5f) * swell;
            }
            // Crossfade the ends so the loop has no click.
            var fade = rate / 2;
            for (var index = 0; index < fade; index++)
            {
                var k = index / (float)fade;
                samples[index] = samples[index] * k + samples[samples.Length - fade + index] * (1 - k);
            }
            var clip = AudioClip.Create("City hum", samples.Length - fade, 1, rate, false);
            var trimmed = new float[samples.Length - fade];
            System.Array.Copy(samples, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }

        private AudioClip Chirp(int notes)
        {
            const int rate = 22050;
            var length = (int)(rate * 0.12f);
            var samples = new float[length * notes + rate / 10];
            for (var note = 0; note < notes; note++)
            {
                var start = note * length + random.Next(rate / 40);
                var pitch = 2600f + (float)random.NextDouble() * 1800f;
                for (var index = 0; index < length && start + index < samples.Length; index++)
                {
                    var t = index / (float)rate;
                    var envelope = Mathf.Sin(Mathf.PI * index / length);
                    var sweep = pitch + 900f * Mathf.Sin(t * 60f);
                    samples[start + index] += Mathf.Sin(2 * Mathf.PI * sweep * t) * envelope * 0.35f;
                }
            }
            var clip = AudioClip.Create("Bird", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
