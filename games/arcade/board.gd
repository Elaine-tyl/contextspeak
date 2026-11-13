extends Control

signal action_requested(action: String, answer: Dictionary)

const INK := Color("243d35")
const MUTED := Color("5b6f66")
const LINE := Color("dbe5de")
const PAGE := Color("f3f6f1")
const GREEN := Color("17694f")
const GREEN_DARK := Color("12553f")
const GREEN_SOFT := Color("e3f0e8")
const BLUE := Color("2f55c4")
const BLUE_SOFT := Color("e8eefc")
const ORANGE := Color("d9791c")
const ORANGE_TEXT := Color("a35a12")
const ORANGE_SOFT := Color("fdf0e1")
const RED := Color("b4352f")
const RED_SOFT := Color("fbe7e4")
const CARD_WIDTH := 760.0
const PICTURE = preload("res://picture.gd")
const ART = preload("res://ui_art.gd")
const MODE_NAMES := {
	"dictation": "Dictation",
	"gap-fill": "Missing words",
	"picture": "Picture choice",
	"comprehension": "Listen and answer",
	"response": "Speaking",
	"riddle": "Riddle",
	"puzzle": "Puzzle",
	"pk": "AI sparring",
	"spelling": "Spelling bee",
	"word-order": "Sentence builder",
	"minimal-pairs": "Sound detective",
	"true-false": "Quick fire",
}

var state: Dictionary = {}
var screen_key := ""
var body: VBoxContainer
var choices_grid: GridContainer
var gap_inputs: Array[LineEdit] = []
var answer_input: TextEdit
var choice_buttons: Array[Button] = []
var selected_choice := ""
var play_button: Button
var submit_button: Button
var record_button: Button
var next_button: Button
var retry_button: Button
var status_label: Label
var error_label: Label
var result_label: Label
var bridge: JavaScriptObject
var callback: JavaScriptObject
var page_margin: MarginContainer
var card: PanelContainer
var actions: BoxContainer
var hero
var header_mode: Label
var header_dots: Control
## Seconds per character for the typewriter reveal.
const TYPE_SPEED := 0.028
var bold_font: FontVariation
# Tile games (spelling, sentence builder)
var tile_buttons: Array[Button] = []
var placed: Array[int] = []
var answer_row: HFlowContainer
var answer_hint: Label
# Quick fire timer
var timer_started_ms := -1
var timer_limit := 0.0
var timer_bar: ProgressBar
var timer_label: Label
var timer_answer_ms := -1

func _ready() -> void:
	theme = make_theme()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	resized.connect(adapt_layout)
	get_viewport().gui_focus_changed.connect(func(_control): report_state())
	var backdrop = ART.new()
	backdrop.kind = "backdrop"
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(backdrop)
	var scroll := ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	page_margin = MarginContainer.new()
	page_margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(page_margin)
	card = PanelContainer.new()
	card.add_theme_stylebox_override("panel", card_style())
	page_margin.add_child(card)
	body = VBoxContainer.new()
	body.add_theme_constant_override("separation", 16)
	card.add_child(body)
	add_label("ContextSpeak Arcade", 30, true)
	add_label("Open this game from the ContextSpeak website to use your saved levels.", 18).add_theme_color_override("font_color", MUTED)
	adapt_layout()
	if OS.has_feature("web"):
		bridge = JavaScriptBridge.get_interface("ContextSpeakArcade")
		if bridge:
			callback = JavaScriptBridge.create_callback(_on_state)
			bridge.subscribe(callback)
			emit_action("ready")

# ---------------------------------------------------------------- theme
func make_theme() -> Theme:
	var value := Theme.new()
	bold_font = FontVariation.new()
	bold_font.base_font = ThemeDB.fallback_font
	bold_font.variation_embolden = 0.7
	value.default_font_size = 18
	value.set_color("font_color", "Label", INK)
	value.set_color("font_color", "LineEdit", INK)
	value.set_color("font_color", "TextEdit", INK)
	value.set_color("font_placeholder_color", "TextEdit", Color("7a8c83"))
	value.set_color("font_placeholder_color", "LineEdit", Color("7a8c83"))
	value.set_color("caret_color", "LineEdit", BLUE)
	value.set_color("caret_color", "TextEdit", BLUE)
	value.set_color("selection_color", "LineEdit", Color(BLUE, 0.25))
	value.set_color("selection_color", "TextEdit", Color(BLUE, 0.25))
	for type_name in ["LineEdit", "TextEdit"]:
		value.set_stylebox("normal", type_name, box(Color.WHITE, Color("c3d1c8"), 2))
		value.set_stylebox("focus", type_name, ring(BLUE))
		value.set_stylebox("read_only", type_name, box(Color("eef2ee"), LINE, 2))
	# Plain buttons are quiet outlines; variations below carry emphasis.
	button_type(value, "Button", Color.WHITE, Color("c3d1c8"), INK, GREEN_SOFT, GREEN)
	value.set_type_variation("PrimaryButton", "Button")
	button_type(value, "PrimaryButton", GREEN, GREEN, Color.WHITE, GREEN_DARK, GREEN_DARK)
	value.set_type_variation("RecordingButton", "Button")
	button_type(value, "RecordingButton", RED, RED, Color.WHITE, Color("9a2a25"), Color("9a2a25"))
	value.set_type_variation("GhostButton", "Button")
	button_type(value, "GhostButton", Color(0, 0, 0, 0), Color(0, 0, 0, 0), MUTED, Color("e9efea"), Color(0, 0, 0, 0))
	value.set_type_variation("ChoiceButton", "Button")
	button_type(value, "ChoiceButton", Color.WHITE, LINE, INK, Color("f6faf7"), GREEN, 2)
	var picked := box(GREEN_SOFT, GREEN, 3, 16)
	value.set_stylebox("pressed", "ChoiceButton", picked)
	value.set_stylebox("hover_pressed", "ChoiceButton", picked)
	value.set_type_variation("PictureButton", "ChoiceButton")
	for item in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
		var style: StyleBox = value.get_stylebox(item, "ChoiceButton").duplicate()
		style.content_margin_top = 132
		style.content_margin_bottom = 12
		value.set_stylebox(item, "PictureButton", style)
	value.set_type_variation("TileButton", "Button")
	button_type(value, "TileButton", Color.WHITE, Color("b9c8bd"), INK, BLUE_SOFT, BLUE, 2)
	value.set_stylebox("normal", "TileButton", tile_box(Color.WHITE, Color("b9c8bd")))
	value.set_stylebox("hover", "TileButton", tile_box(BLUE_SOFT, BLUE))
	value.set_stylebox("pressed", "TileButton", tile_box(BLUE_SOFT, BLUE))
	value.set_stylebox("disabled", "TileButton", tile_box(Color("eef2ee"), Color("e1e8e2"), 2))
	value.set_type_variation("PlacedTile", "Button")
	button_type(value, "PlacedTile", BLUE, BLUE, Color.WHITE, Color("2445a3"), Color("2445a3"))
	value.set_stylebox("normal", "PlacedTile", tile_box(BLUE, BLUE))
	value.set_stylebox("hover", "PlacedTile", tile_box(Color("2445a3"), Color("2445a3")))
	value.set_stylebox("pressed", "PlacedTile", tile_box(Color("2445a3"), Color("2445a3")))
	value.set_type_variation("Chip", "PanelContainer")
	return value

