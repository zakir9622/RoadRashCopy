"""Generates every 3D asset headlessly:  blender --background --python gen_assets.py

Outputs .glb files into Godot/assets/models/. Bright-red (1,0,0) materials mark
panels the game recolours per rider; everything else keeps its authored colour.
Style: clean low-poly with bevels — reads sharply at speed, cheap on mobile.
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
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 3.0
    return mat


def add_box(name, size, location, mat, rotation=(0, 0, 0), bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    obj.data.materials.append(mat)
    return obj


def add_cylinder(name, radius, depth, location, mat, rotation=(0, 0, 0), vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=depth, location=location, rotation=rotation, vertices=vertices)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def add_sphere(name, radius, location, mat, segments=16):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=location,
                                         segments=segments, ring_count=segments // 2)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def export(filename):
    path = os.path.join(OUT, filename)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True)
    print("WROTE", path)


def build_bike(cop=False):
    """Sport bike with rider. Z-forward becomes -Z in Godot via glTF; built lying
    along +Y here (Blender forward), rider leaning into the bars."""
    reset()
    tank_color = (0.05, 0.15, 0.6) if cop else (1.0, 0.0, 0.0)   # red = tint target
    body = material("body", tank_color, metallic=0.55, roughness=0.3)
    dark = material("dark", (0.06, 0.06, 0.07), roughness=0.85)
    chrome = material("chrome", (0.65, 0.67, 0.7), metallic=1.0, roughness=0.2)
    leather = material("leather", (0.12, 0.10, 0.09), roughness=0.9)
    skin = material("skin", (0.7, 0.5, 0.38), roughness=0.8)
    visor = material("visor", (0.05, 0.05, 0.08), metallic=0.8, roughness=0.1)

    # Wheels (Y axis = forward)
    add_cylinder("wheel_f", 0.32, 0.11, (0, 0.72, 0.32), dark, rotation=(0, math.pi / 2, 0))
    add_cylinder("wheel_r", 0.34, 0.14, (0, -0.62, 0.34), dark, rotation=(0, math.pi / 2, 0))

    # Frame spine + tank + tail
    add_box("frame", (0.16, 1.1, 0.14), (0, 0.02, 0.55), chrome, bevel=0.03)
    add_box("tank", (0.3, 0.5, 0.24), (0, 0.18, 0.72), body, bevel=0.05)
    add_box("tail", (0.26, 0.45, 0.14), (0, -0.42, 0.78), body, rotation=(math.radians(-12), 0, 0), bevel=0.04)
    add_box("seat", (0.28, 0.42, 0.07), (0, -0.18, 0.78), leather, bevel=0.03)

    # Front fork + bars + headlight fairing
    add_cylinder("fork_l", 0.03, 0.62, (-0.09, 0.62, 0.58), chrome, rotation=(math.radians(-20), 0, 0))
    add_cylinder("fork_r", 0.03, 0.62, (0.09, 0.62, 0.58), chrome, rotation=(math.radians(-20), 0, 0))
    add_box("fairing", (0.34, 0.22, 0.3), (0, 0.55, 0.86), body, rotation=(math.radians(-18), 0, 0), bevel=0.05)
    add_box("bars", (0.5, 0.05, 0.05), (0, 0.48, 1.02), dark)
    add_cylinder("headlight", 0.07, 0.04, (0, 0.68, 0.88), material(
        "light", (1, 1, 0.9), emission=(1.0, 0.95, 0.8)), rotation=(math.radians(72), 0, 0))

    # Exhaust
    add_cylinder("exhaust", 0.05, 0.7, (0.16, -0.35, 0.42), chrome, rotation=(math.radians(80), 0, 0))

    # Engine block
    add_box("engine", (0.34, 0.5, 0.3), (0, 0.05, 0.42), dark, bevel=0.04)

    # --- rider, tucked ---
    suit = material("suit", (0.08, 0.3, 0.05) if cop else (0.16, 0.16, 0.18), roughness=0.85)
    add_box("torso", (0.34, 0.55, 0.24), (0, -0.12, 1.06), suit,
            rotation=(math.radians(38), 0, 0), bevel=0.05)
    add_sphere("helmet", 0.15, (0, 0.28, 1.22), body if not cop else material(
        "cop_helmet", (0.9, 0.9, 0.92), metallic=0.3, roughness=0.3))
    add_box("visor_b", (0.2, 0.05, 0.1), (0, 0.41, 1.2), visor, bevel=0.02)
    # Arms to the bars
    add_box("arm_l", (0.09, 0.42, 0.09), (-0.2, 0.18, 1.02), suit, rotation=(math.radians(46), 0, math.radians(10)), bevel=0.02)
    add_box("arm_r", (0.09, 0.42, 0.09), (0.2, 0.18, 1.02), suit, rotation=(math.radians(46), 0, math.radians(-10)), bevel=0.02)
    # Legs gripping the tank
    add_box("leg_l", (0.11, 0.4, 0.12), (-0.17, -0.28, 0.62), suit, rotation=(math.radians(-30), 0, 0), bevel=0.02)
    add_box("leg_r", (0.11, 0.4, 0.12), (0.17, -0.28, 0.62), suit, rotation=(math.radians(-30), 0, 0), bevel=0.02)
    _ = skin

    if cop:
        add_box("beacon", (0.1, 0.1, 0.08), (0, -0.05, 0.95), material(
            "beacon", (0.9, 0.1, 0.1), emission=(1.0, 0.1, 0.1)), bevel=0.02)

    export("cop_bike.glb" if cop else "bike.glb")


def build_car():
    reset()
    body = material("paint", (0.8, 0.8, 0.82), metallic=0.4, roughness=0.4)
    glass = material("glass", (0.1, 0.12, 0.16), metallic=0.6, roughness=0.1)
    dark = material("tyres", (0.05, 0.05, 0.06), roughness=0.9)

    add_box("body", (1.8, 4.4, 0.7), (0, 0, 0.62), body, bevel=0.08)
    add_box("cabin", (1.6, 2.2, 0.62), (0, -0.2, 1.2), glass, bevel=0.1)
    for sx in (-0.82, 0.82):
        for sy in (1.45, -1.45):
            add_cylinder(f"wheel_{sx}_{sy}", 0.34, 0.22, (sx, sy, 0.34), dark,
                         rotation=(0, math.pi / 2, 0))
    add_box("bumper_f", (1.7, 0.2, 0.25), (0, 2.25, 0.45), dark, bevel=0.04)
    add_box("bumper_r", (1.7, 0.2, 0.25), (0, -2.25, 0.45), dark, bevel=0.04)
    export("car.glb")


def build_tree():
    reset()
    trunk = material("trunk", (0.24, 0.16, 0.1), roughness=0.95)
    leaf = material("leaf", (0.13, 0.32, 0.12), roughness=0.95)
    add_cylinder("trunk", 0.22, 2.6, (0, 0, 1.3), trunk, vertices=10)
    add_sphere("canopy1", 1.5, (0, 0, 3.4), leaf, segments=12)
    add_sphere("canopy2", 1.1, (0.7, 0.3, 2.8), leaf, segments=10)
    add_sphere("canopy3", 1.0, (-0.6, -0.2, 2.9), leaf, segments=10)
    export("tree.glb")


def build_rock():
    reset()
    rock = material("rock", (0.45, 0.38, 0.3), roughness=1.0)
    bpy.ops.mesh.primitive_ico_sphere_add(radius=1.4, subdivisions=1, location=(0, 0, 0.8))
    obj = bpy.context.active_object
    obj.scale = (1.3, 1.0, 0.75)
    obj.data.materials.append(rock)
    export("rock.glb")


def build_building():
    reset()
    concrete = material("concrete", (0.35, 0.36, 0.4), roughness=0.9)
    glass = material("windows", (0.4, 0.55, 0.7), metallic=0.7, roughness=0.15,
                     emission=(0.9, 0.8, 0.5))
    add_box("tower", (8, 8, 18), (0, 0, 9), concrete, bevel=0.0)
    for level in range(4):
        add_box(f"band{level}", (8.3, 8.3, 1.2), (0, 0, 4 + level * 4), glass, bevel=0.0)
    export("building.glb")


if __name__ == "__main__":
    build_bike(cop=False)
    build_bike(cop=True)
    build_car()
    build_tree()
    build_rock()
    build_building()
    print("ALL ASSETS DONE")
    sys.exit(0)
