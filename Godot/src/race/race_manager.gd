class_name RaceManager
extends Node
## Race phases, standings, traffic, heat-driven police spawns, and bust handling.

signal phase_changed(phase: int)
signal finished(summary: Dictionary)

enum Phase { STAGING, COUNTDOWN, RACING, FINISHED }

var phase: int = Phase.STAGING
var countdown_remaining: float = 3.0
var track: Track
var riders: Array = []
var racers: Array = []
var player: Rider
var rival_ais: Array = []
var police_ais: Array = []
var traffic: Traffic
var busted: bool = false
var heat: HeatDirector = HeatDirector.new()
var _max_police: int = 1
var _police_spawned: int = 0
var _spawn_cooldown: float = 0.0
var _finish_position: int = 0

## Callable set by Race scene to spawn a cop behind the player when heat demands it.
var spawn_police_behind: Callable = Callable()


func configure(p_track: Track, p_player: Rider, p_rivals: Array, p_police: Array,
		p_traffic: Traffic, max_police: int = 1) -> void:
	track = p_track
	player = p_player
	traffic = p_traffic
	rival_ais = p_rivals
	police_ais = p_police
	_max_police = maxi(max_police, 1)
	_police_spawned = p_police.size()

	racers = [player]
	for ai in rival_ais:
		racers.append(ai.rider)
	riders = racers.duplicate()
	for ai in police_ais:
		riders.append(ai.rider)

	player.crashed.connect(_on_player_crashed)
	player.attacked.connect(_on_player_attacked)


func register_heat_punch() -> void:
	heat.on_punch()


func _on_player_crashed(_r: Rider) -> void:
	heat.on_crash()


func _on_player_attacked(_side: float, kick: bool) -> void:
	if kick:
		heat.on_kick()
	else:
		heat.on_punch()


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
	heat.tick(delta)
	_spawn_cooldown = maxf(_spawn_cooldown - delta, 0.0)
	if _spawn_cooldown <= 0.0 and spawn_police_behind.is_valid():
		if heat.should_spawn_reinforcement(_active_police(), _max_police):
			spawn_police_behind.call()
			_police_spawned += 1
			_spawn_cooldown = 25.0
		elif heat.should_spawn_cop(_active_police(), _max_police) and _police_spawned == 0:
			spawn_police_behind.call()
			_police_spawned += 1
			_spawn_cooldown = 18.0

	for ai in rival_ais:
		ai.step(delta, racers, player)
	for ai in police_ais:
		if ai.step(delta, player):
			busted = true
			_finish(0)
			return

	for rider_obj in riders:
		(rider_obj as Rider).step(delta)

	if traffic != null:
		traffic.step(delta)
		_check_traffic_collisions()

	_check_rider_collisions()

	if player.bike_destroyed():
		_finish(0)
		return

	if player.distance >= track.length:
		_finish(position_of(player))


func _active_police() -> int:
	var n := 0
	for ai in police_ais:
		if not ai.dormant or ai.pursuing:
			n += 1
	return n


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
				rider.take_bike_damage(minf(closing * 1.2, 55.0), null)
				if rider.is_player:
					heat.on_near_miss()
				break


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
