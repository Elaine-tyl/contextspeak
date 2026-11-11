extends SceneTree

# These checks protect the real walking routes, immediate pause, keyboard
# steering and composed camera while the visual environment is rebuilt.
const World = preload("../scripts/world.gd")
var failures := 0
var arrivals := 0

func _initialize() -> void:
	call_deferred("run")

func expect(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func opaque_obstruction(world: Node3D, from: Vector3, to: Vector3) -> String:
	# Decorative geometry has no collision, so a physics ray alone cannot protect
	# a speaking face from a new roof beam, planter or opaque window panel.
	for node in world.find_children("*", "GeometryInstance3D", true, false):
		if not node.is_visible_in_tree():
			continue
		var actor := false
		for avatar in [world.hero, world.maya, world.companion]:
			actor = actor or avatar.is_ancestor_of(node)
		if actor:
			continue
		var paint: Material = node.material_override
		if paint is StandardMaterial3D and paint.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED:
			continue
		if node is MeshInstance3D:
			var local_from: Vector3 = node.global_transform.affine_inverse() * from
			var local_to: Vector3 = node.global_transform.affine_inverse() * to
			if node.get_aabb().intersects_segment(local_from, local_to) != null:
				return str(node.name)
		elif node is MultiMeshInstance3D and node.multimesh:
			for index in node.multimesh.instance_count:
				var placement: Transform3D = node.global_transform * node.multimesh.get_instance_transform(index)
				if node.multimesh.mesh.get_aabb().intersects_segment(placement.affine_inverse() * from, placement.affine_inverse() * to) != null:
					return str(node.name)
	return ""

func camera_is_clear(world: Node3D) -> bool:
	var volume := PhysicsShapeQueryParameters3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = 0.2
	volume.shape = sphere
	volume.transform = world.camera.global_transform
	if not world.get_world_3d().direct_space_state.intersect_shape(volume).is_empty():
		return false
	# Include visual-only architecture and foliage that physics cannot see.
	for node in world.find_children("*", "GeometryInstance3D", true, false):
		if not node.is_visible_in_tree():
			continue
		if node is MeshInstance3D:
			var bounds: AABB = node.global_transform * node.get_aabb()
			if bounds.grow(0.2).has_point(world.camera.global_position):
				return false
		elif node is MultiMeshInstance3D and node.multimesh:
			for index in node.multimesh.instance_count:
				var placement: Transform3D = node.global_transform * node.multimesh.get_instance_transform(index)
				var bounds: AABB = placement * node.multimesh.mesh.get_aabb()
				if bounds.grow(0.2).has_point(world.camera.global_position):
					return false
	return true

func run() -> void:
	var world = World.new()
	root.add_child(world)
	world.set_process(false)
	world.seated_at_table.connect(func(): arrivals += 1)
	await physics_frame
	var startup_camera: Transform3D = world.camera.transform
	world._process(1.0 / 60)
	expect(world.camera.transform.is_equal_approx(startup_camera), "Exploration starts at its settled camera composition without an opening jump.")
	var player_head: Vector2 = world.camera.unproject_position(world.player.position + Vector3(0, 1.65, 0))
	var player_feet: Vector2 = world.camera.unproject_position(world.player.position + Vector3(0, 0.05, 0))
	if not OS.get_cmdline_user_args().has("--baseline"):
		expect(absf(player_head.y - player_feet.y) / root.get_visible_rect().size.y > 0.14, "The lower exploration camera keeps the learner readable within the room.")
	expect(not world.near_barista(), "The entrance cannot start a distant counter interaction.")
	world.walking_to_counter = true
	for frame in 140:
		world.move_player(1.0 / 60, false)
		await physics_frame
		if not world.walking_to_counter:
			break
	expect(world.player.position.distance_to(Vector3(0, 0, -0.7)) < 0.18, "The original entrance-to-counter route stays clear and arrives without teleporting.")
	expect(world.near_barista(), "Counter arrival still permits talking to Maya.")
	world.show_reward()
	world.show_reward()
	expect(world.cups.get_child_count() == 2, "Reward display remains idempotent with two coffee cups.")
	world.join_friend()
	world.move_player(1.0 / 60, false)
	await physics_frame
	var stopped: Vector3 = world.player.position
	world.move_player(1.0 / 60, true)
	await physics_frame
	expect(world.player.position == stopped and world.player.velocity == Vector3.ZERO, "Pausing stops a moving guided route immediately.")
	expect(world.walking_to_friend, "Pausing preserves the chosen destination.")
	for frame in 140:
		world.move_player(1.0 / 60, false)
		await physics_frame
		if world.seated:
			break
	expect(world.seated and arrivals == 1, "The counter-to-friend route reaches the original seat and signals exactly once.")
	expect(world.player.position == Vector3(3.45, 0, -0.3), "Seating preserves the established chair alignment.")
	expect(world.cups.get_child(0).position == Vector3(4.35, 0.96, -0.3), "Delivered coffee lands on the friend table.")
	world.move_player(1.0 / 60, false)
	expect(world.player.velocity == Vector3.ZERO and world.hero.current == "sit", "Seated movement remains stopped with the seated animation.")
	world.reset_journey()
	await physics_frame
	world.reduce_motion = true
	world._process(0.016)
	var steering_camera: Vector3 = world.camera.position
	var start: Vector3 = world.player.position
	key(KEY_D, true)
	for frame in 10:
		world.move_player(1.0 / 60, false)
		await physics_frame
	key(KEY_D, false)
	expect(world.player.position.x > start.x + 0.35 and absf(world.player.position.z - start.z) < 0.05, "D still steers east without shifting the camera-relative controls.")
	world._process(0.016)
	if not OS.get_cmdline_user_args().has("--baseline"):
		expect(world.camera.position.x > steering_camera.x + 0.1, "Exploration camera gently follows actual manual eastward movement.")
	key(KEY_UP, true)
	for frame in 10:
		world.move_player(1.0 / 60, false)
		await physics_frame
	key(KEY_UP, false)
	expect(world.player.position.z < start.z - 0.35, "Arrow Up still steers toward the counter.")
	world.move_player(1.0 / 60, true)

	world.reduce_motion = true
	world.player.position = Vector3(0, 0, -0.7)
	world.in_conversation = true
	world._process(1.0)
	var settled_counter: Transform3D = world.camera.transform
	world._process(0.016)
	expect(world.camera.transform == settled_counter, "Reduced motion settles the counter camera immediately and deterministically.")
	expect(world.camera.position == Vector3(1.8, 2.6, 1.15) and world.camera_focus == Vector3(0, 1.25, -3.4), "Exploration changes preserve the established counter camera and aim.")
	expect(world.camera.is_position_in_frustum(Vector3(0, 1, -3.55)), "The counter camera keeps Maya in view.")
	expect(world.camera.is_position_in_frustum(Vector3(0, 1.5, -0.7)), "The counter camera also keeps the learner's head in view.")
	expect(opaque_obstruction(world, world.camera.global_position, Vector3(0, 1.55, -3.55)) == "", "New architectural details do not obscure Maya's speaking face.")
	var face_top: Vector2 = world.camera.unproject_position(Vector3(0, 1.65, -3.55))
	var face_chin: Vector2 = world.camera.unproject_position(Vector3(0, 1.35, -3.55))
	var face_fraction: float = absf(face_top.y - face_chin.y) / root.get_visible_rect().size.y
	print("Counter face framing: ", face_fraction)
	if not OS.get_cmdline_user_args().has("--baseline"):
		expect(face_fraction > 0.045, "The counter camera makes Maya's face readable rather than distant behind the counter.")
	var camera_volume := PhysicsShapeQueryParameters3D.new()
	var camera_sphere := SphereShape3D.new()
	camera_sphere.radius = 0.2
	camera_volume.shape = camera_sphere
	camera_volume.transform = world.camera.global_transform
	expect(world.get_world_3d().direct_space_state.intersect_shape(camera_volume).is_empty(), "The closer counter camera has clearance from walls and furniture colliders.")
	world.seat_with_friend()
	world._process(1.0)
	expect(world.camera.position == Vector3(2.2, 2.5, 4.2), "The seated camera retains its original composition.")
	expect(world.camera.is_position_in_frustum(Vector3(5.75, 1, -0.3)), "The seated camera keeps Alex in view.")
	expect(opaque_obstruction(world, world.camera.global_position, Vector3(5.75, 1.25, -0.3)) == "", "The conservatory's cutaway keeps Alex's speaking face unobstructed.")
	world.reset_journey()
	world.in_conversation = false
	world.reduce_motion = false
	var before: Vector3 = world.camera.position
	var prior_rotation: Quaternion = world.camera.quaternion
	world._process(1.0 / 60)
	expect(world.camera.position.distance_to(before) > 0 and world.camera.position.distance_to(before) < 0.5, "Ordinary camera transitions blend rather than jump.")
	if not OS.get_cmdline_user_args().has("--baseline"):
		expect(prior_rotation.angle_to(world.camera.quaternion) < 0.15, "The view direction also blends, preventing a sudden dialogue-to-overview turn.")
	world.reduce_motion = true
	world._process(1.0)
	var settled_exploration: Transform3D = world.camera.transform
	world._process(0.016)
	expect(world.camera.transform == settled_exploration, "Reduced motion settles exploration immediately and deterministically.")
	var window_size := root.size
	var content_size := root.content_scale_size
	for size in [Vector2i(1280, 800), Vector2i(573, 793), Vector2i(800, 800)]:
		root.size = size
		root.content_scale_size = size
		await process_frame
		for at in [Vector3(-6.8, 0, -5), Vector3(6.8, 0, -5), Vector3(-6.8, 0, 5), Vector3(6.8, 0, 5), Vector3(0, 0, -0.7), Vector3(3.45, 0, -0.3), Vector3(0, 0, 4)]:
			world.player.position = at
			world._process(0.016)
			expect(world.camera.is_position_in_frustum(at + Vector3(0, 1.65, 0)) and world.camera.is_position_in_frustum(at + Vector3(0, 0.45, 0)), "Exploration frames the learner's head and body at %s in %s" % [at, size])
			if size.x >= 1000:
				var screen_head: Vector2 = world.camera.unproject_position(at + Vector3(0, 1.65, 0)) / root.get_visible_rect().size
				var screen_body: Vector2 = world.camera.unproject_position(at + Vector3(0, 0.45, 0)) / root.get_visible_rect().size
				expect(screen_head.x > 0.24 and screen_body.x > 0.24 and screen_head.x < 0.8 and screen_body.x < 0.8, "The learner stays clear of desktop mission/menu cards at " + str(at))
			expect(camera_is_clear(world), "Exploration camera keeps sphere clearance from collision, tree and architecture at %s in %s" % [at, size])
	root.size = window_size
	root.content_scale_size = content_size
	await process_frame
	world.reset_journey()
	world.in_conversation = true
	world._process(0.016)
	world.in_conversation = false
	world.reduce_motion = false
	var transition_start: Transform3D = world.camera.transform
	var focus_start: Vector3 = world.camera_focus
	var unmoved: Vector3 = world.player.position
	var velocity_before: Vector3 = world.player.velocity
	world._process(1.0)
	var single_step: Vector3 = world.camera.position
	var single_focus: Vector3 = world.camera_focus
	world.camera.transform = transition_start
	world.camera_focus = focus_start
	for frame in 60:
		world._process(1.0 / 60)
	expect(world.camera.position.distance_to(single_step) < 0.0001 and world.camera_focus.distance_to(single_focus) < 0.0001, "Camera damping gives the same composition at different frame rates.")
	expect(world.player.position == unmoved and world.player.velocity == velocity_before, "Camera tracking never advances or changes player physics.")
	world.reduce_motion = true
	world._process(0.016)
	if not OS.get_cmdline_user_args().has("--baseline"):
		expect(world.find_child("ForestGarden", true, false) != null, "The real outside garden is attached to the playable café.")
		expect(opaque_obstruction(world, Vector3(4.2, 2.8, -5.1), Vector3(4.2, 2.8, -6.1)) == "", "Rear glazing reveals the 3D garden instead of an opaque window/back wall.")
		var panes := 0
		for mesh in world.find_children("*", "MeshInstance3D", true, false):
			var paint: Material = mesh.material_override
			if mesh.is_visible_in_tree() and paint is StandardMaterial3D and paint.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED:
				panes += 1
				expect(paint.albedo_color.a <= 0.15 and mesh.cast_shadow == GeometryInstance3D.SHADOW_CASTING_SETTING_OFF, "Conservatory glass stays clear without an opaque shadow overlay.")
		expect(panes >= 2 and panes <= 4, "Conservatory glazing uses a few large surfaces rather than many transparent layers.")

	var shadows := 0
	var lights := 0
	var visible_meshes := 0
	var environment_materials := {}
	var environment_geometry := {}
	var surfaces := 0
	var triangles := 0
	for node in world.find_children("*", "Light3D", true, false):
		lights += 1
		if node.shadow_enabled:
			shadows += 1
	for mesh in world.find_children("*", "MeshInstance3D", true, false):
		if not mesh.is_visible_in_tree():
			continue
		visible_meshes += 1
		var actor := false
		for avatar in [world.hero, world.maya, world.companion]:
			actor = actor or avatar.is_ancestor_of(mesh)
		for surface in mesh.mesh.get_surface_count():
			surfaces += 1
			var arrays: Array = mesh.mesh.surface_get_arrays(surface)
			triangles += arrays[Mesh.ARRAY_INDEX].size() / 3 if arrays[Mesh.ARRAY_INDEX] != null else arrays[Mesh.ARRAY_VERTEX].size() / 3
			if not actor:
				var paint = mesh.get_active_material(surface)
				if paint:
					environment_materials[paint.get_rid()] = true
		if not actor:
			environment_geometry[mesh.mesh.get_rid()] = true
	var grouped_instances := 0
	for node in world.find_children("*", "MultiMeshInstance3D", true, false):
		if not node.is_visible_in_tree() or not node.multimesh:
			continue
		visible_meshes += 1
		var count: int = node.multimesh.instance_count
		grouped_instances += count
		var geometry: Mesh = node.multimesh.mesh
		environment_geometry[geometry.get_rid()] = true
		if node.material_override:
			environment_materials[node.material_override.get_rid()] = true
		for surface in geometry.get_surface_count():
			surfaces += 1
			var arrays: Array = geometry.surface_get_arrays(surface)
			triangles += count * (arrays[Mesh.ARRAY_INDEX].size() / 3 if arrays[Mesh.ARRAY_INDEX] != null else arrays[Mesh.ARRAY_VERTEX].size() / 3)
	print("Cafe render structure: visible meshes=", visible_meshes, " surfaces=", surfaces, " indexed triangles=", triangles, " environment materials=", environment_materials.size(), " environment meshes=", environment_geometry.size(), " lights=", lights, " shadow lights=", shadows)
	print("Grouped decorative instances: ", grouped_instances)
	expect(shadows == 1 and lights <= 6, "Only the sun casts shadows and warm fills remain bounded.")
	expect(visible_meshes <= 500 and surfaces <= 550 and triangles <= 180000, "The visual layer stays within the WebGL mesh and triangle budget.")
	if not OS.get_cmdline_user_args().has("--baseline"):
		expect(environment_materials.size() <= 45, "Repeated architecture and furniture share a bounded material set.")
		expect(environment_geometry.size() <= 100, "Repeated architectural pieces share geometry.")
		var ambience = world.find_child("CafeAmbience", true, false)
		expect(ambience != null, "The café includes a small bounded ambient detail.")
		if ambience:
			world.reduce_motion = false
			world._process(0.5)
			var animated: Transform3D = ambience.get_child(0).transform
			world._process(0.5)
			expect(ambience.get_child(0).transform != animated, "The decorative detail animates without moving gameplay objects.")
			world.reduce_motion = true
			world._process(0.5)
			var still: Transform3D = ambience.get_child(0).transform
			world._process(1.0)
			expect(ambience.get_child(0).transform == still, "Reduced motion suppresses ambient animation completely.")
		var canopy = world.find_child("HeroCanopy2", true, false)
		expect(canopy != null, "The hero tree's actual canopy is integrated into the scene.")
		if canopy:
			world.reduce_motion = true
			world._process(0.1)
			var rest: Transform3D = canopy.transform
			world.reduce_motion = false
			world._process(0.7)
			expect(canopy.transform != rest, "The world advances the garden's restrained canopy animation.")
			world.reduce_motion = true
			world._process(0.7)
			expect(canopy.transform == rest, "The world forwards reduced motion and restores the real canopy's resting pose.")
	print("Cozy café world: ", "passed" if failures == 0 else "%d failed" % failures)
	world.free()
	quit(0 if failures == 0 else 1)
