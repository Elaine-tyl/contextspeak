"""Tropical beach assets for ContextSpeak (real-world metres, GLB). Vegetation materials are named
Leaf*/Trunk*/Flower* so Unity can swap in the wind-sway shader. Boats: bow faces Blender -Y (= Unity +Z)."""
import bpy, bmesh, math, sys, os, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(__file__))
from vehicles import reset, mat, box, export, preview, MATS

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'

def root(name):
    o = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(o); return o

def mesh_obj(name, verts, faces, material, parent, smooth=True):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o.parent = parent
    me.materials.append(material)
    for p in me.polygons: p.use_smooth = smooth
    return o

def tube(name, points, radii, material, parent, segs=10):
    """A tapered tube through 3D points (trunks, stems)."""
    verts, faces = [], []
    for i, (p, r) in enumerate(zip(points, radii)):
        d = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
        a = d.orthogonal().normalized(); b = d.cross(a).normalized()
        for s in range(segs):
            t = 2 * math.pi * s / segs
            verts.append(tuple(Vector(p) + (a * math.cos(t) + b * math.sin(t)) * r))
    for i in range(len(points) - 1):
        for s in range(segs):
            j = (s + 1) % segs
            faces.append((i * segs + s, i * segs + j, (i + 1) * segs + j, (i + 1) * segs + s))
    return mesh_obj(name, verts, faces, material, parent)

def frond(name, base, direction, length, width, droop, material, parent, leaflets=True):
    """A palm frond: a curved rachis with leaflets as a ribbon that tapers and droops."""
    verts, faces = [], []
    d = Vector(direction).normalized(); side = Vector((-d.y, d.x, 0)).normalized()
    n = 14
    for i in range(n + 1):
        t = i / n
        p = Vector(base) + d * length * t + Vector((0, 0, 0.25 * length * math.sin(math.pi * t * 0.6) - droop * t * t * length))
        w = width * math.sin(math.pi * min(1, t * 1.15)) * (1 - 0.3 * t)
        fold = Vector((0, 0, -w * 0.25))
        verts += [tuple(p - side * w + fold), tuple(p), tuple(p + side * w + fold)]
    for i in range(n):
        a = i * 3
        faces += [(a, a + 1, a + 4, a + 3), (a + 1, a + 2, a + 5, a + 4)]
    o = mesh_obj(name, verts, faces, material, parent)
    return o

def palm(name, height, lean, seed):
    reset(); random.seed(seed)
    trunk = mat('Trunk palm', (0.42, 0.33, 0.22), 0.85)
    leaf = mat('Leaf palm', (0.22, 0.42, 0.12), 0.6)
    nut = mat('Trunk coconut', (0.35, 0.25, 0.1), 0.6)
    r = root(name)
    pts, radii = [], []
    for i in range(13):
        t = i / 12
        pts.append((lean * height * t * t, 0, height * t))
        radii.append(0.2 * (1 - 0.35 * t) + (0.03 if i % 2 else 0))
    tube('Trunk', pts, radii, trunk, r, 12)
    top = Vector(pts[-1])
    for k in range(13):
        ang = 2 * math.pi * k / 13 + random.uniform(-0.2, 0.2)
        d = (math.cos(ang), math.sin(ang), random.uniform(-0.1, 0.4))
        frond('Frond', top, d, random.uniform(3.6, 4.6), 0.55, random.uniform(0.55, 0.9), leaf, r)
    for k in range(5):
        a = 2 * math.pi * k / 5
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.14, location=(top.x + 0.25 * math.cos(a), 0.25 * math.sin(a), top.z - 0.35))
        o = bpy.context.active_object; o.data.materials.append(nut); o.parent = r
    return r

