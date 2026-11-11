extends CanvasLayer

signal talk_requested
signal walk_requested
signal restart_requested
signal close_requested
signal record_requested
signal replay_requested
signal reply_submitted(text: String)
signal stage_requested(stage: int)

const Stages = preload("stages.gd")

const INK := Color("#3b3029")
const MUTED := Color("#716357")
const CREAM := Color("#fcf8ef")
const BORDER := Color("#d7c9b5")
const GREEN := Color("#446650")
const SAGE := Color("#e8eddf")
const TERRACOTTA := Color("#a7513e")
var root: Control
var hud: PanelContainer
var menu: PanelContainer
var header: PanelContainer
var dialogue: PanelContainer
var status: Label
var progress: Label
var goal: Label
var receipt: Label
var npc_line: Label
var transcript: Label
var feedback: Label
var reply: LineEdit
var send: Button
var record: Button
var replay: Button
var talk: Button
var walk: Button
var npc_title: Label
var meter: ProgressBar
var recording_status: Label
var at_table := false
var completed := false
var is_recording := false
var microphone_ready := false
var stage_picker: OptionButton
var stage_select: Button
var stage_hint: Label
var active_stage := 0
var restart_button: Button
var reply_allowed := true
var close_button: Button
var stage_controls: VBoxContainer
var location: Label
var mission_title: Label
var mission_description: Label
var speech_scroll: ScrollContainer
var speech_content: VBoxContainer
var feedback_scroll: ScrollContainer
var reduce_motion := false
var dialogue_transition: Tween
var header_stack: VBoxContainer
var header_row: HBoxContainer
var brand: Label
var mission_columns: BoxContainer
var mission_info: VBoxContainer
var mission_actions: VBoxContainer
var mission_summary: VBoxContainer
var mission_summary_header: HBoxContainer
var mission_toggle: Button
var mission_expanded := false
var progress_style: StyleBoxFlat

func label(text: String, size := 16) -> Label:
	var item := Label.new()
	item.text = text
	item.add_theme_font_size_override("font_size", size)
	item.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return item

func style(colour: Color, border: Color, radius := 10) -> StyleBoxFlat:
	var value := StyleBoxFlat.new()
	value.bg_color = colour
	value.border_color = border
	value.set_border_width_all(1)
	value.set_corner_radius_all(radius)
	value.content_margin_left = 16
	value.content_margin_right = 16
	value.content_margin_top = 10
	value.content_margin_bottom = 10
	return value

func focus_style() -> StyleBoxFlat:
	var value := style(Color.TRANSPARENT, TERRACOTTA)
	value.set_border_width_all(2)
	value.set_expand_margin_all(2)
	return value

func button_style(colour: Color, border: Color) -> StyleBoxFlat:
	var value := style(colour, border)
	value.content_margin_top = 8
	value.content_margin_bottom = 8
	return value

func button(text: String, action: Callable, primary := false) -> Button:
	var item := Button.new()
	item.text = text
	item.custom_minimum_size.y = 42
	if primary:
		item.add_theme_stylebox_override("normal", button_style(GREEN, GREEN))
		item.add_theme_stylebox_override("hover", button_style(Color("#3d5c49"), Color("#3d5c49")))
		item.add_theme_stylebox_override("pressed", button_style(Color("#324d3e"), Color("#324d3e")))
		item.add_theme_color_override("font_color", Color.WHITE)
		item.add_theme_color_override("font_hover_color", Color.WHITE)
		item.add_theme_color_override("font_pressed_color", Color.WHITE)
		item.add_theme_color_override("font_focus_color", Color.WHITE)
	item.pressed.connect(action)
	return item

func panel() -> PanelContainer:
	var item := PanelContainer.new()
	var card := style(CREAM, BORDER, 16)
	card.shadow_color = Color(0.20, 0.13, 0.08, 0.16)
	card.shadow_size = 10
	card.shadow_offset = Vector2(0, 4)
	item.add_theme_stylebox_override("panel", card)
	item.minimum_size_changed.connect(func(): fit_panel.call_deferred(item))
	return item

func fit_panel(item: PanelContainer) -> void:
	# Read width after layout settles; never restore an earlier narrow frame.
	item.size.y = item.get_combined_minimum_size().y

func content(parent: PanelContainer) -> VBoxContainer:
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 10)
	parent.add_child(column)
	return column

