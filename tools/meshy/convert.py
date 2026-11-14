"""Blender worker: preserve authored UVs/materials, reduce topology, export Unity GLB."""
import json
from pathlib import Path
import sys

import bpy
sys.path.insert(0, str(Path(__file__).parent))
from locomotion import rig_relaxed_character
from pose import relaxed_arm, pose_arms
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
DESTINATION = ROOT / 'games/unity/Assets/_Game/Art/Meshy'
TRIANGLE_BUDGET = 60000


def bounds(meshes):
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    return Vector(tuple(min(p[i] for p in points) for i in range(3))), Vector(tuple(max(p[i] for p in points) for i in range(3)))


def material(folder, limit):
    result = bpy.data.materials.new('Meshy PBR')
    result.use_nodes = True
    tree = result.node_tree
    shader = tree.nodes.get('Principled BSDF')
    maximum = 0
    for channel, socket in [('BaseColor', 'Base Color'), ('Roughness', 'Roughness'), ('Metallic', 'Metallic'), ('Normal', 'Normal')]:
        path = folder / (channel + '.png')
        if not path.exists():
            continue
        image = bpy.data.images.load(str(path), check_existing=True)
        if max(image.size) > limit:
            image.scale(limit, limit)
            image.pack()  # Commit resized pixels; otherwise glTF may reuse the original source PNG.
        maximum = max(maximum, *image.size)
        texture = tree.nodes.new('ShaderNodeTexImage')
        texture.image = image
        if channel != 'BaseColor':
            image.colorspace_settings.name = 'Non-Color'
        if channel == 'Normal':
            normal = tree.nodes.new('ShaderNodeNormalMap')
            tree.links.new(texture.outputs['Color'], normal.inputs['Color'])
            tree.links.new(normal.outputs['Normal'], shader.inputs['Normal'])
        else:
            tree.links.new(texture.outputs['Color'], shader.inputs[socket])
    return result, maximum


