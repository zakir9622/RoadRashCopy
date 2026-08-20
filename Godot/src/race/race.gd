extends Node3D
## Race scene root: builds the world from RaceContext, spawns the grid, wires
## the camera, environment, HUD and manager. Everything procedural — the scene
## file itself is nearly empty, so there is nothing to desync from code.

const RIDER_MODEL := "res://assets/models/bike.glb"
const RAT_MODEL := "res://assets/models/bike_rat.glb"
const COP_MODEL := "res://assets/models/cop_bike.glb"
const KAMI_MODEL := "res://assets/models/bike_kami.glb"
const SUPER_MODEL := "res://assets/models/bike_super.glb"

var manager: RaceManager
var track: Track
var player: Rider
var camera: Camera3D
var hud: CanvasLayer
var _biome := "coast"

var _fov_base := 62.0
var _cam_back := 2.4
var _cam_up := 1.35
var _cam_look := 8.0
var _cockpit: Node3D


## Autoloads fetched by tree path, not identifier: identifier globals bind to
## phantom instances under --script test harnesses, silently ignoring test
## setup. The path is identical in game mode and under tests.
func _context() -> Node:
	return get_node_or_null("/root/RaceContext")


func _state() -> Node:
	return get_node_or_null("/root/GameState")


func _ready() -> void:
	if _context() == null or _state() == null:
		push_error("Race: RaceContext and GameState autoloads are required")
		return
	var definition: Dictionary = _context().track()
	_biome = String(definition.get("biome", "coast"))

	track = Track.new()
	track.name = "Track"
	add_child(track)
	track.build(definition)

	_build_environment(definition)
	_spawn_grid(definition)
	_build_camera()
	_build_hud()

	manager = RaceManager.new()
	manager.name = "RaceManager"
	add_child(manager)

	var rival_ais := []
	var police_ais := []
	for child_ai in _rival_ais:
		rival_ais.append(child_ai)
	for child_ai in _police_ais:
		police_ais.append(child_ai)

	var traffic := Traffic.new()
	traffic.name = "Traffic"
	add_child(traffic)
	traffic.build(track, int(definition["traffic"]), player)

	manager.configure(track, player, rival_ais, police_ais, traffic, int(definition["police"]))
	manager.spawn_police_behind = _spawn_police_unit
	manager.finished.connect(_on_finished)
	manager.start()

	Sfx.play_music()


var _rival_ais: Array = []
var _police_ais: Array = []


func _spawn_grid(definition: Dictionary) -> void:
	var spec: Dictionary = _state().current_bike_spec()
	var rasher := "Rasher"
	if _state().has_method("player_display_name"):
		rasher = String(_state().call("player_display_name"))
	# Classic Road Rash: you start at the BACK of a tight 15-bike pack.
	player = _make_rider(rasher, 14.0, 0.0, Color(0.9, 0.35, 0.1))
	player.is_player = true
	player.rider_id = "player"
	player.top_speed = float(spec["top_speed"])
	player.accel = float(spec["accel"])
	player.handling = float(spec["handling"])
	player.nitro_boost = float(spec["nitro"])

	var controller := PlayerController.new()
	controller.name = "PlayerController"
	controller.rider = player
	controller.get_opponents = func(): return manager.riders if manager else []
	add_child(controller)

	var rival_count: int = 0 if _context().has_method("is_time_trial") and _context().is_time_trial() \
		else Campaign.RIVAL_COUNT
	var scale: float = float(_context().division_scale)
	for i in rival_count:
		var profile: Dictionary = Campaign.ROSTER[i]
		var lateral := lerpf(-track.half_width * 0.72, track.half_width * 0.72,
			float(i) / maxf(float(rival_count - 1), 1.0))
		var stagger := 18.0 + (i % 7) * 2.6 + (i / 7) * 1.8
		var gang := String(profile.get("gang", "Desades"))
		var suit: Color = Campaign.GANG_COLORS.get(gang, Color(0.5, 0.5, 0.5))
		if Story.is_hostile(_state().save, String(profile["id"])) and String(profile["id"]) == "natasha":
			profile = profile.duplicate()
			profile["aggression"] = 1.8
		var rival := _make_rider(String(profile["name"]), stagger, lateral, suit, false, profile)
		rival.rider_id = String(profile["id"])
		rival.gang = gang
		rival.suit_color = suit
		rival.body_color = suit.lightened(0.15)
		rival.top_speed = (44.0 + float(profile["skill"]) * 18.0) * scale
		rival.accel = (10.0 + float(profile["skill"]) * 6.0) * scale
		rival.weapon = int(profile["weapon"])
		var agr := float(profile["aggression"])
		if Story.is_hostile(_state().save, rival.rider_id):
			agr += 0.35
		_rival_ais.append(RivalAI.new(rival, float(profile["skill"]) * scale, agr, 1000 + i))

	# One officer starts behind the grid; heat spawns reinforcements from the rear.
	if int(definition["police"]) > 0:
		_spawn_police_unit(-22.0, track.half_width * 0.55)

