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
var light_anchors: Array[Vector3] = []

const SEGMENT := 6.0  # metres of road per mesh cross-section
var _road_mat: ShaderMaterial
var _window_nodes: Array[Node3D] = []
var _ocean_mat: StandardMaterial3D
var _stream_mms: Array = []


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
	if biome == "city" or biome == "night":
		_build_overpasses()
		_build_streetlights()
		_build_landmarks()
	if biome == "mountain" or biome == "coast" or biome == "desert":
		_build_horizon_peaks()
		if biome != "city":
			_build_landmarks()
	_build_hazards(rng_seed)
	_build_roadblocks(rng_seed)
	_build_finish()
	if biome == "coast":
		_build_water()


## Piecewise corners, not a kilometre-scale sine. The old generator used
## `sin(z * 0.004)` (period ~1.57 km) so Coast (curviness 0.5) only drifted
## ~30 m sideways — the camera read as a ruler. This spine: short opening
## straight for the grid, then alternating corners and straights at radii
## a 250 cc bike can actually take. Seeded so replays stay identical.
func _build_curve(rng_seed: int) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = rng_seed + String(definition["id"]).hash()

	curve = Curve3D.new()
	curve.bake_interval = 1.2
	var track_length := float(definition["length"])
	var curviness := float(definition["curviness"])
	var hills := float(definition["hills"])

	var pos := Vector3.ZERO
	var heading := 0.0
	var traveled := 0.0
	curve.add_point(pos)

	# Opening straight so the start grid is fair, then the first corner is
	# already in the chase camera's look-ahead — Road Rash 3D, not a drag strip.
	var opening := 52.0
	pos += Vector3(sin(heading), 0.0, cos(heading)) * opening
	traveled += opening
	curve.add_point(_elevated(pos, traveled, hills))

	var turn_sign := 1.0
	var guard := 0
	while traveled < track_length and guard < 500:
		guard += 1
		var turn := (0.62 + rng.randf() * 0.78 + curviness * 0.48) * turn_sign
		var radius := clampf(52.0 / maxf(curviness, 0.45), 20.0, 86.0)
		radius *= rng.randf_range(0.78, 1.12)
		var arc := absf(turn) * radius
		var steps := maxi(5, int(ceil(arc / 8.0)))
		for _i in range(1, steps + 1):
			heading += turn / float(steps)
			var ds := arc / float(steps)
			pos += Vector3(sin(heading), 0.0, cos(heading)) * ds
			traveled += ds
			curve.add_point(_elevated(pos, traveled, hills))
			if traveled >= track_length:
				break
		turn_sign *= -1.0
		if traveled >= track_length:
			break
		var straight := rng.randf_range(48.0, 90.0) / clampf(curviness, 0.7, 1.8)
		straight = clampf(straight, 36.0, 100.0)
		var s_steps := maxi(2, int(ceil(straight / 16.0)))
		for _i in range(1, s_steps + 1):
			var ds := straight / float(s_steps)
			pos += Vector3(sin(heading), 0.0, cos(heading)) * ds
			traveled += ds
			curve.add_point(_elevated(pos, traveled, hills))
			if traveled >= track_length:
				break
	length = curve.get_baked_length()


func _elevated(xz: Vector3, s: float, hills: float) -> Vector3:
	xz.y = (sin(s * 0.007) * 8.0 + sin(s * 0.018) * 3.5) * hills
	return xz


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
			mat.set_shader_parameter("asphalt_tint", Vector3(0.72, 0.74, 0.78))
			mat.set_shader_parameter("wetness", 0.08)
			mat.set_shader_parameter("extra_lanes", 1.0)
			mat.set_shader_parameter("center_color", Color(0.98, 0.82, 0.12))
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
	_road_mat = mat
	return mat


func set_wetness(amount: float) -> void:
	if _road_mat != null:
		_road_mat.set_shader_parameter("wetness", clampf(amount, 0.0, 1.0))


