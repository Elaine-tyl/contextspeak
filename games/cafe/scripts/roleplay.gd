extends RefCounted

const CATALOG_PATH := "res://data/roleplay.json"
const CHALLENGE_PATH := "res://data/real_life.json"
const CONTRACTIONS := {
	"i'd": "i would", "i'm": "i am", "i'll": "i will",
	"don't": "do not", "can't": "cannot", "it's": "it is",
	"let's": "let us", "that's": "that is", "there's": "there is",
	"wanna": "want to", "gonna": "going to", "didn't": "did not",
	"i've": "i have", "we've": "we have", "you've": "you have", "they've": "they have",
	"isn't": "is not", "aren't": "are not", "haven't": "have not", "hasn't": "has not",
	"won't": "will not", "wouldn't": "would not", "shouldn't": "should not"
}
var catalog: Array = []
var challenge_catalog: Array = []
var state: Dictionary

func _init() -> void:
	state = _new_state("", "", "everyday")
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(CATALOG_PATH))
	if value is Array:
		catalog = value
	if FileAccess.file_exists(CHALLENGE_PATH):
		value = JSON.parse_string(FileAccess.get_file_as_string(CHALLENGE_PATH))
		if value is Array:
			challenge_catalog = value

func _new_state(scene_id: String, role_id: String, difficulty: String) -> Dictionary:
	return {"version": 2, "scene": scene_id, "role": role_id, "difficulty": difficulty, "answers": [], "first_reply": "", "goal_assisted": false, "correction": "", "feedback": "", "acknowledgement": ""}

func scene_data(scene_id: String, difficulty := "") -> Dictionary:
	if difficulty == "":
		difficulty = state.difficulty
	for scene in challenge_catalog if difficulty == "challenge" else catalog:
		if scene.id == scene_id:
			return scene
	return {}

func role_data(scene_id := "", role_id := "", difficulty := "") -> Dictionary:
	if scene_id == "":
		scene_id = state.scene
	if role_id == "":
		role_id = state.role
	for role in scene_data(scene_id, difficulty).get("roles", []):
		if role.id == role_id:
			return role
	return {}

func start(scene_id: String, role_id: String, difficulty := "everyday") -> bool:
	if difficulty not in ["everyday", "challenge"] or role_data(scene_id, role_id, difficulty).is_empty():
		return false
	state = _new_state(scene_id, role_id, difficulty)
	return true

func session_key() -> String:
	return state.scene + "/" + state.role + ("/challenge" if state.difficulty == "challenge" else "")

func exchange() -> int:
	return 1 if state.first_reply != "" else 0

func history() -> Array:
	var replies: Array = []
	for answer in state.answers:
		replies.append(answer.text)
		if state.difficulty == "challenge":
			replies.append(answer.follow_up)
	if exchange() == 1:
		replies.append(state.first_reply)
	return replies

func turn() -> int:
	return state.answers.size()

func complete() -> bool:
	return state.scene != "" and turn() == 3

func pending() -> String:
	return state.correction

func step() -> Dictionary:
	if state.scene == "" or complete():
		return {}
	var current: Dictionary = role_data().turns[turn()].duplicate(true)
	if exchange() == 1:
		current.merge(current.follow_up, true)
	return current

func prompt() -> String:
	if complete():
		return "We did it! Thanks for your help."
	if pending() != "":
		return "Please repeat: " + pending()
	return str(step().get("prompt", ""))

func line() -> String:
	return (state.acknowledgement + " " if state.acknowledgement != "" else "") + prompt()

func respond(text: String, review: Dictionary) -> String:
	if complete() or state.scene == "" or pending() != "" or text.strip_edges() == "":
		return ""
	if not review.get("accepted") is bool:
		return ""
	for field in ["correction", "feedback", "acknowledgement"]:
		if not review.get(field) is String or review[field].length() > (160 if field == "acknowledgement" else 240):
			return ""
	if review.feedback.strip_edges() == "" or review.acknowledgement.strip_edges() == "":
		return ""
	state.feedback = review.feedback
	state.acknowledgement = review.acknowledgement
	state.correction = review.correction.strip_edges()
	if not review.accepted and state.correction == "":
		state.correction = step().sample
	if pending() != "":
		return ""
	return advance(text, false)

