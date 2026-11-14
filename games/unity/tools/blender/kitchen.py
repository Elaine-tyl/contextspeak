"""ContextSpeak café kitchen kit — runs inside Blender 5.3 (Blender MCP or Scripting tab).

Builds real-size, bevelled, PBR café-kitchen equipment and exports one GLB per item to
Assets/Art/kitchen/ (front faces -Y in Blender = +Z in Unity, origin on the floor, centred):
  EspressoMachine (two-group, stainless + matte black, walnut handles), CoffeeGrinder, KnockBox,
  MilkJugs, Range (six burners, two ovens), ExtractorHood, PrepTable, SinkUnit, Fridge,
  WallShelf (plates, cups, jars), PotRail (hanging pans), PassShelf (heat lamps), Pendant,
  CupStack, BeanJars.

    exec(open(".../tools/blender/kitchen.py").read()); build_all()
"""
import bpy, math, os
import mathutils

OUTS = [
    "/Users/elaineting/.codex/worktrees/capture-to-library/eng learning/games/unity/Assets/Art/kitchen/",
    "/Users/elaineting/Projects/ContextSpeak/games/unity/Assets/Art/kitchen/",
]
SHOTS = "/Users/elaineting/Projects/ContextSpeak/.local/kitchen_shots/"

PALETTE = {
    "Steel":      dict(color=(0.72, 0.73, 0.74), metal=1.0, rough=0.32),
    "BrushedSteel": dict(color=(0.62, 0.63, 0.64), metal=1.0, rough=0.45),
    "Chrome":     dict(color=(0.92, 0.92, 0.93), metal=1.0, rough=0.08),
    "MatteBlack": dict(color=(0.025, 0.025, 0.028), metal=0.2, rough=0.55),
    "CastIron":   dict(color=(0.04, 0.04, 0.04), metal=0.6, rough=0.6),
    "Walnut":     dict(color=(0.30, 0.17, 0.09), metal=0.0, rough=0.45),
    "Oak":        dict(color=(0.62, 0.45, 0.28), metal=0.0, rough=0.55),
    "Ceramic":    dict(color=(0.93, 0.92, 0.89), metal=0.0, rough=0.12),
    "Sage":       dict(color=(0.44, 0.53, 0.45), metal=0.0, rough=0.25),
    "Terracotta": dict(color=(0.66, 0.33, 0.20), metal=0.0, rough=0.7),
    "Copper":     dict(color=(0.86, 0.48, 0.32), metal=1.0, rough=0.28),
    "Glass":      dict(color=(0.75, 0.78, 0.76), metal=0.0, rough=0.05, alpha=0.28),
    "SmokedGlass": dict(color=(0.30, 0.20, 0.12), metal=0.0, rough=0.05, alpha=0.45),
    "Beans":      dict(color=(0.16, 0.08, 0.04), metal=0.0, rough=0.5),
    "GaugeFace":  dict(color=(0.95, 0.94, 0.90), metal=0.0, rough=0.2),
    "Rubber":     dict(color=(0.05, 0.05, 0.05), metal=0.0, rough=0.85),
    "Glow":       dict(color=(1.0, 0.55, 0.25), metal=0.0, rough=0.4, emit=3.0),
    "WarmLight":  dict(color=(1.0, 0.85, 0.6), metal=0.0, rough=0.4, emit=6.0),
    "Linen":      dict(color=(0.86, 0.83, 0.76), metal=0.0, rough=0.9),
    "Herb":       dict(color=(0.24, 0.42, 0.20), metal=0.0, rough=0.7),
}


def _ctx():
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == 'VIEW_3D')
    return bpy.context.temp_override(window=win, area=area, scene=win.scene, view_layer=win.view_layer), win


def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0: block.remove(item)


