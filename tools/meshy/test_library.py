"""Regressions for the real ZIP/loose-file import boundary."""
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from tools.meshy import library


class LibraryTests(unittest.TestCase):
    def test_incremental_intake_preserves_models_no_longer_in_downloads(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            first = downloads / 'First.fbx'
            first.write_bytes(b'first')
            old = library.import_all(downloads, target)
            first.unlink()
            (downloads / 'Second.fbx').write_bytes(b'second')
            records = library.import_all(downloads, target, merge=True)
            self.assertEqual(len(records), 2)
            self.assertIn(old[0], records)
            self.assertEqual(records, library.import_all(downloads, target, merge=True))

    def test_unrelated_invalid_zip_and_project_backup_do_not_block_meshy(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'unrelated.zip').write_text('not a ZIP archive')
            (root / 'New.fbx').write_bytes(b'new')
            with zipfile.ZipFile(root / 'ContextSpeak-project-backup.zip', 'w') as pack:
                pack.writestr('Assets/Old.fbx', b'old')
            sources = library.discover(root)
            self.assertEqual(len(sources), 1)
            self.assertEqual(sources[0][0], (root / 'New.fbx', None))

    def test_all_fbx_sources_are_imported_without_a_brand_name(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            (downloads / 'Grandfather.FBX').write_bytes(b'grandfather')
            with zipfile.ZipFile(downloads / 'People.ZIP', 'w') as archive:
                archive.writestr('characters/Teacher.fbx', b'teacher')
                archive.writestr('characters/Teacher.png', b'color')
            records = library.import_all(downloads, target)
            self.assertEqual(len(records), 2)
            self.assertEqual({(target / record['fbx']).read_bytes() for record in records},
                             {b'grandfather', b'teacher'})
            self.assertEqual(records, library.import_all(downloads, target))

    def test_webp_disguised_as_png_is_converted_without_changing_download(self):
        from io import BytesIO
        from PIL import Image
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            pixels = BytesIO()
            Image.new('RGBA', (2, 2), (20, 40, 60, 200)).save(pixels, format='WEBP', lossless=True)
            archive_path = downloads / 'Meshy_flower.zip'
            with zipfile.ZipFile(archive_path, 'w') as archive:
                archive.writestr('Meshy_flower.fbx', b'flower')
                archive.writestr('Meshy_flower.png', pixels.getvalue())
            original = archive_path.read_bytes()
            record = library.import_all(downloads, target)[0]
            texture = target / record['textures']['baseColor']
            with Image.open(texture) as image:
                self.assertEqual(image.format, 'PNG')
                self.assertEqual(image.getpixel((0, 0)), (20, 40, 60, 200))
            stamp = texture.stat().st_mtime_ns
            library.import_all(downloads, target)
            self.assertEqual(texture.stat().st_mtime_ns, stamp)
            self.assertEqual(archive_path.read_bytes(), original)

    def test_zip_loose_duplicates_and_optional_maps_are_repeatable(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            with zipfile.ZipFile(downloads / '桌子.zip', 'w') as archive:
                archive.writestr('pack/Meshy_AI_Table_texture.fbx', b'fbx table')
                archive.writestr('pack/Meshy_AI_Table_texture.png', b'base color')
            (downloads / 'Meshy_AI_Table_texture.fbx').write_bytes(b'fbx table')
            (downloads / 'Meshy_AI_Chair_generate.fbx').write_bytes(b'fbx chair')
            records = library.import_all(downloads, target)
            self.assertEqual(len(records), 2)
            table = next(r for r in records if 'Table' in r['name'])
            self.assertEqual(len(table['sources']), 2)
            self.assertEqual((target / table['fbx']).read_bytes(), b'fbx table')
            self.assertEqual((target / table['textures']['baseColor']).read_bytes(), b'base color')
            chair = next(r for r in records if 'Chair' in r['name'])
            self.assertEqual(chair['textures'], {})
            stamp = (target / table['fbx']).stat().st_mtime_ns
            again = library.import_all(downloads, target)
            self.assertEqual(records, again)
            self.assertEqual((target / table['fbx']).stat().st_mtime_ns, stamp)
            self.assertEqual(json.loads((target / 'manifest.json').read_text())['models'], records)

    def test_surface_map_inverts_roughness_and_survives_rerun(self):
        from PIL import Image
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            model = downloads / 'Meshy_Surface.fbx'
            model.write_bytes(b'model')
            for suffix, value in [('_metallic', 40), ('_roughness', 200)]:
                Image.new('L', (2, 2), value).save(downloads / ('Meshy_Surface' + suffix + '.png'))
            first = library.import_all(downloads, target)
            self.assertIn('surface', first[0]['textures'])
            with Image.open(target / first[0]['textures']['surface']) as image:
                self.assertEqual(image.getpixel((0, 0)), (40, 0, 0, 55))
            self.assertEqual(first, library.import_all(downloads, target))

    def test_new_duplicate_alias_keeps_existing_unity_asset_path(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            def add(name):
                with zipfile.ZipFile(downloads / (name + '.zip'), 'w') as archive:
                    archive.writestr(name + '.fbx', b'same model')
            add('Meshy_B')
            first = library.import_all(downloads, target)[0]
            add('Meshy_A')  # A later download sorts before the existing canonical source.
            second = library.import_all(downloads, target)[0]
            self.assertEqual(first['fbx'], second['fbx'])
            self.assertEqual(first['name'], second['name'])
            self.assertEqual(len(list(target.rglob('*.fbx'))), 1)
            self.assertEqual(len(second['sources']), 2)

    def test_snapshot_does_not_absorb_downloads_arriving_during_a_build(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            (downloads / 'First.fbx').write_bytes(b'first')
            snapshot = library.discover(downloads)
            (downloads / 'Later.fbx').write_bytes(b'later')
            records = library.import_all(downloads, target, sources=snapshot)
            self.assertEqual(len(records), 1)
            self.assertEqual(records[0]['sources'], ['First.fbx'])

    def test_rejects_zip_traversal_before_writing_assets(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            with zipfile.ZipFile(downloads / 'Meshy_bad.zip', 'w') as archive:
                archive.writestr('Meshy_bad.fbx', b'model')
                archive.writestr('../outside.png', b'bad')
            with self.assertRaisesRegex(ValueError, 'Unsafe archive'):
                library.import_all(downloads, target)
            self.assertFalse(target.exists())

    def test_same_filename_different_models_keep_separate_textures(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            downloads, target = root / 'Downloads', root / 'Assets'
            downloads.mkdir()
            for index in range(2):
                with zipfile.ZipFile(downloads / f'Meshy_{index}.zip', 'w') as archive:
                    archive.writestr('Meshy_AI_Model.fbx', bytes([index]))
                    archive.writestr('Meshy_AI_Model.png', bytes([index + 2]))
            records = library.import_all(downloads, target)
            self.assertEqual(len({r['fbx'] for r in records}), 2)
            for record in records:
                model = (target / record['fbx']).read_bytes()[0]
                self.assertEqual((target / record['textures']['baseColor']).read_bytes(), bytes([model + 2]))


if __name__ == '__main__':
    unittest.main()
