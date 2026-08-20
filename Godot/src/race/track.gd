class_name Track
extends Node3D
## Builds the whole highway from a track definition: a Curve3D spine, a road
## ribbon ArrayMesh with real asphalt, verges, guardrails and instanced props.
##
## All movers work in track space (distance s, lateral x); this node is the
## single converter between track space and world space.

var definition: Dictionary
var curve: Curve3D
var length: float = 0.0
var half_width: float = 10.0

const SEGMENT := 6.0  # metres of road per mesh cross-section


func build(track_def: Dictionary, rng_seed: int = 1337) -> void:
	definition = track_def
	half_width = float(track_def["width"]) * 0.5
	_build_curve(rng_seed)
	_build_road()
	_build_ground()
	_build_guardrails()
	_build_props(rng_seed)


## Deterministic spine: sweeping sine curves + hills, seeded so the same track
## is identical every run (replays, tests, fair racing lines).
func _build_curve(rng_seed: int) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed + String(definition["id"]).hash()

	curve = Curve3D.new()
	curve.bake_interval = 2.0
	var track_length := float(definition["length"])
	var curviness := float(definition["curviness"])
	var hills := float(definition["hills"])

	var phase1 := rng.randf_range(0.0, TAU)
	var phase2 := rng.randf_range(0.0, TAU)
	var step := 40.0
	var count := int(track_length / step) + 2
	for i in count:
		var z := i * step
		var x := sin(z * 0.004 + phase1) * 60.0 * curviness \
			+ sin(z * 0.0013 + phase2) * 110.0 * curviness
		var y := (sin(z * 0.002 + phase2) * 9.0 + sin(z * 0.0007 + phase1) * 14.0) * hills
		curve.add_point(Vector3(x, y, z))
	length = curve.get_baked_length()


func sample(distance: float, lateral: float, height: float = 0.0) -> Transform3D:
	var d := clampf(distance, 0.0, length)
	var t := curve.sample_baked_with_rotation(d, true, true)
	var origin := t.origin + t.basis.x * lateral + t.basis.y * height
	return Transform3D(t.basis, origin)


func forward(distance: float) -> Vector3:
	var d := clampf(distance, 0.0, length)
	return -curve.sample_baked_with_rotation(d, true, true).basis.z


func _road_material() -> Material:
	var mat := ShaderMaterial.new()
	mat.shader = load("res://src/shaders/road.gdshader")
	_set_tex(mat, "albedo_tex", "res://assets/textures/asphalt_02_Diffuse.jpg")
	_set_tex(mat, "normal_tex", "res://assets/textures/asphalt_02_nor_gl.jpg")
	_set_tex(mat, "arm_tex", "res://assets/textures/asphalt_02_arm.jpg")
	return mat


static func _set_tex(mat: ShaderMaterial, pname: String, path: String) -> void:
	if ResourceLoader.exists(path):
		mat.set_shader_parameter(pname, load(path))


## The road ribbon: two triangles per segment, UV.v runs down the track so the
## asphalt texture tiles along it without stretching.
func _build_road() -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var steps := int(length / SEGMENT)
	var v_tile := 0.35
	for i in steps:
		var d0 := i * SEGMENT
		var d1 := minf(d0 + SEGMENT, length)
		var t0 := sample(d0, 0.0)
		var t1 := sample(d1, 0.0)
		var l0 := t0.origin - t0.basis.x * half_width
		var r0 := t0.origin + t0.basis.x * half_width
		var l1 := t1.origin - t1.basis.x * half_width
		var r1 := t1.origin + t1.basis.x * half_width
		var n0 := t0.basis.y
		var n1 := t1.basis.y
		var v0 := d0 * v_tile
		var v1 := d1 * v_tile

		st.set_normal(n0); st.set_uv(Vector2(0, v0)); st.add_vertex(l0)
		st.set_normal(n0); st.set_uv(Vector2(1, v0)); st.add_vertex(r0)
		st.set_normal(n1); st.set_uv(Vector2(1, v1)); st.add_vertex(r1)

		st.set_normal(n0); st.set_uv(Vector2(0, v0)); st.add_vertex(l0)
		st.set_normal(n1); st.set_uv(Vector2(1, v1)); st.add_vertex(r1)
		st.set_normal(n1); st.set_uv(Vector2(0, v1)); st.add_vertex(l1)
	st.generate_tangents()

	var mesh_instance := MeshInstance3D.new()
	mesh_instance.name = "Road"
	mesh_instance.mesh = st.commit()
	mesh_instance.material_override = _road_material()
	add_child(mesh_instance)


