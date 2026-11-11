extends RefCounted

const MENU := {"coffee": 400, "latte": 650, "tea": 300, "lemonade": 350}
const BUDGET := 1200
var state: Dictionary = fresh()

func fresh() -> Dictionary:
	return {"version": 1, "stage": "order", "items": {}, "temperature": "", "service": "", "complete": false}

func total(items: Dictionary) -> int:
	var cents := 0
	for drink in items:
		cents += MENU[drink] * items[drink]
	return cents

func valid_order(items: Dictionary) -> bool:
	var count := 0
	for drink in items:
		if not MENU.has(drink) or not items[drink] is int or items[drink] < 1 or items[drink] > 2:
			return false
		count += items[drink]
	return count == 2 and total(items) <= BUDGET

func order_text() -> String:
	var parts: PackedStringArray = []
	for drink in state.items:
		parts.append("%d %s%s" % [state.items[drink], drink, "s" if state.items[drink] == 2 else ""])
	return ", ".join(parts)

func prompt() -> String:
	match state.stage:
		"temperature": return "Would you like your drinks hot or iced?"
		"service": return "Are your drinks for here or to take away?"
		"confirm": return "That's %s, %s, %s. Your total is $%.2f. Shall I place your order?" % [order_text(), state.temperature, state.service, total(state.items) / 100.0]
		"complete": return "Here are your drinks! Enjoy your time together."
	return "Hi! I'm Maya. What can I get for you and your friend? Your budget is twelve dollars."

func apply(intent: Dictionary) -> Dictionary:
	if state.complete:
		return result(prompt())
	var reply := ""
	match intent.get("action", "unknown"):
		"ask_price":
			reply = "Coffee is four dollars, latte six fifty, tea three dollars, and lemonade three fifty. " + prompt()
		"repeat": reply = prompt()
		"cancel":
			state = fresh()
			reply = "No problem. Let's start a new order. " + prompt()
		"order":
			var items: Dictionary = {}
			if not intent.get("items") is Array or intent.items.size() > 2:
				return result("Please order two drinks from the menu.")
			for item in intent.items:
				if not item is Dictionary or not MENU.has(item.get("drink")):
					return result("Please choose coffee, latte, tea or lemonade.")
				var raw: Variant = item.get("quantity")
				if not (raw is int or raw is float) or raw != int(raw) or raw < 1 or raw > 2:
					return result("Please order two drinks in total.")
				var quantity := int(raw)
				items[item.drink] = items.get(item.drink, 0) + quantity
			state = fresh()
			state.items = items
			if total(items) > BUDGET:
				reply = "That costs $%.2f, which is over your twelve-dollar budget. What would you like instead?" % (total(items) / 100.0)
			elif not valid_order(items):
				reply = "You need two drinks, one for you and one for your friend. Please tell me your complete order."
			else:
				if intent.get("temperature") in ["hot", "iced"]:
					state.temperature = intent.temperature
				if intent.get("service") in ["here", "takeaway"]:
					state.service = intent.service
				advance()
		"temperature":
			if state.stage in ["temperature", "service", "confirm"] and intent.get("temperature") in ["hot", "iced"]:
				state.temperature = intent.temperature
				advance()
		"service":
			if state.stage in ["service", "confirm"] and intent.get("service") in ["here", "takeaway"]:
				state.service = intent.service
				advance()
		"confirm":
			if state.stage == "confirm" and valid_order(state.items) and state.temperature != "" and state.service != "":
				state.complete = true
				state.stage = "complete"
		_:
			reply = "I didn't quite understand. You can ask the price, give your order, or ask me to repeat. " + prompt()
	return result(reply if reply != "" else prompt())

func advance() -> void:
	state.stage = "temperature" if state.temperature == "" else "service" if state.service == "" else "confirm"

func result(reply: String) -> Dictionary:
	var goals := int(valid_order(state.items)) + int(state.temperature != "") + int(state.service != "") + int(state.complete)
	return {"reply": reply, "complete": state.complete, "score": goals * 25, "reward": 2 if state.complete else 0, "order": order_text() if not state.items.is_empty() else "Your order is empty", "total": total(state.items), "stage": state.stage}

func parse(text: String) -> Dictionary:
	var words := text.to_lower().strip_edges().replace("’", "'")
	if "how much" in words or "price" in words or "cost" in words:
		return {"action": "ask_price"}
	if "repeat" in words or "say that again" in words:
		return {"action": "repeat"}
	if "cancel" in words or "start over" in words:
		return {"action": "cancel"}
	var negative := RegEx.new()
	negative.compile("\\b(no|not|incorrect|wrong|don.t)\\b")
	var is_negative: bool = negative.search(words) != null or "n't" in words
	# Strip explicitly rejected options before reading a corrected choice.
	var rejected := RegEx.new()
	rejected.compile("\\b(?:not|no|don't want|do not want|don't like|do not like)\\s+(?:hot|iced|cold|for here|here|to go|take away|takeaway)\\b")
	var positive := rejected.sub(words, "", true)
	if negative.search(positive) or "n't" in positive:
		return {"action": "repeat"}
	var expression := RegEx.new()
	expression.compile("(one|two|1|2|a|an)\\s+(?:(hot|iced)\\s+)?(coffees?|lattes?|teas?|lemonades?)\\b")
	var items: Array = []
	for match_result in expression.search_all(words):
		items.append({"drink": match_result.get_string(3).trim_suffix("s"), "quantity": 2 if match_result.get_string(1) in ["two", "2"] else 1})
	var temperature := "iced" if "iced" in positive or "cold" in positive else "hot" if "hot" in positive else ""
	var service := "takeaway" if "take away" in positive or "takeaway" in positive or "to go" in positive else "here" if "here" in positive else ""
	if not items.is_empty():
		if is_negative:
			return {"action": "repeat"}
		return {"action": "order", "items": items, "temperature": temperature, "service": service}
	if state.stage in ["temperature", "service", "confirm"] and temperature != "":
		return {"action": "temperature", "temperature": temperature}
	if state.stage in ["service", "confirm"] and service != "":
		return {"action": "service", "service": service}
	var agreement := RegEx.new()
	agreement.compile("^(yes|yeah|yep|correct|that is correct|that's correct|go ahead|sounds good)\\b")
	if state.stage == "confirm":
		if not is_negative and not "?" in words and agreement.search(words):
			return {"action": "confirm"}
		# A model must never upgrade an ambiguous final reply into agreement.
		return {"action": "repeat"}
	if is_negative:
		return {"action": "repeat"}
	return {"action": "unknown"}

func restore(saved: Dictionary) -> bool:
	state = fresh()
	if saved.get("version") != 1 or not saved.get("items") is Dictionary:
		return false
	# JSON numbers are floats; accept only exact, bounded integers.
	var items: Dictionary = {}
	for drink in saved.items:
		var quantity: Variant = saved.items[drink]
		if not MENU.has(drink) or not (quantity is int or quantity is float) or quantity != int(quantity) or quantity < 1 or quantity > 2:
			return false
		items[drink] = int(quantity)
	if not saved.get("temperature", "") in ["", "hot", "iced"] or not saved.get("service", "") in ["", "here", "takeaway"]:
		return false
	state.items = items
	if valid_order(items):
		state.temperature = saved.get("temperature", "")
		state.service = saved.get("service", "")
		advance()
		if saved.get("complete") == true and state.stage == "confirm":
			state.complete = true
			state.stage = "complete"
	return true

func summary() -> Dictionary:
	return result(prompt())
