extends SceneTree
## Visual QA harness:  xvfb-run godot --rendering-driver vulkan --script res://tests/screenshot.gd
## Renders key scenes with the real pipeline (shaders, HDRI, glow, fog) and
## writes PNGs for review. Requires a rendering context — not --headless.

const OUT_DIR := "/workspace/artifacts"

const AUTOLOADS := {
	"GameState": "res://src/autoload/game_state.gd",
	"RaceContext": "res://src/autoload/race_context.gd",
	"AudioDirector": "res://src/autoload/audio_director.gd",
}


func _init() -> void:
	_run()


func _run() -> void:
	DirAccess.make_dir_recursive_absolute(OUT_DIR)
	for autoload_name in AUTOLOADS:
		var node := Node.new()
		node.name = autoload_name
		node.set_script(load(AUTOLOADS[autoload_name]))
		root.add_child(node)
	await process_frame

	await _shoot("res://src/ui/MainMenu.tscn", "menu.png", 90, "")
	await _shoot("res://src/race/Race.tscn", "race_coast.png", 120, "coast_run")
	await _shoot("res://src/race/Race.tscn", "race_city.png", 120, "downtown")
	await _shoot("res://src/race/Race.tscn", "race_night.png", 120, "night_city")
	await _shoot("res://src/ui/Garage.tscn", "garage.png", 30, "")

	print("SCREENSHOTS DONE")
	quit(0)


func _shoot(scene_path: String, out_name: String, frames: int, track_id: String) -> void:
	if track_id != "":
		var context := root.get_node("RaceContext")
		context.track_id = track_id
	print("SHOT: %s -> %s" % [scene_path, out_name])
	var scene: PackedScene = load(scene_path)
	var instance := scene.instantiate()
	root.add_child(instance)

	# Drive the player forward so race shots capture motion, not the grid.
	for frame in frames:
		var race := instance as Node3D
		if scene_path.contains("Race") and race != null and race.get("player") != null:
			var player: Rider = race.get("player")
			player.in_throttle = 1.0
		await process_frame

	var image := root.get_viewport().get_texture().get_image()
	image.save_png(OUT_DIR.path_join(out_name))
	print("WROTE %s (%dx%d)" % [out_name, image.get_width(), image.get_height()])

	instance.queue_free()
	await process_frame
	await process_frame