func set_detail_window(distance: float, radius: float = 320.0) -> void:
	for node in _window_nodes:
		if node == null or not is_instance_valid(node):
			continue
		var d := float(node.get_meta("track_distance", distance))
		node.visible = absf(d - distance) < radius
	for entry in _stream_mms:
		var mm: MultiMesh = entry.get("mm")
		var xforms: Array = entry.get("xforms", [])
		var origins: PackedFloat32Array = entry.get("s", PackedFloat32Array())
		if mm == null:
			continue
		for i in mini(mm.instance_count, xforms.size()):
			var xf: Transform3D = xforms[i]
			if i < origins.size() and absf(origins[i] - distance) > radius * 1.6:
				xf.basis = xf.basis.scaled(Vector3(0.001, 0.001, 0.001))
			mm.set_instance_transform(i, xf)


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
		mat.uv1_scale = Vector3(18, 18, 1)
	mat.albedo_color = tint
	mat.roughness = 1.0
	_apply_pbr_maps(mat, ground_tex)

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
	var step := 6.0
	var samples := int(length / step) + 1
	var mat := StandardMaterial3D.new()
	var tex := "res://assets/textures/aerial_grass_rock_Diffuse.jpg"
	if ResourceLoader.exists(tex):
		mat.albedo_texture = load(tex)
	mat.uv1_scale = Vector3(0.08, 0.08, 0.08)
	mat.roughness = 0.97
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	_apply_pbr_maps(mat, tex)
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
	var shelf := xf.origin + lat * (half_width + 7.0) + up * (rise * 0.28)
	var outer := xf.origin + lat * (half_width + 16.0)
	var crest := outer + up * rise
	var ninner := nxf.origin + nlat * (half_width + 2.0)
	var nshelf := nxf.origin + nlat * (half_width + 7.0) + nup * (rise * 0.28)
	var nouter := nxf.origin + nlat * (half_width + 16.0)
	var ncrest := nouter + nup * rise
	var u0 := d * 0.04
	var u1 := next_d * 0.04
	_quad(st, inner, shelf, nshelf, ninner, Vector2(0, u0), Vector2(0.45, u0), Vector2(0.45, u1), Vector2(0, u1))
	_quad(st, shelf, outer, nouter, nshelf, Vector2(0.45, u0), Vector2(1, u0), Vector2(1, u1), Vector2(0.45, u1))
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
		_scatter_prop("res://assets/models/tree.glb", "StreetTrees", rng, 18.0, Vector2(1.5, 2.4), Vector2(0.55, 0.85), 0.0, [-1.0, 1.0])
		_scatter_prop("res://assets/models/car.glb", "ParkedCars", rng, 34.0, Vector2(2.0, 2.6), Vector2(0.95, 1.05), 0.0, [-1.0, 1.0], true)
		return

	match biome:
		"coast":
			_scatter_prop("res://assets/models/palm.glb", "Palms", rng, 10.0, Vector2(4.2, 9.0), Vector2(0.9, 1.4), -0.05, [1.0])
			_scatter_prop("res://assets/models/palm.glb", "PalmsOcean", rng, 16.0, Vector2(6.0, 11.0), Vector2(0.8, 1.2), -0.8, [-1.0])
			_scatter_prop("res://assets/models/tree.glb", "Trees", rng, 28.0, Vector2(12.0, 32.0), Vector2(0.9, 1.7), -0.3, [1.0])
		"desert":
			_scatter_prop("res://assets/models/rock.glb", "Rocks", rng, 16.0, Vector2(4.5, 28.0), Vector2(0.7, 2.1), -0.2)
			_scatter_prop("res://assets/models/cactus.glb", "Cactus", rng, 22.0, Vector2(6.0, 20.0), Vector2(0.8, 1.6), 0.0)
		"mountain":
			_scatter_prop("res://assets/models/pine.glb", "Pines", rng, 8.0, Vector2(3.6, 14.0), Vector2(1.0, 1.9), -0.15)
			_scatter_prop("res://assets/models/pine.glb", "PineWall", rng, 12.0, Vector2(14.0, 28.0), Vector2(1.2, 2.2), -0.2)
			_scatter_prop("res://assets/models/rock.glb", "Boulders", rng, 16.0, Vector2(4.0, 12.0), Vector2(0.8, 2.0), -0.15)
			_build_tunnel()
		_:
			_scatter_prop("res://assets/models/tree.glb", "Props", rng, 26.0, Vector2(6.0, 34.0), Vector2(0.8, 1.6), -0.4)


