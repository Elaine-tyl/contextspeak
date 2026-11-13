using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.Slice
{
    [Serializable]
    public sealed class GlossaryEntry
    {
        public string Phrase, Meaning, Chinese, Example;
    }

    /// Vocabulary help for idioms and café words that NPCs use at higher bands.
    /// Chinese glosses are authored, so the translation toggle works offline.
    public static class Glossary
    {
        public static readonly GlossaryEntry[] Entries =
        {
            new GlossaryEntry { Phrase = "right up your alley", Meaning = "exactly the kind of thing you like", Chinese = "正合你的口味", Example = "If you like fruity coffee, this is right up your alley." },
            new GlossaryEntry { Phrase = "on the pricey side", Meaning = "a little expensive", Chinese = "有点贵", Example = "It's a little on the pricey side." },
            new GlossaryEntry { Phrase = "fire away", Meaning = "go ahead and say or ask it", Chinese = "请说吧", Example = "Know what you want? Fire away." },
            new GlossaryEntry { Phrase = "stick with", Meaning = "keep choosing the same thing", Chinese = "继续选（原来的）", Example = "I'll stick with my usual." },
            new GlossaryEntry { Phrase = "in the mood for", Meaning = "wanting something right now", Chinese = "想要；有…的心情", Example = "I'm in the mood for something sweet." },
            new GlossaryEntry { Phrase = "creature of habit", Meaning = "someone who always does the same thing", Chinese = "习惯性很强的人", Example = "The usual? You're a creature of habit." },
            new GlossaryEntry { Phrase = "the usual", Meaning = "what you normally order", Chinese = "老样子", Example = "Can I get the usual?" },
            new GlossaryEntry { Phrase = "split the bill", Meaning = "each person pays part", Chinese = "分开付账；AA", Example = "Could we split the bill?" },
            new GlossaryEntry { Phrase = "dairy-free", Meaning = "with no milk products", Chinese = "不含乳制品", Example = "Do you have anything dairy-free?" },
            new GlossaryEntry { Phrase = "tempt", Meaning = "make someone want something", Chinese = "诱惑；吸引", Example = "Can I tempt you with dessert?" },
            new GlossaryEntry { Phrase = "is that the lot", Meaning = "is that everything (British, informal)", Chinese = "就这些吗", Example = "Two lattes — is that the lot?" },
            new GlossaryEntry { Phrase = "all set", Meaning = "finished; you have everything you need", Chinese = "都好了", Example = "Are you all set?" },
            new GlossaryEntry { Phrase = "grab a seat", Meaning = "sit down anywhere (informal)", Chinese = "随便找个位子坐", Example = "Grab a seat and I'll bring it over." },
            new GlossaryEntry { Phrase = "shout when you're ready", Meaning = "tell me when you are ready (informal)", Chinese = "准备好了叫我", Example = "Here's the menu — shout when you're ready." },
            new GlossaryEntry { Phrase = "can't go wrong with", Meaning = "it is a safe, good choice", Chinese = "选…不会错", Example = "You can't go wrong with the cheesecake." },
            new GlossaryEntry { Phrase = "pour-over", Meaning = "coffee made by pouring water slowly through ground coffee", Chinese = "手冲咖啡", Example = "The pour-over takes a few minutes." },
            new GlossaryEntry { Phrase = "flat white", Meaning = "espresso with a thin layer of velvety milk", Chinese = "馥芮白", Example = "A flat white, please." },
            new GlossaryEntry { Phrase = "extra shot", Meaning = "one more serving of espresso", Chinese = "加一份浓缩", Example = "Can I get an extra shot?" },
            new GlossaryEntry { Phrase = "allergens", Meaning = "ingredients that can cause allergies", Chinese = "过敏原", Example = "It contains milk and nuts." },
            new GlossaryEntry { Phrase = "tight lines", Meaning = "good luck (said to people who fish)", Chinese = "（钓鱼祝福）好运", Example = "Tight lines!" },
            new GlossaryEntry { Phrase = "bread and butter", Meaning = "the usual, most common thing", Chinese = "最常见的；基本的", Example = "Perch are your bread and butter here." },
            new GlossaryEntry { Phrase = "the one that got away", Meaning = "a fish (or chance) you nearly got but lost", Chinese = "差一点就到手的（鱼/机会）", Example = "Every angler has a story about the one that got away." },
            new GlossaryEntry { Phrase = "hooked", Meaning = "caught on a hook; also: very interested", Chinese = "上钩了；着迷", Example = "I've been hooked ever since." },
            new GlossaryEntry { Phrase = "lurking", Meaning = "waiting, hidden", Chinese = "潜伏", Example = "A pike is lurking in the weeds." },
            new GlossaryEntry { Phrase = "take my word for it", Meaning = "believe me", Chinese = "相信我", Example = "It was huge — take my word for it." },
            new GlossaryEntry { Phrase = "undivided attention", Meaning = "complete attention", Chinese = "全神贯注", Example = "You've got my undivided attention." },
            new GlossaryEntry { Phrase = "settling up", Meaning = "paying the bill", Chinese = "结账", Example = "How are we settling up?" },
            new GlossaryEntry { Phrase = "no judgement", Meaning = "I won't criticise you", Chinese = "不评判你", Example = "Oat milk? No judgement." },
        };

        /// Glossary entries that appear in a line, longest first.
        public static GlossaryEntry[] In(string line)
        {
            var text = (line ?? "").ToLowerInvariant().Replace("’", "'");
            return Entries.Where(entry => text.Contains(entry.Phrase)).OrderByDescending(entry => entry.Phrase.Length).ToArray();
        }
    }
}
