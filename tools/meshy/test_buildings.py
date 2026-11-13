"""Architecture must be applied to the town, not merely imported or exhibited."""
import json
from pathlib import Path
import unittest

ART = Path(__file__).resolve().parents[2] / 'games/unity/Assets/_Game/Art'

class BuildingTests(unittest.TestCase):
    def test_every_inspected_building_has_a_contextual_assignment(self):
        path = ART / 'architecture.json'
        self.assertTrue(path.exists(), 'Missing contextual architecture assignments')
        rows = json.loads(path.read_text())
        sources = [row for row in json.loads((ART/'WorldAssets/catalog.json').read_text())
                   if row.get('zone') in ['Architecture', 'Home']]
        self.assertEqual({row['id'] for row in rows}, {row['id'] for row in sources})
        self.assertEqual(len(rows), len({row['id'] for row in rows}))
        rooms = [row for row in rows if row['kind'] == 'room']
        self.assertEqual({row['scene'] for row in rooms}, {'counter','home','school','market','restaurant','japan','mall'})
        for row in rows:
            self.assertTrue(row['label'])
            self.assertIn(row['kind'], ['room','interior','district'])
            self.assertGreater(row['height'], 0)

if __name__ == '__main__':
    unittest.main()
