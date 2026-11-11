extends Node3D

# Original low-poly botany. The six opaque leaf batches use a broad folded
# silhouette; no image cards, alpha foliage, new lights or per-leaf animation.
const Decor = preload("cafe_decor.gd")
const TREE_ANCHOR := Vector3(-5.8, 0, -3.6)
const LEAF_COLOURS := ["#657d50", "#879b63", "#b1bd79"]
var _built := false
var _elapsed := 0.0
var _swaying: Array[Node3D] = []
var _rests: Array[Transform3D] = []

func build() -> void:
	if _built:
		return
	_built = true
	_build_hero()
	_build_outside()
	_build_window_vines()

func advance(delta: float, reduced: bool) -> void:
	_elapsed = 0.0 if reduced else fposmod(_elapsed + maxf(delta, 0.0), TAU / 0.7)
	for index in _swaying.size():
		var angle := 0.0 if reduced else sin(_elapsed * 0.7) * (0.0035 if index == 0 else 0.008)
		_swaying[index].transform = _rests[index] * Transform3D(Basis(Vector3.BACK, angle), Vector3.ZERO)

func _piece(label: String, mesh: Mesh, at: Vector3, paint: Material, scale_by := Vector3.ONE) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.name = label
	node.mesh = mesh
	node.position = at
	node.scale = scale_by
	node.material_override = paint
	add_child(node)
	return node

func _build_hero() -> void:
	var paths: Array = [
		_path([Vector3(0, 0.2, 0), Vector3(-0.1, 1.15, 0.04), Vector3(0.14, 2.25, 0.02), Vector3(0.03, 3.3, -0.13), Vector3(0.35, 4.15, -0.24), Vector3(0.15, 5.45, -0.35)], [0.47, 0.39, 0.34, 0.29, 0.21, 0.09]),
		_path([Vector3(0.04, 2.9, -0.1), Vector3(0.7, 3.75, -0.3), Vector3(1.55, 4.28, -0.5), Vector3(2.45, 4.8, -0.95), Vector3(3.2, 5.2, -1.1)], [0.25, 0.19, 0.14, 0.08, 0.025]),
		_path([Vector3(0.2, 3.8, -0.2), Vector3(-0.5, 4.3, -0.2), Vector3(-0.95, 4.85, 0.0), Vector3(-1.2, 5.3, 0.15)], [0.18, 0.13, 0.07, 0.018]),
		_path([Vector3(1.3, 4.15, -0.44), Vector3(2.0, 4.6, 0.2), Vector3(3.05, 5.1, 0.45), Vector3(3.85, 5.35, 0.55)], [0.12, 0.095, 0.055, 0.018]),
		_path([Vector3(0.2, 4.1, -0.24), Vector3(0.8, 4.65, 0.5), Vector3(0.6, 5.05, 1.3)], [0.13, 0.08, 0.025]),
		_path([Vector3(0.25, 4.8, -0.3), Vector3(0.85, 5.3, -0.25), Vector3(1.15, 5.6, -0.4)], [0.1, 0.06, 0.02]),
		_path([Vector3(-0.1, 0.22, 0), Vector3(-0.58, 0.13, 0.32), Vector3(-0.8, 0.12, 0.4)], [0.19, 0.11, 0.025]),
		_path([Vector3(0.1, 0.22, 0), Vector3(0.6, 0.12, -0.3), Vector3(0.83, 0.12, -0.35)], [0.18, 0.1, 0.025])
	]
	_piece("HeroCurvedTrunk", _tube_mesh("forest_hero_trunk", paths, 9), TREE_ANCHOR, Decor.surface("oak", "#684e3b"))
	_piece("TreePlanterBench", _ring_mesh(), TREE_ANCHOR, Decor.surface("oak", "#9d846c"))
	if not Decor.geometry.has("forest_soil"):
		var soil := CylinderMesh.new()
		soil.top_radius = 0.79
		soil.bottom_radius = 0.79
		soil.height = 0.25
		soil.radial_segments = 12
		Decor.geometry["forest_soil"] = soil
	_piece("TreePlanterSoil", Decor.geometry["forest_soil"], TREE_ANCHOR + Vector3(0, 0.18, 0), Decor.finish("#4e5842"))
	var body := StaticBody3D.new()
	body.name = "TreePlanterBody"
	body.position = TREE_ANCHOR + Vector3(0, 0.24, 0)
	add_child(body)
	var collision := CollisionShape3D.new()
	var cylinder := CylinderShape3D.new()
	cylinder.radius = 1.08
	cylinder.height = 0.48
	collision.shape = cylinder
	body.add_child(collision)
	var tips := [Vector3(-6.6, 5.25, -3.6), Vector3(-4.9, 5.6, -3.8), Vector3(-3.25, 5.3, -4.55), Vector3(-2.05, 5.4, -3.0), Vector3(-5.2, 5.05, -2.1)]
	var batches: Array = [[], [], []]
	var pivot := Vector3(-4.8, 5.15, -3.5)
	for cluster in tips.size():
		for index in 102:
			var angle := index * 2.399963 + cluster * 0.71
			var distance := sqrt((index + 0.5) / 102.0)
			var at: Vector3 = tips[cluster] + Vector3(cos(angle) * distance * 1.0, sin(angle * 1.9) * 0.43, sin(angle) * distance * 0.94)
			var shape := _leaf_transform(at, angle + 0.8, sin(angle * 2.1) * 0.64, 0.67 + 0.23 * (0.5 + 0.5 * sin(index * 1.7)))
			# Clamp the actual folded leaf bounds, including its rotation.
			var bounds: AABB = shape * _leaf_mesh().get_aabb()
			if bounds.position.y < 4.22:
				shape.origin.y += 4.22 - bounds.position.y
			if bounds.end.y > 6.48:
				shape.origin.y -= bounds.end.y - 6.48
			if bounds.end.x > -0.8:
				shape.origin.x -= bounds.end.x + 0.8
			shape.origin -= pivot
			batches[index % 3].append(shape)
	for index in 3:
		var leaves := _leaf_batch("HeroCanopy%d" % index, batches[index], pivot, LEAF_COLOURS[index])
		if index == 2:
			_swaying.append(leaves)
			_rests.append(leaves.transform)

