extends Node3D

# Original ContextSpeak scenery. The shared campus avatars use the existing
# credited CC0 Quaternius rig; optional furniture/food uses vendored CC0 packs.
const Avatar = preload("avatar.gd")
const Decor = preload("cafe_decor.gd")
const FriendArt = preload("roleplay_friend_art.gd")
const SPAWN := Vector3(0, 0, 10)
const WALK_SPEED := 3.8
const TALK_DISTANCE := 2.1
const ACTIONS := ["water", "harvest", "basket", "pay", "meal", "table", "outfit", "price", "bags", "ride", "tickets", "snack", "meet"]
const SCENES := {
	"shopping": ["Clover & Cotton", "Good finds with a friend", "#a3b9a0"],
	"farm": ["Sunny Farm", "Grow something together", "#c1a15d"],
	"market": ["Willow Market", "Fresh finds & friendly faces", "#d69c79"],
	"restaurant": ["The Garden Table", "Good food & warm welcomes", "#b47d86"],
	"amusement_park": ["Sunny Days Park", "A little adventure together", "#d5aa72"]
}

var player: CharacterBody3D
var hero: Avatar
var npc: Avatar
var camera: Camera3D
var in_conversation := false
var reduce_motion := false
var scene_id := "shopping"
var walking_to_partner := false
var action_props: Node3D
var scenery: Node3D
var materials: Dictionary = {}
var action_tweens: Array[Tween] = []
var farm_soils: Array[MeshInstance3D] = []
var friend_art: RefCounted
var route: Dictionary = {}
var actors: Dictionary = {}
var identities: Dictionary = {}
var target_npc_id := ""
var focused_npc_id := ""
var walk_points: Array[Vector3] = []
var blocked_seconds := 0.0
var target_marker: Label3D

func configure(selected_scene: String, role_label: String, npc_name: String, role_id := "") -> void:
	scene_id = selected_scene if SCENES.has(selected_scene) else "shopping"
	_stop_action_tweens()
	friend_art = FriendArt.new(self)
	farm_soils.clear()
	actors.clear()
	identities.clear()
	walk_points.clear()
	for child in get_children():
		remove_child(child)
		child.queue_free()
	route = JSON.parse_string(FileAccess.get_file_as_string("res://data/scene_routes.json"))[scene_id]
	walking_to_partner = false
	in_conversation = false
	focused_npc_id = ""
	scenery = Node3D.new()
	scenery.name = "Scenery"
	add_child(scenery)
	_build_lighting()
	_build_ground()
	match scene_id:
		"shopping": friend_art.build_shopping()
		"farm": _build_farm()
		"market": _build_market()
		"restaurant": _build_restaurant()
		"amusement_park": friend_art.build_park()
	_build_signs(role_label, npc_name)
	_build_actors(role_id, npc_name)
	player = CharacterBody3D.new()
	player.name = "Learner"
	player.position = _point(route.spawn)
	add_child(player)
	hero = _character("learner-clothed", Vector3.ZERO)
	remove_child(hero)
	player.add_child(hero)
	hero.rotation.y = PI
	_add_capsule(player, "LearnerCollision")
	camera = Camera3D.new()
	camera.name = "RoleplayCamera"
	camera.current = true
	camera.fov = 52
	add_child(camera)
	_update_camera(0.0, true)
	action_props = Node3D.new()
	action_props.name = "CompletedActions"
	add_child(action_props)
	set_target_npc(route.zones[0].id)

func _point(value: Array) -> Vector3:
	return Vector3(value[0], value[1], value[2])

func _add_capsule(parent: Node3D, title: String) -> void:
	var collider := CollisionShape3D.new()
	collider.name = title
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.3
	capsule.height = 1.65
	collider.shape = capsule
	collider.position.y = 0.85
	parent.add_child(collider)

func _build_actors(role_id: String, fallback_name: String) -> void:
	var turns: Array = []
	var catalog: Array = JSON.parse_string(FileAccess.get_file_as_string("res://data/roleplay.json"))
	for item in catalog:
		if item.id != scene_id:
			continue
		var selected: Dictionary = item.roles[0]
		for candidate in item.roles:
			if candidate.id == role_id:
				selected = candidate
		turns = selected.turns
	for index in route.zones.size():
		var zone: Dictionary = route.zones[index]
		var identity: Dictionary = turns[index].get("npc", {})
		var data := {
			"id": zone.id, "name": identity.get("name", fallback_name.get_slice(",", 0) if index == 0 else ["Alex", "Mia", "Sam"][index]),
			"role": identity.get("role", zone.label + " guide"), "zone_label": zone.label,
			"position": _point(zone.npc_position), "approach": _point(zone.approach)
		}
		identities[zone.id] = data
		var actor := _character(["woman-clothed", "learner-clothed", "barista-clothed"][index], data.position)
		actor.name = "ConversationPartner" if index == 0 else "Partner_" + zone.id
		actor.rotation.y = 0.05
		var body := StaticBody3D.new()
		actor.add_child(body)
		_add_capsule(body, "PartnerCollision" if index == 0 else "PartnerCollision_" + zone.id)
		actors[zone.id] = actor
		var tag := _text(actor, data.name + " · " + data.role, Vector3(0, 2.12, 0), 25, "#335947", 0.006)
		tag.name = "PartnerSign"
		tag.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		tag.outline_size = 5
		tag.outline_modulate = Color("#fff4dc")
	target_marker = _text(self, "", Vector3.ZERO, 28, "#795b24", 0.008)
	target_marker.name = "QuestMarker"
	target_marker.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	target_marker.outline_size = 6
	target_marker.outline_modulate = Color("#fff4dc")

