using System;
using System.Linq;
namespace ContextSpeak.Learning
{
    public sealed class ReviewTransfer
    {
        public LearningItem Item;
        public string Location;
    }
    public static class ScenarioTransferManager
    {
        public static PracticeScenario Prepare(ReviewTransfer transfer, PracticeScenario scene)
        {
            if (transfer.Item.Id == "polite-requests") return scene;
            scene.ItemId = transfer.Item.Id;
            if (scene.ItemId.StartsWith("vocab:", StringComparison.Ordinal))
            {
                var word = transfer.Item.Expression;
                scene.Prompt = "Here in " + scene.Id + ", can you give a new example using the word " + word + "?";
                scene.Goal = "Give a meaningful new example using " + word + " in this location. Accept natural alternatives.";
                scene.Model = "For example, we talked about " + word + " here yesterday and I learned something useful.";
                scene.Vocabulary = new[] { word };
            }
            else if (scene.ItemId == "compare-perspectives")
            {
                scene.Prompt = "Here in " + scene.Id + ", compare two ways of handling a problem and explain your view.";
                scene.Goal = "Compare two relevant perspectives and explain a preference. No prescribed vocabulary.";
                scene.Model = "Some people prefer working alone, whereas I prefer working together because we can share ideas.";
            }
            else
            {
                scene.Prompt = "Here in " + scene.Id + ", give a specific example of a time someone helped you.";
                scene.Goal = "Describe a concrete example of receiving help. Any reasonable experience is acceptable.";
                scene.Model = "For example, a friend explained a difficult task yesterday, so I could finish it by myself.";
            }
            return scene;
        }
        private static readonly string[] Route = { "cafe", "school", "home" };
        public static ReviewTransfer Next(LearnerMemory memory, string current, DateTime utc)
        {
            var item = memory.Items.Where(candidate => SpacedReviewScheduler.Due(candidate, utc) && candidate.LastScenario != current)
                .OrderBy(candidate => candidate.DueUtc).FirstOrDefault()
                ?? memory.Items.Where(candidate => SpacedReviewScheduler.Due(candidate, utc)).OrderBy(candidate => candidate.DueUtc).FirstOrDefault();
            if (item == null) return null;
            var previous = Array.IndexOf(Route, item.LastScenario);
            return new ReviewTransfer { Item = item, Location = Route[(previous + 1 + Route.Length) % Route.Length] };
        }
    }
}
