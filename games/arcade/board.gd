extends Control

signal action_requested(action: String, answer: Dictionary)

const INK := Color("243d35")
const GREEN := Color("176b58")
const BLUE := Color("3156ce")
const RED := Color("a52d35")
const PICTURE = preload("res://picture.gd")

var state: Dictionary = {}
var screen_key := ""
var body: VBoxContainer
var choices_grid: GridContainer
var gap_inputs: Array[LineEdit] = []
var answer_input: TextEdit
var choice_buttons: Array[Button] = []
var selected_choice := ""
var play_button: Button
var submit_button: Button
var record_button: Button
var next_button: Button
var retry_button: Button
var status_label: Label
var error_label: Label
var result_label: Label
var bridge: JavaScriptObject
var callback: JavaScriptObject

func _ready() -> void:
	theme = make_theme()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	resized.connect(adapt_layout)
	get_viewport().gui_focus_changed.connect(func(_control): report_state())
	var scroll := ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var margin := MarginContainer.new()
	margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for edge in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + edge, 24)
	scroll.add_child(margin)
	body = VBoxContainer.new()
	body.add_theme_constant_override("separation", 16)
	margin.add_child(body)
	add_label("ContextSpeak · 2D Arcade", 24)
	add_label("Open this game from the ContextSpeak website to use your saved levels.", 18)
	if OS.has_feature("web"):
		bridge = JavaScriptBridge.get_interface("ContextSpeakArcade")
		if bridge:
			callback = JavaScriptBridge.create_callback(_on_state)
			bridge.subscribe(callback)
			emit_action("ready")

func make_theme() -> Theme:
	var value := Theme.new()
	value.default_font_size = 18
	value.set_color("font_color", "Label", INK)
	value.set_color("font_color", "Button", INK)
	value.set_color("font_hover_color", "Button", GREEN)
	value.set_color("font_disabled_color", "Button", Color("82928a"))
	value.set_color("font_color", "LineEdit", INK)
	value.set_color("font_color", "TextEdit", INK)
	value.set_color("font_placeholder_color", "TextEdit", Color("687e70"))
	value.set_color("font_placeholder_color", "LineEdit", Color("687e70"))
	value.set_color("caret_color", "LineEdit", BLUE)
	value.set_color("caret_color", "TextEdit", BLUE)
	for type_name in ["Button", "LineEdit", "TextEdit"]:
		value.set_stylebox("normal", type_name, box(Color.WHITE, Color("cfdbd4")))
		value.set_stylebox("focus", type_name, box(Color(0, 0, 0, 0), BLUE, 2))
		value.set_stylebox("read_only", type_name, box(Color("edf2ec"), Color("d6dfd7")))
	value.set_stylebox("hover", "Button", box(Color("e0eee5"), GREEN))
	value.set_stylebox("pressed", "Button", box(Color("d2e7dc"), GREEN))
	value.set_stylebox("disabled", "Button", box(Color("edf2ec"), Color("d6dfd7")))
	return value

func box(fill: Color, border: Color, width := 1) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill
	style.border_color = border
	style.set_border_width_all(width)
	style.set_corner_radius_all(12)
	style.content_margin_left = 16
	style.content_margin_right = 16
	style.content_margin_top = 12
	style.content_margin_bottom = 12
	return style

func _on_state(arguments: Array) -> void:
	if arguments.is_empty() or not arguments[0] is String:
		return
	var parsed = JSON.parse_string(arguments[0])
	if parsed is Dictionary:
		receive(parsed)

func receive(snapshot: Dictionary) -> void:
	if not snapshot.get("round") is Dictionary:
		return
	state = snapshot
	var result: Dictionary = state.get("result", {}) if state.get("result") is Dictionary else {}
	var key := str(state.round.id) + ":" + str(result.get("id", "answer"))
	# Playback/recording status must never recreate a field while the learner types.
	if key != screen_key:
		screen_key = key
		build_screen(result)
	update_controls()
	report_state()

