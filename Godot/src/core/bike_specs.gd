class_name BikeSpecs
## The shop bikes and their physical character. Upgrades stack multiplicatively
## on whatever bike is ridden, so money spent always changes how the bike feels.


static func all() -> Array[Dictionary]:
	return [
		{"id": "rat", "name": "Rat 250", "price": 0,
		 "top_speed": 42.0, "accel": 9.0, "handling": 1.0, "nitro": 0.0},
		{"id": "sport", "name": "Streetfighter 600", "price": 4500,
		 "top_speed": 54.0, "accel": 13.0, "handling": 1.15, "nitro": 6.0},
		{"id": "super", "name": "Superbike 1000", "price": 12000,
		 "top_speed": 66.0, "accel": 17.0, "handling": 1.3, "nitro": 10.0},
	]


static func find(id: String) -> Dictionary:
	for bike in all():
		if bike["id"] == id:
			return bike
	return all()[0]


## Engine stages raise top speed and acceleration; tire stages raise handling.
static func effective(bike: Dictionary, engine_stage: int, tire_stage: int) -> Dictionary:
	var spec := bike.duplicate()
	spec["top_speed"] = float(bike["top_speed"]) * (1.0 + 0.06 * engine_stage)
	spec["accel"] = float(bike["accel"]) * (1.0 + 0.08 * engine_stage)
	spec["handling"] = float(bike["handling"]) * (1.0 + 0.07 * tire_stage)
	return spec


static func upgrade_cost(stage: int) -> int:
	return 600 + stage * 500
