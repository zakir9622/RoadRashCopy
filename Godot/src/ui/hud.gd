extends CanvasLayer
## In-race HUD: speed, stamina/health bars, position, countdown, weapon,
## knockouts, cash, police warning, rear-view mirror, and touch controls.
## Pull-based: reads the race every frame, so it can never go stale or leak.

var _race: Node3D
var _speed: Label
var _position_label: Label
var _weapon: Label
var _knockouts: Label
var _countdown: Label
var _police_warning: Label
var _health_bar: ProgressBar
var _stamina_bar: ProgressBar
var _nitro_bar: ProgressBar
var _mirror_viewport: SubViewport
var _mirror_camera: Camera3D
var _damage_flash: ColorRect
var _flash_level: float = 0.0


func bind(race: Node3D) -> void:
	_race = race
	_build()
	var player: Rider = race.player
	player.damaged.connect(_on_player_damaged)


func _build() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(root)

	_damage_flash = ColorRect.new()
	_damage_flash.set_anchors_preset(Control.PRESET_FULL_RECT)
	_damage_flash.color = Color(1, 0.2, 0.24, 0.0)
	_damage_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_damage_flash)

	# --- top-left: position ---
	var top_left := _panel(root, Control.PRESET_TOP_LEFT, Vector2(24, 24))
	_position_label = _label(top_left, "POS 1", 34, ThemeColors.ACCENT)
	_knockouts = _label(top_left, "KO 0", 18, ThemeColors.INK_MUTED)

	# --- top-right: cash + weapon ---
	var top_right := _panel(root, Control.PRESET_TOP_RIGHT, Vector2(-24, 24))
	_label(top_right, "$%d" % int(GameState.save.get("cash", 0)), 24, ThemeColors.ACCENT)
	_weapon = _label(top_right, "FISTS", 18, ThemeColors.INK)

	# --- bottom-right: speed + nitro ---
	var bottom_right := _panel(root, Control.PRESET_BOTTOM_RIGHT, Vector2(-24, -24))
	_speed = _label(bottom_right, "0", 56, ThemeColors.INK)
	_label(bottom_right, "KM/H", 14, ThemeColors.INK_MUTED)
	_nitro_bar = _bar(bottom_right, Color(1.0, 0.48, 0.1))

	# --- bottom-left: stamina + health ---
	var bottom_left := _panel(root, Control.PRESET_BOTTOM_LEFT, Vector2(24, -24))
	_label(bottom_left, "STAMINA", 14, ThemeColors.INK_MUTED)
	_stamina_bar = _bar(bottom_left, ThemeColors.ACCENT)
	_label(bottom_left, "BIKE", 14, ThemeColors.INK_MUTED)
	_health_bar = _bar(bottom_left, ThemeColors.DANGER)

	# --- centre: countdown + police warning ---
	var centre := VBoxContainer.new()
	centre.set_anchors_preset(Control.PRESET_CENTER_TOP)
	centre.position += Vector2(0, 60)
	centre.alignment = BoxContainer.ALIGNMENT_CENTER
	root.add_child(centre)
	_countdown = _label(centre, "", 110, ThemeColors.INK)
	_countdown.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_police_warning = _label(centre, "", 20, ThemeColors.POLICE_BLUE)
	_police_warning.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_build_mirror(root)
	if DisplayServer.is_touchscreen_available():
		_build_touch_controls(root)


func _build_mirror(root: Control) -> void:
	_mirror_viewport = SubViewport.new()
	_mirror_viewport.size = Vector2i(360, 120)
	_mirror_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_mirror_viewport)

	_mirror_camera = Camera3D.new()
	_mirror_camera.fov = 65.0
	_mirror_viewport.add_child(_mirror_camera)

	var frame := PanelContainer.new()
	frame.set_anchors_preset(Control.PRESET_CENTER_TOP)
	frame.position = Vector2(-180, 8)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0, 0, 0, 0.85)
	style.border_width_bottom = 2
	style.border_width_top = 2
	style.border_width_left = 2
	style.border_width_right = 2
	style.border_color = ThemeColors.POLICE_BLUE
	style.corner_radius_top_left = 16
	style.corner_radius_top_right = 16
	style.corner_radius_bottom_left = 16
	style.corner_radius_bottom_right = 16
	frame.add_theme_stylebox_override("panel", style)
	root.add_child(frame)

	var view := TextureRect.new()
	view.texture = _mirror_viewport.get_texture()
	view.custom_minimum_size = Vector2(360, 120)
	view.flip_h = true   # mirrors mirror
	frame.add_child(view)


