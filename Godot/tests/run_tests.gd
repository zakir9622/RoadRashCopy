extends SceneTree
## Headless test suite:  godot --headless --script res://tests/run_tests.gd
## Exit code 0 = all green. Covers the pure-logic core plus a 300-frame race
## simulation with the full AI grid — the "Stable" gate from the plan.

var failures: int = 0
var checks: int = 0


func _init() -> void:
	_test_stamina()
	_test_combat()
	_test_catalog()
	_test_campaign_ledger()
	_test_bike_specs()
	_test_track_geometry()
	_test_race_simulation()

	# A compile failure in a dependency can silently skip whole test functions;
	# demanding the full check count turns that into a loud failure.
	const EXPECTED_CHECKS := 41
	if checks < EXPECTED_CHECKS:
		printerr("FAIL: only %d/%d checks ran — a test aborted early" % [checks, EXPECTED_CHECKS])
		failures += 1

	if failures == 0:
		print("ALL %d CHECKS PASSED" % checks)
		quit(0)
	else:
		print("%d/%d CHECKS FAILED" % [failures, checks])
		quit(1)


func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + message)


func _test_stamina() -> void:
	check(StaminaRules.apply_swing(100.0) < 100.0, "swing drains stamina")
	check(StaminaRules.apply_kick(100.0) < StaminaRules.apply_swing(100.0),
		"kick costs more than swing")
	check(StaminaRules.recover(50.0, 100.0) == StaminaRules.MAX, "recover clamps at max")
	check(StaminaRules.apply_hit(5.0) == 0.0, "hit clamps at zero")
	check(StaminaRules.is_exhausted(5.0), "low stamina is exhausted")
	check(not StaminaRules.is_exhausted(60.0), "high stamina is not exhausted")


func _test_combat() -> void:
	check(CombatMath.base_damage(CombatMath.Weapon.BAT) > CombatMath.base_damage(CombatMath.Weapon.FISTS),
		"bat beats fists")
	check(CombatMath.compute_damage(CombatMath.Weapon.FISTS, 10.0) >
		CombatMath.compute_damage(CombatMath.Weapon.FISTS, 0.0), "relative speed adds damage")
	check(CombatMath.knockback(CombatMath.Weapon.KICK, 10.0) >
		CombatMath.knockback(CombatMath.Weapon.BAT, 10.0), "kick shoves hardest")
	check(CombatMath.can_steal(20.0, CombatMath.Weapon.BAT), "hard hit steals a bat")
	check(not CombatMath.can_steal(20.0, CombatMath.Weapon.FISTS), "cannot steal fists")
	check(CombatMath.better(CombatMath.Weapon.BAT, CombatMath.Weapon.CHAIN) == CombatMath.Weapon.BAT,
		"bat outranks chain")


func _test_catalog() -> void:
	var tracks := TrackCatalog.all()
	check(tracks.size() == 5, "five tracks")
	for track in tracks:
		check(float(track["length"]) > 500.0, "%s has real length" % track["id"])
		check(int(track["rivals"]) > 0, "%s has opponents" % track["id"])
	check(TrackCatalog.find("downtown")["name"] == "Downtown", "find by id")
	check(TrackCatalog.at(99)["id"] == "night_city", "index clamps to last")
	check(TrackCatalog.at(-5)["id"] == "coast_run", "index clamps to first")


func _test_campaign_ledger() -> void:
	var save := {"cash": 100, "chapter": 0}
	var result := Campaign.apply_result(save, 1, 2, 800, false, 50)
	check(bool(result["won"]), "P1 wins the event")
	check(int(result["prize"]) == 800, "winner takes full purse")
	check(int(save["cash"]) == 100 + 800 + 300 - 50, "cash math")
	check(int(save["chapter"]) == 1, "win advances the chapter")

	var save2 := {"cash": 0, "chapter": 2}
	var result4 := Campaign.apply_result(save2, 4, 0, 1000, false, 0)
	check(bool(result4["won"]), "P4 still qualifies — classic Road Rash rule")
	check(int(result4["prize"]) == 300, "P4 takes 30% share")

	var save3 := {"cash": 500, "chapter": 1}
	var busted := Campaign.apply_result(save3, 2, 0, 1000, true, 0)
	check(not bool(busted["won"]), "a bust voids the result")
	check(int(busted["fine"]) == 400, "bust fines the player")
	check(int(save3["chapter"]) == 1, "bust does not advance")

	var save4 := {"cash": 0, "chapter": 0}
	var lost := Campaign.apply_result(save4, 7, 1, 1000, false, 100)
	check(not bool(lost["won"]), "P7 does not qualify")
	check(int(save4["chapter"]) == 0, "loss does not advance")