func _scatter_prop(path: String, node_name: String, rng: RandomNumberGenerator,
		spacing: float, clearance: Vector2, scale_range: Vector2, y_bias: float,
		sides: Array = [-1.0, 1.0], face_road: bool = false) -> void:
	var mesh := _extract_mesh(path)
	if mesh == null:
		mesh = _fallback_prop_mesh(String(definition.get("biome", "coast")))
	var count := maxi(int(length / spacing), 1)
	var side_count := maxi(sides.size(), 1)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = count * side_count
	var idx := 0
	var distances := PackedFloat32Array()
	distances.resize(mm.instance_count)
	for i in count:
		for side in sides:
			var sside := float(side)
			var d := i * spacing + rng.randf_range(-spacing * 0.28, spacing * 0.28)
			var lateral := (half_width + rng.randf_range(clearance.x, clearance.y)) * sside
			var t := sample(clampf(d, 0.0, length), lateral, y_bias)
			var s := rng.randf_range(scale_range.x, scale_range.y)
			if face_road:
				var inward := (-t.basis.x * sside)
				inward.y = 0.0
				if inward.length() < 0.001:
					inward = Vector3.FORWARD
				t.basis = Basis.looking_at(inward.normalized(), Vector3.UP).scaled(Vector3(s, s, s))
			else:
				t.basis = Basis(Vector3.UP, rng.randf_range(0.0, TAU)).scaled(Vector3(s, s, s))
			var aabb := mesh.get_aabb()
			t.origin += t.basis.y.normalized() * (-aabb.position.y * s)
			mm.set_instance_transform(idx, t)
			distances[idx] = d
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = node_name
	inst.multimesh = mm
	add_child(inst)
	if node_name == "ParkedCars" or node_name == "StreetTrees":
		var xforms: Array = []
		for i in mm.instance_count:
			xforms.append(mm.get_instance_transform(i))
		_stream_mms.append({"mm": mm, "xforms": xforms, "s": distances})


func _build_city_skyline(rng: RandomNumberGenerator) -> void:
	# RR3D city: shop fronts hard against the curb, then a wall of towers, then a far skyline.
	_city_row(rng, ["res://assets/models/building_shop.glb"], 8.2, 3.6, Vector2(0.95, 1.15), Vector2(0.9, 1.15), "City_Shops")
	_city_row(rng, [
		"res://assets/models/building.glb",
		"res://assets/models/building_apartment.glb",
		"res://assets/models/building_office.glb",
	], 11.0, 10.5, Vector2(0.95, 1.35), Vector2(0.85, 1.7), "City_Towers")
	_city_row(rng, [
		"res://assets/models/building_office.glb",
		"res://assets/models/building.glb",
	], 22.0, 24.0, Vector2(1.6, 2.4), Vector2(1.8, 2.6), "City_Horizon")


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
				var w := rng.randf_range(width_range.x, width_range.y)
				var h := rng.randf_range(height_range.x, height_range.y)
				var t := _facing_road(clampf(d, 0.0, length), lateral, 0.0, side)
				t.basis = t.basis.scaled(Vector3(w, h, w))
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
	var pole := CylinderMesh.new()
	pole.top_radius = 0.09
	pole.bottom_radius = 0.12
	pole.height = 4.2
	var pmat := StandardMaterial3D.new()
	pmat.albedo_color = Color(0.18, 0.18, 0.2)
	pole.material = pmat
	var d := 90.0
	var n := 0
	while d < length - 80.0:
		n += 1
		d += 140.0
	if n <= 0:
		return
	var boards := MultiMesh.new()
	boards.transform_format = MultiMesh.TRANSFORM_3D
	boards.mesh = board
	boards.instance_count = n
	var poles := MultiMesh.new()
	poles.transform_format = MultiMesh.TRANSFORM_3D
	poles.mesh = pole
	poles.instance_count = n
	d = 90.0
	var idx := 0
	while d < length - 80.0 and idx < n:
		var side := -1.0 if int(d / 90.0) % 2 == 0 else 1.0
		var xf := _facing_road(d, (half_width + 5.8) * side, 4.0, side)
		boards.set_instance_transform(idx, xf)
		var pole_xf := sample(d, (half_width + 5.8) * side, 2.1)
		poles.set_instance_transform(idx, pole_xf)
		d += 140.0
		idx += 1
	var board_inst := MultiMeshInstance3D.new()
	board_inst.name = "Billboard"
	board_inst.multimesh = boards
	add_child(board_inst)
	var pole_inst := MultiMeshInstance3D.new()
	pole_inst.name = "BillboardPoles"
	pole_inst.multimesh = poles
	add_child(pole_inst)


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
	light_anchors.clear()
	for i in steps:
		for side: float in [-1.0, 1.0]:
			var t := sample(i * spacing, (half_width + 0.85) * side, 3.6)
			mm.set_instance_transform(idx, t)
			light_anchors.append(t.origin)
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = "Streetlights"
	inst.multimesh = mm
	add_child(inst)
	_build_light_heads()


