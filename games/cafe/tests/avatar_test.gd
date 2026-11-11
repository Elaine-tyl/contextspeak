extends SceneTree
const Avatar = preload("../scripts/avatar.gd")
var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func bone_in_avatar(avatar: Node3D, skeleton: Skeleton3D, bone: String) -> Vector3:
	return avatar.global_transform.affine_inverse() * skeleton.global_transform * skeleton.get_bone_global_pose(skeleton.find_bone(bone)).origin

func visible_parts(avatar: Node3D) -> Array[MeshInstance3D]:
	var parts: Array[MeshInstance3D] = []
	for mesh in avatar.find_children("*", "MeshInstance3D", true, false):
		if mesh.is_visible_in_tree():
			parts.append(mesh)
	return parts

func visible_bounds(avatar: Node3D) -> AABB:
	var bounds := AABB()
	var first := true
	for mesh in visible_parts(avatar):
		var local_transform := avatar.global_transform.affine_inverse() * mesh.global_transform
		var part_bounds: AABB = local_transform * mesh.get_aabb()
		bounds = part_bounds if first else bounds.merge(part_bounds)
		first = false
	return bounds

func run() -> void:
	var all_meshes := {}
	var all_materials := {}
	var avatars: Array[Node3D] = []
	for clothing in ["woman-clothed", "learner-clothed", "barista-clothed", "woman-clothed"]:
		var avatar := Avatar.new()
		root.add_child(avatar)
		# Building an actor at its destination must preserve local human dimensions.
		if avatars.size() == 3:
			avatar.position = Vector3(3, 0.3, -2)
			avatar.rotation.y = 0.8
		avatar.setup(load("res://assets/quaternius/woman.fbx").instantiate(), clothing)
		avatars.append(avatar)
		check(avatar.current == "idle" and avatar.animation.is_playing(), "Quaternius idle must start through the shared avatar action")
		avatar.animation.advance(0.1)
		await process_frame
		await process_frame
		var skeleton: Skeleton3D = avatar.find_child("Skeleton3D", true, false)
		var head := bone_in_avatar(avatar, skeleton, "HeadTop_End")
		var foot := bone_in_avatar(avatar, skeleton, "LeftToeBase")
		check(head.y > 1.60 and head.y < 1.72 and absf(foot.y) < 0.035, "Standing FBX rig must stay at 1.65 m in avatar space, even after placement")
		var standing_bounds := visible_bounds(avatar)
		check(standing_bounds.size.y > 1.55 and standing_bounds.size.y < 1.9, "Styled standing geometry must remain human scale")
		check(standing_bounds.position.y > -0.11 and standing_bounds.position.y < 0.05, "Rounded sneakers must stay close to the collision floor")
		check(standing_bounds.size.x < 0.85 and standing_bounds.size.z < 0.8, "Clothes and backpack must stay inside the established walking clearance")
		var face: MeshInstance3D = avatar.find_child("CampusFace", true, false)
		var jacket: MeshInstance3D = avatar.find_child("CampusJacket", true, false)
		check(face != null and jacket != null, "The campus face and outfit must remain on the bone rig")
		var standing_face := avatar.global_transform.affine_inverse() * face.global_position
		var standing_hips := bone_in_avatar(avatar, skeleton, "Hips")
		var parts := visible_parts(avatar)
		var shadows := 0
		var triangles := 0
		for mesh in parts:
			triangles += mesh.mesh.get_faces().size() / 3
			var material := mesh.get_active_material(0) as StandardMaterial3D
			check(material != null and material.roughness >= 0.7, "Every visible part needs a matte café material")
			if mesh.mesh is ArrayMesh and not all_meshes.has(mesh.mesh.get_instance_id()):
				var arrays: Array = mesh.mesh.surface_get_arrays(0)
				var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
				var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
				var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
				var outward := true
				for triangle in range(0, indices.size(), 3):
					var a := indices[triangle]
					var b := indices[triangle + 1]
					var c := indices[triangle + 2]
					# Godot front faces use clockwise winding relative to their normals.
					if (vertices[b] - vertices[a]).cross(vertices[c] - vertices[a]).dot(normals[a] + normals[b] + normals[c]) >= 0:
						outward = false
				check(outward, "Garment faces must render outward under back-face culling")
			all_meshes[mesh.mesh.get_instance_id()] = true
			all_materials[material.get_instance_id()] = true
			if mesh.cast_shadow != GeometryInstance3D.SHADOW_CASTING_SETTING_OFF:
				shadows += 1
		check(parts.size() >= 30 and parts.size() <= 64, "Facial and outfit detail must fit a bounded 64-part avatar")
		check(triangles <= 18000, "Rounded avatar forms must stay below 18,000 visible triangles")
		print("Cafe outfit ", clothing, ": ", parts.size(), " parts / ", shadows, " shadow casters / ", triangles, " triangles")
		check(shadows >= 4 and shadows <= 12, "Major body forms must ground the actor with a bounded shadow pass")
		if clothing == "learner-clothed":
			check(avatar.find_child("CampusBackpack", true, false) != null, "The learner must retain the campus backpack")
		if clothing == "barista-clothed":
			check(avatar.find_child("CampusApron", true, false) != null, "The café worker needs an identifiable apron")
		avatar.play("walk")
		check(avatar.current == "walk" and avatar.animation.is_playing(), "Walking must map to the bundled Walking clip")
		avatar.play("sit")
		for frame in 4:
			avatar.animation.advance(0.1)
		await process_frame
		await process_frame
		var seated_hips := bone_in_avatar(avatar, skeleton, "Hips")
		var seated_face := avatar.global_transform.affine_inverse() * face.global_position
		check(avatar.current == "sit" and seated_hips.y < standing_hips.y - 0.25, "The actual seated animation must lower the hips")
		check(seated_face.y < standing_face.y - 0.2, "The visible face must follow the seated head bone")
		check(visible_bounds(avatar).size.y < standing_bounds.size.y - 0.2, "Seated geometry must follow the rig rather than float in its standing pose")
		check(face.mesh.get_instance_id() in all_meshes and visible_parts(avatar).size() == parts.size(), "Animation must not allocate replacement geometry")
		avatar.play("idle")
		for frame in 4:
			avatar.animation.advance(0.1)
		await process_frame
		await process_frame
		check(avatar.current == "idle" and bone_in_avatar(avatar, skeleton, "Hips").y > seated_hips.y + 0.25, "A restarted journey must be able to stand again")
	check(all_meshes.size() <= 5, "Avatars must reuse at most five shared geometry resources across outfits")
	check(all_materials.size() <= 26, "The campus cast must share a bounded matte-material palette")
	print("Cafe avatar budget: ", all_meshes.size(), " geometry resources; ", all_materials.size(), " materials across ", avatars.size(), " actors")
	for avatar in avatars:
		avatar.queue_free()
	print("Cafe Quaternius avatar: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
