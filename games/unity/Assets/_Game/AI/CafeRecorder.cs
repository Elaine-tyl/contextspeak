using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [Serializable] internal class MicMessage { public int generation; public string value; }
    public sealed class CafeRecorder : MonoBehaviour
    {
        public event Action<byte[]> Finished;
        public event Action<string> Failed;
        public event Action Changed;
        public string State { get; private set; } = "idle";
        public bool WaitingForPermission { get; private set; }
        private AudioClip clip;
        private int generation;
        private float began;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_MicStart(string owner, int generation);
        [DllImport("__Internal")] private static extern void CS_MicStop();
        [DllImport("__Internal")] private static extern void CS_MicCancel();
#endif
        public void Begin()
        {
            Cancel();
            State = "requesting";
            Changed?.Invoke();
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_MicStart(gameObject.name, generation);
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
                Fail("No microphone found. Connect one, then retry or type a reply.");
                yield break;
            }
            try { clip = Microphone.Start(null, false, 20, 16000); }
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
            if (frames <= 0 && clip != null && Time.realtimeSinceStartup - began >= 19.9f) frames = clip.samples;
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
                CafeWave.Decode(bytes);
                Finished?.Invoke(bytes);
            }
            catch (FormatException) { Failed?.Invoke("Recording could not be read. Please retry."); }
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
            generation++;
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
            if (State == "recording" && Time.realtimeSinceStartup - began >= 20) Stop();
#endif
        }
        private void OnDestroy()
        {
            Cancel();
        }
    }
}
