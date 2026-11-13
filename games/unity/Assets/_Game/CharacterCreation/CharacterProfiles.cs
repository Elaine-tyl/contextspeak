using System;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Small versioned JSON preference works on desktop and WebGL without changing mission save files.
    public static class CharacterProfiles
    {
        public const string Key = "contextspeak.character-profile";
        public static bool Exists => TryLoad(out _);

        public static bool Decode(string json, out CharacterProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(json) || json.Length > 4096) return false;
            try
            {
                var loaded = JsonUtility.FromJson<CharacterProfile>(json);
                if (loaded == null || loaded.Version != 1 || !loaded.Created) return false;
                loaded.Normalize();
                profile = loaded;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static bool TryLoad(out CharacterProfile profile) => Decode(PlayerPrefs.GetString(Key, ""), out profile);

        public static CharacterProfile Draft()
        {
            if (TryLoad(out var saved)) return saved;
            // Earlier installs stored only the protagonist choice; do not reset that choice or quests.
            var look = PlayerPrefs.GetInt(PlayerAppearance.Preference, 0) == 1 ? PlayerLook.Boy : PlayerLook.Girl;
            return new CharacterProfile { Look = look, Name = look == PlayerLook.Boy ? "Lucas" : "Emma" };
        }

        public static bool TrySave(CharacterProfile source, out string error)
        {
            if (source == null || source.Version != 1) { error = "This character profile format is not supported."; return false; }
            var previous = PlayerPrefs.GetString(Key, "");
            var previousLook = PlayerPrefs.GetInt(PlayerAppearance.Preference, 0);
            error = "";
            try
            {
                if (!string.IsNullOrEmpty(previous))
                {
                    try
                    {
                        var existing = JsonUtility.FromJson<CharacterProfile>(previous);
                        if (existing != null && existing.Version > 1)
                        {
                            error = "This profile was saved by a newer version. Return without changes or update the game.";
                            return false;
                        }
                    }
                    catch (ArgumentException) { /* A confirmed new profile may replace corrupt data. */ }
                }
                var saved = source.Copy();
                saved.Normalize();
                saved.Created = true;
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(saved));
                PlayerPrefs.SetInt(PlayerAppearance.Preference, (int)saved.Look);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception)
            {
                if (previous == "") PlayerPrefs.DeleteKey(Key);
                else PlayerPrefs.SetString(Key, previous);
                PlayerPrefs.SetInt(PlayerAppearance.Preference, previousLook);
                error = "Your character could not be saved. Check available storage and try again.";
                return false;
            }
        }
    }
}