func _paint(colour: String) -> StandardMaterial3D:
	if not materials.has(colour):
		materials[colour] = Decor.finish(colour)
	return materials[colour]

func _mesh(parent: Node3D, shape: PrimitiveMesh, at: Vector3, colour: String) -> MeshInstance3D:
	var part := MeshInstance3D.new()
	part.mesh = shape
	part.position = at
	part.material_override = _paint(colour)
	parent.add_child(part)
	return part

func _box(parent: Node3D, size: Vector3, at: Vector3, colour: String, solid := false) -> MeshInstance3D:
	var shape := Decor.box_mesh(size)
	var part := _mesh(parent, shape, at, colour)
	if solid:
		var body := StaticBody3D.new()
		var collider := CollisionShape3D.new()
		var bounds := BoxShape3D.new()
		bounds.size = size
		collider.shape = bounds
		part.add_child(body)
		body.add_child(collider)
	return part

func _round(parent: Node3D, at: Vector3, radius: float, height: float, colour: String, top_radius := -1.0) -> MeshInstance3D:
	var shape := CylinderMesh.new()
	shape.top_radius = radius if top_radius < 0 else top_radius
	shape.bottom_radius = radius
	shape.height = height
	shape.radial_segments = 20
	return _mesh(parent, shape, at, colour)

func _oval(parent: Node3D, at: Vector3, size: Vector3, colour: String) -> MeshInstance3D:
	var shape := SphereMesh.new()
	shape.radius = 0.5
	shape.height = 1
	shape.radial_segments = 16
	shape.rings = 8
	var part := _mesh(parent, shape, at, colour)
	part.scale = size
	return part

func _text(parent: Node3D, words: String, at: Vector3, font_size := 30, colour := "#35594b", pixel_size := 0.0045) -> Label3D:
	var label := Label3D.new()
	label.text = words
	label.font_size = font_size
	label.pixel_size = pixel_size
	label.modulate = Color(colour)
	label.outline_size = 0
	label.position = at
	parent.add_child(label)
	return label

func _asset(file: String, at: Vector3, height: float) -> Node3D:
	var model: Node3D = load("res://assets/" + file + (".glb" if file.get_extension() == "" else "")).instantiate()
	scenery.add_child(model)
	var bounds := AABB()
	var first := true
	for mesh in model.find_children("*", "MeshInstance3D", true, false):
		var part: AABB = mesh.global_transform * mesh.get_aabb()
		bounds = part if first else bounds.merge(part)
		first = false
	var factor := height / maxf(bounds.size.y, 0.01)
	model.scale *= factor
	model.position = at - Vector3(bounds.get_center().x, bounds.position.y, bounds.get_center().z) * factor
	if file.begins_with("kaykit/"):
		Decor.recolour(model, file)
	return model

func _character(clothes: String, at: Vector3) -> Avatar:
	var avatar := Avatar.new()
	add_child(avatar)
	avatar.setup(load("res://assets/quaternius/woman.fbx").instantiate(), clothes)
	avatar.position = at
	return avatar

func _build_lighting() -> void:
	var world_environment := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("#c6d4c1") if scene_id == "farm" else Color("#b9cbc2")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("#fff3dc")
	environment.ambient_light_energy = 0.37
	world_environment.environment = environment
	add_child(world_environment)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, -32, 0)
	sun.light_color = Color("#fff1d6")
	sun.light_energy = 0.32
	sun.shadow_enabled = true
	sun.shadow_opacity = 0.65
	sun.directional_shadow_max_distance = 55
	add_child(sun)

