"""Build the editable ContextSpeak reference player from CC0 anatomical data.

Blender 5.3: Blender --background --python tools/blender/reference_character.py
No add-on, network, MakeHuman program code, or paid content is required.
Official CC0 assets and exact checksums live in art/sources/makehuman/provenance.json.
The tailored garments, hair, backpack and original build script are project art.
"""
from pathlib import Path
import hashlib
import json
import math
import struct
import sys
try:
    import bpy
    from mathutils import Vector, Quaternion
    from mathutils.kdtree import KDTree
    from mathutils.bvhtree import BVHTree
except ModuleNotFoundError:
    bpy = None
    Vector = Quaternion = KDTree = BVHTree = None
ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'art/sources/makehuman'
OUT = ROOT / '.local/art-candidates'
VARIANT = 'ReferenceBarista' if '--barista' in sys.argv else 'ReferencePlayer'
BARISTA = VARIANT == 'ReferenceBarista'
BLEND = ROOT / f'art/blender/{VARIANT}.blend'
DATA = SRC / 'makehuman/data'
SYSTEM = SRC / 'system_assets'
MATS = {}
OBJECTS = []
RIG = None
BASE = []
BASE_WEIGHTS = []
KD = None
RENAME = {
    'head': 'Head',
    'upperarm_l': 'LeftArm',
    'upperarm_r': 'RightArm',
    'thigh_l': 'LeftLeg',
    'thigh_r': 'RightLeg',
}


