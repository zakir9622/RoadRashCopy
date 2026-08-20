extends CanvasLayer
## Road Rash dashboard HUD: diegetic bottom instrument cluster, dual rear
## mirrors, clear centre view, and bottom-thumb touch controls on mobile.

const DASH_H := 0.0
const MIRROR_W := 96.0
const MIRROR_H := 52.0
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
var _nitro_bar: ProgressBar
var _speed_gauge: Control
var _rpm_gauge: Control
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
	# Thin top strip — never covers the road. Analog cluster sits over the bars.
	_position_label = _label(root, "POS 15/15", 18, ThemeColors.ACCENT)
	ThemeColors.place(_position_label, Control.PRESET_CENTER_TOP, 10)
	_position_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_police_label = _label(root, "", 15, ThemeColors.POLICE_BLUE)
	ThemeColors.place(_police_label, Control.PRESET_CENTER_TOP, 32)
	_police_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_ko_label = _label(root, "KO 0", 14, ThemeColors.ACCENT)
	ThemeColors.place(_ko_label, Control.PRESET_TOP_RIGHT, 12)

	_weapon_label = _label(root, "FISTS", 14, ThemeColors.INK)
	ThemeColors.place(_weapon_label, Control.PRESET_TOP_LEFT, 12)

	_rival_name = _label(root, "", 14, ThemeColors.INK_MUTED)
	ThemeColors.place(_rival_name, Control.PRESET_TOP_WIDE, 52)
	_rival_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_banter_label = _label(root, "", 16, ThemeColors.ACCENT)
	_banter_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ThemeColors.place(_banter_label, Control.PRESET_TOP_WIDE, 72)

	_rival_portrait = PanelContainer.new()
	_rival_portrait.visible = false
	root.add_child(_rival_portrait)

	var cluster := HBoxContainer.new()
	cluster.add_theme_constant_override("separation", 28)
	root.add_child(cluster)
	ThemeColors.place(cluster, Control.PRESET_CENTER_BOTTOM, 18)

	_rpm_gauge = _make_gauge(cluster, "RPM")
	var mid := VBoxContainer.new()
	mid.add_theme_constant_override("separation", 4)
	cluster.add_child(mid)
	_speed_gauge = _make_gauge(mid, "MPH")
	_speed_value = _label(mid, "0", 22, ThemeColors.INK)
	_speed_value.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_distance_label = _label(mid, "", 12, ThemeColors.INK_MUTED)
	_distance_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	var bars := VBoxContainer.new()
	bars.add_theme_constant_override("separation", 3)
	cluster.add_child(bars)
	_stamina_bar = _bar(bars, ThemeColors.ACCENT)
	_stamina_bar.custom_minimum_size = Vector2(92, 8)
	_bike_bar = _bar(bars, ThemeColors.DANGER)
	_bike_bar.custom_minimum_size = Vector2(92, 8)
	_nitro_bar = _bar(bars, ThemeColors.POLICE_BLUE)
	_nitro_bar.custom_minimum_size = Vector2(92, 8)
	_rival_bar = _bar(bars, Color(0.4, 0.7, 1.0, 0.8))
	_rival_bar.custom_minimum_size = Vector2(92, 6)


