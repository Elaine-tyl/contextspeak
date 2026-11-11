using System;
using ContextSpeak.Arcade;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class ArcadeChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Arcade regression: " + message);
        }
        public static void Verify()
        {
            foreach (var value in new[] { "garbage", "{}", "{\"round\":{\"id\":\"x\",\"kind\":\"unknown\"}}" })
                Check(ArcadeState.Read(value) == null, "Malformed or unsupported host state cannot replace a round.");
            var state = ArcadeState.Read("{\"round\":{\"id\":\"a\",\"kind\":\"dictation\",\"track\":\"listening\"},\"recording\":\"idle\"}");
            Check(!ArcadeState.CanAnswer(state), "Audio must finish before checking an answer.");
            state.played = true;
            Check(ArcadeState.CanAnswer(state), "A played question permits an answer.");
            state.pending = true;
            Check(!ArcadeState.CanAnswer(state), "An uncertain response must retry the same submission.");
            state.pending = false;
            state.result = new ArcadeReceipt { id = "receipt" };
            Check(!ArcadeState.CanAnswer(state), "A receipt freezes its round.");
            Check(!ArcadeState.Valid(state.round, new ArcadeAnswer { response = "!?..." }), "Punctuation alone cannot become an apparently submitted dictation answer.");
            var gaps = new ArcadeRound { kind = "gap-fill", gapText = new[] { "I", "coffee", "." } };
            Check(!ArcadeState.Valid(gaps, new ArcadeAnswer { gapAnswers = new[] { "like" } }), "Every blank requires exactly one answer.");
            Check(!ArcadeState.Valid(gaps, new ArcadeAnswer { gapAnswers = new[] { "like", " " } }), "Whitespace does not answer a blank.");
            Check(ArcadeState.Valid(gaps, new ArcadeAnswer { gapAnswers = new[] { "like", "today" } }), "Two complete blanks permit checking.");
            var choices = new ArcadeRound { kind = "picture", choices = new[] { new ArcadeChoice { id = "bridge" } } };
            Check(!ArcadeState.Valid(choices, new ArcadeAnswer { choice = "wrong-id" }), "Unknown choice ids cannot be sent.");
            Check(ArcadeState.Valid(choices, new ArcadeAnswer { choice = "bridge" }), "Authored choice ids can be selected.");
            Check(!ArcadeState.Valid(new ArcadeRound { kind = "pk", track = "speaking" }, new ArcadeAnswer { response = "pretend audio" }), "Typing cannot replace a spoken recording.");
            Debug.Log("ContextSpeak Unity arcade rule checks passed");
        }
    }
}
