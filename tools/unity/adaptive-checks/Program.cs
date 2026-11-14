using System;
using System.Linq;
using System.Text.Json;
using ContextSpeak.Learning;
using ContextSpeak.Slice;

static class Program
{
    static int passed;
    static readonly System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();
    static readonly DateTime Now = new DateTime(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);
    static void Check(bool ok, string name) { if (!ok) { failures.Add(name); Console.WriteLine("FAIL " + name); return; } passed++; Console.WriteLine("PASS " + name); }
    static UtteranceEvidence Good(string text = "Could I have a latte, please?", bool mock = false) => new UtteranceEvidence {
        Text = text, Spoken = true, ProviderAvailable = !mock, GoalAssessed = !mock, Relevant = true, GoalMet = true,
    };
    static PracticeScenario Scenario(string id = "cafe") => new PracticeScenario {
        Id = id, Npc = "Maya", Topic = "drinks", ItemId = "request", Model = "Could I have a latte, please?",
        Prompt = "What would you like?", Goal = "Make a polite request", Vocabulary = new[] { "latte" },
    };
    static void Main()
    {
        var memory = new LearnerMemory();
        var practice = new IELTSPracticeManager(memory);
        practice.Begin(Scenario(), GameMode.GuidedLearning, Now);
        var wrong = Good("Can I has a latte?"); wrong.Correction = "Can I have a latte?";
        wrong.ErrorCategory = "verb-form";
        Check(!practice.Submit(wrong, Now).Advance && practice.Stage == LearningStage.Feedback, "Guided correction blocks advance");
        practice.ShowRepetition();
        Check(!practice.Submit(Good("Can I has a latte?"), Now).Advance && practice.Stage == LearningStage.Repetition, "Incorrect repeat stays gated even with positive provider verdict");
        Check(!practice.Submit(Good("Could I have a latte, please?"), Now).Advance && practice.Stage == LearningStage.Recall, "Valid alternative enters hidden recall, not next dialogue");
        Check(memory.Items.Single(value => value.Id == "request").IndependentSuccesses == 0, "Repetition is not independent evidence");
        Check(practice.VisibleCorrection == "", "Recall hides the corrected sentence");
        var recall = practice.Submit(Good(), Now);
        Check(recall.Advance && memory.Items.Single(value => value.Id == "request").IndependentSuccesses == 1, "Independent recall is recorded separately");
        Check(memory.Items.Single(value => value.Id == "request").State != KnowledgeState.MASTERED, "Same-session recall is not mastery");
        Check(memory.Items.Single(value => value.Id == "request").DueUtc == Now.AddDays(1).Ticks, "First review due in one real day");
        Check(ScenarioTransferManager.Next(memory, "cafe", Now.AddDays(1))?.Location == "school", "Due review transfers to a different NPC context");
        practice.Begin(Scenario("school"), GameMode.GuidedLearning, Now.AddDays(1));
        practice.Submit(Good(), Now.AddDays(1));
        Check(memory.Items.Single(value => value.Id == "request").DueUtc == Now.AddDays(4).Ticks, "Independent success expands to three days");
        practice.Begin(Scenario("home"), GameMode.GuidedLearning, Now.AddDays(4));
        practice.Submit(Good(), Now.AddDays(4));
        Check(memory.Items.Single(value => value.Id == "request").State == KnowledgeState.MASTERED, "Delayed independent production in three contexts establishes evidence-based mastery");
        var item = memory.Items.Single(value => value.Id == "request");
        SpacedReviewScheduler.Record(item, false, true, Now.AddDays(11));
        Check(item.State == KnowledgeState.NEEDS_REVIEW && item.DueUtc == Now.AddDays(12).Ticks, "Failed recall reschedules and removes mastered state");
        var same = new LearnerMemory();
        var repetitions = new IELTSPracticeManager(same);
        for (int i = 0; i < 8; i++) { repetitions.Begin(Scenario(), GameMode.GuidedLearning, Now); repetitions.Submit(Good(), Now); }
        Check(same.Items.Single(value => value.Id == "request").IndependentSuccesses == 1 && same.Items.Single(value => value.Id == "request").State != KnowledgeState.MASTERED, "Repeated same-day answers cannot farm mastery");
        var mock = new IELTSPracticeManager(new LearnerMemory()); mock.Begin(Scenario(), GameMode.GuidedLearning, Now);
        Check(mock.Submit(Good(mock:true), Now).Status.Contains("MOCK") && mock.Memory.History.Count == 0, "Missing AI is honest mock mode without credit");
        var assessment = SpeakingAssessmentService.Assess(Good(), IeltsBand.Band5);
        Check(assessment.Pronunciation == null && assessment.PronunciationNote.Contains("not assessed"), "Transcript cannot score pronunciation");
        var acoustic = Good(); acoustic.AcousticScore = 85; acoustic.AcousticProvider = "test acoustic service";
        Check(SpeakingAssessmentService.Assess(acoustic, IeltsBand.Band5).Pronunciation == 85, "Acoustic provider evidence is accepted");
        var topic = AdaptiveDialogueManager.Next(Scenario(), Good("I prefer tea because coffee keeps me awake."), 1, 5);
        Check(topic.Question.Contains("tea") && !topic.Question.Contains("weekend"), "Follow-up uses the actual answer's relevant topic");
        Check(AdaptiveDialogueManager.Next(Scenario(), Good("Could you repeat that?"), 1, 3).Kind == FollowUpKind.Clarification, "Listening clarification does not change topic");
        var free = new IELTSPracticeManager(new LearnerMemory()); free.Begin(Scenario(), GameMode.FreeLife, Now);
        Check(free.Submit(wrong, Now).Advance && free.Stage != LearningStage.Repetition, "Free life keeps conversation moving");
        var exam = new IELTSMockExamManager(); exam.Begin(Now);
        exam.StartRecording();
        Check(!exam.MoveNext(wrong, Now) && exam.Feedback.Count == 0, "Exam never interrupts an active recording");
        exam.StopRecording();
        Check(exam.MoveNext(wrong, Now) && exam.Feedback.Count == 0, "Exam defers first answer feedback");
        exam.MoveNext(Good(), Now); exam.MoveNext(Good(), Now);
        Check(exam.Complete && exam.Feedback.Count == 3, "Exam feedback appears after all three parts");
        var json = JsonSerializer.Serialize(memory, new JsonSerializerOptions { IncludeFields = true });
        var loaded = JsonSerializer.Deserialize<LearnerMemory>(json, new JsonSerializerOptions { IncludeFields = true });
        Check(loaded.Items.Single(value => value.Id == "request").DueUtc == item.DueUtc && loaded.History.Count == memory.History.Count, "Evidence and due dates survive save/reload");
        Check(LearningDashboardController.Build(new LearnerMemory(), Now).Contains("No speaking evidence yet"), "Empty dashboard does not invent results");
        Check(AdaptiveDialogueManager.RecommendedBand(memory, 5) >= 3, "Adaptation remains in supported practice range");
        Check(LearningMemoryService.Sanitize(null).Items.Count == 0, "Older saves initialize additive learner memory");
        var interruptedMemory = new LearnerMemory();
        var interrupted = new IELTSPracticeManager(interruptedMemory); interrupted.Begin(Scenario(), GameMode.GuidedLearning, Now);
        interrupted.Submit(wrong, Now);
        var restoredPractice = new IELTSPracticeManager(interruptedMemory); restoredPractice.Begin(Scenario(), GameMode.GuidedLearning, Now);
        Check(restoredPractice.Stage == LearningStage.Feedback && restoredPractice.VisibleCorrection != "", "Closing or reloading preserves mandatory correction gate");
        Check(memory.Items.Any(value => value.Id == "vocab:latte"), "Vocabulary used in actual answers has separate knowledge records");
        var before = memory.Items.Single(value => value.Id == "vocab:latte").IndependentSuccesses;
        var observed = LearningMemoryService.ObserveObject(memory, "bookshelf", "book");
        Check(observed.State == KnowledgeState.NEW && observed.IndependentSuccesses == 0, "Looking at an object creates exposure, not mastery");
        var noCredit = Good(); noCredit.Spoken = false;
        var typed = new IELTSPracticeManager(memory); typed.Begin(Scenario(), GameMode.GuidedLearning, Now.AddDays(30)); typed.Submit(noCredit, Now.AddDays(30));
        Check(memory.Items.Single(value => value.Id == "vocab:latte").IndependentSuccesses == before, "Typed evidence cannot raise vocabulary mastery");
        var offTopic = new IELTSPracticeManager(new LearnerMemory()); offTopic.Begin(Scenario(), GameMode.GuidedLearning, Now);
        var unrelated = Good("My sister live in London."); unrelated.Correction = "My sister lives in London."; unrelated.Relevant = unrelated.GoalMet = false;
        offTopic.Submit(unrelated, Now);
        Check(offTopic.VisibleCorrection == Scenario().Model, "An off-topic grammar correction is not an impossible mandatory target");
        Check(!AdaptiveDialogueManager.Clarification("Could you repeat the homework instructions? I understood the first task, but I missed the deadline."), "A complete school clarification answer reaches assessment");
        var transferred = new IELTSPracticeManager(new LearnerMemory());
        foreach (var entry in new[] { ("cafe", 0), ("school", 0), ("cafe", 1), ("cafe", 3) }) {
            transferred.Begin(Scenario(entry.Item1), GameMode.GuidedLearning, Now.AddDays(entry.Item2)); transferred.Submit(Good(), Now.AddDays(entry.Item2));
        }
        Check(transferred.Memory.Items.Single(value => value.Id == "request").State != KnowledgeState.MASTERED, "Same-day location visits cannot masquerade as delayed transfer");
        var transfer = new ReviewTransfer { Item = new LearningItem { Id = "vocab:latte", Expression = "latte" }, Location = "school" };
        var task = ScenarioTransferManager.Prepare(transfer, Scenario("school"));
        Check(task.Goal.Contains("latte") && task.Model.Contains("latte") && task.ItemId == "vocab:latte", "Transfer prompt, goal, model and item all address the same skill");
        var changeTopic = AdaptiveDialogueManager.Next(Scenario(), Good("Can we talk about travel instead?"), 1, 5);
        Check(changeTopic.Kind == FollowUpKind.TopicChange && changeTopic.Question.Contains("travel"), "A requested topic change follows the learner's chosen subject");
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "contextspeak-v7-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, "save.json");
        try {
            LearningSaveFile.Write(path, "original"); LearningSaveFile.Write(path, "updated");
            Check(System.IO.File.ReadAllText(path) == "updated" && System.IO.File.ReadAllText(path + ".bak") == "original", "Atomic save retains previous recoverable version");
        } finally { System.IO.Directory.Delete(folder, true); }
        if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
        Console.WriteLine("Adaptive V7: " + passed + " checks passed");
    }
}
