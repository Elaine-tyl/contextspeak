extends SceneTree

# A misplaced planter, transparent leaf material, unique per-leaf meshes or
# continuously moving reduced-motion canopy would violate the playable scene.
const Decor = preload("../scripts/cafe_decor.gd")
var failures := 0

func _initialize() -> void:
	call_deferred("run")

func expect(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func mesh_triangles(mesh: Mesh) -> int:
	var count := 0
	for surface in mesh.get_surface_count():
		var arrays: Array = mesh.surface_get_arrays(surface)
		count += arrays[Mesh.ARRAY_INDEX].size() / 3 if arrays[Mesh.ARRAY_INDEX] != null else arrays[Mesh.ARRAY_VERTEX].size() / 3
	return count

func winding_matches_normals(mesh: ArrayMesh) -> bool:
	for surface in mesh.get_surface_count():
		var arrays: Array = mesh.surface_get_arrays(surface)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		for index in range(0, indices.size(), 3):
			var a: int = indices[index]
			var b: int = indices[index + 1]
			var c: int = indices[index + 2]
			var clockwise := (vertices[c] - vertices[a]).cross(vertices[b] - vertices[a])
			if clockwise.dot(normals[a] + normals[b] + normals[c]) <= 0:
				return false
	return true

func run() -> void:
	if not ResourceLoader.exists("res://scripts/forest_garden.gd"):
		expect(false, "The forest garden helper must exist before the world can build botanical geometry.")
		quit(1)
		return
	var Garden = load("res://scripts/forest_garden.gd")
	var garden = Garden.new()
	root.add_child(garden)
	garden.build()
	await physics_frame
	var built_count: int = garden.get_child_count()
	garden.build()
	expect(garden.get_child_count() == built_count, "Repeated build calls must not duplicate the garden or colliders.")
	var right_ground = garden.get_node_or_null("RightGardenGround")
	expect(right_ground != null, "The friend-table glazing must look onto garden ground rather than an empty sky gap.")
	if right_ground:
		var bounds: AABB = right_ground.global_transform * right_ground.get_aabb()
		expect(bounds.position.x > 7.5 and bounds.end.x >= 25 and bounds.end.z >= 6, "The right garden continues beyond the window view without exposing a floating terrain edge.")
	var right_tree := false
	for node in garden.find_children("OutsideTree*", "MeshInstance3D", true, false):
		right_tree = right_tree or node.position.x > 12
		if node.position.x > 12:
			var crown_height: float = node.get_aabb().end.y * node.scale.y * 0.85
			expect(crown_height > 0.8 and crown_height < 2, "Distant right-window foliage must sit below the seated camera's header sightline.")
	expect(right_tree, "The seated-friend camera needs tree silhouettes through the far-right glazing.")
	var triangles := 0
	var mesh_nodes := 0
	var resources := {}
	var materials := {}
	for node in garden.find_children("*", "MeshInstance3D", true, false):
		mesh_nodes += 1
		triangles += mesh_triangles(node.mesh)
		resources[node.mesh.get_rid()] = true
		if node.mesh is ArrayMesh:
			expect(winding_matches_normals(node.mesh), "Clockwise front faces must agree with the original geometry's outward normals: " + str(node.name))
		if node.material_override:
			materials[node.material_override.get_rid()] = true
		var bounds: AABB = node.global_transform * node.get_aabb()
		var exterior: bool = bounds.end.z < -6.2 or bounds.end.x < -7.6 or bounds.position.x > 7.6
		expect(exterior or bounds.end.x < -0.7, "Interior tree geometry must stay left of the counter and friend camera rays.")
	var leaves := 0
	var grouped: Array = garden.find_children("*", "MultiMeshInstance3D", true, false)
	for node in grouped:
		mesh_nodes += 1
		var mesh: Mesh = node.multimesh.mesh
		resources[mesh.get_rid()] = true
		expect(winding_matches_normals(mesh), "Folded leaf normals must agree with their clockwise front/back faces.")
		materials[node.material_override.get_rid()] = true
		triangles += mesh_triangles(mesh) * node.multimesh.instance_count
		leaves += node.multimesh.instance_count
		expect(mesh is ArrayMesh and mesh_triangles(mesh) <= 12, "Leaves must use small original pointed geometry, not spherical canopy clouds.")
		var leaf_size: Vector3 = mesh.get_aabb().size
		expect(leaf_size.x >= leaf_size.z * 0.65, "The leaf lamina must be broad enough to make layered canopy mass instead of thin spikes.")
		var paint: StandardMaterial3D = node.material_override
		expect(paint.transparency == BaseMaterial3D.TRANSPARENCY_DISABLED and paint.albedo_color.a == 1.0, "Grouped leaves remain opaque to avoid canopy alpha overdraw.")
		var batch_bounds: AABB = node.global_transform * node.custom_aabb
		if node.name.begins_with("Hero"):
			expect(batch_bounds.position.y >= 4.18 and batch_bounds.end.y <= 6.52 and batch_bounds.end.x < -0.7, "Hero canopy must stay high and left to preserve NPC visibility.")
		else:
			expect(batch_bounds.end.z < -6.2 or batch_bounds.end.x < -7.6, "Outdoor foliage and vines stay behind glazing or on the left frame.")
		# Godot's dummy renderer has no MultiMesh transform buffer. In a real GL
		# run, independently verify every rendered leaf lies inside its batch.
		if DisplayServer.get_name() != "headless":
			for index in node.multimesh.instance_count:
				var bounds: AABB = node.multimesh.get_instance_transform(index) * mesh.get_aabb()
				expect(node.custom_aabb.encloses(bounds), "The real rendered leaf transforms must fit their precomputed culling bounds.")
	expect(mesh_nodes < 100 and triangles <= 12000, "Botanical geometry must remain below 100 meshes and 12,000 drawn triangles.")
	expect(grouped.size() <= 6 and leaves >= 1000 and leaves <= 1300, "Lush layered canopies must contain 1,000–1,300 leaves batched into at most six groups.")
	expect(materials.size() <= 10 and resources.size() <= 12, "The garden must reuse a small set of materials and geometry.")
	expect(garden.find_children("*", "Light3D", true, false).is_empty(), "The garden must not add lights.")
	var colliders: Array = garden.find_children("*", "CollisionShape3D", true, false)
	expect(colliders.size() <= 1, "Only the optional tree planter may add collision.")
	for collider in colliders:
		expect(collider.shape is CylinderShape3D, "The tree planter uses one bounded simple cylinder collider.")
		if collider.shape is CylinderShape3D:
			expect(collider.shape.radius <= 1.15 and collider.shape.height <= 0.6, "The planter collider fits its small bench footprint.")
			expect(Vector2(collider.global_position.x, collider.global_position.z).distance_to(Vector2(-5.8, -3.6)) < 0.02, "The sole garden collider stays at the specified tree anchor.")
	var query := PhysicsShapeQueryParameters3D.new()
	var body := CapsuleShape3D.new()
	body.radius = 0.3
	body.height = 1.6
	query.shape = body
	for route in [[Vector3(0, 0.9, 4), Vector3(0, 0.9, -0.7)], [Vector3(0, 0.9, -0.7), Vector3(3.45, 0.9, -0.3)]]:
		for step in 20:
			query.transform = Transform3D(Basis.IDENTITY, route[0].lerp(route[1], step / 19.0))
			expect(garden.get_world_3d().direct_space_state.intersect_shape(query).is_empty(), "The original entrance, counter and friend routes must remain clear.")
	var resting := {}
	var instances := {}
	for node in grouped:
		resting[node] = node.transform
		instances[node] = node.multimesh.get_instance_transform(0)
	garden.advance(1.0, false)
	var moving := false
	for node in grouped:
		moving = moving or node.transform != resting[node]
		expect(node.multimesh.get_instance_transform(0) == instances[node], "Ambient sway must not rewrite individual leaf transforms every frame.")
	expect(moving, "Normal motion includes subtle grouped foliage movement.")
	garden.advance(0.1, true)
	for node in grouped:
		expect(node.transform == resting[node], "Reduced motion must settle foliage to its resting transform immediately.")
	garden.advance(5.0, true)
	for node in grouped:
		expect(node.transform == resting[node], "Reduced motion leaves the entire canopy still.")
	var cached_geometry: int = Decor.geometry.size()
	var cached_finishes: int = Decor.finishes.size()
	var second = Garden.new()
	root.add_child(second)
	second.build()
	expect(Decor.geometry.size() == cached_geometry and Decor.finishes.size() == cached_finishes, "A second garden must reuse cached mesh and material resources.")
	for node in second.find_children("*", "MeshInstance3D", true, false):
		expect(resources.has(node.mesh.get_rid()), "Repeated gardens must share their original geometry.")
	print("Forest garden: mesh nodes=", mesh_nodes, " drawn triangles=", triangles, " grouped leaves=", leaves, " leaf groups=", grouped.size(), " meshes=", resources.size(), " materials=", materials.size())
	print("Forest garden: ", "passed" if failures == 0 else "%d failed" % failures)
	second.free()
	garden.free()
	quit(0 if failures == 0 else 1)
