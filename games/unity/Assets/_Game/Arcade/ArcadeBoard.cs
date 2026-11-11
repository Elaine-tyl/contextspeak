using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Arcade
{
    // The authenticated website owns audio, recordings and scoring; this board only presents a round.
    public sealed class ArcadeBoard : MonoBehaviour
    {
        public event Action<string, string, string> Requested;
        private ArcadeSnapshot state;
        private VisualElement root, body, choices;
        private TextField response;
        private readonly List<TextField> gaps = new List<TextField>();
        private readonly List<Button> choiceButtons = new List<Button>();
        private Button play, submit, record, next, retry, leave;
        private Label status, error;
        private string screenKey = "", choice = "";
        private bool pendingFocus, restorePlayFocus;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_ArcadeReady();
        [DllImport("__Internal")] private static extern void CS_ArcadeAction(string action, string answer, string roundId);
        [DllImport("__Internal")] private static extern void CS_ArcadeReport(string json);
#endif
        private void Awake()
        {
            root = CafeInterface.CreateRoot(gameObject);
            root.style.display = DisplayStyle.Flex;
            GetComponent<UIDocument>().panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            root.style.backgroundColor = (Color)new Color32(246, 248, 244, 255);
            root.styleSheets.Add(Resources.Load<StyleSheet>("ArcadeStyles"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            body = new VisualElement { name = "arcade-board" };
            scroll.Add(body);
            Text("ContextSpeak · Arcade", "title");
            Text("Open this game from the ContextSpeak website to use your saved levels.");
            root.RegisterCallback<FocusInEvent>(_ => root.schedule.Execute(Report));
            root.RegisterCallback<GeometryChangedEvent>(_ => Report());
        }
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_ArcadeReady();
#endif
        }
        public void OnHostState(string json)
        {
            var value = ArcadeState.Read(json);
            if (value == null) return;
            var recovery = value.pending && !(state?.pending ?? false);
            state = value;
            var key = state.round.id + ":" + (state.result?.id ?? "answer");
            if (screenKey != key)
            {
                screenKey = key;
                Build();
            }
            if (recovery)
            {
                pendingFocus = true;
                body.schedule.Execute(FocusFirst).StartingIn(16);
            }
            UpdateControls();
        }
        private void Build()
        {
            body.Clear();
            gaps.Clear();
            choiceButtons.Clear();
            response = null;
            submit = record = next = retry = null;
            choice = "";
            var round = state.round;
            Text("CONTEXTSPEAK  /  LEVEL " + round.level + " OF 10", "eyebrow");
            Text(round.title, "title");
            Text(round.instruction);
            play = Button("Play challenge", () => Emit("play"));
            status = Text("");
            error = Text("", "error");
            if (state.result != null) Result();
            else if (round.track == "speaking") Speaking();
            else if (round.kind == "dictation") Dictation();
            else if (round.kind == "gap-fill") MissingWords();
            else Choices();
            retry = Button("Retry same answer", () => Emit("retry"));
            retry.name = "retry_answer";
            leave = Button("Back to games", () => Emit("leave"));
            pendingFocus = true;
            body.schedule.Execute(FocusFirst).StartingIn(16);
        }
        private void FocusFirst()
        {
            if (!pendingFocus) return;
            var target = state.pending ? retry : next ?? play;
            // A receipt can arrive before the host releases its busy flag. Wait
            // for an enabled target before declaring keyboard controls ready.
            if (body.panel == null || !(target.worldBound.width > 0) || !target.enabledInHierarchy)
            {
                body.schedule.Execute(FocusFirst).StartingIn(16);
                return;
            }
            target.Focus();
            pendingFocus = false;
            Report();
        }
        private void Dictation()
        {
            Text("Type what you hear");
            response = new TextField { name = "response", multiline = true, maxLength = 2000 };
            response.style.minHeight = 120;
            response.RegisterValueChangedCallback(_ => UpdateControls());
            response.RegisterCallback<KeyDownEvent>(ev => {
                if (ev.keyCode != KeyCode.Tab) return;
                if (ev.shiftKey) play.Focus();
                else if (submit.enabledSelf) submit.Focus();
                ev.PreventDefault();
                ev.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            body.Add(response);
            SubmitButton();
        }
        private void MissingWords()
        {
            Text("Fill in the missing words");
            var sentence = new VisualElement();
            sentence.AddToClassList("sentence");
            body.Add(sentence);
            var parts = state.round.gapText ?? Array.Empty<string>();
            for (var index = 0; index < parts.Length; index++)
            {
                sentence.Add(new Label(parts[index]) { enableRichText = false });
                if (index == parts.Length - 1 || index >= 6) continue;
                var field = new TextField { name = "word_" + (index + 1), maxLength = 80 };
                field.style.width = 180;
                field.style.maxWidth = Length.Percent(100);
                field.RegisterValueChangedCallback(_ => UpdateControls());
                gaps.Add(field);
                sentence.Add(field);
            }
            SubmitButton();
        }
        private void Choices()
        {
            choices = new VisualElement();
            choices.AddToClassList("choices");
            body.Add(choices);
            var items = state.round.choices ?? Array.Empty<ArcadeChoice>();
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var button = new Button(() => {
                    if (!ArcadeState.CanAnswer(state)) return;
                    choice = item.id;
                    for (var cursor = 0; cursor < choiceButtons.Count; cursor++)
                        choiceButtons[cursor].EnableInClassList("selected", items[cursor].id == choice);
                    UpdateControls();
                });
                button.text = string.IsNullOrEmpty(item.picture) ? item.label : "Picture " + (index + 1);
                button.AddToClassList("choice");
                if (!string.IsNullOrEmpty(item.picture)) button.Add(new ArcadePicture(item.picture));
                choices.Add(button);
                choiceButtons.Add(button);
            }
            SubmitButton();
        }
        private void Speaking()
        {
            Text(state.round.audioText, "question");
            foreach (var criterion in state.round.criteria ?? Array.Empty<string>()) Text("• " + criterion);
            if (state.round.kind == "pk") Text("Echo · simulated AI opponent · target " + state.round.opponentScore + "/100");
            Text("Stop recording for automatic feedback. Google processes the recording; only the transcript and feedback are saved.", "eyebrow");
            record = Button("Start recording", () => Emit(state.recording == "recording" ? "stop" : state.recording == "requesting" ? "cancel" : "record"));
        }
        private void Result()
        {
            Text(state.scored ? "Score " + state.result.score + "/100" : "More audio evidence needed", "title");
            if (state.round.kind == "pk" && state.scored)
                Text(state.result.score >= state.result.feedback.opponentScore ? "You matched or beat Echo!" : "Echo won this round. Try again!", "question");
            Text(state.result.feedback?.explanation ?? "Answer checked.");
            if (!string.IsNullOrEmpty(state.result.feedback?.correctAnswer)) Text("Answer: " + state.result.feedback.correctAnswer);
            Text("Saved to your account. Detailed corrections are below the game.");
            next = Button("Next question", () => Emit("next"));
        }
        private ArcadeAnswer Answer() => new ArcadeAnswer { response = response?.value, gapAnswers = gaps.Select(field => field.value).ToArray(), choice = choice };
        [Serializable] private class DictationAnswer { public string response; }
        [Serializable] private class GapAnswer { public string[] gapAnswers; }
        [Serializable] private class ChoiceAnswer { public string choice; }
        private string AnswerJson(ArcadeAnswer answer)
        {
            if (state.round.kind == "dictation") return JsonUtility.ToJson(new DictationAnswer { response = answer.response.Trim() });
            if (state.round.kind == "gap-fill") return JsonUtility.ToJson(new GapAnswer { gapAnswers = answer.gapAnswers.Select(value => value.Trim()).ToArray() });
            return JsonUtility.ToJson(new ChoiceAnswer { choice = answer.choice });
        }
        private void SubmitButton()
        {
            submit = Button("Check answer", () => {
                if (!ArcadeState.CanAnswer(state)) return;
                var answer = Answer();
                if (ArcadeState.Valid(state.round, answer)) Emit("submit", AnswerJson(answer));
            });
            submit.name = "check_answer";
        }
        private void Emit(string action, string answer = "")
        {
            var id = state?.round?.id ?? "";
            Requested?.Invoke(action, answer, id);
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_ArcadeAction(action, answer, id);
#endif
        }
        private void UpdateControls()
        {
            var canAnswer = ArcadeState.CanAnswer(state);
            if (state.playDisabled && root.focusController.focusedElement == play) restorePlayFocus = true;
            play.SetEnabled(!state.playDisabled);
            if (!state.playDisabled && restorePlayFocus)
            {
                play.Focus();
                restorePlayFocus = false;
            }
            status.text = state.status ?? "";
            error.text = state.error ?? "";
            if (response != null) response.SetEnabled(canAnswer);
            foreach (var gap in gaps) gap.SetEnabled(canAnswer);
            foreach (var button in choiceButtons) button.SetEnabled(canAnswer);
            submit?.SetEnabled(canAnswer && ArcadeState.Valid(state.round, Answer()));
            if (record != null)
            {
                record.text = state.recording == "recording" ? "Stop recording" : state.recording == "requesting" ? "Cancel microphone request" : "Start recording";
                record.SetEnabled(state.played && !state.busy && !state.advancing && !state.pending && !state.speechBusy);
            }
            next?.SetEnabled(!state.advancing && !state.busy);
            retry.style.display = state.pending ? DisplayStyle.Flex : DisplayStyle.None;
            retry.SetEnabled(!state.busy && !state.advancing);
            leave.SetEnabled(!state.busy && !state.advancing);
            Report();
        }
        private Label Text(string text, string style = "")
        {
            var label = new Label(text) { enableRichText = false };
            if (style != "") label.AddToClassList(style);
            body.Add(label);
            return label;
        }
        private Button Button(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            body.Add(button);
            return button;
        }
        [Serializable] private class Diagnostic
        {
            public string roundId, phase, kind, recording, focus;
            public bool answerReady, played, pending;
            public float width;
        }
        private void Report()
        {
            if (state == null || root == null) return;
            var active = root.focusController?.focusedElement as VisualElement;
            var field = active?.GetFirstAncestorOfType<TextField>();
            var diagnostic = new Diagnostic {
                roundId = state.round.id, phase = pendingFocus ? "loading" : state.result == null ? "answer" : "result", kind = state.round.kind,
                recording = state.recording, played = state.played, pending = state.pending,
                answerReady = ArcadeState.Valid(state.round, Answer()), width = float.IsNaN(root.worldBound.width) ? 0 : root.worldBound.width,
                focus = field?.name ?? active?.name ?? ""
            };
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_ArcadeReport(JsonUtility.ToJson(diagnostic));
#endif
        }
    }
}