def mat(name):
    m = bpy.data.materials.get("K_" + name)
    if m: return m
    p = PALETTE[name]
    m = bpy.data.materials.new("K_" + name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*p["color"], 1)
    b.inputs["Metallic"].default_value = p["metal"]
    b.inputs["Roughness"].default_value = p["rough"]
    if "alpha" in p:
        b.inputs["Alpha"].default_value = p["alpha"]
        m.surface_render_method = 'BLENDED'
    if "emit" in p:
        b.inputs["Emission Color"].default_value = (*p["color"], 1)
        b.inputs["Emission Strength"].default_value = p["emit"]
    return m


PARTS = []


def _finish(o, material, bevel):
    o.data.materials.append(mat(material))
    if bevel > 0:
        mod = o.modifiers.new("Bevel", 'BEVEL'); mod.width = bevel; mod.segments = 2; mod.limit_method = 'ANGLE'
    PARTS.append(o)
    return o


def box(size, loc, material, bevel=0.004, rot=(0, 0, 0)):
    ctx, win = _ctx()
    with ctx: bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=[math.radians(a) for a in rot])
    o = win.view_layer.objects.active
    o.scale = size
    with ctx: bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(o, material, bevel)


def cyl(r, depth, loc, material, rot=(0, 0, 0), verts=28, bevel=0.002, r2=None):
    ctx, win = _ctx()
    with ctx:
        if r2 is None: bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=[math.radians(a) for a in rot])
        else: bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc, rotation=[math.radians(a) for a in rot])
    return _finish(win.view_layer.objects.active, material, bevel)


def torus(R, r, loc, material, rot=(0, 0, 0), arc=None):
    ctx, win = _ctx()
    with ctx: bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=32, minor_segments=10, location=loc, rotation=[math.radians(a) for a in rot])
    return _finish(win.view_layer.objects.active, material, 0)


def sphere(r, loc, material, scale=(1, 1, 1)):
    ctx, win = _ctx()
    with ctx: bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=r, location=loc)
    o = win.view_layer.objects.active
    o.scale = scale
    return _finish(o, material, 0)


def tube(points, r, material):
    """A bent pipe (steam wand, tap) through points."""
    curve = bpy.data.curves.new("pipe", 'CURVE'); curve.dimensions = '3D'
    curve.bevel_depth = r; curve.bevel_resolution = 3
    spline = curve.splines.new('POLY'); spline.points.add(len(points) - 1)
    for i, p in enumerate(points): spline.points[i].co = (*p, 1)
    o = bpy.data.objects.new("pipe", curve)
    ctx, win = _ctx()
    win.scene.collection.objects.link(o)
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); win.view_layer.objects.active = o
        bpy.ops.object.convert(target='MESH')
    o = win.view_layer.objects.active
    o.data.materials.clear()
    return _finish(o, material, 0)


def cup(x, y, z, scale=1.0, material="Ceramic", saucer=True):
    cyl(0.038 * scale, 0.07 * scale, (x, y, z + 0.035 * scale), material, r2=0.03 * scale, verts=20)
    torus(0.018 * scale, 0.004 * scale, (x + 0.042 * scale, y, z + 0.04 * scale), material, rot=(90, 0, 0))
    if saucer: cyl(0.06 * scale, 0.008, (x, y, z + 0.004), material, verts=24)