func button_type(value: Theme, type_name: String, fill: Color, border: Color, text: Color, hover_fill: Color, hover_border: Color, width := 2) -> void:
	value.set_stylebox("normal", type_name, box(fill, border, width, 14))
	value.set_stylebox("hover", type_name, box(hover_fill, hover_border, width, 14))
	value.set_stylebox("pressed", type_name, box(hover_fill, hover_border, width, 14))
	value.set_stylebox("hover_pressed", type_name, box(hover_fill, hover_border, width, 14))
	value.set_stylebox("disabled", type_name, box(Color("eef2ee") if fill.a > 0 else Color(0, 0, 0, 0), Color("e1e8e2") if fill.a > 0 else Color(0, 0, 0, 0), width, 14))
	value.set_stylebox("focus", type_name, ring(BLUE))
	# Godot's default focus/pressed text is near-white; keep every state readable.
	for item in ["font_color", "font_focus_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color"]:
		value.set_color(item, type_name, text)
	value.set_color("font_disabled_color", type_name, Color("8c9b93"))

func tile_box(fill: Color, border: Color, bottom := 5) -> StyleBoxFlat:
	# A chunky "key cap" look: thicker bottom edge.
	var style := box(fill, border, 2, 12)
	style.border_width_bottom = bottom
	style.content_margin_left = 14
	style.content_margin_right = 14
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	return style

func box(fill: Color, border: Color, width := 1, radius := 14) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill
	style.border_color = border
	style.set_border_width_all(width)
	style.set_corner_radius_all(radius)
	style.content_margin_left = 18
	style.content_margin_right = 18
	style.content_margin_top = 12
	style.content_margin_bottom = 12
	style.anti_aliasing = true
	return style

func ring(color: Color) -> StyleBoxFlat:
	# Drawn on top of the normal style: an outer focus ring that never hides text.
	var style := StyleBoxFlat.new()
	style.draw_center = false
	style.border_color = color
	style.set_border_width_all(3)
	style.set_corner_radius_all(17)
	style.set_expand_margin_all(4)
	return style

func card_style() -> StyleBoxFlat:
	var style := box(Color.WHITE, LINE, 1, 26)
	style.shadow_color = Color(INK, 0.08)
	style.shadow_size = 18
	style.shadow_offset = Vector2(0, 6)
	style.content_margin_left = 36
	style.content_margin_right = 36
	style.content_margin_top = 30
	style.content_margin_bottom = 30
	return style

func tint_style(fill: Color, border: Color = Color(0, 0, 0, 0), radius := 14, pad := Vector2(16, 10)) -> StyleBoxFlat:
	var style := box(fill, border, 0 if border.a == 0 else 1, radius)
	style.content_margin_left = pad.x
	style.content_margin_right = pad.x
	style.content_margin_top = pad.y
	style.content_margin_bottom = pad.y
	return style

# ---------------------------------------------------------------- bridge
func _on_state(arguments: Array) -> void:
	if arguments.is_empty() or not arguments[0] is String:
		return
	var parsed = JSON.parse_string(arguments[0])
	if parsed is Dictionary:
		receive(parsed)

func receive(snapshot: Dictionary) -> void:
	if not snapshot.get("round") is Dictionary:
		return
	state = snapshot
	var result: Dictionary = state.get("result", {}) if state.get("result") is Dictionary else {}
	var key := str(state.round.id) + ":" + str(result.get("id", "answer"))
	# Playback/recording status must never recreate a field while the learner types.
	if key != screen_key:
		screen_key = key
		build_screen(result)
	update_controls()
	report_state()

