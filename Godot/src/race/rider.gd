class_name Rider
extends Node3D
## Track-space rider with Road Rash states: RIDING, CRASHED (slide),
## RUNNING (sprint back to the bike), REMOUNT. Rider stamina and bike health
## are separate meters — empty stamina ejects you; zero bike health is a DNF.

signal crashed(rider: Rider)
signal knocked_out(rider: Rider)
signal damaged(amount: float, from_side: float)
signal attacked(side: float, kick: bool)
signal weapon_stolen(from_name: String)
signal landed

enum State { RIDING, CRASHED, RUNNING, REMOUNT }

var rider_name: String = "Rider"
var rider_id: String = "player"
var gang: String = ""
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
var _pose_timer: float = 0.0
var _pose_kind: int = 0  # 0 none, 1 punch L, 2 punch R, 3 kick
var _crash_phase: float = 0.0
var _windup_side: float = 0.0
var _windup_active: bool = false
var airborne: float = 0.0
var air_height: float = 0.0
var air_v: float = 0.0

var _bike_distance: float = 0.0
var _bike_lateral: float = 0.0
const RUN_SPEED := 9.5

var in_throttle: float = 0.0
var in_brake: float = 0.0
var in_steer: float = 0.0
var in_nitro: bool = false

var visual: Node3D
var _visual_anim: Node3D
var _run_phase: float = 0.0
var body_color: Color = Color(0.9, 0.35, 0.1)
var suit_color: Color = Color(0.14, 0.14, 0.16)


func setup(p_track: Track, start_distance: float, start_lateral: float) -> void:
	track = p_track
	distance = start_distance
	lateral = start_lateral
	_apply_transform()


func bind_visual(root: Node3D) -> void:
	visual = root
	if root.get_script() == null:
		root.set_script(load("res://src/race/rider_visual.gd"))
	_visual_anim = root
	if _visual_anim.has_method("initialize"):
		_visual_anim.initialize(self)
	if _visual_anim.has_method("set_colors"):
		_visual_anim.set_colors(body_color, suit_color)


func alive() -> bool:
	return health > 0.0


func bike_destroyed() -> bool:
	return health <= 0.0


func step(delta: float) -> void:
	_attack_cooldown = maxf(_attack_cooldown - delta, 0.0)
	_pose_timer = maxf(_pose_timer - delta, 0.0)
	stamina = StaminaRules.recover(stamina, delta)
	if state == State.CRASHED:
		_crash_phase += delta

	match state:
		State.CRASHED:
			_step_crashed(delta)
		State.RUNNING:
			_step_running(delta)
		State.REMOUNT:
			_state_timer -= delta
			if _state_timer <= 0.0:
				state = State.RIDING
				speed = maxf(speed, 8.0)
		State.RIDING:
			_ride(delta)

	_apply_transform()
	_apply_pose()


func _step_crashed(delta: float) -> void:
	_state_timer -= delta
	speed = maxf(speed - 22.0 * delta, 0.0)
	distance += speed * delta
	if _state_timer <= 0.0 and alive():
		if StaminaRules.is_exhausted(stamina):
			crash()
			return
		state = State.RUNNING
		speed = 0.0
		_crash_phase = 0.0


func _step_running(delta: float) -> void:
	_run_phase += delta
	# Classic: hold brake to stand still and let traffic / cops pass.
	if in_brake > 0.4:
		lean = lerpf(lean, 0.0, 8.0 * delta)
		return
	var to_bike := Vector2(_bike_lateral - lateral, _bike_distance - distance)
	var dist := to_bike.length()
	if dist < 1.2:
		state = State.REMOUNT
		_state_timer = 0.9 if not StaminaRules.is_exhausted(stamina) else 1.8
		return
	var dir := to_bike / maxf(dist, 0.001)
	lateral += (dir.x + in_steer * 0.35) * RUN_SPEED * delta
	distance += dir.y * RUN_SPEED * delta
	lean = lerpf(lean, in_steer * 0.25, 6.0 * delta)


