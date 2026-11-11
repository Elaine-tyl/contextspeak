extends Node3D

# One tiny foliage sway, advanced by the café so its accessibility setting is
# authoritative. It never touches lights, actors, collision or the camera.
var elapsed := 0.0

func advance(delta: float, reduced: bool) -> void:
	elapsed = 0.0 if reduced else fmod(elapsed + delta, TAU / 0.8)
	for leaf in get_children():
		leaf.rotation.z = 0.0 if reduced else sin(elapsed * 0.8) * 0.018
