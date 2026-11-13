"""The inspected human roster must be used, not just copied into Assets."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class CastTests(unittest.TestCase):
    def test_every_distinct_human_is_cast_without_repeating_bodies_in_one_scene(self):
        roster = json.loads((ROOT / 'tools/meshy/models.json').read_text())
        cast = json.loads((ROOT / 'games/unity/Assets/_Game/Art/Meshy/cast.json').read_text())
        models = {model['id'] for model in roster}
        protagonists = {'Academy', 'Daydream'}
        used = set()
        for world in ['ForestCafe', 'LifeTown', 'ForestSlice']:
            actors = [actor for actor in cast['actors'] if actor['world'] == world]
            if world == 'ForestSlice':
                actors += cast['residents']
            bodies = [actor['model'] for actor in actors]
            self.assertEqual(len(bodies), len(set(bodies)), world)
            self.assertTrue(set(bodies).issubset(models - protagonists))
            used.update(bodies)
        self.assertEqual(used | protagonists, models)
        self.assertEqual(len({r['id'] for r in cast['residents']}), len(cast['residents']))


if __name__ == '__main__':
    unittest.main()
