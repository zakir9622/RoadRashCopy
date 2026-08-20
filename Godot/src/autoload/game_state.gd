extends Node
## Save/load and session state. JSON at user://save.json with a schema version
## so a future format change migrates instead of corrupting.

const SAVE_PATH := "user://save.json"
const SCHEMA := 1

var save: Dictionary = _default_save()


func _ready() -> void:
	load_save()


static func _default_save() -> Dictionary:
	return {
		"schema": SCHEMA,
		"cash": 0,
		"chapter": 0,
		"races": 0,
		"bike": "rat",
		"owned_bikes": ["rat"],
		"engine_stage": 0,
		"tire_stage": 0,
		"sfx_volume": 1.0,
		"music_volume": 0.8,
	}


func load_save() -> void:
	if not FileAccess.file_exists(SAVE_PATH):
		save = _default_save()
		return
	var file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if file == null:
		save = _default_save()
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if parsed is Dictionary and int(parsed.get("schema", 0)) == SCHEMA:
		# Merge over defaults so a save from an older build gains new keys.
		var merged := _default_save()
		merged.merge(parsed, true)
		save = merged
	else:
		save = _default_save()


func persist() -> void:
	var file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if file == null:
		push_warning("GameState: could not open save for writing")
		return
	file.store_string(JSON.stringify(save))


func current_bike_spec() -> Dictionary:
	var bike := BikeSpecs.find(String(save.get("bike", "rat")))
	return BikeSpecs.effective(bike, int(save.get("engine_stage", 0)), int(save.get("tire_stage", 0)))
