extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if not ResourceLoader.exists("res://board.gd"):
		check(false, "Arcade must have a native 2D game board.")
		quit(1)
		return
	var board = load("res://board.gd").new()
	root.add_child(board)
	await process_frame
	var state = {"round": {"id": "gap", "kind": "gap-fill", "track": "listening", "level": 10, "title": "Listen closely", "instruction": "Fill the missing words.", "gapText": ["The ", " proposal ", " the ", " ", " ", " ", "."]}, "played": true, "frozen": false, "recording": "idle"}
	board.receive(state)
	await process_frame
	check(board.gap_inputs.size() == 6, "All six missing words must have native answer fields.")
	board.gap_inputs[0].text = "decentralization"
	board.gap_inputs[0].grab_focus()
	board.gap_inputs[0].caret_column = 5
	state.status = "Playback complete."
	board.receive(state)
	check(board.gap_inputs[0].text == "decentralization", "Status updates must preserve typed answers.")
	check(board.gap_inputs[0].has_focus() and board.gap_inputs[0].caret_column == 5, "Status updates must preserve focus/caret.")
	board.size = Vector2(360, 760)
	board.adapt_layout()
	await process_frame
	check(board.choices_grid.columns == 1, "Mobile choices must use one column.")
	board.size = Vector2(288, 760)
	board.adapt_layout()
	for input in board.gap_inputs:
		input.text = "internationalization"
		input.text_changed.emit(input.text)
		check(input.custom_minimum_size.x <= board.size.x - 48, "All long blanks must fit the available native content width.")
	for kind in ["dictation", "gap-fill", "picture", "comprehension", "response", "riddle", "puzzle", "pk"]:
		state.round.id = kind
		state.round.kind = kind
		state.round.track = "speaking" if kind in ["response", "riddle", "puzzle", "pk"] else "listening"
		state.round.choices = [{"id": "b", "label": "Bridge", "picture": "bridge"}]
		board.receive(state)
		await process_frame
		check(board.get_node_or_null("Scroll") != null, "Every mode renders a scrollable native board: " + kind)
		check(board.play_button != null, "Every mode provides AI playback: " + kind)
	state.playDisabled = true
	state.speechBusy = false
	board.receive(state)
	check(not board.record_button.disabled, "Reaching the playback cap must not prevent recording an answer.")
	state.result = {"id": "receipt", "score": 80, "feedback": {"explanation": "Good work <b>plain text</b>.", "correctAnswer": "bridge", "opponentScore": 70}}
	board.receive(state)
	await process_frame
	check(board.next_button != null, "Server results provide Next without local grading.")
	check(board.result_label.text.contains("80/100"), "Show server-owned marks.")
	check(board.body.get_children().any(func(child): return child is Label and child.text.contains("You win")), "PK results retain the simulated opponent outcome.")
	state.erase("result")
	state.round.id = "dictation-again"
	state.round.kind = "dictation"
	state.round.track = "listening"
	board.receive(state)
	board.answer_input.text = "...!!!"
	board.update_controls()
	check(board.submit_button.disabled, "Punctuation alone cannot submit an answer that the host would reject.")
	board.play_button.grab_focus()
	state.playDisabled = true
	board.receive(state)
	state.playDisabled = false
	board.receive(state)
	check(board.play_button.has_focus(), "Playback must return keyboard focus when its button becomes available again.")
	state.pending = true
	state.frozen = true
	board.receive(state)
	check(board.retry_button.has_focus(), "An uncertain submission must focus Retry so keyboard users can recover.")
	board.free()
	await new_mode_checks()
	print("Arcade board checks: " + str(failures) + " failures")
	quit(1 if failures else 0)


