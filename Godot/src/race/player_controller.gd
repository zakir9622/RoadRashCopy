class_name PlayerController
extends Node
## Keyboard + touch input for the player rider. Touch fields are written by the
## dashboard HUD; this node merges them with desktop controls each physics tick.

var rider: Rider
var get_opponents: Callable = Callable()

var touch_steer: float = 0.0
var touch_throttle: float = 0.0
var touch_brake: float = 0.0
var touch_attack_left: bool = false
var touch_attack_right: bool = false
var touch_kick: bool = false
var touch_nitro: bool = false
var touch_windup_left: bool = false
var touch_windup_right: bool = false


func _physics_process(_delta: float) -> void:
	if rider == null:
		return

	var steer := Input.get_axis("steer_left", "steer_right")
	if absf(touch_steer) > 0.05:
		steer = touch_steer

	var throttle := Input.get_action_strength("throttle")
	if touch_throttle > 0.05:
		throttle = touch_throttle

	var brake := Input.get_action_strength("brake")
	if touch_brake > 0.05:
		brake = touch_brake

	rider.in_steer = clampf(steer, -1.0, 1.0)
	rider.in_throttle = clampf(throttle, 0.0, 1.0)
	rider.in_brake = clampf(brake, 0.0, 1.0)
	rider.in_nitro = Input.is_action_pressed("nitro") or touch_nitro

	var opponents: Array = get_opponents.call() if get_opponents.is_valid() else []

	# Road Rash timed punch: hold attack + throttle = wind-up, release = fast steal punch.
	if Input.is_action_pressed("attack_left") and Input.is_action_pressed("throttle"):
		rider.begin_windup(-1.0)
	elif Input.is_action_just_released("attack_left") and rider.is_winding_up():
		rider.release_windup(opponents)
	elif touch_windup_left:
		rider.begin_windup(-1.0)
	elif touch_attack_left:
		rider.try_attack(-1.0, false, opponents)
		touch_attack_left = false
	elif Input.is_action_just_pressed("attack_left"):
		rider.try_attack(-1.0, false, opponents)

	if Input.is_action_pressed("attack_right") and Input.is_action_pressed("throttle"):
		rider.begin_windup(1.0)
	elif Input.is_action_just_released("attack_right") and rider.is_winding_up():
		rider.release_windup(opponents)
	elif touch_windup_right:
		rider.begin_windup(1.0)
	elif touch_attack_right:
		rider.try_attack(1.0, false, opponents)
		touch_attack_right = false
	elif Input.is_action_just_pressed("attack_right"):
		rider.try_attack(1.0, false, opponents)

	if Input.is_action_just_pressed("kick") or touch_kick:
		var side := 1.0
		for opponent in opponents:
			var other := opponent as Rider
			if other != rider and absf(other.distance - rider.distance) < 2.4:
				side = signf(other.lateral - rider.lateral)
				break
		rider.try_attack(side if side != 0.0 else 1.0, true, opponents)
		touch_kick = false

	if not Input.is_action_pressed("attack_left") and not Input.is_action_pressed("attack_right") \
			and not touch_windup_left and not touch_windup_right:
		if rider.is_winding_up() and not Input.is_action_pressed("throttle"):
			rider.cancel_windup()
