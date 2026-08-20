"""Headless sportbike generator — Road Rash crotch-rocket silhouettes.

  blender --background --python Godot/tools/blender/gen_assets.py
"""
import bpy
import math
import os
import sys

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets", "models")
os.makedirs(OUT, exist_ok=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, metallic=0.0, roughness=0.6, emission=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 3.0
    return mat


def add_mesh(name, mesh_obj, location, mat, rotation=(0, 0, 0)):
    bpy.context.collection.objects.link(mesh_obj)
    mesh_obj.name = name
    mesh_obj.location = location
    mesh_obj.rotation_euler = rotation
    if mat:
        mesh_obj.data.materials.append(mat)
    return mesh_obj


def box(name, size, loc, mat, rot=(0, 0, 0), bevel=0.03):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        m = o.modifiers.new("bevel", "BEVEL")
        m.width = bevel
        m.segments = 2
    o.data.materials.append(mat)
    return o


def cyl(name, r, depth, loc, mat, rot=(0, 0, 0), verts=28):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, location=loc,
                                         rotation=rot, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(mat)
    return o


def export(filename):
    path = os.path.join(OUT, filename)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True)
    print("WROTE", path)


def build_bike(style="sport", cop=False):
    """Full-fairing sportbike, ~2.0 m wheelbase. Built along +Y (Blender forward)."""
    reset()
    body_c = (0.05, 0.15, 0.65) if cop else (1.0, 0.0, 0.0)
    body = material("body", body_c, metallic=0.55, roughness=0.28)
    dark = material("dark", (0.05, 0.05, 0.06), roughness=0.9)
    chrome = material("chrome", (0.72, 0.74, 0.78), metallic=1.0, roughness=0.18)
    rubber = material("rubber", (0.04, 0.04, 0.05), roughness=0.95)
    leather = material("leather", (0.1, 0.09, 0.08), roughness=0.88)
    glass = material("glass", (0.02, 0.02, 0.04), metallic=0.7, roughness=0.08)
    suit = material("suit", (0.12, 0.12, 0.14), roughness=0.85)

    scale = 0.82 if style == "rat" else 1.0
    wb = 1.95 * scale  # wheelbase

    # Wheels — chunky sport tyres
    fr = 0.36 * scale
    rr = 0.38 * scale
    cyl("wheel_f", fr, 0.13 * scale, (0, wb * 0.42, fr), rubber, (0, math.pi / 2, 0))
    cyl("wheel_r", rr, 0.17 * scale, (0, -wb * 0.42, rr), rubber, (0, math.pi / 2, 0))
    # Discs
    cyl("disc_f", fr * 0.72, 0.04, (0, wb * 0.42, fr + 0.02), chrome, (0, math.pi / 2, 0))
    cyl("disc_r", rr * 0.72, 0.05, (0, -wb * 0.42, rr + 0.02), chrome, (0, math.pi / 2, 0))

    # Frame + engine
    box("frame", (0.14 * scale, 1.05 * scale, 0.16 * scale), (0, 0.02 * scale, 0.58 * scale), chrome, bevel=0.025)
    box("engine", (0.38 * scale, 0.55 * scale, 0.34 * scale), (0, 0.0, 0.42 * scale), dark, bevel=0.04)

    if style == "rat":
        # Naked starter — smaller tank, no full fairing
        box("tank", (0.32 * scale, 0.55 * scale, 0.22 * scale), (0, 0.12 * scale, 0.78 * scale), body, bevel=0.05)
        box("tail", (0.24 * scale, 0.38 * scale, 0.12 * scale), (0, -0.38 * scale, 0.76 * scale), body,
            rot=(math.radians(-10), 0, 0), bevel=0.04)
    else:
        # Full fairing nose + side panels
        box("nose", (0.36 * scale, 0.38 * scale, 0.32 * scale), (0, wb * 0.38, 0.92 * scale), body,
            rot=(math.radians(-16), 0, 0), bevel=0.06)
        box("belly", (0.34 * scale, 0.62 * scale, 0.26 * scale), (0, 0.08 * scale, 0.62 * scale), body, bevel=0.05)
        box("tail", (0.28 * scale, 0.48 * scale, 0.16 * scale), (0, -0.44 * scale, 0.82 * scale), body,
            rot=(math.radians(-14), 0, 0), bevel=0.05)
        box("windscreen", (0.28 * scale, 0.08 * scale, 0.22 * scale), (0, wb * 0.34, 1.02 * scale), glass,
            rot=(math.radians(-28), 0, 0), bevel=0.02)

    box("seat", (0.3 * scale, 0.44 * scale, 0.08 * scale), (0, -0.16 * scale, 0.8 * scale), leather, bevel=0.03)

    # Forks + triple clamp
    for sx in (-0.1, 0.1):
        cyl(f"fork_{sx}", 0.028 * scale, 0.72 * scale, (sx * scale, wb * 0.36, 0.62 * scale), chrome,
            rot=(math.radians(-18), 0, 0))
    box("clamp", (0.22 * scale, 0.08 * scale, 0.06 * scale), (0, wb * 0.32, 0.88 * scale), dark)

    # Handlebars
    box("bars", (0.56 * scale, 0.05 * scale, 0.05 * scale), (0, wb * 0.28, 1.0 * scale), dark)

    # Dual exhaust
    for sx in (0.14, 0.2):
        cyl(f"exhaust_{sx}", 0.045 * scale, 0.75 * scale, (sx * scale, -0.32 * scale, 0.44 * scale), chrome,
            rot=(math.radians(78), 0, 0))

    cyl("headlight", 0.08 * scale, 0.05, (0, wb * 0.44, 0.98 * scale),
        material("light", (1, 1, 0.92), emission=(1, 0.95, 0.85)), rot=(math.radians(70), 0, 0))

    # Rider — named bones for combat poses
    box("torso", (0.36 * scale, 0.52 * scale, 0.26 * scale), (0, -0.1 * scale, 1.08 * scale), suit,
        rot=(math.radians(36), 0, 0), bevel=0.05)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.16 * scale, location=(0, 0.26 * scale, 1.26 * scale))
    helm = bpy.context.active_object
    helm.name = "helmet"
    helm.data.materials.append(body if not cop else material("cop_helmet", (0.92, 0.92, 0.95), metallic=0.35))
    box("visor_b", (0.22 * scale, 0.05 * scale, 0.1 * scale), (0, 0.4 * scale, 1.24 * scale), glass, bevel=0.02)
    box("arm_l", (0.1 * scale, 0.44 * scale, 0.1 * scale), (-0.22 * scale, 0.16 * scale, 1.04 * scale), suit,
        rot=(math.radians(44), 0, math.radians(12)), bevel=0.02)
    box("arm_r", (0.1 * scale, 0.44 * scale, 0.1 * scale), (0.22 * scale, 0.16 * scale, 1.04 * scale), suit,
        rot=(math.radians(44), 0, math.radians(-12)), bevel=0.02)
    box("leg_l", (0.12 * scale, 0.42 * scale, 0.13 * scale), (-0.18 * scale, -0.26 * scale, 0.64 * scale), suit,
        rot=(math.radians(-28), 0, 0), bevel=0.02)
    box("leg_r", (0.12 * scale, 0.42 * scale, 0.13 * scale), (0.18 * scale, -0.26 * scale, 0.64 * scale), suit,
        rot=(math.radians(-28), 0, 0), bevel=0.02)

    if cop:
        box("beacon", (0.12 * scale, 0.1 * scale, 0.08 * scale), (0, -0.02 * scale, 0.98 * scale),
            material("beacon", (0.95, 0.1, 0.1), emission=(1, 0.1, 0.1)), bevel=0.02)

    export("cop_bike.glb" if cop else ("bike_rat.glb" if style == "rat" else "bike.glb"))


