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
var hazards: Array = []
var roadblocks: Array = []

const SEGMENT := 6.0  # metres of road per mesh cross-section


func build(track_def: Dictionary, rng_seed: int = 1337) -> void:
	definition = track_def
	half_width = float(track_def["width"]) * 0.5
	_build_curve(rng_seed)
	_build_road()
	_build_ground()
	var biome := String(definition.get("biome", ""))
	if biome == "city" or biome == "night":
		_build_sidewalks()
	else:
		_build_terrain_banks()
	_build_guardrails()
	_build_props(rng_seed)
	_build_hazards(rng_seed)
	_build_roadblocks(rng_seed)
	_build_finish()
	if biome == "coast":
		_build_water()
	if biome == "city" or biome == "night":
		_build_streetlights()


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
	var biome := String(definition.get("biome", "coast"))
	match biome:
		"desert":
			mat.set_shader_parameter("asphalt_tint", Vector3(1.18, 1.05, 0.82))
			mat.set_shader_parameter("center_color", Color(0.95, 0.92, 0.78))
			mat.set_shader_parameter("wetness", 0.0)
			mat.set_shader_parameter("extra_lanes", 0.0)
		"city":
			mat.set_shader_parameter("asphalt_tint", Vector3(0.82, 0.84, 0.88))
			mat.set_shader_parameter("wetness", 0.08)
			mat.set_shader_parameter("extra_lanes", 1.0)
		"night":
			mat.set_shader_parameter("asphalt_tint", Vector3(0.55, 0.58, 0.7))
			mat.set_shader_parameter("wetness", 0.55)
			mat.set_shader_parameter("extra_lanes", 1.0)
			mat.set_shader_parameter("center_color", Color(1.0, 0.86, 0.35))
		"mountain":
			mat.set_shader_parameter("asphalt_tint", Vector3(0.78, 0.8, 0.82))
			mat.set_shader_parameter("wetness", 0.12)
			mat.set_shader_parameter("extra_lanes", 0.0)
		_:
			mat.set_shader_parameter("asphalt_tint", Vector3(1.0, 1.0, 1.0))
			mat.set_shader_parameter("wetness", 0.05)
			mat.set_shader_parameter("extra_lanes", 0.0)
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

		# Clockwise from above — Godot culls counter-clockwise faces.
		st.set_normal(n0); st.set_uv(Vector2(0, v0)); st.add_vertex(l0)
		st.set_normal(n1); st.set_uv(Vector2(1, v1)); st.add_vertex(r1)
		st.set_normal(n0); st.set_uv(Vector2(1, v0)); st.add_vertex(r0)

		st.set_normal(n0); st.set_uv(Vector2(0, v0)); st.add_vertex(l0)
		st.set_normal(n1); st.set_uv(Vector2(0, v1)); st.add_vertex(l1)
		st.set_normal(n1); st.set_uv(Vector2(1, v1)); st.add_vertex(r1)
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
	var tint := Color(0.72, 0.95, 0.62)
	var drop := 2.0
	var verge := 90.0
	match biome:
		"desert":
			tint = Color(1.28, 1.08, 0.68)
			drop = 0.6
			verge = 120.0
		"city", "night":
			ground_tex = "res://assets/textures/concrete_wall_008_Diffuse.jpg"
			tint = Color(0.72, 0.74, 0.76)
			drop = 0.08
			verge = 28.0
		"mountain":
			ground_tex = "res://assets/textures/aerial_grass_rock_Diffuse.jpg"
			tint = Color(0.55, 0.68, 0.48)
			drop = 4.5
			verge = 70.0
		"coast":
			tint = Color(0.62, 0.88, 0.55)
			drop = 1.4

	var mat := StandardMaterial3D.new()
	if ResourceLoader.exists(ground_tex):
		mat.albedo_texture = load(ground_tex)
		mat.uv1_scale = Vector3(28, 28, 1)
	mat.albedo_color = tint
	mat.roughness = 1.0

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var steps := int(length / (SEGMENT * 2.0))
	for i in steps:
		var d0 := i * SEGMENT * 2.0
		var d1 := minf(d0 + SEGMENT * 2.0, length)
		for side: float in [-1.0, 1.0]:
			var t0 := sample(d0, 0.0)
			var t1 := sample(d1, 0.0)
			var inner := half_width + (3.2 if biome == "city" or biome == "night" else 0.0)
			var in0 := t0.origin + t0.basis.x * (inner * side) - Vector3.UP * 0.02
			var out0 := t0.origin + t0.basis.x * ((inner + verge) * side) - Vector3.UP * drop
			var in1 := t1.origin + t1.basis.x * (inner * side) - Vector3.UP * 0.02
			var out1 := t1.origin + t1.basis.x * ((inner + verge) * side) - Vector3.UP * drop
			if biome == "coast" and side < 0.0:
				out0 -= Vector3.UP * 3.5
				out1 -= Vector3.UP * 3.5
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
	# Emit whichever winding is clockwise seen from above (the visible side).
	var flipped := (b - a).cross(c - a).y > 0.0
	if flipped:
		st.set_normal(n); st.set_uv(ua); st.add_vertex(a)
		st.set_normal(n); st.set_uv(uc); st.add_vertex(c)
		st.set_normal(n); st.set_uv(ub); st.add_vertex(b)
		st.set_normal(n); st.set_uv(ua); st.add_vertex(a)
		st.set_normal(n); st.set_uv(ud); st.add_vertex(d)
		st.set_normal(n); st.set_uv(uc); st.add_vertex(c)
	else:
		st.set_normal(n); st.set_uv(ua); st.add_vertex(a)
		st.set_normal(n); st.set_uv(ub); st.add_vertex(b)
		st.set_normal(n); st.set_uv(uc); st.add_vertex(c)
		st.set_normal(n); st.set_uv(ua); st.add_vertex(a)
		st.set_normal(n); st.set_uv(uc); st.add_vertex(c)
		st.set_normal(n); st.set_uv(ud); st.add_vertex(d)