def leafy(name, kind, seed):
    reset(); random.seed(seed)
    r = root(name)
    if kind == 'banana':
        stem = mat('Trunk banana', (0.45, 0.55, 0.25), 0.7); leaf = mat('Leaf banana', (0.3, 0.55, 0.15), 0.55)
        tube('Stem', [(0, 0, 0), (0, 0, 1.4), (0, 0, 2.6)], [0.16, 0.13, 0.1], stem, r)
        for k in range(8):
            a = 2 * math.pi * k / 8 + random.uniform(-0.2, 0.2)
            frond('Leaf', (0, 0, 2.4 + random.uniform(-0.3, 0.3)), (math.cos(a), math.sin(a), 0.6), 2.0, 0.42, 0.5, leaf, r)
    elif kind == 'hibiscus':
        leaf = mat('Leaf hibiscus', (0.12, 0.34, 0.1), 0.5); flower = mat('Flower hibiscus', (0.85, 0.08, 0.15), 0.4)
        for k in range(14):
            bpy.ops.mesh.primitive_ico_sphere_add(radius=random.uniform(0.35, 0.55), subdivisions=2, location=(random.uniform(-0.6, 0.6), random.uniform(-0.6, 0.6), random.uniform(0.4, 1.3)))
            o = bpy.context.active_object; o.data.materials.append(leaf); o.parent = r
        for k in range(16):
            a = random.uniform(0, 2 * math.pi); h = random.uniform(0.5, 1.5)
            bpy.ops.mesh.primitive_cone_add(radius1=0.12, depth=0.1, location=(0.85 * math.cos(a), 0.85 * math.sin(a), h), rotation=(math.pi / 2, 0, a + math.pi / 2))
            o = bpy.context.active_object; o.data.materials.append(flower); o.parent = r
    elif kind == 'paradise':
        leaf = mat('Leaf paradise', (0.2, 0.45, 0.25), 0.5); orange = mat('Flower orange', (1.0, 0.45, 0.05), 0.4); blue = mat('Flower blue', (0.15, 0.25, 0.8), 0.4)
        for k in range(9):
            a = 2 * math.pi * k / 9
            frond('Leaf', (0, 0, 0.1), (math.cos(a) * 0.35, math.sin(a) * 0.35, 1), 1.5, 0.18, 0.15, leaf, r)
        for k in range(3):
            a = 2 * math.pi * k / 3
            p = Vector((0.25 * math.cos(a), 0.25 * math.sin(a), 1.35))
            box('Bract', (0.08, 0.35, 0.05), p, leaf, 0, r)
            bpy.ops.mesh.primitive_cone_add(radius1=0.06, depth=0.3, location=p + Vector((0, 0.1, 0.12)), rotation=(-0.6, 0, a))
            o = bpy.context.active_object; o.data.materials.append(orange); o.parent = r
            box('Petal', (0.03, 0.14, 0.03), p + Vector((0, 0.18, 0.06)), blue, 0, r)
    elif kind == 'fern':
        leaf = mat('Leaf fern', (0.18, 0.4, 0.12), 0.55)
        for k in range(10):
            a = 2 * math.pi * k / 10 + random.uniform(-0.15, 0.15)
            frond('Frond', (0, 0, 0.05), (math.cos(a), math.sin(a), 0.9), 0.9, 0.16, 0.6, leaf, r)
    elif kind == 'grass':
        leaf = mat('Leaf beach grass', (0.55, 0.58, 0.3), 0.7)
        for k in range(24):
            a = random.uniform(0, 2 * math.pi); rr = random.uniform(0, 0.25)
            frond('Blade', (rr * math.cos(a), rr * math.sin(a), 0), (math.cos(a) * 0.25, math.sin(a) * 0.25, 1), random.uniform(0.5, 0.9), 0.025, 0.3, leaf, r)
    elif kind == 'shade':
        trunk = mat('Trunk sea almond', (0.35, 0.28, 0.2), 0.8); leaf = mat('Leaf sea almond', (0.16, 0.38, 0.12), 0.55)
        tube('Trunk', [(0, 0, 0), (0.2, 0, 2.5), (0.1, 0, 4.5)], [0.35, 0.28, 0.2], trunk, r)
        for tier, z in enumerate((3.2, 4.6, 6.0)):
            for k in range(6):
                a = 2 * math.pi * k / 6 + tier * 0.5
                tube('Branch', [(0.1, 0, z - 0.8), (2.6 * math.cos(a), 2.6 * math.sin(a), z)], [0.09, 0.05], trunk, r, 6)
                bpy.ops.mesh.primitive_ico_sphere_add(radius=1.2 - tier * 0.15, subdivisions=2, location=(2.8 * math.cos(a), 2.8 * math.sin(a), z + 0.2))
                o = bpy.context.active_object; o.scale = (1.3, 1.3, 0.45); o.data.materials.append(leaf); o.parent = r
    return r