func _build_light_heads() -> void:
	var head := BoxMesh.new()
	head.size = Vector3(0.8, 0.12, 0.35)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.2, 0.2, 0.18)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.85, 0.5)
	mat.emission_energy_multiplier = 2.2 if String(definition.get("biome", "")) == "night" else 0.7
	head.material = mat
	var spacing := 28.0
	var steps := maxi(int(length / spacing), 1)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = head
	mm.instance_count = steps * 2
	var idx := 0
	for i in steps:
		for side: float in [-1.0, 1.0]:
			var t := sample(i * spacing, (half_width + 0.35) * side, 7.05)
			t.basis = Basis(Vector3.UP, side * PI * 0.5)
			mm.set_instance_transform(idx, t)
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = "StreetlightHeads"
	inst.multimesh = mm
	add_child(inst)


func _build_overpasses() -> void:
	var deck_mat := StandardMaterial3D.new()
	deck_mat.albedo_color = Color(0.42, 0.43, 0.46)
	deck_mat.roughness = 0.9
	var d := 380.0
	var n := 0
	while d < length - 200.0:
		var deck := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(half_width * 2.8 + 8.0, 0.7, 12.0)
		box.material = deck_mat
		deck.mesh = box
		deck.name = "Overpass"
		add_child(deck)
		deck.global_transform = sample(d, 0.0, 6.4)
		deck.set_meta("track_distance", d)
		_window_nodes.append(deck)
		for side in [-1.0, 1.0]:
			var pillar := MeshInstance3D.new()
			var cyl := CylinderMesh.new()
			cyl.top_radius = 0.45
			cyl.bottom_radius = 0.55
			cyl.height = 6.2
			cyl.material = deck_mat
			pillar.mesh = cyl
			add_child(pillar)
			pillar.global_transform = sample(d, (half_width + 1.6) * side, 3.1)
		d += 420.0
		n += 1
		if n >= 4:
			break


func _build_horizon_peaks() -> void:
	var biome := String(definition.get("biome", "coast"))
	var mat := StandardMaterial3D.new()
	mat.roughness = 1.0
	match biome:
		"desert":
			mat.albedo_color = Color(0.72, 0.52, 0.32)
		"coast":
			mat.albedo_color = Color(0.28, 0.38, 0.26)
		_:
			mat.albedo_color = Color(0.32, 0.38, 0.34)
	var mesh: Mesh
	if biome == "mountain":
		var prism := PrismMesh.new()
		prism.size = Vector3(90.0, 70.0, 90.0)
		prism.material = mat
		mesh = prism
	else:
		var hill := BoxMesh.new()
		hill.size = Vector3(90.0, 28.0, 50.0)
		hill.material = mat
		mesh = hill
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = 10
	var idx := 0
	for i in 5:
		for side: float in [-1.0, 1.0]:
			var d := length * (0.12 + i * 0.18)
			var t := sample(clampf(d, 0.0, length), (half_width + 70.0 + i * 8.0) * side, 8.0)
			var s := 0.8 + i * 0.15
			t.basis = t.basis.scaled(Vector3(s, s * (1.2 if biome == "mountain" else 0.7), s))
			mm.set_instance_transform(idx, t)
			idx += 1
	var inst := MultiMeshInstance3D.new()
	inst.name = "HorizonPeaks"
	inst.multimesh = mm
	add_child(inst)