func text_scroll() -> ScrollContainer:
	var scroll := ScrollContainer.new()
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	# Text may be long; only the text region scrolls, leaving reply actions visible.
	scroll.follow_focus = true
	var scrollbar := scroll.get_v_scroll_bar()
	scrollbar.focus_mode = Control.FOCUS_ALL
	scrollbar.custom_step = 24
	# Arrow keys scroll this region without moving focus to another UI action.
	scrollbar.focus_neighbor_top = NodePath(".")
	scrollbar.focus_neighbor_bottom = NodePath(".")
	return scroll

func _ready() -> void:
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var theme := Theme.new()
	theme.default_font_size = 16
	theme.set_color("font_color", "Label", INK)
	for kind in ["Button", "OptionButton"]:
		theme.set_stylebox("normal", kind, button_style(Color("#fffdf7"), BORDER))
		theme.set_stylebox("hover", kind, button_style(Color("#f2eadb"), Color("#b9a68b")))
		theme.set_stylebox("pressed", kind, button_style(SAGE, Color("#a7b39a")))
		theme.set_stylebox("disabled", kind, button_style(Color("#e9e4da"), Color("#d8cfc1")))
		theme.set_stylebox("focus", kind, focus_style())
		for state in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
			theme.set_color(state, kind, INK)
		theme.set_color("font_disabled_color", kind, Color("#81776b"))
	theme.set_color("arrow_color", "OptionButton", INK)
	theme.set_stylebox("panel", "PopupMenu", style(CREAM, BORDER, 10))
	theme.set_stylebox("hover", "PopupMenu", style(SAGE, BORDER, 6))
	theme.set_color("font_color", "PopupMenu", INK)
	theme.set_color("font_hover_color", "PopupMenu", INK)
	theme.set_stylebox("normal", "LineEdit", style(Color("#fffdf7"), Color("#b9aa95")))
	theme.set_stylebox("focus", "LineEdit", focus_style())
	theme.set_stylebox("read_only", "LineEdit", style(Color("#e9e4da"), Color("#d8cfc1")))
	theme.set_color("font_color", "LineEdit", INK)
	theme.set_color("font_uneditable_color", "LineEdit", MUTED)
	theme.set_color("font_placeholder_color", "LineEdit", MUTED)
	theme.set_color("caret_color", "LineEdit", GREEN)
	theme.set_color("selection_color", "LineEdit", Color("#cbd8c3"))
	theme.set_color("font_selected_color", "LineEdit", INK)
	theme.set_stylebox("panel", "TooltipPanel", style(CREAM, BORDER, 8))
	theme.set_color("font_color", "TooltipLabel", INK)
	var track := StyleBoxFlat.new()
	track.bg_color = Color("#e9e1d4")
	track.set_corner_radius_all(3)
	theme.set_stylebox("background", "ProgressBar", track)
	var fill := track.duplicate() as StyleBoxFlat
	fill.bg_color = GREEN
	theme.set_stylebox("fill", "ProgressBar", fill)
	var scroll_track := StyleBoxFlat.new()
	scroll_track.bg_color = Color(0.44, 0.36, 0.28, 0.06)
	scroll_track.content_margin_left = 6
	scroll_track.set_corner_radius_all(3)
	theme.set_stylebox("scroll", "VScrollBar", scroll_track)
	var scroll_focus := focus_style()
	# Keep the ring inside the clipped text region so every edge stays visible.
	scroll_focus.set_expand_margin_all(0)
	scroll_focus.set_corner_radius_all(3)
	theme.set_stylebox("scroll_focus", "VScrollBar", scroll_focus)
	for state in ["grabber", "grabber_highlight", "grabber_pressed"]:
		var thumb := scroll_track.duplicate() as StyleBoxFlat
		thumb.bg_color = Color("#b3a38c") if state == "grabber" else MUTED
		theme.set_stylebox(state, "VScrollBar", thumb)
	get_tree().root.theme = theme
	root.theme = theme
	add_child(root)
	if OS.has_feature("web"):
		var window := JavaScriptBridge.get_interface("window")
		reduce_motion = bool(window.matchMedia("(prefers-reduced-motion: reduce)").matches)

	header = panel()
	var header_style := header.get_theme_stylebox("panel").duplicate() as StyleBoxFlat
	header_style.content_margin_top = 8
	header_style.content_margin_bottom = 8
	header.add_theme_stylebox_override("panel", header_style)
	root.add_child(header)
	header_stack = VBoxContainer.new()
	header_stack.add_theme_constant_override("separation", 8)
	header.add_child(header_stack)
	header_row = HBoxContainer.new()
	var title := header_row
	title.add_theme_constant_override("separation", 20)
	header_stack.add_child(title)
	brand = label("ContextSpeak", 24)
	brand.custom_minimum_size.x = 200
	title.add_child(brand)
	location = label("AFTER CLASS CAFE  /  A drink with a friend", 16)
	location.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.add_child(location)
	for item in title.get_children():
		item.autowrap_mode = TextServer.AUTOWRAP_OFF
		item.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		item.vertical_alignment = VERTICAL_ALIGNMENT_CENTER

	stage_controls = VBoxContainer.new()
	stage_controls.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	stage_controls.add_theme_constant_override("separation", 2)
	title.add_child(stage_controls)
	stage_hint = label("Practice stage · Saved journey", 12)
	stage_hint.add_theme_color_override("font_color", MUTED)
	stage_hint.autowrap_mode = TextServer.AUTOWRAP_OFF
	stage_controls.add_child(stage_hint)
	var choices := HBoxContainer.new()
	stage_controls.add_child(choices)
	stage_picker = OptionButton.new()
	stage_picker.custom_minimum_size = Vector2(240, 42)
	stage_picker.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	stage_picker.tooltip_text = "Practice stage. Choose a stage, then press Select."
	for stage_name in Stages.LABELS:
		stage_picker.add_item(stage_name)
	stage_picker.select(0)
	choices.add_child(stage_picker)
	stage_select = button("Select", func(): stage_requested.emit(stage_picker.selected))
	stage_select.tooltip_text = "Start the selected practice stage"
	choices.add_child(stage_select)

	hud = panel()
	root.add_child(hud)
	var task := content(hud)
	mission_summary = VBoxContainer.new()
	mission_summary.add_theme_constant_override("separation", 3)
	task.add_child(mission_summary)
	mission_summary_header = HBoxContainer.new()
	mission_summary_header.add_theme_constant_override("separation", 8)
	mission_summary.add_child(mission_summary_header)
	mission_toggle = button("Mission details", toggle_mission)
	mission_toggle.tooltip_text = "Show or hide mission instructions, movement actions and your order"
	mission_summary_header.add_child(mission_toggle)
	mission_summary.hide()
	mission_columns = BoxContainer.new()
	mission_columns.vertical = true
	mission_columns.add_theme_constant_override("separation", 10)
	task.add_child(mission_columns)
	mission_info = VBoxContainer.new()
	mission_info.add_theme_constant_override("separation", 8)
	mission_info.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mission_columns.add_child(mission_info)
	mission_actions = VBoxContainer.new()
	mission_actions.add_theme_constant_override("separation", 8)
	mission_columns.add_child(mission_actions)
	var mission_caption := label("YOUR MISSION", 12)
	mission_caption.add_theme_color_override("font_color", MUTED)
	mission_info.add_child(mission_caption)
	mission_title = label("A drink with a friend", 22)
	mission_info.add_child(mission_title)
	mission_description = label("Order two drinks. Stay within $12.", 16)
	mission_info.add_child(mission_description)
	progress = label("Task goals: 0/4", 14)
	progress_style = style(SAGE, Color("#cfd8c3"), 8)
	progress_style.content_margin_left = 10
	progress_style.content_margin_right = 10
	progress_style.content_margin_top = 6
	progress_style.content_margin_bottom = 6
	progress.add_theme_stylebox_override("normal", progress_style)
	progress.add_theme_color_override("font_color", Color("#40563d"))
	mission_info.add_child(progress)
	goal = label("Meet Maya at the counter.", 16)
	mission_info.add_child(goal)
	receipt = label("Your order is empty", 14)
	receipt.add_theme_color_override("font_color", MUTED)
	mission_info.add_child(receipt)
	walk = button("Walk to counter [Space]", func(): walk_requested.emit(), true)
	mission_actions.add_child(walk)
	talk = button("Talk to Maya [E]", func(): talk_requested.emit())
	talk.disabled = true
	mission_actions.add_child(talk)
	restart_button = button("Restart mission", func(): restart_requested.emit())
	mission_actions.add_child(restart_button)
	var shortcuts := label("Move: WASD / arrows\nTalk: E · Leave conversation: Esc", 12)
	shortcuts.add_theme_color_override("font_color", MUTED)
	mission_actions.add_child(shortcuts)
	status = label("Connecting to local AI…", 12)
	status.add_theme_color_override("font_color", MUTED)
	mission_actions.add_child(status)

	menu = panel()
	root.add_child(menu)
	var drinks := content(menu)
	var menu_caption := label("TODAY'S MENU", 12)
	menu_caption.add_theme_color_override("font_color", MUTED)
	drinks.add_child(menu_caption)
	drinks.add_child(label("Made for conversation.", 18))
	drinks.add_child(label("Coffee                 $4.00\nLatte                    $6.50\nTea                       $3.00\nLemonade             $3.50", 17))
	drinks.add_child(label("Hot or iced · Here or to go", 12))

	dialogue = panel()
	dialogue.size.x = 740
	root.add_child(dialogue)
	var conversation := content(dialogue)
	conversation.add_theme_constant_override("separation", 9)
	var identity := HBoxContainer.new()
	npc_title = label("Maya  ·  Your barista", 20)
	npc_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	identity.add_child(npc_title)
	close_button = button("×", func(): close_requested.emit())
	close_button.custom_minimum_size.x = 42
	close_button.tooltip_text = "Close conversation (Esc)"
	identity.add_child(close_button)
	conversation.add_child(identity)
	speech_scroll = text_scroll()
	speech_content = VBoxContainer.new()
	speech_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	speech_content.add_theme_constant_override("separation", 8)
	speech_scroll.add_child(speech_content)
	conversation.add_child(speech_scroll)
	npc_line = label("", 20)
	npc_line.custom_minimum_size.y = 42
	speech_content.add_child(npc_line)
	transcript = label("Speak naturally. Your recording stays on this computer.", 13)
	transcript.add_theme_color_override("font_color", MUTED)
	speech_content.add_child(transcript)
	reply = LineEdit.new()
	reply.placeholder_text = "Type a reply, or record your voice…"
	reply.max_length = 1000
	reply.keep_editing_on_text_submit = true
	reply.custom_minimum_size.y = 44
	reply.text_submitted.connect(func(text: String): reply_submitted.emit(text))
	reply.gui_input.connect(reply_input)
	conversation.add_child(reply)
	var controls := HFlowContainer.new()
	controls.add_theme_constant_override("h_separation", 8)
	record = button("Record reply", func(): record_requested.emit(), true)
	controls.add_child(record)
	send = button("Send typed reply", func(): reply_submitted.emit(reply.text))
	controls.add_child(send)
	replay = button("Hear Maya", func(): replay_requested.emit())
	controls.add_child(replay)
	conversation.add_child(controls)
	recording_status = label("", 13)
	conversation.add_child(recording_status)
	meter = ProgressBar.new()
	meter.max_value = 1
	meter.show_percentage = false
	meter.custom_minimum_size.y = 6
	conversation.add_child(meter)
	feedback = label("Ask for a price or say ‘Please repeat’ whenever you need.", 13)
	feedback.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	feedback.add_theme_color_override("font_color", MUTED)
	var feedback_style := style(Color("#f2ede2"), Color("#e6ddcd"), 8)
	feedback_style.content_margin_left = 10
	feedback_style.content_margin_right = 10
	feedback_style.content_margin_top = 7
	feedback_style.content_margin_bottom = 7
	feedback.add_theme_stylebox_override("normal", feedback_style)
	feedback_scroll = text_scroll()
	feedback_scroll.add_child(feedback)
	conversation.add_child(feedback_scroll)
	for text_item in [npc_line, transcript, feedback]:
		text_item.minimum_size_changed.connect(func(): place_dialogue.call_deferred())
	recording(false)
	dialogue.hide()
	dialogue.minimum_size_changed.connect(func(): place_dialogue.call_deferred())
	dialogue.visibility_changed.connect(dialogue_visibility_changed)
	stage_controls.visibility_changed.connect(func(): layout.call_deferred())
	root.resized.connect(layout)
	layout.call_deferred()

