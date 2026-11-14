"""Prepare supplied clips with native FBX skinning; source files stay intact."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'games/unity/Assets/_Game/Art/MeshyLibrary'
OUTPUT = ROOT / 'games/unity/Assets/_Game/Art/Motion'
BINDINGS = [
    ('TrenchElder', 'Walk', '81fedc6499'),
    ('OldCardigan', 'Walk', '64f9efaab6'),
    ('OfficerMason', 'Walk', '816fb69a44'), ('OfficerTaylor', 'Walk', '5f00929bd8'),
]

def prepare():
    records = json.loads((LIBRARY / 'manifest.json').read_text())['models']
    OUTPUT.mkdir(parents=True, exist_ok=True)
    entries = []
    heights = {item['id']: item['height'] for item in json.loads(Path(__file__).with_name('models.json').read_text())}
    registry_path = Path(__file__).with_name('replacements.json')
    replacements = json.loads(registry_path.read_text()) if registry_path.exists() else {}
    bindings = [(model, motion, prefix, None) for model, motion, prefix in BINDINGS]
    for model, replacement in replacements.items():
        bindings.extend((model, binding['motion'], replacement['sourceHash'], binding['take'])
                        for binding in replacement['bindings'])
    for model, motion, prefix, take in bindings:
        source = next(item for item in records if item['sha256'].startswith(prefix))
        folder = OUTPUT / 'Textures' / model
        folder.mkdir(parents=True, exist_ok=True)
        maps = {}
        channels = dict(source['textures'])
        if not channels.get('baseColor'):
            embedded = list((LIBRARY / Path(source['fbx']).parent / 'EmbeddedTextures').glob('*.png'))
            if len(embedded) != 1:
                raise ValueError('Extract the single embedded character atlas in Unity first: ' + model)
            channels['baseColor'] = str(embedded[0].relative_to(LIBRARY))
        for channel, name in [('baseColor', 'BaseColor'), ('normal', 'Normal'), ('surface', 'MetallicSmoothness')]:
            if channel not in channels:
                continue
            target = folder / (name + '.png')
            with Image.open(LIBRARY / channels[channel]) as image:
                image.thumbnail((1024, 1024), Image.Resampling.LANCZOS)
                image.save(target)
            maps[channel] = target.relative_to(OUTPUT).as_posix()
        entries.append({'model': model, 'motion': motion, 'sourceHash': source['sha256'],
                        'source': source['fbx'], 'height': heights[model], 'textures': maps,
                        'file': model + ('-All' if take else '-' + motion) + '.fbx',
                        **({'take': take, 'native': True} if take else {})})
        if take:
            target = OUTPUT / (model + '-All.fbx')
            if not target.exists() or hashlib.sha256(target.read_bytes()).hexdigest() != source['sha256']:
                shutil.copy2(LIBRARY / source['fbx'], target)
    (OUTPUT / 'manifest.json').write_text(json.dumps(entries, indent=2) + '\n')
    subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender', '--background', '--factory-startup',
                    '--python-exit-code', '1', '--python', str(Path(__file__).with_name('motion_export.py'))],
                   check=True, cwd=ROOT)

if __name__ == '__main__':
    prepare()
