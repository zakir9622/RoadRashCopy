"""Skeletal Road Rash 3D rider: armature, skinned mesh, NLA animation clips.

Clips exported for Godot AnimationPlayer:
  ride, ride_tuck, punch_l, punch_r, kick, windup_l, windup_r,
  crash, run, remount
"""
import bpy
import math
from mathutils import Vector


BONES = [
    "hips", "spine", "chest", "neck", "head",
    "shoulder_l", "upper_arm_l", "forearm_l", "hand_l",
    "shoulder_r", "upper_arm_r", "forearm_r", "hand_r",
    "thigh_l", "shin_l", "foot_l",
    "thigh_r", "shin_r", "foot_r",
]


def _rad(deg):
    return math.radians(deg)


def _add_bone(edit_bones, name, head, tail, parent=None, connect=False):
    b = edit_bones.new(name)
    b.head = Vector(head)
    b.tail = Vector(tail)
    if parent is not None:
        b.parent = parent
        b.use_connect = connect
    b.use_deform = True
    return b


def build_armature(scale=1.0):
    s = scale
    arm_data = bpy.data.armatures.new("RiderArmature")
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.collection.objects.link(arm)
    arm.location = (0.0, -0.06 * s, 0.78 * s)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones

    hips = _add_bone(eb, "hips", (0, 0, 0), (0, 0.03 * s, 0.11 * s))
    spine = _add_bone(eb, "spine", hips.tail, (0, 0.10 * s, 0.28 * s), hips, True)
    chest = _add_bone(eb, "chest", spine.tail, (0, 0.18 * s, 0.44 * s), spine, True)
    neck = _add_bone(eb, "neck", chest.tail, (0, 0.24 * s, 0.52 * s), chest, True)
    _add_bone(eb, "head", neck.tail, (0, 0.30 * s, 0.66 * s), neck, True)

    for sx, side in [(-1, "l"), (1, "r")]:
        sh = _add_bone(
            eb, f"shoulder_{side}",
            (sx * 0.08 * s, 0.16 * s, 0.42 * s),
            (sx * 0.18 * s, 0.16 * s, 0.42 * s),
            chest, False,
        )
        ua = _add_bone(
            eb, f"upper_arm_{side}",
            sh.tail,
            (sx * 0.26 * s, 0.04 * s, 0.34 * s),
            sh, True,
        )
        fa = _add_bone(
            eb, f"forearm_{side}",
            ua.tail,
            (sx * 0.32 * s, -0.08 * s, 0.28 * s),
            ua, True,
        )
        _add_bone(
            eb, f"hand_{side}",
            fa.tail,
            (sx * 0.34 * s, -0.14 * s, 0.26 * s),
            fa, True,
        )

        th = _add_bone(
            eb, f"thigh_{side}",
            (sx * 0.09 * s, 0.0, 0.04 * s),
            (sx * 0.13 * s, -0.14 * s, 0.0),
            hips, False,
        )
        shn = _add_bone(
            eb, f"shin_{side}",
            th.tail,
            (sx * 0.12 * s, -0.30 * s, -0.02 * s),
            th, True,
        )
        _add_bone(
            eb, f"foot_{side}",
            shn.tail,
            (sx * 0.12 * s, -0.42 * s, 0.0),
            shn, True,
        )

    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    return arm


def _align_z(obj, direction):
    if direction.length < 1e-6:
        return
    obj.rotation_euler = direction.normalized().to_track_quat("Z", "Y").to_euler()


