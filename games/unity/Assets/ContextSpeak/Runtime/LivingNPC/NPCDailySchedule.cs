using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// One block of a daily routine — the same shape as the JSON designers write:
    /// { "startHour": 9, "endHour": 12, "activity": "ServeCustomers", "location": "Forest Café" }.
    [Serializable]
    public sealed class ScheduleBlock
    {
        public float startHour;
        public float endHour;
        public string activity = "Wander";
        public string location = "";

        public bool Contains(float hour) => startHour <= endHour ? hour >= startHour && hour < endHour : hour >= startHour || hour < endHour;
        public override string ToString() => Clock(startHour) + "–" + Clock(endHour) + " " + activity + (string.IsNullOrEmpty(location) ? "" : " @ " + location);
        public static string Clock(float hour) => ((int)hour % 24).ToString("00") + ":" + ((int)(hour % 1 * 60)).ToString("00");
    }

    /// A configurable daily routine (data, not code). Parses the designer JSON array, answers "what
    /// should I be doing at this hour" with a small per-person offset (people are not all punctual to
    /// the second), and what comes next.
    [Serializable]
    public sealed class NPCDailySchedule
    {
        public List<ScheduleBlock> Blocks = new List<ScheduleBlock>();
        [Tooltip("This person's habitual lateness/earliness (hours, ±).")] public float Offset;

        [Serializable] private sealed class Wrapper { public ScheduleBlock[] items; }

        public static NPCDailySchedule FromJson(string json, float offset = 0)
        {
            var schedule = new NPCDailySchedule { Offset = offset };
            if (string.IsNullOrWhiteSpace(json)) return schedule;
            var trimmed = json.Trim();
            if (trimmed.StartsWith("[")) trimmed = "{\"items\":" + trimmed + "}";
            var parsed = JsonUtility.FromJson<Wrapper>(trimmed);
            if (parsed?.items != null) schedule.Blocks.AddRange(parsed.items);
            schedule.Blocks.Sort((a, b) => a.startHour.CompareTo(b.startHour));
            return schedule;
        }

        public string ToJson()
        {
            var json = JsonUtility.ToJson(new Wrapper { items = Blocks.ToArray() }, true);
            var start = json.IndexOf('['); var end = json.LastIndexOf(']');
            return start >= 0 && end > start ? json.Substring(start, end - start + 1) : "[]";
        }

        public int IndexAt(float hour)
        {
            if (Blocks.Count == 0) return -1;
            var h = Mathf.Repeat(hour - Offset, 24);
            for (var i = 0; i < Blocks.Count; i++) if (Blocks[i].Contains(h)) return i;
            // Gaps: keep doing the previous block.
            var best = -1; var bestStart = float.MinValue;
            for (var i = 0; i < Blocks.Count; i++)
            {
                var s = Blocks[i].startHour;
                if (s <= h && s > bestStart) { bestStart = s; best = i; }
            }
            return best >= 0 ? best : Blocks.Count - 1;
        }

        public ScheduleBlock At(float hour) { var i = IndexAt(hour); return i >= 0 ? Blocks[i] : null; }

        public ScheduleBlock Next(float hour)
        {
            var i = IndexAt(hour);
            if (i < 0) return null;
            for (var k = 1; k <= Blocks.Count; k++)
            {
                var candidate = Blocks[(i + k) % Blocks.Count];
                if (candidate.activity != Blocks[i].activity || candidate.location != Blocks[i].location) return candidate;
            }
            return Blocks[i];
        }

        /// Hours left in the current block (for "running late" decisions).
        public float Remaining(float hour)
        {
            var block = At(hour);
            if (block == null) return 0;
            var h = Mathf.Repeat(hour - Offset, 24);
            return Mathf.Repeat(block.endHour - h, 24);
        }
    }
}