## Canyon walls, desert dunes, or a coastal bluff on the inland shoulder.
func _build_terrain_banks() -> void:
	var biome := String(definition.get("biome", "coast"))
	var step := 10.0
	var samples := int(length / step) + 1
	var mat := StandardMaterial3D.new()
	var tex := "res://assets/textures/aerial_grass_rock_Diffuse.jpg"
	if ResourceLoader.exists(tex):
		mat.albedo_texture = load(tex)
	mat.uv1_scale = Vector3(0.05, 0.05, 0.05)
	mat.roughness = 0.97
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	match biome:
		"desert":
			mat.albedo_color = Color(0.95, 0.74, 0.44)
		"mountain":
			mat.albedo_color = Color(0.48, 0.52, 0.42)
		_:
			mat.albedo_color = Color(0.38, 0.52, 0.30)
	for side in [-1.0, 1.0]:
		if biome == "coast" and side < 0.0:
			continue
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		for i in samples:
			var d := clampf(float(i) * step, 0.0, length)
			var next_d := clampf(d + step, 0.0, length)
			var rise := 1.6
			match biome:
				"mountain":
					rise = 10.0 + 5.0 * sin(d * 0.035)
				"desert":
					rise = 2.4 + 1.8 * sin(d * 0.07 + side)
				"coast":
					rise = 6.0 + 1.4 * sin(d * 0.045)
			_bank_span(st, d, next_d, side, rise)
		var mi := MeshInstance3D.new()
		mi.mesh = st.commit()
		mi.material_override = mat
		mi.name = "TerrainBank_%s" % ("L" if side < 0.0 else "R")
		add_child(mi)


func _bank_span(st: SurfaceTool, d: float, next_d: float, side: float, rise: float) -> void:
	var xf := sample(d, 0.0, 0.0)
	var nxf := sample(next_d, 0.0, 0.0)
	var lat := xf.basis.x.normalized() * side
	var nlat := nxf.basis.x.normalized() * side
	var up := xf.basis.y.normalized()
	var nup := nxf.basis.y.normalized()
	var inner := xf.origin + lat * (half_width + 2.0)
	var outer := xf.origin + lat * (half_width + 16.0)
	var crest := outer + up * rise
	var ninner := nxf.origin + nlat * (half_width + 2.0)
	var nouter := nxf.origin + nlat * (half_width + 16.0)
	var ncrest := nouter + nup * rise
	var u0 := d * 0.04
	var u1 := next_d * 0.04
	_quad(st, inner, outer, nouter, ninner, Vector2(0, u0), Vector2(1, u0), Vector2(1, u1), Vector2(0, u1))
	_quad(st, outer, crest, ncrest, nouter, Vector2(1, u0), Vector2(1.4, u0), Vector2(1.4, u1), Vector2(1, u1))


