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
	var lifts: Array[float] = []
	var scales: Array[float] = []
	for path in _traffic_mesh_paths():
		var glb := Track._extract_mesh(String(path))
		if glb == null:
			continue
		var kit := glb.duplicate(true) as Mesh
		if kit == null:
			kit = glb
		_enable_instance_colors(kit)
		meshes.append(kit)
		var aabb := kit.get_aabb()
		var car_len := maxf(aabb.size.z, aabb.size.x)
		lifts.append(-aabb.position.y)
		# Kenney cars are ~2.5 m long; box fallbacks are already road-scale.
		scales.append(4.6 / maxf(car_len, 0.5) if car_len < 3.6 else 1.0)
	if meshes.size() < 3:
		meshes.append(_box_car(Vector3(1.85, 1.35, 4.4), Color(0.62, 0.16, 0.12)))
		lifts.append(0.7)
		scales.append(1.0)
		meshes.append(_box_car(Vector3(1.95, 1.85, 4.9), Color(0.18, 0.22, 0.38)))
		lifts.append(0.7)
		scales.append(1.0)
		meshes.append(_box_car(Vector3(2.05, 1.55, 5.4), Color(0.72, 0.72, 0.7)))
		lifts.append(0.7)
		scales.append(1.0)

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
			"color": Color(1, 1, 1).lerp(palette[i % palette.size()], 0.32),
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
		_batches.append({"mm": mm, "cars": group, "y_lift": lifts[mesh_i], "scale": scales[mesh_i]})


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
			var y_lift := float(batch.get("y_lift", 0.7))
			var car_scale := float(batch.get("scale", 1.0))
			var t := track.sample(s, float(car["lane"]), 0.0)
			if bool(car["oncoming"]):
				t.basis = t.basis.rotated(t.basis.y.normalized(), PI)
			t.basis = t.basis.scaled(Vector3(car_scale, car_scale, car_scale))
			t.origin += t.basis.y.normalized() * y_lift * car_scale
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


static func _traffic_mesh_paths() -> Array:
	return [
		"res://assets/models/kenney/car/car_sedan.glb",
		"res://assets/models/kenney/car/car_van.glb",
		"res://assets/models/kenney/car/car_suv.glb",
		"res://assets/models/kenney/car/car_taxi.glb",
		"res://assets/models/kenney/car/car_hatch.glb",
		"res://assets/models/kenney/car/car_police.glb",
		"res://assets/models/car.glb",
	]


static func _enable_instance_colors(mesh: Mesh) -> void:
	for i in mesh.get_surface_count():
		var mat := mesh.surface_get_material(i)
		var std := mat as StandardMaterial3D
		if std == null:
			continue
		var dup := std.duplicate() as StandardMaterial3D
		dup.vertex_color_use_as_albedo = true
		mesh.surface_set_material(i, dup)
