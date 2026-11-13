extends Control
## The listening/speaking hero banner and picture cards from the shared library.

const UI = preload("res://ui_art.gd")
const GREEN := Color("17694f")
const GREEN_SOFT := Color("e3f0e8")
const BLUE := Color("2f55c4")
const BLUE_SOFT := Color("e8eefc")

var kind := "listen"
## True while audio plays or the microphone records; animates the waveform.
var active := false
var _time := 0.0

func _process(delta: float) -> void:
	if active and kind in ["listen", "voice"]:
		_time += delta
		queue_redraw()

func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()

func _draw() -> void:
	if kind in ["listen", "voice"]:
		_draw_hero()
		return
	_draw_library_picture()

# ---------------------------------------------------------------- hero banner
func _draw_hero() -> void:
	var accent := BLUE if kind == "listen" else GREEN
	var soft := BLUE_SOFT if kind == "listen" else GREEN_SOFT
	var rect := Rect2(Vector2.ZERO, size)
	var style := StyleBoxFlat.new()
	style.bg_color = soft
	style.set_corner_radius_all(20)
	draw_style_box(style, rect)
	# Icon disc
	var r := minf(size.y * 0.32, 34.0)
	var disc := Vector2(size.y * 0.55 + 6, size.y / 2)
	draw_circle(disc, r + 6, Color(accent, 0.15))
	draw_circle(disc, r, accent)
	UI.draw_glyph(self, "headphones" if kind == "listen" else "mic", disc, r * 0.5, Color.WHITE)
	# Waveform: calm when idle, moving while active.
	var left := disc.x + r + 28
	var right := size.x - 28
	var bars := int(clampf((right - left) / 14.0, 6, 48))
	var step := (right - left) / bars
	for i in bars:
		var phase := i * 0.55 + _time * 7.0
		var amp := 0.25 + 0.75 * absf(sin(i * 0.47) * cos(i * 0.21 + 0.6))
		if active:
			amp = clampf(amp * (0.55 + 0.45 * sin(phase)), 0.12, 1.0)
		var h := (size.y * 0.5) * amp
		var x := left + i * step + step / 2
		var col := accent if active or i % 3 != 2 else Color(accent, 0.55)
		draw_line(Vector2(x, size.y / 2 - h / 2), Vector2(x, size.y / 2 + h / 2), col, maxf(3.0, step * 0.45), true)

# ---------------------------------------------------------------- choice pictures
# Pictures come from the shared SVG library (pictures.json, generated from the
# website's contracts), rasterised once per id and cached.
static var _library: Dictionary = {}
static var _textures: Dictionary = {}

static func texture_for(id: String) -> Texture2D:
	if _textures.has(id):
		return _textures[id]
	if _library.is_empty():
		var file := FileAccess.open("res://pictures.json", FileAccess.READ)
		if file:
			var parsed = JSON.parse_string(file.get_as_text())
			if parsed is Dictionary:
				_library = parsed
	var entry = _library.get(id)
	if not entry is Dictionary:
		return null
	var image := Image.new()
	if image.load_svg_from_string(str(entry.get("svg", "")), 3.0) != OK:
		return null
	var texture := ImageTexture.create_from_image(image)
	_textures[id] = texture
	return texture

func _draw_library_picture() -> void:
	var texture := texture_for(kind)
	if texture == null:
		# Unknown picture: a neutral placeholder rather than an empty card.
		draw_rect(Rect2(Vector2.ZERO, size), Color("eef2ee"))
		return
	var ratio := 200.0 / 120.0
	var w := minf(size.x, size.y * ratio)
	var h := w / ratio
	draw_texture_rect(texture, Rect2((size - Vector2(w, h)) / 2, Vector2(w, h)), false)