# ---------------------------------------------------------------- screens
func track_accent() -> Color:
	return GREEN if str(state.round.get("track", "")) == "speaking" else BLUE

func build_screen(result: Dictionary) -> void:
	for child in body.get_children():
		body.remove_child(child)
		child.queue_free()
	gap_inputs.clear()
	choice_buttons.clear()
	selected_choice = ""
	answer_input = null
	submit_button = null
	record_button = null
	next_button = null
	retry_button = null
	result_label = null
	tile_buttons.clear()
	placed.clear()
	answer_row = null
	answer_hint = null
	timer_started_ms = -1
	timer_answer_ms = -1
	timer_bar = null
	timer_label = null
	var round: Dictionary = state.round
	var speaking: bool = round.track == "speaking"
	add_header(round)
	add_label(str(round.title), 32, true)
	var instruction := add_label(str(round.instruction), 18)
	instruction.add_theme_color_override("font_color", MUTED)
	hero = PICTURE.new()
	hero.kind = "voice" if speaking else "listen"
	hero.custom_minimum_size = Vector2(0, 104)
	body.add_child(hero)
	play_button = add_button("Play challenge", func(): emit_action("play"), "play", "PrimaryButton" if result.is_empty() else "")
	play_button.name = "play_challenge"
	play_button.custom_minimum_size.y = 56
	status_label = add_chip("", BLUE_SOFT, BLUE)
	error_label = add_chip("", RED_SOFT, RED)
	choices_grid = GridContainer.new()
	choices_grid.columns = 2
	choices_grid.add_theme_constant_override("h_separation", 14)
	choices_grid.add_theme_constant_override("v_separation", 14)
	body.add_child(choices_grid)
	actions = BoxContainer.new()
	actions.name = "Actions"
	actions.add_theme_constant_override("separation", 12)
	if not result.is_empty():
		show_result(result)
	elif speaking:
		show_speaking(round)
	elif round.kind == "gap-fill":
		show_gaps(round)
	elif round.kind in ["spelling", "word-order"]:
		show_tiles(round)
	elif round.kind == "true-false":
		show_quick_fire(round)
	elif round.kind == "dictation":
		add_section_title("Type what you hear")
		answer_input = TextEdit.new()
		answer_input.name = "response"
		answer_input.placeholder_text = "Listen first, then write the sentence…"
		answer_input.custom_minimum_size.y = 140
		answer_input.add_theme_font_size_override("font_size", 20)
		answer_input.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
		answer_input.text_changed.connect(update_controls)
		answer_input.gui_input.connect(func(event):
			if event is InputEventKey and event.pressed and event.keycode == KEY_TAB:
				if event.shift_pressed:
					play_button.grab_focus()
				elif submit_button and not submit_button.disabled:
					submit_button.grab_focus()
				answer_input.accept_event())
		body.add_child(answer_input)
		make_submit()
	else:
		show_choices(round)
		make_submit()
	body.add_child(actions)
	retry_button = add_button("Retry same answer", func(): emit_action("retry"), "retry", "", actions)
	retry_button.name = "retry_answer"
	var back := add_button("Back to games", func(): emit_action("leave"), "back", "GhostButton", actions)
	back.name = "back_to_games"
	adapt_layout()
	# A gentle fade so each new question reads as a fresh card.
	card.modulate.a = 0.0
	create_tween().tween_property(card, "modulate:a", 1.0, 0.22)
	if next_button:
		next_button.grab_focus()
	else:
		play_button.grab_focus()

func add_header(round: Dictionary) -> void:
	var accent := track_accent()
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	body.add_child(row)
	var track_name := "SPEAKING" if round.track == "speaking" else "LISTENING"
	var chip := chip_label(track_name, BLUE_SOFT if accent == BLUE else GREEN_SOFT, accent, 14)
	row.add_child(chip)
	var mode := Label.new()
	mode.text = str(MODE_NAMES.get(str(round.kind), str(round.kind).capitalize()))
	mode.add_theme_color_override("font_color", MUTED)
	mode.add_theme_font_size_override("font_size", 15)
	row.add_child(mode)
	header_mode = mode
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(spacer)
	var level := Label.new()
	level.text = "Level " + str(int(round.level)) + " of 10"
	level.add_theme_font_override("font", bold_font)
	level.add_theme_font_size_override("font_size", 15)
	level.add_theme_color_override("font_color", ORANGE_TEXT)
	row.add_child(level)
	var dots = ART.new()
	dots.kind = "dots"
	dots.value = int(round.level)
	dots.accent = ORANGE
	dots.custom_minimum_size = Vector2(150, 20)
	dots.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(dots)
	header_dots = dots

func show_speaking(round: Dictionary) -> void:
	add_section_title("Your challenge")
	var prompt := add_label("“" + str(round.get("audioText", "")) + "”", 22)
	prompt.add_theme_stylebox_override("normal", quote_style(GREEN))
	var criteria: Array = round.get("criteria", [])
	if not criteria.is_empty():
		add_section_title("A strong answer will")
		for criterion in criteria:
			var line := add_label("✓  " + str(criterion), 18)
			line.add_theme_stylebox_override("normal", tint_style(Color("f6faf7"), Color(0, 0, 0, 0), 12, Vector2(14, 8)))
	if round.has("opponentScore"):
		var versus := add_label("You  vs  Echo, the AI opponent · Echo’s target: " + str(round.opponentScore) + "/100", 18)
		versus.add_theme_stylebox_override("normal", tint_style(ORANGE_SOFT, Color(0, 0, 0, 0), 14, Vector2(16, 12)))
		versus.add_theme_color_override("font_color", ORANGE_TEXT)
	var note := add_label("Stop recording to get AI feedback automatically. Google processes the recording; only the transcript and feedback are saved.", 15)
	note.add_theme_color_override("font_color", MUTED)
	body.add_child(actions)
	record_button = add_button("Start recording", recording_action, "mic", "PrimaryButton", actions)
	record_button.custom_minimum_size.y = 56
	body.remove_child(actions)

