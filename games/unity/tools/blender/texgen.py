"""Procedural, tileable PBR ground materials (no downloads needed).

Writes out_tex/<Name>_Color.png (sRGB), _Normal.png (tangent space, +Y up / OpenGL),
_Mask.png (URP terrain/Lit mask: R metallic, G ambient occlusion, B height, A smoothness).
Albedo values are kept in physically plausible ranges (dry sand ~0.35, grass ~0.12, asphalt ~0.06 linear).
"""
import os, sys
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out_tex')
N = int(os.environ.get('TEX_SIZE', 1024))
rng = np.random.default_rng(7)


def grid(n=N):
    y, x = np.mgrid[0:n, 0:n] / n
    return x, y


def perlin(freq, seed, n=N):
    """Periodic gradient noise, frequency = cells per tile (integer → seamless)."""
    r = np.random.default_rng(seed)
    angles = r.uniform(0, 2 * np.pi, (freq, freq))
    gx, gy = np.cos(angles), np.sin(angles)
    x, y = grid(n)
    fx, fy = x * freq, y * freq
    x0, y0 = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - x0, fy - y0

    def dot(ix, iy, dx, dy):
        ix %= freq; iy %= freq
        return gx[iy, ix] * dx + gy[iy, ix] * dy

    fade = lambda t: t * t * t * (t * (t * 6 - 15) + 10)
    u, v = fade(tx), fade(ty)
    n00 = dot(x0, y0, tx, ty); n10 = dot(x0 + 1, y0, tx - 1, ty)
    n01 = dot(x0, y0 + 1, tx, ty - 1); n11 = dot(x0 + 1, y0 + 1, tx - 1, ty - 1)
    return (n00 * (1 - u) + n10 * u) * (1 - v) + (n01 * (1 - u) + n11 * u) * v  # ~[-0.7, 0.7]


def fbm(freq, octaves, seed, gain=0.5):
    total, amp, norm = 0, 1.0, 0
    for o in range(octaves):
        total = total + amp * perlin(freq * 2 ** o, seed + o)
        norm += amp; amp *= gain
    return total / norm * 1.4 * 0.5 + 0.5      # ≈ 0…1


def worley(freq, seed, jitter=0.9, warp=0.0):
    """Periodic cellular noise: returns (F1, F2, cell id hash in 0…1). warp: domain distortion in cells."""
    r = np.random.default_rng(seed)
    pts = r.uniform(0.5 - jitter / 2, 0.5 + jitter / 2, (freq, freq, 2))
    ids = r.uniform(0, 1, (freq, freq))
    x, y = grid()
    fx, fy = x * freq, y * freq
    if warp:
        fx = fx + perlin(4, seed + 500) * warp + perlin(9, seed + 501) * warp * 0.5
        fy = fy + perlin(4, seed + 502) * warp + perlin(9, seed + 503) * warp * 0.5
    cx, cy = np.floor(fx).astype(int), np.floor(fy).astype(int)
    f1 = np.full(fx.shape, 9.0); f2 = np.full(fx.shape, 9.0); idv = np.zeros(fx.shape)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ix, iy = cx + dx, cy + dy
            p = pts[iy % freq, ix % freq]
            d = np.hypot(ix + p[..., 0] - fx, iy + p[..., 1] - fy)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            idv = np.where(closer, ids[iy % freq, ix % freq], idv)
            f1 = np.where(closer, d, f1)
    return f1, f2, idv


