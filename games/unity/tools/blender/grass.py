"""Efficient grass/wildflower tufts: real curved blade geometry (no alpha cards → no overdraw),
tapered, randomly bent and rotated, coloured through a small gradient atlas (dark base → lit tip).
Each species exports one GLB with <Name>_LOD0/1/2 (100 % / 45 % / 15 % of the blades).
"""
import math, os, random
import bpy, bmesh
import numpy as np
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out_nature')
ATLAS = os.path.join(OUT, 'tex', 'GrassAtlas.png')
# Atlas columns (u ranges) — base colour → tip colour (sRGB hex), physically modest saturation.
COLUMNS = [('2E3F1A', '7A9443'), ('263716', '5F7A33'), ('3A4A1E', '9AA052'), ('5B5530', 'C2B37A'), ('6B6236', 'D8C994'),
           ('2F4520', '6E8A45'), ('3C5A2A', '88A45E'), ('F2F0E8', 'FFFFFF'), ('D9A21E', 'F2C63A'), ('8E5BB8', 'B98BE0')]


def atlas():
    os.makedirs(os.path.dirname(ATLAS), exist_ok=True)
    w, h = 320, 256
    img = np.zeros((h, w, 4))
    for i, (a, b) in enumerate(COLUMNS):
        ca = np.array([int(a[k:k + 2], 16) for k in (0, 2, 4)]) / 255
        cb = np.array([int(b[k:k + 2], 16) for k in (0, 2, 4)]) / 255
        for y in range(h):
            t = (y / (h - 1)) ** 0.8
            img[y, i * 32:(i + 1) * 32, :3] = ca * (1 - t) + cb * t
    img[..., 3] = 1
    image = bpy.data.images.new('GrassAtlas', w, h)
    image.pixels = img.flatten()
    image.filepath_raw = ATLAS
    image.file_format = 'PNG'
    image.save()
    return image


def material(image):
    mat = bpy.data.materials.new('Leaf_Grass')
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    tex = nodes.new('ShaderNodeTexImage'); tex.image = image; tex.interpolation = 'Linear'
    bsdf = nodes['Principled BSDF']
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.75
    return mat


def blade(bm, uv, base, height, width, yaw, lean, curl, column, segments=4):
    rows = []
    for s in range(segments + 1):
        t = s / segments
        bend = lean * t * t
        pos = base + Vector((math.sin(yaw) * bend, math.cos(yaw) * bend, height * t * (1 - 0.15 * lean * t)))
        half = width * (1 - t) ** 0.9 * 0.5 + 0.0008
        side = Vector((math.cos(yaw), -math.sin(yaw), 0)) * half
        twist = Vector((0, 0, curl * t * half))
        rows.append((bm.verts.new(pos - side - twist), bm.verts.new(pos + side + twist), t))
    u0 = (column * 32 + 6) / 320; u1 = (column * 32 + 26) / 320
    for (a, b, ta), (c, d, tb) in zip(rows, rows[1:]):
        face = bm.faces.new((a, b, d, c))
        for loop, (u, v) in zip(face.loops, ((u0, ta), (u1, ta), (u1, tb), (u0, tb))):
            loop[uv].uv = (u, min(v, 0.995))


def head(bm, uv, centre, radius, column, petals=8):
    """A tiny flower head: a flat star of petals (wildflowers)."""
    u0 = (column * 32 + 6) / 320; u1 = (column * 32 + 26) / 320
    middle = bm.verts.new(centre + Vector((0, 0, radius * 0.15)))
    ring = [bm.verts.new(centre + Vector((math.cos(a) * radius, math.sin(a) * radius, 0)))
            for a in np.linspace(0, 2 * math.pi, petals, endpoint=False)]
    for i in range(petals):
        face = bm.faces.new((middle, ring[i], ring[(i + 1) % petals]))
        for loop, (u, v) in zip(face.loops, ((u0, 0.95), (u1, 0.8), (u0, 0.8))):
            loop[uv].uv = (u, v)


SPECIES = {
    # name: (blades, height range m, width m, lean, columns, flower column or None, flower count)
    'GrassFresh': (70, (0.18, 0.38), 0.010, 0.35, [0, 1, 2], None, 0),
    'GrassMeadow': (55, (0.35, 0.75), 0.008, 0.55, [0, 2, 5], None, 0),
    'GrassDry': (60, (0.25, 0.6), 0.007, 0.6, [3, 4, 2], None, 0),
    'GrassShort': (90, (0.06, 0.16), 0.012, 0.25, [1, 0, 6], None, 0),
    'Reeds': (30, (0.9, 1.6), 0.014, 0.25, [5, 2, 1], None, 0),
    'WildDaisies': (40, (0.15, 0.3), 0.009, 0.3, [0, 6], 7, 7),
    'WildButtercups': (40, (0.18, 0.35), 0.008, 0.3, [0, 5], 8, 6),
    'WildViolets': (35, (0.1, 0.22), 0.012, 0.25, [6, 1], 9, 6),
}


def build(name, spec, mat, seed):
    blades, (hmin, hmax), width, lean, columns, flower, flowers = spec
    random.seed(seed)
    objs = []
    plan = []
    for i in range(blades):
        r = 0.16 * math.sqrt(random.random()) * (1.6 if name == 'Reeds' else 1)
        a = random.random() * 2 * math.pi
        plan.append(dict(base=Vector((math.cos(a) * r, math.sin(a) * r, 0)), height=random.uniform(hmin, hmax), width=width * random.uniform(0.7, 1.3),
                         yaw=random.uniform(0, 2 * math.pi), lean=lean * random.uniform(0.3, 1.2) * 0.9, curl=random.uniform(-1, 1), column=random.choice(columns)))
    heads = [dict(base=Vector((random.uniform(-0.12, 0.12), random.uniform(-0.12, 0.12), 0)), height=random.uniform(hmin, hmax) * 1.1) for _ in range(flowers)]
    for level, share in enumerate((1.0, 0.45, 0.15)):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new('UVMap')
        keep = plan[:max(3, int(len(plan) * share))]
        for b in keep:
            blade(bm, uv, b['base'], b['height'], b['width'] * (1 + (1 - share) * 0.8), b['yaw'], b['lean'], b['curl'], b['column'], 4 if level == 0 else 2)
        for h in heads[:max(1, int(len(heads) * share))] if flower is not None else []:
            blade(bm, uv, h['base'], h['height'], 0.003, random.uniform(0, 6.28), 0.05, 0, columns[0], 2)
            head(bm, uv, h['base'] + Vector((0, 0, h['height'])), 0.024 if name != 'WildViolets' else 0.018, flower)
        mesh = bpy.data.meshes.new(f'{name}_LOD{level}')
        bm.to_mesh(mesh); bm.free()
        mesh.materials.append(mat)
        for p in mesh.polygons: p.use_smooth = True
        obj = bpy.data.objects.new(mesh.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        objs.append(obj)
    for o in bpy.context.scene.objects: o.select_set(o in objs)
    path = os.path.join(OUT, name + '.glb')
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_yup=True)
    tris = [sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs]
    print(name, tris)
    for o in objs: bpy.data.objects.remove(o)


if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    image = atlas()
    mat = material(image)
    for i, (name, spec) in enumerate(SPECIES.items()):
        build(name, spec, mat, 100 + i)