func new_mode_checks() -> void:
	var board = load("res://board.gd").new()
	root.add_child(board)
	board.size = Vector2(900, 800)
	await process_frame
	var answers: Array = []
	board.action_requested.connect(func(action, answer): if action == "submit": answers.append(answer))
	# Nine-picture grid from the shared SVG library.
	var nine := []
	for id in ["apple", "pear", "orange", "banana", "grapes", "lemon", "bread", "cake", "cheese"]:
		nine.append({"id": id, "label": id, "picture": id})
	board.receive({"round": {"id": "p9", "kind": "picture", "track": "listening", "level": 7, "title": "Nine", "instruction": "Pick.", "choices": nine}, "played": true, "frozen": false, "recording": "idle"})
	board.adapt_layout()
	await process_frame
	check(board.choice_buttons.size() == 9 and board.choices_grid.columns == 3, "Nine pictures must form a 3 × 3 grid.")
	board.size = Vector2(360, 800)
	board.adapt_layout()
	check(board.choices_grid.columns == 3, "The 3 × 3 grid stays 3 × 3 on phones.")
	check(load("res://picture.gd").texture_for("grapes") != null, "Library pictures must load from pictures.json.")
	board.size = Vector2(900, 800)
	# Spelling: tiles, keyboard typing, undo and the submitted word.
	board.receive({"round": {"id": "sp", "kind": "spelling", "track": "listening", "level": 4, "title": "Spell", "instruction": "Spell.", "tiles": ["t", "a", "c", "e"]}, "played": false, "frozen": false, "recording": "idle"})
	await process_frame
	check(board.tile_buttons.size() == 4 and board.tile_buttons[0].disabled, "Tiles wait until the word has been played.")
	board.receive({"round": {"id": "sp", "kind": "spelling", "track": "listening", "level": 4, "title": "Spell", "instruction": "Spell.", "tiles": ["t", "a", "c", "e"]}, "played": true, "frozen": false, "recording": "idle"})
	check(board.submit_button.disabled, "An empty answer cannot be checked.")
	board.place_tile(2)
	board.place_tile(1)
	board.place_tile(0)
	check(board.tile_answer() == "cat" and board.tile_buttons[2].disabled, "Placed tiles build the word and are used up.")
	board.remove_placed(2)
	check(board.tile_answer() == "ca" and not board.tile_buttons[0].disabled, "Tapping a placed tile returns it.")
	var key := InputEventKey.new()
	key.pressed = true
	key.keycode = KEY_T
	key.unicode = "t".unicode_at(0)
	board._unhandled_key_input(key)
	check(board.tile_answer() == "cat", "Typing a letter picks a free tile.")
	board.submit_answer()
	check(answers.size() == 1 and answers[-1].get("response") == "cat", "Spelling submits the built word.")
	# Sentence builder joins words with spaces.
	board.receive({"round": {"id": "wo", "kind": "word-order", "track": "listening", "level": 1, "title": "Order", "instruction": "Order.", "tiles": ["tea", "I", "like"]}, "played": true, "frozen": false, "recording": "idle"})
	board.place_tile(1)
	board.place_tile(2)
	board.place_tile(0)
	board.submit_answer()
	check(answers[-1].get("response") == "I like tea", "Sentence builder submits words in order.")
	# Quick fire: the timer starts after playback and the answer carries elapsed time.
	board.receive({"round": {"id": "tf", "kind": "true-false", "track": "listening", "level": 7, "title": "Quick", "instruction": "Answer.", "picture": "cat", "timeLimit": 7, "choices": [{"id": "true", "label": "True"}, {"id": "false", "label": "False"}]}, "played": false, "frozen": false, "recording": "idle"})
	check(board.timer_started_ms < 0, "The timer waits for the statement to finish.")
	board.receive({"round": {"id": "tf", "kind": "true-false", "track": "listening", "level": 7, "title": "Quick", "instruction": "Answer.", "picture": "cat", "timeLimit": 7, "choices": [{"id": "true", "label": "True"}, {"id": "false", "label": "False"}]}, "played": true, "frozen": false, "recording": "idle"})
	check(board.timer_started_ms >= 0, "The timer starts after playback.")
	board.choice_buttons[0].pressed.emit()
	board.submit_answer()
	check(answers[-1].get("choice") == "true" and answers[-1].has("elapsedMs"), "Quick fire submits the choice and elapsed time.")
	board.timer_started_ms = 0
	check(board.quick_fire_elapsed(700000) == 600000, "Returning after a long break still produces a valid Quick fire submission.")
	board.free()