func _spawn_police_unit(offset_behind: float = -18.0, lateral: float = 0.0) -> void:
	var start_s := maxf(player.distance + offset_behind, 0.0)
	var cop := _make_rider("Officer", start_s, lateral, Color(0.12, 0.22, 0.88), true)
	cop.is_police = true
	cop.top_speed = player.top_speed * 1.05 + 4.0
	cop.accel = player.accel * 1.1
	var ai := PoliceAI.new(cop)
	_police_ais.append(ai)
	if manager != null:
		manager.police_ais = _police_ais
		manager.riders = manager.racers.duplicate()
		for p in _police_ais:
			manager.riders.append(p.rider)


func _make_rider(rider_name: String, start_s: float, start_x: float,
		colour: Color, cop: bool = false, profile: Dictionary = {}) -> Rider:
	var rider := Rider.new()
	rider.name = rider_name.replace(" ", "_")
	rider.rider_name = rider_name
	rider.body_color = colour
	rider.suit_color = colour.darkened(0.15)
	if not profile.is_empty():
		rider.rider_id = String(profile.get("id", rider_name.to_lower()))
		rider.gang = String(profile.get("gang", ""))
	add_child(rider)
	rider.setup(track, start_s, start_x)
	rider.visual = _make_bike_visual(colour, cop, rider)
	rider.add_child(rider.visual)
	rider.bind_visual(rider.visual)
	return rider


func _bike_model_path(bike_id: String, cop: bool) -> String:
	if cop:
		return COP_MODEL
	match bike_id:
		"sport":
			return RIDER_MODEL
		"kami":
			return KAMI_MODEL if ResourceLoader.exists(KAMI_MODEL) else RIDER_MODEL
		"super":
			return SUPER_MODEL if ResourceLoader.exists(SUPER_MODEL) else RIDER_MODEL
		_:
			return RAT_MODEL