## Wide verge strips either side of the road so the world does not end at the
## white line. Textured with the aerial grass/rock set per biome.
func _build_ground() -> void:
	var biome := String(definition.get("biome", "coast"))
	var ground_tex := "res://assets/textures/aerial_grass_rock_Diffuse.jpg"
	var tint := Color(1, 1, 1)
	match biome:
		"desert": tint = Color(1.25, 1.05, 0.72)
		"city", "night": ground_tex = "res://assets/textures/concrete_wall_008_Diffuse.jpg"
		"mountain": tint = Color(0.85, 0.9, 0.85)

	var mat := StandardMaterial3D.new()
	if ResourceLoader.exists(ground_tex):
		mat.albedo_texture = load(ground_tex)
		mat.uv1_scale = Vector3(28, 28, 1)
	mat.albedo_color = tint
	mat.roughness = 1.0

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var steps := int(length / (SEGMENT * 2.0))
	var verge := 90.0
	for i in steps:
		var d0 := i * SEGMENT * 2.0
		var d1 := minf(d0 + SEGMENT * 2.0, length)
		for side: float in [-1.0, 1.0]:
			var t0 := sample(d0, 0.0)
			var t1 := sample(d1, 0.0)
			var in0 := t0.origin + t0.basis.x * (half_width * side) - Vector3.UP * 0.02
			var out0 := t0.origin + t0.basis.x * ((half_width + verge) * side) - Vector3.UP * 2.0
			var in1 := t1.origin + t1.basis.x * (half_width * side) - Vector3.UP * 0.02
			var out1 := t1.origin + t1.basis.x * ((half_width + verge) * side) - Vector3.UP * 2.0
			var u0 := d0 * 0.02
			var u1 := d1 * 0.02
			if side < 0:
				_quad(st, out0, in0, in1, out1, Vector2(0, u0), Vector2(1, u0), Vector2(1, u1), Vector2(0, u1))
			else:
				_quad(st, in0, out0, out1, in1, Vector2(0, u0), Vector2(1, u0), Vector2(1, u1), Vector2(0, u1))
	var mesh_instance := MeshInstance3D.new()
	mesh_instance.name = "Ground"
	mesh_instance.mesh = st.commit()
	mesh_instance.material_override = mat
	add_child(mesh_instance)


static func _quad(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, d: Vector3,
		ua: Vector2, ub: Vector2, uc: Vector2, ud: Vector2) -> void:
	var n := (b - a).cross(c - a).normalized()
	if n.y < 0.0:
		n = -n
	st.set_normal(n); st.set_uv(ua); st.add_vertex(a)
	st.set_normal(n); st.set_uv(ub); st.add_vertex(b)
	st.set_normal(n); st.set_uv(uc); st.add_vertex(c)
	st.set_normal(n); st.set_uv(ua); st.add_vertex(a)
	st.set_normal(n); st.set_uv(uc); st.add_vertex(c)
	st.set_normal(n); st.set_uv(ud); st.add_vertex(d)


