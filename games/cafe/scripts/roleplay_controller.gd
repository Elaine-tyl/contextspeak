extends RefCounted

const Lesson = preload("roleplay.gd")
const SceneMenu = preload("scene_menu.gd")
const SceneWorld = preload("roleplay_world.gd")
const CafeChallengeWorld = preload("cafe_roleplay_world.gd")
const SAVE := "user://contextspeak-scenes-v1.json"

var host: Node3D
var lesson = Lesson.new()
var menu: RefCounted
var sessions: Dictionary = {}
var conversation_id := ""
var blocked_line := ""
var outro := ""
var last_nearby := "--"
var last_zone := "--"

func setup(game: Node3D) -> void:
	host = game
	load_sessions()
	menu = SceneMenu.new(host.ui, [] if host.is_2d else lesson.catalog)
	menu.opened.connect(host.close_conversation)
	menu.chosen.connect(select_scene)

func active() -> bool:
	return lesson.state.scene != ""

func menu_open() -> bool:
	return menu != null and menu.panel.visible

func select_scene(scene: String, role: String, difficulty := "everyday") -> void:
	if scene == "cafe" and difficulty == "everyday":
		host.select_stage(0)
	else:
		begin(scene, role, false, difficulty)

func replace_world(next: Node3D) -> void:
	var previous: Node3D = host.world
	var parent := previous.get_parent()
	next.reduce_motion = previous.reduce_motion
	parent.remove_child(previous)
	previous.queue_free()
	host.world = next
	parent.add_child(next)

func begin(scene: String, role: String, restart := false, difficulty := "everyday") -> bool:
	if host.is_2d and scene != "cafe":
		return false
	var next = Lesson.new()
	if not next.start(scene, role, difficulty):
		return false
	host.close_conversation()
	lesson = next
	host.stages.select(0)
	var key: String = lesson.session_key()
	if not restart and sessions.get(key) is Dictionary:
		lesson.restore(sessions[key])
	var next_world: Node3D
	if host.is_2d:
		next_world = host.make_world()
	else:
		next_world = CafeChallengeWorld.new() if scene == "cafe" else SceneWorld.new()
	replace_world(next_world)
	var identity: Dictionary = lesson.role_data()
	host.world.configure(scene, identity.label, identity.npc, role)
	for index in lesson.turn():
		host.world.set_target_npc(identity.turns[index].npc.id)
		host.world.apply_action(identity.turns[index].action, "step_" + str(index), action_caption(index))
	host.world.set_target_npc(target_id())
	host.ui.active_stage = 0
	host.ui.stage_controls.hide()
	host.ui.transcript.text = ""
	host.ui.reply.text = ""
	host.ui.mission_title.text = lesson.scene_data(scene).title
	host.ui.mission_description.text = "Your role: " + identity.label
	if difficulty == "challenge":
		host.ui.mission_description.text += " · Real-life challenge"
	host.ui.restart_button.text = "Restart this role"
	host.ui.status.text = "Explore with WASD / arrows. Approach an NPC and press E."
	refresh()
	tick()
	if host.connection.token == "":
		host.connect_companion()
	return true

func target_id() -> String:
	return "" if lesson.complete() else str(lesson.step().npc.id)

func speaker() -> Dictionary:
	return host.world.npc_data(conversation_id if conversation_id != "" else target_id())

func speaker_name() -> String:
	return str(speaker().get("name", "your neighbour"))

func current_line() -> String:
	if outro != "":
		return outro
	if blocked_line != "":
		return blocked_line
	# Previous actors' acknowledgements must not become the next NPC's words.
	return lesson.line() if lesson.pending() != "" or lesson.exchange() == 1 else lesson.prompt()

func can_reply() -> bool:
	return (
		active() and not lesson.complete()
		and conversation_id != "" and conversation_id == target_id()
		and outro == "" and blocked_line == ""
		and host.world.nearest_npc_id() == conversation_id
	)

func exploration_line() -> String:
	if lesson.complete():
		return "Mission complete. Keep exploring or try another role."
	var next: Dictionary = host.world.npc_data(target_id())
	return "Find %s at %s. Press E when nearby." % [next.get("name", "the next NPC"), next.get("zone_label", "the next area")]

