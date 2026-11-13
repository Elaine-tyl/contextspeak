using System;
using System.Reflection;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class GameAudioChecks
    {
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("Game audio: " + message);
        }

        public static void Verify()
        {
            var type = typeof(CafeClient).Assembly.GetType("ContextSpeak.Cafe.GameAudio");
            Check(type != null, "Persistent master audio settings must exist.");
            const string muteKey = "ContextSpeak.Audio.Muted", volumeKey = "ContextSpeak.Audio.Volume";
            var hadMute = PlayerPrefs.HasKey(muteKey);
            var hadVolume = PlayerPrefs.HasKey(volumeKey);
            var mute = PlayerPrefs.GetInt(muteKey);
            var volume = PlayerPrefs.GetFloat(volumeKey);
            var listener = AudioListener.volume;
            void Call(string method, params object[] values) => type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, values);
            try
            {
                PlayerPrefs.DeleteKey(muteKey);
                PlayerPrefs.DeleteKey(volumeKey);
                Call("Restore");
                Check(Mathf.Approximately(AudioListener.volume, 1), "New installs start audible.");
                Call("SetVolume", 0.35f);
                Call("SetMuted", true);
                Check(AudioListener.volume == 0, "Mute silences all Unity audio immediately.");
                Call("Restore");
                Check(AudioListener.volume == 0, "Mute survives restoring preferences.");
                Call("SetMuted", false);
                Check(Mathf.Approximately(AudioListener.volume, 0.35f), "Unmute restores the chosen volume.");
                Call("SetVolume", -3f);
                Check(AudioListener.volume == 0, "Negative volume is clamped.");
                Call("SetVolume", float.NaN);
                Check(Mathf.Approximately(AudioListener.volume, 1), "Invalid volume cannot poison the listener.");
                Debug.Log("Persistent game audio checks passed");
            }
            finally
            {
                if (hadMute) PlayerPrefs.SetInt(muteKey, mute); else PlayerPrefs.DeleteKey(muteKey);
                if (hadVolume) PlayerPrefs.SetFloat(volumeKey, volume); else PlayerPrefs.DeleteKey(volumeKey);
                PlayerPrefs.Save();
                Call("Restore");
                AudioListener.volume = listener;
            }
        }
    }
}
