extends Control

var kind := "listen"
const INK := Color("176b58")
const GOLD := Color("e6a64a")

func _draw() -> void:
	var center := size / 2
	var scale_factor := minf(size.x / 240, size.y / 100)
	draw_set_transform(center, 0, Vector2.ONE * scale_factor)
	match kind:
		"umbrella":
			draw_colored_polygon(PackedVector2Array([Vector2(-48, 0), Vector2(-40, -24), Vector2(-20, -42), Vector2(0, -48), Vector2(20, -42), Vector2(40, -24), Vector2(48, 0)]), INK)
			draw_line(Vector2.ZERO, Vector2(0, 33), GOLD, 6, true)
			draw_arc(Vector2(10, 33), 10, 0, PI, 20, GOLD, 6, true)
		"book":
			draw_style_box(panel(Color("e8bf78")), Rect2(-48, -35, 96, 70))
			draw_line(Vector2(0, -31), Vector2(0, 30), INK, 4, true)
			for y in [-18, -3, 12]:
				draw_line(Vector2(-35, y), Vector2(-10, y), INK, 3, true)
				draw_line(Vector2(10, y), Vector2(35, y), INK, 3, true)
		"bridge":
			for x in [-35, 35]:
				draw_line(Vector2(x, -36), Vector2(x, 30), INK, 7, true)
			draw_line(Vector2(-65, 12), Vector2(65, 12), GOLD, 8, true)
			draw_polyline(PackedVector2Array([Vector2(-65, 10), Vector2(-35, -32), Vector2.ZERO, Vector2(35, -32), Vector2(65, 10)]), INK, 4, true)
			for y in [32, 42]:
				draw_line(Vector2(-65, y), Vector2(65, y), Color("9cbdce"), 3, true)
		"bicycle":
			for x in [-38, 38]:
				draw_arc(Vector2(x, 17), 26, 0, TAU, 48, INK, 5, true)
			draw_polyline(PackedVector2Array([Vector2(-38, 17), Vector2(-15, -17), Vector2(10, 17), Vector2(-38, 17), Vector2(20, -20), Vector2(38, 17)]), GOLD, 4, true)
			draw_line(Vector2(20, -20), Vector2(18, -35), INK, 4, true)
			draw_line(Vector2(18, -35), Vector2(35, -35), INK, 4, true)
		_:
			draw_style_box(panel(Color("e1ece3") if kind == "voice" else Color("e5eafb")), Rect2(-110, -45, 220, 90))
			for index in 15:
				var height := 8 + 25 * absf(sin(index * 0.75))
				var x := float(index * 12 - 84)
				draw_line(Vector2(x, -height), Vector2(x, height), INK if kind == "voice" else Color("3156ce"), 5, true)

func panel(color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(14)
	return style

func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()
