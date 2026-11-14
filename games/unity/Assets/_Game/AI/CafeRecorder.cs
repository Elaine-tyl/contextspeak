using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [Serializable] internal class MicActivity { public int generation; public float level; }
    [Serializable] internal class MicMessage { public int generation; public string value; }
    public sealed class CafeRecorder : MonoBehaviour
    {
        public event Action<byte[]> Finished;
        public event Action<string> Failed;
        public event Action Changed;
        public string State { get; private set; } = "idle";
        public int MaxSeconds = 20;
        public float Level { get; private set; }
        private float nextMeter;
        private readonly float[] meterSamples = new float[256];
        public bool WaitingForPermission { get; private set; }
        private AudioClip clip;
        private int generation;
        private float began;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_MicStart(string owner, int generation, int maxSeconds);
        [DllImport("__Internal")] private static extern void CS_MicStop();
        [DllImport("__Internal")] private static extern void CS_MicCancel();
#endif
        public void Begin()
        {
            Cancel();
            State = "requesting";
            Changed?.Invoke();
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_MicStart(gameObject.name, generation, Mathf.Clamp(MaxSeconds, 20, 120));
#else
            StartCoroutine(StartNative(generation));
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        private IEnumerator StartNative(int current)
        {
            var requested = Time.realtimeSinceStartup;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            WaitingForPermission = true;
            var available = true;
            try { MicrophoneAccess.CS_RequestMicPermission(); }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException)
            {
                available = false;
            }
            if (!available) { Fail("Microphone support is missing. Rebuild the desktop app, then retry."); yield break; }
            MicrophoneAccessState access;
            do
            {
                access = MicrophoneAccess.Poll(MicrophoneAccess.CS_MicPermissionStatus(), Time.realtimeSinceStartup - requested, current == generation);
                if (access == MicrophoneAccessState.Waiting) yield return null;
            } while (access == MicrophoneAccessState.Waiting);
            if (access == MicrophoneAccessState.Cancelled) yield break;
            WaitingForPermission = false;
            if (access != MicrophoneAccessState.Allowed)
            {
                Fail(access == MicrophoneAccessState.Denied
                    ? "Allow ContextSpeak in System Settings > Privacy & Security > Microphone, then retry."
                    : "Microphone permission timed out. Click Record reply to try again.");
                yield break;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            WaitingForPermission = true;
            var permission = Application.RequestUserAuthorization(UserAuthorization.Microphone);
            while (!permission.isDone && current == generation && Time.realtimeSinceStartup - requested < 30) yield return null;
            if (current != generation) yield break;
            WaitingForPermission = false;
            if (!permission.isDone || !Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Fail("Allow microphone access in Settings, then retry.");
                yield break;
            }
#endif
            if (current != generation) yield break;
            if (Microphone.devices.Length == 0)
            {
                Fail("No microphone found. Connect one and retry recording.");
                yield break;
            }
            try { clip = Microphone.Start(null, false, Mathf.Clamp(MaxSeconds, 20, 120), 16000); }
            catch (UnityException) { clip = null; }
            if (clip == null) { Fail("The microphone could not start. Check your input device, then retry."); yield break; }
            began = Time.realtimeSinceStartup;
            while (current == generation && Microphone.GetPosition(null) <= 0 && Time.realtimeSinceStartup - began < 5) yield return null;
            if (current != generation) yield break;
            if (Microphone.GetPosition(null) <= 0) { Fail("The microphone did not send audio. Check your input device, then retry."); yield break; }
            State = "recording";
            Changed?.Invoke();
        }
#endif
        public void Stop()
        {
            if (State != "recording") return;
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_MicStop();
#else
            var frames = Microphone.GetPosition(null);
            if (frames <= 0 && clip != null && Time.realtimeSinceStartup - began >= Mathf.Clamp(MaxSeconds, 20, 120) - .1f) frames = clip.samples;
            Microphone.End(null);
            var recorded = clip;
            clip = null;
            State = "idle";
            Changed?.Invoke();
            try
            {
                if (recorded == null || frames <= 0) throw new FormatException("Speak a little longer, then stop.");
                var samples = new float[frames * recorded.channels];
                recorded.GetData(samples, 0);
                if (!ContextSpeak.Learning.VoiceCoach.HasSpeech(samples, recorded.frequency))
                    throw new FormatException("No clear voice detected. Check your microphone and speak again.");
                Finished?.Invoke(CafeWave.Encode(samples, recorded.frequency, recorded.channels));
            }
            catch (FormatException error) { Failed?.Invoke(error.Message); }
            finally { if (recorded != null) Destroy(recorded); }
#endif
        }
        public void OnMicReady(string json)
        {
            var message = Read(json);
            if (message == null || State != "requesting") return;
            State = "recording";
            Changed?.Invoke();
        }
        public void OnMicData(string json)
        {
            var message = Read(json);
            if (message == null || State != "recording") return;
            State = "idle";
            Changed?.Invoke();
            try
            {
                var bytes = Convert.FromBase64String(message.value);
                if (bytes.Length > 8000000) throw new FormatException("Use a shorter recording.");
                var decoded = CafeWave.Decode(bytes);
                if (!ContextSpeak.Learning.VoiceCoach.HasSpeech(decoded.Samples, decoded.Rate))
                { Failed?.Invoke("No clear voice detected. Check your microphone and speak again."); return; }
                Finished?.Invoke(bytes);
            }
            catch (FormatException) { Failed?.Invoke("Recording could not be read. Please retry."); }
        }
        public void OnMicActivity(string json)
        {
            if (State != "recording" || string.IsNullOrEmpty(json) || json.Length > 250) return;
            try
            {
                var activity = JsonUtility.FromJson<MicActivity>(json);
                if (activity == null || activity.generation != generation || float.IsNaN(activity.level)) return;
                Level = Mathf.Clamp01(activity.level); Changed?.Invoke();
            }
            catch (ArgumentException) { }
        }
        public void OnMicError(string json)
        {
            var message = Read(json);
            if (message != null) Fail(message.value);
        }
        private MicMessage Read(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 11000000) return null;
            try
            {
                var message = JsonUtility.FromJson<MicMessage>(json);
                return message != null && message.generation == generation ? message : null;
            }
            catch (ArgumentException) { return null; }
        }
        private void Fail(string message)
        {
            Cancel();
            Failed?.Invoke(message);
        }
        public void Cancel()
        {
            generation++; Level = 0;
            WaitingForPermission = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_MicCancel();
#else
            if (Microphone.IsRecording(null)) Microphone.End(null);
#endif
            if (clip != null) Destroy(clip);
            clip = null;
            State = "idle";
            Changed?.Invoke();
        }
        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (State == "recording" && clip != null && Time.unscaledTime > nextMeter)
            {
                nextMeter = Time.unscaledTime + .1f;
                var position = Microphone.GetPosition(null);
                if (position >= meterSamples.Length && clip.GetData(meterSamples, position - meterSamples.Length))
                {
                    var peak = 0f;
                    foreach (var sample in meterSamples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                    Level = Mathf.Clamp01(peak * 4); Changed?.Invoke();
                }
            }
            if (State == "recording" && Time.realtimeSinceStartup - began >= Mathf.Clamp(MaxSeconds, 20, 120)) Stop();
#endif
        }
        private void OnDestroy()
        {
            Cancel();
        }
    }
}
