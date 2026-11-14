using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// What an NPC remembers about the player (gameplay facts only — no personal data beyond what the
    /// player chose to say in game): relationship, encounters, preferences, commitments, quest outcomes
    /// and short conversation summaries. Also stores each NPC's world state for save/load.
    [Serializable]
    public sealed class NPCMemory
    {
        public string Npc;
        public int Relationship;
        public int Encounters;
        public int LastSeenDay = -1;
        public List<string> Keys = new List<string>();
        public List<string> Values = new List<string>();
        public List<string> Summaries = new List<string>();
        public List<string> Commitments = new List<string>();

        public string Get(string key) { var i = Keys.IndexOf(key); return i >= 0 ? Values[i] : null; }
        public void Set(string key, string value) { var i = Keys.IndexOf(key); if (i >= 0) Values[i] = value; else { Keys.Add(key); Values.Add(value); } }
        public void Summary(string text) { Summaries.Add(text); while (Summaries.Count > 10) Summaries.RemoveAt(0); }
    }

    [Serializable]
    public sealed class NPCSavedState
    {
        public string Npc, Scene, Activity, Place;
        public float X, Y, Z, Hunger, Fatigue, Social, Comfort, Recreation;
    }

    [Serializable]
    public sealed class LivingWorldSave
    {
        public int Version = 1;
        public int Day;
        public float Hour = 8;
        public List<NPCMemory> Memories = new List<NPCMemory>();
        public List<NPCSavedState> States = new List<NPCSavedState>();
    }

    public static class NPCRelationshipMemory
    {
        private static LivingWorldSave data;
        public static string FilePath = Path.Combine(Application.persistentDataPath, "living_npcs.json");
        public static LivingWorldSave Data { get { if (data == null) Load(); return data; } }

        public static void Load()
        {
            try { data = File.Exists(FilePath) ? JsonUtility.FromJson<LivingWorldSave>(File.ReadAllText(FilePath)) : null; }
            catch (Exception e) { Debug.LogWarning("Living NPC save unreadable (" + e.Message + ") — starting fresh."); data = null; }
            data ??= new LivingWorldSave();
        }

        public static void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(Data, false)); }
            catch (Exception e) { Debug.LogWarning("Living NPC save failed: " + e.Message); }
        }

        public static NPCMemory Of(string npc)
        {
            var memory = Data.Memories.FirstOrDefault(m => m.Npc == npc);
            if (memory == null) { memory = new NPCMemory { Npc = npc }; Data.Memories.Add(memory); }
            return memory;
        }

        public static void Remember(string npc, string key, string value) => Of(npc).Set(key, value);
        public static void Encounter(string npc, int day) { var m = Of(npc); if (m.LastSeenDay != day) { m.Encounters++; m.LastSeenDay = day; } }
        public static void Relate(string npc, int delta, string why) { var m = Of(npc); m.Relationship = Mathf.Clamp(m.Relationship + delta, -100, 100); if (!string.IsNullOrEmpty(why)) m.Summary(why); }

        public static NPCSavedState State(string npc, string scene) => Data.States.FirstOrDefault(s => s.Npc == npc && s.Scene == scene);

        public static void Store(NPCSavedState state)
        {
            Data.States.RemoveAll(s => s.Npc == state.Npc && s.Scene == state.Scene);
            Data.States.Add(state);
        }

        /// For tests: replace the in-memory data (and optionally the file location).
        public static void Use(LivingWorldSave save, string path = null) { data = save; if (path != null) FilePath = path; }
    }
}