func _build_sidewalks() -> void:
	var step := 4.0
	var samples := int(length / step) + 1
	var mat := StandardMaterial3D.new()
	var tex := "res://assets/textures/concrete_wall_008_Diffuse.jpg"
	if ResourceLoader.exists(tex):
		mat.albedo_texture = load(tex)
	mat.albedo_color = Color(0.78, 0.78, 0.80) if String(definition.get("biome", "")) == "city" \
		else Color(0.28, 0.28, 0.32)
	mat.roughness = 0.84
	mat.uv1_scale = Vector3(0.35, 0.35, 0.35)
	for side in [-1.0, 1.0]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		for i in samples:
			var d := clampf(float(i) * step, 0.0, length)
			var next_d := clampf(d + step, 0.0, length)
			var a := sample(d, (half_width + 0.04) * side, 0.03)
			var b := sample(d, (half_width + 4.6) * side, 0.03)
			var c := sample(next_d, (half_width + 4.6) * side, 0.03)
			var e := sample(next_d, (half_width + 0.04) * side, 0.03)
			var u0 := d * 0.08
			var u1 := next_d * 0.08
			if side < 0.0:
				_quad(st, b.origin, a.origin, e.origin, c.origin, Vector2(0, u0), Vector2(1, u0), Vector2(1, u1), Vector2(0, u1))
			else:
				_quad(st, a.origin, b.origin, c.origin, e.origin, Vector2(0, u0), Vector2(1, u0), Vector2(1, u1), Vector2(0, u1))
		var mi := MeshInstance3D.new()
		mi.mesh = st.commit()
		mi.material_override = mat
		mi.name = "Sidewalk_%s" % ("L" if side < 0.0 else "R")
		add_child(mi)
	_build_curbs()


func _build_curbs() -> void:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.82, 0.32, 0.18) if String(definition.get("biome", "")) == "city" \
		else Color(0.55, 0.55, 0.58)
	mat.roughness = 0.55
	var box := BoxMesh.new()
	box.size = Vector3(0.22, 0.16, 5.2)
	box.material = mat
	var spacing := 5.6
	var steps := maxi(int((length - 10.0) / spacing), 1)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = box
	mm.instance_count = steps * 2
	var idx := 0
	for i in steps:
		var d := 4.0 + i * spacing
		for side: float in [-1.0, 1.0]:
			mm.set_instance_transform(idx, sample(d, half_width * side, 0.09))
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = "Curbs"
	inst.multimesh = mm
	add_child(inst)


## Guardrails as one MultiMesh per side: hundreds of posts+rails, two draw calls.
func _build_guardrails() -> void:
	var biome := String(definition.get("biome", ""))
	if biome == "city" or biome == "night":
		return
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


## Roadside props: biome vegetation, or a two-row city skyline on the sidewalks.
func _build_props(rng_seed: int) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed * 7 + 1
	var biome := String(definition.get("biome", "coast"))

	if biome == "city" or biome == "night":
		_build_city_skyline(rng)
		_build_city_furniture(rng)
		return

	match biome:
		"coast":
			_scatter_prop("res://assets/models/palm.glb", "Palms", rng, 22.0, Vector2(5.5, 16.0), Vector2(0.85, 1.35), -0.05)
			_scatter_prop("res://assets/models/tree.glb", "Trees", rng, 34.0, Vector2(14.0, 36.0), Vector2(0.9, 1.7), -0.3)
		"desert":
			_scatter_prop("res://assets/models/rock.glb", "Rocks", rng, 20.0, Vector2(5.0, 32.0), Vector2(0.7, 2.1), -0.2)
			_scatter_prop("res://assets/models/cactus.glb", "Cactus", rng, 38.0, Vector2(7.0, 22.0), Vector2(0.8, 1.5), 0.0)
		"mountain":
			_scatter_prop("res://assets/models/tree.glb", "Pines", rng, 18.0, Vector2(8.0, 28.0), Vector2(0.9, 1.8), -0.2)
			_scatter_prop("res://assets/models/rock.glb", "Boulders", rng, 24.0, Vector2(5.0, 18.0), Vector2(0.8, 2.0), -0.15)
		_:
			_scatter_prop("res://assets/models/tree.glb", "Props", rng, 26.0, Vector2(6.0, 34.0), Vector2(0.8, 1.6), -0.4)


