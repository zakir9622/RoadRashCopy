class_name PoliceAI
extends RefCounted
## Road Rash police are pressure, not racers: they only need to be close when
## you stop. Crash near one, or dawdle alongside one, and the run is over.

var rider: Rider
var bust_radius: float = 14.0
var escape_distance: float = 120.0
var _stopped_timer: float = 0.0
var _escape_timer: float = 0.0
var _siren_timer: float = 0.0
var pursuing: bool = true
var has_busted: bool = false


func _init(p_rider: Rider) -> void:
	rider = p_rider
	rider.is_police = true


## Returns true the moment this unit busts the player.
func step(delta: float, player: Rider) -> bool:
	if has_busted or player == null or rider.state != Rider.State.RIDING:
		return false

	var gap := player.distance - rider.distance

	if not pursuing:
		rider.in_throttle = 0.5
		return false

	# Chase: full throttle behind, ease off alongside to sit on the target.
	rider.in_throttle = 1.0 if gap > 6.0 else 0.75
	rider.in_steer = clampf((player.lateral - rider.lateral) * 0.4, -1.0, 1.0)

	# Siren pressure while close — the classic tell that heat is on.
	var distance_to := absf(gap)
	if distance_to < bust_radius * 3.0 and player.is_player:
		_siren_timer -= delta
		if _siren_timer <= 0.0:
			_siren_timer = 2.4
			Sfx.play("siren", -8.0)

	# Escape: hold the gap long enough and the pursuit breaks off.
	if distance_to > escape_distance:
		_escape_timer += delta
		if _escape_timer > 4.0:
			pursuing = false
	else:
		_escape_timer = 0.0

	# Bust: crashed nearby, or stopped alongside for over a second.
	if distance_to < bust_radius:
		if player.state == Rider.State.CRASHED:
			has_busted = true
			return true
		_stopped_timer = _stopped_timer + delta if player.speed < 5.0 else 0.0
		if _stopped_timer > 1.2:
			has_busted = true
			return true
	else:
		_stopped_timer = 0.0
	return false