# --------------------------------------------------------------------------------------- items
def espresso_machine():
    W, D = 0.82, 0.56
    box((0.7, D - 0.08, 0.04), (0, 0.02, 0.02), "Rubber", 0.004)                       # base plinth
    box((W, D, 0.06), (0, 0, 0.06), "BrushedSteel", 0.008)                              # base
    box((W, 0.34, 0.32), (0, 0.09, 0.25), "MatteBlack", 0.02)                           # body
    for side in (-1, 1): box((0.012, 0.36, 0.33), (side * W / 2, 0.09, 0.25), "Steel", 0.004)   # side cheeks
    box((W + 0.02, D - 0.02, 0.025), (0, 0.01, 0.42), "Steel", 0.006)                   # top deck
    for i in range(9): box((0.006, D - 0.08, 0.008), (-0.34 + i * 0.085, 0.01, 0.436), "Chrome", 0)   # cup rails
    box((0.72, 0.2, 0.025), (0, -0.17, 0.10), "Steel", 0.004)                           # drip tray
    for i in range(16): box((0.004, 0.18, 0.004), (-0.33 + i * 0.044, -0.17, 0.115), "MatteBlack", 0)
    box((W, 0.05, 0.07), (0, -0.09, 0.37), "Steel", 0.008)                              # front brow
    for x in (-0.19, 0.19):
        cyl(0.05, 0.07, (x, -0.11, 0.30), "Chrome", verts=32)                           # group head
        cyl(0.045, 0.03, (x, -0.11, 0.255), "Steel", verts=32)                          # portafilter basket
        tube([(x, -0.11, 0.255), (x, -0.20, 0.255), (x, -0.30, 0.26)], 0.008, "Chrome")
        cyl(0.016, 0.12, (x, -0.33, 0.26), "Walnut", rot=(90, 0, 0), verts=16)           # walnut handle
        cyl(0.012, 0.03, (x - 0.012, -0.11, 0.225), "Chrome", verts=12)                 # spouts
        cyl(0.012, 0.03, (x + 0.012, -0.11, 0.225), "Chrome", verts=12)
        cyl(0.03, 0.015, (x, -0.075, 0.37), "GaugeFace", rot=(90, 0, 0), verts=24)       # gauges
        torus(0.03, 0.005, (x, -0.083, 0.37), "Chrome", rot=(90, 0, 0))
        box((0.04, 0.02, 0.02), (x, -0.075, 0.34), "MatteBlack", 0.003)                 # paddle
        cup(x - 0.02, -0.15, 0.115, 0.85)
    for side in (-1, 1):
        x = side * 0.36
        cyl(0.012, 0.03, (x, -0.10, 0.36), "Chrome", rot=(90, 0, 0), verts=12)
        tube([(x, -0.11, 0.36), (x, -0.16, 0.34), (x + side * 0.02, -0.17, 0.22), (x + side * 0.02, -0.17, 0.17)], 0.0055, "Chrome")
        sphere(0.015, (x, -0.10, 0.39), "MatteBlack")                                   # steam knob
    box((0.22, 0.008, 0.035), (0, -0.07, 0.40), "Glow", 0.002)                          # status display
    for i, (x, y) in enumerate([(-0.3, 0.1), (-0.2, 0.1), (-0.1, 0.12), (0.0, 0.1), (0.1, 0.12), (0.2, 0.1), (0.3, 0.1), (-0.25, -0.05), (0.25, -0.05)]):
        cup(x, y, 0.445, 0.75, "Ceramic" if i % 3 else "Sage", saucer=False)


def grinder():
    box((0.2, 0.26, 0.04), (0, 0, 0.02), "MatteBlack", 0.008)
    box((0.17, 0.22, 0.3), (0, 0.02, 0.19), "MatteBlack", 0.02)
    box((0.12, 0.05, 0.08), (0, -0.1, 0.25), "Steel", 0.006)                            # chute
    box((0.12, 0.08, 0.008), (0, -0.09, 0.06), "Chrome", 0.002)                         # fork
    cyl(0.07, 0.04, (0, 0.02, 0.36), "Steel", verts=32)
    cyl(0.085, 0.22, (0, 0.02, 0.49), "SmokedGlass", r2=0.06, verts=32)                 # hopper
    cyl(0.07, 0.12, (0, 0.02, 0.43), "Beans", r2=0.06, verts=24)
    cyl(0.09, 0.02, (0, 0.02, 0.61), "MatteBlack", verts=32)
    cyl(0.025, 0.02, (0.09, -0.04, 0.2), "Chrome", rot=(0, 90, 0), verts=20)            # dial


