class_name Rider
extends Node3D
## One rider — player, rival or cop — moving in track space. States:
## RIDING / CRASHED / REMOUNT. Combat, stamina and health live here so the
## player and every AI share exactly one rulebook.

signal crashed(rider: Rider)
signal knocked_out(rider: Rider)
signal damaged(amount: float, from_side: float)

enum State { RIDING, CRASHED, REMOUNT }

var rider_name: String = "Rider"
var is_player: bool = false
var is_police: bool = false

var track: Track
var distance: float = 0.0
var lateral: float = 0.0
var speed: float = 0.0
var lean: float = 0.0

var top_speed: float = 42.0
var accel: float = 9.0
var handling: float = 1.0
var nitro_boost: float = 0.0
var nitro_fuel: float = 1.0

var health: float = 100.0
var stamina: float = StaminaRules.MAX
var weapon: int = CombatMath.Weapon.FISTS
var aggression: float = 1.0
var knockouts: int = 0

var state: int = State.RIDING
var _state_timer: float = 0.0
var _attack_cooldown: float = 0.0
var _knockback_velocity: float = 0.0

# Input for this frame; the player controller and AI both write these.
var in_throttle: float = 0.0
var in_steer: float = 0.0
var in_nitro: bool = false

var visual: Node3D


func setup(p_track: Track, start_distance: float, start_lateral: float) -> void:
	track = p_track
	distance = start_distance
	lateral = start_lateral
	_apply_transform()


func alive() -> bool:
	return health > 0.0


func step(delta: float) -> void:
	_attack_cooldown = maxf(_attack_cooldown - delta, 0.0)
	stamina = StaminaRules.recover(stamina, delta)

	match state:
		State.CRASHED:
			_state_timer -= delta
			speed = maxf(speed - 30.0 * delta, 0.0)
			distance += speed * delta
			if _state_timer <= 0.0 and alive():
				state = State.REMOUNT
				# Exhausted riders pick the bike up slowly — Road Rash's stamina bite.
				_state_timer = 1.2 if not StaminaRules.is_exhausted(stamina) else 2.4
		State.REMOUNT:
			_state_timer -= delta
			if _state_timer <= 0.0:
				state = State.RIDING
		State.RIDING:
			_ride(delta)

	_apply_transform()


func _ride(delta: float) -> void:
	var target_top := top_speed
	if in_nitro and nitro_fuel > 0.0 and nitro_boost > 0.0:
		target_top += nitro_boost
		nitro_fuel = maxf(nitro_fuel - delta * 0.25, 0.0)

	if in_throttle > 0.0:
		speed = move_toward(speed, target_top * in_throttle, accel * delta)
	else:
		speed = move_toward(speed, 0.0, 14.0 * delta)

	# Corner drag: carrying full speed through a bend costs grip.
	var curvature := _local_curvature()
	var corner_penalty := clampf(curvature * speed * 0.022 / handling, 0.0, 0.35)
	speed *= 1.0 - corner_penalty * delta

	distance += speed * delta
	var steer_rate := (7.0 + speed * 0.06) * handling
	lateral += in_steer * steer_rate * delta
	lateral += _knockback_velocity * delta
	_knockback_velocity = move_toward(_knockback_velocity, 0.0, 18.0 * delta)

	# Running off the road: verge slows hard; the guardrail is a wall.
	var limit := track.half_width - 0.6
	if absf(lateral) > limit:
		lateral = clampf(lateral, -limit, limit)
		if absf(_knockback_velocity) > 6.0:
			take_damage(12.0, signf(lateral), null)
			_knockback_velocity = -_knockback_velocity * 0.4
		speed *= 1.0 - 1.5 * delta

	lean = lerpf(lean, -in_steer * 0.5 - curvature * signf(_curve_direction()) * 0.15, 8.0 * delta)


func _local_curvature() -> float:
	var ahead := 18.0
	var f0 := track.forward(distance)
	var f1 := track.forward(distance + ahead)
	return f0.angle_to(f1)


func _curve_direction() -> float:
	var t := track.sample(distance, 0.0)
	var f1 := track.forward(distance + 18.0)
	return signf(t.basis.x.dot(f1))


func try_attack(side: float, kick: bool, opponents: Array) -> bool:
	if state != State.RIDING or _attack_cooldown > 0.0:
		return false
	var w := CombatMath.Weapon.KICK if kick else weapon
	_attack_cooldown = CombatMath.cooldown(w)
	stamina = StaminaRules.apply_kick(stamina) if kick else StaminaRules.apply_swing(stamina)
	if is_player:
		Sfx.play("kick" if kick else "hit", -6.0, randf_range(0.95, 1.05))

	var reach := CombatMath.reach(w)
	var hit_any := false
	for opponent in opponents:
		var other := opponent as Rider
		if other == self or other.state != State.RIDING or not other.alive():
			continue
		var gap_s: float = absf(other.distance - distance)
		var gap_x: float = other.lateral - lateral
		if gap_s > reach or absf(gap_x) > reach or signf(gap_x) != signf(side):
			continue
		var damage := CombatMath.compute_damage(w, speed - other.speed, aggression)
		# Exhausted attackers hit like wet paper — stamina matters.
		if StaminaRules.is_exhausted(stamina):
			damage *= 0.45
		other.take_damage(damage, -signf(gap_x), self)
		other._knockback_velocity += CombatMath.knockback(w, damage) * signf(gap_x)
		if CombatMath.can_steal(damage, other.weapon) and not kick:
			weapon = CombatMath.better(weapon, other.weapon)
			other.weapon = CombatMath.Weapon.FISTS
		hit_any = true
	return hit_any


func take_damage(amount: float, from_side: float, attacker: Rider) -> void:
	if not alive():
		return
	health = maxf(health - amount, 0.0)
	stamina = StaminaRules.apply_hit(stamina)
	damaged.emit(amount, from_side)
	if health <= 0.0:
		crash()
		if attacker != null:
			attacker.knockouts += 1
		knocked_out.emit(self)
	elif amount >= 18.0 and state == State.RIDING:
		crash()


func crash() -> void:
	if state == State.CRASHED:
		return
	state = State.CRASHED
	_state_timer = 1.6
	speed *= 0.3
	crashed.emit(self)
	if is_player:
		Sfx.play("crash", -2.0)


func heal_for_new_race() -> void:
	health = 100.0
	stamina = StaminaRules.MAX
	state = State.RIDING
	knockouts = 0
	speed = 0.0
	nitro_fuel = 1.0


func _apply_transform() -> void:
	if track == null:
		return
	var t := track.sample(distance, lateral, 0.0)
	transform = t
	if visual != null:
		visual.rotation.z = lean
		if state == State.CRASHED:
			visual.rotation.z = lerpf(visual.rotation.z, PI * 0.45, 0.3)
