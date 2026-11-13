using System;
using System.Collections;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Slice
{
    /// Plays NPC speech at the band's speed, with pitch-preserving "Slower" and "Repeat".
    public sealed class SliceVoice : MonoBehaviour
    {
        private AudioSource source;
        private float[] lastSamples;
        private int lastRate, generation;
        public bool Playing => source != null && source.isPlaying;
        public bool HasLine => lastSamples != null;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0;
        }

        public void Say(ITextToSpeech tts, string text, float rate, Action<string> failed, Action started = null)
        {
            Cancel();
            if (tts == null || !tts.Available || string.IsNullOrWhiteSpace(text)) { lastSamples = null; return; } // subtitles only
            var current = generation;
            StartCoroutine(tts.Speak(text, clip => {
                if (current != generation || !isActiveAndEnabled) { Destroy(clip); return; }
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                lastRate = clip.frequency;
                lastSamples = samples;
                Destroy(clip);
                Play(rate);
                started?.Invoke();
            }, error => { if (current == generation) failed?.Invoke(error); }));
        }

        /// Replays the last line, optionally slower. Pitch is preserved.
        public void Play(float rate)
        {
            if (lastSamples == null) return;
            source.Stop();
            if (source.clip != null) Destroy(source.clip);
            var stretched = AudioStretch.Stretch(lastSamples, lastRate, rate);
            var clip = AudioClip.Create("NPC line", Math.Max(1, stretched.Length), 1, lastRate, false);
            clip.SetData(stretched, 0);
            source.clip = clip;
            source.Play();
        }

        public void Cancel()
        {
            generation++;
            if (source == null) return;
            source.Stop();
        }

        private void OnDestroy()
        {
            Cancel();
            if (source != null && source.clip != null) Destroy(source.clip);
        }
    }
}
