extends SceneTree

# Optional real-GPU smoke check, deliberately separate from headless regressions.
const World = preload("../scripts/world.gd")
const Interface = preload("../scripts/interface.gd")

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("Run without --headless to measure the native renderer")
		quit(1)
		return
	Engine.max_fps = 60
	var world = World.new()
	var ui = Interface.new()
	root.add_child(world)
	root.add_child(ui)
	for frame in 90:
		await process_frame
	var started := Time.get_ticks_msec()
	var calls := 0.0
	for frame in 120:
		await process_frame
		calls += Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
	var seconds := (Time.get_ticks_msec() - started) / 1000.0
	print(JSON.stringify({
		"renderer": RenderingServer.get_current_rendering_method(),
		"pixels": [root.get_texture().get_width(), root.get_texture().get_height()],
		"fps_cap": 60,
		"sample_frames": 120,
		"fps": snappedf(120.0 / seconds, 0.1),
		"average_draw_calls": roundi(calls / 120),
		"visible_triangles": Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)
	}))
	root.get_texture().get_image().save_png("/private/tmp/contextspeak-cozy-native-render.png")
	world.free()
	ui.free()
	quit(0)
