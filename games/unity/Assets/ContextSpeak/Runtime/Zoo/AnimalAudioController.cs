using UnityEngine;

namespace ContextSpeak.Life
{
    /// Occasional, positional animal calls: only when a visitor is near, never in a constant loop, and
    /// quieter at night. Clips are optional — with none assigned the animal is simply silent (the zoo
    /// builder reports which recordings are missing).
    [RequireComponent(typeof(AudioSource))]
    public sealed class AnimalAudioController : MonoBehaviour
    {
        public AudioClip[] Calls = new AudioClip[0];
        public float MinGap = 55, MaxGap = 160, HearWithin = 60;
        private AudioSource source;
        private AnimalController controller;
        private float wait;

        private void Start()
        {
            source = GetComponent<AudioSource>();
            controller = GetComponent<AnimalController>();
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4;
            source.maxDistance = HearWithin;
            source.playOnAwake = false;
            wait = Random.Range(MinGap * 0.3f, MaxGap);
        }

        private void Update()
        {
            if (Calls.Length == 0 || (wait -= Time.deltaTime) > 0) return;
            wait = Random.Range(MinGap, MaxGap) * (SkyState.IsNight ? 2.5f : 1);
            if (controller != null && controller.State == AnimalState.Sleeping) return;
            var camera = Camera.main;
            if (camera == null || Vector3.Distance(camera.transform.position, transform.position) > HearWithin) return;
            source.pitch = Random.Range(0.94f, 1.06f);
            source.PlayOneShot(Calls[Random.Range(0, Calls.Length)], Random.Range(0.6f, 0.9f));
        }
    }
}
