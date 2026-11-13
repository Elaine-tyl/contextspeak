"""Exercise the Blender rebuild command against the already-shipped assets."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from tools.blender.build import blender_binary

ROOT = Path(__file__).resolve().parents[2]


class BlenderBuildTests(unittest.TestCase):
    @unittest.skipUnless(blender_binary().is_file(), 'Blender is required for the launcher test')
    def test_generator_exception_fails_build_even_with_cached_assets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            scripts = root / 'tools/blender'
            scripts.mkdir(parents=True)
            for name in ('build.py', 'test_assets.py'):
                shutil.copyfile(ROOT / 'tools/blender' / name, scripts / name)
            for name in ('art', 'games'):
                (root / name).symlink_to(ROOT / name, target_is_directory=True)
            (scripts / 'speaking_lodge.py').write_text(
                "raise RuntimeError('export failed before writing files')\n", encoding='utf-8')
            sentinel = root / 'second-generator-started'
            (scripts / 'reference_character.py').write_text(
                "from pathlib import Path\nPath('second-generator-started').touch()\n", encoding='utf-8')
            result = subprocess.run([sys.executable, str(scripts / 'build.py')], cwd=root,
                                    capture_output=True, text=True, timeout=60)
            output = result.stdout + result.stderr
            self.assertIn('RuntimeError: export failed before writing files', output)
            self.assertNotEqual(result.returncode, 0, output)
            self.assertFalse(sentinel.exists(), 'A failed generator must stop the build immediately.')
