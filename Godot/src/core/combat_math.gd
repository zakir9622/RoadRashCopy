class_name CombatMath
## All combat balance in one testable place: damage, reach, cooldowns, knockback.
## Weapons follow the classic ladder — fists free, chain fast, bat heavy, kick shoves.

enum Weapon { FISTS, CHAIN, BAT, KICK }

const WEAPON_NAMES := {
	Weapon.FISTS: "FISTS",
	Weapon.CHAIN: "CHAIN",
	Weapon.BAT: "BASEBALL BAT",
	Weapon.KICK: "KICK",
}


static func base_damage(weapon: int) -> float:
	match weapon:
		Weapon.CHAIN: return 14.0
		Weapon.BAT: return 20.0
		Weapon.KICK: return 8.0
		_: return 10.0


static func reach(weapon: int) -> float:
	match weapon:
		Weapon.CHAIN: return 2.6
		Weapon.BAT: return 2.2
		Weapon.KICK: return 1.8
		_: return 1.9


static func cooldown(weapon: int) -> float:
	match weapon:
		Weapon.CHAIN: return 0.55
		Weapon.BAT: return 0.85
		Weapon.KICK: return 0.70
		_: return 0.45


## Relative speed matters: two riders locked side by side are stationary with
## respect to each other, and a punch should still feel like a punch.
static func compute_damage(weapon: int, relative_speed: float, aggression: float = 1.0) -> float:
	var speed_bonus: float = clampf(absf(relative_speed) * 0.35, 0.0, 12.0)
	return (base_damage(weapon) + speed_bonus) * maxf(aggression, 0.0)


## Lateral shove in metres/sec applied to the victim. Kicks trade damage for push.
static func knockback(weapon: int, damage: float) -> float:
	var scale := 8.0 if weapon == Weapon.KICK else 2.5
	return damage * scale * 0.05


## Whether a hit this hard knocks the victim's weapon loose.
static func can_steal(damage: float, victim_weapon: int) -> bool:
	return victim_weapon != Weapon.FISTS and damage >= 16.0


static func better(a: int, b: int) -> int:
	# Bat > chain > fists. Kick is a move, not a holdable, and is never "held".
	var rank := {Weapon.FISTS: 0, Weapon.KICK: 0, Weapon.CHAIN: 1, Weapon.BAT: 2}
	return a if int(rank[a]) >= int(rank[b]) else b