func _build_ground() -> void:
	var outdoors := scene_id in ["farm", "market", "amusement_park"]
	var floor_colour := "#afbe92" if scene_id == "farm" else "#b9c9a0" if scene_id == "amusement_park" else "#d5c1a0"
	var floor := _box(scenery, Vector3(36, 0.22, 28), Vector3(0, -0.12, 0), floor_colour, true)
	if not outdoors:
		floor.material_override = Decor.surface("floor", "#9d846c")
	# Broad connected paths remain free of props and actors.
	_box(scenery, Vector3(4, 0.025, 26), Vector3(0, 0.01, 0), "#eadbbf")
	_box(scenery, Vector3(32, 0.028, 3.6), Vector3(0, 0.014, 0), "#eadbbf")
	_box(scenery, Vector3(3.4, 0.03, 5), Vector3(10, 0.015, -2), "#eadbbf")
	_box(scenery, Vector3(3.4, 0.03, 10.2), Vector3(4, 0.015, 4.1), "#eadbbf")
	_box(scenery, Vector3(8.5, 0.03, 3.2), Vector3(7.2, 0.015, 8.2), "#eadbbf")
	_box(scenery, Vector3(3.4, 0.03, 5), Vector3(-10, 0.015, -2), "#eadbbf")
	# Visible rails describe every boundary; there are no hidden trapping walls.
	for x in [-17.7, 17.7]:
		_box(scenery, Vector3(0.2, 0.7, 27.6), Vector3(x, 0.35, 0), "#a4b18a", true)
	for z in [-13.7, 13.7]:
		_box(scenery, Vector3(35.4, 0.7, 0.2), Vector3(0, 0.35, z), "#a4b18a", true)
	if not outdoors:
		_box(scenery, Vector3(35.5, 3.9, 0.2), Vector3(0, 1.95, -13.7), "#d8d1c3", true).material_override = Decor.surface("plaster", "#d8d1c3")
	for index in route.zones.size():
		var zone: Dictionary = route.zones[index]
		var center := _point(zone.center)
		var tint: String = ["#c3ccb2", "#d9c6a5", "#d2bdb0"][index]
		var area := _box(scenery, Vector3(zone.size[0], 0.012, zone.size[1]), center + Vector3(0, 0.005, 0), tint)
		if not outdoors:
			area.material_override = Decor.surface("fabric", tint)
		var display := _display_position(index)
		_box(scenery, Vector3(1.8, 0.13, 1.4), display + Vector3(0, 0.83, 0), "#9d846c", true).material_override = Decor.surface("oak", "#9d846c")
		for dx in [-0.65, 0.65]:
			_box(scenery, Vector3(0.12, 0.8, 1.05), display + Vector3(dx, 0.4, 0), "#ad8b66", true)
		_text(scenery, "OUR CONVERSATION", display + Vector3(0, 0.65, 0.73), 20, "#5e7560", 0.003)

func _display_position(index: int) -> Vector3:
	var center := _point(route.zones[index].center)
	return center + Vector3(-3.8 if index == 0 else 3.8, 0, 0)

