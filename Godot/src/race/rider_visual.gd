class_name RiderVisual
extends Node3D
## Road Rash 3D-style rider animation: wheels, tuck, punches, kicks, weapons,
## crash tumble, shoulder glance, off-bike runner.

var _wheel_f: Node3D
var _wheel_r: Node3D
var _torso: Node3D
var _rider_root: Node3D
var _head: Node3D
var _arm_l: Node3D
var _arm_r: Node3D
var _fore_l: Node3D
var _fore_r: Node3D
var _leg_l: Node3D
var _leg_r: Node3D
var _runner: Node3D
var _weapon_l: Node3D
var _weapon_r: Node3D
var _wheel_spin: float = 0.0
var _glance_timer: float = 0.0
var _body_color: Color = Color.WHITE
var _suit_color: Color = Color(0.14, 0.14, 0.16)


func initialize(rider: Node3D) -> void:
	_wheel_f = find_child("wheel_f", true, false)
	_wheel_r = find_child("wheel_r", true, false)
	_torso = find_child("torso", true, false)
	_rider_root = find_child("rider_root", true, false)
	_head = find_child("head", true, false)
	_arm_l = find_child("arm_l", true, false)
	_arm_r = find_child("arm_r", true, false)
	_fore_l = find_child("forearm_l", true, false)
	_fore_r = find_child("forearm_r", true, false)
	_leg_l = find_child("leg_l", true, false)
	_leg_r = find_child("leg_r", true, false)
	if _runner != null:
		return
	if ResourceLoader.exists("res://assets/models/runner.glb"):
		var scene: PackedScene = load("res://assets/models/runner.glb")
		_runner = scene.instantiate() as Node3D
		_runner.visible = false
		rider.add_child(_runner)
	_load_weapon_props()


func set_colors(body: Color, suit: Color) -> void:
	_body_color = body
	_suit_color = suit
	_tint_suit_meshes()


func _tint_suit_meshes() -> void:
	for node in find_children("*", "MeshInstance3D", true, false):
		var mesh := node as MeshInstance3D
		if mesh == null or mesh.mesh == null:
			continue
		for i in mesh.mesh.get_surface_count():
			var mat := mesh.get_surface_override_material(i)
			if mat == null:
				mat = mesh.mesh.surface_get_material(i)
			var std := mat as StandardMaterial3D
			if std == null:
				continue
			if std.resource_name.begins_with("suit") or std.albedo_color.v < 0.35:
				var tinted := std.duplicate() as StandardMaterial3D
				tinted.albedo_color = _suit_color.lerp(Color(0.08, 0.08, 0.1), 0.35)
				mesh.set_surface_override_material(i, tinted)


func _load_weapon_props() -> void:
	for side, path in [["l", "weapon_club.glb"], ["r", "weapon_bat.glb"]]:
		var full := "res://assets/models/%s" % path
		if not ResourceLoader.exists(full):
			continue
		var scene: PackedScene = load(full)
		var prop := scene.instantiate() as Node3D
		prop.visible = false
		prop.scale = Vector3(0.55, 0.55, 0.55)
		add_child(prop)
		if side == "l":
			_weapon_l = prop
		else:
			_weapon_r = prop


