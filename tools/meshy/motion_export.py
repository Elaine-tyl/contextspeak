"""Blender worker: export bounded animated FBXs without baking away their skeletons."""
import json
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'games/unity/Assets/_Game/Art/Motion'
LIBRARY = ROOT / 'games/unity/Assets/_Game/Art/MeshyLibrary'
for item in json.loads((OUTPUT / 'manifest.json').read_text()):
    if item.get('native'):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(LIBRARY / item['source']))
    rigs = [obj for obj in bpy.data.objects if obj.type == 'ARMATURE']
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if not rigs or not any(rig.animation_data and rig.animation_data.action for rig in rigs):
        raise ValueError('Missing supplied skeleton/action: ' + item['file'])
    actions = [rig.animation_data.action for rig in rigs if rig.animation_data and rig.animation_data.action]
    bpy.context.scene.frame_start = int(min(action.frame_range[0] for action in actions))
    bpy.context.scene.frame_end = int(max(action.frame_range[1] for action in actions))
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)
    for rig in rigs:
        rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    for mesh in meshes:
        count = sum(len(face.vertices) - 2 for face in mesh.data.polygons)
        if count > 30000:
            bpy.context.view_layer.objects.active = mesh
            modifier = mesh.modifiers.new('Game topology', 'DECIMATE')
            modifier.ratio = 29800 / count
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    lower = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    upper = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    scale = item['height'] / (upper.z - lower.z)
    root = bpy.data.objects.new('MotionScale', None)
    bpy.context.collection.objects.link(root)
    for obj in list(bpy.data.objects):
        if obj != root and obj.parent is None:
            obj.parent = root
    root.scale = (scale,) * 3
    root.location = Vector((-(upper.x + lower.x) / 2, -(upper.y + lower.y) / 2, -lower.z)) * scale
    for rig in rigs:
        rig.data.pose_position = 'POSE'
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / item['file']), object_types={'EMPTY', 'ARMATURE', 'MESH'},
                             add_leaf_bones=False, axis_forward='-Z', axis_up='Y',
                             bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
                             path_mode='STRIP', embed_textures=False)
    print('Exported supplied motion', item['file'], flush=True)
