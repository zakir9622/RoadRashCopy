class_name TrackCatalog
## Every track in the game, in one reviewable list, easiest to hardest.
## Adding a track is a data change, not a new generator.


static func all() -> Array[Dictionary]:
	return [
		{
			"id": "coast_run", "name": "Coast Run", "biome": "coast",
			"length": 2400.0, "width": 22.0, "curviness": 0.5, "hills": 0.3,
			"traffic": 10, "police": 1, "rivals": 5, "purse": 800, "night": false,
		},
		{
			"id": "palm_desert", "name": "Palm Desert", "biome": "desert",
			"length": 3000.0, "width": 24.0, "curviness": 0.7, "hills": 0.2,
			"traffic": 8, "police": 1, "rivals": 6, "purse": 1200, "night": false,
		},
		{
			"id": "downtown", "name": "Downtown", "biome": "city",
			"length": 2600.0, "width": 17.0, "curviness": 1.2, "hills": 0.15,
			"traffic": 20, "police": 2, "rivals": 7, "purse": 1800, "night": false,
		},
		{
			"id": "sierra_pass", "name": "Sierra Pass", "biome": "mountain",
			"length": 2800.0, "width": 19.0, "curviness": 1.5, "hills": 1.0,
			"traffic": 6, "police": 1, "rivals": 9, "purse": 2600, "night": false,
		},
		{
			"id": "night_city", "name": "Night City", "biome": "night",
			"length": 2700.0, "width": 16.0, "curviness": 1.3, "hills": 0.4,
			"traffic": 16, "police": 3, "rivals": 12, "purse": 4000, "night": true,
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
