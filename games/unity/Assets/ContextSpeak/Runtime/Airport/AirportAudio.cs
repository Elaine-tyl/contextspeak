using UnityEngine;

namespace ContextSpeak.Life
{
    /// Synthesised airport sound (no audio files): terminal crowd murmur, announcement chime,
    /// scanner beeps, conveyor hum, cabin ambience and engine roar. Spatial where it has a source.
    public sealed class AirportAudio : MonoBehaviour
    {
        public static AirportAudio Instance { get; private set; }
        public enum Mode { Terminal, Cabin }
        public Mode Ambience = Mode.Terminal;
        [Range(0, 1)] public float Volume = 0.7f;
        public Transform[] Hums = new Transform[0];       // conveyors, X-ray machine: spatial hum
        public Transform[] Cafes = new Transform[0];      // café chatter: spatial
        private AudioSource bed, engine, oneShots;
        private float engineTarget = 0.25f;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            bed = Loop(gameObject, Ambience == Mode.Cabin ? Noise(5, 0.03f, 0, 11) : Murmur(6, 3), Ambience == Mode.Cabin ? 0.35f : 0.28f, false);
            if (Ambience == Mode.Cabin) engine = Loop(gameObject, Noise(4, 0.008f, 0.2f, 17), 0.2f, false);
            oneShots = gameObject.AddComponent<AudioSource>();
            var hum = Tone(2f, 120, 0.06f);
            foreach (var source in Hums) if (source != null) Loop(source.gameObject, hum, 0.5f, true);
            var chatter = Murmur(5, 9);
            foreach (var source in Cafes) if (source != null) Loop(source.gameObject, chatter, 0.6f, true);
        }

        private AudioSource Loop(GameObject host, AudioClip clip, float volume, bool spatial)
        {
            var source = host.AddComponent<AudioSource>();
            source.clip = clip; source.loop = true; source.volume = volume * Volume;
            source.spatialBlend = spatial ? 1 : 0; source.minDistance = 2; source.maxDistance = 25; source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            return source;
        }

        /// 0 = idle at the gate, 1 = take-off thrust, ~0.5 cruise.
        public void Engines(float level) => engineTarget = level;

        private void Update()
        {
            if (engine == null) return;
            engine.volume = Mathf.MoveTowards(engine.volume, (0.12f + engineTarget * 0.6f) * Volume, Time.deltaTime * 0.25f);
            engine.pitch = Mathf.MoveTowards(engine.pitch, 0.7f + engineTarget * 0.6f, Time.deltaTime * 0.2f);
        }

        public void Chime() { oneShots.PlayOneShot(Chord(new[] { 784f, 659f, 523f }, 0.45f), 0.5f * Volume); }
        public void ScannerOk() { oneShots.PlayOneShot(Chord(new[] { 1046f }, 0.18f), 0.4f * Volume); }
        public void ScannerAlarm() { oneShots.PlayOneShot(Chord(new[] { 440f, 440f, 440f }, 0.15f, true), 0.6f * Volume); }

        private static AudioClip Chord(float[] notes, float each, bool buzz = false)
        {
            const int rate = 22050;
            var length = (int)(rate * each * notes.Length);
            var samples = new float[length];
            for (var i = 0; i < length; i++)
            {
                var note = Mathf.Min(notes.Length - 1, i / (int)(rate * each));
                var t = (i % (int)(rate * each)) / (float)rate;
                var wave = Mathf.Sin(2 * Mathf.PI * notes[note] * t);
                if (buzz) wave = Mathf.Sign(wave) * 0.6f;
                samples[i] = wave * Mathf.Exp(-t * (buzz ? 2 : 5)) * 0.5f;
            }
            var clip = AudioClip.Create("Chime", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip Tone(float seconds, float frequency, float level)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            for (var i = 0; i < samples.Length; i++) samples[i] = (Mathf.Sin(2 * Mathf.PI * frequency * i / rate) + 0.4f * Mathf.Sin(2 * Mathf.PI * frequency * 2 * i / rate)) * level;
            var clip = AudioClip.Create("Hum", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// Crowd murmur: band-limited noise with syllable-rate swells (sounds like distant voices).
        private static AudioClip Murmur(float seconds, int seed)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            var random = new System.Random(seed);
            float low = 0, mid = 0, envelope = 0, target = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                if (i % 1800 == 0) target = (float)random.NextDouble();
                envelope += (target - envelope) * 0.002f;
                var white = (float)random.NextDouble() * 2 - 1;
                low += (white - low) * 0.08f;
                mid += (low - mid) * 0.3f;
                samples[i] = (low - mid) * (0.4f + envelope) * 3f;
            }
            for (var i = 0; i < rate / 10; i++) { var k = i / (rate / 10f); samples[i] = samples[i] * k + samples[samples.Length - rate / 10 + i] * (1 - k); }
            var clip = AudioClip.Create("Murmur", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip Noise(float seconds, float smoothing, float swell, int seed)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            var random = new System.Random(seed);
            float low = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                low += ((float)random.NextDouble() * 2 - 1 - low) * smoothing;
                samples[i] = Mathf.Clamp(low * (smoothing < 0.02f ? 6f : 2.5f) * (1 - swell + swell * Mathf.Sin(i * 6.283f / samples.Length * 2)), -1, 1);
            }
            for (var i = 0; i < rate / 10; i++) { var k = i / (rate / 10f); samples[i] = samples[i] * k + samples[samples.Length - rate / 10 + i] * (1 - k); }
            var clip = AudioClip.Create("Noise", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
