using System;
using System.Collections.Generic;
using ContextSpeak.Learning;

namespace ContextSpeak.Proactive
{
    public enum InteractionFrequency { Off, Low, Normal, High }
    public enum InteractionKind { Ambient, Conversation, Story }

    [Serializable] public sealed class ProactiveSettings
    {
        public InteractionFrequency Frequency = InteractionFrequency.High;
        public GameMode Mode = GameMode.GuidedLearning;
    }
    [Serializable] public sealed class NPCSocialProfile
    {
        public string Id = "", Name = "Resident", Occupation = "Resident", Personality = "friendly";
        public string[] Locations = Array.Empty<string>(), AvailableEvents = Array.Empty<string>();
        public float Sociability = .6f;
        public float FrequencyMultiplier = 1;
        public int StartHour = 7, EndHour = 22;
    }
    [Serializable] public sealed class SocialMemory
    {
        public string NpcId = "";
        public int Relationship, Encounters;
        public int MirroredRelationship;
        public List<string> Summaries = new List<string>();
    }
    [Serializable] public sealed class CooldownRecord { public string Key = ""; public long Until; }
    [Serializable] public sealed class LifeEventRun
    {
        public string Id = "", NpcId = "", Origin = "", TargetLocation = "", FirstMeaning = "", ModelName = "", DisplayName = "";
        public int Step;
        public bool WaitingForTransfer;
    }
    [Serializable] public sealed class ProactiveSave
    {
        public int Version = 1;
        public ProactiveSettings Settings = new ProactiveSettings();
        public LearnerMemory Learner = new LearnerMemory();
        public List<SocialMemory> Memories = new List<SocialMemory>();
        public List<CooldownRecord> Cooldowns = new List<CooldownRecord>();
        public List<string> Completed = new List<string>();
        public LifeEventRun Run;
    }
    public sealed class InteractionAvailability
    {
        public bool Cutscene, MockExam, Recording, VehicleTransition, Swimming, Transaction, Conversation, Menu, QuestConflict;
        public bool CanOffer => !Cutscene && !MockExam && !Recording && !VehicleTransition && !Swimming && !Transaction && !Conversation && !Menu && !QuestConflict;
    }
    public sealed class EventContext
    {
        public string Location = "", Activity = "", Quest = "";
        public int Hour = 12, Band = 5;
        public bool Raining, Familiar;
    }
    public sealed class ContactEvidence
    {
        public bool ActualContact, ObstacleClear, NpcAvailable;
        public float ClosingSpeed, ApproachDot;
    }
    public static class CollisionPolicy
    {
        // A touch sensor alone is insufficient: a physical surface contact must also be reported.
        public static bool Candidate(ContactEvidence value) => value != null && value.ActualContact && value.ObstacleClear
            && value.NpcAvailable && value.ClosingSpeed >= .35f && value.ClosingSpeed <= 5.5f && value.ApproachDot > .15f;
    }
}
