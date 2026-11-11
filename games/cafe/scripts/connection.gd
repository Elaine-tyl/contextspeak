extends Node

const BASE := "http://127.0.0.1:4320"
var token := ""
var requests: Array[HTTPRequest] = []

func connect_local() -> bool:
	var response: Dictionary = await call_api("/session", null)
	if response.ok and response.data is Dictionary:
		token = str(response.data.get("token", ""))
	return token != ""

func call_api(path: String, payload: Variant, raw := false, audio := false) -> Dictionary:
	var request := HTTPRequest.new()
	request.timeout = 90 if audio else 35
	request.body_size_limit = 3200000
	add_child(request)
	requests.append(request)
	var headers: PackedStringArray = ["X-ContextSpeak-Client: " + ("web" if OS.has_feature("web") else "native")]
	if token != "":
		headers.append("Authorization: Bearer " + token)
	var started: Error
	if payload == null:
		started = request.request(BASE + path, headers, HTTPClient.METHOD_GET)
	elif raw:
		headers.append("Content-Type: audio/wav")
		started = request.request_raw(BASE + path, headers, HTTPClient.METHOD_POST, payload)
	else:
		headers.append("Content-Type: application/json")
		started = request.request(BASE + path, headers, HTTPClient.METHOD_POST, JSON.stringify(payload))
	if started != OK:
		requests.erase(request)
		request.queue_free()
		return {"ok": false, "data": null, "error": "Start the local companion: pnpm world:dev. You can still type simple replies."}
	var response: Array = await request.request_completed
	requests.erase(request)
	request.queue_free()
	if response[0] != HTTPRequest.RESULT_SUCCESS:
		return {"ok": false, "data": null, "error": "Local connection unavailable. Your mission is kept; try again or type your reply."}
	var bytes: PackedByteArray = response[3]
	var data: Variant = bytes if audio and response[1] == 200 else JSON.parse_string(bytes.get_string_from_utf8())
	return {"ok": response[1] == 200, "data": data, "error": data.get("error", "Please retry your reply.") if data is Dictionary else "Please retry your reply."}

func cancel() -> void:
	# HTTPRequest cancellation alone does not resolve an awaiting signal.
	for request in requests.duplicate():
		request.cancel_request()
		request.request_completed.emit(HTTPRequest.RESULT_REQUEST_FAILED, 0, PackedStringArray(), PackedByteArray())
