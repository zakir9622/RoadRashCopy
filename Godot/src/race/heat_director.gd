class_name HeatDirector
extends RefCounted
## Road Rash heat: fighting and reckless riding attract police from behind.
## Clean riding cools it off. Pure logic — unit-testable without a scene tree.

var heat: float = 0.0

const DECAY_PER_SEC := 0.035
const PUNCH_HEAT := 0.08
const KICK_HEAT := 0.05
const NEAR_MISS_HEAT := 0.04
const CRASH_HEAT := 0.15
const IDLE_HEAT := 0.22
const SPAWN_THRESHOLD := 0.45
const REINFORCE_THRESHOLD := 0.75


func tick(delta: float) -> void:
	heat = maxf(heat - DECAY_PER_SEC * delta, 0.0)


func on_punch() -> void:
	heat = minf(heat + PUNCH_HEAT, 1.0)


func on_kick() -> void:
	heat = minf(heat + KICK_HEAT, 1.0)


func on_near_miss() -> void:
	heat = minf(heat + NEAR_MISS_HEAT, 1.0)


func on_crash() -> void:
	heat = minf(heat + CRASH_HEAT, 1.0)


func on_idle() -> void:
	# Classic: sit still long enough and a motor officer appears.
	heat = minf(heat + IDLE_HEAT, 1.0)


func should_spawn_cop(active_cops: int, max_cops: int) -> bool:
	return heat >= SPAWN_THRESHOLD and active_cops < max_cops


func should_spawn_reinforcement(active_cops: int, max_cops: int) -> bool:
	return heat >= REINFORCE_THRESHOLD and active_cops < max_cops
