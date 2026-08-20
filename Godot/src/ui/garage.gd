extends Control
## Garage: buy/select bikes, buy engine and tire stages. Prices from BikeSpecs,
## money from the save — spending here changes the physics next race.

var _cash_label: Label
var _list: VBoxContainer


func _ready() -> void:
	var background := ColorRect.new()
	background.color = Color(0.03, 0.04, 0.06)
	background.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", ThemeColors.styled_panel())
	ThemeColors.center_wrap(self).add_child(panel)

	_list = VBoxContainer.new()
	_list.add_theme_constant_override("separation", 10)
	_list.custom_minimum_size = Vector2(560, 0)
	panel.add_child(_list)

	_rebuild()


func _rebuild() -> void:
	for child in _list.get_children():
		child.queue_free()

	var title := Label.new()
	title.text = "GARAGE"
	title.add_theme_font_size_override("font_size", 36)
	title.add_theme_color_override("font_color", ThemeColors.ACCENT)
	_list.add_child(title)

	_cash_label = Label.new()
	_cash_label.text = "CASH  $%d" % int(GameState.save.get("cash", 0))
	_cash_label.add_theme_font_size_override("font_size", 20)
	_cash_label.add_theme_color_override("font_color", ThemeColors.INK)
	_list.add_child(_cash_label)

	var owned: Array = GameState.save.get("owned_bikes", ["rat"])
	var current := String(GameState.save.get("bike", "rat"))

	for bike in BikeSpecs.all():
		var id := String(bike["id"])
		var row := HBoxContainer.new()
		_list.add_child(row)

		var name_label := Label.new()
		name_label.text = "%s  ·  %d km/h" % [String(bike["name"]), int(float(bike["top_speed"]) * 3.6)]
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_label.add_theme_font_size_override("font_size", 20)
		name_label.add_theme_color_override("font_color",
			ThemeColors.ACCENT if id == current else ThemeColors.INK)
		row.add_child(name_label)

		var button := Button.new()
		if id == current:
			button.text = "RIDING"
			button.disabled = true
		elif owned.has(id):
			button.text = "RIDE"
			button.pressed.connect(func():
				GameState.save["bike"] = id
				GameState.persist()
				_rebuild())
		else:
			button.text = "BUY $%d" % int(bike["price"])
			button.disabled = int(GameState.save.get("cash", 0)) < int(bike["price"])
			button.pressed.connect(func():
				GameState.save["cash"] = int(GameState.save["cash"]) - int(bike["price"])
				(GameState.save["owned_bikes"] as Array).append(id)
				GameState.save["bike"] = id
				GameState.persist()
				AudioDirector.play("pickup", -6.0)
				_rebuild())
		button.add_theme_stylebox_override("normal", ThemeColors.button_style())
		button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
		row.add_child(button)

	_upgrade_row("ENGINE", "engine_stage")
	_upgrade_row("TIRES", "tire_stage")

	var back := Button.new()
	back.text = "BACK"
	back.add_theme_font_size_override("font_size", 20)
	back.add_theme_stylebox_override("normal", ThemeColors.button_style())
	back.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	back.pressed.connect(func(): get_tree().change_scene_to_file("res://src/ui/MainMenu.tscn"))
	_list.add_child(back)


func _upgrade_row(label_text: String, key: String) -> void:
	var stage := int(GameState.save.get(key, 0))
	var row := HBoxContainer.new()
	_list.add_child(row)

	var label := Label.new()
	label.text = "%s  STAGE %d/5" % [label_text, stage]
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_size_override("font_size", 20)
	label.add_theme_color_override("font_color", ThemeColors.INK)
	row.add_child(label)

	var button := Button.new()
	if stage >= 5:
		button.text = "MAX"
		button.disabled = true
	else:
		var cost := BikeSpecs.upgrade_cost(stage)
		button.text = "UPGRADE $%d" % cost
		button.disabled = int(GameState.save.get("cash", 0)) < cost
		button.pressed.connect(func():
			GameState.save["cash"] = int(GameState.save["cash"]) - cost
			GameState.save[key] = stage + 1
			GameState.persist()
			AudioDirector.play("pickup", -6.0)
			_rebuild())
	button.add_theme_stylebox_override("normal", ThemeColors.button_style())
	button.add_theme_stylebox_override("hover", ThemeColors.button_style(true))
	row.add_child(button)
