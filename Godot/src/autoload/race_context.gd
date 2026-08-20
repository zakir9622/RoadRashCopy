extends Node
## Cross-scene payload: which track the next race runs and whether it counts
## for the campaign. Set by the menus, read once by Race.tscn on ready.

var track_id: String = "coast_run"
var campaign_event: bool = false


func launch_campaign(chapter_index: int) -> void:
	var chapters := Campaign.chapters()
	var chapter: Dictionary = chapters[clampi(chapter_index, 0, chapters.size() - 1)]
	track_id = String(chapter["track"])
	campaign_event = true


func launch_quick_race(id: String) -> void:
	track_id = id
	campaign_event = false


func track() -> Dictionary:
	return TrackCatalog.find(track_id)
