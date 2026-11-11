extends Node3D

const Mission = preload("mission.gd")
const Friend = preload("friend.gd")
const World = preload("world.gd")
const Cafe2D = preload("world_2d.gd")
const Interface = preload("interface.gd")
const Connection = preload("connection.gd")
const Recorder = preload("recorder.gd")
const Stages = preload("stages.gd")
const Roleplay = preload("roleplay_controller.gd")
const WebViewport = preload("web_viewport.gd")
const SAVE := "user://contextspeak-cafe-v1.json"

var stages = Stages.new()
var roleplay = Roleplay.new()
var mission = Mission.new()
var friend = Friend.new()
var world = World.new()
var ui = Interface.new()
var connection = Connection.new()
var voice = Connection.new()
var recorder = Recorder.new()
var speaker := AudioStreamPlayer.new()
var epoch := 0
var voice_epoch := 0
var working := false
var near_counter := false
var web_voice: JavaScriptObject
var voice_callbacks: Array[JavaScriptObject] = []
var web_viewport = WebViewport.new()
var is_2d := false

func _init() -> void:
	is_2d = OS.get_cmdline_user_args().has("--2d")
	if OS.has_feature("web"):
		is_2d = bool(JavaScriptBridge.get_interface("window").contextSpeak2D)
	if is_2d:
		world.free()
		world = make_world()

func make_world() -> Node3D:
	return Cafe2D.new() if is_2d else World.new()

func progress_file() -> String:
	return "user://contextspeak-cafe-2d-v1.json" if is_2d else SAVE

func _ready() -> void:
	web_viewport.setup(get_tree().root)
	add_child(world)
	if OS.has_feature("web"):
		var window := JavaScriptBridge.get_interface("window")
		world.reduce_motion = bool(window.matchMedia("(prefers-reduced-motion: reduce)").matches)
		web_voice = window.contextSpeakVoice
		voice_callbacks = [JavaScriptBridge.create_callback(web_voice_started), JavaScriptBridge.create_callback(web_voice_ended), JavaScriptBridge.create_callback(web_voice_failed)]
	add_child(ui)
	if is_2d:
		ui.location.text = "2D CAFÉ  /  A drink with a friend"
	add_child(connection)
	add_child(voice)
	add_child(recorder)
	add_child(speaker)
	roleplay.setup(self)
	speaker.finished.connect(voice_finished)
	world.seated_at_table.connect(arrive_at_table)
	ui.talk_requested.connect(talk)
	ui.walk_requested.connect(walk_action)
	ui.restart_requested.connect(restart)
	ui.stage_requested.connect(select_stage)
	ui.close_requested.connect(close_conversation)
	ui.record_requested.connect(record_reply)
	ui.replay_requested.connect(func(): say(current_line()))
	ui.reply_submitted.connect(submit_text)
	recorder.recorded.connect(transcribe)
	recorder.failed.connect(recording_failed)
	recorder.activity.connect(ui.mic_activity)
	load_progress()
	ui.update_stage(0)
	restore_world_state()
	refresh()
	await connect_companion()
	if is_2d:
		ui.announce("2D café · " + ui.progress.text + " · " + ui.goal.text)

func connect_companion(speak_selected_preview := false) -> void:
	var current := epoch
	var connected: bool = await connection.connect_local()
	if current != epoch:
		return
	voice.token = connection.token
	if roleplay.active():
		ui.status.text = "Free local AI · Role progress on this device" if connected else "Local companion unavailable. Reopen the scene after starting pnpm world:dev."
	elif stages.preview():
		ui.status.text = "Test preview · Changes are not saved."
		if not connected:
			ui.status.text += " Local companion unavailable."
	else:
		ui.status.text = "Free local AI · Guest save on this device" if connected else "Local companion unavailable. Simple typed replies still work."
	if connected and speak_selected_preview and (stages.preview() or roleplay.active()) and ui.dialogue.visible:
		say(current_line())

func refresh() -> void:
	if roleplay.active():
		roleplay.refresh()
		return
	ui.update(mission.summary())
	ui.update_journey(mission.state.complete, friend.state)
	if friend.state.phase in ["seated", "complete"]:
		ui.feedback.text = friend.feedback if friend.feedback != "" else "Tell Alex your answer. Local AI gives feedback on your meaning and expression."