func reply_input(event: InputEvent) -> void:
	# LineEdit consumes Escape while editing, before the world's unhandled input.
	# Forward only this existing exit shortcut through the same close action.
	if dialogue.visible and event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_ESCAPE:
		reply.accept_event()
		close_requested.emit()

func toggle_mission() -> void:
	mission_expanded = not mission_expanded
	layout()

func dialogue_visibility_changed() -> void:
	if dialogue.visible:
		mission_expanded = false
	layout.call_deferred()

func reflow_mission(narrow: bool) -> void:
	var compact := narrow and not mission_expanded
	mission_summary.visible = narrow
	mission_columns.visible = not compact
	mission_toggle.text = "Hide details" if mission_expanded else "Mission details"
	if compact:
		if mission_title.get_parent() != mission_summary_header:
			mission_title.reparent(mission_summary_header)
			mission_summary_header.move_child(mission_title, 0)
			progress.reparent(mission_summary)
			goal.reparent(mission_summary)
	else:
		if mission_title.get_parent() != mission_info:
			mission_title.reparent(mission_info)
			mission_info.move_child(mission_title, 1)
			progress.reparent(mission_info)
			mission_info.move_child(progress, 3)
			goal.reparent(mission_info)
			mission_info.move_child(goal, 4)
	var summary_visible := not compact or not dialogue.visible
	for item in [mission_title, progress, goal]:
		item.visible = summary_visible
	mission_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mission_title.add_theme_font_size_override("font_size", 18 if compact else 22)
	mission_title.max_lines_visible = 1 if compact else -1
	mission_title.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS if compact else TextServer.OVERRUN_NO_TRIMMING
	progress.max_lines_visible = 1 if compact else -1
	progress.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS if compact else TextServer.OVERRUN_NO_TRIMMING
	goal.add_theme_font_size_override("font_size", 14 if compact else 16)
	goal.max_lines_visible = 2 if compact else -1
	goal.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS if compact else TextServer.OVERRUN_NO_TRIMMING
	progress_style.content_margin_top = 4 if compact else 6
	progress_style.content_margin_bottom = 4 if compact else 6

