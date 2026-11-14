using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum AgeGroup { Child, Teen, YoungAdult, Adult, Senior }
    public enum EnglishAccent { Australian, British, American, Malaysian, Indian, Irish, Scottish, NewZealand }

    /// Personality as simple 0…1 dials that change behaviour (not just flavour text).
    [Serializable]
    public sealed class NPCPersonality
    {
        [Range(0, 1), Tooltip("Starts conversations, greets people, chats longer.")] public float Sociability = 0.5f;
        [Range(0, 1), Tooltip("Avoids strangers; looks away sooner; rarely initiates.")] public float Shyness = 0.3f;
        [Range(0, 1), Tooltip("Waits calmly in queues; tolerates delays.")] public float Patience = 0.6f;
        [Range(0, 1), Tooltip("Short answers while working; walks faster.")] public float Busyness = 0.4f;
        [Range(0, 1), Tooltip("Looks at the phone / around while walking.")] public float Distraction = 0.4f;
        public string CommunicationStyle = "friendly";
    }

    /// Voice settings for TTS / recorded lines. Accents are styles, never "worse pronunciation".
    [Serializable]
    public sealed class NPCVoice
    {
        public EnglishAccent Accent = EnglishAccent.Australian;
        [Range(0.6f, 1.4f)] public float Rate = 1;
        [Range(-0.3f, 0.3f)] public float Pitch;
        public string Tone = "warm";
    }

    /// Persistent identity of an important NPC (name, job, home, work, routine, relationships, voice,
    /// English level). Profiles are assets: edit them in the Inspector, or paste a schedule JSON.
    [CreateAssetMenu(menuName = "ContextSpeak/NPC/Living NPC Profile", fileName = "NPC Profile")]
    public sealed class NPCProfile : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "npc";
        public string DisplayName = "Resident";
        public AgeGroup Age = AgeGroup.Adult;
        public string Occupation = "Resident";
        [Tooltip("LivingPlace id of the home.")] public string Home = "home";
        [Tooltip("LivingPlace id of the workplace or school (empty = none).")] public string Work = "";
        [Tooltip("Character model (Generated/Motion prefab name).")] public string Model = "CafeCharm";
        public NPCPersonality Personality = new NPCPersonality();
        [Header("Relationships (NPC ids)")]
        public string[] Friends = new string[0];
        public string[] Family = new string[0];
        public string[] Coworkers = new string[0];
        [Tooltip("Walks with this NPC when both go the same way (students to school, couples).")] public string WalkWith = "";
        [Header("Routine")]
        public string[] PreferredActivities = new string[0];
        [TextArea(6, 30), Tooltip("JSON array of { startHour, endHour, activity, location }.")] public string ScheduleJson = "[]";
        [Tooltip("Typical lateness (minutes, ±).")] public float Punctuality = 5;
        [Header("Player")]
        public string RelationshipToPlayer = "stranger";
        [Header("Language")]
        [Range(3, 9)] public int EnglishBand = 6;
        public NPCVoice Voice = new NPCVoice();
        [Header("Ambient lines (cheap, no AI call)")]
        public string[] Greetings = { "Hi!", "Hey there!", "Morning!" };
        public string[] SmallTalk = { "Busy day today.", "Nice weather, isn't it?", "See you later!" };
        [TextArea] public string MemorySummary = "";

        private NPCDailySchedule parsed;
        public NPCDailySchedule Schedule
        {
            get
            {
                if (parsed == null)
                {
                    var hash = 0; foreach (var c in Id) hash = hash * 31 + c;
                    var offset = (Mathf.Abs(hash % 1000) / 1000f * 2 - 1) * Punctuality / 60f;
                    parsed = NPCDailySchedule.FromJson(ScheduleJson, offset);
                }
                return parsed;
            }
        }

        private void OnValidate() { parsed = null; }

        public bool IsFriend(string id) => Array.IndexOf(Friends, id) >= 0 || Array.IndexOf(Family, id) >= 0 || Array.IndexOf(Coworkers, id) >= 0;
    }
}
