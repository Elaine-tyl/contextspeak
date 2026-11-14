using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Slice;

namespace ContextSpeak.Learning
{
    /// A three-part formative rehearsal. Nothing inspects a partial recording or interrupts speech.
    public sealed class IELTSMockExamManager
    {
        public int Part { get; private set; }
        public bool Recording { get; private set; }
        public bool Active { get; private set; }
        public bool Complete => Active && Part >= 3;
        public readonly List<AssessmentRecord> Feedback = new List<AssessmentRecord>();
        private IeltsBand band = IeltsBand.Band5;
        private readonly List<UtteranceEvidence> answers = new List<UtteranceEvidence>();
        public string Status { get; private set; } = "";
        public string Prompt => Part == 0 ? "Part 1 · Tell me about a place you enjoy visiting and why."
            : Part == 1 ? "Part 2 · Describe a time someone helped you. Explain what happened, who helped and why it mattered. Take a minute to prepare; speak for one to two minutes when ready."
            : Part == 2 ? "Part 3 · How can communities help people become more independent? Compare different approaches and their limitations."
            : "Rehearsal complete. Review your feedback in Learning progress.";
        public void Begin(DateTime utc, int targetBand = 5) { band = (IeltsBand)Math.Max(3, Math.Min(9, targetBand)); Part = 0; Active = true; Recording = false; Feedback.Clear(); answers.Clear(); Status = "Three-part practice, not an official IELTS exam. Feedback is deferred."; }
        public void StartRecording() { if (Active && !Complete) Recording = true; }
        public void StopRecording() => Recording = false;
        public void Cancel() { Active = false; Recording = false; }
        public bool MoveNext(UtteranceEvidence evidence, DateTime utc)
        {
            if (!Active || Recording || Complete) return false;
            if (!evidence.Spoken || !evidence.ProviderAvailable || !evidence.GoalAssessed)
            { Status = "MOCK / unassessed — reconnect the services; this answer has not been recorded."; return false; }
            answers.Add(evidence);
            Part++;
            if (Complete) Feedback.AddRange(answers.Select(answer => SpeakingAssessmentService.Assess(answer, band)));
            return true;
        }
        public ExamRecord Result(DateTime utc) => !Complete ? null : new ExamRecord {
            Utc = utc.Ticks,
            Feedback = "Three-part formative rehearsal — not an official IELTS result.\n" + string.Join("\n\n", Feedback.Select((value, i) => "Part " + (i + 1) + "\n" + value.Feedback)),
            Answers = answers.Select((answer, i) => new LearningEvidence {
                Id = Guid.NewGuid().ToString("N"), Utc = utc.Ticks, Scenario = "exam-part-" + (i + 1), Text = answer.Text,
                Correction = Feedback[i].Correction, Feedback = Feedback[i].Feedback, Independent = true, Success = Feedback[i].TaskMet,
            }).ToList(),
        };
    }
}
