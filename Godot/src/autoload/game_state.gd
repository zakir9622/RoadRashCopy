extends Node
## Save/load and session state. JSON at user://save.json with a schema version
## so a future format change migrates instead of corrupting.

const SAVE_PATH := "user://save.json"
const SCHEMA := 2

var save: Dictionary = _default_save()


func _ready() -> void:
	load_save()


static func _default_save() -> Dictionary:
	return {
		"schema": SCHEMA,
		"cash": 1000,
		"chapter": 0,
		"races": 0,
		"bike": "rat",
		"owned_bikes": ["rat"],
		"engine_stage": 0,
		"tire_stage": 0,
		"sfx_volume": 1.0,
		"music_volume": 0.8,
		"player_name": "",
		"grudges": {},
		"game_over": false,
		"champion": false,
		"seen_prologue": false,
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
	if parsed is Dictionary and int(parsed.get("schema", 0)) >= 1:
		var merged := _default_save()
		merged.merge(parsed, true)
		merged["schema"] = SCHEMA
		if int(parsed.get("schema", 0)) < 2 and int(merged.get("cash", 0)) == 0 \
				and int(merged.get("races", 0)) == 0:
			merged["cash"] = 1000
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


func player_display_name() -> String:
	var n := String(save.get("player_name", "")).strip_edges()
	return n if n != "" else "Rasher"


func note_grudge(rider_id: String) -> void:
	if rider_id == "":
		return
	var grudges: Dictionary = save.get("grudges", {})
	grudges[rider_id] = int(grudges.get(rider_id, 0)) + 1
	save["grudges"] = grudges


func reset_career() -> void:
	var name := String(save.get("player_name", ""))
	save = _default_save()
	save["player_name"] = name
	persist()
