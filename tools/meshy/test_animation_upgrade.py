"""Complete animation packs replace static companions without changing identities."""
import tempfile
import unittest
import zipfile
from pathlib import Path

class AnimationUpgradeTests(unittest.TestCase):
    def test_selects_full_animation_fbx_and_its_maps_only(self):
        from tools.meshy.animation_upgrade import select_source
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / 'pack.zip'
            with zipfile.ZipFile(archive, 'w') as z:
                z.writestr('avatar/Meshy_AI_Elliot_animated_mixamo_All_animation.fbx', b'animated')
                z.writestr('avatar/Meshy_AI_Elliot_character_mixamo.fbx', b'static')
                z.writestr('avatar/Meshy_AI_Elliot_animated_mixamo_All_animation.png', b'base')
            source, maps = select_source(archive)
            self.assertTrue(source[1].endswith('_All_animation.fbx'))
            self.assertEqual(set(maps), {'baseColor'})

    def test_missing_or_ambiguous_animation_source_is_rejected(self):
        from tools.meshy.animation_upgrade import select_source
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / 'pack.zip'
            for names in [['static.fbx'], ['a_All_animation.fbx', 'b_All_animation.fbx']]:
                with zipfile.ZipFile(archive, 'w') as z:
                    for name in names: z.writestr(name, b'fbx')
                with self.assertRaises(ValueError): select_source(archive)

    def test_roles_keep_each_real_take_once_and_use_own_locomotion(self):
        from tools.meshy.animation_upgrade import bind_takes
        takes = ['Walking', 'Casual_Walk', 'Running', 'Run_02', 'Unarmed_Vault', 'Agree_Gesture']
        rows = bind_takes(takes)
        self.assertEqual(len(rows), len(takes))
        self.assertEqual({row['take'] for row in rows}, set(takes))
        roles = {row['motion']: row['take'] for row in rows}
        self.assertEqual(roles['Walk'], 'Casual_Walk')
        self.assertEqual(roles['Run'], 'Running')
        self.assertEqual(roles['Vault'], 'Unarmed_Vault')
        self.assertEqual(roles['Agree_Gesture'], 'Agree_Gesture')

    def test_library_ignores_retired_sources_and_static_pack_companions(self):
        from tools.meshy import library
        import hashlib
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            downloads = root / 'Downloads'
            downloads.mkdir()
            (downloads / 'old.fbx').write_bytes(b'old')
            with zipfile.ZipFile(downloads / 'new.zip', 'w') as pack:
                pack.writestr('Actor_All_animation.fbx', b'animated')
                pack.writestr('Actor_character_mixamo.fbx', b'static')
            records = library.import_all(downloads, root / 'Assets',
                                         retired={hashlib.sha256(b'old').hexdigest()})
            self.assertEqual(len(records), 1)
            self.assertEqual((root / 'Assets' / records[0]['fbx']).read_bytes(), b'animated')

    def test_retirement_changes_only_explicit_superseded_hashes(self):
        from tools.meshy.animation_upgrade import replace_records
        old = [{'sha256': 'old', 'name': 'Elliot'}, {'sha256': 'keep', 'name': 'Other NPC'}]
        result = replace_records(old, [{'sha256': 'new', 'name': 'Elliot'}], {'old'})
        self.assertEqual({row['sha256'] for row in result}, {'new', 'keep'})
        self.assertEqual(len(old), 2)

if __name__ == '__main__': unittest.main()
