using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Slice
{
    /// Procedural footstep sounds per surface, so the controller has audio feedback without imported clips.
    [RequireComponent(typeof(AudioSource))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        private AudioSource source;
        private readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        private System.Random random = new System.Random(7);
        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.spatialBlend = 0.6f;
            source.volume = 0.35f;
            foreach (var surface in new[] { "wood", "grass", "stone", "gravel", "water" })
                clips[surface] = Enumerable.Range(0, 3).Select(variant => Make(surface, variant)).ToArray();
            var player = GetComponent<Cafe.CafePlayer>();
            if (player != null) player.Footstep += Play;
        }
        public void Play(string surface, float loudness)
        {
            if (!clips.TryGetValue(surface, out var set)) set = clips["stone"];
            source.pitch = 0.92f + (float)random.NextDouble() * 0.16f;
            source.PlayOneShot(set[random.Next(set.Length)], 0.4f + loudness * 0.6f);
        }
        private AudioClip Make(string surface, int variant)
        {
            const int rate = 22050;
            var length = surface == "water" ? 0.22f : surface == "grass" ? 0.12f : 0.09f;
            var samples = new float[(int)(rate * length)];
            var noise = new System.Random(surface.GetHashCode() + variant);
            float low = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                var white = (float)(noise.NextDouble() * 2 - 1);
                low += (white - low) * (surface == "wood" ? 0.08f : surface == "stone" ? 0.35f : surface == "water" ? 0.05f : 0.18f);
                var body = surface == "wood" ? Mathf.Sin(i * 2 * Mathf.PI * (140 + variant * 15) / rate) * 0.6f : 0;
                var envelope = Mathf.Exp(-t * (surface == "grass" || surface == "water" ? 6 : 14));
                samples[i] = (low * (surface == "gravel" ? 1.6f : 1) + body) * envelope * 0.6f;
            }
            var clip = AudioClip.Create("Footstep " + surface, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
