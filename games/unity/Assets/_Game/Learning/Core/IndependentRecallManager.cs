namespace ContextSpeak.Learning
{
    public static class IndependentRecallManager
    {
        public static bool Passed(UtteranceEvidence evidence, AssessmentRecord assessment) =>
            RepeatVerificationManager.Passed(evidence, assessment);
    }
}
