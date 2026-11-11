extends RefCounted

# CSS pixels keep text readable when the browser canvas uses a Retina buffer.
var viewport: Window
var browser: JavaScriptObject
var resized: JavaScriptObject

func setup(window: Window) -> void:
	viewport = window
	if not OS.has_feature("web"):
		return
	browser = JavaScriptBridge.get_interface("window")
	resized = JavaScriptBridge.create_callback(on_resize)
	browser.addEventListener("resize", resized)
	on_resize([])

func on_resize(_args: Array) -> void:
	if browser != null and viewport != null:
		fit(Vector2i(int(browser.innerWidth), int(browser.innerHeight)))

func fit(size: Vector2i) -> void:
	viewport.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_IGNORE
	viewport.content_scale_size = Vector2i(maxi(360, size.x), maxi(600, size.y))

func dispose() -> void:
	if browser != null:
		browser.removeEventListener("resize", resized)
	browser = null
	resized = null
	viewport = null
