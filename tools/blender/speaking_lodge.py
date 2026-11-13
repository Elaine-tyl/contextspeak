"""Original ContextSpeak art. Run with Blender --background --python this_file.py.

Keeps .blend sources outside Unity; exports metre-scale GLB files for the pinned
Unity glTF importer. No downloads, API keys or third-party game art are required.
"""
from pathlib import Path
import math
import random
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/blender'
EXPORT = ROOT / 'games/unity/Assets/_Game/Art/Blender'
PREVIEW = ROOT / '.local/art-previews'
PALETTE = {
    'oak': 'A47950', 'walnut': '493A2B', 'cream': 'E5DCC7', 'sage': '687D65',
    'roof': '394F4B', 'brass': 'B79962', 'paper': 'F8EDD3', 'ink': '273A36',
    'skin': 'DDA580', 'hair': '3C2C23', 'iris': '586750', 'sole': 'C2B59B',
}
MATERIALS = {}


def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    MATERIALS.clear()
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 760
    scene.render.resolution_percentage = 100
    scene.world.color = (0.28, 0.28, 0.28)


def material(key):
    if key not in MATERIALS:
        hex_colour = PALETTE[key]
        srgb = [int(hex_colour[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        linear = [v / 12.92 if v < 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in srgb]
        mat = bpy.data.materials.new('ContextSpeak ' + key)
        mat.diffuse_color = (*linear, 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (*linear, 1)
        shader.inputs['Roughness'].default_value = 0.38 if key == 'brass' else 0.74
        shader.inputs['Metallic'].default_value = 0.65 if key == 'brass' else 0
        MATERIALS[key] = mat
    return MATERIALS[key]


def group(name, at=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = at
    return obj


def finish(obj, name, at, size, colour, parent):
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(colour))
    obj.parent = parent
    obj.location = at
    return obj


def box(name, at, size, colour, parent, bevel=0.025):
    bpy.ops.mesh.primitive_cube_add()
    obj = finish(bpy.context.object, name, at, size, colour, parent)
    if bevel:
        modifier = obj.modifiers.new('Soft crafted edges', 'BEVEL')
        modifier.width = min(bevel, min(size) / 3)
        modifier.segments = 2
        obj.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
    return obj


def oval(name, at, size, colour, parent, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings)
    obj = finish(bpy.context.object, name, at, size, colour, parent)
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj


def beam(name, start, end, width, colour, parent):
    direction = Vector(end) - Vector(start)
    obj = box(name, (Vector(start) + Vector(end)) / 2, (width, width, direction.length), colour, parent)
    obj.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
    return obj


def lodge():
    reset()
    root = group('Lodge')
    box('Stone foundation', (0, 0, 0.04), (7.4, 5.5, 0.08), 'sage', root)
    for plank in range(22):
        box('Deck plank', (-3.48 + plank * 0.33, 0, 0.13), (0.315, 5.3, 0.16), 'oak', root, 0.012)
    for x in (-3.15, 3.15):
        for y in (-2.15, 2.15):
            box('Timber post', (x, y, 1.55), (0.22, 0.22, 2.9), 'walnut', root)
            beam('Corner brace', (x, y, 2.1), (x * 0.76, y, 2.92), 0.13, 'oak', root)
        box('Eave beam', (x, 0, 3), (0.24, 4.9, 0.22), 'walnut', root)
        roof = box('Sloping roof', (x / 2, 0, 3.55), (3.55, 5.6, 0.12), 'roof', root)
        roof.rotation_euler.y = math.copysign(math.radians(20), x)
    box('Ridge cap', (0, 0, 4.12), (0.22, 5.65, 0.16), 'walnut', root)
    for y in (-2.3, 0, 2.3):
        beam('Roof truss', (-3.3, y, 3), (0, y, 4.1), 0.14, 'oak', root)
        beam('Roof truss', (3.3, y, 3), (0, y, 4.1), 0.14, 'oak', root)
    for i in range(12):
        box('Rear wall board', (-2.95 + i * 0.54, 2.15, 1.55), (0.52, 0.14, 2.7), 'sage', root)
    box('Welcome counter', (1.8, 1.15, 0.66), (1.9, 0.8, 1), 'oak', root)
    box('Countertop', (1.8, 1.15, 1.2), (2.05, 0.95, 0.13), 'walnut', root)
    box('Mission board', (-1.45, 1.95, 1.8), (2.5, 0.17, 1.45), 'walnut', root)
    box('Cork panel', (-1.45, 1.84, 1.8), (2.26, 0.06, 1.2), 'oak', root)
    for i, x in enumerate((-2.15, -1.45, -0.75)):
        paper = box('Route paper', (x, 1.79, 1.83), (0.55, 0.02, 0.8), 'paper', root, 0.005)
        paper.rotation_euler.y = math.radians((i - 1) * 4)
        oval('Brass pin', (x, 1.75, 2.15), (0.07, 0.035, 0.07), 'brass', root)
        for line in range(4):
            box('Paper line', (x - 0.02, 1.77, 2.02 - line * 0.13), (0.35 - line * 0.035, 0.01, 0.015), 'sage', root, 0)
    for x in (-2.8, 2.8):
        box('Planter box', (x, -2.08, 0.38), (0.6, 0.6, 0.35), 'oak', root)
        for i in range(6):
            angle = i * math.tau / 6
            leaf = oval('Broad leaf', (x + math.sin(angle) * 0.18, -2.08 + math.cos(angle) * 0.18, 0.78), (0.15, 0.25, 0.65), 'sage', root)
            leaf.rotation_euler = (math.cos(angle) * 0.45, math.sin(angle) * 0.45, angle)
        box('Pendant cable', (x, -1.1, 2.8), (0.025, 0.025, 0.5), 'ink', root, 0)
        oval('Warm lantern', (x, -1.1, 2.52), (0.35, 0.35, 0.45), 'brass', root)
    save(root, 'SpeakingLodge')
    render('SpeakingLodge', (10, -15, 6), (0, 0, 1.7), 11)


def guide():
    reset()
    root = group('TrailGuide')
    box('Jacket', (0, 0, 1.08), (0.44, 0.25, 0.6), 'sage', root, 0.08)
    box('Cotton shirt', (0, -0.135, 1.12), (0.17, 0.015, 0.48), 'cream', root)
    box('Collar left', (-0.1, -0.13, 1.37), (0.11, 0.07, 0.15), 'sage', root)
    box('Collar right', (0.1, -0.13, 1.37), (0.11, 0.07, 0.15), 'sage', root)
    for side in (-1, 1):
        box('Chest pocket', (side * 0.135, -0.14, 1.18), (0.105, 0.03, 0.12), 'sage', root)
        oval('Pocket button', (side * 0.135, -0.16, 1.22), (0.025, 0.012, 0.025), 'brass', root)
        arm = group('LeftArm' if side < 0 else 'RightArm', (side * 0.26, 0, 1.35), root)
        oval('Jacket sleeve', (side * 0.025, 0, -0.23), (0.16, 0.2, 0.48), 'sage', arm)
        box('Cuff', (side * 0.025, 0, -0.43), (0.14, 0.17, 0.07), 'cream', arm)
        oval('Hand', (side * 0.025, -0.01, -0.53), (0.11, 0.12, 0.18), 'skin', arm)
        oval('Thumb', (-side * 0.03, -0.05, -0.5), (0.05, 0.06, 0.09), 'skin', arm)
        leg = group('LeftLeg' if side < 0 else 'RightLeg', (side * 0.115, 0, 0.8), root)
        oval('Cargo trousers', (0, 0, -0.3), (0.21, 0.23, 0.66), 'walnut', leg)
        box('Cargo pocket', (side * 0.095, 0, -0.2), (0.05, 0.18, 0.19), 'walnut', leg)
        box('Boot', (0, -0.035, -0.68), (0.205, 0.33, 0.2), 'ink', leg, 0.045)
        box('Boot sole', (0, -0.045, -0.767), (0.215, 0.35, 0.035), 'sole', leg, 0.015)
        for i in range(3):
            box('Boot lace', (0, -0.15, -0.65 - i * 0.025), (0.1, 0.015, 0.012), 'cream', leg, 0)
        box('Backpack strap', (side * 0.15, -0.145, 1.12), (0.04, 0.025, 0.48), 'walnut', root, 0.01)
    box('Canvas backpack', (0, 0.24, 1.11), (0.34, 0.22, 0.43), 'oak', root, 0.07)
    box('Backpack flap', (0, 0.31, 1.27), (0.33, 0.12, 0.17), 'walnut', root)
    box('Buckle', (0, 0.38, 1.22), (0.05, 0.025, 0.06), 'brass', root)
    oval('Neck', (0, 0, 1.42), (0.13, 0.13, 0.16), 'skin', root)
    head = group('Head', (0, 0, 1.48), root)
    oval('Face', (0, -0.005, 0.12), (0.3, 0.255, 0.345), 'skin', head, 24, 12)
    oval('Nose', (0, -0.137, 0.105), (0.045, 0.05, 0.065), 'skin', head)
    for side in (-1, 1):
        oval('Ear', (side * 0.149, 0, 0.12), (0.055, 0.065, 0.1), 'skin', head)
        oval('Eye white', (side * 0.065, -0.113, 0.16), (0.058, 0.023, 0.034), 'paper', head)
        oval('Iris', (side * 0.065, -0.139, 0.16), (0.033, 0.012, 0.034), 'iris', head)
        oval('Pupil', (side * 0.065, -0.146, 0.16), (0.018, 0.008, 0.023), 'ink', head)
        oval('Catchlight', (side * 0.065 - 0.005, -0.151, 0.168), (0.008, 0.004, 0.009), 'paper', head)
        brow = oval('Brow', (side * 0.065, -0.128, 0.205), (0.082, 0.018, 0.014), 'hair', head)
        brow.rotation_euler.y = side * -0.07
    oval('Smile', (0, -0.124, 0.045), (0.075, 0.014, 0.012), 'walnut', head)
    oval('Hair cap', (0, 0.025, 0.255), (0.32, 0.25, 0.18), 'hair', head)
    rng = random.Random(1211)
    for i in range(9):
        lock = oval('Swept hair lock', (-0.12 + i * 0.03, -0.07, 0.285 + rng.uniform(-0.02, 0.02)), (0.09, 0.17, 0.11), 'hair', head)
        lock.rotation_euler.y = -0.4
    save(root, 'TrailGuide')
    render('TrailGuide', (2.6, -4.7, 2.3), (0, 0, 0.92), 3.1)


def save(root, name):
    SOURCE.mkdir(parents=True, exist_ok=True)
    EXPORT.mkdir(parents=True, exist_ok=True)
    bpy.context.scene['asset_owner'] = 'ContextSpeak original procedural art'
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / (name + '.blend')))
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(EXPORT / (name + '.glb')), export_format='GLB',
                              use_selection=True, export_apply=True, export_extras=True,
                              export_cameras=False, export_lights=False)


def render(name, camera_position, target, scale):
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.camera_add(location=camera_position)
    camera = bpy.context.object
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = scale
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type='AREA', location=(2, -4, 7))
    bpy.context.object.data.energy = 1800
    bpy.context.object.data.shape = 'DISK'
    bpy.context.object.data.size = 5
    bpy.ops.object.light_add(type='AREA', location=(-4, 2, 5))
    bpy.context.object.data.energy = 1100
    bpy.context.object.data.size = 4
    bpy.context.scene.render.filepath = str(PREVIEW / (name + '.png'))
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / (name + '.blend')))


lodge()
guide()