func current_line() -> String:
	if roleplay.active():
		return roleplay.current_line()
	return friend.line() if friend.state.phase in ["seated", "complete"] else mission.prompt()

func can_reply() -> bool:
	if roleplay.active():
		return roleplay.can_reply()
	return friend.state.phase == "seated" or not mission.state.complete

func _physics_process(delta: float) -> void:
	world.move_player(delta, ui.dialogue.visible or roleplay.menu_open())
	if roleplay.active():
		roleplay.tick()
		return
	var near: bool = world.near_barista()
	ui.talk.disabled = not near if not mission.state.complete else friend.state.phase == "walking"
	if near and not near_counter and not ui.dialogue.visible and not mission.state.complete:
		ui.announce("You reached Maya. Press E to talk.")
	near_counter = near

func _unhandled_key_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if roleplay.menu_open():
			if event.keycode == KEY_ESCAPE:
				roleplay.menu.hide_menu()
			return
		if event.keycode == KEY_E and not ui.dialogue.visible:
			talk()
		elif event.keycode == KEY_SPACE and not ui.dialogue.visible:
			walk_action()
		elif event.keycode == KEY_ESCAPE:
			close_conversation()

func walk_action() -> void:
	if roleplay.active():
		if roleplay.target_id() != "":
			world.walk_to_npc(roleplay.target_id())
		return
	if mission.state.complete:
		if friend.state.phase in ["seated", "complete"]:
			talk()
		else:
			join_friend()
	else:
		world.walking_to_counter = true

func talk() -> void:
	if roleplay.active():
		roleplay.interact()
		return
	if mission.state.complete and friend.state.phase == "counter":
		join_friend()
		return
	if world.near_barista() or world.seated:
		world.walking_to_counter = false
		world.in_conversation = true
		ui.show_dialogue(current_line())
		refresh()
		say(current_line())

func stop_voice() -> void:
	voice_epoch += 1
	if web_voice:
		web_voice.stop()
	speaker.stop()
	voice.cancel()

func say(text: String) -> void:
	stop_voice()
	if connection.token == "" or recorder.recording:
		return
	ui.status.text = "Preparing the local voice…"
	if web_voice:
		web_voice.speak(connection.token, text, voice_callbacks[0], voice_callbacks[1], voice_callbacks[2])
		return
	var current := voice_epoch
	var response: Dictionary = await voice.call_api("/speech", {"text": text}, false, true)
	if current != voice_epoch or not ui.dialogue.visible or recorder.recording:
		return
	if response.ok:
		var sound := AudioStreamWAV.load_from_buffer(response.data)
		if sound:
			speaker.stream = sound
			speaker.play()
			voice_started()
			return
	voice_failed()

func voice_started() -> void:
	ui.status.text = "Listen to " + roleplay.speaker_name() + "…" if roleplay.active() else "Listen to Alex…" if world.seated else "Listen to Maya…"
	ui.announce(ui.npc_line.text + " · " + ui.progress.text + " · " + ui.status.text + " · " + ui.feedback.text)

func voice_finished() -> void:
	if roleplay.active():
		if roleplay.finish_voice():
			return
		ui.status.text = "Your turn · local AI" if roleplay.can_reply() else "Press Esc to keep exploring."
		refresh()
		return
	ui.status.text = "Conversation complete · local AI" if friend.state.phase == "complete" else "Your turn · local AI"
	refresh()
	ui.announce(ui.npc_line.text + " · " + ui.progress.text + " · " + ui.status.text + " · " + ui.feedback.text)
	if mission.state.complete and friend.state.phase == "counter":
		join_friend.call_deferred()

func voice_failed() -> void:
	if roleplay.active() and roleplay.finish_voice():
		return
	ui.status.text = "Voice unavailable. Use Hear to retry; your task is kept."
	ui.announce(ui.npc_line.text + " · " + ui.progress.text + " · " + ui.status.text + " · " + ui.feedback.text)

func web_voice_started(_args: Array) -> void:
	voice_started()

func web_voice_ended(_args: Array) -> void:
	voice_finished()

