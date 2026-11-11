extends RefCounted

# Original procedural clothing and fairground art. Geometry uses the world's
# shared material/primitive helpers; no downloaded assets or model-driven state.
const CLOTH_COLOURS := {
	"blue": "#6b9dcc", "red": "#ca786e", "green": "#87a67c",
	"yellow": "#dec273", "pink": "#d6a0af", "purple": "#aa92bb",
	"white": "#f1e9d6", "black": "#52616a", "orange": "#d89c68"
}
const TICKET_NUMBERS := {
	"one": 1, "two": 2, "three": 3, "four": 4, "five": 5, "six": 6,
	"seven": 7, "eight": 8, "nine": 9, "ten": 10, "eleven": 11, "twelve": 12
}
const LANDMARKS := {
	"ferris_wheel": ["FERRIS WHEEL", Vector3(6.5, 1.4, -6.7)],
	"carousel": ["CAROUSEL", Vector3(13.4, 1.4, -6.7)],
	"coaster": ["ROLLER COASTER", Vector3(10, 1.4, -10.7)],
	"tickets": ["TICKET BOOTH", Vector3(-10, 1.4, -7.1)],
	"snacks": ["SNACK STAND", Vector3(10, 1.4, 4.8)],
	"entrance": ["PARK ENTRANCE", Vector3(0.4, 1.4, 10.7)]
}
var world: Node3D
var wheel: Node3D
var carousel: Node3D
var cabins: Array[Node3D] = []

func _init(context: Node3D) -> void:
	world = context

func _node(parent: Node3D, title: String, at := Vector3.ZERO) -> Node3D:
	var node := Node3D.new()
	node.name = title
	node.position = at
	parent.add_child(node)
	return node

func _beam(parent: Node3D, start: Vector3, end: Vector3, width: float, colour: String) -> void:
	var beam: MeshInstance3D = world._box(parent, Vector3(width, start.distance_to(end), width), (start + end) / 2, colour)
	beam.quaternion = Quaternion(Vector3.UP, (end - start).normalized())

func _ring(parent: Node3D, radius: float, thickness: float, at: Vector3, colour: String) -> MeshInstance3D:
	var ring := TorusMesh.new()
	ring.inner_radius = radius - thickness
	ring.outer_radius = radius + thickness
	ring.rings = 32
	ring.ring_segments = 8
	var part: MeshInstance3D = world._mesh(parent, ring, at, colour)
	part.rotation.x = PI / 2
	return part

func _garment(parent: Node3D, kind: String, colour: String, at := Vector3.ZERO) -> Node3D:
	var clothes := _node(parent, "Garment", at)
	var body: MeshInstance3D = world._box(clothes, Vector3(0.55, 0.59, 0.12), Vector3(0, 0.32, 0), colour)
	body.name = kind.capitalize().replace("-", "") + "Body"
	for side in [-1, 1]:
		var sleeve: MeshInstance3D = world._box(clothes, Vector3(0.18, 0.28 if kind == "t-shirt" else 0.54, 0.13), Vector3(side * 0.33, 0.39 if kind == "t-shirt" else 0.3, 0), colour)
		sleeve.rotation.z = side * 0.25
		world._box(clothes, Vector3(0.16, 0.05, 0.14), Vector3(side * 0.38, 0.06 if kind != "t-shirt" else 0.25, 0), colour)
	world._oval(clothes, Vector3(0, 0.61, 0.015), Vector3(0.2, 0.1, 0.1), "#eee1c9")
	if kind == "hoodie":
		world._oval(clothes, Vector3(0, 0.71, -0.015), Vector3(0.37, 0.35, 0.18), colour)
		world._oval(clothes, Vector3(0, 0.72, 0.07), Vector3(0.23, 0.23, 0.055), "#d6dfdc")
		world._box(clothes, Vector3(0.31, 0.13, 0.025), Vector3(0, 0.17, 0.077), colour)
		for side in [-1, 1]:
			world._box(clothes, Vector3(0.012, 0.18, 0.015), Vector3(side * 0.07, 0.48, 0.08), "#f5ead5")
	elif kind == "jacket":
		world._box(clothes, Vector3(0.025, 0.57, 0.025), Vector3(0, 0.32, 0.078), "#f4e1bb")
		for side in [-1, 1]:
			world._box(clothes, Vector3(0.13, 0.12, 0.028), Vector3(side * 0.16, 0.2, 0.078), "#d9ae8d")
	return clothes

