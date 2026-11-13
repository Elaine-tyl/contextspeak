"""Semantic scale prevents furniture, roads and aircraft becoming generic tiny props."""
import unittest
import hashlib
import json
import struct
from pathlib import Path
from prepare_world import category


class EnvironmentTests(unittest.TestCase):
    def test_contextual_copies_keep_materials_and_bounded_geometry(self):
        root = Path(__file__).resolve().parents[2] / 'games/unity/Assets/_Game/Art/WorldAssets'
        def document(path):
            data = path.read_bytes()
            size = struct.unpack_from('<I', data, 12)[0]
            return json.loads(data[20:20 + size])
        for row in json.loads((root / 'Contextual/report.json').read_text()):
            with self.subTest(model=row['id']):
                source = root / (row['id'] + '.glb')
                self.assertEqual(row['sourceHash'], hashlib.sha256(source.read_bytes()).hexdigest())
                original = document(source)
                copy = document(root / 'Contextual' / source.name)
                triangles = sum(copy['accessors'][primitive['indices']]['count'] // 3
                                for mesh in copy['meshes'] for primitive in mesh['primitives'])
                self.assertLessEqual(triangles, row['target'])
                self.assertTrue(copy.get('images'))
                for channel in ['normalTexture', 'emissiveTexture', 'alphaMode']:
                    if any(channel in mat for mat in original['materials']):
                        self.assertTrue(any(channel in mat for mat in copy['materials']), channel)

    def test_pianos_keep_instrument_scale(self):
        for name in ['grand_piano_with_beethoven', 'рояль_на_кру']:
            self.assertEqual(category(name, '')[2], 3.0)

    def test_asphalt_is_a_road_asset(self):
        self.assertEqual(category('Double_Yellow_Lines_on_Asphalt', '')[:2],
                         ('Roads', 'Walkable town road'))

    def test_towering_pine_is_not_a_tower_building(self):
        self.assertEqual(category('Towering_Pine_20260710_texture', ''),
                         ('Garden', 'Arboretum trail', 4.5))
        self.assertEqual(category('Brick_Horizon_Tower_20261020_texture', '')[0], 'Architecture')

    def test_new_architecture_and_aircraft_are_not_generic_crafts(self):
        for name in ['Brick_Horizon_Tower', 'modern_city_building', '摩天大楼，很高']:
            self.assertEqual(category(name, '')[0], 'Architecture')
        for name in ['Aereo_gigante_airliner', 'Azure_Horizon_Jet', 'yolcu_uçağı']:
            self.assertEqual(category(name, '')[0], 'Vehicles')


if __name__ == '__main__':
    unittest.main()
