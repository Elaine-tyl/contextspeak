using System;
using System.Linq;
using System.Text.Json;
using ContextSpeak.Proactive;
using ContextSpeak.Learning;

static class Program
{
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static UtteranceEvidence Voice(string text, string correction = "") => new UtteranceEvidence {
        Text = text, Correction = correction, Spoken = true, ProviderAvailable = true, GoalAssessed = true, Relevant = true, GoalMet = true };
    static void Main()
    {
        var now = new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
        var state = new ProactiveSave();
        Check(state.Settings.Frequency == InteractionFrequency.High && state.Settings.Mode == GameMode.GuidedLearning, "Requested high / guided defaults");
        var contact = new ContactEvidence { ActualContact = true, ClosingSpeed = 1.1f, ApproachDot = .9f, ObstacleClear = true, NpcAvailable = true };
        Check(CollisionPolicy.Candidate(contact), "Real gentle head-on contact can react");
        contact.ActualContact = false; Check(!CollisionPolicy.Candidate(contact), "Proximity alone never creates collision");
        contact.ActualContact = true; contact.ClosingSpeed = .05f; Check(!CollisionPolicy.Candidate(contact), "Standing overlap is not an accident");
        contact.ClosingSpeed = 1; contact.ApproachDot = -.1f; Check(!CollisionPolicy.Candidate(contact), "Moving apart cannot trigger");
        contact.ApproachDot = .9f; contact.ObstacleClear = false; Check(!CollisionPolicy.Candidate(contact), "Blocked retreat suppresses cinematic contact");
        var availability = new InteractionAvailability();
        Check(availability.CanOffer, "Free exploring can receive offer");
        foreach (var field in typeof(InteractionAvailability).GetFields().Where(f => f.FieldType == typeof(bool)))
        {
            field.SetValue(availability, true); Check(!availability.CanOffer, field.Name + " blocks interruption"); field.SetValue(availability, false);
        }
        var cooldown = new EventCooldownManager(state);
        Check(cooldown.Ready("tourist", "collision", now), "Fresh cooldown ready");
        cooldown.Reserve("tourist", "collision", now);
        Check(!cooldown.Ready("other", "spill", now.AddSeconds(24)), "High frequency still global 25-second gate");
        Check(!cooldown.Ready("tourist", "collision", now.AddSeconds(26), true), "Resumed encounter respects cooldown");
        Check(!cooldown.Ready("tourist", "spill", now.AddSeconds(26)), "Per-NPC cooldown enforced");
        Check(!cooldown.Ready("other", "collision", now.AddSeconds(100)), "Per-event cooldown enforced");
        Check(cooldown.Ready("other", "spill", now.AddSeconds(26)), "Different NPC/event after global gate allowed");
        var copy = JsonSerializer.Deserialize<ProactiveSave>(JsonSerializer.Serialize(state, Json), Json);
        Check(!new EventCooldownManager(copy).Ready("tourist", "collision", now.AddSeconds(5)), "Cooldown survives reload");
        state.Settings.Frequency = InteractionFrequency.Off;
        Check(!cooldown.Ready("new", "spill", now.AddHours(1)), "Off disables unsolicited conversations");
        state.Settings.Frequency = InteractionFrequency.High;
        Check(SceneContexts.Density("hospital") < SceneContexts.Density("city"), "Hospital invitations have lower density than city exploration");
        var npc = new NPCSocialProfile { Id = "tourist", Name = "Jamie", Locations = new[] { "city", "cafe" } };
        var context = new EventContext { Location = "airport", Hour = 10 };
        var selector = new ContextualEventSelector();
        Check(selector.Select(context, npc, state, false).Id == "flight-delay", "Airport event fits location");
        context.Location = "cafe";
        Check(selector.Select(context, npc, state, false).Id == "cafe-spill", "Café starts spilled drink event");
        Check(selector.Select(context, npc, state, true).Id == "collision", "Contact prioritizes apology");
        Check(ProactiveEventContent.All().SelectMany(e => e.Locations).Distinct().Count() >= 10, "Requested scene contexts have shared definitions");
        var failedGoal = new PracticeOutcome { Advance = true, Assessment = new AssessmentRecord { Assessed = true, TaskMet = false } };
        Check(!EventQuestAdapter.CanAdvance(failedGoal, Voice("I like football.")), "Free-life irrelevant answer cannot complete an event");
        var quest = new EventQuestAdapter(state);
        Check(quest.Begin("collision", "tourist", "city", now), "Tourist chain can start");
        Check(!quest.Begin("cafe-spill", "other", "cafe", now), "One concurrent accepted event");
        Check(!quest.Advance(false, "I'm fine.", "city", now), "Unassessed response never advances chain");
        Check(quest.Advance(true, "I'm alright, but you dropped your map.", "city", now), "Assessed response advances");
        Check(quest.Prompt(5).Contains("noticing"), "Noticing dropped map receives relevant follow-up");
        var concern = new EventQuestAdapter(new ProactiveSave()); concern.Begin("collision", "tourist", "city", now);
        concern.Advance(true, "No worries. Are you okay?", "city", now);
        Check(concern.Prompt(5).Contains("kind") && concern.Prompt(5) != quest.Prompt(5), "Concern has different follow-up");
        var practice = new IELTSPracticeManager(state.Learner);
        practice.Begin(quest.Scenario(5), GameMode.GuidedLearning, now);
        var wrong = practice.Submit(Voice("I can helps you.", "I can help you."), now);
        Check(!wrong.Advance && practice.Stage == LearningStage.Feedback, "Guided correction blocks event progression");
        practice.ShowRepetition();
        Check(!practice.Submit(Voice("I can helps you.", "I can help you."), now).Advance, "Wrong repeat blocked");
        Check(!practice.Submit(Voice("I'd be happy to help."), now).Advance && practice.Stage == LearningStage.Recall, "Valid alternative enters separate recall");
        Check(practice.VisibleCorrection == "", "Recall hides correction");
        Check(practice.Submit(Voice("Let me help you with that."), now).Advance, "Independent recall releases event step");
        quest.Advance(true, "Let me pick up your map.", "city", now);
        quest.Advance(true, "Follow the path past the fountain to Forest Café.", "city", now);
        Check(state.Run.WaitingForTransfer && state.Run.TargetLocation == "cafe", "Tourist chain waits for café transfer");
        var json = JsonSerializer.Serialize(state, Json); copy = JsonSerializer.Deserialize<ProactiveSave>(json, Json);
        var resumed = new EventQuestAdapter(copy);
        Check(!resumed.Resume("restaurant"), "Wrong location does not resume chain");
        Check(resumed.Resume("cafe") && resumed.Prompt(5).Contains("again"), "Tourist remembers player at café after reload");
        resumed.Advance(true, "It's nice to see you again.", "cafe", now);
        resumed.Advance(true, "I'd love to visit the museum with you.", "cafe", now);
        var memory = NPCMemoryService.Of(copy, "tourist");
        Check(memory.Relationship > 0 && copy.Run == null, "Completion creates relationship and releases event");
        var relationship = memory.Relationship;
        Check(!resumed.Advance(true, "again", "cafe", now) && memory.Relationship == relationship, "Completed reward cannot replay");
        for (var i = 0; i < 30; i++) NPCMemoryService.Remember(copy, "tourist", "Helped with directions " + i);
        Check(memory.Summaries.Count <= 6 && memory.Summaries.All(s => s.Length <= 180), "Bounded game-fact memory");
        Check(SceneContexts.Location("bank:Patel") == "" && SceneContexts.Location("ForestSlice") == "cafe", "Unknown venue is not silently treated as café");
        Check(SceneContexts.Location("cafe:Maya") == "cafe" && SceneContexts.Location("school:Reed") == "school" && SceneContexts.Location("mall:Ellie") == "mall", "Named city actors normalize their location prefix");
        Check(SceneContexts.Location("MainCity") == "city" && SceneContexts.Location("ForestCafe") == "cafe" && SceneContexts.Location("ShoppingMall") == "mall", "Scene aliases map to authored context");
        Console.WriteLine("Proactive V7.1: " + passed + " checks passed");
    }
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
}