func _rack(at: Vector3, title: String, colours: Array) -> void:
	var rack := _node(world.scenery, "ClothingRack_" + title, at)
	world._box(rack, Vector3(2.7, 0.14, 0.65), Vector3(0, 0.07, 0), "#c6ac86", true)
	for x in [-1.25, 1.25]:
		world._round(rack, Vector3(x, 0.98, 0), 0.045, 1.96, "#a38b72")
		world._box(rack, Vector3(0.45, 0.1, 0.6), Vector3(x, 0.05, 0), "#c6ac86", true)
	_beam(rack, Vector3(-1.28, 1.95, 0), Vector3(1.28, 1.95, 0), 0.07, "#a38b72")
	for index in range(3):
		var x := (index - 1) * 0.79
		_beam(rack, Vector3(x - 0.16, 1.76, 0), Vector3(x, 1.91, 0), 0.027, "#e1c8a0")
		_beam(rack, Vector3(x, 1.91, 0), Vector3(x + 0.16, 1.76, 0), 0.027, "#e1c8a0")
		var garment := _garment(rack, ["hoodie", "t-shirt", "jacket"][index], colours[index], Vector3(x, 1.14, 0.06))
		garment.scale = Vector3.ONE * 0.76
	world._text(rack, title, Vector3(0, 2.2, 0), 25, "#5e7660", 0.0048)

func build_shopping() -> void:
	world._window(Vector3(-10, 2.5, -13.55), "#a6baa2")
	world._window(Vector3(10, 2.5, -13.55), "#d1a5a4")
	_rack(Vector3(-13.1, 0, -8.4), "COZY COLOURS", ["#6b9dcc", "#dec273", "#ca786e"])
	_rack(Vector3(-6.7, 0, -8.4), "YOUR FAVOURITES", ["#87a67c", "#d6a0af", "#aa92bb"])
	var sale := _node(world.scenery, "SaleArea", Vector3(10, 0, -8.1))
	world._box(sale, Vector3(3.6, 0.9, 1.2), Vector3(0, 0.45, 0), "#c3a17d", true)
	world._box(sale, Vector3(3.8, 0.1, 1.3), Vector3(0, 0.95, 0), "#ead6b6")
	for index in range(4):
		for layer in range(3):
			world._box(sale, Vector3(0.6, 0.07, 0.7), Vector3(-1.2 + index * 0.8, 1.04 + layer * 0.075, 0), ["#6b9dcc", "#87a67c", "#d6a0af", "#dec273"][index])
	world._box(sale, Vector3(2.5, 0.6, 0.08), Vector3(0, 1.8, -0.4), "#ca786e")
	world._text(sale, "SALE · LOVELY FINDS", Vector3(0, 1.82, -0.34), 29, "#fff1d8", 0.005)
	var fitting := _node(world.scenery, "FittingRoom", Vector3(10, 0, 3.7))
	world._box(fitting, Vector3(2.8, 2.5, 0.16), Vector3(0, 1.25, -0.6), "#d1b59b", true)
	for x in [-1.35, 1.35]:
		world._box(fitting, Vector3(0.13, 2.5, 1.8), Vector3(x, 1.25, 0.22), "#e0c9aa", true)
	for index in range(12):
		world._box(fitting, Vector3(0.2, 2.05, 0.09), Vector3(-1.1 + index * 0.2, 1.25, 1.05), "#a7b8b5" if index % 2 == 0 else "#bccbc5", true)
	world._text(fitting, "FITTING ROOMS", Vector3(0, 2.7, 1.1), 29, "#5b7560", 0.005)
	world._box(world.scenery, Vector3(1.4, 2.35, 0.14), Vector3(6.6, 1.2, 4.6), "#c2a27e", true)
	var mirror: MeshInstance3D = world._box(world.scenery, Vector3(1.2, 2.14, 0.035), Vector3(6.6, 1.2, 4.69), "#bfd9d8")
	mirror.name = "ShopMirror"
	_beam(world.scenery, Vector3(6.1, 0.4, 4.72), Vector3(7.1, 1.9, 4.72), 0.035, "#e6efdf")
	world._asset("furniture/pottedPlant", Vector3(14.7, 0, 9.7), 1.55)