def rgb(hex):
    a = [int(hex[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple((x / 12.92 if x < 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in a))


def mat(name, hex, rough=0.7, metal=0, texture=None, alpha=False, grain=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb(hex), 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    m.diffuse_color = (*rgb(hex), 1)
    if texture:
        im = bpy.data.images.load(str(texture), check_existing=True)
        if max(im.size) > 1024:
            im.scale(1024, 1024)
        im.pack()
        node = m.node_tree.nodes.new('ShaderNodeTexImage')
        node.image = im
        m.node_tree.links.new(node.outputs['Color'], p.inputs['Base Color'])
        if alpha:
            m.node_tree.links.new(node.outputs['Alpha'], p.inputs['Alpha'])
            m.surface_render_method = 'DITHERED'
            m.use_transparency_overlap = False
    if grain:
        # Render-only microscopic cloth texture: exported PBR stays stable matte.
        noise = m.node_tree.nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 180
        noise.inputs['Detail'].default_value = 2
        bump = m.node_tree.nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.18
        bump.inputs['Distance'].default_value = 0.0006
        m.node_tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
        m.node_tree.links.new(bump.outputs['Normal'], p.inputs['Normal'])
    MATS[name] = m
    return m


def read_obj(path):
    vs = []
    uv = []
    fs = []
    fuv = []
    groups = []
    g = 'body'
    for line in path.read_text().splitlines():
        a = line.split()
        if not a:
            continue
        if a[0] == 'v':
            vs.append(Vector(tuple(map(float, a[1:4]))))
        elif a[0] == 'vt':
            uv.append(tuple(map(float, a[1:3])))
        elif a[0] == 'g':
            g = a[1]
        elif a[0] == 'f':
            fields = [x.split('/') for x in a[1:]]
            fs.append([int(x[0]) - 1 for x in fields])
            fuv.append([int(x[1]) - 1 if len(x) > 1 and x[1] else 0 for x in fields])
            groups.append(g)
    return (vs, uv, fs, fuv, groups)


def mesh(name, vs, fs, material, weights=None, uvs=None, faceuv=None):
    d = bpy.data.meshes.new(name + ' topology')
    d.from_pydata(vs, [], fs)
    d.update()
    o = bpy.data.objects.new(name, d)
    bpy.context.collection.objects.link(o)
    d.materials.append(material)
    for p in d.polygons:
        p.use_smooth = True
    if uvs is not None and faceuv is not None:
        layer = d.uv_layers.new(name='UVMap')
        for p, f in zip(d.polygons, faceuv):
            for li, ui in zip(p.loop_indices, f):
                layer.data[li].uv = uvs[ui]
    if weights is not None:
        bind(o, weights)
    OBJECTS.append(o)
    return o


def bind(o, weights):
    for i, ws in enumerate(weights):
        if not ws:
            ws = {'pelvis': 1}
        total = sum(ws.values())
        for n, w in ws.items():
            n = RENAME.get(n, n)
            if w > 0:
                g = o.vertex_groups.get(n) or o.vertex_groups.new(name=n)
                g.add([i], w / total, 'REPLACE')
    mod = o.modifiers.new('Anatomical skin weights', 'ARMATURE')
    mod.object = RIG
    o.parent = RIG


def nearest(v):
    _, i, _ = KD.find(Vector(v))
    return BASE_WEIGHTS[i]


def rigid(o, bone):
    bind(o, [{bone: 1} for _ in o.data.vertices])
    return o


def calc_normals(vs, fs):
    ns = [Vector() for _ in vs]
    for f in fs:
        n = (vs[f[1]] - vs[f[0]]).cross(vs[f[2]] - vs[f[0]])
        for i in f:
            ns[i] += n
    return [n.normalized() if n.length else Vector((0, 0, 1)) for n in ns]


def tube(name, points, radii, material, segments=10, flat=1, bone=None, closed=False):
    pts = [Vector(p) for p in points]
    vs = []
    fs = []
    for j, c in enumerate(pts):
        t = (pts[(j + 1) % len(pts)] - pts[j - 1] if closed else pts[min(j + 1, len(pts) - 1)] - pts[max(j - 1, 0)]).normalized()
        u = t.cross(Vector((0, 1, 0)))
        if u.length < 0.01:
            u = t.cross(Vector((1, 0, 0)))
        u.normalize()
        v = t.cross(u).normalized()
        r = radii[j] if isinstance(radii, list) else radii
        for i in range(segments):
            a = math.tau * i / segments
            vs.append(c + r * (u * math.cos(a) + v * math.sin(a) * flat))
    count = len(pts) if closed else len(pts) - 1
    for j in range(count):
        for i in range(segments):
            fs.append((j * segments + i, j * segments + (i + 1) % segments, (j + 1) % len(pts) * segments + (i + 1) % segments, (j + 1) % len(pts) * segments + i))
    if not closed:
        fs.extend([tuple(reversed(range(segments))), tuple(((len(pts) - 1) * segments + i for i in range(segments)))])
    return mesh(name, vs, fs, material, [{bone: 1} if bone else nearest(v) for v in vs])


def bezier(points, count=20):
    # Catmull-Rom interpolation for editable swept hair / garment seams.
    p = [Vector(v) for v in points]
    res = []
    for j in range(len(p) - 1):
        a, b, c, d = (p[max(0, j - 1)], p[j], p[j + 1], p[min(len(p) - 1, j + 2)])
        for k in range(count):
            t = k / count
            res.append(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t))
    res.append(p[-1])
    return res


def seam(name, points, material, r=0.0015, bone=None):
    return tube(name, bezier(points, 5), r, material, 6, bone=bone)


def box(name, at, size, material, bevel=0.01, bone='spine_03'):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    o = bpy.context.object
    o.name = name
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        m = o.modifiers.new('Tailored rounded corners', 'BEVEL')
        m.width = bevel
        m.segments = 3
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=m.name)
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.use_smooth = True
    # Put coordinates in the shared bind space before assigning bone weights.
    for v in o.data.vertices:
        v.co += o.location
    o.location = (0, 0, 0)
    OBJECTS.append(o)
    rigid(o, bone)
    return o


def make_base():
    global BASE, BASE_WEIGHTS, KD, RIG
    vs, uv, fs, fuv, groups = read_obj(DATA / '3dobjs/base.obj')
    morphs = {
        'macrodetails/asian-female-young': 0.55,
        'macrodetails/caucasian-female-young': 0.35,
        'macrodetails/african-female-young': 0.1,
        'head/head-age-decr': 0.17,
        'head/head-round': 0.25,
        'head/head-scale-horiz-incr': 0.06,
        'chin/chin-width-decr': 0.14,
        'mouth/mouth-angles-up': 0.45,
        'nose/nose-scale-depth-decr': 0.12,
        'eyes/l-eye-scale-incr': 0.35,
        'eyes/r-eye-scale-incr': 0.35,
    }
    for path, w in morphs.items():
        for l in (DATA / 'targets' / (path + '.target')).read_text().splitlines():
            a = l.split()
            if len(a) == 4 and (not a[0].startswith('#')):
                vs[int(a[0])] += Vector(tuple(map(float, a[1:]))) * w
    ground = -8.17565
    # Metres, Blender Z-up, face toward -Y. Broader reference-like head, without toy proportions.

    def convert(v):
        v = Vector((v.x * 0.105, -v.z * 0.105, (v.y - ground) * 0.105))
        if v.z > 1.375:
            v = Vector((v.x * 1.065, v.y * 1.065, 1.375 + (v.z - 1.375) * 1.09))
        return v
    BASE = [convert(v) for v in vs]
    rawweights = json.loads((SRC / 'mpfb2/src/mpfb/data/rigs/standard/weights.game_engine.json').read_text())['weights']
    BASE_WEIGHTS = [{} for _ in vs]
    for name, vals in rawweights.items():
        for i, w in vals:
            if w > 0:
                BASE_WEIGHTS[i][name] = w
    for i, ws in enumerate(BASE_WEIGHTS):
        BASE_WEIGHTS[i] = dict(sorted(ws.items(), key=lambda kv: -kv[1])[:4])
    jointverts = {}
    for f, g in zip(fs, groups):
        jointverts.setdefault(g, set()).update(f)

    def point(spec):
        typ = spec['strategy']
        if typ == 'CUBE':
            ids = jointverts[spec['cube_name']]
            v = sum((BASE[i] for i in ids), Vector()) / len(ids)
        elif typ == 'VERTEX':
            v = BASE[spec['vertex_index']].copy()
        elif typ == 'MEAN':
            ids = spec['vertex_indices']
            v = sum((BASE[i] for i in ids), Vector()) / len(ids)
        else:
            ids = spec.get('vertex_indices')
            v = Vector([BASE[ids[k]][k] for k in range(3)]) if ids else Vector(spec['default_position']) + Vector((0, 0, 0.858))
        return v + Vector(spec.get('offset', (0, 0, 0)))
    rigdata = json.loads((SRC / 'mpfb2/src/mpfb/data/rigs/standard/rig.game_engine.json').read_text())
    arm = bpy.data.armatures.new('ReferencePlayer deform rig')
    RIG = bpy.data.objects.new('ReferencePlayerRig', arm)
    bpy.context.collection.objects.link(RIG)
    bpy.context.view_layer.objects.active = RIG
    RIG.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for n, s in rigdata.items():
        b = arm.edit_bones.new(RENAME.get(n, n))
        b.head = point(s['head'])
        b.tail = point(s['tail'])
        b.roll = s.get('roll', 0)
        if (b.tail - b.head).length < 0.0001:
            b.tail = b.head + Vector((0, 0, 0.02))
    for n, s in rigdata.items():
        if s.get('parent'):
            arm.edit_bones[RENAME.get(n, n)].parent = arm.edit_bones[RENAME.get(s['parent'], s['parent'])]
    bpy.ops.object.mode_set(mode='OBJECT')
    RIG.select_set(False)
    bodyfaces = [(f, fu) for f, fu, g in zip(fs, fuv, groups) if g == 'body']
    ids = sorted(set((i for f, _ in bodyfaces for i in f)))
    remap = {i: j for j, i in enumerate(ids)}
    body = mesh(
        'Anatomical body · CC0 MakeHuman hm08',
        [BASE[i] for i in ids],
        [[remap[i] for i in f] for f, _ in bodyfaces],
        MATS['Skin'],
        [BASE_WEIGHTS[i] for i in ids],
        uv,
        [u for _, u in bodyfaces],
    )
    KD = KDTree(len(ids))
    for i in ids:
        KD.insert(BASE[i], i)
    KD.balance()
    # Correctly fit body-part proxies to the morphed base before removing helpers.
    for category, name, material in [
        ('eyes/high-poly', 'high-poly', MATS['Eyes']),
        ('eyebrows/eyebrow001', 'eyebrow001', MATS['Eyebrows']),
        ('eyelashes/eyelashes01', 'eyelashes01', MATS['Eyelashes']),
    ]:
        p = SYSTEM / category
        pv, pu, pf, pfu, pg = read_obj(p / (name + '.obj'))
        mapping = []
        scale = [1, 1, 1]
        active = False
        for l in (p / (name + '.mhclo')).read_text().splitlines():
            a = l.split()
            if not a or a[0].startswith('#'):
                continue
            if a[0] in ['x_scale', 'y_scale', 'z_scale']:
                k = {'x_scale': 0, 'y_scale': 1, 'z_scale': 2}[a[0]]
                scale[k] = abs(vs[int(a[1])][k] - vs[int(a[2])][k]) / float(a[3])
            if a[0] == 'verts':
                active = True
                continue
            if active:
                if len(a) == 1 and a[0].isdigit():
                    mapping.append(vs[int(a[0])].copy())
                elif len(a) == 9 and a[0].isdigit():
                    v = sum((vs[int(a[k])] * float(a[k + 3]) for k in range(3)), Vector())
                    v += Vector([float(a[6 + k]) * scale[k] for k in range(3)])
                    mapping.append(v)
                else:
                    active = False
        if len(mapping) != len(pv):
            raise RuntimeError(f'Proxy vertex count mismatch {name}: {len(mapping)} vs {len(pv)}')
        mesh(
            'Fitted ' + name,
            [convert(v) for v in mapping],
            pf,
            material,
            [{'head': 1} for _ in mapping],
            pu,
            pfu,
        )
    return (body, bodyfaces, ids, vs, convert, morphs)


def lathe(name, rings, material, opening=0, steps=48, weights=True):
    # Rings: z, width radius, depth radius, centre Y. Deliberately open jacket front.
    vs = []
    fs = []
    for j, (z, rx, ry, cy) in enumerate(rings):
        for i in range(steps + 1 if opening else steps):
            angle = opening + (math.tau - 2 * opening) * i / steps if opening else math.tau * i / steps
            # Small tailored wrinkles in relaxed cotton; no random noisy silhouette.
            wrinkle = 0.0015 * math.sin(6 * angle + j * 1.9) * math.sin(j * 1.7)
            vs.append((math.sin(angle) * (rx + wrinkle), cy - math.cos(angle) * (ry + wrinkle), z))
    stride = steps + 1 if opening else steps
    for j in range(len(rings) - 1):
        for i in range(steps):
            fs.append((j * stride + i, j * stride + (i + 1) % stride, (j + 1) * stride + (i + 1) % stride, (j + 1) * stride + i))
    return mesh(name, vs, fs, material, [{'spine_03': 1} for _ in vs])


def garments(body, bodyfaces, ids):
    # Denim uses actual continuous anatomical pelvis/crotch/leg topology.
    skinfs = [list(p.vertices) for p in body.data.polygons]
    ns = calc_normals([v.co for v in body.data.vertices], skinfs)
    selected = []
    for p in body.data.polygons:
        c = p.center
        if 0.175 < c.z < 0.935 and abs(c.x) < 0.245:
            selected.append(list(p.vertices))
    used = sorted(set((i for f in selected for i in f)))
    rem = {i: j for j, i in enumerate(used)}
    vv = []
    for i in used:
        p = body.data.vertices[i].co.copy()
        p += ns[i] * 0.012
        # Relaxed straight legs, fabric not skin tight. Keep natural connected crotch.
        if p.z < 0.74:
            side = 1 if p.x > 0 else -1
            cx = side * (0.105 + (0.74 - p.z) * 0.1)
            p.x = cx + (p.x - cx) * 1.12
            p.y = 0.01 + (p.y - 0.01) * 1.1
            fold = math.sin(p.z * 95 + side) * 0.003 * (0.4 + 0.6 * math.exp(-((p.z - 0.47) / 0.12) ** 2))
            p.x += side * fold
        vv.append(p)
    jeans = mesh(
        'Relaxed denim · continuous crotch topology',
        vv,
        [[rem[i] for i in f] for f in selected],
        MATS['Denim'],
        [BASE_WEIGHTS[ids[i]] for i in used],
    )
    # Evenly spaced tee rings encode stripes as real exported material regions.
    rings = []
    for i in range(41):
        z = 0.91 + i * 0.00975
        t = (z - 0.91) / 0.39
        rx = 0.16 + 0.04 * t
        ry = 0.135 + 0.01 * math.sin(t * math.pi)
        rings.append((z, rx, ry, -0.015))
    rings.extend([(1.32, 0.174, 0.126, -0.014), (1.345, 0.135, 0.091, -0.008), (1.365, 0.067, 0.063, -0.003)])
    tee = lathe('Breton stripe cotton tee', rings, MATS['Cotton'], steps=32)
    tee.data.materials.append(MATS['Stripe'])
    for p in tee.data.polygons:
        if p.center.z < 1.305 and int((p.center.z - 0.91) / 0.019) % 2 == 0:
            p.material_index = 1
    hoodie = lathe(
        'Cream zip hoodie · open tailored torso',
        [
            (0.885, 0.174, 0.17, 0.006),
            (0.9, 0.178, 0.175, 0.006),
            (0.96, 0.188, 0.18, 0.003),
            (1.04, 0.187, 0.185, 0.002),
            (1.12, 0.198, 0.189, 0.006),
            (1.2, 0.211, 0.187, 0.006),
            (1.27, 0.215, 0.176, 0.008),
            (1.32, 0.208, 0.147, 0.012),
            (1.35, 0.2, 0.125, 0.017),
            (1.365, 0.115, 0.084, 0.018),
        ],
        MATS['Hoodie'],
        opening=0.42,
        steps=52,
    )
    solid = hoodie.modifiers.new('Cotton edge thickness', 'SOLIDIFY')
    solid.thickness = 0.004
    # Rib knit waistband, visibly interrupted by zip.
    lathe(
        'Hoodie rib knit hem',
        [(0.878, 0.177, 0.175, 0.006), (0.89, 0.18, 0.177, 0.006), (0.915, 0.181, 0.177, 0.006)],
        MATS['Rib'],
        opening=0.43,
        steps=52,
    )
    for s in (-1, 1):
        shoulder = RIG.data.bones['LeftArm' if s > 0 else 'RightArm'].head_local
        elbow = RIG.data.bones['lowerarm_l' if s > 0 else 'lowerarm_r'].head_local
        wrist = RIG.data.bones['hand_l' if s > 0 else 'hand_r'].head_local
        end = elbow.lerp(wrist, 0.76)
        pts = [
            shoulder + Vector((-s * 0.015, 0, 0.018)),
            shoulder.lerp(elbow, 0.25),
            shoulder.lerp(elbow, 0.65),
            elbow,
            elbow.lerp(end, 0.4),
            elbow.lerp(end, 0.8),
            end,
        ]
        tube(
            'Soft hoodie sleeve ' + str(s),
            pts,
            [0.104, 0.098, 0.092, 0.082, 0.082, 0.073, 0.064],
            MATS['Hoodie'],
            24,
            flat=1.03,
        )
        axis = (end - elbow).normalized()
        tube(
            'Rib knit cuff ' + str(s),
            [end - axis * 0.022, end + axis * 0.015],
            [0.064, 0.061],
            MATS['Rib'],
            24,
        )
        # Zipper tapes and hood drawstrings frame the striped tee.
        seam(
            'Brass zipper tape ' + str(s),
            [
                (s * 0.074, -0.154, 0.89),
                (s * 0.079, -0.168, 1.07),
                (s * 0.082, -0.165, 1.23),
                (s * 0.049, -0.066, 1.35),
            ],
            MATS['Zip'],
            0.002,
        )
        seam(
            'Hoodie draw cord ' + str(s),
            [(s * 0.066, -0.075, 1.345), (s * 0.096, -0.133, 1.28), (s * 0.091, -0.15, 1.18)],
            MATS['Cotton'],
            0.0024,
        )
        seam(
            'Kangaroo pocket seam ' + str(s),
            [(s * 0.105, -0.139, 0.93), (s * 0.155, -0.13, 0.98), (s * 0.169, -0.119, 1.045)],
            MATS['HoodSeam'],
            0.0016,
        )
        # Rolled denim cuff, plus subtle double side seams.
        cx = s * 0.167
        cuff = lathe(
            'Rolled jean cuff ' + str(s),
            [
                (0.145, 0.07, 0.073, -0.02),
                (0.155, 0.073, 0.076, -0.02),
                (0.205, 0.073, 0.076, -0.02),
                (0.225, 0.068, 0.07, -0.02),
            ],
            MATS['DenimCuff'],
            steps=32,
        )
        # lathe is centred: translate before skinning adjustment.
        for v in cuff.data.vertices:
            v.co.x += cx
        # Replace incorrect centre sampling with explicit calf/foot weights.
        for g in list(cuff.vertex_groups):
            cuff.vertex_groups.remove(g)
        g = cuff.vertex_groups.new(name='calf_l' if s > 0 else 'calf_r')
        g.add(list(range(len(cuff.data.vertices))), 1, 'REPLACE')
        seam(
            'Denim outer seam ' + str(s),
            [
                (s * 0.232, 0.005, 0.91),
                (s * 0.22, 0.018, 0.7),
                (s * 0.222, 0.011, 0.44),
                (s * 0.224, 0.008, 0.2),
            ],
            MATS['Stitch'],
            0.0014,
        )
        seam(
            'Back pocket stitching ' + str(s),
            [
                (s * 0.065, 0.118, 0.84),
                (s * 0.15, 0.13, 0.84),
                (s * 0.15, 0.135, 0.75),
                (s * 0.11, 0.142, 0.72),
                (s * 0.063, 0.123, 0.75),
                (s * 0.065, 0.118, 0.84),
            ],
            MATS['Stitch'],
            0.0012,
        )
    # Hood made as a curved open padded band, resting behind the neck.
    hoodpts = []
    for i in range(45):
        a = -2.0 + i * 4.0 / 44
        hoodpts.append((0.119 * math.sin(a), 0.023 + 0.091 * math.cos(a), 1.375 + 0.035 * math.cos(a)))
    tube('Draped hood padded rim', hoodpts, 0.03, MATS['Hoodie'], 14, flat=0.65, bone='spine_03')
    # Leather waist belt peeks between the open hoodie panels.
    lathe(
        'Warm leather belt',
        [(0.92, 0.163, 0.119, 0.006), (0.945, 0.162, 0.119, 0.006)],
        MATS['Leather'],
        steps=48,
    )
    buckle = box(
        'Antique brass belt buckle',
        (0, -0.118, 0.935),
        (0.038, 0.009, 0.032),
        MATS['Brass'],
        0.004,
        bone='pelvis',
    )
    box('Buckle inset', (0, -0.124, 0.935), (0.025, 0.003, 0.02), MATS['Leather'], 0.002, bone='pelvis')


def barista_garments(body, bodyfaces, ids):
    start = len(OBJECTS)
    garments(body, bodyfaces, ids)
    # Keep the anatomical denim trousers; replace the player layers with a uniform.
    for o in list(OBJECTS[start:]):
        if not any((word in o.name.lower() for word in ['denim', 'jean', 'belt', 'buckle'])):
            OBJECTS.remove(o)
            bpy.data.objects.remove(o, do_unlink=True)
    rings = [
        (0.89, 0.17, 0.135, 0.005),
        (0.96, 0.18, 0.147, 0.005),
        (1.06, 0.185, 0.15, 0.005),
        (1.16, 0.2, 0.155, 0.005),
        (1.27, 0.206, 0.144, 0.008),
        (1.33, 0.212, 0.13, 0.01),
        (1.365, 0.2, 0.11, 0.01),
        (1.385, 0.068, 0.065, 0),
    ]
    lathe('Barista cream cotton shirt', rings, MATS['Cotton'], steps=48)
    for side in (-1, 1):
        shoulder = RIG.data.bones['LeftArm' if side > 0 else 'RightArm'].head_local
        elbow = RIG.data.bones['lowerarm_l' if side > 0 else 'lowerarm_r'].head_local
        wrist = RIG.data.bones['hand_l' if side > 0 else 'hand_r'].head_local
        end = elbow.lerp(wrist, 0.48)
        pts = [
            shoulder + Vector((-side * 0.025, 0, 0.012)),
            shoulder.lerp(elbow, 0.5),
            elbow,
            elbow.lerp(end, 0.6),
            end,
        ]
        tube(
            'Barista rolled shirt sleeve ' + str(side),
            pts,
            [0.089, 0.079, 0.07, 0.064, 0.057],
            MATS['Cotton'],
            22,
        )
        axis = (end - elbow).normalized()
        tube(
            'Barista folded shirt cuff ' + str(side),
            [end - axis * 0.018, end + axis * 0.012],
            0.06,
            MATS['Rib'],
            22,
        )
    # Curved bib apron with full front coverage and a separate sewn pocket.
    rows = [
        (0.67, 0.211, -0.192),
        (0.75, 0.217, -0.197),
        (0.87, 0.202, -0.197),
        (0.99, 0.184, -0.188),
        (1.1, 0.133, -0.183),
        (1.24, 0.105, -0.179),
        (1.285, 0.1, -0.166),
    ]
    vv = []
    ff = []
    steps = 28
    for z, w, front in rows:
        for i in range(steps + 1):
            u = -1 + 2 * i / steps
            vv.append((u * w, front + 0.047 * u * u, z))
    for j in range(len(rows) - 1):
        for i in range(steps):
            ff.append((j * (steps + 1) + i, j * (steps + 1) + i + 1, (j + 1) * (steps + 1) + i + 1, (j + 1) * (steps + 1) + i))
    apron = mesh(
        'Barista chestnut bib apron',
        vv,
        ff,
        MATS['Bag'],
        [{'spine_01': 1} if v[2] > 0.95 else {'pelvis': 1} for v in vv],
    )
    th = apron.modifiers.new('Canvas apron thickness', 'SOLIDIFY')
    th.thickness = 0.003
    for side in (-1, 1):
        seam(
            'Apron sewn edge ' + str(side),
            [(side * w, front + 0.045, z) for z, w, front in rows],
            MATS['Stitch'],
            0.0013,
        )
        tube(
            'Apron neck strap ' + str(side),
            bezier(
                [
                    (side * 0.088, -0.18, 1.27),
                    (side * 0.094, -0.14, 1.36),
                    (side * 0.079, 0.05, 1.405),
                    (side * 0.058, 0.091, 1.32),
                ],
                7,
            ),
            0.009,
            MATS['Leather'],
            8,
            flat=0.3,
            bone='spine_03',
        )
        box(
            'Apron brass strap rivet ' + str(side),
            (side * 0.088, -0.174, 1.263),
            (0.01, 0.004, 0.01),
            MATS['Brass'],
            0.003,
        )
    lathe(
        'Apron waist tie',
        [(0.99, 0.192, 0.165, 0.006), (1.012, 0.192, 0.165, 0.006)],
        MATS['Leather'],
        steps=48,
    )
    box(
        'Apron sewn front pocket',
        (0, -0.215, 0.928),
        (0.172, 0.01, 0.124),
        MATS['BagFlap'],
        0.009,
        bone='spine_01',
    )
    seam(
        'Apron pocket stitching',
        [
            (-0.077, -0.223, 0.984),
            (-0.077, -0.223, 0.882),
            (0.077, -0.223, 0.882),
            (0.077, -0.223, 0.984),
        ],
        MATS['Stitch'],
        0.0012,
        'spine_01',
    )
    box('Barista brass name badge', (0.043, -0.192, 1.205), (0.055, 0.005, 0.018), MATS['Brass'], 0.003)


def shoes():
    for s in (-1, 1):
        cx = s * 0.167
        bone = 'foot_l' if s > 0 else 'foot_r'
        # Rounded canvas last: connected cross-section mesh, rubber toe and sole.
        vs = []
        fs = []
        sections = [
            (-0.218, 0.025, 0.052),
            (-0.205, 0.051, 0.071),
            (-0.17, 0.062, 0.086),
            (-0.1, 0.063, 0.105),
            (-0.035, 0.059, 0.135),
            (0.018, 0.052, 0.126),
            (0.045, 0.044, 0.083),
        ]
        for y, r, h in sections:
            for i in range(20):
                a = math.tau * i / 20
                vs.append((cx + r * math.cos(a), y, 0.044 + (h - 0.044) * max(0, math.sin(a)) + 0.012 * min(0, math.sin(a))))
        for j in range(len(sections) - 1):
            for i in range(20):
                fs.append((j * 20 + i, j * 20 + (i + 1) % 20, (j + 1) * 20 + (i + 1) % 20, (j + 1) * 20 + i))
        fs += [tuple(reversed(range(20))), tuple(((len(sections) - 1) * 20 + i for i in range(20)))]
        mesh('Canvas low top shoe ' + str(s), vs, fs, MATS['Canvas'], [{bone: 1} for _ in vs])
        box(
            'Rubber sole ' + str(s),
            (cx, -0.083, 0.032),
            (0.133, 0.284, 0.036),
            MATS['Rubber'],
            0.018,
            bone,
        )
        box(
            'Rubber toe cap ' + str(s),
            (cx, -0.185, 0.066),
            (0.119, 0.081, 0.045),
            MATS['Rubber'],
            0.019,
            bone,
        )
        seam(
            'Rubber foxing stripe ' + str(s),
            [
                (cx - 0.064, -0.18, 0.035),
                (cx - 0.064, 0.025, 0.035),
                (cx, 0.055, 0.035),
                (cx + 0.064, 0.025, 0.035),
                (cx + 0.064, -0.18, 0.035),
            ],
            MATS['Canvas'],
            0.0014,
            bone,
        )
        for j in range(5):
            y = -0.135 + j * 0.024
            z = 0.093 + j * 0.006
            seam(
                'Cotton shoe lace ' + str(s) + ' ' + str(j),
                [(cx - 0.035, y, z), (cx, y + 0.009, z + 0.004), (cx + 0.035, y, z)],
                MATS['Cotton'],
                0.0022,
                bone,
            )
        seam(
            'Canvas heel seam ' + str(s),
            [(cx, 0.05, 0.05), (cx, 0.046, 0.09), (cx, 0.027, 0.122)],
            MATS['Stitch'],
            0.001,
            bone,
        )


def backpack():
    box('Chestnut canvas backpack', (0, 0.208, 1.116), (0.282, 0.164, 0.354), MATS['Bag'], 0.045)
    box('Leather backpack base', (0, 0.223, 0.956), (0.278, 0.164, 0.046), MATS['Leather'], 0.015)
    box(
        'Rounded foldover backpack flap',
        (0, 0.294, 1.241),
        (0.274, 0.035, 0.146),
        MATS['BagFlap'],
        0.025,
    )
    box('Backpack front pocket', (0, 0.302, 1.052), (0.22, 0.055, 0.115), MATS['Bag'], 0.02)
    for s in (-1, 1):
        points = [
            (s * 0.127, -0.182, 1.06),
            (s * 0.157, -0.164, 1.27),
            (s * 0.157, -0.062, 1.365),
            (s * 0.125, 0.103, 1.34),
            (s * 0.102, 0.2, 1.26),
        ]
        tube(
            'Padded leather shoulder strap ' + str(s),
            bezier(points, 7),
            0.017,
            MATS['Leather'],
            8,
            flat=0.27,
            bone='spine_03',
        )
        # Visible seam gives the straps a sewn edge rather than plain bars.
        seam(
            'Shoulder strap seam ' + str(s),
            [(s * 0.138, -0.188, 1.09), (s * 0.163, -0.17, 1.27), (s * 0.163, -0.067, 1.36)],
            MATS['Stitch'],
            0.0009,
            'spine_03',
        )
        box(
            'Brass strap adjuster ' + str(s),
            (s * 0.143, -0.184, 1.17),
            (0.029, 0.01, 0.039),
            MATS['Brass'],
            0.004,
        )
        box(
            'Leather flap fastener ' + str(s),
            (s * 0.076, 0.325, 1.153),
            (0.024, 0.012, 0.12),
            MATS['Leather'],
            0.007,
        )
        box(
            'Brass bag buckle ' + str(s),
            (s * 0.076, 0.337, 1.12),
            (0.034, 0.008, 0.032),
            MATS['Brass'],
            0.004,
        )
        seam(
            'Backpack outline stitching ' + str(s),
            [
                (s * 0.111, 0.319, 0.98),
                (s * 0.124, 0.314, 1.12),
                (s * 0.12, 0.314, 1.27),
                (s * 0.03, 0.317, 1.3),
            ],
            MATS['Stitch'],
            0.0012,
            'spine_03',
        )
    tube(
        'Backpack carry loop',
        bezier([(-0.045, 0.22, 1.3), (-0.04, 0.21, 1.36), (0.04, 0.21, 1.36), (0.045, 0.22, 1.3)], 7),
        0.008,
        MATS['Leather'],
        8,
        flat=0.5,
        bone='spine_03',
    )
    # diamond emblem
    badge = box(
        'Diamond leather backpack badge',
        (0, 0.316, 1.247),
        (0.029, 0.006, 0.029),
        MATS['Tan'],
        0.003,
    )
    # Geometry already in bind space; rotate individual badge verts about its centre.
    for v in badge.data.vertices:
        c = Vector((0, 0.316, 1.247))
        v.co = c + Quaternion((0, 1, 0), math.pi / 4) @ (v.co - c)


def hair_patch(name, control_points, widths, material, surface):
    """A broad curved lock follows the head, with a softly domed cross section."""
    points = bezier(control_points, 12)
    centre = Vector((0, -0.005, 1.545))
    vertices = []
    faces = []
    cross_steps = 8
    for index, point in enumerate(points):
        tangent = (points[min(index + 1, len(points) - 1)] - points[max(index - 1, 0)]).normalized()
        normal = (point - centre).normalized()
        across = normal.cross(tangent).normalized()
        progress = index / (len(points) - 1) * (len(widths) - 1)
        lower = min(int(progress), len(widths) - 2)
        fraction = progress - lower
        width = widths[lower] * (1 - fraction) + widths[lower + 1] * fraction
        for step in range(cross_steps + 1):
            u = -1 + 2 * step / cross_steps
            proposed = point + across * width * u
            direction = (proposed - centre).normalized()
            hit, skin_normal, _, _ = surface.ray_cast(centre, direction, 1)
            depth = 0.012 + 0.004 * (1 - u * u)
            vertices.append(hit + skin_normal * depth if hit is not None else proposed)
    stride = cross_steps + 1
    for row in range(len(points) - 1):
        for column in range(cross_steps):
            start = row * stride + column
            faces.append((start, start + 1, start + stride + 1, start + stride))
    return mesh(name, vertices, faces, material, [{'head': 1} for _ in vertices])


def hair(body):
    """Layer an asymmetric side part over a fitted scalp, then gather a bun."""
    skin_faces = [list(polygon.vertices) for polygon in body.data.polygons]
    surface = BVHTree.FromPolygons(
        [vertex.co for vertex in body.data.vertices],
        skin_faces,
        all_triangles=False,
    )
    centre = Vector((0, -0.005, 1.545))

    def scalp(theta, phi):
        direction = Vector((math.sin(theta) * math.sin(phi), -math.sin(theta) * math.cos(phi), math.cos(theta)))
        hit, normal, _, _ = surface.ray_cast(centre, direction, 1)
        return hit + normal * 0.01 if hit is not None else centre + direction * 0.11
    vertices = []
    faces = []
    steps = 64
    rows = 15
    for row in range(rows):
        for column in range(steps):
            phi = math.tau * column / steps
            # The asymmetric boundary is concealed by the overlapping fringe.
            edge = 1.3 + 0.72 * (1 - math.cos(phi)) * 0.5 + 0.2 * math.sin(phi)
            theta = 0.015 + (edge - 0.015) * row / (rows - 1)
            vertices.append(scalp(theta, phi))
    for row in range(rows - 1):
        for column in range(steps):
            faces.append((row * steps + column, row * steps + (column + 1) % steps, (row + 1) * steps + (column + 1) % steps, (row + 1) * steps + column))
    mesh(
        'Continuous fitted chestnut scalp',
        vertices,
        faces,
        MATS['Hair'],
        [{'head': 1} for _ in vertices],
    )
    hair_patch(
        'Layered long side fringe',
        [(-0.034, -0.082, 1.651), (0.009, -0.113, 1.634), (0.056, -0.119, 1.603), (0.09, -0.093, 1.556)],
        [0.017, 0.033, 0.029, 0.003],
        MATS['Hair'],
        surface,
    )
    hair_patch(
        'Overlapping fringe highlight',
        [
            (-0.032, -0.087, 1.654),
            (0.018, -0.118, 1.636),
            (0.064, -0.116, 1.599),
            (0.094, -0.084, 1.558),
        ],
        [0.01, 0.017, 0.013, 0.002],
        MATS['HairLight'],
        surface,
    )
    hair_patch(
        'Layered short side fringe',
        [
            (-0.032, -0.082, 1.65),
            (-0.067, -0.106, 1.626),
            (-0.095, -0.092, 1.581),
            (-0.103, -0.057, 1.537),
        ],
        [0.014, 0.027, 0.019, 0.002],
        MATS['Hair'],
        surface,
    )
    hair_patch(
        'Left temple sweep',
        [
            (-0.055, -0.054, 1.655),
            (-0.099, -0.056, 1.62),
            (-0.112, -0.021, 1.573),
            (-0.102, 0.01, 1.541),
        ],
        [0.014, 0.023, 0.02, 0.003],
        MATS['Hair'],
        surface,
    )
    hair_patch(
        'Right temple sweep',
        [(0.042, -0.043, 1.657), (0.098, -0.041, 1.624), (0.114, -0.004, 1.581), (0.104, 0.027, 1.543)],
        [0.014, 0.023, 0.02, 0.003],
        MATS['Hair'],
        surface,
    )
    # Fine grooves lie on the scalp surface instead of hanging above the head.
    for index in range(18):
        phi = -math.pi + index * math.tau / 18
        if math.cos(phi) > 0.15:
            continue
        edge = 1.25 + 0.65 * (1 - math.cos(phi)) * 0.5
        points = [scalp(0.12 + (edge - 0.12) * (1 - step / 18), phi) for step in range(19)]
        tube('Fine swept hair groove %02d' % index, points, 0.0009, MATS['HairLight'], 5, bone='head')
    bun = []
    for index in range(144):
        angle = index / 144 * math.tau
        radius = 0.043 + 0.011 * math.cos(3 * angle)
        bun.append((radius * math.cos(2 * angle), 0.056 + radius * math.sin(2 * angle) * 0.52, 1.663 + 0.012 * math.sin(3 * angle)))
    tube('Compact folded bun', bun, 0.017, MATS['Hair'], 10, flat=0.85, bone='head', closed=True)
    tie = [(0.033 * math.cos(index * math.tau / 48), 0.057 + 0.024 * math.sin(index * math.tau / 48), 1.646) for index in range(48)]
    tube('Ochre bun tie', tie, 0.003, MATS['Tan'], 7, bone='head', closed=True)
    for side in (-1, 1):
        curl = bezier(
            [
                (side * 0.102, -0.027, 1.572),
                (side * 0.11, -0.037, 1.536),
                (side * 0.106, -0.054, 1.504),
                (side * 0.114, -0.047, 1.486),
            ],
            10,
        )
        radii = [0.003 * (1 - index / (len(curl) + 1)) + 0.0004 for index in range(len(curl))]
        tube('Fine face framing curl ' + str(side), curl, radii, MATS['Hair'], 6, flat=0.6, bone='head')


def accessories():
    chain = [(0.082 * math.cos(i * math.tau / 64), -0.014 + 0.069 * math.sin(i * math.tau / 64), 1.373 - 0.043 * max(0, -math.sin(i * math.tau / 64))) for i in range(64)]
    tube('Fine pendant necklace', chain, 0.0011, MATS['Brass'], 5, bone='spine_03', closed=True)
    box('Small brass pendant', (0, -0.086, 1.329), (0.009, 0.003, 0.015), MATS['Brass'], 0.003)
    # Wristwatch: strap around wrist with face angled to catch light.
    wrist = RIG.data.bones['hand_l'].head_local
    elbow = RIG.data.bones['lowerarm_l'].head_local
    pos = wrist.lerp(elbow, 0.1)
    axis = (wrist - elbow).normalized()
    u = axis.cross(Vector((0, 1, 0))).normalized()
    v = axis.cross(u)
    pts = [pos + 0.036 * (u * math.cos(i * math.tau / 40) + v * math.sin(i * math.tau / 40)) for i in range(40)]
    tube('Leather wristwatch band', pts, 0.006, MATS['Leather'], 6, bone='hand_l', closed=True)
    box(
        'Watch face',
        pos + Vector((0, -0.035, 0)),
        (0.028, 0.012, 0.028),
        MATS['Watch'],
        0.006,
        bone='hand_l',
    )


def pose():
    # Relaxed posture; these local transforms are exported with inverse bind matrices.
    for name, angle in [('LeftArm', math.radians(25)), ('RightArm', math.radians(-25))]:
        b = RIG.pose.bones[name]
        q = b.bone.matrix_local.to_quaternion()
        b.rotation_mode = 'QUATERNION'
        b.rotation_quaternion = q.inverted() @ Quaternion((0, 1, 0), angle) @ q
    bpy.context.view_layer.update()


def studio():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.world.color = (0.2, 0.2, 0.2)
    scene.view_settings.view_transform = 'AgX'
    # Render objects are excluded from the selected GLB export.
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0.005))
    floor = bpy.context.object
    floor.name = 'Studio floor · render only'
    floor.data.materials.append(MATS['Background'])
    for name, loc, power, size, color in [
        ('Softbox key', (-3, -4, 5), 450, 4, (1, 0.86, 0.72)),
        ('Softbox fill', (3, -2, 3), 260, 3, (0.78, 0.86, 1)),
        ('Warm rim', (1, 3, 4), 520, 3, (1, 0.91, 0.79)),
    ]:
        d = bpy.data.lights.new(name, 'AREA')
        o = bpy.data.objects.new(name, d)
        bpy.context.collection.objects.link(o)
        o.location = loc
        d.energy = power
        d.shape = 'DISK'
        d.size = size
        d.color = color
        o.rotation_euler = (Vector((0, 0, 1)) - o.location).to_track_quat('-Z', 'Y').to_euler()
    d = bpy.data.cameras.new('Reference review camera')
    cam = bpy.data.objects.new('Reference review camera', d)
    bpy.context.collection.objects.link(cam)
    cam.location = (2.6, -5.4, 2.5)
    target = Vector((0, 0, 0.9))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    d.type = 'ORTHO'
    d.ortho_scale = 2.03
    scene.camera = cam
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(OUT / (VARIANT + '.png'))


