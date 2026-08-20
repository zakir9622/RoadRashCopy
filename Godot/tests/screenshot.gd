extends SceneTree
## Visual QA harness:  xvfb-run godot --rendering-driver vulkan --script res://tests/screenshot.gd
## Renders key scenes with the real pipeline and writes PNGs. Needs a display
## (not --headless). Not part of CI.

const AUTOLOADS := {
	"GameState": "res://src/autoload/game_state.gd",
	"RaceContext": "res://src/autoload/race_context.gd",
	"AudioDirector": "res://src/autoload/audio_director.gd",
}


func _init() -> void:
	_run()


func _out_dir() -> String:
	var env := OS.get_environment("SCREENSHOT_DIR")
	return env if env != "" else "user://screenshots"


func _run() -> void:
	var out := _out_dir()
	DirAccess.make_dir_recursive_absolute(out)
	for autoload_name in AUTOLOADS:
		var node := Node.new()
		node.name = autoload_name
		node.set_script(load(AUTOLOADS[autoload_name]))
		root.add_child(node)
	await process_frame

	await _shoot("res://src/ui/MainMenu.tscn", out.path_join("menu.png"), 18, "")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_coast.png"), 10, "coast_run")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_city.png"), 10, "downtown")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_desert.png"), 10, "palm_desert")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_sierra.png"), 10, "sierra_pass")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_night.png"), 10, "night_city")
	await _shoot("res://src/ui/Garage.tscn", out.path_join("garage.png"), 12, "")

	print("SCREENSHOTS DONE")
	quit(0)


func _shoot(scene_path: String, out_path: String, frames: int, track_id: String) -> void:
	if track_id != "":
		var context := root.get_node_or_null("RaceContext")
		if context == null:
			printerr("SHOT FAIL: RaceContext missing")
			return
		context.track_id = track_id
	print("SHOT: %s -> %s" % [scene_path, out_path])
	var scene: PackedScene = load(scene_path)
	if scene == null:
		printerr("SHOT FAIL: could not load %s" % scene_path)
		return
	var instance := scene.instantiate()
	root.add_child(instance)

	for frame in frames:
		await process_frame

	if scene_path.contains("Race"):
		_prepare_race_shot(instance)
		_frame_chase(instance)
		await process_frame
		await process_frame

	var image := root.get_viewport().get_texture().get_image()
	image.save_png(out_path)
	print("WROTE %s (%dx%d)" % [out_path, image.get_width(), image.get_height()])

	instance.queue_free()
	await process_frame
	await process_frame


func _prepare_race_shot(instance: Node) -> void:
	var player := instance.get("player") as Rider
	var manager = instance.get("manager")
	if player == null:
		return
	if manager != null:
		manager.phase = RaceManager.Phase.RACING
		manager.countdown_remaining = 0.0
	player.distance = 420.0
	player.lateral = 0.0
	player.speed = 34.0
	player.in_throttle = 1.0
	if player.has_method("_apply_transform"):
		player._apply_transform()
	var extra := 0
	for child in instance.get_children():
		if child is Rider and child != player:
			var rival := child as Rider
			rival.distance = 455.0 + extra * 7.0
			rival.lateral = clampf(rival.lateral, -4.0, 4.0)
			rival.speed = 28.0
			if rival.has_method("_apply_transform"):
				rival._apply_transform()
			extra += 1
	_freeze_tree(instance)


func _freeze_tree(node: Node) -> void:
	node.set_process(false)
	node.set_physics_process(false)
	for child in node.get_children():
		_freeze_tree(child)


func _frame_chase(instance: Node) -> void:
	var cam := instance.get("camera") as Camera3D
	var track := instance.get("track") as Track
	var player := instance.get("player") as Rider
	if cam == null or track == null or player == null:
		return
	var behind := track.sample(maxf(player.distance - 8.0, 0.0), player.lateral * 0.2, 2.7)
	var look := track.sample(player.distance + 24.0, player.lateral * 0.1, 0.85)
	cam.global_position = behind.origin
	View.look_at(cam, look.origin)
	cam.fov = 60.0
