extends SceneTree

class PendingSession:
	extends RefCounted
	signal finished(value: String)
	var settled := false

	func settle(value: String) -> void:
		if settled:
			return
		settled = true
		finished.emit(value)

class SessionConnection:
	extends "res://scripts/connection.gd"
	var sessions: Array = []

	func connect_local() -> bool:
		var session := PendingSession.new()
		sessions.append(session)
		var value: String = await session.finished
		if value != "":
			token = value
		return token != ""

	func cancel() -> void:
		super.cancel()
		# Real HTTPRequest cancellation resolves its awaiting session as failed.
		for session in sessions.duplicate():
			session.settle("")

class SpeechConnection:
	extends "res://scripts/connection.gd"
	var spoken_lines: Array[String] = []

	func call_api(path: String, payload: Variant, _raw := false, _audio := false) -> Dictionary:
		if path == "/speech" and payload is Dictionary:
			spoken_lines.append(str(payload.get("text", "")))
		return {"ok": false, "data": null, "error": "Voice fixture unavailable"}

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var cafe = load("res://scripts/cafe.gd").new()
	if not cafe.has_method("connect_companion"):
		check(false, "Selecting a preview during the session handshake must reconnect the local companion")
		for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
			component.free()
		cafe.free()
		quit(1)
		return
	cafe.connection.free()
	cafe.connection = SessionConnection.new()
	cafe.voice.free()
	cafe.voice = SpeechConnection.new()
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		root.add_child(component)
	await process_frame
	await process_frame
	cafe.connect_companion()
	check(cafe.connection.sessions.size() == 1, "Initial connection starts one session handshake")
	cafe.select_stage(3)
	check(cafe.connection.sessions.size() == 2, "Switching while the handshake is pending starts a replacement session")
	check(cafe.connection.sessions[0].settled, "Switching resolves the cancelled session")
	check(cafe.ui.status.text.contains("Test preview"), "A cancelled handshake cannot overwrite the preview status")
	cafe.select_stage(4)
	check(cafe.connection.sessions.size() == 3, "Another immediate switch replaces the pending reconnect")
	check(cafe.ui.stage_picker.focus_mode == Control.FOCUS_ALL and not cafe.ui.stage_picker.disabled and not cafe.ui.stage_select.disabled, "The stage selector remains available while a session is pending")
	check(cafe.voice.spoken_lines.is_empty(), "Pending handshakes cannot speak an old selected turn")
	cafe.ui.reply.text = "A draft invitation"
	cafe.connection.sessions[2].settle("replacement-session-token")
	await process_frame
	check(cafe.connection.token == "replacement-session-token" and cafe.voice.token == "replacement-session-token", "The replacement handshake restores AI and voice credentials")
	check(cafe.ui.active_stage == 4 and cafe.ui.reply.text == "A draft invitation", "Reconnect completion cannot reset the selected turn or its draft")
	check(cafe.voice.spoken_lines == ["Could you invite me to join you and suggest a time?"], "The latest explicit selection speaks once when its replacement handshake becomes ready")
	check(cafe.ui.stage_hint.text.contains("Test preview"), "A voice failure keeps the preview saving boundary visible in the header")
	check(cafe.ui.status.text.contains("Voice unavailable"), "Speech failure keeps the existing typed and replay fallback available")
	cafe.select_stage(2)
	check(cafe.connection.sessions.size() == 3, "A ready session remains usable across later preview switches")
	check(cafe.voice.spoken_lines.size() == 2 and cafe.voice.spoken_lines[1] == "We finally have our drinks! What do you like to drink, and why?", "Explicit selection with a ready token speaks the selected opening line immediately")
	cafe.select_stage(0)
	check(cafe.voice.spoken_lines.size() == 2, "Continuing the saved journey stays silent with a ready token")
	cafe.connection.token = ""
	cafe.voice.token = ""
	cafe.select_stage(3)
	cafe.select_stage(0)
	check(cafe.connection.sessions.size() == 5, "Resuming during a pending preview handshake replaces it with a normal session")
	cafe.connection.sessions[4].settle("normal-resume-token")
	await process_frame
	check(cafe.voice.spoken_lines.size() == 2 and not cafe.ui.dialogue.visible, "A cancelled preview and a normal reconnect cannot cause unsolicited speech")
	cafe.connection.token = ""
	cafe.voice.token = ""
	cafe.select_stage(4)
	cafe.close_conversation()
	cafe.connection.sessions[5].settle("late-closed-session")
	await process_frame
	check(cafe.voice.spoken_lines.size() == 2, "Closing a preview cancels its pending request to speak")
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		component.free()
	cafe.free()
	print("Cafe practice session checks: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
