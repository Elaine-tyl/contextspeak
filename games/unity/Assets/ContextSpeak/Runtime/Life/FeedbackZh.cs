using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextSpeak.Life
{
    /// Chinese (简体) support for the speaking feedback card, fully offline (no AI, no tokens):
    ///  • fixed UI wording and the four IELTS criteria;
    ///  • the coach's grammar/pragmatics explanations (matched to the SpeakingCoach rule texts);
    ///  • ordering sentences built from the café menu (sizes, hot/iced, quantities, menu items).
    /// Anything it cannot translate faithfully returns "" — the card then shows English only rather than a guess.
    public static class FeedbackZh
    {
        public static string Criterion(string english)
        {
            var e = english.ToLowerInvariant();
            if (e.Contains("fluency")) return "流利度与连贯性";
            if (e.Contains("lexical")) return "词汇丰富度";
            if (e.Contains("grammat")) return "语法多样性与准确性";
            if (e.Contains("pronunciation")) return "发音";
            return "";
        }

        private static readonly (string key, string zh)[] Explanations =
        {
            ("After can, use the base verb", "can 后面接动词原形：can I have。"),
            ("don't take am/are", "want / need / like 前面不用 am/are：说 I want，不说 I am want。"),
            ("would like + noun", "would like + 名词（I'd like a latte），或 would like to + 动词（I'd like to order）。"),
            ("With cost, ask", "问价格用 How much does it cost?（或直接说 How much is it?）。"),
            ("Questions with have use does", "用 have 提问时要加 does：Does it have…?"),
            ("After did/do not, use the base verb", "did not / do not 后面接动词原形：I didn't order。"),
            ("Short adjectives add -er", "短形容词比较级加 -er：cheaper、bigger、sweeter。"),
            ("Use an before a vowel sound", "元音发音开头的词前用 an：an iced latte。"),
            ("Use a before a consonant sound", "辅音发音开头的词前用 a：a latte。"),
            ("More than one needs a plural noun", "超过一个要用复数：two lattes。"),
            ("Use a before one countable item", "一个可数的东西前面要加 a：I'd like a latte。"),
            ("Give me / I want can sound abrupt", "对店员说 Give me / I want 听起来比较生硬，用 Could I get…? 或 I'd like… 更自然。"),
            ("A single word works", "只说一个词也能听懂，但完整的礼貌请求听起来更友好。"),
        };

        /// Chinese for a coach explanation (several sentences may be joined); "" if none are known.
        public static string Explanation(string english)
        {
            if (string.IsNullOrWhiteSpace(english)) return "";
            var parts = Explanations.Where(x => english.IndexOf(x.key, StringComparison.OrdinalIgnoreCase) >= 0).Select(x => x.zh).Distinct().ToArray();
            return string.Join(" ", parts);
        }

        private static readonly (string en, string zh, string unit)[] Menu =
        {
            ("café latte", "拿铁", "杯"), ("matcha latte", "抹茶拿铁", "杯"), ("latte", "拿铁", "杯"), ("cappuccino", "卡布奇诺", "杯"),
            ("americano", "美式咖啡", "杯"), ("flat white", "澳白", "杯"), ("forest mocha", "森林摩卡", "杯"), ("mocha", "摩卡", "杯"),
            ("english breakfast tea", "英式早餐茶", "杯"), ("chamomile honey tea", "洋甘菊蜂蜜茶", "杯"), ("tea", "茶", "杯"), ("coffee", "咖啡", "杯"),
            ("forest berry smoothie", "森林莓果冰沙", "杯"), ("green garden smoothie", "绿色蔬果冰沙", "杯"), ("smoothie", "冰沙", "杯"),
            ("elderflower lemonade", "接骨木花柠檬水", "杯"), ("lemonade", "柠檬水", "杯"), ("fresh orange juice", "鲜榨橙汁", "杯"), ("orange juice", "橙汁", "杯"),
            ("butter croissant", "黄油可颂", "个"), ("croissant", "可颂", "个"), ("banana bread", "香蕉面包", "块"),
            ("blueberry cheesecake", "蓝莓芝士蛋糕", "块"), ("cheesecake", "芝士蛋糕", "块"), ("dark chocolate brownie", "黑巧克力布朗尼", "块"), ("brownie", "布朗尼", "块"),
            ("buttermilk pancakes", "酪乳松饼", "份"), ("avocado sourdough toast", "牛油果酸面包吐司", "份"), ("grilled chicken sandwich", "烤鸡三明治", "份"),
            ("sandwich", "三明治", "份"), ("mushroom grain bowl", "蘑菇谷物碗", "份"), ("extra espresso shot", "额外一份浓缩", "份"),
        };

        private static readonly Dictionary<string, string> Numbers = new Dictionary<string, string>
        { { "a", "一" }, { "an", "一" }, { "one", "一" }, { "two", "两" }, { "three", "三" }, { "four", "四" }, { "2", "两" }, { "3", "三" }, { "4", "四" } };

        private static readonly Dictionary<string, string> Fixed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Iced, please.", "冰的，谢谢。" }, { "Hot, please.", "热的，谢谢。" },
            { "Could I see the menu, please?", "请问可以看一下菜单吗？" }, { "Could I have the bill, please?", "请问可以买单吗？" },
            { "Card, please.", "刷卡，谢谢。" }, { "Cash, please.", "付现金，谢谢。" }, { "That's everything, thanks.", "就这些，谢谢。" },
            { "Yes, that's right.", "对，没错。" }, { "No, thank you.", "不用了，谢谢。" },
        };

        private static readonly Regex Order = new Regex(
            @"^(?:(?:hi|hello|hey)[!,.]?\s+)?(?<lead>could i (?:get|have)|can i (?:get|have)|i'd like|i would like|i'll have|i will have)?\s*(?:(?<n>an|a|one|two|three|four|2|3|4)\b)?\s*(?:(?<size>small|medium|large)\b)?\s*(?:(?<temp>hot|iced)\b)?\s*(?<item>[a-zé ]+?)s?\s*(?<please>,? please)?[.?!]*$",
            RegexOptions.IgnoreCase);

        /// Chinese for a learner-facing model / corrected sentence; "" when it is not a sentence we can translate exactly.
        public static string Sentence(string english)
        {
            var s = (english ?? "").Trim();
            if (s == "") return "";
            if (Fixed.TryGetValue(s, out var known)) return known;
            var m = Order.Match(s);
            if (!m.Success) return "";
            var itemText = m.Groups["item"].Value.Trim().ToLowerInvariant();
            var item = Menu.FirstOrDefault(x => itemText == x.en || itemText == x.en + "s" || itemText == x.en + "es");
            if (item.en == null) return "";
            var hello = Regex.IsMatch(s, @"^(?:hi|hello|hey)\b", RegexOptions.IgnoreCase) ? "你好！" : "";
            var n = m.Groups["n"].Success && Numbers.TryGetValue(m.Groups["n"].Value.ToLowerInvariant(), out var num) ? num : "一";
            var sizeEn = m.Groups["size"].Value.ToLowerInvariant();
            var size = sizeEn == "small" ? "小杯" : sizeEn == "medium" ? "中杯" : sizeEn == "large" ? "大杯" : "";
            var tempEn = m.Groups["temp"].Value.ToLowerInvariant();
            var temp = tempEn == "hot" ? "热" : tempEn == "iced" ? "冰" : "";
            var lead = m.Groups["lead"].Value.ToLowerInvariant();
            var polite = lead.StartsWith("could") || lead.StartsWith("can") ? "请问可以给我" : lead != "" ? "我要" : "请给我";
            var tail = lead.StartsWith("could") || lead.StartsWith("can") ? "吗？" : "。";
            if (lead == "" && m.Groups["please"].Success) tail = "，谢谢。";
            return hello + polite + n + item.unit + size + temp + item.zh + tail;
        }

        /// Practice band for one criterion, relative to the learner's training target (lower bound of e.g. "5.0–5.5").
        /// Developing = one band below target, on target = target, above target = one band above (clamped 1–9).
        public static double? CriterionBand(int level, double target)
        {
            // level: 0 NotAssessed, 1 Developing, 2 OnTarget, 3 AboveTarget (matches CriterionLevel order)
            if (level <= 0) return null;
            var band = level == 1 ? target - 1 : level == 2 ? target : target + 1;
            return Math.Max(1, Math.Min(9, band));
        }

        /// IELTS-style overall: mean of the scored criteria, rounded to the nearest half band (.25 → .5, .75 → next whole).
        public static double? Overall(IEnumerable<double?> bands)
        {
            var scored = bands.Where(b => b.HasValue).Select(b => b.Value).ToArray();
            if (scored.Length == 0) return null;
            var mean = scored.Average();
            return Math.Floor(mean * 2 + 0.5) / 2;
        }

        public static double TargetFrom(string bandText)
        {
            var m = Regex.Match(bandText ?? "", @"(\d(?:\.\d)?)");
            return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 5;
        }

        public static string LevelZh(int level) => level == 1 ? "需加强" : level == 2 ? "达到目标" : level == 3 ? "超出目标" : "未评分";
    }
}