func _scatter_prop(path: String, node_name: String, rng: RandomNumberGenerator,
		spacing: float, clearance: Vector2, scale_range: Vector2, y_bias: float) -> void:
	var mesh := _extract_mesh(path)
	if mesh == null:
		mesh = _fallback_prop_mesh(String(definition.get("biome", "coast")))
	var count := maxi(int(length / spacing), 1)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = count * 2
	var idx := 0
	for i in count:
		for side: float in [-1.0, 1.0]:
			var d := i * spacing + rng.randf_range(-spacing * 0.3, spacing * 0.3)
			var lateral := (half_width + rng.randf_range(clearance.x, clearance.y)) * side
			var t := sample(clampf(d, 0.0, length), lateral, y_bias)
			var s := rng.randf_range(scale_range.x, scale_range.y)
			t.basis = Basis(Vector3.UP, rng.randf_range(0.0, TAU)).scaled(Vector3(s, s, s))
			var aabb := mesh.get_aabb()
			t.origin += t.basis.y.normalized() * (-aabb.position.y * s)
			mm.set_instance_transform(idx, t)
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = node_name
	inst.multimesh = mm
	add_child(inst)


func _build_city_skyline(rng: RandomNumberGenerator) -> void:
	# Street frontage: shops on the sidewalk. Towers one lot back.
	_city_row(rng, ["res://assets/models/building_shop.glb"], 20.0, 2.7, Vector2(0.9, 1.2), Vector2(0.85, 1.05), "City_Shops")
	_city_row(rng, [
		"res://assets/models/building.glb",
		"res://assets/models/building_apartment.glb",
		"res://assets/models/building_office.glb",
	], 16.0, 9.2, Vector2(0.85, 1.4), Vector2(0.75, 1.85), "City_Towers")


func _city_row(rng: RandomNumberGenerator, kits: Array, spacing: float, shoulder: float,
		width_range: Vector2, height_range: Vector2, node_name: String) -> void:
	var meshes: Array[Mesh] = []
	for kit_path in kits:
		var mesh := _extract_mesh(String(kit_path))
		if mesh != null:
			meshes.append(mesh)
	if meshes.is_empty():
		meshes.append(_fallback_prop_mesh("city"))
	var count := maxi(int(length / spacing), 1)
	# One MultiMesh per kit so shop glass and tower concrete stay distinct.
	for m_i in meshes.size():
		var mesh: Mesh = meshes[m_i]
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = mesh
		var n := 0
		for i in count:
			if i % meshes.size() != m_i:
				continue
			n += 2
		if n <= 0:
			continue
		mm.instance_count = n
		var idx := 0
		for i in count:
			if i % meshes.size() != m_i:
				continue
			for side: float in [-1.0, 1.0]:
				if idx >= mm.instance_count:
					break
				var d := i * spacing + rng.randf_range(-3.0, 3.0)
				var lateral := (half_width + shoulder + rng.randf_range(0.0, 0.8)) * side
				var t := sample(clampf(d, 0.0, length), lateral, 0.0)
				var w := rng.randf_range(width_range.x, width_range.y)
				var h := rng.randf_range(height_range.x, height_range.y)
				t.basis = Basis(Vector3.UP, side * PI * 0.5).scaled(Vector3(w, h, w))
				var aabb := mesh.get_aabb()
				t.origin += Vector3.UP * (-aabb.position.y * h)
				mm.set_instance_transform(idx, t)
				idx += 1
		var inst := MultiMeshInstance3D.new()
		inst.name = "%s_%d" % [node_name, m_i]
		inst.multimesh = mm
		add_child(inst)


func _build_city_furniture(rng: RandomNumberGenerator) -> void:
	var hydrant := CylinderMesh.new()
	hydrant.top_radius = 0.12
	hydrant.bottom_radius = 0.14
	hydrant.height = 0.7
	var hmat := StandardMaterial3D.new()
	hmat.albedo_color = Color(0.82, 0.12, 0.1)
	hmat.roughness = 0.45
	hydrant.material = hmat
	var d := 28.0
	var n := 0
	while d < length - 20.0:
		n += 2
		d += 36.0
	if n <= 0:
		return
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = hydrant
	mm.instance_count = n
	d = 28.0
	var idx := 0
	while d < length - 20.0 and idx < n:
		for side in [-1.0, 1.0]:
			if idx >= n:
				break
			var t := sample(d + rng.randf_range(-4.0, 4.0), (half_width + 1.1) * side, 0.38)
			mm.set_instance_transform(idx, t)
			idx += 1
		d += 36.0
	var inst := MultiMeshInstance3D.new()
	inst.name = "City_Hydrants"
	inst.multimesh = mm
	add_child(inst)
	_build_billboards(rng)


