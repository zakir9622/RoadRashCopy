extends SceneTree
## One-off visual debug: renders the bare Track from above with no fog and a
## plain sun, and prints which track definition the Race scene actually loads.

const OUT := "/workspace/artifacts"


func _init() -> void:
	_run()


func _run() -> void:
	# --- part 1: which definition does Race read? ---
	var context := Node.new()
	context.name = "RaceContext"
	context.set_script(load("res://src/autoload/race_context.gd"))
	root.add_child(context)
	var state := Node.new()
	state.name = "GameState"
	state.set_script(load("res://src/autoload/game_state.gd"))
	root.add_child(state)
	var audio := Node.new()
	audio.name = "AudioDirector"
	audio.set_script(load("res://src/autoload/audio_director.gd"))
	root.add_child(audio)
	await process_frame

	context.track_id = "night_city"
	print("DEBUG: context.track_id = ", context.track_id)
	print("DEBUG: context.track() = ", context.track()["id"])

	var race_scene: PackedScene = load("res://src/race/Race.tscn")
	var race := race_scene.instantiate()
	root.add_child(race)
	await process_frame
	print("DEBUG: race loaded definition = ", race.track.definition["id"])
	race.queue_free()
	await process_frame

	# --- part 2: bare track, no fog, camera looking down the road ---
	var world := Node3D.new()
	root.add_child(world)
	var track := Track.new()
	world.add_child(track)
	track.build(TrackCatalog.find("coast_run"))

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-55, 30, 0)
	world.add_child(sun)

	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.3, 0.5, 0.8)
	env.ambient_light_color = Color(0.6, 0.6, 0.65)
	env.ambient_light_energy = 1.0
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	world.add_child(world_env)

	var camera := Camera3D.new()
	world.add_child(camera)
	var t := track.sample(60.0, 0.0, 6.0)
	camera.global_position = t.origin
	camera.look_at(track.sample(110.0, 0.0, 0.0).origin, Vector3.UP)
	camera.make_current()

	for i in 30:
		await process_frame
	var image := root.get_viewport().get_texture().get_image()
	image.save_png(OUT + "/road_debug.png")
	print("DEBUG: wrote road_debug.png")
	quit(0)