func web_voice_failed(_args: Array) -> void:
	voice_failed()

func join_friend() -> void:
	if not mission.state.complete or friend.state.phase not in ["counter", "walking"]:
		return
	close_conversation()
	if friend.state.phase == "counter":
		friend.begin_walk(true)
	world.join_friend()
	refresh()
	ui.announce("Order complete. Walking to Alex's table…")
	save_progress()

func arrive_at_table() -> void:
	if not friend.sit():
		return
	world.in_conversation = true
	refresh()
	ui.show_dialogue(friend.line())
	ui.transcript.text = ""
	save_progress()
	say(friend.line())

func record_reply() -> void:
	if recorder.recording:
		recorder.stop()
		return
	if working or not can_reply():
		return
	stop_voice()
	if connection.token == "":
		recording_failed("Start pnpm world:dev, then reopen the game for local speech. You can type your answer now.")
		return
	recorder.start()
	ui.recording(true)
	ui.feedback.text = "Listening… stop when finished. Maximum 20 seconds."
	ui.announce("Preparing microphone. Wait for the recording indicator before speaking.")

func recording_failed(message: String) -> void:
	ui.recording(false)
	ui.feedback.text = message
	ui.announce(message)
	ui.reply.grab_focus()

func transcribe(audio: PackedByteArray) -> void:
	ui.recording(false)
	working = true
	ui.busy(true)
	ui.feedback.text = "Recognizing your reply on this computer…"
	ui.announce(ui.feedback.text)
	var current := epoch
	var response: Dictionary = await connection.call_api("/transcribe", audio, true)
	if current != epoch:
		return
	working = false
	ui.busy(false)
	ui.reply.grab_focus()
	if response.ok and response.data is Dictionary:
		submit_text(str(response.data.get("text", "")))
	else:
		recording_failed(response.error)

func submit_text(text: String) -> void:
	text = text.strip_edges()
	if working or text == "" or not can_reply() or not ui.dialogue.visible:
		return
	stop_voice()
	ui.transcript.text = "You said: " + text
	ui.reply.text = ""
	if roleplay.active():
		roleplay.submit(text)
		return
	if friend.state.phase == "seated":
		check_friend_reply(text)
		return
	var intent: Dictionary = mission.parse(text)
	if intent.action == "unknown" and connection.token != "":
		working = true
		ui.busy(true)
		var current := epoch
		var response: Dictionary = await connection.call_api("/interpret", {"text": text, "stage": mission.state.stage})
		if current != epoch:
			return
		working = false
		ui.busy(false)
		ui.reply.grab_focus()
		if not response.ok or not response.data is Dictionary:
			recording_failed(response.error)
			ui.reply.text = text
			return
		intent = response.data
	var outcome: Dictionary = mission.apply(intent)
	ui.npc_line.text = outcome.reply
	ui.feedback.text = "Your order is kept. You can ask the price or change your complete order."
	refresh()
	ui.layout.call_deferred()
	save_progress()
	if outcome.complete:
		world.show_reward()
	else:
		ui.reply.grab_focus()
	say(outcome.reply)

func check_friend_reply(text: String) -> void:
	working = true
	ui.busy(true)
	ui.feedback.text = "Alex is thinking about your reply…"
	ui.announce(ui.feedback.text)
	var current := epoch
	var response: Dictionary = await connection.call_api("/conversation", {"text": text, "turn": friend.state.answers.size(), "order": mission.order_text(), "history": friend.state.answers})
	if current != epoch:
		return
	working = false
	ui.busy(false)
	ui.reply.grab_focus()
	if not response.ok or not response.data is Dictionary:
		recording_failed(response.error)
		ui.reply.text = text
		return
	friend.respond(text, response.data)
	ui.npc_line.text = friend.line()
	refresh()
	ui.feedback.text = friend.feedback
	if friend.state.phase != "complete":
		ui.reply.grab_focus()
	ui.layout.call_deferred()
	save_progress()
	say(friend.line())

func close_conversation() -> void:
	epoch += 1
	working = false
	recorder.cancel()
	connection.cancel()
	stop_voice()
	ui.recording(false)
	ui.busy(false)
	ui.dialogue.hide()
	world.in_conversation = false
	roleplay.conversation_closed()

