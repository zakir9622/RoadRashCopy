extends Node
## One place for every sound. Pools AudioStreamPlayers so playback never
## allocates mid-race, and survives scene changes as an autoload.

const POOL_SIZE := 12

var _pool: Array[AudioStreamPlayer] = []
var _music: AudioStreamPlayer
var _engine: AudioStreamPlayer
var _wind: AudioStreamPlayer
var _ambience: AudioStreamPlayer
var _streams: Dictionary = {}
var _ambience_key := ""


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

	_wind = AudioStreamPlayer.new()
	add_child(_wind)

	_ambience = AudioStreamPlayer.new()
	add_child(_ambience)

	# Headless runs (tests, CI) get silence: the dummy audio driver never reaps
	# its playback list, so any started stream reports as a leak at exit.
	if DisplayServer.get_name() == "headless":
		return

	for key in [
		"hit", "kick", "crash", "siren", "horn", "click", "engine", "music",
		"pickup", "go", "wind", "ambience_coast", "ambience_desert",
		"ambience_city", "ambience_mountain",
	]:
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
	for looped in [_music, _engine, _wind, _ambience]:
		if looped == null:
			continue
		looped.stop()
		looped.stream = null
	_ambience_key = ""
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
		_engine.play()
		if not _engine.finished.is_connected(_on_engine_finished):
			_engine.finished.connect(_on_engine_finished)
	elif not active and _engine.playing:
		_engine.stop()
	_engine.pitch_scale = 0.65 + clampf(speed01, 0.0, 1.0) * 1.25
	_engine.volume_db = -7.0 + clampf(speed01, 0.0, 1.0) * 4.0


func _on_engine_finished() -> void:
	_engine.play()


## Wind rush + biome bed. Night city shares the downtown rumble.
func set_world(biome: String, speed01: float, riding: bool) -> void:
	if DisplayServer.get_name() == "headless":
		return
	var bed := "ambience_city"
	match biome:
		"coast":
			bed = "ambience_coast"
		"desert":
			bed = "ambience_desert"
		"mountain":
			bed = "ambience_mountain"
		"city", "night":
			bed = "ambience_city"
	if _streams.has(bed) and _ambience_key != bed:
		_ambience_key = bed
		_ambience.stream = _streams[bed]
		_ambience.volume_db = -14.0
		_ambience.play()
		if not _ambience.finished.is_connected(_on_ambience_finished):
			_ambience.finished.connect(_on_ambience_finished)
	var want_wind := riding and speed01 > 0.04
	if _streams.has("wind"):
		if want_wind and not _wind.playing:
			_wind.stream = _streams["wind"]
			_wind.play()
			if not _wind.finished.is_connected(_on_wind_finished):
				_wind.finished.connect(_on_wind_finished)
		elif not want_wind and _wind.playing:
			_wind.stop()
		_wind.volume_db = -20.0 + clampf(speed01, 0.0, 1.0) * 12.0
		_wind.pitch_scale = 0.82 + clampf(speed01, 0.0, 1.0) * 0.45


func _on_ambience_finished() -> void:
	_ambience.play()


func _on_wind_finished() -> void:
	_wind.play()