func _build_signs(role_label: String, _npc_name: String) -> void:
	_box(scenery, Vector3(6.8, 1.0, 0.12), Vector3(0, 3.8, -12.9), "#507563")
	var title := _text(scenery, SCENES[scene_id][0] + " · " + scene_id.capitalize(), Vector3(0, 3.87, -12.8), 38, "#fff4db", 0.008)
	title.name = "SceneSign"
	_text(scenery, SCENES[scene_id][1], Vector3(0, 3.52, -12.8), 24, "#eee7ce", 0.006)
	var role := _text(scenery, "YOU · " + role_label, SPAWN + Vector3(0, 2.18, 0), 23, "#335947", 0.006)
	role.name = "RoleSign"
	role.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	role.outline_size = 4
	role.outline_modulate = Color("#fff4dc")
	for index in route.zones.size():
		var zone: Dictionary = route.zones[index]
		var at := _point(zone.center) + Vector3(0, 0, -3.9)
		_box(scenery, Vector3(0.12, 2.5, 0.12), at + Vector3(0, 1.25, 0), "#a58d6d", true)
		_box(scenery, Vector3(3.5, 0.7, 0.12), at + Vector3(0, 2.5, 0), "#507563")
		var sign := _text(scenery, str(index + 1) + " · " + zone.label.to_upper(), at + Vector3(0, 2.52, 0.08), 30, "#fff4db", 0.006)
		sign.name = "ZoneSign_" + zone.id
		sign.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	var gate := Node3D.new()
	gate.name = "EntranceGate"
	scenery.add_child(gate)
	for x in [-2.7, 2.7]:
		_box(gate, Vector3(0.3, 3.0, 0.3), Vector3(x, 1.5, 11.7), "#a58d6d", true)
	_box(gate, Vector3(5.8, 0.65, 0.3), Vector3(0, 3.0, 11.7), "#507563")
	_text(gate, "WELCOME · EXPLORE", Vector3(0, 3.02, 11.88), 33, "#fff4db", 0.007)
	for x in [-3.0, 3.0]:
		_box(scenery, Vector3(0.15, 3.6, 0.15), Vector3(x, 1.8, -12.9), "#a58d6d", true)
	var entrance := _text(scenery, "WELCOME
WASD / ARROWS · WALK
E · TALK WHEN NEAR", Vector3(0, 0.08, 11.7), 26, "#4e715b", 0.007)
	entrance.rotation_degrees.x = -90

func _window(at: Vector3, accent: String) -> void:
	_box(scenery, Vector3(2.35, 1.65, 0.1), at, "#b4d0cf")
	for x in [-1.2, 0.0, 1.2]:
		_box(scenery, Vector3(0.08, 1.85, 0.13), at + Vector3(x, 0, 0.08), "#e9d9b8")
	for y in [-0.9, 0.0, 0.9]:
		_box(scenery, Vector3(2.5, 0.08, 0.13), at + Vector3(0, y, 0.08), "#e9d9b8")
	_box(scenery, Vector3(2.6, 0.2, 0.16), at + Vector3(0, 1.04, 0.07), accent)

func _plant(parent: Node3D, at: Vector3, mature := true, kind := "tomato") -> void:
	if kind == "carrot":
		_round(parent, at + Vector3(0, 0.075, 0), 0.045, 0.15, "#d89a63", 0.095)
		for side in [-1, 0, 1]:
			var leaf := _oval(parent, at + Vector3(side * 0.06, 0.25, 0), Vector3(0.08, 0.25, 0.06), "#789459")
			leaf.rotation.z = side * -0.35
		return
	var height := 0.6 if mature else 0.32
	_round(parent, at + Vector3(0, height / 2, 0), 0.025, height, "#68844d")
	for side in [-1, 1]:
		var leaf := _oval(parent, at + Vector3(side * 0.13, height * 0.62, 0), Vector3(0.3, 0.1, 0.16), "#7e9d63")
		leaf.rotation.z = side * 0.35
	if mature:
		_oval(parent, at + Vector3(0, 0.57, 0), Vector3(0.25, 0.27, 0.25), "#cb8269")
		_oval(parent, at + Vector3(0.035, 0.74, 0), Vector3(0.13, 0.05, 0.12), "#63834c")

func _fence(x: float, z: float, width: float) -> void:
	for offset in [-width / 2, 0.0, width / 2]:
		_box(scenery, Vector3(0.14, 0.95, 0.16), Vector3(x + offset, 0.47, z), "#ddc6a0", true)
		_round(scenery, Vector3(x + offset, 0.99, z), 0.105, 0.12, "#eadab9", 0)
	for y in [0.34, 0.71]:
		_box(scenery, Vector3(width, 0.13, 0.09), Vector3(x, y, z), "#e7d5af", true)

func _build_farm() -> void:
	# Crop beds sit along the field edges; the middle stays walkable.
	for x in [-13.1, -6.8]:
		for z in [-8.0, -4.8]:
			_box(scenery, Vector3(2.2, 0.27, 1.7), Vector3(x, 0.13, z), "#9a7855", true)
			farm_soils.append(_box(scenery, Vector3(2.0, 0.04, 1.5), Vector3(x, 0.29, z), "#765a43"))
			for dx in [-0.65, 0.0, 0.65]:
				for dz in [-0.4, 0.4]:
					_plant(scenery, Vector3(x + dx, 0.31, z + dz), true, "carrot" if z == -8.0 else "tomato")
	_box(scenery, Vector3(2.8, 2.45, 1.6), Vector3(-6.7, 1.22, -10.3), "#b4bd8d", true)
	_box(scenery, Vector3(3.1, 0.18, 1.9), Vector3(-6.7, 2.54, -10.3), "#a4785d")
	_text(scenery, "TOOLS & SEEDS", Vector3(-6.7, 2.1, -9.45), 24, "#4d6a47", 0.005)
	_stall(Vector3(10, 0, -8.0), "#8fa78c", "FARM SHOP")
	for x in [7.2, 12.8]:
		_basket(scenery, Vector3(x, 0, -8.2), true)
	_box(scenery, Vector3(3.0, 0.95, 1.1), Vector3(10, 0.48, 4.0), "#b79770", true)
	for x in [9.2, 10.0, 10.8]:
		_basket(scenery, Vector3(x, 0.98, 4.0), true)
	for x in [6.6, 13.4]:
		_box(scenery, Vector3(1.0, 0.6, 0.9), Vector3(x, 0.3, 3.5), "#bf9667", true)
	_fence(-10, -11.6, 8.8)
	_fence(10, -11.6, 8.8)

func _produce(parent: Node3D, at: Vector3, colour: String) -> void:
	_oval(parent, at, Vector3(0.24, 0.22, 0.24), colour)
	_round(parent, at + Vector3(0, 0.13, 0), 0.018, 0.1, "#6f8554")
	_oval(parent, at + Vector3(0.055, 0.16, 0), Vector3(0.12, 0.04, 0.07), "#7f9c5b")

func _basket(parent: Node3D, at: Vector3, full := false) -> void:
	_box(parent, Vector3(0.75, 0.35, 0.53), at + Vector3(0, 0.2, 0), "#bf9667")
	_box(parent, Vector3(0.65, 0.04, 0.43), at + Vector3(0, 0.4, 0), "#9f784e")
	for y in [0.1, 0.21, 0.32]:
		_box(parent, Vector3(0.77, 0.025, 0.55), at + Vector3(0, y, 0), "#e5c28f")
	for x in [-0.25, 0.25]:
		_box(parent, Vector3(0.06, 0.36, 0.05), at + Vector3(x, 0.54, 0), "#b98c5d")
	_box(parent, Vector3(0.56, 0.06, 0.05), at + Vector3(0, 0.72, 0), "#b98c5d")
	if full:
		for x in [-0.2, 0.05, 0.23]:
			_produce(parent, at + Vector3(x, 0.43, -0.06), "#cd8668" if x < 0.1 else "#b6bd70")

func _stall(at: Vector3, accent: String, title: String) -> void:
	_box(scenery, Vector3(2.7, 0.92, 1.35), at + Vector3(0, 0.46, 0), "#c7a47b", true)
	for x in [-1.32, 1.32]:
		_round(scenery, at + Vector3(x, 1.5, 0.42), 0.055, 3, "#a78c69")
	for index in range(8):
		_box(scenery, Vector3(0.37, 0.13, 1.85), at + Vector3(-1.295 + index * 0.37, 2.95, -0.05), accent if index % 2 == 0 else "#f2e4c6")
		_box(scenery, Vector3(0.37, 0.27, 0.08), at + Vector3(-1.295 + index * 0.37, 2.78, 0.88), accent if index % 2 == 0 else "#f2e4c6")
	for x in [-0.85, 0.0, 0.85]:
		_box(scenery, Vector3(0.72, 0.19, 0.86), at + Vector3(x, 1.0, 0.05), "#a9855f")
		for z in [-0.19, 0.13]:
			for dx in [-0.17, 0.13]:
				_produce(scenery, at + Vector3(x + dx, 1.17, z), "#cc876b" if x < 0 else "#a6b66b" if x == 0 else "#e0b96d")
	_box(scenery, Vector3(1.8, 0.42, 0.07), at + Vector3(0, 0.62, 0.71), "#567761")
	_text(scenery, title, at + Vector3(0, 0.63, 0.76), 26, "#fff0cf", 0.004)

func _build_market() -> void:
	_stall(Vector3(-10, 0, -8.0), "#ca9474", "ORCHARD FRUIT")
	_stall(Vector3(10, 0, -8.0), "#8fa78c", "GARDEN GREENS")
	_stall(Vector3(10, 0, 4.0), "#b4a2b9", "CHECKOUT")
	for center in [-10, 10]:
		for x in [-3.1, 3.1]:
			_basket(scenery, Vector3(center + x, 0, -8.1), true)
		for index in range(7):
			var flag := _box(scenery, Vector3(0.5, 0.4, 0.04), Vector3(center - 3.0 + index, 3.9, -9.1), ["#d39b79", "#aab98a", "#c7b3c8"][index % 3])
			flag.rotation.z = 0.15 if index % 2 == 0 else -0.15
	_box(scenery, Vector3(1.5, 1.2, 0.2), Vector3(6.4, 0.6, 4.4), "#a88662", true)
	_text(scenery, "LOCAL & FRESH
Apples $3
Carrots $3", Vector3(6.4, 0.8, 4.53), 25, "#fff0cf", 0.005)
	_asset("furniture/pottedPlant", Vector3(14.6, 0, 9.7), 1.4)

func _restaurant_table(at: Vector3) -> void:
	_asset("kaykit/table_round_A_small.gltf", at, 0.94)
	_box(scenery, Vector3(1.28, 0.88, 1.28), at + Vector3(0, 0.44, 0), "#bd9d77", true).hide()
	for side in [-1, 1]:
		var chair := _asset("kaykit/chair_A.gltf", at + Vector3(side * 1.05, 0, 0), 0.9)
		chair.rotation.y = -side * PI / 2
		_box(scenery, Vector3(0.55, 0.8, 0.55), at + Vector3(side * 1.05, 0.4, 0), "#bd9d77", true).hide()
		_round(scenery, at + Vector3(side * 0.42, 0.96, 0), 0.21, 0.025, "#fcf0d9")
		_round(scenery, at + Vector3(side * 0.42, 0.978, 0), 0.16, 0.008, "#d7dac0")
	_asset("furniture/plantSmall1", at + Vector3(0, 0.96, 0), 0.29)

func _build_restaurant() -> void:
	_window(Vector3(-10, 2.5, -13.55), "#ba8b91")
	_window(Vector3(10, 2.5, -13.55), "#ba8b91")
	_box(scenery, Vector3(2.2, 1.1, 0.9), Vector3(-10, 0.55, -8.2), "#b49c79", true)
	_text(scenery, "WELCOME IN
TABLES FOR FRIENDS", Vector3(-10, 1.8, -8.1), 26, "#52735c", 0.005)
	for x in [6.7, 13.3]:
		for z in [-8.1, -3.9]:
			_round(scenery, Vector3(x, 0.014, z), 1.65, 0.018, "#c2cbb4")
			_restaurant_table(Vector3(x, 0, z))
	_box(scenery, Vector3(3.2, 1.05, 1.0), Vector3(10, 0.53, 4.0), "#b49c79", true)
	_box(scenery, Vector3(3.4, 0.1, 1.1), Vector3(10, 1.1, 4.0), "#eee0c4")
	_asset("kaykit/dishrack_plates.gltf", Vector3(9.1, 1.17, 4.0), 0.32)
	_asset("food/croissant", Vector3(10.2, 1.17, 4.0), 0.17)
	_asset("food/cup-tea", Vector3(10.75, 1.17, 4.0), 0.18)
	for at in [Vector3(-13.2, 0, -9), Vector3(-6.8, 0, -9), Vector3(6.5, 0, 3.4), Vector3(14.8, 0, 9.7)]:
		_asset("furniture/pottedPlant", at, 1.45)

func _process(delta: float) -> void:
	if friend_art:
		friend_art.animate(delta, reduce_motion)
	if not is_instance_valid(camera) or not is_instance_valid(player):
		return
	_update_camera(delta)
	var role := scenery.get_node_or_null("RoleSign") as Label3D
	if role:
		role.position = player.position + Vector3(0, 2.18, 0)
	if is_instance_valid(target_marker) and identities.has(target_npc_id):
		target_marker.text = "◆ TALK NEXT" + (" · E" if nearest_npc_id() == target_npc_id else "")

func _update_camera(delta: float, snap := false) -> void:
	var aim := player.position + Vector3(0, 0.6, 0)
	var offset := Vector3(7.2, 10, 12)
	if in_conversation and actors.has(focused_npc_id):
		aim = (player.position + actors[focused_npc_id].position) / 2 + Vector3(0, -0.9, 0)
		offset = Vector3(3.4, 6.3, 8.0)
	var destination := aim + offset
	camera.position = destination if snap or reduce_motion else camera.position.lerp(destination, 1.0 - exp(-delta * 5.0))
	camera.look_at(aim)

func nearest_npc_id() -> String:
	if not is_instance_valid(player):
		return ""
	var closest := ""
	var distance := TALK_DISTANCE
	for id in actors:
		var gap: float = Vector2(player.position.x, player.position.z).distance_to(Vector2(actors[id].position.x, actors[id].position.z))
		if gap < distance:
			closest = id
			distance = gap
	return closest

func near_npc() -> bool:
	return nearest_npc_id() != ""

func npc_data(id: String) -> Dictionary:
	return identities.get(id, {}).duplicate()

func set_target_npc(id: String) -> void:
	target_npc_id = id if actors.has(id) else ""
	target_marker.visible = target_npc_id != ""
	if target_marker.visible:
		target_marker.position = actors[id].position + Vector3(0, 2.7, 0)
		target_marker.text = "◆ TALK NEXT"
		npc = actors[id]
	elif not actors.is_empty():
		npc = actors.values()[0]

func focus_npc(id: String) -> void:
	if not actors.has(id):
		focused_npc_id = ""
		return
	walking_to_partner = false
	walk_points.clear()
	focused_npc_id = id
	npc = actors[id]
	var toward: Vector3 = npc.position - player.position
	hero.rotation.y = atan2(toward.x, toward.z)
	npc.rotation.y = atan2(-toward.x, -toward.z)

func current_zone() -> String:
	if not is_instance_valid(player):
		return "Entrance"
	for zone in route.zones:
		var center := _point(zone.center)
		if absf(player.position.x - center.x) <= zone.size[0] / 2.0 and absf(player.position.z - center.z) <= zone.size[1] / 2.0:
			return zone.label
	return "Entrance" if player.position.z > 4 else "Connecting path"

func walk_to_npc(id := "") -> void:
	var destination: String = id if actors.has(id) else target_npc_id
	if not identities.has(destination):
		return
	walk_points = _safe_route(identities[destination].approach)
	walking_to_partner = not walk_points.is_empty()
	blocked_seconds = 0.0

func _segment_clear(start: Vector3, end: Vector3) -> bool:
	var sphere := CapsuleShape3D.new()
	sphere.radius = 0.36
	sphere.height = 1.65
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = sphere
	query.transform.origin = start + Vector3(0, 0.85, 0)
	query.motion = end - start
	query.motion.y = 0
	query.exclude = [player.get_rid()]
	var fraction := get_world_3d().direct_space_state.cast_motion(query)
	return fraction[0] > 0.999

func _safe_route(destination: Vector3) -> Array[Vector3]:
	var graph := AStar3D.new()
	var points: Array = route.navigation.points
	for index in points.size():
		graph.add_point(index, _point(points[index]))
	for edge in route.navigation.links:
		if _segment_clear(_point(points[int(edge[0])]), _point(points[int(edge[1])])):
			graph.connect_points(int(edge[0]), int(edge[1]))
	var first := points.size()
	var last := first + 1
	graph.add_point(first, Vector3(player.position.x, 0, player.position.z))
	graph.add_point(last, destination)
	for index in points.size():
		var at := _point(points[index])
		if _segment_clear(player.position, at):
			graph.connect_points(first, index)
		if _segment_clear(at, destination):
			graph.connect_points(index, last)
	if _segment_clear(player.position, destination):
		graph.connect_points(first, last)
	var result: Array[Vector3] = []
	for at in graph.get_point_path(first, last):
		result.append(at)
	if not result.is_empty():
		result.pop_front()
	return result

func move_player(delta: float, paused: bool) -> void:
	if not is_instance_valid(player):
		return
	if paused:
		player.velocity = Vector3.ZERO
		hero.play("idle")
		return
	var direction := _manual_direction()
	var speed := WALK_SPEED
	if direction.length() > 0.1:
		walking_to_partner = false
		walk_points.clear()
	elif walking_to_partner:
		while not walk_points.is_empty() and player.position.distance_to(walk_points[0]) < 0.12:
			walk_points.pop_front()
		if walk_points.is_empty():
			walking_to_partner = false
		else:
			direction = walk_points[0] - player.position
			direction.y = 0
			speed = minf(speed, direction.length() / maxf(get_physics_process_delta_time(), 0.001))
	if direction.length() > 0.01:
		hero.rotation.y = lerp_angle(hero.rotation.y, atan2(direction.x, direction.z), minf(delta * 10, 1))
		hero.play("walk")
	else:
		hero.play("idle")
	player.velocity.x = direction.normalized().x * speed
	player.velocity.z = direction.normalized().z * speed
	player.velocity.y = 0 if player.is_on_floor() else player.velocity.y - 18 * delta
	var before := player.position
	player.move_and_slide()
	if walking_to_partner:
		blocked_seconds = blocked_seconds + delta if before.distance_to(player.position) < 0.003 else 0.0
		if blocked_seconds > 0.7:
			walking_to_partner = false
			walk_points.clear()

func _manual_direction() -> Vector3:
	var direction := Vector3.ZERO
	direction.x = float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT))
	direction.z = float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP))
	return direction

