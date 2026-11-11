extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if not OS.get_user_data_dir().get_file().begins_with("ContextSpeak-Test-"):
		check(false, "Run runtime tests through world:test so real progress stays isolated.")
		quit(1)
		return
	var cafe = load("res://scripts/cafe.gd").new()
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		root.add_child(component)
	await process_frame
	cafe.roleplay.setup(cafe)
	var chooser = cafe.roleplay.menu
	if chooser.get("difficulty") == null:
		check(false, "Scene selection needs a usable Everyday/Real-life challenge choice.")
		_cleanup(cafe)
		quit(1)
		return
	cafe.connection.token = "offline-runtime"
	var order: Dictionary = cafe.mission.state.duplicate(true)
	var friend: Dictionary = cafe.friend.state.duplicate(true)
	chooser.show_menu()
	check(chooser.difficulty.item_count == 2 and not chooser.difficulty.disabled, "The original cafe can also start a harder conversation.")
	chooser.difficulty.select(1)
	chooser.enter()
	check(cafe.roleplay.active() and cafe.roleplay.lesson.state.scene == "cafe", "Selecting the cafe challenge enters the real forest cafe.")
	check(cafe.roleplay.lesson.state.difficulty == "challenge", "The selected difficulty reaches the active lesson.")
	check(cafe.world.name == "CafeChallengeWorld", "The challenge reuses the forest cafe rather than a generic role map.")
	cafe.world.player.position = Vector3(0, 0, -0.7)
	cafe.talk()
	check(cafe.ui.dialogue.visible and cafe.roleplay.speaker_name() == "Maya", "Approaching the original counter starts Maya's opening.")
	var accepted := {"accepted": true, "correction": "", "feedback": "Clear and practical.", "acknowledgement": "Sure."}
	var opening: String = cafe.roleplay.lesson.prompt()
	cafe.roleplay.finish(cafe.roleplay.lesson.respond("Two takeaway drinks, please, and oat milk for mine.", accepted))
	check(cafe.roleplay.lesson.exchange() == 1 and cafe.ui.dialogue.visible, "An opening keeps the dialogue at the same counter.")
	check(cafe.ui.npc_line.text != opening and cafe.ui.progress.text.contains("Follow-up"), "The distinct follow-up and its phase are visible.")
	var before: Dictionary = cafe.roleplay.lesson.state.duplicate(true)
	var checkpoint_epoch: int = cafe.epoch
	for request in ["Sorry, could you say that again, please?", "Please repeat the question.", "Please say that again."]:
		cafe.roleplay.submit(request)
		check(cafe.roleplay.lesson.state == before and not cafe.working and cafe.epoch == checkpoint_epoch, "Repeat replays locally without assessment, history or progress changes.")
	cafe.roleplay.lesson.state.correction = cafe.roleplay.lesson.step().sample
	var pending: Dictionary = cafe.roleplay.lesson.state.duplicate(true)
	for request in ["Please repeat the question.", "Please say that again."]:
		cafe.roleplay.submit(request)
		check(cafe.roleplay.lesson.state == pending and not cafe.working, "Repeat during a pending correction must neither grade nor replace it.")
	cafe.roleplay.lesson.state = before
	cafe.roleplay.save_session()
	check(cafe.roleplay.sessions.has("cafe/customer/challenge"), "Challenge partial progress uses its separate save key.")
	cafe.roleplay.begin("shopping", "shopper")
	check(cafe.roleplay.lesson.state.difficulty == "everyday", "Existing two-argument scene entry still selects Everyday.")
	cafe.roleplay.begin("cafe", "customer", false, "challenge")
	check(cafe.roleplay.lesson.exchange() == 1, "Returning restores the unfinished follow-up.")
	cafe.roleplay.begin("cafe", "customer", true, "challenge")
	check(cafe.roleplay.lesson.exchange() == 0 and cafe.roleplay.lesson.turn() == 0, "Restart keeps the selected difficulty and resets only that run.")
	cafe.select_stage(0)
	check(not cafe.roleplay.active() and cafe.mission.state == order and cafe.friend.state == friend, "The original cafe journey survives challenge entry and restart.")
	_cleanup(cafe)
	await process_frame
	print("Real-life challenge runtime: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)

func _cleanup(cafe: Node3D) -> void:
	for component in [cafe.world, cafe.ui, cafe.connection, cafe.voice, cafe.recorder, cafe.speaker]:
		component.free()
	cafe.free()
