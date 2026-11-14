"""Upgrade only the supplied complete animation packs; preserve stable actor IDs."""
from pathlib import Path, PurePosixPath
import json
import shutil
import zipfile
from tools.meshy import library

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'games/unity/Assets/_Game/Art'
BACKUP = ROOT / '.local/meshy/animated-upgrade/backup'
REGISTRY = Path(__file__).with_name('replacements.json')


def select_source(archive):
    with zipfile.ZipFile(archive) as source:
        entries = source.infolist()
        for entry in entries:
            path = PurePosixPath(entry.filename)
            if path.is_absolute() or '..' in path.parts or '\\' in entry.filename or entry.file_size > 2_000_000_000:
                raise ValueError('Unsafe animation archive member: ' + entry.filename)
        names = [entry.filename for entry in entries if not entry.is_dir()]
        candidates = [name for name in names if name.endswith('_All_animation.fbx')]
        if len(candidates) != 1:
            raise ValueError('Expected one complete animated FBX in ' + str(archive))
        model = candidates[0]
        return (archive, model), library.textures(PurePosixPath(model), names)


def bind_takes(takes):
    """Bind locomotion explicitly; retain every other real take as a named preview."""
    preferred = {'Walk': ['Casual_Walk', 'Walking'], 'Run': ['Running', 'RunFast'], 'Vault': ['Unarmed_Vault']}
    chosen = {role: next((take for take in choices if take in takes), None) for role, choices in preferred.items()}
    roles = {take: role for role, take in chosen.items() if take is not None}
    return [{'motion': roles.get(take, take), 'take': take} for take in takes]


def replace_records(previous, replacements, retired):
    active = {row['sha256']: row for row in previous if row['sha256'] not in retired}
    active.update({row['sha256']: row for row in replacements})
    return sorted(active.values(), key=lambda row: row['name'].lower())


def backup(path):
    if not path.exists():
        return
    target = BACKUP / path.relative_to(ROOT)
    if target.exists():
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    if path.is_dir():
        shutil.copytree(path, target)
    else:
        shutil.copy2(path, target)


