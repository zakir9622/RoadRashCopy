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

	await _shoot("res://src/ui/MainMenu.tscn", out.path_join("menu.png"), 90, "")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_coast.png"), 120, "coast_run")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_city.png"), 120, "downtown")
	await _shoot("res://src/race/Race.tscn", out.path_join("race_night.png"), 120, "night_city")
	await _shoot("res://src/ui/Garage.tscn", out.path_join("garage.png"), 30, "")

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
		if scene_path.contains("Race") and instance.get("player") != null:
			var player: Rider = instance.get("player")
			player.in_throttle = 1.0
		await process_frame

	var image := root.get_viewport().get_texture().get_image()
	image.save_png(out_path)
	print("WROTE %s (%dx%d)" % [out_path, image.get_width(), image.get_height()])

	instance.queue_free()
	await process_frame
	await process_frame
