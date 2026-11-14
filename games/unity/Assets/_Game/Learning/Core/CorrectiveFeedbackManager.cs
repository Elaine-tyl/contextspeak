namespace ContextSpeak.Learning
{
    public static class CorrectiveFeedbackManager
    {
        public static string Focus(AssessmentRecord result) => result.Correction != ""
            ? "Try: " + result.Correction + "\n" + result.Grammar
            : result.TaskMet ? "Good communication. Now recall it without a model." : "Answer the question in your own words. Ask for clarification if needed.";
    }
}
