"""ContextSpeak zoo animals — runs inside Blender 5.3 (via the Blender MCP or Scripting tab).

For each species: import the Meshy model, face it towards -Y (Unity +Z), scale it to its real height,
decimate it to a game budget, build a quadruped armature from hand-measured landmarks, skin it
with rule-based weights (legs below the belly line, smooth distance blending on the body, trunk
and ears only where they belong) and export a skinned GLB. Unity's AnimalAnimator then drives the
bones procedurally (gait, grazing, drinking, ear flaps, trunk swing, tail, breathing).

Landmarks are (u, v): u = 0 at the nose → 1 at the tail end, v = 0 at the ground → 1 at the top.

    exec(open(".../tools/blender/zoo_animals.py").read()); build_all()
"""
import bpy, glob, math, os
import numpy as np
import mathutils

DOWNLOADS = "/Users/elaineting/Downloads/unitycomponent/"
SRC = "/Users/elaineting/Projects/ContextSpeak/.local/zoo_src/"
OUTS = [
    "/Users/elaineting/.codex/worktrees/capture-to-library/eng learning/games/unity/Assets/Art/zoo/",
    "/Users/elaineting/Projects/ContextSpeak/games/unity/Assets/Art/zoo/",
]

QUAD = dict(legs=True)
SPECIES = {
    # name: source, yaw (deg, to face -Y), real height (m, top of the model), triangle budget, landmarks
    "Elephant": dict(src=DOWNLOADS + "Meshy_AI_Zoo_Elephant_20261113_character.glb", yaw=0, height=3.1, tris=14000,
        belly=0.38, spine=0.70, shoulder=(0.43, 0.62), hip=(0.85, 0.62), knee=0.22, ankle=0.07,
        neck=(0.36, 0.74), headbase=(0.27, 0.80), nose=(0.15, 0.70), tail=[(0.97, 0.78), (0.99, 0.48), (1.0, 0.18)],
        trunk=[(0.18, 0.62), (0.15, 0.42), (0.17, 0.24), (0.25, 0.15)], trunk_x=0.11,
        ears=dict(root=(0.31, 0.82), tip=(0.36, 0.55), min_x=0.30)),
    "Rhino": dict(src="Rhino", yaw=0, height=1.75, tris=16000,
        belly=0.33, spine=0.68, shoulder=(0.42, 0.62), hip=(0.84, 0.62), knee=0.22, ankle=0.06,
        neck=(0.33, 0.70), headbase=(0.24, 0.66), nose=(0.04, 0.45), tail=[(0.97, 0.70), (0.99, 0.45)]),
    "Tiger": dict(src="Tiger", yaw=90, height=1.05, tris=18000, body_end=0.77,
        belly=0.40, spine=0.72, shoulder=(0.22, 0.70), hip=(0.68, 0.72), knee=0.26, ankle=0.08,
        neck=(0.17, 0.76), headbase=(0.11, 0.80), nose=(0.0, 0.74), tail=[(0.76, 0.72), (0.86, 0.45), (0.94, 0.32), (1.0, 0.45)]),
    "Deer": dict(src="Stag", yaw=0, height=1.95, tris=14000,
        belly=0.32, spine=0.47, shoulder=(0.38, 0.45), hip=(0.88, 0.47), knee=0.16, ankle=0.05,
        neck=(0.30, 0.52), headbase=(0.17, 0.68), nose=(0.0, 0.63), tail=[(0.98, 0.52), (1.0, 0.43)]),
    "Sheep": dict(src="Sheep", yaw=-90, height=0.95, tris=10000,
        belly=0.33, spine=0.65, shoulder=(0.38, 0.55), hip=(0.85, 0.55), knee=0.17, ankle=0.04,
        neck=(0.20, 0.72), headbase=(0.11, 0.85), nose=(0.0, 0.75), tail=[(0.97, 0.55), (1.0, 0.45)]),
    "Pony": dict(src="Horse", yaw=0, height=1.55, tris=14000,
        belly=0.43, spine=0.62, shoulder=(0.38, 0.55), hip=(0.83, 0.60), knee=0.27, ankle=0.06,
        neck=(0.33, 0.66), headbase=(0.13, 0.90), nose=(0.0, 0.72), tail=[(0.93, 0.66), (0.96, 0.40), (0.97, 0.18)]),
    "Pig": dict(src="Pig", yaw=0, height=0.9, tris=10000,
        belly=0.22, spine=0.60, shoulder=(0.36, 0.45), hip=(0.78, 0.45), knee=0.10, ankle=0.03,
        neck=(0.26, 0.62), headbase=(0.16, 0.62), nose=(0.0, 0.50), tail=[(0.93, 0.75), (1.0, 0.80)]),
    "Chicken": dict(src="Chicken", yaw=0, height=0.45, tris=6000, legs=False),
}