func _build_wheel() -> void:
	var support := _node(world.scenery, "FerrisWheel", Vector3(6.5, 0, -8.0))
	world._round(support, Vector3(0, 0.12, 0), 1.55, 0.24, "#c8ae86")
	for side in [-1, 1]:
		_beam(support, Vector3(side * 1.05, 0.2, 0.2), Vector3(0, 2.15, 0.2), 0.14, "#e2c99c")
	wheel = _node(support, "FerrisWheelRotor", Vector3(0, 2.15, 0.05))
	_ring(wheel, 1.35, 0.065, Vector3.ZERO, "#dfb46e")
	world._oval(wheel, Vector3(0, 0, 0.1), Vector3(0.3, 0.3, 0.18), "#c98078")
	for index in range(8):
		var angle := index * TAU / 8
		var edge := Vector3(cos(angle), sin(angle), 0) * 1.35
		_beam(wheel, Vector3.ZERO, edge, 0.045, "#ebd8ae")
		var cabin := _node(wheel, "WheelCabin" + str(index), edge)
		cabins.append(cabin)
		world._box(cabin, Vector3(0.4, 0.3, 0.35), Vector3(0, -0.2, 0.08), ["#c98078", "#8eabb8", "#91ac84", "#d5b46d"][index % 4])
		world._box(cabin, Vector3(0.44, 0.06, 0.38), Vector3(0, 0.06, 0.08), "#f0dbb3")
		for side in [-1, 1]:
			world._box(cabin, Vector3(0.035, 0.21, 0.03), Vector3(side * 0.17, -0.04, 0.23), "#ebd8ae")
	world._box(support, Vector3(2.7, 0.5, 0.75), Vector3(0, 0.25, 0), "#c8ae86", true)

func _build_carousel() -> void:
	var base := _node(world.scenery, "Carousel", Vector3(13.4, 0, -8.0))
	world._round(base, Vector3(0, 0.2, 0), 1.5, 0.4, "#c8ad82")
	world._box(base, Vector3(2.5, 0.5, 2.2), Vector3(0, 0.25, 0), "#c8ad82", true)
	carousel = _node(base, "CarouselRotor")
	world._round(carousel, Vector3(0, 1.55, 0), 0.15, 2.6, "#dfbf79")
	world._round(carousel, Vector3(0, 2.65, 0), 1.6, 0.65, "#c98078", 0.18)
	world._round(carousel, Vector3(0, 2.31, 0), 1.65, 0.12, "#edd6a7")
	world._oval(carousel, Vector3(0, 3.04, 0), Vector3(0.22, 0.25, 0.22), "#dfbf79")
	for index in range(5):
		var angle := index * TAU / 5
		var at := Vector3(cos(angle), 0, sin(angle)) * 1.03
		world._round(carousel, at + Vector3(0, 1.37, 0), 0.025, 1.8, "#e9d3a4")
		var pony := _node(carousel, "CarouselPony" + str(index), at + Vector3(0, 1, 0))
		pony.rotation.y = -angle
		world._oval(pony, Vector3.ZERO, Vector3(0.55, 0.27, 0.21), "#efe3c7")
		world._oval(pony, Vector3(0.24, 0.2, 0), Vector3(0.18, 0.35, 0.18), "#efe3c7")
		world._box(pony, Vector3(0.21, 0.08, 0.24), Vector3(-0.02, 0.14, 0), "#91ac84")
		for x in [-0.17, 0.17]:
			world._box(pony, Vector3(0.055, 0.24, 0.06), Vector3(x, -0.17, 0), "#d3c19e")

