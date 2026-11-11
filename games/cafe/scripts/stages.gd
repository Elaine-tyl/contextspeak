extends RefCounted

const LABELS := [
	"Continue saved journey",
	"Counter ordering",
	"Friend preferences",
	"Weekend plans",
	"Invitations",
	"Completion preview"
]
const SAMPLE_ANSWERS := [
	"I like coffee because it helps me wake up.",
	"I would like to go to the park this weekend.",
	"Would you like to come to the park with me on Saturday at ten?"
]
var selected := 0

func select(value: int) -> bool:
	if value < 0 or value >= LABELS.size():
		return false
	selected = value
	return true

func preview() -> bool:
	return selected != 0

func fixture() -> Dictionary:
	if not preview():
		return {}
	var ordered := selected > 1
	var answers: Array = []
	for index in maxi(0, selected - 2):
		answers.append({"text": SAMPLE_ANSWERS[index], "feedback": "Sample answer from an earlier practice step."})
	return {
		"version": 2,
		"order": {
			"version": 1,
			"stage": "complete" if ordered else "order",
			"items": {"coffee": 2} if ordered else {},
			"temperature": "hot" if ordered else "",
			"service": "here" if ordered else "",
			"complete": ordered
		},
		"friend": {
			"version": 1,
			"phase": "complete" if selected == 5 else "seated" if ordered else "counter",
			"answers": answers
		}
	}
