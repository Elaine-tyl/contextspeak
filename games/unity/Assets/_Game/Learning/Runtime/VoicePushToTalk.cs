using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Learning
{
    /// Holding V is optional; the visible microphone button also supports click-to-start/stop.
    public sealed class VoicePushToTalk : MonoBehaviour
    {
        private Func<bool> active;
        private Func<string> state;
        private Action toggle;
        private bool held;
        public void Bind(Func<bool> available, Func<string> recording, Action record)
        { active = available; state = recording; toggle = record; }
        private void Update()
        {
            if (Keyboard.current == null || active == null) return;
            if (!active()) { held = false; return; }
            var key = Keyboard.current.vKey;
            if (key.wasPressedThisFrame && state() == "idle") { held = true; toggle(); }
            if (key.wasReleasedThisFrame && held)
            {
                held = false;
                if (state() == "recording" || state() == "requesting") toggle();
            }
        }
    }
}
