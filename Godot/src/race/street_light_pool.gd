class_name StreetLightPool
extends Node3D
## A handful of OmniLights that hop to the nearest streetlight anchors.
## Never one real light per pole.

var _anchors: Array[Vector3] = []
var _lights: Array[OmniLight3D] = []
var _player: Rider


func configure(anchors: Array, player: Rider, count: int) -> void:
	_player = player
	_anchors.clear()
	for a in anchors:
		if a is Vector3:
			_anchors.append(a)
	for light in _lights:
		light.queue_free()
	_lights.clear()
	for i in count:
		var omni := OmniLight3D.new()
		omni.omni_range = 16.0
		omni.light_energy = 1.6
		omni.light_color = Color(1.0, 0.82, 0.55)
		omni.shadow_enabled = false
		add_child(omni)
		_lights.append(omni)
	set_process(count > 0 and not _anchors.is_empty())


func _process(_delta: float) -> void:
	if _player == null or _lights.is_empty() or _anchors.is_empty():
		return
	var origin := _player.global_position
	var scored: Array = []
	for a in _anchors:
		scored.append({"p": a, "d": origin.distance_squared_to(a)})
	scored.sort_custom(func(x, y): return float(x["d"]) < float(y["d"]))
	for i in _lights.size():
		if i >= scored.size():
			_lights[i].visible = false
			continue
		_lights[i].visible = true
		_lights[i].global_position = scored[i]["p"] + Vector3(0, 6.4, 0)
