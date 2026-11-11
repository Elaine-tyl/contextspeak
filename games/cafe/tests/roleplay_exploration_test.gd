extends SceneTree

# Removing NPC proximity, corridor navigation, collision, or camera following
# must break an observable exploration behavior in this real world.
const ZONES := {
	"shopping": ["clothes", "sale", "fitting"],
	"farm": ["fields", "farm_shop", "packing"],
	"market": ["fruit_stall", "produce_stall", "checkout"],
	"restaurant": ["entrance", "dining", "till"],
	"amusement_park": ["tickets", "rides", "snacks"]
}
const ALTERNATE_ROLES := {
	"shopping": ["buddy", ["Alex", "Maya", "Jamie"]],
	"farm": ["farm_shop_customer", ["Jo", "Mia", "Sam"]],
	"market": ["vendor", ["Ana", "Ben", "Mia"]],
	"restaurant": ["server", ["Tom", "Mia", "Alex"]],
	"amusement_park": ["planner", ["Sam", "Jamie", "Alex"]]
}
var failures := 0

func _initialize() -> void:
	call_deferred("run")

func expect(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func run() -> void:
	var world_script = load("res://scripts/roleplay_world.gd")
	var probe = world_script.new()
	expect(probe.has_method("npc_data") and probe.has_method("set_target_npc") and probe.has_method("current_zone"), "Exploration exposes actual NPC destinations and zone names.")
	if probe.has_method("npc_data"):
		root.add_child(probe)
		probe.configure("shopping", "Visitor", "Alex")
		expect(probe.find_child("EntranceGate", true, false) is Node3D, "An entrance landmark makes the spawn area recognizable.")
		var colours: Array[Color] = []
		for actor in probe.actors.values():
			colours.append(actor.find_child("CampusJacket", true, false).material_override.albedo_color)
		expect(colours[0] != colours[1] and colours[1] != colours[2] and colours[0] != colours[2], "Each NPC has a distinct visible outfit as well as a name.")
	probe.free()
	if failures:
		quit(1)
		return
	Engine.time_scale = 6.0
	for scene_id in ZONES:
		var world = world_script.new()
		root.add_child(world)
		world.reduce_motion = true
		world.configure(scene_id, "Visitor", "Alex")
		await physics_frame
		expect(not world.in_conversation and world.nearest_npc_id() == "", scene_id + ": entrance leaves exploration and choice available.")
		var entered: Array[String] = []
		var previous := world.player.position as Vector3
		for id in ZONES[scene_id]:
			var data: Dictionary = world.npc_data(id)
			expect(data.get("id") == id and data.get("name", "") != "" and data.get("role", "") != "", scene_id + ": each distinct NPC has an authored identity.")
			world.set_target_npc(id)
			world.walk_to_npc(id)
			expect(world.player.position == previous, scene_id + ": optional walking never teleports to a destination.")
			for frame in 180:
				world.move_player(1.0 / 60.0, false)
				await physics_frame
				if not world.walking_to_partner:
					break
			expect(world.player.position.distance_to(data.approach) < 0.3, scene_id + ": a corridor route reaches " + id + " from the prior area.")
			expect(world.nearest_npc_id() == id and world.near_npc(), scene_id + ": talking selects the actual nearby NPC.")
			expect(not world.in_conversation, scene_id + ": arriving never opens a conversation.")
			entered.append(world.current_zone())
			var contact: bool = world.player.test_move(world.player.global_transform, (data.position - world.player.position).normalized() * 2.0)
			expect(contact, scene_id + ": " + id + " prevents walking through its actor.")
			world.focus_npc(id)
			world.in_conversation = true
			world._process(1.0)
			expect(world.camera.position.distance_to(data.position) < 12.0, scene_id + ": dialogue camera frames the nearby actor.")
			world.in_conversation = false
			previous = world.player.position
			expect(data.position.distance_to(previous) > 0.6, scene_id + ": actors and learner keep separate positions.")
		expect(entered[0] != entered[1] and entered[1] != entered[2], scene_id + ": walking visits three identified areas.")
		world.reset_actions()
		await physics_frame
		world._process(1.0)
		var start: Vector3 = world.player.position
		var camera_start: Vector3 = world.camera.position
		var key := InputEventKey.new()
		key.physical_keycode = KEY_D
		key.pressed = true
		Input.parse_input_event(key.duplicate())
		for frame in 8:
			world.move_player(1.0 / 60.0, false)
			await physics_frame
		world._process(1.0)
		key.pressed = false
		Input.parse_input_event(key.duplicate())
		expect(world.player.position.x > start.x + 1.0, scene_id + ": D moves the learner through the entrance.")
		expect(world.camera.position.x > camera_start.x + 1.0, scene_id + ": exploration camera follows the learner across the map.")
		world.walk_to_npc(ZONES[scene_id][0])
		key.pressed = true
		Input.parse_input_event(key.duplicate())
		world.move_player(1.0 / 60.0, false)
		expect(not world.walking_to_partner, scene_id + ": manual steering cancels optional guided walking.")
		key.pressed = false
		Input.parse_input_event(key.duplicate())
		world.configure(scene_id, "Alternate role", "Fallback", ALTERNATE_ROLES[scene_id][0])
		for index in 3:
			var alternate: Dictionary = world.npc_data(ZONES[scene_id][index])
			expect(alternate.name == ALTERNATE_ROLES[scene_id][1][index], scene_id + ": actor identity follows the selected catalog role.")
		world.free()
	Engine.time_scale = 1.0
	print("Explorable role worlds: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
