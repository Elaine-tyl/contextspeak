"""Water interaction test assets — runs inside Blender 5.3 (Blender MCP or Scripting tab).

Exports to Assets/Art/water/ (Y-up GLB, metres, origin at deck/ground level):
  Pool        10 × 5 m basin: shallow end 1.0 m with entry steps, slope, deep end 2.0 m, ladder,
              rounded coping and a stone deck. Interior material "PoolTile" has UVs in metres
              (cube projection, 1 UV unit = 1 m) so Unity tiles real-size mosaic on it.
  PoolWater   dense surface grid (10 × 5 m, 160 × 80 quads) for ripple displacement.
  PondBasin   16 × 16 m natural basin (1.6 m deep), muddy bed below the waterline, grassy banks, rocks.
  PondWater   13 × 13 m dense grid.
  RiverBed    30 × 9 m channel (0.9 m deep) with stones in the current.
  RiverWater  30 × 6 m dense grid.
"""
import bpy, bmesh, math, os, random
import mathutils

OUTS = [
    "/Users/elaineting/.codex/worktrees/capture-to-library/eng learning/games/unity/Assets/Art/water/",
    "/Users/elaineting/Projects/ContextSpeak/games/unity/Assets/Art/water/",
]
SHOTS = "/Users/elaineting/Projects/ContextSpeak/.local/water_shots/"

COLOURS = {
    "PoolTile": ((0.55, 0.78, 0.86), 0.0, 0.15), "PoolLine": ((0.06, 0.16, 0.3), 0.0, 0.15),
    "PoolCoping": ((0.86, 0.84, 0.79), 0.0, 0.55), "PoolDeck": ((0.72, 0.68, 0.6), 0.0, 0.7),
    "Chrome": ((0.92, 0.92, 0.93), 1.0, 0.08), "PondBed": ((0.22, 0.19, 0.14), 0.0, 0.8),
    "PondBank": ((0.32, 0.42, 0.2), 0.0, 0.85), "Rock": ((0.42, 0.41, 0.38), 0.0, 0.75),
    "MossRock": ((0.3, 0.36, 0.22), 0.0, 0.8), "Gravel": ((0.45, 0.41, 0.35), 0.0, 0.85), "Water": ((0.3, 0.5, 0.55), 0.0, 0.05),
}


def _ctx():
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == 'VIEW_3D')
    return bpy.context.temp_override(window=win, area=area, scene=win.scene, view_layer=win.view_layer), win


def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0: block.remove(item)


def mat(name):
    m = bpy.data.materials.get(name)
    if m: return m
    colour, metal, rough = COLOURS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*colour, 1); b.inputs["Metallic"].default_value = metal; b.inputs["Roughness"].default_value = rough
    return m


def new_object(name, bm):
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new(name, mesh)
    _ctx()[1].scene.collection.objects.link(o)
    return o


def add_box(name, size, loc, material, bevel=0.0):
    ctx, win = _ctx()
    with ctx: bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = win.view_layer.objects.active; o.name = name; o.scale = size
    with ctx: bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat(material))
    if bevel: m = o.modifiers.new("b", 'BEVEL'); m.width = bevel; m.segments = 3
    return o


def tube(points, r, material):
    curve = bpy.data.curves.new("tube", 'CURVE'); curve.dimensions = '3D'; curve.bevel_depth = r; curve.bevel_resolution = 4
    spline = curve.splines.new('POLY'); spline.points.add(len(points) - 1)
    for i, p in enumerate(points): spline.points[i].co = (*p, 1)
    o = bpy.data.objects.new("tube", curve)
    ctx, win = _ctx(); win.scene.collection.objects.link(o)
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); win.view_layer.objects.active = o
        bpy.ops.object.convert(target='MESH')
    o = win.view_layer.objects.active
    o.data.materials.clear(); o.data.materials.append(mat(material))
    return o


def rock(loc, size, material="Rock", seed=0):
    ctx, win = _ctx()
    with ctx: bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=loc)
    o = win.view_layer.objects.active
    rnd = random.Random(seed)
    for v in o.data.vertices:
        n = v.co.normalized()
        v.co = v.co * (1 + 0.22 * math.sin(n.x * 5 + seed) * math.cos(n.y * 4 - seed) + 0.1 * rnd.random())
    o.scale = (size * rnd.uniform(0.8, 1.3), size * rnd.uniform(0.7, 1.1), size * rnd.uniform(0.45, 0.75))
    o.rotation_euler = (0, 0, rnd.uniform(0, 6.28))
    with ctx: bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.data.materials.append(mat(material))
    return o


