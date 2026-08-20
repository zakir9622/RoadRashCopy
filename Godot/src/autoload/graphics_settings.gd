extends Node
## Graphics / camera / control prefs persisted on GameState.save.
## Safe under --script tests: missing GameState returns the defaults.

const QUALITY_LOW := "low"
const QUALITY_HIGH := "high"


func _state() -> Node:
	return get_node_or_null("/root/GameState")


func _read(key: String, fallback: Variant) -> Variant:
	var state := _state()
	if state == null:
		return fallback
	return state.save.get(key, fallback)


func _write(key: String, value: Variant) -> void:
	var state := _state()
	if state == null:
		return
	state.save[key] = value


func persist() -> void:
	var state := _state()
	if state != null and state.has_method("persist"):
		state.persist()


func is_high() -> bool:
	return String(_read("graphics_quality", QUALITY_HIGH)) != QUALITY_LOW


func set_high(enabled: bool) -> void:
	_write("graphics_quality", QUALITY_HIGH if enabled else QUALITY_LOW)


func steer_sensitivity() -> float:
	return clampf(float(_read("steer_sensitivity", 1.0)), 0.4, 2.2)


func set_steer_sensitivity(value: float) -> void:
	_write("steer_sensitivity", clampf(value, 0.4, 2.2))


func invert_swipe() -> bool:
	return bool(_read("invert_swipe", false))


func set_invert_swipe(value: bool) -> void:
	_write("invert_swipe", value)


func show_brake() -> bool:
	return bool(_read("show_brake", true))


func set_show_brake(value: bool) -> void:
	_write("show_brake", value)


func mirrors_enabled() -> bool:
	return bool(_read("mirrors", false))


func set_mirrors_enabled(value: bool) -> void:
	_write("mirrors", value)


func weather_off() -> bool:
	return String(_read("weather", "auto")) == "off"


func set_weather_off(value: bool) -> void:
	_write("weather", "off" if value else "auto")


func cam_back() -> float:
	return 5.2 if String(_read("camera_distance", "far")) == "near" else 6.2


func cam_up() -> float:
	return 1.72 if String(_read("camera_distance", "far")) == "near" else 2.05


func cam_look() -> float:
	return clampf(float(_read("camera_look", 55.0)), 28.0, 80.0)


func set_camera_near(enabled: bool) -> void:
	_write("camera_distance", "near" if enabled else "far")


func is_camera_near() -> bool:
	return String(_read("camera_distance", "far")) == "near"


func set_camera_look(value: float) -> void:
	_write("camera_look", clampf(value, 28.0, 80.0))


func shadow_distance() -> float:
	return 180.0 if is_high() else 80.0


func rain_amount() -> int:
	return 900 if is_high() else 0


func street_omni_count() -> int:
	return 6 if is_high() else 0


func apply_to_environment(env: Environment, sun: DirectionalLight3D, night: bool = false) -> void:
	if env != null:
		env.glow_enabled = is_high() and night
		env.glow_intensity = 0.4 if env.glow_enabled else 0.0
	if sun != null:
		sun.shadow_enabled = true
		sun.directional_shadow_max_distance = shadow_distance()
