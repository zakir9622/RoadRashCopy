class_name RiderVisual
extends Node3D
## Animates the bike+rider GLB like Road Rash 3D mocap: spinning wheels,
## lean/tuck, full arm extension punches, kicks, crash tumble, shoulder glance.

var _wheel_f: Node3D
var _wheel_r: Node3D
var _torso: Node3D
var _rider_root: Node3D
var _arm_l: Node3D
var _arm_r: Node3D
var _fore_l: Node3D
var _fore_r: Node3D
var _leg_r: Node3D
var _runner: Node3D
var _wheel_spin: float = 0.0


func initialize(rider: Node3D) -> void:
	_wheel_f = find_child("wheel_f", true, false)
	_wheel_r = find_child("wheel_r", true, false)
	_torso = find_child("torso", true, false)
	_rider_root = find_child("rider_root", true, false)
	_arm_l = find_child("arm_l", true, false)
	_arm_r = find_child("arm_r", true, false)
	_fore_l = find_child("forearm_l", true, false)
	_fore_r = find_child("forearm_r", true, false)
	_leg_r = find_child("leg_r", true, false)
	if _runner != null:
		return
	if ResourceLoader.exists("res://assets/models/runner.glb"):
		var scene: PackedScene = load("res://assets/models/runner.glb")
		_runner = scene.instantiate() as Node3D
		_runner.visible = false
		rider.add_child(_runner)


func apply(state: int, speed: float, lean: float, pose_kind: int, pose_t: float,
		running: bool, run_phase: float) -> void:
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

	# Road Rash 3D: tuck over bars at speed, upright when slow.
	var speed01 := clampf(speed / 55.0, 0.0, 1.0)
	if _torso != null:
		_torso.rotation.x = lerpf(0.12, 0.55, speed01)
	if _rider_root != null:
		_rider_root.rotation.z = lean * 0.35

	if state == Rider.State.CRASHED:
		rotation.z = lerpf(rotation.z, PI * 0.35, 0.2)
	else:
		rotation.z = lerpf(rotation.z, lean * 0.15, 0.15)

	_reset_limbs()
	if pose_t > 0.0:
		var swing := sin((1.0 - pose_t) * PI)
		match pose_kind:
			1:
				_punch(-1.0, swing)
			2:
				_punch(1.0, swing)
			3:
				_kick(swing)


func _punch(side: float, amount: float) -> void:
	var upper := _arm_l if side < 0.0 else _arm_r
	var fore := _fore_l if side < 0.0 else _fore_r
	if upper != null:
		upper.rotation.x = -1.0 * amount
		upper.position.x = side * amount * 0.35
	if fore != null:
		fore.rotation.x = -1.35 * amount
		fore.position.x = side * amount * 0.55


func _kick(amount: float) -> void:
	if _leg_r != null:
		_leg_r.rotation.x = 0.9 * amount
		_leg_r.position.x = 0.45 * amount


func _reset_limbs() -> void:
	for node in [_arm_l, _arm_r, _fore_l, _fore_r, _leg_r]:
		if node == null:
			continue
		node.rotation.x = move_toward(node.rotation.x, 0.0, 0.2)
		node.position.x = move_toward(node.position.x, 0.0, 0.2)


func _animate_runner(phase: float) -> void:
	var swing := sin(phase * TAU * 2.0) * 0.35
	for side in ["l", "r"]:
		var leg := _runner.find_child("leg_%s" % side, true, false)
		if leg != null:
			leg.rotation.x = swing if side == "l" else -swing
		var arm := _runner.find_child("arm_%s" % side, true, false)
		if arm != null:
			arm.rotation.x = -swing if side == "l" else swing
