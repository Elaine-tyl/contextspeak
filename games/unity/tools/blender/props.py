"""Greenhouse café props: terracotta pots (several shapes) and hanging ivy strands, with LODs.
Colours come from the grass atlas (ivy) or a small terracotta texture, so the vegetation shader
and the URP Lit shader can both use them."""
import math, os, random
import bpy, bmesh
import numpy as np
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out_nature')
ATLAS = os.path.join(OUT, 'tex', 'GrassAtlas.png')


def terracotta_image():
    size = 256
    rng = np.random.default_rng(3)
    base = np.array([0.62, 0.33, 0.21])
    noise = rng.normal(0, 0.035, (size, size, 1))
    streak = (np.sin(np.linspace(0, 40, size))[:, None, None] * 0.02)
    img = np.clip(base * (1 + noise + streak), 0, 1)
    # A whitish salt bloom near the rim (v high) like real clay pots.
    v = np.linspace(0, 1, size)[:, None, None]
    img = img * (1 - 0.25 * np.clip((v - 0.75) * 4, 0, 1) * rng.uniform(0.4, 1, (size, size, 1))) + 0.18 * np.clip((v - 0.75) * 4, 0, 1)
    rgba = np.concatenate([np.clip(img, 0, 1), np.ones((size, size, 1))], axis=2)
    image = bpy.data.images.new('Terracotta', size, size)
    image.pixels = rgba.flatten()
    path = os.path.join(OUT, 'tex', 'Terracotta.png')
    image.filepath_raw = path; image.file_format = 'PNG'; image.save()
    return image


def material(name, image, rough):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = image
    bsdf = mat.node_tree.nodes['Principled BSDF']
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough
    return mat


