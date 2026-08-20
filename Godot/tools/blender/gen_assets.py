"""Procedural Road Rash 3D-style bikes + humanoid riders (headless Blender).

Motion-capture *feel* via named bone-like parts: torso tuck, shoulder-check,
full arm extension punches, kick, wheel spin hooks in Godot.

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


def mat(name, color, metallic=0.0, roughness=0.6, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 2.5
    return m


def box(name, size, loc, material, rot=(0, 0, 0), bevel=0.025, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    o.data.materials.append(material)
    if parent:
        o.parent = parent
        o.matrix_parent_inverse = parent.matrix_world.inverted()
    return o


def cyl(name, radius, depth, loc, material, rot=(0, 0, 0), verts=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, location=loc,
                                         rotation=rot, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
        o.matrix_parent_inverse = parent.matrix_world.inverted()
    return o


def sphere(name, radius, loc, material, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=loc, segments=16, ring_count=12)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
        o.matrix_parent_inverse = parent.matrix_world.inverted()
    return o


def export(filename):
    path = os.path.join(OUT, filename)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True)
    print("WROTE", path)


def build_humanoid_rider(root, suit_mat, skin_mat, helm_mat, visor_mat, scale=1.0):
    """Seated humanoid with hierarchical arms/legs for punch/kick/tuck poses."""
    s = scale
    rider = bpy.data.objects.new("rider_root", None)
    bpy.context.collection.objects.link(rider)
    rider.parent = root
    rider.location = (0, -0.08 * s, 0.82 * s)

    # Pelvis / hips on seat
    pelvis = box("pelvis", (0.34 * s, 0.22 * s, 0.18 * s), (0, 0, 0.08 * s), suit_mat,
                 rot=(math.radians(18), 0, 0), bevel=0.03, parent=rider)

    # Torso — leans with speed in Godot via torso node
    torso = box("torso", (0.38 * s, 0.48 * s, 0.28 * s), (0, 0.12 * s, 0.38 * s), suit_mat,
                rot=(math.radians(32), 0, 0), bevel=0.04, parent=rider)

    # Neck + head + helmet (Road Rash 3D: big visible helmet read)
    cyl("neck", 0.07 * s, 0.1 * s, (0, 0.28 * s, 0.58 * s), skin_mat,
        rot=(math.radians(20), 0, 0), parent=rider)
    sphere("head", 0.11 * s, (0, 0.36 * s, 0.66 * s), skin_mat, parent=rider)
    sphere("helmet", 0.155 * s, (0, 0.38 * s, 0.68 * s), helm_mat, parent=rider)
    box("visor", (0.2 * s, 0.04 * s, 0.09 * s), (0, 0.48 * s, 0.72 * s), visor_mat,
        rot=(math.radians(24), 0, 0), bevel=0.01, parent=rider)

    # Shoulders — anchor for arm chains
    for side, sx in [("l", -1), ("r", 1)]:
        shoulder = box(f"shoulder_{side}", (0.12 * s, 0.16 * s, 0.12 * s),
                       (sx * 0.22 * s, 0.22 * s, 0.48 * s), suit_mat,
                       rot=(math.radians(30), 0, sx * math.radians(8)), bevel=0.02, parent=rider)
        upper = box(f"upper_arm_{side}", (0.1 * s, 0.34 * s, 0.1 * s),
                    (sx * 0.26 * s, 0.18 * s, 0.38 * s), suit_mat,
                    rot=(math.radians(48), 0, sx * math.radians(14)), bevel=0.02, parent=rider)
        fore = box(f"forearm_{side}", (0.085 * s, 0.3 * s, 0.085 * s),
                   (sx * 0.34 * s, 0.08 * s, 0.32 * s), suit_mat,
                   rot=(math.radians(52), 0, sx * math.radians(6)), bevel=0.02, parent=rider)
        box(f"glove_{side}", (0.1 * s, 0.12 * s, 0.08 * s),
            (sx * 0.42 * s, 0.02 * s, 0.28 * s), mat("glove", (0.08, 0.08, 0.09), roughness=0.9),
            rot=(math.radians(55), 0, 0), bevel=0.015, parent=rider)
        # Godot pose hooks (legacy names)
        upper.name = f"arm_{side}"
        fore.name = f"forearm_{side}"

    # Legs gripping tank
    for side, sx in [("l", -1), ("r", 1)]:
        thigh = box(f"thigh_{side}", (0.14 * s, 0.38 * s, 0.14 * s),
                    (sx * 0.16 * s, -0.12 * s, 0.22 * s), suit_mat,
                    rot=(math.radians(-24), 0, sx * math.radians(6)), bevel=0.02, parent=rider)
        shin = box(f"shin_{side}", (0.11 * s, 0.34 * s, 0.11 * s),
                   (sx * 0.14 * s, -0.28 * s, 0.08 * s), suit_mat,
                   rot=(math.radians(-18), 0, 0), bevel=0.02, parent=rider)
        box(f"boot_{side}", (0.12 * s, 0.22 * s, 0.1 * s),
            (sx * 0.12 * s, -0.42 * s, 0.02 * s), mat("boot", (0.06, 0.06, 0.07), roughness=0.95),
            bevel=0.02, parent=rider)
        if side == "r":
            shin.name = "leg_r"
        else:
            shin.name = "leg_l"

    return rider


def build_bike(style="sport", cop=False):
    reset()
    root = bpy.data.objects.new("bike_root", None)
    bpy.context.collection.objects.link(root)

    body_c = (0.08, 0.18, 0.72) if cop else (1.0, 0.0, 0.0)
    body = mat("body", body_c, metallic=0.58, roughness=0.26)
    dark = mat("dark", (0.04, 0.04, 0.05), roughness=0.92)
    chrome = mat("chrome", (0.74, 0.76, 0.8), metallic=1.0, roughness=0.15)
    rubber = mat("rubber", (0.03, 0.03, 0.04), roughness=0.98)
    leather = mat("leather", (0.09, 0.08, 0.07), roughness=0.9)
    glass = mat("glass", (0.02, 0.02, 0.05), metallic=0.75, roughness=0.06)
    suit = mat("suit", (0.14, 0.14, 0.16), roughness=0.82)
    skin = mat("skin", (0.72, 0.52, 0.4), roughness=0.85)
    helm = mat("helmet", body_c, metallic=0.35, roughness=0.28) if not cop else mat(
        "helmet", (0.92, 0.92, 0.95), metallic=0.35, roughness=0.28)

    sc = 0.84 if style == "rat" else 1.0
    wb = 2.05 * sc

    # Wheels — named for Godot spin
    fr, rr = 0.37 * sc, 0.4 * sc
    wf = cyl("wheel_f", fr, 0.14 * sc, (0, wb * 0.43, fr), rubber, (0, math.pi / 2, 0), parent=root)
    wr = cyl("wheel_r", rr, 0.18 * sc, (0, -wb * 0.43, rr), rubber, (0, math.pi / 2, 0), parent=root)
    cyl("tyre_f", fr * 1.04, 0.16 * sc, wf.location, rubber, (0, math.pi / 2, 0), parent=root)
    cyl("disc_f", fr * 0.68, 0.035, (0, wb * 0.43, fr + 0.02), chrome, (0, math.pi / 2, 0), parent=root)
    cyl("disc_r", rr * 0.68, 0.04, (0, -wb * 0.43, rr + 0.02), chrome, (0, math.pi / 2, 0), parent=root)

    # Frame / engine block
    box("frame", (0.12 * sc, 1.15 * sc, 0.14 * sc), (0, 0.04 * sc, 0.56 * sc), chrome, bevel=0.02, parent=root)
    box("engine", (0.4 * sc, 0.58 * sc, 0.36 * sc), (0, 0.02 * sc, 0.44 * sc), dark, bevel=0.05, parent=root)

    if style == "rat":
        box("tank", (0.34 * sc, 0.58 * sc, 0.24 * sc), (0, 0.14 * sc, 0.8 * sc), body, bevel=0.05, parent=root)
        box("tail", (0.26 * sc, 0.42 * sc, 0.14 * sc), (0, -0.4 * sc, 0.78 * sc), body,
            rot=(math.radians(-12), 0, 0), bevel=0.04, parent=root)
    else:
        # Curved fairing stack — RR3D sportbike silhouette
        box("nose", (0.38 * sc, 0.42 * sc, 0.34 * sc), (0, wb * 0.4, 0.96 * sc), body,
            rot=(math.radians(-18), 0, 0), bevel=0.07, parent=root)
        box("side_l", (0.06 * sc, 0.52 * sc, 0.28 * sc), (-0.2 * sc, 0.1 * sc, 0.66 * sc), body,
            rot=(0, 0, math.radians(8)), bevel=0.04, parent=root)
        box("side_r", (0.06 * sc, 0.52 * sc, 0.28 * sc), (0.2 * sc, 0.1 * sc, 0.66 * sc), body,
            rot=(0, 0, math.radians(-8)), bevel=0.04, parent=root)
        box("belly", (0.36 * sc, 0.66 * sc, 0.28 * sc), (0, 0.06 * sc, 0.62 * sc), body, bevel=0.05, parent=root)
        box("tail", (0.3 * sc, 0.52 * sc, 0.18 * sc), (0, -0.46 * sc, 0.84 * sc), body,
            rot=(math.radians(-16), 0, 0), bevel=0.05, parent=root)
        box("windscreen", (0.3 * sc, 0.06 * sc, 0.24 * sc), (0, wb * 0.36, 1.06 * sc), glass,
            rot=(math.radians(-32), 0, 0), bevel=0.015, parent=root)

    box("seat", (0.32 * sc, 0.46 * sc, 0.09 * sc), (0, -0.18 * sc, 0.82 * sc), leather, bevel=0.03, parent=root)

    for sx in (-0.095, 0.095):
        cyl(f"fork_{sx}", 0.03 * sc, 0.78 * sc, (sx * sc, wb * 0.38, 0.64 * sc), chrome,
            rot=(math.radians(-20), 0, 0), parent=root)
    box("bars", (0.58 * sc, 0.05 * sc, 0.05 * sc), (0, wb * 0.3, 1.04 * sc), dark, parent=root)
    box("clamp", (0.24 * sc, 0.07 * sc, 0.06 * sc), (0, wb * 0.34, 0.9 * sc), dark, parent=root)

    for sx in (0.15, 0.22):
        cyl(f"exhaust_{sx}", 0.048 * sc, 0.82 * sc, (sx * sc, -0.34 * sc, 0.46 * sc), chrome,
            rot=(math.radians(82), 0, 0), parent=root)

    sphere("headlight", 0.09 * sc, (0, wb * 0.46, 1.02 * sc),
           mat("light", (1, 0.98, 0.9), emission=(1, 0.95, 0.82)), parent=root)

    build_humanoid_rider(root, suit, skin, helm, glass, sc)

    if cop:
        box("beacon", (0.12 * sc, 0.1 * sc, 0.08 * sc), (0, -0.04 * sc, 0.98 * sc),
            mat("beacon", (0.95, 0.1, 0.1), emission=(1, 0.1, 0.1)), bevel=0.02, parent=root)

    export("cop_bike.glb" if cop else ("bike_rat.glb" if style == "rat" else "bike.glb"))


def build_runner():
    """Off-bike runner — visible when sprinting back to the machine."""
    reset()
    root = bpy.data.objects.new("runner_root", None)
    bpy.context.collection.objects.link(root)
    suit = mat("suit", (0.14, 0.14, 0.16), roughness=0.85)
    skin = mat("skin", (0.72, 0.52, 0.4), roughness=0.85)
    helm = mat("helmet", (0.9, 0.35, 0.1), metallic=0.3, roughness=0.3)
    box("torso", (0.42, 0.28, 0.22), (0, 0, 1.05), suit, rot=(math.radians(8), 0, 0), bevel=0.04, parent=root)
    sphere("helmet", 0.16, (0, 0, 1.42), helm, parent=root)
    for side, sx in [("l", -1), ("r", 1)]:
        box(f"arm_{side}", (0.1, 0.38, 0.1), (sx * 0.28, 0, 1.0), suit,
            rot=(math.radians(-30), 0, sx * math.radians(20)), bevel=0.02, parent=root)
        box(f"leg_{side}", (0.12, 0.42, 0.12), (sx * 0.12, 0, 0.48), suit, bevel=0.02, parent=root)
    export("runner.glb")


def build_car():
    reset()
    body = mat("paint", (0.78, 0.78, 0.8), metallic=0.45, roughness=0.38)
    glass = mat("glass", (0.12, 0.14, 0.18), metallic=0.65, roughness=0.12)
    dark = mat("tyres", (0.05, 0.05, 0.06), roughness=0.92)
    box("body", (1.85, 4.5, 0.72), (0, 0, 0.64), body, bevel=0.09)
    box("cabin", (1.65, 2.25, 0.64), (0, -0.18, 1.22), glass, bevel=0.1)
    for sx in (-0.84, 0.84):
        for sy in (1.48, -1.48):
            cyl(f"w_{sx}_{sy}", 0.35, 0.22, (sx, sy, 0.35), dark, (0, math.pi / 2, 0))
    export("car.glb")


def build_tree():
    reset()
    trunk = mat("trunk", (0.24, 0.16, 0.1), roughness=0.95)
    leaf = mat("leaf", (0.13, 0.32, 0.12), roughness=0.95)
    cyl("trunk", 0.22, 2.6, (0, 0, 1.3), trunk, verts=10)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.5, location=(0, 0, 3.4))
    bpy.context.active_object.data.materials.append(leaf)
    export("tree.glb")


def build_rock():
    reset()
    rock = mat("rock", (0.45, 0.38, 0.3), roughness=1.0)
    bpy.ops.mesh.primitive_ico_sphere_add(radius=1.4, subdivisions=1, location=(0, 0, 0.8))
    bpy.context.active_object.data.materials.append(rock)
    export("rock.glb")


def build_building():
    reset()
    concrete = mat("concrete", (0.35, 0.36, 0.4), roughness=0.9)
    glass = mat("windows", (0.4, 0.55, 0.7), metallic=0.7, roughness=0.15, emission=(0.9, 0.8, 0.5))
    box("tower", (8, 8, 18), (0, 0, 9), concrete)
    for level in range(4):
        box(f"band{level}", (8.3, 8.3, 1.2), (0, 0, 4 + level * 4), glass)
    export("building.glb")


def build_building_shop():
    reset()
    brick = mat("brick", (0.42, 0.28, 0.22), roughness=0.92)
    glass = mat("shop_glass", (0.55, 0.7, 0.85), metallic=0.5, roughness=0.12, emission=(0.7, 0.85, 1.0))
    awning = mat("awning", (0.85, 0.15, 0.12), roughness=0.8)
    box("shop_base", (7, 6, 5), (0, 0, 2.5), brick, bevel=0.06)
    box("shop_window", (6, 0.4, 3.2), (0, 3.1, 2.2), glass, bevel=0.02)
    box("shop_awning", (7.2, 1.2, 0.3), (0, 3.4, 3.8), awning, bevel=0.02)
    box("shop_roof", (7.5, 7.5, 1.2), (0, 0, 5.6), brick, bevel=0.04)
    export("building_shop.glb")


def build_building_apartment():
    reset()
    concrete = mat("concrete", (0.32, 0.34, 0.38), roughness=0.88)
    glass = mat("apt_glass", (0.35, 0.5, 0.65), metallic=0.65, roughness=0.18, emission=(0.85, 0.75, 0.45))
    box("apt_base", (6, 6, 14), (0, 0, 7), concrete, bevel=0.05)
    for row in range(5):
        for col in range(2):
            box(f"win_{row}_{col}", (1.4, 0.08, 1.6),
                (-1.4 + col * 2.8, 3.05, 2.5 + row * 2.4), glass, bevel=0.01)
    export("building_apartment.glb")


def build_weapon_club():
    reset()
    wood = mat("wood", (0.45, 0.28, 0.14), roughness=0.85)
    cyl("club", 0.045, 0.72, (0, 0, 0.36), wood, rot=(0, 0, math.radians(18)))
    export("weapon_club.glb")


def build_weapon_bat():
    reset()
    wood = mat("bat", (0.38, 0.24, 0.12), roughness=0.8)
    cyl("bat", 0.038, 0.82, (0, 0, 0.41), wood, rot=(0, 0, math.radians(12)))
    export("weapon_bat.glb")


def build_sign():
    reset()
    metal = mat("pole", (0.35, 0.36, 0.4), metallic=0.7, roughness=0.4)
    sign = mat("face", (0.9, 0.85, 0.1), roughness=0.7)
    cyl("post", 0.05, 2.4, (0, 0, 1.2), metal)
    box("board", (0.9, 0.06, 0.6), (0, 0, 2.2), sign, bevel=0.02)
    export("sign.glb")


def build_barrier():
    reset()
    paint = mat("barrier", (0.9, 0.75, 0.1), roughness=0.75)
    stripe = mat("stripe", (0.1, 0.1, 0.12), roughness=0.9)
    box("base", (3.2, 1.2, 0.9), (0, 0, 0.45), paint, bevel=0.04)
    box("stripe_a", (3.25, 1.25, 0.15), (0, 0, 0.55), stripe)
    export("barrier.glb")


if __name__ == "__main__":
    build_bike("sport", cop=False)
    build_bike("rat", cop=False)
    build_bike("sport", cop=True)
    build_runner()
    build_car()
    build_tree()
    build_rock()
    build_building()
    build_building_shop()
    build_building_apartment()
    build_weapon_club()
    build_weapon_bat()
    build_sign()
    build_barrier()
    print("ALL ASSETS DONE")
    sys.exit(0)
