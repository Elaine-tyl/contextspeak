extends "cafe_roleplay_world.gd"

const Art = preload("cafe_2d_art.gd")
var art: Node2D

func _ready() -> void:
	RenderingServer.set_default_clear_color(Color("#e8e0cf"))
	# Logical coordinates/physics are shared; all visible art is native 2D.
	player = CharacterBody3D.new()
	player.position = Vector3(0, 0, 4)
	add_child(player)
	hero = Avatar.new()
	player.add_child(hero)
	companion = Avatar.new()
	add_child(companion)
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.28
	capsule.height = 1.55
	var collider := CollisionShape3D.new()
	collider.shape = capsule
	collider.position.y = 0.8
	player.add_child(collider)
	camera = Camera3D.new()
	add_child(camera)
	cups = Node3D.new()
	add_child(cups)
	solid(Vector3(15, 0.2, 12), Vector3(0, -0.1, 0))
	solid(Vector3(6.4, 1.1, 2.5), Vector3(0, 0.55, -2.2))
	solid(Vector3(2.45, 1.0, 0.55), Vector3(-5.5, 0.5, -2.4))
	for x in [-4.6, 4.6]:
		for z in [-0.3, 3.2]:
			solid(Vector3(1.4, 0.8, 1.4), Vector3(x, 0.4, z))
	art = Art.new()
	art.world = self
	add_child(art)
	get_viewport().size_changed.connect(reframe)
	reframe()

func solid(size: Vector3, at: Vector3) -> void:
	var body := StaticBody3D.new()
	var collision := CollisionShape3D.new()
	var bounds := BoxShape3D.new()
	bounds.size = size
	collision.shape = bounds
	body.position = at
	body.add_child(collision)
	add_child(body)

func reframe() -> void:
	art.frame_room(get_viewport().get_visible_rect().size)

func guide_npc_id() -> String:
	return "friend" if seated or walking_to_friend else target_npc_id

func _process(delta: float) -> void:
	art.elapsed += delta if not reduce_motion else 0.0
	art.queue_redraw()

func show_reward() -> void:
	if cups.get_child_count() != 0:
		return
	# The 2D renderer draws the earned cups; no imported models are instantiated.
	for index in 2:
		cups.add_child(Node3D.new())
