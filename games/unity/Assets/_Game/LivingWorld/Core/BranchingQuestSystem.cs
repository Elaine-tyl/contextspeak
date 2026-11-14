using System;
using System.Linq;

namespace ContextSpeak.LivingWorld
{
    /// Exactly-once authored actions. No API in this engine accepts arbitrary inventory/currency changes.
    public sealed class BranchingQuestSystem
    {
        public LivingWorldState State { get; }
        public StoryTemplate[] Templates { get; }
        public StoryTemplate Current => Templates.FirstOrDefault(item => item.Id == State.Run.Event);
        public int Step => State.Run.Step;
        public string Ticket => State.Run.Event + ":" + Step + ":" + State.Run.Revision;
        public bool Active => Current != null && !State.Completed.Contains(Current.Id);

        public BranchingQuestSystem(LivingWorldState state, StoryTemplate[] templates)
        {
            if (!ScenarioGenerator.Valid(templates)) throw new ArgumentException("Invalid living-world scenario catalog.");
            State = LivingWorldState.Normalize(state); Templates = templates;
            // Validate every saved conversation, including inactive ones restored later.
            State.Runs.RemoveAll(run => !templates.Any(item => item.Id == run.Event));
            foreach (var run in State.Runs)
            {
                var template = templates.First(item => item.Id == run.Event);
                if (run.Step < 0 || run.Step >= template.Steps.Length)
                {
                    run.Step = Enumerable.Range(0, template.Steps.Length)
                        .FirstOrDefault(step => !State.Receipts.Contains(run.Event + ":" + step));
                    run.Repair = run.RepairChoice = ""; run.Revision++;
                }
                if (!template.Steps[run.Step].Choices.Any(choice => choice.Id == run.RepairChoice))
                    run.Repair = run.RepairChoice = "";
            }
            if (Current == null || Step < 0 || Step >= Current.Steps.Length || State.Completed.Contains(State.Run.Event))
                State.Run = new StoryRun { Revision = State.Run.Revision + 1 };
        }
        public bool Eligible(StoryTemplate item, StoryContext context) => !State.Completed.Contains(item.Id)
            && (item.Prerequisite == "" || State.Flags.Contains(item.Prerequisite))
            && context.Minute >= item.StartMinute && context.Minute < item.EndMinute
            && (!item.OnlyRain || context.Raining);
        public StoryTemplate Offer(StoryContext context) => Templates.FirstOrDefault(item => item.Location == context.Location
            && Eligible(item, context) && !(State.DeferredEvent == item.Id && context.AbsoluteMinute < State.DeferredUntil));
        public bool Begin(string id, StoryContext context)
        {
            var item = Templates.FirstOrDefault(template => template.Id == id);
            if (item == null || item.Location != context.Location || !Eligible(item, context)) return false;
            State.Day = Math.Max(1, context.Day); State.Minute = context.Minute;
            if (State.Run.Event != id)
            {
                var run = State.Runs.FirstOrDefault(saved => saved.Event == id);
                if (run == null) { run = new StoryRun { Event = id }; State.Runs.Add(run); }
                State.Run = run;
                State.Run.Revision++;
            }
            return true;
        }
        public void Defer(StoryContext context)
        {
            State.DeferredEvent = State.Run.Event;
            State.DeferredUntil = context.AbsoluteMinute + 120;
            State.Run.Revision++; // cancel any late speech/assessment callback
        }
        public StoryResult Answer(string ticket, string choiceId, string text, bool spoken, StoryAssessment assessment)
        {
            if (!Active || ticket != Ticket) return new StoryResult(StoryStatus.Stale, "That turn has already changed. Please try the current question.");
            var choice = Current.Steps[Step].Choices.FirstOrDefault(item => item.Id == choiceId);
            if (choice == null || State.Run.Repair != "" && choiceId != State.Run.RepairChoice)
                return new StoryResult(StoryStatus.Rejected, "Finish the corrected reply before changing your choice.");
            if (string.IsNullOrWhiteSpace(text) || text.Length > 400) return new StoryResult(StoryStatus.Rejected, "Use a short clear reply.");
            if (!spoken) return new StoryResult(StoryStatus.Rehearsal, "Rehearsal only. Record your answer to continue.");
            if (assessment == null || !assessment.Assessed) return new StoryResult(StoryStatus.Unavailable, "The answer was not assessed. Retry; your progress is kept.");
            if (!assessment.Relevant || !assessment.GoalMet) return new StoryResult(StoryStatus.Rejected, "Try answering the current question and your chosen intention. You can ask for help.");
            if (!string.IsNullOrWhiteSpace(assessment.Correction))
            {
                State.Run.Repair = assessment.Correction.Substring(0, Math.Min(240, assessment.Correction.Length));
                State.Run.RepairChoice = choiceId;
                State.Run.Revision++;
                return new StoryResult(StoryStatus.Repeat, "Say the corrected reply, or a natural version with the same meaning.");
            }
            var receipt = Current.Id + ":" + Step;
            if (State.Receipts.Contains(receipt)) return new StoryResult(StoryStatus.Stale, "This step is already complete.");
            State.Receipts.Add(receipt);
            NPCMemorySystem.Remember(State, Current.NpcId, Current.NpcName, choice.Fact, choice.Promise, true);
            if (Current.Id == "tourist-market") ResolvePromise("Meet at the night market");
            Apply(choice.Action);
            State.Run.Repair = State.Run.RepairChoice = "";
            State.Run.Revision++;
            if (choice.Next < 0)
            {
                State.Completed.Add(Current.Id);
                return new StoryResult(StoryStatus.Completed, "Conversation complete. Your choice and NPC memory are saved.");
            }
            State.Run.Step = choice.Next;
            return new StoryResult(StoryStatus.Advanced, "That was clear. Let's continue.");
        }
        private void ResolvePromise(string promise) => NPCMemorySystem.Find(State, "jordan")?.Promises.Remove(promise);
        private void Apply(StoryAction action)
        {
            string flag = null;
            switch (action)
            {
                case StoryAction.MeetMarket: flag = "market-invitation"; break;
                case StoryAction.DeclineMarket: flag = "market-declined"; break;
                case StoryAction.JoinTrip: flag = "trip-invitation"; break;
                case StoryAction.DeclineTrip: flag = "trip-declined"; break;
                case StoryAction.MorningFlight: State.Itinerary = "Morning flight requested"; flag = "trip-resolved"; break;
                case StoryAction.Refund: State.Itinerary = "Refund information requested"; flag = "trip-resolved"; break;
            }
            if (flag != null && !State.Flags.Contains(flag)) State.Flags.Add(flag);
            if (action == StoryAction.MorningFlight || action == StoryAction.Refund)
                ResolvePromise("Discuss travel plans at the airport");
        }
    }
}
