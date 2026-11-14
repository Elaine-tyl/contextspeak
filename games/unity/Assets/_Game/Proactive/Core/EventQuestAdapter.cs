using System;
using ContextSpeak.Learning;
namespace ContextSpeak.Proactive
{
    /// Authored transitions only. Assessment can unlock a step, never supply world commands or rewards.
    public sealed class EventQuestAdapter
    {
        private readonly ProactiveSave save;
        private readonly LifeEventDefinition[] definitions;
        public EventQuestAdapter(ProactiveSave save, LifeEventDefinition[] definitions = null)
        { this.save = save; this.definitions = definitions ?? ProactiveEventContent.All(); }
        public LifeEventDefinition Definition(string id) => Array.Find(definitions, e => e.Id == id);
        public static bool CanAdvance(PracticeOutcome outcome, UtteranceEvidence evidence)
            => evidence != null && evidence.Spoken && outcome != null && outcome.Advance
                && outcome.Assessment != null && outcome.Assessment.Assessed && outcome.Assessment.TaskMet;
        public bool Begin(string id, string npc, string location, DateTime utc)
        {
            if (save.Run != null || Definition(id) == null) return false;
            save.Run = new LifeEventRun { Id = id, NpcId = npc, Origin = location };
            NPCMemoryService.Of(save, npc).Encounters++;
            return true;
        }
        public bool Resume(string location)
        {
            if (save.Run == null || !save.Run.WaitingForTransfer || save.Run.TargetLocation != location) return false;
            save.Run.WaitingForTransfer = false;
            return true;
        }
        public void RememberFirstMeaning(string text)
        {
            if (save.Run == null || save.Run.Step != 0) return;
            save.Run.FirstMeaning = Noticed(text) ? "noticed map" : Concern(text) ? "are you okay" : "reassured";
        }
        public PracticeScenario Scenario(int band)
        {
            var run = save.Run;
            if (run == null) return null;
            var definition = Definition(run.Id);
            var goal = definition.Goal;
            var model = definition.Model;
            if (run.Id == "collision")
            {
                if (run.Step == 1) { goal = "Offer to help with the dropped map, or politely explain you cannot. Equivalent expressions are valid."; model = "Let me pick up your map for you."; }
                if (run.Step == 2) { goal = "Explain a plausible route to Forest Café, or offer to check a map together."; model = "Follow the path past the fountain. Forest Café is on your right."; }
                if (run.Step == 3) { goal = "Greet someone you helped earlier and respond to their thanks."; model = "It's lovely to see you again. I'm glad you found the café."; }
                if (run.Step == 4) { goal = "Accept or politely decline a museum invitation and give a reason."; model = "I'd love to. I'm interested in the local history."; }
            }
            else if (run.Step > 0)
            {
                goal = run.Id == "cafe-spill" ? "Suggest getting a cloth or help from café staff to clean up safely." : "Give a relevant reason or suggestion related to this encounter.";
                model = run.Id == "cafe-spill" ? "Let's ask the barista for a cloth and keep people away from the spill." : "Let's ask a member of staff so we can work out what to do next.";
            }
            return new PracticeScenario { Id = "life-event:" + run.Id + ":" + run.Step, ItemId = run.Id + ":" + run.Step,
                Npc = run.NpcId, Topic = definition.Title, Prompt = Prompt(band), Goal = goal, Model = model,
                Band = Math.Max(3, Math.Min(9, band)), ObjectWord = definition.Prop,
                Vocabulary = new[] { definition.Prop == "map" ? "directions" : "help" }, StressTip = "Keep the key words clear. A natural, understandable accent is welcome." };
        }
        public string Prompt(int band)
        {
            var run = save.Run;
            if (run == null) return "Thanks for your help!";
            if (run.WaitingForTransfer) return "Thanks! I'll see you at Forest Café.";
            if (run.Id == "collision")
            {
                if (run.Step == 1) return Noticed(run.FirstMeaning) ? "Thanks for noticing! Could you help me pick up my map?"
                    : Concern(run.FirstMeaning) ? "That's kind of you. I'm fine, thank you. Oh, my map! Could you give me a hand?"
                    : "Glad you're alright! I've dropped my map. Could you give me a hand?";
                if (run.Step == 2) return band >= 7 ? "I'm meeting someone at Forest Café. Could you explain the route and a landmark so I don't miss it?" : "Do you know how to get to Forest Café?";
                if (run.Step == 3) return "Hey, it's you again! Thanks for helping me find this place. How's your day going?";
                if (run.Step == 4) return "I'm thinking of visiting the museum later. Would you like to come along?";
            }
            if (run.Step > 0) return run.Id == "cafe-spill" ? "Thanks! What should we do about the spill before someone slips?" : "Thanks. What would you suggest we do next?";
            var prompt = Definition(run.Id).Prompt;
            if (band >= 7) prompt += " What would be the best option, and why?";
            return prompt;
        }
        public bool Advance(bool verifiedSpokenSuccess, string originalMeaning, string location, DateTime utc)
        {
            var run = save.Run;
            if (!verifiedSpokenSuccess || run == null || run.WaitingForTransfer) return false;
            if (run.Step == 0 && !string.IsNullOrEmpty(originalMeaning)) RememberFirstMeaning(originalMeaning);
            run.Step++;
            if (run.Id == "collision" && run.Step == 3)
            {
                run.WaitingForTransfer = true; run.TargetLocation = "cafe";
                NPCRelationshipManager.Helped(save, run.NpcId, "Helped with a dropped map and directions to Forest Café.");
                return true;
            }
            if (run.Id == "collision" ? run.Step >= 5 : run.Step >= 2)
            {
                var key = run.NpcId + ":" + run.Id;
                if (!save.Completed.Contains(key))
                {
                    save.Completed.Add(key);
                    while (save.Completed.Count > 100) save.Completed.RemoveAt(0);
                    NPCRelationshipManager.Helped(save, run.NpcId, "Completed " + Definition(run.Id).Title + " at " + location + ".");
                }
                save.Run = null;
            }
            return true;
        }
        private static bool Noticed(string text) => System.Text.RegularExpressions.Regex.IsMatch(text ?? "", @"\b(dropped|map|book)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static bool Concern(string text) => System.Text.RegularExpressions.Regex.IsMatch(text ?? "", @"\b(you okay|you alright|you ok|are you)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