func _build_outside() -> void:
	_piece("RearGardenGround", Decor.box_mesh(Vector3(24, 0.18, 7.6)), Vector3(0, -0.21, -10.15), Decor.finish("#68795f"))
	_piece("LeftGardenGround", Decor.box_mesh(Vector3(3.7, 0.18, 8.6)), Vector3(-9.6, -0.21, -1.9), Decor.finish("#68795f"))
	# Continue the backdrop into the seated camera's right-hand window view.
	_piece("RightGardenGround", Decor.box_mesh(Vector3(20, 0.18, 24)), Vector3(17.8, -0.21, -6), Decor.finish("#68795f"))
	var paths: Array = [
		_path([Vector3.ZERO, Vector3(0.08, 1.3, 0.02), Vector3(-0.05, 2.7, -0.07), Vector3(0.18, 4.3, 0)], [0.23, 0.19, 0.13, 0.035]),
		_path([Vector3(0, 2.1, 0), Vector3(-0.6, 2.9, -0.15), Vector3(-1.15, 3.7, -0.1)], [0.11, 0.07, 0.018]),
		_path([Vector3(0, 2.6, 0), Vector3(0.6, 3.3, 0.35), Vector3(1.1, 3.9, 0.45)], [0.11, 0.065, 0.018])
	]
	var tree_mesh := _tube_mesh("forest_exterior_trunk", paths, 7)
	var tree_positions := [Vector3(-9.0, -0.12, -8.7), Vector3(-6.8, -0.12, -9.7), Vector3(-3.8, -0.12, -11.0), Vector3(3.4, -0.12, -10.6), Vector3(6.4, -0.12, -8.9), Vector3(9.5, -0.12, -9.9), Vector3(13, -0.12, -10)]
	var batches: Array = [[], []]
	for tree in tree_positions.size():
		var at: Vector3 = tree_positions[tree]
		var tree_scale := Vector3(0.9, 1.0 + (tree % 3) * 0.16, 0.9)
		if at.x > 12:
			# A distant sapling keeps foliage in the low seated-window sightline.
			tree_scale.y = 0.4
		var trunk := _piece("OutsideTree%d" % tree, tree_mesh, at, Decor.surface("oak", "#684e3b"), tree_scale)
		trunk.rotation.y = tree * 1.73
		for index in 96:
			var angle := index * 2.399963 + tree * 0.8
			var radius := sqrt((index + 0.5) / 96.0)
			var leaf_at := at + Vector3(cos(angle) * radius * 1.52, 3.65 * tree_scale.y + sin(angle * 1.7) * 0.86, sin(angle) * radius * 1.26)
			batches[index % 2].append(_leaf_transform(leaf_at, angle, sin(angle * 1.4) * 0.68, 0.83 + 0.17 * sin(index * 1.7)))
	for index in 2:
		_leaf_batch("OutsideCanopy%d" % index, batches[index], Vector3.ZERO, LEAF_COLOURS[index])
	_piece("GardenStream", _stream_mesh(), Vector3.ZERO, Decor.finish("#719590")).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	if not Decor.geometry.has("forest_rock"):
		var rock := SphereMesh.new()
		rock.radius = 0.34
		rock.height = 0.52
		rock.radial_segments = 8
		rock.rings = 3
		Decor.geometry["forest_rock"] = rock
	for index in 5:
		var at := Vector3(-8.7 + index * 1.28, 0.04, -7.0 - 0.19 * sin(index * 2.0))
		var rock := _piece("StreamStone%d" % index, Decor.geometry["forest_rock"], at, Decor.finish("#89917c"), Vector3(0.85 + 0.2 * sin(index), 0.7, 0.75))
		rock.rotation.y = index * 1.5

