class_name Campaign
## Road Rash Big Game: 5 divisions × 5 events = 25 races. Finish 4th+ to advance.
## Four gangs from Road Rash 3D rotate through divisions.

const REQUIRED_POSITION := 4
const EVENTS_PER_DIVISION := 5
const DIVISION_COUNT := 5

const GANGS := ["Desades", "Dewleys", "Kaffe Boys", "Techgeists"]

const GANG_COLORS := {
	"Desades": Color(0.85, 0.12, 0.15),
	"Dewleys": Color(0.15, 0.55, 0.22),
	"Kaffe Boys": Color(0.92, 0.72, 0.08),
	"Techgeists": Color(0.35, 0.55, 0.95),
}

const DIVISION_NAMES := ["Rookie", "Amateur", "Pro", "Expert", "Champion"]

const ROSTER := [
	{"id": "biff", "name": "Biff", "skill": 0.80, "aggression": 1.6, "weapon": CombatMath.Weapon.BAT, "gang": "Desades",
		"banter": "You're roadkill, rookie."},
	{"id": "viper", "name": "Viper", "skill": 0.97, "aggression": 0.7, "weapon": CombatMath.Weapon.FISTS, "gang": "Desades",
		"banter": "Too slow. Always too slow."},
	{"id": "natasha", "name": "Natasha", "skill": 0.90, "aggression": 1.2, "weapon": CombatMath.Weapon.CHAIN, "gang": "Dewleys",
		"banter": "Chain's coming for your teeth."},
	{"id": "axle", "name": "Axle", "skill": 0.84, "aggression": 1.5, "weapon": CombatMath.Weapon.CHAIN, "gang": "Dewleys",
		"banter": "Eat asphalt."},
	{"id": "slater", "name": "Slater", "skill": 0.93, "aggression": 0.9, "weapon": CombatMath.Weapon.BAT, "gang": "Kaffe Boys",
		"banter": "Bat time."},
	{"id": "diego", "name": "Diego", "skill": 0.82, "aggression": 1.4, "weapon": CombatMath.Weapon.FISTS, "gang": "Kaffe Boys",
		"banter": "I'll kick you into traffic."},
	{"id": "franco", "name": "Franco", "skill": 0.88, "aggression": 1.1, "weapon": CombatMath.Weapon.BAT, "gang": "Techgeists",
		"banter": "Techgeists don't lose."},
	{"id": "ivan", "name": "Ivan", "skill": 0.95, "aggression": 0.8, "weapon": CombatMath.Weapon.CHAIN, "gang": "Techgeists",
		"banter": "You're in my lane."},
	{"id": "miles", "name": "Miles", "skill": 0.86, "aggression": 1.3, "weapon": CombatMath.Weapon.CHAIN, "gang": "Desades",
		"banter": "Hope you packed bandages."},
	{"id": "razor", "name": "Razor", "skill": 0.89, "aggression": 1.5, "weapon": CombatMath.Weapon.BAT, "gang": "Dewleys",
		"banter": "Razor's gonna slice you up."},
	{"id": "tank", "name": "Tank", "skill": 0.83, "aggression": 1.7, "weapon": CombatMath.Weapon.CHAIN, "gang": "Kaffe Boys",
		"banter": "Get outta my way!"},
	{"id": "spike", "name": "Spike", "skill": 0.94, "aggression": 0.9, "weapon": CombatMath.Weapon.FISTS, "gang": "Techgeists",
		"banter": "Spike's gonna stick it to you."},
	{"id": "crow", "name": "Crow", "skill": 0.91, "aggression": 1.2, "weapon": CombatMath.Weapon.FISTS, "gang": "Desades",
		"banter": "Crow picks the bones clean."},
]


static func total_events() -> int:
	return DIVISION_COUNT * EVENTS_PER_DIVISION


static func event_at(chapter_index: int) -> Dictionary:
	var idx := clampi(chapter_index, 0, total_events() - 1)
	var division := idx / EVENTS_PER_DIVISION
	var event := idx % EVENTS_PER_DIVISION
	var track := TrackCatalog.at(event)
	var gang: String = GANGS[division % GANGS.size()]
	return {
		"title": "Division %d — %s  ·  Event %d" % [division + 1, DIVISION_NAMES[division], event + 1],
		"track": String(track["id"]),
		"division": division,
		"event": event,
		"gang": gang,
		"skill_scale": 1.0 + division * 0.07 + event * 0.02,
		"intro": _intro_for(division, event, gang, String(track["name"])),
	}


static func chapters() -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	for i in total_events():
		out.append(event_at(i))
	return out


static func _intro_for(division: int, event: int, gang: String, track_name: String) -> String:
	var lines := [
		"%s territory on %s. The %s want this road." % [gang, track_name, gang],
		"Win or walk — Division %s doesn't forgive back markers." % DIVISION_NAMES[division],
		"Event %d of 5. Finish top %d or it's over." % [event + 1, REQUIRED_POSITION],
	]
	return " ".join(lines)


static func banter_for(rider_id: String) -> String:
	for profile in ROSTER:
		if String(profile["id"]) == rider_id:
			return String(profile.get("banter", "See you in the gravel."))
	return "Eat my exhaust."


## Applies one race result to a save dictionary. Returns a summary the results
## screen renders verbatim. Pure: no I/O, no scene access, fully testable.
static func apply_result(save: Dictionary, position: int, knockouts: int,
		purse: int, busted: bool, repair_bill: int) -> Dictionary:
	var won := position > 0 and position <= REQUIRED_POSITION and not busted
	var prize := 0
	if won:
		var shares := {1: 1.0, 2: 0.65, 3: 0.45, 4: 0.3}
		prize = int(purse * float(shares.get(position, 0.0)))
	var combat_bonus := knockouts * 150
	var fine := 400 if busted else 0
	var net := prize + combat_bonus - repair_bill - fine

	save["cash"] = int(save.get("cash", 0)) + net
	if won:
		save["chapter"] = mini(int(save.get("chapter", 0)) + 1, total_events())
	save["races"] = int(save.get("races", 0)) + 1

	return {
		"won": won, "position": position, "prize": prize,
		"knockouts": knockouts, "combat_bonus": combat_bonus,
		"repair_bill": repair_bill, "fine": fine, "net": net,
		"balance": save["cash"], "busted": busted,
	}
