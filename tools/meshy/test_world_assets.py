"""Every downloaded FBX must have an explicit real game use, not just an import."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'games/unity/Assets/_Game/Art'

class WorldAssetTests(unittest.TestCase):
    def test_every_unique_source_has_live_character_motion_or_placed_usage(self):
        path = ART / 'WorldAssets/catalog.json'
        self.assertTrue(path.exists(), 'All FBXs need a gameplay usage catalog')
        catalog = json.loads(path.read_text())
        sources = json.loads((ART / 'MeshyLibrary/manifest.json').read_text())['models']
        self.assertEqual({item['sha256'] for item in sources}, {item['sourceHash'] for item in catalog})
        for item in catalog:
            with self.subTest(source=item['name']):
                self.assertIn(item['usage'], ['character', 'animation', 'world'])
                self.assertTrue(item['purpose'])
                if item['usage'] == 'world':
                    self.assertTrue(item['zone'])
                    self.assertEqual(len(item['position']), 3)
                    self.assertTrue((ART / 'WorldAssets' / (item['id'] + '.glb')).exists())
                    self.assertNotEqual(item['position'], [0, 0, 0], 'A catalogue entry alone is not a scene placement')
                    report = json.loads((ART / 'WorldAssets' / (item['id'] + '.json')).read_text())
                    self.assertLessEqual(report['triangles'], item['triangleBudget'])
                    self.assertLessEqual(item['triangleBudget'], 180000)
                    self.assertLessEqual(report['textureLimit'], 512)
                    self.assertLess(report['bytes'], 16_000_000)
                else:
                    self.assertTrue(item['model'])

    def test_all_supplied_clips_have_a_named_motion_binding(self):
        path = ART / 'WorldAssets/catalog.json'
        self.assertTrue(path.exists(), 'Supplied animation bindings are absent')
        catalog = json.loads(path.read_text())
        motions = [{**item, 'motion': role} for item in catalog
                   for role in item.get('motions', [item['motion']] if item.get('motion') else [])]
        expected = json.loads((ART / 'Motion/manifest.json').read_text())
        self.assertEqual(len(motions), len(expected))
        self.assertEqual({(m['sourceHash'], m['model'], m['motion']) for m in motions},
                         {(m['sourceHash'], m['model'], m['motion']) for m in expected})
        self.assertTrue({'Walk', 'Run', 'Vault'}.issubset({item['motion'] for item in motions}))
        self.assertTrue(all(item['model'] in {item['model'] for item in expected} for item in motions))

if __name__ == '__main__':
    unittest.main()
