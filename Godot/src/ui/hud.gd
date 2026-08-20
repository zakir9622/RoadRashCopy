extends CanvasLayer
## Modern in-race HUD: slim top strip, digital speed, PUBG-style two-thumb
## controls, optional rear mirrors.

const MIRROR_W := 96.0
const MIRROR_H := 52.0

var _race: Node3D
var _speed_value: Label
var _position_label: Label
var _distance_label: Label
var _weapon_label: Label
var _rival_name: Label
var _countdown_overlay: Label
var _ko_label: Label
var _police_label: Label
var _stamina_bar: ProgressBar
var _bike_bar: ProgressBar
var _rival_bar: ProgressBar
var _nitro_bar: ProgressBar
var _rpm_bar: ProgressBar
var _mirror_l_cam: Camera3D
var _mirror_r_cam: Camera3D
var _mirror_row: Control
var _damage_flash: ColorRect
var _flash_level: float = 0.0
var _flash_l: ColorRect
var _flash_r: ColorRect
var _split_label: Label
var _race_clock: float = 0.0
var _next_split: float = 400.0
var _last_ko: int = 0
var _rival_portrait: PanelContainer
var _banter_label: Label
var _banter_timer: float = 0.0
var _touch_root: Control
var _steer_origin: float = 0.0
var _steering: bool = false
var _punch_held: bool = false
var _brake_btn: Button


func bind(race: Node3D) -> void:
	if race == null:
		return
	_race = race
	_build()
	var player: Rider = race.player
	if player == null:
		return
	player.damaged.connect(_on_player_damaged)
	player.attacked.connect(_on_player_attacked)
	player.weapon_stolen.connect(_on_weapon_stolen)


func _gfx():
	return get_node_or_null("/root/GraphicsSettings")


func refresh_quality() -> void:
	var gfx =  _gfx()
	if _brake_btn != null and gfx != null:
		_brake_btn.visible = bool(gfx.call("show_brake"))


func _build() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	_damage_flash = ColorRect.new()
	_damage_flash.set_anchors_preset(Control.PRESET_FULL_RECT)
	_damage_flash.color = Color(1, 0.2, 0.24, 0.0)
	_damage_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_damage_flash)

	_flash_l = ColorRect.new()
	_flash_l.color = Color(1, 0.25, 0.2, 0.0)
	_flash_l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_flash_l.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	_flash_l.offset_right = 48
	root.add_child(_flash_l)
	_flash_r = ColorRect.new()
	_flash_r.color = Color(1, 0.25, 0.2, 0.0)
	_flash_r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_flash_r.set_anchors_preset(Control.PRESET_RIGHT_WIDE)
	_flash_r.offset_left = -48
	root.add_child(_flash_r)

	_countdown_overlay = Label.new()
	_countdown_overlay.add_theme_font_size_override("font_size", 72)
	_countdown_overlay.add_theme_color_override("font_color", ThemeColors.ACCENT)
	_countdown_overlay.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(_countdown_overlay)
	ThemeColors.place(_countdown_overlay, Control.PRESET_CENTER, 0)

	_build_dashboard(root)
	var gfx =  _gfx()
	if gfx != null and bool(gfx.call("mirrors_enabled")):
		_build_mirrors(root)
	if DisplayServer.get_name() != "headless":
		_build_touch_controls(root)


