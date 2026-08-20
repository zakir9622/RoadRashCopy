class_name WeatherDirector
extends Node3D
## Camera-local rain and road wetness. Particles never span the whole track.

var raining: bool = false
var _player: Rider
var _camera: Camera3D
var _track: Track
var _particles: GPUParticles3D
var _splash: GPUParticles3D


func configure(track: Track, player: Rider, camera: Camera3D, biome: String) -> void:
	_track = track
	_player = player
	_camera = camera
	var gfx := get_node_or_null("/root/GraphicsSettings")
	var weather_off := gfx != null and gfx.weather_off()
	var high := gfx == null or gfx.is_high()
	var want_rain := false
	var wet := 0.0
	var drops := 0
	if (not weather_off) and high:
		match biome:
			"night":
				want_rain = true
				wet = 0.72
				drops = 900
			"city":
				want_rain = true
				wet = 0.38
				drops = 520
			"coast":
				want_rain = true
				wet = 0.22
				drops = 280
			"mountain":
				want_rain = false
				wet = 0.12
			_:
				want_rain = false
	raining = want_rain
	if _track != null:
		_track.set_wetness(wet if raining else (0.0 if biome == "desert" else wet * 0.25))
	_build_rain(raining, drops)
	if _particles != null:
		_particles.emitting = raining
		_particles.amount = maxi(drops, 1)
	if _splash != null:
		_splash.emitting = raining
	set_process(raining)


func _build_rain(enabled: bool, amount: int = 800) -> void:
	if _particles != null:
		return
	if not enabled:
		return
	_particles = GPUParticles3D.new()
	_particles.name = "Rain"
	_particles.amount = maxi(amount, 80)
	_particles.lifetime = 0.55
	_particles.visibility_aabb = AABB(Vector3(-18, -8, -18), Vector3(36, 28, 48))
	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0.12, -1.0, 0.08)
	mat.spread = 8.0
	mat.initial_velocity_min = 18.0
	mat.initial_velocity_max = 28.0
	mat.gravity = Vector3(0, -40, 0)
	mat.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	mat.emission_box_extents = Vector3(12, 8, 16)
	_particles.process_material = mat
	var draw := BoxMesh.new()
	draw.size = Vector3(0.03, 0.35, 0.03)
	var drop := StandardMaterial3D.new()
	drop.albedo_color = Color(0.75, 0.82, 0.95, 0.55)
	drop.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	drop.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	draw.material = drop
	_particles.draw_pass_1 = draw
	add_child(_particles)

	_splash = GPUParticles3D.new()
	_splash.name = "RainSplash"
	_splash.amount = 120
	_splash.lifetime = 0.28
	var splash_mat := ParticleProcessMaterial.new()
	splash_mat.direction = Vector3(0, 1, 0)
	splash_mat.spread = 50.0
	splash_mat.initial_velocity_min = 0.4
	splash_mat.initial_velocity_max = 1.6
	splash_mat.gravity = Vector3(0, -6, 0)
	splash_mat.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	splash_mat.emission_box_extents = Vector3(6, 0.05, 10)
	_splash.process_material = splash_mat
	var spark := SphereMesh.new()
	spark.radius = 0.04
	spark.height = 0.08
	var spark_mat := StandardMaterial3D.new()
	spark_mat.albedo_color = Color(0.85, 0.9, 1.0, 0.4)
	spark_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	spark_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	spark.material = spark_mat
	_splash.draw_pass_1 = spark
	add_child(_splash)


func _process(_delta: float) -> void:
	if _camera == null:
		return
	var origin := _camera.global_position + _camera.global_transform.basis.z * -10.0
	if _particles != null:
		_particles.global_position = origin + Vector3(0, 10, 0)
	if _splash != null and _player != null and _track != null:
		var road := _track.sample(_player.distance + 6.0, _player.lateral, 0.04)
		_splash.global_transform = road
