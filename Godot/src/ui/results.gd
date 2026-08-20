extends CanvasLayer
## Road Rash rank sheet plus post-race vignette (rival / cop / championship).


func present(summary: Dictionary) -> void:
	var scrim := ColorRect.new()
	scrim.color = Color(0, 0, 0, 0.78)
	scrim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(scrim)

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	ThemeColors.center_wrap(scrim).add_child(panel)

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	box.custom_minimum_size = Vector2(420, 0)
	panel.add_child(box)

	var rank := Label.new()
	var place := int(summary.get("position", 0))
	rank.text = str(place) if place > 0 else "-"
	rank.add_theme_font_size_override("font_size", 84)
	rank.add_theme_color_override("font_color", ThemeColors.ACCENT)
	rank.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(rank)

	var headline := Label.new()
	if bool(summary.get("game_over", false)):
		headline.text = "GAME OVER"
		headline.add_theme_color_override("font_color", ThemeColors.DANGER)
	elif bool(summary.get("champion", false)):
		headline.text = "CHAMPION"
		headline.add_theme_color_override("font_color", ThemeColors.ACCENT)
	elif bool(summary.get("busted", false)):
		headline.text = "BUSTED"
		headline.add_theme_color_override("font_color", ThemeColors.POLICE_BLUE)
	elif bool(summary.get("won", false)):
		headline.text = "EVENT WON"
		headline.add_theme_color_override("font_color", ThemeColors.INK)
	else:
		headline.text = "OUTSIDE THE MONEY"
		headline.add_theme_color_override("font_color", ThemeColors.INK_MUTED)
	headline.add_theme_font_size_override("font_size", 24)
	headline.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(headline)

	_row(box, "RACE PRIZE", "$%d" % int(summary.get("prize", 0)), ThemeColors.INK)
	_row(box, "KNOCKOUTS (%d)" % int(summary.get("knockouts", 0)),
		"+$%d" % int(summary.get("combat_bonus", 0)), ThemeColors.INK)
	_row(box, "REPAIRS", "-$%d" % int(summary.get("repair_bill", 0)), ThemeColors.DANGER)
	if int(summary.get("fine", 0)) > 0:
		_row(box, "POLICE FINE", "-$%d" % int(summary.get("fine", 0)), ThemeColors.POLICE_BLUE)
	_row(box, "BALANCE", "$%d" % int(summary.get("balance", 0)), ThemeColors.ACCENT, 24)

	var state := get_node_or_null("/root/GameState")
	if state == null:
		get_tree().paused = false
		return
	var beat: Dictionary = Story.vignette(state.save, summary)
	var speaker := Label.new()
	speaker.text = String(beat.get("speaker", "")).to_upper()
	speaker.add_theme_font_size_override("font_size", 13)
	speaker.add_theme_color_override("font_color", ThemeColors.ACCENT)
	box.add_child(speaker)
	var body := Label.new()
	body.text = String(beat.get("body", ""))
	body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	body.custom_minimum_size = Vector2(380, 0)
	body.add_theme_font_size_override("font_size", 16)
	body.add_theme_color_override("font_color", ThemeColors.INK)
	box.add_child(body)

	var button := Button.new()
	if bool(summary.get("game_over", false)):
		button.text = "NEW CAREER"
		button.pressed.connect(func():
			state.reset_career()
			Sfx.play("click", -8.0)
			get_tree().change_scene_to_file("res://src/ui/MainMenu.tscn"))
	else:
		button.text = "CONTINUE"
		button.pressed.connect(func():
			Sfx.play("click", -8.0)
			get_tree().change_scene_to_file("res://src/ui/MainMenu.tscn"))
	button.add_theme_font_size_override("font_size", 22)
	button.add_theme_stylebox_override("normal", ThemeColors.button_style())
	button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	box.add_child(button)

	get_tree().paused = false


func _row(parent: Control, left: String, right: String, colour: Color, size: int = 19) -> void:
	var row := HBoxContainer.new()
	parent.add_child(row)
	var left_label := Label.new()
	left_label.text = left
	left_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	left_label.add_theme_font_size_override("font_size", size)
	left_label.add_theme_color_override("font_color", ThemeColors.INK_MUTED)
	row.add_child(left_label)
	var right_label := Label.new()
	right_label.text = right
	right_label.add_theme_font_size_override("font_size", size)
	right_label.add_theme_color_override("font_color", colour)
	row.add_child(right_label)