func recording_action() -> void:
	match state.get("recording", "idle"):
		"requesting":
			emit_action("cancel")
		"recording":
			emit_action("stop")
		_:
			emit_action("record")

func show_gaps(round: Dictionary) -> void:
	add_section_title("Fill in the missing words")
	var holder := PanelContainer.new()
	holder.add_theme_stylebox_override("panel", tint_style(Color("f8faf8"), LINE, 18, Vector2(20, 18)))
	body.add_child(holder)
	var flow := HFlowContainer.new()
	flow.add_theme_constant_override("h_separation", 8)
	flow.add_theme_constant_override("v_separation", 12)
	holder.add_child(flow)
	var parts: Array = round.get("gapText", [])
	for index in parts.size():
		for word in str(parts[index]).split(" ", false):
			var label := Label.new()
			label.text = word
			label.add_theme_font_size_override("font_size", 21)
			label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
			flow.add_child(label)
		if index < parts.size() - 1:
			var input := LineEdit.new()
			input.name = "word_" + str(index + 1)
			input.placeholder_text = "Word " + str(index + 1)
			input.tooltip_text = input.placeholder_text
			input.max_length = 80
			input.alignment = HORIZONTAL_ALIGNMENT_CENTER
			input.add_theme_font_size_override("font_size", 20)
			fit_gap(input)
			input.text_changed.connect(func(_text):
				fit_gap(input)
				pop(input)
				update_controls())
			input.text_submitted.connect(func(_text): submit_answer())
			flow.add_child(input)
			gap_inputs.append(input)
	# Typewriter: the sentence types itself out and each blank pops in on its turn.
	typewrite(flow.get_children())
	make_submit()

func show_choices(round: Dictionary) -> void:
	var choices: Array = round.get("choices", [])
	var letters := "ABCDEFGH"
	for index in choices.size():
		var item: Dictionary = choices[index]
		var button := Button.new()
		button.name = "choice_" + str(index)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		button.toggle_mode = true
		button.text = "Picture " + str(index + 1) if item.has("picture") else str(item.label)
		if item.has("picture"):
			button.theme_type_variation = "PictureButton"
			button.custom_minimum_size = Vector2(0, 176)
			var art = PICTURE.new()
			art.name = "Art"
			art.kind = str(item.picture)
			art.mouse_filter = Control.MOUSE_FILTER_IGNORE
			art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			art.offset_left = 10
			art.offset_right = -10
			art.offset_top = 10
			art.offset_bottom = -40
			button.add_child(art)
		elif round.kind in ["minimal-pairs", "true-false"]:
			# Big, centred words: the sound is the whole question.
			button.theme_type_variation = "ChoiceButton"
			button.custom_minimum_size = Vector2(0, 76)
			button.add_theme_font_size_override("font_size", 26)
			button.add_theme_font_override("font", bold_font)
			if round.kind == "true-false":
				var mark = ART.new()
				mark.name = "Icon"
				mark.kind = "icon"
				mark.glyph = "check" if str(item.id) == "true" else "cross"
				mark.color = GREEN if str(item.id) == "true" else RED
				button.add_child(mark)
		else:
			button.theme_type_variation = "ChoiceButton"
			button.custom_minimum_size = Vector2(0, 68)
			button.alignment = HORIZONTAL_ALIGNMENT_LEFT
			var badge = ART.new()
			badge.kind = "badge"
			badge.value = letters[index % letters.length()]
			badge.accent = track_accent()
			badge.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			button.add_child(badge)
			for item_name in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
				var style: StyleBox = theme.get_stylebox(item_name, "ChoiceButton").duplicate()
				style.content_margin_left = 60
				button.add_theme_stylebox_override(item_name, style)
		button.pressed.connect(func():
			selected_choice = str(item.id)
			for other in choice_buttons:
				other.button_pressed = other == button
				other.queue_redraw()
				for child in other.get_children():
					child.queue_redraw()
			update_controls())
		choices_grid.add_child(button)
		choice_buttons.append(button)

