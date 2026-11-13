extends Control
## Small vector pieces for the arcade board: button icons, the score ring,
## level dots, chips and the soft page backdrop. Everything is drawn in code so
## the Web export needs no image assets.

const PALETTE := {
	"ink": Color("243d35"),
	"muted": Color("5b6f66"),
	"bg": Color("f3f6f1"),
	"line": Color("dbe5de"),
	"green": Color("17694f"),
	"green_soft": Color("e3f0e8"),
	"blue": Color("2f55c4"),
	"blue_soft": Color("e8eefc"),
	"orange": Color("d9791c"),
	"orange_soft": Color("fdf0e1"),
	"red": Color("b4352f"),
	"red_soft": Color("fbe7e4"),
	"white": Color.WHITE,
}

## Which piece to draw: "icon", "ring", "dots", "backdrop" or "badge".
var kind := "icon"
## Icon name for kind == "icon": play, mic, stop, check, next, retry, back.
var glyph := "play"
var color := Color.WHITE
## Score (ring), current level (dots) or letter (badge).
var value: Variant = 0
var accent := Color("2f55c4")
var _shown := 0.0

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	if kind == "icon" and get_parent() is Button:
		set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		get_parent().resized.connect(queue_redraw)
	if kind == "ring":
		# Count up once so a new score is noticed.
		var tween := create_tween()
		tween.tween_method(_set_shown, 0.0, float(value), 0.7).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)

func _set_shown(amount: float) -> void:
	_shown = amount
	queue_redraw()

func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()

func _draw() -> void:
	match kind:
		"icon":
			_draw_button_icon()
		"ring":
			_draw_ring()
		"dots":
			_draw_dots()
		"backdrop":
			_draw_backdrop()
		"badge":
			_draw_badge()

# ---------------------------------------------------------------- icons
func _draw_button_icon() -> void:
	var button := get_parent() as Button
	if button == null:
		return
	var font := button.get_theme_font("font")
	var font_size := button.get_theme_font_size("font_size")
	var text_width := font.get_string_size(button.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
	# Sit just left of the centred label.
	var center := Vector2((size.x - text_width) / 2.0 - 20.0, size.y / 2.0)
	draw_glyph(self, glyph, center, 10.0, color)

static func draw_glyph(canvas: CanvasItem, name: String, c: Vector2, r: float, col: Color) -> void:
	match name:
		"play":
			canvas.draw_colored_polygon(PackedVector2Array([c + Vector2(-r * 0.55, -r), c + Vector2(r, 0), c + Vector2(-r * 0.55, r)]), col)
		"stop":
			canvas.draw_rect(Rect2(c - Vector2(r, r) * 0.8, Vector2(r, r) * 1.6), col)
		"mic":
			canvas.draw_rect(Rect2(c + Vector2(-r * 0.38, -r), Vector2(r * 0.76, r * 1.25)), col)
			canvas.draw_circle(c + Vector2(0, -r), r * 0.38, col)
			canvas.draw_circle(c + Vector2(0, r * 0.25), r * 0.38, col)
			canvas.draw_arc(c + Vector2(0, 0.0), r * 0.72, 0.1, PI - 0.1, 16, col, 2.2, true)
			canvas.draw_line(c + Vector2(0, r * 0.72), c + Vector2(0, r * 1.05), col, 2.2, true)
		"check":
			canvas.draw_polyline(PackedVector2Array([c + Vector2(-r * 0.8, 0), c + Vector2(-r * 0.2, r * 0.6), c + Vector2(r * 0.9, -r * 0.6)]), col, 3.0, true)
		"cross":
			canvas.draw_line(c + Vector2(-r * 0.7, -r * 0.7), c + Vector2(r * 0.7, r * 0.7), col, 3.2, true)
			canvas.draw_line(c + Vector2(r * 0.7, -r * 0.7), c + Vector2(-r * 0.7, r * 0.7), col, 3.2, true)
		"next":
			canvas.draw_line(c + Vector2(-r * 0.8, 0), c + Vector2(r * 0.8, 0), col, 3.0, true)
			canvas.draw_polyline(PackedVector2Array([c + Vector2(r * 0.1, -r * 0.7), c + Vector2(r * 0.8, 0), c + Vector2(r * 0.1, r * 0.7)]), col, 3.0, true)
		"back":
			canvas.draw_line(c + Vector2(-r * 0.8, 0), c + Vector2(r * 0.8, 0), col, 2.5, true)
			canvas.draw_polyline(PackedVector2Array([c + Vector2(-r * 0.1, -r * 0.7), c + Vector2(-r * 0.8, 0), c + Vector2(-r * 0.1, r * 0.7)]), col, 2.5, true)
		"retry":
			canvas.draw_arc(c, r * 0.8, -PI * 0.15, PI * 1.45, 24, col, 2.6, true)
			var tip := c + Vector2(cos(-PI * 0.15), sin(-PI * 0.15)) * r * 0.8
			canvas.draw_colored_polygon(PackedVector2Array([tip + Vector2(-r * 0.45, -r * 0.1), tip + Vector2(r * 0.35, -r * 0.45), tip + Vector2(r * 0.2, r * 0.4)]), col)
		"headphones":
			canvas.draw_arc(c + Vector2(0, r * 0.1), r, PI, TAU, 24, col, r * 0.22, true)
			for side in [-1, 1]:
				canvas.draw_rect(Rect2(c + Vector2(side * r - r * 0.28, 0), Vector2(r * 0.56, r * 0.85)), col)

# ---------------------------------------------------------------- score ring
func _draw_ring() -> void:
	var c := size / 2.0
	var r := minf(size.x, size.y) / 2.0 - 8.0
	draw_arc(c, r, 0, TAU, 64, PALETTE.line, 12.0, true)
	var score := clampf(_shown, 0, 100)
	if score > 0:
		draw_arc(c, r, -PI / 2, -PI / 2 + TAU * score / 100.0, 64, accent, 12.0, true)
	var font := get_theme_default_font()
	var text := str(int(round(score)))
	var big := int(r * 0.78)
	var w := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, big).x
	draw_string(font, c + Vector2(-w / 2.0, big * 0.32), text, HORIZONTAL_ALIGNMENT_LEFT, -1, big, PALETTE.ink)
	var small := int(r * 0.26)
	var sw := font.get_string_size("of 100", HORIZONTAL_ALIGNMENT_LEFT, -1, small).x
	draw_string(font, c + Vector2(-sw / 2.0, big * 0.32 + small * 1.3), "of 100", HORIZONTAL_ALIGNMENT_LEFT, -1, small, PALETTE.muted)

