extends RefCounted

# Original campus art attached to the existing CC0 animated rig. The cast shares
# three small meshes and a matte palette; animation only moves bone attachments.
const IVORY := "#f6ebd8"
const RIB := "#ddc9ac"
const SAGE := "#4f7969"
const NAVY := "#425d70"
const APRON := "#8a694c"
const DENIM := "#64788c"
const INK_DENIM := "#394859"
const GOLD := "#d5ae6f"
const INK := "#342d35"
static var materials: Dictionary = {}
static var meshes: Dictionary = {}
var skeleton: Skeleton3D
var space: Node3D
var anchors: Dictionary = {}

func point(bone: String) -> Vector3:
	return space.global_transform.affine_inverse() * skeleton.global_transform * skeleton.get_bone_global_pose(skeleton.find_bone(bone)).origin

func anchor(bone: String) -> BoneAttachment3D:
	if anchors.has(bone):
		return anchors[bone]
	var node := BoneAttachment3D.new()
	node.name = "Campus_" + bone
	node.bone_name = bone
	skeleton.add_child(node)
	node.transform = skeleton.get_bone_global_pose(skeleton.find_bone(bone))
	anchors[bone] = node
	return node

func piece(bone: String, shape: Mesh, at: Vector3, colour: String, title := "", shadow := false) -> MeshInstance3D:
	if not materials.has(colour):
		var fabric := StandardMaterial3D.new()
		fabric.albedo_color = Color(colour)
		fabric.roughness = 0.88
		materials[colour] = fabric
	var node := MeshInstance3D.new()
	node.mesh = shape
	node.material_override = materials[colour]
	# Only the large silhouette casts shadows, keeping face and trim draw cost low.
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if shadow else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	if title != "":
		node.name = title
	anchor(bone).add_child(node)
	node.global_transform = space.global_transform * Transform3D(Basis.IDENTITY, at)
	return node

func oval(bone: String, at: Vector3, size: Vector3, colour: String, title := "", shadow := false) -> MeshInstance3D:
	if not meshes.has("oval"):
		var sphere := SphereMesh.new()
		sphere.radius = 0.5
		sphere.height = 1
		sphere.radial_segments = 16
		sphere.rings = 8
		meshes["oval"] = sphere
	var node := piece(bone, meshes["oval"], at, colour, title, shadow)
	node.global_basis = space.global_basis * Basis.from_scale(size)
	return node