func _ride(delta: float) -> void:
	if stamina <= 0.0:
		crash()
		return

	if air_height > 0.0 or air_v > 0.0:
		_step_air(delta)
		return

	var target_top := top_speed
	if in_nitro and nitro_fuel > 0.0 and nitro_boost > 0.0:
		target_top += nitro_boost
		nitro_fuel = maxf(nitro_fuel - delta * 0.25, 0.0)

	if in_throttle > 0.0:
		speed = move_toward(speed, target_top * in_throttle, accel * delta)
	elif in_brake > 0.0:
		speed = move_toward(speed, 0.0, 22.0 * in_brake * delta)
	else:
		speed = move_toward(speed, 0.0, 10.0 * delta)

	var curvature := _local_curvature()
	var corner_penalty := clampf(curvature * speed * 0.022 / handling, 0.0, 0.35)
	speed *= 1.0 - corner_penalty * delta

	distance += speed * delta
	var steer_rate := (7.0 + speed * 0.06) * handling
	lateral += in_steer * steer_rate * delta
	lateral += _knockback_velocity * delta
	_knockback_velocity = move_toward(_knockback_velocity, 0.0, 18.0 * delta)

	var limit := track.half_width - 0.6
	if absf(lateral) > limit:
		lateral = clampf(lateral, -limit, limit)
		take_bike_damage(8.0 * delta, null)
		speed *= 1.0 - 1.8 * delta

	lean = lerpf(lean, -in_steer * 0.55 - curvature * signf(_curve_direction()) * 0.18, 8.0 * delta)


func launch(impulse: float) -> void:
	# Cow / crest ramp: stay on the bike, lose steering, land further down the road.
	air_v = maxf(impulse, 2.0)
	airborne = 0.01
	if air_height < 0.05:
		air_height = 0.05


func is_airborne() -> bool:
	return air_height > 0.08


func _step_air(delta: float) -> void:
	air_v -= 28.0 * delta
	air_height += air_v * delta
	distance += speed * delta
	lateral += in_steer * 3.2 * delta
	var limit := track.half_width - 0.6
	lateral = clampf(lateral, -limit, limit)
	lean = lerpf(lean, 0.0, 4.0 * delta)
	if air_height <= 0.0:
		var was_air := airborne > 0.12
		air_height = 0.0
		air_v = 0.0
		airborne = 0.0
		speed *= 0.92
		if was_air:
			landed.emit()
	else:
		airborne += delta


func _local_curvature() -> float:
	var f0 := track.forward(distance)
	var f1 := track.forward(distance + 18.0)
	return f0.angle_to(f1)


func _curve_direction() -> float:
	var t := track.sample(distance, 0.0)
	var f1 := track.forward(distance + 18.0)
	return signf(t.basis.x.dot(f1))


func begin_windup(side: float) -> void:
	if state != State.RIDING or _attack_cooldown > 0.0:
		return
	if _windup_active and _windup_side == side:
		return
	_windup_active = true
	_windup_side = side


func release_windup(opponents: Array) -> bool:
	if not _windup_active:
		return false
	_windup_active = false
	return try_attack(_windup_side, false, opponents, true)


func cancel_windup() -> void:
	_windup_active = false


func try_attack(side: float, kick: bool, opponents: Array, windup: bool = false) -> bool:
	if state != State.RIDING or _attack_cooldown > 0.0:
		return false
	var w := CombatMath.Weapon.KICK if kick else weapon
	_attack_cooldown = CombatMath.windup_cooldown(w) if windup else CombatMath.cooldown(w)
	stamina = StaminaRules.apply_kick(stamina) if kick else StaminaRules.apply_swing(stamina)
	_pose_kind = 3 if kick else (1 if side < 0.0 else 2)
	_pose_timer = 0.38 if kick else (0.28 if windup else 0.32)
	attacked.emit(side, kick)
	if is_player:
		Sfx.play("kick" if kick else "hit", -6.0, randf_range(0.95, 1.05))

	var reach := CombatMath.reach(w)
	var hit_any := false
	for opponent in opponents:
		var other := opponent as Rider
		if other == self or other.state != Rider.State.RIDING or not other.alive():
			continue
		if other.is_police:
			continue
		var gap_s: float = absf(other.distance - distance)
		var gap_x: float = other.lateral - lateral
		if gap_s > reach or absf(gap_x) > reach or signf(gap_x) != signf(side):
			continue
		var steer_toward := 1.0 - clampf(absf(gap_x) / reach, 0.0, 1.0)
		var damage := CombatMath.compute_damage(w, speed - other.speed, aggression, steer_toward, windup)
		if StaminaRules.is_exhausted(stamina):
			damage *= 0.45
		other.take_rider_hit(damage, -signf(gap_x), self)
		var kb := CombatMath.knockback(w, damage)
		if kick:
			kb *= 1.35
		other._knockback_velocity += kb * signf(gap_x)
		if CombatMath.can_steal(damage, other.weapon, windup) and not kick:
			var stolen := other.weapon
			weapon = CombatMath.better(weapon, other.weapon)
			other.weapon = CombatMath.Weapon.FISTS
			if is_player and stolen != CombatMath.Weapon.FISTS:
				weapon_stolen.emit(other.rider_name)
				Sfx.play("pickup", -4.0, 1.1)
		hit_any = true
		if is_player and other.rider_id != "":
			var loop := Engine.get_main_loop()
			if loop is SceneTree:
				var gs := (loop as SceneTree).root.get_node_or_null("GameState")
				if gs != null and gs.has_method("note_grudge"):
					gs.note_grudge(other.rider_id)
	return hit_any