func build_screen(result: Dictionary) -> void:
	for child in body.get_children():
		body.remove_child(child)
		child.queue_free()
	gap_inputs.clear()
	choice_buttons.clear()
	selected_choice = ""
	answer_input = null
	submit_button = null
	record_button = null
	next_button = null
	retry_button = null
	result_label = null
	var round: Dictionary = state.round
	add_label("CONTEXTSPEAK  /  LEVEL " + str(int(round.level)) + " OF 10", 16)
	add_label(str(round.title), 30)
	var art = PICTURE.new()
	art.kind = "voice" if round.track == "speaking" else "listen"
	art.custom_minimum_size = Vector2(0, 100)
	body.add_child(art)
	add_label(str(round.instruction), 18)
	play_button = add_button("Play challenge", func(): emit_action("play"))
	status_label = add_label("", 16)
	error_label = add_label("", 16)
	error_label.add_theme_color_override("font_color", RED)
	choices_grid = GridContainer.new()
	choices_grid.columns = 2
	choices_grid.add_theme_constant_override("h_separation", 12)
	choices_grid.add_theme_constant_override("v_separation", 12)
	body.add_child(choices_grid)
	if not result.is_empty():
		show_result(result)
	elif round.track == "speaking":
		show_speaking(round)
	elif round.kind == "gap-fill":
		show_gaps(round)
	elif round.kind == "dictation":
		add_label("Type what you hear", 18)
		answer_input = TextEdit.new()
		answer_input.name = "response"
		answer_input.placeholder_text = "Listen first, then write the sentence…"
		answer_input.custom_minimum_size.y = 135
		answer_input.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
		answer_input.text_changed.connect(update_controls)
		answer_input.gui_input.connect(func(event):
			if event is InputEventKey and event.pressed and event.keycode == KEY_TAB:
				if event.shift_pressed:
					play_button.grab_focus()
				elif submit_button and not submit_button.disabled:
					submit_button.grab_focus()
				answer_input.accept_event())
		body.add_child(answer_input)
		make_submit()
	else:
		show_choices(round)
		make_submit()
	retry_button = add_button("Retry same answer", func(): emit_action("retry"))
	retry_button.name = "retry_answer"
	add_button("Back to games", func(): emit_action("leave"))
	adapt_layout()
	if next_button:
		next_button.grab_focus()
	else:
		play_button.grab_focus()

func show_speaking(round: Dictionary) -> void:
	add_label(str(round.get("audioText", "")), 21)
	for criterion in round.get("criteria", []):
		add_label("• " + str(criterion), 17)
	if round.has("opponentScore"):
		add_label("Echo · simulated AI opponent · target " + str(round.opponentScore) + "/100", 17)
	add_label("Stop recording to get AI feedback automatically. Google processes the recording; only the transcript and feedback are saved.", 15)
	record_button = add_button("Start recording", recording_action)

func recording_action() -> void:
	match state.get("recording", "idle"):
		"requesting":
			emit_action("cancel")
		"recording":
			emit_action("stop")
		_:
			emit_action("record")

func show_gaps(round: Dictionary) -> void:
	var flow := HFlowContainer.new()
	flow.add_theme_constant_override("h_separation", 6)
	flow.add_theme_constant_override("v_separation", 10)
	body.add_child(flow)
	var parts: Array = round.get("gapText", [])
	for index in parts.size():
		for word in str(parts[index]).split(" ", false):
			var label := Label.new()
			label.text = word
			flow.add_child(label)
		if index < parts.size() - 1:
			var input := LineEdit.new()
			input.name = "word_" + str(index + 1)
			input.placeholder_text = "Word " + str(index + 1)
			input.tooltip_text = input.placeholder_text
			input.max_length = 80
			fit_gap(input)
			input.text_changed.connect(func(_text):
				fit_gap(input)
				update_controls())
			input.text_submitted.connect(func(_text): submit_answer())
			flow.add_child(input)
			gap_inputs.append(input)
	make_submit()

func show_choices(round: Dictionary) -> void:
	var choices: Array = round.get("choices", [])
	for index in choices.size():
		var item: Dictionary = choices[index]
		var button := Button.new()
		button.name = "choice_" + str(index)
		button.custom_minimum_size = Vector2(0, 64)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		button.toggle_mode = true
		button.text = "Picture " + str(index + 1) if item.has("picture") else str(item.label)
		if item.has("picture"):
			button.custom_minimum_size.y = 150
			button.alignment = HORIZONTAL_ALIGNMENT_LEFT
			var art = PICTURE.new()
			art.kind = str(item.picture)
			art.mouse_filter = Control.MOUSE_FILTER_IGNORE
			art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			art.offset_top = 40
			button.add_child(art)
		button.pressed.connect(func():
			selected_choice = str(item.id)
			for other in choice_buttons:
				other.button_pressed = other == button
			update_controls())
		choices_grid.add_child(button)
		choice_buttons.append(button)

