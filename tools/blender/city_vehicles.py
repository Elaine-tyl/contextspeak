"""Procedural city bus (12 m) and low-floor tram (24 m) for Metro City, exported as GLB.
Real-world sizes in metres. Front faces Blender -Y (= Unity +Z after glTF import); doors on the
kerb side (Blender -X = Unity +X for a bus driving on the right). The tram has doors on both sides."""
import bpy, bmesh, math, sys, os
from mathutils import Vector

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

MATS = {}
def mat(name, colour, rough=0.45, metal=0.0, emit=None, alpha=1.0):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*colour, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = 4.0
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        m.surface_render_method = 'BLENDED'
    MATS[name] = m
    return m

def box(name, size, loc, material, bevel=0.0, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object; o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = bevel; mod.segments = 3; mod.limit_method = 'ANGLE'
    o.data.materials.append(material)
    for poly in o.data.polygons: poly.use_smooth = bevel > 0
    if parent: o.parent = parent
    return o

def wheel(name, loc, radius, width, rubber, hub, parent):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=width, location=loc, rotation=(0, math.radians(90), 0))
    tyre = bpy.context.active_object; tyre.name = name; tyre.data.materials.append(rubber); tyre.parent = parent
    for poly in tyre.data.polygons: poly.use_smooth = True
    side = 1 if loc[0] > 0 else -1
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius * 0.6, depth=0.04, location=(loc[0] + side * width / 2, loc[1], loc[2]), rotation=(0, math.radians(90), 0))
    cap = bpy.context.active_object; cap.name = name + ' hub'; cap.data.materials.append(hub); cap.parent = parent

def text(name, value, loc, size, material, parent, rot=(math.radians(90), 0, 0)):
    bpy.ops.object.text_add(location=loc, rotation=rot)
    t = bpy.context.active_object; t.name = name; t.data.body = value; t.data.size = size
    t.data.align_x = 'CENTER'; t.data.align_y = 'CENTER'; t.data.extrude = 0.005
    t.data.materials.append(material)
    bpy.ops.object.convert(target='MESH')
    t = bpy.context.active_object; t.parent = parent
    return t


def side_text(name, value, y, z, size, material, parent, half_width, align='CENTER'):
    """Lettering on both flanks, readable from outside on each side."""
    for side in (-1, 1):
        rot = (math.radians(90), 0, math.radians(-90) if side < 0 else math.radians(90))
        bpy.ops.object.text_add(location=(side * (half_width + 0.035), y, z), rotation=rot)
        t = bpy.context.active_object; t.name = name; t.data.body = value; t.data.size = size
        t.data.align_x = align; t.data.align_y = 'CENTER'; t.data.extrude = 0.008
        t.data.materials.append(material)
        bpy.ops.object.convert(target='MESH')
        bpy.context.active_object.parent = parent

def windows_side(x, y0, y1, z0, z1, gap, glass, frame, parent, doors=(), door_mat=None, outward=1):
    """Glazing along one side from y0 to y1 with door openings [(y_centre, width)]."""
    y = y0
    count = 0
    while y < y1 - 0.2:
        width = min(1.6, y1 - y)
        centre = y + width / 2
        if any(abs(centre - dy) < dw / 2 + 0.6 for dy, dw in doors):
            y += width + gap; continue
        box(f'Window {count}', (0.04, width - gap, z1 - z0), (x, centre, (z0 + z1) / 2), glass, 0.02, parent)
        count += 1
        y += width + gap
    for dy, dw in doors:
        box('Door', (0.05, dw, z1 - 0.05), (x + outward * 0.005, dy, z1 / 2 + 0.05), door_mat, 0.02, parent)
        box('Door glass', (0.06, dw * 0.42, z1 - 0.9), (x + outward * 0.01, dy - dw * 0.24, z1 / 2 + 0.4), glass, 0.01, parent)
        box('Door glass', (0.06, dw * 0.42, z1 - 0.9), (x + outward * 0.01, dy + dw * 0.24, z1 / 2 + 0.4), glass, 0.01, parent)
        box('Door seal', (0.07, 0.03, z1 - 0.1), (x + outward * 0.012, dy, z1 / 2 + 0.05), frame, 0, parent)

def seats(parent, x_rows, y0, y1, step, fabric, frame):
    y = y0
    while y < y1:
        for x in x_rows:
            box('Seat', (0.45, 0.45, 0.08), (x, y, 0.75), fabric, 0.02, parent)
            box('Seat back', (0.45, 0.06, 0.55), (x, y + 0.24, 1.05), fabric, 0.02, parent)
            box('Seat leg', (0.06, 0.06, 0.4), (x, y, 0.52), frame, 0, parent)
        y += step