func tick() -> void:
	var nearby: String = host.world.nearest_npc_id()
	var zone: String = host.world.current_zone()
	host.ui.talk.disabled = nearby == ""
	host.ui.talk.text = "Move closer to an NPC [E]" if nearby == "" else "Talk to %s [E]" % host.world.npc_data(nearby).name
	host.ui.location.text = lesson.scene_data(lesson.state.scene).title.to_upper() + "  /  " + zone
	if not host.ui.dialogue.visible and not menu_open() and (nearby != last_nearby or zone != last_zone):
		var cue := "Explore · " + zone
		if nearby != "":
			cue += " · Press E to talk to " + str(host.world.npc_data(nearby).name)
		host.ui.status.text = cue
		host.ui.announce(cue + " · " + exploration_line() + " · " + host.ui.progress.text + " · " + host.ui.receipt.text)
	last_nearby = nearby
	last_zone = zone

func interact() -> bool:
	if host.ui.dialogue.visible or menu_open():
		return false
	var nearby: String = host.world.nearest_npc_id()
	if nearby == "":
		host.ui.announce("Move closer to an NPC, then press E. " + exploration_line())
		return false
	conversation_id = nearby
	blocked_line = ""
	outro = ""
	if nearby != target_id():
		blocked_line = "Hey, good to see you! " + ("Enjoy exploring. You're all set!" if lesson.complete() else exploration_line())
	host.world.focus_npc(nearby)
	host.world.in_conversation = true
	host.ui.reply.text = ""
	host.ui.transcript.text = ""
	refresh()
	host.ui.show_dialogue(current_line())
	host.ui.announce(current_line() + " · " + host.ui.progress.text + " · " + host.ui.feedback.text)
	host.say(current_line())
	return true

func conversation_closed() -> void:
	conversation_id = ""
	blocked_line = ""
	outro = ""
	last_nearby = "--"
	last_zone = "--"
	if active():
		host.world.focus_npc("")
		host.ui.reply_allowed = false

func finish_voice() -> bool:
	if outro == "":
		return false
	host.close_conversation()
	refresh()
	tick()
	return true

func leave() -> void:
	if not active():
		return
	host.close_conversation()
	lesson = Lesson.new()
	replace_world(host.make_world())
	host.world.seated_at_table.connect(host.arrive_at_table)
	host.ui.stage_controls.show()
	host.ui.location.text = ("2D CAFÉ" if host.is_2d else "AFTER CLASS CAFE") + "  /  A drink with a friend"
	host.ui.mission_title.text = "A drink with a friend"
	host.ui.mission_description.text = "Order two drinks. Stay within $12."
	host.ui.completed = false
	host.ui.reply_allowed = true
	host.ui.walk.disabled = false

func refresh() -> void:
	var ui: CanvasLayer = host.ui
	ui.at_table = true
	ui.menu.hide()
	ui.completed = lesson.complete()
	ui.reply_allowed = can_reply()
	ui.progress.text = "Conversation goals: %d/3" % lesson.turn()
	if lesson.complete():
		ui.progress.text += " · Mission complete"
	elif lesson.state.difficulty == "challenge":
		ui.progress.text += " · " + ("Follow-up" if lesson.exchange() == 1 else "Opening")
	ui.goal.text = exploration_line()
	if not lesson.complete():
		ui.goal.text += "\n" + ("Repeat your correction there." if lesson.pending() != "" else lesson.step().goal)
	var assisted := 0
	for answer in lesson.state.answers:
		if answer.assisted:
			assisted += 1
	ui.receipt.text = "%d independent · %d assisted" % [lesson.turn() - assisted, assisted]
	ui.npc_title.text = speaker_name() + "  ·  " + str(speaker().get("role", "Neighbour"))
	ui.replay.text = "Hear " + speaker_name()
	var next: Dictionary = host.world.npc_data(target_id())
	ui.walk.text = "Explore freely" if lesson.complete() else "Walk to %s [Space]" % next.get("name", "next NPC")
	ui.walk.disabled = lesson.complete()
	ui.feedback.text = lesson.state.feedback if lesson.state.feedback != "" else "Speak or type your answer. Stop recording to check it automatically."
	if lesson.state.difficulty == "challenge" and lesson.state.feedback == "":
		ui.feedback.text = lesson.role_data().setting + "\n" + ui.feedback.text
	if blocked_line != "":
		ui.feedback.text = "Press Esc or × to keep exploring."
		if not lesson.complete():
			ui.feedback.text += " Follow the task marker to your next NPC."
	if conversation_id != "":
		ui.npc_line.text = current_line()
	ui.busy(host.working)
	ui.layout.call_deferred()
	ui.announce((current_line() if host.ui.dialogue.visible else exploration_line()) + " · " + ui.progress.text + " · " + ui.receipt.text + " · " + ui.status.text + " · " + ui.feedback.text)

