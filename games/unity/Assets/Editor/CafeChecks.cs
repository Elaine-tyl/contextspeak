using System;
using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEditor;

namespace ContextSpeak.Editor
{
    public static class CafeChecks
    {
        private static readonly List<string> Failures = new List<string>();
        public static void Verify()
        {
            Failures.Clear();
            var game = new CafeMission();
            Check(!game.OrderComplete, "A new order is incomplete.");
            game.Apply(game.Parse("Two lattes, please."));
            Check(game.Stage == "order" && !game.OrderComplete, "An over-budget order cannot advance.");
            game.Apply(game.Parse("One coffee and one tea, please."));
            Check(game.Stage == "temperature" && game.Total == 700, "Two affordable drinks advance.");
            game.Apply(game.Parse("Not iced. Hot, please."));
            Check(game.Temperature == "hot", "A rejected option is not selected.");
            game.Apply(game.Parse("For here, please."));
            Check(game.Stage == "confirm", "Service follows temperature.");
            game.Apply(game.Parse("I don't think that's correct."));
            Check(!game.OrderComplete, "Negative confirmation must never place an order.");
            game.Apply(game.Parse("Yes, that's correct."));
            Check(game.OrderComplete && game.BeginWalk() && game.Sit(), "Confirmation unlocks walking and sitting.");
            Check(!game.ReviewFriend("Tea.", new FriendReview { accepted = false, acknowledgement = "Thanks.", feedback = "Please add a reason." }), "Correction cannot advance a turn.");
            Check(game.Turn == 0, "The same question remains after rejection.");
            for (var index = 0; index < 3; index++)
                Check(game.ReviewFriend("A complete answer " + index, new FriendReview { accepted = true, acknowledgement = "Nice!", feedback = "Clear answer." }), "A checked answer advances once.");
            Check(game.Complete && game.Turn == 3, "Three accepted turns complete the cafe.");
            var restored = new CafeMission();
            Check(restored.Restore(game.Save()) && restored.Complete, "A valid local save restores.");
            Check(!restored.Restore("{\"version\":1,\"phase\":\"complete\"}"), "A corrupt save cannot fabricate completion.");
            Check(!restored.Restore("garbage"), "Malformed saves are rejected.");
            var practice = new CafeMission();
            practice.StartPractice("friend");
            Check(practice.Practice && practice.Phase == "seated", "The test stage is labelled practice.");
            Check(!practice.OrderComplete || practice.Practice, "Stage jumps never impersonate earned progress.");
            var ambiguous = new CafeMission();
            ambiguous.Apply(ambiguous.Parse("Two hot coffees for here."));
            ambiguous.Apply(ambiguous.Parse("Yes, but wait."));
            Check(!ambiguous.OrderComplete, "An ambiguous affirmative cannot confirm an order.");
            var quantities = new CafeMission();
            quantities.Apply(quantities.Parse("Two coffees and three lattes."));
            Check(quantities.Stage == "order", "Unsupported quantities cannot be silently discarded.");
            var counterPractice = new CafeMission();
            counterPractice.StartPractice("counter");
            counterPractice.Apply(counterPractice.Parse("Cancel, please."));
            Check(counterPractice.Practice, "Starting over in practice must never overwrite earned progress.");
            var badTranscript = false;
            try { CafeClient.ReadTranscript(System.Text.Encoding.UTF8.GetBytes("not json")); } catch (FormatException) { badTranscript = true; }
            Check(badTranscript, "Malformed transcription must produce recoverable feedback.");
            var longTranscript = false;
            try { CafeClient.ReadTranscript(System.Text.Encoding.UTF8.GetBytes("{\"text\":\"" + new string('a', 401) + "\"}")); } catch (FormatException) { longTranscript = true; }
            Check(longTranscript, "Transcription and reply limits must agree.");
            var samples = new float[4800];
            samples[10] = 0.5f;
            var wave = CafeWave.Encode(samples, 16000, 1);
            var decoded = CafeWave.Decode(wave);
            Check(decoded.Rate == 16000 && decoded.Samples.Length == samples.Length, "PCM encoding preserves rate and duration.");
            Check(Math.Abs(decoded.Samples[10] - 0.5f) < 0.001f, "PCM encoding preserves speech amplitude.");
            var rejected = false;
            try { CafeWave.Decode(new byte[44]); } catch (FormatException) { rejected = true; }
            Check(rejected, "Invalid audio cannot be played or uploaded.");
            if (Failures.Count > 0) throw new Exception(string.Join("\n", Failures));
            UnityEngine.Debug.Log("ContextSpeak Unity mission checks passed");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) Failures.Add(message);
        }
    }
}