def build_car():
    reset()
    body = material("paint", (0.78, 0.78, 0.8), metallic=0.45, roughness=0.38)
    glass = material("glass", (0.12, 0.14, 0.18), metallic=0.65, roughness=0.12)
    dark = material("tyres", (0.05, 0.05, 0.06), roughness=0.92)
    box("body", (1.85, 4.5, 0.72), (0, 0, 0.64), body, bevel=0.09)
    box("cabin", (1.65, 2.25, 0.64), (0, -0.18, 1.22), glass, bevel=0.1)
    for sx in (-0.84, 0.84):
        for sy in (1.48, -1.48):
            cyl(f"w_{sx}_{sy}", 0.35, 0.22, (sx, sy, 0.35), dark, (0, math.pi / 2, 0))
    export("car.glb")


def build_tree():
    reset()
    trunk = material("trunk", (0.24, 0.16, 0.1), roughness=0.95)
    leaf = material("leaf", (0.13, 0.32, 0.12), roughness=0.95)
    cyl("trunk", 0.22, 2.6, (0, 0, 1.3), trunk, verts=10)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.5, location=(0, 0, 3.4))
    bpy.context.active_object.data.materials.append(leaf)
    export("tree.glb")


def build_rock():
    reset()
    rock = material("rock", (0.45, 0.38, 0.3), roughness=1.0)
    bpy.ops.mesh.primitive_ico_sphere_add(radius=1.4, subdivisions=1, location=(0, 0, 0.8))
    bpy.context.active_object.data.materials.append(rock)
    export("rock.glb")


def build_building():
    reset()
    concrete = material("concrete", (0.35, 0.36, 0.4), roughness=0.9)
    glass = material("windows", (0.4, 0.55, 0.7), metallic=0.7, roughness=0.15, emission=(0.9, 0.8, 0.5))
    box("tower", (8, 8, 18), (0, 0, 9), concrete)
    for level in range(4):
        box(f"band{level}", (8.3, 8.3, 1.2), (0, 0, 4 + level * 4), glass)
    export("building.glb")


if __name__ == "__main__":
    build_bike("sport", cop=False)
    build_bike("rat", cop=False)
    build_bike("sport", cop=True)
    build_car()
    build_tree()
    build_rock()
    build_building()
    print("ALL ASSETS DONE")
    sys.exit(0)