def _ctx():
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == 'VIEW_3D')
    return bpy.context.temp_override(window=win, area=area, scene=win.scene, view_layer=win.view_layer), win


def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images, bpy.data.actions):
        for item in list(block):
            if item.users == 0: block.remove(item)


def load(spec):
    ctx, win = _ctx()
    src = spec["src"]
    with ctx:
        if src.endswith(".glb"): bpy.ops.import_scene.gltf(filepath=src)
        else: bpy.ops.import_scene.fbx(filepath=glob.glob(SRC + src + "/**/*.fbx", recursive=True)[0])
    meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.data.polygons and o.name != "Icosphere"]
    for o in list(bpy.data.objects):
        if o not in meshes and o.type == 'MESH': bpy.data.objects.remove(o)
    # Bake world transforms into the vertices, drop old rigs.
    for o in meshes:
        for m in [m for m in o.modifiers if m.type == 'ARMATURE']: o.modifiers.remove(m)
        mw = o.matrix_world.copy()
        o.parent = None
        o.data.transform(mw)
        o.matrix_world = mathutils.Matrix.Identity(4)
        o.vertex_groups.clear()
        if o.data.shape_keys: o.shape_key_clear()
    for o in list(bpy.data.objects):
        if o.type == 'ARMATURE': bpy.data.objects.remove(o)
    with ctx:
        bpy.ops.object.select_all(action='DESELECT')
        for o in meshes: o.select_set(True)
        win.view_layer.objects.active = meshes[0]
        if len(meshes) > 1: bpy.ops.object.join()
    body = win.view_layer.objects.active
    R = mathutils.Matrix.Rotation(math.radians(spec["yaw"]), 4, 'Z')
    body.data.transform(R)
    # Straighten: Meshy models are often turned a little — align the long horizontal axis with Y.
    co = np.array([v.co[:] for v in body.data.vertices])
    xy = co[:, :2] - co[:, :2].mean(0)
    w, vec = np.linalg.eigh(xy.T @ xy)
    axis = vec[:, 1]
    angle = math.atan2(axis[0], axis[1])            # angle from +Y
    if angle > math.pi / 2: angle -= math.pi
    if angle < -math.pi / 2: angle += math.pi
    if spec.get("straighten", True) and abs(angle) < math.radians(40):
        body.data.transform(mathutils.Matrix.Rotation(angle, 4, 'Z'))
    co = np.array([v.co[:] for v in body.data.vertices])
    mn, mx = co.min(0), co.max(0)
    s = spec["height"] / (mx[2] - mn[2])
    centre = np.array([(mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]])
    body.data.transform(mathutils.Matrix.Diagonal((s, s, s, 1)) @ mathutils.Matrix.Translation(-mathutils.Vector(centre)))
    body.data.update()
    body.name = body.data.name = spec["name"]
    return body


def decimate(body, target):
    count = sum(len(p.vertices) - 2 for p in body.data.polygons)
    if count <= target * 1.1: return count
    ctx, win = _ctx()
    for attempt in range(3):
        count = sum(len(p.vertices) - 2 for p in body.data.polygons)
        if count <= target * 1.15: break
        mod = body.modifiers.new("Decimate", 'DECIMATE')
        mod.ratio = max(0.01, target / count)
        mod.use_collapse_triangulate = True
        with ctx:
            win.view_layer.objects.active = body
            bpy.ops.object.modifier_apply(modifier=mod.name)
    return sum(len(p.vertices) - 2 for p in body.data.polygons)


def shrink_images(size=2048):
    for image in bpy.data.images:
        if image.size[0] > size: image.scale(size, size)