def knockbox_and_jugs():
    box((0.16, 0.16, 0.12), (0, 0, 0.06), "MatteBlack", 0.01)
    cyl(0.012, 0.15, (0, 0, 0.1), "Rubber", rot=(0, 90, 0), verts=12)
    for i, x in enumerate((0.16, 0.25)):
        cyl(0.04, 0.11, (x, 0.0, 0.055), "Steel", r2=0.034, verts=24)
        torus(0.022, 0.005, (x + 0.042, 0.0, 0.065), "Steel", rot=(90, 0, 0))
    box((0.3, 0.2, 0.012), (0.08, 0.15, 0.006), "Rubber", 0.003)                        # tamping mat
    cyl(0.03, 0.015, (0.0, 0.15, 0.02), "Chrome", verts=24); cyl(0.012, 0.06, (0.0, 0.15, 0.055), "Walnut", verts=16)


def range_cooker():
    W, D, H = 1.2, 0.8, 0.9
    box((W, D, 0.12), (0, 0, 0.06), "MatteBlack", 0.004)                                # kick
    box((W, D, H - 0.12), (0, 0, 0.12 + (H - 0.12) / 2), "Steel", 0.01)
    for x in (-0.3, 0.3):
        box((0.56, 0.02, 0.42), (x, -D / 2 - 0.005, 0.42), "BrushedSteel", 0.006)       # oven doors
        box((0.36, 0.01, 0.16), (x, -D / 2 - 0.015, 0.47), "SmokedGlass", 0.004)
        cyl(0.012, 0.46, (x, -D / 2 - 0.05, 0.66), "Chrome", rot=(0, 90, 0), verts=16)
        for s in (-1, 1): cyl(0.008, 0.04, (x + s * 0.21, -D / 2 - 0.03, 0.66), "Chrome", rot=(90, 0, 0), verts=12)
    box((W, 0.06, 0.1), (0, -D / 2 + 0.01, H - 0.06), "Steel", 0.006)                   # control rail
    for i in range(6):
        cyl(0.025, 0.03, (-0.5 + i * 0.2, -D / 2 - 0.035, H - 0.06), "MatteBlack", rot=(90, 0, 0), verts=24)
    box((W, D, 0.02), (0, 0, H + 0.01), "CastIron", 0.004)                              # hob
    for i, (x, y) in enumerate([(-0.4, -0.18), (0, -0.18), (0.4, -0.18), (-0.4, 0.18), (0, 0.18), (0.4, 0.18)]):
        cyl(0.07, 0.02, (x, y, H + 0.03), "CastIron", verts=24)
        for a in range(4): box((0.32, 0.018, 0.018), (x, y, H + 0.05), "CastIron", 0, rot=(0, 0, 45 * a))
        if i == 1:   # a stock pot
            cyl(0.15, 0.24, (x, y, H + 0.18), "Steel", verts=32); torus(0.15, 0.006, (x, y, H + 0.3), "Steel")
        if i == 3:   # a skillet
            cyl(0.13, 0.04, (x, y, H + 0.08), "CastIron", verts=32)
            cyl(0.012, 0.22, (x - 0.24, y, H + 0.09), "CastIron", rot=(0, 90, 0), verts=12)
        if i == 5:   # a copper saucepan
            cyl(0.1, 0.11, (x, y, H + 0.115), "Copper", verts=32)
            cyl(0.01, 0.18, (x + 0.18, y, H + 0.15), "Steel", rot=(0, 90, 0), verts=12)
    box((W, 0.04, 0.16), (0, D / 2 - 0.02, H + 0.08), "Steel", 0.006)                   # backguard


def hood():
    box((1.5, 0.9, 0.06), (0, 0, 2.06), "Steel", 0.006)                                 # canopy lip
    ctx, win = _ctx()
    with ctx: bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.0, radius2=0.32, depth=0.42, location=(0, 0, 2.3), rotation=(0, 0, math.radians(45)))
    o = win.view_layer.objects.active
    with ctx: bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    o.scale = (1.05, 0.62, 1)
    with ctx: bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    _finish(o, "Steel", 0.004)
    box((0.42, 0.34, 1.0), (0, 0.18, 3.0), "BrushedSteel", 0.004)                       # chimney
    for x in (-0.45, 0.45): box((0.22, 0.12, 0.006), (x, -0.1, 2.025), "WarmLight", 0)
    for i in range(10): box((0.006, 0.6, 0.02), (-0.6 + i * 0.133, 0.05, 2.04), "BrushedSteel", 0)   # baffle filters