func _make_bike_visual(colour: Color, cop: bool, rider: Rider) -> Node3D:
	var bike_id := "sport"
	if cop:
		bike_id = "cop"
	elif rider != null and rider.is_player:
		var state := _state()
		if state != null:
			bike_id = String(state.save.get("bike", "rat"))
	elif rider != null and not rider.is_player:
		if rider.top_speed > 55.0:
			bike_id = "super"
		elif rider.top_speed > 48.0:
			bike_id = "kami"
		else:
			bike_id = "sport"
	var path := _bike_model_path(bike_id, cop)
	if ResourceLoader.exists(path):
		var scene: PackedScene = load(path)
		if scene != null:
			var model := scene.instantiate() as Node3D
			if model != null:
				model.scale = Vector3(1.0, 1.0, 1.0)
				_tint_model(model, colour)
				if rider != null and rider.is_player:
					_hide_helmet_from_cockpit(model)
				return model
	# Fallback silhouette: body box + wheels, still clearly a bike.
	var root := Node3D.new()
	var body := MeshInstance3D.new()
	var body_mesh := BoxMesh.new()
	body_mesh.size = Vector3(0.4, 0.6, 2.0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = colour
	mat.metallic = 0.6
	mat.roughness = 0.35
	body_mesh.material = mat
	body.mesh = body_mesh
	body.position.y = 0.7
	root.add_child(body)
	for z in [-0.75, 0.75]:
		var wheel := MeshInstance3D.new()
		var wheel_mesh := CylinderMesh.new()
		wheel_mesh.top_radius = 0.32
		wheel_mesh.bottom_radius = 0.32
		wheel_mesh.height = 0.12
		var dark := StandardMaterial3D.new()
		dark.albedo_color = Color(0.08, 0.08, 0.09)
		wheel_mesh.material = dark
		wheel.mesh = wheel_mesh
		wheel.rotation.z = PI / 2.0
		wheel.position = Vector3(0.0, 0.32, z)
		root.add_child(wheel)
	return root


static func _tint_model(model: Node3D, colour: Color) -> void:
	for child in model.find_children("*", "MeshInstance3D", true, false):
		var mesh_child := child as MeshInstance3D
		if mesh_child.mesh == null:
			continue
		for surface in mesh_child.mesh.get_surface_count():
			var mat := mesh_child.mesh.surface_get_material(surface)
			var std := mat as StandardMaterial3D
			if std == null:
				continue
			# Tintable panels are the generator's "body" material, marked
			# bright red as a fallback signature.
			var by_name := std.resource_name.begins_with("body")
			var by_color := std.albedo_color.r > 0.85 and std.albedo_color.g < 0.3
			if by_name or by_color:
				var tinted := std.duplicate() as StandardMaterial3D
				tinted.albedo_color = colour
				mesh_child.set_surface_override_material(surface, tinted)


func _build_environment(definition: Dictionary) -> void:
	var env := Environment.new()
	var night := bool(definition.get("night", false))

	var sky_path := "res://assets/sky/dikhololo_night_hdri.hdr" if night \
		else "res://assets/sky/kloppenheim_02_puresky_hdri.hdr"
	if ResourceLoader.exists(sky_path):
		var sky_mat := PanoramaSkyMaterial.new()
		sky_mat.panorama = load(sky_path)
		var sky := Sky.new()
		sky.sky_material = sky_mat
		env.background_mode = Environment.BG_SKY
		env.sky = sky
		env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
		env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	else:
		env.background_mode = Environment.BG_COLOR
		env.background_color = Color(0.04, 0.05, 0.09) if night else Color(0.45, 0.65, 0.85)
		env.ambient_light_color = Color(0.5, 0.55, 0.65)
		env.ambient_light_energy = 1.0

	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_exposure = 1.0 if not night else 1.3
	env.glow_enabled = true
	env.glow_intensity = 0.5
	env.glow_bloom = 0.1
	env.fog_enabled = true
	env.fog_sky_affect = 0.0
	var biome := String(definition.get("biome", "coast"))
	match biome:
		"desert":
			env.fog_light_color = Color(0.92, 0.78, 0.52)
			env.fog_density = 0.0024
		"coast":
			env.fog_light_color = Color(0.70, 0.82, 0.90)
			env.fog_density = 0.0018
		"city":
			env.fog_light_color = Color(0.62, 0.66, 0.72)
			env.fog_density = 0.0017
		"mountain":
			env.fog_light_color = Color(0.68, 0.76, 0.82)
			env.fog_density = 0.0026
		_:
			env.fog_light_color = Color(0.05, 0.07, 0.12) if night else Color(0.75, 0.8, 0.88)
			env.fog_density = 0.0035 if night else 0.0012

	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-38.0, 40.0, 0.0)
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 220.0
	match biome:
		"desert":
			sun.light_energy = 1.45
			sun.light_color = Color(1.0, 0.88, 0.62)
			sun.rotation_degrees = Vector3(-52.0, 28.0, 0.0)
		"coast":
			sun.light_energy = 1.15
			sun.light_color = Color(0.95, 0.97, 1.0)
		"city":
			sun.light_energy = 1.05
			sun.light_color = Color(0.96, 0.96, 1.0)
		"mountain":
			sun.light_energy = 1.1
			sun.light_color = Color(0.92, 0.95, 1.0)
			sun.rotation_degrees = Vector3(-28.0, 50.0, 0.0)
		_:
			sun.light_energy = 0.15 if night else 1.2
			sun.light_color = Color(0.7, 0.75, 1.0) if night else Color(1.0, 0.95, 0.85)
	add_child(sun)


func _hide_helmet_from_cockpit(visual: Node3D) -> void:
	# Cockpit sits in front of the visor; keep the head out of the lens.
	for n in ["helmet", "mesh_head", "visor"]:
		var node := visual.find_child(n, true, false)
		if node is VisualInstance3D:
			(node as VisualInstance3D).layers = 2


func _build_camera() -> void:
	camera = Camera3D.new()
	camera.fov = _fov_base
	camera.near = 0.12
	camera.far = 900.0
	camera.cull_mask = camera.cull_mask & ~2
	add_child(camera)
	camera.make_current()
	if player != null and player.visual != null:
		_cockpit = player.visual.find_child("cockpit_cam", true, false) as Node3D
	_snap_camera()


func _snap_camera() -> void:
	_update_camera(1.0)


func _update_camera(delta: float) -> void:
	if player == null or track == null or camera == null:
		return
	if player.state == Rider.State.RIDING or player.state == Rider.State.REMOUNT \
			or player.is_airborne():
		_cockpit_camera(delta)
	else:
		_chase_camera(delta)


