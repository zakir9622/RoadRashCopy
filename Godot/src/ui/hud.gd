extends CanvasLayer
## Road Rash dashboard HUD: diegetic bottom instrument cluster, dual rear
## mirrors, clear centre view, and bottom-thumb touch controls on mobile.

const DASH_H := 210.0
const MIRROR_W := 88.0
const MIRROR_H := 44.0
const PORTRAIT := true

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
var _mirror_l_cam: Camera3D
var _mirror_r_cam: Camera3D
var _mirror_l_tex: TextureRect
var _mirror_r_tex: TextureRect
var _damage_flash: ColorRect
var _flash_level: float = 0.0
var _rival_portrait: PanelContainer
var _banter_label: Label
var _banter_timer: float = 0.0
var _touch_root: Control


func bind(race: Node3D) -> void:
	_race = race
	_build()
	var player: Rider = race.player
	player.damaged.connect(_on_player_damaged)
	player.attacked.connect(_on_player_attacked)
	player.weapon_stolen.connect(_on_weapon_stolen)


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

	_countdown_overlay = Label.new()
	_countdown_overlay.add_theme_font_size_override("font_size", 72)
	_countdown_overlay.add_theme_color_override("font_color", ThemeColors.ACCENT)
	_countdown_overlay.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(_countdown_overlay)
	ThemeColors.place(_countdown_overlay, Control.PRESET_CENTER, 0)

	_build_dashboard(root)
	_build_mirrors(root)
	if DisplayServer.is_touchscreen_available() or OS.has_feature("mobile"):
		_build_touch_controls(root)


func _build_dashboard(root: Control) -> void:
	var dash := PanelContainer.new()
	var dash_style := StyleBoxFlat.new()
	dash_style.bg_color = Color(0.02, 0.03, 0.05, 0.92)
	dash_style.border_width_top = 3
	dash_style.border_color = ThemeColors.ACCENT_DIM
	dash_style.content_margin_left = 16.0
	dash_style.content_margin_right = 16.0
	dash_style.content_margin_top = 10.0
	dash_style.content_margin_bottom = 10.0
	dash.add_theme_stylebox_override("panel", dash_style)
	dash.custom_minimum_size = Vector2(0, DASH_H)
	root.add_child(dash)
	ThemeColors.place(dash, Control.PRESET_BOTTOM_WIDE, 0)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 18)
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	dash.add_child(row)

	# Left gauge cluster — speedo
	var left := VBoxContainer.new()
	left.custom_minimum_size = Vector2(150, 0)
	row.add_child(left)
	_speed_value = _label(left, "0", 44, ThemeColors.INK)
	_speed_value.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label(left, "KM/H", 13, ThemeColors.INK_MUTED).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_position_label = _label(left, "POS 1/8", 18, ThemeColors.ACCENT)
	_position_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	# Centre — stamina, bike, rival, weapon, distance
	var centre := VBoxContainer.new()
	centre.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	centre.add_theme_constant_override("separation", 6)
	row.add_child(centre)

	var bar_row := HBoxContainer.new()
	bar_row.add_theme_constant_override("separation", 12)
	centre.add_child(bar_row)
	var stam_col := VBoxContainer.new()
	stam_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar_row.add_child(stam_col)
	_label(stam_col, "STAMINA", 12, ThemeColors.INK_MUTED)
	_stamina_bar = _bar(stam_col, ThemeColors.ACCENT)
	var bike_col := VBoxContainer.new()
	bike_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar_row.add_child(bike_col)
	_label(bike_col, "BIKE", 12, ThemeColors.INK_MUTED)
	_bike_bar = _bar(bike_col, ThemeColors.DANGER)

	var rival_row := HBoxContainer.new()
	centre.add_child(rival_row)
	_rival_name = _label(rival_row, "RIVAL —", 13, ThemeColors.INK_MUTED)
	_rival_name.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_rival_bar = _bar(centre, ThemeColors.POLICE_BLUE)
	_rival_bar.custom_minimum_size = Vector2(0, 10)

	_rival_portrait = PanelContainer.new()
	var portrait_style := StyleBoxFlat.new()
	portrait_style.bg_color = Color(0.05, 0.06, 0.08, 0.9)
	portrait_style.border_color = ThemeColors.ACCENT_DIM
	portrait_style.set_border_width_all(2)
	_rival_portrait.add_theme_stylebox_override("panel", portrait_style)
	_rival_portrait.custom_minimum_size = Vector2(52, 52)
	rival_row.add_child(_rival_portrait)

	_banter_label = _label(root, "", 16, ThemeColors.ACCENT)
	_banter_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ThemeColors.place(_banter_label, Control.PRESET_TOP_WIDE, 58)

	var info_row := HBoxContainer.new()
	centre.add_child(info_row)
	_weapon_label = _label(info_row, "FISTS", 15, ThemeColors.INK)
	_weapon_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_distance_label = _label(info_row, "2.4 KM", 15, ThemeColors.INK_MUTED)
	_police_label = _label(info_row, "", 14, ThemeColors.POLICE_BLUE)

	# Right gauge — tach style readout + KO
	var right := VBoxContainer.new()
	right.custom_minimum_size = Vector2(150, 0)
	row.add_child(right)
	var tach := _label(right, "RPM", 13, ThemeColors.INK_MUTED)
	tach.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var rpm := _label(right, "x4", 36, ThemeColors.INK)
	rpm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label(right, "KNOCKOUTS", 12, ThemeColors.INK_MUTED).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_ko_label = _label(right, "KO 0", 18, ThemeColors.ACCENT)
	_ko_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