def prep_table():
    W, D, H = 1.8, 0.75, 0.9
    box((W, D, 0.04), (0, 0, H - 0.02), "Steel", 0.004)
    box((W, 0.03, 0.12), (0, D / 2 - 0.015, H + 0.06), "Steel", 0.003)                  # upstand
    box((W - 0.08, D - 0.08, 0.02), (0, 0, 0.2), "BrushedSteel", 0.003)                 # under shelf
    for x in (-W / 2 + 0.04, W / 2 - 0.04):
        for y in (-D / 2 + 0.04, D / 2 - 0.04):
            cyl(0.02, H - 0.04, (x, y, (H - 0.04) / 2), "Steel", verts=16)
    box((0.5, 0.35, 0.03), (-0.3, -0.05, H + 0.015), "Oak", 0.006)                      # cutting board
    for i, x in enumerate((-0.42, -0.34, -0.26)): sphere(0.03, (x, -0.08, H + 0.055), "Herb" if i else "Terracotta")
    box((0.22, 0.03, 0.01), (-0.12, -0.1, H + 0.036), "Steel", 0.001, rot=(0, 0, 20))   # knife
    box((0.1, 0.022, 0.018), (-0.27, -0.14, H + 0.04), "Walnut", 0.003, rot=(0, 0, 20))
    for i, x in enumerate((0.35, 0.55)):                                                # steel bowls
        cyl(0.12, 0.08, (x, 0.0, H + 0.04), "Steel", r2=0.07, verts=32)
    for i in range(3): cyl(0.18, 0.012, (0.0 + i * 0.002, 0.0, 0.215 + i * 0.012), "Ceramic", verts=32)   # plates below
    for i in range(4): box((0.3, 0.2, 0.06), (-0.5 + i * 0.32, 0.12, 0.25), "Linen", 0.01)


def sink_unit():
    W, D, H = 1.4, 0.7, 0.9
    box((W, D, H - 0.1), (0, 0, (H - 0.1) / 2 + 0.1), "Steel", 0.008)
    box((W, D, 0.1), (0, 0, 0.05), "MatteBlack", 0.003)
    box((W, D, 0.03), (0, 0, H - 0.015), "Steel", 0.003)
    for x in (-0.33, 0.33):
        box((0.55, 0.45, 0.012), (x, -0.03, H - 0.002), "BrushedSteel", 0.002)
        box((0.5, 0.4, 0.02), (x, -0.03, H - 0.015), "CastIron", 0)                     # bowl shadow
        box((0.28, 0.012, 0.02), (x, -D / 2 - 0.01, 0.6), "Chrome", 0.003)              # door pull
        box((0.62, 0.012, 0.66), (x, -D / 2 - 0.002, 0.45), "BrushedSteel", 0.004)      # doors
    box((W, 0.03, 0.12), (0, D / 2 - 0.015, H + 0.06), "Steel", 0.003)
    tube([(0, 0.27, H), (0, 0.27, H + 0.38), (0, 0.15, H + 0.44), (0, 0.04, H + 0.36), (0, 0.04, H + 0.28)], 0.012, "Chrome")
    cyl(0.022, 0.1, (0, 0.27, H + 0.05), "Chrome", verts=20)
    for s in (-1, 1): box((0.07, 0.015, 0.015), (s * 0.06, 0.27, H + 0.1), "Chrome", 0.003)
    for i in range(4): cyl(0.14, 0.012, (0.55, 0.12, H + 0.15 + i * 0.03), "Ceramic", rot=(80, 0, 0), verts=28)   # drying plates
    box((0.3, 0.2, 0.01), (0.55, 0.1, H + 0.005), "Chrome", 0.002)