# ---------------------------------------------------------------- tile games
func show_tiles(round: Dictionary) -> void:
	var spelling: bool = round.kind == "spelling"
	add_section_title("Your word" if spelling else "Your sentence")
	var holder := PanelContainer.new()
	holder.add_theme_stylebox_override("panel", tint_style(Color("f8faf8"), LINE, 18, Vector2(16, 14)))
	holder.custom_minimum_size.y = 78
	body.add_child(holder)
	answer_row = HFlowContainer.new()
	answer_row.name = "AnswerRow"
	answer_row.add_theme_constant_override("h_separation", 6 if spelling else 8)
	answer_row.add_theme_constant_override("v_separation", 8)
	holder.add_child(answer_row)
	answer_hint = Label.new()
	answer_hint.text = ("Tap the letters below" if spelling else "Tap the words below") + (" — or type them" if spelling else "")
	answer_hint.add_theme_color_override("font_color", Color("8c9b93"))
	answer_hint.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	answer_row.add_child(answer_hint)
	add_section_title("Letter tiles" if spelling else "Word tiles")
	var tiles_row := HFlowContainer.new()
	tiles_row.add_theme_constant_override("h_separation", 8)
	tiles_row.add_theme_constant_override("v_separation", 10)
	body.add_child(tiles_row)
	var tiles: Array = round.get("tiles", [])
	for index in tiles.size():
		var tile := Button.new()
		tile.name = "tile_" + str(index)
		tile.text = str(tiles[index])
		tile.theme_type_variation = "TileButton"
		tile.add_theme_font_override("font", bold_font)
		tile.add_theme_font_size_override("font_size", 26 if spelling else 20)
		tile.custom_minimum_size = Vector2(56, 58) if spelling else Vector2(0, 52)
		tile.pressed.connect(func(): place_tile(index))
		tiles_row.add_child(tile)
		tile_buttons.append(tile)
	var clear := add_button("Clear", clear_tiles, "retry", "GhostButton")
	clear.name = "clear_tiles"
	clear.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	make_submit()

func tiles_open() -> bool:
	return not state.get("frozen", false) and state.get("played", false) and not state.get("result") is Dictionary

func place_tile(index: int) -> void:
	if not tiles_open() or index in placed:
		return
	placed.append(index)
	render_answer_row(true)
	update_controls()
	# Keep keyboard users moving: focus the next free tile.
	for tile in tile_buttons:
		if not tile.disabled and tile.visible:
			tile.grab_focus()
			return
	if submit_button and not submit_button.disabled:
		submit_button.grab_focus()

func remove_placed(position: int) -> void:
	if not tiles_open() or position >= placed.size():
		return
	var index: int = placed[position]
	placed.remove_at(position)
	render_answer_row(false)
	update_controls()
	tile_buttons[index].grab_focus()

func clear_tiles() -> void:
	if not tiles_open():
		return
	placed.clear()
	render_answer_row(false)
	update_controls()

func tile_answer() -> String:
	var parts: Array[String] = []
	for index in placed:
		parts.append(tile_buttons[index].text)
	return ("" if state.round.kind == "spelling" else " ").join(parts)

func render_answer_row(animate_last: bool) -> void:
	for child in answer_row.get_children():
		if child != answer_hint:
			answer_row.remove_child(child)
			child.queue_free()
	answer_hint.visible = placed.is_empty()
	var spelling: bool = state.round.kind == "spelling"
	for position in placed.size():
		var chip := Button.new()
		chip.name = "placed_" + str(position)
		chip.text = tile_buttons[placed[position]].text
		chip.tooltip_text = "Remove " + chip.text
		chip.theme_type_variation = "PlacedTile"
		chip.add_theme_font_override("font", bold_font)
		chip.add_theme_font_size_override("font_size", 26 if spelling else 20)
		chip.custom_minimum_size = Vector2(52, 54) if spelling else Vector2(0, 48)
		chip.pressed.connect(func(): remove_placed(position))
		answer_row.add_child(chip)
		if animate_last and position == placed.size() - 1:
			chip.modulate.a = 0.0
			chip.ready.connect(func():
				chip.pivot_offset = chip.size / 2
				chip.scale = Vector2(0.6, 0.6)
				var tween := chip.create_tween()
				tween.tween_property(chip, "modulate:a", 1.0, 0.12)
				tween.parallel().tween_property(chip, "scale", Vector2.ONE, 0.22).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT), CONNECT_ONE_SHOT)

## Spelling also accepts the physical keyboard: letters pick a free tile, Backspace undoes.
func _unhandled_key_input(event: InputEvent) -> void:
	if state.is_empty() or state.round.get("kind") != "spelling" or tile_buttons.is_empty():
		return
	var key := event as InputEventKey
	if key == null or not key.pressed or key.echo:
		return
	if key.keycode == KEY_BACKSPACE and not placed.is_empty():
		remove_placed(placed.size() - 1)
		get_viewport().set_input_as_handled()
		return
	var letter := char(key.unicode).to_lower() if key.unicode > 0 else ""
	if letter.length() != 1 or not tiles_open():
		return
	for index in tile_buttons.size():
		if not index in placed and tile_buttons[index].text.to_lower() == letter:
			place_tile(index)
			get_viewport().set_input_as_handled()
			return

# ---------------------------------------------------------------- quick fire
func show_quick_fire(round: Dictionary) -> void:
	var frame := PanelContainer.new()
	frame.add_theme_stylebox_override("panel", tint_style(Color("f8faf8"), LINE, 20, Vector2(12, 12)))
	body.add_child(frame)
	# Picture sits above the True / False buttons so learners look, then answer.
	body.move_child(frame, choices_grid.get_index())
	var art = PICTURE.new()
	art.kind = str(round.get("picture", ""))
	art.custom_minimum_size = Vector2(0, 180)
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	frame.add_child(art)
	timer_limit = float(round.get("timeLimit", 10))
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	body.add_child(row)
	timer_bar = ProgressBar.new()
	timer_bar.name = "Timer"
	timer_bar.show_percentage = false
	timer_bar.max_value = 1.0
	timer_bar.step = 0.0
	timer_bar.value = 1.0
	timer_bar.custom_minimum_size.y = 14
	timer_bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	timer_bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	var track := StyleBoxFlat.new()
	track.bg_color = Color("e6ece7")
	track.set_corner_radius_all(7)
	timer_bar.add_theme_stylebox_override("background", track)
	set_timer_colour(GREEN)
	row.add_child(timer_bar)
	timer_label = Label.new()
	timer_label.text = str(int(timer_limit)) + " s"
	timer_label.add_theme_font_override("font", bold_font)
	timer_label.custom_minimum_size.x = 64
	timer_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	row.add_child(timer_label)
	var hint := add_label("The timer starts when the statement finishes. Answer in time for full marks.", 15)
	hint.add_theme_color_override("font_color", MUTED)
	show_choices(round)
	make_submit()

