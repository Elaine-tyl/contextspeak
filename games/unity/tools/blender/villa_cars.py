"""Vehicles for the villa: decimate, colour variants, nose → Blender −Y (Unity +Z), origin at the bottom centre."""
import bpy, sys, os, math, colorsys
import numpy as np
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]
out = args[0]; SUPER = args[1]; SUV = args[2]
os.makedirs(out, exist_ok=True)

def load(path, ratio):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if ratio < 1:
        m = o.modifiers.new("d", "DECIMATE"); m.ratio = ratio; bpy.ops.object.modifier_apply(modifier="d")
    # nose is at −X in these Meshy exports (checked on renders): turn it to −Y, then sit on the ground.
    for v in o.data.vertices: v.co = Vector((-v.co.y, v.co.x, v.co.z))
    zs = [v.co.z for v in o.data.vertices]; xs = [v.co.x for v in o.data.vertices]; ys = [v.co.y for v in o.data.vertices]
    c = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    for v in o.data.vertices: v.co -= c
    return o

def base_image(o):
    for slot in o.material_slots:
        for n in slot.material.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image and "normal" not in n.image.name.lower() and "rough" not in n.image.name.lower() and "metal" not in n.image.name.lower() and "emission" not in n.image.name.lower():
                return n
    return None

def shrink(img, size=2048):
    if img.size[0] > size: img.scale(size, size)

def recolour(node, name, hue, sat=1.0, val=1.0):
    src = node.image; w, h = src.size
    px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    mx = np.max(px[..., :3], -1); mn = np.min(px[..., :3], -1)
    s = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0)
    body = (b > r + 0.06) & (b > g) & (s > 0.25)                       # the blue paint
    # rebuild the colour with the new hue, keeping each pixel's brightness (reflections stay)
    v = mx * val; ss = np.clip(s * sat, 0, 1)
    hh = hue * 6; i = np.floor(hh).astype(int) % 6; f = hh - np.floor(hh)
    p_ = v * (1 - ss); q = v * (1 - ss * f); t = v * (1 - ss * (1 - f))
    table = [(v, t, p_), (q, v, p_), (p_, v, t), (p_, q, v), (t, p_, v), (v, p_, q)][int(hh) % 6]
    for k in range(3): px[..., k] = np.where(body, table[k], px[..., k])
    img = bpy.data.images.new(name, w, h, alpha=True); img.pixels.foreach_set(px.ravel())
    node.image = img

def export(o, name):
    o.name = name; o.data.name = name
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, name + ".glb"), export_format="GLB", use_selection=True, export_yup=True, export_image_format="JPEG")
    xs = [v.co.x for v in o.data.vertices]; ys = [v.co.y for v in o.data.vertices]; zs = [v.co.z for v in o.data.vertices]
    print("CAR", name, "tris", sum(len(p.vertices) - 2 for p in o.data.polygons), "size", round(max(xs) - min(xs), 2), round(max(ys) - min(ys), 2), round(max(zs) - min(zs), 2))

for name, hue, sat, val in (("SportsCar_Blue", None, 1, 1), ("SportsCar_Red", 0.995, 1.15, 1.0), ("SportsCar_Yellow", 0.13, 1.1, 1.25)):
    o = load(SUPER, 0.06)
    node = base_image(o)
    if node: shrink(node.image)
    if hue is not None and node: recolour(node, name + "_paint", hue, sat, val)
    export(o, name)
o = load(SUV, 1.0); node = base_image(o)
if node: shrink(node.image)
export(o, "FamilySUV")