func _build_coaster() -> void:
	var track := _node(world.scenery, "CoasterTrack", Vector3(10, 0, -11.7))
	# A compact distant track silhouette sits behind the conversation.
	for lane in [-0.2, 0.2]:
		for index in range(16):
			var x := -2.9 + index * 0.37
			var next_x := x + 0.37
			var y := 1.2 + 0.65 * sin(index * PI / 8)
			var next_y := 1.2 + 0.65 * sin((index + 1) * PI / 8)
			_beam(track, Vector3(x, y, lane), Vector3(next_x, next_y, lane), 0.08, "#8ba6ad")
			if index % 3 == 0:
				_beam(track, Vector3(x, 0.05, lane), Vector3(x, y, lane), 0.045, "#b9a987")
	world._box(track, Vector3(6.1, 0.18, 1.1), Vector3(0, 0.09, 0), "#b9a987", true)
	world._box(track, Vector3(0.5, 0.25, 0.48), Vector3(-1.4, 2.02, 0), "#ce8177")

func _booth(at: Vector3, title: String, colour: String, node_name: String) -> Node3D:
	var booth := _node(world.scenery, node_name, at)
	world._box(booth, Vector3(1.85, 1.1, 1.25), Vector3(0, 0.55, 0), "#d5b98d", true)
	world._box(booth, Vector3(2, 0.12, 1.4), Vector3(0, 1.15, 0), "#f1dfba")
	for x in [-0.88, 0.88]:
		world._round(booth, Vector3(x, 1.65, -0.38), 0.045, 1.1, "#c2a27b")
	for index in range(6):
		world._box(booth, Vector3(0.35, 0.13, 1.65), Vector3(-0.875 + index * 0.35, 2.2, -0.04), colour if index % 2 == 0 else "#f1dfba")
	world._box(booth, Vector3(1.6, 0.44, 0.05), Vector3(0, 0.65, 0.66), colour)
	world._text(booth, title, Vector3(0, 0.66, 0.7), 28, "#fff2d7", 0.0042)
	return booth

func build_park() -> void:
	_build_wheel()
	_build_carousel()
	_build_coaster()
	_booth(Vector3(-10, 0, -8.0), "TICKETS & ADVENTURES", "#8ba6ad", "TicketBooth")
	var stand := _booth(Vector3(10, 0, 4.0), "SNACKS & SMILES", "#c98078", "SnackStand")
	_snack(stand, "popcorn", Vector3(-0.42, 1.22, 0))
	_snack(stand, "ice cream", Vector3(0.38, 1.22, 0))
	for x in [-13.4, -6.6]:
		world._box(world.scenery, Vector3(0.75, 0.6, 0.75), Vector3(x, 0.3, -8), "#c5a47e", true)
		world._oval(world.scenery, Vector3(x, 1.3, -8), Vector3(1.25, 1.5, 1.25), "#91ac84")
	for index in range(9):
		var flag: MeshInstance3D = world._box(world.scenery, Vector3(0.45, 0.38, 0.04), Vector3(6 + index, 4.2 - sin(index * PI / 8) * 0.15, -10.2), ["#c98078", "#8ba6ad", "#dfbf79", "#91ac84"][index % 4])
		flag.rotation.z = 0.15 if index % 2 == 0 else -0.15
	world._box(world.scenery, Vector3(8.5, 0.02, 0.025), Vector3(10, 4.38, -10.2), "#b8a783")
	world._round(world.scenery, Vector3(6.3, 0.75, 10.8), 0.8, 0.12, "#c5a47e")
	world._round(world.scenery, Vector3(6.3, 0.35, 10.8), 0.12, 0.7, "#a28c6a")
	world._box(world.scenery, Vector3(1.4, 0.8, 1.4), Vector3(6.3, 0.4, 10.8), "#c5a47e", true)
	for x in [5.1, 7.5]:
		world._box(world.scenery, Vector3(0.4, 0.4, 1.2), Vector3(x, 0.2, 10.8), "#c5a47e", true)

func animate(delta: float, reduced: bool) -> void:
	if reduced:
		return
	if is_instance_valid(wheel):
		wheel.rotation.z += delta * 0.12
		for cabin in cabins:
			cabin.rotation.z = -wheel.rotation.z
	if is_instance_valid(carousel):
		carousel.rotation.y += delta * 0.18

