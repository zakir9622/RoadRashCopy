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
	await _shoot("res://src/race/Race.tscn", out.path_join("race_coast.png"), 8, "coast_run")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_city.png"), 8, "downtown")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_desert.png"), 8, "palm_desert")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_sierra.png"), 8, "sierra_pass")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_night.png"), 8, "night_city")
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

	if scene_path.contains("Race"):
		_skip_countdown(instance)
		_place_pack(instance)

	for frame in frames:
		await process_frame

	if scene_path.contains("Race"):
		_skip_countdown(instance)
		_place_pack(instance)
		_frame_chase(instance)
		_clear_countdown_labels(instance)
		await process_frame
		await process_frame
		_freeze_tree(instance)
		await process_frame

	var image := root.get_viewport().get_texture().get_image()
	image.save_png(out_path)
	print("WROTE %s (%dx%d)" % [out_path, image.get_width(), image.get_height()])

	instance.queue_free()
	await process_frame
	await process_frame


func _skip_countdown(instance: Node) -> void:
	var manager = instance.get("manager")
	if manager == null:
		return
	manager.phase = RaceManager.Phase.RACING
	manager.countdown_remaining = -2.0


func _place_pack(instance: Node) -> void:
	var player := instance.get("player") as Rider
	if player == null:
		return
	# Mid first corner so the chase look-ahead shows the road actually bending.
	player.distance = 100.0
	player.lateral = 0.0
	player.speed = 38.0
	player.in_throttle = 1.0
	if player.has_method("_apply_transform"):
		player._apply_transform()
	var extra := 0
	for child in instance.get_children():
		if child is Rider and child != player:
			var rival := child as Rider
			rival.distance = 112.0 + extra * 4.2
			rival.lateral = clampf(-3.0 + extra * 1.05, -4.5, 4.5)
			rival.speed = 32.0
			if rival.has_method("_apply_transform"):
				rival._apply_transform()
			extra += 1


func _freeze_tree(node: Node) -> void:
	node.set_process(false)
	node.set_physics_process(false)
	for child in node.get_children():
		_freeze_tree(child)


func _clear_countdown_labels(node: Node) -> void:
	if node is Label:
		var text := String((node as Label).text)
		if text in ["1", "2", "3", "GO!"]:
			(node as Label).text = ""
	for child in node.get_children():
		_clear_countdown_labels(child)


func _frame_chase(instance: Node) -> void:
	var cam := instance.get("camera") as Camera3D
	var track := instance.get("track") as Track
	var player := instance.get("player") as Rider
	if cam == null or track == null or player == null:
		return
	var behind := track.sample(maxf(player.distance - 6.2, 0.0), player.lateral * 0.28, 2.05)
	var look := track.sample(player.distance + 55.0, player.lateral * 0.08, 0.7)
	cam.global_position = behind.origin
	View.look_at(cam, look.origin)
	cam.fov = 68.0