func _build_mirrors(root: Control) -> void:
	var mirror_row := HBoxContainer.new()
	mirror_row.add_theme_constant_override("separation", 6)
	root.add_child(mirror_row)
	if PORTRAIT:
		ThemeColors.place(mirror_row, Control.PRESET_TOP_WIDE, 8)
	else:
		ThemeColors.place(mirror_row, Control.PRESET_BOTTOM_WIDE, int(DASH_H + 6))

	var spacer_l := Control.new()
	spacer_l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mirror_row.add_child(spacer_l)

	_mirror_l_tex = _make_mirror_panel(mirror_row, "mirror_l")
	if PORTRAIT:
		var gap := Control.new()
		gap.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		mirror_row.add_child(gap)
	else:
		var gap := Control.new()
		gap.custom_minimum_size = Vector2(280, 0)
		mirror_row.add_child(gap)
	_mirror_r_tex = _make_mirror_panel(mirror_row, "mirror_r")

	var spacer_r := Control.new()
	spacer_r.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mirror_row.add_child(spacer_r)


func _make_mirror_panel(parent: Control, cam_name: String) -> TextureRect:
	var frame := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0, 0, 0, 0.88)
	style.border_width_left = 2
	style.border_width_right = 2
	style.border_width_top = 2
	style.border_width_bottom = 2
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
	return view


