extends Node
## One place for every sound. Pools AudioStreamPlayers so playback never
## allocates mid-race, and survives scene changes as an autoload.

const POOL_SIZE := 12

var _pool: Array[AudioStreamPlayer] = []
var _music: AudioStreamPlayer
var _engine: AudioStreamPlayer
var _streams: Dictionary = {}


func _ready() -> void:
	for i in POOL_SIZE:
		var player := AudioStreamPlayer.new()
		player.bus = "Master"
		add_child(player)
		_pool.append(player)

	_music = AudioStreamPlayer.new()
	add_child(_music)

	_engine = AudioStreamPlayer.new()
	add_child(_engine)

	# Headless runs (tests, CI) get silence: the dummy audio driver never reaps
	# its playback list, so any started stream reports as a leak at exit.
	if DisplayServer.get_name() == "headless":
		return

	for key in ["hit", "kick", "crash", "siren", "horn", "click", "engine", "music", "pickup", "go"]:
		var path := "res://assets/audio/%s.wav" % key
		if ResourceLoader.exists(path):
			_streams[key] = load(path)


## Volume settings come from the save, looked up through the tree rather than
## the GameState identifier so this script also compiles under --script tests.
func _setting(key: String, fallback: float) -> float:
	var state := get_node_or_null("/root/GameState")
	if state == null:
		return fallback
	return clampf(float(state.save.get(key, fallback)), 0.01, 1.0)


## Releases every playback and cached stream. Called on exit (and by the smoke
## harness before quit) so nothing is still registered with the AudioServer
## when it tears down — playing streams otherwise leak at exit.
func stop_all() -> void:
	for player in _pool:
		player.stop()
		player.stream = null
	_music.stop()
	_music.stream = null
	_engine.stop()
	_engine.stream = null
	_streams.clear()


func _exit_tree() -> void:
	stop_all()


func play(key: String, volume_db: float = 0.0, pitch: float = 1.0) -> void:
	if not _streams.has(key):
		return
	for player in _pool:
		if not player.playing:
			player.stream = _streams[key]
			player.volume_db = volume_db + linear_to_db(_setting("sfx_volume", 1.0))
			player.pitch_scale = pitch
			player.play()
			return


func play_music() -> void:
	if not _streams.has("music") or _music.playing:
		return
	_music.stream = _streams["music"]
	_music.volume_db = linear_to_db(_setting("music_volume", 0.8)) - 6.0
	_music.play()
	# Loop by restarting; the generated WAV is seamless.
	if not _music.finished.is_connected(_on_music_finished):
		_music.finished.connect(_on_music_finished)


func _on_music_finished() -> void:
	_music.play()


func stop_music() -> void:
	_music.stop()


## Engine loop pitched by normalized speed — the classic cheap trick that works.
func set_engine(speed01: float, active: bool) -> void:
	if not _streams.has("engine"):
		return
	if active and not _engine.playing:
		_engine.stream = _streams["engine"]
		_engine.volume_db = -14.0
		_engine.play()
		if not _engine.finished.is_connected(_on_engine_finished):
			_engine.finished.connect(_on_engine_finished)
	elif not active and _engine.playing:
		_engine.stop()
	_engine.pitch_scale = 0.7 + clampf(speed01, 0.0, 1.0) * 1.1


func _on_engine_finished() -> void:
	_engine.play()