func _test_bike_specs() -> void:
	var rat := BikeSpecs.find("rat")
	var tuned := BikeSpecs.effective(rat, 5, 5)
	check(float(tuned["top_speed"]) > float(rat["top_speed"]), "engine stages add speed")
	check(float(tuned["handling"]) > float(rat["handling"]), "tire stages add handling")
	check(BikeSpecs.find("nonsense")["id"] == "rat", "unknown bike falls back")
	check(BikeSpecs.upgrade_cost(4) > BikeSpecs.upgrade_cost(0), "stages get pricier")


func _test_track_geometry() -> void:
	var track := Track.new()
	root.add_child(track)
	track.build(TrackCatalog.find("coast_run"))
	check(track.length > 2000.0, "curve bakes to full length")
	var t0 := track.sample(0.0, 0.0)
	var t1 := track.sample(100.0, 0.0)
	check(t0.origin.distance_to(t1.origin) > 60.0, "distance moves through space")
	var left := track.sample(50.0, -5.0)
	var right := track.sample(50.0, 5.0)
	check(left.origin.distance_to(right.origin) > 8.0, "lateral offset separates")
	track.queue_free()


func _test_race_simulation() -> void:
	# Full grid, 300 fixed steps, no scene tree tick needed — the manager's
	# step functions are called directly, exactly what _physics_process does.
	var track := Track.new()
	root.add_child(track)
	track.build(TrackCatalog.find("coast_run"))

	var player := Rider.new()
	player.is_player = true
	root.add_child(player)
	player.setup(track, 40.0, 0.0)
	player.top_speed = 50.0
	player.accel = 12.0

	var rivals: Array = []
	var riders: Array = [player]
	for i in 5:
		var rival := Rider.new()
		root.add_child(rival)
		rival.setup(track, 30.0 - i * 5.0, -4.0 + i * 2.0)
		rival.top_speed = 46.0
		rival.accel = 11.0
		var profile: Dictionary = Campaign.ROSTER[i]
		rivals.append(RivalAI.new(rival, float(profile["skill"]), float(profile["aggression"]), 77 + i))
		riders.append(rival)

	var cop := Rider.new()
	root.add_child(cop)
	cop.setup(track, 5.0, 0.0)
	cop.top_speed = 52.0
	cop.accel = 13.0
	var police := PoliceAI.new(cop)

	var delta := 1.0 / 60.0
	player.in_throttle = 1.0
	var start_distance := player.distance
	for frame in 300:
		for ai in rivals:
			ai.step(delta, riders, player)
		police.step(delta, player)
		for rider_obj in riders:
			(rider_obj as Rider).step(delta)
		cop.step(delta)

	check(player.distance > start_distance + 100.0, "player advances under throttle")
	var any_rival_moved := false
	for ai in rivals:
		if ai.rider.distance > 40.0:
			any_rival_moved = true
	check(any_rival_moved, "rivals race on their own")
	check(not police.has_busted, "no bust while riding clean")

	# Combat: park a rival alongside and hit it.
	var target: Rider = rivals[0].rider
	target.distance = player.distance + 0.5
	target.lateral = player.lateral + 1.2
	target.state = Rider.State.RIDING
	var health_before := target.health
	player.stamina = StaminaRules.MAX
	var landed := player.try_attack(1.0, false, riders)
	check(landed, "attack in range lands")
	check(target.health < health_before, "landed hit removes health")
	check(player.stamina < StaminaRules.MAX, "attacking spends stamina")

	# Crash + bust: stop the player on top of the cop.
	player.crash()
	cop.distance = player.distance
	cop.lateral = player.lateral
	var busted := police.step(delta, player)
	check(busted, "crashing on a cop is a bust")

	for rider_obj in riders:
		(rider_obj as Rider).queue_free()
	cop.queue_free()
	track.queue_free()