func _build_window_vines() -> void:
	# The trailing strands sit outside the left glazing, away from camera rays.
	var leaves: Array = []
	for strand in 3:
		for index in 10:
			var at := Vector3(-8.03 + sin(index * 0.7) * 0.035, 4.2 - index * 0.17, -4.95 + strand * 1.0)
			leaves.append(_leaf_transform(at, 0.6 + (index % 2) * 2.1, -1.0, 0.26))
	var pivot := Vector3(-8.03, 4.2, -3.95)
	for index in leaves.size():
		leaves[index].origin -= pivot
	var node := _leaf_batch("WindowVines", leaves, pivot, LEAF_COLOURS[2])
	_swaying.append(node)
	_rests.append(node.transform)

func _leaf_batch(label: String, transforms: Array, pivot: Vector3, colour: String) -> MultiMeshInstance3D:
	var node := MultiMeshInstance3D.new()
	node.name = label
	node.multimesh = MultiMesh.new()
	node.multimesh.transform_format = MultiMesh.TRANSFORM_3D
	node.multimesh.mesh = _leaf_mesh()
	node.multimesh.instance_count = transforms.size()
	node.material_override = Decor.finish(colour)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	node.position = pivot
	add_child(node)
	var bounds := AABB()
	for index in transforms.size():
		node.multimesh.set_instance_transform(index, transforms[index])
		var leaf_bounds: AABB = transforms[index] * node.multimesh.mesh.get_aabb()
		bounds = leaf_bounds if index == 0 else bounds.merge(leaf_bounds)
	# The shared mesh has tiny local bounds; these scene-space batches need their
	# complete extents for stable culling, including on Compatibility renderers.
	node.custom_aabb = bounds.grow(0.035)
	return node

func _leaf_transform(at: Vector3, yaw: float, pitch: float, length: float) -> Transform3D:
	return Transform3D(Basis.from_euler(Vector3(pitch, yaw, sin(yaw * 1.3) * 0.27)).scaled(Vector3(length * 1.05, length, length)), at)

func _path(points: Array, radii: Array) -> Dictionary:
	return {"points": points, "radii": radii}

func _tube_mesh(key: String, paths: Array, sides: int) -> ArrayMesh:
	if Decor.geometry.has(key):
		return Decor.geometry[key]
	var vertices: Array[Vector3] = []
	var normals: Array[Vector3] = []
	var indices: Array[int] = []
	for path in paths:
		var points: Array = path.points
		var offset := vertices.size()
		for ring in points.size():
			var tangent: Vector3 = (points[mini(ring + 1, points.size() - 1)] - points[maxi(ring - 1, 0)]).normalized()
			var u := tangent.cross(Vector3.FORWARD).normalized()
			var v := tangent.cross(u).normalized()
			for side in sides:
				var angle := TAU * side / sides
				var normal := u * cos(angle) + v * sin(angle)
				vertices.append(points[ring] + normal * path.radii[ring])
				normals.append(normal)
		for ring in points.size() - 1:
			for side in sides:
				var a: int = offset + ring * sides + side
				var b: int = offset + ring * sides + (side + 1) % sides
				var c: int = a + sides
				var d: int = b + sides
				# Godot front faces are clockwise when viewed from outside.
				indices.append_array([a, c, b, b, c, d])
		# The tube tips are capped; the planter hides the base of the main trunk.
		for end in [0, points.size() - 1]:
			var centre := vertices.size()
			vertices.append(points[end])
			normals.append((points[0] - points[1]).normalized() if end == 0 else (points[-1] - points[-2]).normalized())
			for side in sides:
				var a: int = offset + end * sides + side
				var b: int = offset + end * sides + (side + 1) % sides
				indices.append_array([centre, a, b] if end == 0 else [centre, b, a])
	return _cache_mesh(key, vertices, normals, indices)