def fridge():
    W, D, H = 0.72, 0.75, 2.0
    box((W, D, 0.1), (0, 0, 0.05), "MatteBlack", 0.003)
    box((W, D, H - 0.1), (0, 0, 0.1 + (H - 0.1) / 2), "BrushedSteel", 0.008)
    box((W - 0.02, 0.02, H - 0.15), (0, -D / 2 - 0.005, 0.1 + (H - 0.1) / 2), "Steel", 0.005)
    for z in (0.6, 1.5): cyl(0.012, 0.42, (W / 2 - 0.07, -D / 2 - 0.05, z), "Chrome", verts=12)
    box((0.12, 0.01, 0.05), (-0.18, -D / 2 - 0.02, H - 0.12), "Glow", 0.002)            # temperature display


def wall_shelf():
    L = 1.6
    for z in (1.55, 1.95):
        box((L, 0.28, 0.035), (0, 0, z), "Oak", 0.005)
        for x in (-0.6, 0.6): box((0.03, 0.22, 0.12), (x, 0.03, z - 0.075), "MatteBlack", 0.003)
    for i in range(6): cyl(0.13, 0.01, (-0.55, 0.0, 1.575 + i * 0.012), "Ceramic", verts=28)       # plates
    for i in range(6): cyl(0.1, 0.01, (-0.25, 0.0, 1.575 + i * 0.012), "Sage", verts=28)
    for i in range(5): cup(0.05 + i * 0.1, -0.02, 1.57, 1.0, "Ceramic" if i % 2 else "Sage", saucer=False)
    for i, (x, h) in enumerate([(-0.55, 0.2), (-0.35, 0.16), (-0.15, 0.24), (0.05, 0.18)]):
        cyl(0.06, h, (x, 0.0, 1.97 + h / 2), "Glass", verts=24)
        cyl(0.055, h * 0.7, (x, 0.0, 1.97 + h * 0.35), "Beans" if i % 2 == 0 else "Linen", verts=24)
        cyl(0.062, 0.02, (x, 0.0, 1.97 + h), "Oak", verts=24)
    for i in range(3):                                                                  # potted herbs
        x = 0.35 + i * 0.18
        cyl(0.06, 0.1, (x, 0.0, 2.02), "Terracotta", r2=0.07, verts=20)
        sphere(0.08, (x, 0.0, 2.12), "Herb", (1, 1, 0.8))


def pot_rail():
    cyl(0.012, 1.4, (0, 0, 2.05), "Chrome", rot=(0, 90, 0), verts=16)
    for x in (-0.68, 0.68): box((0.03, 0.06, 0.06), (x, 0.03, 2.05), "Chrome", 0.003)
    for i, (x, r, material) in enumerate([(-0.45, 0.13, "Copper"), (-0.12, 0.11, "CastIron"), (0.2, 0.09, "Copper"), (0.48, 0.08, "Steel")]):
        torus(0.025, 0.004, (x, 0, 2.02), "Chrome", rot=(90, 0, 0))
        cyl(0.01, 0.2, (x, 0, 1.9), "Steel" if material != "CastIron" else "CastIron", verts=10)
        cyl(r, 0.03, (x, 0, 1.78 - r), material, rot=(90, 0, 0), verts=32)
    for i, x in enumerate((0.6, 0.66)):                                                 # ladles
        cyl(0.005, 0.3, (x, 0, 1.88), "Steel", verts=8); sphere(0.035, (x, 0, 1.72), "Steel", (1, 1, 0.5))


def pass_shelf():
    box((1.6, 0.4, 0.03), (0, 0, 0.0 + 0.015), "Steel", 0.003)
    for x in (-0.75, 0.75): box((0.03, 0.03, 0.5), (x, 0.17, 0.27), "Steel", 0.002)
    box((1.6, 0.12, 0.06), (0, 0.05, 0.53), "MatteBlack", 0.006)                         # heat lamp bar
    for x in (-0.5, 0.0, 0.5): box((0.3, 0.008, 0.03), (x, 0.0, 0.497), "Glow", 0)
    box((1.3, 0.01, 0.06), (0, -0.21, 0.04), "Chrome", 0.002)                           # ticket rail


