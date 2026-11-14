"""Small animated animals for ContextSpeak Life, built in Blender as light meshes with SEPARATE moving parts
(the Meshy animals are static, rig-less and 100k–700k triangles — fine as hero props, too heavy for flocks).
Each export is a GLB whose child objects are named so the Unity behaviours can move them:
  WingL / WingR  (pivot at the shoulder/hinge)   — birds, butterflies
  Tail           (pivot at the tail root)         — koi
  Leg_L0..Leg_R3 (pivot at the hip)               — crab
Units: metres, forward = −Y in Blender (= +Z in Unity), up = +Z.
Run: python3 villa_fauna.py -- <out_dir>
"""
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector, Matrix
OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1]) if "--" in sys.argv else "fauna_out"
os.makedirs(OUT, exist_ok=True)

def mat(name, colour, rough=0.6, image=None, cutout=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*colour, 1); p.inputs["Roughness"].default_value = rough
    if image is not None:
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = image
        m.node_tree.links.new(t.outputs["Color"], p.inputs["Base Color"])
        if cutout:
            g = m.node_tree.nodes.new("ShaderNodeMath"); g.operation = "GREATER_THAN"; g.inputs[1].default_value = 0.5
            m.node_tree.links.new(t.outputs["Alpha"], g.inputs[0]); m.node_tree.links.new(g.outputs[0], p.inputs["Alpha"])
            if hasattr(m, "surface_render_method"): m.surface_render_method = "DITHERED"
            if hasattr(m, "blend_method"): m.blend_method = "CLIP"
    m.use_backface_culling = False
    return m

def image(name, rgba):
    img = bpy.data.images.new(name, rgba.shape[1], rgba.shape[0], alpha=True)
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32)[::-1].ravel())
    img.pack()
    return img

def obj(name, parent=None, pivot=(0, 0, 0)):
    me = bpy.data.meshes.new(name); o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.location = pivot
    if parent: o.parent = parent; o.matrix_parent_inverse = parent.matrix_world.inverted()
    return o

def ellipsoid(o, centre, radii, material, seg=12, rings=8):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + Vector(centre) - o.location
    for f in bm.faces: f.smooth = True
    add(o, bm, material)

def tube(o, p0, p1, r0, r1, material, seg=6):
    bm = bmesh.new(); p0 = Vector(p0) - o.location; p1 = Vector(p1) - o.location
    a = (p1 - p0).normalized(); t = a.orthogonal().normalized(); b = a.cross(t)
    rings = [[bm.verts.new(c + (t * math.cos(2 * math.pi * i / seg) + b * math.sin(2 * math.pi * i / seg)) * r) for i in range(seg)] for c, r in ((p0, r0), (p1, r1))]
    for i in range(seg):
        j = (i + 1) % seg; bm.faces.new([rings[0][i], rings[0][j], rings[1][j], rings[1][i]]).smooth = True
    add(o, bm, material)

def quad(o, corners, material, uvs=((0, 0), (1, 0), (1, 1), (0, 1))):
    bm = bmesh.new(); uv = bm.loops.layers.uv.new("UVMap")
    f = bm.faces.new([bm.verts.new(Vector(c) - o.location) for c in corners])
    for l, u in zip(f.loops, uvs): l[uv].uv = u
    add(o, bm, material)

def add(o, bm, material):
    """Merge a bmesh into the object's mesh with the given material."""
    if material.name not in [m.name for m in o.data.materials]: o.data.materials.append(material)
    idx = [m.name for m in o.data.materials].index(material.name)
    for f in bm.faces: f.material_index = idx
    old = bmesh.new(); old.from_mesh(o.data)
    if "UVMap" not in old.loops.layers.uv: old.loops.layers.uv.new("UVMap")
    tmp = bpy.data.meshes.new("tmp"); bm.to_mesh(tmp); bm.free()
    old.from_mesh(tmp); old.to_mesh(o.data); old.free(); bpy.data.meshes.remove(tmp)

def export(root, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in [root] + list(root.children_recursive): o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name + ".glb"), export_format="GLB", use_selection=True, export_yup=True)
    print("FAUNA", name, sum(len(o.data.polygons) for o in [root] + list(root.children_recursive) if o.type == "MESH"), "faces")

