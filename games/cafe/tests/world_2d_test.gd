extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if not ResourceLoader.exists("res://scripts/world_2d.gd"):
		check(false, "The café needs a distinct playable 2D renderer.")
		quit(1)
		return
	var game = load("res://scripts/cafe.gd").new()
	game.is_2d = true
	check(game.progress_file() == "user://contextspeak-cafe-2d-v1.json", "2D progress must have its own native save.")
	var world = game.make_world()
	root.add_child(world)
	await physics_frame
	check(world.find_children("*", "MeshInstance3D", true, false).is_empty(), "2D mode must not load imported 3D meshes.")
	check(world.art is Node2D, "The preview renders through a native 2D canvas.")
	check(not world.near_barista(), "The player starts at the entrance, away from Maya.")
	world.walking_to_counter = true
	for frame in 160:
		world.move_player(1.0 / 60, false)
		await physics_frame
		if not world.walking_to_counter:
			break
	check(world.near_barista(), "The shared helper route must reach Maya in 2D.")
	world.set_target_npc("friend")
	check(world.has_method("guide_npc_id") and world.guide_npc_id() == "friend", "The guide must point manual explorers to the active challenge NPC before walking.")
	world.set_target_npc("counter")
	world.show_reward()
	world.show_reward()
	check(world.cups.get_child_count() == 2, "Reward replay must still create exactly two drinks.")
	world.join_friend()
	for frame in 160:
		world.move_player(1.0 / 60, false)
		await physics_frame
		if world.seated:
			break
	check(world.seated and world.nearest_npc_id() == "friend", "Ordering can lead to the same friend seat in 2D.")
	world.reset_journey()
	world.player.position = Vector3(0, 0, -0.7)
	var key := InputEventKey.new()
	key.physical_keycode = KEY_W
	key.pressed = true
	Input.parse_input_event(key)
	for frame in 60:
		world.move_player(1.0 / 60, false)
		await physics_frame
	key.pressed = false
	Input.parse_input_event(key)
	check(world.player.position.z > -1.1, "The player cannot walk through the drawn counter.")
	world.player.position = Vector3(-5.9, 0, -1.2)
	key = key.duplicate()
	key.pressed = true
	Input.parse_input_event(key)
	for frame in 100:
		world.move_player(1.0 / 60, false)
		await physics_frame
	key.pressed = false
	Input.parse_input_event(key)
	check(world.player.position.z > -2.15, "The reading-nook bookcase must block walking through its floor footprint.")
	world.art.frame_room(Vector2(573, 793))
	check(Rect2(Vector2.ZERO, Vector2(573, 793)).encloses(world.art.room_bounds()), "Narrow framing must contain the illustrated room.")
	world.free()
	game.world.free()
	game.world = game.make_world()
	for component in [game.world, game.ui, game.connection, game.voice, game.recorder, game.speaker]:
		root.add_child(component)
	await process_frame
	game.connection.token = "offline-2d-runtime"
	game.roleplay.setup(game)
	check(game.roleplay.menu.picker.item_count == 1, "The 2D preview only advertises its implemented café scene.")
	check(game.roleplay.begin("cafe", "customer", true, "challenge"), "The shared challenge can use the 2D adapter.")
	check(game.world.get_script().resource_path.ends_with("world_2d.gd"), "Challenge selection retains the 2D presentation.")
	check(game.roleplay.progress_file() == "user://contextspeak-scenes-2d-v1.json", "Challenge saves are also isolated.")
	check(not game.roleplay.begin("shopping", "shopper"), "A missing 2D scene cannot silently switch to 3D.")
	game.roleplay.leave()
	check(game.world.get_script().resource_path.ends_with("world_2d.gd"), "Returning to Everyday retains the 2D adapter.")
	for component in [game.world, game.ui, game.connection, game.voice, game.recorder, game.speaker]:
		component.free()
	game.free()
	print("2D café world: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
