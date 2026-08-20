class_name ThemeColors
## Road Rash palette, one place: warm rank gold, asphalt panels, danger red.

const ACCENT := Color("ffc53d")
const ACCENT_DIM := Color("d4a017")
const DANGER := Color("ff3344")
const INK := Color("f5f5f0")
const INK_MUTED := Color("b8b8a8")
const PANEL := Color(0.031, 0.047, 0.071, 0.88)
const POLICE_BLUE := Color("2f80ff")


## Anchors a code-built control to a screen corner or edge CORRECTLY: presets
## only move the origin, so without matching grow directions a bottom/right
## panel expands off-screen as its content sizes itself.
static func place(control: Control, preset: int, margin: int = 24) -> void:
	control.set_anchors_and_offsets_preset(preset, Control.PRESET_MODE_MINSIZE, margin)
	match preset:
		Control.PRESET_BOTTOM_WIDE:
			control.grow_vertical = Control.GROW_DIRECTION_BEGIN
			control.grow_horizontal = Control.GROW_DIRECTION_BOTH
		Control.PRESET_TOP_WIDE:
			control.grow_horizontal = Control.GROW_DIRECTION_BOTH
		Control.PRESET_TOP_RIGHT:
			control.grow_horizontal = Control.GROW_DIRECTION_BEGIN
		Control.PRESET_BOTTOM_LEFT:
			control.grow_vertical = Control.GROW_DIRECTION_BEGIN
		Control.PRESET_BOTTOM_RIGHT:
			control.grow_horizontal = Control.GROW_DIRECTION_BEGIN
			control.grow_vertical = Control.GROW_DIRECTION_BEGIN
		Control.PRESET_CENTER_TOP:
			control.grow_horizontal = Control.GROW_DIRECTION_BOTH
		Control.PRESET_CENTER_BOTTOM:
			control.grow_horizontal = Control.GROW_DIRECTION_BOTH
			control.grow_vertical = Control.GROW_DIRECTION_BEGIN
		Control.PRESET_CENTER_LEFT:
			control.grow_vertical = Control.GROW_DIRECTION_BOTH
		Control.PRESET_CENTER_RIGHT:
			control.grow_horizontal = Control.GROW_DIRECTION_BEGIN
			control.grow_vertical = Control.GROW_DIRECTION_BOTH


## Full-rect CenterContainer: the only reliable way to truly centre a
## dynamically sized panel built from code.
static func center_wrap(parent: Node) -> CenterContainer:
	var wrapper := CenterContainer.new()
	wrapper.set_anchors_preset(Control.PRESET_FULL_RECT)
	parent.add_child(wrapper)
	return wrapper


static func styled_panel() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = PANEL
	style.border_width_left = 4
	style.border_color = ACCENT
	style.content_margin_left = 24.0
	style.content_margin_right = 24.0
	style.content_margin_top = 18.0
	style.content_margin_bottom = 18.0
	return style


static func button_style(hover: bool = false) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(1, 1, 1, 0.14) if hover else Color(1, 1, 1, 0.08)
	style.border_width_bottom = 2
	style.border_color = ACCENT if hover else ACCENT_DIM
	style.content_margin_left = 28.0
	style.content_margin_right = 28.0
	style.content_margin_top = 12.0
	style.content_margin_bottom = 12.0
	return style