func layout() -> void:
	var narrow := root.size.x < 900
	reflow_mission(narrow)
	# The expanded details card is a temporary overlay above long dialogue.
	hud.z_index = 1 if narrow and mission_expanded else 0
	# Control hit testing follows sibling order, independently of visual z-index.
	if narrow and mission_expanded:
		if hud.get_index() < dialogue.get_index():
			root.move_child(hud, dialogue.get_index())
	else:
		root.move_child(hud, 1)
	var stage_parent: Node = header_stack if narrow else header_row
	if stage_controls.get_parent() != stage_parent:
		stage_controls.reparent(stage_parent)
	brand.custom_minimum_size.x = 156 if narrow else 200
	brand.add_theme_font_size_override("font_size", 22 if narrow else 24)
	header_row.add_theme_constant_override("separation", 12 if narrow else 20)
	stage_controls.size_flags_horizontal = Control.SIZE_EXPAND_FILL if narrow else Control.SIZE_SHRINK_END
	mission_columns.vertical = not narrow
	mission_columns.add_theme_constant_override("separation", 14 if narrow else 10)
	mission_actions.custom_minimum_size.x = 225 if narrow else 0
	npc_line.add_theme_font_size_override("font_size", 18 if narrow else 20)
	header.position = Vector2(18, 16)
	header.size = Vector2(root.size.x - 36, 0)
	hud.position = Vector2(18, 156 if narrow and stage_controls.visible else 96)
	var hud_width := root.size.x - 36 if narrow else 252.0
	if narrow and dialogue.visible and not mission_expanded:
		hud_width = mission_toggle.get_combined_minimum_size().x + 32
	hud.size = Vector2(hud_width, 0)
	menu.visible = root.size.x >= 1100 and not at_table
	menu.position = Vector2(root.size.x - 240, 96)
	menu.size = Vector2(222, 0)
	dialogue.size = Vector2(minf(740, root.size.x - 36), 0)
	dialogue.position.x = (root.size.x - dialogue.size.x) / 2
	place_dialogue.call_deferred()