func clear_stage() -> void:
	mission = Mission.new()
	friend = Friend.new()
	near_counter = false
	world.reset_journey()
	world.player.velocity = Vector3.ZERO
	# Remove immediately: show_reward checks the child count in this frame.
	for cup in world.cups.get_children():
		world.cups.remove_child(cup)
		cup.queue_free()
	ui.reply.text = ""
	ui.transcript.text = ""
	ui.npc_line.text = ""
	ui.feedback.text = "Ask for a price or say ‘Please repeat’ whenever you need."

func restore_world_state() -> void:
	if not mission.state.complete:
		return
	world.show_reward()
	if friend.state.phase in ["seated", "complete"]:
		world.seat_with_friend()
	elif friend.state.phase == "walking":
		world.join_friend()

func select_stage(selection: int) -> void:
	if selection < 0 or selection >= Stages.LABELS.size():
		return
	roleplay.leave()
	# Resolve cancelled work against the previous epoch before replacing state.
	close_conversation()
	stages.select(selection)
	clear_stage()
	if stages.preview():
		restore_progress(stages.fixture())
	else:
		load_progress()
	ui.update_stage(selection)
	restore_world_state()
	if selection == 1:
		world.player.position = Vector3(0, 0, -0.7)
		world.hero.rotation.y = PI
		world.hero.play("idle")
	ui.status.text = "Test preview · Changes are not saved." if stages.preview() else "Saved journey · Guest save on this device"
	refresh()
	if stages.preview():
		world.in_conversation = true
		ui.show_dialogue(current_line())
		ui.announce(current_line() + " · " + ui.progress.text + " · " + ui.feedback.text)
	else:
		ui.announce("Saved journey restored. " + ui.progress.text)
	if connection.token == "":
		connect_companion(stages.preview())
	elif stages.preview():
		say(current_line())

func restart() -> void:
	if roleplay.active():
		roleplay.begin(roleplay.lesson.state.scene, roleplay.lesson.state.role, true, roleplay.lesson.state.difficulty)
		roleplay.save_session()
		return
	if stages.preview():
		select_stage(stages.selected)
		return
	close_conversation()
	clear_stage()
	refresh()
	save_progress()

func restore_progress(value: Dictionary) -> void:
	if value.get("version") == 2 and value.get("order") is Dictionary:
		mission.restore(value.order)
		if value.get("friend") is Dictionary:
			friend.restore(value.friend, mission.state.complete)
	else:
		# Previous order-only saves unlock the new table conversation.
		mission.restore(value)

func load_progress() -> void:
	var saved: Variant
	if OS.has_feature("web"):
		saved = JavaScriptBridge.get_interface("window").contextSpeakLoadGuest()
	else:
		if not FileAccess.file_exists(progress_file()):
			return
		var file := FileAccess.open(progress_file(), FileAccess.READ)
		if file and file.get_length() < 64000:
			saved = file.get_as_text()
	if saved is String and saved.length() < 64000:
		var value: Variant = JSON.parse_string(saved)
		if value is Dictionary:
			restore_progress(value)

func save_progress() -> void:
	if stages.preview():
		return
	var saved := JSON.stringify({"version": 2, "order": mission.state, "friend": friend.state})
	if OS.has_feature("web"):
		if not JavaScriptBridge.get_interface("window").contextSpeakSaveGuest(saved):
			ui.status.text = "Browser storage is unavailable. Keep this game open to retain progress."
		return
	var file := FileAccess.open(progress_file() + ".tmp", FileAccess.WRITE)
	if file == null:
		ui.status.text = "Progress could not be saved. Keep this game open."
		return
	file.store_string(saved)
	file.close()
	if DirAccess.rename_absolute(progress_file() + ".tmp", progress_file()) != OK:
		ui.status.text = "Progress could not be saved. Keep this game open."

func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and is_instance_valid(ui) and ui.is_inside_tree():
		close_conversation()

func _exit_tree() -> void:
	epoch += 1
	web_viewport.dispose()
	recorder.cancel()
	connection.cancel()
	stop_voice()