def apply_sources():
    probe = ROOT / '.local/meshy/animated-upgrade'
    probe.mkdir(parents=True, exist_ok=True)
    registry = json.loads(REGISTRY.read_text())
    sources = [dict(id=ident, archive=row['archive'], member=row['member'], sha256=row['sourceHash'])
               for ident, row in registry.items()]
    downloads = Path.home() / 'Downloads'
    for row in sources:
        archive = downloads / row['archive']
        if not archive.exists():
            # Downloads may be reorganized or renamed after the files were attached.
            matches = []
            for candidate in downloads.rglob('*.zip'):
                if candidate.stat().st_size > 500_000_000:
                    continue
                if not zipfile.is_zipfile(candidate):
                    continue
                with zipfile.ZipFile(candidate) as pack:
                    if row['member'] in pack.namelist():
                        matches.append(candidate)
            if len(matches) != 1:
                raise ValueError('Cannot uniquely locate animation archive: ' + row['archive'])
            archive = matches[0]
        row['archive'] = archive.relative_to(downloads).as_posix()
    selected = [select_source(downloads / row['archive']) for row in sources]
    staging = probe / 'library'
    replacements = library.import_all(Path.home() / 'Downloads', staging, sources=selected)
    master = ART / 'MeshyLibrary'
    manifest_path = master / 'manifest.json'
    previous_doc = json.loads(manifest_path.read_text())
    models_path = Path(__file__).with_name('models.json')
    models = json.loads(models_path.read_text())
    retired = set()
    cleanup = []
    for source in sources:
        ident = source['id']
        family = Path(source['member']).stem.removeprefix('Meshy_AI_').split('_animated_mixamo_')[0].lower()
        current = next(row for row in replacements if row['sha256'] == source['sha256'])
        old = [row for row in previous_doc['models'] if row['name'].lower().startswith(family) and row['sha256'] != source['sha256']]
        retired.update(row['sha256'] for row in old)
        for row in old:
            cleanup.append(str((master / row['fbx']).parent.relative_to(ROOT)))
        takes = [binding['take'] for binding in registry[ident]['bindings']]
        model = next(row for row in models if row['id'] == ident)
        model.update(archive=source['archive'], sourceHash=source['sha256'], fullAnimation=True,
                     animation='Casual_Walk' if 'Casual_Walk' in takes else 'Walking',
                     triangleBudget=30000, textureLimit=1024)
        old_hashes = set(registry.get(ident, {}).get('superseded', []))
        retired.update(old_hashes)
        registry[ident] = {'archive': source['archive'], 'sourceHash': source['sha256'],
                           'member': source['member'], 'bindings': bind_takes(takes),
                           'superseded': sorted(old_hashes | {row['sha256'] for row in old})}
        source_folder = staging / Path(current['fbx']).parent
        shutil.copytree(source_folder, master / Path(current['fbx']).parent, dirs_exist_ok=True)
        for relative in ['Meshy/' + ident + '.glb', 'Meshy/manifest.json', 'Motion/manifest.json']:
            backup(ART / relative)
    backup(manifest_path)
    backup(models_path)
    backup(REGISTRY)
    previous_doc['models'] = replace_records(previous_doc['models'], replacements, retired)
    manifest_path.write_text(json.dumps(previous_doc, ensure_ascii=False, indent=2) + '\n')
    models_path.write_text(json.dumps(models, indent=2) + '\n')
    REGISTRY.write_text(json.dumps(registry, indent=2) + '\n')
    from tools.meshy.prepare import from_library
    staging = probe / 'normalized'
    staging.mkdir(parents=True, exist_ok=True)
    records = []
    for model in models:
        if model['id'] in registry:
            records.append({**model, **from_library(model, staging / model['id'])})
        else:
            records.append({'id': model['id']})  # Incremental conversion preserves these existing exports.
    (staging / 'sources.json').write_text(json.dumps(records, indent=2) + '\n')
    (probe / 'cleanup.json').write_text(json.dumps(cleanup, indent=2))
    print('Canonical animated characters prepared:', len(replacements), '; retired family sources:', len(retired))


def retire_superseded():
    """Remove only verified replacements, with a local recovery copy of each asset."""
    report = ROOT / '.local/meshy/animated-upgrade/unity-report.json'
    if not report.exists() or not json.loads(report.read_text()).get('passed'):
        raise ValueError('Verify replacement prefabs in Unity before retiring old assets')
    registry = json.loads(REGISTRY.read_text())
    retired = {sha for actor in registry.values() for sha in actor['superseded']}
    paths = []
    for folder in (ART / 'MeshyLibrary').iterdir():
        if folder.is_dir() and any(folder.name.endswith('_' + sha[:10]) for sha in retired):
            paths.append(folder)
    paths.extend(ART / 'Motion' / name for name in
                 ['Academy-Vault.fbx', 'Daydream-Walk.fbx', 'Daydream-Run.fbx'])
    # Two static uniform displays were alternate protagonist exports, not distinct residents.
    for ident in ['Worldafc7b82cee', 'World55b50b8147']:
        for folder in [ART / 'WorldAssets', ROOT / 'games/unity/Assets/Generated/WorldAssets']:
            paths.extend(path for path in folder.glob(ident + '.*') if path.suffix != '.meta')
    for path in paths:
        for item in [path, Path(str(path) + '.meta')]:
            if not item.exists():
                continue
            backup(item)
            if item.is_dir():
                shutil.rmtree(item)
            else:
                item.unlink()
    print('Retired superseded project assets; recovery copies:', BACKUP)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--retire', action='store_true', help='Remove verified superseded project assets')
    if parser.parse_args().retire:
        retire_superseded()
    else:
        apply_sources()
