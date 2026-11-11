extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var cafe = load("res://scripts/cafe.gd").new()
	if not cafe.has_method("select_stage"):
		check(false, "Practice selection must reset the active scene without changing the saved journey")
		cafe.free()
		quit(1)
		return
	# Run real UI, world and cancellation code without starting a network session.
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		root.add_child(component)
	await process_frame
	await process_frame
	cafe.connection.token = "offline-runtime-fixture"
	cafe.load_progress()
	var original_order: Dictionary = cafe.mission.state.duplicate(true)
	var original_friend: Dictionary = cafe.friend.state.duplicate(true)
	var existed := FileAccess.file_exists(cafe.SAVE)
	var original_bytes := FileAccess.get_file_as_bytes(cafe.SAVE) if existed else PackedByteArray()
	cafe.select_stage(5)
	check(cafe.friend.state.phase == "complete" and cafe.ui.record.disabled, "Completion preview disables reply controls")
	cafe.ui.reply.text = "stale typed answer"
	cafe.ui.transcript.text = "stale transcript"
	cafe.ui.feedback.text = "stale correction"
	cafe.recorder.recording = true
	cafe.recorder.pcm.append(42)
	cafe.working = true
	var previous_epoch: int = cafe.epoch
	var previous_voice_epoch: int = cafe.voice_epoch
	cafe.select_stage(3)
	check(cafe.epoch > previous_epoch and cafe.voice_epoch > previous_voice_epoch, "Switching invalidates old AI and voice completions")
	check(not cafe.working and not cafe.recorder.recording and cafe.recorder.pcm.is_empty(), "Switching cancels pending work and discards captured audio")
	check(cafe.ui.reply.editable and not cafe.ui.record.disabled and not cafe.ui.send.disabled, "Switching from completion restores usable reply controls")
	check(cafe.ui.reply.text == "" and cafe.ui.transcript.text == "", "Switching clears the previous reply and transcript")
	check(not cafe.ui.feedback.text.contains("stale") and cafe.ui.feedback.text.contains("Sample answer"), "Switching clears old corrections and labels sample history")
	check(cafe.world.seated and cafe.world.cups.get_child_count() == 2, "Friend preview seats the player with exactly two drinks")
	check(cafe.friend.state.answers.size() == 1 and cafe.ui.npc_line.text.contains("this weekend"), "Weekend practice opens the intended conversation turn")
	cafe.friend.state.answers.append({"text": "preview response", "feedback": "preview feedback"})
	cafe.restart()
	check(cafe.stages.selected == 3 and cafe.friend.state.answers.size() == 1, "Restart resets the selected preview to its sample starting point")
	check(cafe.world.cups.get_child_count() == 2, "Repeated previews recreate reward cups in the same frame")
	cafe.save_progress()
	check(FileAccess.file_exists(cafe.SAVE) == existed, "Preview saves cannot create or remove normal progress")
	if existed:
		check(FileAccess.get_file_as_bytes(cafe.SAVE) == original_bytes, "Preview replies and restarts cannot overwrite the native save")
	cafe.select_stage(1)
	check(not cafe.world.seated and cafe.world.cups.get_child_count() == 0 and cafe.world.near_barista(), "Counter practice resets the seating and reward and starts within talking distance")
	check(is_equal_approx(cafe.world.hero.rotation.y, PI), "Counter preview faces Maya after leaving a seated stage")
	check(cafe.ui.dialogue.visible and cafe.ui.reply.editable, "Counter practice is immediately ready to test")
	cafe.select_stage(0)
	check(cafe.mission.state == original_order and cafe.friend.state == original_friend, "Continue reloads the untouched original saved journey")
	check(not cafe.stages.preview() and not cafe.ui.dialogue.visible, "Continuing leaves preview mode and closes the prior preview conversation")
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		component.free()
	cafe.free()
	print("Cafe practice switching checks: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