func reset_ambient() -> void:
	if is_instance_valid(wheel):
		wheel.rotation = Vector3.ZERO
		for cabin in cabins:
			cabin.rotation = Vector3.ZERO
	if is_instance_valid(carousel):
		carousel.rotation = Vector3.ZERO

func _positive_choice(meaning: String, choices: Dictionary) -> String:
	# Keep alternatives separate so a rejected noun cannot win by lookup order.
	var text := meaning.to_lower().replace("’", "'").replace("no worries", "")
	for alternative in [" instead of ", " rather than "]:
		text = text.replace(alternative, "|not ")
	for boundary in [";", ",", ".", "!", "?", " but ", " let's ", " let us ", " and try ", " how about "]:
		text = text.replace(boundary, "|")
	text = text.replace("-", " ")
	var selected := ""
	for clause in text.split("|", false):
		var negated := false
		for negative in ["not ", "don't ", "won't ", "no ", "skip ", "avoid ", "scared ", "afraid ", "can't ", "instead of "]:
			if clause.contains(negative):
				negated = true
				break
		if negated:
			continue
		var padded := " " + clause.strip_edges() + " "
		var latest := -1
		for choice in choices:
			for phrase in choices[choice]:
				var found := padded.rfind(" " + phrase + " ")
				if found > latest:
					latest = found
					selected = choice
	return selected

func _outfit(prop: Node3D, meaning: String) -> String:
	var kind := _positive_choice(meaning, {
		"hoodie": ["hoodie", "hoodies"], "jacket": ["jacket", "jackets"],
		"sweatshirt": ["sweatshirt", "sweatshirts", "sweater", "sweaters"],
		"t-shirt": ["t shirt", "t shirts", "shirt", "shirts"],
		"dress": ["dress", "dresses"], "jeans": ["jeans"], "shorts": ["shorts"],
		"shoes": ["shoes", "sneakers"], "pants": ["pants", "trousers"],
		"skirt": ["skirt", "skirts"], "boots": ["boots"]
	})
	var colour_choices := {}
	for colour in CLOTH_COLOURS:
		colour_choices[colour] = [colour]
	var colour := _positive_choice(meaning, colour_choices)
	var finish: String = CLOTH_COLOURS.get(colour, "#cbbd9f")
	var description := ((colour + " ") if not colour.is_empty() else "") + kind
	if kind in ["hoodie", "jacket", "sweatshirt", "t-shirt"]:
		var garment := _garment(prop, kind, finish, Vector3(0, 0.05, 0))
		garment.name = "SelectedGarment"
	else:
		var card: MeshInstance3D = world._box(prop, Vector3(0.62, 0.62, 0.055), Vector3(0, 0.35, 0), finish)
		card.name = "ClothingCard"
		card.set_meta("item", kind)
		card.set_meta("colour", colour)
		var title: Label3D = world._text(prop, description.to_upper() if not kind.is_empty() else "YOUR OUTFIT", Vector3(0, 0.36, 0.04), 25, "#355c47", 0.0036)
		title.name = "ClothingChoice"
		world._box(prop, Vector3(0.48, 0.035, 0.015), Vector3(0, 0.15, 0.04), "#eee2c6")
	world._round(prop, Vector3(0, 0.025, 0), 0.33, 0.04, "#ead6b4")
	return description.capitalize() if not kind.is_empty() else "An outfit together"

func _price(prop: Node3D, meaning: String) -> String:
	var sale := meaning.contains("cheap") or meaning.contains("sale") or meaning.contains("less")
	world._box(prop, Vector3(0.48, 0.53, 0.045), Vector3(0, 0.29, 0), "#f7e7c7")
	world._oval(prop, Vector3(0, 0.5, 0.03), Vector3(0.055, 0.055, 0.025), "#b4a485")
	var tag: Label3D = world._text(prop, "SALE\nLESS TO SPEND" if sale else "PRICE\nLET'S CHECK", Vector3(0, 0.28, 0.033), 23, "#b46e65" if sale else "#5f7b63", 0.003)
	tag.name = "PriceTag"
	return "A cheaper find" if sale else "Price checked"