# ---------------------------------------------------------------- level dots
func _draw_dots() -> void:
	var level := int(value)
	var gap := 7.0
	var d := minf(10.0, (size.x - gap * 9) / 10.0)
	var y := size.y / 2.0
	for i in 10:
		var x := size.x - (10 - i) * (d + gap) + gap + d / 2.0
		if i < level:
			draw_circle(Vector2(x, y), d / 2.0, accent)
		else:
			draw_circle(Vector2(x, y), d / 2.0, PALETTE.line)

# ---------------------------------------------------------------- letter badge (A, B, C…)
func _draw_badge() -> void:
	var r := 15.0
	var c := Vector2(30, size.y / 2.0)
	var selected := false
	if get_parent() is Button:
		selected = (get_parent() as Button).button_pressed
	draw_circle(c, r, accent if selected else PALETTE.bg)
	draw_arc(c, r, 0, TAU, 32, accent, 2.0, true)
	var font := get_theme_default_font()
	var text := str(value)
	var w := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 16).x
	draw_string(font, c + Vector2(-w / 2.0, 6), text, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, PALETTE.white if selected else accent)

# ---------------------------------------------------------------- page backdrop
func _draw_backdrop() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), PALETTE.bg)
	# Soft shapes in the three ContextSpeak colours; purely decorative.
	var blobs := [
		[Vector2(0.06, 0.08), 150.0, Color(PALETTE.blue, 0.10)],
		[Vector2(0.95, 0.18), 190.0, Color(PALETTE.green, 0.09)],
		[Vector2(0.88, 0.92), 160.0, Color(PALETTE.orange, 0.10)],
		[Vector2(0.10, 0.85), 120.0, Color(PALETTE.green, 0.07)],
	]
	for blob in blobs:
		draw_circle(Vector2(size.x * blob[0].x, size.y * blob[0].y), blob[1], blob[2])
	for i in 18:
		var p := Vector2(fposmod(i * 211.0, size.x), fposmod(i * 137.0 + 40, size.y))
		draw_circle(p, 2.5, Color(PALETTE.ink, 0.06))