func _cockpit_camera(delta: float) -> void:
	if _cockpit == null and player.visual != null:
		_cockpit = player.visual.find_child("cockpit_cam", true, false) as Node3D
	var origin: Vector3
	if _cockpit != null:
		origin = _cockpit.global_position
	else:
		origin = track.sample(player.distance + 0.28, player.lateral, 1.18 + player.air_height).origin
	var look := track.sample(player.distance + 16.0, player.lateral * 0.10, 0.62 + player.air_height * 0.25)
	if delta >= 0.99 or camera.global_position.distance_to(origin) > 8.0:
		camera.global_position = origin
	else:
		camera.global_position = camera.global_position.lerp(origin, 1.0 - exp(-16.0 * delta))
	View.look_at(camera, look.origin)
	camera.rotate_object_local(Vector3.FORWARD, player.lean * 1.05)
	var speed01 := clampf(player.speed / maxf(player.top_speed, 1.0), 0.0, 1.2)
	camera.fov = lerpf(camera.fov, _fov_base + speed01 * 12.0, 7.0 * delta)


func _chase_camera(delta: float) -> void:
	var behind := track.sample(maxf(player.distance - _cam_back, 0.0), player.lateral * 0.4, _cam_up)
	var look := track.sample(player.distance + 4.0, player.lateral * 0.2, 0.55)
	if delta >= 0.99 or camera.global_position.distance_to(behind.origin) > 10.0:
		camera.global_position = behind.origin
	else:
		camera.global_position = camera.global_position.lerp(behind.origin, 1.0 - exp(-10.0 * delta))
	View.look_at(camera, look.origin)


func _build_hud() -> void:
	hud = load("res://src/ui/Hud.tscn").instantiate()
	add_child(hud)
	hud.name = "Hud"
	hud.call_deferred("bind", self)


func _process(delta: float) -> void:
	if player == null or manager == null or camera == null:
		return
	_update_camera(delta)
	var riding := manager.phase == RaceManager.Phase.RACING and player.state == Rider.State.RIDING
	var speed01 := player.speed / maxf(player.top_speed, 1.0)
	Sfx.set_engine(speed01, riding)
	Sfx.set_world(_biome, speed01, riding)


func _unhandled_input(event: InputEvent) -> void:
	if manager == null:
		return
	if event.is_action_pressed("pause") and manager.phase != RaceManager.Phase.FINISHED:
		_toggle_pause()


var _pause_layer: CanvasLayer


func _toggle_pause() -> void:
	if _pause_layer != null:
		_pause_layer.queue_free()
		_pause_layer = null
		get_tree().paused = false
		return

	get_tree().paused = true
	_pause_layer = CanvasLayer.new()
	_pause_layer.layer = 20
	# The pause menu must keep processing while the tree is paused.
	_pause_layer.process_mode = Node.PROCESS_MODE_ALWAYS
	add_child(_pause_layer)

	var scrim := ColorRect.new()
	scrim.color = Color(0, 0, 0, 0.7)
	scrim.set_anchors_preset(Control.PRESET_FULL_RECT)
	_pause_layer.add_child(scrim)

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 12)
	ThemeColors.center_wrap(scrim).add_child(box)

	var title := Label.new()
	title.text = "PAUSED"
	title.add_theme_font_size_override("font_size", 44)
	title.add_theme_color_override("font_color", ThemeColors.ACCENT)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)

	var resume := Button.new()
	resume.text = "RESUME"
	resume.add_theme_font_size_override("font_size", 24)
	resume.add_theme_stylebox_override("normal", ThemeColors.button_style())
	resume.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	resume.pressed.connect(_toggle_pause)
	box.add_child(resume)

	var quit_button := Button.new()
	quit_button.text = "QUIT TO MENU"
	quit_button.add_theme_font_size_override("font_size", 24)
	quit_button.add_theme_stylebox_override("normal", ThemeColors.button_style())
	quit_button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	quit_button.pressed.connect(func():
		get_tree().paused = false
		get_tree().change_scene_to_file("res://src/ui/MainMenu.tscn"))
	box.add_child(quit_button)


func _on_finished(summary: Dictionary) -> void:
	var results_scene: PackedScene = load("res://src/ui/Results.tscn")
	if results_scene == null:
		push_error("Race: Results.tscn missing")
		return
	var results := results_scene.instantiate()
	add_child(results)
	results.call_deferred("present", summary)