def _cyl_between(name, a, b, radius, material, verts=12):
    a, b = Vector(a), Vector(b)
    mid = (a + b) * 0.5
    length = max((b - a).length, 0.02)
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, location=mid, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    _align_z(o, b - a)
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def _sphere_at(name, loc, radius, material, segments=12):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=loc, segments=segments, ring_count=max(10, segments // 2))
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def _world_bone(arm, name):
    bone = arm.data.bones[name]
    head = arm.matrix_world @ bone.head_local
    tail = arm.matrix_world @ bone.tail_local
    return head, tail


def _mat(name, color, metallic=0.0, roughness=0.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def build_skinned_mesh(arm, suit, skin, helm, visor, scale=1.0):
    s = scale
    parts = []
    bpy.context.view_layer.update()
    boot = _mat("boot", (0.055, 0.04, 0.032), roughness=0.92)
    glove = _mat("glove", (0.07, 0.06, 0.05), roughness=0.88)

    def bone_cyl(name, radius, material, verts=14):
        h, t = _world_bone(arm, name)
        parts.append(_cyl_between(f"mesh_{name}", h, t, radius * s, material, verts))

    # Jacketed torso — overlapping spheres, not a stick of cylinders.
    hips_h, hips_t = _world_bone(arm, "hips")
    chest_h, chest_t = _world_bone(arm, "chest")
    hips = _sphere_at("mesh_hips", (hips_h + hips_t) * 0.5, 0.145 * s, suit, 20)
    hips.scale = (1.15, 0.85, 0.95)
    parts.append(hips)
    chest = _sphere_at("mesh_chest", (chest_h + chest_t) * 0.5, 0.175 * s, suit, 20)
    chest.scale = (1.22, 0.72, 1.15)
    parts.append(chest)
    bone_cyl("spine", 0.14, suit)
    bone_cyl("chest", 0.13, suit)
    bone_cyl("neck", 0.052, skin)
    h, t = _world_bone(arm, "head")
    head_c = (h + t) * 0.5
    parts.append(_sphere_at("mesh_head", head_c, 0.115 * s, skin, 24))
    helm_c = head_c + Vector((0, 0.018 * s, 0.018 * s))
    helmet = _sphere_at("helmet", helm_c, 0.142 * s, helm, 24)
    parts.append(helmet)
    visor_c = head_c + Vector((0, 0.11 * s, 0.012 * s))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.092 * s, location=visor_c, segments=18, ring_count=10)
    vis = bpy.context.active_object
    vis.name = "visor"
    vis.scale = (1.08, 0.32, 0.52)
    bpy.ops.object.transform_apply(scale=True)
    vis.rotation_euler = (_rad(12), 0, 0)
    vis.data.materials.append(visor)
    parts.append(vis)

    for side in ("l", "r"):
        bone_cyl(f"shoulder_{side}", 0.078, suit)
        bone_cyl(f"upper_arm_{side}", 0.062, suit)
        bone_cyl(f"forearm_{side}", 0.052, suit)
        hh, ht = _world_bone(arm, f"hand_{side}")
        parts.append(_sphere_at(f"glove_{side}", (hh + ht) * 0.5, 0.055 * s, glove, 14))
        bone_cyl(f"thigh_{side}", 0.092, suit)
        bone_cyl(f"shin_{side}", 0.068, suit)
        fh, ft = _world_bone(arm, f"foot_{side}")
        boot_cyl = _cyl_between(f"boot_{side}", fh, ft, 0.058 * s, boot, 12)
        parts.append(boot_cyl)
        heel = _sphere_at(f"boot_heel_{side}", fh, 0.048 * s, boot, 12)
        parts.append(heel)
        toe = _sphere_at(f"boot_toe_{side}", ft + Vector((0, -0.02 * s, 0)), 0.042 * s, boot, 12)
        parts.append(toe)
        parts.append(_sphere_at(f"knee_{side}", fh, 0.055 * s, suit, 12))

    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.active_object
    mesh.name = "rider_mesh"
    bpy.ops.object.shade_smooth()

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    return mesh


def _new_action(arm, name):
    action = bpy.data.actions.new(name)
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = action
    return action


def _key(arm, bone, frame, rot=(0, 0, 0), loc=None):
    pb = arm.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (_rad(rot[0]), _rad(rot[1]), _rad(rot[2]))
    pb.keyframe_insert(data_path="rotation_euler", frame=frame)
    if loc is not None:
        pb.location = loc
        pb.keyframe_insert(data_path="location", frame=frame)


def _reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)


def _push_nla(arm, name, start, end):
    action = arm.animation_data.action
    action.name = name
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, int(start), action)
    strip.frame_end = end
    arm.animation_data.action = None


def _fcurve_linear(arm):
    action = arm.animation_data.action
    if action is None:
        return
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = "AUTO_CLAMPED"
            kp.handle_right_type = "AUTO_CLAMPED"