func _make_gauge(parent: Control, caption: String) -> Control:
	var g := Control.new()
	g.custom_minimum_size = Vector2(108, 108)
	g.set_meta("value", 0.0)
	g.set_meta("caption", caption)
	g.draw.connect(func():
		var c := g.size * 0.5
		var r := minf(g.size.x, g.size.y) * 0.42
		g.draw_arc(c, r, PI * 0.75, PI * 2.25, 28, Color(0, 0, 0, 0.45), 10.0, true)
		g.draw_arc(c, r, PI * 0.75, PI * 2.25, 28, ThemeColors.ACCENT_DIM, 2.0, true)
		var t := clampf(float(g.get_meta("value")), 0.0, 1.0)
		var ang := lerpf(PI * 0.75, PI * 2.25, t)
		var needle := c + Vector2(cos(ang), sin(ang)) * r * 0.82
		g.draw_line(c, needle, ThemeColors.ACCENT, 3.0, true)
		g.draw_circle(c, 4.0, ThemeColors.INK)
	)
	parent.add_child(g)
	var cap := Label.new()
	cap.text = caption
	cap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	cap.add_theme_font_size_override("font_size", 11)
	cap.add_theme_color_override("font_color", ThemeColors.INK_MUTED)
	cap.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	cap.offset_top = -16
	g.add_child(cap)
	return g


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

	# Left: translucent steer pad. Right: throttle + compact punches.
	var steer := _ghost_pad(_touch_root, Vector2(132, 132))
	ThemeColors.place(steer, Control.PRESET_BOTTOM_LEFT, 20)
	steer.gui_input.connect(func(event: InputEvent) -> void:
		if event is InputEventScreenTouch or event is InputEventScreenDrag or event is InputEventMouseButton or event is InputEventMouseMotion:
			var local := steer.get_local_mouse_position()
			var x := clampf((local.x / maxf(steer.size.x, 1.0)) * 2.0 - 1.0, -1.0, 1.0)
			if event is InputEventScreenTouch and not event.pressed:
				controller.touch_steer = 0.0
			elif event is InputEventMouseButton and not event.pressed:
				controller.touch_steer = 0.0
			else:
				controller.touch_steer = x
	)

	var throttle := _ghost_pad(_touch_root, Vector2(120, 150))
	ThemeColors.place(throttle, Control.PRESET_BOTTOM_RIGHT, 20)
	var go_lbl := Label.new()
	go_lbl.text = "GAS"
	go_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	go_lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	go_lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
	go_lbl.add_theme_font_size_override("font_size", 16)
	go_lbl.add_theme_color_override("font_color", Color(1, 1, 1, 0.55))
	throttle.add_child(go_lbl)
	throttle.gui_input.connect(func(event: InputEvent) -> void:
		var down := false
		if event is InputEventScreenTouch:
			down = event.pressed
			controller.touch_throttle = 1.0 if down else 0.0
		elif event is InputEventMouseButton:
			down = event.pressed
			controller.touch_throttle = 1.0 if down else 0.0
	)

	var punches := HBoxContainer.new()
	punches.add_theme_constant_override("separation", 8)
	_touch_root.add_child(punches)
	ThemeColors.place(punches, Control.PRESET_BOTTOM_RIGHT, 24)
	punches.offset_bottom = -180
	punches.offset_top = punches.offset_bottom - 56
	punches.offset_right = -20
	punches.offset_left = -220

	var brake := _ghost_btn(punches, "BRK")
	brake.button_down.connect(func(): controller.touch_brake = 1.0)
	brake.button_up.connect(func(): controller.touch_brake = 0.0)
	var punch_l := _ghost_btn(punches, "L")
	punch_l.pressed.connect(func(): controller.touch_attack_left = true)
	var punch_r := _ghost_btn(punches, "R")
	punch_r.pressed.connect(func(): controller.touch_attack_right = true)
	var kick := _ghost_btn(punches, "K")
	kick.pressed.connect(func(): controller.touch_kick = true)
	var nitro := _ghost_btn(punches, "N")
	nitro.button_down.connect(func(): controller.touch_nitro = true)
	nitro.button_up.connect(func(): controller.touch_nitro = false)


func _ghost_pad(parent: Control, size: Vector2) -> Control:
	var p := Panel.new()
	p.custom_minimum_size = size
	p.mouse_filter = Control.MOUSE_FILTER_STOP
	var st := StyleBoxFlat.new()
	st.bg_color = Color(1, 1, 1, 0.08)
	st.set_corner_radius_all(int(minf(size.x, size.y) * 0.5))
	st.border_color = Color(1, 1, 1, 0.18)
	st.set_border_width_all(1)
	p.add_theme_stylebox_override("panel", st)
	parent.add_child(p)
	return p


func _ghost_btn(parent: Control, text: String) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(52, 52)
	b.add_theme_font_size_override("font_size", 15)
	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(1, 1, 1, 0.10)
	normal.set_corner_radius_all(26)
	normal.set_border_width_all(1)
	normal.border_color = Color(1, 1, 1, 0.22)
	b.add_theme_stylebox_override("normal", normal)
	b.add_theme_stylebox_override("pressed", ThemeColors.button_style(true))
	b.add_theme_color_override("font_color", Color(1, 1, 1, 0.7))
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
	if _race == null or _race.manager == null or _race.player == null or _race.track == null:
		return
	var manager: RaceManager = _race.manager
	var player: Rider = _race.player
	var track: Track = _race.track

	_speed_value.text = str(int(TrackCatalog.to_mph(player.speed)))
	if _speed_gauge != null:
		_speed_gauge.set_meta("value", clampf(TrackCatalog.to_mph(player.speed) / 150.0, 0.0, 1.0))
		_speed_gauge.queue_redraw()
	if _rpm_gauge != null:
		_rpm_gauge.set_meta("value", clampf(player.speed / maxf(player.top_speed, 1.0), 0.0, 1.0))
		_rpm_gauge.queue_redraw()
	_position_label.text = "POS %d/%d" % [manager.position_of(player), manager.racers.size()]
	_weapon_label.text = String(CombatMath.WEAPON_NAMES.get(player.weapon, "FISTS"))
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
		View.look_at(_mirror_l_cam, look.origin)
	if _mirror_r_cam != null:
		var back_r := track.sample(back_s, player.lateral + 0.5, 1.35)
		_mirror_r_cam.global_transform = back_r
		var look_r := track.sample(maxf(player.distance - 28.0, 0.0), player.lateral + 1.2, 0.8)
		View.look_at(_mirror_r_cam, look_r.origin)
