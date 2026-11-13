using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Slice
{
    public enum PracticeNeed { Everyday, Dining, Shopping, Travel }
    /// Select real places from the authored curriculum; the model never invents a destination.
    public sealed class SpeakingTrail
    {
        public IeltsBand Band;
        public LessonPlace[] Stops;

        public static SpeakingTrail Create(SliceSave save)
        {
            var band = BandProfile.For(save.Band).Band;
            var defaults = band <= IeltsBand.Band4 ? new[] { "home", "market", "cafe" }
                : band <= IeltsBand.Band6 ? new[] { "cafe", "restaurant", "mall" }
                : new[] { "market", "fishing", "mall" };
            switch (save.PracticeNeed)
            {
                case PracticeNeed.Dining: defaults = new[] { "cafe", "restaurant", "japan" }; break;
                case PracticeNeed.Shopping: defaults = new[] { "market", "mall", "cafe" }; break;
                case PracticeNeed.Travel: defaults = new[] { "park", "fishing", "japan" }; break;
            }
            var words = Focus(save.FocusVocabulary).Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            var ordered = defaults.Concat(LifeCurriculum.Places.Select(place => place.Id)).Distinct().ToArray();
            return new SpeakingTrail
            {
                Band = band,
                Stops = ordered.Select(LifeCurriculum.Find)
                    .OrderByDescending(place => words.Count(word => Matches(place, word))).Take(3).ToArray(),
            };
        }

        private static bool Matches(LessonPlace place, string word)
        {
            var vocabulary = place.BasicWords.Concat(place.PreciseWords).Concat(place.Topics.Split('|'));
            return vocabulary.Any(topic => topic.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0
                || word.StartsWith(topic, StringComparison.OrdinalIgnoreCase));
        }

        public static string Focus(string text)
        {
            text = text ?? "";
            text = text.Substring(0, Math.Min(120, text.Length)).ToLowerInvariant();
            var result = string.Join(", ", Regex.Split(text, @"[,;\n]+")
                .Select(word => Regex.Replace(word, @"[^a-z '-]", "").Trim())
                .Where(word => word.Length > 0).Distinct().Take(8));
            return result.Substring(0, Math.Min(120, result.Length)).TrimEnd(' ', ',');
        }

        public bool Done(SliceSave save, string scene) => save.Lessons != null
            && save.Lessons.Any(lesson => lesson != null && lesson.SceneId == scene && lesson.Band == Band && lesson.Complete);
        public int Completed(SliceSave save) => Stops.Count(place => Done(save, place.Id));
        public LessonPlace Next(SliceSave save) => Stops.FirstOrDefault(place => !Done(save, place.Id));
    }
}