func _shopping_bag(prop: Node3D, meaning: String) -> String:
	var bag: MeshInstance3D = world._box(prop, Vector3(0.48, 0.52, 0.31), Vector3(0, 0.29, 0), "#a8bd9f")
	bag.name = "ShoppingBag"
	for side in [-1, 1]:
		world._box(prop, Vector3(0.035, 0.2, 0.035), Vector3(side * 0.13, 0.63, 0), "#f0dfbb")
	world._box(prop, Vector3(0.29, 0.035, 0.035), Vector3(0, 0.74, 0), "#f0dfbb")
	world._text(prop, "C & C", Vector3(0, 0.31, 0.17), 26, "#f8edcf", 0.0034)
	var fitting_choice := _positive_choice(meaning, {
		"fitting": ["try it on", "try this on", "try them on", "try on", "fitting room", "check the size"]
	})
	if fitting_choice != "":
		var fitting: Label3D = world._text(prop, "TRY IT ON\nFITTING ROOM ←", Vector3(0, 0.58, 0.17), 21, "#4e715b", 0.003)
		fitting.name = "FittingRoomCue"
		return "Ready to try on"
	return "Bags together"

func _landmark(meaning: String) -> String:
	return _positive_choice(meaning, {
		"coaster": ["coaster", "roller coaster", "rollercoaster"],
		"carousel": ["carousel", "merry go round"],
		"ferris_wheel": ["wheel", "ferris wheel"],
		"tickets": ["ticket", "tickets", "ticket booth"],
		"snacks": ["snack", "snacks", "snack stand", "food", "picnic", "ice cream", "icecream", "popcorn"],
		"entrance": ["entrance", "gate"]
	})

func _shared_plan(prop: Node3D, meaning: String, meeting := false) -> void:
	world._box(prop, Vector3(0.6, 0.65, 0.045), Vector3(0, 0.36, 0), "#e9d5ad")
	var clock: MeshInstance3D = world._round(prop, Vector3(0, 0.45, 0.05), 0.16, 0.025, "#f5e9cc")
	clock.rotation.x = PI / 2
	_beam(prop, Vector3(0, 0.45, 0.075), Vector3(0, 0.56, 0.075), 0.018, "#789079")
	_beam(prop, Vector3(0, 0.45, 0.075), Vector3(0.09, 0.42, 0.075), 0.018, "#789079")
	var words := "MEETING PLAN" if meeting else "OUR PARK PLAN"
	if not meeting and (meaning.contains("later") or meaning.contains("come back")):
		words = "COME BACK LATER"
	elif not meeting and (meaning.contains("worries") or meaning.contains("skip") or meaning.contains("not")):
		words = "TAKE OUR TIME"
	var label: Label3D = world._text(prop, words, Vector3(0, 0.2, 0.04), 23, "#486a53", 0.0029)
	label.name = "SharedMeetingCue" if meeting else "SharedPlanCue"

func _marker(prop: Node3D, landmark: String, meeting: bool) -> void:
	var marker := _node(prop, "MeetingMarker" if meeting else "SelectedRideMarker")
	if world.scene_id == "amusement_park":
		marker.position = LANDMARKS[landmark][1] - prop.position
	else:
		marker.position.y = 0.25
	world._round(marker, Vector3(0, 0.15, 0), 0.025, 0.7, "#9b8464")
	world._box(marker, Vector3(1.3, 0.35, 0.06), Vector3(0, 0.5, 0), "#e4c675" if meeting else "#a8bf9c")
	var label: Label3D = world._text(marker, ("MEET: " if meeting else "") + LANDMARKS[landmark][0], Vector3(0, 0.51, 0.045), 23, "#486a53", 0.0035)
	label.name = "MeetingLandmark" if meeting else "RideChoice"
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED

