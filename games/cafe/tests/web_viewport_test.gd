extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if not FileAccess.file_exists("res://scripts/web_viewport.gd"):
		check(false, "The browser must size its logical interface in CSS pixels")
		quit(1)
		return
	var viewport = load("res://scripts/web_viewport.gd").new()
	var original := root.content_scale_size
	viewport.setup(root)
	check(root.content_scale_size == original, "Native startup must preserve its configured content size")
	viewport.fit(Vector2i(573, 793))
	await process_frame
	check(root.content_scale_size == Vector2i(573, 793), "A high-density narrow browser needs CSS-sized text and a full-height canvas")
	check(root.get_visible_rect().size == Vector2(573, 793), "Controls must lay out against the logical browser dimensions")
	viewport.fit(Vector2i(1280, 800))
	check(root.content_scale_size == Vector2i(1280, 800), "Desktop test coordinates must retain their existing scale")
	viewport.fit(Vector2i.ZERO)
	check(root.content_scale_size.x > 0 and root.content_scale_size.y > 0, "A hidden or minimized canvas cannot create a zero-sized viewport")
	viewport.dispose()
	viewport.on_resize([]) # A resize queued before teardown must be harmless.
	print("Web viewport scaling: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
