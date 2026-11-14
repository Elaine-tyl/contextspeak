using System;
using System.Linq;
using ContextSpeak.LivingWorld;

namespace ContextSpeak.Editor
{
    public static class LivingWorldChecks
    {
        private static int checks;
        private static void Check(bool result, string why)
        {
            if (!result) throw new Exception("Living world: " + why);
            checks++;
        }
        private static StoryAssessment Good(string correction = "") => new StoryAssessment
            { Assessed = true, Relevant = true, GoalMet = true, Correction = correction };
        private static StoryContext At(string location, int minute = 19 * 60) => new StoryContext
            { Location = location, Day = 1, Minute = minute, Band = 5 };
        private static BranchingQuestSystem New(LivingWorldState state = null) =>
            new BranchingQuestSystem(state ?? new LivingWorldState(), LivingWorldTemplates.All());
        private static StoryResult Say(BranchingQuestSystem engine, string choice, StoryAssessment assessment = null, bool spoken = true) =>
            engine.Answer(engine.Ticket, choice, "An independently assessed response.", spoken, assessment ?? Good());

        public static void Verify()
        {
            checks = 0;
            var state = new LivingWorldState();
            var engine = New(state);
            Check(!engine.Begin("tourist-market", At("night-market")), "Cannot skip prerequisites.");
            Check(engine.Begin("tourist-cafe", At("cafe")), "Tourist can start at the cafe.");
            var first = engine.Ticket;
            Check(Say(engine, "directions", spoken: false).Status == StoryStatus.Rehearsal && engine.Step == 0, "Typing is never spoken credit.");
            Check(Say(engine, "directions", new StoryAssessment()).Status == StoryStatus.Unavailable && engine.Step == 0, "Unassessed answers cannot progress.");
            Check(Say(engine, "directions", Good("I can show you the way.")).Status == StoryStatus.Repeat && engine.Step == 0, "Correction stays on the same task.");
            Check(Say(engine, "unable").Status == StoryStatus.Rejected && engine.Step == 0, "Cannot switch action during mandatory repair.");
            Check(Say(engine, "directions").Status == StoryStatus.Advanced && engine.Step == 1, "Natural assessed paraphrase completes repair.");
            Check(engine.Answer(first, "directions", "Replay", true, Good()).Status == StoryStatus.Stale && engine.Step == 1, "Late/double callbacks are ignored.");
            Check(Say(engine, "details").Status == StoryStatus.Advanced && engine.Step == 2, "Asking for details gets a distinct follow-up.");
            Check(Say(engine, "meet").Status == StoryStatus.Completed, "Player can promise to meet at the market.");
            Check(state.Flags.Contains("market-invitation") && NPCMemorySystem.Find(state, "jordan").Promises.Contains("Meet at the night market"), "Confirmed promises are saved.");
            Check(!engine.Begin("tourist-cafe", At("cafe")), "Completed events do not trigger again.");
            Check(!engine.Begin("tourist-market", At("night-market", 12 * 60)), "Night market respects opening time.");
            Check(engine.Begin("tourist-market", At("night-market")), "Market stage unlocks after the promise.");
            Check(Say(engine, "recommend").Status == StoryStatus.Advanced, "Reunion accepts an original food recommendation.");
            Check(Say(engine, "join").Status == StoryStatus.Completed && state.Flags.Contains("trip-invitation"), "Trip choice unlocks airport.");
            Check(engine.Begin("tourist-airport", At("airport")), "Airport continues the saved chain.");
            Check(Say(engine, "options").Status == StoryStatus.Advanced, "Ask about alternative flights.");
            Check(Say(engine, "morning").Status == StoryStatus.Completed && state.Itinerary == "Morning flight requested", "Only authored actions update the itinerary.");
            var before = NPCMemorySystem.Find(state, "jordan").Relationship;
            Check(!engine.Begin("tourist-market", At("night-market")) && NPCMemorySystem.Find(state, "jordan").Relationship == before, "Replay gives no extra relationship credit.");
            var restored = New(state);
            Check(restored.State.Completed.Count == 3 && restored.State.Flags.Contains("trip-resolved"), "Progress survives engine recreation.");

            var decline = New();
            decline.Begin("tourist-cafe", At("cafe")); Say(decline, "unable"); Say(decline, "decline");
            Check(!decline.State.Flags.Contains("market-invitation") && decline.State.Completed.Contains("tourist-cafe"), "Polite refusal is a successful branch, not a failed story.");
            var deferred = New();
            deferred.Begin("tourist-cafe", At("cafe")); deferred.Defer(At("cafe"));
            Check(deferred.Offer(At("cafe")) == null, "Dismissed NPC cannot immediately repeat the invitation.");
            Check(deferred.Begin("tourist-cafe", At("cafe")), "Manual resume can recover a deferred event.");
            var savedRepair = New();
            savedRepair.Begin("tourist-cafe", At("cafe")); Say(savedRepair, "directions", Good("Could I show you the way?"));
            var repairRestored = New(savedRepair.State);
            Check(repairRestored.State.Run.Repair != "" && Say(repairRestored, "directions").Status == StoryStatus.Advanced, "Correction survives save/load without granting free progress.");
            var switching = New();
            switching.Begin("tourist-cafe", At("cafe")); Say(switching, "directions");
            var rainy = At("camping"); rainy.Raining = true;
            switching.Begin("camp-rain", rainy); Say(switching, "shelter");
            switching.Begin("tourist-cafe", At("cafe"));
            Check(switching.Step == 1 && Say(switching, "meet").Status == StoryStatus.Completed, "Switching activities preserves each partially finished conversation.");
            var invalid = LivingWorldTemplates.All();
            invalid[0].Steps[0].Choices[0].Action = (StoryAction)999;
            Check(!ScenarioGenerator.Valid(invalid), "Unknown world actions are rejected before play.");
            var nullChoice = LivingWorldTemplates.All();
            nullChoice[0].Steps[0].Choices[0] = null;
            Check(!ScenarioGenerator.Valid(nullChoice), "Null authored choices fail validation without throwing.");
            var wrongAction = LivingWorldTemplates.All();
            wrongAction[0].Steps[0].Choices[0].Action = StoryAction.Refund;
            Check(!ScenarioGenerator.Valid(wrongAction), "A cafe cannot execute an airport-only action.");
            var damaged = new LivingWorldState();
            damaged.Runs.Add(new StoryRun { Event = "tourist-cafe", Step = 99 });
            var recovered = New(damaged);
            Check(recovered.Begin("tourist-cafe", At("cafe")) && recovered.Step == 0
                && Say(recovered, "directions").Status == StoryStatus.Advanced, "Invalid inactive saved turns recover before resuming.");
            var refusedTrip = New();
            refusedTrip.Begin("tourist-cafe", At("cafe")); Say(refusedTrip, "directions"); Say(refusedTrip, "meet");
            refusedTrip.Begin("tourist-market", At("night-market")); Say(refusedTrip, "recommend"); Say(refusedTrip, "decline");
            Check(NPCMemorySystem.Find(refusedTrip.State, "jordan").Promises.Count == 0, "A completed market meeting resolves its promise even when declining the trip.");
            Check(NPCMemorySystem.Find(state, "jordan").Promises.Count == 0, "Airport resolution closes the travel promise.");
            var context = At("camping"); context.Raining = false;
            Check(New().Offer(context) == null, "Rain contingency respects actual weather.");
            context.Raining = true;
            Check(New().Offer(context)?.Id == "camp-rain", "Rain makes the camping contingency eligible.");
            foreach (var band in Enumerable.Range(3, 7))
            {
                var generated = ScenarioGenerator.Generate(LivingWorldTemplates.All()[0], 0, band);
                Check(generated.Prompt != "" && generated.Goal != "" && generated.Vocabulary.Length > 0, "All IELTS3–9 practice targets have authored context.");
            }
            foreach (var activity in LivingWorldTemplates.All().Where(item => item.Id != "tourist-cafe"
                && item.Id != "tourist-market" && item.Id != "tourist-airport"))
            {
                var practice = New(); var place = At(activity.Location); place.Raining = true;
                Check(practice.Begin(activity.Id, place), "Every activity can start in its authored conditions.");
                var firstChoice = practice.Current.Steps[0].Choices[0].Id;
                Check(Say(practice, firstChoice).Status == StoryStatus.Advanced, "Every activity has a reachable second turn.");
                var lastChoice = practice.Current.Steps[1].Choices[0].Id;
                Check(Say(practice, lastChoice).Status == StoryStatus.Completed && practice.State.Memories.Count == 1,
                    "Every activity completes with exactly one NPC memory.");
            }
            Check(New().State.Memories.Count == 0 && New().State.Flags.Count == 0, "New players have no invented history.");
            Console.WriteLine("Living-world regressions passed: " + checks);
        }
    }
}
