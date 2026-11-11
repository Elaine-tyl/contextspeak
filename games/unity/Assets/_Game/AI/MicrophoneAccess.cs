using System.Runtime.InteropServices;

namespace ContextSpeak.Cafe
{
    public enum MicrophoneAccessState { Waiting, Allowed, Denied, TimedOut, Cancelled }
    public static class MicrophoneAccess
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("ContextSpeakMic")] public static extern int CS_MicPermissionStatus();
        [DllImport("ContextSpeakMic")] public static extern void CS_RequestMicPermission();
#endif
        public static MicrophoneAccessState Poll(int nativeStatus, float elapsed, bool current)
        {
            // A late OS response must never reopen a cancelled recording.
            if (!current) return MicrophoneAccessState.Cancelled;
            if (nativeStatus == 3) return MicrophoneAccessState.Allowed;
            if (nativeStatus == 1 || nativeStatus == 2) return MicrophoneAccessState.Denied;
            if (elapsed >= 30) return MicrophoneAccessState.TimedOut;
            return MicrophoneAccessState.Waiting;
        }
    }
}