def grid(name, sx, sy, nx, ny, height=lambda x, y: 0.0, material=None):
    bm = bmesh.new()
    verts = [[bm.verts.new((-sx / 2 + sx * i / nx, -sy / 2 + sy * j / ny, 0)) for i in range(nx + 1)] for j in range(ny + 1)]
    for j in range(ny + 1):
        for i in range(nx + 1):
            v = verts[j][i]; v.co.z = height(v.co.x, v.co.y)
    for j in range(ny):
        for i in range(nx): bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for loop in f.loops: loop[uv].uv = (loop.vert.co.x, loop.vert.co.y)       # 1 UV = 1 m
    o = new_object(name, bm)
    if material: o.data.materials.append(mat(material))
    return o


def finish(objs, name):
    ctx, win = _ctx()
    with ctx:
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:
            o.select_set(True); win.view_layer.objects.active = o
            for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        win.view_layer.objects.active = objs[0]
        if len(objs) > 1: bpy.ops.object.join()
        o = win.view_layer.objects.active
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
    o.name = o.data.name = name
    for out in OUTS: os.makedirs(out, exist_ok=True)
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); win.view_layer.objects.active = o
        bpy.ops.export_scene.gltf(filepath=OUTS[0] + name + ".glb", use_selection=True, export_format='GLB', export_yup=True)
    for out in OUTS[1:]:
        with open(OUTS[0] + name + ".glb", 'rb') as a, open(out + name + ".glb", 'wb') as b: b.write(a.read())
    return o


def metre_uvs(o):
    """Cube-project UVs so 1 UV unit = 1 m (for real-size tiles)."""
    ctx, win = _ctx()
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); win.view_layer.objects.active = o
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')


# ------------------------------------------------------------------------------------------- pool
def pool():
    L, W, shallow, deep = 10.0, 5.0, 1.0, 2.0
    deck = add_box("deck", (L + 5, W + 5, 0.4), (0, 0, -0.2), "PoolDeck")
    # Cutter: shallow floor (x < -1), slope to the deep end (x > 1.5).
    bm = bmesh.new()
    profile = [(-L / 2, 0.5), (-L / 2, -shallow), (-1.0, -shallow), (1.5, -deep), (L / 2, -deep), (L / 2, 0.5)]
    front = [bm.verts.new((x, -W / 2, z)) for x, z in profile]
    back = [bm.verts.new((x, W / 2, z)) for x, z in profile]
    bm.faces.new(front[::-1]); bm.faces.new(back)
    for i in range(len(profile)):
        j = (i + 1) % len(profile)
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    cutter = new_object("cutter", bm)
    cutter.data.materials.append(mat("PoolTile"))
    boolean = deck.modifiers.new("cut", 'BOOLEAN'); boolean.operation = 'DIFFERENCE'; boolean.object = cutter; boolean.solver = 'EXACT'
    boolean.material_mode = 'TRANSFER'
    ctx, win = _ctx()
    with ctx:
        bpy.ops.object.select_all(action='DESELECT'); deck.select_set(True); win.view_layer.objects.active = deck
        bpy.ops.object.modifier_apply(modifier="cut")
    bpy.data.objects.remove(cutter)
    metre_uvs(deck)
    parts = [deck]
    # Entry steps in the shallow end (tiled), full width of the end wall.
    for k in range(3):
        h = shallow - (k + 1) * 0.25
        parts.append(add_box("step", (0.4, 2.0, shallow - h), (-L / 2 + 0.2 + k * 0.4, -W / 2 + 1.0 + 0.0, -shallow + (shallow - h) / 2), "PoolTile"))
    for p in parts[1:]: metre_uvs(p)
    # Lane line on the floor.
    parts.append(add_box("lane line", (L - 3.2, 0.22, 0.004), (0.5, 0, -1.5), "PoolLine"))
    # Rounded coping around the rim.
    for (sx, sy, x, y) in [(L + 0.7, 0.35, 0, -W / 2 - 0.175), (L + 0.7, 0.35, 0, W / 2 + 0.175), (0.35, W, -L / 2 - 0.175, 0), (0.35, W, L / 2 + 0.175, 0)]:
        parts.append(add_box("coping", (sx, sy, 0.06), (x, y, 0.03), "PoolCoping", 0.02))
    # Ladder at the deep end.
    for y in (W / 2 - 1.3, W / 2 - 0.8):
        parts.append(tube([(L / 2 - 1.0, y, -1.2), (L / 2 - 1.0, y, 0.0 - 0.0), (L / 2 - 1.0, y, 0.75), (L / 2 - 0.7, y, 0.95), (L / 2 - 0.2, y, 0.85), (L / 2 - 0.2, y, 0.06)], 0.022, "Chrome"))
    for z in (-0.95, -0.6, -0.25):
        parts.append(add_box("rung", (0.06, 0.5, 0.04), (L / 2 - 1.0 + 0.0, W / 2 - 1.05, z), "Chrome", 0.01))
    finish(parts, "Pool")
    clear()
    finish([grid("water", L, W, 160, 80, material="Water")], "PoolWater")