func _leaf_mesh() -> ArrayMesh:
	if Decor.geometry.has("forest_folded_leaf"):
		return Decor.geometry["forest_folded_leaf"]
	# A six-edge oval lamina has broad shoulders and a short pointed tip. The
	# raised base-to-tip vein and curled edges give volume without alpha cards.
	var points := [Vector3.ZERO, Vector3(-0.37, -0.025, 0.28), Vector3(-0.34, -0.045, 0.66), Vector3(0, 0.05, 0.95), Vector3(0.34, -0.045, 0.66), Vector3(0.37, -0.025, 0.28)]
	var faces := [[0, 2, 1], [0, 3, 2], [0, 4, 3], [0, 5, 4]]
	var smooth: Array[Vector3] = []
	smooth.resize(points.size())
	smooth.fill(Vector3.ZERO)
	for face in faces:
		var normal: Vector3 = (points[face[2]] - points[face[0]]).cross(points[face[1]] - points[face[0]]).normalized()
		for index in face:
			smooth[index] += normal
	for index in smooth.size():
		smooth[index] = smooth[index].normalized()
	var vertices: Array[Vector3] = []
	var normals: Array[Vector3] = []
	var indices: Array[int] = []
	# Four triangles on each side keep rounded silhouettes at eight triangles
	# per leaf; smooth normals avoid a jagged mosaic across the curved lamina.
	for face in faces:
		for back in 2:
			var triangle: Array = face.duplicate()
			if back == 1:
				triangle.reverse()
			for index in triangle:
				indices.append(vertices.size())
				vertices.append(points[index])
				normals.append(smooth[index] * (-1.0 if back == 1 else 1.0))
	return _cache_mesh("forest_folded_leaf", vertices, normals, indices)

func _ring_mesh() -> ArrayMesh:
	if Decor.geometry.has("forest_planter_bench"):
		return Decor.geometry["forest_planter_bench"]
	var vertices: Array[Vector3] = []
	var normals: Array[Vector3] = []
	var indices: Array[int] = []
	for segment in 16:
		var a := TAU * segment / 16.0
		var b := TAU * (segment + 1) / 16.0
		var outer_a := Vector3(cos(a), 0, sin(a))
		var outer_b := Vector3(cos(b), 0, sin(b))
		var points := [outer_a * 1.08 + Vector3.UP * 0.46, outer_b * 1.08 + Vector3.UP * 0.46, outer_b * 0.79 + Vector3.UP * 0.46, outer_a * 0.79 + Vector3.UP * 0.46]
		_quad(vertices, normals, indices, points[0], points[1], points[2], points[3], Vector3.UP)
		_quad(vertices, normals, indices, outer_b * 1.08 + Vector3.UP * 0.08, outer_a * 1.08 + Vector3.UP * 0.08, points[0], points[1], (outer_a + outer_b).normalized())
		_quad(vertices, normals, indices, outer_a * 0.79 + Vector3.UP * 0.08, outer_b * 0.79 + Vector3.UP * 0.08, points[2], points[3], -(outer_a + outer_b).normalized())
	return _cache_mesh("forest_planter_bench", vertices, normals, indices)

func _stream_mesh() -> ArrayMesh:
	if Decor.geometry.has("forest_stream"):
		return Decor.geometry["forest_stream"]
	var vertices: Array[Vector3] = []
	var normals: Array[Vector3] = []
	var indices: Array[int] = []
	for index in 7:
		var x := -10.1 + index * 1.16
		var next_x := x + 1.16
		var z := -7.55 + sin(index * 0.8) * 0.3
		var next_z := -7.55 + sin((index + 1) * 0.8) * 0.3
		_quad(vertices, normals, indices, Vector3(x, -0.105, z - 0.25), Vector3(next_x, -0.105, next_z - 0.25), Vector3(next_x, -0.105, next_z + 0.25), Vector3(x, -0.105, z + 0.25), Vector3.UP)
	return _cache_mesh("forest_stream", vertices, normals, indices)

func _quad(vertices: Array[Vector3], normals: Array[Vector3], indices: Array[int], a: Vector3, b: Vector3, c: Vector3, d: Vector3, normal: Vector3) -> void:
	var offset := vertices.size()
	vertices.append_array([a, b, c, d])
	normals.append_array([normal, normal, normal, normal])
	if (c - a).cross(b - a).dot(normal) > 0:
		indices.append_array([offset, offset + 1, offset + 2, offset, offset + 2, offset + 3])
	else:
		indices.append_array([offset, offset + 2, offset + 1, offset, offset + 3, offset + 2])

func _cache_mesh(key: String, vertices: Array[Vector3], normals: Array[Vector3], indices: Array[int]) -> ArrayMesh:
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = PackedVector3Array(vertices)
	arrays[Mesh.ARRAY_NORMAL] = PackedVector3Array(normals)
	arrays[Mesh.ARRAY_INDEX] = PackedInt32Array(indices)
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	Decor.geometry[key] = mesh
	return mesh
