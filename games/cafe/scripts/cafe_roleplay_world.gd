extends "world.gd"

# Adapt the existing forest room to the same NPC interface as the other scenes.
# The original café's order, friend and save controllers remain separate.
var target_npc_id := "counter"
var completed_actions: Dictionary = {}

func configure(_scene: String, _role: String, _npc: String, _role_id := "") -> void:
	name = "CafeChallengeWorld"
	camera.current = true

func npc_data(id: String) -> Dictionary:
	match id:
		"counter":
			return {"id": id, "name": "Maya", "role": "barista", "zone_label": "Counter", "position": BARISTA, "approach": Vector3(0, 0, -0.7)}
		"friend":
			return {"id": id, "name": "Alex", "role": "your friend", "zone_label": "Window table", "position": Vector3(5.75, 0, -0.3), "approach": Vector3(3.45, 0, -0.3)}
	return {}

func nearest_npc_id() -> String:
	if seated:
		return "friend"
	if player.position.distance_to(Vector3(5.75, 0, -0.3)) < 2.7:
		return "friend"
	return "counter" if near_barista() else ""

func set_target_npc(id: String) -> void:
	target_npc_id = id if not npc_data(id).is_empty() else ""

func focus_npc(id: String) -> void:
	walking_to_counter = false
	walking_to_friend = false
	viewing_friend = id == "friend"
	if id == "friend" and target_npc_id == "friend" and nearest_npc_id() == "friend":
		seat_with_friend()
	elif id == "counter":
		hero.rotation.y = PI

func current_zone() -> String:
	if seated or player.position.x > 2.7 and player.position.z < 1.5:
		return "Window table"
	return "Counter" if near_barista() else "Entrance"

func walk_to_npc(id := "") -> void:
	var destination: String = target_npc_id if id == "" else id
	if destination == "counter" and not seated:
		walking_to_counter = true
		walking_to_friend = false
	elif destination == "friend":
		join_friend()

func apply_action(action: String, step_id := "", _caption := "") -> void:
	if completed_actions.has(step_id):
		return
	completed_actions[step_id] = action
	if action == "pay":
		show_reward()
