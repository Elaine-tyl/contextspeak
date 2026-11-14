using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Simplified gameplay needs (0 = satisfied, 1 = urgent). They rise with (game) time and fall when
    /// the matching activity happens. Values persist with the NPC's save state.
    [Serializable]
    public sealed class NPCNeeds
    {
        [Range(0, 1)] public float Hunger = 0.2f, Fatigue = 0.1f, Social = 0.3f, Comfort = 0.1f, Recreation = 0.3f;
        [Tooltip("Per game hour.")] public float HungerRate = 0.09f, FatigueRate = 0.05f, SocialRate = 0.06f, ComfortRate = 0.04f, RecreationRate = 0.05f;

        public void Tick(float gameHours, string activity)
        {
            Hunger = Mathf.Clamp01(Hunger + HungerRate * gameHours * (activity == "Eat" || activity == "Breakfast" || activity == "Lunch" ? -12 : 1));
            Fatigue = Mathf.Clamp01(Fatigue + FatigueRate * gameHours * (activity == "Sleep" ? -10 : activity == "Rest" || activity == "Relax" || activity == "Break" ? -2 : activity == "Work" || activity == "ServeCustomers" ? 1.5f : 1));
            Social = Mathf.Clamp01(Social + SocialRate * gameHours * (activity == "Socialize" || activity == "Chat" ? -8 : activity == "ServeCustomers" ? -1 : 1));
            Comfort = Mathf.Clamp01(Comfort + ComfortRate * gameHours * (activity == "Shelter" || activity == "Relax" || activity == "Sleep" ? -6 : SkyState.Raining ? 4 : 1));
            Recreation = Mathf.Clamp01(Recreation + RecreationRate * gameHours * (activity == "Relax" || activity == "Socialize" || activity == "Explore" ? -6 : 1));
        }

        public void Satisfy(string need, float amount)
        {
            switch (need)
            {
                case "Hunger": Hunger = Mathf.Clamp01(Hunger - amount); break;
                case "Fatigue": Fatigue = Mathf.Clamp01(Fatigue - amount); break;
                case "Social": Social = Mathf.Clamp01(Social - amount); break;
                case "Comfort": Comfort = Mathf.Clamp01(Comfort - amount); break;
                case "Recreation": Recreation = Mathf.Clamp01(Recreation - amount); break;
            }
        }
    }

    /// A goal the utility AI can pick, with its score this evaluation.
    public struct NPCGoal
    {
        public string Activity;
        public string Location;
        public float Score;
        public string Reason;
        public override string ToString() => Activity + (string.IsNullOrEmpty(Location) ? "" : " @ " + Location) + " (" + Score.ToString("0.00") + ", " + Reason + ")";
    }

    /// Utility AI: scores the scheduled activity against needs-driven alternatives (eat, rest, shelter,
    /// socialise) and keeps the current goal unless a candidate beats it by a margin AND the
    /// commitment time has passed — no flip-flopping between goals every frame.
    [Serializable]
    public sealed class NPCUtilityAI
    {
        public float SwitchMargin = 0.15f;
        [Tooltip("Real seconds a goal is kept before alternatives are considered (unless urgent).")] public float Commitment = 20;
        public NPCGoal Current { get; private set; }
        public float Since { get; private set; } = 999;

        public NPCGoal Evaluate(ScheduleBlock block, NPCNeeds needs, NPCPersonality personality, bool working, bool raining, bool outdoors, float dt)
        {
            Since += dt;
            var scheduled = new NPCGoal { Activity = block != null ? block.activity : "Wander", Location = block != null ? block.location : "", Score = 0.6f, Reason = "schedule" };
            // Duties weigh more; sleep at night is firm.
            if (working) scheduled.Score = 0.85f;
            if (scheduled.Activity == "Sleep") scheduled.Score = 0.95f;
            var best = scheduled;
            void Consider(string activity, string location, float score, string reason)
            {
                if (score > best.Score) best = new NPCGoal { Activity = activity, Location = location, Score = score, Reason = reason };
            }
            if (!working)
            {
                Consider("Eat", "restaurant/café/home", needs.Hunger * 1.05f, "hungry");
                Consider("Rest", "", needs.Fatigue * 0.9f, "tired");
                Consider("Socialize", "", needs.Social * (0.5f + personality.Sociability * 0.5f), "lonely");
            }
            else
            {
                Consider("Eat", "break", needs.Hunger > 0.9f ? 0.9f : 0, "very hungry at work");
            }
            if (raining && outdoors && scheduled.Activity != "Commute") Consider("Shelter", "", 0.9f + needs.Comfort * 0.1f, "raining");

            // Hysteresis + commitment.
            var current = Current;
            var same = current.Activity == best.Activity && current.Location == best.Location;
            if (string.IsNullOrEmpty(current.Activity) || same) { Current = best; if (!same) Since = 0; return Current; }
            var currentStill = current.Activity == scheduled.Activity && current.Location == scheduled.Location ? scheduled.Score
                : current.Reason == "raining" ? (raining && outdoors ? 0.9f : 0) : current.Score * 0.97f;
            var urgent = best.Score > 0.95f || best.Reason == "raining";
            var scheduleChanged = best.Reason == "schedule" && current.Reason == "schedule";
            if (scheduleChanged || urgent || Since > Commitment && best.Score > currentStill + SwitchMargin || currentStill <= 0)
            {
                Current = best;
                Since = 0;
            }
            return Current;
        }

        public void Reset() { Current = default; Since = 999; }
    }
}
