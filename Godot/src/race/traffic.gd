class_name Traffic
extends Node3D
## Civilian traffic: pooled MultiMesh cars (several kits) driving both
## directions. One draw call per kit; per-car state is plain data.

var track: Track
var cars: Array[Dictionary] = []
var _batches: Array = []
var _player: Rider
var _horn_cooldown: float = 0.0
var _rng := RandomNumberGenerator.new()


func build(p_track: Track, count: int, player: Rider, rng_seed: int = 99) -> void:
	track = p_track
	_player = player
	_rng.seed = rng_seed

	var meshes: Array[Mesh] = []
	var glb := Track._extract_mesh("res://assets/models/car.glb")
	if glb != null:
		meshes.append(glb)
	meshes.append(_box_car(Vector3(1.85, 1.35, 4.4), Color(0.62, 0.16, 0.12)))
	meshes.append(_box_car(Vector3(1.95, 1.85, 4.9), Color(0.18, 0.22, 0.38)))
	meshes.append(_box_car(Vector3(2.05, 1.55, 5.4), Color(0.72, 0.72, 0.7)))

	var palette := [
		Color(0.75, 0.73, 0.7), Color(0.2, 0.25, 0.55), Color(0.55, 0.12, 0.1),
		Color(0.15, 0.15, 0.17), Color(0.7, 0.55, 0.15), Color(0.3, 0.45, 0.3),
		Color(0.82, 0.78, 0.55), Color(0.12, 0.14, 0.16),
	]
	var groups: Array = []
	for _m in meshes.size():
		groups.append([])
	var spacing := track.length / float(maxi(count, 1))
	for i in count:
		var oncoming := _rng.randf() < 0.5
		var lane := (track.half_width * 0.55) * (-1.0 if oncoming else 1.0)
		if _rng.randf() < 0.35:
			lane *= 0.42
		var mesh_i := i % meshes.size()
		var car := {
			"s": spacing * i + _rng.randf_range(-10.0, 10.0),
			"lane": lane,
			"lane_target": lane,
			"speed": _rng.randf_range(10.0, 22.0),
			"oncoming": oncoming,
			"mesh": mesh_i,
			"local": groups[mesh_i].size(),
			"color": palette[i % palette.size()],
			"lane_cd": _rng.randf_range(6.0, 18.0),
		}
		cars.append(car)
		groups[mesh_i].append(car)

	_batches.clear()
	for mesh_i in meshes.size():
		var group: Array = groups[mesh_i]
		if group.is_empty():
			continue
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.use_colors = true
		mm.mesh = meshes[mesh_i]
		mm.instance_count = group.size()
		for j in group.size():
			mm.set_instance_color(j, group[j]["color"])
		var inst := MultiMeshInstance3D.new()
		inst.name = "Traffic_%d" % mesh_i
		inst.multimesh = mm
		add_child(inst)
		_batches.append({"mm": mm, "cars": group})


static func _box_car(size: Vector3, colour: Color) -> Mesh:
	var box := BoxMesh.new()
	box.size = size
	var mat := StandardMaterial3D.new()
	mat.albedo_color = colour
	mat.metallic = 0.35
	mat.roughness = 0.42
	mat.vertex_color_use_as_albedo = true
	box.material = mat
	return box


func step(delta: float) -> void:
	_horn_cooldown = maxf(_horn_cooldown - delta, 0.0)
	for batch in _batches:
		var mm: MultiMesh = batch["mm"]
		var group: Array = batch["cars"]
		for j in group.size():
			var car: Dictionary = group[j]
			_step_car(car, delta)
			var s := float(car["s"])
			var t := track.sample(s, float(car["lane"]), 0.7)
			if bool(car["oncoming"]):
				t.basis = t.basis.rotated(t.basis.y.normalized(), PI)
			mm.set_instance_transform(j, t)
			var colour: Color = car["color"]
			if _braking(car):
				colour = colour.lerp(Color(0.95, 0.12, 0.08), 0.72)
			mm.set_instance_color(j, colour)


func _step_car(car: Dictionary, delta: float) -> void:
	var direction := -1.0 if bool(car["oncoming"]) else 1.0
	var s := float(car["s"]) + float(car["speed"]) * direction * delta
	if s > track.length:
		s -= track.length
	elif s < 0.0:
		s += track.length
	car["s"] = s
	car["lane_cd"] = float(car["lane_cd"]) - delta
	if float(car["lane_cd"]) <= 0.0:
		car["lane_cd"] = _rng.randf_range(10.0, 24.0)
		if _rng.randf() < 0.18:
			var sign_lane := -1.0 if bool(car["oncoming"]) else 1.0
			var outer := track.half_width * 0.55 * sign_lane
			var inner := track.half_width * 0.22 * sign_lane
			car["lane_target"] = inner if absf(float(car["lane"]) - outer) < 0.8 else outer
	car["lane"] = lerpf(float(car["lane"]), float(car["lane_target"]), clampf(delta * 0.9, 0.0, 1.0))

	if _player != null and _horn_cooldown <= 0.0:
		var gap_s: float = absf(s - _player.distance)
		var gap_x: float = absf(float(car["lane"]) - _player.lateral)
		if gap_s < 6.0 and gap_x < 2.2:
			_horn_cooldown = 3.0
			Sfx.play("horn", -10.0)


func _braking(car: Dictionary) -> bool:
	if _player == null:
		return false
	var ahead := float(car["s"]) - _player.distance
	if bool(car["oncoming"]):
		return false
	return ahead > 0.0 and ahead < 28.0 and _player.speed > float(car["speed"]) + 6.0
