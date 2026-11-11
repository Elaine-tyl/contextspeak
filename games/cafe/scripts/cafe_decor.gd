extends RefCounted

const PALETTE = preload("cafe_palette.gdshader")
const SURFACE = preload("cafe_surface.gdshader")
const Ambience = preload("cafe_ambience.gd")
static var finishes: Dictionary = {}
static var geometry: Dictionary = {}

static func finish(colour: String) -> StandardMaterial3D:
	if not finishes.has(colour):
		var paint := StandardMaterial3D.new()
		paint.albedo_color = Color(colour)
		paint.roughness = 0.86
		finishes[colour] = paint
	return finishes[colour]

static func surface(kind: String, colour: String) -> ShaderMaterial:
	var key := kind + colour
	if not finishes.has(key):
		var paint := ShaderMaterial.new()
		paint.shader = SURFACE
		paint.set_shader_parameter("colour", Color(colour))
		paint.set_shader_parameter("finish", {"oak": 0, "floor": 1, "plaster": 2, "fabric": 3}.get(kind, 0))
		finishes[key] = paint
	return finishes[key]

static func box_mesh(size: Vector3) -> BoxMesh:
	var key := str(size)
	if not geometry.has(key):
		var shape := BoxMesh.new()
		shape.size = size
		geometry[key] = shape
	return geometry[key]

static func recolour(model: Node3D, file: String) -> void:
	# A shared material per atlas/finish keeps repeated counter/chair surfaces cheap.
	# Headless checks inspect resource counts; WebGL QA verifies actual shading.
	var upholstered := "chair" in file
	for mesh in model.find_children("*", "MeshInstance3D", true, false):
		for surface_index in mesh.mesh.get_surface_count():
			var original = mesh.get_active_material(surface_index)
			if not original is StandardMaterial3D:
				continue
			if file.begins_with("furniture/"):
				# The furniture pack's named surfaces have no atlas to remap.
				match original.resource_name:
					"wood": mesh.set_surface_override_material(surface_index, surface("oak", "#9d846c"))
					"woodDark": mesh.set_surface_override_material(surface_index, surface("oak", "#684e3b"))
					"plant": mesh.set_surface_override_material(surface_index, finish("#6f8c79"))
					"metal", "metalMedium": mesh.set_surface_override_material(surface_index, finish("#9fa8a0"))
					"carpetWhite": mesh.set_surface_override_material(surface_index, finish("#d8d1c3"))
					"carpetDarker": mesh.set_surface_override_material(surface_index, finish("#b96f53"))
					"lamp": mesh.set_surface_override_material(surface_index, finish("#f8ebcb"))
				continue
			if original.albedo_texture == null:
				continue
			var key := "atlas_" + str(original.albedo_texture.get_rid()) + str(upholstered)
			if not finishes.has(key):
				var paint := ShaderMaterial.new()
				paint.shader = PALETTE
				paint.set_shader_parameter("base_texture", original.albedo_texture)
				paint.set_shader_parameter("accent", Color("#9d846c"))
				paint.set_shader_parameter("sage_colour", Color("#6f8c79"))
				paint.set_shader_parameter("upholstery", upholstered)
				finishes[key] = paint
			mesh.set_surface_override_material(surface_index, finishes[key])

static func round_piece(scene: Node3D, at: Vector3, radius: float, height: float, colour: String) -> MeshInstance3D:
	var key := "round_%s_%s" % [radius, height]
	if not geometry.has(key):
		var shape := CylinderMesh.new()
		shape.top_radius = radius
		shape.bottom_radius = radius
		shape.height = height
		shape.radial_segments = 24
		geometry[key] = shape
	var node := MeshInstance3D.new()
	node.mesh = geometry[key]
	node.material_override = finish(colour)
	node.position = at
	scene.add_child(node)
	return node

