using System;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class MicrophoneChecks
    {
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("Microphone regression: " + message);
        }
        public static void Verify()
        {
            Check(MicrophoneAccess.Poll(3, 0, true) == MicrophoneAccessState.Allowed, "Granted native permission must start the recording flow.");
            Check(MicrophoneAccess.Poll(2, 0, true) == MicrophoneAccessState.Denied, "Denied permission must return a retryable error.");
            Check(MicrophoneAccess.Poll(1, 0, true) == MicrophoneAccessState.Denied, "Restricted access cannot start recording.");
            Check(MicrophoneAccess.Poll(0, 29.9f, true) == MicrophoneAccessState.Waiting, "An open OS permission prompt may still finish.");
            Check(MicrophoneAccess.Poll(0, 30, true) == MicrophoneAccessState.TimedOut, "A missing OS response cannot leave Cancel request forever.");
            Check(MicrophoneAccess.Poll(3, 1, false) == MicrophoneAccessState.Cancelled, "Late permission after close cannot start a microphone.");
            Check(MicrophoneAccess.Poll(3, 40, true) == MicrophoneAccessState.Allowed, "A granted permission wins over elapsed waiting time after focus returns.");
            Debug.Log("ContextSpeak native microphone permission checks passed");
        }
    }
}
