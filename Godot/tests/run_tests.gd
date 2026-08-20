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
	_test_story()
	_test_bike_specs()
	_test_track_geometry()
	_test_world_biomes()
	_test_heat()
	_test_classic_rules()
	_test_view()
	_test_race_simulation()
	_test_rider_rig()
	_test_cinematic_hud()

	# A compile failure in a dependency can silently skip whole test functions;
	# demanding the full check count turns that into a loud failure.
	const EXPECTED_CHECKS := 165
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
	check(CombatMath.traffic_hit_damage(20.0) > 50.0, "traffic hit hurts")
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
	check(TrackCatalog.find("downtown")["name"] == "The City", "find by id")
	check(TrackCatalog.find("coast_run")["name"] == "Pacific Coast", "pacific coast named")
	check(TrackCatalog.find("sierra_pass")["name"] == "Sierra Nevada", "sierra named")
	check(TrackCatalog.to_miles(1609.34) > 0.99 and TrackCatalog.to_miles(1609.34) < 1.01, "metres to miles")
	check(TrackCatalog.at(99)["id"] == "night_city", "index clamps to last")
	check(TrackCatalog.at(-5)["id"] == "coast_run", "index clamps to first")


func _test_campaign_ledger() -> void:
	var save := {"cash": 100, "chapter": 0}
	var result := Campaign.apply_result(save, 1, 2, 800, false, 50)
	check(bool(result["won"]), "P1 wins the event")
	check(Campaign.total_events() == 25, "25 campaign events")
	check(String(Campaign.event_at(0)["gang"]) == "Desades", "division gang assigned")
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


func _test_story() -> void:
	var busted := Story.vignette({"player_name": "Ace", "chapter": 0, "grudges": {}},
		{"busted": true, "won": false, "game_over": false, "champion": false})
	check(String(busted["speaker"]).find("OFFICER") >= 0 or String(busted["title"]) == "BUSTED",
		"bust vignette is a cop")
	var pre := Story.pre_race_copy({"chapter": 0, "grudges": {}, "player_name": "Ace"})
	check(String(pre["body"]).length() > 20, "pre-race has story copy")
	check(not Story.is_hostile({"grudges": {}}, "natasha"), "Natasha starts friendly")
	check(Story.is_hostile({"grudges": {"natasha": 2}}, "natasha"), "hitting Natasha is a grudge")
	check(Story.PROLOGUE.length() > 40, "prologue exists")

	var broke := {"cash": 50, "chapter": 0}
	var collapsed := Campaign.apply_result(broke, 8, 0, 800, true, 200)
	check(bool(collapsed["game_over"]), "broke after bust is game over")
	var last := {"cash": 2000, "chapter": 24}
	var crown := Campaign.apply_result(last, 1, 0, 4000, false, 0)
	check(bool(crown["champion"]), "winning event 25 is the championship")
	check(Campaign.ROSTER.size() == 15, "fifteen named rashers including player pack")
	check(Campaign.RIVAL_COUNT == 14, "fourteen other rashers on the grid")
	check(Story.CLUB.find("PANZER") >= 0, "club is Der Panzer Klub")
	check(Story.SHOP.find("OLLEY") >= 0, "shop is Olley's")


func _test_bike_specs() -> void:
	var rat := BikeSpecs.find("rat")
	var tuned := BikeSpecs.effective(rat, 5, 5)
	check(float(tuned["top_speed"]) > float(rat["top_speed"]), "engine stages add speed")
	check(float(tuned["handling"]) > float(rat["handling"]), "tire stages add handling")
	check(BikeSpecs.find("nonsense")["id"] == "rat", "unknown bike falls back")
	check(BikeSpecs.upgrade_cost(4) > BikeSpecs.upgrade_cost(0), "stages get pricier")
	check(String(BikeSpecs.find("rat")["name"]) == "Panda 250", "starter is Panda 250")
	check(String(BikeSpecs.find("sport")["name"]) == "Shuriken 600", "mid bike is Shuriken")
	check(String(BikeSpecs.find("kami")["name"]) == "Kamikaze 750", "Kamikaze 750 in shop")
	check(String(BikeSpecs.find("super")["name"]) == "Diablo 1000", "Diablo 1000 in shop")
	check(BikeSpecs.all().size() == 4, "four shop bikes")


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
	check(_min_forward_dot(track, 50.0, 160.0) < 0.85, "coast turns within 200 m of the grid")
	track.queue_free()

	var city := Track.new()
	root.add_child(city)
	city.build(TrackCatalog.find("downtown"))
	check(_min_forward_dot(city, 50.0, 160.0) < 0.50, "city corners harder than coast")
	var s300 := city.sample(300.0, 0.0)
	check(absf(s300.origin.x) > 12.0, "city has left the start axis by 300 m")
	city.queue_free()

	track = Track.new()
	root.add_child(track)
	track.build(TrackCatalog.find("coast_run"))
	var kinds := {}
	for hz in track.hazards:
		kinds[String(hz["kind"])] = true
	check(bool(kinds.get("oil", false)), "oil slick on track")
	check(bool(kinds.get("deer", false)), "deer on track")
	check(bool(kinds.get("cow", false)), "cow on track")
	check(track.get_node_or_null("FinishBanner") != null, "chequered finish banner")
	track.queue_free()