func _tickets(prop: Node3D, meaning: String) -> void:
	var quantity := 2
	var amount := RegEx.new()
	amount.compile("\\b([1-9][0-9]?)\\b")
	var found := amount.search(meaning)
	if found:
		quantity = int(found.get_string(1))
	else:
		var words := " " + meaning.replace(".", " ").replace(",", " ") + " "
		for word in TICKET_NUMBERS:
			if words.contains(" " + word + " "):
				quantity = TICKET_NUMBERS[word]
				break
	if meaning.contains("each"):
		quantity = 2
	# Larger orders use a labelled pack instead of filling the scene with cards.
	var visible := mini(quantity, 4)
	for index in visible:
		var x: float = (index - (visible - 1) / 2.0) * 0.34
		var ticket: MeshInstance3D = world._box(prop, Vector3(0.29, 0.51, 0.025), Vector3(x, 0.3, 0), "#e7c87d" if index % 2 == 0 else "#a5bfad")
		ticket.name = "ParkTicket" + str(index + 1)
		world._text(prop, "SUNNY\nDAYS\nADMIT 1", Vector3(x, 0.32, 0.022), 19, "#54705b", 0.0022)
		world._box(prop, Vector3(0.25, 0.015, 0.01), Vector3(x, 0.17, 0.02), "#f6e7c6")
	world._text(prop, "%d TICKETS" % quantity, Vector3(0, 0.68, 0.04), 22, "#486a53", 0.003)

func _snack(parent: Node3D, meaning: String, at := Vector3.ZERO) -> String:
	var snack := _node(parent, "ParkSnack", at)
	var choice := _positive_choice(meaning, {"icecream": ["ice cream", "icecream"], "popcorn": ["popcorn"]})
	if choice == "icecream":
		var cone: MeshInstance3D = world._round(snack, Vector3(0, 0.18, 0), 0.015, 0.32, "#d6b078", 0.13)
		cone.name = "IceCreamCone"
		world._oval(snack, Vector3(0, 0.4, 0), Vector3(0.32, 0.29, 0.32), "#ebc0b2")
		return "Ice cream together"
	if choice == "popcorn":
		var carton: MeshInstance3D = world._box(snack, Vector3(0.35, 0.36, 0.3), Vector3(0, 0.2, 0), "#f2e0ba")
		carton.name = "PopcornCarton"
		for x in [-0.12, 0.0, 0.12]:
			world._box(snack, Vector3(0.055, 0.34, 0.015), Vector3(x, 0.2, 0.16), "#c98078")
		for index in range(9):
			world._oval(snack, Vector3(-0.12 + (index % 3) * 0.12, 0.41 + (index % 2) * 0.035, -0.1 + (index / 3) * 0.1), Vector3(0.14, 0.13, 0.14), "#f2dda1")
		return "Popcorn to share"
	var food: MeshInstance3D = world._box(snack, Vector3(0.5, 0.4, 0.045), Vector3(0, 0.23, 0), "#f4e7c9")
	food.name = "SharedSnack"
	world._text(snack, "SNACK\nPLAN", Vector3(0, 0.25, 0.03), 24, "#486a53", 0.0034)
	return "A snack to share"

func apply_action(action: String, prop: Node3D, meaning: String, step_id: String) -> String:
	# Actual choices take priority; short confirmations can use the authored goal.
	var context := meaning
	if meaning.strip_edges() in ["", "yes", "yes!", "ok", "okay", "sure", "sure!"]:
		context = step_id.to_lower().replace("_", " ")
	match action:
		"outfit": return _outfit(prop, context)
		"price": return _price(prop, context)
		"bags": return _shopping_bag(prop, context)
		"ride":
			var landmark := _landmark(context)
			if landmark in ["ferris_wheel", "carousel", "coaster"]:
				_marker(prop, landmark, false)
			else:
				_shared_plan(prop, context)
			world._round(prop, Vector3(0, 0.05, 0), 0.27, 0.08, "#e5c979")
			return "Our park plan"
		"tickets":
			_tickets(prop, context)
			return "Tickets ready"
		"snack": return _snack(prop, context)
		"meet":
			var landmark := _landmark(context)
			if not landmark.is_empty():
				_marker(prop, landmark, true)
			else:
				_shared_plan(prop, context, true)
			world._round(prop, Vector3(0, 0.05, 0), 0.27, 0.08, "#e5c979")
			return "Meeting place chosen"
	return ""