def export(root, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in [root] + list(root.children_recursive): o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True, export_yup=True)

def bus():
    reset()
    root = bpy.data.objects.new('CityBus', None); bpy.context.collection.objects.link(root)
    paint = mat('Bus red', (0.62, 0.05, 0.05), 0.3)
    white = mat('Bus white', (0.92, 0.92, 0.9), 0.35)
    skirt = mat('Bus skirt', (0.08, 0.08, 0.09), 0.6)
    glass = mat('Bus glass', (0.05, 0.08, 0.1), 0.05, 0.2, alpha=0.55)
    frame = mat('Bus frame', (0.12, 0.12, 0.13), 0.4, 0.6)
    rubber = mat('Tyre', (0.03, 0.03, 0.03), 0.9)
    hub = mat('Hub', (0.7, 0.7, 0.72), 0.3, 1.0)
    led = mat('Destination LED', (0.05, 0.03, 0.0), 0.5, emit=(1.0, 0.55, 0.05))
    head = mat('Headlight', (0.9, 0.9, 0.85), 0.2, emit=(1.0, 0.97, 0.85))
    tail = mat('Tail light', (0.4, 0.0, 0.0), 0.2, emit=(1.0, 0.05, 0.02))
    fabric = mat('Seat fabric', (0.1, 0.18, 0.35), 0.8)
    floor = mat('Bus floor', (0.25, 0.25, 0.27), 0.8)
    L, W, H = 12.0, 2.55, 3.05
    # Body shell (open inside is not needed for a game camera: glass shows the seats).
    box('Body lower', (W, L, 1.05), (0, 0, 0.35 + 0.525), paint, 0.06, root)
    box('Body upper band', (W, L, 0.3), (0, 0, 2.85), white, 0.06, root)
    box('Roof', (W - 0.1, L - 0.1, 0.12), (0, 0, 3.0), white, 0.05, root)
    box('Skirt', (W + 0.02, L - 0.4, 0.25), (0, 0, 0.42), skirt, 0.02, root)
    box('Floor', (W - 0.12, L - 0.2, 0.05), (0, 0, 0.42), floor, 0, root)
    # Pillars between the windows, front and back.
    for x in (-W / 2, W / 2):
        box('Window rail', (0.06, L - 0.3, 0.08), (x, 0, 1.42), frame, 0, root)
        box('Window rail', (0.06, L - 0.3, 0.08), (x, 0, 2.68), frame, 0, root)
    windows_side(-W / 2 - 0.01, -L / 2 + 1.2, L / 2 - 0.4, 1.46, 2.64, 0.08, glass, frame, root,
                 doors=[(-L / 2 + 1.1, 1.2), (0.6, 1.3)], door_mat=frame, outward=-1)
    windows_side(W / 2 + 0.01, -L / 2 + 1.2, L / 2 - 0.4, 1.46, 2.64, 0.08, glass, frame, root, outward=1)
    box('Windscreen', (W - 0.2, 0.06, 1.55), (0, -L / 2 - 0.01, 1.95), glass, 0.03, root)
    box('Rear window', (W - 0.5, 0.06, 0.9), (0, L / 2 + 0.01, 2.2), glass, 0.03, root)
    box('Destination sign', (W - 0.5, 0.05, 0.28), (0, -L / 2 - 0.03, 2.87), led, 0, root)
    text('Route', '12  CITY CENTRE', (0, -L / 2 - 0.06, 2.87), 0.2, head, root, rot=(math.radians(90), 0, math.radians(180)))
    for x in (-0.85, 0.85):
        box('Headlight', (0.38, 0.05, 0.18), (x, -L / 2 - 0.02, 0.75), head, 0.02, root)
        box('Tail light', (0.25, 0.05, 0.45), (x * 1.15, L / 2 + 0.02, 1.05), tail, 0.02, root)
    box('Bumper', (W, 0.12, 0.25), (0, -L / 2 - 0.05, 0.45), skirt, 0.03, root)
    box('Bumper', (W, 0.12, 0.25), (0, L / 2 + 0.05, 0.45), skirt, 0.03, root)
    box('Roof unit', (1.8, 2.6, 0.3), (0, 1.5, 3.2), white, 0.08, root)
    for x in (-1, 1):
        box('Mirror arm', (0.35, 0.04, 0.04), (x * (W / 2 + 0.15), -L / 2 + 0.15, 2.4), frame, 0, root)
        box('Mirror', (0.05, 0.18, 0.4), (x * (W / 2 + 0.32), -L / 2 + 0.15, 2.25), frame, 0.02, root)
    box('Side stripe', (W + 0.01, L - 0.6, 0.08), (0, 0, 1.25), white, 0, root)
    for y in (-L / 2 + 2.6, L / 2 - 2.9):
        for x in (-W / 2 + 0.12, W / 2 - 0.12):
            wheel('Wheel', (x, y, 0.5), 0.5, 0.32, rubber, hub, root)
    letters = mat('Livery white', (0.97, 0.97, 0.95), 0.4)
    side_text('Livery', 'METRO CITY BUS', -2.05, 1.16, 0.3, letters, root, W / 2)
    side_text('Route', 'Route 12  ·  City Ring', -1.9, 0.82, 0.15, letters, root, W / 2)
    side_text('Fleet', '1207', -4.6, 2.86, 0.16, mat('Livery dark', (0.1, 0.1, 0.12), 0.5), root, W / 2)
    text('Fleet front', '1207', (0, -L / 2 - 0.07, 1.05), 0.14, letters, root, rot=(math.radians(90), 0, math.radians(180)))
    seats(root, (-0.75, 0.75), -2.5, 5.2, 0.85, fabric, frame)
    box('Driver seat', (0.5, 0.5, 0.9), (0.6, -L / 2 + 1.6, 0.9), fabric, 0.03, root)
    export(root, os.path.join(OUT, 'CityBus.glb'))
    return root

