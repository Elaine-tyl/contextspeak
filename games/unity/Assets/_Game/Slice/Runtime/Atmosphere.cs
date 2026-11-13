using System;
using UnityEngine;

namespace ContextSpeak.Slice
{
    /// Day/night lighting, weather, rain, fireflies/butterflies and ambient birdsong driven by WorldClock.
    public sealed class Atmosphere : MonoBehaviour
    {
        public Light Sun;
        public Light[] WarmLights = new Light[0];
        public ParticleSystem Rain, Butterflies, Fireflies;
        public Transform[] Birds = new Transform[0];
        public Renderer Pond;
        private AudioSource ambience, chirps;
        private float nextChirp;
        private System.Random random = new System.Random(11);
        private static readonly Color Dawn = new Color(1f, 0.72f, 0.5f), Noon = new Color(1f, 0.93f, 0.8f), Dusk = new Color(1f, 0.6f, 0.38f), Night = new Color(0.45f, 0.55f, 0.85f);

        private void Awake()
        {
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.volume = 0.18f;
            ambience.clip = Noise("Forest wind", 6f, 0.02f);
            ambience.Play();
            chirps = gameObject.AddComponent<AudioSource>();
            chirps.volume = 0.25f;
        }

        public void Apply(WorldClock clock)
        {
            var hour = clock.Minute / 60f;
            var daylight = clock.Daylight;
            var rain = clock.Weather == Weather.Rain;
            var cloud = clock.Weather != Weather.Clear ? 0.55f : 1f;
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-10, 190, Mathf.InverseLerp(5, 21, hour)), -38, 0);
                Sun.intensity = Mathf.Lerp(0.08f, 1.7f, daylight) * cloud;
                Sun.color = hour < 9 ? Color.Lerp(Dawn, Noon, (hour - 5) / 4f) : hour > 17 ? Color.Lerp(Noon, Dusk, (hour - 17) / 4f) : Noon;
                if (daylight <= 0) Sun.color = Night;
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(0.35f, 1f, daylight) * (rain ? 0.8f : 1);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.1f, 0.8f, daylight) * (rain ? 0.7f : 1);
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
                RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1f, daylight) * (rain ? 0.7f : 1));
            RenderSettings.fogDensity = rain ? 0.018f : 0.004f;
            foreach (var warm in WarmLights) if (warm != null) warm.intensity = Mathf.Lerp(3.2f, 1.6f, daylight);
            Toggle(Rain, rain);
            Toggle(Butterflies, daylight > 0.6f && !rain);
            Toggle(Fireflies, daylight < 0.3f && !rain);
            ambience.volume = rain ? 0.4f : 0.16f;
            if (Pond != null && Pond.material.HasProperty("_BaseColor"))
                Pond.material.SetColor("_BaseColor", Color.Lerp(new Color(0.12f, 0.2f, 0.28f), new Color(0.28f, 0.45f, 0.42f), daylight) * (rain ? 0.85f : 1));
            // Birds circle the trees during the day.
            for (var i = 0; i < Birds.Length; i++)
            {
                if (Birds[i] == null) continue;
                Birds[i].gameObject.SetActive(daylight > 0.3f && !rain);
                var angle = Time.time * (0.25f + i * 0.04f) + i * 2.1f;
                Birds[i].localPosition = new Vector3(Mathf.Cos(angle) * (6 + i), 7 + Mathf.Sin(angle * 2) * 0.8f, Mathf.Sin(angle) * (6 + i));
                Birds[i].localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, Mathf.Sin(Time.time * 12 + i) * 18);
            }
            if (daylight > 0.4f && !rain && Time.time > nextChirp)
            {
                nextChirp = Time.time + 3 + (float)random.NextDouble() * 7;
                chirps.pitch = 0.9f + (float)random.NextDouble() * 0.3f;
                chirps.PlayOneShot(Chirp(random.Next(3)));
            }
        }

        private static void Toggle(ParticleSystem system, bool on)
        {
            if (system == null) return;
            if (on && !system.isPlaying) system.Play();
            if (!on && system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static AudioClip Noise(string name, float seconds, float smoothing)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            var random = new System.Random(3);
            float low = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                low += ((float)random.NextDouble() * 2 - 1 - low) * smoothing;
                var swell = 0.6f + 0.4f * Mathf.Sin(i * 2 * Mathf.PI / samples.Length * 2);
                samples[i] = low * swell * 3f;
            }
            // Crossfade the ends so the loop has no click.
            for (var i = 0; i < rate / 10; i++)
            {
                var k = i / (rate / 10f);
                samples[i] = samples[i] * k + samples[samples.Length - rate / 10 + i] * (1 - k);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip Chirp(int variant)
        {
            const int rate = 22050;
            var samples = new float[rate / 3];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                var note = (int)(t * (8 + variant * 3)) % 2 == 0;
                var frequency = 2600 + variant * 500 + Mathf.Sin(t * 60) * 600;
                samples[i] = note ? Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Exp(-((t * 9) % 1) * 6) * 0.3f : 0;
            }
            var clip = AudioClip.Create("Birdsong", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
