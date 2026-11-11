extends Node3D

# Original conservatory geometry. The complete collision envelope stays in
# world.gd; these frames, panes and lanterns are deliberately visual only.
const Decor = preload("cafe_decor.gd")
const TIMBER := "#544737"
static var glass_finish: StandardMaterial3D
static var brass_finish: StandardMaterial3D
static var lantern_finish: StandardMaterial3D
static var pane_mesh: QuadMesh
var timber: Array[Transform3D] = []
var brass: Array[Transform3D] = []
var plinth: Array[Transform3D] = []

func build() -> void:
	if get_child_count() != 0:
		return
	_build_glazing()
	_build_rear_roof()
	_build_counter_joinery()
	_build_lanterns()
	_group("ConservatoryTimber", timber, Decor.surface("oak", TIMBER))
	_group("ConservatoryBrass", brass, _brass())
	_group("ConservatoryPlinth", plinth, Decor.surface("plaster", "#78816c"))

func _block(group: Array[Transform3D], at: Vector3, size: Vector3) -> void:
	group.append(Transform3D(Basis.from_scale(size), at))

func _beam(group: Array[Transform3D], start: Vector3, end: Vector3, width: float, depth: float) -> void:
	var up := (end - start).normalized()
	var guide := Vector3.FORWARD if absf(up.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT
	var right := up.cross(guide).normalized()
	group.append(Transform3D(Basis(right * width, up * start.distance_to(end), right.cross(up) * depth), (start + end) / 2))

func _group(title: String, placements: Array[Transform3D], paint: Material) -> void:
	var node := MultiMeshInstance3D.new()
	node.name = title
	node.multimesh = MultiMesh.new()
	node.multimesh.transform_format = MultiMesh.TRANSFORM_3D
	node.multimesh.mesh = Decor.box_mesh(Vector3.ONE)
	node.multimesh.instance_count = placements.size()
	node.material_override = paint
	add_child(node)
	var bounds := AABB()
	for index in placements.size():
		node.multimesh.set_instance_transform(index, placements[index])
		var part: AABB = placements[index] * node.multimesh.mesh.get_aabb()
		bounds = part if index == 0 else bounds.merge(part)
	node.custom_aabb = bounds.grow(0.01)

func _build_glazing() -> void:
	# Three single-layer panes reveal the real forest beyond the old boundary.
	_pane("RearGardenGlass", Vector2(14.6, 3.48), Vector3(0, 2.61, -5.8))
	_pane("LeftGardenGlass", Vector2(5.5, 3.48), Vector3(-7.4, 2.61, -3.05), PI / 2)
	_pane("RightGardenGlass", Vector2(4.5, 3.48), Vector3(7.4, 2.61, -3.55), -PI / 2)
	# Wide structural bays, with slender mullions behind the counter and plants.
	for x in [-7.3, -3.4, 3.4, 7.3]:
		_block(timber, Vector3(x, 2.22, -5.68), Vector3(0.16, 4.44, 0.19))
	for x in [-5.4, -1.7, 0.0, 1.7, 5.4]:
		_block(timber, Vector3(x, 2.61, -5.68), Vector3(0.055, 3.48, 0.11))
	for y in [0.85, 2.5, 4.4]:
		_block(timber, Vector3(0, y, -5.68), Vector3(14.72, 0.085, 0.19))
	_block(plinth, Vector3(0, 0.39, -5.72), Vector3(14.7, 0.78, 0.16))
	for side in [-1.0, 1.0]:
		var rear_end := -0.3 if side < 0 else -1.3
		var length := rear_end + 5.8
		var centre := (rear_end - 5.8) / 2
		for z in [-5.68, -3.0, rear_end]:
			_block(timber, Vector3(side * 7.3, 2.22, z), Vector3(0.19, 4.44, 0.16))
		for y in [0.85, 2.5, 4.4]:
			_block(timber, Vector3(side * 7.3, y, centre), Vector3(0.19, 0.085, length))
		_block(plinth, Vector3(side * 7.33, 0.39, centre), Vector3(0.16, 0.78, length))
		# The camera-facing wall is cut to a low sill; its old collider stays full.
		var front_length := 6.0 - rear_end
		var front_centre := (6.0 + rear_end) / 2
		_block(plinth, Vector3(side * 7.33, 0.27, front_centre), Vector3(0.16, 0.54, front_length))
		_block(timber, Vector3(side * 7.3, 0.58, front_centre), Vector3(0.22, 0.075, front_length))

func _build_rear_roof() -> void:
	# Only the rear truss and short eaves remain visible. The front roof is open
	# so neither overview nor dialogue cameras look through a canopy of rafters.
	var ridge := Vector3(0, 6.15, -5.68)
	_beam(timber, Vector3(-7.3, 4.4, -5.68), ridge, 0.20, 0.22)
	_beam(timber, ridge, Vector3(7.3, 4.4, -5.68), 0.20, 0.22)
	_beam(timber, Vector3(0, 4.4, -5.68), ridge, 0.13, 0.16)
	for side in [-1.0, 1.0]:
		_beam(timber, Vector3(side * 3.4, 4.4, -5.68), Vector3(side * 3.4, 5.34, -5.68), 0.10, 0.13)
		_beam(timber, Vector3(side * 7.3, 4.4, -5.68), Vector3(side * 7.3, 4.4, -1.6), 0.18, 0.20)
		_beam(timber, Vector3(side * 7.3, 3.72, -3.0), Vector3(side * 7.3, 4.4, -3.7), 0.13, 0.15)
	_beam(timber, ridge, Vector3(0, 6.15, -2.8), 0.16, 0.18)

func _build_counter_joinery() -> void:
	# Shallow applied timber boards leave the existing counter top/props intact.
	for index in 21:
		_block(timber, Vector3(-3.0 + index * 0.3, 0.57, -0.875), Vector3(0.014, 0.67, 0.018))
	_block(brass, Vector3(0, 1.105, -0.875), Vector3(6.35, 0.018, 0.025))

func _build_lanterns() -> void:
	for at in [Vector3(-6.8, 2.88, -5.36), Vector3(6.8, 2.88, -5.36), Vector3(-7.05, 2.65, -0.45)]:
		_block(brass, at + Vector3(0, 0.275, 0), Vector3(0.32, 0.06, 0.29))
		_block(brass, at + Vector3(0, -0.25, 0), Vector3(0.30, 0.05, 0.27))
		_block(brass, at + Vector3(0, 0.4, 0), Vector3(0.035, 0.23, 0.035))
		for x in [-0.12, 0.12]:
			for z in [-0.10, 0.10]:
				_block(brass, at + Vector3(x, 0, z), Vector3(0.023, 0.49, 0.023))
		var glow := MeshInstance3D.new()
		glow.name = "WarmLantern"
		glow.mesh = Decor.box_mesh(Vector3.ONE)
		glow.scale = Vector3(0.16, 0.32, 0.14)
		glow.position = at
		glow.material_override = _lantern()
		glow.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(glow)

func _pane(title: String, size: Vector2, at: Vector3, turn := 0.0) -> void:
	if pane_mesh == null:
		pane_mesh = QuadMesh.new()
		pane_mesh.size = Vector2.ONE
	var node := MeshInstance3D.new()
	node.name = title
	node.mesh = pane_mesh
	node.scale = Vector3(size.x, size.y, 1)
	node.position = at
	node.rotation.y = turn
	node.material_override = _glass()
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(node)

static func _glass() -> StandardMaterial3D:
	# Semantic resources are separate from Decor.finish's opaque color cache.
	if glass_finish == null:
		glass_finish = StandardMaterial3D.new()
		glass_finish.albedo_color = Color(0.64, 0.77, 0.69, 0.075)
		glass_finish.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		glass_finish.cull_mode = BaseMaterial3D.CULL_DISABLED
		glass_finish.roughness = 0.18
		glass_finish.metallic_specular = 0.3
	return glass_finish

static func _brass() -> StandardMaterial3D:
	if brass_finish == null:
		brass_finish = StandardMaterial3D.new()
		brass_finish.albedo_color = Color("#b6965c")
		brass_finish.metallic = 0.38
		brass_finish.roughness = 0.42
	return brass_finish

static func _lantern() -> StandardMaterial3D:
	if lantern_finish == null:
		lantern_finish = StandardMaterial3D.new()
		lantern_finish.albedo_color = Color("#e7c88b")
		lantern_finish.emission_enabled = true
		lantern_finish.emission = Color("#ffd990")
		lantern_finish.emission_energy_multiplier = 0.65
		lantern_finish.roughness = 0.6
	return lantern_finish
