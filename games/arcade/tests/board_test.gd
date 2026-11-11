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
	print("Arcade board checks: " + str(failures) + " failures")
	quit(1 if failures else 0)
