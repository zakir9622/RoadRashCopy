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
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rider_rig import build_rigged_rider
from motorcycle import build_street_bike


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


def export(filename, animations=False):
    path = os.path.join(OUT, filename)
    bpy.ops.object.select_all(action="SELECT")
    kwargs = {
        "filepath": path,
        "export_format": "GLB",
        "use_selection": True,
        "export_animations": True,
        "export_nla_strips": True,
        "export_force_sampling": True,
        "export_apply": False,
    }
    bpy.ops.export_scene.gltf(**kwargs)
    print("WROTE", path, "anims" if animations else "")


def join_meshes():
    """Collapse every mesh into one object so Godot MultiMesh can instance it."""
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        return
    bpy.context.view_layer.objects.active = meshes[0]
    try:
        bpy.ops.object.mode_set(mode="OBJECT")
    except Exception:
        pass
    for o in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers):
            try:
                bpy.ops.object.modifier_apply(modifier=mod.name)
            except Exception:
                pass
    if len(meshes) < 2:
        return
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()


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

    kind = "cop" if cop else style
    paint = {
        "rat": (0.82, 0.12, 0.10),
        "sport": (0.08, 0.18, 0.72),
        "kami": (0.08, 0.08, 0.09),
        "super": (0.72, 0.04, 0.10),
        "cop": (0.10, 0.18, 0.55),
    }[kind]
    suit = mat("suit", (0.12, 0.12, 0.13), roughness=0.78)
    skin = mat("skin", (0.78, 0.56, 0.42), roughness=0.62)
    helm = mat("helmet", paint, metallic=0.45, roughness=0.22)
    if cop:
        helm = mat("helmet", (0.92, 0.92, 0.95), metallic=0.4, roughness=0.22)
    glass = mat("glass", (0.04, 0.05, 0.08), metallic=0.3, roughness=0.05)

    sc = {"rat": 0.86, "sport": 1.0, "kami": 1.04, "super": 1.08, "cop": 1.0}[kind]
    build_street_bike(root, kind)
    build_rigged_rider(root, suit, skin, helm, glass, sc)

    out = {
        "cop": "cop_bike.glb",
        "rat": "bike_rat.glb",
        "sport": "bike.glb",
        "kami": "bike_kami.glb",
        "super": "bike_super.glb",
    }[kind]
    export(out, animations=True)


def build_runner():
    """Standalone skinned runner with the same armature clips as the bike rider."""
    reset()
    root = bpy.data.objects.new("runner_root", None)
    bpy.context.collection.objects.link(root)
    suit = mat("suit", (0.14, 0.14, 0.16), roughness=0.85)
    skin = mat("skin", (0.72, 0.52, 0.4), roughness=0.85)
    helm = mat("helmet", (0.9, 0.35, 0.1), metallic=0.3, roughness=0.3)
    visor = mat("visor", (0.02, 0.02, 0.05), metallic=0.75, roughness=0.06)
    build_rigged_rider(root, suit, skin, helm, visor, 1.0)
    export("runner.glb", animations=True)


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
    join_meshes()
    export("tree.glb")


def build_rock():
    reset()
    rock = mat("rock", (0.45, 0.38, 0.3), roughness=1.0)
    bpy.ops.mesh.primitive_ico_sphere_add(radius=1.4, subdivisions=1, location=(0, 0, 0.8))
    bpy.context.active_object.data.materials.append(rock)
    export("rock.glb")


def build_palm():
    reset()
    trunk_m = mat("palm_trunk", (0.45, 0.32, 0.16), roughness=0.95)
    leaf_m = mat("frond", (0.16, 0.42, 0.12), roughness=0.9)
    cyl("trunk", 0.14, 5.4, (0, 0, 2.7), trunk_m, verts=8)
    for i in range(8):
        ang = i * (2 * math.pi / 8)
        box(f"frond{i}", (0.18, 3.1, 0.07),
            (math.cos(ang) * 0.7, math.sin(ang) * 0.7, 5.55),
            leaf_m,
            rot=(math.radians(28), 0, ang + math.pi / 2), bevel=0.01)
    join_meshes()
    export("palm.glb")


def build_cactus():
    reset()
    green = mat("cactus", (0.22, 0.44, 0.18), roughness=0.88)
    cyl("stem", 0.26, 2.5, (0, 0, 1.25), green, verts=10)
    cyl("arm", 0.14, 1.15, (0.55, 0, 1.7), green, rot=(0, math.pi / 2, 0), verts=8)
    cyl("arm_up", 0.12, 0.7, (0.95, 0, 2.15), green, verts=8)
    join_meshes()
    export("cactus.glb")


