"""Locate Blender, rebuild original assets, then verify the exported files."""
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def blender_binary():
    return Path(os.environ.get('BLENDER_BINARY') or shutil.which('blender')
                or '/Applications/Blender.app/Contents/MacOS/Blender')


def main():
    editor = blender_binary()
    if not editor.is_file():
        raise SystemExit('Install Blender 5.3, or set BLENDER_BINARY to its executable.')
    for script, arguments in (('speaking_lodge.py', []), ('reference_character.py', []),
                              ('reference_character.py', ['--', '--barista'])):
        generator = Path(__file__).with_name(script)
        subprocess.run([editor, '--background', '--factory-startup', '--python-exit-code', '1',
                        '--python', generator, *arguments], cwd=ROOT, check=True)
    # Publish only validated exports. Normal Unity builds use these checked-in GLBs.
    sys.path.insert(0, str(ROOT))
    from tools.blender.reference_character import validate_glb
    destination = ROOT / 'games/unity/Assets/_Game/Art/Blender'
    for name in ('ReferencePlayer', 'ReferenceBarista'):
        candidate = ROOT / '.local/art-candidates' / (name + '.glb')
        validate_glb(candidate)
        shutil.copyfile(candidate, destination / candidate.name)
    subprocess.run([sys.executable, '-m', 'unittest', 'tools/blender/test_assets.py'],
                   cwd=ROOT, check=True)


if __name__ == '__main__':
    main()
