extends RefCounted

signal chosen(scene: String, role: String, difficulty: String)
signal opened

var panel: PanelContainer
var picker: OptionButton
var roles: OptionButton
var difficulty: OptionButton
var description: Label
var catalog: Array
var backdrop: ColorRect
var interface: CanvasLayer
var enter_button: Button
var back_button: Button

func _init(ui: CanvasLayer, scenes: Array) -> void:
	interface = ui
	catalog = scenes
	var entry: Button = ui.button("Scenes", show_menu)
	entry.tooltip_text = "Choose a setting and your conversation role"
	var header: HBoxContainer = ui.header_row
	header.add_child(entry)
	header.move_child(entry, 1)
	backdrop = ColorRect.new()
	backdrop.color = Color(0.18, 0.13, 0.09, 0.34)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop.z_index = 4
	ui.root.add_child(backdrop)
	panel = ui.panel()
	panel.z_index = 5
	ui.root.add_child(panel)
	var column: VBoxContainer = ui.content(panel)
	column.add_child(ui.label("Choose your scene", 24))
	column.add_child(ui.label("Pick a role, explore the areas and meet the NPCs. Move with WASD or arrows; press E nearby.", 15))
	picker = OptionButton.new()
	picker.custom_minimum_size.y = 44
	picker.add_item("After Class Cafe · original journey")
	for scene in catalog:
		picker.add_item(scene.title)
	column.add_child(picker)
	column.add_child(ui.label("Your role", 15))
	roles = OptionButton.new()
	roles.custom_minimum_size.y = 44
	column.add_child(roles)
	column.add_child(ui.label("Practice", 15))
	difficulty = OptionButton.new()
	difficulty.custom_minimum_size.y = 44
	difficulty.add_item("Everyday")
	difficulty.add_item("Real-life challenge · US & Canada")
	column.add_child(difficulty)
	description = ui.label("", 15)
	column.add_child(description)
	column.add_child(ui.label("Three goals per role. Challenges add a follow-up at each stop. Repeat corrections to continue. Each difficulty keeps its own progress on this device.", 13))
	var actions := HBoxContainer.new()
	enter_button = ui.button("Enter scene", enter, true)
	back_button = ui.button("Back", hide_menu)
	actions.add_child(enter_button)
	actions.add_child(back_button)
	column.add_child(actions)
	picker.item_selected.connect(update_roles)
	roles.item_selected.connect(update_description)
	difficulty.item_selected.connect(update_description)
	ui.root.resized.connect(layout.bind(ui.root))
	update_roles(0)
	panel.hide()
	backdrop.hide()
	layout(ui.root)

func layout(root: Control) -> void:
	panel.size = Vector2(minf(520, root.size.x - 36), 0)
	panel.position = Vector2((root.size.x - panel.size.x) / 2, 100)

func show_menu() -> void:
	opened.emit()
	panel.show()
	backdrop.show()
	picker.grab_focus()
	interface.announce("Choose your scene, role and practice difficulty, then enter the scene.")

func hide_menu() -> void:
	panel.hide()
	backdrop.hide()

func update_roles(index: int) -> void:
	roles.clear()
	roles.disabled = index == 0
	roles.focus_mode = Control.FOCUS_NONE if index == 0 else Control.FOCUS_ALL
	if index == 0:
		roles.add_item("Cafe learner")
	else:
		for role in catalog[index - 1].roles:
			roles.add_item(role.label)
	roles.select(0)
	update_description(0)
	# Explicit focus neighbours make this canvas menu modal for keyboard users.
	var controls: Array = [picker, difficulty, enter_button, back_button] if index == 0 else [picker, roles, difficulty, enter_button, back_button]
	for position in controls.size():
		var item: Control = controls[position]
		item.focus_next = item.get_path_to(controls[(position + 1) % controls.size()])
		item.focus_previous = item.get_path_to(controls[(position - 1 + controls.size()) % controls.size()])

func update_description(_index: int) -> void:
	if picker.selected == 0:
		description.text = "Handle a detailed order and pickup mix-up with Maya, then negotiate plans with Alex. Six exchanges in the forest cafe." if difficulty.selected == 1 else "Order at the counter, then sit and chat with Alex. Your original cafe save is kept."
	else:
		var role: Dictionary = catalog[picker.selected - 1].roles[roles.selected]
		description.text = "Explore the areas and meet three NPCs. First stop: " + str(role.turns[0].npc.name) + "."
		if difficulty.selected == 1:
			description.text += " Explain your choices, handle a complication and answer a follow-up before moving on."

func enter() -> void:
	hide_menu()
	var practice := "challenge" if difficulty.selected == 1 else "everyday"
	if picker.selected == 0:
		chosen.emit("cafe", "customer" if practice == "challenge" else "learner", practice)
	else:
		var scene: Dictionary = catalog[picker.selected - 1]
		chosen.emit(scene.id, scene.roles[roles.selected].id, practice)
