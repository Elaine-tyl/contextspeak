extends Node3D

signal seated_at_table

const Avatar = preload("avatar.gd")
const Decor = preload("cafe_decor.gd")
const ForestArchitecture = preload("forest_architecture.gd")
const ForestGarden = preload("forest_garden.gd")
const BARISTA := Vector3(0, 0, -3.55)
var player: CharacterBody3D
var hero: Avatar
var maya: Avatar
var camera: Camera3D
var cups: Node3D
var walking_to_counter := false
var walking_to_friend := false
var seated := false
var companion: Avatar
var barista_tag: Label3D
var friend_tag: Label3D
var in_conversation := false
var viewing_friend := false
var reduce_motion := false
var camera_focus := Vector3(0, 1.1, -1.3)
var ambience: Node3D
var forest_garden: Node3D

func material(colour: String) -> StandardMaterial3D:
	return Decor.finish(colour)

func box(size: Vector3, position_at: Vector3, colour: String, solid := false) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	mesh.mesh = Decor.box_mesh(size)
	mesh.material_override = material(colour)
	mesh.position = position_at
	add_child(mesh)
	if solid:
		var body := StaticBody3D.new()
		var collision := CollisionShape3D.new()
		var bounds := BoxShape3D.new()
		bounds.size = size
		collision.shape = bounds
		mesh.add_child(body)
		body.add_child(collision)
	return mesh

func asset(file: String, at: Vector3, height: float) -> Node3D:
	var model: Node3D = load("res://assets/" + file + (".glb" if file.get_extension() == "" else "")).instantiate()
	add_child(model)
	# Normalize the imported models to metres, preserving their proportions.
	var bounds := AABB()
	var first := true
	for mesh in model.find_children("*", "MeshInstance3D", true, false):
		var part: AABB = mesh.global_transform * mesh.get_aabb()
		bounds = part if first else bounds.merge(part)
		first = false
	var factor := height / maxf(bounds.size.y, 0.01)
	model.scale *= factor
	# Asset origins vary: center X/Z and place the lowest point on the floor.
	model.position = at - Vector3(bounds.get_center().x, bounds.position.y, bounds.get_center().z) * factor
	if (file.begins_with("kaykit/") and not "jar" in file) or file.begins_with("furniture/"):
		Decor.recolour(model, file)
	return model

func character(clothing: String, at: Vector3) -> Avatar:
	var avatar := Avatar.new()
	add_child(avatar)
	avatar.setup(load("res://assets/quaternius/woman.fbx").instantiate(), clothing)
	avatar.position = at
	return avatar

func sign_text(text: String, at: Vector3, size := 32) -> Label3D:
	var sign := Label3D.new()
	sign.text = text
	sign.font_size = size
	sign.pixel_size = 0.007
	sign.modulate = Color("#fff5dd")
	sign.outline_size = 0
	sign.position = at
	add_child(sign)
	return sign