func show_result(result: Dictionary) -> void:
	var score = result.get("score")
	result_label = add_label("More audio evidence needed" if score == null else str(int(score)) + "/100 · Answer checked", 27)
	result_label.add_theme_color_override("font_color", GREEN)
	var feedback: Dictionary = result.get("feedback", {})
	if feedback.has("opponentScore") and score != null:
		var opponent := int(feedback.opponentScore)
		var outcome := "You win" if score > opponent else "Draw" if score == opponent else "Echo wins this round"
		add_label(outcome + " · You " + str(int(score)) + " : Echo " + str(opponent), 20)
	if feedback.has("correctAnswer"):
		add_label("Correct answer: " + str(feedback.correctAnswer), 20)
	if feedback.get("gapFill") is Dictionary:
		for word in feedback.gapFill.get("words", []):
			var line := add_label(str(word.answer) + (" ✓" if word.correct else " → " + str(word.expected)), 19)
			line.add_theme_color_override("font_color", GREEN if word.correct else RED)
	add_label(str(feedback.get("explanation", "")), 18)
	add_label("Saved to your account. Detailed feedback is available below the game.", 16)
	next_button = add_button("Next question", func(): emit_action("next"))

func make_submit() -> void:
	submit_button = add_button("Check answer", submit_answer)

func submit_answer() -> void:
	if not submit_button or submit_button.disabled:
		return
	var answer := {}
	match str(state.round.kind):
		"dictation":
			answer.response = answer_input.text
		"gap-fill":
			answer.gapAnswers = gap_inputs.map(func(input): return input.text)
		_:
			answer.choice = selected_choice
	emit_action("submit", answer)

func update_controls() -> void:
	if not play_button:
		return
	var frozen: bool = state.get("frozen", false)
	var played: bool = state.get("played", false)
	play_button.disabled = state.get("playDisabled", false)
	status_label.text = str(state.get("status", ""))
	status_label.visible = not status_label.text.is_empty()
	error_label.text = str(state.get("error", ""))
	error_label.visible = not error_label.text.is_empty()
	var retry_visible: bool = state.get("pending", false) and not state.get("result") is Dictionary
	var recovering := retry_visible and (not retry_button.visible or retry_button.disabled)
	retry_button.visible = retry_visible
	retry_button.disabled = state.get("busy", false)
	if recovering and not retry_button.disabled:
		retry_button.grab_focus()
	if answer_input:
		answer_input.editable = not frozen
	for input in gap_inputs:
		input.editable = not frozen and played
	for button in choice_buttons:
		button.disabled = frozen or not played
	if submit_button:
		var complete := false
		if answer_input:
			var meaningful := RegEx.create_from_string("[\\p{L}\\p{N}]").search(answer_input.text) != null
			complete = meaningful and answer_input.text.length() <= 2000
		elif not gap_inputs.is_empty():
			complete = gap_inputs.all(func(input): return not input.text.strip_edges().is_empty())
		else:
			complete = not selected_choice.is_empty()
		submit_button.disabled = frozen or not played or not complete
	if record_button:
		var recording: String = state.get("recording", "idle")
		record_button.text = "Stop recording" if recording == "recording" else "Cancel microphone request" if recording == "requesting" else "Start recording"
		record_button.disabled = frozen or not played or state.get("speechBusy", false) and recording not in ["requesting", "recording"]
	if next_button:
		next_button.disabled = state.get("busy", false) or state.get("advancing", false)
	report_state()

func add_label(text: String, font_size: int) -> Label:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", font_size)
	body.add_child(label)
	return label

func add_button(text: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.name = text.to_snake_case()
	button.custom_minimum_size.y = 48
	button.pressed.connect(action)
	body.add_child(button)
	return button

func fit_gap(input: LineEdit) -> void:
	var preferred := minf(270, maxf(150, input.text.length() * 11 + 34))
	input.custom_minimum_size.x = minf(preferred, maxf(80, size.x - 48))

func adapt_layout() -> void:
	for input in gap_inputs:
		fit_gap(input)
	if choices_grid:
		choices_grid.columns = 1 if size.x < 600 else 2
	report_state()

func report_state() -> void:
	if bridge and not state.is_empty():
		var focused := get_viewport().gui_get_focus_owner()
		bridge.report(JSON.stringify({"focus": str(focused.name) if focused else "", "answerReady": submit_button != null and not submit_button.disabled,"roundId": state.round.id, "phase": "result" if state.get("result") is Dictionary else "answer", "kind": state.round.kind, "played": state.get("played", false), "width": size.x, "recording": state.get("recording", "idle"), "pending": state.get("pending", false)}))

func emit_action(action: String, answer: Dictionary = {}) -> void:
	action_requested.emit(action, answer)
	if bridge:
		bridge.emit(action, JSON.stringify(answer), str(state.get("round", {}).get("id", "")))
