"""Street-bike meshes that read as real 250–1000cc machines, not cubes.

Blender Z-up, Y along the wheelbase. Named hooks for Godot:
  wheel_f, wheel_r, cockpit_cam
"""
import bpy
import math
from mathutils import Vector


def _mat(name, color, metallic=0.0, roughness=0.5, emission=None, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if alpha < 1.0:
        m.blend_method = "BLEND"
        bsdf.inputs["Alpha"].default_value = alpha
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    return m


def _parent(obj, parent):
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def _cyl(name, radius, depth, loc, material, rot=(0, 0, 0), verts=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=depth, location=loc, rotation=rot, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    _smooth(o)
    return _parent(o, parent) if parent else o


def _sphere(name, radius, loc, material, segs=16, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, location=loc, segments=segs, ring_count=max(8, segs // 2))
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    _smooth(o)
    return _parent(o, parent) if parent else o


def _torus(name, major, minor, loc, material, rot=(0, math.pi / 2, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major, minor_radius=minor, location=loc, rotation=rot,
        major_segments=40, minor_segments=16)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    _smooth(o)
    return _parent(o, parent) if parent else o


def _box(name, size, loc, material, rot=(0, 0, 0), bevel=0.012, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
    o.data.materials.append(material)
    return _parent(o, parent) if parent else o


def _pipe(name, a, b, radius, material, verts=10, parent=None):
    a, b = Vector(a), Vector(b)
    mid = (a + b) * 0.5
    length = max((b - a).length, 0.02)
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, location=mid, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = (b - a).normalized().to_track_quat("Z", "Y").to_euler()
    o.data.materials.append(material)
    return _parent(o, parent) if parent else o


def _smooth(obj):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    return obj


def _empty(name, loc, parent, rot=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = rot
    o.empty_display_size = 0.12
    o.empty_display_type = "ARROWS"
    return _parent(o, parent)


def build_street_bike(root, style="rat"):
    """Panda 250 naked, Shuriken 600 faired, Kamikaze 750, Diablo 1000."""
    cop = style == "cop"
    sc = {"rat": 0.86, "sport": 1.0, "kami": 1.04, "super": 1.08, "cop": 1.0}[style]
    wb = {"rat": 1.92, "sport": 2.08, "kami": 2.14, "super": 2.18, "cop": 2.10}[style] * sc
    paint = {
        "rat": (0.82, 0.12, 0.10),
        "sport": (0.08, 0.18, 0.72),
        "kami": (0.08, 0.08, 0.09),
        "super": (0.72, 0.04, 0.10),
        "cop": (0.10, 0.18, 0.55),
    }[style]
    body = _mat("body", paint, metallic=0.42, roughness=0.22)
    dark = _mat("dark", (0.04, 0.04, 0.045), roughness=0.78, metallic=0.35)
    chrome = _mat("chrome", (0.78, 0.80, 0.84), metallic=1.0, roughness=0.12)
    rubber = _mat("rubber", (0.03, 0.03, 0.03), roughness=0.98)
    leather = _mat("leather", (0.07, 0.06, 0.055), roughness=0.88)
    glass = _mat("glass", (0.04, 0.06, 0.10), metallic=0.2, roughness=0.04, alpha=0.35)
    alum = _mat("alum", (0.45, 0.46, 0.48), metallic=0.85, roughness=0.32)
    gold = _mat("forks", (0.55, 0.48, 0.22), metallic=0.8, roughness=0.28)
    light = _mat("light", (1.0, 0.97, 0.88), emission=(1.0, 0.95, 0.8), roughness=0.15)
    disc = _mat("disc", (0.65, 0.66, 0.68), metallic=0.9, roughness=0.25)

    fy = wb * 0.44
    ry = -wb * 0.42
    fr = 0.31 * sc
    rr = 0.33 * sc if style == "rat" else 0.335 * sc

    # --- wheels: torus tyre + rim + disc (named for Godot spin) ---
    wf = _cyl("wheel_f", fr * 0.62, 0.09 * sc, (0, fy, fr), alum, (0, math.pi / 2, 0), 22, root)
    _torus("tyre_f", fr * 0.82, 0.055 * sc, (0, fy, fr), rubber, parent=wf)
    _cyl("disc_f", fr * 0.58, 0.012, (0.05 * sc, fy, fr), disc, (0, math.pi / 2, 0), 20, wf)
    wr = _cyl("wheel_r", rr * 0.62, 0.11 * sc, (0, ry, rr), alum, (0, math.pi / 2, 0), 22, root)
    _torus("tyre_r", rr * 0.84, 0.068 * sc, (0, ry, rr), rubber, parent=wr)
    _cyl("disc_r", rr * 0.55, 0.014, (-0.06 * sc, ry, rr), disc, (0, math.pi / 2, 0), 20, wr)
    # Radial spokes so the wheels read as laced street rims, not plastic toys.
    for wi, wheel, y, z, rad in (("f", wf, fy, fr, fr), ("r", wr, ry, rr, rr)):
        for i in range(8):
            ang = i * (math.pi / 4.0)
            inner = (0.0, y + math.cos(ang) * rad * 0.16, z + math.sin(ang) * rad * 0.16)
            outer = (0.0, y + math.cos(ang) * rad * 0.70, z + math.sin(ang) * rad * 0.70)
            _pipe(f"spoke_{wi}_{i}", inner, outer, 0.007 * sc, chrome, verts=8, parent=wheel)

    # Footpegs + radiator + chain guard — silhouette details the chase cam actually sees.
    for sx in (-1, 1):
        _box(f"peg_{sx}", (0.07 * sc, 0.04 * sc, 0.025 * sc),
             (sx * 0.16 * sc, -0.02 * sc, 0.36 * sc), dark, bevel=0.004, parent=root)
    _box("radiator", (0.28 * sc, 0.08 * sc, 0.22 * sc), (0, fy * 0.18, 0.48 * sc), dark,
         bevel=0.008, parent=root)
    _box("chain_guard", (0.04 * sc, 0.28 * sc, 0.08 * sc), (-0.18 * sc, ry * 0.55, rr + 0.08 * sc),
         dark, bevel=0.006, parent=root)

    # --- steel/alloy frame tubes ---
    _pipe("frame_top", (0, fy * 0.15, 0.78 * sc), (0, ry * 0.15, 0.72 * sc), 0.022 * sc, chrome, parent=root)
    _pipe("frame_down", (0, fy * 0.22, 0.78 * sc), (0, 0.05, 0.42 * sc), 0.024 * sc, chrome, parent=root)
    _pipe("frame_lower", (0, 0.08, 0.40 * sc), (0, ry * 0.55, 0.42 * sc), 0.02 * sc, chrome, parent=root)
    for sx in (-1, 1):
        _pipe(f"frame_side_{sx}", (sx * 0.09 * sc, fy * 0.1, 0.70 * sc),
              (sx * 0.11 * sc, ry * 0.2, 0.62 * sc), 0.016 * sc, chrome, parent=root)

    # --- engine: cases + barrels + cooling fins (reads as a real mill) ---
    _box("engine_case", (0.34 * sc, 0.42 * sc, 0.28 * sc), (0, 0.04, 0.46 * sc), alum, bevel=0.04, parent=root)
    barrels = 1 if style == "rat" else 2
    for i in range(barrels):
        by = 0.02 + (i - (barrels - 1) * 0.5) * 0.16 * sc
        _cyl(f"barrel_{i}", 0.07 * sc, 0.16 * sc, (0, by, 0.62 * sc), dark, verts=16, parent=root)
        for fin in range(5):
            _cyl(f"fin_{i}_{fin}", 0.085 * sc, 0.008, (0, by, 0.56 * sc + fin * 0.018 * sc),
                 alum, verts=16, parent=root)
    _cyl("sprocket", 0.09 * sc, 0.02, (-0.16 * sc, ry * 0.15, 0.42 * sc), dark,
         (0, math.pi / 2, 0), 18, root)
    _pipe("chain", (-0.16 * sc, ry * 0.15, 0.42 * sc), (-0.16 * sc, ry, rr), 0.012 * sc, dark, parent=root)

    # --- swingarm + shocks ---
    _box("swingarm", (0.08 * sc, wb * 0.38, 0.05 * sc), (0, ry * 0.52, rr + 0.02), alum, bevel=0.01, parent=root)
    for sx in (-0.08, 0.08):
        _pipe(f"shock_{sx}", (sx * sc, ry * 0.35, rr + 0.04), (sx * sc, ry * 0.12, 0.72 * sc),
              0.018 * sc, gold, parent=root)

    # --- forks + triple tree ---
    fork_mat = gold if style in ("sport", "kami", "super") else chrome
    for sx in (-0.085, 0.085):
        _pipe(f"fork_{sx}", (sx * sc, fy, fr), (sx * sc, fy * 0.78, 1.02 * sc),
              0.022 * sc, fork_mat, parent=root)
    _box("triple", (0.22 * sc, 0.08 * sc, 0.05 * sc), (0, fy * 0.72, 1.00 * sc), dark, bevel=0.01, parent=root)
    _cyl("fender_f", fr * 0.95, 0.08 * sc, (0, fy - 0.02, fr + 0.08 * sc), body,
         (math.radians(70), 0, 0), 16, root)

    # --- tank (teardrop via scaled spheres, not a cube) ---
    tank_y, tank_z = 0.12 * sc, 0.82 * sc
    tank = _sphere("tank", 0.22 * sc, (0, tank_y, tank_z), body, 20, root)
    tank.scale = (0.72, 1.35, 0.62)
    _sphere("tank_cap", 0.035 * sc, (0, tank_y + 0.04, tank_z + 0.12 * sc), chrome, 10, root)

    # --- seat + tail ---
    _box("seat", (0.30 * sc, 0.42 * sc, 0.07 * sc), (0, -0.18 * sc, 0.80 * sc), leather, bevel=0.03, parent=root)
    tail = _sphere("tail", 0.16 * sc, (0, -0.42 * sc, 0.78 * sc), body, 14, root)
    tail.scale = (0.7, 1.4, 0.45)
    _cyl("tail_light", 0.04 * sc, 0.03, (0, -0.62 * sc, 0.78 * sc),
         _mat("tail_em", (0.8, 0.05, 0.05), emission=(1.0, 0.08, 0.05)), (0, math.pi / 2, 0), 12, root)
    _box("fender_r", (0.22 * sc, 0.28 * sc, 0.04 * sc), (0, ry * 0.72, rr + 0.14 * sc), body,
         rot=(math.radians(18), 0, 0), bevel=0.012, parent=root)
    _box("plate", (0.16 * sc, 0.012 * sc, 0.10 * sc), (0, -0.66 * sc, 0.62 * sc),
         _mat("plate", (0.92, 0.90, 0.82), roughness=0.45), bevel=0.004, parent=root)

    # --- exhaust ---
    ex_x = 0.18 * sc
    _pipe("header", (0.12 * sc, 0.08, 0.50 * sc), (ex_x, -0.15 * sc, 0.38 * sc), 0.028 * sc, chrome, parent=root)
    _pipe("muffler", (ex_x, -0.15 * sc, 0.38 * sc), (ex_x, -0.55 * sc, 0.40 * sc), 0.042 * sc, chrome, parent=root)
    if style in ("kami", "super"):
        _pipe("muffler_r", (-ex_x, -0.15 * sc, 0.38 * sc), (-ex_x, -0.55 * sc, 0.40 * sc),
              0.042 * sc, chrome, parent=root)

    # --- bars + cockpit cluster (the Road Rash 3D dash you look over) ---
    bar_z = 1.08 * sc if style == "rat" else 1.00 * sc
    bar_y = fy * 0.62
    bar_w = 0.62 * sc if style == "rat" else 0.50 * sc
    _cyl("bars", 0.016 * sc, bar_w, (0, bar_y, bar_z), dark, (0, math.pi / 2, 0), 12, root)
    for sx in (-1, 1):
        _sphere(f"grip_{sx}", 0.022 * sc, (sx * bar_w * 0.48, bar_y, bar_z), rubber, 10, root)
        _box(f"mirror_{sx}", (0.08 * sc, 0.02 * sc, 0.05 * sc),
             (sx * 0.28 * sc, bar_y + 0.04, bar_z + 0.06 * sc), dark, bevel=0.008, parent=root)
        _cyl(f"gauge_{sx}", 0.045 * sc, 0.02, (sx * 0.05 * sc, bar_y - 0.02, bar_z + 0.03 * sc),
             _mat("gauge", (0.02, 0.02, 0.02), emission=(0.15, 0.9, 0.35) if sx < 0 else (0.95, 0.85, 0.2)),
             (math.radians(72), 0, 0), 16, root)

    # Headlight / fairing
    if style == "rat" or cop:
        _sphere("headlight", 0.09 * sc, (0, fy * 0.82, 0.92 * sc), light, 14, root)
        _cyl("lamp_ring", 0.095 * sc, 0.02, (0, fy * 0.80, 0.92 * sc), chrome, (0, math.pi / 2, 0), 16, root)
    else:
        _box("nose", (0.36 * sc, 0.38 * sc, 0.28 * sc), (0, fy * 0.55, 0.92 * sc), body,
             rot=(math.radians(-22), 0, 0), bevel=0.05, parent=root)
        _box("windscreen", (0.28 * sc, 0.04 * sc, 0.22 * sc), (0, fy * 0.48, 1.12 * sc), glass,
             rot=(math.radians(-38), 0, 0), bevel=0.01, parent=root)
        for sx in (-1, 1):
            _sphere(f"head_{sx}", 0.055 * sc, (sx * 0.08 * sc, fy * 0.72, 0.90 * sc), light, 12, root)
        _box("side_l", (0.05 * sc, 0.48 * sc, 0.22 * sc), (-0.20 * sc, 0.08, 0.62 * sc), body,
             rot=(0, 0, math.radians(10)), bevel=0.03, parent=root)
        _box("side_r", (0.05 * sc, 0.48 * sc, 0.22 * sc), (0.20 * sc, 0.08, 0.62 * sc), body,
             rot=(0, 0, math.radians(-10)), bevel=0.03, parent=root)

    if cop:
        _box("beacon", (0.10 * sc, 0.10 * sc, 0.07 * sc), (0, -0.08, 0.96 * sc),
             _mat("beacon", (0.9, 0.05, 0.05), emission=(1.0, 0.08, 0.08)), bevel=0.015, parent=root)

    # Camera sits just behind the bars, above the tank — rider helmet is behind this.
    _empty("cockpit_cam", (0.0, bar_y - 0.18 * sc, bar_z + 0.06 * sc), root,
           rot=(math.radians(78), 0, 0))
    return root
