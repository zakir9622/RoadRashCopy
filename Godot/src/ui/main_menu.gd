extends Control
## Title screen: Campaign / Quick Race / Garage / Quit, over a slow cinematic
## pan of the coast track — the game IS the background, no static art needed.

var _menu_box: VBoxContainer


func _ready() -> void:
	_build_backdrop()
	_build_menu()
	AudioDirector.play_music()


func _build_backdrop() -> void:
	# Live 3D backdrop: the coast track with a drifting camera.
	var viewport_container := SubViewportContainer.new()
	viewport_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	viewport_container.stretch = true
	viewport_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(viewport_container)

	var viewport := SubViewport.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport_container.add_child(viewport)

	var world := Node3D.new()
	viewport.add_child(world)

	var track := Track.new()
	world.add_child(track)
	track.build(TrackCatalog.find("coast_run"))

	var env := Environment.new()
	var sky_path := "res://assets/sky/kloppenheim_02_puresky_hdri.hdr"
	if ResourceLoader.exists(sky_path):
		var sky_mat := PanoramaSkyMaterial.new()
		sky_mat.panorama = load(sky_path)
		var sky := Sky.new()
		sky.sky_material = sky_mat
		env.background_mode = Environment.BG_SKY
		env.sky = sky
		env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.glow_enabled = true
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	world.add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-35, 50, 0)
	sun.light_color = Color(1.0, 0.9, 0.75)
	world.add_child(sun)

	var camera := Camera3D.new()
	camera.fov = 55.0
	world.add_child(camera)
	camera.make_current()

	# Slow dolly down the road itself, low over the tarmac like a title shot.
	var tween := create_tween().set_loops()
	camera.global_position = track.sample(80.0, 0.0, 3.0).origin
	var drift := func(progress: float) -> void:
		var s := lerpf(80.0, 600.0, progress)
		var t := track.sample(s, sin(progress * TAU) * 3.0, 3.0)
		camera.global_position = t.origin
		var ahead := track.sample(s + 55.0, 0.0, 0.5)
		camera.look_at(ahead.origin, Vector3.UP)
	tween.tween_method(drift, 0.0, 1.0, 40.0)
	tween.tween_method(drift, 1.0, 0.0, 40.0)


func _build_menu() -> void:
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	add_child(panel)
	ThemeColors.place(panel, Control.PRESET_CENTER_LEFT, 80)

	_menu_box = VBoxContainer.new()
	_menu_box.add_theme_constant_override("separation", 10)
	panel.add_child(_menu_box)

	var title := Label.new()
	title.text = "HIGHWAY RENEGADE"
	title.add_theme_font_size_override("font_size", 42)
	title.add_theme_color_override("font_color", ThemeColors.INK)
	_menu_box.add_child(title)

	var tagline := Label.new()
	tagline.text = "ROAD RASH EDITION"
	tagline.add_theme_font_size_override("font_size", 18)
	tagline.add_theme_color_override("font_color", ThemeColors.ACCENT)
	_menu_box.add_child(tagline)

	var cash := Label.new()
	cash.text = "$%d   ·   CHAPTER %d" % [int(GameState.save.get("cash", 0)),
		int(GameState.save.get("chapter", 0)) + 1]
	cash.add_theme_font_size_override("font_size", 16)
	cash.add_theme_color_override("font_color", ThemeColors.INK_MUTED)
	_menu_box.add_child(cash)

	_menu_button("CAMPAIGN", _on_campaign)
	_menu_button("QUICK RACE", _on_quick_race)
	_menu_button("GARAGE", _on_garage)
	_menu_button("QUIT", _on_quit)


func _menu_button(text: String, handler: Callable) -> void:
	var button := Button.new()
	button.text = text
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.add_theme_font_size_override("font_size", 24)
	button.add_theme_color_override("font_color", ThemeColors.INK_MUTED)
	button.add_theme_color_override("font_hover_color", ThemeColors.ACCENT)
	button.add_theme_stylebox_override("normal", ThemeColors.button_style())
	button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	button.pressed.connect(func():
		AudioDirector.play("click", -8.0)
		handler.call())
	_menu_box.add_child(button)