func apply_action(action: String, step_id: String = "", caption: String = "") -> void:
	var identity := "Action_" + action + ("_" + step_id.validate_node_name() if not step_id.is_empty() else "")
	if not ACTIONS.has(action) or not is_instance_valid(action_props) or action_props.has_node(identity):
		return
	var prop := Node3D.new()
	prop.name = identity
	var zone_index := 0
	for index in route.zones.size():
		if route.zones[index].id == target_npc_id:
			zone_index = index
	if step_id.begins_with("step_") and step_id.trim_prefix("step_").is_valid_int():
		zone_index = clampi(int(step_id.trim_prefix("step_")), 0, 2)
	var slot := action_props.get_child_count() % 3
	prop.position = _display_position(zone_index) + Vector3(0, 0.93, -0.4 + slot * 0.4)
	action_props.add_child(prop)
	var result := ""
	var meaning := caption.to_lower()
	match action:
		"water":
			_round(prop, Vector3(0, 0.2, 0), 0.18, 0.32, "#8caeb5")
			var spout := _round(prop, Vector3(0.24, 0.25, 0), 0.045, 0.35, "#8caeb5")
			spout.rotation.z = -PI / 3
			_round(prop, Vector3(0.4, 0.34, 0), 0.08, 0.035, "#b9ced0").rotation.z = -PI / 3
			_box(prop, Vector3(0.08, 0.3, 0.05), Vector3(-0.24, 0.25, 0), "#8caeb5")
			_box(prop, Vector3(0.23, 0.05, 0.05), Vector3(-0.15, 0.41, 0), "#8caeb5")
			for index in range(3):
				_oval(prop, Vector3(0.43 + index * 0.03, 0.23 - index * 0.07, 0), Vector3(0.045, 0.07, 0.045), "#92c5cd")
			_water_crops(prop)
			result = "Plants watered"
		"harvest":
			if meaning.contains("carrot"):
				for x in [-0.13, 0.13]:
					_carrot(prop, Vector3(x, 0.06, 0))
			else:
				_basket(prop, Vector3(0, 0, 0), false)
				for x in [-0.21, 0.0, 0.21]:
					_produce(prop, Vector3(x, 0.43, -0.06), "#c57d65")
			prop.scale = Vector3.ONE * 0.7
			result = "Harvest gathered"
		"basket":
			if meaning.contains("bag"):
				var bag := _box(prop, Vector3(0.48, 0.49, 0.32), Vector3(0, 0.27, 0), "#d3b17e")
				bag.name = "PaperBag"
				_box(prop, Vector3(0.4, 0.018, 0.25), Vector3(0, 0.525, 0), "#9a7951")
				for x in [-0.12, 0.12]:
					_box(prop, Vector3(0.035, 0.19, 0.035), Vector3(x, 0.61, 0), "#c09e70")
				_box(prop, Vector3(0.275, 0.035, 0.035), Vector3(0, 0.7, 0), "#c09e70")
				_text(prop, "FRESH", Vector3(0, 0.29, 0.17), 23, "#61794e", 0.004)
			elif meaning.contains("egg"):
				_box(prop, Vector3(0.7, 0.13, 0.43), Vector3(0, 0.08, 0), "#b9b496")
				for index in range(6):
					var egg := _oval(prop, Vector3(-0.23 + (index % 3) * 0.23, 0.22, -0.11 + (index / 3) * 0.22), Vector3(0.19, 0.27, 0.19), "#f5e5c9")
					egg.name = "Egg" + str(index + 1)
			else:
				_basket(prop, Vector3(0, 0, 0), false)
				var quantity := 2 if meaning.contains("two apples") else 3
				for index in quantity:
					var fruit := Node3D.new()
					fruit.name = ("Tomato" if meaning.contains("tomato") else "Apple") + str(index + 1)
					prop.add_child(fruit)
					var x: float = (index - (quantity - 1) / 2.0) * 0.21
					_produce(fruit, Vector3(x, 0.43, -0.06), "#c57d65" if meaning.contains("tomato") else "#c88769")
			prop.scale = Vector3.ONE * 0.7
			result = "Basket packed"
		"pay":
			if meaning.contains("bill"):
				_box(prop, Vector3(0.36, 0.51, 0.025), Vector3(0, 0.26, 0), "#fff3d9")
				var bill := _text(prop, "BILL\n$10" if meaning.contains("ten") else "YOUR\nBILL", Vector3(0, 0.3, 0.02), 24, "#4d7b5c", 0.004)
				bill.name = "BillReceipt"
				result = "Bill ready"
			elif meaning.contains("card"):
				_box(prop, Vector3(0.5, 0.31, 0.035), Vector3(0, 0.19, 0), "#82a391")
				_box(prop, Vector3(0.44, 0.055, 0.015), Vector3(0, 0.23, 0.024), "#486b5c")
				_box(prop, Vector3(0.085, 0.075, 0.015), Vector3(-0.14, 0.13, 0.024), "#dec27c")
				result = "Card accepted"
			else:
				_box(prop, Vector3(0.4, 0.07, 0.28), Vector3(0, 0.06, 0), "#6e8f78")
				_box(prop, Vector3(0.3, 0.015, 0.18), Vector3(0, 0.104, 0), "#e4e1b1")
				for index in range(4):
					_round(prop, Vector3(0.22, 0.05 + index * 0.035, 0.08), 0.085, 0.03, "#d5b669")
				_box(prop, Vector3(0.19, 0.35, 0.02), Vector3(-0.1, 0.25, 0), "#fff3d9")
				_text(prop, "PAID", Vector3(-0.1, 0.28, 0.018), 19, "#4d7b5c", 0.0022)
				result = "Payment complete"
		"meal":
			_round(prop, Vector3(0, 0.035, 0), 0.32, 0.055, "#fff2d8")
			var bowl := _round(prop, Vector3(0, 0.15, 0), 0.16, 0.2, "#e4e9d8", 0.27)
			bowl.name = "SoupBowl"
			_round(prop, Vector3(0, 0.254, 0), 0.235, 0.012, "#d5a866")
			for index in range(5):
				var angle: float = index * TAU / 5
				_oval(prop, Vector3(cos(angle) * 0.13, 0.267, sin(angle) * 0.13), Vector3(0.06, 0.025, 0.06), "#8ba56b")
			_round(prop, Vector3(0.34, 0.08, 0), 0.025, 0.37, "#a2aaa0").rotation.x = PI / 2
			result = "Meal served"
		"table":
			_box(prop, Vector3(0.55, 0.34, 0.04), Vector3(0, 0.18, 0), "#d4b38b")
			_box(prop, Vector3(0.49, 0.27, 0.05), Vector3(0, 0.19, 0.008), "#fff0d4")
			_text(prop, "RESERVED\nTABLE 2", Vector3(0, 0.2, 0.04), 20, "#597356", 0.0024)
			_round(prop, Vector3(0, 0.025, 0), 0.23, 0.04, "#b38c65")
			result = "Table reserved"
		"outfit", "price", "bags", "ride", "tickets", "snack", "meet":
			result = friend_art.apply_action(action, prop, meaning, step_id)
	var label := _text(prop, _wrap_caption(caption if not caption.is_empty() else result), Vector3(0, 0.88, 0), 23, "#355c47", 0.005)
	label.name = "ActionCaption"
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.outline_size = 4
	label.outline_modulate = Color("#fff4dc")
	if not reduce_motion:
		var final_scale := prop.scale
		prop.scale *= 0.7
		var tween := create_tween()
		tween.tween_property(prop, "scale", final_scale, 0.4).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		action_tweens.append(tween)

