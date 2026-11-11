extends Node2D

# Original vector art: shared floor coordinates keep sprites and collision aligned.
const ROOM := Vector2(960, 650)
const INK := Color("#51483c")
const CREAM := Color("#fff6e6")
var world: Node3D
var elapsed := 0.0
var font: Font = ThemeDB.fallback_font

func frame_room(viewport: Vector2) -> void:
	var left := 288.0 if viewport.x >= 900 else 16.0
	var top := 100.0 if viewport.x >= 900 else 180.0
	var available := Rect2(left, top, maxf(1, viewport.x - left - 18), maxf(1, viewport.y - top - 16))
	var factor := minf(available.size.x / ROOM.x, available.size.y / ROOM.y)
	scale = Vector2.ONE * factor
	position = available.get_center() - ROOM * factor / 2
	queue_redraw()

func room_bounds() -> Rect2:
	return Rect2(position, ROOM * scale)

func point(at: Vector3) -> Vector2:
	return Vector2(480 + at.x * 58, 380 + at.z * 36)

func panel(bounds: Rect2, colour: Color, radius := 12.0, border := Color.TRANSPARENT) -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = colour
	style.border_color = border
	style.set_border_width_all(2 if border.a > 0 else 0)
	style.set_corner_radius_all(int(radius))
	draw_style_box(style, bounds)

func ellipse(at: Vector2, size: Vector2, colour: Color) -> void:
	# Draw at final size so one-pixel antialiasing does not become a scaled blur.
	var points := PackedVector2Array()
	for index in 40:
		var angle := index * TAU / 40
		points.append(at + Vector2(cos(angle), sin(angle)) * size)
	draw_colored_polygon(points, colour)
	points.append(points[0])
	draw_polyline(points, colour, 1, true)

func text_centered(at: Vector2, value: String, size := 16, colour := INK) -> void:
	var width := font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x
	draw_string(font, at - Vector2(width / 2, 0), value, HORIZONTAL_ALIGNMENT_LEFT, -1, size, colour)

func _draw() -> void:
	room()
	person(point(Vector3(0, 0, -3.55)), "maya")
	counter()
	for z in [-0.3, 3.2]:
		for x in [-4.6, 4.6]:
			table(point(Vector3(x, 0, z)), x > 0 and z < 0)
	person(point(Vector3(5.75, 0, -0.3)), "alex", false, true)
	var foot := point(world.player.position)
	var walking: bool = Vector2(world.player.velocity.x, world.player.velocity.z).length() > 0.1
	person(foot, "you", walking, world.seated, world.hero.rotation.y)
	if world.seated:
		# A foreground tabletop gives the seated characters a shared physical space.
		tabletop(point(Vector3(4.6, 0, -0.3)), true)
	elif world.cups.get_child_count() > 0:
		for index in 2:
			cup(foot + Vector2(-11 + index * 22, -28), 0.7)
	tag(point(Vector3(0, 0, -3.55)) + Vector2(0, -94), "MAYA · BARISTA")
	tag(point(Vector3(5.75, 0, -0.3)) + Vector2(0, -94), "ALEX · YOUR FRIEND")
	var guide: String = world.guide_npc_id()
	if not world.in_conversation and guide != "":
		var target := point(Vector3(5.75, 0, -0.3) if guide == "friend" else Vector3(0, 0, -3.55))
		var bob := sin(elapsed * 2.5) * 3
		draw_circle(target + Vector2(45, -68 + bob), 13, Color("#e9ae5c"), true, -1, true)
		text_centered(target + Vector2(45, -63 + bob), "E", 15)
	text_centered(Vector2(480, 627), "AFTER CLASS CAFÉ  ·  A LITTLE PAUSE, A GOOD CONVERSATION", 13, Color("#847059"))