func place_dialogue() -> void:
	var available := maxf(300, root.size.y - 128)
	speech_scroll.custom_minimum_size.y = minf(speech_content.get_combined_minimum_size().y, minf(144, available * 0.27))
	feedback_scroll.custom_minimum_size.y = minf(feedback.get_combined_minimum_size().y, minf(112, available * 0.21))
	dialogue.size.y = dialogue.get_combined_minimum_size().y
	dialogue.position.y = maxf(80, root.size.y - dialogue.size.y - 28)

func show_dialogue(text: String) -> void:
	var opening := not dialogue.visible
	npc_line.text = text
	dialogue.show()
	if opening:
		speech_scroll.scroll_vertical = 0
		feedback_scroll.scroll_vertical = 0
		if dialogue_transition:
			dialogue_transition.kill()
		dialogue.modulate.a = 1.0 if reduce_motion else 0.0
		if not reduce_motion:
			dialogue_transition = create_tween()
			dialogue_transition.tween_property(dialogue, "modulate:a", 1.0, 0.14)
	if not completed and reply_allowed:
		reply.grab_focus()
	else:
		close_button.grab_focus()
	announce(text)
	layout.call_deferred()

func announce(text: String) -> void:
	if active_stage != 0:
		text = "Test preview · " + Stages.LABELS[active_stage] + " · " + text
	if OS.has_feature("web"):
		var document := JavaScriptBridge.get_interface("document")
		var element: Variant = document.getElementById("world-status")
		if element != null:
			element.textContent = text

