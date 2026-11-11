extends SceneTree

var selected_stage := -1
var restarted_missions := 0
var closed_dialogues := 0

func stage_requested(value: int) -> void:
	selected_stage = value

func close_requested() -> void:
	closed_dialogues += 1

func contrast(first: Color, second: Color) -> float:
	var first_light := first.srgb_to_linear().get_luminance()
	var second_light := second.srgb_to_linear().get_luminance()
	return (maxf(first_light, second_light) + 0.05) / (minf(first_light, second_light) + 0.05)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var interface = load("res://scripts/interface.gd").new()
	root.add_child(interface)
	await process_frame
	await process_frame
	if interface.get("stage_picker") == null:
		push_error("The header must expose a keyboard-accessible practice stage selector")
		quit(1)
		return
	interface.stage_requested.connect(stage_requested)
	var scene_menu = load("res://scripts/scene_menu.gd").new(interface, [])
	await process_frame
	await process_frame
	var scene_entry: Button = interface.header_row.get_child(1)
	if not scene_entry.get_global_rect().has_point(Vector2(298, 54)) or not interface.stage_picker.get_global_rect().has_point(Vector2(1040, 64)) or interface.hud.position.y != 96:
		push_error("The desktop Scenes, practice stage and mission panel anchors must stay compatible with the existing browser controls")
		quit(1)
		return
	if selected_stage != -1 or interface.stage_picker.selected != 0:
		push_error("Populating practice choices must preserve the saved journey without selecting a preview")
		quit(1)
		return
	interface.stage_picker.select(3)
	if selected_stage != -1:
		push_error("Changing the pending option must wait for explicit Select")
		quit(1)
		return
	interface.stage_select.pressed.emit()
	if selected_stage != 3:
		push_error("Select must activate the chosen stage")
		quit(1)
		return
	interface.update_stage(3)
	if not interface.stage_hint.text.contains("Test preview"):
		push_error("Fixture stages must be clearly labelled as test previews")
		quit(1)
		return
	interface.update_stage(0)
	interface.show_dialogue("Hi! I'm Maya. What can I get for you and your friend? Your budget is twelve dollars.")
	await process_frame
	await process_frame
	# Simulate a narrow first frame followed by the actual browser viewport.
	interface.menu.size.x = 32
	interface.layout()
	await process_frame
	await process_frame
	if interface.header.size.x < interface.root.size.x - 40 or interface.menu.size.x < 210:
		push_error("Deferred panel reflow must preserve assigned header and menu widths")
		quit(1)
		return
	if interface.header.size.y > 90:
		push_error("The brand header must remain compact after text reflow: %s" % interface.header.size)
		quit(1)
		return
	if interface.header.get_global_rect().end.y > interface.hud.position.y or interface.hud.get_global_rect().end.y > interface.root.size.y:
		push_error("Stage controls must fit above the HUD and keep mission controls inside the viewport: header %s, HUD %s, viewport %s" % [interface.header.get_global_rect(), interface.hud.get_global_rect(), interface.root.size])
		quit(1)
		return
	interface.busy(true)
	if interface.stage_picker.disabled or interface.stage_select.disabled:
		push_error("A busy preview must not prevent switching practice stages")
		quit(1)
		return
	interface.busy(false)
	interface.stage_picker.grab_focus()
	if not interface.stage_picker.has_focus() or interface.stage_select.focus_mode != Control.FOCUS_ALL:
		push_error("Practice selection and its explicit action must be keyboard accessible")
		quit(1)
		return
	if interface.dialogue.size.y > 380:
		push_error("A short NPC turn must not inflate the dialogue panel beyond 380 pixels")
		quit(1)
		return
	# A LineEdit handles ui_cancel before the world's unhandled key shortcut.
	# The focused typed fallback must still offer the same Escape exit as buttons.
	interface.close_requested.connect(close_requested)
	interface.reply.grab_focus()
	var escape := InputEventKey.new()
	escape.keycode = KEY_ESCAPE
	escape.pressed = true
	Input.parse_input_event(escape)
	await process_frame
	if closed_dialogues != 1:
		push_error("Escape while typing must request exactly one conversation close")
		quit(1)
		return
	escape.pressed = false
	Input.parse_input_event(escape)
	await process_frame
	# Long local feedback must remain readable without moving the reply actions
	# below the viewport or hiding the speaker's identity and close action.
	interface.npc_line.text = "Could you tell me about a drink you enjoy, why you like it, and when you usually order it? ".repeat(6)
	interface.transcript.text = "You said: " + "I like coffee because it is warm and I share it with friends. ".repeat(8)
	interface.feedback.text = "Try this: I would like two iced coffees to have here, please. Say the full sentence again, and include the temperature and where you will enjoy your order. ".repeat(12)
	await process_frame
	await process_frame
	await process_frame
	if interface.dialogue.position.y < 80 or interface.dialogue.get_global_rect().end.y > interface.root.size.y - 16 or not interface.dialogue.get_global_rect().encloses(interface.send.get_global_rect()):
		push_error("Long corrections must keep dialogue and reply actions inside the viewport")
		quit(1)
		return
	if not interface.dialogue.get_global_rect().encloses(interface.close_button.get_global_rect()):
		push_error("Long feedback must keep the dialogue close action visible")
		quit(1)
		return
	var feedback_scroll := interface.feedback.get_parent() as ScrollContainer
	if feedback_scroll == null or feedback_scroll.get_v_scroll_bar().max_value <= feedback_scroll.size.y:
		push_error("Long feedback must be scrollable rather than clipped or truncated")
		quit(1)
		return
	feedback_scroll.scroll_vertical = int(feedback_scroll.get_v_scroll_bar().max_value)
	await process_frame
	if feedback_scroll.scroll_vertical <= 0:
		push_error("The end of a long correction must remain reachable")
		quit(1)
		return
	interface.reply.grab_focus()
	var tab := InputEventKey.new()
	tab.keycode = KEY_TAB
	tab.pressed = true
	Input.parse_input_event(tab.duplicate())
	await process_frame
	tab.pressed = false
	Input.parse_input_event(tab.duplicate())
	if not interface.record.has_focus():
		push_error("Tab from the typed reply must continue to the existing Record action")
		quit(1)
		return
	for region in [interface.speech_scroll, feedback_scroll]:
		var scrollbar: VScrollBar = region.get_v_scroll_bar()
		var reached := false
		for _step in 16:
			tab.pressed = true
			Input.parse_input_event(tab.duplicate())
			await process_frame
			tab.pressed = false
			Input.parse_input_event(tab.duplicate())
			if scrollbar.has_focus():
				reached = true
				break
		if not reached:
			push_error("Every overflowing dialogue text region must be reachable with Tab")
			quit(1)
			return
		var navigate := InputEventKey.new()
		for key in [KEY_HOME, KEY_DOWN, KEY_END]:
			navigate.keycode = key
			navigate.pressed = true
			Input.parse_input_event(navigate.duplicate())
			await process_frame
			navigate.pressed = false
			Input.parse_input_event(navigate.duplicate())
			if key == KEY_HOME and region.scroll_vertical != 0:
				push_error("Home must let keyboard readers return to the beginning of long dialogue text")
				quit(1)
				return
			if key == KEY_DOWN and region.scroll_vertical < 16:
				push_error("Down must scroll long dialogue text by a readable text-line step")
				quit(1)
				return
			if key == KEY_END and region.scroll_vertical < scrollbar.max_value - scrollbar.page - 1:
				push_error("End must expose the last line of a long NPC turn or correction: scroll %s, max %s, page %s" % [region.scroll_vertical, scrollbar.max_value, scrollbar.page])
				quit(1)
				return
		var scroll_focus: StyleBoxFlat = scrollbar.get_theme_stylebox("scroll_focus")
		var scroll_card: StyleBoxFlat = interface.dialogue.get_theme_stylebox("panel")
		if scroll_focus.border_width_left < 2 or contrast(scroll_focus.border_color, scroll_card.bg_color) < 3:
			push_error("The focused dialogue scrollbar must remain visible against its card")
			quit(1)
			return
	for control in [interface.walk, interface.talk, interface.restart_button, interface.stage_picker, interface.stage_select, interface.record, interface.send, interface.replay, interface.close_button]:
		if control.size.y < 42:
			push_error("All practice actions must retain a comfortable 42 pixel hit target")
			quit(1)
			return
		for state in ["normal", "hover", "pressed", "focus"]:
			var font_name: String = "font_color" if state == "normal" else "font_%s_color" % state
			var background: StyleBoxFlat = control.get_theme_stylebox("normal" if state == "focus" else state)
			if contrast(control.get_theme_color(font_name), background.bg_color) < 4.5:
				push_error("Practice action text must remain readable in %s state: %s" % [state, control.text])
				quit(1)
				return
		var focus: StyleBoxFlat = control.get_theme_stylebox("focus")
		var card: StyleBoxFlat = interface.dialogue.get_theme_stylebox("panel")
		if focus.border_width_left < 2 or contrast(focus.border_color, card.bg_color) < 3:
			push_error("Keyboard focus must remain visible against its surrounding card")
			quit(1)
			return
	interface.reply_allowed = false
	interface.completed = false
	interface.busy(false)
	interface.show_dialogue("The next task is with another NPC. Keep exploring.")
	if interface.reply.editable or not interface.record.disabled or not interface.send.disabled or not interface.close_button.has_focus():
		push_error("A future NPC greeting must keep replies disabled and focus the usable close action")
		quit(1)
		return
	interface.reply_allowed = true
	interface.busy(false)
	interface.show_dialogue("Your next question is ready.")
	if not interface.reply.has_focus() or not interface.reply.editable or interface.record.disabled or interface.send.disabled:
		push_error("Returning to a task NPC must restore the focused typed and recorded reply controls")
		quit(1)
		return
	interface.recording(true)
	interface.mic_activity(0, 0.6)
	interface.mic_activity(0.2, 0.1)
	if not interface.recording_status.text.begins_with("Recording"):
		push_error("A ready browser microphone must not revert to the preparation indicator")
		quit(1)
		return
	interface.recording(false)
	interface.update({"score": 100, "complete": true, "order": "2 coffees", "total": 800, "stage": "complete"})
	interface.busy(false)
	if not interface.record.disabled or not interface.send.disabled:
		push_error("Completed missions must keep reply controls disabled after an async request finishes")
		quit(1)
		return
	# Simulate the actual narrow in-app browser at CSS pixel density. Its controls
	# must reflow instead of shrinking an entire 1280px HUD into unreadable text.
	root.size = Vector2i(573, 793)
	root.content_scale_size = Vector2i(573, 793)
	await process_frame
	await process_frame
	await process_frame
	if interface.header.get_global_rect().end.x > 573 or interface.header.get_global_rect().end.y >= interface.hud.position.y:
		push_error("The narrow header must keep all scene and practice controls above the mission card: header %s, HUD %s, root %s" % [interface.header.get_global_rect(), interface.hud.get_global_rect(), interface.root.size])
		quit(1)
		return
	for control in [scene_entry, interface.stage_picker, interface.stage_select]:
		if not Rect2(Vector2.ZERO, Vector2(573, 793)).encloses(control.get_global_rect()) or not control.is_visible_in_tree():
			push_error("The narrow layout must preserve every existing mission and practice action: %s" % control.text)
			quit(1)
			return
	if interface.get("mission_toggle") == null or not interface.mission_toggle.is_visible_in_tree():
		push_error("The narrow HUD must expose an operable Mission details toggle")
		quit(1)
		return
	if interface.hud.size.y > 80 or interface.hud.size.x > 220:
		push_error("An open conversation must leave the NPC camera clear by reducing the mission card to its details action")
		quit(1)
		return
	interface.npc_line.text = "What would you like to do with your friend this weekend? ".repeat(8)
	interface.feedback.text = "Please give a reason and suggest a time. ".repeat(30)
	interface.mission_toggle.pressed.emit()
	await process_frame
	await process_frame
	for control in [interface.walk, interface.talk, interface.restart_button]:
		if not control.is_visible_in_tree() or not Rect2(Vector2.ZERO, Vector2(573, 793)).encloses(control.get_global_rect()):
			push_error("Expanding Mission details must restore each existing mission action: %s" % control.text)
			quit(1)
			return
	if not interface.mission_description.is_visible_in_tree() or not interface.receipt.is_visible_in_tree() or not interface.status.is_visible_in_tree():
		push_error("Mission details must retain the description, order and local-AI status")
		quit(1)
		return
	interface.restart_requested.connect(func(): restarted_missions += 1)
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.position = interface.restart_button.get_global_rect().get_center()
	click.pressed = true
	Input.parse_input_event(click.duplicate())
	await process_frame
	click.pressed = false
	Input.parse_input_event(click.duplicate())
	await process_frame
	if restarted_missions != 1:
		push_error("Expanded mission actions must receive clicks above overlapping long dialogue")
		quit(1)
		return
	interface.mission_toggle.pressed.emit()
	await process_frame
	await process_frame
	if interface.walk.is_visible_in_tree() or interface.hud.size.y > 80:
		push_error("Collapsing Mission details must restore the unobstructed conversation view")
		quit(1)
		return
	if not Rect2(Vector2.ZERO, Vector2(573, 793)).encloses(interface.dialogue.get_global_rect()) or not interface.dialogue.get_global_rect().encloses(interface.send.get_global_rect()):
		push_error("Narrow long feedback must leave the reply actions inside the visible dialogue")
		quit(1)
		return
	scene_menu.show_menu()
	await process_frame
	await process_frame
	if not Rect2(Vector2.ZERO, Vector2(573, 793)).encloses(scene_menu.panel.get_global_rect()):
		push_error("Scene and role selection must fit the narrow browser without clipping its actions")
		quit(1)
		return
	scene_menu.hide_menu()
	interface.dialogue.hide()
	interface.mission_title.text = "An afternoon of conversation with your friend"
	interface.goal.text = "Tell your friend which activity you prefer, explain why, then suggest a time and a place to meet after class. ".repeat(2)
	await process_frame
	await process_frame
	if interface.hud.size.y > 140 or not interface.mission_title.is_visible_in_tree() or not interface.progress.is_visible_in_tree() or not interface.goal.is_visible_in_tree():
		push_error("Free exploration must keep a compact mission title, progress and current-goal summary: card %s, goal %s, title %s" % [interface.hud.size, interface.goal.size, interface.mission_title.size])
		quit(1)
		return
	interface.show_dialogue("Hi! I'm Maya. What can I get for you and your friend? Your budget is twelve dollars.")
	interface.feedback.text = "Ask for a price or say Please repeat whenever you need."
	interface.transcript.text = "Speak naturally. Your recording stays on this computer."
	interface.npc_line.text = "Hi! I'm Maya. What can I get for you and your friend? Your budget is twelve dollars."
	await process_frame
	await process_frame
	await process_frame
	if interface.dialogue.size.y > 380:
		push_error("A short NPC turn must stay compact in the narrow browser: %s" % interface.dialogue.size)
		quit(1)
		return
	root.size = Vector2i(1280, 800)
	root.content_scale_size = Vector2i(1280, 800)
	await process_frame
	await process_frame
	await process_frame
	if interface.mission_toggle.is_visible_in_tree() or not interface.walk.is_visible_in_tree() or not interface.mission_description.is_visible_in_tree() or not interface.receipt.is_visible_in_tree():
		push_error("Returning to desktop width must restore the original full mission controls")
		quit(1)
		return
	if not scene_entry.get_global_rect().has_point(Vector2(298, 54)) or not interface.stage_picker.get_global_rect().has_point(Vector2(1040, 64)) or interface.hud.position.y != 96:
		push_error("Responsive details must preserve the original desktop UI anchors after resizing back")
		quit(1)
		return
	print("Cafe dialogue layout and completion controls: passed")
	quit(0)