def props():
    """Shells, driftwood, rocks, sun lounger, umbrella, coral, fish (one GLB each)."""
    out = []
    reset(); r = root('BeachShell'); shell = mat('Shell', (0.95, 0.85, 0.75), 0.4)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.05, location=(0, 0, 0.02)); o = bpy.context.active_object; o.scale = (1, 1.3, 0.45); o.data.materials.append(shell); o.parent = r
    export(r, os.path.join(OUT, 'BeachShell.glb'))
    reset(); r = root('Driftwood'); wood = mat('Driftwood', (0.62, 0.56, 0.48), 0.9)
    tube('Log', [(-1.2, 0, 0.12), (0, 0.15, 0.15), (1.1, -0.1, 0.1), (1.6, 0.2, 0.14)], [0.12, 0.14, 0.1, 0.05], wood, r, 8)
    tube('Branch', [(0.3, 0.1, 0.15), (0.7, 0.7, 0.3)], [0.05, 0.02], wood, r, 6)
    export(r, os.path.join(OUT, 'Driftwood.glb'))
    reset(); r = root('CoastRock'); rock = mat('Rock', (0.45, 0.43, 0.4), 0.85)
    bpy.ops.mesh.primitive_ico_sphere_add(radius=1, subdivisions=3); o = bpy.context.active_object; o.parent = r; o.data.materials.append(rock)
    random.seed(4)
    for v in o.data.vertices: v.co *= random.uniform(0.75, 1.15); v.co.z *= 0.6
    export(r, os.path.join(OUT, 'CoastRock.glb'))
    reset(); r = root('SunLounger'); frame = mat('Lounger frame', (0.9, 0.9, 0.88), 0.4); cushion = mat('Cushion', (0.05, 0.55, 0.6), 0.7)
    box('Base', (0.7, 1.9, 0.08), (0, 0, 0.35), frame, 0.02, r); box('Cushion', (0.65, 1.3, 0.08), (0, 0.25, 0.43), cushion, 0.03, r)
    bk = box('Back', (0.65, 0.7, 0.08), (0, -0.75, 0.6), cushion, 0.03, r); bk.rotation_euler = (math.radians(-40), 0, 0)
    for x in (-0.3, 0.3):
        for y in (-0.8, 0.8): box('Leg', (0.05, 0.05, 0.35), (x, y, 0.17), frame, 0, r)
    export(r, os.path.join(OUT, 'SunLounger.glb'))
    reset(); r = root('BeachUmbrella'); pole = mat('Umbrella pole', (0.6, 0.45, 0.3), 0.6); canvas = mat('Umbrella canvas', (0.95, 0.55, 0.35), 0.8)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.03, depth=2.4, location=(0, 0, 1.2)); o = bpy.context.active_object; o.data.materials.append(pole); o.parent = r
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=1.4, depth=0.45, location=(0, 0, 2.3)); o = bpy.context.active_object; o.data.materials.append(canvas); o.parent = r
    export(r, os.path.join(OUT, 'BeachUmbrella.glb'))
    reset(); r = root('Coral'); random.seed(7)
    for k, colour in enumerate(((0.95, 0.4, 0.45), (0.95, 0.65, 0.2), (0.6, 0.3, 0.75), (0.2, 0.7, 0.65))):
        m = mat('Coral %d' % k, colour, 0.6)
        for j in range(4):
            base = (random.uniform(-0.8, 0.8), random.uniform(-0.8, 0.8), 0)
            tube('Branch', [base, (base[0] + random.uniform(-0.2, 0.2), base[1] + random.uniform(-0.2, 0.2), random.uniform(0.4, 0.9))], [0.08, 0.03], m, r, 6)
        bpy.ops.mesh.primitive_ico_sphere_add(radius=0.3, subdivisions=2, location=(random.uniform(-0.8, 0.8), random.uniform(-0.8, 0.8), 0.15)); o = bpy.context.active_object; o.scale = (1, 1, 0.6); o.data.materials.append(m); o.parent = r
    export(r, os.path.join(OUT, 'Coral.glb'))
    for name, colour, stripe in (('FishBlue', (0.1, 0.35, 0.9), (1, 0.85, 0.1)), ('FishClown', (1, 0.45, 0.05), (1, 1, 1)), ('FishYellow', (1, 0.85, 0.1), (0.1, 0.1, 0.1))):
        reset(); r = root(name); body = mat(name + ' body', colour, 0.4); band = mat(name + ' stripe', stripe, 0.4)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.1, location=(0, 0, 0)); o = bpy.context.active_object; o.scale = (0.45, 1.3, 0.9); o.data.materials.append(body); o.parent = r
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.08, depth=0.1, location=(0, 0.16, 0), rotation=(math.radians(-90), 0, 0)); o = bpy.context.active_object; o.scale = (0.2, 1, 1); o.data.materials.append(body); o.parent = r
        box('Stripe', (0.1, 0.03, 0.17), (0, -0.02, 0), band, 0, r)
        export(r, os.path.join(OUT, name + '.glb'))
    return out