func _build_touch_controls(root: Control) -> void:
	var controller: PlayerController = _race.get_node_or_null("PlayerController")
	if controller == null:
		return

	_touch_root = Control.new()
	_touch_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	_touch_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_touch_root)

	var safe := DisplayServer.get_display_safe_area()
	var bottom_inset := maxf(0.0, float(get_viewport().get_visible_rect().size.y - safe.end.y))

	var bar := HBoxContainer.new()
	bar.add_theme_constant_override("separation", 10)
	bar.alignment = BoxContainer.ALIGNMENT_END
	_touch_root.add_child(bar)
	bar.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	bar.offset_bottom = -(DASH_H + bottom_inset + 8)
	bar.offset_top = bar.offset_bottom - (128 if PORTRAIT else 108)
	bar.offset_left = 12
	bar.offset_right = -12

	# Steer cluster — bottom-left
	var steer_box := HBoxContainer.new()
	steer_box.add_theme_constant_override("separation", 8)
	steer_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(steer_box)
	var steer_l := _touch_btn(steer_box, "◄", Vector2(108, 100))
	steer_l.button_down.connect(func(): controller.touch_steer = -1.0)
	steer_l.button_up.connect(func(): controller.touch_steer = 0.0)
	var steer_r := _touch_btn(steer_box, "►", Vector2(108, 100))
	steer_r.button_down.connect(func(): controller.touch_steer = 1.0)
	steer_r.button_up.connect(func(): controller.touch_steer = 0.0)

	# Drive + combat — bottom-right
	var action_box := HBoxContainer.new()
	action_box.add_theme_constant_override("separation", 8)
	bar.add_child(action_box)

	var brake := _touch_btn(action_box, "BRK", Vector2(88, 100))
	brake.button_down.connect(func(): controller.touch_brake = 1.0)
	brake.button_up.connect(func(): controller.touch_brake = 0.0)

	var go := _touch_btn(action_box, "GO", Vector2(108, 100))
	go.button_down.connect(func(): controller.touch_throttle = 1.0)
	go.button_up.connect(func(): controller.touch_throttle = 0.0)

	var punch_l := _touch_btn(action_box, "👊L", Vector2(88, 100))
	punch_l.pressed.connect(func(): controller.touch_attack_left = true)
	var punch_r := _touch_btn(action_box, "👊R", Vector2(88, 100))
	punch_r.pressed.connect(func(): controller.touch_attack_right = true)
	var kick := _touch_btn(action_box, "KICK", Vector2(72, 92))
	kick.pressed.connect(func(): controller.touch_kick = true)
	var nitro := _touch_btn(action_box, "N2O", Vector2(72, 92))
	nitro.button_down.connect(func(): controller.touch_nitro = true)
	nitro.button_up.connect(func(): controller.touch_nitro = false)


func _touch_btn(parent: Control, text: String, size: Vector2) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = size
	b.add_theme_font_size_override("font_size", 18)
	var normal := ThemeColors.button_style()
	normal.bg_color = Color(1, 1, 1, 0.12)
	b.add_theme_stylebox_override("normal", normal)
	b.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	b.add_theme_stylebox_override("pressed", ThemeColors.button_style(true))
	b.add_theme_color_override("font_color", ThemeColors.INK)
	parent.add_child(b)
	return b


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
	bar.custom_minimum_size = Vector2(120, 12)
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.75)
	var fill := StyleBoxFlat.new()
	fill.bg_color = colour
	bar.add_theme_stylebox_override("background", bg)
	bar.add_theme_stylebox_override("fill", fill)
	parent.add_child(bar)
	return bar


func _on_player_damaged(_amount: float, _from_side: float) -> void:
	_flash_level = 0.55


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
	if _race == null or _race.manager == null:
		return
	var manager: RaceManager = _race.manager
	var player: Rider = _race.player
	var track: Track = _race.track

	_speed_value.text = str(int(player.speed * 3.6))
	_position_label.text = "POS %d/%d" % [manager.position_of(player), manager.racers.size()]
	_weapon_label.text = String(CombatMath.WEAPON_NAMES[player.weapon])
	_stamina_bar.value = player.stamina / StaminaRules.MAX
	_bike_bar.value = player.health / 100.0

	var remaining := maxf(track.length - player.distance, 0.0)
	_distance_label.text = "%.1f KM" % (remaining / 1000.0)

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

	_flash_level = maxf(_flash_level - delta * 2.2, 0.0)
	_damage_flash.color.a = _flash_level


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
	if track == null:
		return
	var back_s := maxf(player.distance - 14.0, 0.0)
	var back_t := track.sample(back_s, player.lateral - 0.5, 1.35)
	if _mirror_l_cam != null:
		_mirror_l_cam.global_transform = back_t
		var look := track.sample(maxf(player.distance - 28.0, 0.0), player.lateral - 1.2, 0.8)
		_mirror_l_cam.look_at(look.origin, Vector3.UP)
	if _mirror_r_cam != null:
		var back_r := track.sample(back_s, player.lateral + 0.5, 1.35)
		_mirror_r_cam.global_transform = back_r
		var look_r := track.sample(maxf(player.distance - 28.0, 0.0), player.lateral + 1.2, 0.8)
		_mirror_r_cam.look_at(look_r.origin, Vector3.UP)
