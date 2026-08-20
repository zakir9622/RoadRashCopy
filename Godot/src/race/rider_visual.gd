class_name RiderVisual
extends Node3D
## Plays skeletal Road Rash 3D clips (ride / tuck / punch / kick / crash / run)
## from the imported GLB AnimationPlayer, with wheel spin and weapon attachments.

var _wheel_f: Node3D
var _wheel_r: Node3D
var _armature: Node3D
var _skeleton: Skeleton3D
var _ap: AnimationPlayer
var _runner: Node3D
var _runner_ap: AnimationPlayer
var _weapon_l: Node3D
var _weapon_r: Node3D
var _hand_l_attach: BoneAttachment3D
var _hand_r_attach: BoneAttachment3D
var _bike_meshes: Array = []
var _wheel_spin: float = 0.0
var _suit_color: Color = Color(0.14, 0.14, 0.16)
var _current: String = ""
var _clip_map: Dictionary = {}
var _flame: MeshInstance3D


func initialize(rider: Node3D) -> void:
	_wheel_f = find_child("wheel_f", true, false)
	_wheel_r = find_child("wheel_r", true, false)
	_armature = find_child("Armature", true, false)
	_skeleton = find_child("Skeleton3D", true, false) as Skeleton3D
	_ap = find_child("AnimationPlayer", true, false) as AnimationPlayer
	if _ap != null:
		_index_clips(_ap)
	for child in find_children("*", "MeshInstance3D", true, false):
		var mesh := child as MeshInstance3D
		if mesh == null:
			continue
		var n := String(mesh.name).to_lower()
		if n.begins_with("wheel") or n.begins_with("tyre") or n.begins_with("disc"):
			continue
		if n.begins_with("mesh_") or n.begins_with("helmet") or n.begins_with("visor") \
				or n.begins_with("glove") or n.begins_with("boot") or n == "rider_mesh":
			continue
		_bike_meshes.append(mesh)
	_attach_weapons()
	_ensure_flame()
	if _runner != null:
		return
	if ResourceLoader.exists("res://assets/models/runner.glb"):
		var scene: PackedScene = load("res://assets/models/runner.glb")
		_runner = scene.instantiate() as Node3D
		_runner.visible = false
		rider.add_child(_runner)
		_runner_ap = _runner.find_child("AnimationPlayer", true, false) as AnimationPlayer


func set_colors(_body: Color, suit: Color) -> void:
	_suit_color = suit
	_tint_suit_meshes()


func _leather_tex() -> Texture2D:
	var diff := "res://assets/textures/leather_red_02_Diffuse.jpg"
	if ResourceLoader.exists(diff):
		var leather: Texture2D = load(diff)
		return leather
	var tex := NoiseTexture2D.new()
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_CELLULAR
	n.frequency = 0.11
	n.cellular_jitter = 0.35
	tex.noise = n
	tex.seamless = true
	tex.width = 256
	tex.height = 256
	return tex


func _clip_leaf(n: String) -> String:
	var leaf := n.to_lower()
	var parts := leaf.split("|")
	leaf = parts[parts.size() - 1]
	parts = leaf.split("/")
	return parts[parts.size() - 1]


func _index_clips(player: AnimationPlayer) -> void:
	_clip_map.clear()
	for n in player.get_animation_list():
		var key := String(n)
		var leaf := _clip_leaf(key)
		for tag in ["ride_tuck", "punch_l", "punch_r", "windup_l", "windup_r",
				"kick", "crash", "run", "remount", "ride"]:
			if leaf == tag:
				_clip_map[tag] = key
				break


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
			if std.resource_name.to_lower().begins_with("suit"):
				var tinted := std.duplicate() as StandardMaterial3D
				tinted.albedo_color = _suit_color
				tinted.albedo_texture = _leather_tex()
				tinted.uv1_scale = Vector3(3.2, 3.2, 3.2)
				tinted.roughness = 0.74
				tinted.metallic = 0.04
				var nor := "res://assets/textures/leather_red_02_nor_gl.jpg"
				if ResourceLoader.exists(nor):
					tinted.normal_enabled = true
					tinted.normal_texture = load(nor)
					tinted.normal_scale = 0.55
				var arm := "res://assets/textures/leather_red_02_arm.jpg"
				if ResourceLoader.exists(arm):
					tinted.ao_enabled = true
					tinted.ao_texture = load(arm)
					tinted.ao_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
					tinted.roughness_texture = load(arm)
					tinted.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_GREEN
				mesh.set_surface_override_material(i, tinted)


