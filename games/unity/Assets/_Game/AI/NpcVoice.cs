using System;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    // Reused by every world. Closing or recording invalidates even a late TTS callback.
    public sealed class NpcVoice : MonoBehaviour
    {
        private AudioSource audioSource;
        private int generation;
        public bool Playing => audioSource != null && audioSource.isPlaying;
        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
        }
        public void Say(CafeClient client, string text, Action<string> failed, Action ready = null)
        {
            Cancel();
            var current = generation;
            StartCoroutine(client.Speak(text, clip => {
                if (current != generation || !isActiveAndEnabled)
                {
                    Destroy(clip);
                    return;
                }
                audioSource.clip = clip;
                audioSource.Play();
                ready?.Invoke();
            }, error => {
                if (current == generation && isActiveAndEnabled) failed(error);
            }));
        }
        public void Cancel()
        {
            generation++;
            if (audioSource == null) return;
            audioSource.Stop();
            if (audioSource.clip != null) Destroy(audioSource.clip);
            audioSource.clip = null;
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy() { Cancel(); }
    }
}
