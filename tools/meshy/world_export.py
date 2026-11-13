"""Bounded static GLBs, preserving source UVs and PBR. Original FBXs are untouched."""
import json
import os
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'games/unity/Assets/_Game/Art'
DEST = ART / 'WorldAssets'
records = json.loads((DEST / 'catalog.json').read_text())
report = []
for item in records:
    if item['usage'] != 'world' or os.environ.get('WORLD_ASSET_ONLY') and item['id'] != os.environ['WORLD_ASSET_ONLY']:
        continue
    output = DEST / (item['id'] + '.glb')
    stats = DEST / (item['id'] + '.json')
    if output.exists() and stats.exists() and not os.environ.get('WORLD_ASSET_FORCE'):
        cached = json.loads(stats.read_text())
        if cached.get('materialVersion', 1) >= item.get('materialVersion', 2) and cached.get('extent', item['extent']) == item['extent'] and cached.get('targetHeight', 0) == item.get('targetHeight', 0) and cached.get('textureLimit') == item['textureLimit']:
            report.append(cached)
            continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ART / 'MeshyLibrary' / item['source']))
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    before = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in meshes)
    for obj in meshes:
        # Static scenery does not need supplied/empty armatures or animation curves.
        world = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = world
        obj.animation_data_clear()
        obj.modifiers.clear()
        if before > 16000:
            bpy.context.view_layer.objects.active = obj
            modifier = obj.modifiers.new('Scenery budget', 'DECIMATE')
            modifier.ratio = 15800 / before
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)
    material = bpy.data.materials.new('Source PBR')
    material.use_nodes = True
    tree = material.node_tree
    shader = tree.nodes.get('Principled BSDF')
    for channel, socket in [('baseColor', 'Base Color'), ('roughness', 'Roughness'), ('metallic', 'Metallic'), ('normal', 'Normal'), ('emission', 'Emission Color')]:
        path = item['textures'].get(channel)
        if not path:
            continue
        image = bpy.data.images.load(str(ART / 'MeshyLibrary' / path), check_existing=True)
        width, height = image.size
        ratio = min(1, item['textureLimit'] / max(width, height))
        if ratio < 1:
            image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
        image.pack()
        if channel not in ['baseColor', 'emission']:
            image.colorspace_settings.name = 'Non-Color'
        texture = tree.nodes.new('ShaderNodeTexImage')
        texture.image = image
        if channel == 'baseColor' and item.get('cutout'):
            clip = tree.nodes.new('ShaderNodeMath')
            clip.operation = 'ROUND'
            tree.links.new(texture.outputs['Alpha'], clip.inputs[0])
            tree.links.new(clip.outputs[0], shader.inputs['Alpha'])
        if channel == 'emission':
            shader.inputs['Emission Strength'].default_value = 1
        if channel == 'normal':
            normal = tree.nodes.new('ShaderNodeNormalMap')
            tree.links.new(texture.outputs['Color'], normal.inputs['Color'])
            tree.links.new(normal.outputs['Normal'], shader.inputs['Normal'])
        else:
            tree.links.new(texture.outputs['Color'], shader.inputs[socket])
    if item['textures'].get('baseColor'):
        for obj in meshes:
            obj.data.materials.clear()
            obj.data.materials.append(material)
            for face in obj.data.polygons:
                face.material_index = 0
    else:
        for image in bpy.data.images:
            if image.size[0] > 0:
                width, height = image.size
                ratio = min(1, item['textureLimit'] / max(width, height))
                image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
                image.pack()
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    scale = item['targetHeight'] / (high.z - low.z) if item.get('targetHeight') else item['extent'] / max(high - low)
    root = bpy.data.objects.new(item['id'], None)
    bpy.context.collection.objects.link(root)
    for obj in meshes:
        obj.parent = root
    root.scale = (scale,) * 3
    root.location = Vector((-(low.x + high.x) / 2, -(low.y + high.y) / 2, -low.z)) * scale
    bpy.context.view_layer.update()
    staging = DEST / '.staging'
    staging.mkdir(exist_ok=True)
    temporary = staging / output.name
    bpy.ops.export_scene.gltf(filepath=str(temporary), export_format='GLB', export_animations=False, export_skins=False)
    temporary.replace(output)
    data = {'extent': item['extent'], 'targetHeight': item.get('targetHeight', 0), 'materialVersion': item.get('materialVersion', 2), 'id': item['id'], 'sourceHash': item['sourceHash'], 'inputTriangles': before,
            'triangles': sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in meshes),
            'textureLimit': item['textureLimit'], 'bytes': output.stat().st_size}
    stats.write_text(json.dumps(data) + '\n')
    report.append(data)
    print('Prepared', len(report), item['name'], data['triangles'], flush=True)
all_reports = [json.loads((DEST / (entry['id'] + '.json')).read_text()) for entry in records
               if entry['usage'] == 'world' and (DEST / (entry['id'] + '.json')).exists()]
(DEST / 'export-report.json').write_text(json.dumps(all_reports, indent=2) + '\n')
print('World export complete:', len(report), flush=True)
