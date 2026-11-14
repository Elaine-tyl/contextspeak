using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Saved state of the player's villa: doors (open / locked), garage door, gate, TV, lights mode,
    /// where the car is parked, chores done today, the chosen outfit and the speaking topics completed.
    /// File: <persistentDataPath>/villa_state.json. Saved on every change (debounced) and on quit.
    [Serializable]
    public sealed class VillaSaveData
    {
        [Serializable] public sealed class DoorEntry { public string id; public bool open; public bool locked; }
        public List<DoorEntry> doors = new List<DoorEntry>();
        public bool tvOn;
        public bool hasCar;
        public float carX, carY, carZ, carYaw;
        public int plantsWateredDay = -1, petFedDay = -1, laundryDay = -1, cookedDay = -1;
        public int outfit;
        public List<string> topicsDone = new List<string>();
        public string lightsMode = "auto";       // auto | on | off
        public int elevatorFloor;
        public List<string> cars = new List<string>();             // "<car object>:x,y,z,yaw"
        public List<string> roomLights = new List<string>();   // "Room:on" / "Room:off" (absent = auto)
        public bool holdingUmbrella;
    }

    public static class VillaState
    {
        public static VillaSaveData Data { get; private set; } = new VillaSaveData();
        public static bool Loaded { get; private set; }
        public static event Action Changed;
        private static bool dirty;
        private static float dirtyAt;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "villa_state.json");

        /// Day counter used for once-per-day chores (in-game days are not tracked globally, so real date).
        public static int Today => DateTime.Now.DayOfYear + DateTime.Now.Year * 400;

        public static void Load()
        {
            try
            {
                Data = File.Exists(FilePath) ? JsonUtility.FromJson<VillaSaveData>(File.ReadAllText(FilePath)) ?? new VillaSaveData() : new VillaSaveData();
            }
            catch (Exception e) { Debug.LogWarning("[Villa] state not read (" + e.Message + ") — starting fresh"); Data = new VillaSaveData(); }
            Loaded = true;
        }

        public static void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true)); dirty = false; }
            catch (Exception e) { Debug.LogWarning("[Villa] state not saved: " + e.Message); }
        }

        public static void Touch() { dirty = true; dirtyAt = Time.unscaledTime; Changed?.Invoke(); }

        /// Called every frame by VillaDirector: writes 1 s after the last change.
        public static void Tick() { if (dirty && Time.unscaledTime - dirtyAt > 1f) Save(); }

        public static VillaSaveData.DoorEntry Door(string id)
        {
            foreach (var d in Data.doors) if (d.id == id) return d;
            var entry = new VillaSaveData.DoorEntry { id = id };
            Data.doors.Add(entry);
            return entry;
        }

        public static bool TopicDone(string topic) => Data.topicsDone.Contains(topic);
        public static void CompleteTopic(string topic) { if (!Data.topicsDone.Contains(topic)) Data.topicsDone.Add(topic); Touch(); }

        /// For tests: forget everything (in memory and on disk).
        public static void ResetForTests() { Data = new VillaSaveData(); try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { } }
    }
}
