"""ContextSpeak Life — Luxury two-storey villa, built in Blender as modular, enterable architecture.

Reference: Meshy "Contemporary Luxury Villa with Glass" (a single closed 1M-triangle shell, no interior).
Its design language is preserved — cream plaster volumes, flat roofs with white parapets, dark vertical
wood slats, a tall gold-tinted glazed stair tower at the front right, a front balcony with a glass
railing over the garage, black gate between stone piers, front lawn, paved driveway — and the building
is rebuilt with real openings, floors, stairs, rooms and furniture so it can be walked through.

Run (Blender 4.x/5.x, or the `bpy` module):  python3 luxury_villa.py -- <out_dir> [reference.fbx]
Writes: LuxuryVilla.blend, LuxuryVilla_{Architecture,Interior,Doors,Exterior}.fbx + .glb, Textures/*.png,
        LuxuryVilla_manifest.json (rooms, doors, interaction anchors, lights, pool, in Blender coordinates).
Coordinates: metres, Blender X right, +Y into the plot (the street is at −Y), Z up. Unity (glTF) = (−x, z, −y).
"""
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(ARGS[0] if ARGS else "villa_out")
REFERENCE = ARGS[1] if len(ARGS) > 1 else None
TEX = os.path.join(OUT, "Textures")
os.makedirs(TEX, exist_ok=True)

H1 = 3.4          # floor-to-floor height (upper floor top at z = 3.4)
SLAB = 0.25
HROOF = 6.8       # roof slab top
EXT = 0.3         # exterior wall thickness
INT = 0.15        # interior wall thickness
X0, X1, Y0, Y1 = -11.0, 11.0, 0.0, 16.0   # house footprint
RNG = np.random.default_rng(7)

# ----------------------------------------------------------------------------- textures (procedural PBR)

def value_noise(size, cells, seed):
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells))
    x = np.linspace(0, cells, size, endpoint=False)
    xi = np.floor(x).astype(int); xf = x - xi
    xf = xf * xf * (3 - 2 * xf)
    a = g[np.ix_(xi % cells, xi % cells)]; b = g[np.ix_(xi % cells, (xi + 1) % cells)]
    c = g[np.ix_((xi + 1) % cells, xi % cells)]; d = g[np.ix_((xi + 1) % cells, (xi + 1) % cells)]
    u = xf[None, :]; v = xf[:, None]
    return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v


def fbm(size, base, octaves, seed):
    total = np.zeros((size, size)); amp = 1; norm = 0
    for o in range(octaves):
        total += amp * value_noise(size, base * 2 ** o, seed + o); norm += amp; amp *= 0.5
    return total / norm


def save_rgb(name, rgb):
    img = bpy.data.images.new(name, rgb.shape[1], rgb.shape[0], alpha=False)
    px = np.ones((rgb.shape[0], rgb.shape[1], 4), dtype=np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px[::-1].ravel())
    path = os.path.join(TEX, name + ".png")
    img.filepath_raw = path; img.file_format = "PNG"; img.save()
    return img


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def texture_set(name, size, colour_fn, rough_fn, height_fn, strength=2.0):
    h = np.broadcast_to(np.asarray(height_fn(size), dtype=float), (size, size)).copy()
    c = np.broadcast_to(np.asarray(colour_fn(size, h), dtype=float), (size, size, 3)).copy()
    r = np.broadcast_to(np.asarray(rough_fn(size, h), dtype=float), (size, size)).copy()
    return (save_rgb(name + "_BaseColor", c), save_rgb(name + "_Roughness", np.repeat(r[..., None], 3, -1)),
            save_rgb(name + "_Normal", normal_from_height(h, strength)))


def mix(a, b, t): return np.asarray(a)[None, None, :] * (1 - t[..., None]) + np.asarray(b)[None, None, :] * t[..., None]


def tex_wood(size):
    n = fbm(size, 4, 5, 1); grain = fbm(size, 64, 3, 2)
    y = np.linspace(0, 1, size, endpoint=False)[:, None] * np.ones((1, size))
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    planks = 10                                   # 2 m texture → 20 cm boards
    row = np.floor(y * planks); offset = (row * 0.37) % 1
    seam = (np.abs(((y * planks) % 1) - 0.0) < 0.012) | (np.abs(((x + offset) * 1.5 % 1)) < 0.004)
    tone = (row * 0.618) % 1
    streak = np.sin((x * 40 + n * 6 + tone * 3) * math.pi) * 0.5 + 0.5
    h = 0.6 + streak * 0.08 + grain * 0.1 - seam * 0.5
    def colour(s, h):
        t = np.clip(0.25 + tone * 0.4 + streak * 0.25 + (grain - 0.5) * 0.3, 0, 1)
        c = mix((0.36, 0.22, 0.12), (0.62, 0.43, 0.26), t)
        c[seam] *= 0.55
        return c
    return colour, (lambda s, h: np.clip(0.42 + (1 - streak) * 0.12 + seam * 0.3, 0, 1)), (lambda s: h)


def tex_stone(size):
    n = fbm(size, 6, 6, 11); pores = (value_noise(size, 128, 12) > 0.86).astype(float)
    y = np.linspace(0, 1, size, endpoint=False)[:, None] * np.ones((1, size))
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    rows = 3; row = np.floor(y * rows); offs = (row % 2) * 0.5
    joint = (((y * rows) % 1) < 0.015) | ((((x + offs) * 2) % 1) < 0.008)
    h = 0.6 + n * 0.25 - pores * 0.15 - joint * 0.5
    colour = lambda s, h: np.where(joint[..., None], mix((0.6, 0.57, 0.52), (0.6, 0.57, 0.52), n), mix((0.78, 0.72, 0.62), (0.9, 0.86, 0.78), n)) * (1 - pores[..., None] * 0.15)
    return colour, (lambda s, h: np.clip(0.62 + n * 0.2 + joint * 0.2, 0, 1)), (lambda s: h)


def tex_marble(size):
    n = fbm(size, 3, 6, 21)
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    veins = np.abs(np.sin((x * 3 + n * 4) * math.pi)) ** 18
    h = 0.5 + veins * 0.02
    colour = lambda s, h: mix((0.94, 0.93, 0.91), (0.55, 0.55, 0.57), np.clip(veins * 0.8 + (n - 0.5) * 0.15, 0, 1))
    return colour, (lambda s, h: np.clip(0.18 + veins * 0.1, 0, 1)), (lambda s: h)


def tex_plaster(size, tint):
    n = fbm(size, 16, 5, 31)
    colour = lambda s, h: mix(np.array(tint) * 0.95, np.array(tint), n)
    return colour, (lambda s, h: 0.85 + n * 0.1), (lambda s: n * 0.15)


def tex_concrete(size):
    n = fbm(size, 8, 6, 41); spots = (value_noise(size, 96, 42) > 0.9).astype(float)
    colour = lambda s, h: mix((0.5, 0.5, 0.49), (0.66, 0.66, 0.64), n) * (1 - spots[..., None] * 0.1)
    return colour, (lambda s, h: 0.8 + n * 0.12), (lambda s: n * 0.3 - spots * 0.1)


def tex_tiles(size, count, a, b, grout, seed):
    y = np.linspace(0, 1, size, endpoint=False)[:, None] * np.ones((1, size))
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    joint = (((x * count) % 1) < 0.04) | (((y * count) % 1) < 0.04)
    cell = (np.floor(x * count) * 7 + np.floor(y * count) * 13) % 5 / 5
    n = fbm(size, 8, 3, seed)
    h = np.where(joint, 0.2, 0.7 + n * 0.05)
    colour = lambda s, h: np.where(joint[..., None], np.array(grout)[None, None, :] * np.ones((s, s, 3)), mix(a, b, np.clip(cell * 0.6 + n * 0.4, 0, 1)))
    return colour, (lambda s, h: np.where(joint, 0.8, 0.12 + n * 0.08)), (lambda s: h)


def tex_slats(size):
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    n = fbm(size, 4, 5, 51); grain = fbm(size, 48, 3, 52)
    slat = 8                                       # 1 m texture → 12.5 cm slats with gaps
    gap = ((x * slat) % 1) < 0.18
    h = np.where(gap, 0.1, 0.7 + grain * 0.1)
    colour = lambda s, h: np.where(gap[..., None], 0.08, mix((0.2, 0.12, 0.07), (0.36, 0.23, 0.13), np.clip(grain * 0.7 + n * 0.3, 0, 1)))
    return colour, (lambda s, h: np.where(gap, 0.9, 0.48 + grain * 0.1)), (lambda s: h)


def tex_fabric(size, a, b, seed):
    y = np.linspace(0, 1, size, endpoint=False)[:, None] * np.ones((1, size))
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    weave = (np.sin(x * 2 * math.pi * 120) * np.sin(y * 2 * math.pi * 120)) * 0.5 + 0.5
    n = fbm(size, 8, 4, seed)
    colour = lambda s, h: mix(a, b, np.clip(weave * 0.3 + n * 0.7, 0, 1))
    return colour, (lambda s, h: 0.88 + weave * 0.08), (lambda s: weave * 0.4)


def tex_leather(size):
    cells = fbm(size, 48, 2, 61); n = fbm(size, 6, 4, 62)
    colour = lambda s, h: mix((0.24, 0.13, 0.07), (0.42, 0.25, 0.14), np.clip(n * 0.8 + cells * 0.2, 0, 1))
    return colour, (lambda s, h: 0.48 + cells * 0.15), (lambda s: cells * 0.5)


def tex_metal(size, a, b, brushed):
    n = fbm(size, 4, 3, 71)
    lines = fbm(size, 256, 1, 72) if brushed else np.zeros((size, size))
    if brushed: lines = np.repeat(lines[:, :1], size, 1) * 0.7 + lines * 0.3
    colour = lambda s, h: mix(a, b, np.clip(n * 0.5 + lines * 0.5, 0, 1))
    return colour, (lambda s, h: 0.32 + lines * 0.15), (lambda s: lines * 0.2)


def tex_grass(size):
    n = fbm(size, 16, 6, 81); blades = fbm(size, 200, 1, 82)
    colour = lambda s, h: mix((0.18, 0.32, 0.12), (0.36, 0.52, 0.2), np.clip(n * 0.6 + blades * 0.4, 0, 1))
    return colour, (lambda s, h: 0.92), (lambda s: blades * 0.5)


def tex_leaves(size):
    n = fbm(size, 24, 5, 141); spots = fbm(size, 96, 2, 142)
    colour = lambda s, h: mix((0.12, 0.24, 0.08), (0.34, 0.5, 0.18), np.clip(n * 0.6 + spots * 0.6 - 0.2, 0, 1))
    return colour, (lambda s, h: 0.7 + spots * 0.2), (lambda s: spots)


def tex_bark(size):
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    n = fbm(size, 8, 5, 151)
    ridges = np.abs(np.sin((x * 14 + n * 3) * math.pi))
    colour = lambda s, h: mix((0.18, 0.13, 0.09), (0.38, 0.3, 0.22), np.clip(ridges * 0.7 + n * 0.3, 0, 1))
    return colour, (lambda s, h: 0.85 + ridges * 0.1), (lambda s: ridges)


def tex_cobble(size):
    n = fbm(size, 10, 4, 91)
    y = np.linspace(0, 1, size, endpoint=False)[:, None] * np.ones((1, size))
    x = np.linspace(0, 1, size, endpoint=False)[None, :] * np.ones((size, 1))
    count = 8; row = np.floor(y * count); off = (row % 2) * 0.5
    cx = ((x * count + off) % 1) - 0.5; cy = ((y * count) % 1) - 0.5
    d = np.maximum(np.abs(cx), np.abs(cy))
    stone = np.clip((0.46 - d) * 12, 0, 1)
    tone = ((np.floor(x * count + off) * 13 + row * 7) % 6) / 6
    h = stone * (0.7 + n * 0.2)
    colour = lambda s, h: np.where(stone[..., None] > 0.2, mix((0.48, 0.44, 0.4), (0.7, 0.66, 0.6), np.clip(tone * 0.6 + n * 0.4, 0, 1)), 0.28)
    return colour, (lambda s, h: 0.75 + (1 - stone) * 0.2), (lambda s: h)

# name: (tile size in metres, texture builder, metallic, extra)
MATERIALS = {}


def material(name, tile, builder, size=512, metallic=0.0, strength=2.0, alpha=None, emission=None, colour=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree; nodes = nt.nodes
    p = nodes.get("Principled BSDF")
    p.inputs["Metallic"].default_value = metallic
    if builder is not None:
        c, r, h = builder(size)
        base, rough, normal = texture_set(name, size, c, r, h, strength)
        rough.colorspace_settings.name = "Non-Color"; normal.colorspace_settings.name = "Non-Color"
        tb = nodes.new("ShaderNodeTexImage"); tb.image = base
        tr = nodes.new("ShaderNodeTexImage"); tr.image = rough
        tn = nodes.new("ShaderNodeTexImage"); tn.image = normal
        nm = nodes.new("ShaderNodeNormalMap")
        nt.links.new(tb.outputs["Color"], p.inputs["Base Color"])
        nt.links.new(tr.outputs["Color"], p.inputs["Roughness"])
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
    elif colour is not None:
        p.inputs["Base Color"].default_value = (*colour, 1)
        p.inputs["Roughness"].default_value = 0.4
    if alpha is not None:
        p.inputs["Alpha"].default_value = alpha
        p.inputs["Roughness"].default_value = 0.05
        if hasattr(m, "surface_render_method"): m.surface_render_method = "BLENDED"
        if hasattr(m, "blend_method"): m.blend_method = "BLEND"
    if emission is not None:
        p.inputs["Emission Color"].default_value = (*emission, 1)
        p.inputs["Emission Strength"].default_value = 3.0
    MATERIALS[name] = (m, tile)
    return m


def build_materials():
    material("Villa_Wood", 2.0, tex_wood, 1024)
    material("Villa_Stone", 1.2, tex_stone, 1024)
    material("Villa_Marble", 2.0, tex_marble, 1024, strength=0.5)
    material("Villa_Plaster", 2.0, lambda s: tex_plaster(s, (0.93, 0.9, 0.84)), 512, strength=1)
    material("Villa_PlasterWhite", 2.0, lambda s: tex_plaster(s, (0.97, 0.96, 0.94)), 512, strength=1)
    material("Villa_Concrete", 2.0, tex_concrete, 512)
    material("Villa_Slats", 1.0, tex_slats, 512, strength=4)
    material("Villa_Tile", 0.6, lambda s: tex_tiles(s, 2, (0.86, 0.85, 0.82), (0.92, 0.91, 0.88), (0.7, 0.7, 0.68), 101), 512)
    material("Villa_PoolTile", 0.5, lambda s: tex_tiles(s, 20, (0.18, 0.5, 0.62), (0.32, 0.66, 0.74), (0.85, 0.9, 0.9), 102), 512)
    material("Villa_Fabric", 0.5, lambda s: tex_fabric(s, (0.42, 0.42, 0.44), (0.56, 0.56, 0.58), 111), 512, strength=1)
    material("Villa_FabricWarm", 0.5, lambda s: tex_fabric(s, (0.72, 0.66, 0.56), (0.84, 0.79, 0.7), 112), 512, strength=1)
    material("Villa_Leather", 0.5, tex_leather, 512)
    material("Villa_Aluminium", 1.0, lambda s: tex_metal(s, (0.12, 0.11, 0.1), (0.2, 0.18, 0.16), False), 256, metallic=0.9)
    material("Villa_BrushedMetal", 1.0, lambda s: tex_metal(s, (0.6, 0.6, 0.62), (0.78, 0.78, 0.8), True), 512, metallic=1.0)
    material("Villa_Grass", 3.0, tex_grass, 512)
    material("Villa_Cobble", 1.5, tex_cobble, 512)
    material("Villa_Glass", 1.0, None, alpha=0.22, colour=(0.75, 0.85, 0.88))
    material("Villa_GoldGlass", 1.0, None, alpha=0.4, colour=(0.85, 0.66, 0.32))
    material("Villa_Black", 1.0, None, colour=(0.05, 0.05, 0.05))
    material("Villa_Screen", 1.0, None, colour=(0.02, 0.02, 0.03))
    material("Villa_LightPanel", 1.0, None, colour=(1, 0.95, 0.85), emission=(1, 0.86, 0.66))
    material("Villa_Water", 1.0, None, alpha=0.5, colour=(0.2, 0.55, 0.65))
    material("Villa_Leaf", 1.5, tex_leaves, 512, strength=3)
    material("Villa_Bark", 1.0, tex_bark, 256, strength=3)
    material("Villa_Soil", 1.0, None, colour=(0.22, 0.16, 0.11))
    material("Villa_Ceramic", 1.0, None, colour=(0.95, 0.95, 0.95))

# ----------------------------------------------------------------------------- V9 detail: alpha cards, extra PBR sets

def save_rgba(name, rgba):
    img = bpy.data.images.new(name, rgba.shape[1], rgba.shape[0], alpha=True)
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32)[::-1].ravel())
    path = os.path.join(TEX, name + ".png")
    img.filepath_raw = path; img.file_format = "PNG"; img.save()
    return img