func _on_campaign() -> void:
	var chapter := int(GameState.save.get("chapter", 0))
	if chapter >= Campaign.chapters().size():
		chapter = Campaign.chapters().size() - 1
	RaceContext.launch_campaign(chapter)
	_show_chapter_intro(chapter)


func _show_chapter_intro(chapter_index: int) -> void:
	var info: Dictionary = Campaign.chapters()[chapter_index]
	var scrim := ColorRect.new()
	scrim.color = Color(0, 0, 0, 0.8)
	scrim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(scrim)

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	ThemeColors.center_wrap(scrim).add_child(panel)

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 14)
	panel.add_child(box)

	var title := Label.new()
	title.text = String(info["title"])
	title.add_theme_font_size_override("font_size", 32)
	title.add_theme_color_override("font_color", ThemeColors.ACCENT)
	box.add_child(title)

	var intro := Label.new()
	intro.text = String(info["intro"])
	intro.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	intro.custom_minimum_size = Vector2(520, 0)
	intro.add_theme_font_size_override("font_size", 19)
	intro.add_theme_color_override("font_color", ThemeColors.INK)
	box.add_child(intro)

	var rule := Label.new()
	rule.text = "FINISH TOP %d TO ADVANCE" % Campaign.REQUIRED_POSITION
	rule.add_theme_font_size_override("font_size", 15)
	rule.add_theme_color_override("font_color", ThemeColors.POLICE_BLUE)
	box.add_child(rule)

	var ride := Button.new()
	ride.text = "RIDE"
	ride.add_theme_font_size_override("font_size", 24)
	ride.add_theme_stylebox_override("normal", ThemeColors.button_style())
	ride.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	ride.pressed.connect(func(): get_tree().change_scene_to_file("res://src/race/Race.tscn"))
	box.add_child(ride)


func _on_quick_race() -> void:
	var scrim := ColorRect.new()
	scrim.color = Color(0, 0, 0, 0.8)
	scrim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(scrim)

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	ThemeColors.center_wrap(scrim).add_child(panel)

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	panel.add_child(box)

	var title := Label.new()
	title.text = "QUICK RACE"
	title.add_theme_font_size_override("font_size", 30)
	title.add_theme_color_override("font_color", ThemeColors.ACCENT)
	box.add_child(title)

	# Tracks unlock with campaign progress; chapter N unlocks track N.
	var unlocked := int(GameState.save.get("chapter", 0)) + 1
	var tracks := TrackCatalog.all()
	for i in tracks.size():
		var track: Dictionary = tracks[i]
		var button := Button.new()
		var locked := i >= unlocked
		button.text = "%s%s  ·  %.1f km  ·  $%d" % [
			"LOCKED — " if locked else "", String(track["name"]),
			float(track["length"]) / 1000.0, int(track["purse"])]
		button.disabled = locked
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.add_theme_font_size_override("font_size", 20)
		button.add_theme_stylebox_override("normal", ThemeColors.button_style())
		button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
		var id := String(track["id"])
		button.pressed.connect(func():
			RaceContext.launch_quick_race(id)
			get_tree().change_scene_to_file("res://src/race/Race.tscn"))
		box.add_child(button)

	var back := Button.new()
	back.text = "BACK"
	back.add_theme_stylebox_override("normal", ThemeColors.button_style())
	back.pressed.connect(func(): scrim.queue_free())
	box.add_child(back)


func _on_garage() -> void:
	get_tree().change_scene_to_file("res://src/ui/Garage.tscn")


func _on_quit() -> void:
	# Stop audio and let the mix thread drain before quitting, so playback
	# objects are unregistered and the process exits without leak warnings.
	AudioDirector.stop_all()
	await get_tree().create_timer(0.2).timeout
	get_tree().quit()