func _attach_weapons() -> void:
	if _skeleton == null:
		_load_weapon_loose()
		return
	_hand_l_attach = _bone_attach("hand_l")
	_hand_r_attach = _bone_attach("hand_r")
	_weapon_l = _spawn_chain(_hand_l_attach)
	_weapon_r = _spawn_weapon("res://assets/models/weapon_bat.glb", _hand_r_attach)


func _bone_attach(bone_name: String) -> BoneAttachment3D:
	var idx := _skeleton.find_bone(bone_name)
	if idx < 0:
		for i in _skeleton.get_bone_count():
			if bone_name in _skeleton.get_bone_name(i).to_lower():
				idx = i
				break
	var attach := BoneAttachment3D.new()
	attach.name = "attach_%s" % bone_name
	if idx >= 0:
		attach.bone_idx = idx
		attach.bone_name = _skeleton.get_bone_name(idx)
	_skeleton.add_child(attach)
	return attach


func _spawn_weapon(path: String, parent: Node) -> Node3D:
	if parent == null or not ResourceLoader.exists(path):
		return null
	var scene: PackedScene = load(path)
	var prop := scene.instantiate() as Node3D
	prop.visible = false
	prop.scale = Vector3(0.45, 0.45, 0.45)
	prop.rotation_degrees = Vector3(80, 0, 20)
	parent.add_child(prop)
	_bind_wood(prop)
	return prop


func _spawn_chain(parent: Node) -> Node3D:
	if parent == null:
		return null
	var prop := Node3D.new()
	prop.name = "chain"
	prop.visible = false
	var link := BoxMesh.new()
	link.size = Vector3(0.045, 0.09, 0.028)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.22, 0.23, 0.25)
	mat.metallic = 1.0
	mat.roughness = 0.28
	_bind_hero_metal(mat)
	link.material = mat
	for i in 8:
		var mi := MeshInstance3D.new()
		mi.mesh = link
		mi.position = Vector3(0.0, -0.08 * i, 0.02 * (i % 2))
		mi.rotation_degrees = Vector3(0.0, 40.0 if i % 2 == 0 else -40.0, 12.0)
		prop.add_child(mi)
	prop.scale = Vector3(0.9, 0.9, 0.9)
	prop.rotation_degrees = Vector3(70, 0, 10)
	parent.add_child(prop)
	return prop


func _bind_hero_metal(mat: StandardMaterial3D) -> void:
	var diff := "res://assets/textures/metal_plate_Diffuse.jpg"
	if ResourceLoader.exists(diff):
		mat.albedo_texture = load(diff)


func _bind_wood(root: Node) -> void:
	var diff := "res://assets/textures/rough_wood_Diffuse.jpg"
	if not ResourceLoader.exists(diff):
		return
	for child in root.find_children("*", "MeshInstance3D", true, false):
		var mesh := child as MeshInstance3D
		if mesh == null or mesh.mesh == null:
			continue
		for i in mesh.mesh.get_surface_count():
			var mat := mesh.get_surface_override_material(i)
			if mat == null:
				mat = mesh.mesh.surface_get_material(i)
			var std := (mat as StandardMaterial3D)
			if std == null:
				std = StandardMaterial3D.new()
			else:
				std = std.duplicate() as StandardMaterial3D
			std.albedo_texture = load(diff)
			var nor := "res://assets/textures/rough_wood_nor_gl.jpg"
			if ResourceLoader.exists(nor):
				std.normal_enabled = true
				std.normal_texture = load(nor)
			var arm := "res://assets/textures/rough_wood_arm.jpg"
			if ResourceLoader.exists(arm):
				std.ao_enabled = true
				std.ao_texture = load(arm)
				std.ao_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
				std.roughness_texture = load(arm)
				std.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_GREEN
			mesh.set_surface_override_material(i, std)


func _load_weapon_loose() -> void:
	_weapon_l = _spawn_chain(self)
	_try_loose_weapon("r", "res://assets/models/weapon_bat.glb")


