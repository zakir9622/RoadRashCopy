class_name Traffic
extends Node3D
## Civilian traffic: pooled MultiMesh cars driving both directions in fixed
## lanes. One draw call for every car on the road; per-car state is plain data.

var track: Track
var cars: Array[Dictionary] = []
var _mm: MultiMesh
var _player: Rider
var _horn_cooldown: float = 0.0


func build(p_track: Track, count: int, player: Rider, rng_seed: int = 99) -> void:
	track = p_track
	_player = player
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed

	var mesh := Track._extract_mesh("res://assets/models/car.glb")
	if mesh == null:
		var box := BoxMesh.new()
		box.size = Vector3(1.9, 1.4, 4.4)
		var mat := StandardMaterial3D.new()
		mat.albedo_color = Color(0.6, 0.15, 0.13)
		box.material = mat
		mesh = box

	_mm = MultiMesh.new()
	_mm.transform_format = MultiMesh.TRANSFORM_3D
	_mm.use_colors = true
	_mm.mesh = mesh
	_mm.instance_count = count
	var inst := MultiMeshInstance3D.new()
	inst.multimesh = _mm
	add_child(inst)

	var palette := [
		Color(0.75, 0.73, 0.7), Color(0.2, 0.25, 0.55), Color(0.55, 0.12, 0.1),
		Color(0.15, 0.15, 0.17), Color(0.7, 0.55, 0.15), Color(0.3, 0.45, 0.3),
	]
	var spacing := track.length / float(maxi(count, 1))
	for i in count:
		var oncoming := rng.randf() < 0.5
		cars.append({
			"s": spacing * i + rng.randf_range(-10.0, 10.0),
			"lane": (track.half_width * 0.55) * (-1.0 if oncoming else 1.0),
			"speed": rng.randf_range(11.0, 19.0),
			"oncoming": oncoming,
		})
		_mm.set_instance_color(i, palette[i % palette.size()])


func step(delta: float) -> void:
	_horn_cooldown = maxf(_horn_cooldown - delta, 0.0)
	for i in cars.size():
		var car := cars[i]
		var direction := -1.0 if bool(car["oncoming"]) else 1.0
		var s := float(car["s"]) + float(car["speed"]) * direction * delta
		# Wrap so the road never empties out over a long race.
		if s > track.length:
			s -= track.length
		elif s < 0.0:
			s += track.length
		car["s"] = s

		var t := track.sample(s, float(car["lane"]), 0.7)
		if bool(car["oncoming"]):
			t.basis = t.basis.rotated(t.basis.y.normalized(), PI)
		_mm.set_instance_transform(i, t)

		# Near-miss horn: close pass on the player, capped so it never spams.
		if _player != null and _horn_cooldown <= 0.0:
			var gap_s: float = absf(s - _player.distance)
			var gap_x: float = absf(float(car["lane"]) - _player.lateral)
			if gap_s < 6.0 and gap_x < 2.2:
				_horn_cooldown = 3.0
				Sfx.play("horn", -10.0)

		# Collision with any rider is handled by RaceManager reading `cars`.
