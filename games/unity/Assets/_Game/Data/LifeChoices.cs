using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Cafe
{
    public static class LifeChoices
    {
        public static string Words(string text)
        {
            return Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant().Replace('’', '\''), @"[^a-z0-9' ]", " "), @"\s+", " ").Trim();
        }
        public static string Select(string step, string text)
        {
            var options = step == "market_pay" ? new[] { "cash", "card" }
                : step == "cafe_temperature" ? new[] { "hot", "iced" }
                : step == "cafe_service" ? new[] { "for here", "to go" }
                : new[] { "coffee", "latte", "tea", "lemonade" };
            var words = Regex.Replace(Words(text), @"\bcold\b", "iced");
            words = Regex.Replace(words, @"\b(take away|takeaway)\b", "to go");
            words = Regex.Replace(words, @"\b(?:not|no|don't want|do not want|dont want) (?:a |an |the )?(?:" + string.Join("|", options) + @")\b", "");
            if (Regex.IsMatch(words, @"\b(?:don't|do not|not|never|cannot|can't) (?:want|pay|paying|order|buy|have|take)\b")) return "";
            var selected = options.Where(option => Regex.IsMatch(words, @"\b" + option + @"\b")).ToArray();
            if (step == "cafe_order" && Regex.IsMatch(words, @"\b(two|three|four|five|six|seven|eight|nine|[2-9]|[0-9]{2,}|coffees|lattes|teas|lemonades)\b")) return "";
            return selected.Length == 1 ? selected[0] : "";
        }
        public static bool Confirmed(string text)
        {
            var words = Words(text);
            return Regex.IsMatch(words, @"^(yes|yeah|yep|correct|that is correct|that's correct|go ahead|sounds good)\b")
                && !Regex.IsMatch(words, @"\b(no|not|isn't|don't|but|wait|maybe|actually|unless|instead|if|guess|unsure|later|cancel|stop|hold|change|wrong|incorrect)\b");
        }
    }
}