func take_rider_hit(amount: float, from_side: float, attacker: Rider) -> void:
	if not alive():
		return
	stamina = StaminaRules.apply_hit(stamina)
	health = maxf(health - amount * 0.25, 0.0)
	damaged.emit(amount, from_side)
	if stamina <= 0.0 or amount >= 22.0:
		crash()
		if attacker != null:
			attacker.knockouts += 1
			if attacker.is_player:
				Sfx.play("sting", -3.0, 1.0)
		knocked_out.emit(self)
	elif health <= 0.0:
		knocked_out.emit(self)


func take_bike_damage(amount: float, attacker: Rider) -> void:
	if not alive():
		return
	health = maxf(health - amount, 0.0)
	damaged.emit(amount, 0.0)
	if health <= 0.0:
		knocked_out.emit(self)
	elif amount >= 28.0 and state == State.RIDING:
		crash()


func take_damage(amount: float, from_side: float, attacker: Rider) -> void:
	if attacker != null:
		take_rider_hit(amount, from_side, attacker)
	else:
		take_bike_damage(amount, attacker)


func crash() -> void:
	if state == State.CRASHED or state == State.RUNNING:
		return
	_bike_distance = distance
	_bike_lateral = lateral
	state = State.CRASHED
	_state_timer = 1.05
	_crash_phase = 0.0
	speed *= 0.35
	air_height = 0.0
	air_v = 0.0
	airborne = 0.0
	crashed.emit(self)
	if is_player:
		Sfx.play("crash", -2.0)


func is_vulnerable_to_police() -> bool:
	return state == State.CRASHED or state == State.RUNNING


func set_dropped_bike(s: float, x: float) -> void:
	_bike_distance = s
	_bike_lateral = x


func is_winding_up() -> bool:
	return _windup_active


func pickup_weapon(w: int) -> bool:
	if w == CombatMath.Weapon.FISTS or w == CombatMath.Weapon.KICK:
		return false
	if CombatMath.better(w, weapon) != w or w == weapon:
		return false
	weapon = w
	Sfx.play("pickup", -4.0, 1.15)
	return true


func heal_for_new_race(reset_weapon: bool = false) -> void:
	health = 100.0
	stamina = StaminaRules.MAX
	state = State.RIDING
	knockouts = 0
	speed = 0.0
	nitro_fuel = 1.0
	if reset_weapon:
		weapon = CombatMath.Weapon.FISTS
	_crash_phase = 0.0
	_windup_active = false
	air_height = 0.0
	air_v = 0.0
	airborne = 0.0


func _apply_pose() -> void:
	if _visual_anim == null or not _visual_anim.has_method("apply"):
		return
	var pose_t := clampf(_pose_timer / 0.38, 0.0, 1.0) if _pose_timer > 0.0 else 0.0
	_visual_anim.apply(state, speed, lean, _pose_kind, pose_t,
			state == State.RUNNING, _run_phase, weapon, _crash_phase, _windup_active, _windup_side,
			in_nitro and nitro_fuel > 0.0 and nitro_boost > 0.0)


func _apply_transform() -> void:
	if track == null:
		return
	var height := air_height
	if state == State.RUNNING:
		height = -0.35
	transform = track.sample(distance, lateral, height)