def pendant():
    cyl(0.004, 0.9, (0, 0, 0.45 + 0.2), "MatteBlack", verts=8)
    cyl(0.17, 0.2, (0, 0, 0.1), "MatteBlack", r2=0.04, verts=40)
    cyl(0.15, 0.005, (0, 0, 0.0), "WarmLight", verts=40)


ITEMS = {
    "EspressoMachine": espresso_machine, "CoffeeGrinder": grinder, "BaristaTools": knockbox_and_jugs, "Range": range_cooker,
    "ExtractorHood": hood, "PrepTable": prep_table, "SinkUnit": sink_unit, "Fridge": fridge, "WallShelf": wall_shelf,
    "PotRail": pot_rail, "PassShelf": pass_shelf, "KitchenPendant": pendant,
}


def build(name, render=True):
    clear()
    PARTS.clear()
    ITEMS[name]()
    ctx, win = _ctx()
    with ctx:
        bpy.ops.object.select_all(action='DESELECT')
        for o in PARTS:
            o.select_set(True)
            win.view_layer.objects.active = o
            for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
        bpy.ops.object.select_all(action='DESELECT')
        for o in PARTS: o.select_set(True)
        win.view_layer.objects.active = PARTS[0]
        bpy.ops.object.join()
        o = win.view_layer.objects.active
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
    o.name = o.data.name = name
    mn = mathutils.Vector([min(v.co[i] for v in o.data.vertices) for i in range(3)])
    mx = mathutils.Vector([max(v.co[i] for v in o.data.vertices) for i in range(3)])
    if name not in ("ExtractorHood", "PotRail", "WallShelf", "KitchenPendant", "PassShelf"):
        o.data.transform(mathutils.Matrix.Translation((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z)))
    else:
        o.data.transform(mathutils.Matrix.Translation((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, 0)))
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    for out in OUTS: os.makedirs(out, exist_ok=True)
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); win.view_layer.objects.active = o
        bpy.ops.export_scene.gltf(filepath=OUTS[0] + name + ".glb", use_selection=True, export_format='GLB', export_yup=True)
    for out in OUTS[1:]:
        with open(OUTS[0] + name + ".glb", 'rb') as a, open(out + name + ".glb", 'wb') as b: b.write(a.read())
    if render: shot(o, name)
    return {"tris": tris, "size": [round(v, 2) for v in (mx - mn)]}


def shot(o, name):
    ctx, win = _ctx(); sc = win.scene
    os.makedirs(SHOTS, exist_ok=True)
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = 640; sc.render.resolution_y = 480
    world = sc.world or bpy.data.worlds.new("World"); sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.82, 0.85, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    light = bpy.data.lights.new("Key", 'AREA'); light.energy = 400; light.size = 3
    lo = bpy.data.objects.new("Key", light); sc.collection.objects.link(lo); lo.location = (2, -3, 4); lo.rotation_euler = (math.radians(45), 0, math.radians(30))
    cam_data = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_data); sc.collection.objects.link(cam); sc.camera = cam
    size = max(o.dimensions)
    centre = o.matrix_world @ (sum((mathutils.Vector(c) for c in o.bound_box), mathutils.Vector()) / 8)
    cam.location = centre + mathutils.Vector((size * 1.0, -size * 1.6, size * 0.7))
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = 45
    sc.render.filepath = SHOTS + name + ".png"
    with ctx: bpy.ops.render.render(write_still=True)


def build_all(names=None):
    report = {}
    for name in names or ITEMS:
        try: report[name] = build(name)
        except Exception as e:
            import traceback; report[name] = "FAILED: " + traceback.format_exc()[-500:]
    return report
