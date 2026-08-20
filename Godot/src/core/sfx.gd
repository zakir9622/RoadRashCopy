class_name Sfx
## Runtime-resolved bridge to the AudioDirector autoload. Gameplay scripts call
## this instead of the autoload identifier so they compile and run headless
## (tests, CI) where autoload singletons are never instantiated.


static func _director() -> Node:
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null or tree.root == null:
		return null
	return tree.root.get_node_or_null("AudioDirector")


static func play(key: String, volume_db: float = 0.0, pitch: float = 1.0) -> void:
	var director := _director()
	if director != null:
		director.play(key, volume_db, pitch)


static func set_engine(speed01: float, active: bool) -> void:
	var director := _director()
	if director != null:
		director.set_engine(speed01, active)


static func play_music() -> void:
	var director := _director()
	if director != null:
		director.play_music()
