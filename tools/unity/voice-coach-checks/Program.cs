using System;
using System.Linq;
using ContextSpeak.Learning;
using ContextSpeak.Slice;
static class Program
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    static void Main()
    {
        var scenario = new PracticeScenario { Id = "cafe", Topic = "order", Prompt = "What would you like to drink?", Vocabulary = new[] { "latte", "oat milk", "medium" }, Model = "Could I have a latte, please?", Band = 3 };
        var hints = VoiceCoach.Hints(scenario, new LearnerMemory(), LearningStage.IndependentAnswer);
        Check(hints.Length >= 3 && hints.Length <= 6 && hints.Contains("latte"), "Relevant 3–6 contextual vocabulary hints");
        Check(!hints.Contains(scenario.Model), "Initial hints do not reveal full model");
        Check(VoiceCoach.Hints(scenario, null, LearningStage.Recall).Length == 0, "Hidden recall cannot leak hints");
        Check(VoiceCoach.Hints(scenario, null, LearningStage.IndependentAnswer, true).Length == 0, "Exam does not reveal hints");
        var weak = new LearnerMemory(); weak.Items.Add(new LearningItem { Id = "vocab:medium", Expression = "medium", State = KnowledgeState.NEEDS_REVIEW });
        Check(VoiceCoach.Hints(scenario, weak, LearningStage.IndependentAnswer)[0] == "medium", "Relevant weak vocabulary prioritized");
        var evidence = new UtteranceEvidence { Text = "I'd like a latte.", Spoken = true, ProviderAvailable = true, GoalAssessed = true, Relevant = true, GoalMet = true, ImprovedExpression = "Could I have a latte, please?", LanguageScoresAssessed = true, CoherenceScore = 80, LexicalScore = 75, GrammarScore = 90, ScoreEvidence = "I'd like a latte." };
        var result = SpeakingAssessmentService.Assess(evidence, IeltsBand.Band3);
        Check(result.GrammarScore == 90 && result.LexicalScore == 75, "Provider language practice marks accepted with current-answer evidence");
        Check(result.Pronunciation == null && result.FluencyScore == null, "Text alone never scores pronunciation or fluency");
        Check(result.Correction == "" && result.ImprovedExpression != "", "Optional natural expression is not a grammar error");
        var natural = SpeakingAssessmentService.Assess(new UtteranceEvidence { Text = "I want one coffee with no sugar.", Spoken = true, ProviderAvailable = true, GoalAssessed = true, Relevant = true, GoalMet = true }, IeltsBand.Band7);
        Check(natural.Correction == "", "A valid informal request is not mislabeled as a grammar error");
        evidence.ScoreEvidence = "An invented sentence.";
        Check(SpeakingAssessmentService.Assess(evidence, IeltsBand.Band3).GrammarScore == null, "Invented evidence cannot support marks");
        evidence.ScoreEvidence = evidence.Text; evidence.GrammarScore = 101;
        Check(SpeakingAssessmentService.Assess(evidence, IeltsBand.Band3).GrammarScore == null, "Out-of-range marks rejected");
        evidence.GrammarScore = 90; evidence.Spoken = false;
        Check(SpeakingAssessmentService.Assess(evidence, IeltsBand.Band3).GrammarScore == null, "Typed input cannot earn speaking marks");
        Check(!VoiceCoach.HasSpeech(new float[16000], 16000), "Silent recording rejected before STT");
        Check(!VoiceCoach.HasSpeech(Enumerable.Repeat(.001f, 16000).ToArray(), 16000), "Quiet constant noise rejected");
        Check(VoiceCoach.HasSpeech(Enumerable.Range(0,16000).Select(i => (float)(.1 * Math.Sin(i*.07))).ToArray(),16000), "Audible varying samples reach STT without pronunciation claims");
        Check(!VoiceCoach.CanCapture(true, false, false) && !VoiceCoach.CanCapture(false, true, false), "NPC pending audio and replay block capture");
        Check(VoiceCoach.CanCapture(false, false, false) && !VoiceCoach.CanCapture(false, false, true), "Idle microphone allowed; busy assessment blocked");
        Console.WriteLine("Voice coach V9: " + count + " checks passed");
    }
}
