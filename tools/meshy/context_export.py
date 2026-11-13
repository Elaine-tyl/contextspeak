"""Lower-cost contextual copies preserve source UVs/materials; gallery copies stay intact."""
import hashlib
import json
from pathlib import Path
import bpy

ROOT = Path(__file__).resolve().parents[2] / 'games/unity/Assets/_Game/Art/WorldAssets'
OUTPUT = ROOT / 'Contextual'
TARGETS = {'World03c68813dd': 5000, 'World73438ba0b7': 5000,
           'Worlded6f92e1a2': 3000, 'World65b03a2587': 1200,
           'World930940d8f6': 6000, 'Worlddd317d3e66': 6000}
architecture = ROOT.parent / 'architecture.json'
if architecture.exists():
    for row in json.loads(architecture.read_text()):
        stats = json.loads((ROOT / (row['id'] + '.json')).read_text())
        # Disconnected source details cannot always collapse safely. Keep those once, not as repeated trees.
        TARGETS[row['id']] = 8000 if stats['triangles'] <= 30000 else stats['triangles']
OUTPUT.mkdir(exist_ok=True)
report_path = OUTPUT / 'report.json'
previous = {row['id']: row for row in json.loads(report_path.read_text())} if report_path.exists() else {}
report = []
for model, target in TARGETS.items():
    source = ROOT / (model + '.glb')
    sha = hashlib.sha256(source.read_bytes()).hexdigest()
    old = previous.get(model, {})
    output = OUTPUT / source.name
    if output.exists() and old.get('sourceHash') == sha and old.get('target') == target:
        report.append(old)
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    before = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in meshes)
    for obj in meshes:
        if before <= target:
            continue
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new('Contextual shared mesh budget', 'DECIMATE')
        modifier.ratio = target / before
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for image in bpy.data.images:
        if image.size[0]:
            image.pack()
    bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB', export_animations=False)
    count = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in meshes)
    report.append({'id': model, 'sourceHash': sha, 'target': target, 'triangles': count})
    print('Contextual mesh', model, count, flush=True)
report_path.write_text(json.dumps(report, indent=2) + '\n')