func set_timer_colour(colour: Color) -> void:
	if not timer_bar:
		return
	var fill := StyleBoxFlat.new()
	fill.bg_color = colour
	fill.set_corner_radius_all(7)
	timer_bar.add_theme_stylebox_override("fill", fill)

func _process(_delta: float) -> void:
	if timer_bar == null or timer_started_ms < 0:
		return
	var elapsed := (Time.get_ticks_msec() - timer_started_ms) / 1000.0
	if timer_answer_ms >= 0:
		elapsed = timer_answer_ms / 1000.0
	var left := maxf(0.0, timer_limit - elapsed)
	timer_bar.value = left / timer_limit
	if left <= 0.0:
		timer_label.text = "Time's up"
		timer_label.add_theme_color_override("font_color", RED)
		set_timer_colour(RED)
	else:
		timer_label.text = "%.1f s" % left
		set_timer_colour(GREEN if left > timer_limit * 0.5 else ORANGE if left > timer_limit * 0.25 else RED)

func show_result(result: Dictionary) -> void:
	var score = result.get("score")
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 22)
	body.add_child(row)
	if score != null:
		var ring_art = ART.new()
		ring_art.kind = "ring"
		ring_art.value = int(score)
		ring_art.accent = GREEN if int(score) >= 70 else ORANGE
		ring_art.custom_minimum_size = Vector2(132, 132)
		row.add_child(ring_art)
	var summary := VBoxContainer.new()
	summary.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	summary.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_child(summary)
	result_label = Label.new()
	result_label.text = "More audio evidence needed" if score == null else str(int(score)) + "/100 · Answer checked"
	result_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	result_label.add_theme_font_override("font", bold_font)
	result_label.add_theme_font_size_override("font_size", 26)
	result_label.add_theme_color_override("font_color", GREEN if score != null and int(score) >= 70 else ORANGE_TEXT)
	summary.add_child(result_label)
	var headline := Label.new()
	headline.text = "Great work — level unlocked at 70+." if score != null and int(score) >= 70 else "Keep going — 70 or more unlocks the next level."
	headline.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	headline.add_theme_color_override("font_color", MUTED)
	summary.add_child(headline)
	var feedback: Dictionary = result.get("feedback", {})
	if feedback.has("opponentScore") and score != null:
		var opponent := int(feedback.opponentScore)
		var outcome := "You win" if score > opponent else "Draw" if score == opponent else "Echo wins this round"
		var banner := add_label(outcome + " · You " + str(int(score)) + " : Echo " + str(opponent), 21, true)
		var won: bool = score >= opponent
		banner.add_theme_stylebox_override("normal", tint_style(GREEN_SOFT if won else ORANGE_SOFT, Color(0, 0, 0, 0), 14, Vector2(18, 12)))
		banner.add_theme_color_override("font_color", GREEN if won else ORANGE_TEXT)
	if feedback.has("correctAnswer"):
		add_section_title("Correct answer")
		var answer := add_label(str(feedback.correctAnswer), 20)
		answer.add_theme_stylebox_override("normal", quote_style(BLUE))
		typewrite([answer])
	if feedback.get("gapFill") is Dictionary:
		add_section_title("Your words")
		var flow := HFlowContainer.new()
		flow.add_theme_constant_override("h_separation", 8)
		flow.add_theme_constant_override("v_separation", 8)
		body.add_child(flow)
		var chips: Array = []
		for word in feedback.gapFill.get("words", []):
			var ok: bool = word.correct
			var line := Label.new()
			line.text = str(word.answer) + (" ✓" if ok else " → " + str(word.expected))
			line.add_theme_font_size_override("font_size", 18)
			line.add_theme_color_override("font_color", GREEN if ok else RED)
			line.add_theme_stylebox_override("normal", tint_style(GREEN_SOFT if ok else RED_SOFT, Color(0, 0, 0, 0), 999, Vector2(14, 6)))
			flow.add_child(line)
			chips.append(line)
		typewrite(chips, 0.6)
	var explanation := str(feedback.get("explanation", ""))
	if not explanation.is_empty():
		add_label(explanation, 18)
	var saved := add_label("Saved to your account. Detailed feedback is available below the game.", 15)
	saved.add_theme_color_override("font_color", MUTED)
	body.add_child(actions)
	next_button = add_button("Next question", func(): emit_action("next"), "next", "PrimaryButton", actions)
	next_button.custom_minimum_size.y = 56
	body.remove_child(actions)

## Reveals labels character by character and fades/pops other controls in, in order.
## Inputs stay usable throughout; only their appearance animates.
func typewrite(items: Array, delay := 0.0) -> void:
	if items.is_empty():
		return
	# Bound to the question's own node, so it stops if the screen is rebuilt.
	var tween: Tween = (items[0] as Node).create_tween()
	if delay > 0:
		tween.tween_interval(delay)
	for item in items:
		if item is Label:
			var label: Label = item
			label.visible_ratio = 0.0
			tween.tween_property(label, "visible_ratio", 1.0, maxf(0.05, label.text.length() * TYPE_SPEED))
		elif item is Control:
			var control: Control = item
			control.modulate.a = 0.0
			tween.tween_callback(func():
				control.pivot_offset = control.size / 2
				control.scale = Vector2(0.85, 0.85))
			tween.tween_property(control, "modulate:a", 1.0, 0.14)
			tween.parallel().tween_property(control, "scale", Vector2.ONE, 0.22).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

