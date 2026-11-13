using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// One output control for NPC speech, ambience and effects. Recording is independent.
    public static class GameAudio
    {
        private const string MuteKey = "ContextSpeak.Audio.Muted", VolumeKey = "ContextSpeak.Audio.Volume";
        public static bool Muted { get; private set; }
        public static float Volume { get; private set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Restore()
        {
            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            Volume = ValidVolume(PlayerPrefs.GetFloat(VolumeKey, 1));
            Apply();
        }

        public static void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        public static void SetVolume(float volume)
        {
            Volume = ValidVolume(volume);
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.Save();
            Apply();
        }

        private static float ValidVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
        private static void Apply() => AudioListener.volume = Muted ? 0 : Volume;
    }
}