def tram():
    reset()
    root = bpy.data.objects.new('CityTram', None); bpy.context.collection.objects.link(root)
    green = mat('Tram green', (0.06, 0.32, 0.18), 0.3)
    yellow = mat('Tram yellow', (0.95, 0.72, 0.08), 0.35)
    skirt = mat('Tram skirt', (0.1, 0.11, 0.12), 0.6)
    glass = mat('Tram glass', (0.05, 0.08, 0.1), 0.05, 0.2, alpha=0.5)
    frame = mat('Tram frame', (0.15, 0.15, 0.16), 0.4, 0.6)
    led = mat('Tram LED', (0.05, 0.03, 0.0), 0.5, emit=(1.0, 0.55, 0.05))
    head = mat('Tram light', (0.9, 0.9, 0.85), 0.2, emit=(1.0, 0.97, 0.85))
    fabric = mat('Tram seat', (0.45, 0.12, 0.1), 0.8)
    steel = mat('Pantograph', (0.4, 0.4, 0.42), 0.3, 1.0)
    floor = mat('Tram floor', (0.3, 0.3, 0.32), 0.8)
    L, W, H = 24.0, 2.65, 3.4
    box('Body lower', (W, L - 1.0, 0.9), (0, 0, 0.3 + 0.45), green, 0.06, root)
    box('Body upper band', (W, L - 1.0, 0.45), (0, 0, 3.0), yellow, 0.06, root)
    box('Roof', (W - 0.15, L - 1.2, 0.15), (0, 0, 3.3), mat('Tram roof', (0.82, 0.83, 0.82), 0.5), 0.05, root)
    box('Floor', (W - 0.12, L - 1.2, 0.05), (0, 0, 0.32), floor, 0, root)
    box('Skirt', (W + 0.02, L - 1.2, 0.3), (0, 0, 0.3), skirt, 0.02, root)
    doors = [(-7.5, 1.3), (0.0, 1.3), (7.5, 1.3)]
    for x in (-W / 2, W / 2):
        box('Window rail', (0.06, L - 1.2, 0.08), (x, 0, 1.24), frame, 0, root)
        box('Window rail', (0.06, L - 1.2, 0.08), (x, 0, 2.74), frame, 0, root)
        windows_side(x + (0.01 if x > 0 else -0.01), -L / 2 + 1.6, L / 2 - 1.6, 1.28, 2.7, 0.08, glass, frame, root,
                     doors=doors, door_mat=yellow, outward=1 if x > 0 else -1)
    # Articulation bellows between the three sections.
    for y in (-4.0, 4.0):
        box('Articulation', (W - 0.05, 0.35, 2.9), (0, y, 1.75), skirt, 0.05, root)
    # Rounded cabs at both ends (bidirectional tram).
    for end in (-1, 1):
        y = end * (L / 2 - 0.5)
        box('Cab', (W - 0.05, 1.0, 2.7), (0, y, 1.65), green, 0.35, root)
        box('Cab windscreen', (W - 0.4, 0.06, 1.4), (0, end * L / 2 - end * 0.02, 2.05), glass, 0.05, root)
        box('Destination', (W - 0.7, 0.05, 0.3), (0, end * L / 2 - end * 0.04, 3.0), led, 0, root)
        text('Line', 'T1  HARBOUR', (0, end * L / 2 - end * 0.01 + end * 0.02, 3.0), 0.2, head, root,
             rot=(math.radians(90), 0, math.radians(180) if end < 0 else 0))
        for x in (-0.85, 0.85):
            box('Lamp', (0.32, 0.05, 0.15), (x, end * L / 2 - end * 0.03, 0.85), head, 0.02, root)
    # Bogies and pantograph.
    for y in (-9.5, 0.0, 9.5):
        box('Bogie', (W - 0.4, 2.2, 0.35), (0, y, 0.2), skirt, 0.03, root)
    box('Pantograph base', (1.2, 1.4, 0.15), (0, 0, 3.45), steel, 0.02, root)
    arm = box('Pantograph arm', (0.08, 1.6, 0.08), (0, 0.3, 3.95), steel, 0, root)
    arm.rotation_euler = (math.radians(35), 0, 0)
    box('Pantograph head', (1.6, 0.12, 0.06), (0, 0.9, 4.4), steel, 0, root)
    letters = mat('Tram letters', (0.97, 0.97, 0.95), 0.4)
    dark = mat('Tram dark letters', (0.08, 0.2, 0.12), 0.4)
    for y in (-3.8, 3.8):
        side_text('Livery', 'METRO TRAM', y, 0.78, 0.32, letters, root, W / 2)
    side_text('Line', 'T1  HARBOUR LINE  ·  CENTRAL STATION', 0.0, 3.0, 0.27, dark, root, W / 2)
    side_text('Fleet', '304', -10.6, 0.78, 0.22, letters, root, W / 2)
    seats(root, (-0.8, 0.8), -10.5, -8.6, 0.9, fabric, frame)
    seats(root, (-0.8, 0.8), -6.2, -1.4, 0.9, fabric, frame)
    seats(root, (-0.8, 0.8), 1.4, 6.2, 0.9, fabric, frame)
    seats(root, (-0.8, 0.8), 8.6, 10.5, 0.9, fabric, frame)
    export(root, os.path.join(OUT, 'CityTram.glb'))
    return root

