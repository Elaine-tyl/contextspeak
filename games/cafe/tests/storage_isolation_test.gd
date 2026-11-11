extends SceneTree

func _initialize() -> void:
	if not OS.get_user_data_dir().get_file().begins_with("ContextSpeak-Test-"):
		push_error("Native regression tests must have isolated progress storage, not the user's game directory.")
		quit(1)
		return
	var file := FileAccess.open("user://isolation-probe.txt", FileAccess.WRITE)
	if file == null:
		push_error("The isolated test directory must be writable.")
		quit(1)
		return
	file.store_string("Test progress, never real learning progress.")
	file.close()
	print("Isolated test storage: passed")
	quit(0)