def hexc(h):
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def mix(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + b * t


def colour(field_low, field_high, t):
    return mix(np.broadcast_to(hexc(field_low), t.shape + (3,)), np.broadcast_to(hexc(field_high), t.shape + (3,)), t)


def save(name, albedo, height, smooth, ao=None, strength=4.0):
    os.makedirs(OUT, exist_ok=True)
    albedo = np.clip(albedo, 0, 1)
    height = (height - height.min()) / max(height.max() - height.min(), 1e-6)
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * strength
    normal = np.dstack([-dx, dy, np.ones_like(height)])        # image row 0 = top → +V is up
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    if ao is None:   # cavity occlusion from the height field
        blur = height
        for _ in range(3):
            blur = (np.roll(blur, 2, 0) + np.roll(blur, -2, 0) + np.roll(blur, 2, 1) + np.roll(blur, -2, 1) + blur) / 5
        ao = np.clip(1 - np.maximum(blur - height, 0) * 6, 0.35, 1)
    mask = np.dstack([np.zeros_like(height), ao, height, np.clip(smooth, 0, 1)])
    Image.fromarray((albedo * 255).astype(np.uint8)).save(os.path.join(OUT, name + '_Color.png'))
    Image.fromarray(((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(OUT, name + '_Normal.png'))
    Image.fromarray((mask * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name + '_Mask.png'))
    lin = (albedo ** 2.2).mean(axis=(0, 1))
    print(f'{name}: mean linear albedo {lin.round(3)}')


def grass():
    base = fbm(4, 5, 11)
    blades = fbm(64, 3, 12, 0.6)
    streak = perlin(96, 13) * 0.5 + perlin(160, 14) * 0.5
    dry = np.clip((fbm(3, 4, 15) - 0.55) * 4, 0, 1)
    soil = np.clip((fbm(8, 4, 16) - 0.7) * 5, 0, 1)
    clumps = fbm(16, 4, 17)
    c = colour('34461F', '66773A', blades * 0.6 + base * 0.2 + clumps * 0.2)
    c = mix(c, colour('8A8448', 'A79A5E', blades), dry * 0.7)
    c = mix(c, colour('4A3A28', '5E4A33', base), soil * 0.8)
    c *= (0.7 + 0.45 * (streak[..., None] * 0.5 + 0.5)) * (0.8 + 0.35 * clumps[..., None])
    h = blades * 0.7 + streak * 0.3 - soil * 0.4
    save('Grass', c, h, 0.12 + 0.1 * (1 - blades))


def soil(name, wet):
    base = fbm(6, 6, 21)
    clods = worley(24, 22)[0]
    f1, _, cid = worley(36, 23, 0.95, warp=0.6)
    radius = 0.12 + 0.25 * cid
    pebbles = np.where(cid > 0.82, np.clip((radius - f1) / radius * 3, 0, 1), 0) * (fbm(6, 3, 24) > 0.45)
    c = colour('5A4632', '7B6248', base) if not wet else colour('33281D', '45372A', base)
    c = mix(c, colour('8C8578', 'A39C8E', base), pebbles * 0.8)
    h = base * 0.6 + (1 - clods) * 0.3 + pebbles * 0.5
    smooth = (0.1 + 0.05 * base) if not wet else (0.45 + 0.25 * (1 - h))
    save(name, c, h, smooth)


def mud():
    base = fbm(5, 6, 31)
    puddle = np.clip((fbm(3, 4, 32) - 0.58) * 6, 0, 1)
    tracks = np.clip(1 - np.abs(perlin(12, 33)) * 8, 0, 1) * 0.4
    c = colour('2E241A', '4A3926', base)
    c = mix(c, colour('241D16', '2C241B', base), puddle)
    h = base - puddle * 0.6 - tracks
    save('Mud', c, h, 0.35 + puddle * 0.55)


def gravel():
    f1, f2, cell = worley(40, 41, 0.85, warp=0.4)
    radius = 0.38 + 0.18 * cell
    stone = np.clip((radius - f1) / radius * 5, 0, 1)
    dome = np.sqrt(np.clip(1 - (f1 / radius) ** 2, 0, 1))
    grain = fbm(128, 2, 42)
    tone = cell[..., None]
    c = mix(colour('3F362C', '55493B', grain), np.broadcast_to(hexc('85796A'), f1.shape + (3,)) * (0.7 + 0.55 * tone), stone)
    h = dome * stone + grain * 0.15
    save('Gravel', c, h, 0.12 + 0.12 * stone, strength=6)


def stone():
    f1, f2, cell = worley(4, 51, 0.9, warp=0.8)
    cracks = np.clip((f2 - f1) * 20, 0, 1)
    base = fbm(8, 6, 52)
    fine = fbm(64, 3, 53)
    lichen = np.clip((fbm(6, 4, 54) - 0.62) * 5, 0, 1)
    strata = perlin(3, 55) * 0.5 + 0.5
    c = colour('5F5A54', '958E84', base * 0.6 + fine * 0.25 + strata * 0.15) * (0.94 + 0.12 * cell[..., None])
    c = mix(c, colour('8E9466', 'B7B58A', fine), lichen * 0.5)
    c *= (0.62 + 0.38 * cracks[..., None])
    h = base * 0.6 + fine * 0.2 + cracks * 0.4
    save('Stone', c, h, 0.18 + 0.1 * fine, strength=5)


def moss():
    base = fbm(6, 6, 61)
    tufts = 1 - np.clip(worley(70, 62, 0.9)[0] * 2.2, 0, 1)
    c = colour('2F4A1C', '5E7A2E', base * 0.6 + tufts * 0.4)
    c = mix(c, colour('6B6A3A', '847B44', base), np.clip((fbm(4, 3, 63) - 0.6) * 4, 0, 1) * 0.6)
    save('Moss', c, tufts * 0.7 + base * 0.3, 0.15 + 0.1 * tufts)


def sand(name, wet):
    grain = fbm(256, 2, 71)
    base = fbm(4, 5, 72)
    x, y = grid()
    ripple = np.sin((y + perlin(4, 73) * 0.08) * 2 * np.pi * 28) * 0.5 + 0.5
    shell = np.clip((fbm(40, 2, 74) - 0.8) * 8, 0, 1)
    if wet:
        c = colour('7D6A50', '937D5E', base * 0.6 + grain * 0.4)
        h = base * 0.5 + grain * 0.2 + ripple * 0.2
        s = 0.55 + 0.25 * base
    else:
        c = colour('A48E6C', 'BDA984', base * 0.5 + grain * 0.5)
        c = mix(c, colour('E8E0D2', 'F2ECE2', grain), shell * 0.6)
        h = ripple * 0.5 + grain * 0.3 + base * 0.2
        s = 0.08 + 0.05 * grain
    save(name, c, h, s, strength=3)


def asphalt():
    grain = fbm(200, 2, 81)
    aggregate = np.clip((fbm(180, 1, 82) - 0.62) * 6, 0, 1)
    patch = np.clip((fbm(3, 4, 83) - 0.55) * 3, 0, 1)
    cracks = np.clip(1 - np.abs(perlin(5, 84) + perlin(11, 85) * 0.5) * 30, 0, 1) * np.clip((fbm(4, 3, 86) - 0.55) * 5, 0, 1)
    c = colour('2A2A2B', '3A3A3B', grain) * (1 - patch[..., None] * 0.18)
    c = mix(c, colour('6A6863', '8A8780', grain), aggregate * 0.5)
    c = mix(c, colour('141414', '1A1A1A', grain), cracks)
    h = grain * 0.4 + aggregate * 0.5 - cracks * 0.8
    save('Asphalt', c, h, 0.12 + 0.12 * patch, strength=5)


def concrete():
    base = fbm(5, 6, 91)
    pores = np.clip((0.12 - worley(160, 92, 0.9)[0]) * 10, 0, 1)
    stains = np.clip((fbm(3, 5, 93) - 0.55) * 3, 0, 1)
    x, y = grid()
    joints = np.clip(1 - np.minimum(np.abs((x * 2) % 1 - 0.5), np.abs((y * 2) % 1 - 0.5)) * 0 - 0, 0, 1)
    seam = ((np.abs(x * 2 % 1) < 0.004) | (np.abs(y * 2 % 1) < 0.004)).astype(float)
    c = colour('8D8B86', 'A8A59F', base) * (1 - stains[..., None] * 0.15) * (1 - pores[..., None] * 0.3) * (1 - seam[..., None] * 0.45)
    h = base * 0.3 - pores * 0.5 - seam * 0.8
    save('Concrete', c, h, 0.2 + 0.1 * base, strength=4)


def debris():
    base = fbm(6, 5, 101)
    c = colour('3E3021', '57432C', base)
    h = base * 0.3
    x, y = grid()
    for leaf in range(900):          # scattered fallen leaves and twigs
        cx, cy = rng.uniform(0, 1, 2)
        ang = rng.uniform(0, np.pi)
        sx, sy = rng.uniform(0.006, 0.016), rng.uniform(0.003, 0.007)
        if leaf % 9 == 0: sx, sy = rng.uniform(0.02, 0.05), 0.0012
        dx = (x - cx + 0.5) % 1 - 0.5; dy = (y - cy + 0.5) % 1 - 0.5
        u = dx * np.cos(ang) + dy * np.sin(ang); v = -dx * np.sin(ang) + dy * np.cos(ang)
        m = np.clip(1 - (u / sx) ** 2 - (v / sy) ** 2, 0, 1)
        if m.max() == 0: continue
        tint = hexc(['7A4E22', '8E6A2A', '5E3B1E', '9C7A3A', '6B5A2A', '4E3A22'][leaf % 6]) * rng.uniform(0.8, 1.1)
        c = mix(c, np.broadcast_to(tint, c.shape), (m > 0) * 0.92)
        h = np.maximum(h, 0.3 + m * 0.5 + leaf / 900 * 0.2)
    save('ForestDebris', c, h, 0.12 + 0.08 * base, strength=4)


def bricks():
    """Old clay brick pavers in running bond, worn and slightly uneven, mortar-filled joints with moss."""
    x, y = grid()
    rows = 8
    bw, bh = 1 / 4, 1 / rows                 # bricks 2:1 → 4 per row × 8 rows per tile
    row = np.floor(y / bh)
    shift = (row % 2) * bw / 2
    u = ((x + shift) % bw) / bw
    v = (y % bh) / bh
    col = np.floor((x + shift) / bw) % 4
    cell = (row * 7 + col * 13) % 23 / 23.0
    edge = np.minimum(np.minimum(u, 1 - u) * 2, np.minimum(v, 1 - v) * 1) * 2.2 + (fbm(64, 2, 115) - 0.5) * 0.25
    face = np.clip(edge * 6, 0, 1)
    wear = fbm(24, 4, 111)
    grime = np.clip((fbm(5, 4, 112) - 0.5) * 2.5, 0, 1)
    tone = 0.75 + 0.5 * cell
    red = colour('6E4434', '8E5A44', wear)
    grey = colour('5E5853', '8C847C', wear)
    c = mix(grey, red, np.clip(cell * 0.55 + wear * 0.35 + 0.05, 0, 1)) * tone[..., None] * 0.92
    c = mix(c, colour('4A453F', '6F665B', wear), grime * 0.6)
    chips = np.clip((fbm(40, 3, 114) - 0.62) * 6, 0, 1) * (edge < 0.5)
    face = face * (1 - chips)
    mortar = colour('6E6A60', '857F72', wear)
    moss = colour('3E4A22', '5A6B2E', wear)
    joint = 1 - face
    c = mix(c, mix(mortar, moss, np.clip((fbm(8, 3, 113) - 0.5) * 3, 0, 1)), joint)
    h = face * (0.7 + 0.3 * wear) + cell * 0.1
    save('BrickPavers', c, h, 0.12 + 0.18 * face * (1 - grime), strength=5)


ALL = {'Grass': grass, 'DrySoil': lambda: soil('DrySoil', False), 'WetSoil': lambda: soil('WetSoil', True), 'Mud': mud,
       'Gravel': gravel, 'Stone': stone, 'Moss': moss, 'BeachSand': lambda: sand('BeachSand', False),
       'WetBeachSand': lambda: sand('WetBeachSand', True), 'Asphalt': asphalt, 'Concrete': concrete, 'ForestDebris': debris, 'BrickPavers': bricks}

if __name__ == '__main__':
    for name in (sys.argv[1:] or ALL):
        ALL[name]()