func busy(value: bool) -> void:
	reply.editable = not value and not completed and reply_allowed
	send.disabled = value or completed or is_recording or not reply_allowed
	record.disabled = value or completed or not reply_allowed
	replay.disabled = value or is_recording

func recording(value: bool) -> void:
	is_recording = value
	microphone_ready = false
	record.text = "Stop & check reply" if value else "Record reply"
	recording_status.visible = value
	meter.visible = value
	reply.editable = not value and not completed and reply_allowed
	send.disabled = value or completed or not reply_allowed
	replay.disabled = value
	if value:
		record.grab_focus.call_deferred()
		mic_activity(0, 0)

func mic_activity(level: float, seconds: float) -> void:
	meter.value = level
	if seconds < 0.6 and not microphone_ready:
		recording_status.text = "Preparing microphone… wait a moment before speaking."
		return
	if not microphone_ready:
		microphone_ready = true
		announce("Recording reply. Speak now, then stop to check your answer.")
	recording_status.text = "Recording  %02d / 20 seconds · %s" % [seconds, "Audio detected" if level > 0.04 else "Speak into your microphone"]

func update(summary: Dictionary) -> void:
	completed = summary.complete
	progress.text = "Task goals: %d/4 · %d%%" % [summary.score / 25, summary.score]
	var goals := {"order": "Choose two drinks within budget.", "temperature": "Choose hot or iced.", "service": "Choose here or to go.", "confirm": "Check your order, then confirm.", "complete": "You did it. Enjoy your drinks!"}
	goal.text = goals.get(summary.get("stage", "order"), goals.order)
	var total: int = summary.get("total", 0)
	receipt.text = "%s\n$%.2f total · $%.2f %s" % [summary.get("order", "Your order is empty"), total / 100.0, abs(1200 - total) / 100.0, "remaining" if total <= 1200 else "over budget"]
	if completed:
		progress.text += "\nMission complete · 2 drinks earned"
		feedback.text = "Order confirmed · within budget · temperature and service chosen. Task score: 100/100. This does not assess pronunciation."
	busy(false)
	announce((npc_line.text + " " if dialogue.visible else "") + progress.text)

func update_journey(ordered: bool, chat: Dictionary) -> void:
	at_table = chat.phase in ["seated", "complete"]
	menu.visible = root.size.x >= 1100 and not at_table
	walk.text = "Walk to counter [Space]" if not ordered else "Join Alex [Space]" if chat.phase in ["counter", "walking"] else "Talk to Alex [Space]"
	talk.text = "Talk to Maya [E]" if not ordered else "Sit with Alex [E]" if chat.phase in ["counter", "walking"] else "Talk to Alex [E]"
	npc_title.text = "Alex  ·  Your friend" if chat.phase in ["seated", "complete"] else "Maya  ·  Your barista"
	replay.text = "Hear Alex" if chat.phase in ["seated", "complete"] else "Hear Maya"
	if not ordered:
		return
	if chat.phase in ["counter", "walking"]:
		progress.text = "Order complete · 2 drinks earned"
		goal.text = "Join Alex at the table."
	else:
		completed = chat.phase == "complete"
		progress.text = "Friend conversation: %d/3" % chat.answers.size()
		var prompts := ["Tell Alex what you like and why.", "Share a plan for the weekend.", "Invite Alex and suggest a time."]
		goal.text = prompts[mini(chat.answers.size(), 2)]
		if completed:
			progress.text += "\nMission complete · A drink with a friend"
			goal.text = "You ordered and had a conversation!"
		busy(false)
	announce((npc_line.text + " " if dialogue.visible else "") + progress.text)

func update_stage(selection: int) -> void:
	active_stage = selection
	stage_picker.select(selection)
	var preview := selection != 0
	stage_hint.text = "Practice stage · Test preview · not saved" if preview else "Practice stage · Saved journey"
	restart_button.text = "Restart test preview" if preview else "Restart mission"