func between(bone: String, start: Vector3, end: Vector3, radius: float, colour: String, title := "", shadow := false, overlap := 0.10) -> MeshInstance3D:
	if not meshes.has("sleeve"):
		var capsule := CapsuleMesh.new()
		capsule.radius = 0.25
		capsule.height = 1
		capsule.radial_segments = 12
		capsule.rings = 4
		meshes["sleeve"] = capsule
	var node := piece(bone, meshes["sleeve"], (start + end) / 2, colour, title, shadow)
	var up := (end - start).normalized()
	var guide := Vector3.FORWARD if absf(up.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT
	var right := up.cross(guide).normalized()
	var size := Vector3(radius * 4, maxf(start.distance_to(end) + overlap, 0.015), radius * 4)
	node.global_basis = space.global_basis * Basis(right, up, right.cross(up)) * Basis.from_scale(size)
	return node

func sleeve(bone: String, end_bone: String, radius: float, colour: String, shadow := false) -> void:
	between(bone, point(bone), point(end_bone), radius, colour, "", shadow)

func garment(bone: String, at: Vector3, size: Vector3, colour: String, title: String, shadow := false) -> MeshInstance3D:
	if not meshes.has("garment"):
		# A softly rolled hem, full chest and tapered shoulders in one smooth mesh.
		var profile := [Vector2(-0.5, 0.72), Vector2(-0.48, 0.86), Vector2(-0.38, 0.94), Vector2(-0.12, 1), Vector2(0.15, 1), Vector2(0.29, 0.97), Vector2(0.38, 0.85), Vector2(0.44, 0.63), Vector2(0.5, 0.4)]
		var vertices := PackedVector3Array()
		var normals := PackedVector3Array()
		var indices := PackedInt32Array()
		const SEGMENTS := 16
		for row in profile.size():
			var previous: Vector2 = profile[maxi(row - 1, 0)]
			var next: Vector2 = profile[mini(row + 1, profile.size() - 1)]
			var slope := (next.y - previous.y) / (next.x - previous.x)
			for column in SEGMENTS:
				var angle := TAU * column / SEGMENTS
				vertices.append(Vector3(cos(angle) * profile[row].y * 0.5, profile[row].x, sin(angle) * profile[row].y * 0.5))
				normals.append(Vector3(cos(angle), -slope * 0.5, sin(angle)).normalized())
				if row > 0:
					var lower := (row - 1) * SEGMENTS + column
					var upper := row * SEGMENTS + column
					var lower_next := (row - 1) * SEGMENTS + (column + 1) % SEGMENTS
					var upper_next := row * SEGMENTS + (column + 1) % SEGMENTS
					indices.append_array(PackedInt32Array([lower, lower_next, upper, lower_next, upper_next, upper]))
		# Separate cap vertices keep the collar and rolled hem normals tidy.
		for cap in 2:
			var row := 0 if cap == 0 else profile.size() - 1
			var normal := Vector3.DOWN if cap == 0 else Vector3.UP
			var centre := vertices.size()
			vertices.append(Vector3(0, profile[row].x, 0))
			normals.append(normal)
			for column in SEGMENTS:
				var angle := TAU * column / SEGMENTS
				vertices.append(Vector3(cos(angle) * profile[row].y * 0.5, profile[row].x, sin(angle) * profile[row].y * 0.5))
				normals.append(normal)
			for column in SEGMENTS:
				var current := centre + 1 + column
				var next := centre + 1 + (column + 1) % SEGMENTS
				indices.append_array(PackedInt32Array([centre, next, current] if cap == 0 else [centre, current, next]))
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = vertices
		arrays[Mesh.ARRAY_NORMAL] = normals
		arrays[Mesh.ARRAY_INDEX] = indices
		var shape := ArrayMesh.new()
		shape.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		meshes["garment"] = shape
	var node := piece(bone, meshes["garment"], at, colour, title, shadow)
	node.global_basis = space.global_basis * Basis.from_scale(size)
	return node

func build(rig: Skeleton3D, role: String, avatar: Node3D) -> void:
	skeleton = rig
	space = avatar
	var friend := role == "woman-clothed"
	var learner := role == "learner-clothed"
	var skin := "#d8a17c" if learner else "#f2c7a7" if friend else "#b77d58"
	var hair := "#49352e" if learner else "#644532" if friend else "#302b2d"
	var jacket := NAVY if learner else SAGE if friend else IVORY
	var sleeve_colour := IVORY if learner else jacket
	var trousers := INK_DENIM if learner else DENIM
	var hips := point("Hips")
	var neck := point("Neck")
	var middle := (hips + neck) / 2 - Vector3(0, 0.035, 0)
	var height := neck.y - hips.y + 0.10
	garment("Spine1", middle, Vector3(0.43, height, 0.29), jacket, "CampusJacket", true)
	oval("Neck", neck, Vector3(0.14, 0.115, 0.14), skin)
	oval("Spine1", middle + Vector3(0, -height * 0.46, 0), Vector3(0.36, 0.045, 0.255), RIB, "CampusRibbedHem")
	for side in ["Left", "Right"]:
		sleeve(side + "Arm", side + "ForeArm", 0.075, sleeve_colour, true)
		sleeve(side + "ForeArm", side + "Hand", 0.064, sleeve_colour)
		var wrist := point(side + "Hand")
		var forearm := point(side + "ForeArm")
		between(side + "ForeArm", wrist.lerp(forearm, 0.07), wrist.lerp(forearm, 0.18), 0.065, RIB, "", false, 0.005)
		oval(side + "Hand", wrist + Vector3(0, -0.015, 0), Vector3(0.096, 0.115, 0.10), skin)
		sleeve(side + "UpLeg", side + "Leg", 0.084, trousers, true)
		sleeve(side + "Leg", side + "Foot", 0.070, trousers, true)
		var foot := point(side + "Foot") + Vector3(0, -0.012, 0.06)
		oval(side + "Foot", foot, Vector3(0.15, 0.12, 0.265), IVORY, "CampusSneaker", true)
		oval(side + "Foot", foot + Vector3(0, -0.038, 0), Vector3(0.153, 0.036, 0.263), RIB)
		oval(side + "Foot", foot + Vector3(0, 0.042, 0.018), Vector3(0.075, 0.018, 0.07), jacket)
	if learner:
		oval("Spine1", middle + Vector3(0, -0.005, -0.19), Vector3(0.28, 0.34, 0.16), APRON, "CampusBackpack", true)
		oval("Spine1", middle + Vector3(0, -0.075, -0.266), Vector3(0.20, 0.135, 0.045), GOLD, "CampusBackpackPocket")
		for x in [-0.14, 0.14]:
			oval("Spine1", middle + Vector3(x, 0.035, 0.115), Vector3(0.035, 0.29, 0.035), APRON)
		oval("Spine1", middle + Vector3(0, 0, 0.149), Vector3(0.014, height * 0.58, 0.018), IVORY)
		oval("Spine1", middle + Vector3(-0.087, 0.09, 0.137), Vector3(0.055, 0.067, 0.018), GOLD, "CampusBadge")
		for x in [-0.085, 0.085]:
			oval("Spine1", middle + Vector3(x, -0.10, 0.14), Vector3(0.07, 0.014, 0.018), IVORY)
	elif friend:
		oval("Spine1", middle + Vector3(0, 0, 0.149), Vector3(0.025, height * 0.62, 0.018), RIB)
		for y in [-0.105, 0.0, 0.105]:
			oval("Spine1", middle + Vector3(0, y, 0.162), Vector3(0.022, 0.022, 0.012), GOLD)
	else:
		garment("Spine1", middle + Vector3(0, 0.015, 0.148), Vector3(0.28, height * 0.79, 0.022), APRON, "CampusApron")
		garment("Hips", hips + Vector3(0, -0.065, 0.105), Vector3(0.34, 0.30, 0.13), APRON, "CampusApronSkirt")
		oval("Spine1", middle + Vector3(0, -0.085, 0.17), Vector3(0.17, 0.085, 0.025), APRON, "CampusApronPocket")
		oval("Spine1", middle + Vector3(-0.06, 0.105, 0.165), Vector3(0.06, 0.027, 0.014), IVORY, "CampusNameTag")
		for x in [-0.085, 0.085]:
			oval("Spine1", middle + Vector3(x, height * 0.31, 0.107), Vector3(0.028, 0.17, 0.025), APRON)
	# Small rounded collar leaves frame the neck without a rigid cylinder edge.
	for x in [-0.055, 0.055]:
		var collar := oval("Spine1", neck + Vector3(x, -0.035, 0.045), Vector3(0.085, 0.055, 0.11), IVORY)
		collar.global_basis = space.global_basis * Basis(Vector3.FORWARD, signf(x) * 0.35) * Basis.from_scale(Vector3(0.085, 0.055, 0.11))
	face(skin, hair, friend)

func face(skin: String, hair: String, buns: bool) -> void:
	var head := point("Head") + Vector3(0, 0.068, 0.012)
	oval("Head", head, Vector3(0.315, 0.345, 0.288), skin, "CampusFace", true)
	oval("Head", head + Vector3(0, 0.105, -0.023), Vector3(0.33, 0.205, 0.295), hair, "CampusHair")
	for sign_x in [-1, 1]:
		var x: float = sign_x * 0.061
		oval("Head", head + Vector3(x, 0.009, 0.136), Vector3(0.042, 0.057, 0.023), IVORY)
		oval("Head", head + Vector3(x, 0.008, 0.151), Vector3(0.026, 0.041, 0.014), INK)
		oval("Head", head + Vector3(x - 0.005, 0.017, 0.16), Vector3(0.008, 0.010, 0.006), "#fffaf0")
		oval("Head", head + Vector3(x, 0.06, 0.132), Vector3(0.043, 0.012, 0.015), hair)
		oval("Head", head + Vector3(sign_x * 0.096, -0.041, 0.114), Vector3(0.036, 0.019, 0.012), "#df9d91")
		oval("Head", head + Vector3(sign_x * 0.153, -0.012, -0.002), Vector3(0.041, 0.066, 0.05), skin)
		oval("Head", head + Vector3(sign_x * 0.139, 0.058, -0.005), Vector3(0.052, 0.13, 0.10), hair)
		if buns:
			oval("Head", head + Vector3(sign_x * 0.135, 0.148, -0.075), Vector3(0.135, 0.14, 0.14), hair)
			oval("Head", head + Vector3(sign_x * 0.135, 0.103, -0.065), Vector3(0.125, 0.025, 0.12), GOLD)
	# Two swept locks give the hair a deliberate silhouette from either camera side.
	for lock in [[-0.055, 0.116, -0.35], [0.075, 0.103, 0.48]]:
		var fringe := oval("Head", head + Vector3(lock[0], lock[1], 0.091), Vector3(0.18, 0.105, 0.11), hair)
		fringe.global_basis = space.global_basis * Basis(Vector3.FORWARD, lock[2]) * Basis.from_scale(Vector3(0.18, 0.105, 0.11))
	oval("Head", head + Vector3(0, -0.03, 0.145), Vector3(0.029, 0.034, 0.027), skin)
	oval("Head", head + Vector3(0, -0.077, 0.131), Vector3(0.044, 0.012, 0.012), "#945e59")