func normalized(text: String) -> String:
	text = text.to_lower().replace("’", "'")
	for contraction in CONTRACTIONS:
		text = text.replace(contraction, CONTRACTIONS[contraction])
	for person in ["you", "we", "they", "he", "she", "it"]:
		text = text.replace(person + "'ll", person + " will").replace(person + "'d", person + " would")
	for person in ["you", "we", "they"]:
		text = text.replace(person + "'re", person + " are")
	var currency := RegEx.new()
	currency.compile("\\$([0-9]+)")
	text = currency.sub(text, "$1 dollars", true)
	var punctuation := RegEx.new()
	punctuation.compile("[^a-z0-9 ]")
	text = punctuation.sub(text, " ", true)
	var numbers := ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"]
	var tens := {"twenty": 20, "thirty": 30, "forty": 40, "fifty": 50, "sixty": 60, "seventy": 70, "eighty": 80, "ninety": 90}
	var tokens := text.split(" ", false)
	var words := PackedStringArray()
	var index := 0
	while index < tokens.size():
		var word: String = tokens[index]
		if tens.has(word):
			var number: int = tens[word]
			if index + 1 < tokens.size() and numbers.find(tokens[index + 1]) in range(1, 10):
				number += numbers.find(tokens[index + 1])
				index += 1
			words.append(str(number))
		else:
			words.append(str(numbers.find(word)) if word in numbers else word)
		index += 1
	return " ".join(words)

func requests_repeat(text: String) -> bool:
	var request := RegEx.new()
	request.compile("^(sorry )?((please )?((can|could|would) you )?(please )?(repeat( that| the question)?|say that again|speak more slowly)|i did not catch that)( please)?$")
	return request.search(normalized(text)) != null

func repeat_correction(text: String) -> String:
	if pending() == "" or complete():
		return ""
	# This checks the recognized words, not the learner's accent or pronunciation.
	if normalized(text) != normalized(pending()):
		state.feedback = "Not quite the model sentence. Hear it again, then repeat the whole sentence. Punctuation does not matter."
		return ""
	return advance(pending(), true)

func advance(text: String, assisted: bool) -> String:
	var action: String = step().action
	state.correction = ""
	if assisted:
		state.acknowledgement = "Nice, that's it!"
		state.feedback = "Corrected sentence practised. Try an answer of your own next time."
	if state.difficulty == "challenge" and exchange() == 0:
		# A good opening does not award the goal or skip its same-NPC follow-up.
		state.first_reply = text
		state.goal_assisted = assisted
		return ""
	var answer := {"text": text, "assisted": assisted}
	if state.difficulty == "challenge":
		answer = {"text": state.first_reply, "follow_up": text, "assisted": assisted or state.goal_assisted}
	state.answers.append(answer)
	state.first_reply = ""
	state.goal_assisted = false
	return action

func restore(value: Dictionary) -> bool:
	# JSON numbers are floats in Godot; array membership is type-sensitive.
	if (value.get("version") != 1 and value.get("version") != 2) or not value.get("scene") is String or not value.get("role") is String:
		return false
	var difficulty: String = "everyday" if value.version == 1 else str(value.get("difficulty", ""))
	if difficulty not in ["everyday", "challenge"] or role_data(value.scene, value.role, difficulty).is_empty() or not value.get("answers") is Array or value.answers.size() > 3:
		return false
	var candidate := _new_state(value.scene, value.role, difficulty)
	for answer in value.answers:
		if not answer is Dictionary or not answer.get("text") is String or answer.text.strip_edges() == "" or answer.text.length() > 400 or not answer.get("assisted") is bool:
			return false
		var record := {"text": answer.text, "assisted": answer.assisted}
		if difficulty == "challenge":
			if not answer.get("follow_up") is String or answer.follow_up.strip_edges() == "" or answer.follow_up.length() > 400:
				return false
			record.follow_up = answer.follow_up
		elif answer.get("follow_up", "") != "":
			return false
		candidate.answers.append(record)
	for field in ["correction", "feedback", "acknowledgement"]:
		if not value.get(field) is String or value[field].length() > (160 if field == "acknowledgement" else 240):
			return false
		candidate[field] = value[field]
	if value.version == 2:
		if not value.get("first_reply") is String or value.first_reply.length() > 400 or not value.get("goal_assisted") is bool:
			return false
		candidate.first_reply = value.first_reply
		candidate.goal_assisted = value.goal_assisted
	if (difficulty == "everyday" or candidate.answers.size() == 3) and (candidate.first_reply != "" or candidate.goal_assisted):
		return false
	if (candidate.goal_assisted and candidate.first_reply == "") or (candidate.first_reply != "" and candidate.first_reply.strip_edges() == ""):
		return false
	if candidate.answers.size() == 3 and candidate.correction != "":
		return false
	state = candidate
	return true