func _min_forward_dot(track: Track, d0: float, d1: float) -> float:
	var f0 := track.forward(d0)
	var worst := 1.0
	var d := d0 + 15.0
	while d <= d1:
		worst = minf(worst, f0.dot(track.forward(d)))
		d += 15.0
	return worst


func _test_world_biomes() -> void:
	var coast := Track.new()
	root.add_child(coast)
	coast.build(TrackCatalog.find("coast_run"))
	check(coast.get_node_or_null("Ocean") != null, "coast has ocean")
	check(coast.get_node_or_null("Ground") != null, "coast has ground")
	check(coast.get_node_or_null("TerrainBank_R") != null, "coast has inland bank")
	coast.queue_free()

	var city := Track.new()
	root.add_child(city)
	city.build(TrackCatalog.find("downtown"))
	check(city.get_node_or_null("Sidewalk_L") != null, "city sidewalk L")
	check(city.get_node_or_null("Sidewalk_R") != null, "city sidewalk R")
	check(city.find_child("City_Shops_0", true, false) != null, "city shops")
	check(city.find_child("City_Horizon_0", true, false) != null, "city far skyline")
	check(city.find_child("StreetTrees", true, false) != null, "city street trees")
	check(city.find_child("Overpass", true, false) != null, "city overpass")
	city.queue_free()

	var desert := Track.new()
	root.add_child(desert)
	desert.build(TrackCatalog.find("palm_desert"))
	check(desert.get_node_or_null("Ground") != null, "desert ground")
	check(desert.get_node_or_null("TerrainBank_L") != null, "desert dunes")
	desert.queue_free()

	var mountain := Track.new()
	root.add_child(mountain)
	mountain.build(TrackCatalog.find("sierra_pass"))
	check(mountain.get_node_or_null("TerrainBank_L") != null, "mountain canyon L")
	check(mountain.get_node_or_null("TerrainBank_R") != null, "mountain canyon R")
	check(mountain.get_node_or_null("HorizonPeaks") != null, "mountain horizon")
	check(mountain.get_node_or_null("Tunnel") != null, "sierra tunnel")
	mountain.queue_free()

	for wav in ["wind", "ambience_coast", "ambience_desert", "ambience_city", "ambience_mountain"]:
		check(ResourceLoader.exists("res://assets/audio/%s.wav" % wav), "%s audio exists" % wav)
	check(ResourceLoader.exists("res://assets/models/palm.glb"), "palm model")
	check(ResourceLoader.exists("res://assets/models/pine.glb"), "pine model")
	check(ResourceLoader.exists("res://assets/models/building_office.glb"), "office tower")


func _test_heat() -> void:
	var heat := HeatDirector.new()
	check(heat.heat == 0.0, "heat starts cold")
	for i in 6:
		heat.on_punch()
	check(heat.heat >= HeatDirector.SPAWN_THRESHOLD, "punch raises heat enough to spawn")
	check(heat.should_spawn_cop(0, 2), "enough heat spawns a cop")
	heat.tick(100.0)
	check(heat.heat == 0.0, "heat decays to zero")
	heat.on_idle()
	check(heat.heat >= HeatDirector.IDLE_HEAT * 0.9, "sitting still raises heat")


func _test_view() -> void:
	View.look_at(null, Vector3.ZERO)
	var n := Node3D.new()
	View.look_at(n, Vector3.ZERO)
	View.look_at(n, Vector3(0.0, 0.0, 8.0))
	check(n.position == Vector3.ZERO, "detached look_at is a no-op")
	n.position = Vector3(1.0, 2.0, 3.0)
	View.look_at(n, n.position)
	check(n.position == Vector3(1.0, 2.0, 3.0), "null/coincident look_at does not move the node")
	n.free()