func _build_dashboard(root: Control) -> void:
	_weapon_label = _label(root, "FISTS", 13, ThemeColors.INK)
	ThemeColors.place(_weapon_label, Control.PRESET_TOP_LEFT, 12)

	_split_label = _label(root, "", 14, ThemeColors.ACCENT)
	_split_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ThemeColors.place(_split_label, Control.PRESET_CENTER_TOP, 88)

	_position_label = _label(root, "POS 15/15", 18, ThemeColors.ACCENT)
	ThemeColors.place(_position_label, Control.PRESET_CENTER_TOP, 10)
	_position_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_police_label = _label(root, "", 14, ThemeColors.POLICE_BLUE)
	ThemeColors.place(_police_label, Control.PRESET_CENTER_TOP, 32)
	_police_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_ko_label = _label(root, "KO 0", 13, ThemeColors.ACCENT)
	ThemeColors.place(_ko_label, Control.PRESET_TOP_RIGHT, 64)

	var pause := Button.new()
	pause.text = "II"
	pause.custom_minimum_size = Vector2(48, 48)
	pause.focus_mode = Control.FOCUS_NONE
	pause.process_mode = Node.PROCESS_MODE_ALWAYS
	pause.add_theme_font_size_override("font_size", 16)
	pause.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	var pst := StyleBoxFlat.new()
	pst.bg_color = Color(0, 0, 0, 0.45)
	pst.set_corner_radius_all(24)
	pst.set_border_width_all(1)
	pst.border_color = Color(1, 1, 1, 0.28)
	pause.add_theme_stylebox_override("normal", pst)
	pause.add_theme_stylebox_override("pressed", ThemeColors.button_style(true))
	root.add_child(pause)
	ThemeColors.place(pause, Control.PRESET_TOP_RIGHT, 10)
	pause.pressed.connect(func():
		if _race != null and _race.has_method("_toggle_pause"):
			_race._toggle_pause())

	_rival_name = _label(root, "", 13, ThemeColors.INK_MUTED)
	ThemeColors.place(_rival_name, Control.PRESET_TOP_WIDE, 52)
	_rival_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_banter_label = _label(root, "", 15, ThemeColors.ACCENT)
	_banter_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ThemeColors.place(_banter_label, Control.PRESET_TOP_WIDE, 70)

	_rival_portrait = PanelContainer.new()
	_rival_portrait.visible = false
	root.add_child(_rival_portrait)

	var cluster := VBoxContainer.new()
	cluster.add_theme_constant_override("separation", 3)
	cluster.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(cluster)
	ThemeColors.place(cluster, Control.PRESET_CENTER_BOTTOM, 118)

	_speed_value = _label(cluster, "0", 36, ThemeColors.INK)
	_speed_value.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var mph := _label(cluster, "MPH", 11, ThemeColors.INK_MUTED)
	mph.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_distance_label = _label(cluster, "", 11, ThemeColors.INK_MUTED)
	_distance_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_rpm_bar = _bar(cluster, ThemeColors.ACCENT_DIM)
	_rpm_bar.custom_minimum_size = Vector2(160, 6)

	var bars := HBoxContainer.new()
	bars.add_theme_constant_override("separation", 8)
	bars.mouse_filter = Control.MOUSE_FILTER_IGNORE
	cluster.add_child(bars)
	_stamina_bar = _bar(bars, ThemeColors.ACCENT)
	_stamina_bar.custom_minimum_size = Vector2(70, 7)
	_bike_bar = _bar(bars, ThemeColors.DANGER)
	_bike_bar.custom_minimum_size = Vector2(70, 7)
	_nitro_bar = _bar(bars, ThemeColors.POLICE_BLUE)
	_nitro_bar.custom_minimum_size = Vector2(70, 7)
	_rival_bar = _bar(bars, Color(0.4, 0.7, 1.0, 0.8))
	_rival_bar.custom_minimum_size = Vector2(70, 5)


func _build_mirrors(root: Control) -> void:
	_mirror_row = HBoxContainer.new()
	_mirror_row.add_theme_constant_override("separation", 8)
	_mirror_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_mirror_row)
	ThemeColors.place(_mirror_row, Control.PRESET_TOP_WIDE, 8)
	var spacer_l := Control.new()
	spacer_l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_mirror_row.add_child(spacer_l)
	_make_mirror_panel(_mirror_row, "mirror_l")
	var gap := Control.new()
	gap.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_mirror_row.add_child(gap)
	_make_mirror_panel(_mirror_row, "mirror_r")
	var spacer_r := Control.new()
	spacer_r.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_mirror_row.add_child(spacer_r)


func _make_mirror_panel(parent: Control, cam_name: String) -> void:
	var frame := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0, 0, 0, 0.88)
	style.set_border_width_all(2)
	style.border_color = ThemeColors.POLICE_BLUE
	frame.add_theme_stylebox_override("panel", style)
	parent.add_child(frame)
	var vp := SubViewport.new()
	vp.size = Vector2i(int(MIRROR_W), int(MIRROR_H))
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(vp)
	var cam := Camera3D.new()
	cam.name = cam_name
	cam.fov = 68.0
	vp.add_child(cam)
	if cam_name == "mirror_l":
		_mirror_l_cam = cam
	else:
		_mirror_r_cam = cam
	var view := TextureRect.new()
	view.texture = vp.get_texture()
	view.custom_minimum_size = Vector2(MIRROR_W, MIRROR_H)
	view.stretch_mode = TextureRect.STRETCH_SCALE
	view.flip_h = true
	frame.add_child(view)


