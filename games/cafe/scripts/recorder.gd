extends Node

signal recorded(audio: PackedByteArray)
signal failed(message: String)
signal activity(level: float, seconds: float)
var level := 0.0
var effect: AudioEffectCapture
var pcm := PackedByteArray()
var captured_bytes := 0
var microphone: AudioStreamPlayer
var deadline: Timer
var recording := false
var bus := -1
var web_microphone: JavaScriptObject
var web_callbacks: Array[JavaScriptObject] = []

func _ready() -> void:
	if OS.has_feature("web"):
		web_microphone = JavaScriptBridge.get_interface("window").contextSpeakMicrophone
		web_callbacks = [
			JavaScriptBridge.create_callback(web_ready),
			JavaScriptBridge.create_callback(web_activity),
			JavaScriptBridge.create_callback(web_finished),
			JavaScriptBridge.create_callback(web_failed)
		]
		return
	AudioServer.add_bus()
	bus = AudioServer.bus_count - 1
	AudioServer.set_bus_name(bus, "Cafe microphone")
	AudioServer.set_bus_mute(bus, true)
	effect = AudioEffectCapture.new()
	effect.buffer_length = 2.0
	AudioServer.add_bus_effect(bus, effect)
	microphone = AudioStreamPlayer.new()
	microphone.stream = AudioStreamMicrophone.new()
	microphone.playback_type = AudioServer.PLAYBACK_TYPE_STREAM
	microphone.bus = "Cafe microphone"
	add_child(microphone)
	deadline = Timer.new()
	deadline.one_shot = true
	deadline.wait_time = 20
	deadline.timeout.connect(stop)
	add_child(deadline)

func _process(_delta: float) -> void:
	if recording and not OS.has_feature("web"):
		drain()
		activity.emit(level, 20 - deadline.time_left)

func drain() -> void:
	# Poll captured frames; AudioEffectRecord's worker is unavailable in no-thread Web exports.
	var frames := effect.get_buffer(effect.get_frames_available())
	var limit := 20 * int(AudioServer.get_mix_rate()) * 2
	level = 0
	for frame in frames:
		level = maxf(level, minf(1, absf(frame.x + frame.y) * 2))
		if captured_bytes >= limit:
			break
		pcm.encode_s16(captured_bytes, int(clampf((frame.x + frame.y) * 0.5, -1.0, 1.0) * 32767))
		captured_bytes += 2

func start() -> void:
	if recording:
		return
	recording = true
	if OS.has_feature("web"):
		web_microphone.start(web_callbacks[0], web_callbacks[1], web_callbacks[2], web_callbacks[3])
		return
	# Reserve once: growing a PackedByteArray for every sample stalls Web audio.
	pcm.resize(20 * int(AudioServer.get_mix_rate()) * 2)
	captured_bytes = 0
	effect.clear_buffer()
	microphone.play()
	deadline.start()

func stop() -> void:
	if not recording:
		return
	recording = false
	if OS.has_feature("web"):
		web_microphone.stop()
		return
	deadline.stop()
	drain()
	var capture := AudioStreamWAV.new()
	capture.format = AudioStreamWAV.FORMAT_16_BITS
	capture.mix_rate = int(AudioServer.get_mix_rate())
	capture.stereo = false
	capture.data = pcm.slice(0, captured_bytes)
	microphone.stop()
	if capture.data.size() < 100:
		failed.emit("No recording captured. Allow microphone access, or type your reply.")
		return
	if not contains_speech(capture):
		failed.emit("No speech detected. Move closer to the microphone and try again, or type your reply.")
		return
	recorded.emit(wave(capture))

func web_ready(_args: Array) -> void:
	if recording:
		activity.emit(0, 0.6)

func web_activity(args: Array) -> void:
	if recording:
		activity.emit(float(args[0]), float(args[1]))

func web_finished(args: Array) -> void:
	recording = false
	var bytes := JavaScriptBridge.js_buffer_to_packed_byte_array(args[0])
	var capture := AudioStreamWAV.load_from_buffer(bytes)
	if capture == null or not contains_speech(capture):
		failed.emit("No speech detected. Move closer to the microphone and try again, or type your reply.")
		return
	recorded.emit(bytes)

func web_failed(args: Array) -> void:
	recording = false
	failed.emit(str(args[0]))

static func contains_speech(capture: AudioStreamWAV) -> bool:
	# A short audible interval prevents silent PCM from becoming an ASR hallucination.
	var audible := 0
	var samples := capture.data
	var needed := int(capture.mix_rate * 0.08) * (2 if capture.stereo else 1)
	for offset in range(0, samples.size() - 1, 2):
		if absi(samples.decode_s16(offset)) >= 300:
			audible += 1
			if audible >= needed:
				return true
	return false

static func wave(capture: AudioStreamWAV) -> PackedByteArray:
	# Cap delayed timer callbacks at 20 seconds and preserve complete PCM frames.
	var channels := 2 if capture.stereo else 1
	var frame_bytes := channels * 2
	var length := mini(capture.data.size(), 20 * capture.mix_rate * frame_bytes)
	length -= length % frame_bytes
	var audio := PackedByteArray()
	audio.resize(44)
	for part in [[0, "RIFF"], [8, "WAVEfmt "], [36, "data"]]:
		var text: PackedByteArray = part[1].to_ascii_buffer()
		for index in text.size():
			audio[part[0] + index] = text[index]
	audio.encode_u32(4, length + 36)
	audio.encode_u32(16, 16)
	audio.encode_u16(20, 1)
	audio.encode_u16(22, channels)
	audio.encode_u32(24, capture.mix_rate)
	audio.encode_u32(28, capture.mix_rate * frame_bytes)
	audio.encode_u16(32, frame_bytes)
	audio.encode_u16(34, 16)
	audio.encode_u32(40, length)
	audio.append_array(capture.data.slice(0, length))
	return audio

func cancel() -> void:
	recording = false
	if web_microphone:
		web_microphone.cancel()
	if deadline:
		deadline.stop()
	pcm.clear()
	if effect:
		effect.clear_buffer()
	if microphone:
		microphone.stop()

func _exit_tree() -> void:
	cancel()
	if bus >= 0 and bus < AudioServer.bus_count:
		AudioServer.remove_bus(bus)