def _ellipse_mask(size, cx, cy, rx, ry, angle):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    ca, sa = math.cos(angle), math.sin(angle)
    dx, dy = x - cx, y - cy
    u = (dx * ca + dy * sa) / rx; v = (-dx * sa + dy * ca) / ry
    # pointed leaf: narrower towards both tips
    w = 1 - np.abs(u)
    return np.clip(1 - (v / np.maximum(w, 1e-3)) ** 2, 0, 1) * (np.abs(u) < 1), u, v


def leaf_atlas(size=1024, seed=5):
    """A twig card: a stem with ~9 leaves (alpha). Used for tree crowns and shrubs."""
    rng = np.random.default_rng(seed)
    rgba = np.zeros((size, size, 4), np.float32)
    n = fbm(size, 32, 3, seed)
    for k in range(11):
        t = k / 10
        cx, cy = 0.5 + (rng.uniform(-0.18, 0.18)), 0.1 + t * 0.8
        ang = rng.uniform(-1.0, 1.0) + (0.6 if k % 2 else -0.6)
        m, u, v = _ellipse_mask(size, cx, cy, rng.uniform(0.12, 0.17), rng.uniform(0.045, 0.06), ang + math.pi / 2)
        tone = rng.uniform(0, 1)
        col = np.array((0.16 + tone * 0.12, 0.3 + tone * 0.16, 0.08 + tone * 0.05))
        vein = np.exp(-(v * 14) ** 2) * 0.25
        shade = (0.75 + 0.35 * (1 - np.abs(v)) + (n - 0.5) * 0.3 + vein)[..., None]
        a = (m > 0.02).astype(np.float32)
        rgba[..., :3] = rgba[..., :3] * (1 - a[..., None]) + (col[None, None, :] * shade) * a[..., None]
        rgba[..., 3] = np.maximum(rgba[..., 3], a)
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    stem = (np.abs(x - 0.5 - (y - 0.5) * 0.05) < 0.006) & (y > 0.05) & (y < 0.92)
    rgba[stem, :3] = (0.25, 0.2, 0.1); rgba[stem, 3] = 1
    return rgba