def validate_glb(path):
    data = path.read_bytes()
    magic, version, length = struct.unpack_from('<4sII', data)
    assert magic == b'glTF' and version == 2 and (length == len(data))
    jsonlength, kind = struct.unpack_from('<II', data, 12)
    assert kind == 1313821514
    g = json.loads(data[20:20 + jsonlength])
    boff = 20 + jsonlength
    blen, bkind = struct.unpack_from('<II', data, boff)
    assert bkind == 5130562
    payload = data[boff + 8:boff + 8 + blen]
    for view in g['bufferViews']:
        assert view.get('byteOffset', 0) + view['byteLength'] <= len(payload)
    types = {5126: ('f', 4), 5123: ('H', 2), 5125: ('I', 4), 5121: ('B', 1)}
    width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

    def values(index):
        a = g['accessors'][index]
        view = g['bufferViews'][a['bufferView']]
        fmt, size = types[a['componentType']]
        n = width[a['type']]
        stride = view.get('byteStride', n * size)
        off = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from('<' + fmt * n, payload, off + i * stride) for i in range(a['count'])]
    triangles = 0
    vertices = 0
    for m in g['meshes']:
        for prim in m['primitives']:
            assert prim.get('mode', 4) == 4
            attrs = prim['attributes']
            positions = values(attrs['POSITION'])
            vertices += len(positions)
            assert all((math.isfinite(x) for row in positions for x in row))
            indices = values(prim['indices'])
            assert len(indices) % 3 == 0
            assert max((row[0] for row in indices)) < len(positions)
            triangles += len(indices) // 3
            assert 'JOINTS_0' in attrs and 'WEIGHTS_0' in attrs
            assert all((0 <= x < 53 for row in values(attrs['JOINTS_0']) for x in row))
            assert all((abs(sum(row) - 1) < 0.0001 for row in values(attrs['WEIGHTS_0'])))
    assert triangles <= 60000
    assert len(g['skins']) == 1 and len(g['skins'][0]['joints']) == 53
    assert len(values(g['skins'][0]['inverseBindMatrices'])) == 53
    names = [n.get('name', '') for n in g['nodes']]
    assert all((names.count(name) == 1 for name in ['Head', 'LeftArm', 'RightArm', 'LeftLeg', 'RightLeg']))
    assert not any(('SOURCE full' in name or name.startswith('helper-') or name.startswith('joint-') for name in names))
    images = []
    for im in g.get('images', []):
        view = g['bufferViews'][im['bufferView']]
        blob = payload[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
        assert blob.startswith(b'\x89PNG')
        w, h = struct.unpack_from('>II', blob, 16)
        assert max(w, h) <= 1024
        images.append({'name': im.get('name', ''), 'width': w, 'height': h, 'bytes': len(blob)})
    return {
        'export_triangles': triangles,
        'export_vertices_with_material_splits': vertices,
        'export_meshes': len(g['meshes']),
        'export_primitives': sum((len(m['primitives']) for m in g['meshes'])),
        'export_materials': len(g['materials']),
        'export_skins': len(g['skins']),
        'glb_bytes': len(data),
        'textures': images,
        'validation': 'GLB header, buffers, finite positions, indices, normalized weights, 53 joints/binds, required names, hidden geometry exclusion, texture dimensions verified',
    }


def build():
    OUT.mkdir(parents=True, exist_ok=True)
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.preferences.filepaths.file_preview_type = 'NONE'
    provenance = json.loads((SRC / 'provenance.json').read_text())
    for f in provenance['files']:
        if hashlib.sha256((SRC / f['path']).read_bytes()).hexdigest() != f['sha256']:
            raise RuntimeError('CC0 source checksum mismatch: ' + f['path'])
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    mat(
        'Skin',
        'E6B398',
        0.57,
        texture=SYSTEM / 'skins/young_asian_female/young_lightskinned_female_diffuse3.png',
    )
    p = MATS['Skin'].node_tree.nodes.get('Principled BSDF')
    p.inputs['Subsurface Weight'].default_value = 0.075
    mat('Eyes', 'FFFFFF', 0.3, texture=SYSTEM / 'eyes/materials/brown_eye.png', alpha=True)
    mat('Eyebrows', '5D3E2A', 0.75, texture=SYSTEM / 'eyebrows/eyebrow001/eyebrow001.png', alpha=True)
    mat(
        'Eyelashes',
        '31221B',
        0.65,
        texture=SYSTEM / 'eyelashes/eyelashes01/eyelashes01.png',
        alpha=True,
    )
    for name, color, rough, metal, grain in [
        ('Hoodie', 'E9D9C1', 0.88, 0, True),
        ('HoodSeam', 'C6B399', 0.9, 0, False),
        ('Rib', 'D4C1A4', 0.9, 0, True),
        ('Cotton', 'EEE7D8', 0.9, 0, True),
        ('Stripe', '33444A', 0.84, 0, False),
        ('Denim', '4C647D', 0.89, 0, True),
        ('DenimCuff', '7D91A7', 0.92, 0, True),
        ('Stitch', 'BA9B65', 0.9, 0, False),
        ('Leather', '61422C', 0.64, 0, True),
        ('Bag', '966D48', 0.86, 0, True),
        ('BagFlap', 'A07A53', 0.82, 0, True),
        ('Tan', 'C89A5B', 0.73, 0, False),
        ('Brass', 'BD9655', 0.35, 0.68, False),
        ('Zip', 'BFA77D', 0.45, 0.55, False),
        ('Canvas', '535854', 0.92, 0, True),
        ('Rubber', 'DDD3BE', 0.8, 0, False),
        ('Hair', '442C1C', 0.65, 0, False),
        ('HairLight', '6B452D', 0.63, 0, False),
        ('Watch', '253A39', 0.28, 0.2, False),
        ('Background', 'EEE9DF', 0.88, 0, False),
    ]:
        mat(name, color, rough, metal, grain=grain)
    body, bf, ids, vs, convert, morphs = make_base()
    if BARISTA:
        barista_garments(body, bf, ids)
    else:
        garments(body, bf, ids)
    shoes()
    if not BARISTA:
        backpack()
    hair(body)
    accessories()
    # Preserve the editable complete anatomy in Blender. Export only visible skin,
    # so clothing remains fully opaque and does not clip against the torso.
    visible = body.copy()
    visible.data = body.data.copy()
    bpy.context.collection.objects.link(visible)
    visible.name = 'Visible anatomical skin · head neck arms ankles'
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(visible.data)
    to_remove = []
    for f in bm.faces:
        c = f.calc_center_median()
        if 0.19 < c.z < 1.315 and abs(c.x) < 0.27 or c.z < 0.095:
            to_remove.append(f)
    bmesh.ops.delete(bm, geom=to_remove, context='FACES')
    bm.to_mesh(visible.data)
    bm.free()
    body.name = 'SOURCE full anatomical body · hidden editable'
    body.hide_render = True
    body.hide_set(True)
    OBJECTS.remove(body)
    OBJECTS.append(visible)
    pose()
    root = bpy.data.objects.new(VARIANT, None)
    bpy.context.collection.objects.link(root)
    RIG.parent = root
    root['source'] = 'MakeHuman hm08 CC0 + original ContextSpeak clothing/hair'
    root['source_revision'] = provenance['makehuman_revision']
    root['morph_recipe'] = json.dumps(morphs, sort_keys=True)
    root['source_asset_license'] = 'CC0-1.0'
    root['provenance_manifest'] = 'art/sources/makehuman/provenance.json'
    # Artist note remains inspectable in the editable blend.
    text = bpy.data.texts.new('ABOUT · provenance and limits')
    text.write('Anatomical MakeHuman hm08 base, female young blended morphs, fitted official eyes/eyebrow/eyelash data: CC0. See art/sources/makehuman/provenance.json. No MakeHuman AGPL or MPFB GPL program code is included in this builder. Original tailored clothes/hair/backpack. Full deform skeleton with fingers; relaxed arm pose. No facial animation blendshapes yet. Procedural microscopic cloth bump is render-only; GLB carries matte PBR base colors and actual skin/eye/alpha textures.\n')
    # Join temporary copies into one skinned mesh; the editable source stays split.
    bpy.ops.object.select_all(action='DESELECT')
    copies = []
    for source in OBJECTS:
        o = source.copy()
        o.data = source.data.copy()
        bpy.context.collection.objects.link(o)
        o.hide_set(False)
        o.hide_render = False
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers):
            if mod.type != 'ARMATURE':
                # Put non-deforming thickness before skinning when baking the copy.
                bpy.ops.object.modifier_move_up(modifier=mod.name)
                bpy.ops.object.modifier_apply(modifier=mod.name)
        copies.append(o)
    for o in copies:
        o.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    exportmesh = bpy.context.object
    exportmesh.name = VARIANT + ' render mesh'
    root.select_set(True)
    RIG.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=str(OUT / (VARIANT + '.glb')),
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_animations=False,
        export_skins=True,
        export_all_influences=False,
        export_materials='EXPORT',
        export_extras=True,
    )
    bpy.data.objects.remove(exportmesh, do_unlink=True)
    export_stats = validate_glb(OUT / (VARIANT + '.glb'))
    deps = bpy.context.evaluated_depsgraph_get()
    tris = 0
    bounds = []
    for o in OBJECTS:
        ev = o.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        bounds.extend((o.matrix_world @ v.co for v in me.vertices))
        ev.to_mesh_clear()
    required = ['Head', 'LeftArm', 'RightArm', 'LeftLeg', 'RightLeg']
    assert all((n in RIG.data.bones for n in required))
    report = {
        'triangles': tris,
        'height_metres': max((v.z for v in bounds)) - min((v.z for v in bounds)),
        'objects': len(OBJECTS),
        'deform_bones': len(RIG.data.bones),
        'required_nodes': required,
        'source_checksums_verified': len(provenance['files']),
        'makehuman_revision': provenance['makehuman_revision'],
        'limits': [
            'No facial animation shape keys yet',
            'Microscopic procedural cloth bump remains in Blender; GLB contains matte PBR colors and textured skin/eyes',
            'Clothing skin weights interpolated from nearest anatomical vertex; extreme motion not validated',
        ],
    }
    report.update(export_stats)
    assert tris <= 60000, report
    (OUT / (VARIANT + '.qa.json')).write_text(json.dumps(report, indent=2) + '\n')
    studio()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.render.render(write_still=True)
    # Also provide a straight-on close view for facial review.
    cam = bpy.context.scene.camera
    cam.location = (0, -3, 1.57)
    cam.rotation_euler = (Vector((0, 0, 1.47)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = 0.58
    bpy.context.scene.render.resolution_x = 1100
    bpy.context.scene.render.resolution_y = 1100
    bpy.context.scene.render.filepath = str(OUT / (VARIANT + '-face.png'))
    bpy.ops.render.render(write_still=True)
    print('REFERENCE_CHARACTER_QA', json.dumps(report))
if __name__ == '__main__':
    if '--validate-glb' in sys.argv:
        argument = sys.argv.index('--validate-glb') + 1
        print(json.dumps(validate_glb(Path(sys.argv[argument])), indent=2))
    elif bpy is None:
        raise SystemExit('Build this character with Blender --background --python tools/blender/reference_character.py')
    else:
        build()
