class_name RivalAI
extends RefCounted
## Drives one rival Rider. Skill sets pace; aggression starts fights; a rubber
## band keeps beaten rivals in the mirror without ever teleporting anyone.

var rider: Rider
var skill: float = 0.85
var _target_lateral: float = 0.0
var _decision_timer: float = 0.0
var _banter_cooldown: float = 0.0
var _rng := RandomNumberGenerator.new()


func _init(p_rider: Rider, p_skill: float, p_aggression: float, seed_value: int) -> void:
	rider = p_rider
	skill = p_skill
	rider.aggression = p_aggression
	_rng.seed = seed_value


func step(delta: float, all_riders: Array, player: Rider) -> void:
	if rider.state != Rider.State.RIDING:
		return

	_decision_timer -= delta
	_banter_cooldown = maxf(_banter_cooldown - delta, 0.0)
	if _decision_timer <= 0.0:
		_decision_timer = _rng.randf_range(0.45, 1.0)
		_decide(player)

	var pace := skill
	if player != null:
		var gap := rider.distance - player.distance
		if gap < -80.0:
			pace = minf(skill + 0.18, 1.0)
		elif gap > 90.0:
			pace = skill * 0.82
	# Corners bunch the pack the way Road Rash 3D did — nobody full-throttles a hairpin.
	var curvature := rider._local_curvature()
	if curvature > 0.10:
		pace *= clampf(1.0 - curvature * 0.55, 0.62, 1.0)
	rider.in_throttle = pace

	var error := _target_lateral - rider.lateral
	rider.in_steer = clampf(error * 0.42, -1.0, 1.0)

	# Road Rash pack fights: rivals brawl each other and hunt the player.
	for other in all_riders:
		var target := other as Rider
		if target == rider or not target.alive() or target.is_police:
			continue
		if absf(target.distance - rider.distance) < 2.6 and absf(target.lateral - rider.lateral) < 2.8:
			var side := signf(target.lateral - rider.lateral)
			var kick := _rng.randf() < 0.35
			rider.try_attack(side, kick, all_riders)
			if target == player and _banter_cooldown <= 0.0 and _rng.randf() < 0.12:
				_banter_cooldown = 8.0
				_push_banter(player)
			break


func _push_banter(player: Rider) -> void:
	if player == null or not player.is_player:
		return
	var race := player.get_parent()
	if race == null:
		return
	var hud := race.get_node_or_null("Hud")
	if hud != null and hud.has_method("show_banter"):
		hud.show_banter(Campaign.banter_for(rider.rider_id))


func _decide(player: Rider) -> void:
	var limit := rider.track.half_width - 1.2
	var curvature := rider._local_curvature()
	if curvature > 0.08:
		# Hug the inside of the next corner so the pack stacks in the apex.
		var inside := -rider._curve_direction() * limit * 0.42
		_target_lateral = clampf(inside + _rng.randf_range(-0.8, 0.8), -limit, limit)
		return
	if player != null and rider.aggression > 1.0 \
			and absf(player.distance - rider.distance) < 40.0:
		_target_lateral = clampf(player.lateral + _rng.randf_range(-0.7, 0.7), -limit, limit)
	else:
		# Hold a lane near the current line instead of teleporting across the highway.
		_target_lateral = clampf(rider.lateral + _rng.randf_range(-2.4, 2.4), -limit, limit)
