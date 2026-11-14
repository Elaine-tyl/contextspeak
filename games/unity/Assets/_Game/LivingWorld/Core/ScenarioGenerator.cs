using System;
using System.Linq;

namespace ContextSpeak.LivingWorld
{
    [Serializable]
    public sealed class StoryChoice
    {
        public string Id, Label, Goal, Fact, Promise;
        public int Next;
        public StoryAction Action;
    }
    [Serializable]
    public sealed class StoryStep
    {
        public string Simple, Everyday, Advanced;
        public string[] Vocabulary;
        public StoryChoice[] Choices;
    }
    [Serializable]
    public sealed class StoryTemplate
    {
        public string Id, Title, Location, NpcId, NpcName, Role, Prerequisite = "";
        public int StartMinute, EndMinute = 1440;
        public bool OnlyRain;
        public StoryStep[] Steps;
    }
    public sealed class GeneratedScenario
    {
        public string Prompt, Goal;
        public string[] Vocabulary;
    }
    /// Chooses bounded authored language. The LLM assesses meaning; it never invents world actions.
    public static class ScenarioGenerator
    {
        private static readonly string[] Goals =
        {
            "Answer clearly using a short natural sentence.",
            "Answer clearly and add one relevant detail.",
            "Give an answer and a relevant reason.",
            "Connect your ideas and explain your preference naturally.",
            "Develop your answer and explain a practical trade-off.",
            "Respond flexibly, qualify your view and address a possible complication.",
            "Give a precise, nuanced response and consider an alternative perspective.",
        };
        public static GeneratedScenario Generate(StoryTemplate template, int step, int band)
        {
            var line = template.Steps[step];
            return new GeneratedScenario
            {
                Prompt = band <= 4 ? line.Simple : band <= 6 ? line.Everyday : line.Advanced,
                Goal = Goals[Math.Max(0, Math.Min(6, band - 3))],
                Vocabulary = line.Vocabulary,
            };
        }
        private static bool AllowedAction(StoryTemplate template, StoryAction action)
        {
            switch (action)
            {
                case StoryAction.None: case StoryAction.ActivityComplete: return true;
                case StoryAction.MeetMarket: case StoryAction.DeclineMarket:
                    return template.Id == "tourist-cafe" && template.Location == "cafe";
                case StoryAction.JoinTrip: case StoryAction.DeclineTrip:
                    return template.Id == "tourist-market" && template.Prerequisite == "market-invitation";
                case StoryAction.MorningFlight: case StoryAction.Refund:
                    return template.Id == "tourist-airport" && template.Prerequisite == "trip-invitation";
                default: return false;
            }
        }
        public static bool Valid(StoryTemplate[] templates)
        {
            if (templates == null || templates.Length == 0 || templates.Any(item => item == null)
                || templates.Select(item => item.Id).Distinct().Count() != templates.Length) return false;
            foreach (var item in templates)
            {
                if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Location) || string.IsNullOrEmpty(item.NpcId)
                    || item.Steps == null || item.Steps.Length == 0 || item.StartMinute < 0 || item.EndMinute > 1440 || item.StartMinute >= item.EndMinute) return false;
                for (var index = 0; index < item.Steps.Length; index++)
                {
                    var step = item.Steps[index];
                    if (step == null || string.IsNullOrEmpty(step.Simple) || string.IsNullOrEmpty(step.Everyday)
                        || string.IsNullOrEmpty(step.Advanced) || step.Choices == null || step.Choices.Length == 0
                        || step.Vocabulary == null || step.Vocabulary.Length == 0) return false;
                    if (step.Choices.Any(choice => choice == null)) return false;
                    if (step.Choices.Select(choice => choice.Id).Distinct().Count() != step.Choices.Length) return false;
                    foreach (var choice in step.Choices)
                        if (string.IsNullOrEmpty(choice.Id) || string.IsNullOrEmpty(choice.Goal) || choice.Goal.Length > 150
                            || !AllowedAction(item, choice.Action) || choice.Next < -1
                            || choice.Next >= item.Steps.Length || choice.Next >= 0 && choice.Next <= index) return false;
                }
            }
            return true;
        }
    }
}