def car_body(root, L, W, paint, glass, rubber, hub, head, tail, cabin_from, cabin_to, cabin_height=0.55, lower=0.72):
    box('Body', (W, L, lower), (0, 0, 0.3 + lower / 2), paint, 0.14, root)
    box('Cabin', (W - 0.12, cabin_to - cabin_from, cabin_height), (0, (cabin_from + cabin_to) / 2, 0.3 + lower + cabin_height / 2 - 0.04), glass, 0.12, root)
    box('Roof', (W - 0.16, cabin_to - cabin_from - 0.35, 0.06), (0, (cabin_from + cabin_to) / 2 + 0.05, 0.3 + lower + cabin_height - 0.03), paint, 0.05, root)
    for x in (-W / 2 + 0.05, W / 2 - 0.05):
        for y in (-L / 2 + 0.85, L / 2 - 0.8):
            wheel('Wheel', (x, y, 0.33), 0.33, 0.24, rubber, hub, root)
    for x in (-0.6, 0.6):
        box('Headlight', (0.32, 0.05, 0.12), (x, -L / 2 - 0.01, 0.75), head, 0.02, root)
        box('Tail light', (0.3, 0.05, 0.12), (x, L / 2 + 0.01, 0.8), tail, 0.02, root)

def taxi():
    reset()
    root = bpy.data.objects.new('CityTaxi', None); bpy.context.collection.objects.link(root)
    paint = mat('Taxi yellow', (0.95, 0.72, 0.05), 0.3)
    glass = mat('Taxi glass', (0.05, 0.07, 0.09), 0.05, 0.3)
    car_body(root, 4.6, 1.8, paint, glass, mat('Tyre', (0.03, 0.03, 0.03), 0.9), mat('Hub', (0.7, 0.7, 0.72), 0.3, 1.0),
             mat('Headlight', (0.9, 0.9, 0.85), 0.2, emit=(1.0, 0.97, 0.85)), mat('Tail light', (0.4, 0, 0), 0.2, emit=(1.0, 0.05, 0.02)), -1.0, 1.3)
    checks = mat('Taxi checks', (0.05, 0.05, 0.05), 0.5)
    for x in (-0.91, 0.91):
        for index in range(12):
            box('Check', (0.02, 0.18, 0.08), (x, -1.9 + index * 0.34, 0.86 + (index % 2) * 0.08), checks, 0, root)
    sign = mat('Taxi sign', (0.95, 0.95, 0.9), 0.3, emit=(1.0, 0.85, 0.4))
    box('Roof sign', (0.75, 0.3, 0.22), (0, 0.15, 1.73), sign, 0.04, root)
    black = mat('Taxi letters', (0.05, 0.05, 0.05), 0.4)
    for end in (-1, 1):
        text('TAXI', 'TAXI', (0, 0.15 + end * 0.16, 1.73), 0.16, black, root, rot=(math.radians(90), 0, math.radians(180) if end > 0 else 0))
    side_text('Door', 'CITY CAB', 0.0, 0.72, 0.2, black, root, 0.9)
    side_text('Phone', '1300 222 888', 0.0, 0.52, 0.11, black, root, 0.9)
    export(root, os.path.join(OUT, 'CityTaxi.glb'))

