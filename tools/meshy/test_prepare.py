"""Checks for safe, repeatable import of user-supplied model archives."""
import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

SPEC = importlib.util.spec_from_file_location('prepare', Path(__file__).with_name('prepare.py'))


class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def prepare_module(self):
        self.assertTrue(Path(SPEC.origin).exists(), 'The repeatable model importer is missing')
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        return module

    def archive(self, entries):
        path = self.root / 'model.zip'
        with zipfile.ZipFile(path, 'w') as output:
            for name, content in entries.items():
                output.writestr(name, content)
        return path

    def test_extract_preserves_source_and_maps_material_channels(self):
        module = self.prepare_module()
        archive = self.archive({'model/body.fbx': b'mesh', 'model/body.png': b'base',
                                'model/body_normal.png': b'normal', 'model/body_roughness.png': b'rough'})
        original = archive.read_bytes()
        output = module.extract(archive, self.root / 'source')
        self.assertEqual(output['fbx'], 'Model.fbx')
        self.assertEqual((self.root / 'source/BaseColor.png').read_bytes(), b'base')
        self.assertEqual((self.root / 'source/Normal.png').read_bytes(), b'normal')
        self.assertEqual((self.root / 'source/Roughness.png').read_bytes(), b'rough')
        self.assertEqual(archive.read_bytes(), original)
        self.assertEqual(len(output['sha256']), 64)

    def test_library_source_keeps_optional_maps_and_original_bytes(self):
        module = self.prepare_module()
        library = self.root / 'library'
        library.mkdir()
        (library / 'body.fbx').write_bytes(b'authored animated body')
        (library / 'body.png').write_bytes(b'base')
        sha = hashlib.sha256(b'authored animated body').hexdigest()
        record = {'sha256': sha, 'fbx': 'body.fbx', 'textures': {'baseColor': 'body.png'},
                  'sources': ['grandfather.fbx']}
        (library / 'manifest.json').write_text(json.dumps({'models': [record]}))
        output = module.from_library({'sourceHash': sha}, self.root / 'normalized', library)
        self.assertEqual(output['sha256'], sha)
        self.assertEqual((self.root / 'normalized/Model.fbx').read_bytes(), b'authored animated body')
        self.assertEqual((self.root / 'normalized/BaseColor.png').read_bytes(), b'base')
        self.assertFalse((self.root / 'normalized/Normal.png').exists())
        self.assertEqual((library / 'body.fbx').read_bytes(), b'authored animated body')

    def test_unsafe_paths_are_rejected_before_any_extraction(self):
        module = self.prepare_module()
        archive = self.archive({'good/model.fbx': b'mesh', '../escape.png': b'bad'})
        with self.assertRaises(ValueError):
            module.extract(archive, self.root / 'source')
        self.assertFalse((self.root / 'source/Model.fbx').exists())

    def test_ambiguous_models_or_materials_are_rejected(self):
        module = self.prepare_module()
        for entries in ({'a.fbx': b'a', 'b.fbx': b'b'},
                        {'a.fbx': b'a', 'a.png': b'a', 'b.png': b'b'}, {'a.png': b'a'}):
            with self.subTest(entries=entries):
                with self.assertRaises(ValueError):
                    module.extract(self.archive(entries), self.root / 'source')


if __name__ == '__main__':
    unittest.main()
