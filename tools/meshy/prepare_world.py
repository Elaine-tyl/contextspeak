"""Catalogue every distinct download and prepare streamed scenery game copies."""
import json
import re
from pathlib import Path
import subprocess
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'games/unity/Assets/_Game/Art'
DEST = ART / 'WorldAssets'

# A source can contain a whole scene. Keep those on spacious perimeter plots.
def category(name, key):
    name = name.lower()
    if any(word in name for word in ['piano', 'рояль', 'მუსიკალური']):
        return 'Furniture', 'Piano and music garden', 3.0
    if any(word in name for word in ['asphalt', 'road', 'pavement']):
        return 'Roads', 'Walkable town road', 4.0
    if key in ['afc7b82cee', '55b50b8147']:
        return 'Wardrobe', 'Uniform display', 1.7
    if re.search(r'(^|[_\s-])tower([_\s-]|$)', name) or any(word in name for word in ['house', 'villa', 'bungalow', 'haven_caf', 'building', '摩天大楼', 'hotel_room', 'living_room', 'mansion', 'apartments', 'heights', 'academy', 'clinic', 'hospital', 'cathedral', 'cottage', 'дом', 'bank_kiosk']):
        return 'Architecture', 'Neighborhood architecture', 7.0
    if any(word in name for word in ['kitchen', 'kueche', 'dining_room']):
        return 'Home', 'Kitchen showroom', 3.6
    if any(word in name for word in ['wardrobe', 'armoire', 'closet']):
        return 'Wardrobe', 'Clothing and storage display', 2.0
    if any(word in name for word in ['chair', 'table', 'piano', 'рояль', 'მუსიკალური', 'sofa', 'armchair', 'loveseat', 'sectional', 'voluminous', '沙发']):
        return 'Furniture', 'Furniture and music garden', 1.8
    if any(word in name for word in ['airliner', 'aereo', 'horizon_jet', 'uçağı']):
        return 'Vehicles', 'Airport aircraft display', 8.0
    if any(word in name for word in ['hummer', 'car_', 'packard', 'pickup', 'gangster', 'velocity', 'chevy', 'sedan', 'suv', 'chopper', 'motorcycle', 'bike', 'tram', 'bmw', 'studebaker', 'cfmoto', 'desert_raider', 'rails']):
        return 'Vehicles', 'Town transport exhibit', 3.2
    if any(word in name for word in ['bridge', 'river', 'strand', 'island', 'raft', 'boat', 'skiff', 'ferry', 'sand_with', 'lifebuoy', 'cliff', 'mountain', 'riverbank', 'pond', 'oasis', 'terrain', 'cast_rod']):
        return 'Waterside', 'Waterside trail exhibit', 3.5
    if any(word in name for word in ['octopus', 'dolphin', 'fish', 'koi', 'sanke', 'forellen', 'fin_', 'azure_elegance', 'highly_detailed_3d_model']):
        return 'Aquarium', 'Aquarium sculpture trail', 0.9
    if any(word in name for word in ['elephant', 'rhino', 'tiger', 'cat_', 'teste3', 'majesty', 'cochon', 'horse', 'equine', 'duck', 'gallina', 'sheep', '绵羊', 'pup', 'guard', '6_2026', 'crab', 'dragonbird', 'bluebird', 'stag']):
        return 'Animals', 'Animal garden', 1.0
    if any(word in name for word in ['market', 'fruit', 'harvest', 'cherry', 'latte', 'backp', 'biryani', 'ramen', 'kebab', 'döner', 'veneti', 'chuletas', 'duck_breast', 'tomato', 'mushroom', 'singular_freshly']):
        return 'Market', 'Market and café display', 1.2
    if any(word in name for word in ['tree', 'pine', 'spruce']):
        return 'Garden', 'Arboretum trail', 4.5
    if any(word in name for word in ['flower', 'blossom', 'grass', 'wheat', 'lotus', 'butterfly', 'irises', 'allium', 'daisy', 'jasmine', 'lilie', 'calendula', 'princesse', 'plant', 'vallisneria', 'asas_de_fada', '1000054021']):
        return 'Garden', 'Botanical trail', 0.9
    return 'Crafts', 'Outdoor craft exhibit', 1.8