def reset(): bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- birds
def bird(name, body_col, wing_col, belly_col, length, span, beak_col=(0.95, 0.65, 0.15)):
    reset()
    body_m, wing_m, belly_m, beak_m, eye_m = mat(name + "_Body", body_col), mat(name + "_Wing", wing_col), mat(name + "_Belly", belly_col), mat(name + "_Beak", beak_col, 0.4), mat(name + "_Eye", (0.02, 0.02, 0.02), 0.1)
    root = obj(name)
    L = length
    ellipsoid(root, (0, 0, 0.0), (0.17 * L, 0.5 * L, 0.17 * L), body_m)
    ellipsoid(root, (0, 0.08 * L, -0.05 * L), (0.14 * L, 0.35 * L, 0.11 * L), belly_m)
    ellipsoid(root, (0, -0.48 * L, 0.08 * L), (0.12 * L, 0.14 * L, 0.12 * L), body_m)                        # head
    tube(root, (0, -0.6 * L, 0.07 * L), (0, -0.75 * L, 0.05 * L), 0.035 * L, 0.004, beak_m, 6)
    for s in (-1, 1): ellipsoid(root, (s * 0.085 * L, -0.53 * L, 0.11 * L), (0.022 * L,) * 3, eye_m, 6, 4)
    quad(root, [(-0.12 * L, 0.45 * L, 0.02 * L), (0.12 * L, 0.45 * L, 0.02 * L), (0.16 * L, 0.8 * L, 0.0), (-0.16 * L, 0.8 * L, 0.0)], wing_m)   # tail fan
    for s, nm in ((-1, "WingL"), (1, "WingR")):
        w = obj(nm, root, (s * 0.12 * L, -0.08 * L, 0.06 * L))
        half = span / 2
        quad(w, [(s * 0.12 * L, -0.25 * L, 0.06 * L), (s * (0.12 * L + half * 0.55), -0.22 * L, 0.06 * L), (s * (0.12 * L + half), 0.05 * L, 0.06 * L), (s * 0.12 * L, 0.18 * L, 0.06 * L)], wing_m)
    export(root, name)

bird("Bird_Gull", (0.93, 0.93, 0.92), (0.62, 0.64, 0.67), (0.97, 0.97, 0.96), 0.45, 1.15)
bird("Bird_Sparrow", (0.45, 0.32, 0.2), (0.35, 0.24, 0.15), (0.82, 0.74, 0.6), 0.16, 0.24, (0.3, 0.25, 0.2))
bird("Bird_Bluebird", (0.2, 0.42, 0.78), (0.15, 0.3, 0.62), (0.88, 0.55, 0.32), 0.17, 0.27, (0.15, 0.15, 0.15))
bird("Bird_Sandpiper", (0.6, 0.52, 0.42), (0.45, 0.38, 0.3), (0.95, 0.94, 0.9), 0.2, 0.36, (0.15, 0.13, 0.1))

# ---------------------------------------------------------------- butterflies (wing texture with alpha, two wings hinged on the body)
def wing_texture(base, accent, seed):
    rng = np.random.default_rng(seed); n = 256
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    fore = ((x - 0.05) ** 2 / 0.75 + (y - 0.62) ** 2 / 0.12) < 1
    hind = ((x - 0.05) ** 2 / 0.36 + (y - 0.25) ** 2 / 0.08) < 1
    m = (fore | hind) & (x > 0.02)
    col = np.zeros((n, n, 4), np.float32)
    col[..., :3] = base
    edge = m & ~((((x - 0.05) ** 2 / 0.6 + (y - 0.62) ** 2 / 0.09) < 1) | (((x - 0.05) ** 2 / 0.27 + (y - 0.25) ** 2 / 0.06) < 1))
    col[edge, :3] = (0.05, 0.04, 0.04)
    for k in range(5):
        cx, cy, r = rng.uniform(0.25, 0.7), rng.uniform(0.2, 0.75), rng.uniform(0.04, 0.07)
        spot = ((x - cx) ** 2 + (y - cy) ** 2) < r * r
        col[spot & m, :3] = accent
    vein = (np.abs(np.sin((np.arctan2(y - 0.45, x) * 9))) < 0.08) & m
    col[vein, :3] *= 0.6
    col[..., 3] = m.astype(np.float32)
    return col

for i, (base, accent, nm) in enumerate((((0.95, 0.55, 0.1), (0.05, 0.05, 0.05), "Monarch"), ((0.2, 0.45, 0.95), (0.85, 0.9, 1.0), "Blue"),
                                        ((0.98, 0.95, 0.85), (0.95, 0.75, 0.2), "White"), ((0.95, 0.85, 0.15), (0.1, 0.1, 0.1), "Yellow"))):
    reset()
    tex = image("Butterfly_" + nm, wing_texture(base, accent, 10 + i))
    wm = mat("Butterfly_" + nm + "_Wing", (1, 1, 1), 0.7, tex, cutout=True)
    bm_ = mat("Butterfly_Body", (0.08, 0.07, 0.06))
    root = obj("Butterfly_" + nm)
    ellipsoid(root, (0, 0, 0), (0.008, 0.035, 0.008), bm_, 8, 6)
    for s, wname in ((-1, "WingL"), (1, "WingR")):
        w = obj(wname, root, (s * 0.004, 0, 0))
        quad(w, [(s * 0.004, -0.045, 0), (s * 0.06, -0.045, 0), (s * 0.06, 0.035, 0), (s * 0.004, 0.035, 0)], wm,
             ((0, 1), (1, 1), (1, 0), (0, 0)) if s > 0 else ((0, 1), (1, 1), (1, 0), (0, 0)))
    export(root, "Butterfly_" + nm)