func _test_classic_rules() -> void:
	var track := Track.new()
	root.add_child(track)
	track.build(TrackCatalog.find("coast_run"))

	var player := Rider.new()
	player.is_player = true
	root.add_child(player)
	player.setup(track, 40.0, 0.0)
	player.stamina = StaminaRules.MAX
	player.weapon = CombatMath.Weapon.BAT

	var cop := Rider.new()
	cop.is_police = true
	root.add_child(cop)
	cop.setup(track, 40.0, 1.2)
	cop.health = 100.0
	cop.stamina = StaminaRules.MAX
	cop.state = Rider.State.RIDING
	var cop_health := cop.health
	var landed := player.try_attack(1.0, false, [player, cop])
	check(not landed, "motor officers are immune to punches")
	check(is_equal_approx(cop.health, cop_health), "cop health unchanged")

	player.speed = 40.0
	player.launch(8.0)
	player.step(0.08)
	check(player.is_airborne(), "cow ramp launches the rider")

	player.air_height = 0.0
	player.air_v = 0.0
	player.crash()
	player.state = Rider.State.RUNNING
	player.set_dropped_bike(player.distance + 20.0, player.lateral)
	player.in_brake = 1.0
	var held := player.distance
	player.step(0.25)
	check(is_equal_approx(player.distance, held), "brake holds still while running back")

	player.queue_free()
	cop.queue_free()
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
	cop.setup(track, 28.0, 0.0)  # behind the player on the grid
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

	# Crash + bust: classic rule — only while down near an officer.
	player.crash()
	cop.distance = player.distance
	cop.lateral = player.lateral
	police.has_busted = false
	police._grace = 5.0
	check(player.is_vulnerable_to_police(), "crashed player is vulnerable")
	check(not police.step(delta, player), "grace blocks immediate bust")
	police._grace = 0.0
	check(police.step(delta, player), "crash near cop is a bust")
	police.has_busted = false
	player.state = Rider.State.RIDING
	player.speed = 30.0
	check(not police.step(delta, player), "clean riding is not a bust")

	for rider_obj in riders:
		(rider_obj as Rider).queue_free()
	cop.queue_free()
	track.queue_free()


func _test_rider_rig() -> void:
	check(ResourceLoader.exists("res://assets/models/bike.glb"), "sportbike glb exists")
	var scene: PackedScene = load("res://assets/models/bike.glb")
	check(scene != null, "sportbike glb loads")
	if scene == null:
		return
	var model := scene.instantiate() as Node3D
	root.add_child(model)
	var ap := model.find_child("AnimationPlayer", true, false) as AnimationPlayer
	check(ap != null, "bike.glb has AnimationPlayer")
	var skel := model.find_child("Skeleton3D", true, false)
	check(skel != null, "bike.glb has Skeleton3D")
	check(model.find_child("cockpit_cam", true, false) != null, "cockpit camera marker")
	check(model.find_child("wheel_f", true, false) != null, "front wheel named for spin")
	check(ResourceLoader.exists("res://assets/models/bike_rat.glb"), "Panda 250 glb exists")
	check(ResourceLoader.exists("res://assets/models/bike_kami.glb"), "Kamikaze glb exists")
	check(ResourceLoader.exists("res://assets/models/bike_super.glb"), "Diablo glb exists")
	var names := PackedStringArray()
	if ap != null:
		names = ap.get_animation_list()
	var joined := " ".join(names).to_lower()
	check("ride" in joined, "ride clip exported")
	check("punch_l" in joined or "punch" in joined, "punch clip exported")
	check("kick" in joined, "kick clip exported")
	check("crash" in joined, "crash clip exported")
	check("run" in joined, "run clip exported")
	model.queue_free()