func _build_touch_controls(root: Control) -> void:
	var controller: PlayerController = _race.get_node_or_null("PlayerController")
	if controller == null:
		return
	_touch_root = Control.new()
	_touch_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	_touch_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_touch_root)

	var steer := Control.new()
	steer.mouse_filter = Control.MOUSE_FILTER_STOP
	_touch_root.add_child(steer)
	steer.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	steer.anchor_right = 0.42
	steer.anchor_top = 0.38
	steer.offset_left = 0
	steer.offset_right = 0
	steer.gui_input.connect(func(event: InputEvent) -> void:
		_on_steer_input(controller, steer, event))

	var race_btn := _round_btn(_touch_root, "RACE", Vector2(132, 132), Color(1.0, 0.78, 0.2, 0.22))
	ThemeColors.place(race_btn, Control.PRESET_BOTTOM_RIGHT, 18)
	race_btn.button_down.connect(func(): controller.touch_throttle = 1.0)
	race_btn.button_up.connect(func(): controller.touch_throttle = 0.0)

	var punch := _round_btn(_touch_root, "PUNCH", Vector2(76, 76), Color(1.0, 0.28, 0.32, 0.28))
	ThemeColors.place(punch, Control.PRESET_BOTTOM_RIGHT, 18)
	punch.offset_bottom = -160
	punch.offset_top = punch.offset_bottom - 76
	punch.offset_right = -28
	punch.offset_left = punch.offset_right - 76
	punch.button_down.connect(func():
		_punch_held = true
		controller.begin_touch_punch())
	punch.button_up.connect(func():
		_punch_held = false
		controller.end_touch_punch())

	var center := HBoxContainer.new()
	center.add_theme_constant_override("separation", 14)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_touch_root.add_child(center)
	ThemeColors.place(center, Control.PRESET_CENTER_BOTTOM, 18)

	var kick := _round_btn(center, "KICK", Vector2(64, 64), Color(1, 1, 1, 0.12))
	kick.pressed.connect(func(): controller.touch_kick = true)
	_brake_btn = _round_btn(center, "BRK", Vector2(64, 64), Color(1, 1, 1, 0.12))
	_brake_btn.button_down.connect(func(): controller.touch_brake = 1.0)
	_brake_btn.button_up.connect(func(): controller.touch_brake = 0.0)
	var gfx =  _gfx()
	if gfx != null:
		_brake_btn.visible = bool(gfx.call("show_brake"))
	var nitro := _round_btn(center, "N2O", Vector2(64, 64), Color(0.3, 0.55, 1.0, 0.2))
	nitro.button_down.connect(func(): controller.touch_nitro = true)
	nitro.button_up.connect(func(): controller.touch_nitro = false)


func _on_steer_input(controller: PlayerController, pad: Control, event: InputEvent) -> void:
	var gfx = _gfx()
	var sens := 1.0
	var invert := false
	if gfx != null:
		sens = float(gfx.call("steer_sensitivity"))
		invert = bool(gfx.call("invert_swipe"))
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed:
			_steering = true
			_steer_origin = touch.position.x
		else:
			_steering = false
			controller.touch_steer = 0.0
	elif event is InputEventScreenDrag and _steering:
		var drag := event as InputEventScreenDrag
		var delta := (drag.position.x - _steer_origin) / maxf(pad.size.x * 0.28, 40.0)
		if invert:
			delta = -delta
		controller.touch_steer = clampf(delta * sens, -1.0, 1.0)
	elif event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.pressed:
			_steering = true
			_steer_origin = pad.get_local_mouse_position().x
		else:
			_steering = false
			controller.touch_steer = 0.0
	elif event is InputEventMouseMotion and _steering and (event as InputEventMouseMotion).button_mask != 0:
		var local := pad.get_local_mouse_position().x
		var delta := (local - _steer_origin) / maxf(pad.size.x * 0.28, 40.0)
		if invert:
			delta = -delta
		controller.touch_steer = clampf(delta * sens, -1.0, 1.0)


