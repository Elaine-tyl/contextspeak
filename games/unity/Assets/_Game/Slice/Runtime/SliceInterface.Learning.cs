using System;
using System.Linq;
using ContextSpeak.Learning;
using UnityEngine.UIElements;

namespace ContextSpeak.Slice
{
    public sealed partial class SliceInterface
    {
        private VisualElement learningCard;
        private VoiceCoachPanel learningCoach;
        private Label learningPhase, learningMessage, learningModel;
        private Button learningRepeat, learningReference, learningPlayback;

        private void BuildLearningCard()
        {
            learningCard = Add(dialogue, "learning-v7");
            learningPhase = Text(learningCard, "", "section-title");
            learningCoach = new VoiceCoachPanel(learningCard);
            learningMessage = Text(learningCard, "", "status");
            learningModel = Text(learningCard, "", "corrected");
            var actions = Add(learningCard, null, "row", "wrap");
            learningRepeat = Action(actions, "Practise correction", "learn-repeat", "", "speak");
            learningReference = Action(actions, "Hear model", "learn-reference", "", "secondary");
            learningPlayback = Action(actions, "My recording", "learn-audio", "", "secondary");
        }

        private void RenderLearning(SliceView view)
        {
            Show(learningCard, view.Adaptive);
            historyToggle.SetEnabled(true);
            if (!view.Adaptive) return;
            var recall = !view.LearningExam && view.LearningStage == LearningStage.Recall;
            learningPhase.text = view.LearningExam ? "IELTS MOCK · feedback after all parts" : "LEARNING · " + view.LearningStage;
            learningMessage.text = view.Mic == "recording" ? "Recording · " + new string('▮', UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(view.InputLevel * 16))) : view.LearningStatus;
            learningModel.text = view.LearningTarget;
            Show(learningModel, view.LearningTarget != "" && !recall && !view.LearningExam);
            learningCoach.Render(view.LearningHints, view.LearningAssessment, recall || view.LearningExam && !view.LearningComplete,
                report: view.LearningExam && view.LearningComplete ? view.LearningFeedback : "");
            // Recall conceals model, notes, hints, history, transcript and replay.
            Show(recognized, !recall && !view.LearningExam && view.Recognized != "");
            Show(history, showHistory && !recall && !view.LearningExam);
            historyToggle.SetEnabled(!recall && !view.LearningExam);
            repeat.SetEnabled(view.CanReplay && !recall);
            slower.SetEnabled(view.CanReplay && !recall);
            Show(suggestions, false);
            Show(aiTip, false);
            Show(correction, false);
            Show(status, false);
            root.EnableInClassList("has-speaking-feedback", false);
            Show(learningRepeat, view.LearningStage == LearningStage.Feedback && !view.LearningExam);
            Show(learningReference, view.LearningTarget != "" && !recall && !view.LearningExam);
            Show(learningPlayback, view.CanReplayRecording && !recall && !view.LearningExam);
            learningRepeat.SetEnabled(!view.Busy);
            mic.SetEnabled(!view.Busy && view.Mic != "offline" && !view.LearningComplete
                && (view.LearningExam || view.LearningStage != LearningStage.Feedback));
            learningPhase.tooltip = "Repetition and independent recall are recorded separately. Practice targets are not official IELTS scores.";
        }

        private void LearningPage(VisualElement body, SliceView view)
        {
            var memory = view.Save.Learner;
            Text(body, "YOUR LEARNING", "section-title");
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                var label = mode == GameMode.FreeLife ? "Free life · natural conversation" : mode == GameMode.GuidedLearning ? "Guided · correct, repeat, recall" : "Mock exam · feedback at the end";
                var button = Action(body, label, "learn-mode", mode.ToString(), "option", "wide");
                button.EnableInClassList("selected", memory.Mode == mode);
            }
            Text(body, "Café → School → Home", "section-title");
            Text(body, "Speak independently, practise a correction when needed, recall without the model, then revisit the expression on a later day with a different NPC. Repetition alone never establishes mastery.", "hud-muted");
            foreach (var place in new[] { ("Café · Maya", "cafe"), ("School · Ms Taylor", "school"), ("Home · Alex", "home") })
                Action(body, place.Item1, "learn-start", place.Item2, "secondary", "wide");
            Action(body, "Review due expressions", "learn-review", "cafe", "secondary", "wide");
            Action(body, "Daily spoken journal · Home", "learn-journal", "", "secondary", "wide");
            Action(body, "Exam centre · School", "learn-exam", "", "secondary", "wide");
            Text(body, "The exam is a three-part formative rehearsal. No official score is predicted; answers can be up to two minutes. Stop recording when you finish.", "hud-muted");
            Text(body, LearningDashboardController.Build(memory, DateTime.UtcNow));
            Text(body, "Review schedule (UTC)", "section-title");
            foreach (var item in memory.Items.Where(item => item.DueUtc > 0).OrderBy(item => item.DueUtc).Take(15))
                Text(body, item.Expression + " · " + (SpacedReviewScheduler.Due(item, DateTime.UtcNow) ? "REVIEW DUE" : item.State.ToString())
                    + " · " + new DateTime(item.DueUtc, DateTimeKind.Utc).ToString("MMM d, HH:mm") + " UTC", "hud-muted");
        }
    }
}