func _build_tunnel() -> void:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.28, 0.28, 0.3)
	mat.roughness = 0.95
	var start := length * 0.42
	var wall_h := 6.4
	var wall_z := 38.0
	for side in [-1.0, 1.0]:
		var wall := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(1.2, wall_h, wall_z)
		box.material = mat
		wall.mesh = box
		wall.name = "TunnelWall"
		add_child(wall)
		wall.global_transform = sample(start, (half_width + 0.9) * side, wall_h * 0.5)
	var roof := MeshInstance3D.new()
	var rbox := BoxMesh.new()
	rbox.size = Vector3(half_width * 2.0 + 3.0, 1.0, wall_z)
	rbox.material = mat
	roof.mesh = rbox
	roof.name = "Tunnel"
	add_child(roof)
	roof.global_transform = sample(start, 0.0, wall_h + 0.4)
	roof.set_meta("track_distance", start)
	_window_nodes.append(roof)
	# Emissive tunnel interior lights — cheap mood, not one OmniLight per fixture.
	var lamp := BoxMesh.new()
	lamp.size = Vector3(0.35, 0.08, 1.8)
	var lamp_mat := StandardMaterial3D.new()
	lamp_mat.albedo_color = Color(0.9, 0.85, 0.6)
	lamp_mat.emission_enabled = true
	lamp_mat.emission = Color(1.0, 0.82, 0.45)
	lamp_mat.emission_energy_multiplier = 3.4
	lamp.material = lamp_mat
	for i in 5:
		var li := MeshInstance3D.new()
		li.mesh = lamp
		li.name = "TunnelLamp"
		add_child(li)
		var ld := start - 14.0 + i * 7.0
		li.global_transform = sample(ld, 0.0, wall_h - 0.2)
		li.set_meta("track_distance", start)
		_window_nodes.append(li)


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
	mat.uv1_scale = Vector3(6, 6, 1)
	if ResourceLoader.exists("res://assets/textures/asphalt_02_nor_gl.jpg"):
		mat.normal_enabled = true
		mat.normal_texture = load("res://assets/textures/asphalt_02_nor_gl.jpg")
		mat.normal_scale = 0.45
	mesh.material = mat
	_ocean_mat = mat
	plane.mesh = mesh
	plane.name = "Ocean"
	add_child(plane)
	var t := sample(length * 0.25, -half_width - 38.0, -1.2)
	plane.global_transform = t
	plane.rotate_object_local(Vector3.RIGHT, -PI * 0.5)
	# Foam strip at the verge so the water reads as a shoreline, not a slab.
	var foam := MeshInstance3D.new()
	var foam_mesh := PlaneMesh.new()
	foam_mesh.size = Vector2(length * 0.32, 8.0)
	var foam_mat := StandardMaterial3D.new()
	foam_mat.albedo_color = Color(0.82, 0.9, 0.95, 0.55)
	foam_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	foam_mat.roughness = 0.35
	foam_mesh.material = foam_mat
	foam.mesh = foam_mesh
	foam.name = "OceanFoam"
	add_child(foam)
	foam.global_transform = sample(length * 0.25, -half_width - 14.0, -0.4)
	foam.rotate_object_local(Vector3.RIGHT, -PI * 0.5)
	set_process(true)


func _process(delta: float) -> void:
	if _ocean_mat != null:
		_ocean_mat.uv1_offset.x = fmod(_ocean_mat.uv1_offset.x + delta * 0.03, 1.0)
		_ocean_mat.uv1_offset.y = fmod(_ocean_mat.uv1_offset.y + delta * 0.012, 1.0)


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