def pot(name, profile, segments, mat, soil=None):
    """Lathe a pot from (radius, height) points (outer wall top→rim lip→inner wall)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')
    rings = []
    for r, z in profile:
        rings.append([bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)) for a in np.linspace(0, 2 * math.pi, segments, endpoint=False)])
    for i in range(len(rings) - 1):
        for j in range(segments):
            k = (j + 1) % segments
            f = bm.faces.new((rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]))
            for loop, (u, v) in zip(f.loops, ((j / segments, i / len(rings)), ((j + 1) / segments, i / len(rings)), ((j + 1) / segments, (i + 1) / len(rings)), (j / segments, (i + 1) / len(rings)))):
                loop[uv].uv = (u, v)
    bottom = bm.faces.new(list(reversed(rings[0])))
    for loop in bottom.loops: loop[uv].uv = (0.5, 0.02)
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    mesh.materials.append(mat)
    for p in mesh.polygons: p.use_smooth = True
    obj = bpy.data.objects.new(name, mesh); bpy.context.scene.collection.objects.link(obj)
    if soil:
        r, z = soil
        bpy.ops.mesh.primitive_circle_add(vertices=segments, radius=r, fill_type='NGON', location=(0, 0, z))
        disc = bpy.context.active_object
        disc.name = name + '_soil'
        dm = bpy.data.materials.new('Soil'); dm.diffuse_color = (0.12, 0.08, 0.05, 1)
        dm.use_nodes = True; dm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.08, 0.05, 0.03, 1)
        disc.data.materials.append(dm)
        disc.parent = obj
    return obj


def export(objs, name):
    for o in bpy.context.scene.objects: o.select_set(False)
    for o in objs:
        o.select_set(True)
        for c in o.children: c.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name + '.glb'), export_format='GLB', use_selection=True, export_yup=True)
    print(name, sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs))


def ivy(name, length, seed, atlas_mat):
    random.seed(seed)
    objs = []
    for level, share in enumerate((1.0, 0.4)):
        bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap')
        strands = 3
        for s in range(strands):
            x0 = random.uniform(-0.25, 0.25); y0 = random.uniform(-0.08, 0.08)
            L = length * random.uniform(0.6, 1.0)
            steps = int(L / 0.032)
            for i in range(steps):
                if random.random() > share: continue
                t = i / steps
                p = Vector((x0 + math.sin(t * 7 + s) * 0.05 * t, y0 + math.cos(t * 5 + s) * 0.04 * t, -L * t))
                # A small heart-ish leaf: 5-vertex fan, alternating sides of the stem.
                side = 1 if i % 2 else -1
                ang = random.uniform(0, 2 * math.pi)
                size = random.uniform(0.035, 0.06) * (1.1 - 0.4 * t)
                d = Vector((math.cos(ang) * side, math.sin(ang), -0.3)).normalized()
                n = Vector((-d.y, d.x, 0.4)).normalized()
                tip = p + d * size * 1.6
                left = p + d * size * 0.7 + n.cross(d).normalized() * size * 0.7
                right = p + d * size * 0.7 - n.cross(d).normalized() * size * 0.7
                verts = [bm.verts.new(v) for v in (p, left, tip, right)]
                f = bm.faces.new(verts)
                col = random.choice([1, 5, 6])
                u0 = (col * 32 + 8) / 320; u1 = (col * 32 + 24) / 320
                vv = random.uniform(0.35, 0.85)
                for loop, (u, v) in zip(f.loops, ((u0, vv - 0.15), (u0, vv), (u1, vv + 0.1), (u1, vv))):
                    loop[uv].uv = (u, v)
            # Stem: a thin ribbon.
            prev = None
            for i in range(0, steps + 1, 2 if level == 0 else 4):
                t = i / steps
                p = Vector((x0 + math.sin(t * 7 + s) * 0.05 * t, y0 + math.cos(t * 5 + s) * 0.04 * t, -L * t))
                a = bm.verts.new(p + Vector((0.004, 0, 0))); b = bm.verts.new(p - Vector((0.004, 0, 0)))
                if prev:
                    f = bm.faces.new((prev[0], prev[1], b, a))
                    for loop in f.loops: loop[uv].uv = ((3 * 32 + 16) / 320, 0.2)
                prev = (a, b)
        mesh = bpy.data.meshes.new(f'{name}_LOD{level}'); bm.to_mesh(mesh); bm.free()
        mesh.materials.append(atlas_mat)
        obj = bpy.data.objects.new(mesh.name, mesh); bpy.context.scene.collection.objects.link(obj)
        objs.append(obj)
    # Pivot at the top (hangs from a beam): geometry already hangs below z = 0.
    export(objs, name)
    for o in objs: bpy.data.objects.remove(o)


if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    terra = material('Terracotta', terracotta_image(), 0.85)
    atlas = bpy.data.images.load(ATLAS)
    leaf = material('Leaf_Ivy', atlas, 0.6)
    shapes = {
        'TerracottaPotSmall': ([(0.055, 0), (0.07, 0.03), (0.085, 0.11), (0.095, 0.115), (0.095, 0.13), (0.082, 0.13), (0.078, 0.12), (0.06, 0.02)], (0.08, 0.115)),
        'TerracottaPotTall': ([(0.07, 0), (0.09, 0.05), (0.11, 0.26), (0.125, 0.27), (0.125, 0.3), (0.108, 0.3), (0.1, 0.28), (0.075, 0.02)], (0.1, 0.27)),
        'TerracottaPotWide': ([(0.09, 0), (0.12, 0.02), (0.15, 0.12), (0.165, 0.13), (0.165, 0.16), (0.15, 0.16), (0.14, 0.14), (0.1, 0.02)], (0.14, 0.13)),
        'TerracottaJug': ([(0.04, 0), (0.07, 0.04), (0.085, 0.12), (0.06, 0.2), (0.035, 0.24), (0.04, 0.27), (0.03, 0.27), (0.025, 0.24)], None),
    }
    for name, (profile, soil) in shapes.items():
        o = pot(name, profile, 28, terra, soil)
        export([o], name)
        bpy.data.objects.remove(o)
    for i, length in enumerate((1.2, 1.8, 2.4)):
        ivy(f'HangingIvy{i + 1}', length, 40 + i, leaf)
