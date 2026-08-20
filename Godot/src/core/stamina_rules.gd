class_name StaminaRules
## Road Rash rider stamina: fighting and taking hits drain it; empty stamina
## makes crashes worse and remounts slower. Pure logic — unit-tested headless.

const MAX := 100.0
const RECOVER_PER_SEC := 8.0
const SWING_COST := 6.0
const KICK_COST := 10.0
const HIT_TAKEN_COST := 14.0
const EXHAUSTED_BELOW := 12.0


static func apply_swing(current: float) -> float:
	return clampf(current - SWING_COST, 0.0, MAX)


static func apply_kick(current: float) -> float:
	return clampf(current - KICK_COST, 0.0, MAX)


static func apply_hit(current: float) -> float:
	return clampf(current - HIT_TAKEN_COST, 0.0, MAX)


static func recover(current: float, delta: float) -> float:
	return clampf(current + RECOVER_PER_SEC * delta, 0.0, MAX)


static func is_exhausted(current: float) -> bool:
	return current <= EXHAUSTED_BELOW