func _wrap_caption(words: String) -> String:
	var lines := PackedStringArray()
	var line := ""
	for word in words.strip_edges().left(64).split(" ", false):
		if line.length() + word.length() > 14 and not line.is_empty():
			lines.append(line)
			line = ""
		line += (" " if not line.is_empty() else "") + word
	if not line.is_empty():
		lines.append(line)
	return "\n".join(lines)

func _carrot(parent: Node3D, at: Vector3) -> void:
	var carrot := Node3D.new()
	parent.add_child(carrot)
	carrot.position = at
	carrot.rotation.z = -1.05
	_round(carrot, Vector3(0, 0.2, 0), 0.015, 0.38, "#d6955e", 0.09)
	for side in [-1, 0, 1]:
		var leaf := _oval(carrot, Vector3(side * 0.055, 0.5, 0), Vector3(0.065, 0.23, 0.06), "#79945a")
		leaf.rotation.z = side * -0.35

func _water_crops(prop: Node3D) -> void:
	if scene_id != "farm":
		return
	for soil in farm_soils:
		soil.material_override = _paint("#594d3f")
	# Small water glints sit on a real crop bed, in addition to the can.
	var drops := Node3D.new()
	drops.name = "WateredPlants"
	prop.add_child(drops)
	drops.position = Vector3(-13.1, 0.34, -8.0) - prop.position
	for index in range(6):
		var drop := _oval(drops, Vector3(-0.8 + (index % 3) * 0.8, 0.015, -0.4 + (index / 3) * 0.8), Vector3(0.15, 0.025, 0.11), "#9bbfc1")
		if not reduce_motion:
			var end := drop.position
			drop.position.y += 0.7
			var tween := create_tween()
			tween.tween_interval(index * 0.06)
			tween.tween_property(drop, "position", end, 0.45).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
			action_tweens.append(tween)

func reset_actions() -> void:
	_stop_action_tweens()
	if friend_art:
		friend_art.reset_ambient()
	for soil in farm_soils:
		soil.material_override = _paint("#765a43")
	if is_instance_valid(action_props):
		for child in action_props.get_children():
			action_props.remove_child(child)
			child.queue_free()
	walking_to_partner = false
	walk_points.clear()
	in_conversation = false
	if is_instance_valid(player):
		player.position = SPAWN
		player.velocity = Vector3.ZERO
		hero.rotation.y = PI
		hero.play("idle")

func _stop_action_tweens() -> void:
	for tween in action_tweens:
		if tween.is_valid():
			tween.kill()
	action_tweens.clear()

func _exit_tree() -> void:
	_stop_action_tweens()
