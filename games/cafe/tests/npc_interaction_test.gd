extends SceneTree

class QuietCafe extends "res://scripts/cafe.gd":
	var spoken: Array[String] = []
	func say(text: String) -> void:
		spoken.append(text)

class MemoryController extends "res://scripts/roleplay_controller.gd":
	var saved: Dictionary = {}
	func load_sessions() -> void:
		pass
	func save_session() -> void:
		saved = lesson.state.duplicate(true)
		sessions[lesson.state.scene + "/" + lesson.state.role] = saved

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var controller = load("res://scripts/roleplay_controller.gd").new()
	if not controller.has_method("interact"):
		check(false, "NPC questions must start through a nearby interaction, not scene entry")
		quit(1)
		return
	var cafe = QuietCafe.new()
	cafe.roleplay = MemoryController.new()
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		root.add_child(component)
	await process_frame
	cafe.roleplay.setup(cafe)
	cafe.connection.token = "local-test-no-network"
	cafe.roleplay.begin("shopping", "shopper")
	check(not cafe.ui.dialogue.visible and not cafe.world.in_conversation, "Entering a scene must leave the player free to walk")
	check(not cafe.world.near_npc() and cafe.spoken.is_empty(), "The entrance is not an automatic remote conversation")
	cafe.talk()
	check(not cafe.ui.dialogue.visible, "E away from an NPC cannot start a remote question")
	var turns: Array = cafe.roleplay.lesson.role_data().turns
	var first: String = turns[0].npc.id
	var second: String = turns[1].npc.id
	var third: String = turns[2].npc.id
	cafe.world.player.position = cafe.world.npc_data(third).approach
	cafe.world.walking_to_partner = true
	cafe.world.walk_points.append(Vector3.ZERO)
	cafe.talk()
	check(cafe.ui.dialogue.visible and not cafe.can_reply() and cafe.ui.record.disabled, "A future NPC greets and directs without allowing a quest answer")
	check(cafe.ui.feedback.text.begins_with("Press Esc"), "A greeting must explain how to keep exploring instead of requesting a disabled reply")
	check(not cafe.world.walking_to_partner and cafe.world.walk_points.is_empty(), "Stopping to greet an actor cancels the previous walking helper")
	cafe.submit_text("Let me skip ahead.")
	check(cafe.roleplay.lesson.turn() == 0 and not cafe.working, "A side greeting cannot submit an answer or award progress")
	cafe.close_conversation()
	check(cafe.world.focused_npc_id == "", "Closing clears the dialogue camera's actor focus")
	cafe.world.player.position = cafe.world.npc_data(first).approach
	cafe.talk()
	check(cafe.can_reply() and cafe.ui.reply.editable, "The current nearby NPC enables speaking and typing")
	check(cafe.ui.npc_line.text == turns[0].prompt, "The nearby actor asks its own authored question")
	var retry := {"accepted": false, "correction": turns[0].sample, "feedback": "Tell me what clothes you want.", "acknowledgement": "Let's try that line."}
	cafe.roleplay.finish(cafe.roleplay.lesson.respond("banana", retry))
	check(cafe.ui.dialogue.visible and cafe.can_reply() and cafe.roleplay.target_id() == first, "A correction stays with the same actor")
	check(cafe.roleplay.saved.correction == turns[0].sample, "Pending correction is saved before continuing exploration")
	cafe.roleplay.finish(cafe.roleplay.lesson.repeat_correction(turns[0].sample))
	check(cafe.roleplay.lesson.turn() == 1 and not cafe.can_reply(), "A passed goal disables further replies to the old NPC")
	check(not cafe.ui.npc_line.text.contains("Let's try that line."), "A successful correction must not keep asking the learner to retry")
	check(not cafe.ui.npc_line.text.contains(turns[1].prompt), "The old actor must not ask the next actor's question")
	cafe.voice_finished()
	check(not cafe.ui.dialogue.visible and not cafe.world.in_conversation, "A finished acknowledgement returns to free movement")
	check(cafe.roleplay.target_id() == second, "The next stop highlights a different NPC")
	cafe.talk()
	check(not cafe.can_reply() and cafe.roleplay.lesson.turn() == 1, "Revisiting a completed actor does not start a remote next task")
	cafe.close_conversation()
	cafe.world.player.position = cafe.world.npc_data(second).approach
	cafe.talk()
	check(cafe.can_reply() and cafe.ui.npc_line.text == turns[1].prompt, "The next question requires reaching the next actor")
	var accepted := {"accepted": true, "correction": "", "feedback": "Clear answer.", "acknowledgement": "Sounds good!"}
	cafe.roleplay.finish(cafe.roleplay.lesson.respond(turns[1].sample, accepted))
	cafe.voice_failed()
	check(not cafe.ui.dialogue.visible and not cafe.world.in_conversation, "Failed outro audio must not trap the player")
	cafe.world.player.position = cafe.world.npc_data(third).approach
	cafe.talk()
	cafe.roleplay.finish(cafe.roleplay.lesson.respond(turns[2].sample, accepted))
	cafe.voice_finished()
	check(cafe.roleplay.lesson.complete() and not cafe.ui.dialogue.visible, "Finishing all NPC goals returns to exploration")
	check(cafe.roleplay.saved.answers.size() == 3 and cafe.roleplay.saved.answers[0].assisted, "NPC traversal preserves independent and assisted evidence")
	cafe.talk()
	check(not cafe.can_reply() and not cafe.ui.feedback.text.contains("task marker"), "Completed greetings cannot point to a nonexistent next task")
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		component.free()
	cafe.free()
	await process_frame
	print("NPC interaction lifecycle: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
