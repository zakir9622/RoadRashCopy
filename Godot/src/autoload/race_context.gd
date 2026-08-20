extends Node
## Cross-scene payload: track, mode, and campaign event index for the next race.

enum Mode { CAMPAIGN, QUICK, THRASH, TIME_TRIAL }

var track_id: String = "coast_run"
var campaign_event: bool = false
var mode: int = Mode.CAMPAIGN
var chapter_index: int = 0
var division_scale: float = 1.0


func launch_campaign(chapter_index: int) -> void:
	var info := Campaign.event_at(chapter_index)
	track_id = String(info["track"])
	campaign_event = true
	mode = Mode.CAMPAIGN
	self.chapter_index = chapter_index
	division_scale = float(info["skill_scale"])


func launch_quick_race(id: String) -> void:
	track_id = id
	campaign_event = false
	mode = Mode.QUICK
	division_scale = 1.0


func launch_thrash(id: String) -> void:
	track_id = id
	campaign_event = false
	mode = Mode.THRASH
	division_scale = 1.15


func launch_time_trial(id: String) -> void:
	track_id = id
	campaign_event = false
	mode = Mode.TIME_TRIAL
	division_scale = 1.0


func is_time_trial() -> bool:
	return mode == Mode.TIME_TRIAL


func track() -> Dictionary:
	var def := TrackCatalog.find(track_id)
	if division_scale > 1.0:
		def = def.duplicate(true)
		def["rivals"] = mini(int(def["rivals"]) + int((division_scale - 1.0) * 4.0), Campaign.RIVAL_COUNT)
	return def