func _facing_road(d: float, lateral: float, height: float, side: float) -> Transform3D:
	var xf := sample(d, lateral, height)
	var up := xf.basis.y.normalized()
	var inward := -xf.basis.x * side
	inward.y = 0.0
	if inward.length_squared() < 0.0001:
		inward = Vector3.FORWARD
	inward = inward.normalized()
	var along := up.cross(inward)
	if along.length_squared() < 0.0001:
		along = -xf.basis.z
	along = along.normalized()
	inward = along.cross(up).normalized()
	return Transform3D(Basis(along, up, inward), xf.origin)


static func _apply_pbr_maps(mat: StandardMaterial3D, diffuse_path: String) -> void:
	var nor := diffuse_path.replace("_Diffuse.jpg", "_nor_gl.jpg")
	var arm := diffuse_path.replace("_Diffuse.jpg", "_arm.jpg")
	if ResourceLoader.exists(nor):
		mat.normal_enabled = true
		mat.normal_texture = load(nor)
		mat.normal_scale = 0.7
	if ResourceLoader.exists(arm):
		mat.ao_enabled = true
		mat.ao_texture = load(arm)
		mat.ao_light_affect = 0.6


func _build_landmarks() -> void:
	var d := 220.0
	var kind := 0
	while d < length - 160.0:
		var side := 1.0 if kind % 2 == 0 else -1.0
		var node := _make_landmark(kind % 3)
		node.name = "Landmark"
		node.set_meta("track_distance", d)
		add_child(node)
		var xf := _facing_road(d, (half_width + 9.5) * side, 0.0, side)
		node.global_transform = xf
		_window_nodes.append(node)
		d += 400.0
		kind += 1


func _make_landmark(kind: int) -> Node3D:
	var root := Node3D.new()
	match kind:
		0:
			# Gas station canopy + pumps.
			var canopy := MeshInstance3D.new()
			var roof := BoxMesh.new()
			roof.size = Vector3(8.0, 0.22, 6.0)
			var rmat := StandardMaterial3D.new()
			rmat.albedo_color = Color(0.85, 0.12, 0.1)
			rmat.emission_enabled = true
			rmat.emission = Color(0.9, 0.25, 0.12)
			rmat.emission_energy_multiplier = 0.4
			roof.material = rmat
			canopy.mesh = roof
			canopy.position = Vector3(0, 4.2, 0)
			root.add_child(canopy)
			for i in 3:
				var pump := MeshInstance3D.new()
				var box := BoxMesh.new()
				box.size = Vector3(0.6, 1.4, 0.45)
				var pmat := StandardMaterial3D.new()
				pmat.albedo_color = Color(0.85, 0.85, 0.82)
				box.material = pmat
				pump.mesh = box
				pump.position = Vector3(-2.0 + i * 2.0, 0.7, 0.0)
				root.add_child(pump)
		1:
			# Plaza kiosk.
			var slab := MeshInstance3D.new()
			var slab_mesh := BoxMesh.new()
			slab_mesh.size = Vector3(10.0, 0.18, 8.0)
			var smat := StandardMaterial3D.new()
			smat.albedo_color = Color(0.55, 0.56, 0.58)
			slab_mesh.material = smat
			slab.mesh = slab_mesh
			slab.position = Vector3(0, 0.1, 0)
			root.add_child(slab)
			var kiosk := MeshInstance3D.new()
			var kmesh := BoxMesh.new()
			kmesh.size = Vector3(3.2, 3.4, 3.2)
			var kmat := StandardMaterial3D.new()
			kmat.albedo_color = Color(0.18, 0.22, 0.28)
			kmat.emission_enabled = true
			kmat.emission = Color(0.4, 0.7, 1.0)
			kmat.emission_energy_multiplier = 0.6
			kmesh.material = kmat
			kiosk.mesh = kmesh
			kiosk.position = Vector3(0, 1.8, 0)
			root.add_child(kiosk)
		_:
			# Rest-stop shed.
			var shed := MeshInstance3D.new()
			var shed_mesh := BoxMesh.new()
			shed_mesh.size = Vector3(5.5, 3.2, 4.0)
			var shed_mat := StandardMaterial3D.new()
			shed_mat.albedo_color = Color(0.42, 0.36, 0.28)
			shed_mesh.material = shed_mat
			shed.mesh = shed_mesh
			shed.position = Vector3(0, 1.6, 0)
			root.add_child(shed)
	return root

