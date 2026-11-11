extends SceneTree
const World = preload("../scripts/world.gd")
var failures := 0

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var world = World.new()
	root.add_child(world)
	for file in ["furniture/tableRound", "furniture/chairCushion", "mini-characters/character-female-c", "kaykit/table_round_A_small.gltf", "kaykit/chair_A.gltf"]:
		var at := Vector3(2, 0, 1)
		var model = world.asset(file, at, 0.95)
		var bounds := AABB()
		var first := true
		for mesh in model.find_children("*", "MeshInstance3D", true, false):
			var part: AABB = mesh.global_transform * mesh.get_aabb()
			bounds = part if first else bounds.merge(part)
			first = false
		if not is_equal_approx(bounds.get_center().x, at.x) or not is_equal_approx(bounds.get_center().z, at.z):
			failures += 1
			push_error("Imported furniture/people must be centered at their intended placement: " + file)
		if not is_zero_approx(bounds.position.y) or not is_equal_approx(bounds.size.y, 0.95):
			failures += 1
			push_error("Imported models must sit on the floor at the requested height: " + file)
	print("Cafe asset placement: ", "passed" if failures == 0 else "%d failed" % failures)
	# Dispose scene instances before the dummy renderer releases material resources.
	await process_frame
	world.free()
	quit(0 if failures == 0 else 1)
