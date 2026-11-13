using System;

namespace ContextSpeak.Slice
{
    // Numeric values are persisted: never renumber existing saves.
    public enum IeltsBand { Band3 = 3, Band4 = 4, Band5 = 5, Band6 = 6, Band7 = 7, Band8 = 8, Band9 = 9 }
    public enum Strictness { Practice, Guided, Challenge }
    public enum SpeakingCriterion { FluencyCoherence, LexicalResource, GrammaticalRange, Pronunciation }

    /// Original training targets informed by the public IELTS descriptors, not an exam score.
    [Serializable]
    public sealed class BandProfile
    {
        public IeltsBand Band;
        public string Label, Summary;
        public float SpeechRate;
        public int MaxQuestionsPerTurn, MinWordsForFullTask;
        public bool Idioms, Interruptions;
        public Strictness DefaultStrictness;
        public string[] NpcTraits;

        public static readonly BandProfile[] All =
        {
            Create(IeltsBand.Band3, "IELTS 3", "Name familiar things and give a short personal answer.", 0.75f, 1, 1, Strictness.Practice, "short concrete questions", "everyday nouns", "model phrases"),
            Create(IeltsBand.Band4, "IELTS 4", "Link simple sentences about familiar routines.", 0.82f, 1, 2, Strictness.Practice, "simple follow-ups", "basic connectors", "familiar routines"),
            Create(IeltsBand.Band5, "IELTS 5.0–5.5", "Describe an experience and add a simple reason.", 0.88f, 1, 3, Strictness.Practice, "clear pronunciation", "familiar vocabulary", "one question at a time"),
            Create(IeltsBand.Band6, "IELTS 6.0–6.5", "Explain choices, compare options and clarify meaning.", 1f, 2, 5, Strictness.Guided, "natural speed", "paraphrase", "connected reasons"),
            Create(IeltsBand.Band7, "IELTS 7.0–7.5", "Develop a viewpoint and handle an unexpected change.", 1.03f, 2, 7, Strictness.Guided, "precise collocations", "flexible structures", "follow-up questions"),
            Create(IeltsBand.Band8, "IELTS 8", "Discuss trade-offs with precise, flexible language.", 1.05f, 2, 9, Strictness.Challenge, "nuanced opinions", "effective paraphrase", "appropriate register"),
            Create(IeltsBand.Band9, "IELTS 9", "Explore unfamiliar ideas, qualify claims and respond spontaneously.", 1.05f, 2, 10, Strictness.Challenge, "flexible precision", "fully developed reasoning", "natural idiomatic language"),
        };

        private static BandProfile Create(IeltsBand band, string label, string summary, float rate, int questions, int words, Strictness strictness, params string[] traits)
        {
            return new BandProfile
            {
                Band = band, Label = label, Summary = summary, SpeechRate = rate,
                MaxQuestionsPerTurn = questions, MinWordsForFullTask = words,
                DefaultStrictness = strictness, NpcTraits = traits,
                Idioms = band >= IeltsBand.Band7, Interruptions = false,
            };
        }

        public static bool Valid(IeltsBand band) => band >= IeltsBand.Band3 && band <= IeltsBand.Band9;

        public static BandProfile For(IeltsBand band)
        {
            foreach (var profile in All)
                if (profile.Band == band) return profile;
            return All[0];
        }

        public static IeltsBand Adjust(IeltsBand band, double averageScore, int turns)
        {
            if (!Valid(band)) return IeltsBand.Band3;
            if (turns < 6) return band;
            if (averageScore >= 0.85 && band < IeltsBand.Band9) return band + 1;
            if (averageScore < 0.45 && band > IeltsBand.Band3) return band - 1;
            return band;
        }

        public static string CriterionName(SpeakingCriterion criterion)
        {
            switch (criterion)
            {
                case SpeakingCriterion.FluencyCoherence: return "Fluency & coherence";
                case SpeakingCriterion.LexicalResource: return "Lexical resource";
                case SpeakingCriterion.GrammaticalRange: return "Grammatical range & accuracy";
                default: return "Pronunciation";
            }
        }
    }
}
