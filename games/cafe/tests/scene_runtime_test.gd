extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var script = load("res://scripts/roleplay_controller.gd")
	if script == null:
		check(false, "Scene selection needs a controller that preserves the cafe")
		quit(1)
		return
	if not script.new().has_method("action_caption"):
		check(false, "Scene effects need the accepted friend-scene answer, including restored choices")
		quit(1)
		return
	var cafe = load("res://scripts/cafe.gd").new()
	# Exercise the real UI and scene switching without a live AI server.
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		root.add_child(component)
	await process_frame
	cafe.roleplay.setup(cafe)
	check(cafe.roleplay.menu.picker.get_theme_color("font_color").get_luminance() < 0.3, "Scene choices need readable dark text on the cream menu")
	check(cafe.roleplay.menu.picker.get_theme_color("font_focus_color").get_luminance() < 0.3, "Keyboard-focused scene choices must keep readable text")
	var chooser = cafe.roleplay.menu
	check(chooser.roles.focus_mode == Control.FOCUS_NONE, "The cafe's disabled role selector must not trap keyboard activation")
	chooser.show_menu()
	var back: Control = chooser.panel.get_child(0).get_child(-1).get_child(-1)
	back.grab_focus()
	check(chooser.panel.is_ancestor_of(back.find_next_valid_focus()), "Tab at the menu end must stay inside the scene menu")
	chooser.picker.grab_focus()
	check(chooser.panel.is_ancestor_of(chooser.picker.find_prev_valid_focus()), "Shift-Tab at the menu start must stay inside the scene menu")
	chooser.hide_menu()
	cafe.connection.token = "offline-test"
	cafe.load_progress()
	var original_order: Dictionary = cafe.mission.state.duplicate(true)
	var original_friend: Dictionary = cafe.friend.state.duplicate(true)
	check(not cafe.roleplay.begin("invalid", "student"), "Invalid scene selection keeps the current world")
	check(cafe.roleplay.begin("shopping", "shopper"), "Shopping with a friend starts from the scene menu")
	check(not cafe.world.near_npc() and cafe.roleplay.active(), "A selected scene starts at its exploration entrance")
	check(not cafe.ui.dialogue.visible, "Scene selection must not open a remote dialogue")
	cafe.world.player.position = cafe.world.npc_data(cafe.roleplay.target_id()).approach
	cafe.talk()
	check(cafe.ui.npc_title.text.contains(cafe.roleplay.lesson.step().npc.name), "The nearby actor replaces cafe labels")
	check(not cafe.ui.stage_controls.visible and not cafe.ui.menu.visible, "Cafe-only controls are hidden in other scenes")
	check(cafe.ui.reply.editable and not cafe.ui.record.disabled, "A role opens usable reply controls")
	var accepted := {"accepted": true, "correction": "", "feedback": "Nice choice.", "acknowledgement": "Looks good!"}
	cafe.roleplay.lesson.respond("I'd like a red jacket.", accepted)
	check(cafe.roleplay.action_caption(0) == "I'd like a red jacket.", "Friend-scene visuals must reflect the learner's own choice")
	var chosen_outfit: Dictionary = cafe.roleplay.lesson.state.duplicate(true)
	cafe.roleplay.sessions["shopping/shopper"] = chosen_outfit
	cafe.roleplay.begin("shopping", "shopper")
	check(cafe.roleplay.lesson.turn() == 1 and cafe.roleplay.action_caption(0).contains("red jacket"), "Restored visuals retain the learner's clothing choice")
	check(cafe.roleplay.begin("amusement_park", "planner"), "The park offers a different friend role")
	check(not cafe.ui.dialogue.visible and not cafe.ui.reply.editable, "Park selection resets into free exploration")
	var previous_epoch: int = cafe.epoch
	cafe.recorder.recording = true
	cafe.recorder.pcm.append(42)
	cafe.working = true
	check(cafe.roleplay.begin("market", "shopper"), "A different role can be selected during a recording")
	check(cafe.epoch > previous_epoch and not cafe.working and not cafe.recorder.recording, "Switching cancels old voice, recording and assessment")
	check(cafe.ui.transcript.text == "" and cafe.ui.reply.text == "", "Switching clears stale input and transcript")
	cafe.select_stage(1)
	check(not cafe.roleplay.active() and cafe.ui.stage_controls.visible, "Cafe practice remains available after new scenes")
	check(cafe.world.near_barista() and cafe.stages.preview(), "Returning to a cafe preview restores the cafe world")
	cafe.select_stage(0)
	check(cafe.mission.state == original_order and cafe.friend.state == original_friend, "New role lessons cannot overwrite cafe progress")
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		component.free()
	cafe.free()
	await process_frame
	print("Scene switching runtime: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