func _ready() -> void:
	var environment := WorldEnvironment.new()
	var sky := Environment.new()
	sky.background_mode = Environment.BG_COLOR
	sky.background_color = Color("#c5d8d5")
	sky.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	sky.ambient_light_color = Color("#ebe8dc")
	sky.ambient_light_energy = 0.38
	environment.environment = sky
	add_child(environment)
	var sunlight := DirectionalLight3D.new()
	sunlight.rotation_degrees = Vector3(-48, -28, 0)
	sunlight.light_color = Color("#fff4e8")
	sunlight.light_energy = 0.46
	sunlight.shadow_enabled = true
	sunlight.shadow_opacity = 0.65
	sunlight.directional_shadow_max_distance = 28
	add_child(sunlight)

	_build_room()
	# The collision stays simple; KayKit supplies the visible restaurant equipment.
	box(Vector3(6.4, 1.1, 2.5), Vector3(0, 0.55, -2.2), "#426854", true).hide()
	for x in [-2.0, 0.0, 2.0]:
		asset("kaykit/kitchencounter_straight_A.gltf", Vector3(x, 0, -2.2), 1.22)
	asset("kaykit/kitchentable_B.gltf", Vector3(-3.1, 0, -4.5), 1.1)
	asset("kaykit/kitchencounter_sink.gltf", Vector3(2.7, 0, -4.6), 1.1)
	asset("kaykit/dishrack_plates.gltf", Vector3(2.7, 1.12, -4.6), 0.35)
	asset("kaykit/jar_A_medium.gltf", Vector3(-3.4, 1.12, -4.5), 0.38)
	asset("kaykit/jar_B_small.gltf", Vector3(-2.9, 1.12, -4.5), 0.26)
	box(Vector3(4.7, 1.4, 0.12), Vector3(0, 2.55, -5.45), "#684e3b").material_override = Decor.surface("oak", "#684e3b")
	box(Vector3(4.5, 1.2, 0.035), Vector3(0, 2.55, -5.37), "#334d43")
	sign_text("CONTEXTSPEAK\nAfter Class Cafe", Vector3(0, 2.6, -5.32), 42)
	asset("furniture/kitchenCoffeeMachine", Vector3(-1.9, 1.22, -2.35), 0.75)
	asset("food/croissant", Vector3(1.65, 1.22, -2.0), 0.16)
	asset("food/croissant", Vector3(2.1, 1.22, -2.0), 0.16)
	asset("food/cup-coffee", Vector3(-1.25, 1.22, -2.0), 0.15)
	asset("furniture/plantSmall1", Vector3(2.65, 1.22, -2.2), 0.5)
	maya = character("barista-clothed", BARISTA)
	barista_tag = sign_text("MAYA  ·  BARISTA", Vector3(0, 2.45, -3.55), 26)
	barista_tag.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	barista_tag.modulate = Color("#334d43")

	for x in [-5.1, 5.1]:
		var window_light := OmniLight3D.new()
		window_light.position = Vector3(x, 2.6, -4.7)
		window_light.light_color = Color("#fff4e8")
		window_light.light_energy = 0.16
		window_light.omni_range = 4.2
		window_light.shadow_enabled = false
		add_child(window_light)
		# The hero tree occupies the left conservatory bay; retain the right pot.
		if x > 0:
			asset("furniture/pottedPlant", Vector3(x, 0, -4.75), 1.5)
	for x in [-4.6, 4.6]:
		for z in [-0.3, 3.2]:
			asset("kaykit/table_round_A_small.gltf", Vector3(x, 0, z), 0.95)
			box(Vector3(1.4, 0.8, 1.4), Vector3(x, 0.4, z), "#967657", true).hide()
			for side in [-1, 1]:
				var chair := asset("kaykit/chair_A.gltf", Vector3(x + side * 1.1, 0, z), 0.95)
				chair.rotation.y = -side * PI / 2
			if x != 4.6 or z != -0.3:
				asset("food/cup-tea", Vector3(x, 0.96, z), 0.13)
			asset("furniture/plantSmall1", Vector3(x + 0.35, 0.96, z + 0.15), 0.32)
	asset("furniture/books", Vector3(4.5, 0.96, -0.8), 0.12)
	var entry := asset("kaykit/door_A.gltf", Vector3(-7.18, 0, 3.3), 2.7)
	entry.rotation.y = PI / 2
	asset("furniture/lampRoundFloor", Vector3(-6.5, 0, 1.3), 2.1)
	companion = character("woman-clothed", Vector3(5.75, 0, -0.3))
	companion.rotation.y = -PI / 2
	companion.play("sit")
	friend_tag = sign_text("ALEX · YOUR FRIEND", Vector3(5.75, 2.15, -0.3), 24)
	friend_tag.modulate = Color("#334d43")
	friend_tag.billboard = BaseMaterial3D.BILLBOARD_ENABLED

	player = CharacterBody3D.new()
	player.position = Vector3(0, 0, 4)
	add_child(player)
	hero = character("learner-clothed", Vector3.ZERO)
	remove_child(hero)
	player.add_child(hero)
	var collider := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.28
	capsule.height = 1.55
	collider.shape = capsule
	collider.position.y = 0.8
	player.add_child(collider)
	camera = Camera3D.new()
	add_child(camera)
	camera.fov = 56
	_update_camera(0, true)
	Decor.build(self)
	ambience = find_child("CafeAmbience", true, false)
	forest_garden = ForestGarden.new()
	forest_garden.name = "ForestGarden"
	add_child(forest_garden)
	forest_garden.build()
	cups = Node3D.new()
	add_child(cups)

func _build_room() -> void:
	box(Vector3(15, 0.2, 12), Vector3(0, -0.1, 0), "#9d846c", true).material_override = Decor.surface("floor", "#9d846c")
	# Tall glazing replaces only the visible walls. Full original boundary
	# collision remains beneath it; the outside forest adds no accessible area.
	box(Vector3(15, 3.8, 0.2), Vector3(0, 1.8, -5.8), "#d8d1c3", true).hide()
	for x in [-7.4, 7.4]:
		box(Vector3(0.2, 3.8, 12), Vector3(x, 1.8, 0), "#d8d1c3", true).hide()
	var architecture := ForestArchitecture.new()
	architecture.name = "ForestConservatory"
	add_child(architecture)
	architecture.build()