func _build_billboards(rng: RandomNumberGenerator) -> void:
	var board := BoxMesh.new()
	board.size = Vector3(6.4, 3.6, 0.16)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.15, 0.16, 0.22)
	mat.emission_enabled = true
	mat.emission = Color(0.95, 0.45, 0.12) if rng.randf() < 0.5 else Color(0.2, 0.55, 0.95)
	mat.emission_energy_multiplier = 1.6
	board.material = mat
	var d := 90.0
	while d < length - 80.0:
		var side := -1.0 if int(d / 90.0) % 2 == 0 else 1.0
		var mi := MeshInstance3D.new()
		mi.mesh = board
		mi.name = "Billboard"
		add_child(mi)
		var xf := sample(d, (half_width + 5.8) * side, 4.4)
		xf.basis = Basis(Vector3.UP, side * PI * 0.5)
		mi.global_transform = xf
		d += 140.0


func _build_streetlights() -> void:
	# Light poles with emissive heads — cheap city mood without shadowed lights.
	var pole := CylinderMesh.new()
	pole.top_radius = 0.05
	pole.bottom_radius = 0.08
	pole.height = 7.2
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.12, 0.12, 0.14)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.82, 0.45) if String(definition.get("biome", "")) == "night" \
		else Color(0.95, 0.9, 0.7)
	mat.emission_energy_multiplier = 3.2 if String(definition.get("biome", "")) == "night" else 1.4
	pole.material = mat

	var spacing := 28.0
	var steps := maxi(int(length / spacing), 1)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = pole
	mm.instance_count = steps * 2
	var idx := 0
	for i in steps:
		for side: float in [-1.0, 1.0]:
			var t := sample(i * spacing, (half_width + 0.85) * side, 3.6)
			mm.set_instance_transform(idx, t)
			idx += 1
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


func _build_hazards(rng_seed: int) -> void:
	hazards.clear()
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed * 13 + 3
	# Classic set is always present so every race has oil, wildlife, and a sign.
	_add_hazard(400.0, -half_width * 0.25, "oil", 1.0)
	_add_hazard(minf(720.0, length * 0.28), half_width * 0.2, "deer", 1.0)
	_add_hazard(minf(1100.0, length * 0.42), -half_width * 0.15, "cow", -1.0)
	var steps := int(length / 180.0)
	for i in steps:
		if rng.randf() > 0.5:
			continue
		var d := i * 180.0 + rng.randf_range(20.0, 80.0)
		if d < 450.0:
			continue
		var roll := rng.randf()
		var kind := "oil"
		if roll > 0.7:
			kind = "sign"
		elif roll > 0.45:
			kind = "cow" if rng.randf() < 0.5 else "deer"
		var lat := rng.randf_range(-half_width * 0.55, half_width * 0.55)
		var dir := -1.0 if rng.randf() < 0.5 else 1.0
		_add_hazard(d, lat, kind, dir)


func _add_hazard(d: float, lat: float, kind: String, dir: float) -> void:
	var hz := {"distance": d, "lateral": lat, "kind": kind, "dir": dir}
	match kind:
		"sign":
			if ResourceLoader.exists("res://assets/models/sign.glb"):
				var side := 1.0 if lat >= 0.0 else -1.0
				_place_hazard_mesh(d, lat + half_width * side * 0.85, "sign.glb", 0.8)
		"deer":
			hz["node"] = _place_animal(d, lat, false)
		"cow":
			hz["node"] = _place_animal(d, lat, true)
		"oil":
			_place_oil(d, lat)
	hazards.append(hz)


func _place_hazard_mesh(d: float, lateral: float, file: String, scale: float) -> void:
	var path := "res://assets/models/%s" % file
	if not ResourceLoader.exists(path):
		return
	var scene: PackedScene = load(path)
	var node := scene.instantiate() as Node3D
	add_child(node)
	node.global_transform = sample(clampf(d, 0.0, length), lateral, 0.0)
	node.scale = Vector3(scale, scale, scale)


func _build_roadblocks(rng_seed: int) -> void:
	roadblocks.clear()
	if int(definition.get("police", 0)) < 2:
		return
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed * 19 + 5
	var count := clampi(int(length / 900.0), 1, 3)
	for i in count:
		var d := (i + 1) * (length / float(count + 1))
		var lat := rng.randf_range(-half_width * 0.35, half_width * 0.35)
		roadblocks.append({"distance": d, "lateral": lat, "width": 3.2})
		if ResourceLoader.exists("res://assets/models/barrier.glb"):
			_place_hazard_mesh(d, lat, "barrier.glb", 1.1)
		# Police cruiser parked across a lane — classic blockade.
		if ResourceLoader.exists("res://assets/models/car.glb"):
			_place_hazard_mesh(d, lat + 2.4, "car.glb", 1.0)


