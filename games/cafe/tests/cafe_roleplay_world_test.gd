extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if not ResourceLoader.exists("res://scripts/cafe_roleplay_world.gd"):
		check(false, "A cafe challenge must reuse the existing forest room and actors.")
		quit(1)
		return
	var world = load("res://scripts/cafe_roleplay_world.gd").new()
	root.add_child(world)
	world.configure("cafe", "Meeting a friend", "Maya", "customer")
	await physics_frame
	check(world.find_child("ForestGarden", true, false) != null, "The conversation world retains the forest conservatory.")
	check(world.nearest_npc_id() == "", "The entrance cannot start a distant NPC conversation.")
	world.player.position = Vector3(3.45, 0, -0.3)
	world.focus_npc("friend")
	check(not world.seated, "Greeting Alex before the counter goals must not trap the player in a seat.")
	var start: Vector3 = world.player.position
	var key := InputEventKey.new()
	key.physical_keycode = KEY_S
	key.pressed = true
	Input.parse_input_event(key)
	for frame in 6:
		world.move_player(1.0 / 60, false)
		await physics_frame
	key.pressed = false
	Input.parse_input_event(key)
	check(world.player.position.z > start.z + 0.15, "The learner can walk away from an early Alex greeting and return to Maya.")
	world.reset_journey()
	world.set_target_npc("counter")
	world.walk_to_npc()
	for frame in 160:
		world.move_player(1.0 / 60, false)
		await physics_frame
		if not world.walking_to_counter:
			break
	check(world.nearest_npc_id() == "counter", "The original helper route reaches Maya's counter.")
	world.focus_npc("counter")
	check(world.npc_data("counter").name == "Maya" and not world.seated, "Counter focus does not seat or replace either actor.")
	world.apply_action("pay", "step_1", "Drinks collected")
	world.apply_action("pay", "step_1", "Drinks collected")
	check(world.cups.get_child_count() == 2, "Finishing pickup shows exactly two drinks, including restored/replayed actions.")
	world.set_target_npc("friend")
	world.walk_to_npc()
	for frame in 160:
		world.move_player(1.0 / 60, false)
		await physics_frame
		if world.seated:
			break
	check(world.seated and world.nearest_npc_id() == "friend", "The helper carries the drinks to the existing friend seat.")
	world.focus_npc("friend")
	check(world.npc_data("friend").name == "Alex", "The seated follow-ups belong to Alex.")
	check(world.player.position == Vector3(3.45, 0, -0.3), "Challenge seating preserves the original chair alignment.")
	world.free()
	print("Cafe challenge world: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