func _round_btn(parent: Control, text: String, size: Vector2, fill: Color) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = size
	b.focus_mode = Control.FOCUS_NONE
	b.add_theme_font_size_override("font_size", 14)
	b.add_theme_color_override("font_color", Color(1, 1, 1, 0.78))
	var normal := StyleBoxFlat.new()
	normal.bg_color = fill
	normal.set_corner_radius_all(int(minf(size.x, size.y) * 0.5))
	normal.set_border_width_all(1)
	normal.border_color = Color(1, 1, 1, 0.28)
	var pressed := normal.duplicate() as StyleBoxFlat
	pressed.bg_color = Color(fill.r, fill.g, fill.b, minf(fill.a + 0.25, 0.7))
	b.add_theme_stylebox_override("normal", normal)
	b.add_theme_stylebox_override("pressed", pressed)
	b.add_theme_stylebox_override("hover", pressed)
	parent.add_child(b)
	return b


func _label(parent: Control, text: String, size: int, colour: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", colour)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label


func _bar(parent: Control, colour: Color) -> ProgressBar:
	var bar := ProgressBar.new()
	bar.max_value = 1.0
	bar.value = 1.0
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(120, 12)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.55)
	bg.set_corner_radius_all(4)
	var fill := StyleBoxFlat.new()
	fill.bg_color = colour
	fill.set_corner_radius_all(4)
	bar.add_theme_stylebox_override("background", bg)
	bar.add_theme_stylebox_override("fill", fill)
	parent.add_child(bar)
	return bar


func _on_player_damaged(_amount: float, from_side: float) -> void:
	_flash_level = 0.55
	if _flash_l != null and from_side < -0.15:
		_flash_l.color.a = 0.55
	if _flash_r != null and from_side > 0.15:
		_flash_r.color.a = 0.55


func _on_player_attacked(_side: float, _kick: bool) -> void:
	if _race != null and _race.manager != null:
		_race.manager.register_heat_punch()


func _on_weapon_stolen(from_name: String) -> void:
	show_banter("Yeah! Stole from %s!" % from_name)


func show_banter(text: String) -> void:
	if _banter_label == null:
		return
	_banter_label.text = text
	_banter_timer = 2.8


func _update_rival_portrait(rival: Rider) -> void:
	if _rival_portrait == null:
		return
	for child in _rival_portrait.get_children():
		child.queue_free()
	if rival == null:
		return
	var face := Label.new()
	face.text = rival.rider_name.substr(0, 1)
	face.add_theme_font_size_override("font_size", 28)
	face.add_theme_color_override("font_color", rival.suit_color.lightened(0.35))
	face.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	face.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	face.set_anchors_preset(Control.PRESET_FULL_RECT)
	_rival_portrait.add_child(face)