static func build(scene) -> void:
	# Composition around three anchors: the counter, welcome area and friend table.
	# Timber boards and a brass rail are supplied by ForestConservatory; the
	# original counter silhouette, surface height and all serving props stay put.
	scene.box(Vector3(6.35, 0.68, 0.065), Vector3(0, 0.57, -0.925), "#82674c").material_override = surface("oak", "#82674c")
	scene.box(Vector3(6.4, 0.07, 0.09), Vector3(0, 0.18, -0.90), "#684e3b")
	scene.box(Vector3(6.45, 0.08, 0.11), Vector3(0, 1.16, -0.90), "#9d846c").material_override = surface("oak", "#9d846c")
	for x in [-2, 0, 2]:
		round_piece(scene, Vector3(x, 3.45, -1.8), 0.012, 0.7, "#684e3b")
		round_piece(scene, Vector3(x, 3.72, -1.8), 0.08, 0.04, "#684e3b")
		if not geometry.has("pendant_dome"):
			var dome := SphereMesh.new()
			dome.radius = 0.30
			dome.height = 0.56
			dome.is_hemisphere = true
			dome.radial_segments = 24
			dome.rings = 8
			geometry["pendant_dome"] = dome
		var shade := MeshInstance3D.new()
		shade.name = "PendantShade"
		shade.position = Vector3(x, 2.94, -1.8)
		shade.mesh = geometry["pendant_dome"]
		shade.material_override = finish("#b96f53")
		scene.add_child(shade)
		var underside := round_piece(scene, Vector3(x, 2.94, -1.8), 0.27, 0.015, "#f8ebcb")
		underside.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		var glow := OmniLight3D.new()
		glow.position = Vector3(x, 2.85, -1.8)
		glow.light_color = Color("#fff0dc")
		glow.light_energy = 0.18
		glow.omni_range = 2.65
		glow.omni_attenuation = 1.6
		glow.shadow_enabled = false
		scene.add_child(glow)
	round_piece(scene, Vector3(4.6, 0.012, -0.3), 1.65, 0.018, "#6f8c79").material_override = surface("fabric", "#6f8c79")
	round_piece(scene, Vector3(4.6, 0.023, -0.3), 1.52, 0.008, "#d8d6b8").material_override = surface("fabric", "#d8d6b8")
	scene.box(Vector3(2.5, 0.018, 1.15), Vector3(0, 0.012, 3.9), "#6f8c79").material_override = surface("fabric", "#6f8c79")
	for x in [-1.12, 1.12]:
		scene.box(Vector3(0.025, 0.006, 1), Vector3(x, 0.024, 3.9), "#d8d1c3")
	# A small welcome sign stays outside the two automatic walking routes.
	scene.box(Vector3(1.15, 1.25, 0.10), Vector3(-2.8, 0.95, 2.5), "#9d846c").material_override = surface("oak", "#9d846c")
	scene.box(Vector3(1.02, 1.12, 0.025), Vector3(-2.8, 0.95, 2.565), "#d8d1c3")
	var welcome = scene.sign_text("AFTER CLASS\nCoffee & good company\nMake yourself at home", Vector3(-2.8, 0.98, 2.59), 24)
	welcome.pixel_size = 0.0038
	welcome.modulate = Color("#334d43")
	for x in [-3.21, -2.39]:
		scene.box(Vector3(0.07, 0.85, 0.08), Vector3(x, 0.42, 2.5), "#9d846c")
	# Upholstered window bench and its timber base.
	scene.box(Vector3(0.62, 0.18, 2.3), Vector3(-6.8, 0.48, -0.6), "#6f8c79").material_override = surface("fabric", "#6f8c79")
	scene.box(Vector3(0.15, 0.64, 2.3), Vector3(-7.02, 0.78, -0.6), "#91a38a").material_override = surface("fabric", "#91a38a")
	for z in [-1.45, 0.25]:
		scene.box(Vector3(0.48, 0.44, 0.07), Vector3(-6.8, 0.22, z), "#9d846c")
	_build_ambient_plant(scene)

static func _build_ambient_plant(scene) -> void:
	# One compact terracotta planter sits on a window sill, away from both paths.
	scene.box(Vector3(2.8, 0.10, 0.40), Vector3(5.1, 1.0, -5.4), "#9d846c").material_override = surface("oak", "#9d846c")
	round_piece(scene, Vector3(5.75, 1.17, -5.34), 0.15, 0.25, "#b96f53")
	var ambience := Ambience.new()
	ambience.name = "CafeAmbience"
	scene.add_child(ambience)
	var foliage := Node3D.new()
	foliage.position = Vector3(5.75, 1.28, -5.34)
	ambience.add_child(foliage)
	for index in 5:
		var leaf := round_piece(foliage, Vector3(sin(index * 2.4) * 0.12, 0.10 + index * 0.035, cos(index * 2.4) * 0.08), 0.09, 0.018, "#6f8c79")
		leaf.rotation = Vector3(0.5, index * 2.4, 0.5)
		leaf.scale = Vector3(0.7, 1, 1.8)
		leaf.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
