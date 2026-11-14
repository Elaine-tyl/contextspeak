using ContextSpeak.Slice;
namespace ContextSpeak.Learning
{
    public static class RepeatVerificationManager
    {
        // The provider checks semantic alternatives; the local grammar pass rejects known errors even
        // if a provider mistakenly approves them. No literal sentence matching or keyword mastery.
        public static bool Passed(UtteranceEvidence evidence, AssessmentRecord assessment) =>
            evidence.Spoken && assessment.Assessed && assessment.TaskMet && assessment.Correction == "";
    }
}
