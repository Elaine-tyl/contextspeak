using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Slice
{
    public enum CriterionLevel { NotAssessed, Developing, OnTarget, AboveTarget }

    [Serializable]
    public sealed class CriterionNote
    {
        public SpeakingCriterion Criterion;
        public CriterionLevel Level;
        public string Note;
    }

    [Serializable]
    public sealed class SpeakingResult
    {
        public string Original = "", Corrected = "", Explanation = "";
        public bool SignificantError;   // grammar that changes or blurs meaning, or an unmet task
        public bool PragmaticTip;       // understandable, but sounds abrupt at this band
        public bool TaskMet;
        public CriterionNote[] Criteria = new CriterionNote[0];
        public double Score;            // 0..1, used only for band suggestions and progress
        public string[] GoodLanguage = new string[0]; // phrases worth praising
    }

    public enum Gate { Continue, OfferRetry, RequireRetry, ShowModelAndContinue }

    /// Text-based coaching aligned to the four IELTS Speaking criteria.
    /// It never pretends to judge pronunciation from a transcript; that needs a dedicated provider.
    public static class SpeakingCoach
    {
        private sealed class Rule
        {
            public Regex Pattern;
            public Func<Match, string> Fix;
            public string Why;
            public bool Significant = true;
        }

        private static readonly string Countable = "latte|cappuccino|americano|flat white|mocha|croissant|brownie|smoothie|sandwich|spritz|grain bowl";

        private static readonly Rule[] Rules =
        {
            new Rule { Pattern = new Regex(@"\bcan i has\b", RegexOptions.IgnoreCase), Fix = m => "can I have", Why = "After can, use the base verb: can I have." },
            new Rule { Pattern = new Regex(@"\b(i|we) (?:am|are) (want|need|like)\b", RegexOptions.IgnoreCase), Fix = m => m.Groups[1].Value + " " + m.Groups[2].Value, Why = "Want, need and like don't take am/are: say I want, not I am want." },
            new Rule { Pattern = new Regex(@"\bwould like to (a|an|the|some|one|two) ", RegexOptions.IgnoreCase), Fix = m => "would like " + m.Groups[1].Value + " ", Why = "Use would like + noun (I'd like a latte) or would like to + verb (I'd like to order)." },
            new Rule { Pattern = new Regex(@"\bhow much (?:is|it is) (?:it|this|that) cost\b", RegexOptions.IgnoreCase), Fix = m => "how much does it cost", Why = "With cost, ask How much does it cost? (or simply How much is it?)." },
            new Rule { Pattern = new Regex(@"\b(?:is|does) (it|this|that) has\b", RegexOptions.IgnoreCase), Fix = m => "does " + m.Groups[1].Value + " have", Why = "Questions with have use does: Does it have…?" },
            new Rule { Pattern = new Regex(@"\bis (it|this|that) have\b", RegexOptions.IgnoreCase), Fix = m => "does " + m.Groups[1].Value + " have", Why = "Questions with have use does: Does it have…?" },
            new Rule { Pattern = new Regex(@"\b(didn't|did not|don't|do not|doesn't|does not) (ordered|wanted|asked|paid|said)\b", RegexOptions.IgnoreCase), Fix = m => m.Groups[1].Value + " " + Base(m.Groups[2].Value), Why = "After did/do not, use the base verb: I didn't order." },
            new Rule { Pattern = new Regex(@"\bmore (cheap|big|small|sweet|strong|hot|cold)\b", RegexOptions.IgnoreCase), Fix = m => Comparative(m.Groups[1].Value), Why = "Short adjectives add -er: cheaper, bigger, sweeter." },
            new Rule { Pattern = new Regex(@"\ba (iced|oat|almond|americano|apple|orange|english|extra|espresso)\b", RegexOptions.IgnoreCase), Fix = m => "an " + m.Groups[1].Value, Why = "Use an before a vowel sound: an iced latte.", Significant = false },
            new Rule { Pattern = new Regex(@"\ban (latte|large|small|medium|cappuccino|flat|mocha|tea|coffee|croissant|brownie|hot|cold)\b", RegexOptions.IgnoreCase), Fix = m => "a " + m.Groups[1].Value, Why = "Use a before a consonant sound: a latte.", Significant = false },
            new Rule { Pattern = new Regex(@"\b(two|three|four|five|2|3|4|5) (?:(hot|iced|large|small|medium) )?(latte|cappuccino|americano|croissant|brownie|smoothie|lemonade|sandwich|coffee|tea)\b(?!s)", RegexOptions.IgnoreCase), Fix = m => m.Groups[1].Value + " " + (m.Groups[2].Success ? m.Groups[2].Value + " " : "") + Plural(m.Groups[3].Value), Why = "More than one needs a plural noun: two lattes.", Significant = false },
            new Rule { Pattern = new Regex(@"\b(want|like|have|get|order|take|need) (?:(hot|iced|large|small|medium) )?(" + Countable + @")\b", RegexOptions.IgnoreCase), Fix = m => m.Groups[1].Value + " a " + (m.Groups[2].Success ? m.Groups[2].Value + " " : "") + m.Groups[3].Value, Why = "Use a before one countable item: I'd like a latte.", Significant = false },
        };

        private static readonly (Regex pattern, string better, string why)[] Pragmatics =
        {
            (new Regex(@"^(?:give me|i want|bring me)\b", RegexOptions.IgnoreCase), "Could I get", "Give me / I want can sound abrupt to staff. Could I get…? or I'd like… is more natural."),
            (new Regex(@"^menu[.!?]?$", RegexOptions.IgnoreCase), "Could I see the menu, please?", "A single word works, but a full polite request sounds friendlier."),
            (new Regex(@"^(?:bill|check)[.!?]?$", RegexOptions.IgnoreCase), "Could I have the bill, please?", "A single word works, but a full polite request sounds friendlier."),
        };

        private static readonly string[] Praise =
        {
            "could i get", "could i have", "i'd like", "i would like", "would you mind", "what would you recommend", "go for", "stick with", "i'll go with",
            "i'll have", "do you happen to", "is it possible", "would it be possible", "i was wondering", "split the bill", "dairy free", "on the side",
            "to be honest", "actually", "sounds great", "that sounds", "right up my alley", "fair enough", "i'm in the mood for", "treat myself",
        };

        public static SpeakingResult Review(string utterance, IeltsBand band, bool taskMet, bool spoken, double? pronunciationScore = null)
        {
            var original = (utterance ?? "").Trim();
            var corrected = original;
            var reasons = new List<string>();
            var significant = false;
            foreach (var rule in Rules)
            {
                var match = rule.Pattern.Match(corrected);
                if (!match.Success) continue;
                var fixedText = rule.Fix(match);
                corrected = corrected.Substring(0, match.Index) + MatchCase(match.Value, fixedText) + corrected.Substring(match.Index + match.Length);
                reasons.Add(rule.Why);
                significant |= rule.Significant;
            }
            corrected = Capitalize(corrected);

            var pragmatic = false;
            if (band >= IeltsBand.Band6)
                foreach (var (pattern, better, why) in Pragmatics)
                    if (pattern.IsMatch(original))
                    {
                        pragmatic = true;
                        if (reasons.Count == 0) reasons.Add(why);
                        if (corrected == Capitalize(original))
                            corrected = better.EndsWith("?") ? better : Capitalize(Regex.Replace(pattern.Replace(original, better, 1), @"[,.!?]*\s*(?:please)?[.!?]*$", "", RegexOptions.IgnoreCase)) + ", please?";
                    }

            var words = Regex.Matches(original.ToLowerInvariant(), @"[a-z']+").Cast<Match>().Select(m => m.Value).ToArray();
            var profile = BandProfile.For(band);
            var fillers = words.Count(word => word == "um" || word == "uh" || word == "erm" || word == "hmm");
            var good = Praise.Where(phrase => original.ToLowerInvariant().Replace("’", "'").Contains(phrase)).ToArray();
            var distinct = words.Distinct().Count();

            var notes = new List<CriterionNote>
            {
                new CriterionNote
                {
                    Criterion = SpeakingCriterion.FluencyCoherence,
                    Level = words.Length >= profile.MinWordsForFullTask && fillers <= 1 ? (words.Length >= profile.MinWordsForFullTask * 2 ? CriterionLevel.AboveTarget : CriterionLevel.OnTarget) : CriterionLevel.Developing,
                    Note = words.Length < profile.MinWordsForFullTask
                        ? $"At {profile.Label}, aim for a fuller answer (about {profile.MinWordsForFullTask}+ words) that links ideas."
                        : fillers > 1 ? "Several hesitation sounds. Pause silently or use a linking phrase like 'Let me think…'." : "Clear and connected." + (spoken ? "" : " (Typed rehearsal: speaking speed not measured.)"),
                },
                new CriterionNote
                {
                    Criterion = SpeakingCriterion.LexicalResource,
                    Level = good.Length > 0 || distinct >= profile.MinWordsForFullTask + 3 ? (good.Length > 1 ? CriterionLevel.AboveTarget : CriterionLevel.OnTarget) : CriterionLevel.Developing,
                    Note = good.Length > 0 ? "Natural phrase: “" + good[0] + "”." : band >= IeltsBand.Band7 ? "Try a more precise or idiomatic choice, e.g. “I'll go for…” or “I'm in the mood for…”." : "Try a polite request phrase such as “Could I get…?”.",
                },
                new CriterionNote
                {
                    Criterion = SpeakingCriterion.GrammaticalRange,
                    Level = significant ? CriterionLevel.Developing : reasons.Count > 0 ? CriterionLevel.OnTarget : (Regex.IsMatch(original, @"\b(?:would|could|if|which|that|because|although|while)\b", RegexOptions.IgnoreCase) ? CriterionLevel.AboveTarget : CriterionLevel.OnTarget),
                    Note = reasons.Count > 0 ? reasons[0] : "Accurate." + (band >= IeltsBand.Band7 ? " Mix in a complex structure (if…, which…, would…) to show range." : ""),
                },
                pronunciationScore.HasValue
                    ? new CriterionNote { Criterion = SpeakingCriterion.Pronunciation, Level = pronunciationScore >= 0.8 ? CriterionLevel.AboveTarget : pronunciationScore >= 0.6 ? CriterionLevel.OnTarget : CriterionLevel.Developing, Note = "From the pronunciation assessment provider. Accent variation is not penalised." }
                    : new CriterionNote { Criterion = SpeakingCriterion.Pronunciation, Level = CriterionLevel.NotAssessed, Note = spoken ? "Not scored: speech recognition confidence is not a pronunciation measure. Connect a pronunciation provider to enable this." : "Not scored for typed answers." },
            };

            var assessed = notes.Where(note => note.Level != CriterionLevel.NotAssessed).ToArray();
            var score = assessed.Average(note => note.Level == CriterionLevel.Developing ? 0.4 : note.Level == CriterionLevel.OnTarget ? 0.8 : 1.0);
            if (!taskMet) score *= 0.5;

            return new SpeakingResult
            {
                Original = original,
                Corrected = corrected != Capitalize(original) ? corrected : "",
                Explanation = string.Join(" ", reasons.Distinct()),
                SignificantError = significant || !taskMet,
                PragmaticTip = pragmatic,
                TaskMet = taskMet,
                Criteria = notes.ToArray(),
                Score = Math.Round(score, 2),
                GoodLanguage = good,
            };
        }

        /// Decide whether the conversation continues. Retry counts are finite so learners never get stuck.
        public static Gate Decide(Strictness strictness, SpeakingResult result, int attempts)
        {
            var needsWork = result.SignificantError || (strictness == Strictness.Challenge && result.Corrected != "");
            if (!needsWork) return result.Corrected != "" ? Gate.OfferRetry : Gate.Continue;
            switch (strictness)
            {
                case Strictness.Practice: return Gate.OfferRetry;
                case Strictness.Guided: return attempts < 2 ? Gate.RequireRetry : Gate.ShowModelAndContinue;
                default: return attempts < 3 ? Gate.RequireRetry : Gate.ShowModelAndContinue;
            }
        }

        /// Repeat-and-practise check: the new attempt must be free of significant errors and keep the
        /// corrected sentence's key words. Natural rewordings with the same content also pass.
        public static bool PracticePassed(string attempt, string target, IeltsBand band)
        {
            var review = Review(attempt, band, true, true);
            if (review.SignificantError || review.Corrected != "") return false;
            // Keep the business meaning as well as grammar: a denied order or changed quantity is
            // not a successful repeat. Polite request forms remain interchangeable.
            string Meaning(string text) => string.Join("|", OrderIntentParser.Parse(text)
                .Where(intent => intent.Kind != IntentKind.Greet && intent.Kind != IntentKind.Thanks)
                .Select(intent => intent.Kind + ":" + intent.Item + ":" + intent.Quantity + ":" + intent.Detail + ":"
                    + string.Join(",", intent.Options.OrderBy(option => option.Key).Select(option => option.Key + "=" + option.Value)))
                .OrderBy(value => value));
            if (Meaning(attempt) != Meaning(target)) return false;
            var stop = new HashSet<string> { "a", "an", "the", "please", "i", "could", "can", "would", "get", "have", "like", "to", "it", "is", "that", "with", "and", "do", "does" };
            string[] Words(string text) => Regex.Matches((text ?? "").ToLowerInvariant(), @"[a-z']+").Cast<Match>().Select(m => m.Value).Where(word => !stop.Contains(word)).Distinct().ToArray();
            var key = Words(target);
            if (key.Length == 0) return true;
            var said = new HashSet<string>(Words(attempt));
            return key.Count(word => said.Contains(word) || said.Contains(word.TrimEnd('s'))) >= Math.Ceiling(key.Length * 0.6);
        }

        /// A hint that gets more specific with each failed attempt.
        public static string Hint(string model, int attempts)
        {
            if (string.IsNullOrEmpty(model)) return "";
            var words = model.Split(' ');
            if (attempts <= 1) return "Hint: start with “" + string.Join(" ", words.Take(Math.Min(2, words.Length))) + "…”";
            if (attempts == 2) return "Hint: “" + string.Join(" ", words.Take(Math.Max(2, words.Length / 2))) + "…”";
            return "Try saying: “" + model + "”";
        }

        private static string Base(string verb) => verb == "paid" ? "pay" : verb == "said" ? "say" : verb.EndsWith("ed") ? verb.Substring(0, verb.Length - 2) : verb;
        private static string Comparative(string adjective)
        {
            switch (adjective.ToLowerInvariant())
            {
                case "big": return "bigger";
                case "hot": return "hotter";
                case "cheap": return "cheaper";
                default: return adjective + "er";
            }
        }
        private static string Plural(string noun) => noun.EndsWith("ch") ? noun + "es" : noun == "coffee" || noun == "tea" ? noun + "s" : noun + "s";
        private static string MatchCase(string source, string replacement) => source.Length > 0 && char.IsUpper(source[0]) ? Capitalize(replacement) : replacement;
        private static string Capitalize(string text)
        {
            text = Regex.Replace(text, @"\bi\b", "I");
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
