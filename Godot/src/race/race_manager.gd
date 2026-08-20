class_name RaceManager
extends Node
## Owns the race: phases, standings, traffic collisions, police busts, finish.
## Everything the HUD shows comes from here; everything the campaign records
## leaves through `finished`.

signal phase_changed(phase: int)
signal finished(summary: Dictionary)

enum Phase { STAGING, COUNTDOWN, RACING, FINISHED }

var phase: int = Phase.STAGING
var countdown_remaining: float = 3.0
var track: Track
var riders: Array = []          # every Rider incl. player and police
var racers: Array = []          # standings-eligible: player + rivals
var player: Rider
var rival_ais: Array = []
var police_ais: Array = []
var traffic: Traffic
var busted: bool = false
var _finish_position: int = 0


func configure(p_track: Track, p_player: Rider, p_rivals: Array, p_police: Array, p_traffic: Traffic) -> void:
	track = p_track
	player = p_player
	traffic = p_traffic
	rival_ais = p_rivals
	police_ais = p_police

	racers = [player]
	for ai in rival_ais:
		racers.append(ai.rider)
	riders = racers.duplicate()
	for ai in police_ais:
		riders.append(ai.rider)


func start() -> void:
	phase = Phase.COUNTDOWN
	countdown_remaining = 3.0
	phase_changed.emit(phase)


func _physics_process(delta: float) -> void:
	match phase:
		Phase.COUNTDOWN:
			countdown_remaining -= delta
			if countdown_remaining <= 0.0:
				phase = Phase.RACING
				phase_changed.emit(phase)
				Sfx.play("go", -4.0)
		Phase.RACING:
			_step_race(delta)
		_:
			pass


func _step_race(delta: float) -> void:
	for ai in rival_ais:
		ai.step(delta, racers, player)
	for ai in police_ais:
		if ai.step(delta, player):
			busted = true
			_finish(0)
			return

	for rider_obj in riders:
		var rider := rider_obj as Rider
		rider.step(delta)

	if traffic != null:
		traffic.step(delta)
		_check_traffic_collisions()

	_check_rider_collisions()

	if player.distance >= track.length:
		_finish(position_of(player))
		return

	# Rivals that cross the line keep their result; the race ends on the player.


## Standings: distance travelled, finished riders locked ahead.
func position_of(rider: Rider) -> int:
	var pos := 1
	for other_obj in racers:
		var other := other_obj as Rider
		if other != rider and other.distance > rider.distance:
			pos += 1
	return pos


func standings() -> Array:
	var sorted := racers.duplicate()
	sorted.sort_custom(func(a, b): return (a as Rider).distance > (b as Rider).distance)
	return sorted


## Hitting a car is a wall at speed: heavy damage plus a crash.
func _check_traffic_collisions() -> void:
	for rider_obj in riders:
		var rider := rider_obj as Rider
		if rider.state != Rider.State.RIDING or rider.speed < 3.0:
			continue
		for car in traffic.cars:
			var gap_s: float = absf(float(car["s"]) - rider.distance)
			var gap_x: float = absf(float(car["lane"]) - rider.lateral)
			if gap_s < 2.6 and gap_x < 1.6:
				var closing: float = absf(rider.speed - float(car["speed"]))
				rider.take_damage(minf(closing * 1.2, 55.0), signf(rider.lateral - float(car["lane"])), null)
				break


## Rider-vs-rider shoulder contact: gentle push apart, damage only at speed delta.
func _check_rider_collisions() -> void:
	for i in riders.size():
		for j in range(i + 1, riders.size()):
			var a := riders[i] as Rider
			var b := riders[j] as Rider
			if a.state != Rider.State.RIDING or b.state != Rider.State.RIDING:
				continue
			if absf(a.distance - b.distance) < 1.8 and absf(a.lateral - b.lateral) < 1.0:
				var push := signf(a.lateral - b.lateral)
				if push == 0.0:
					push = 1.0
				a.lateral += push * 0.5
				b.lateral -= push * 0.5


func _finish(position: int) -> void:
	if phase == Phase.FINISHED:
		return
	phase = Phase.FINISHED
	_finish_position = position
	phase_changed.emit(phase)

	var purse := int(track.definition.get("purse", 800))
	var repair := int((100.0 - player.health) * 3.0)
	var state := get_node("/root/GameState")
	var summary := Campaign.apply_result(
		state.save, position, player.knockouts, purse, busted, repair)
	state.persist()
	finished.emit(summary)