## A small bounce on each keystroke so typing into a blank feels alive.
func pop(control: Control) -> void:
	control.pivot_offset = control.size / 2
	var tween := control.create_tween()
	tween.tween_property(control, "scale", Vector2(1.07, 1.07), 0.06)
	tween.tween_property(control, "scale", Vector2.ONE, 0.12).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

func make_submit() -> void:
	body.add_child(actions)
	submit_button = add_button("Check answer", submit_answer, "check", "PrimaryButton", actions)
	submit_button.custom_minimum_size.y = 56
	body.remove_child(actions)

func quick_fire_elapsed(now_ms: int) -> int:
	return clampi(now_ms - timer_started_ms, 0, 600000)

func submit_answer() -> void:
	if not submit_button or submit_button.disabled:
		return
	var answer := {}
	match str(state.round.kind):
		"dictation":
			answer.response = answer_input.text
		"spelling", "word-order":
			answer.response = tile_answer()
		"true-false":
			answer.choice = selected_choice
			if timer_started_ms >= 0:
				timer_answer_ms = quick_fire_elapsed(Time.get_ticks_msec())
				answer.elapsedMs = timer_answer_ms
		"gap-fill":
			answer.gapAnswers = gap_inputs.map(func(input): return input.text)
		_:
			answer.choice = selected_choice
	emit_action("submit", answer)

# ---------------------------------------------------------------- live updates
func update_controls() -> void:
	if not play_button:
		return
	var frozen: bool = state.get("frozen", false)
	var played: bool = state.get("played", false)
	var recording: String = state.get("recording", "idle")
	play_button.disabled = state.get("playDisabled", false)
	status_label.text = str(state.get("status", ""))
	status_label.visible = not status_label.text.is_empty()
	(status_label.get_parent() as Control).visible = status_label.visible
	error_label.text = str(state.get("error", ""))
	error_label.visible = not error_label.text.is_empty()
	(error_label.get_parent() as Control).visible = error_label.visible
	if hero:
		hero.active = recording == "recording" or status_label.text.begins_with("Playing")
	var retry_visible: bool = state.get("pending", false) and not state.get("result") is Dictionary
	var recovering := retry_visible and (not retry_button.visible or retry_button.disabled)
	retry_button.visible = retry_visible
	retry_button.disabled = state.get("busy", false)
	if recovering and not retry_button.disabled:
		retry_button.grab_focus()
	if answer_input:
		answer_input.editable = not frozen
	for input in gap_inputs:
		input.editable = not frozen and played
	for index in tile_buttons.size():
		tile_buttons[index].disabled = frozen or not played or index in placed
	if timer_bar and played and timer_started_ms < 0 and not frozen:
		timer_started_ms = Time.get_ticks_msec()
	for button in choice_buttons:
		button.disabled = frozen or not played
	if submit_button:
		var complete := false
		if answer_input:
			var meaningful := RegEx.create_from_string("[\\p{L}\\p{N}]").search(answer_input.text) != null
			complete = meaningful and answer_input.text.length() <= 2000
		elif not gap_inputs.is_empty():
			complete = gap_inputs.all(func(input): return not input.text.strip_edges().is_empty())
		elif not tile_buttons.is_empty():
			complete = not placed.is_empty()
		else:
			complete = not selected_choice.is_empty()
		submit_button.disabled = frozen or not played or not complete
	if record_button:
		record_button.text = "Stop recording" if recording == "recording" else "Cancel microphone request" if recording == "requesting" else "Start recording"
		record_button.theme_type_variation = "RecordingButton" if recording == "recording" else "PrimaryButton"
		set_icon(record_button, "stop" if recording == "recording" else "mic")
		record_button.disabled = frozen or not played or state.get("speechBusy", false) and recording not in ["requesting", "recording"]
	if next_button:
		next_button.disabled = state.get("busy", false) or state.get("advancing", false)
	# Icons follow each button's emphasis (white on filled buttons).
	for button in [play_button, submit_button, record_button, next_button, retry_button]:
		if button:
			refresh_icon(button)
	report_state()

# ---------------------------------------------------------------- building blocks
func add_label(text: String, font_size: int, bold := false) -> Label:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", font_size)
	if bold:
		label.add_theme_font_override("font", bold_font)
	body.add_child(label)
	return label

func add_section_title(text: String) -> Label:
	var label := add_label(text.to_upper(), 13, true)
	label.add_theme_color_override("font_color", MUTED)
	return label

func add_chip(text: String, fill: Color, color: Color) -> Label:
	var chip := chip_label(text, fill, color, 16)
	(chip.get_child(0) as Label).autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	chip.size_flags_horizontal = Control.SIZE_FILL
	body.add_child(chip)
	return chip.get_child(0) as Label

func chip_label(text: String, fill: Color, color: Color, font_size: int) -> PanelContainer:
	var chip := PanelContainer.new()
	chip.theme_type_variation = "Chip"
	chip.add_theme_stylebox_override("panel", tint_style(fill, Color(0, 0, 0, 0), 999 if font_size < 16 else 12, Vector2(12, 5) if font_size < 16 else Vector2(16, 10)))
	chip.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	var label := Label.new()
	label.text = text
	label.add_theme_color_override("font_color", color)
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_font_override("font", bold_font)
	chip.add_child(label)
	return chip

