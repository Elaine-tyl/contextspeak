using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.Slice
{
    [Serializable]
    public sealed class KitchenTicket
    {
        public string Id;
        public string[] Lines = new string[0];
        public float Remaining, Total;
        public string Table = "";
        public bool Ready => Remaining <= 0;
    }

    /// Preparation queue shared by the barista and kitchen. Times are real seconds, compressed from menu prep times.
    [Serializable]
    public sealed class KitchenQueue
    {
        public List<KitchenTicket> Tickets = new List<KitchenTicket>();

        public KitchenTicket Add(string id, OrderCart cart)
        {
            var seconds = Math.Max(8f, Math.Min(45f, cart.Lines.Sum(line => line.Menu.PrepSeconds * line.Quantity) * 0.12f));
            var ticket = new KitchenTicket { Id = id, Lines = cart.Lines.Select(line => line.Describe()).ToArray(), Remaining = seconds, Total = seconds, Table = cart.Table };
            Tickets.Add(ticket);
            return ticket;
        }
        /// Only the first ticket is worked on at a time, like a single barista.
        public KitchenTicket Tick(float dt)
        {
            var current = Tickets.FirstOrDefault();
            if (current == null) return null;
            if (!current.Ready) current.Remaining -= Math.Max(0, dt);
            return current.Ready ? current : null;
        }
        public void Collect(string id) => Tickets.RemoveAll(ticket => ticket.Id == id);
    }

    [Serializable]
    public sealed class TurnRecord
    {
        public string Objective, Utterance, Corrected, Explanation;
        public float Score;
        public bool Spoken;
        public int Day, Minute;
        public CriterionLevel Fluency, Lexical, Grammar, Pronunciation;
    }

    /// Learning dashboard data (spec §16).
    [Serializable]
    public sealed class LearningProgress
    {
        public List<TurnRecord> Turns = new List<TurnRecord>();
        public List<string> Objectives = new List<string>();
        public List<string> Phrases = new List<string>();
        public List<string> Scenarios = new List<string>();
        public List<string> Achievements = new List<string>();

        public void Record(TurnRecord record, SpeakingResult result)
        {
            Turns.Add(record);
            if (Turns.Count > 200) Turns.RemoveAt(0);
            if (result.TaskMet && !string.IsNullOrEmpty(record.Objective) && !Objectives.Contains(record.Objective)) Objectives.Add(record.Objective);
            foreach (var phrase in result.GoodLanguage) if (!Phrases.Contains(phrase)) Phrases.Add(phrase);
        }
        public double Average(int last = 10) => Turns.Count == 0 ? 0 : Turns.Skip(Math.Max(0, Turns.Count - last)).Average(turn => turn.Score);
        public string[] Weaknesses()
        {
            var recent = Turns.Skip(Math.Max(0, Turns.Count - 20)).ToArray();
            var list = new List<string>();
            if (recent.Count(turn => turn.Grammar == CriterionLevel.Developing) >= 2) list.Add("Grammatical accuracy: " + (recent.LastOrDefault(turn => turn.Explanation != "")?.Explanation ?? "articles and verb forms"));
            if (recent.Count(turn => turn.Fluency == CriterionLevel.Developing) >= 3) list.Add("Fluency: give fuller answers that link two ideas.");
            if (recent.Count(turn => turn.Lexical == CriterionLevel.Developing) >= 3) list.Add("Vocabulary: use polite request phrases and more precise words.");
            return list.ToArray();
        }
        public string Recommendation(IeltsBand band)
        {
            var weak = Weaknesses();
            if (weak.Length > 0) return "Practise: " + weak[0];
            var suggested = BandProfile.Adjust(band, Average(), Turns.Count);
            return suggested > band ? "You're consistently on target. Try " + BandProfile.For(suggested).Label + "." : "Keep going — try fishing with Hazel or ask Rowan for a recommendation.";
        }
        public bool Unlock(string id)
        {
            if (Achievements.Contains(id)) return false;
            Achievements.Add(id);
            return true;
        }
    }

    [Serializable]
    public sealed class WorldClock
    {
        public int Day = 1;
        public float Minute = 15 * 60; // 3pm
        public Weather Weather = Weather.Clear;
        public float NextWeather = 30;
        public const float MinutesPerSecond = 1f; // one real second = one game minute

        public int MinuteOfDay => (int)Minute;
        public string Time => $"{(int)Minute / 60:00}:{(int)Minute % 60:00}";
        public float Daylight
        {
            get
            {
                var hour = Minute / 60f;
                if (hour < 5 || hour > 21) return 0;
                if (hour < 7) return (hour - 5) / 2f;
                if (hour > 19) return (21 - hour) / 2f;
                return 1;
            }
        }
        public void Tick(float dt, Random random)
        {
            Minute += dt * MinutesPerSecond;
            if (Minute >= 24 * 60) { Minute -= 24 * 60; Day++; }
            NextWeather -= dt * MinutesPerSecond;
            if (NextWeather > 0) return;
            var roll = random.NextDouble();
            Weather = roll < 0.55 ? Weather.Clear : roll < 0.8 ? Weather.Cloudy : Weather.Rain;
            NextWeather = 60 + (float)random.NextDouble() * 120;
        }
    }

    /// Everything the slice saves. JsonUtility-friendly: public fields, lists and enums only.
    [Serializable]
    public sealed class SliceSave
    {
        public int Version = 1;
        public IeltsBand Band = IeltsBand.Band3;
        public Strictness Strictness = Strictness.Guided;
        public bool Subtitles = true, Translation;
        public float MouseSensitivity = 1;
        public Wallet Wallet = new Wallet();
        public OrderCart Cart = new OrderCart();
        public WaiterBrain Waiter = new WaiterBrain();
        public KitchenQueue Kitchen = new KitchenQueue();
        public List<Receipt> Receipts = new List<Receipt>();
        public List<string> Inventory = new List<string>(); // "id:count"
        public int FishingXp;
        public List<Catch> Catches = new List<Catch>();
        public WorldClock Clock = new WorldClock();
        public LearningProgress Progress = new LearningProgress();
        public List<SceneLesson> Lessons = new List<SceneLesson>();
        public string FocusVocabulary = "";
        public PracticeNeed PracticeNeed;
        public float Energy = 1, Hunger = 0.2f, Thirst = 0.3f;
        public string Seat = "";
        public float PlayerX, PlayerY, PlayerZ;
        public List<string> QuestsDone = new List<string>();
        public List<string> Earned = new List<string>(); // objectives met toward the active quests

        public int Count(string id) => Inventory.Where(entry => entry.StartsWith(id + ":", StringComparison.Ordinal)).Select(entry => int.Parse(entry.Substring(id.Length + 1))).FirstOrDefault();
        public void Give(string id, int amount = 1)
        {
            var total = Count(id) + amount;
            Inventory.RemoveAll(entry => entry.StartsWith(id + ":", StringComparison.Ordinal));
            if (total > 0) Inventory.Add(id + ":" + total);
        }
        /// Saves written by a newer or broken build are ignored rather than crashing the game.
        public static SliceSave Sanitize(SliceSave save)
        {
            if (save == null || save.Version != 1) return new SliceSave();
            if (!BandProfile.Valid(save.Band)) save.Band = IeltsBand.Band3;
            save.Wallet = save.Wallet ?? new Wallet();
            save.Cart = save.Cart ?? new OrderCart();
            save.Cart.Lines = (save.Cart.Lines ?? new List<CartLine>()).Where(line => line != null && CafeMenu.Find(line.Item) != null).ToList();
            save.Waiter = save.Waiter ?? new WaiterBrain();
            save.Kitchen = save.Kitchen ?? new KitchenQueue();
            save.Receipts = save.Receipts ?? new List<Receipt>();
            save.Inventory = save.Inventory ?? new List<string>();
            save.Catches = save.Catches ?? new List<Catch>();
            save.Clock = save.Clock ?? new WorldClock();
            save.Progress = save.Progress ?? new LearningProgress();
            save.Lessons = (save.Lessons ?? new List<SceneLesson>()).Where(lesson => lesson != null && BandProfile.Valid(lesson.Band)).ToList();
            foreach (var lesson in save.Lessons) lesson.Step = Math.Max(0, Math.Min(3, lesson.Step));
            save.FocusVocabulary = SpeakingTrail.Focus(save.FocusVocabulary);
            if (!Enum.IsDefined(typeof(PracticeNeed), save.PracticeNeed)) save.PracticeNeed = PracticeNeed.Everyday;
            save.QuestsDone = save.QuestsDone ?? new List<string>();
            save.Earned = save.Earned ?? new List<string>();
            save.MouseSensitivity = Math.Max(0.2f, Math.Min(save.MouseSensitivity, 3));
            return save;
        }
    }
}
