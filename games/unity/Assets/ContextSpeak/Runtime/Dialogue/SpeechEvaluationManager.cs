using System;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Microphone → local speech-to-text (the free ContextSpeak companion, `pnpm world:dev`) →
    /// text for the dialogue state machine. NPC lines are spoken by the companion's local voice,
    /// with a slower playback option. When the companion is not running the manager switches to an
    /// explicitly labelled MOCK mode in which the learner types what they would say.
    /// No API keys live in the client.
    public sealed class SpeechEvaluationManager : MonoBehaviour
    {
        public event Action<string, bool> Heard;   // text, spoken
        public string Status { get; private set; } = "Connecting to the local speech companion…";
        public bool Busy { get; private set; }
        public bool Mock => SaveManager.Data.forceMock || client == null || !client.Connected;
        public string RecorderState => recorder != null ? recorder.State : "idle";
        public bool Speaking => voice != null && voice.isPlaying;
        private CafeClient client;
        private CafeRecorder recorder;
        private AudioSource voice;
        private int generation;

        private void Awake()
        {
            client = gameObject.AddComponent<CafeClient>();
            recorder = gameObject.AddComponent<CafeRecorder>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0;
            recorder.Finished += Transcribe;
            recorder.Failed += error => Status = error;
            Connect();
        }

        public void Connect()
        {
            StartCoroutine(client.Connect(error => Status = error == ""
                ? "Speech companion connected · press R to speak"
                : "MOCK MODE — speech companion offline (run pnpm world:dev). Type what you would say."));
        }

        public void ToggleRecording()
        {
            if (Busy) return;
            if (Mock) { Status = "MOCK MODE — type your answer in the box below."; return; }
            if (recorder.State == "recording") { recorder.Stop(); return; }
            if (recorder.State == "requesting") { recorder.Cancel(); return; }
            voice.Stop();
            recorder.Begin();
            Status = "Listening… press R again to stop.";
        }

        private void Transcribe(byte[] wave)
        {
            var current = ++generation;
            Busy = true;
            Status = "Converting your speech to text…";
            StartCoroutine(client.Transcribe(wave, text => {
                if (current != generation) return;
                Busy = false;
                Status = "Heard you.";
                Heard?.Invoke(text, true);
            }, error => {
                if (current != generation) return;
                Busy = false;
                Status = error;
            }));
        }

        /// Mock mode only (or a typed rehearsal, which never counts as speaking practice).
        public void SubmitTyped(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Heard?.Invoke(text.Trim(), false);
        }

        public void Speak(string line, bool slow = false)
        {
            if (string.IsNullOrWhiteSpace(line) || Mock || recorder.State != "idle") return;
            var current = ++generation;
            StartCoroutine(client.Speak(line, clip => {
                if (current != generation) { Destroy(clip); return; }
                voice.Stop();
                if (voice.clip != null) Destroy(voice.clip);
                voice.clip = clip;
                voice.pitch = slow ? 0.8f : 1f;   // slow playback for lower bands or on request
                voice.Play();
            }, error => Status = error));
        }

        public void Cancel()
        {
            generation++;
            Busy = false;
            if (recorder != null) recorder.Cancel();
            if (voice != null) voice.Stop();
            if (client != null) client.Cancel();
        }
    }
}
