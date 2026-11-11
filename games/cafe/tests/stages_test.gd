extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	var script = load("res://scripts/stages.gd")
	if script == null:
		check(false, "Practice stages must provide independent, bounded preview fixtures")
		quit(1)
		return
	var stages = script.new()
	check(not stages.preview(), "The first launch continues the saved journey")
	check(stages.fixture().is_empty(), "Continue must not seed a preview over the real save")
	check(not stages.select(-1) and not stages.select(6), "Invalid stage IDs cannot enter a preview")
	for selection in range(1, 6):
		check(stages.select(selection), "Each listed practice stage can be selected")
		check(stages.preview(), "Practice selections disable normal progress writes")
		var value: Dictionary = stages.fixture()
		var mission = load("res://scripts/mission.gd").new()
		var friend = load("res://scripts/friend.gd").new()
		check(mission.restore(value.order), "Preview orders must pass the normal save validation")
		check(friend.restore(value.friend, mission.state.complete) if selection > 1 else friend.state.phase == "counter", "Preview friend progress must pass the normal save validation")
		if selection == 1:
			check(not mission.state.complete and mission.state.stage == "order", "Counter practice starts with an empty unfinished order")
		else:
			check(mission.order_text() == "2 coffees" and mission.state.temperature == "hot" and mission.state.service == "here", "Friend practice has a valid completed sample order")
			check(friend.state.answers.size() == selection - 2, "A selected friend turn has only its preceding sample answers")
			check(friend.state.phase == ("complete" if selection == 5 else "seated"), "The completion preview alone is complete")
			check(friend.feedback == "" or friend.feedback.contains("Sample answer"), "Sample feedback is clearly labelled")
		value.order.items["coffee"] = 1
		value.friend.answers.append({"text": "changed", "feedback": "changed"})
		var restarted: Dictionary = stages.fixture()
		check(restarted.friend.answers.size() == maxi(0, selection - 2), "Restart creates a fresh fixture instead of retaining preview answers")
		check(not restarted.order.items.has("coffee") or restarted.order.items.coffee == 2, "Fixture mutations cannot alter later restarts")
	check(stages.select(0), "The saved journey remains available after every preview")
	check(not stages.preview() and stages.fixture().is_empty(), "Returning to the saved journey removes preview seeding and permits normal saves")
	print("Cafe practice fixture checks: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
