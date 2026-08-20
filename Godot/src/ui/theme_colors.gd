class_name ThemeColors
## Road Rash palette, one place: warm rank gold, asphalt panels, danger red.

const ACCENT := Color("ffc53d")
const ACCENT_DIM := Color("d4a017")
const DANGER := Color("ff3344")
const INK := Color("f5f5f0")
const INK_MUTED := Color("b8b8a8")
const PANEL := Color(0.031, 0.047, 0.071, 0.88)
const POLICE_BLUE := Color("2f80ff")


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
