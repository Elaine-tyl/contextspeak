"""Import every unique Downloads FBX into a local Unity asset library."""
import argparse
from contextlib import contextmanager
import hashlib
from io import BytesIO
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[2]
DESTINATION = ROOT / 'games/unity/Assets/_Game/Art/MeshyLibrary'
CHANNELS = {'_normal': 'normal', '_roughness': 'roughness', '_metallic': 'metallic',
            '_emission': 'emission', '_basecolor': 'baseColor', '_diffuse': 'baseColor'}


@contextmanager
def open_source(source):
    path, member = source
    if member is None:
        with path.open('rb') as stream:
            yield stream
    else:
        with zipfile.ZipFile(path) as archive, archive.open(member) as stream:
            yield stream


def digest(source):
    with open_source(source) as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def textures(model, files):
    result = {}
    for name in files:
        path = PurePosixPath(name)
        if path.parent != model.parent or path.suffix.lower() not in {'.png', '.jpg', '.jpeg'}:
            continue
        suffix = path.stem.lower().removeprefix(model.stem.lower())
        if path.stem.lower() == model.stem.lower():
            result['baseColor'] = name
        elif path.stem.lower().startswith(model.stem.lower()) and suffix in CHANNELS:
            result[CHANNELS[suffix]] = name
    return result


def discover(downloads):
    """Include renamed/unbranded FBXs and case-insensitive ZIP extensions."""
    sources = []
    for path in sorted(p for p in downloads.rglob('*') if p.is_file() and p.suffix.lower() == '.zip'):
        with zipfile.ZipFile(path) as archive:
            files = [i.filename for i in archive.infolist() if not i.is_dir() and not i.filename.startswith('__MACOSX/')]
            models = [n for n in files if PurePosixPath(n).suffix.lower() == '.fbx']
            if not models:
                continue
            for entry in archive.infolist():
                name = PurePosixPath(entry.filename)
                if name.is_absolute() or '..' in name.parts or '\\' in entry.filename:
                    raise ValueError('Unsafe archive path: ' + entry.filename)
                if entry.file_size > 2_000_000_000:
                    raise ValueError('Asset exceeds 2 GB: ' + entry.filename)
            for model in models:
                sources.append(((path, model), textures(PurePosixPath(model), files)))
    for path in sorted(downloads.rglob('*')):
        if path.is_file() and path.suffix.lower() == '.fbx':
            files = [str(p) for p in path.parent.iterdir() if p.is_file()]
            sources.append(((path, None), textures(PurePosixPath(str(path)), files)))
    return sources


def copy_source(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists() and digest((target, None)) == digest(source):
        return
    temporary = target.with_suffix(target.suffix + '.partial')
    with open_source(source) as stream, temporary.open('wb') as output:
        shutil.copyfileobj(stream, output, length=1024 * 1024)
    temporary.replace(target)


def copy_texture(source, target):
    """Some Meshy PNG downloads contain WebP pixels, which Unity cannot import."""
    with open_source(source) as stream:
        header = stream.read(12)
        if header[:4] == b'RIFF' and header[8:] == b'WEBP':
            from PIL import Image
            with Image.open(stream) as image:
                output = BytesIO()
                encoding = 'PNG' if target.suffix.lower() == '.png' else 'JPEG'
                (image if encoding == 'PNG' else image.convert('RGB')).save(output, format=encoding)
            data = output.getvalue()
            target.parent.mkdir(parents=True, exist_ok=True)
            if not target.exists() or target.read_bytes() != data:
                target.write_bytes(data)
            return
    copy_source(source, target)


def pack_surface(folder, maps):
    """URP Lit reads metallic in R and smoothness (one minus roughness) in A."""
    if not ('metallic' in maps or 'roughness' in maps):
        return
    from PIL import Image, ImageOps
    target = folder / 'MetallicSmoothness.png'
    inputs = [folder / Path(maps[c]).name for c in ('metallic', 'roughness') if c in maps]
    maps['surface'] = (Path(maps[next(iter(maps))]).parent / target.name).as_posix()
    if target.exists() and target.stat().st_mtime_ns >= max(p.stat().st_mtime_ns for p in inputs):
        return
    size = (1024, 1024)
    def channel(name, default):
        if name not in maps:
            return Image.new('L', size, default)
        with Image.open(folder / Path(maps[name]).name) as image:
            return image.convert('L').resize(size, Image.Resampling.LANCZOS)
    metallic, roughness = channel('metallic', 0), channel('roughness', 128)
    zero = Image.new('L', size, 0)
    Image.merge('RGBA', (metallic, zero, zero, ImageOps.invert(roughness))).save(target)


def import_all(downloads, destination, sources=None):
    discovered = discover(downloads) if sources is None else sources  # Validate paths before creating any asset files.
    manifest = destination / 'manifest.json'
    previous = json.loads(manifest.read_text())['models'] if manifest.exists() else []
    canonical = {record['sha256']: record for record in previous}
    records = {}
    for source, images in discovered:
        path, member = source
        sha = digest(source)
        name = Path(member or path.name).stem
        label = re.sub(r'^Meshy_AI_', '', name, flags=re.I)
        label = re.sub(r'[^\w.-]+', '_', label).strip('_')
        folder_name = label + '_' + sha[:10]
        existing = canonical.get(sha, {})
        record = records.setdefault(sha, {'name': existing.get('name', label), 'sha256': sha,
            'fbx': existing.get('fbx', folder_name + '/' + name + '.fbx'),
            'textures': {}, 'sources': []})
        alias = path.relative_to(downloads).as_posix() + ('::' + member if member else '')
        record['sources'].append(alias)
        folder = destination / Path(record['fbx']).parent
        copy_source(source, destination / record['fbx'])
        for channel, image in images.items():
            if channel in record['textures']:
                continue
            image_source = (path, image) if member else (Path(image), None)
            relative = Path(record['fbx']).parent / Path(image).name
            copy_texture(image_source, destination / relative)
            record['textures'][channel] = relative.as_posix()
        pack_surface(folder, record['textures'])
        print('Prepared', record['name'], flush=True)
    result = sorted(records.values(), key=lambda r: r['name'].lower())
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'manifest.json').write_text(json.dumps({
        'provenance': 'User-provided Meshy downloads; redistribution license not verified.',
        'models': result}, ensure_ascii=False, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--downloads', type=Path, default=Path.home() / 'Downloads')
    parser.add_argument('--destination', type=Path, default=DESTINATION)
    args = parser.parse_args()
    records = import_all(args.downloads, args.destination)
    print(f'Prepared {len(records)} unique FBXs in {args.destination}')
