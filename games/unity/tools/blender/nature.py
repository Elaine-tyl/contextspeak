"""Turn a dense Meshy FBX (one textured mesh, PBR maps) into a game-ready GLB with three LODs.

Usage: python3 nature.py <name> <category> <fbx> <base.png> [normal.png] [roughness.png]
Categories set the real-world size and triangle budgets:
  tree, conifer, palm, flower, grass, crop, rock, cliff, terrain, aquatic.
Writes out_nature/<name>.glb (nodes <name>_LOD0/1/2), <name>.png preview and a JSON stats line.
"""
import json, math, os, sys
import bpy
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out_nature')
TEX = os.path.join(OUT, 'tex')
# (size metres, measured on 'h' height or 'w' widest horizontal side, LOD0 triangles, texture px)
CATEGORY = {
    'tree': (13, 'h', 26000, 2048), 'conifer': (18, 'h', 22000, 2048), 'palm': (10, 'h', 18000, 2048),
    'flower': (0.6, 'h', 5000, 1024), 'grass': (0.55, 'h', 5000, 1024), 'crop': (1.0, 'h', 6000, 1024),
    'rock': (2.4, 'w', 7000, 1024), 'cliff': (24, 'w', 16000, 2048), 'terrain': (30, 'w', 40000, 2048), 'aquatic': (0.8, 'h', 4000, 1024),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def image(path, colour, size):
    if not path or not os.path.exists(path):
        return None
    img = bpy.data.images.load(path)
    if max(img.size) > size:
        img.scale(size, size)
    os.makedirs(TEX, exist_ok=True)
    img.filepath_raw = os.path.join(TEX, os.path.basename(path))
    img.file_format = 'PNG'
    img.save()
    if not colour:
        img.colorspace_settings.name = 'Non-Color'
    return img


def material(name, base, normal, rough, size):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes['Principled BSDF']
    bsdf.inputs['Metallic'].default_value = 0
    b = image(base, True, size)
    if b:
        t = nodes.new('ShaderNodeTexImage'); t.image = b
        links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
    n = image(normal, False, size // 2 if size > 1024 else size)
    if n:
        t = nodes.new('ShaderNodeTexImage'); t.image = n
        m = nodes.new('ShaderNodeNormalMap'); m.inputs['Strength'].default_value = 1.0
        links.new(t.outputs['Color'], m.inputs['Color']); links.new(m.outputs['Normal'], bsdf.inputs['Normal'])
    r = image(rough, False, 512)
    if r:
        t = nodes.new('ShaderNodeTexImage'); t.image = r
        links.new(t.outputs['Color'], bsdf.inputs['Roughness'])
    else:
        bsdf.inputs['Roughness'].default_value = 0.8
    return mat


def drop_small_islands(obj, target):
    """Dense foliage is thousands of separate leaf shells the decimator cannot merge.
    Keep the largest shells (by area) until the budget is met — sparser, but the silhouette holds."""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    seen, islands = set(), []
    for face in bm.faces:
        if face.index in seen: continue
        stack, island = [face], []
        seen.add(face.index)
        while stack:
            f = stack.pop(); island.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen: seen.add(g.index); stack.append(g)
        islands.append((sum(f.calc_area() for f in island), island))
    islands.sort(key=lambda item: -item[0])
    kept, doomed = 0, []
    for area, island in islands:
        count = sum(len(f.verts) - 2 for f in island)
        if kept + count <= target or kept == 0: kept += count
        else: doomed.extend(island)
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    bm.to_mesh(obj.data); bm.free()


def rebake(high, src, work, level, target, texsize):
    import subprocess
    dst = os.path.join(work, f'plain{level}.obj')
    subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mlab.py'), src, dst, str(target), 'plain'],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    before = set(bpy.context.scene.objects)
    bpy.ops.wm.obj_import(filepath=dst, forward_axis='Y', up_axis='Z')
    low = [o for o in bpy.context.scene.objects if o not in before][0]
    for p in low.data.polygons: p.use_smooth = True
    for o in bpy.context.scene.objects: o.select_set(o == low)
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    size = texsize if level == 0 else texsize // 2
    name = high.name + f'_bake{level}'
    images = {}
    for kind, colour in (('base', True), ('rough', False)):
        img = bpy.data.images.new(f'{name}_{kind}', size, size)
        if not colour: img.colorspace_settings.name = 'Non-Color'
        images[kind] = img
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes['Principled BSDF']
    bsdf.inputs['Metallic'].default_value = 0
    target_node = nodes.new('ShaderNodeTexImage'); target_node.image = images['base']
    low.data.materials.clear(); low.data.materials.append(mat)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 1
    bake = scene.render.bake
    bake.use_selected_to_active = True
    bake.cage_extrusion = max(high.dimensions) * 0.01
    bake.max_ray_distance = max(high.dimensions) * 0.03
    bake.margin = 4
    for o in bpy.context.scene.objects: o.select_set(o in (high, low))
    bpy.context.view_layer.objects.active = low
    hide = [o for o in bpy.context.scene.objects if o not in (high, low)]
    for o in hide: o.hide_render = True
    nodes.active = target_node
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
    target_node.image = images['rough']; nodes.active = target_node
    bpy.ops.object.bake(type='ROUGHNESS')
    for o in hide: o.hide_render = False
    os.makedirs(TEX, exist_ok=True)
    for kind, img in images.items():
        img.filepath_raw = os.path.join(TEX, f'{name}_{kind}.png'); img.file_format = 'PNG'; img.save()
    nodes.remove(target_node)
    base = nodes.new('ShaderNodeTexImage'); base.image = images['base']
    links.new(base.outputs['Color'], bsdf.inputs['Base Color'])
    rough = nodes.new('ShaderNodeTexImage'); rough.image = images['rough']
    links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])
    return low


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def main(name, category, fbx, base, normal=None, rough=None):
    size, axis, budget, texsize = CATEGORY[category]
    reset()
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in bpy.context.scene.objects:
        o.select_set(o.type == 'MESH')
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    for o in list(bpy.context.scene.objects):
        if o != obj:
            bpy.data.objects.remove(o)
    obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    source_tris = tris(obj)
    # Meshy splits vertices along every UV seam; weld them so the decimator can collapse across seams.
    span = max(obj.dimensions)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=span * 2e-5)
    bpy.ops.object.mode_set(mode='OBJECT')
    # Meshy exports Y-up content that the FBX importer already turns Z-up; measure and normalise.
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    dims = hi - lo
    measure = dims.z if axis == 'h' else max(dims.x, dims.y)
    scale = size / max(measure, 1e-6)
    for v in obj.data.vertices:
        v.co = (v.co - Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))) * scale
    obj.data.update()
    obj.data.materials.clear()
    obj.data.materials.append(material(name, base, normal, rough, texsize))
    for p in obj.data.polygons:
        p.use_smooth = True
    lods = []
    import subprocess, tempfile
    work = tempfile.mkdtemp()
    for o in bpy.context.scene.objects: o.select_set(o == obj)
    src = os.path.join(work, 'src.obj')
    bpy.ops.wm.obj_export(filepath=src, export_selected_objects=True, export_materials=False, export_normals=True, export_uv=True, forward_axis='Y', up_axis='Z')
    for level, ratio in enumerate((1.0, 0.33, 0.1)):
        target = int(budget * ratio)
        if tris(obj) <= target:
            copy = obj.copy(); copy.data = obj.data.copy()
            bpy.context.scene.collection.objects.link(copy)
        else:
            dst = os.path.join(work, f'lod{level}.obj')
            # MeshLab quadric collapse keeps UV seams intact (Blender's decimator stalls on Meshy's fragmented atlases).
            subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mlab.py'), src, dst, str(target)],
                           check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            before = set(bpy.context.scene.objects)
            bpy.ops.wm.obj_import(filepath=dst, forward_axis='Y', up_axis='Z')
            copy = [o for o in bpy.context.scene.objects if o not in before][0]
            copy.data.materials.clear(); copy.data.materials.append(obj.data.materials[0])
            for p in copy.data.polygons: p.use_smooth = True
            if tris(copy) > target * 1.6:
                # Fragmented UV atlas blocks the collapse: decimate freely and re-bake the textures.
                bpy.data.objects.remove(copy)
                copy = rebake(obj, src, work, level, target, texsize)
        copy.name = copy.data.name = f'{name}_LOD{level}'
        lods.append(copy)
    bpy.data.objects.remove(obj)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + '.glb')
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=False, export_yup=True,
                              export_image_format='JPEG', export_jpeg_quality=88, export_apply=True)
    # Preview: LOD0 only, textured workbench render from a three-quarter view.
    for o in lods[1:]:
        o.hide_render = True
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = scene.render.resolution_y = 420
    scene.render.film_transparent = False
    world = bpy.data.worlds.new('w'); scene.world = world
    world.color = (0.62, 0.7, 0.78)
    d = lods[0].dimensions
    reach = max(d.x, d.y, d.z)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam)
    cam.location = Vector((reach * 1.1, -reach * 1.5, d.z * 0.6 + reach * 0.45))
    cam.rotation_euler = (Vector((0, 0, d.z * 0.45)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    stats = dict(name=name, category=category, source_tris=source_tris, dims=[round(v, 3) for v in dims],
                 lod_tris=[tris(o) for o in lods], size=[round(v, 2) for v in lods[0].dimensions],
                 glb_mb=round(os.path.getsize(path) / 1048576, 2))
    print('STATS ' + json.dumps(stats))
    with open(os.path.join(OUT, 'stats.jsonl'), 'a') as f:
        f.write(json.dumps(stats) + '\n')


if __name__ == '__main__':
    a = sys.argv[1:]
    main(*a)
