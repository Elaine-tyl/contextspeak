using System;
using System.Collections;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Slice
{
    // Paid or local AI providers sit behind these interfaces so they can be swapped without touching gameplay.
    // No API secret ever lives in the client: the companion (pnpm world:dev) or a backend proxy holds them.

    public interface ISpeechToText
    {
        bool Available { get; }
        IEnumerator Transcribe(byte[] wave, Action<string> success, Action<string> failure);
    }

    public interface ITextToSpeech
    {
        bool Available { get; }
        IEnumerator Speak(string text, Action<AudioClip> success, Action<string> failure);
    }

    [Serializable] public sealed class SliceReviewRequest { public string band, npc, task, npcLine, text, goal; public string reference = ""; public string[] history = Array.Empty<string>(); }

    [Serializable] public sealed class TranscriptPracticeScores
    {
        public int coherence, lexical, grammar;
        public string evidence = "";
    }

    [Serializable] public sealed class SliceReviewReply { public TranscriptPracticeScores languageScores; public string correction = "", explanation = "", lexicalTip = "", acknowledgement = ""; public bool relevant = true, assessedGoal, goalMet;
        public ContextSpeak.Learning.UtteranceEvidence Evidence(string text) => new ContextSpeak.Learning.UtteranceEvidence {
            Text = text, Spoken = true, ProviderAvailable = true, GoalAssessed = assessedGoal, Relevant = relevant, GoalMet = goalMet,
            Correction = correction ?? "", Explanation = explanation ?? "", ImprovedExpression = lexicalTip ?? "",
            LanguageScoresAssessed = languageScores != null, ScoreEvidence = languageScores?.evidence ?? "",
            CoherenceScore = languageScores?.coherence ?? 0, LexicalScore = languageScores?.lexical ?? 0, GrammarScore = languageScores?.grammar ?? 0,
        };
    }

    /// Optional language reviewer. It can only add feedback; it never changes the order, money or progress.
    public interface IConversationReviewer
    {
        bool Available { get; }
        IEnumerator Review(SliceReviewRequest request, Action<SliceReviewReply> success, Action<string> failure);
    }

    /// Dedicated pronunciation assessment (e.g. a phoneme-level scoring API behind the backend proxy).
    /// Speech-recognition confidence is deliberately NOT used as a substitute.
    public interface IPronunciationAssessor
    {
        bool Available { get; }
        IEnumerator Assess(byte[] wave, string reference, Action<double?> done);
    }

    /// Local companion: Whisper-style STT, local TTS and a local Qwen reviewer on 127.0.0.1:4320.
    public sealed class CompanionServices : ISpeechToText, ITextToSpeech, IConversationReviewer
    {
        private readonly CafeClient client;
        public CompanionServices(CafeClient client) { this.client = client; }
        public bool Available => client != null && client.Connected;
        public IEnumerator Transcribe(byte[] wave, Action<string> success, Action<string> failure) => client.Transcribe(wave, success, failure);
        public IEnumerator Speak(string text, Action<AudioClip> success, Action<string> failure) => client.Speak(text, success, failure);
        public IEnumerator Review(SliceReviewRequest request, Action<SliceReviewReply> success, Action<string> failure) =>
            client.Json("/slice/review", request, success, failure);
    }

    /// Fallback when no provider is configured: pronunciation stays "not assessed".
    public sealed class NoPronunciationAssessor : IPronunciationAssessor
    {
        public bool Available => false;
        public IEnumerator Assess(byte[] wave, string reference, Action<double?> done) { done(null); yield break; }
    }
}
