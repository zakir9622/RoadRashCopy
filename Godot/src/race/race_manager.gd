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
var _idle_timer: float = 0.0

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
	for rider_obj in riders:
		(rider_obj as Rider).heal_for_new_race((rider_obj as Rider).is_player)
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
	if player == null or track == null:
		return
	heat.tick(delta)
	_spawn_cooldown = maxf(_spawn_cooldown - delta, 0.0)
	_tick_idle_heat(delta)
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

	_check_hazards()
	_step_animals(delta)
	_check_roadblocks()
	_check_rider_collisions()

	if player.bike_destroyed():
		_finish(0)
		return

	if player.distance >= track.length:
		_finish(position_of(player))


func _tick_idle_heat(delta: float) -> void:
	if player == null:
		return
	var sitting := player.state == Rider.State.RIDING and player.speed < 1.6 and not player.is_airborne()
	var down := player.state == Rider.State.CRASHED or player.state == Rider.State.RUNNING
	if sitting or down:
		_idle_timer += delta
		if _idle_timer >= 12.0:
			heat.on_idle()
			_idle_timer = 0.0
	else:
		_idle_timer = 0.0


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
				var dmg := minf(closing * 1.2, 55.0)
				rider.take_bike_damage(dmg, null)
				if rider.state == Rider.State.RIDING and dmg >= 30.0:
					rider.crash()
				if rider.is_player:
					heat.on_near_miss()
				break


func _step_animals(delta: float) -> void:
	if track == null:
		return
	for hz in track.hazards:
		var kind := String(hz.get("kind", ""))
		if kind != "deer" and kind != "cow":
			continue
		var pace := 2.1 if kind == "cow" else 3.2
		hz["lateral"] = float(hz["lateral"]) + float(hz.get("dir", 1.0)) * pace * delta
		if absf(float(hz["lateral"])) > track.half_width:
			hz["dir"] = -float(hz.get("dir", 1.0))
		var node: Variant = hz.get("node", null)
		if node is Node3D and is_instance_valid(node):
			var n := node as Node3D
			var s := float(n.get_meta("kit_scale", 1.0))
			var lift := float(n.get_meta("y_lift", 0.5))
			var xf := track.sample(clampf(float(hz["distance"]), 0.0, track.length),
					float(hz["lateral"]), 0.0)
			xf.basis = xf.basis.scaled(Vector3(s, s, s))
			xf.origin += xf.basis.y.normalized() * lift
			n.global_transform = xf


func _check_hazards() -> void:
	if track == null:
		return
	for rider_obj in riders:
		var rider := rider_obj as Rider
		if rider.state != Rider.State.RIDING:
			continue
		if rider.is_airborne():
			continue
		for hz in track.hazards:
			if bool(hz.get("taken", false)):
				continue
			var d := float(hz["distance"])
			var lat := float(hz["lateral"])
			if absf(rider.distance - d) > 2.0 or absf(rider.lateral - lat) > 1.4:
				continue
			match String(hz["kind"]):
				"oil":
					rider.speed *= 0.72
					rider.take_bike_damage(6.0, null)
					if randf() < 0.08:
						rider.crash()
				"sign":
					rider.take_bike_damage(18.0, null)
					rider.crash()
				"deer":
					rider.take_bike_damage(24.0, null)
					rider.crash()
				"cow":
					# Classic: hit a cow slow and you dump; hit it fast and it is a ramp.
					if rider.speed > 32.0:
						rider.launch(6.5 + rider.speed * 0.06)
						rider.take_bike_damage(8.0, null)
					else:
						rider.take_bike_damage(28.0, null)
						rider.crash()
				"chain":
					_try_pickup(rider, hz, CombatMath.Weapon.CHAIN)
				"bat":
					_try_pickup(rider, hz, CombatMath.Weapon.BAT)


func _check_roadblocks() -> void:
	if track == null:
		return
	for rider_obj in riders:
		var rider := rider_obj as Rider
		if rider.state != Rider.State.RIDING:
			continue
		for block in track.roadblocks:
			var d := float(block["distance"])
			var lat := float(block["lateral"])
			var w := float(block.get("width", 3.0))
			if absf(rider.distance - d) > 2.5 or absf(rider.lateral - lat) > w * 0.5:
				continue
			rider.take_bike_damage(32.0, null)
			rider.crash()
			if rider.is_player:
				heat.on_crash()


func _try_pickup(rider: Rider, hz: Dictionary, weapon: int) -> void:
	if not rider.pickup_weapon(weapon):
		return
	hz["taken"] = true
	var node: Variant = hz.get("node", null)
	if node is Node3D and is_instance_valid(node):
		(node as Node3D).visible = false


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
	var state := get_node_or_null("/root/GameState")
	var summary: Dictionary
	if state == null:
		summary = {
			"won": false, "position": position, "prize": 0, "knockouts": player.knockouts,
			"combat_bonus": 0, "repair_bill": repair, "fine": 0, "net": -repair,
			"balance": 0, "busted": busted, "game_over": false, "champion": false,
		}
	else:
		summary = Campaign.apply_result(
			state.save, position, player.knockouts, purse, busted, repair)
		state.persist()
	finished.emit(summary)
