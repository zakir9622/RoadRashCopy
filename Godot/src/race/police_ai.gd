class_name PoliceAI
extends RefCounted
## Classic Road Rash police: approach from behind, siren in the mirrors, bust
## only when you crash or run back to your bike. Sitting still raises heat so
## an officer spawns — it does not bust you while you are still riding.

var rider: Rider
var bust_radius: float = 16.0
var escape_distance: float = 55.0
var _siren_timer: float = 0.0
var _escape_timer: float = 0.0
var _grace: float = 5.0
var pursuing: bool = true
var has_busted: bool = false
var dormant: bool = false


func _init(p_rider: Rider) -> void:
	rider = p_rider
	rider.is_police = true


func step(delta: float, player: Rider) -> bool:
	if has_busted or player == null or dormant:
		return false
	if rider.state != Rider.State.RIDING:
		rider.in_throttle = 0.0
		return false

	_grace = maxf(_grace - delta, 0.0)
	var gap := player.distance - rider.distance  # + = player ahead

	if not pursuing:
		return false

	# Dynamic speed cap — chase hard but never warp past the player.
	rider.top_speed = player.speed * 1.08 + 6.0

	if gap < -8.0:
		# Cop drifted ahead — back off and let the player pass.
		rider.in_throttle = 0.25
	elif gap > 4.0:
		rider.in_throttle = clampf(0.55 + gap / 50.0, 0.55, 1.0)
	else:
		rider.in_throttle = 0.7

	rider.in_steer = clampf((player.lateral - rider.lateral) * 0.55, -1.0, 1.0)

	var behind := gap > 0.0
	var distance_to := absf(gap)

	if behind and distance_to < bust_radius * 4.5 and player.is_player:
		_siren_timer -= delta
		if _siren_timer <= 0.0:
			_siren_timer = 2.2
			Sfx.play("siren", -4.0)
			Sfx.play("horn", -8.0)

	if behind and distance_to > escape_distance:
		_escape_timer += delta
		if _escape_timer > 5.0:
			pursuing = false
			dormant = true
			rider.in_throttle = 0.0
	else:
		_escape_timer = 0.0

	if _grace > 0.0:
		return false

	if distance_to < bust_radius and player.is_vulnerable_to_police():
		has_busted = true
		return true
	return false


func signed_gap_to(player: Rider) -> float:
	return player.distance - rider.distance
