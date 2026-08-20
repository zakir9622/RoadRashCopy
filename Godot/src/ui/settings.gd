extends Control
## Reusable pause / menu settings: audio, graphics, controls, camera, weather.

signal closed

var _music: HSlider
var _sfx: HSlider
var _quality: CheckButton
var _steer: HSlider
var _invert: CheckButton
var _brake: CheckButton
var _mirrors: CheckButton
var _near: CheckButton
var _look: HSlider
var _weather: CheckButton


func _ready() -> void:
	_build()
	_load_from_save()


func _gfx():
	return get_node_or_null("/root/GraphicsSettings")


func _state() -> Node:
	return get_node_or_null("/root/GameState")


func _build() -> void:
	var scrim := ColorRect.new()
	scrim.color = Color(0.02, 0.03, 0.05, 0.82)
	scrim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(scrim)

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	ThemeColors.center_wrap(self).add_child(panel)

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 10)
	box.custom_minimum_size = Vector2(460, 0)
	panel.add_child(box)

	var title := Label.new()
	title.text = "SETTINGS"
	title.add_theme_font_size_override("font_size", 32)
	title.add_theme_color_override("font_color", ThemeColors.ACCENT)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)

	_music = _slider_row(box, "MUSIC")
	_sfx = _slider_row(box, "SFX")
	_quality = _check_row(box, "HIGH GRAPHICS")
	_steer = _slider_row(box, "STEER SENSITIVITY")
	_steer.min_value = 0.4
	_steer.max_value = 2.2
	_invert = _check_row(box, "INVERT SWIPE")
	_brake = _check_row(box, "SHOW BRAKE")
	_mirrors = _check_row(box, "REAR MIRRORS")
	_near = _check_row(box, "CLOSE CAMERA")
	_look = _slider_row(box, "LOOK AHEAD")
	_look.min_value = 28.0
	_look.max_value = 80.0
	_weather = _check_row(box, "DISABLE WEATHER")

	var back := Button.new()
	back.text = "DONE"
	back.add_theme_font_size_override("font_size", 22)
	back.add_theme_stylebox_override("normal", ThemeColors.button_style())
	back.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	back.pressed.connect(_on_done)
	box.add_child(back)


func _slider_row(parent: Control, caption: String) -> HSlider:
	var label := Label.new()
	label.text = caption
	label.add_theme_font_size_override("font_size", 14)
	label.add_theme_color_override("font_color", ThemeColors.INK_MUTED)
	parent.add_child(label)
	var slider := HSlider.new()
	slider.min_value = 0.0
	slider.max_value = 1.0
	slider.step = 0.05
	slider.custom_minimum_size = Vector2(0, 22)
	parent.add_child(slider)
	return slider


func _check_row(parent: Control, caption: String) -> CheckButton:
	var row := CheckButton.new()
	row.text = caption
	row.add_theme_font_size_override("font_size", 16)
	row.add_theme_color_override("font_color", ThemeColors.INK)
	parent.add_child(row)
	return row


func _load_from_save() -> void:
	var state := _state()
	var gfx =  _gfx()
	if state != null:
		_music.value = float(state.save.get("music_volume", 0.8))
		_sfx.value = float(state.save.get("sfx_volume", 1.0))
	if gfx != null:
		_quality.button_pressed = bool(gfx.call("is_high"))
		_steer.value = float(gfx.call("steer_sensitivity"))
		_invert.button_pressed = bool(gfx.call("invert_swipe"))
		_brake.button_pressed = bool(gfx.call("show_brake"))
		_mirrors.button_pressed = bool(gfx.call("mirrors_enabled"))
		_near.button_pressed = bool(gfx.call("is_camera_near"))
		_look.value = float(gfx.call("cam_look"))
		_weather.button_pressed = bool(gfx.call("weather_off"))


func _on_done() -> void:
	var state := _state()
	var gfx =  _gfx()
	if state != null:
		state.save["music_volume"] = _music.value
		state.save["sfx_volume"] = _sfx.value
	if gfx != null:
		gfx.call("set_high", _quality.button_pressed)
		gfx.call("set_steer_sensitivity", _steer.value)
		gfx.call("set_invert_swipe", _invert.button_pressed)
		gfx.call("set_show_brake", _brake.button_pressed)
		gfx.call("set_mirrors_enabled", _mirrors.button_pressed)
		gfx.call("set_camera_near", _near.button_pressed)
		gfx.call("set_camera_look", _look.value)
		gfx.call("set_weather_off", _weather.button_pressed)
		gfx.call("persist")
	elif state != null and state.has_method("persist"):
		state.persist()
	closed.emit()
	queue_free()