# ------------------------------------------------------------------------------------------- pond
def pond():
    rnd = random.Random(4)

    def h(x, y):
        r = math.hypot(x / 6.2, y / 5.4) + 0.08 * math.sin(math.atan2(y, x) * 3 + 1) + 0.05 * math.sin(x * 0.9 + y * 1.3)
        if r < 1:
            return -1.6 * (1 - r * r) ** 0.6 + 0.05 * math.sin(x * 2.1) * math.cos(y * 1.7)
        return 0.06 * math.sin(x * 0.7) * math.cos(y * 0.6) + min(0.25, (r - 1) * 0.3)
    basin = grid("basin", 16, 16, 96, 96, h)
    basin.data.materials.append(mat("PondBed")); basin.data.materials.append(mat("PondBank"))
    for poly in basin.data.polygons: poly.material_index = 0 if poly.center.z < -0.08 else 1
    parts = [basin]
    for i in range(16):
        a = i / 16 * 6.283 + rnd.uniform(-0.15, 0.15)
        r = rnd.uniform(0.92, 1.05)
        x, y = math.cos(a) * 6.2 * r, math.sin(a) * 5.4 * r
        parts.append(rock((x, y, h(x, y) + 0.05), rnd.uniform(0.25, 0.6), "MossRock" if i % 3 else "Rock", i))
    parts.append(rock((1.5, -1.0, -0.7), 0.7, "Rock", 31))
    finish(parts, "PondBasin")
    clear()
    finish([grid("water", 13, 13, 120, 120, material="Water")], "PondWater")


# ------------------------------------------------------------------------------------------ river
def river():
    rnd = random.Random(9)

    def h(x, y):
        centre = 0.5 * math.sin(x * 0.18)
        d = abs(y - centre)
        if d < 3: return -0.9 * (1 - (d / 3) ** 2) + 0.04 * math.sin(x * 1.3 + y)
        return min(0.35, (d - 3) * 0.25) + 0.03 * math.sin(x * 0.8)
    bed = grid("bed", 30, 9, 150, 45, h)
    bed.data.materials.append(mat("Gravel")); bed.data.materials.append(mat("PondBank"))
    for poly in bed.data.polygons: poly.material_index = 0 if poly.center.z < 0.02 else 1
    parts = [bed]
    for i in range(14):
        x = -13 + i * 2 + rnd.uniform(-0.6, 0.6)
        y = 0.5 * math.sin(x * 0.18) + rnd.uniform(-2.2, 2.2)
        parts.append(rock((x, y, h(x, y) + 0.1), rnd.uniform(0.25, 0.55), "MossRock" if i % 2 else "Rock", 100 + i))
    finish(parts, "RiverBed")
    clear()
    finish([grid("water", 30, 6, 150, 30, material="Water")], "RiverWater")


def shot(name):
    ctx, win = _ctx(); sc = win.scene
    clear()
    with ctx: bpy.ops.import_scene.gltf(filepath=OUTS[0] + name + ".glb")
    o = next(o for o in bpy.data.objects if o.type == 'MESH')
    os.makedirs(SHOTS, exist_ok=True)
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 640; sc.render.resolution_y = 400
    world = sc.world or bpy.data.worlds.new("World"); sc.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.8, 0.88, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 1
    sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 3
    so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(30))
    cam_data = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_data); sc.collection.objects.link(cam); sc.camera = cam
    size = max(o.dimensions)
    cam.location = (size * 0.55, -size * 0.75, size * 0.55)
    cam.rotation_euler = (mathutils.Vector((0, 0, -0.5)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = SHOTS + name + ".png"
    with ctx: bpy.ops.render.render(write_still=True)


def build_all():
    out = {}
    for fn in (pool, pond, river):
        clear()
        try: fn(); out[fn.__name__] = "ok"
        except Exception as e:
            import traceback; out[fn.__name__] = traceback.format_exc()[-700:]
    for name in ("Pool", "PondBasin", "RiverBed"):
        try: shot(name)
        except Exception as e: out["shot " + name] = repr(e)
    return out