def build_rig(body, spec):
    """Armature from the landmarks + rule-based skin weights."""
    co = np.array([v.co[:] for v in body.data.vertices])
    mn, mx = co.min(0), co.max(0)
    L, H, W = mx[1] - mn[1], mx[2] - mn[2], mx[0] - mn[0]
    u = (co[:, 1] - mn[1]) / L
    v = (co[:, 2] - mn[2]) / H

    def P(uv, x=0.0):
        return mathutils.Vector((x, mn[1] + uv[0] * L, mn[2] + uv[1] * H))

    bones = {}       # name -> (head, tail, parent)
    sh, hp = spec["shoulder"], spec["hip"]
    mid = ((sh[0] + hp[0]) / 2, spec["spine"])
    root = mathutils.Vector((0, mn[1] + mid[0] * L, 0))
    bones["Root"] = (root, root + mathutils.Vector((0, 0, H * 0.15)), None)
    bones["Hips"] = (P((hp[0], spec["spine"])), P(mid), "Root")
    bones["Spine"] = (P(mid), P((sh[0], spec["spine"])), "Hips")
    bones["Chest"] = (P((sh[0], spec["spine"])), P(spec["neck"]), "Spine")
    bones["Neck"] = (P(spec["neck"]), P(spec["headbase"]), "Chest")
    bones["Head"] = (P(spec["headbase"]), P(spec["nose"]), "Neck")
    tail = spec.get("tail", [])
    for i in range(len(tail) - 1):
        bones["Tail%d" % (i + 1)] = (P(tail[i]), P(tail[i + 1]), "Hips" if i == 0 else "Tail%d" % i)
    trunk = spec.get("trunk", [])
    for i in range(len(trunk) - 1):
        bones["Trunk%d" % (i + 1)] = (P(trunk[i]), P(trunk[i + 1]), "Head" if i == 0 else "Trunk%d" % i)
    ears = spec.get("ears")
    if ears:
        for side, sign in (("L", 1), ("R", -1)):
            ex = sign * W * 0.5 * ears["min_x"] * 1.4
            bones["Ear_" + side] = (P(ears["root"], ex), P(ears["tip"], ex * 1.6), "Head")

    legs = {}
    body_end = spec.get("body_end", 1.0)
    split = (sh[0] + hp[0]) / 2
    if spec.get("legs", True):
        # Feet: the lowest vertices of each quadrant (a lifted foot in a walking pose still counts);
        # then track each leg column upwards.
        lowpart = (v < spec["belly"] * 0.7) & (u < body_end) & (u > min(sh[0], spec["headbase"][0]) - 0.02)
        for front, joint in ((True, sh), (False, hp)):
            half = lowpart & ((u < split) if front else (u >= split))
            if half.sum() < 20: continue
            fx = (np.percentile(co[half, 0], 5) + np.percentile(co[half, 0], 95)) / 2
            for side, sign in (("L", 1), ("R", -1)):
                quad = half & (((co[:, 0] - fx) * sign) > 0)
                if quad.sum() < 10: continue
                zmin = co[quad, 2].min()
                m = quad & (co[:, 2] < zmin + 0.04 * H)
                key = ("F" if front else "B") + side
                c = co[m].mean(0)
                radius = max(0.05 * L, (np.percentile(np.abs(co[m, 0] - c[0]), 90) + np.percentile(np.abs(co[m, 1] - c[1]), 90)) * 0.9)
                track = {}
                level = (zmin - mn[2]) / H + 0.02
                while level <= spec["belly"] + 0.001:
                    z = mn[2] + level * H
                    sel = (np.abs(co[:, 2] - z) < 0.025 * H) & (np.hypot(co[:, 0] - c[0], co[:, 1] - c[1]) < radius) & (((co[:, 0] - fx) * sign) > -0.02 * L)
                    if sel.sum() > 3:
                        q = co[sel].mean(0)
                        c = np.array([q[0], q[1], z])
                    track[round(level, 2)] = c.copy()
                    level += 0.03

                def at(level):
                    keys = sorted(track)
                    k = min(keys, key=lambda x: abs(x - level))
                    return mathutils.Vector(track[k])
                ankle = at(spec["ankle"]); knee = at(spec["knee"]); top = at(spec["belly"])
                foot = mathutils.Vector((ankle.x, ankle.y - H * 0.03, min(zmin, ankle.z - 0.02 * H)))
                hipj = mathutils.Vector((top.x * 0.85, top.y, mn[2] + joint[1] * H))
                parent = "Chest" if front else "Hips"
                bones["Leg_%s_1" % key] = (hipj, knee, parent)
                bones["Leg_%s_2" % key] = (knee, ankle, "Leg_%s_1" % key)
                bones["Leg_%s_3" % key] = (ankle, foot, "Leg_%s_2" % key)
                legs[key] = (radius, sign, front)

    # ---- armature object
    ctx, win = _ctx()
    arm_data = bpy.data.armatures.new(spec["name"] + "Rig")
    arm = bpy.data.objects.new(spec["name"] + "Rig", arm_data)
    win.scene.collection.objects.link(arm)
    with ctx:
        bpy.ops.object.select_all(action='DESELECT')
        win.view_layer.objects.active = arm
        arm.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        for name, (h, t, parent) in bones.items():
            b = arm_data.edit_bones.new(name)
            b.head, b.tail = h, t
            if (t - h).length < 1e-3: b.tail = h + mathutils.Vector((0, 0, 0.05))
        for name, (h, t, parent) in bones.items():
            if parent: arm_data.edit_bones[name].parent = arm_data.edit_bones[parent]
        bpy.ops.object.mode_set(mode='OBJECT')

    # ---- weights: chain projection, so a vertex only ever blends between neighbouring bones
    n = len(co)
    names = list(bones.keys())
    idx = {b: i for i, b in enumerate(names)}

    def chain(points, labels, blend):
        """Nearest point on a polyline → (distance, weights over labels) with linear blending at joints."""
        pts = [np.array(p[:]) for p in points]
        best = np.full(n, 1e9); seg = np.zeros(n, int); tt = np.zeros(n); seglen = np.zeros(len(pts) - 1)
        for k in range(len(pts) - 1):
            a, b = pts[k], pts[k + 1]
            ab = b - a; L2 = max(ab @ ab, 1e-9); seglen[k] = math.sqrt(L2)
            t = np.clip(((co - a) @ ab) / L2, 0, 1)
            d = np.linalg.norm(co - (a + t[:, None] * ab), axis=1)
            better = d < best
            best[better] = d[better]; seg[better] = k; tt[better] = t[better]
        w = np.zeros((n, len(names)))
        rows = np.arange(n)
        for k in range(len(labels)):
            on = seg == k
            if not on.any(): continue
            s_from_start = tt[on] * seglen[k]
            s_to_end = (1 - tt[on]) * seglen[k]
            own = np.ones(on.sum())
            if k > 0:
                f = np.clip(0.5 - s_from_start / (2 * blend), 0, 0.5)
                w[rows[on], idx[labels[k - 1]]] += f; own -= f
            if k < len(labels) - 1:
                f = np.clip(0.5 - s_to_end / (2 * blend), 0, 0.5)
                w[rows[on], idx[labels[k + 1]]] += f; own -= f
            w[rows[on], idx[labels[k]]] += own
        return best, w

    blend = 0.08 * L
    main_pts = [bones["Hips"][0], bones["Spine"][0], bones["Chest"][0], bones["Neck"][0], bones["Head"][0], bones["Head"][1]]
    d_main, w_main = chain(main_pts, ["Hips", "Spine", "Chest", "Neck", "Head"], blend)
    final = w_main
    tails = [b for b in names if b.startswith("Tail")]
    if tails:
        tail_pts = [bones["Hips"][0]] + [bones[t][0] for t in tails] + [bones[tails[-1]][1]]
        d_tail, w_tail = chain(tail_pts, ["Hips"] + tails, blend * 0.5)
        tail_u0 = (body_end - 0.06) if body_end < 1 else spec["tail"][0][0] - 0.02
        is_tail = (u > tail_u0) & (d_tail < d_main)
        final[is_tail] = w_tail[is_tail]

    # Base-colour luminance per vertex (tusks/horns stay rigid with the head).
    lum = np.zeros(n)
    try:
        mat = body.data.materials[0]
        tex = next(nd for nd in mat.node_tree.nodes if nd.type == 'TEX_IMAGE' and nd.image and 'normal' not in nd.image.name.lower() and 'rough' not in nd.image.name.lower() and 'metal' not in nd.image.name.lower())
        img = tex.image; iw, ih = img.size
        px = np.array(img.pixels[:]).reshape(ih, iw, 4)
        uvl = body.data.uv_layers.active.data
        loop_uv = np.array([d.uv[:] for d in uvl])
        loop_v = np.array([l.vertex_index for l in body.data.loops])
        xs = np.clip((loop_uv[:, 0] % 1) * (iw - 1), 0, iw - 1).astype(int)
        ys = np.clip((loop_uv[:, 1] % 1) * (ih - 1), 0, ih - 1).astype(int)
        rgb = px[ys, xs, :3]
        l = rgb.mean(1) - (rgb.max(1) - rgb.min(1))
        acc = np.zeros(n); cnt = np.zeros(n)
        np.add.at(acc, loop_v, l); np.add.at(cnt, loop_v, 1)
        lum = acc / np.maximum(cnt, 1)
    except Exception:
        pass

    def smooth(x, a, b):
        t = np.clip((x - a) / (b - a), 0, 1)
        return t * t * (3 - 2 * t)

    head_w = final[:, idx["Head"]].copy()
    trunks = [b for b in names if b.startswith("Trunk")]
    if trunks:
        tpts = [bones[t][0] for t in trunks] + [bones[trunks[-1]][1]]
        d_tr, w_tr = chain(tpts, trunks, blend * 0.4)
        tusk = lum > 0.42
        member = (np.abs(co[:, 0]) < spec["trunk_x"]) & (u < 0.26) & (v < 0.68) & ~tusk & (d_tr < 0.25 * H)
        base = np.array(bones[trunks[0]][0][:])
        ramp = smooth(np.linalg.norm(co - base, axis=1), 0.02 * H, 0.10 * H)
        m = np.where(member, ramp, 0.0) * head_w
        final[:, idx["Head"]] -= m
        final += w_tr * m[:, None]
    for side, sign in (("L", 1), ("R", -1)):
        ear = "Ear_" + side
        if ear not in idx: continue
        x0 = W * 0.5 * spec["ears"]["min_x"]
        m = smooth(co[:, 0] * sign, x0, x0 + 0.10 * H) * ((u > 0.2) & (u < 0.46) & (v > 0.38)) * final[:, idx["Head"]]
        final[:, idx["Head"]] -= m
        final[:, idx[ear]] += m

    # Legs: a vertex below the belly line belongs to the nearest leg chain if that is nearer than the body.
    band = 0.06
    leg_total = np.zeros(n); leg_w = np.zeros((n, len(names)))
    best_leg = np.full(n, 1e9)
    for key in legs:
        lp = [bones["Leg_%s_1" % key][0], bones["Leg_%s_2" % key][0], bones["Leg_%s_3" % key][0], bones["Leg_%s_3" % key][1]]
        d_leg, w_leg = chain(lp, ["Leg_%s_%d" % (key, i) for i in (1, 2, 3)], 0.05 * H)
        take = d_leg < best_leg
        best_leg[take] = d_leg[take]
        leg_w[take] = w_leg[take]
    trunk_like = np.zeros(n, bool)
    for b in trunks + tails: trunk_like |= final[:, idx[b]] > 0.3
    near = np.zeros(n, bool)
    for key, (radius, _, _) in legs.items():
        h1 = bones["Leg_%s_1" % key]; h2 = bones["Leg_%s_2" % key]; h3 = bones["Leg_%s_3" % key]
        for a, b in ((h1[0], h1[1]), (h2[0], h2[1]), (h3[0], h3[1])):
            a = np.array(a[:]); b = np.array(b[:]); ab = b - a
            t = np.clip(((co - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
            near |= np.linalg.norm(co - (a + t[:, None] * ab), axis=1) < radius * 1.3
    low = (v < spec["belly"] - band) & (u < body_end) & (u > min(sh[0], spec["headbase"][0]) - 0.02)
    is_leg = ~trunk_like & (((v < spec["belly"] + band) & (best_leg < d_main * 1.05) & near) | low)
    t = np.clip((v - (spec["belly"] - band)) / (2 * band), 0, 1)
    t = np.where(is_leg, t, 1.0)
    final = leg_w * (1 - t)[:, None] + final * t[:, None]
    final = np.clip(final, 0, None)
    final /= np.maximum(final.sum(1, keepdims=True), 1e-9)

    for name in names: body.vertex_groups.new(name=name)
    for j, name in enumerate(names):
        col = final[:, j]
        nz = np.nonzero(col > 0.01)[0]
        group = body.vertex_groups[name]
        for i in nz: group.add([int(i)], float(col[i]), 'REPLACE')
    body.parent = arm
    mod = body.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    return arm, len(names)


def export(objs, name):
    ctx, win = _ctx()
    with ctx:
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        win.view_layer.objects.active = objs[0]
        first = OUTS[0] + name + ".glb"
        os.makedirs(OUTS[0], exist_ok=True)
        bpy.ops.export_scene.gltf(filepath=first, use_selection=True, export_format='GLB', export_image_format='JPEG',
                                  export_jpeg_quality=88, export_skins=True, export_animations=False, export_apply=False,
                                  export_yup=True)
    for out in OUTS[1:]:
        os.makedirs(out, exist_ok=True)
        with open(first, 'rb') as a, open(out + name + ".glb", 'wb') as b: b.write(a.read())
    return os.path.getsize(first)


def build(name):
    spec = dict(SPECIES[name]); spec["name"] = name
    clear()
    body = load(spec)
    tris = decimate(body, spec["tris"])
    shrink_images(2048)
    objs = [body]
    bones = 0
    if spec.get("legs", True):
        arm, bones = build_rig(body, spec)
        objs = [arm, body]
    size = export(objs, name)
    return {"tris": tris, "bones": bones, "kb": size // 1024}


def build_all(names=None):
    report = {}
    for name in names or SPECIES:
        try: report[name] = build(name)
        except Exception as e: report[name] = "FAILED: %r" % e
    return report


def rig_test(name, folder="/Users/elaineting/Projects/ContextSpeak/.local/zoo_shots/"):
    """Renders rest / walk / graze / ears+trunk poses of the current rig for a visual check."""
    win = bpy.context.window_manager.windows[0]; sc = win.scene
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    body = next(o for o in bpy.data.objects if o.type == 'MESH')
    H = max(v.co.z for v in body.data.vertices)
    L = max(v.co.y for v in body.data.vertices) - min(v.co.y for v in body.data.vertices)

    def pose(rot):
        for pb in arm.pose.bones: pb.rotation_mode = 'XYZ'; pb.rotation_euler = (0, 0, 0)
        for bone, e in rot.items():
            if bone in arm.pose.bones: arm.pose.bones[bone].rotation_euler = [math.radians(a) for a in e]
        bpy.context.view_layer.update()

    cam_data = bpy.data.cameras.get("SurveyCam") or bpy.data.cameras.new("SurveyCam"); cam_data.type = 'ORTHO'
    cam = bpy.data.objects.get("SurveyCam") or bpy.data.objects.new("SurveyCam", cam_data)
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    sc.camera = cam; cam_data.ortho_scale = max(L, H) * 1.35
    sc.render.resolution_x = 640; sc.render.resolution_y = 480
    files = []
    for tag, rot, side in [
        ("rest", {}, True),
        ("walk", {"Leg_FL_1": (25, 0, 0), "Leg_FR_1": (-25, 0, 0), "Leg_BL_1": (-25, 0, 0), "Leg_BR_1": (25, 0, 0), "Leg_FL_2": (-25, 0, 0), "Leg_BR_2": (25, 0, 0)}, True),
        ("graze", {"Chest": (8, 0, 0), "Neck": (35, 0, 0), "Head": (20, 0, 0), "Trunk1": (25, 0, 0), "Trunk2": (30, 0, 0)}, True),
        ("ears", {"Ear_L": (0, 0, 15), "Ear_R": (0, 0, -15), "Trunk1": (-35, 0, 0), "Trunk2": (-35, 0, 0), "Tail1": (0, 25, 0), "Neck": (0, 0, 25)}, False),
    ]:
        pose(rot)
        cam.location = (L * 3, 0, H * 0.5) if side else (L * 1.5, -L * 2.2, H * 0.8)
        cam.rotation_euler = (math.radians(90), 0, math.radians(90)) if side else (math.radians(82), 0, math.radians(34))
        sc.render.filepath = folder + "rig_%s_%s.png" % (name, tag)
        bpy.ops.render.render(write_still=True)
        files.append(sc.render.filepath)
    pose({})
    return files


# ---------------------------------------------------------------------------------------------
# Animals with no downloaded model: built here from a skeleton (Skin modifier), painted with a
# computed coat (stripes / patches), baked to a texture, rigged like the others and exported.
def _L(side, pts): return [((side * x, y, z), r) for (x, y, z), r in pts]

BLUEPRINTS = {
    "Zebra": dict(
        chains=[
            [((0, 0.55, 1.22), .27), ((0, 0.1, 1.18), .30), ((0, -0.35, 1.22), .29), ((0, -0.55, 1.30), .20), ((0, -0.78, 1.50), .15),
             ((0, -0.95, 1.66), .14), ((0, -1.12, 1.58), .11), ((0, -1.30, 1.46), .085)],
            [((0, 0.55, 1.22), .27), ((0, 0.80, 1.22), .06), ((0, 0.92, 0.95), .04), ((0, 0.95, 0.60), .05)],
        ] + sum([[
            [((0, -0.35, 1.22), .29)] + _L(s, [((.16, -0.33, 1.0), .13), ((.16, -0.33, .58), .065), ((.16, -0.33, .14), .05), ((.16, -0.36, .04), .06)]),
            [((0, 0.55, 1.22), .27)] + _L(s, [((.17, 0.55, 1.0), .16), ((.17, 0.66, .60), .07), ((.17, 0.60, .14), .05), ((.17, 0.57, .04), .06)]),
            [((0, -0.95, 1.66), .14)] + _L(s, [((.07, -0.93, 1.84), .035)]),
        ] for s in (1, -1)], []),
        spec=dict(belly=0.47, spine=0.64, shoulder=(0.43, 0.64), hip=(0.81, 0.64), knee=0.31, ankle=0.075, neck=(0.35, 0.69), headbase=(0.18, 0.885), nose=(0.035, 0.78),
                  tail=[(0.916, 0.65), (0.967, 0.5), (0.98, 0.32)], tris=12000)),
    "Giraffe": dict(
        chains=[
            [((0, 0.75, 2.05), .36), ((0, 0.2, 2.2), .42), ((0, -0.4, 2.5), .42), ((0, -0.65, 2.85), .26), ((0, -1.0, 3.55), .19), ((0, -1.3, 4.25), .15),
             ((0, -1.4, 4.5), .14), ((0, -1.62, 4.42), .10), ((0, -1.85, 4.3), .075)],
            [((0, 0.75, 2.05), .36), ((0, 1.05, 2.0), .06), ((0, 1.15, 1.5), .035), ((0, 1.17, 1.0), .06)],
        ] + sum([[
            [((0, -0.4, 2.5), .42)] + _L(s, [((.22, -0.4, 2.05), .16), ((.22, -0.4, 1.05), .08), ((.22, -0.4, .16), .06), ((.22, -0.44, .05), .075)]),
            [((0, 0.75, 2.05), .36)] + _L(s, [((.22, 0.75, 1.75), .20), ((.22, 0.9, .95), .085), ((.22, 0.82, .16), .06), ((.22, 0.78, .05), .075)]),
            [((0, -1.4, 4.5), .14)] + _L(s, [((.05, -1.4, 4.72), .03)]),
            [((0, -1.4, 4.5), .14)] + _L(s, [((.16, -1.33, 4.58), .035)]),
        ] for s in (1, -1)], []),
        spec=dict(belly=0.36, spine=0.46, shoulder=(0.48, 0.50), hip=(0.85, 0.42), knee=0.21, ankle=0.04, neck=(0.40, 0.60), headbase=(0.17, 0.95), nose=(0.02, 0.90),
                  tail=[(0.94, 0.42), (0.98, 0.31), (0.99, 0.21)], tris=12000)),
}


def _coat(name, co):
    x, y, z = co[:, 0], co[:, 1], co[:, 2]
    n = len(co)
    wob = 0.06 * np.sin(z * 9 + x * 7) + 0.04 * np.sin(y * 13 + z * 5)
    if name == "Zebra":
        legs = z < 0.9
        neck = (y < -0.5) & ~legs
        s = np.where(legs, z * 1.25, np.where(neck, (y * 0.6 - z * 0.8), y)) + wob
        dark = np.sin(s * (2 * math.pi / 0.17)) > 0.05
        dark |= (y < -1.2) | (z < 0.09) | ((y > 0.9) & (z < 0.75)) | ((np.abs(x) < 0.035) & neck & (z > 1.45))
        col = np.where(dark[:, None], np.array([0.035, 0.03, 0.03]), np.array([0.86, 0.84, 0.78]))
    else:
        rng = np.random.default_rng(7)
        g = 0.3
        cells = np.floor(co / g).astype(int)
        f1 = np.full(n, 9.0); f2 = np.full(n, 9.0); tone = np.zeros(n)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    c = cells + (dx, dy, dz)
                    h = (c[:, 0] * 73856093 ^ c[:, 1] * 19349663 ^ c[:, 2] * 83492791) & 0xFFFF
                    jit = np.stack([((h * k) % 997) / 997.0 for k in (3, 7, 13)], 1)
                    d = np.linalg.norm(co - (c + 0.15 + 0.7 * jit) * g, axis=1)
                    new1 = d < f1
                    f2 = np.where(new1, f1, np.minimum(f2, d)); tone = np.where(new1, (h % 100) / 100.0, tone); f1 = np.minimum(f1, d)
        patch = (f2 - f1) > 0.045 + 0.05 * np.clip((1.0 - z) / 1.0, 0, 1)
        patch &= z > 0.55
        brown = np.array([0.42, 0.22, 0.08])[None, :] * (0.8 + 0.4 * tone[:, None])
        col = np.where(patch[:, None], brown, np.array([0.87, 0.76, 0.52]))
        dark = (z < 0.1) | ((y > 1.1) & (z < 1.25)) | (z > 4.68) | (y < -1.78)
        col = np.where(dark[:, None], np.array([0.1, 0.07, 0.05]), col)
    return np.clip(col, 0, 1)


def build_procedural(name):
    bp = BLUEPRINTS[name]
    clear()
    ctx, win = _ctx()
    verts, edges, radii, index = [], [], [], {}
    for chain in bp["chains"]:
        prev = None
        for p, r in chain:
            key = tuple(round(v, 4) for v in p)
            if key not in index: index[key] = len(verts); verts.append(p); radii.append(r)
            if prev is not None: edges.append((prev, index[key]))
            prev = index[key]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, edges, [])
    body = bpy.data.objects.new(name, mesh)
    win.scene.collection.objects.link(body)
    skin = body.modifiers.new("Skin", 'SKIN')
    for i, r in enumerate(radii):
        mesh.skin_vertices[0].data[i].radius = (r, r)
        mesh.skin_vertices[0].data[i].use_root = i == 0
    sub = body.modifiers.new("Subsurf", 'SUBSURF'); sub.levels = 3
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); win.view_layer.objects.active = body
        bpy.ops.object.modifier_apply(modifier="Skin"); bpy.ops.object.modifier_apply(modifier="Subsurf")
        bpy.ops.object.shade_smooth()
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=0.01); bpy.ops.object.mode_set(mode='OBJECT')
    mesh = body.data
    co = np.array([v.co[:] for v in mesh.vertices])
    mesh.transform(mathutils.Matrix.Translation((0, 0, -co[:, 2].min())))
    co = np.array([v.co[:] for v in mesh.vertices])
    col = _coat(name, co)
    attr = mesh.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    attr.data.foreach_set("color", np.concatenate([col, np.ones((len(col), 1))], 1).ravel())
    mat = bpy.data.materials.new(name + "Coat"); mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission"); at = nt.nodes.new("ShaderNodeAttribute"); at.attribute_name = "Col"
    nt.links.new(at.outputs["Color"], em.inputs["Color"]); nt.links.new(em.outputs[0], out.inputs["Surface"])
    img = bpy.data.images.new(name + "_Color", 1024, 1024)
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; nt.nodes.active = tex
    mesh.materials.append(mat)
    sc = win.scene; engine = sc.render.engine; sc.render.engine = 'CYCLES'; sc.cycles.samples = 1; sc.render.bake.margin = 8
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); win.view_layer.objects.active = body
        bpy.ops.object.bake(type='EMIT')
    sc.render.engine = engine
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
    bsdf.inputs["Roughness"].default_value = 0.75
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
    img.pack()
    spec = dict(bp["spec"]); spec["name"] = name
    tris = decimate(body, spec["tris"])
    arm, bones = build_rig(body, spec)
    size = export([arm, body], name)
    return {"tris": tris, "bones": bones, "kb": size // 1024}