def _window_grid(glass, width, depth, height, rows, cols, face_y):
    cell_w = width * 0.78 / cols
    cell_h = height * 0.72 / rows
    start_x = -width * 0.35
    start_z = height * 0.14
    for row in range(rows):
        for col in range(cols):
            box(f"win_{row}_{col}", (cell_w * 0.72, 0.08, cell_h * 0.7),
                (start_x + col * cell_w, face_y, start_z + row * cell_h),
                glass, bevel=0.0)


def build_building():
    reset()
    concrete = mat("concrete", (0.38, 0.39, 0.44), roughness=0.9)
    glass = mat("windows", (0.35, 0.52, 0.68), metallic=0.72, roughness=0.14, emission=(0.9, 0.8, 0.5))
    trim = mat("trim", (0.22, 0.22, 0.25), roughness=0.7)
    box("tower", (8, 8, 20), (0, 0, 10), concrete, bevel=0.04)
    box("crown", (8.6, 8.6, 0.7), (0, 0, 20.2), trim, bevel=0.02)
    _window_grid(glass, 8, 8, 20, 7, 4, 4.05)
    join_meshes()
    export("building.glb")


def build_building_shop():
    reset()
    brick = mat("brick", (0.46, 0.30, 0.22), roughness=0.92)
    glass = mat("shop_glass", (0.55, 0.72, 0.88), metallic=0.5, roughness=0.12, emission=(0.75, 0.88, 1.0))
    awning = mat("awning", (0.85, 0.14, 0.12), roughness=0.8)
    sign = mat("sign", (0.95, 0.85, 0.15), roughness=0.55, emission=(0.95, 0.8, 0.2))
    box("shop_base", (7.2, 6.4, 5.2), (0, 0, 2.6), brick, bevel=0.05)
    box("shop_window", (5.8, 0.18, 2.8), (0, 3.22, 2.15), glass, bevel=0.01)
    box("shop_door", (1.4, 0.12, 2.4), (2.2, 3.22, 1.3), glass, bevel=0.01)
    box("shop_awning", (7.4, 1.4, 0.22), (0, 3.55, 3.7), awning, bevel=0.02)
    box("shop_sign", (3.2, 0.12, 0.7), (0, 3.3, 4.55), sign, bevel=0.01)
    box("shop_roof", (7.6, 6.8, 0.9), (0, 0, 5.7), brick, bevel=0.03)
    join_meshes()
    export("building_shop.glb")


def build_building_apartment():
    reset()
    concrete = mat("concrete", (0.34, 0.36, 0.40), roughness=0.88)
    glass = mat("apt_glass", (0.32, 0.48, 0.62), metallic=0.65, roughness=0.18, emission=(0.85, 0.75, 0.45))
    rail = mat("rail", (0.55, 0.55, 0.58), metallic=0.4, roughness=0.5)
    box("apt_base", (7, 7, 16), (0, 0, 8), concrete, bevel=0.04)
    _window_grid(glass, 7, 7, 16, 6, 3, 3.54)
    for row in range(4):
        box(f"balc_{row}", (4.2, 0.9, 0.12), (0, 3.85, 3.6 + row * 2.8), rail, bevel=0.01)
    join_meshes()
    export("building_apartment.glb")


def build_building_office():
    reset()
    concrete = mat("office", (0.28, 0.32, 0.38), roughness=0.72, metallic=0.15)
    glass = mat("curtain", (0.25, 0.42, 0.58), metallic=0.8, roughness=0.08, emission=(0.55, 0.7, 0.95))
    crown = mat("crown", (0.18, 0.2, 0.24), roughness=0.5)
    box("tower", (10, 10, 28), (0, 0, 14), concrete, bevel=0.03)
    _window_grid(glass, 10, 10, 28, 10, 5, 5.05)
    box("crown", (10.6, 10.6, 1.1), (0, 0, 28.4), crown, bevel=0.02)
    box("mast", (0.35, 0.35, 3.2), (0, 0, 30.4), crown, bevel=0.0)
    join_meshes()
    export("building_office.glb")


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
    extra = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    world_only = extra and extra[0] == "world"

    def world_props():
        build_car()
        build_tree()
        build_rock()
        build_palm()
        build_cactus()
        build_building()
        build_building_shop()
        build_building_apartment()
        build_building_office()
        build_weapon_club()
        build_weapon_bat()
        build_sign()
        build_barrier()

    if not world_only:
        build_bike("sport", cop=False)
        build_bike("rat", cop=False)
        build_bike("kami", cop=False)
        build_bike("super", cop=False)
        build_bike("sport", cop=True)
        build_runner()
    world_props()
    print("ALL ASSETS DONE")
    sys.exit(0)