func _try_loose_weapon(side: String, full: String) -> void:
	if not ResourceLoader.exists(full):
		return
	var scene: PackedScene = load(full)
	var prop := scene.instantiate() as Node3D
	prop.visible = false
	prop.scale = Vector3(0.55, 0.55, 0.55)
	add_child(prop)
	_bind_wood(prop)
	if side == "l":
		_weapon_l = prop
	else:
		_weapon_r = prop


func apply(state: int, speed: float, lean: float, pose_kind: int, pose_t: float,
		running: bool, _run_phase: float, weapon: int, _crash_phase: float,
		windup: bool, windup_side: float, nitro_on: bool = false) -> void:
	visible = not running
	_ensure_flame()
	if _flame != null:
		_flame.visible = nitro_on and not running
	if _runner != null:
		_runner.visible = running
		if running:
			_play_on(_runner_ap, "run", true)
			_hide_weapons()
			return

	_wheel_spin += speed * 0.35
	if _wheel_f != null:
		_wheel_f.rotation.x = _wheel_spin
	if _wheel_r != null:
		_wheel_r.rotation.x = _wheel_spin

	if state == Rider.State.CRASHED:
		rotation.z = lerpf(rotation.z, PI * 0.22, 0.18)
		_play("crash", false)
	elif state == Rider.State.REMOUNT:
		rotation.z = lerpf(rotation.z, lean * 0.12, 0.2)
		_play("remount", false)
	else:
		rotation.z = lerpf(rotation.z, lean * 0.18, 0.15)
		if windup:
			_play("windup_l" if windup_side < 0.0 else "windup_r", true)
		elif pose_t > 0.0:
			match pose_kind:
				1:
					_play("punch_l", false)
				2:
					_play("punch_r", false)
				3:
					_play("kick", false)
				_:
					_play_ride(speed)
		else:
			_play_ride(speed)

	if _armature != null:
		_armature.rotation.z = lean * 0.28
	_show_held_weapon(weapon, pose_kind, pose_t, windup)


func _play_ride(speed: float) -> void:
	if speed > 28.0:
		_play("ride_tuck", true)
	else:
		_play("ride", true)


func _play(tag: String, loop: bool) -> void:
	_play_on(_ap, tag, loop)


func _play_on(player: AnimationPlayer, tag: String, loop: bool) -> void:
	if player == null:
		return
	var clip := String(_clip_map.get(tag, ""))
	if clip == "" and player == _ap:
		_index_clips(player)
		clip = String(_clip_map.get(tag, ""))
	if player != _ap:
		clip = ""
		for n in player.get_animation_list():
			if _clip_leaf(String(n)) == tag:
				clip = n
				break
	if clip == "":
		return
	var anim := player.get_animation(clip)
	if anim != null:
		anim.loop_mode = Animation.LOOP_LINEAR if loop else Animation.LOOP_NONE
	if _current == "%s:%s" % [player.get_instance_id(), clip] and player.is_playing():
		return
	player.play(clip, 0.12)
	_current = "%s:%s" % [player.get_instance_id(), clip]


func _show_held_weapon(weapon: int, pose_kind: int, pose_t: float, windup: bool) -> void:
	_hide_weapons()
	if weapon == CombatMath.Weapon.FISTS or weapon == CombatMath.Weapon.KICK:
		return
	var swinging := pose_t > 0.12 or windup
	if weapon == CombatMath.Weapon.CHAIN and _weapon_l != null:
		_weapon_l.visible = swinging or pose_kind != 3
	elif weapon == CombatMath.Weapon.BAT and _weapon_r != null:
		_weapon_r.visible = swinging or pose_kind != 3


func _hide_weapons() -> void:
	if _weapon_l != null:
		_weapon_l.visible = false
	if _weapon_r != null:
		_weapon_r.visible = false


func _ensure_flame() -> void:
	if _flame != null:
		return
	_flame = MeshInstance3D.new()
	_flame.name = "NitroFlame"
	var mesh := SphereMesh.new()
	mesh.radius = 0.11
	mesh.height = 0.42
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(1.0, 0.42, 0.08, 0.8)
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.45, 0.1)
	mat.emission_energy_multiplier = 5.2
	mesh.material = mat
	_flame.mesh = mesh
	_flame.visible = false
	_flame.position = Vector3(0.0, 0.28, 0.92)
	add_child(_flame)