func quote_style(accent: Color) -> StyleBoxFlat:
	var style := tint_style(BLUE_SOFT if accent == BLUE else GREEN_SOFT, Color(0, 0, 0, 0), 14, Vector2(20, 16))
	style.border_color = accent
	style.border_width_left = 5
	return style

func add_button(text: String, action: Callable, icon_name := "", variation := "", parent: Node = null) -> Button:
	var button := Button.new()
	button.text = text
	button.name = text.to_snake_case()
	button.custom_minimum_size.y = 50
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.theme_type_variation = variation
	button.add_theme_font_override("font", bold_font)
	button.pressed.connect(action)
	(parent if parent else body).add_child(button)
	if not icon_name.is_empty():
		var icon = ART.new()
		icon.name = "Icon"
		icon.kind = "icon"
		icon.glyph = icon_name
		button.add_child(icon)
		refresh_icon(button)
	return button

func set_icon(button: Button, glyph: String) -> void:
	var icon = button.get_node_or_null("Icon")
	if icon and icon.glyph != glyph:
		icon.glyph = glyph
		icon.queue_redraw()

func refresh_icon(button: Button) -> void:
	var icon = button.get_node_or_null("Icon")
	if not icon:
		return
	var filled := button.theme_type_variation in ["PrimaryButton", "RecordingButton"]
	icon.color = Color("8c9b93") if button.disabled else Color.WHITE if filled else GREEN
	icon.queue_redraw()

func fit_gap(input: LineEdit) -> void:
	var preferred := minf(270, maxf(150, input.text.length() * 12 + 40))
	# Fit inside the card: page margins plus card padding.
	var available := size.x - 2 * outer_margin() - 2 * card_padding() - 40
	input.custom_minimum_size.x = minf(preferred, maxf(80, minf(available, size.x - 48)))

## 4 pictures: 2 × 2; 6 pictures: 3 × 2; 9 pictures: 3 × 3 (also on phones).
func grid_columns(narrow: bool) -> int:
	var count := choice_buttons.size()
	var pictures := count > 0 and choice_buttons[0].theme_type_variation == "PictureButton"
	if pictures:
		if count >= 9:
			return 3
		return 2 if narrow or count <= 4 else 3
	if not state.is_empty() and state.round.get("kind") in ["minimal-pairs", "true-false"]:
		return 3 if count == 3 and not narrow else 2
	return 1 if narrow else 2

func size_pictures(narrow: bool) -> void:
	var count := choice_buttons.size()
	if count == 0 or choice_buttons[0].theme_type_variation != "PictureButton":
		return
	var height := 176.0 if count <= 4 else 140.0
	if narrow:
		height = 150.0 if count <= 4 else 112.0
	for button in choice_buttons:
		button.custom_minimum_size.y = height
		for item_name in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
			var style: StyleBox = theme.get_stylebox(item_name, "PictureButton").duplicate()
			style.content_margin_top = height - 36
			style.content_margin_bottom = 8
			button.add_theme_stylebox_override(item_name, style)
		button.add_theme_font_size_override("font_size", 14 if count >= 9 else 16)

func outer_margin() -> float:
	return maxf(12.0 if size.x < 600 else 24.0, (size.x - CARD_WIDTH) / 2.0)

func card_padding() -> float:
	return 18.0 if size.x < 600 else 36.0

func adapt_layout() -> void:
	var narrow := size.x < 600
	if page_margin:
		var side := int(outer_margin())
		page_margin.add_theme_constant_override("margin_left", side)
		page_margin.add_theme_constant_override("margin_right", side)
		page_margin.add_theme_constant_override("margin_top", 14 if narrow else 32)
		page_margin.add_theme_constant_override("margin_bottom", 14 if narrow else 32)
	if card:
		var style: StyleBoxFlat = card.get_theme_stylebox("panel")
		style.content_margin_left = card_padding()
		style.content_margin_right = card_padding()
		style.content_margin_top = 20.0 if narrow else 30.0
		style.content_margin_bottom = 20.0 if narrow else 30.0
	for input in gap_inputs:
		fit_gap(input)
	if choices_grid:
		choices_grid.columns = grid_columns(narrow)
		size_pictures(narrow)
	if actions and is_instance_valid(actions):
		actions.vertical = narrow
	# Keep the header on one line on phones: drop the secondary bits.
	if header_mode and is_instance_valid(header_mode):
		header_mode.visible = size.x >= 480
	if header_dots and is_instance_valid(header_dots):
		header_dots.visible = not narrow
	report_state()

func report_state() -> void:
	if bridge and not state.is_empty():
		var focused := get_viewport().gui_get_focus_owner()
		bridge.report(JSON.stringify({"focus": str(focused.name) if focused else "", "answerReady": submit_button != null and not submit_button.disabled,"roundId": state.round.id, "phase": "result" if state.get("result") is Dictionary else "answer", "kind": state.round.kind, "played": state.get("played", false), "width": size.x, "recording": state.get("recording", "idle"), "pending": state.get("pending", false)}))

func emit_action(action: String, answer: Dictionary = {}) -> void:
	action_requested.emit(action, answer)
	if bridge:
		bridge.emit(action, JSON.stringify(answer), str(state.get("round", {}).get("id", "")))