def convert(record, staging, render_preview=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    folder = staging / record['id']
    bpy.ops.import_scene.fbx(filepath=str(folder / record['fbx']))
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    rigs = [obj for obj in bpy.data.objects if obj.type == 'ARMATURE']
    source_animations = [action.name for action in bpy.data.actions]
    if record.get('bakeSourcePose'):
        # Older centimetre rigs have incompatible bind transforms in glTFast.
        # Bake their authored walking pose into this game copy, then weight a
        # simple metre-scale rig. The full FBX/animation stays in MeshyLibrary.
        graph = bpy.context.evaluated_depsgraph_get()
        for obj in meshes:
            world = obj.matrix_world.copy()
            obj.data = bpy.data.meshes.new_from_object(obj.evaluated_get(graph), depsgraph=graph)
            obj.modifiers.clear()
            obj.parent = None
            obj.matrix_world = world
        for rig in rigs:
            bpy.data.objects.remove(rig, do_unlink=True)
        rigs = []
        for action in list(bpy.data.actions):
            bpy.data.actions.remove(action)
    supplied_rig = bool(rigs)
    imported = list(bpy.data.objects)
    for rig in rigs:
        rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    before = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in meshes)
    budget = record.get('triangleBudget', TRIANGLE_BUDGET)
    for obj in meshes:
        bpy.context.view_layer.objects.active = obj
        if before > budget:
            decimate = obj.modifiers.new('Game mesh budget', 'DECIMATE')
            decimate.ratio = (budget - 100) / before
            decimate.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=decimate.name)
        for face in obj.data.polygons:
            face.use_smooth = True
    limit = record.get('textureLimit', 2048)
    if (folder / 'BaseColor.png').exists():
        pbr, texture_size = material(folder, limit)
        for obj in meshes:
            obj.data.materials.clear()
            obj.data.materials.append(pbr)
            for polygon in obj.data.polygons:
                polygon.material_index = 0
    else:
        # Loose animated FBXs can carry their authored textures inside the FBX.
        images = [node.image for obj in meshes for mat in obj.data.materials if mat and mat.use_nodes
                  for node in mat.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image]
        if not images:
            raise ValueError('Character has no source base texture: ' + record['id'])
        for image in images:
            if max(image.size) > limit:
                image.scale(limit, limit)
            image.pack()
        texture_size = max(max(image.size) for image in images)
    lower, upper = bounds(meshes)
    scale = record['height'] / (upper.z - lower.z)
    root = bpy.data.objects.new(record['id'], None)
    bpy.context.collection.objects.link(root)
    for obj in imported:
        if obj.parent is None:
            obj.parent = root
    root.scale = (scale,) * 3
    root.location = Vector((-(upper.x + lower.x) / 2, -(upper.y + lower.y) / 2, -lower.z)) * scale
    bpy.context.view_layer.update()
    if record.get('relaxArms') and not rigs:
        # Skinned two-bone arm pose (rigid hands) — replaces the old per-vertex bend that warped hands.
        pose_arms(meshes, record['height'])
        bpy.context.view_layer.update()
    lower, upper = bounds(meshes)
    if record.get('locomotionRig') and not rigs:
        rigs.append(rig_relaxed_character(meshes, record['height']))
    triangles = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in meshes)
    output = DESTINATION / (record['id'] + '.glb')
    # glTF exports the bind pose and the supplied action separately. No vault is used as idle.
    for rig in rigs:
        rig.data.pose_position = 'POSE'
    bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB', export_image_format='AUTO',
                              export_animations=bool(rigs),
                              export_rest_position_armature=True, export_skins=True,
                              export_yup=True, export_apply=False)
    # A real material/geometry preview, independent of the later Unity acceptance screenshot.
    for rig in rigs:
        rig.data.pose_position = 'REST'
    camera = bpy.data.objects.new('Preview camera', bpy.data.cameras.new('Preview camera'))
    bpy.context.collection.objects.link(camera)
    camera.location = (2.8, -5.3, 2.2)
    direction = Vector((0, 0, 0.9)) - camera.location
    camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 2.4
    bpy.context.scene.camera = camera
    for position, energy, size in [((1, -3, 5), 550, 4), ((-3, 0, 3), 350, 3)]:
        lamp = bpy.data.objects.new('Soft light', bpy.data.lights.new('Soft light', 'AREA'))
        bpy.context.collection.objects.link(lamp)
        lamp.location = position
        lamp.rotation_euler = (Vector((0, 0, 1)) - lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lamp.data.energy, lamp.data.shape, lamp.data.size = energy, 'DISK', size
    scene = bpy.context.scene
    scene.world = bpy.data.worlds.new('Warm background')
    scene.world.color = (0.22, 0.22, 0.22)
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 12
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 512, 640, 100
    scene.render.filepath = str(staging.parent / (record['id'] + '-preview.png'))
    if render_preview:
        bpy.ops.render.render(write_still=True)
    return {**record, 'provenance': 'User-provided Meshy archive; redistribution license not verified',
            'inputTriangles': before, 'triangles': triangles, 'rigged': bool(rigs),
            'rigSource': 'supplied' if supplied_rig else 'generated-basic' if rigs else 'none',
            'bones': sum(len(rig.data.bones) for rig in rigs),
            'animations': [action.name for action in bpy.data.actions],
            'sourceAnimations': source_animations,
            'texturesMaxSize': texture_size, 'exportHeight': round(upper.z - lower.z, 5),
            'sourceChannels': [name for name in ('BaseColor', 'Normal', 'Roughness', 'Metallic')
                               if (folder / (name + '.png')).exists()],
            'floor': round(lower.z, 5), 'bytes': output.stat().st_size}


if __name__ == '__main__':
    staging = Path(sys.argv[sys.argv.index('--') + 1])
    records = json.loads((staging / 'sources.json').read_text())
    DESTINATION.mkdir(parents=True, exist_ok=True)
    manifest_path = DESTINATION / 'manifest.json'
    previous = json.loads(manifest_path.read_text()) if manifest_path.exists() else []
    manifest = {record['id']: record for record in previous}
    arguments = sys.argv[sys.argv.index('--') + 2:]
    selected = [value for value in arguments if not value.startswith('--')]
    for record in records:
        if selected and record['id'] not in selected:
            continue
        converted = convert(record, staging, render_preview='--skip-preview' not in arguments)
        manifest[record['id']] = converted
        print('Converted', record['id'], converted['triangles'], 'triangles', converted['bytes'], 'bytes', flush=True)
    (DESTINATION / 'manifest.json').write_text(json.dumps([manifest[record['id']] for record in records], indent=2) + '\n')