def big_leaf(size=1024, seed=9):
    """One long tropical paddle leaf (bird-of-paradise / banana style) with a midrib and slanted veins."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    v = (x - 0.5) / 0.2; u = (y - 0.5) / 0.47
    w = np.sqrt(np.clip(1 - u ** 2, 0, 1)) * (1 - 0.15 * u)
    m = (np.abs(v) < w).astype(np.float32)
    n = fbm(size, 16, 4, seed)
    veins = 0.5 + 0.5 * np.sin((u * 40 + np.abs(v) * 6) * math.pi)
    midrib = np.exp(-(v * 20) ** 2)
    tear = (fbm(size, 24, 2, seed + 1) > 0.62) & (np.abs(v) > 0.55 * w)            # wind tears near the edges
    m[tear] = 0
    col = np.stack([0.12 + 0.08 * n, 0.32 + 0.14 * n, 0.08 + 0.04 * n], -1)
    col *= (0.8 + 0.25 * veins * (np.abs(v) / np.maximum(w, 1e-3)))[..., None]
    col = col * (1 - midrib[..., None]) + np.array((0.55, 0.6, 0.35)) * midrib[..., None]
    col *= (0.75 + 0.3 * (1 - np.abs(v) / np.maximum(w, 1e-3)))[..., None]
    return np.concatenate([col, m[..., None]], -1)


def flower_card(size=512, seed=13):
    rng = np.random.default_rng(seed)
    rgba = np.zeros((size, size, 4), np.float32)
    palettes = [(0.95, 0.55, 0.65), (0.98, 0.85, 0.35), (0.97, 0.96, 0.92), (0.75, 0.55, 0.9)]
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    for k in range(14):
        cx, cy = rng.uniform(0.15, 0.85), rng.uniform(0.15, 0.85); r = rng.uniform(0.045, 0.08)
        col = np.array(palettes[k % len(palettes)])
        ang = np.arctan2(y - cy, x - cx); d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
        petal = d < r * (0.65 + 0.35 * np.abs(np.cos(ang * 2.5)))
        centre = d < r * 0.25
        rgba[petal, :3] = col * (0.8 + 0.2 * (1 - d[petal] / r))[..., None]; rgba[petal, 3] = 1
        rgba[centre, :3] = (0.9, 0.7, 0.2); rgba[centre, 3] = 1
    # a few leaves under the flowers
    leaves = leaf_atlas(size, seed + 3)
    under = (rgba[..., 3] < 0.5) & (leaves[..., 3] > 0.5)
    rgba[under] = leaves[under]
    return rgba


def material_cutout(name, rgba, tile=1.0):
    img = save_rgba(name + "_BaseColor", rgba)
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; p = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
    nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
    cut = nt.nodes.new("ShaderNodeMath"); cut.operation = "GREATER_THAN"; cut.inputs[1].default_value = 0.5
    nt.links.new(tex.outputs["Alpha"], cut.inputs[0]); nt.links.new(cut.outputs[0], p.inputs["Alpha"])
    p.inputs["Roughness"].default_value = 0.65
    if hasattr(m, "surface_render_method"): m.surface_render_method = "DITHERED"
    if hasattr(m, "blend_method"): m.blend_method = "CLIP"
    m.use_backface_culling = False
    MATERIALS[name] = (m, tile)


def tex_travertine(size):
    n = fbm(size, 4, 6, 201); bands = fbm(size, 2, 3, 202)
    y = np.linspace(0, 1, size, endpoint=False)[:, None] * np.ones((1, size))
    layers = np.sin((y * 9 + bands * 2.5) * math.pi) * 0.5 + 0.5
    pits = (value_noise(size, 160, 203) > 0.9).astype(float) * (layers > 0.4)
    rows = 2; joint = ((y * rows) % 1) < 0.006
    colour = lambda s, h: mix((0.78, 0.72, 0.62), (0.9, 0.86, 0.78), np.clip(layers * 0.5 + n * 0.5, 0, 1)) * (1 - pits[..., None] * 0.25) * (1 - joint[..., None] * 0.3)
    return colour, (lambda s, h: np.clip(0.55 + pits * 0.3 + n * 0.1, 0, 1)), (lambda s: 0.6 - pits * 0.4 - joint * 0.4 + n * 0.1)


def tex_asphalt(size):
    n = fbm(size, 24, 5, 211); grit = value_noise(size, 300, 212)
    colour = lambda s, h: mix((0.13, 0.13, 0.14), (0.24, 0.24, 0.25), np.clip(n * 0.6 + grit * 0.4, 0, 1))
    return colour, (lambda s, h: 0.82 + grit * 0.12), (lambda s: grit * 0.6)


def tex_gravel(size):
    cells = fbm(size, 96, 2, 221); n = fbm(size, 8, 3, 222)
    stones = np.clip((cells - 0.45) * 6, 0, 1)
    colour = lambda s, h: mix((0.45, 0.43, 0.4), (0.78, 0.76, 0.72), np.clip(stones * 0.7 + n * 0.3, 0, 1))
    return colour, (lambda s, h: 0.75 + (1 - stones) * 0.15), (lambda s: stones)


def tex_hedge(size):
    atlas = leaf_atlas(512, 31)
    rng = np.random.default_rng(32)
    col = np.zeros((size, size, 3), np.float32) + np.array((0.05, 0.1, 0.03)); h = np.zeros((size, size), np.float32)
    for k in range(90):
        ox, oy = rng.integers(0, size, 2); s = 512
        sub = np.roll(np.roll(np.pad(atlas, ((0, size - s), (0, size - s), (0, 0))) if size > s else atlas[:size, :size], ox, 1), oy, 0)
        a = sub[..., 3] > 0.5
        col[a] = sub[a, :3] * rng.uniform(0.8, 1.15); h[a] = rng.uniform(0.4, 1)
    return (lambda s, hh: col), (lambda s, hh: 0.7 + (1 - h) * 0.2), (lambda s: h)


def build_detail_materials():
    material("Villa_Travertine", 1.2, tex_travertine, 1024)
    material("Villa_Asphalt", 4.0, tex_asphalt, 512)
    material("Villa_Gravel", 1.0, tex_gravel, 512, strength=3)
    material("Villa_Linen", 0.4, lambda s: tex_fabric(s, (0.78, 0.75, 0.69), (0.88, 0.86, 0.8), 231), 512, strength=1)
    material("Villa_Cushion", 0.5, lambda s: tex_fabric(s, (0.86, 0.85, 0.82), (0.93, 0.92, 0.9), 232), 512, strength=1)
    material("Villa_Hedge", 1.2, tex_hedge, 1024, strength=4)
    material("Villa_Curtain", 0.6, lambda s: tex_fabric(s, (0.88, 0.86, 0.82), (0.94, 0.93, 0.9), 233), 512, strength=0.6)
    material_cutout("Villa_LeafCard", leaf_atlas())
    material_cutout("Villa_BigLeaf", big_leaf())
    material_cutout("Villa_FlowerCard", flower_card())
    material("Villa_Rock", 1.0, tex_stone, 512)


# ----------------------------------------------------------------------------- V9 detail: geometry helpers

def _bevel_last(part, nverts, width, segments):
    """Bevel the edges of the most recently added `nverts` vertices (a box)."""
    if width <= 0: return
    part.bm.verts.ensure_lookup_table()
    vs = part.bm.verts[-nverts:]
    edges = {e for v in vs for e in v.link_edges}
    bmesh.ops.bevel(part.bm, geom=list(edges), offset=width, segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True)


def rbox(part, mn, mx, mat, r=0.03, seg=2, yaw=0.0, about=None):
    """A box with rounded (bevelled) edges — cushions, counters, frames."""
    part.box(mn, mx, mat, yaw=yaw, about=about)
    _bevel_last(part, 8, min(r, (mx[0] - mn[0]) / 2.2, (mx[1] - mn[1]) / 2.2, (mx[2] - mn[2]) / 2.2), seg)


def tube(part, p0, p1, r0, r1, mat, seg=7):
    """A tapered cylinder between two points (branches)."""
    p0 = Vector(p0); p1 = Vector(p1); axis = (p1 - p0)
    if axis.length < 1e-4: return
    a = axis.normalized(); t = a.orthogonal().normalized(); b = a.cross(t)
    index = part.mat(mat); tile = MATERIALS[mat][1]
    rings = []
    for (c, r) in ((p0, r0), (p1, r1)):
        rings.append([part.bm.verts.new(c + (t * math.cos(2 * math.pi * i / seg) + b * math.sin(2 * math.pi * i / seg)) * r - part.pivot) for i in range(seg)])
    for i in range(seg):
        j = (i + 1) % seg
        f = part.bm.faces.new([rings[0][i], rings[0][j], rings[1][j], rings[1][i]]); f.material_index = index; f.smooth = True
        for k, loop in enumerate(f.loops):
            loop[part.uv].uv = ((i + (k in (1, 2))) / seg * 2 * math.pi * r0 / tile * 3, (0 if k in (0, 1) else axis.length) / tile)


def card(part, centre, normal, up, w, h, mat, u0=0.0, u1=1.0):
    """A leaf / flower card (double-sided via the material), anchored at its bottom centre."""
    c = Vector(centre); n = Vector(normal).normalized(); upv = Vector(up).normalized()
    side = upv.cross(n).normalized(); upv = n.cross(side).normalized()
    corners = [c - side * w / 2, c + side * w / 2, c + side * w / 2 + upv * h, c - side * w / 2 + upv * h]
    vs = [part.bm.verts.new(v - part.pivot) for v in corners]
    f = part.bm.faces.new(vs); f.material_index = part.mat(mat)
    for loop, uv in zip(f.loops, ((u0, 0), (u1, 0), (u1, 1), (u0, 1))): loop[part.uv].uv = uv


def tree(name, collection, base, height, seed, crown=1.0):
    """A broadleaf tree: tapered trunk, 2 levels of branches, leaf-twig cards at every tip."""
    rng = np.random.default_rng(seed)
    p = Part(name, collection, pivot=base)
    b = Vector(base)
    trunk_top = b + Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), height * 0.45))
    tube(p, b, trunk_top, 0.17 * crown, 0.11 * crown, "Villa_Bark", 9)
    tips = []
    for i in range(int(rng.integers(4, 6))):
        ang = i / 5 * 2 * math.pi + rng.uniform(-0.4, 0.4); lift = rng.uniform(0.45, 0.8)
        d = Vector((math.cos(ang) * (1 - lift), math.sin(ang) * (1 - lift), lift)).normalized()
        end = trunk_top + d * height * rng.uniform(0.35, 0.5) * crown
        tube(p, trunk_top, end, 0.09 * crown, 0.05 * crown, "Villa_Bark", 7)
        for k in range(3):
            d2 = (d + Vector(rng.uniform(-0.7, 0.7, 3)) + Vector((0, 0, 0.3))).normalized()
            tip = end + d2 * height * rng.uniform(0.15, 0.25) * crown
            tube(p, end, tip, 0.045 * crown, 0.02 * crown, "Villa_Bark", 5)
            tips.append(tip)
    for tip in tips:
        for k in range(14):
            off = Vector(rng.normal(0, 0.45, 3)) * crown; off.z *= 0.7
            n = Vector(rng.normal(0, 1, 3)); up = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 1.2))
            if abs(n.normalized().dot(up.normalized())) > 0.95: n = Vector((1, 0, 0))
            card(p, tip + off, n, up, 0.55 * crown, 0.6 * crown, "Villa_LeafCard")
    return p


def tropical(name, collection, base, height, seed, leaves=11):
    """A clump of large paddle leaves on stalks (bird-of-paradise style), like the reference garden."""
    rng = np.random.default_rng(seed)
    p = Part(name, collection, pivot=base)
    b = Vector(base)
    for i in range(leaves):
        ang = i / leaves * 2 * math.pi + rng.uniform(-0.3, 0.3)
        lean = rng.uniform(0.15, 0.45)
        d = Vector((math.cos(ang) * lean, math.sin(ang) * lean, 1)).normalized()
        stalk = height * rng.uniform(0.35, 0.55)
        top = b + d * stalk
        tube(p, b + Vector((math.cos(ang), math.sin(ang), 0)) * 0.05, top, 0.025, 0.015, "Villa_Leaf", 5)
        out = Vector((math.cos(ang), math.sin(ang), 0))
        normal = out.cross(Vector((0, 0, 1))).normalized() if rng.uniform() < 0.5 else (out * 0.5 + Vector((0, 0, 0.3))).normalized().cross(d).normalized()
        upv = (d + out * rng.uniform(0.2, 0.6)).normalized()
        card(p, top, normal, upv, height * 0.32, height * rng.uniform(0.5, 0.65), "Villa_BigLeaf")
    return p


def shrub(name, collection, base, radius, seed, flowers=False):
    rng = np.random.default_rng(seed)
    p = Part(name, collection, pivot=base); b = Vector(base)
    for k in range(int(28 * radius / 0.5)):
        off = Vector((rng.normal(0, radius * 0.5), rng.normal(0, radius * 0.5), abs(rng.normal(radius * 0.35, radius * 0.25))))
        card(p, b + off, Vector(rng.normal(0, 1, 3)), Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 1.5)), radius * 0.7, radius * 0.75,
             "Villa_FlowerCard" if flowers and rng.uniform() < 0.45 else "Villa_LeafCard")
    return p


def rock(part, centre, size, seed):
    rng = np.random.default_rng(seed)
    part.sphere(centre, size, "Villa_Rock", rng.uniform(0.45, 0.7), 1)

# ----------------------------------------------------------------------------- geometry

class Part:
    """One named, editable object (its own pivot), made of boxes with world-scale box-mapped UVs."""
    all = {}

    def __init__(self, name, collection, pivot=(0, 0, 0)):
        self.name = name; self.collection = collection; self.pivot = Vector(pivot)
        self.bm = bmesh.new(); self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mats = []
        Part.all[name] = self

    def mat(self, name):
        if name not in self.mats: self.mats.append(name)
        return self.mats.index(name)

    def box(self, mn, mx, mat, yaw=0.0, about=None, skip=()):
        mn = Vector(mn); mx = Vector(mx)
        if mx.x - mn.x < 1e-4 or mx.y - mn.y < 1e-4 or mx.z - mn.z < 1e-4: return
        corners = [Vector((x, y, z)) for z in (mn.z, mx.z) for y in (mn.y, mx.y) for x in (mn.x, mx.x)]
        if yaw:
            c = Vector(about) if about is not None else (mn + mx) / 2
            rot = Matrix.Rotation(math.radians(yaw), 3, "Z")
            corners = [c + rot @ (v - c) for v in corners]
        vs = [self.bm.verts.new(v - self.pivot) for v in corners]
        faces = {"-z": (0, 2, 3, 1), "+z": (4, 5, 7, 6), "-y": (0, 1, 5, 4), "+y": (2, 6, 7, 3), "-x": (0, 4, 6, 2), "+x": (1, 3, 7, 5)}
        index = self.mat(mat); tile = MATERIALS[mat][1]
        for key, idx in faces.items():
            if key in skip: continue
            f = self.bm.faces.new([vs[i] for i in idx])
            f.material_index = index
            f.normal_update()
            n = f.normal
            for loop in f.loops:
                w = loop.vert.co + self.pivot
                if abs(n.z) > max(abs(n.x), abs(n.y)): u, v = w.x, w.y
                elif abs(n.x) > abs(n.y): u, v = w.y, w.z
                else: u, v = w.x, w.z
                loop[self.uv].uv = (u / tile, v / tile)

    def cylinder(self, centre, radius, height, mat, segments=16):
        c = Vector(centre); index = self.mat(mat); tile = MATERIALS[mat][1]
        ring0 = []; ring1 = []
        for i in range(segments):
            a = 2 * math.pi * i / segments
            p = Vector((c.x + math.cos(a) * radius, c.y + math.sin(a) * radius, c.z))
            ring0.append(self.bm.verts.new(p - self.pivot)); ring1.append(self.bm.verts.new(p + Vector((0, 0, height)) - self.pivot))
        for i in range(segments):
            j = (i + 1) % segments
            f = self.bm.faces.new([ring0[i], ring0[j], ring1[j], ring1[i]]); f.material_index = index
            for k, loop in enumerate(f.loops): loop[self.uv].uv = ((i + (k in (1, 2))) / segments * 2 * math.pi * radius / tile, (loop.vert.co.z + self.pivot.z) / tile)
        for ring in (ring0, ring1):
            f = self.bm.faces.new(ring if ring is ring1 else list(reversed(ring))); f.material_index = index
            for loop in f.loops: w = loop.vert.co + self.pivot; loop[self.uv].uv = (w.x / tile, w.y / tile)

    def sphere(self, centre, radius, mat, squash=1.0, subdiv=2):
        index = self.mat(mat); tile = MATERIALS[mat][1]
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        c = Vector(centre)
        for v in res["verts"]:
            v.co = Vector((v.co.x, v.co.y, v.co.z * squash)) + c - self.pivot
        faces = {f for v in res["verts"] for f in v.link_faces}
        for f in faces:
            f.material_index = index; f.smooth = True
            for loop in f.loops:
                w = loop.vert.co + self.pivot
                loop[self.uv].uv = ((w.x + w.z) / tile, (w.y + w.z * 0.5) / tile)

    def build(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.normal_update(); self.bm.to_mesh(mesh); self.bm.free()
        for m in self.mats: mesh.materials.append(MATERIALS[m][0])
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = self.pivot
        self.collection.objects.link(obj)
        return obj


def wall(part, a, b, t, z0, z1, mat, openings=(), mat_in=None):
    """Straight wall from a to b (2D, along X or Y) with openings: (centre along wall, width, sill, head)."""
    a = Vector((a[0], a[1])); b = Vector((b[0], b[1]))
    along_x = abs(b.x - a.x) > abs(b.y - a.y)
    length = (b - a).length
    start = min(a.x, b.x) if along_x else min(a.y, b.y)
    def piece(s0, s1, h0, h1):
        if s1 - s0 < 0.01 or h1 - h0 < 0.01: return
        if along_x: part.box((start + s0, a.y - t / 2, h0), (start + s1, a.y + t / 2, h1), mat)
        else: part.box((a.x - t / 2, start + s0, h0), (a.x + t / 2, start + s1, h1), mat)
        if h0 in (0.0, H1, 2 * H1) and t < 0.2 and "Trim" in Part.all:              # skirting on interior walls
            tr = Part.all["Trim"]
            for side in (-1, 1):
                if along_x: tr.box((start + s0, a.y + side * t / 2 - (0.012 if side < 0 else 0), h0), (start + s1, a.y + side * t / 2 + (0.012 if side > 0 else 0), h0 + 0.09), "Villa_PlasterWhite")
                else: tr.box((a.x + side * t / 2 - (0.012 if side < 0 else 0), start + s0, h0), (a.x + side * t / 2 + (0.012 if side > 0 else 0), start + s1, h0 + 0.09), "Villa_PlasterWhite")
    cursor = 0.0
    for centre, width, sill, head in sorted(openings):
        o0 = centre - width / 2; o1 = centre + width / 2
        piece(cursor, o0, z0, z1)
        piece(o0, o1, z0, z0 + sill)
        piece(o0, o1, z0 + head, z1)
        cursor = o1
    piece(cursor, length, z0, z1)


MANIFEST = {"units": "metres, Blender coordinates (Unity = (-x, z, -y))", "rooms": [], "doors": [], "anchors": [], "lights": [], "windows": []}


def room(name, floor, x0, y0, x1, y1, kind):
    z = floor * H1
    MANIFEST["rooms"].append({"name": name, "floor": floor, "min": [x0, y0, z], "max": [x1, y1, z + (H1 - SLAB if floor < 2 else 3.15) if kind != "terrace" else z + 3.0], "kind": kind})


def anchor(kind, x, y, z, yaw, room_name, extra=None):
    """yaw: degrees, 0 = facing +Y (into the plot); the direction a seated / standing user faces."""
    d = {"kind": kind, "pos": [round(x, 3), round(y, 3), round(z, 3)], "yaw": yaw, "room": room_name}
    if extra: d.update(extra)
    MANIFEST["anchors"].append(d)


def light(kind, x, y, z, room_name, colour=(1.0, 0.82, 0.62), intensity=1.0, rng=8.0):
    MANIFEST["lights"].append({"kind": kind, "pos": [x, y, z], "room": room_name, "colour": colour, "intensity": intensity, "range": rng})

# ----------------------------------------------------------------------------- the house

def build():
    col = {n: bpy.data.collections.new(n) for n in ("Architecture", "Interior", "Doors", "Exterior", "Surroundings", "Reference")}
    for c in col.values(): bpy.context.scene.collection.children.link(c)
    A, I, D, E = col["Architecture"], col["Interior"], col["Doors"], col["Exterior"]

    # Floors / levels: ground 0, upper H1 = 3.4, top (third) floor H2 = 6.8 (= main roof level), top roof H3.
    H2, H3 = 2 * H1, 3 * H1
    SH = (3.0, 2.5, 5.0, 4.5)                           # lift shaft (x0, y0, x1, y1), outer faces
    SW = (7.71, 0.3, 11.0, 3.0)                         # stairwell void above the U-stair (upper levels)

    def cells(x0, y0, x1, y1, holes, xs=(), ys=()):
        """Rectangle minus rectangular holes, as a list of boxes (grid decomposition)."""
        bx = sorted({x0, x1, *[v for h in holes for v in (h[0], h[2])], *xs}); by = sorted({y0, y1, *[v for h in holes for v in (h[1], h[3])], *ys})
        bx = [v for v in bx if x0 <= v <= x1]; by = [v for v in by if y0 <= v <= y1]
        out = []
        for i in range(len(bx) - 1):
            for j in range(len(by) - 1):
                cx, cy = (bx[i] + bx[i + 1]) / 2, (by[j] + by[j + 1]) / 2
                if any(h[0] < cx < h[2] and h[1] < cy < h[3] for h in holes): continue
                out.append((bx[i], by[j], bx[i + 1], by[j + 1]))
        return out

    Part("Trim", A)                                    # skirting, sills, frames, caps, switches (visual only)
    found = Part("Foundation", A)
    found.box((X0 - 0.2, Y0 - 0.2, -0.6), (X1 + 0.2, Y1 + 0.2, -0.02), "Villa_Concrete")
    g = Part("GroundFloor", A)
    for c in cells(-4, 0, 11, 16, [SH]): g.box((c[0], c[1], -0.02), (c[2], c[3], 0), "Villa_Stone")
    g.box((-11, 7.5, -0.02), (-4, 16, 0), "Villa_Wood")
    g.box((-11, 7.5, 0), (-4, 10.5, 0.01), "Villa_Tile")
    g.box((-11, 0, -0.02), (-4, 7.5, 0.0), "Villa_Concrete")
    for c in cells(1.5, 0, 5, 4.5, [SH]): g.box((c[0], c[1], 0), (c[2], c[3], 0.01), "Villa_Wood")       # study
    g.box((SH[0] + 0.15, SH[1] + 0.15, -0.2), (SH[2] - 0.15, SH[3] - 0.15, -0.15), "Villa_Concrete")      # lift pit

    up = Part("UpperFloor", A)
    def slab(x0, y0, x1, y1, finish, z=H1, holes=()):
        for c in cells(x0, y0, x1, y1, list(holes)):
            up.box((c[0], c[1], z - SLAB), (c[2], c[3], z - 0.02), "Villa_PlasterWhite")
            up.box((c[0], c[1], z - 0.02), (c[2], c[3], z), finish)
    slab(-11, 0, -4, 16, "Villa_Wood")
    up.box((-8, 7.5, H1), (-4, 10.5, H1 + 0.01), "Villa_Tile")
    slab(-4, 0, 5, 4.5, "Villa_Wood", holes=[SH])
    for c in cells(1.5, 0, 5, 4.5, [SH]): up.box((c[0], c[1], H1), (c[2], c[3], H1 + 0.01), "Villa_Tile")
    slab(-4, 4.5, 11, 8, "Villa_Stone")
    slab(4, 8, 11, 16, "Villa_Wood")
    slab(5, 0, 11, 4.5, "Villa_Stone", holes=[SW])     # upper stair landing
    slab(-4, 8, -2.8, 16, "Villa_Stone")
    # Main roof (z 6.8): the third floor's floor inside its footprint (x −4…11, y 0…8), roof terraces on
    # the west wing and over bedroom B, a green roof over the double-height living room.
    for c in cells(X0, 0, X1, 16, [SH, SW], xs=(-4, 4), ys=(8,)):
        cx, cy = (c[0] + c[2]) / 2, (c[1] + c[3]) / 2
        inside = -4 < cx < 11 and cy < 8
        finish = ("Villa_Wood" if cx < 3 and cy < 4.5 else "Villa_Stone") if inside else ("Villa_Grass" if -4 < cx < 4 else "Villa_Wood")
        slab(c[0], c[1], c[2], c[3], finish, z=H2)
    bal = Part("Balcony", A)
    bal.box((4, 16, H1 - SLAB), (11, 18, H1), "Villa_Stone")
    bal.box((-11, -2, H1 - SLAB), (-4, 0, H1), "Villa_Stone")

    # Exterior walls ----------------------------------------------------------------------------------
    ew = Part("ExteriorWalls", A)
    z0, z1, z2 = 0.0, H1, HROOF
    wall(ew, (X0, 0), (-4, 0), EXT, z0, z1, "Villa_Plaster", [(3.5, 6.0, 0, 2.6)])
    wall(ew, (-4, 0), (5, 0), EXT, z0, z1, "Villa_Plaster", [(2.75, 1.6, 0, 2.6), (7.25, 2.4, 0.9, 2.5)])
    wall(ew, (X0, 0), (-4, 0), EXT, z1, z2, "Villa_Plaster", [(3.5, 4.0, 0, 2.6)])
    wall(ew, (-4, 0), (5, 0), EXT, z1, z2, "Villa_Plaster", [(2.75, 3.0, 0.9, 2.5), (7.25, 1.2, 1.4, 2.4)])
    wall(ew, (-4, 0), (5, 0), EXT, H2, H3, "Villa_Plaster", [(3.0, 3.6, 1.1, 2.5), (8.0, 0.8, 1.6, 2.4)])   # media room, powder room
    wall(ew, (5, 0), (11, 0), EXT, z0, H3 + 0.8, "Villa_Plaster", [(3.0, 5.4, 0.3, 9.9)])                # glazed stair tower, 3 storeys
    wall(ew, (X0, 0), (X0, 16), EXT, z0, z1, "Villa_Plaster", [(9.0, 1.4, 0.9, 2.4), (13.25, 2.0, 0.9, 2.5)])
    wall(ew, (X0, 0), (X0, 16), EXT, z1, z2, "Villa_Plaster", [(4.0, 2.0, 0.9, 2.5), (13.25, 2.0, 0.9, 2.5)])
    wall(ew, (X1, 0), (X1, 16), EXT, z0, z1, "Villa_Plaster", [(6.75, 2.4, 0.9, 2.5), (12.5, 2.4, 1.1, 2.5)])
    wall(ew, (X1, 0), (X1, 16), EXT, z1, z2, "Villa_Plaster", [(12.0, 2.4, 0.9, 2.5)])
    wall(ew, (X1, 0), (X1, 8), EXT, H2, H3, "Villa_Plaster", [(6.25, 2.4, 0.9, 2.5)])                 # sky lounge window
    wall(ew, (-4, 0), (-4, 8), EXT, H2, H3, "Villa_Plaster", [(2.25, 1.6, 0, 2.5), (6.25, 2.4, 0, 2.6)])   # media + gym → west roof terrace
    wall(ew, (-4, 8), (X1, 8), EXT, H2, H3, "Villa_Plaster", [(3.5, 4.0, 0.9, 2.5), (11.5, 3.0, 0, 2.6)])  # gym window, sky lounge → east terrace
    wall(ew, (X0, 16), (-4, 16), EXT, z0, z1, "Villa_Plaster", [(3.5, 2.4, 0.9, 2.5)])
    wall(ew, (-4, 16), (4, 16), EXT, z0, z2, "Villa_Plaster", [(4.0, 7.4, 0.0, 6.4)])
    wall(ew, (4, 16), (X1, 16), EXT, z0, z1, "Villa_Plaster", [(2.0, 2.4, 0.0, 2.6), (5.0, 2.0, 0.9, 2.5)])
    wall(ew, (X0, 16), (-4, 16), EXT, z1, z2, "Villa_Plaster", [(3.5, 2.4, 0.9, 2.5)])
    wall(ew, (4, 16), (X1, 16), EXT, z1, z2, "Villa_Plaster", [(3.5, 3.0, 0, 2.6)])
    ew.box((-4, -0.2, z1 + 0.2), (-2.0, -0.16, z2 - 0.1), "Villa_Slats")
    ew.box((3.6, -0.2, 0.2), (4.95, -0.16, H3 - 0.1), "Villa_Slats")
    ew.box((-4, -0.2, H2 + 0.1), (-3.2, -0.16, H3 - 0.1), "Villa_Slats")
    ew.box((-4, 16.16, 0.2), (-3.4, 16.2, z2 - 0.2), "Villa_Slats")
    ew.box((3.4, 16.16, 0.2), (4.0, 16.2, z2 - 0.2), "Villa_Slats")
    for a, b in ((X0, -10.5), (-4.5, -2.1), (-0.4, X1)):                     # stone plinth — NOT across the garage door or the entrance
        ew.box((a, -0.24, 0), (b, -0.15, 0.45), "Villa_Travertine")
    for a, b in (((X0 - 0.09, 0), (X0 - 0.15, 16)), ((X1 + 0.15, 0), (X1 + 0.09, 16))):
        ew.box((min(a[0], b[0]), 0, 0), (max(a[0], b[0]), 16, 0.45), "Villa_Travertine")
    ew.box((-4, -0.24, 0.45), (-2.2, -0.16, H1 - 0.2), "Villa_Travertine")       # natural stone accent wall beside the entrance

    # Interior walls ----------------------------------------------------------------------------------
    iw = Part("InteriorWalls", A)
    zc = H1 - SLAB
    LIFT = (3.5, 1.0, 0, 2.1)                           # lift opening in the x = 5 wall (y 3.0–4.0)
    wall(iw, (-4, 0), (-4, 7.5), INT, 0, zc, "Villa_Plaster", [(5.8, 0.9, 0, 2.1)])
    wall(iw, (X0, 7.5), (-4, 7.5), INT, 0, zc, "Villa_Plaster", [(1.75, 0.9, 0, 2.1)])
    wall(iw, (-7.5, 7.5), (-7.5, 10.5), INT, 0, zc, "Villa_Plaster")
    wall(iw, (X0, 10.5), (-4, 10.5), INT, 0, zc, "Villa_Plaster")
    wall(iw, (-4, 7.5), (-4, 16), INT, 0, zc, "Villa_Plaster", [(1.5, 0.9, 0, 2.1), (4.5, 0.9, 0, 2.1)])
    wall(iw, (1.5, 0), (1.5, 4.5), INT, 0, zc, "Villa_Plaster")
    wall(iw, (1.5, 4.5), (3, 4.5), INT, 0, zc, "Villa_Plaster", [(0.75, 0.9, 0, 2.1)])          # study door (x 1.8–2.7)
    wall(iw, (5, 0), (5, 4.5), INT, 0, zc, "Villa_Plaster", [LIFT])
    u0, u1 = H1, H1 + 3.15
    wall(iw, (-4, 0), (-4, 16), INT, u0, u1, "Villa_Plaster", [(5.8, 0.9, 0, 2.1), (13.0, 0.9, 0, 2.1)])
    wall(iw, (X0, 7.5), (-4, 7.5), INT, u0, u1, "Villa_Plaster", [(2.0, 0.9, 0, 2.1), (5.0, 0.9, 0, 2.1)])
    wall(iw, (-8, 7.5), (-8, 10.5), INT, u0, u1, "Villa_Plaster")
    wall(iw, (X0, 10.5), (-4, 10.5), INT, u0, u1, "Villa_Plaster")
    wall(iw, (1.5, 0), (1.5, 4.5), INT, u0, u1, "Villa_Plaster")
    wall(iw, (-4, 4.5), (1.5, 4.5), INT, u0, u1, "Villa_Plaster", [(2.75, 1.6, 0, 2.2)])
    wall(iw, (1.5, 4.5), (3, 4.5), INT, u0, u1, "Villa_Plaster", [(0.75, 0.9, 0, 2.1)])          # shared bath door (x 1.8–2.7)
    wall(iw, (5, 0), (5, 4.5), INT, u0, u1, "Villa_Plaster", [LIFT])
    wall(iw, (4, 8), (11, 8), INT, u0, u1, "Villa_Plaster", [(1.5, 0.9, 0, 2.1)])
    wall(iw, (4, 8), (4, 16), INT, u0, u1, "Villa_Plaster")
    t0, t1 = H2, H2 + 3.15
    wall(iw, (-4, 4.5), (3, 4.5), INT, t0, t1, "Villa_Plaster", [(2.0, 0.9, 0, 2.1)])            # media room ↔ gym
    wall(iw, (3, 0), (3, 2.5), INT, t0, t1, "Villa_Plaster")                                      # media | powder room
    wall(iw, (3, 4.5), (3, 8), INT, t0, t1, "Villa_Plaster", [(1.75, 2.4, 0, 2.4)])              # gym ↔ sky lounge (open)
    wall(iw, (5, 0), (5, 4.5), INT, t0, t1, "Villa_Plaster", [(1.2, 0.8, 0, 2.1), LIFT])         # powder door, lift
    # Lift shaft: three full-height walls (the x = 5 side is the hall walls above, with the doors).
    shaft = Part("ElevatorShaft", A)
    top = H3 - SLAB
    shaft.box((SH[0] - INT / 2, SH[1], -0.2), (SH[0] + INT / 2, SH[3], top), "Villa_Plaster")
    shaft.box((SH[0], SH[1] - INT / 2, -0.2), (SH[2], SH[1] + INT / 2, top), "Villa_Plaster")
    shaft.box((SH[0], SH[3] - INT / 2, -0.2), (SH[2], SH[3] + INT / 2, top), "Villa_Plaster")
    for a, b in ((zc, u0), (u1, t0), (t1, top)):                                                    # x = 5 between the floors
        shaft.box((SH[2] - INT / 2, SH[1], a), (SH[2] + INT / 2, SH[3], b), "Villa_Plaster")
    for z in (0, H1, H2):                                                                            # hall side: steel frame + indicator
        shaft.box((5.075, 2.88, z), (5.11, 3.0, z + 2.22), "Villa_BrushedMetal")
        shaft.box((5.075, 4.0, z), (5.11, 4.12, z + 2.22), "Villa_BrushedMetal")
        shaft.box((5.075, 2.88, z + 2.1), (5.11, 4.12, z + 2.22), "Villa_BrushedMetal")
        shaft.box((5.075, 3.25, z + 2.3), (5.1, 3.75, z + 2.55), "Villa_Black")                     # floor indicator panel
        shaft.box((5.075, 4.3, z + 1.0), (5.1, 4.42, z + 1.3), "Villa_BrushedMetal")                # call button plate

    # Columns ---------------------------------------------------------------------------------------
    cols = Part("StructuralColumns", A)
    for x, y in ((-4, 8), (4, 8), (-4, 16), (4, 16), (4, 4.5)):
        cols.box((x - 0.18, y - 0.18, 0), (x + 0.18, y + 0.18, H1 - SLAB if (x, y) == (4, 4.5) else HROOF), "Villa_PlasterWhite")
    cols.box((-11.15, -2.15, 0), (-10.85, -1.85, H1 - SLAB), "Villa_PlasterWhite")
    cols.box((-4.15, -2.15, 0), (-3.85, -1.85, H1 - SLAB), "Villa_PlasterWhite")

    # Windows --------------------------------------------------------------------------------------
    win = Part("Windows", A)
    def glazing(x0, y0, x1, y1, z0, z1, mat="Villa_Glass", mullions=0.0, transoms=0.0):
        along_x = abs(x1 - x0) > abs(y1 - y0)
        t = 0.03
        if along_x: win.box((x0, y0 - t, z0), (x1, y0 + t, z1), mat)
        else: win.box((x0 - t, y0, z0), (x0 + t, y1, z1), mat)
        f = 0.05
        length = (x1 - x0) if along_x else (y1 - y0)
        def bar(s0, s1, h0, h1):
            if along_x: win.box((x0 + s0, y0 - 0.06, h0), (x0 + s1, y0 + 0.06, h1), "Villa_Aluminium")
            else: win.box((x0 - 0.06, y0 + s0, h0), (x0 + 0.06, y0 + s1, h1), "Villa_Aluminium")
        bar(0, f, z0, z1); bar(length - f, length, z0, z1); bar(0, length, z0, z0 + f); bar(0, length, z1 - f, z1)
        if mullions > 0:
            n = max(1, int(length / mullions))
            for i in range(1, n): s = length * i / n; bar(s - 0.02, s + 0.02, z0, z1)
        if transoms > 0:
            n = max(1, int((z1 - z0) / transoms))
            for i in range(1, n): h = z0 + (z1 - z0) * i / n; bar(0, length, h - 0.02, h + 0.02)
        MANIFEST["windows"].append({"min": [min(x0, x1), min(y0, y1), z0], "max": [max(x0, x1), max(y0, y1), z1]})
        if z0 > 0.5 and mat == "Villa_Glass":                                     # stone sill, sloped look, outside
            tr = Part.all["Trim"]
            if along_x:
                o = -1 if y0 < 8 else 1
                tr.box((x0 - 0.05, min(y0, y0 + o * 0.22), z0 - 0.05), (x1 + 0.05, max(y0, y0 + o * 0.22), z0), "Villa_Travertine")
            else:
                o = -1 if x0 < 0 else 1
                tr.box((min(x0, x0 + o * 0.22), y0 - 0.05, z0 - 0.05), (max(x0, x0 + o * 0.22), y1 + 0.05, z0), "Villa_Travertine")
    glazing(5.3, 0, 10.7, 0, 0.3, 9.9, "Villa_GoldGlass", 0.9, 0.82)                 # 3-storey stair tower grid
    glazing(-4 + 6.05, 0, -4 + 8.45, 0, 0.9, 2.5, mullions=1.2)
    glazing(-4 + 1.25, 0, -4 + 4.25, 0, H1 + 0.9, H1 + 2.5, mullions=1.0)
    glazing(-4 + 6.65, 0, -4 + 7.85, 0, H1 + 1.4, H1 + 2.4)
    glazing(-4 + 1.2, 0, -4 + 4.8, 0, H2 + 1.1, H2 + 2.5, mullions=1.2)              # media room
    glazing(-4 + 7.6, 0, -4 + 8.4, 0, H2 + 1.6, H2 + 2.4)                            # powder room
    glazing(-3.7, 16, 3.7, 16, 0, 6.4, mullions=1.85, transoms=3.2)
    glazing(X0, 8.3, X0, 9.7, 0.9, 2.4); glazing(X0, 12.25, X0, 14.25, 0.9, 2.5)
    glazing(X0, 3.0, X0, 5.0, H1 + 0.9, H1 + 2.5); glazing(X0, 12.25, X0, 14.25, H1 + 0.9, H1 + 2.5)
    glazing(X1, 5.55, X1, 7.95, 0.9, 2.5, mullions=1.2); glazing(X1, 11.3, X1, 13.7, 1.1, 2.5, mullions=1.2)
    glazing(X1, 10.8, X1, 13.2, H1 + 0.9, H1 + 2.5, mullions=1.2)
    glazing(X1, 5.05, X1, 7.45, H2 + 0.9, H2 + 2.5, mullions=1.2)                    # sky lounge
    glazing(-4 + 1.5, 8, -4 + 5.5, 8, H2 + 0.9, H2 + 2.5, mullions=1.0)              # gym
    glazing(X0 + 2.3, 16, X0 + 4.7, 16, 0.9, 2.5, mullions=1.2); glazing(X0 + 2.3, 16, X0 + 4.7, 16, H1 + 0.9, H1 + 2.5, mullions=1.2)
    glazing(4 + 4.0, 16, 4 + 6.0, 16, 0.9, 2.5)

    # Roofs: parapets around the main roof (the terraces), the third floor's own roof, the tower cap ----------
    roof = Part("Roof", A)
    for (a, b) in (((X0 - 0.3, -0.3), (-4, -0.1)), ((X0 - 0.3, Y1 + 0.1), (X1 + 0.3, Y1 + 0.3)), ((X0 - 0.3, -0.3), (X0 - 0.1, Y1 + 0.3)), ((X1 + 0.1, 8), (X1 + 0.3, Y1 + 0.3))):
        roof.box((a[0], a[1], HROOF), (b[0], b[1], HROOF + 0.6), "Villa_PlasterWhite")
    roof.box((-4.3, -0.3, H3 - 0.3), (X1 + 0.3, 8.3, H3), "Villa_PlasterWhite")
    for (a, b) in (((-4.3, -0.3), (X1 + 0.3, -0.1)), ((-4.3, 8.1), (X1 + 0.3, 8.3)), ((-4.3, -0.3), (-4.1, 8.3)), ((X1 + 0.1, -0.3), (X1 + 0.3, 8.3))):
        roof.box((a[0], a[1], H3), (b[0], b[1], H3 + 0.45), "Villa_PlasterWhite")
    roof.box((4.7, -0.4, H3 + 0.45), (11.3, 4.8, H3 + 0.75), "Villa_PlasterWhite")                     # tower cap
    roof.box((-11.3, -2.3, H1 - SLAB - 0.05), (-3.7, 0.0, H1 - SLAB), "Villa_PlasterWhite")
    roof.box((-1.0, 9.5, HROOF), (3.0, 14.5, HROOF + 0.05), "Villa_Glass")                               # skylight over the living room

    # Stairs: a U-stair per storey in the glazed tower (18 risers × 0.189 m, treads 0.28 m, 1.25 m wide).
    # Flight A rises along +x (y 0.3–1.55) to a half landing (x 9.95–10.85), flight B returns along −x
    # (y 1.75–3.0) to the floor landing at x < 7.71. Floating treads 0.22 m thick; the same U repeats above.
    st = Part("Stairs", A)
    risers = 18; rise = H1 / risers; run = 0.28
    ax0 = 7.71
    for s in range(2):
        b = s * H1
        for i in range(8):
            x = ax0 + i * run
            st.box((x, 0.3, b + rise * (i + 1) - 0.22), (x + run + 0.01, 1.55, b + rise * (i + 1)), "Villa_Wood")
        st.box((ax0 + 8 * run, 0.3, b + 9 * rise - 0.25), (10.85, 3.0, b + 9 * rise), "Villa_Stone")            # half landing
        for j in range(8):
            x1 = ax0 + 8 * run - j * run
            st.box((x1 - run - 0.01, 1.75, b + rise * (10 + j) - 0.22), (x1, 3.0, b + rise * (10 + j)), "Villa_Wood")
        st.box((ax0, 1.55, b), (ax0 + 8 * run, 1.75, b + H1 + 1.05 if s == 1 else b + H1), "Villa_PlasterWhite")  # spine wall between flights
        for i in range(8):                                                                                     # supports under flight A (ground only)
            if s == 0: st.box((ax0 + i * run, 0.3, 0), (ax0 + (i + 1) * run, 1.55, rise * (i + 1) - 0.22), "Villa_PlasterWhite")
    for j in range(8):
        x1 = ax0 + 8 * run - j * run
        st.box((x1 - run, 1.75, 0), (x1, 3.0, rise * (10 + j) - 0.22), "Villa_PlasterWhite")              # ground: closed under flight B
    st.box((ax0 + 8 * run, 0.3, 0), (10.85, 3.0, 9 * rise - 0.25), "Villa_PlasterWhite")
    MANIFEST["stairs"] = {"bottom": [ax0 - 0.5, 0.9, 0], "top": [6.3, 3.75, H1], "top2": [6.3, 3.75, H2], "risers": risers, "rise": rise, "run": run}

    # Railings ------------------------------------------------------------------------------------------
    rail = Part("Railings", A)
    def glass_rail(x0, y0, x1, y1, z, h=1.05):
        along_x = abs(x1 - x0) > abs(y1 - y0)
        if along_x:
            rail.box((min(x0, x1), y0 - 0.015, z + 0.05), (max(x0, x1), y0 + 0.015, z + h - 0.06), "Villa_Glass")
            rail.box((min(x0, x1), y0 - 0.03, z + h - 0.06), (max(x0, x1), y0 + 0.03, z + h), "Villa_BrushedMetal")
        else:
            rail.box((x0 - 0.015, min(y0, y1), z + 0.05), (x0 + 0.015, max(y0, y1), z + h - 0.06), "Villa_Glass")
            rail.box((x0 - 0.03, min(y0, y1), z + h - 0.06), (x0 + 0.03, max(y0, y1), z + h), "Villa_BrushedMetal")
    glass_rail(-2.8, 8, 4, 8, H1)
    glass_rail(-2.8, 8, -2.8, 16, H1)
    for z in (H1, H2): glass_rail(ax0, 3.02, 10.85, 3.02, z)            # landing edge over the stairwell
    glass_rail(ax0 - 0.02, 0.3, ax0 - 0.02, 1.55, H2)                   # top floor: no flight A above
    for s in range(2):                                                   # handrails along the glass and the outer wall
        b = s * H1
        for i in range(8):
            x = ax0 + i * run
            rail.box((x, 0.32, b + rise * (i + 1) + 0.86), (x + run, 0.36, b + rise * (i + 1) + 0.9), "Villa_BrushedMetal")
            x1 = ax0 + 8 * run - i * run
            rail.box((x1 - run, 2.96, b + rise * (10 + i) + 0.86), (x1, 3.0, b + rise * (10 + i) + 0.9), "Villa_BrushedMetal")
    glass_rail(-11, -2, -4, -2, H1); glass_rail(-11, -2, -11, 0, H1); glass_rail(-4, -2, -4, 0, H1)
    glass_rail(4, 18, 11, 18, H1); glass_rail(11, 16, 11, 18, H1); glass_rail(4, 16, 4, 18, H1)
    # Roof terraces: glass on top of the parapets, and around the (non-walkable) green roof.
    for a, b in (((X0 + 0.1, -0.2), (-4, -0.2)), ((X0 - 0.2, 0), (X0 - 0.2, 16)), ((X0, 16.2), (X1, 16.2)), ((X1 + 0.2, 8), (X1 + 0.2, 16))):
        glass_rail(a[0], a[1], b[0], b[1], HROOF + 0.6, 0.5)
    glass_rail(-4, 8.3, -4, 16, HROOF); glass_rail(4, 8.3, 4, 16, HROOF)

    # Garage (structure), roll-up door is a separate animated object ------------------------------------
    gar = Part("Garage", A)
    gar.box((-10.9, 7.1, 0), (-4.2, 7.4, 2.2), "Villa_Concrete")        # storage shelf wall
    gar.box((-10.6, 1.0, 0), (-10.3, 6.8, 1.8), "Villa_Aluminium")       # tool racks
    MANIFEST["garage"] = {"door": {"min": [-10.5, -0.15, 0], "max": [-4.5, 0.15, 2.6]}, "car": [-8.9, 3.6, 0], "car_yaw": 180, "spare_bay": [-5.9, 3.6, 0]}
    # Parking: family SUV + 1 sports car in the garage, 2 sports cars on the display pad in front of the
    # glazed stair tower (x 2–11.5, y −8.6…−0.8), which connects to the front path and the pedestrian gate.
    # flat x, y, yaw (yaw 180 = nose towards the street)
    MANIFEST["cars"] = {"names": ["FamilySUV", "SportsCar_Red", "SportsCar_Yellow", "SportsCar_Blue"],
                        "spots": [-8.9, 3.6, 180, -5.9, 3.6, 180, 5.0, -4.7, 180, 8.9, -4.7, 180]}

    # Pool (embedded: basin below ground, coping flush with the terrace) -------------------------------
    pool = Part("SwimmingPool", A)
    px0, px1, py0, py1, depth_shallow, depth_deep = -5.0, 5.0, 22.0, 26.5, 1.1, 2.0
    pool.box((px0 - 0.3, py0 - 0.3, -depth_deep - 0.3), (px1 + 0.3, py1 + 0.3, -depth_deep), "Villa_PoolTile")
    for (a, b) in (((px0 - 0.3, py0 - 0.3), (px1 + 0.3, py0)), ((px0 - 0.3, py1), (px1 + 0.3, py1 + 0.3)), ((px0 - 0.3, py0), (px0, py1)), ((px1, py0), (px1 + 0.3, py1))):
        pool.box((a[0], a[1], -depth_deep), (b[0], b[1], -0.02), "Villa_PoolTile")
    # Floor: shallow end (west) → slope → deep end (east), as steps of 10 cm.
    for i in range(10):
        x = px0 + i * (px1 - px0) / 10
        d = depth_shallow + (depth_deep - depth_shallow) * max(0, (i - 3)) / 6
        pool.box((x, py0, -depth_deep), (x + (px1 - px0) / 10 + 0.001, py1, -min(d, depth_deep)), "Villa_PoolTile")
    for k in range(3):                                   # entry steps at the shallow end
        pool.box((px0, py0, -depth_shallow), (px0 + 0.4 * (3 - k), py0 + 1.6, -depth_shallow + 0.27 * (k + 1)), "Villa_PoolTile")
    # Coping (stone) around the edge.
    for (a, b) in (((px0 - 0.6, py0 - 0.6), (px1 + 0.6, py0)), ((px0 - 0.6, py1), (px1 + 0.6, py1 + 0.6)), ((px0 - 0.6, py0), (px0, py1)), ((px1, py0), (px1 + 0.6, py1))):
        pool.box((a[0], a[1], -0.02), (b[0], b[1], 0.02), "Villa_Stone")
    MANIFEST["pool"] = {"min": [px0, py0, -depth_deep], "max": [px1, py1, -0.12], "water_level": -0.12, "shallow": depth_shallow, "deep": depth_deep,
                        "ladder_bottom": [px1 - 0.55, (py0 + py1) / 2, -0.12], "ladder_top": [px1 + 0.55, (py0 + py1) / 2, 0.02],
                        "steps": [px0 + 0.6, py0 + 0.8, -0.3], "lights": [[px0 + 2.5, py0 + 0.05, -0.7], [px0 + 7.5, py0 + 0.05, -0.9], [px0 + 2.5, py1 - 0.05, -0.7], [px0 + 7.5, py1 - 0.05, -0.9]]}

    # Rooms for navigation / NPCs.
    for r in (("Garage", 0, -11, 0, -4, 7.5, "garage"), ("Laundry", 0, -11, 7.5, -7.5, 10.5, "laundry"), ("GuestBathroom", 0, -7.5, 7.5, -4, 10.5, "bathroom"),
              ("GuestBedroom", 0, -11, 10.5, -4, 16, "bedroom"), ("Foyer", 0, -4, 0, 1.5, 4.5, "foyer"), ("Study", 0, 1.5, 0, 5, 4.5, "study"),
              ("LivingRoom", 0, -4, 4.5, 4, 16, "living"), ("Dining", 0, 4, 4.5, 11, 9, "dining"), ("Kitchen", 0, 4, 9, 11, 16, "kitchen"), ("StairHall", 0, 5, 0, 7.7, 4.5, "stairs"),
              ("MasterBedroom", 1, -11, 0, -4, 7.5, "bedroom"), ("WalkInWardrobe", 1, -11, 7.5, -8, 10.5, "wardrobe"), ("MasterBathroom", 1, -8, 7.5, -4, 10.5, "bathroom"),
              ("BedroomA", 1, -11, 10.5, -4, 16, "bedroom"), ("FamilyLounge", 1, -4, 0, 1.5, 4.5, "lounge"), ("SharedBathroom", 1, 1.5, 0, 5, 4.5, "bathroom"),
              ("Gallery", 1, -4, 4.5, 11, 8, "gallery"), ("SideGallery", 1, -4, 8, -2.8, 16, "gallery"), ("Landing", 1, 5, 0, 7.7, 4.5, "stairs"), ("BedroomB", 1, 4, 8, 11, 16, "bedroom"),
              ("MediaRoom", 2, -4, 0, 3, 4.5, "media"), ("Gym", 2, -4, 4.5, 3, 8, "gym"), ("SkyLounge", 2, 3, 4.5, 11, 8, "lounge"), ("PowderRoom", 2, 3, 0, 5, 2.5, "bathroom"),
              ("TopLanding", 2, 5, 0, 7.7, 4.5, "stairs"), ("RoofTerraceWest", 2, -11, 0, -4, 16, "terrace"), ("RoofTerraceEast", 2, 4, 8, 11, 16, "terrace")):
        room(*r)

    # Doors: one object per leaf, pivot on the hinge ---------------------------------------------------
    def door(name, hinge, width, along, z, opens, mat="Villa_Wood", height=2.08, lockable=False, room_name=""):
        """hinge: (x, y); along: '+x', '-x', '+y', '-y' direction the closed leaf runs from the hinge;
        opens: +1 / -1 = rotation sign about +Z when opening (90°)."""
        hx, hy = hinge
        p = Part("Door_" + name, D, pivot=(hx, hy, z))
        t = 0.045
        if along in ("+x", "-x"):
            s = 1 if along == "+x" else -1
            x0, x1 = sorted((hx + 0.02 * s, hx + (width - 0.02) * s))
            p.box((x0, hy - t, z), (x1, hy + t, z + height), mat)
            p.box((hx + (width - 0.12) * s - 0.01, hy - t - 0.06, z + 1.0), (hx + (width - 0.12) * s + 0.01, hy + t + 0.06, z + 1.04), "Villa_BrushedMetal")
        else:
            s = 1 if along == "+y" else -1
            y0, y1 = sorted((hy + 0.02 * s, hy + (width - 0.02) * s))
            p.box((hx - t, y0, z), (hx + t, y1, z + height), mat)
            p.box((hx - t - 0.06, hy + (width - 0.12) * s - 0.01, z + 1.0), (hx + t + 0.06, hy + (width - 0.12) * s + 0.01, z + 1.04), "Villa_BrushedMetal")
        MANIFEST["doors"].append({"name": "Door_" + name, "hinge": [hx, hy, z], "width": width, "along": along, "opens": opens, "lockable": lockable, "room": room_name})
    door("Entrance", (-4 + 1.95, 0), 1.6, "+x", 0, 1, "Villa_Slats", 2.55, True, "Foyer")
    door("GarageHall", (-4, 5.35), 0.9, "+y", 0, 1, room_name="Garage")
    door("Laundry", (X0 + 1.3, 7.5), 0.9, "+x", 0, 1, room_name="Laundry")
    door("GuestBedroom", (-4, 11.55), 0.9, "+y", 0, -1, room_name="GuestBedroom")
    door("GuestBathroom", (-4, 8.55), 0.9, "+y", 0, -1, lockable=True, room_name="GuestBathroom")
    door("Study", (1.5 + 0.3, 4.5), 0.9, "+x", 0, -1, room_name="Study")
    door("Master", (-4, 5.35), 0.9, "+y", H1, -1, room_name="MasterBedroom")
    door("Wardrobe", (X0 + 1.55, 7.5), 0.9, "+x", H1, -1, room_name="WalkInWardrobe")
    door("MasterBathroom", (X0 + 4.55, 7.5), 0.9, "+x", H1, -1, lockable=True, room_name="MasterBathroom")
    door("BedroomA", (-4, 12.55), 0.9, "+y", H1, -1, room_name="BedroomA")
    door("SharedBathroom", (1.5 + 0.3, 4.5), 0.9, "+x", H1, -1, lockable=True, room_name="SharedBathroom")
    door("BedroomB", (4 + 1.05, 8), 0.9, "+x", H1, 1, room_name="BedroomB")
    door("Terrace", (4 + 0.8, 16), 2.4, "+x", 0, 1, "Villa_Glass", 2.55, room_name="Kitchen")
    door("RearBalcony", (4 + 2.0, 16), 3.0, "+x", H1, 1, "Villa_Glass", 2.55, room_name="BedroomB")
    door("FrontBalcony", (X0 + 1.5, 0), 4.0, "+x", H1, -1, "Villa_Glass", 2.55, room_name="MasterBedroom")
    H2 = 2 * H1
    door("Media", (-4 + 1.55, 4.5), 0.9, "+x", H2, 1, room_name="MediaRoom")
    door("PowderRoom", (5, 0.8), 0.8, "+y", H2, -1, lockable=True, room_name="PowderRoom")
    door("MediaTerrace", (-4, 1.45), 1.6, "+y", H2, 1, "Villa_Glass", 2.45, room_name="MediaRoom")
    door("GymTerrace", (-4, 5.05), 2.4, "+y", H2, 1, "Villa_Glass", 2.55, room_name="Gym")
    door("SkyTerrace", (6.0, 8), 3.0, "+x", H2, 1, "Villa_Glass", 2.55, room_name="SkyLounge")

    # Glass-and-steel lift: cab (moves), two cab door leaves, two landing door leaves per floor.
    # Pivots: the cab at its floor centre; door leaves at their own centre when closed.
    cab = Part("Elevator_Cab", D, pivot=(4.0, 3.5, 0))
    cab.box((3.17, 2.67, -0.12), (4.83, 4.33, 0), "Villa_Stone")                                   # floor
    cab.box((3.17, 2.67, 2.45), (4.83, 4.33, 2.55), "Villa_BrushedMetal")                         # roof
    cab.box((3.4, 2.9, 2.43), (4.6, 4.1, 2.45), "Villa_LightPanel")                               # ceiling light
    cab.box((3.17, 2.67, 0), (3.22, 4.33, 2.45), "Villa_Glass")                                    # back: glass (looks into the shaft)
    cab.box((3.22, 2.67, 0), (4.78, 2.72, 2.45), "Villa_BrushedMetal")                            # sides
    cab.box((3.22, 4.28, 0), (4.78, 4.33, 2.45), "Villa_BrushedMetal")
    cab.box((4.78, 2.67, 0), (4.83, 3.0, 2.45), "Villa_BrushedMetal")                             # front posts + header
    cab.box((4.78, 4.0, 0), (4.83, 4.33, 2.45), "Villa_BrushedMetal")
    cab.box((4.78, 3.0, 2.12), (4.83, 4.0, 2.45), "Villa_BrushedMetal")
    cab.box((3.24, 2.75, 0.9), (3.28, 4.25, 0.94), "Villa_BrushedMetal")                          # handrail
    cab.box((4.7, 4.05, 0.95), (4.76, 4.25, 1.45), "Villa_Black")                                 # button panel
    for k in range(3): cab.box((4.69, 4.12, 1.05 + k * 0.13), (4.7, 4.18, 1.11 + k * 0.13), "Villa_LightPanel")
    for name, y0, y1 in (("L", 3.0, 3.52), ("R", 3.48, 4.0)):
        leaf = Part("Elevator_CabDoor" + name, D, pivot=(4.76, (y0 + y1) / 2, 0))
        leaf.box((4.74, y0, 0.0), (4.77, y1, 2.1), "Villa_BrushedMetal")
    for f, z in enumerate((0, H1, H2)):
        for name, y0, y1 in (("L", 3.0, 3.52), ("R", 3.48, 4.0)):
            leaf = Part("Elevator_Door_F%d_%s" % (f, name), D, pivot=(4.98, (y0 + y1) / 2, z))
            leaf.box((4.96, y0, z), (5.0, y1, z + 2.1), "Villa_BrushedMetal")
    MANIFEST["elevator"] = {"floors": [0, H1, H2], "names": ["Ground", "First", "Second"], "inside": [4.0, 3.5], "cab_min": [3.17, 2.67], "cab_max": [4.83, 4.33],
                            "door_slide": 0.5, "door_axis": [0, 1, 0], "hall": [5.6, 3.5], "call": [v for z in (0, H1, H2) for v in (5.11, 4.36, z + 1.15)],
                            "indicator": [v for z in (0, H1, H2) for v in (5.11, 3.5, z + 2.42)]}        # flat x,y,z triples (JsonUtility)

    # Garage roll-up door (rises) and the front gate (slides): separate animated objects.
    gd = Part("GarageDoor", D, pivot=(-7.5, 0, 0))
    for i in range(13): gd.box((-10.5, -0.18, i * 0.2), (-4.5, -0.1, i * 0.2 + 0.19), "Villa_Aluminium")
    gate = Part("FrontGate", D, pivot=(-7.5, -10.0, 0))
    for i in range(30): gate.box((-10.6 + i * 0.21, -10.05, 0.05), (-10.6 + i * 0.21 + 0.12, -9.95, 2.2), "Villa_Black")
    gate.box((-10.6, -10.07, 2.1), (-4.4, -9.93, 2.25), "Villa_Black")
    MANIFEST["gate"] = {"pivot": [-7.5, -10.0, 0], "slide": [-6.2, 0, 0]}

    # Interior furniture: one object per item, pivot at its base centre ------------------------------
    def fwd(yaw): return (-math.sin(math.radians(yaw)), math.cos(math.radians(yaw)))
    def item(name, x, y, z, parts, yaw=0):
        p = Part(name, I, pivot=(x, y, z))
        for mn, mx, mat in parts:
            p.box((x + mn[0], y + mn[1], z + mn[2]), (x + mx[0], y + mx[1], z + mx[2]), mat, yaw=yaw, about=(x, y, z))
        return p
    def sofa(name, x, y, z, length, yaw, room_name, fabric="Villa_Linen"):
        p = Part(name, I, pivot=(x, y, z))
        def rb(mn, mx, mat, r): rbox(p, (x + mn[0], y + mn[1], z + mn[2]), (x + mx[0], y + mx[1], z + mx[2]), mat, r, 3, yaw, (x, y, z))
        L = length / 2
        rb((-L - 0.18, -0.46, 0.06), (L + 0.18, 0.46, 0.26), fabric, 0.04)                        # upholstered base
        rb((-L - 0.2, -0.46, 0.06), (-L, 0.46, 0.64), fabric, 0.07)                               # arms
        rb((L, -0.46, 0.06), (L + 0.2, 0.46, 0.64), fabric, 0.07)
        rb((-L - 0.18, 0.24, 0.26), (L + 0.18, 0.46, 0.72), fabric, 0.06)                         # back frame
        for leg in ((-L - 0.12, -0.38), (L + 0.12, -0.38), (-L - 0.12, 0.38), (L + 0.12, 0.38)):
            p.box((x + leg[0] - 0.02, y + leg[1] - 0.02, z), (x + leg[0] + 0.02, y + leg[1] + 0.02, z + 0.06), "Villa_Black", yaw=yaw, about=(x, y, z))
        n = max(2, int(length / 0.75))
        for i in range(n):                                                                         # separate seat + back cushions
            c0 = -L + length * i / n + 0.01; c1 = -L + length * (i + 1) / n - 0.01
            rb((c0, -0.44, 0.26), (c1, 0.24, 0.44), fabric, 0.06)
            rb((c0, 0.06, 0.44), (c1, 0.26, 0.86), fabric, 0.07)
        for i, px in enumerate((-L + 0.25, L - 0.25)):                                            # throw pillows
            rb((px - 0.2, 0.0, 0.44), (px + 0.2, 0.12, 0.8), "Villa_FabricWarm" if i == 0 else "Villa_Fabric", 0.06)
        for i in range(n):
            off = -length / 2 + length * (i + 0.5) / n
            c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
            ax, ay = x + off * c - (-0.1) * s, y + off * s + (-0.1) * c
            anchor("Sofa", ax, ay, z, (yaw + 180) % 360, room_name, {"seat": 0.45})
    def lounger(name, x, y, z, yaw):
        p = Part(name, I, pivot=(x, y, z))
        def rb(mn, mx, mat, r): rbox(p, (x + mn[0], y + mn[1], z + mn[2]), (x + mx[0], y + mx[1], z + mx[2]), mat, r, 2, yaw, (x, y, z))
        for sx in (-0.36, 0.32):                                                                   # teak side rails
            rb((sx, -1.0, 0.22), (sx + 0.04, 1.0, 0.3), "Villa_Wood", 0.01)
        for lx_, ly_ in ((-0.34, -0.9), (0.34, -0.9), (-0.34, 0.9), (0.34, 0.9)):
            rb((lx_ - 0.03, ly_ - 0.03, 0), (lx_ + 0.03, ly_ + 0.03, 0.26), "Villa_Wood", 0.008)
        rb((-0.34, -0.98, 0.3), (0.34, 0.42, 0.4), "Villa_Cushion", 0.05)                           # flat cushion
        p.box((x - 0.34, y + 0.42, z + 0.3), (x + 0.34, y + 0.98, z + 0.34), "Villa_Cushion", yaw=yaw, about=(x, y, z))
        rbox(p, (x - 0.34, y + 0.4, z + 0.32), (x + 0.34, y + 0.5, z + 0.95), "Villa_Cushion", 0.05, 2, yaw - 0.0, (x, y, z))   # raised back
        rb((-0.25, 0.45, 0.82), (0.25, 0.6, 0.98), "Villa_Cushion", 0.06)                          # head pillow
    def chair(name, x, y, z, yaw, room_name, kind="Seat"):
        item(name, x, y, z, [((-0.22, -0.22, 0.42), (0.22, 0.22, 0.47), "Villa_Leather"), ((-0.22, 0.18, 0.47), (0.22, 0.22, 0.95), "Villa_Leather"),
                             ((-0.2, -0.2, 0), (-0.17, -0.17, 0.42), "Villa_BrushedMetal"), ((0.17, -0.2, 0), (0.2, -0.17, 0.42), "Villa_BrushedMetal"),
                             ((-0.2, 0.17, 0), (-0.17, 0.2, 0.42), "Villa_BrushedMetal"), ((0.17, 0.17, 0), (0.2, 0.2, 0.42), "Villa_BrushedMetal")], yaw)
        anchor(kind, x, y, z, (yaw + 180) % 360, room_name, {"seat": 0.47})
    def bed(name, x, y, z, width, yaw, room_name, sleepers):
        item(name, x, y, z, [((-width / 2, -1.0, 0), (width / 2, 1.0, 0.3), "Villa_Wood"), ((-width / 2 + 0.03, -0.97, 0.3), (width / 2 - 0.03, 0.97, 0.52), "Villa_FabricWarm"),
                             ((-width / 2 - 0.05, 0.95, 0), (width / 2 + 0.05, 1.08, 1.1), "Villa_Fabric"),
                             ((-width / 2 + 0.1, 0.55, 0.52), (-0.05, 0.85, 0.66), "Villa_Ceramic"), ((0.05, 0.55, 0.52), (width / 2 - 0.1, 0.85, 0.66), "Villa_Ceramic")], yaw)
        for k in range(sleepers):
            off = (k - (sleepers - 1) / 2) * width / max(1, sleepers)
            c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
            anchor("Bed", x + off * c, y + off * s, z, (yaw + 180) % 360, room_name, {"seat": 0.55, "head": yaw})
    def desk(name, x, y, z, yaw, room_name, computer=True):
        parts = [((-0.7, -0.35, 0.72), (0.7, 0.35, 0.76), "Villa_Wood"), ((-0.68, -0.33, 0), (-0.64, 0.33, 0.72), "Villa_BrushedMetal"), ((0.64, -0.33, 0), (0.68, 0.33, 0.72), "Villa_BrushedMetal")]
        if computer: parts += [((-0.3, 0.12, 0.8), (0.3, 0.16, 1.15), "Villa_Screen"), ((-0.03, 0.15, 0.76), (0.03, 0.2, 0.82), "Villa_BrushedMetal"), ((-0.22, -0.18, 0.76), (0.22, -0.04, 0.78), "Villa_Black")]
        item(name, x, y, z, parts, yaw)
        s, c = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))
        chair(name + "Chair", x + 0.7 * s, y - 0.7 * c, z, (yaw + 180) % 360, room_name, "Desk")
        anchor("Computer" if computer else "Study", x, y, z, yaw, room_name)
    def wardrobe(name, x, y, z, length, yaw, room_name):
        item(name, x, y, z, [((-length / 2, -0.3, 0), (length / 2, 0.3, 2.4), "Villa_Wood"), ((-0.02, -0.31, 0.1), (0.02, -0.3, 2.3), "Villa_Black")], yaw)
        f = fwd(yaw)
        anchor("Wardrobe", x - f[0] * 0.75, y - f[1] * 0.75, z, yaw, room_name)

    # Ground floor.
    sofa("Sofa_Living", 0, 11.0, 0, 3.0, 180, "LivingRoom")
    sofa("Sofa_LivingSide", -2.9, 12.9, 0, 2.0, 90, "LivingRoom")
    cof = Part("CoffeeTable", I, pivot=(0, 12.4, 0))
    rbox(cof, (-0.75, 12.0, 0.32), (0.75, 12.8, 0.4), "Villa_Travertine", 0.02, 2)
    rbox(cof, (-0.55, 12.15, 0.0), (0.55, 12.65, 0.32), "Villa_Travertine", 0.01, 1)
    cof.box((-0.4, 12.3, 0.4), (-0.1, 12.5, 0.46), "Villa_Wood"); cof.box((-0.38, 12.32, 0.46), (-0.12, 12.48, 0.5), "Villa_FabricWarm")   # books
    cof.sphere((0.35, 12.4, 0.48), 0.08, "Villa_Ceramic", 1.0, 2)
    for k, (ax_, ay_, yaw_) in enumerate(((2.6, 13.3, 225), (-2.35, 14.75, 135))):                  # accent armchairs (no seat anchors: decor)
        ac = Part("Armchair_Living_%d" % k, I, pivot=(ax_, ay_, 0))
        def rb(mn, mx, mat, r): rbox(ac, (ax_ + mn[0], ay_ + mn[1], mn[2]), (ax_ + mx[0], ay_ + mx[1], mx[2]), mat, r, 3, yaw_, (ax_, ay_, 0))
        rb((-0.42, -0.4, 0.12), (0.42, 0.4, 0.42), "Villa_Cushion", 0.06); rb((-0.42, 0.22, 0.42), (0.42, 0.4, 0.82), "Villa_Cushion", 0.07)
        rb((-0.45, -0.4, 0.12), (-0.36, 0.4, 0.6), "Villa_Cushion", 0.04); rb((0.36, -0.4, 0.12), (0.45, 0.4, 0.6), "Villa_Cushion", 0.04)
        for lx_, ly_ in ((-0.36, -0.34), (0.36, -0.34), (-0.36, 0.34), (0.36, 0.34)): rb((lx_ - 0.025, ly_ - 0.025, 0), (lx_ + 0.025, ly_ + 0.025, 0.12), "Villa_Wood", 0.01)
        anchor("Seat", ax_, ay_, 0, (yaw_ + 180) % 360, "LivingRoom", {"seat": 0.45})
    for k, (sx_, sy_) in enumerate(((-1.85, 10.95), (1.85, 10.95))):                           # side tables + lamps
        stb = Part("SideTable_%d" % k, I, pivot=(sx_, sy_, 0))
        stb.cylinder((sx_, sy_, 0), 0.22, 0.5, "Villa_Wood", 20)
        stb.cylinder((sx_, sy_, 0.5), 0.05, 0.3, "Villa_Ceramic", 12)
        stb.cylinder((sx_, sy_, 0.78), 0.17, 0.22, "Villa_Linen", 20)
        light("lamp", sx_, sy_, 0.9, "LivingRoom", (1, 0.78, 0.55), 0.7, 3.5)
    # Sheer curtains either side of the glass wall (pleated)
    cu = Part("Curtains", I, pivot=(0, 15.8, 0))
    for side in (-1, 1):
        for k in range(10):
            x0 = side * (3.55 - k * 0.11) - 0.05; dz = 0.03 if k % 2 else 0.0
            cu.box((min(x0, x0 + 0.1), 15.66 + dz, 0.02), (max(x0, x0 + 0.1), 15.7 + dz, 6.3), "Villa_Curtain")
    cu.box((-3.75, 15.72, 6.32), (3.75, 15.78, 6.36), "Villa_Aluminium")                       # curtain track
    item("Rug_Living", 0, 12.4, 0, [((-2.0, -1.5, 0), (2.0, 1.5, 0.012), "Villa_FabricWarm")])
    item("TVConsole", 0, 15.3, 0, [((-1.4, -0.25, 0), (1.4, 0.25, 0.45), "Villa_Wood"), ((-1.1, -0.04, 0.75), (1.1, 0.04, 1.98), "Villa_Screen")])
    anchor("TV", 0, 15.0, 0, 180, "LivingRoom")
    anchor("Family", 0, 9.6, 0, 0, "LivingRoom")
    kc = Part("KitchenCabinets", I, pivot=(8.0, 15.55, 0))
    kc.box((5.1, 15.85 - 0.6, 0.0), (10.9, 15.85, 0.1), "Villa_Black")                          # recessed plinth
    for i in range(9):                                                                          # base cabinet doors (0.62 m) with 4 mm gaps
        x0 = 5.12 + i * 0.64
        rbox(kc, (x0, 15.25 - 0.02, 0.1), (x0 + 0.62, 15.85, 0.88), "Villa_Wood", 0.004, 1)
        kc.box((x0 + 0.08, 15.2, 0.8), (x0 + 0.54, 15.225, 0.815), "Villa_BrushedMetal")          # bar handle
    rbox(kc, (5.05, 15.2, 0.88), (10.95, 15.86, 0.92), "Villa_Marble", 0.006, 1)                # stone worktop
    kc.box((5.1, 15.82, 0.92), (10.9, 15.86, 1.5), "Villa_Marble")                              # backsplash
    for i in range(9):                                                                          # wall cabinets
        x0 = 5.12 + i * 0.64
        rbox(kc, (x0, 15.5, 1.55), (x0 + 0.62, 15.86, 2.3), "Villa_Wood", 0.004, 1)
    kc.box((5.12, 15.5, 1.53), (10.9, 15.84, 1.55), "Villa_LightPanel")                        # under-cabinet LED strip
    MANIFEST["lights"].append({"kind": "task", "pos": [8.0, 15.6, 1.45], "room": "Kitchen", "colour": (1, 0.9, 0.75), "intensity": 0.8, "range": 3})
    # tall unit with a built-in oven at the end of the run
    tu = Part("Oven", I, pivot=(10.4, 14.6, 0))
    rbox(tu, (10.1, 14.3, 0), (10.88, 14.95, 2.3), "Villa_Wood", 0.004, 1)
    tu.box((10.08, 14.35, 0.85), (10.1, 14.9, 1.45), "Villa_Screen")                            # oven glass door
    tu.box((10.06, 14.4, 1.38), (10.08, 14.85, 1.4), "Villa_BrushedMetal")
    tu.box((10.08, 14.35, 1.5), (10.1, 14.9, 1.82), "Villa_Screen")                             # microwave
    ct = Part("Cooktop", I, pivot=(7.0, 15.55, 0.92))
    ct.box((6.62, 15.3, 0.92), (7.38, 15.8, 0.93), "Villa_Screen")
    for (bx, by) in ((6.8, 15.42), (7.2, 15.42), (6.8, 15.68), (7.2, 15.68)):
        ct.cylinder((bx, by, 0.93), 0.09, 0.002, "Villa_BrushedMetal", 20)
    ct.box((6.75, 15.68, 2.3), (7.25, 15.86, 2.45), "Villa_BrushedMetal")                       # extractor hood
    sk = Part("Faucet", I, pivot=(9.4, 15.75, 0.92))
    sk.box((9.05, 15.35, 0.75), (9.75, 15.7, 0.921), "Villa_BrushedMetal")                       # undermount basin
    sk.cylinder((9.4, 15.75, 0.92), 0.025, 0.32, "Villa_BrushedMetal", 12)
    tube(sk, (9.4, 15.75, 1.24), (9.4, 15.55, 1.24), 0.015, 0.015, "Villa_BrushedMetal", 8)
    tube(sk, (9.4, 15.55, 1.24), (9.4, 15.55, 1.12), 0.012, 0.012, "Villa_BrushedMetal", 8)
    fr = Part("Fridge", I, pivot=(10.4, 13.4, 0))
    rbox(fr, (10.05, 13.02, 0), (10.88, 13.78, 2.05), "Villa_BrushedMetal", 0.02, 2)
    fr.box((10.03, 13.39, 0.05), (10.06, 13.41, 2.0), "Villa_Black")                            # French-door split
    for hy in (13.32, 13.48):
        tube(fr, (10.0, hy, 0.9), (10.0, hy, 1.5), 0.012, 0.012, "Villa_BrushedMetal", 8)
    
    anchor("Fridge", 9.6, 13.4, 0, 270, "Kitchen")
    anchor("Stove", 7.0, 14.85, 0, 0, "Kitchen")
    anchor("Sink", 9.4, 14.85, 0, 0, "Kitchen")
    isl = Part("Island", I, pivot=(7.5, 12.2, 0))
    rbox(isl, (6.0, 11.65, 0.0), (9.0, 12.75, 0.9), "Villa_Wood", 0.005, 1)
    rbox(isl, (5.9, 11.6, 0.9), (9.1, 12.8, 0.95), "Villa_Marble", 0.006, 1)                   # worktop
    isl.box((5.9, 11.6, 0.0), (5.95, 12.8, 0.9), "Villa_Marble"); isl.box((9.05, 11.6, 0.0), (9.1, 12.8, 0.9), "Villa_Marble")   # waterfall ends
    for k, fx in enumerate((6.4, 7.5, 8.6)):                                                   # bowl + books as accessories
        if k == 1: isl.sphere((fx, 12.3, 1.0), 0.13, "Villa_Ceramic", 0.45, 2)
    isl.box((8.5, 12.4, 0.95), (8.75, 12.6, 1.0), "Villa_Wood")
    anchor("Counter", 7.5, 13.3, 0, 180, "Kitchen")
    for i, sx in enumerate((6.6, 7.5, 8.4)):
        st_ = Part("Stool_%d" % i, I, pivot=(sx, 11.25, 0))
        rbox(st_, (sx - 0.2, 11.07, 0.6), (sx + 0.2, 11.43, 0.68), "Villa_Leather", 0.035, 3)
        rbox(st_, (sx - 0.2, 11.38, 0.68), (sx + 0.2, 11.43, 0.9), "Villa_Leather", 0.02, 2)
        st_.cylinder((sx, 11.25, 0.0), 0.02, 0.6, "Villa_BrushedMetal", 10)
        st_.cylinder((sx, 11.25, 0.0), 0.18, 0.015, "Villa_BrushedMetal", 20)
        st_.cylinder((sx, 11.25, 0.25), 0.16, 0.012, "Villa_BrushedMetal", 20)
        anchor("Seat", sx, 11.25, 0, 0, "Kitchen", {"seat": 0.68})
    item("DiningTable", 7.5, 6.75, 0, [((-1.3, -0.55, 0.72), (1.3, 0.55, 0.77), "Villa_Wood"), ((-1.2, -0.05, 0), (-1.1, 0.05, 0.72), "Villa_BrushedMetal"), ((1.1, -0.05, 0), (1.2, 0.05, 0.72), "Villa_BrushedMetal")])
    for i, (cx, cy, yaw) in enumerate(((6.7, 5.8, 180), (8.3, 5.8, 180), (6.7, 7.7, 0), (8.3, 7.7, 0), (5.75, 6.75, 90), (9.25, 6.75, 270))):
        chair("DiningChair_%d" % i, cx, cy, 0, yaw, "Dining", "DiningSeat")
    desk("Desk_Study", 3.25, 0.95, 0, 180, "Study")
    item("Bookshelf_Study", 4.7, 1.3, 0, [((-0.18, -0.9, 0), (0.18, 0.9, 2.3), "Villa_Wood")])
    bed("Bed_Guest", -7.5, 14.7, 0, 1.6, 0, "GuestBedroom", 2)
    wardrobe("Wardrobe_Guest", -10.55, 12.3, 0, 1.8, 90, "GuestBedroom")
    item("Toilet_Guest", -5.0, 10.1, 0, [((-0.2, -0.3, 0), (0.2, 0.3, 0.42), "Villa_Ceramic"), ((-0.2, 0.2, 0.42), (0.2, 0.3, 0.8), "Villa_Ceramic")])
    item("Vanity_Guest", -6.6, 10.2, 0, [((-0.5, -0.25, 0), (0.5, 0.25, 0.85), "Villa_Wood"), ((-0.25, -0.15, 0.85), (0.25, 0.15, 0.9), "Villa_Ceramic")])
    anchor("Washbasin", -6.6, 9.6, 0, 0, "GuestBathroom")
    item("Washer", -10.5, 9.8, 0, [((-0.3, -0.3, 0), (0.3, 0.3, 0.85), "Villa_Ceramic"), ((-0.18, -0.31, 0.28), (0.18, -0.3, 0.64), "Villa_Glass")], 90)
    item("Dryer", -10.5, 8.4, 0, [((-0.3, -0.3, 0), (0.3, 0.3, 0.85), "Villa_Ceramic")], 90)
    anchor("Laundry", -9.9, 9.1, 0, 90, "Laundry")
    item("ConsoleTable_Foyer", -1.25, 0.45, 0, [((-0.8, -0.18, 0), (0.8, 0.18, 0.85), "Villa_Wood")])
    item("Rug_Foyer", -1.25, 2.4, 0, [((-1.0, -1.4, 0), (1.0, 1.4, 0.01), "Villa_FabricWarm")])
    # Upper floor.
    bed("Bed_Master", -7.5, 1.4, H1, 1.9, 0, "MasterBedroom", 2)
    item("Nightstand_MasterL", -8.9, 0.5, H1, [((-0.25, -0.2, 0), (0.25, 0.2, 0.5), "Villa_Wood")])
    item("Nightstand_MasterR", -6.1, 0.5, H1, [((-0.25, -0.2, 0), (0.25, 0.2, 0.5), "Villa_Wood")])
    sofa("Armchair_Master", -5.0, 6.4, H1, 0.8, 270, "MasterBedroom")
    wardrobe("Wardrobe_Master", -9.5, 10.1, H1, 2.6, 0, "WalkInWardrobe")
    wardrobe("Wardrobe_Master2", -10.55, 8.9, H1, 1.6, 90, "WalkInWardrobe")
    item("Bathtub", -6.0, 10.0, H1, [((-0.85, -0.38, 0), (0.85, 0.38, 0.55), "Villa_Ceramic")])
    item("Vanity_Master", -7.5, 7.85, H1, [((-0.4, -0.25, 0), (0.4, 0.25, 0.85), "Villa_Marble")])
    anchor("Washbasin", -7.5, 8.4, H1, 180, "MasterBathroom")
    bed("Bed_A", -7.5, 14.7, H1, 1.2, 0, "BedroomA", 1)
    desk("Desk_A", -10.45, 11.8, H1, 90, "BedroomA")
    wardrobe("Wardrobe_A", -4.42, 11.9, H1, 1.6, 270, "BedroomA")
    bed("Bed_B", 9.7, 13.2, H1, 1.2, 270, "BedroomB", 1)
    desk("Desk_B", 5.0, 13.5, H1, 90, "BedroomB")
    wardrobe("Wardrobe_B", 8.3, 8.42, H1, 1.8, 180, "BedroomB")
    sofa("Sofa_Lounge", -1.25, 3.6, H1, 2.4, 0, "FamilyLounge")
    item("TV_Lounge", -1.25, 0.35, H1, [((-0.9, -0.05, 0.7), (0.9, 0.05, 1.7), "Villa_Screen"), ((-1.1, -0.2, 0), (1.1, 0.2, 0.45), "Villa_Wood")])
    anchor("TV", -1.25, 0.8, H1, 0, "FamilyLounge")
    item("Shower_Shared", 4.3, 0.9, H1, [((-0.6, -0.6, 0), (0.6, 0.6, 0.05), "Villa_Tile"), ((-0.6, 0.58, 0), (0.6, 0.6, 2.0), "Villa_Glass")])
    item("Toilet_Shared", 2.0, 3.9, H1, [((-0.2, -0.3, 0), (0.2, 0.3, 0.42), "Villa_Ceramic")])
    item("Vanity_Shared", 2.45, 0.4, H1, [((-0.45, -0.22, 0), (0.45, 0.22, 0.85), "Villa_Marble")])
    anchor("Washbasin", 2.45, 0.95, H1, 180, "SharedBathroom")
    for i, pp in enumerate([(-3.4, 15.4, 0), (3.4, 15.4, 0), (-3.4, 5.2, H1)]):
        item("Plant_%d" % i, pp[0], pp[1], pp[2], [((-0.25, -0.25, 0), (0.25, 0.25, 0.5), "Villa_Concrete")])
        tropical("Plant_Leaves_%d" % i, I, (pp[0], pp[1], pp[2] + 0.48), 1.3, 800 + i, 8)
    # Third floor: home cinema, gym, sky lounge bar, powder room; roof terraces.
    H2 = 2 * H1
    item("TV_Media", -0.5, 0.25, H2, [((-1.8, -0.04, 0.55), (1.8, 0.04, 2.55), "Villa_Screen"), ((-2.0, -0.1, 0), (2.0, 0.2, 0.35), "Villa_Wood")])
    anchor("TV", -0.5, 1.2, H2, 0, "MediaRoom")
    sofa("Sofa_Media", -0.5, 2.4, H2, 3.0, 0, "MediaRoom")
    sofa("Sofa_Media2", -0.5, 3.7, H2, 3.0, 0, "MediaRoom")
    item("Rug_Media", -0.5, 2.3, H2, [((-2.2, -1.9, 0), (2.2, 1.9, 0.012), "Villa_Fabric")])
    item("Treadmill", -3.2, 6.8, H2, [((-0.4, -0.9, 0), (0.4, 0.9, 0.25), "Villa_Black"), ((-0.4, -0.95, 0.25), (0.4, -0.85, 1.3), "Villa_BrushedMetal")])
    anchor("Exercise", -3.2, 6.0, H2, 180, "Gym")
    item("WeightBench", -1.2, 6.4, H2, [((-0.25, -0.7, 0.35), (0.25, 0.7, 0.45), "Villa_Leather"), ((-0.05, -0.6, 0), (0.05, 0.6, 0.35), "Villa_BrushedMetal")])
    item("YogaMats", 1.2, 6.3, H2, [((-0.35, -0.95, 0), (0.35, 0.95, 0.01), "Villa_Fabric"), ((0.45, -0.95, 0), (1.15, 0.95, 0.01), "Villa_FabricWarm")])
    anchor("Exercise", 1.2, 6.3, H2, 0, "Gym")
    item("Mirror_Gym", -0.5, 4.62, H2, [((-1.5, -0.02, 0.3), (1.5, 0.02, 2.2), "Villa_Glass")])
    item("Bar", 8.0, 5.0, H2, [((-1.6, -0.3, 0), (1.6, 0.3, 1.05), "Villa_Wood"), ((-1.7, -0.35, 1.05), (1.7, 0.35, 1.1), "Villa_Marble")])
    for i, sx in enumerate((7.0, 8.0, 9.0)):
        item("Stool_Bar_%d" % i, sx, 5.75, H2, [((-0.18, -0.18, 0.7), (0.18, 0.18, 0.76), "Villa_Leather"), ((-0.03, -0.03, 0), (0.03, 0.03, 0.7), "Villa_BrushedMetal")])
        anchor("Seat", sx, 5.75, H2, 180, "SkyLounge", {"seat": 0.76})
    anchor("Counter", 8.0, 4.3, H2, 0, "SkyLounge")
    sofa("Sofa_Sky", 5.2, 6.4, H2, 2.2, 90, "SkyLounge")
    item("Toilet_Powder", 3.5, 0.5, H2, [((-0.2, -0.3, 0), (0.2, 0.3, 0.42), "Villa_Ceramic")])
    item("Vanity_Powder", 4.4, 2.1, H2, [((-0.4, -0.2, 0), (0.4, 0.2, 0.85), "Villa_Marble")])
    anchor("Washbasin", 4.4, 1.55, H2, 0, "PowderRoom")
    # West roof terrace: outdoor sofas, loungers and planters around a parasol; east terrace: dining.
    sofa("Sofa_TerraceW", -7.5, 3.0, H2, 2.6, 180, "RoofTerraceWest")
    item("CoffeeTable_Terrace", -7.5, 4.4, H2, [((-0.6, -0.4, 0), (0.6, 0.4, 0.38), "Villa_Wood")])
    for i, ly in enumerate((9.5, 11.5, 13.5)):
        lounger("Lounger_Roof_%d" % i, -8.5, ly, H2, 270)
        anchor("Lounger", -8.3, ly, H2, 90, "RoofTerraceWest", {"seat": 0.38})
    for i, py in enumerate((1.0, 7.0, 15.0)):
        item("Planter_Roof_%d" % i, -10.4, py, H2, [((-0.35, -0.8, 0), (0.35, 0.8, 0.6), "Villa_Concrete")])
        tropical("Planter_Roof_Leaves_%d" % i, I, (-10.4, py, H2 + 0.6), 1.5, 900 + i, 9)
        anchor("Plants", -9.7, py, H2, 270, "RoofTerraceWest")
    item("Table_TerraceE", 7.5, 12.0, H2, [((-1.0, -0.5, 0.72), (1.0, 0.5, 0.77), "Villa_Wood"), ((-0.05, -0.05, 0), (0.05, 0.05, 0.72), "Villa_BrushedMetal")])
    for i, (cx, cy, yaw) in enumerate(((7.0, 11.2, 180), (8.0, 11.2, 180), (7.0, 12.8, 0), (8.0, 12.8, 0))):
        chair("Chair_TerraceE_%d" % i, cx, cy, H2, yaw, "RoofTerraceEast", "DiningSeat")
    MANIFEST["umbrellas"] = {"parasols": [-2.5, 28.6, 0, 2.5, 28.6, 0, -7.5, 5.6, H2, 10.0, 14.5, H2], "stand": [-3.55, 0.45, 0]}
    item("UmbrellaStand", -3.55, 0.45, 0, [((-0.13, -0.13, 0), (0.13, 0.13, 0.55), "Villa_BrushedMetal")])
    cu = Part("ClosedUmbrella", I, pivot=(-3.55, 0.45, 0.05))                       # the umbrella waiting in the stand
    cu.cylinder((-3.55, 0.45, 0.05), 0.012, 0.95, "Villa_Black", 8)
    cu.cylinder((-3.55, 0.45, 0.3), 0.055, 0.55, "Villa_Black", 10)
    cu.box((-3.58, 0.43, 0.95), (-3.45, 0.47, 1.0), "Villa_Black")
    anchor("Umbrella", -3.55, 1.0, 0, 180, "Foyer")

    # Light fixtures (emissive panels) + manifest lights.
    fixtures = Part("LightFixtures", I)
    def ceiling_light(x, y, z, room_name, kind="ceiling", colour=(1.0, 0.82, 0.62), intensity=1.0):
        fixtures.box((x - 0.3, y - 0.3, z - 0.04), (x + 0.3, y + 0.3, z), "Villa_LightPanel")
        light(kind, x, y, z - 0.1, room_name, colour, intensity)
    for r in MANIFEST["rooms"]:
        if r["kind"] in ("garage",): ceiling_light((r["min"][0] + r["max"][0]) / 2, (r["min"][1] + r["max"][1]) / 2, r["max"][2], r["name"], "ceiling", (1, 0.95, 0.88))
        elif r["name"] == "LivingRoom":
            for lx, ly in ((-2, 7), (2, 7)): ceiling_light(lx, ly, H1 - SLAB, r["name"])
            light("pendant", 0, 12, 4.8, r["name"], (1.0, 0.8, 0.58), 2.0, 12)
        elif r["name"] == "Gallery": ceiling_light(-1, 6.2, H1 + 3.15, r["name"])
        elif r["kind"] == "terrace":
            for gx, gy in ((r["min"][0] + 0.4, r["min"][1] + 0.4), (r["max"][0] - 0.4, r["max"][1] - 0.4), (r["min"][0] + 0.4, r["max"][1] - 0.4)):
                light("garden", gx, gy, H2 + 0.5, r["name"], (1, 0.8, 0.55), 0.6, 5)
        else: ceiling_light((r["min"][0] + r["max"][0]) / 2, (r["min"][1] + r["max"][1]) / 2, r["max"][2], r["name"])
    light("task", 7.5, 12.2, 2.2, "Kitchen", (1, 0.9, 0.78), 1.4, 4)
    for px in (6.6, 7.5, 8.4):                                       # pendant lamps over the island
        fixtures.box((px - 0.01, 12.19, 2.3), (px + 0.01, 12.21, H1 - SLAB), "Villa_Black")
        fixtures.cylinder((px, 12.2, 2.05), 0.17, 0.25, "Villa_BrushedMetal", 14)
        fixtures.box((px - 0.12, 12.08, 2.04), (px + 0.12, 12.32, 2.06), "Villa_LightPanel")
    light("task", 8.0, 15.2, 1.45, "Kitchen", (1, 0.92, 0.8), 0.8, 3)
    for x, y, z, rn in ((-8.9, 0.5, H1 + 0.7, "MasterBedroom"), (-6.1, 0.5, H1 + 0.7, "MasterBedroom"), (-9.6, 15.6, H1 + 0.9, "BedroomA"), (10.4, 11.9, H1 + 0.9, "BedroomB"), (-9.0, 15.6, 0.9, "GuestBedroom")):
        light("lamp", x, y, z, rn, (1, 0.76, 0.5), 0.6, 3)
    for i in range(0, 17, 4): light("stair", 7.9 + (i % 8) * 0.28, 0.9 if i < 8 else 2.4, 0.3 + i * 0.19, "StairHall", (1, 0.85, 0.65), 0.3, 2)

    # Exterior ---------------------------------------------------------------------------------------
    ext = Part("Exterior", E)
    for (a, b) in (((-17, -10), (17, 21.4)), ((-17, 21.4), (-5.6, 27.1)), ((5.6, 21.4), (17, 27.1)), ((-17, 27.1), (-13.25, 34)), ((-8.35, 27.1), (17, 34)), ((-13.25, 27.1), (-8.35, 27.95)), ((-13.25, 32.85), (-8.35, 34))):
        ext.box((a[0], a[1], -0.3), (b[0], b[1], -0.02), "Villa_Grass")                         # plot, with the pool cut out
    ext.box((-11, -10, -0.02), (-4, 0, 0.0), "Villa_Cobble")                                    # driveway
    ext.box((-4, -10, -0.02), (1, 0, 0.0), "Villa_Stone")                                       # front path
    ext.box((1, -8.6, -0.02), (11.5, -0.8, 0.0), "Villa_Travertine")                            # car display pad
    for k in range(5): ext.box((2.95 + k * 1.95, -8.4, 0.0), (3.05 + k * 1.95, -1.0, 0.004), "Villa_PlasterWhite")   # bay lines
    ext.box((-17, 16, -0.02), (17, 21.4, 0.0), "Villa_Stone")                                   # terrace (rear)
    ext.box((-17, 21.4, -0.02), (-5.6, 27.1, 0.0), "Villa_Stone"); ext.box((5.6, 21.4, -0.02), (17, 27.1, 0.0), "Villa_Stone")
    for (a, b) in (((-17, -10), (-11.6, -9.7)), ((1.6, -10), (17, -9.7)), ((-17.3, -10), (-17, 34)), ((17, -10), (17.3, 34)), ((-17.3, 34), (17.3, 34.3))):
        ext.box((a[0], a[1], 0), (b[0], b[1], 2.0), "Villa_PlasterWhite")
        ext.box((a[0], a[1], 2.0), (b[0], b[1], 2.1), "Villa_Stone")
    for x in (-11.6, -4.0, -3.9, 1.6):                                                          # stone piers by the gate / path
        ext.box((x - 0.3, -10.2, 0), (x + 0.3, -9.5, 2.6), "Villa_Stone")
    rbox(ext, (1.6, -9.65, 0.0), (17, -8.95, 1.15), "Villa_Hedge", 0.12, 2)                   # clipped hedge (front lawn edge)
    for a, b in (((-16.95, 16.6), (-16.35, 33.9)), ((16.35, 16.6), (16.95, 33.9)), ((-16.95, 33.35), (16.95, 33.95))):
        rbox(ext, (a[0], a[1], 0.0), (b[0], b[1], 2.35), "Villa_Hedge", 0.15, 2)             # tall green walls round the pool garden
    for a, b in (((1.0, -0.75), (11.0, -0.24)), ((-11.75, 0.0), (-11.24, 16.0)), ((11.24, 0.0), (11.75, 16.0))):
        ext.box((a[0], a[1], -0.015), (b[0], b[1], 0.004), "Villa_Gravel")                     # gravel drip strip at the foundation
    for k, (x, y, h) in enumerate(((-15.6, 31.8, 6.5), (14, 30, 7.0), (-14, 8, 5.5), (14, 8, 6.0), (14.5, -5, 6.5), (-14.5, -6, 5.0), (15.5, -1.5, 4.5))):
        tree("Tree_%d" % k, E, (x, y, 0), h, 300 + k)
    for k, (x, y, h) in enumerate(((-15.2, 18.0, 2.4), (-15.2, 20.5, 2.0), (-15.2, 25.5, 2.6), (15.2, 18.3, 2.3), (15.2, 21.0, 2.7), (15.2, 26.0, 2.2),
                                   (-6.6, 21.0, 1.8), (6.6, 21.0, 1.9), (-12.0, 32.6, 2.5), (12.0, 32.6, 2.4), (-2.9, -1.0, 1.6), (0.6, -1.0, 1.4))):
        tropical("Plant_Tropical_%d" % k, E, (x, y, 0), h, 400 + k)
    for k, (x, y) in enumerate(((12.6, -8.3), (14.4, -8.3), (16.2, -8.3), (-16.0, 28.5), (16.0, 28.5))):
        shrub("Shrub_%d" % k, E, (x, y, 0), 0.6, 500 + k, flowers=True)
    for k, (x, y) in enumerate(((-15.4, 19.2), (15.4, 19.6), (-15.6, 24.0), (15.5, 24.5), (-13.5, 33.0), (12.2, -7.4))):
        rock(ext, (x, y, 0.05), 0.35, 600 + k)
    # Pool deck furniture: loungers, outdoor table.
    for i, lx in enumerate((-3.5, -1.5, 1.5, 3.5)):
        lounger("Lounger_%d" % i, lx, 28.6, 0, 0)
        anchor("Lounger", lx, 28.4, 0, 180, "Garden", {"seat": 0.38})
    item("OutdoorTable", 11.5, 18.8, 0, [((-0.9, -0.9, 0.7), (0.9, 0.9, 0.75), "Villa_Wood"), ((-0.05, -0.05, 0), (0.05, 0.05, 0.7), "Villa_BrushedMetal")])
    for i, (cx, cy, yaw) in enumerate(((11.5, 17.7, 180), (11.5, 19.9, 0), (10.4, 18.8, 90), (12.6, 18.8, 270))):
        chair("OutdoorChair_%d" % i, cx, cy, 0, yaw, "Garden", "Seat")
    for i, (bx, by) in enumerate(((-14, 18), (-14, 23), (14, 23), (-6.0, 31), (8, 31))):
        ext.box((bx - 1.2, by - 0.6, 0), (bx + 1.2, by + 0.6, 0.35), "Villa_Concrete"); ext.box((bx - 1.1, by - 0.5, 0.35), (bx + 1.1, by + 0.5, 0.4), "Villa_Soil")
        shrub("Planter_Shrubs_%d" % i, E, (bx, by, 0.4), 0.5, 700 + i, flowers=True)
        anchor("Plants", bx, by - 0.9, 0, 0, "Garden")
    for (gx, gy) in ((-16, 16.5), (16, 16.5), (-16, 33), (16, 33), (-6, 21.6), (6, 21.6), (-6, 27), (6, 27)):
        ext.box((gx - 0.08, gy - 0.08, 0), (gx + 0.08, gy + 0.08, 0.9), "Villa_Aluminium")
        light("garden", gx, gy, 0.85, "Garden", (1, 0.8, 0.55), 0.6, 5)
    for (lx, ly, lz) in MANIFEST["pool"]["lights"]: light("pool", lx, ly, lz, "Pool", (0.6, 0.85, 1.0), 2.0, 6)
    tr = Part.all["Trim"]
    for (wx, wy, wz) in ((-2.35, -0.17, 2.35), (-0.1, -0.17, 2.35), (-10.85, -0.17, 2.9), (-4.15, -0.17, 2.9), (4.3, 16.17, 2.4), (-3.7, 16.17, 2.4)):
        tr.box((wx - 0.06, min(wy, wy + (0.08 if wy > 8 else -0.08)), wz - 0.18), (wx + 0.06, max(wy, wy + (0.08 if wy > 8 else -0.08)), wz + 0.18), "Villa_Black")
        light("wall", wx, wy + (0.3 if wy > 8 else -0.3), wz - 0.1, "Garden", (1, 0.8, 0.55), 0.8, 4)
    tr.box((-11.2, -2.2, H1 - SLAB - 0.07), (-3.8, -2.12, H1 - SLAB - 0.04), "Villa_LightPanel")    # LED line under the balcony soffit
    for (dx, dy) in ((X0 + 0.15, -0.2), (X1 - 0.15, -0.2), (X0 + 0.15, 16.2), (X1 - 0.15, 16.2)):
        tube(tr, (dx, dy, -0.02), (dx, dy, HROOF + 0.55), 0.045, 0.045, "Villa_Aluminium", 8)    # rainwater downpipes
    for (a, b, z) in (((X0 - 0.32, -0.32), (-4, -0.08), HROOF + 0.6), ((X0 - 0.32, Y1 + 0.08), (X1 + 0.32, Y1 + 0.32), HROOF + 0.6), ((X0 - 0.32, -0.32), (X0 - 0.08, Y1 + 0.32), HROOF + 0.6),
                      ((X1 + 0.08, 8), (X1 + 0.32, Y1 + 0.32), HROOF + 0.6), ((-4.32, -0.32), (X1 + 0.32, -0.08), 3 * H1 + 0.45), ((-4.32, 8.08), (X1 + 0.32, 8.32), 3 * H1 + 0.45),
                      ((-4.32, -0.32), (-4.08, 8.32), 3 * H1 + 0.45), ((X1 + 0.08, -0.32), (X1 + 0.32, 8.32), 3 * H1 + 0.45)):
        tr.box((a[0], a[1], z), (b[0], b[1], z + 0.03), "Villa_Aluminium")                    # parapet flashing
    for d in MANIFEST["doors"]:                                                                  # door frames, thresholds, light switches
        hx, hy, hz = d["hinge"]; w = d["width"]; s_ = 1 if d["along"] in ("+x", "+y") else -1
        hgt = 2.55 if w > 1.5 else 2.1
        if d["along"] in ("+x", "-x"):
            a0, a1 = sorted((hx, hx + w * s_))
            tr.box((a0 - 0.05, hy - 0.17, hz), (a0, hy + 0.17, hz + hgt), "Villa_PlasterWhite")
            tr.box((a1, hy - 0.17, hz), (a1 + 0.05, hy + 0.17, hz + hgt), "Villa_PlasterWhite")
            tr.box((a0 - 0.05, hy - 0.17, hz + hgt), (a1 + 0.05, hy + 0.17, hz + hgt + 0.05), "Villa_PlasterWhite")
            tr.box((a0, hy - 0.16, hz), (a1, hy + 0.16, hz + 0.006), "Villa_Travertine")
            tr.box((a1 + 0.12, hy - 0.09, hz + 1.05), (a1 + 0.2, hy - 0.08, hz + 1.13), "Villa_PlasterWhite")
        else:
            a0, a1 = sorted((hy, hy + w * s_))
            tr.box((hx - 0.17, a0 - 0.05, hz), (hx + 0.17, a0, hz + hgt), "Villa_PlasterWhite")
            tr.box((hx - 0.17, a1, hz), (hx + 0.17, a1 + 0.05, hz + hgt), "Villa_PlasterWhite")
            tr.box((hx - 0.17, a0 - 0.05, hz + hgt), (hx + 0.17, a1 + 0.05, hz + hgt + 0.05), "Villa_PlasterWhite")
            tr.box((hx - 0.16, a0, hz), (hx + 0.16, a1, hz + 0.006), "Villa_Travertine")
            tr.box((hx - 0.09, a1 + 0.12, hz + 1.05), (hx - 0.08, a1 + 0.2, hz + 1.13), "Villa_PlasterWhite")

    # Surroundings: street, pavements, street lights, neighbouring houses, distant trees, ground beyond the plot.
    S = col["Surroundings"]
    sur = Part("Neighbourhood", S)
    for (a, b) in (((-90, -80), (90, -10.3)), ((-90, 34.3), (90, 100)), ((-90, -10.3), (-17.3, 34.3)), ((17.3, -10.3), (90, 34.3))):
        sur.box((a[0], a[1], -0.5), (b[0], b[1], -0.04), "Villa_Grass")
    sur.box((-90, -22.0, -0.06), (90, -12.0, -0.02), "Villa_Asphalt")                           # road (7 m + parking)
    for i in range(-15, 16):
        sur.box((i * 6 - 1.5, -17.08, -0.02), (i * 6 + 1.5, -16.92, -0.015), "Villa_PlasterWhite")   # lane marking
    for (y0, y1) in ((-12.0, -10.3), (-24.2, -22.0)):
        sur.box((-90, y0, -0.06), (90, y1, 0.1), "Villa_Concrete")                               # pavements
        kerb = y0 if y0 > -20 else y1
        sur.box((-90, kerb - 0.12 if y0 > -20 else kerb, -0.06), (90, kerb if y0 > -20 else kerb + 0.12, 0.13), "Villa_Travertine")
    for i in range(-5, 6):                                                                        # street lights both sides
        for (ly, face) in ((-11.0, -1), (-23.2, 1)):
            lx = i * 15 + (7.5 if face > 0 else 0)
            tube(sur, (lx, ly, 0.1), (lx, ly, 6.0), 0.07, 0.05, "Villa_Aluminium", 8)
            sur.box((lx - 0.15, min(ly, ly + face * 1.2), 5.9), (lx + 0.15, max(ly, ly + face * 1.2), 6.05), "Villa_Aluminium")
            sur.box((lx - 0.12, ly + face * 0.9 - 0.2, 5.86), (lx + 0.12, ly + face * 0.9 + 0.2, 5.9), "Villa_LightPanel")
            light("street", lx, ly + face * 0.9, 5.7, "Street", (1, 0.82, 0.6), 1.6, 14)
        if i % 2 == 0:
            tree("StreetTree_%d" % (i + 5), S, (i * 15 + 3.5, -11.1, 0.1), 5.0, 1000 + i, 0.8)
    def house(name, x0, y0, x1, y1, floors, seed):
        rng = np.random.default_rng(seed)
        hp = Part(name, S, pivot=((x0 + x1) / 2, (y0 + y1) / 2, 0))
        h = floors * 3.2
        hp.box((x0, y0, 0), (x1, y1, h), "Villa_Plaster" if seed % 2 else "Villa_PlasterWhite")
        hp.box((x0 - 0.3, y0 - 0.3, h), (x1 + 0.3, y1 + 0.3, h + 0.35), "Villa_PlasterWhite")
        for f in range(floors):                                                                  # window bands on the street side
            for k in range(int((x1 - x0) / 3)):
                wx = x0 + 1.0 + k * 3
                side_y = y1 if y1 < 0 else y0
                o = 0.03 if y1 < 0 else -0.03
                hp.box((wx, min(side_y, side_y + o), f * 3.2 + 0.9), (wx + 1.8, max(side_y, side_y + o), f * 3.2 + 2.5), "Villa_Glass" if rng.uniform() < 0.3 else "Villa_Screen")
        if rng.uniform() < 0.6: hp.box((x0 + 1, y0 + 1, h + 0.35), (x0 + 4, y0 + 3, h + 1.6), "Villa_Slats")
    house("House_West", -46, -6, -24, 18, 2, 1); house("House_East", 24, -4, 44, 20, 2, 2)
    house("House_SW", -40, -48, -18, -30, 2, 3); house("House_S", -10, -50, 12, -30, 3, 4); house("House_SE", 20, -46, 42, -30, 2, 5)
    house("House_NW", -40, 44, -16, 64, 2, 6); house("House_NE", 18, 44, 40, 66, 2, 7)
    for (a, b) in (((-48, -26), (-22, -25.6)), ((-12, -26), (14, -25.6)), ((18, -26), (44, -25.6))):
        sur.box((a[0], a[1], 0), (b[0], b[1], 1.6), "Villa_Hedge")                              # front hedges opposite
    for k in range(18):                                                                            # distant tree line
        ang = k / 18 * 2 * math.pi
        tree("FarTree_%d" % k, S, (math.cos(ang) * 75, 12 + math.sin(ang) * 75, 0), 9.0, 1100 + k, 1.4)
    # Flower beds (raised timber-edged beds with soil; Unity plants them with the project's flower prefabs)
    # and a koi pond in the west lawn. Flat [x0, y0, x1, y1, z] per bed.
    beds = [(12.0, -8.5, 16.6, -6.2, 0), (12.0, -5.0, 16.6, -1.0, 0),                      # front lawn
            (-16.2, 17.6, -15.0, 32.6, 0), (15.0, 17.6, 16.2, 32.6, 0),                    # along the garden hedges
            (-5.5, 32.0, -1.5, 33.1, 0), (1.5, 32.0, 5.5, 33.1, 0),                        # in front of the rear hedge
            (-10.8, 2.0, -9.8, 6.0, 2 * H1), (8.6, 15.2, 10.8, 15.8, 2 * H1)]              # roof terraces
    for i, (x0, y0, x1, y1, z) in enumerate(beds):
        ext.box((x0 - 0.06, y0 - 0.06, z), (x1 + 0.06, y1 + 0.06, z + 0.32), "Villa_Wood")           # timber edge
        ext.box((x0, y0, z + 0.32), (x1, y1, z + 0.34), "Villa_Soil")
    MANIFEST["flowerbeds"] = [v for b in beds for v in (b[0], b[1], b[2], b[3], b[4] + 0.34)]
    # Koi pond: 4.4 × 4.4 m, 0.7 m deep, natural stone edge, pebble floor.
    kx0, ky0, kx1, ky1 = -13.0, 28.2, -8.6, 32.6
    pond = Part("KoiPond", E)
    pond.box((kx0, ky0, -0.75), (kx1, ky1, -0.7), "Villa_Gravel")
    for (a, b) in (((kx0 - 0.25, ky0 - 0.25), (kx1 + 0.25, ky0)), ((kx0 - 0.25, ky1), (kx1 + 0.25, ky1 + 0.25)), ((kx0 - 0.25, ky0), (kx0, ky1)), ((kx1, ky0), (kx1 + 0.25, ky1))):
        pond.box((a[0], a[1], -0.75), (b[0], b[1], -0.02), "Villa_Concrete")
    for k in range(22):                                                                            # boulders round the rim
        t = k / 22 * 2 * math.pi
        rock(pond, ((kx0 + kx1) / 2 + math.cos(t) * 2.45, (ky0 + ky1) / 2 + math.sin(t) * 2.45, 0.05), 0.28 + 0.12 * ((k * 7) % 3) / 2, 1200 + k)
    for k, (lx, ly) in enumerate(((-12.2, 29.0), (-9.4, 31.6), (-11.0, 31.9))):                   # lily pads
        pond.cylinder((lx, ly, -0.115), 0.28, 0.005, "Villa_Leaf", 14)
    MANIFEST["koipond"] = {"min": [kx0, ky0, -0.7], "max": [kx1, ky1, -0.12], "water_level": -0.12}
    MANIFEST["wildlife"] = {"lawn": [-14.5, 27.5, 14.5, 33.0], "front": [11.8, -8.6, 16.8, -0.5], "perches": [-15.6, 31.8, 5.5, 14, 30, 6.0, -14, 8, 4.5, 14, 8, 5.0, 14.5, -5, 5.5]}
    MANIFEST["street"] = {"gate": [-7.5, -10.0, 0], "road_y": -17.0, "city_exit": [0, -17.0, 0]}
    MANIFEST["entrance"] = [-1.25, -1.2, 0]
    MANIFEST["pet"] = [6.0, 19.0, 0]

    # Water surface (separate object, for the Unity water shader).
    water = Part("PoolWater", A)
    water.box((px0, py0, -0.13), (px1, py1, -0.12), "Villa_Water", skip=("-z", "-x", "+x", "-y", "+y"))

    return col


def reference(col):
    """The supplied Meshy model, scaled to the villa's footprint, hidden in the Reference collection."""
    if not REFERENCE or not os.path.exists(REFERENCE): return
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=REFERENCE)
    for o in set(bpy.data.objects) - before:
        for c in o.users_collection: c.objects.unlink(o)
        col["Reference"].objects.link(o)
        o.scale = (11.6, 11.6, 11.6); o.location = (0, 12, 6.5)
        o.hide_set(True); o.hide_render = True
    col["Reference"].hide_render = True