func room() -> void:
	panel(Rect2(20, 20, 920, 594), Color(0.25, 0.22, 0.17, 0.09), 35)
	panel(Rect2(12, 6, 936, 598), Color("#ede1c8"), 30, Color("#a48d6e"))
	panel(Rect2(34, 30, 892, 166), Color("#627e69"), 18)
	for x in [72, 365, 658]:
		window_at(Vector2(x, 48))
	panel(Rect2(34, 177, 892, 410), Color("#b8895e"), 14)
	# Long staggered floorboards, with restrained grain and warm window reflections.
	for row in 10:
		var y := 180 + row * 40
		var shade := Color("#c69d72") if row % 2 == 0 else Color("#cda67d")
		panel(Rect2(37, y, 886, 38), shade, 2)
		for column in 5:
			var x := 40 + column * 204 + (96 if row % 2 else 0)
			if x < 916:
				draw_line(Vector2(x, y + 2), Vector2(x, y + 36), Color("#b88f67"), 1, true)
			draw_line(Vector2(x + 14, y + 25), Vector2(minf(912, x + 100), y + 25), Color(0.51, 0.35, 0.22, 0.09), 1, true)
	for x in [84, 682]:
		draw_colored_polygon(PackedVector2Array([Vector2(x, 199), Vector2(x + 150, 199), Vector2(x + 227, 331), Vector2(x + 32, 331)]), Color(1, 0.93, 0.68, 0.16))
	panel(Rect2(52, 562, 856, 16), Color("#896647"), 4)
	panel(Rect2(414, 542, 132, 50), Color("#627e69"), 12)
	text_centered(Vector2(480, 571), "WELCOME", 16, CREAM)
	for at in [Vector2(87, 225), Vector2(872, 225), Vector2(75, 535), Vector2(879, 530)]:
		plant(at, 1.05)
	# Small reading nook and a quiet sleeping cat.
	panel(Rect2(83, 242, 144, 60), Color("#8c6b50"), 9)
	for index in 9:
		var colour: Color = [Color("#b96548"), Color("#dfc693"), Color("#66867e")][index % 3]
		panel(Rect2(94 + index * 13, 250, 10, 32 - index % 3 * 4), colour, 2)
	ellipse(Vector2(155, 320), Vector2(31, 12), Color("#a5774e"))
	ellipse(Vector2(157, 315), Vector2(28, 15), Color("#e8be82"))
	draw_circle(Vector2(178, 310), 12, Color("#efca91"), true, -1, true)
	draw_colored_polygon(PackedVector2Array([Vector2(169, 303), Vector2(169, 292), Vector2(179, 301)]), Color("#efca91"))
	draw_arc(Vector2(180, 309), 4, 0.2, 2.8, 10, INK, 1.5, true)
	for at in [Vector2(275, 49), Vector2(610, 49)]:
		draw_line(at, at + Vector2(0, 58), INK, 3, true)
		ellipse(at + Vector2(0, 82), Vector2(47, 20), Color(1, 0.88, 0.57, 0.18))
		draw_colored_polygon(PackedVector2Array([at + Vector2(-23, 67), at + Vector2(-11, 45), at + Vector2(11, 45), at + Vector2(23, 67)]), Color("#d49a52"))
		ellipse(at + Vector2(0, 67), Vector2(23, 5), Color("#ffe4a1"))

func window_at(at: Vector2) -> void:
	panel(Rect2(at, Vector2(228, 120)), Color("#d5bd8c"), 12)
	panel(Rect2(at + Vector2(7, 7), Vector2(214, 106)), Color("#c5d9ce"), 8)
	ellipse(at + Vector2(171, 31), Vector2(18, 18), Color("#fff2c6"))
	for index in 5:
		var x := 17 + index * 42
		panel(Rect2(at + Vector2(x, 39), Vector2(7, 67)), Color("#8ca28b"), 3)
		ellipse(at + Vector2(x + 3, 41), Vector2(24, 30), Color("#a1b69b"))
	draw_line(at + Vector2(114, 7), at + Vector2(114, 114), Color("#e8d6ad"), 6, true)
	draw_line(at + Vector2(7, 62), at + Vector2(220, 62), Color("#e8d6ad"), 5, true)
	panel(Rect2(at + Vector2(-6, 115), Vector2(240, 12)), Color("#dec89c"), 4)
	for index in 4:
		var leaf_at := at + Vector2(8 + index * 31, -1 + index % 2 * 5)
		draw_line(leaf_at - Vector2(0, 12), leaf_at + Vector2(0, 27), Color("#416a50"), 2, true)
		for side in [-1, 1]:
			ellipse(leaf_at + Vector2(side * 7, 11), Vector2(9, 5), Color("#729267"))