## Guardrails as one MultiMesh per side: hundreds of posts+rails, two draw calls.
func _build_guardrails() -> void:
	var rail_mesh := BoxMesh.new()
	rail_mesh.size = Vector3(0.08, 0.35, SEGMENT + 0.3)
	var mat := StandardMaterial3D.new()
	var metal := "res://assets/textures/rusty_metal_02_Diffuse.jpg"
	if ResourceLoader.exists(metal):
		mat.albedo_texture = load(metal)
	mat.metallic = 0.6
	mat.roughness = 0.5
	rail_mesh.material = mat

	var steps := int(length / SEGMENT)
	for side: float in [-1.0, 1.0]:
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = rail_mesh
		mm.instance_count = steps
		for i in steps:
			var d := i * SEGMENT + SEGMENT * 0.5
			var t := sample(d, (half_width + 0.6) * side, 0.55)
			mm.set_instance_transform(i, t)
		var inst := MultiMeshInstance3D.new()
		inst.name = "Guardrail_L" if side < 0 else "Guardrail_R"
		inst.multimesh = mm
		add_child(inst)


## Roadside props: trees (coast/mountain), cacti-ish (desert), buildings (city),
## streetlights (night). All MultiMesh — thousands of props, a handful of draw calls.
func _build_props(rng_seed: int) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed * 7 + 1
	var biome := String(definition.get("biome", "coast"))

	var prop_path := "res://assets/models/tree.glb"
	var scale_range := Vector2(0.8, 1.6)
	match biome:
		"desert":
			prop_path = "res://assets/models/rock.glb"
		"city", "night":
			prop_path = "res://assets/models/building.glb"
			scale_range = Vector2(0.9, 2.2)

	var mesh := _extract_mesh(prop_path)
	if mesh == null:
		mesh = _fallback_prop_mesh(biome)

	var count := int(length / 26.0)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = count * 2
	var idx := 0
	for i in count:
		for side: float in [-1.0, 1.0]:
			var d := i * 26.0 + rng.randf_range(-8.0, 8.0)
			var lateral := (half_width + rng.randf_range(6.0, 34.0)) * side
			var t := sample(clampf(d, 0.0, length), lateral, -0.4)
			var s := rng.randf_range(scale_range.x, scale_range.y)
			t.basis = Basis(Vector3.UP, rng.randf_range(0.0, TAU)).scaled(Vector3(s, s, s))
			mm.set_instance_transform(idx, t)
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = "Props"
	inst.multimesh = mm
	add_child(inst)

	if biome == "night":
		_build_streetlights()


func _build_streetlights() -> void:
	# Light poles with emissive heads — cheap night-city mood without shadowed lights.
	var pole := CylinderMesh.new()
	pole.top_radius = 0.06
	pole.bottom_radius = 0.09
	pole.height = 6.0
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.1, 0.1, 0.12)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.75, 0.4)
	mat.emission_energy_multiplier = 2.0
	pole.material = mat

	var steps := int(length / 60.0)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = pole
	mm.instance_count = steps
	for i in steps:
		var side := -1.0 if i % 2 == 0 else 1.0
		var t := sample(i * 60.0, (half_width + 1.4) * side, 3.0)
		mm.set_instance_transform(i, t)
	var inst := MultiMeshInstance3D.new()
	inst.name = "Streetlights"
	inst.multimesh = mm
	add_child(inst)


static func _extract_mesh(path: String) -> Mesh:
	if not ResourceLoader.exists(path):
		return null
	var scene: PackedScene = load(path)
	if scene == null:
		return null
	var node := scene.instantiate()
	var mesh := _find_mesh(node)
	node.queue_free()
	return mesh


static func _find_mesh(node: Node) -> Mesh:
	if node is MeshInstance3D and node.mesh != null:
		return node.mesh
	for child in node.get_children():
		var found := _find_mesh(child)
		if found != null:
			return found
	return null


static func _fallback_prop_mesh(biome: String) -> Mesh:
	# Simple but shaded: a capsule canopy reads as a tree at 150 km/h.
	if biome == "city" or biome == "night":
		var box := BoxMesh.new()
		box.size = Vector3(8, 18, 8)
		var m := StandardMaterial3D.new()
		m.albedo_color = Color(0.16, 0.17, 0.2)
		box.material = m
		return box
	var capsule := CapsuleMesh.new()
	capsule.radius = 1.6
	capsule.height = 5.0
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.15, 0.32, 0.14)
	capsule.material = mat
	return capsule