# ---------------------------------------------------------------- crab (8 legs + 2 claws on hip pivots)
reset()
shell = mat("Crab_Shell", (0.62, 0.16, 0.06), 0.4); leg_m = mat("Crab_Leg", (0.7, 0.24, 0.1), 0.5); eye_m = mat("Crab_Eye", (0.02, 0.02, 0.02), 0.1)
root = obj("Crab")
ellipsoid(root, (0, 0, 0.035), (0.075, 0.055, 0.025), shell)
for s in (-1, 1):
    tube(root, (s * 0.02, -0.04, 0.05), (s * 0.022, -0.055, 0.075), 0.004, 0.003, leg_m, 5)
    ellipsoid(root, (s * 0.022, -0.056, 0.078), (0.006,) * 3, eye_m, 6, 4)
    for k in range(4):
        hip = (s * 0.06, -0.02 + k * 0.018, 0.03)
        lg = obj("Leg_%s%d" % ("L" if s < 0 else "R", k), root, hip)
        knee = (s * 0.1, -0.025 + k * 0.024, 0.045); foot = (s * 0.13, -0.03 + k * 0.03, 0.0)
        tube(lg, hip, knee, 0.006, 0.005, leg_m, 5); tube(lg, knee, foot, 0.005, 0.002, leg_m, 5)
    cl = obj("Claw_" + ("L" if s < 0 else "R"), root, (s * 0.05, -0.045, 0.035))
    tube(cl, (s * 0.05, -0.045, 0.035), (s * 0.07, -0.085, 0.04), 0.007, 0.006, leg_m, 5)
    ellipsoid(cl, (s * 0.075, -0.1, 0.04), (0.018, 0.022, 0.012), shell, 8, 6)
export(root, "Crab")

# ---------------------------------------------------------------- koi (body + tail on a pivot, patterned texture)
def koi_texture(seed, pattern):
    rng = np.random.default_rng(seed); n = 256
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    col = np.ones((n, n, 4), np.float32); col[..., :3] = (0.97, 0.95, 0.9)
    for k in range(6):
        cx, cy = rng.uniform(0, 1), rng.uniform(0.1, 0.9); rx, ry = rng.uniform(0.08, 0.2), rng.uniform(0.08, 0.25)
        blob = ((x - cx) ** 2 / rx ** 2 + (y - cy) ** 2 / ry ** 2) < 1
        col[blob, :3] = pattern[k % len(pattern)]
    return col
for i, pat in enumerate((((0.95, 0.35, 0.08),), ((0.95, 0.35, 0.08), (0.05, 0.05, 0.05)), ((0.98, 0.75, 0.15),), ((0.08, 0.08, 0.08), (0.95, 0.4, 0.1)))):
    reset()
    tex = image("Koi_%d" % i, koi_texture(30 + i, pat))
    km = mat("Koi_%d_Skin" % i, (1, 1, 1), 0.35, tex); fin = mat("Koi_Fin_%d" % i, (0.98, 0.7, 0.5), 0.5)
    root = obj("Koi_%d" % i)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=1.0)
    uv = bm.loops.layers.uv.new("UVMap")
    for v in bm.verts:
        t = (v.co.y + 1) / 2
        taper = 1 - 0.55 * t
        v.co = Vector((v.co.x * 0.045 * taper, v.co.y * 0.17, v.co.z * 0.04 * taper))
    for f in bm.faces:
        f.smooth = True
        for l in f.loops: l[uv].uv = ((math.atan2(l.vert.co.x, l.vert.co.z) / math.pi + 1) / 2, (l.vert.co.y / 0.17 + 1) / 2)
    add(root, bm, km)
    for s in (-1, 1): quad(root, [(s * 0.03, -0.06, -0.01), (s * 0.08, -0.03, -0.03), (s * 0.06, 0.0, -0.02), (s * 0.03, -0.02, -0.01)], fin)
    tail = obj("Tail", root, (0, 0.16, 0))
    quad(tail, [(0, 0.16, -0.005), (0, 0.27, -0.06), (0, 0.29, 0.0), (0, 0.27, 0.06)], fin)
    export(root, "Koi_%d" % i)
print("FAUNA DONE")