func counter() -> void:
	panel(Rect2(284, 278, 396, 76), Color(0.32, 0.23, 0.16, 0.14), 20)
	panel(Rect2(292, 276, 376, 67), Color("#537761"), 14, Color("#3f614e"))
	for index in 12:
		draw_line(Vector2(307 + index * 30, 292), Vector2(307 + index * 30, 333), Color("#7f9b75"), 3, true)
	panel(Rect2(280, 256, 400, 35), Color("#e2bc85"), 14, Color("#9b734f"))
	panel(Rect2(287, 260, 385, 7), Color("#efd2a2"), 5)
	# Espresso machine, pastry dome and a little register.
	panel(Rect2(312, 208, 82, 51), Color("#48615c"), 10)
	panel(Rect2(320, 214, 66, 22), Color("#8baba5"), 6)
	for x in [331, 366]:
		draw_circle(Vector2(x, 224), 5, CREAM, true, -1, true)
		draw_line(Vector2(x, 237), Vector2(x, 250), INK, 4, true)
	cup(Vector2(351, 256), 0.62)
	panel(Rect2(557, 218, 71, 39), Color(0.8, 0.9, 0.85, 0.5), 17, Color("#e6e9d8"))
	panel(Rect2(551, 254, 83, 9), Color("#9e7451"), 4)
	for x in [574, 602]:
		ellipse(Vector2(x, 247), Vector2(13, 7), Color("#c88b45"))
		draw_arc(Vector2(x, 244), 8, 0.1, 3.0, 12, Color("#ebbb6e"), 4, true)
	panel(Rect2(419, 261, 61, 13), Color("#655a49"), 4)
	panel(Rect2(424, 230, 51, 31), Color("#506c62"), 6, Color("#d5cfaf"))
	text_centered(Vector2(449, 251), "hello!", 12, CREAM)
	plant(Vector2(648, 250), 0.58)

func table(at: Vector2, friend_table: bool) -> void:
	ellipse(at + Vector2(0, 23), Vector2(72, 22), Color(0.32, 0.23, 0.16, 0.15))
	for side in [-1, 1]:
		var chair := at + Vector2(side * 64, 0)
		panel(Rect2(chair - Vector2(20, 26), Vector2(40, 44)), Color("#6d9179"), 13, Color("#486951"))
		panel(Rect2(chair - Vector2(16, 15), Vector2(32, 27)), Color("#a5baa0"), 9)
	panel(Rect2(at + Vector2(-7, 0), Vector2(14, 40)), Color("#8b674a"), 6)
	tabletop(at, friend_table and world.cups.get_child_count() > 0)

func tabletop(at: Vector2, drinks: bool) -> void:
	ellipse(at + Vector2(0, 3), Vector2(55, 30), Color("#936e4c"))
	ellipse(at, Vector2(55, 29), Color("#e3bd87"))
	draw_arc(at, 44, 3.6, 6.0, 24, Color("#f3d7a5"), 2, true)
	plant(at + Vector2(20, -7), 0.34)
	if drinks:
		for side in [-1, 1]:
			cup(at + Vector2(side * 26, 0), 0.72)

func plant(at: Vector2, factor: float) -> void:
	ellipse(at + Vector2(0, 7) * factor, Vector2(22, 9) * factor, Color(0.28, 0.26, 0.19, 0.14))
	panel(Rect2(at + Vector2(-15, -12) * factor, Vector2(30, 25) * factor), Color("#c17f5d"), 6 * factor)
	ellipse(at + Vector2(0, -12) * factor, Vector2(16, 6) * factor, Color("#e5b28b"))
	for index in 7:
		var angle := index * TAU / 7
		var leaf := at + Vector2(cos(angle) * 19, -33 + sin(angle) * 15) * factor
		draw_line(at + Vector2(0, -10) * factor, leaf, Color("#527449"), 3 * factor, true)
		ellipse(leaf, Vector2(15, 8) * factor, Color("#76905a") if index % 2 else Color("#5b825a"))

