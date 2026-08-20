class_name BikeSpecs
## Soundalike shop bikes — EA couldn't license Honda/Suzuki/Kawasaki/Ducati,
## so Road Rash used Panda, Shuriken, Kamikaze, Diablo.


static func all() -> Array[Dictionary]:
	return [
		{"id": "rat", "name": "Panda 250", "price": 0,
		 "top_speed": 42.0, "accel": 9.0, "handling": 1.0, "nitro": 0.0},
		{"id": "sport", "name": "Shuriken 600", "price": 3500,
		 "top_speed": 52.0, "accel": 12.0, "handling": 1.12, "nitro": 5.0},
		{"id": "kami", "name": "Kamikaze 750", "price": 7000,
		 "top_speed": 58.0, "accel": 14.5, "handling": 1.2, "nitro": 8.0},
		{"id": "super", "name": "Diablo 1000", "price": 12000,
		 "top_speed": 66.0, "accel": 17.0, "handling": 1.3, "nitro": 10.0},
	]


static func find(id: String) -> Dictionary:
	for bike in all():
		if bike["id"] == id:
			return bike
	return all()[0]


static func effective(bike: Dictionary, engine_stage: int, tire_stage: int) -> Dictionary:
	var spec := bike.duplicate()
	spec["top_speed"] = float(bike["top_speed"]) * (1.0 + 0.06 * engine_stage)
	spec["accel"] = float(bike["accel"]) * (1.0 + 0.08 * engine_stage)
	spec["handling"] = float(bike["handling"]) * (1.0 + 0.07 * tire_stage)
	return spec


static func upgrade_cost(stage: int) -> int:
	return 600 + stage * 500
