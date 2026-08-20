class_name Campaign
## The authored spine of career mode. Classic Road Rash rule: finish 4th or
## better to bank the event; win every event in a chapter to advance.

const REQUIRED_POSITION := 4

const ROSTER := [
	{"id": "biff", "name": "Biff", "skill": 0.80, "aggression": 1.6, "weapon": CombatMath.Weapon.BAT},
	{"id": "viper", "name": "Viper", "skill": 0.97, "aggression": 0.7, "weapon": CombatMath.Weapon.FISTS},
	{"id": "natasha", "name": "Natasha", "skill": 0.90, "aggression": 1.2, "weapon": CombatMath.Weapon.CHAIN},
	{"id": "axle", "name": "Axle", "skill": 0.84, "aggression": 1.5, "weapon": CombatMath.Weapon.CHAIN},
	{"id": "slater", "name": "Slater", "skill": 0.93, "aggression": 0.9, "weapon": CombatMath.Weapon.BAT},
	{"id": "diego", "name": "Diego", "skill": 0.82, "aggression": 1.4, "weapon": CombatMath.Weapon.FISTS},
	{"id": "franco", "name": "Franco", "skill": 0.88, "aggression": 1.1, "weapon": CombatMath.Weapon.BAT},
	{"id": "ivan", "name": "Ivan", "skill": 0.95, "aggression": 0.8, "weapon": CombatMath.Weapon.CHAIN},
	{"id": "miles", "name": "Miles", "skill": 0.86, "aggression": 1.3, "weapon": CombatMath.Weapon.CHAIN},
	{"id": "razor", "name": "Razor", "skill": 0.89, "aggression": 1.5, "weapon": CombatMath.Weapon.BAT},
	{"id": "tank", "name": "Tank", "skill": 0.83, "aggression": 1.7, "weapon": CombatMath.Weapon.CHAIN},
	{"id": "spike", "name": "Spike", "skill": 0.94, "aggression": 0.9, "weapon": CombatMath.Weapon.FISTS},
	{"id": "crow", "name": "Crow", "skill": 0.91, "aggression": 1.2, "weapon": CombatMath.Weapon.FISTS},
]


static func chapters() -> Array[Dictionary]:
	return [
		{"title": "Chapter 1 — The Coast", "track": "coast_run",
		 "intro": "New bike, old debts. Win on the coast road and the crew starts taking you seriously."},
		{"title": "Chapter 2 — Heat Mirage", "track": "palm_desert",
		 "intro": "The desert doesn't forgive mistakes. Neither does Viper."},
		{"title": "Chapter 3 — Concrete Canyon", "track": "downtown",
		 "intro": "Racing through downtown at rush hour. The cops know your name now."},
		{"title": "Chapter 4 — Thin Air", "track": "sierra_pass",
		 "intro": "Hairpins at altitude. One mistake is a long way down."},
		{"title": "Chapter 5 — Blackout", "track": "night_city",
		 "intro": "Last race. Everyone you ever knocked down is on this grid."},
	]


## Applies one race result to a save dictionary. Returns a summary the results
## screen renders verbatim. Pure: no I/O, no scene access, fully testable.
static func apply_result(save: Dictionary, position: int, knockouts: int,
		purse: int, busted: bool, repair_bill: int) -> Dictionary:
	var won := position > 0 and position <= REQUIRED_POSITION and not busted
	var prize := 0
	if won:
		# Winner takes the purse; lower qualifying spots take a share.
		var shares := {1: 1.0, 2: 0.65, 3: 0.45, 4: 0.3}
		prize = int(purse * float(shares.get(position, 0.0)))
	var combat_bonus := knockouts * 150
	var fine := 400 if busted else 0
	var net := prize + combat_bonus - repair_bill - fine

	save["cash"] = int(save.get("cash", 0)) + net
	if won:
		save["chapter"] = int(save.get("chapter", 0)) + 1
	save["races"] = int(save.get("races", 0)) + 1

	return {
		"won": won, "position": position, "prize": prize,
		"knockouts": knockouts, "combat_bonus": combat_bonus,
		"repair_bill": repair_bill, "fine": fine, "net": net,
		"balance": save["cash"], "busted": busted,
	}
