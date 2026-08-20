extends SceneTree
## Scene smoke harness:  godot --headless --script res://tests/smoke.gd
## Boots every scene with real autoloads, runs frames of game code, then shuts
## audio down before quitting so the exit is provably leak-free. Exit 0 = clean.

const AUTOLOADS := {
	"GameState": "res://src/autoload/game_state.gd",
	"GraphicsSettings": "res://src/autoload/graphics_settings.gd",
	"RaceContext": "res://src/autoload/race_context.gd",
	"AudioDirector": "res://src/autoload/audio_director.gd",
}

const SCENES := {
	"res://src/ui/MainMenu.tscn": 90,
	"res://src/race/Race.tscn": 300,
	"res://src/ui/Garage.tscn": 30,
}


func _init() -> void:
	_run()


func _run() -> void:
	for autoload_name in AUTOLOADS:
		var node := Node.new()
		node.name = autoload_name
		node.set_script(load(AUTOLOADS[autoload_name]))
		root.add_child(node)

	await process_frame

	for path in SCENES:
		print("SMOKE: booting %s" % path)
		var scene: PackedScene = load(path)
		if scene == null:
			printerr("SMOKE FAIL: could not load %s" % path)
			quit(1)
			return
		var instance := scene.instantiate()
		root.add_child(instance)
		for frame in int(SCENES[path]):
			await process_frame
		instance.queue_free()
		await process_frame
		await process_frame
		print("SMOKE: %s ok" % path)

	var director := root.get_node_or_null("AudioDirector")
	if director != null:
		director.stop_all()
		# Freeing the director tears its players down synchronously, which
		# unregisters their playbacks from the AudioServer on this frame.
		root.remove_child(director)
		director.free()
	# One more mix interval so the audio thread drops its playback references.
	await create_timer(0.3).timeout
	print("SMOKE OK")
	quit(0)
