extends SceneTree

var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	var script = load("res://scripts/roleplay.gd")
	if script == null:
		check(false, "Role practice needs an explicit correction and progression state")
		quit(1)
		return
	var lesson = script.new()
	check(not lesson.start("invalid", "student"), "Unknown scenes do not start lessons")
	if not lesson.start("shopping", "shopper"):
		check(false, "Shopping with a friend replaces school")
		quit(1)
		return
	check(not lesson.start("school", "student"), "Removed school saves cannot unlock shopping")
	var old_school: Dictionary = lesson.state.duplicate(true)
	old_school.scene = "school"
	old_school.role = "student"
	check(not lesson.restore(old_school) and lesson.state.scene == "shopping", "An old school record cannot replace new shopping progress")
	check(lesson.turn() == 0 and not lesson.complete(), "Lessons begin at the first goal")
	var review := {"accepted": false, "correction": "I am looking for a blue hoodie.", "feedback": "Tell your friend what clothes you want.", "acknowledgement": "Let's try that together."}
	check(lesson.respond("banana", review) == "", "An unsuitable answer cannot trigger an action")
	check(lesson.turn() == 0 and lesson.pending() != "", "A correction keeps the same turn")
	check(lesson.repeat_correction("I am looking") == "", "Partial readback cannot bypass the missing goal")
	check(lesson.turn() == 0, "A failed readback keeps the correction")
	var restored = script.new()
	check(restored.restore(lesson.state.duplicate(true)), "A pending correction restores")
	check(restored.pending() == lesson.pending(), "Switch/reload cannot skip a pending correction")
	var action: String = restored.repeat_correction("i am looking for a blue hoodie")
	check(action != "" and restored.turn() == 1, "Case and punctuation do not block a correct readback")
	check(restored.state.answers[0].assisted, "Readback completions remain assisted evidence")
	check(restored.repeat_correction("I'm looking for a blue hoodie.") == "", "The same readback cannot advance twice")
	check(restored.respond("answer", {"accepted": "yes"}) == "", "Malformed AI output cannot advance")
	review = {"accepted": true, "correction": "", "feedback": "Clear answer.", "acknowledgement": "Thank you."}
	check(restored.respond("A valid answer for this turn.", review) != "", "An accepted answer advances one authored action")
	check(restored.turn() == 2 and not restored.state.answers[1].assisted, "Independent answers stay separate from readback")
	check(restored.respond("One final answer.", review) != "" and restored.complete(), "Three goals complete the chosen role")
	check(restored.respond("duplicate", review) == "" and restored.turn() == 3, "Completed lessons reject further submissions")
	var invalid: Dictionary = restored.state.duplicate(true)
	invalid.answers.append({"text": "extra", "assisted": false})
	check(not lesson.restore(invalid), "Out-of-bounds saves are rejected")
	invalid = restored.state.duplicate(true)
	invalid.correction = "A stale correction after completion."
	check(not lesson.restore(invalid), "Completed saves cannot contain pending corrections")
	check(lesson.start("market", "shopper") and lesson.turn() == 0 and lesson.pending() == "", "Changing role clears old corrections and answers")
	review.correction = "I would like two apples, please."
	check(lesson.respond("I wants two apples", review) == "", "Even on-topic grammar corrections require a readback")
	check(lesson.repeat_correction("I'd like 2 apples, please.") != "", "Common contractions and spoken numbers match the corrected line")
	lesson.start("shopping", "buddy")
	review.correction = "We will find a jacket today."
	lesson.respond("We find jacket today", review)
	check(lesson.repeat_correction("We'll find a jacket today.") != "", "Plural-person contractions are valid recorded readbacks")
	check(lesson.normalized("Let's try it on.") == lesson.normalized("Let us try it on."), "Casual let's matches an expanded readback")
	check(lesson.normalized("That's a bit much.") == lesson.normalized("That is a bit much."), "That's matches its expanded meaning")
	check(lesson.normalized("I wanna try it on.") == lesson.normalized("I want to try it on."), "Spoken wanna is a valid equivalent readback")
	check(lesson.start("amusement_park", "visitor"), "The amusement park has its own lesson")
	check(lesson.prompt().contains("first"), "A park outing starts by choosing a ride")
	lesson.start("restaurant", "server")
	review.correction = "Your bill is ten dollars."
	lesson.respond("bill ten", review)
	check(lesson.repeat_correction("Your bill is $10.") != "", "ASR currency notation matches spoken dollars")
	print("Role practice state: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