func apply(state: int, speed: float, lean: float, pose_kind: int, pose_t: float,
		running: bool, run_phase: float, weapon: int, crash_phase: float,
		windup: bool, windup_side: float) -> void:
	visible = not running
	if _runner != null:
		_runner.visible = running
		if running:
			_animate_runner(run_phase)
			return

	_wheel_spin += speed * 0.35
	if _wheel_f != null:
		_wheel_f.rotation.x = _wheel_spin
	if _wheel_r != null:
		_wheel_r.rotation.x = _wheel_spin

	var speed01 := clampf(speed / 55.0, 0.0, 1.0)
	if _torso != null:
		_torso.rotation.x = lerpf(0.10, 0.62, speed01)
	if _rider_root != null:
		_rider_root.rotation.z = lean * 0.42

	_glance_timer = maxf(_glance_timer - 0.016, 0.0)
	if absf(lean) > 0.35 and _head != null:
		_head.rotation.y = lerpf(_head.rotation.y, -lean * 0.5, 0.15)
		_glance_timer = 0.4
	elif _head != null:
		_head.rotation.y = lerpf(_head.rotation.y, 0.0, 0.12)

	if state == Rider.State.CRASHED:
		var tumble := minf(crash_phase / 0.9, 1.0)
		rotation.z = lerpf(rotation.z, PI * 0.45 * sin(tumble * PI), 0.25)
		if _rider_root != null:
			_rider_root.position.y = sin(tumble * TAU * 2.0) * 0.08
	else:
		rotation.z = lerpf(rotation.z, lean * 0.18, 0.15)
		if _rider_root != null:
			_rider_root.position.y = move_toward(_rider_root.position.y, 0.0, 0.2)

	_reset_limbs()
	_hide_weapons()
	if windup:
		_windup_pose(windup_side)
	elif pose_t > 0.0:
		var swing := sin((1.0 - pose_t) * PI)
		match pose_kind:
			1:
				_punch(-1.0, swing)
				_show_weapon(-1.0, weapon, swing)
			2:
				_punch(1.0, swing)
				_show_weapon(1.0, weapon, swing)
			3:
				_kick(swing)
	else:
		_idle_weapon(weapon)


func _windup_pose(side: float) -> void:
	var upper := _arm_l if side < 0.0 else _arm_r
	if upper != null:
		upper.rotation.x = -0.55
		upper.position.x = side * 0.15


func _punch(side: float, amount: float) -> void:
	var upper := _arm_l if side < 0.0 else _arm_r
	var fore := _fore_l if side < 0.0 else _fore_r
	if upper != null:
		upper.rotation.x = -1.15 * amount
		upper.position.x = side * amount * 0.42
	if fore != null:
		fore.rotation.x = -1.45 * amount
		fore.position.x = side * amount * 0.62


func _kick(amount: float) -> void:
	if _leg_r != null:
		_leg_r.rotation.x = 1.05 * amount
		_leg_r.position.x = 0.55 * amount
		_leg_r.position.z = 0.25 * amount
	if _leg_l != null:
		_leg_l.rotation.x = -0.15 * amount
	if _torso != null:
		_torso.rotation.z = -0.12 * amount


func _show_weapon(side: float, weapon: int, amount: float) -> void:
	if weapon == CombatMath.Weapon.FISTS or weapon == CombatMath.Weapon.KICK:
		return
	var prop := _weapon_l if side < 0.0 else _weapon_r
	if prop == null:
		return
	prop.visible = amount > 0.15
	var fore := _fore_l if side < 0.0 else _fore_r
	if fore != null:
		prop.position = fore.position + Vector3(side * 0.18, -0.05, 0.12)
		prop.rotation = fore.rotation


func _idle_weapon(weapon: int) -> void:
	if weapon == CombatMath.Weapon.CHAIN and _weapon_l != null:
		_weapon_l.visible = true
		_weapon_l.position = Vector3(-0.35, 0.05, 0.2)
	elif weapon == CombatMath.Weapon.BAT and _weapon_r != null:
		_weapon_r.visible = true
		_weapon_r.position = Vector3(0.35, 0.08, 0.15)


func _hide_weapons() -> void:
	if _weapon_l != null:
		_weapon_l.visible = false
	if _weapon_r != null:
		_weapon_r.visible = false


func _reset_limbs() -> void:
	for node in [_arm_l, _arm_r, _fore_l, _fore_r, _leg_l, _leg_r]:
		if node == null:
			continue
		node.rotation.x = move_toward(node.rotation.x, 0.0, 0.18)
		node.position.x = move_toward(node.position.x, 0.0, 0.18)
		node.position.z = move_toward(node.position.z, 0.0, 0.18)


func _animate_runner(phase: float) -> void:
	var swing := sin(phase * TAU * 2.2) * 0.42
	for side in ["l", "r"]:
		var leg := _runner.find_child("leg_%s" % side, true, false)
		if leg != null:
			leg.rotation.x = swing if side == "l" else -swing
		var arm := _runner.find_child("arm_%s" % side, true, false)
		if arm != null:
			arm.rotation.x = -swing * 1.1 if side == "l" else swing * 1.1