func _exploration_focus(wide_view: float) -> Vector3:
	# Wide views retain more of the room; narrow views track the learner fully
	# with a shorter forward look, keeping the body inside the portrait frustum.
	var room_focus := Vector3(player.position.x * 0.9, 1.1, -1.3 + (player.position.z - 4) * 0.8)
	var player_focus := Vector3(player.position.x, 1.1, player.position.z - 1.3)
	return player_focus.lerp(room_focus, wide_view)

func _process(delta: float) -> void:
	# Dialogue identifies the speaker; omit duplicate floating names in closeups.
	barista_tag.visible = not in_conversation
	friend_tag.visible = not (in_conversation and (seated or viewing_friend))
	if is_instance_valid(ambience):
		ambience.advance(delta, reduce_motion)
	if is_instance_valid(forest_garden):
		forest_garden.advance(delta, reduce_motion)
	if walking_to_friend:
		for index in cups.get_child_count():
			cups.get_child(index).position = player.position + Vector3(-0.22 + index * 0.44, 1.05, -0.2)
	_update_camera(delta)

func _update_camera(delta: float, snap := false) -> void:
	var viewport_size := get_viewport().get_visible_rect().size
	var wide_view := smoothstep(0.8, 1.4, viewport_size.x / maxf(viewport_size.y, 1))
	var focus := _exploration_focus(wide_view)
	var target := focus + Vector3(4.8, 3.5, lerpf(6.3, 10.3, wide_view))
	if seated or (in_conversation and viewing_friend):
		target = Vector3(2.2, 2.5, 4.2)
		focus = Vector3(4.55, 0.35, -0.3)
	elif in_conversation:
		target = Vector3(1.8, 2.6, 1.15)
		focus = Vector3(0, 1.25, -3.4)
	if reduce_motion or snap:
		camera.position = target
		camera_focus = focus
	else:
		var blend := 1 - exp(-delta * 3)
		camera.position = camera.position.lerp(target, blend)
		camera_focus = camera_focus.lerp(focus, blend)
	camera.look_at(camera_focus)

func near_barista() -> bool:
	return player.position.distance_to(BARISTA) < 4.0

func move_player(delta: float, paused: bool) -> void:
	if seated:
		player.velocity = Vector3.ZERO
		hero.play("sit")
		return
	if paused:
		player.velocity = Vector3.ZERO
		hero.play("idle")
		return
	var direction := Vector3.ZERO
	if walking_to_friend:
		direction = Vector3(3.45, 0, -0.3) - player.position
		direction.y = 0
		if direction.length() < 0.15:
			seat_with_friend()
			seated_at_table.emit()
			return
	elif walking_to_counter:
		direction = Vector3(0, 0, -0.7) - player.position
		direction.y = 0
		if direction.length() < 0.15:
			walking_to_counter = false
	else:
		direction.x = float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT))
		direction.z = float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP))
	if direction.length() > 0.15:
		hero.rotation.y = lerp_angle(hero.rotation.y, atan2(direction.x, direction.z), minf(1, delta * 10))
		hero.play("walk")
	else:
		hero.play("idle")
	player.velocity.x = direction.normalized().x * 3
	player.velocity.z = direction.normalized().z * 3
	player.velocity.y -= 18 * delta
	player.move_and_slide()
	player.position.x = clampf(player.position.x, -6.8, 6.8)
	player.position.z = clampf(player.position.z, -5, 5)

func show_reward() -> void:
	if cups.get_child_count() != 0:
		return
	for x in [-0.35, 0.35]:
		var cup := asset("food/cup-coffee", Vector3(x, 1.22, -1.95), 0.15)
		remove_child(cup)
		cups.add_child(cup)

func join_friend() -> void:
	walking_to_counter = false
	walking_to_friend = true

func seat_with_friend() -> void:
	walking_to_friend = false
	walking_to_counter = false
	seated = true
	player.position = Vector3(3.45, 0, -0.3)
	player.velocity = Vector3.ZERO
	hero.rotation.y = PI / 2
	hero.play("sit")
	companion.play("sit")
	for index in cups.get_child_count():
		cups.get_child(index).position = Vector3(4.35 + index * 0.5, 0.96, -0.3)

func reset_journey() -> void:
	walking_to_counter = false
	walking_to_friend = false
	seated = false
	player.position = Vector3(0, 0, 4)
	hero.rotation.y = 0
	hero.play("idle")
