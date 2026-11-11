extends SceneTree
const Friend = preload("../scripts/friend.gd")
var failures := 0

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)

func _initialize() -> void:
	var chat = Friend.new()
	check(not chat.begin_walk(false), "Cannot visit the table before completing the order")
	check(not chat.sit(), "Cannot seat a character before the walk")
	check(chat.begin_walk(true), "A completed order unlocks the walk to the friend")
	check(chat.sit(), "Arrival starts the seated conversation")
	var accepted := {"accepted": true, "acknowledgement": "That sounds nice.", "feedback": "Clear answer with a reason."}
	var rejected := {"accepted": false, "acknowledgement": "Could you say a little more?", "feedback": "Tell me your preference."}
	check(not chat.respond("uh", rejected), "Unclear replies keep the same question")
	check(chat.state.answers.size() == 0, "Rejected replies cannot advance")
	check(chat.line().contains(rejected.feedback), "A rejected reply speaks the repair hint")
	check(not chat.line().contains(chat.prompt()), "A rejected reply does not blindly repeat the same question")
	check(chat.respond("I like tea because it is relaxing.", accepted), "A meaningful preference advances one turn")
	check(chat.line().contains("this weekend"), "An accepted reply asks the next question")
	var missing_activity := {"accepted": false, "acknowledgement": "Thanks for sharing.", "feedback": "Tell me one activity you would like to do this weekend."}
	check(not chat.respond("My shoes are blue.", missing_activity), "An off-topic answer stays on the weekend turn")
	check(chat.state.answers.size() == 1, "A repair does not skip or repeat accepted turns")
	check(chat.line().contains(missing_activity.feedback), "The weekend retry explains what is missing")
	check(chat.respond("I was now playing Batman this weekend.", accepted), "A meaningful plan advances despite imperfect grammar")
	check(chat.line().contains("suggest a time"), "A repaired reply clears the hint and advances to the invitation")
	var resumed = Friend.new()
	check(resumed.restore(JSON.parse_string(JSON.stringify(chat.state)), true), "Seated progress survives JSON restore")
	check(resumed.state.answers.size() == 2, "Restore the exact number of accepted turns")
	check(resumed.feedback == accepted.feedback, "Restore the last saved conversation tip")
	check(not chat.respond("Saturday.", rejected), "An incomplete invitation can be repaired")
	check(chat.restore(resumed.state, true), "Restoring replaces a conversation waiting for repair")
	check(chat.line().contains("suggest a time"), "Restoring clears the transient repair hint")
	check(resumed.respond("Would you like to meet me at the park at ten?", accepted), "An invitation completes the chat")
	check(resumed.state.phase == "complete", "Three accepted replies complete the friend conversation")
	check(not resumed.respond("one more", accepted), "Extra replies cannot duplicate progress")
	check(not Friend.new().restore(resumed.state, false), "A save cannot bypass the counter")
	check(not Friend.new().restore({"version": 1, "phase": "complete", "answers": []}, true), "An empty chat cannot claim completion")
	check(not Friend.new().restore({"version": 1, "phase": "walking", "answers": ["bad"]}, true), "Malformed chat history is rejected")
	print("Cafe friend checks: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)
