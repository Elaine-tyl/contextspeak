extends RefCounted

const QUESTIONS := [
	"We finally have our drinks! What do you like to drink, and why?",
	"What would you like to do this weekend?",
	"Could you invite me to join you and suggest a time?"
]
var state: Dictionary = fresh()
var acknowledgement := ""
var feedback := ""
var retry_hint := ""

func fresh() -> Dictionary:
	return {"version": 1, "phase": "counter", "answers": []}

func begin_walk(ordered: bool) -> bool:
	if not ordered or state.phase != "counter":
		return false
	state.phase = "walking"
	return true

func sit() -> bool:
	if state.phase != "walking":
		return false
	state.phase = "seated"
	return true

func prompt() -> String:
	if state.phase == "complete":
		return "Thanks for the invitation! It was lovely catching up over a drink. Let's do this again soon."
	return QUESTIONS[mini(state.answers.size(), 2)]

func line() -> String:
	return (acknowledgement + " " if acknowledgement != "" else "") + (retry_hint if retry_hint != "" else prompt())

func respond(text: String, review: Dictionary) -> bool:
	if state.phase != "seated" or text.strip_edges() == "" or text.length() > 400:
		return false
	if not review.get("accepted") is bool or not review.get("acknowledgement") is String or not review.get("feedback") is String:
		return false
	if review.acknowledgement.length() > 220 or review.feedback.length() > 240:
		return false
	acknowledgement = review.acknowledgement
	feedback = review.feedback
	retry_hint = "" if review.accepted else feedback
	if not review.accepted:
		return false
	state.answers.append({"text": text, "feedback": feedback})
	if state.answers.size() == QUESTIONS.size():
		state.phase = "complete"
	return true

func restore(saved: Dictionary, ordered: bool) -> bool:
	state = fresh()
	acknowledgement = ""
	feedback = ""
	retry_hint = ""
	if not ordered or saved.get("version") != 1 or not saved.get("phase") in ["counter", "walking", "seated", "complete"]:
		return false
	if not saved.get("answers") is Array or saved.answers.size() > 3:
		return false
	for answer in saved.answers:
		if not answer is Dictionary or not answer.get("text") is String or not answer.get("feedback") is String:
			return false
		if answer.text.strip_edges() == "" or answer.text.length() > 400 or answer.feedback.length() > 240:
			return false
	if (saved.phase == "complete") != (saved.answers.size() == 3):
		return false
	if saved.phase in ["counter", "walking"] and not saved.answers.is_empty():
		return false
	state = saved.duplicate(true)
	if not state.answers.is_empty():
		feedback = state.answers.back().feedback
	return true
