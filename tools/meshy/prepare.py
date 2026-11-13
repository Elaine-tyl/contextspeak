"""Convert the supplied character archives locally; leave the originals untouched."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
MODELS = json.loads(Path(__file__).with_name('models.json').read_text())
LIBRARY = ROOT / 'games/unity/Assets/_Game/Art/MeshyLibrary'


def from_library(model, destination, library=LIBRARY):
    """Prepare a deduplicated FBX, including loose animated files with embedded maps."""
    records = json.loads((library / 'manifest.json').read_text())['models']
    record = next(r for r in records if r['sha256'] == model['sourceHash'])
    destination.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(library / record['fbx'], destination / 'Model.fbx')
    channels = {'baseColor': 'BaseColor', 'normal': 'Normal', 'roughness': 'Roughness', 'metallic': 'Metallic'}
    for channel, name in channels.items():
        if channel in record['textures']:
            shutil.copyfile(library / record['textures'][channel], destination / (name + '.png'))
    return {'archive': record['sources'][0], 'sha256': record['sha256'], 'fbx': 'Model.fbx'}


def extract(archive: Path, destination: Path) -> dict:
    """Validate the whole archive before writing known model/material channels."""
    channels = {'_normal': 'Normal', '_roughness': 'Roughness',
                '_metallic': 'Metallic', '_emission': 'Emission'}
    entries = {}
    with zipfile.ZipFile(archive) as source:
        for entry in source.infolist():
            path = PurePosixPath(entry.filename)
            if path.is_absolute() or '..' in path.parts or '\\' in entry.filename:
                raise ValueError('Unsafe archive path: ' + entry.filename)
            if entry.is_dir() or path.suffix.lower() not in ('.fbx', '.png'):
                continue
            if entry.file_size > 300_000_000:
                raise ValueError('Model channel is unexpectedly large: ' + entry.filename)
            name = 'Model.fbx' if path.suffix.lower() == '.fbx' else next(
                (label + '.png' for suffix, label in channels.items()
                 if path.stem.lower().endswith(suffix)), 'BaseColor.png')
            if name in entries:
                raise ValueError('Ambiguous model channel: ' + name)
            entries[name] = entry
        if 'Model.fbx' not in entries or 'BaseColor.png' not in entries:
            raise ValueError('Each character needs exactly one FBX and a base-color image')
        destination.mkdir(parents=True, exist_ok=True)
        for name, entry in entries.items():
            (destination / name).write_bytes(source.read(entry))
    return {'archive': archive.name, 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
            'fbx': 'Model.fbx'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--downloads', type=Path, default=Path.home() / 'Downloads')
    parser.add_argument('--only', nargs='+', choices=[m['id'] for m in MODELS], default=[])
    parser.add_argument('--skip-preview', action='store_true', help='Skip offline renders when inspected previews already exist')
    args = parser.parse_args()
    if args.only:
        manifest = ROOT / 'games/unity/Assets/_Game/Art/Meshy/manifest.json'
        prepared = {m['id'] for m in json.loads(manifest.read_text())} if manifest.exists() else set()
        if any(m['id'] not in prepared for m in MODELS if m['id'] not in args.only):
            parser.error('Run a full preparation before using --only for incremental updates')
    staging = ROOT / '.local/meshy/normalized'
    staging.mkdir(parents=True, exist_ok=True)
    records = []
    # Validate every supplied source first; conversion never uploads the files.
    for model in MODELS:
        record = from_library(model, staging / model['id']) if 'sourceHash' in model else \
            extract(args.downloads / model['archive'], staging / model['id'])
        records.append({**model, **record})
    (staging / 'sources.json').write_text(json.dumps(records, indent=2) + '\n')
    blender = os.environ.get('BLENDER_BINARY') or shutil.which('blender') or '/Applications/Blender.app/Contents/MacOS/Blender'
    options = ['--skip-preview'] if args.skip_preview else []
    subprocess.run([blender, '--background', '--factory-startup', '--python-exit-code', '1',
                    '--python', str(Path(__file__).with_name('convert.py')), '--', str(staging), *args.only, *options], check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