def material_features(item):
    item['cutout'] = False
    if item['textures'].get('baseColor'):
        with Image.open(ART / 'MeshyLibrary' / item['textures']['baseColor']) as image:
            item['cutout'] = image.mode == 'RGBA' and image.getextrema()[3][0] < 128
    item['materialVersion'] = 2 if item['cutout'] or item['textures'].get('emission') else 1


def finalize():
    path = DEST / 'catalog.json'
    catalog = json.loads(path.read_text())
    for item in catalog:
        if item['usage'] != 'world':
            continue
        material_features(item)
        stats = json.loads((DEST / (item['id'] + '.json')).read_text())
        if stats['triangles'] > 180000:
            raise ValueError('Source exceeds safe per-object budget: ' + item['id'])
        item['triangleBudget'] = max(16000, stats['triangles'])
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')


def prepare():
    records = json.loads((ART / 'MeshyLibrary/manifest.json').read_text())['models']
    characters = json.loads(Path(__file__).with_name('models.json').read_text())
    primary = {}
    for model in characters:
        record = next(item for item in records if item['sha256'] == model.get('sourceHash') or
                      any(source.split('::')[0] == model.get('archive') for source in item['sources']))
        primary[record['sha256']] = model['id']
    motions = json.loads((ART / 'Motion/manifest.json').read_text())
    motion_by_hash = {}
    for item in motions:
        motion_by_hash.setdefault(item['sourceHash'], []).append(item)
    previous = {item['sourceHash']: item for item in json.loads((DEST / 'catalog.json').read_text())} if (DEST / 'catalog.json').exists() else {}
    catalog = []
    for source in records:
        key = source['sha256']
        record = {**previous.get(key, {}), 'id': 'World' + key[:10], 'name': source['name'], 'sourceHash': key, 'source': source['fbx'],
                  'textures': source['textures'], 'sources': source['sources']}
        bindings = motion_by_hash.get(key, [])
        motion = bindings[0] if bindings else None
        if key in primary or motion:
            record.update(usage='character' if key in primary else 'animation', model=primary.get(key, motion['model'] if motion else ''),
                          purpose='Playable protagonist' if primary.get(key) in ['Academy', 'Daydream'] else 'Distinct talking resident')
            if motion:
                record.update(motion=motion['motion'], motions=[binding['motion'] for binding in bindings],
                              purpose='Supplied animations on ' + motion['model'])
        else:
            zone, purpose, size = category(source['name'], key[:10])
            record.update(usage='world', zone=zone, purpose=purpose, extent=size, triangleBudget=record.get('triangleBudget', 16000), textureLimit=512)
            record['targetHeight'] = 1.35 if any(word in source['name'].lower() for word in ['piano', 'рояль']) else 0
            material_features(record)
        catalog.append(record)
    # Curated themed rows around the existing town. Plot placement is completed in Unity
    # against its actual colliders; the resulting position is written back to this catalogue.
    counts = {}
    for record in catalog:
        if record['usage'] == 'world':
            index = counts.get(record['zone'], 0)
            counts[record['zone']] = index + 1
            record.setdefault('position', [0, 0, 0])
    DEST.mkdir(parents=True, exist_ok=True)
    (DEST / 'catalog.json').write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
    print('Catalogue:', len(catalog), 'sources;', counts, flush=True)
    subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender', '--background', '--factory-startup',
                    '--python-exit-code', '1', '--python', str(Path(__file__).with_name('world_export.py'))], check=True)
    finalize()
    subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender', '--background', '--factory-startup',
                    '--python-exit-code', '1', '--python', str(Path(__file__).with_name('context_export.py'))], check=True)

if __name__ == '__main__':
    prepare()
