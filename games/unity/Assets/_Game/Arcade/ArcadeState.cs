using System;
using UnityEngine;
using System.Linq;

namespace ContextSpeak.Arcade
{
    [Serializable] public class ArcadeRound
    {
        public string id, track, kind, title, instruction, audioText;
        public int level, opponentScore;
        public string[] gapText, criteria;
        public ArcadeChoice[] choices;
    }
    [Serializable] public class ArcadeChoice { public string id, label, picture; }
    [Serializable] public class ArcadeReceipt { public string id; public float score; public ArcadeFeedback feedback; }
    [Serializable] public class ArcadeFeedback { public string explanation, correctAnswer; public int opponentScore; }
    [Serializable] public class ArcadeSnapshot
    {
        public ArcadeRound round;
        public ArcadeReceipt result;
        public bool played, playDisabled, speechBusy, frozen, busy, advancing, pending, scored = true;
        public string recording, status, error;
    }
    [Serializable] public class ArcadeAnswer
    {
        public string response, choice;
        public string[] gapAnswers;
    }
    public static class ArcadeState
    {
        public static ArcadeSnapshot Read(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 100000) return null;
            try
            {
                var state = JsonUtility.FromJson<ArcadeSnapshot>(json);
                if (state?.round == null || string.IsNullOrEmpty(state.round.id) || state.round.id.Length > 80) return null;
                // JsonUtility may materialize a nested class for a missing/null JSON field.
                if (state.result != null && string.IsNullOrEmpty(state.result.id)) state.result = null;
                var kinds = new[] { "dictation", "gap-fill", "picture", "comprehension", "response", "riddle", "puzzle", "pk" };
                return Array.IndexOf(kinds, state.round.kind) >= 0 ? state : null;
            }
            catch (ArgumentException) { return null; }
        }
        public static bool CanAnswer(ArcadeSnapshot state)
        {
            return state != null && state.result == null && state.played && !state.frozen
                && !state.busy && !state.advancing && !state.pending && !state.speechBusy;
        }
        public static bool Valid(ArcadeRound round, ArcadeAnswer answer)
        {
            if (round.track == "speaking") return false;
            if (round.kind == "dictation") return !string.IsNullOrWhiteSpace(answer.response) && answer.response.Trim().Length <= 2000 && answer.response.Any(char.IsLetterOrDigit);
            if (round.kind == "gap-fill")
                return round.gapText != null && answer.gapAnswers != null && answer.gapAnswers.Length >= 1 && answer.gapAnswers.Length <= 6
                    && answer.gapAnswers.Length == round.gapText.Length - 1
                    && Array.TrueForAll(answer.gapAnswers, value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 80);
            return round.choices != null && Array.Exists(round.choices, value => value.id == answer.choice);
        }
    }
}
