class_name RivalAI
extends RefCounted
## Drives one rival Rider. Skill sets pace; aggression starts fights; a rubber
## band keeps beaten rivals in the mirror without ever teleporting anyone.

var rider: Rider
var skill: float = 0.85
var _target_lateral: float = 0.0
var _decision_timer: float = 0.0
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
	if _decision_timer <= 0.0:
		_decision_timer = _rng.randf_range(0.7, 1.6)
		_decide(player)

	# Pace: skill fraction of top speed, rubber-banded toward the player so the
	# grid stays a fight instead of a procession.
	var pace := skill
	if player != null:
		var gap := rider.distance - player.distance
		if gap < -120.0:
			pace = minf(skill + 0.15, 1.0)
		elif gap > 140.0:
			pace = skill * 0.85
	rider.in_throttle = pace

	# Steering: PD toward the chosen line, with lane-keeping inside the road.
	var error := _target_lateral - rider.lateral
	rider.in_steer = clampf(error * 0.35, -1.0, 1.0)

	# Fight anyone alongside if aggressive enough.
	if rider.aggression > 1.0:
		for other in all_riders:
			var target := other as Rider
			if target == rider or not target.alive():
				continue
			if absf(target.distance - rider.distance) < 2.0 and absf(target.lateral - rider.lateral) < 2.4:
				rider.try_attack(signf(target.lateral - rider.lateral), _rng.randf() < 0.25, all_riders)
				break


func _decide(player: Rider) -> void:
	var limit := rider.track.half_width - 2.0
	# Hunt the player when close; otherwise pick a racing-line-ish lane.
	if player != null and rider.aggression > 1.2 \
			and absf(player.distance - rider.distance) < 30.0:
		_target_lateral = clampf(player.lateral + _rng.randf_range(-1.0, 1.0), -limit, limit)
	else:
		_target_lateral = _rng.randf_range(-limit, limit) * 0.7