func _test_cinematic_hud() -> void:
	var gs: GDScript = load("res://src/autoload/game_state.gd")
	check(int(gs.get("SCHEMA")) == 3, "save schema is 3")
	var defaults: Dictionary = gs.call("_default_save")
	check(String(defaults.get("graphics_quality", "")) == "high", "default quality is high")
	check(not bool(defaults.get("mirrors", true)), "mirrors off by default")

	var gfx_script: GDScript = load("res://src/autoload/graphics_settings.gd")
	var state_script: GDScript = load("res://src/autoload/game_state.gd")
	var state: Node = state_script.new()
	state.name = "GameState"
	root.add_child(state)
	var gfx: Node = gfx_script.new()
	root.add_child(gfx)
	check(bool(gfx.call("is_high")), "GraphicsSettings defaults to high")
	check(not bool(gfx.call("mirrors_enabled")), "GraphicsSettings mirrors default off")
	check(is_equal_approx(float(gfx.call("shadow_distance")), 180.0), "high shadow distance")
	gfx.call("set_high", false)
	check(is_equal_approx(float(gfx.call("shadow_distance")), 80.0), "low shadow distance")
	gfx.queue_free()
	state.queue_free()

	check(ResourceLoader.exists("res://src/ui/Settings.tscn"), "settings scene exists")
	var packed: PackedScene = load("res://src/ui/Settings.tscn")
	var settings := packed.instantiate() as Control
	check(settings != null and settings.get_script() != null, "settings builds a panel")
	if settings != null:
		settings.free()

	var pc := PlayerController.new()
	check(pc.has_method("begin_touch_punch"), "touch punch windup API")
	check(pc.has_method("end_touch_punch"), "touch punch release API")
	pc.free()

	var city := Track.new()
	root.add_child(city)
	city.build(TrackCatalog.find("downtown"))
	check(city.find_child("Landmark", true, false) != null, "city landmarks")
	check(city.find_child("Billboard", true, false) != null, "city billboards")
	check(city.light_anchors.size() > 0, "streetlight anchors for the omni pool")
	var road := city.get_node_or_null("Road") as MeshInstance3D
	check(road != null and road.material_override is ShaderMaterial, "road shader material")
	city.set_wetness(0.9)
	var wet := 0.0
	if road != null:
		wet = float((road.material_override as ShaderMaterial).get_shader_parameter("wetness"))
	check(wet > 0.8, "set_wetness updates the road")
	var landmark := city.find_child("Landmark", true, false) as Node3D
	if landmark != null:
		landmark.set_meta("track_distance", 0.0)
		city.set_detail_window(2000.0, 80.0)
		check(not landmark.visible, "far landmark hidden by detail window")
		city.set_detail_window(0.0, 400.0)
		check(landmark.visible, "near landmark shown by detail window")
	else:
		check(false, "far landmark hidden by detail window")
		check(false, "near landmark shown by detail window")
	city.queue_free()

	var night := Track.new()
	root.add_child(night)
	night.build(TrackCatalog.find("night_city"))
	var weather_script: GDScript = load("res://src/race/weather_director.gd")
	var weather: Node3D = weather_script.new()
	root.add_child(weather)
	weather.call("configure", night, null, null, "night")
	check(bool(weather.get("raining")), "night city rains on high")
	weather.call("configure", night, null, null, "desert")
	check(not bool(weather.get("raining")), "desert stays dry")
	weather.queue_free()
	night.queue_free()

	var coast := Track.new()
	root.add_child(coast)
	coast.build(TrackCatalog.find("coast_run"))
	check(coast.get_node_or_null("OceanFoam") != null, "coast foam strip")
	var traffic := Traffic.new()
	root.add_child(traffic)
	traffic.build(coast, 8, null)
	check(traffic.cars.size() == 8, "traffic pool size")
	check(traffic.get_child_count() >= 2, "several traffic mesh kits")
	var vary := traffic.cars.size() >= 2 and not is_equal_approx(
		float(traffic.cars[0]["speed"]), float(traffic.cars[1]["speed"]))
	check(vary, "traffic speed variance")
	traffic.queue_free()
	coast.queue_free()

	check(ResourceLoader.exists("res://assets/models/kenney/car/car_sedan.glb"), "Kenney sedan")
	check(ResourceLoader.exists("res://assets/models/kenney/city/kenney_shop_a.glb"), "Kenney shop")
	check(ResourceLoader.exists("res://assets/models/kenney_tree.glb"), "Kenney tree")
	check(ResourceLoader.exists("res://assets/models/kenney_cactus_tall.glb"), "Kenney cactus")
	check(ResourceLoader.exists("res://assets/textures/leather_red_02_Diffuse.jpg"), "CC0 leather")
	check(ResourceLoader.exists("res://assets/textures/sand_01_Diffuse.jpg"), "CC0 sand")
	check(ResourceLoader.exists("res://assets/textures/rubber_tiles_Diffuse.jpg"), "CC0 rubber")
	check(ResourceLoader.exists("res://assets/textures/painted_worn_brick_Diffuse.jpg"), "CC0 brick")
	check(FileAccess.file_exists("res://assets/CC0.md"), "CC0 credits")
	check(Track._first_model([
		"res://assets/models/missing_kit.glb",
		"res://assets/models/kenney/city/kenney_shop_a.glb",
	]) == "res://assets/models/kenney/city/kenney_shop_a.glb", "first_model skips missing kits")

