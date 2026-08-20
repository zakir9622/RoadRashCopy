class_name TrackCatalog
## Every track in the game, in one reviewable list, easiest to hardest.
## Adding a track is a data change, not a new generator.


static func all() -> Array[Dictionary]:
	return [
		{
			"id": "coast_run", "name": "Pacific Coast", "biome": "coast",
			"length": 2400.0, "width": 20.0, "curviness": 0.5, "hills": 0.3,
			"traffic": 12, "police": 1, "rivals": 14, "purse": 800, "night": false,
		},
		{
			"id": "palm_desert", "name": "Palm Desert", "biome": "desert",
			"length": 3000.0, "width": 22.0, "curviness": 0.7, "hills": 0.2,
			"traffic": 10, "police": 1, "rivals": 14, "purse": 1200, "night": false,
		},
		{
			"id": "downtown", "name": "The City", "biome": "city",
			"length": 2600.0, "width": 13.0, "curviness": 1.2, "hills": 0.15,
			"traffic": 22, "police": 2, "rivals": 14, "purse": 1800, "night": false,
		},
		{
			"id": "sierra_pass", "name": "Sierra Nevada", "biome": "mountain",
			"length": 2800.0, "width": 18.0, "curviness": 1.5, "hills": 1.0,
			"traffic": 8, "police": 1, "rivals": 14, "purse": 2600, "night": false,
		},
		{
			"id": "night_city", "name": "Night City", "biome": "night",
			"length": 2700.0, "width": 12.0, "curviness": 1.3, "hills": 0.4,
			"traffic": 18, "police": 3, "rivals": 14, "purse": 4000, "night": true,
		},
	]


static func find(id: String) -> Dictionary:
	for track in all():
		if track["id"] == id:
			return track
	return all()[0]


## Track for a campaign position, clamped so a drifting index never wraps a
## player back to the start and loses their progress.
static func at(index: int) -> Dictionary:
	var tracks := all()
	return tracks[clampi(index, 0, tracks.size() - 1)]


static func to_miles(meters: float) -> float:
	return meters / 1609.34


static func to_mph(meters_per_sec: float) -> float:
	return meters_per_sec * 2.236936