def export(col):
    def select(names):
        bpy.ops.object.select_all(action="DESELECT")
        for o in bpy.data.objects:
            if o.name in names or any(c.name in names for c in o.users_collection): o.select_set(True)
    sets = {"Architecture": ["Architecture"], "Interior": ["Interior"], "Doors": ["Doors"], "Exterior": ["Exterior"], "Surroundings": ["Surroundings"]}
    for name, cols in sets.items():
        select(cols)
        fbx = os.path.join(OUT, "LuxuryVilla_" + name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                 object_types={"MESH", "EMPTY"}, mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False, bake_space_transform=False)
        select(cols)
        glb = os.path.join(OUT, "LuxuryVilla_" + name + ".glb")
        bpy.ops.export_scene.gltf(filepath=glb, use_selection=True, export_format="GLB", export_apply=False, export_yup=True)
    with open(os.path.join(OUT, "LuxuryVilla_manifest.json"), "w") as f:
        json.dump(MANIFEST, f, indent=1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LuxuryVilla.blend"))


def render_preview():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE" if hasattr(bpy.types, "RenderSettings") else scene.render.engine
    scene.render.resolution_x, scene.render.resolution_y = 960, 600
    if hasattr(scene, "eevee"):
        for attr, val in (("taa_render_samples", 8), ("use_raytracing", False), ("use_shadows", True)):
            if hasattr(scene.eevee, attr): setattr(scene.eevee, attr, val)
    world = bpy.data.worlds.new("Sky"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.85, 1.0, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    sun = bpy.data.lights.new("Sun", "SUN"); sun.energy = 4; so = bpy.data.objects.new("Sun", sun); scene.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), 0, math.radians(30))
    cam = bpy.data.cameras.new("Cam"); cam.lens = 24; co = bpy.data.objects.new("Cam", cam); scene.collection.objects.link(co); scene.camera = co
    shots = {"front": ((16, -30, 11), (0, 6, 4)), "rear": ((-16, 44, 10), (0, 14, 2)), "living": ((-2.5, 6.0, 1.6), (1, 15, 2.2)), "upper": ((-2.0, 6.6, H1 + 1.6), (6, 13, H1 + 0.2)),
             "top": ((0, 12, 42), (0, 12, 0)), "lift": ((7.4, 4.0, 1.6), (4.6, 3.5, 1.1)), "roof": ((-14, -6, 12), (0, 8, 7)), "stairwell": ((6.2, 3.9, 8.4), (9.5, 1.5, 4.0)), "kitchen": ((3.0, 7.0, 1.7), (9, 14, 1.0)), "stairs": ((5.6, 3.8, 1.6), (10, 0.9, 2.5))}
    for name, (p, t) in shots.items():
        co.location = p
        co.rotation_euler = (Vector(t) - Vector(p)).to_track_quat("-Z", "Y").to_euler()
        if name == "top":                                      # see the plan: hide roof and upper floor
            for n in ("Roof", "UpperFloor", "Balcony", "LightFixtures"):
                o = bpy.data.objects.get(n)
                if o: o.hide_render = True
        scene.render.filepath = os.path.join(OUT, "preview_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        for n in ("Roof", "UpperFloor", "Balcony", "LightFixtures"):
            o = bpy.data.objects.get(n)
            if o: o.hide_render = False


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_materials()
    build_detail_materials()
    col = build()
    for part in list(Part.all.values()): part.build()
    reference(col)
    export(col)
    if "--no-preview" not in sys.argv: render_preview()
    print("VILLA OK", OUT, len(Part.all), "objects")


main()