func _build_touch_controls(root: Control) -> void:
	var controller: PlayerController = _race.get_node_or_null("PlayerController")
	if controller == null:
		return

	var left_zone := _touch_button(root, "<", Control.PRESET_BOTTOM_LEFT, Vector2(24, -160))
	left_zone.button_down.connect(func(): controller.touch_steer = -1.0)
	left_zone.button_up.connect(func(): controller.touch_steer = 0.0)
	var right_zone := _touch_button(root, ">", Control.PRESET_BOTTOM_LEFT, Vector2(140, -160))
	right_zone.button_down.connect(func(): controller.touch_steer = 1.0)
	right_zone.button_up.connect(func(): controller.touch_steer = 0.0)

	var throttle := _touch_button(root, "GO", Control.PRESET_BOTTOM_RIGHT, Vector2(-140, -160))
	throttle.button_down.connect(func(): controller.touch_throttle = 1.0)
	throttle.button_up.connect(func(): controller.touch_throttle = 0.0)

	var punch := _touch_button(root, "PUNCH", Control.PRESET_BOTTOM_RIGHT, Vector2(-260, -160))
	punch.pressed.connect(func(): controller.touch_attack_right = true)
	var kick := _touch_button(root, "KICK", Control.PRESET_BOTTOM_RIGHT, Vector2(-260, -60))
	kick.pressed.connect(func(): controller.touch_kick = true)
	var nitro := _touch_button(root, "NITRO", Control.PRESET_BOTTOM_RIGHT, Vector2(-140, -60))
	nitro.button_down.connect(func(): controller.touch_nitro = true)
	nitro.button_up.connect(func(): controller.touch_nitro = false)


func _touch_button(root: Control, text: String, preset: int, offset: Vector2) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(100, 84)
	button.set_anchors_preset(preset)
	button.position += offset
	button.add_theme_stylebox_override("normal", ThemeColors.button_style())
	button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	button.add_theme_stylebox_override("pressed", ThemeColors.button_style(true))
	button.add_theme_color_override("font_color", ThemeColors.INK)
	root.add_child(button)
	return button


func _panel(root: Control, preset: int, offset: Vector2) -> VBoxContainer:
	var panel := PanelContainer.new()
	panel.set_anchors_preset(preset)
	panel.position += offset
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	root.add_child(panel)
	var box := VBoxContainer.new()
	panel.add_child(box)
	return box


func _label(parent: Control, text: String, size: int, colour: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", colour)
	parent.add_child(label)
	return label


func _bar(parent: Control, colour: Color) -> ProgressBar:
	var bar := ProgressBar.new()
	bar.max_value = 1.0
	bar.value = 1.0
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(190, 12)
	var background := StyleBoxFlat.new()
	background.bg_color = Color(0, 0, 0, 0.8)
	var fill := StyleBoxFlat.new()
	fill.bg_color = colour
	bar.add_theme_stylebox_override("background", background)
	bar.add_theme_stylebox_override("fill", fill)
	parent.add_child(bar)
	return bar


func _on_player_damaged(_amount: float, _from_side: float) -> void:
	_flash_level = 0.55


func _process(delta: float) -> void:
	if _race == null or _race.manager == null:
		return
	var manager: RaceManager = _race.manager
	var player: Rider = _race.player

	_speed.text = str(int(player.speed * 3.6))
	_position_label.text = "POS %d/%d" % [manager.position_of(player), manager.racers.size()]
	_knockouts.text = "KO %d" % player.knockouts
	_weapon.text = String(CombatMath.WEAPON_NAMES[player.weapon])
	_health_bar.value = player.health / 100.0
	_stamina_bar.value = player.stamina / StaminaRules.MAX
	_nitro_bar.value = player.nitro_fuel

	match manager.phase:
		RaceManager.Phase.COUNTDOWN:
			_countdown.text = str(int(ceil(manager.countdown_remaining)))
		RaceManager.Phase.RACING:
			_countdown.text = "GO!" if manager.countdown_remaining > -1.0 else ""
			manager.countdown_remaining -= delta
		_:
			_countdown.text = ""

	# Police proximity warning.
	var nearest := 1e9
	for ai in manager.police_ais:
		var cop: Rider = ai.rider
		nearest = minf(nearest, absf(cop.distance - player.distance))
	_police_warning.text = "COP %d M" % int(nearest) if nearest < 40.0 else ""

	# Rear-view mirror follows the player, looking backwards.
	if _mirror_camera != null and _race.track != null:
		var t: Transform3D = _race.track.sample(player.distance + 1.5, player.lateral, 1.6)
		_mirror_camera.global_position = t.origin
		var back: Transform3D = _race.track.sample(maxf(player.distance - 10.0, 0.0), player.lateral, 1.2)
		_mirror_camera.look_at(back.origin, Vector3.UP)

	_flash_level = maxf(_flash_level - delta * 2.2, 0.0)
	_damage_flash.color.a = _flash_level