def van():
    reset()
    root = bpy.data.objects.new('FreshMartVan', None); bpy.context.collection.objects.link(root)
    white = mat('Van white', (0.93, 0.94, 0.92), 0.35)
    green = mat('Van green', (0.12, 0.5, 0.28), 0.35)
    glass = mat('Van glass', (0.05, 0.07, 0.09), 0.05, 0.3)
    L, W = 5.4, 2.0
    box('Cargo box', (W, 3.6, 2.0), (0, 0.85, 0.35 + 1.0), white, 0.06, root)
    box('Cab', (W, 1.8, 1.25), (0, -1.75, 0.35 + 0.62), white, 0.12, root)
    box('Windscreen', (W - 0.2, 0.06, 0.55), (0, -2.66, 1.25), glass, 0.02, root)
    box('Stripe', (W + 0.01, 3.5, 0.28), (0, 0.85, 0.7), green, 0, root)
    for x in (-W / 2 + 0.06, W / 2 - 0.06):
        for y in (-1.85, 1.75):
            wheel('Wheel', (x, y, 0.36), 0.36, 0.26, mat('Tyre', (0.03, 0.03, 0.03), 0.9), mat('Hub', (0.7, 0.7, 0.72), 0.3, 1.0), root)
    for x in (-0.65, 0.65):
        box('Headlight', (0.3, 0.05, 0.14), (x, -L / 2 - 0.01, 0.8), mat('Headlight', (0.9, 0.9, 0.85), 0.2, emit=(1.0, 0.97, 0.85)), 0.02, root)
    side_text('Brand', 'FreshMart', 0.85, 1.65, 0.48, green, root, W / 2)
    side_text('Slogan', 'Fresh food, delivered daily', 0.85, 1.15, 0.17, green, root, W / 2)
    side_text('Phone', 'freshmart.metro  ·  1800 600 600', 0.85, 0.7, 0.11, white, root, W / 2)
    text('Back', 'FreshMart', (0, 2.68, 1.6), 0.36, green, root, rot=(math.radians(90), 0, math.radians(180)))
    export(root, os.path.join(OUT, 'FreshMartVan.glb'))

def preview(path_glb, png, distance):
    reset()
    bpy.ops.import_scene.gltf(filepath=path_glb)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y = 960, 540
    world = bpy.data.worlds.new('w'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.75, 0.82, 0.9, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(30))); bpy.context.active_object.data.energy = 3
    bpy.ops.mesh.primitive_plane_add(size=80); bpy.context.active_object.data.materials.append(mat('Ground', (0.3, 0.3, 0.32), 0.9))
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam)
    cam.location = (-distance * 0.75, -distance * 0.75, distance * 0.35)
    direction = Vector((0, 0, 1.5)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.filepath = png
    bpy.ops.render.render(write_still=True)

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    bus(); tram(); taxi(); van()
    for name, distance in (('CityBus', 14), ('CityTram', 24), ('CityTaxi', 8), ('FreshMartVan', 10)):
        preview(os.path.join(OUT, name + '.glb'), os.path.join(OUT, name + '.png'), distance)
    print('done')