func _build_water() -> void:
	var plane := MeshInstance3D.new()
	var mesh := PlaneMesh.new()
	mesh.size = Vector2(length * 0.35, 120.0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.08, 0.28, 0.45)
	mat.metallic = 0.35
	mat.roughness = 0.18
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color.a = 0.88
	mesh.material = mat
	plane.mesh = mesh
	plane.name = "Ocean"
	add_child(plane)
	var t := sample(length * 0.25, -half_width - 38.0, -1.2)
	plane.global_transform = t
	plane.rotate_object_local(Vector3.RIGHT, -PI * 0.5)


func _place_animal(d: float, lateral: float, cow: bool) -> Node3D:
	var body := MeshInstance3D.new()
	var mesh := CapsuleMesh.new()
	mesh.radius = 0.42 if cow else 0.28
	mesh.height = 1.6 if cow else 1.1
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.28, 0.18, 0.1) if cow else Color(0.45, 0.32, 0.18)
	mesh.material = mat
	body.mesh = mesh
	body.name = "Cow" if cow else "Deer"
	add_child(body)
	body.global_transform = sample(clampf(d, 0.0, length), lateral, 0.55 if cow else 0.45)
	body.scale = Vector3(1.1, 0.9, 1.6) if cow else Vector3(0.7, 0.7, 1.2)
	return body


func _place_oil(d: float, lateral: float) -> void:
	var slick := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = 1.6
	mesh.bottom_radius = 1.6
	mesh.height = 0.04
	mesh.rings = 1
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.05, 0.05, 0.06, 0.85)
	mat.roughness = 0.08
	mat.metallic = 0.7
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mesh.material = mat
	slick.mesh = mesh
	slick.name = "Oil"
	add_child(slick)
	slick.global_transform = sample(clampf(d, 0.0, length), lateral, 0.03)


func _build_finish() -> void:
	for side in [-1.0, 1.0]:
		var pole := MeshInstance3D.new()
		var cyl := CylinderMesh.new()
		cyl.top_radius = 0.08
		cyl.bottom_radius = 0.1
		cyl.height = 4.2
		var mat := StandardMaterial3D.new()
		mat.albedo_color = Color(0.9, 0.9, 0.92)
		cyl.material = mat
		pole.mesh = cyl
		add_child(pole)
		pole.global_transform = sample(maxf(length - 4.0, 1.0), half_width * 0.92 * side, 2.1)
	var banner := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(half_width * 1.9, 0.12, 0.9)
	var bmat := StandardMaterial3D.new()
	bmat.albedo_texture = _checker_texture()
	bmat.uv1_scale = Vector3(10, 2, 1)
	bmat.emission_enabled = true
	bmat.emission = Color(0.9, 0.9, 0.9)
	bmat.emission_energy_multiplier = 0.25
	box.material = bmat
	banner.mesh = box
	banner.name = "FinishBanner"
	add_child(banner)
	banner.global_transform = sample(maxf(length - 4.0, 1.0), 0.0, 3.6)
	_build_finish_crowd()


static func _checker_texture() -> ImageTexture:
	var img := Image.create(8, 2, false, Image.FORMAT_RGBA8)
	for x in 8:
		for y in 2:
			img.set_pixel(x, y, Color.BLACK if (x + y) % 2 == 0 else Color.WHITE)
	return ImageTexture.create_from_image(img)


func _build_finish_crowd() -> void:
	var finish_d := maxf(length - 6.0, 2.0)
	for i in 12:
		var person := MeshInstance3D.new()
		var mesh := CapsuleMesh.new()
		mesh.radius = 0.18
		mesh.height = 1.5
		var mat := StandardMaterial3D.new()
		var palette := [ThemeColors.ACCENT, Color(0.8, 0.2, 0.15), Color(0.15, 0.2, 0.7), Color(0.9, 0.9, 0.85)]
		mat.albedo_color = palette[i % palette.size()]
		mesh.material = mat
		person.mesh = mesh
		add_child(person)
		var side := -1.0 if i < 6 else 1.0
		var lat := (half_width + 1.6 + (i % 6) * 0.45) * side
		person.global_transform = sample(finish_d + (i % 3) * 1.1, lat, 0.85)