func cup(at: Vector2, factor: float) -> void:
	ellipse(at + Vector2(0, 4) * factor, Vector2(17, 7) * factor, Color("#e9dcc0"))
	draw_arc(at + Vector2(13, -5) * factor, 6 * factor, -PI / 2, PI / 2, 12, CREAM, 3 * factor, true)
	panel(Rect2(at + Vector2(-12, -15) * factor, Vector2(24, 19) * factor), CREAM, 7 * factor)
	ellipse(at + Vector2(0, -13) * factor, Vector2(10, 4) * factor, Color("#976b49"))
	for index in 2:
		var drift := sin(elapsed * 1.5 + index * 2) * 2
		draw_arc(at + Vector2(index * 8 - 4 + drift, -25) * factor, 8 * factor, -1.5, 0.8, 12, Color(1, 0.97, 0.85, 0.4), 2 * factor, true)

func person(at: Vector2, role: String, walking := false, sitting := false, direction := 0.0) -> void:
	var bob := sin(elapsed * 11) * 2 if walking else sin(elapsed * 1.6) * 0.7
	var footstep := sin(elapsed * 11) * 5 if walking else 0.0
	var skin := Color("#e8b68b")
	var hair := Color("#574032")
	var shirt := Color("#617f9b") if role == "you" else Color("#729b8b") if role == "alex" else Color("#f4e6c8")
	ellipse(at + Vector2(0, 5), Vector2(23, 8), Color(0.25, 0.22, 0.18, 0.17))
	for side in [-1, 1]:
		var leg := Vector2(side * 8, -18 + side * footstep)
		if sitting:
			leg.x += -10 if role == "alex" else 10
		panel(Rect2(at + leg - Vector2(5, 0), Vector2(10, 22)), Color("#555c66") if role != "maya" else Color("#8b6750"), 4)
		panel(Rect2(at + leg + Vector2(-7, 17), Vector2(16, 7)), CREAM, 4, Color("#706552"))
	at.y += bob
	panel(Rect2(at + Vector2(-18, -47), Vector2(36, 34)), shirt, 11, Color("#50665b"))
	for side in [-1, 1]:
		var hand := at + Vector2(side * 21, -24 + side * footstep * 0.5)
		draw_line(at + Vector2(side * 15, -41), hand, shirt, 10, true)
		draw_circle(hand, 5, skin, true, -1, true)
	if role == "maya":
		panel(Rect2(at + Vector2(-13, -39), Vector2(26, 29)), Color("#b67c57"), 5)
		panel(Rect2(at + Vector2(-8, -23), Vector2(16, 10)), Color("#ce9b70"), 3)
	else:
		draw_line(at + Vector2(0, -43), at + Vector2(0, -16), CREAM, 4, true)
		for y in [-35, -24]:
			draw_circle(at + Vector2(6, y), 1.6, Color("#e6c78b"), true, -1, true)
	draw_circle(at + Vector2(0, -61), 20, hair, true, -1, true)
	if role == "maya":
		draw_circle(at + Vector2(12, -81), 11, hair, true, -1, true)
	draw_circle(at + Vector2(0, -59), 17, skin, true, -1, true)
	for side in [-1, 1]:
		draw_circle(at + Vector2(side * 17, -58), 4, skin, true, -1, true)
	for index in 5:
		draw_circle(at + Vector2(-14 + index * 7, -73 + index % 2 * 3), 8, hair, true, -1, true)
	var back: bool = role == "you" and not sitting and absf(direction) > 2.3
	if back:
		draw_circle(at + Vector2(0, -63), 18, hair, true, -1, true)
		panel(Rect2(at + Vector2(-13, -44), Vector2(26, 28)), Color("#ac8150"), 8, Color("#76583d"))
		panel(Rect2(at + Vector2(-9, -29), Vector2(18, 11)), Color("#c29a63"), 5)
	else:
		var blink := fmod(elapsed + (1.4 if role == "alex" else 0.0), 5) < 0.12
		for side in [-1, 1]:
			ellipse(at + Vector2(side * 7, -58), Vector2(2.1, 0.7 if blink else 2.8), INK)
			ellipse(at + Vector2(side * 11, -52), Vector2(4, 2), Color(0.84, 0.43, 0.35, 0.25))
		draw_arc(at + Vector2(0, -53), 4, 0.2, 2.9, 12, INK, 1.3, true)

func tag(at: Vector2, caption: String) -> void:
	panel(Rect2(at - Vector2(80, 17), Vector2(160, 25)), Color(1, 0.97, 0.88, 0.92), 12)
	text_centered(at, caption, 12, Color("#4a6451"))