func _process(delta: float) -> void:
	if _race == null or _race.manager == null or _race.player == null or _race.track == null:
		return
	var manager: RaceManager = _race.manager
	var player: Rider = _race.player
	var track: Track = _race.track

	_speed_value.text = str(int(TrackCatalog.to_mph(player.speed)))
	if _rpm_bar != null:
		_rpm_bar.value = clampf(player.speed / maxf(player.top_speed, 1.0), 0.0, 1.0)
	_position_label.text = "POS %d/%d" % [manager.position_of(player), manager.racers.size()]
	_weapon_label.text = String(CombatMath.WEAPON_NAMES.get(player.weapon, "FISTS"))
	match int(player.weapon):
		CombatMath.Weapon.CHAIN:
			_weapon_label.add_theme_color_override("font_color", Color(0.78, 0.82, 0.88))
		CombatMath.Weapon.BAT:
			_weapon_label.add_theme_color_override("font_color", Color(0.9, 0.62, 0.28))
		_:
			_weapon_label.add_theme_color_override("font_color", ThemeColors.INK)
	_stamina_bar.value = player.stamina / StaminaRules.MAX
	_bike_bar.value = player.health / 100.0
	if _nitro_bar != null:
		_nitro_bar.value = player.nitro_fuel

	var remaining := TrackCatalog.to_miles(maxf(track.length - player.distance, 0.0))
	var traveled := TrackCatalog.to_miles(player.distance)
	_distance_label.text = "ODO %.1f  ·  %.1f LEFT" % [traveled, remaining]

	var nearest: Rider = _nearest_rival(manager, player)
	if nearest != null:
		_rival_name.text = "%s%s" % [nearest.rider_name.to_upper(),
			" · %s" % nearest.gang.to_upper() if nearest.gang != "" else ""]
		_rival_bar.value = nearest.stamina / StaminaRules.MAX
		_update_rival_portrait(nearest)
	else:
		_rival_name.text = "CLEAR"
		_rival_bar.value = 0.0
		_update_rival_portrait(null)

	_banter_timer = maxf(_banter_timer - delta, 0.0)
	if _banter_timer <= 0.0 and _banter_label != null and _banter_label.text != "":
		_banter_label.text = ""

	_ko_label.text = "KO %d" % player.knockouts
	if player.knockouts > _last_ko:
		show_banter("KO!")
	_last_ko = player.knockouts

	if manager.phase == RaceManager.Phase.RACING:
		_race_clock += delta
		if player.distance >= _next_split:
			var miles := TrackCatalog.to_miles(_next_split)
			var mins := int(_race_clock / 60.0)
			var secs := int(_race_clock) % 60
			if _split_label != null:
				_split_label.text = "CP %.1f   %d:%02d" % [miles, mins, secs]
			_next_split += 400.0

	match manager.phase:
		RaceManager.Phase.COUNTDOWN:
			_countdown_overlay.text = str(int(ceil(manager.countdown_remaining)))
		RaceManager.Phase.RACING:
			if manager.countdown_remaining > -0.8:
				_countdown_overlay.text = "GO!"
			else:
				_countdown_overlay.text = ""
			manager.countdown_remaining -= delta
		_:
			_countdown_overlay.text = ""

	var cop_text := ""
	var nearest_cop_gap := 1e9
	var cop_behind := true
	for ai in manager.police_ais:
		if ai.dormant and not ai.pursuing:
			continue
		var g: float = ai.signed_gap_to(player)
		if absf(g) < absf(nearest_cop_gap):
			nearest_cop_gap = g
			cop_behind = g > 0.0
	if absf(nearest_cop_gap) < 80.0:
		var arrow := "◄" if cop_behind else "►"
		cop_text = "%s COP %dm" % [arrow, int(absf(nearest_cop_gap))]
	_police_label.text = cop_text

	_update_mirrors(player, track)
	if _race.has_method("update_detail_window"):
		_race.update_detail_window()

	_flash_level = maxf(_flash_level - delta * 2.2, 0.0)
	_damage_flash.color.a = _flash_level
	if _flash_l != null:
		_flash_l.color.a = maxf(_flash_l.color.a - delta * 2.4, 0.0)
	if _flash_r != null:
		_flash_r.color.a = maxf(_flash_r.color.a - delta * 2.4, 0.0)


func _nearest_rival(manager: RaceManager, player: Rider) -> Rider:
	var best: Rider = null
	var best_gap := 1e9
	for rival in manager.racers:
		var r := rival as Rider
		if r == player:
			continue
		var gap := absf(r.distance - player.distance)
		if gap < best_gap:
			best_gap = gap
			best = r
	return best


func _update_mirrors(player: Rider, track: Track) -> void:
	if track == null or _mirror_l_cam == null:
		return
	var back_s := maxf(player.distance - 14.0, 0.0)
	var back_t := track.sample(back_s, player.lateral - 0.5, 1.35)
	_mirror_l_cam.global_transform = back_t
	var look := track.sample(maxf(player.distance - 28.0, 0.0), player.lateral - 1.2, 0.8)
	View.look_at(_mirror_l_cam, look.origin)
	if _mirror_r_cam != null:
		var back_r := track.sample(back_s, player.lateral + 0.5, 1.35)
		_mirror_r_cam.global_transform = back_r
		var look_r := track.sample(maxf(player.distance - 28.0, 0.0), player.lateral + 1.2, 0.8)
		View.look_at(_mirror_r_cam, look_r.origin)