def hull(name, length, beam, depth, colour, deck=None, bow_rise=0.25):
    """A boat hull: pointed bow at -Y, flat transom at +Y."""
    m = mat(name + ' hull', colour, 0.35)
    verts, faces = [], []
    n = 12
    for i in range(n + 1):
        t = i / n; y = -length / 2 + length * t
        w = beam / 2 * (math.sin(min(1, t * 1.6) * math.pi / 2) ** 0.8)
        z0 = -depth + bow_rise * (1 - t) ** 3; z1 = 0.0 + bow_rise * (1 - t) ** 2
        verts += [(-w, y, z1), (-w * 0.55, y, z0 + depth * 0.25), (0, y, z0), (w * 0.55, y, z0 + depth * 0.25), (w, y, z1)]
    for i in range(n):
        a = i * 5
        for k in range(4): faces.append((a + k, a + k + 1, a + k + 6, a + k + 5))
    last = n * 5; faces.append((last, last + 1, last + 2, last + 3, last + 4))
    return m, verts, faces

def boat(name, kind):
    reset(); r = root(name)
    if kind == 'kayak':
        m, v, f = hull(name, 3.8, 0.7, 0.3, (1.0, 0.45, 0.05), bow_rise=0.12); mesh_obj('Hull', v, f, m, r)
        box('Cockpit rim', (0.45, 0.8, 0.06), (0, 0.2, 0.05), mat('Black', (0.05, 0.05, 0.05), 0.6), 0.02, r)
        box('Paddle shaft', (0.03, 0.03, 2.2), (0, 0.2, 0.3), mat('Paddle', (0.2, 0.2, 0.22), 0.5), 0, r).rotation_euler = (0, math.radians(80), 0)
    elif kind == 'jetski':
        m, v, f = hull(name, 3.2, 1.2, 0.45, (0.95, 0.95, 0.97), bow_rise=0.2); mesh_obj('Hull', v, f, m, r)
        stripe = mat('Jet ski stripe', (0.05, 0.55, 0.85), 0.35)
        box('Body', (0.9, 1.6, 0.35), (0, 0.2, 0.18), stripe, 0.12, r)
        box('Seat', (0.45, 1.0, 0.15), (0, 0.55, 0.42), mat('Seat', (0.08, 0.08, 0.1), 0.6), 0.06, r)
        box('Handlebar', (0.8, 0.08, 0.08), (0, -0.25, 0.75), mat('Black', (0.05, 0.05, 0.05), 0.6), 0.02, r)
        box('Steering column', (0.12, 0.12, 0.4), (0, -0.2, 0.55), mat('Black', (0.05, 0.05, 0.05), 0.6), 0.02, r)
    elif kind == 'speedboat':
        m, v, f = hull(name, 6.5, 2.4, 0.8, (0.95, 0.95, 0.97), bow_rise=0.35); mesh_obj('Hull', v, f, m, r)
        box('Deck', (2.2, 4.5, 0.08), (0, 0.6, 0.02), mat('Teak', (0.55, 0.38, 0.22), 0.6), 0, r)
        box('Console', (0.9, 0.6, 0.9), (0, 0.1, 0.45), mat('Navy', (0.06, 0.15, 0.35), 0.4), 0.05, r)
        box('Windscreen', (1.0, 0.05, 0.4), (0, -0.2, 1.05), mat('Glass', (0.3, 0.4, 0.45), 0.05, 0.2, alpha=0.4), 0.01, r).rotation_euler = (math.radians(-25), 0, 0)
        box('Bench', (2.0, 0.6, 0.4), (0, 2.4, 0.22), mat('Cushion white', (0.95, 0.95, 0.92), 0.7), 0.05, r)
    elif kind == 'sailboat':
        m, v, f = hull(name, 8, 2.8, 1.0, (0.95, 0.96, 0.98), bow_rise=0.3); mesh_obj('Hull', v, f, m, r)
        box('Cabin', (1.8, 2.6, 0.6), (0, 0.6, 0.3), mat('Cabin', (0.9, 0.9, 0.88), 0.4), 0.08, r)
        bpy.ops.mesh.primitive_cylinder_add(radius=0.07, depth=10, location=(0, -0.6, 5)); o = bpy.context.active_object; o.data.materials.append(mat('Mast', (0.8, 0.8, 0.82), 0.3, 0.8)); o.parent = r
        sail = mat('Sail', (0.97, 0.96, 0.92), 0.8)
        mesh_obj('Mainsail', [(0, -0.55, 1.2), (0, -0.55, 9.6), (0, 2.6, 1.2)], [(0, 1, 2)], sail, r, False)
        mesh_obj('Jib', [(0, -0.65, 1.0), (0, -0.65, 8.6), (0, -3.8, 0.3)], [(0, 1, 2)], sail, r, False)
    elif kind == 'yacht':
        m, v, f = hull(name, 16, 4.6, 1.6, (0.97, 0.97, 0.99), bow_rise=0.6); mesh_obj('Hull', v, f, m, r)
        dark = mat('Yacht glass', (0.05, 0.07, 0.1), 0.05, 0.3)
        box('Main deck', (4.2, 11, 1.8), (0, 1.5, 0.9), mat('Superstructure', (0.97, 0.97, 0.99), 0.3), 0.15, r)
        box('Upper deck', (3.6, 6.5, 1.4), (0, 2.2, 2.5), mat('Superstructure', (0.97, 0.97, 0.99), 0.3), 0.15, r)
        for side in (-1, 1):
            box('Window band', (0.05, 9, 0.6), (side * 2.11, 1.5, 1.1), dark, 0.02, r)
            box('Upper window band', (0.05, 5.5, 0.5), (side * 1.81, 2.2, 2.6), dark, 0.02, r)
        box('Flybridge', (3, 3, 0.1), (0, 2.6, 3.25), mat('Teak', (0.55, 0.38, 0.22), 0.6), 0, r)
    elif kind == 'surfboard':
        board = mat('Surfboard', (0.98, 0.95, 0.85), 0.25); stripe = mat('Surfboard stripe', (0.05, 0.6, 0.65), 0.3)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=(0, 0, 0)); o = bpy.context.active_object; o.scale = (0.28, 1.05, 0.04); o.data.materials.append(board); o.parent = r
        box('Stripe', (0.04, 1.8, 0.085), (0, 0, 0), stripe, 0, r)
    elif kind == 'paddleboard':
        board = mat('Paddleboard', (0.1, 0.55, 0.75), 0.3)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=(0, 0, 0)); o = bpy.context.active_object; o.scale = (0.4, 1.6, 0.07); o.data.materials.append(board); o.parent = r
        box('Deck pad', (0.5, 0.9, 0.14), (0, 0.15, 0), mat('Pad', (0.15, 0.15, 0.15), 0.8), 0.02, r)
    return r

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    jobs = []
    for i, (h, lean) in enumerate(((9, 0.12), (11, 0.22), (7.5, 0.05), (12.5, 0.3))):
        name = 'CoconutPalm%d' % (i + 1)
        export(palm(name, h, lean, 11 + i), os.path.join(OUT, name + '.glb'))
    for kind, name in (('banana', 'BananaPlant'), ('hibiscus', 'HibiscusBush'), ('paradise', 'BirdOfParadise'), ('fern', 'TropicalFern'), ('grass', 'BeachGrass'), ('shade', 'SeaAlmondTree')):
        export(leafy(name, kind, len(name)), os.path.join(OUT, name + '.glb'))
    props()
    for kind, name in (('kayak', 'Kayak'), ('jetski', 'JetSki'), ('speedboat', 'Speedboat'), ('sailboat', 'Sailboat'), ('yacht', 'Yacht'), ('surfboard', 'Surfboard'), ('paddleboard', 'Paddleboard')):
        export(boat(name, kind), os.path.join(OUT, name + '.glb'))
    preview(os.path.join(OUT, 'CoconutPalm2.glb'), os.path.join(OUT, 'CoconutPalm2.png'), 16)
    preview(os.path.join(OUT, 'JetSki.glb'), os.path.join(OUT, 'JetSki.png'), 6)
    preview(os.path.join(OUT, 'Yacht.glb'), os.path.join(OUT, 'Yacht.png'), 24)
    preview(os.path.join(OUT, 'HibiscusBush.glb'), os.path.join(OUT, 'HibiscusBush.png'), 5)
    print('done')