def animate_all(arm, scale=1.0):
    s = scale
    scene = bpy.context.scene
    scene.render.fps = 24

    # --- ride: seated weight-shift + breathe (loop) ---
    _new_action(arm, "ride")
    for fr, hip_z, spine_x, head_y in (
        (1, 0, 2, 0),
        (7, 1.5, 4, 4),
        (13, 0, 2, 0),
        (19, -1.5, 5, -4),
        (25, 0, 2, 0),
    ):
        _key(arm, "hips", fr, (0, 0, hip_z))
        _key(arm, "spine", fr, (spine_x, 0, 0))
        _key(arm, "chest", fr, (spine_x * 0.4, 0, 0))
        _key(arm, "head", fr, (4, head_y, 0))
        _key(arm, "upper_arm_l", fr, (6, 0, -4))
        _key(arm, "upper_arm_r", fr, (6, 0, 4))
    _fcurve_linear(arm)
    _push_nla(arm, "ride", 1, 25)

    # --- ride_tuck: crouched over the tank ---
    _reset_pose(arm)
    _new_action(arm, "ride_tuck")
    for fr, extra in ((1, 0), (13, 3), (25, 0)):
        _key(arm, "spine", fr, (18 + extra, 0, 0))
        _key(arm, "chest", fr, (22 + extra * 0.5, 0, 0))
        _key(arm, "head", fr, (12, 0, 0))
        _key(arm, "upper_arm_l", fr, (14, 0, -8))
        _key(arm, "upper_arm_r", fr, (14, 0, 8))
        _key(arm, "hips", fr, (6, 0, 0), loc=(0, 0.01 * s, -0.02 * s))
    _fcurve_linear(arm)
    _push_nla(arm, "ride_tuck", 1, 25)

    # --- punch left / right ---
    for side, sx in (("l", -1), ("r", 1)):
        _reset_pose(arm)
        _new_action(arm, f"punch_{side}")
        other = "r" if side == "l" else "l"
        _key(arm, "hips", 1, (0, 0, 0))
        _key(arm, f"upper_arm_{side}", 1, (6, 0, sx * 4))
        _key(arm, f"forearm_{side}", 1, (0, 0, 0))
        _key(arm, "spine", 1, (2, 0, 0))
        _key(arm, "chest", 4, (4, 0, sx * -12))
        _key(arm, f"upper_arm_{side}", 4, (-55, sx * 25, sx * 20))
        _key(arm, f"forearm_{side}", 4, (-80, 0, 0))
        _key(arm, "head", 4, (0, sx * 18, 0))
        _key(arm, "chest", 7, (8, 0, sx * 18))
        _key(arm, f"upper_arm_{side}", 7, (-95, sx * -10, sx * 8))
        _key(arm, f"forearm_{side}", 7, (-10, 0, 0))
        _key(arm, f"hand_{side}", 7, (0, 0, 0))
        _key(arm, f"upper_arm_{other}", 7, (20, 0, sx * -8))
        _key(arm, "spine", 7, (6, 0, sx * 10))
        _key(arm, "chest", 12, (2, 0, 0))
        _key(arm, f"upper_arm_{side}", 12, (6, 0, sx * 4))
        _key(arm, f"forearm_{side}", 12, (0, 0, 0))
        _key(arm, "head", 12, (4, 0, 0))
        _fcurve_linear(arm)
        _push_nla(arm, f"punch_{side}", 1, 12)

    # --- wind-up ---
    for side, sx in (("l", -1), ("r", 1)):
        _reset_pose(arm)
        _new_action(arm, f"windup_{side}")
        for fr in (1, 10):
            _key(arm, "chest", fr, (6, 0, sx * -16))
            _key(arm, f"upper_arm_{side}", fr, (-40, sx * 30, sx * 25))
            _key(arm, f"forearm_{side}", fr, (-100, 0, 0))
            _key(arm, "head", fr, (0, sx * 12, 0))
        _fcurve_linear(arm)
        _push_nla(arm, f"windup_{side}", 1, 10)

    # --- kick (right leg shove, Road Rash style) ---
    _reset_pose(arm)
    _new_action(arm, "kick")
    _key(arm, "hips", 1, (0, 0, 0))
    _key(arm, "thigh_r", 1, (0, 0, 0))
    _key(arm, "shin_r", 1, (0, 0, 0))
    _key(arm, "hips", 4, (0, 0, -8))
    _key(arm, "thigh_r", 4, (-40, 0, 15))
    _key(arm, "shin_r", 4, (50, 0, 0))
    _key(arm, "spine", 4, (8, 0, -6))
    _key(arm, "thigh_r", 7, (25, 0, 35))
    _key(arm, "shin_r", 7, (-10, 0, 0))
    _key(arm, "foot_r", 7, (20, 0, 0))
    _key(arm, "hips", 7, (0, 0, 10))
    _key(arm, "spine", 7, (4, 0, -12))
    _key(arm, "upper_arm_r", 7, (-20, 0, 15))
    _key(arm, "thigh_r", 14, (0, 0, 0))
    _key(arm, "shin_r", 14, (0, 0, 0))
    _key(arm, "hips", 14, (0, 0, 0))
    _key(arm, "spine", 14, (2, 0, 0))
    _fcurve_linear(arm)
    _push_nla(arm, "kick", 1, 14)

    # --- crash tumble ---
    _reset_pose(arm)
    _new_action(arm, "crash")
    _key(arm, "hips", 1, (0, 0, 0))
    _key(arm, "spine", 4, (35, 20, 40))
    _key(arm, "chest", 4, (20, -15, 25))
    _key(arm, "head", 4, (30, 40, 0))
    _key(arm, "upper_arm_l", 4, (-80, 40, -30))
    _key(arm, "upper_arm_r", 4, (-60, -50, 40))
    _key(arm, "thigh_l", 4, (40, 0, -25))
    _key(arm, "thigh_r", 4, (-20, 0, 30))
    _key(arm, "hips", 10, (50, 0, 80), loc=(0, 0.05 * s, 0.08 * s))
    _key(arm, "spine", 10, (10, -40, -60))
    _key(arm, "head", 10, (-20, -30, 50))
    _key(arm, "upper_arm_l", 10, (-20, -70, 10))
    _key(arm, "upper_arm_r", 10, (-90, 20, -40))
    _key(arm, "thigh_l", 10, (-30, 0, 20))
    _key(arm, "thigh_r", 10, (50, 0, -15))
    _key(arm, "hips", 20, (15, 0, 20), loc=(0, 0.02 * s, -0.04 * s))
    _key(arm, "spine", 20, (25, 10, 15))
    _key(arm, "head", 20, (10, 0, 0))
    _fcurve_linear(arm)
    _push_nla(arm, "crash", 1, 20)

    # --- run cycle (standing sprint back to bike) ---
    _reset_pose(arm)
    _new_action(arm, "run")
    stand = (0, 0.02 * s, 0.12 * s)
    for fr, swing in ((1, 0), (5, 1), (9, 0), (13, -1), (17, 0)):
        _key(arm, "hips", fr, (8, 0, swing * 6), loc=stand)
        _key(arm, "spine", fr, (-6, 0, swing * -4))
        _key(arm, "chest", fr, (4, 0, 0))
        _key(arm, "head", fr, (-8, swing * 8, 0))
        _key(arm, "thigh_l", fr, (swing * 45, 0, 0))
        _key(arm, "thigh_r", fr, (-swing * 45, 0, 0))
        _key(arm, "shin_l", fr, (20 + abs(swing) * 25, 0, 0))
        _key(arm, "shin_r", fr, (20 + abs(swing) * 25, 0, 0))
        _key(arm, "upper_arm_l", fr, (-swing * 50, 0, -10))
        _key(arm, "upper_arm_r", fr, (swing * 50, 0, 10))
        _key(arm, "forearm_l", fr, (-40, 0, 0))
        _key(arm, "forearm_r", fr, (-40, 0, 0))
    _fcurve_linear(arm)
    _push_nla(arm, "run", 1, 17)

    # --- remount ---
    _reset_pose(arm)
    _new_action(arm, "remount")
    _key(arm, "hips", 1, (8, 0, 0), loc=(0, 0.02 * s, 0.12 * s))
    _key(arm, "thigh_l", 1, (20, 0, 0))
    _key(arm, "thigh_r", 1, (-10, 0, 0))
    _key(arm, "hips", 6, (0, 0, 0), loc=(0, 0.04 * s, 0.18 * s))
    _key(arm, "spine", 6, (-10, 0, 0))
    _key(arm, "hips", 12, (0, 0, 0), loc=(0, 0, 0))
    _key(arm, "spine", 12, (2, 0, 0))
    _key(arm, "upper_arm_l", 12, (6, 0, -4))
    _key(arm, "upper_arm_r", 12, (6, 0, 4))
    _fcurve_linear(arm)
    _push_nla(arm, "remount", 1, 12)

    _reset_pose(arm)


def build_rigged_rider(parent_root, suit, skin, helm, visor, scale=1.0):
    arm = build_armature(scale)
    arm.parent = parent_root
    build_skinned_mesh(arm, suit, skin, helm, visor, scale)
    animate_all(arm, scale)
    # Keep named hooks so legacy Godot pose code still finds something.
    arm.name = "Armature"
    return arm