func submit(text: String) -> void:
	if not can_reply():
		return
	if lesson.requests_repeat(text):
		host.ui.feedback.text = "Of course. Listen again, then answer in your own words."
		host.say(current_line())
		return
	if lesson.pending() != "":
		finish(lesson.repeat_correction(text))
		return
	host.working = true
	host.ui.busy(true)
	host.ui.feedback.text = "Checking your answer on this computer…"
	host.ui.announce(host.ui.feedback.text)
	var current: int = host.epoch
	var payload := {"scene": lesson.state.scene, "role": lesson.state.role, "turn": lesson.turn(), "text": text, "history": lesson.history()}
	if lesson.state.difficulty == "challenge":
		payload.difficulty = "challenge"
		payload.exchange = lesson.exchange()
	var response: Dictionary = await host.connection.call_api("/roleplay", payload)
	if current != host.epoch:
		return
	host.working = false
	host.ui.busy(false)
	if not response.ok or not response.data is Dictionary:
		host.recording_failed(response.error)
		host.ui.reply.text = text
		return
	finish(lesson.respond(text, response.data))

func finish(action: String) -> void:
	if action != "":
		host.world.apply_action(action, "step_" + str(lesson.turn() - 1), action_caption(lesson.turn() - 1))
		host.world.set_target_npc(target_id())
		outro = lesson.state.acknowledgement + " " + ("You're all set. Have fun exploring!" if lesson.complete() else exploration_line())
	refresh()
	if can_reply():
		host.ui.reply.grab_focus()
	save_session()
	host.say(current_line())
	if outro != "" and host.connection.token == "":
		host.close_conversation.call_deferred()

func action_caption(index: int) -> String:
	# Friends can choose their own clothes/rides; restore the same choice later.
	if lesson.state.scene in ["shopping", "amusement_park"]:
		return lesson.state.answers[index].text
	return lesson.role_data().turns[index].sample

func progress_file() -> String:
	return "user://contextspeak-scenes-2d-v1.json" if host.is_2d else SAVE

func load_sessions() -> void:
	var saved: Variant
	if OS.has_feature("web"):
		saved = JavaScriptBridge.get_interface("window").contextSpeakLoadScene()
	elif FileAccess.file_exists(progress_file()):
		var file := FileAccess.open(progress_file(), FileAccess.READ)
		if file and file.get_length() < 64000:
			saved = file.get_as_text()
	if saved is String and saved.length() < 64000:
		var value: Variant = JSON.parse_string(saved)
		if value is Dictionary and value.get("version") == 1 and value.get("sessions") is Dictionary:
			for key in value.sessions:
				var restored = Lesson.new()
				if value.sessions[key] is Dictionary and restored.restore(value.sessions[key]) and key == restored.session_key():
					sessions[key] = restored.state

func save_session() -> void:
	sessions[lesson.session_key()] = lesson.state.duplicate(true)
	var saved := JSON.stringify({"version": 1, "sessions": sessions})
	if OS.has_feature("web"):
		# Use the same guarded bridge as cafe saves, with a separate explicit key.
		if not JavaScriptBridge.get_interface("window").contextSpeakSaveScene(saved):
			host.ui.status.text = "Browser storage is unavailable. Keep this game open to retain progress."
		return
	var file := FileAccess.open(progress_file() + ".tmp", FileAccess.WRITE)
	if file == null:
		host.ui.status.text = "Progress could not be saved. Keep this game open."
		return
	file.store_string(saved)
	file.close()
	if DirAccess.rename_absolute(progress_file() + ".tmp", progress_file()) != OK:
		host.ui.status.text = "Progress could not be saved. Keep this game open."
