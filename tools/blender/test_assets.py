"""Check exported art contracts without requiring Blender for each test run."""
import json
from pathlib import Path
import struct
import unittest

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'games/unity/Assets/_Game/Art/Blender'


def glb(name):
    data = (ART / (name + '.glb')).read_bytes()
    magic, version, size = struct.unpack_from('<III', data)
    if (magic, version, size) != (0x46546C67, 2, len(data)):
        raise ValueError('Invalid glTF binary header')
    length, kind = struct.unpack_from('<II', data, 12)
    if kind != 0x4E4F534A:
        raise ValueError('Missing glTF JSON chunk')
    return json.loads(data[20:20 + length])


class BlenderArtTests(unittest.TestCase):
    def test_reference_characters_have_real_skinned_meshes_and_bounded_exports(self):
        for name in ('ReferencePlayer', 'ReferenceBarista'):
            with self.subTest(name=name):
                self.assertTrue((ROOT / 'art/blender' / (name + '.blend')).is_file())
                model = glb(name)
                self.assertEqual(len(model['meshes']), 1)
                skin = model['skins'][0]
                self.assertEqual(len(skin['joints']), 53)
                self.assertEqual(model['accessors'][skin['inverseBindMatrices']]['count'], 53)
                names = {model['nodes'][index]['name'] for index in skin['joints']}
                self.assertTrue({'Head', 'LeftArm', 'RightArm', 'LeftLeg', 'RightLeg'} <= names)
                parts = model['meshes'][0]['primitives']
                self.assertTrue(all('JOINTS_0' in part['attributes'] and 'WEIGHTS_0' in part['attributes']
                                    for part in parts))
                triangles = sum(model['accessors'][part['indices']]['count'] // 3 for part in parts)
                self.assertGreater(triangles, 30000, 'Do not replace the anatomical mesh with a block proxy.')
                self.assertLess(triangles, 60000)
                self.assertLess((ART / (name + '.glb')).stat().st_size, 5 * 1024 * 1024)
                self.assertTrue(all('bufferView' in image for image in model['images']),
                                'Player builds need embedded, reproducible textures.')

    def test_lodge_is_editable_and_contains_its_mission_board(self):
        self.assertTrue((ROOT / 'art/blender/SpeakingLodge.blend').is_file())
        model = glb('SpeakingLodge')
        names = {node.get('name') for node in model['nodes']}
        self.assertIn('Mission board', names)
        self.assertIn('Lodge', names)
        self.assertGreaterEqual(len(model['materials']), 4)
        triangles = sum(model['accessors'][part['indices']]['count'] // 3
                        for mesh in model['meshes'] for part in mesh['primitives'])
        self.assertLess(triangles, 30000, 'The lodge must stay lightweight for the browser.')

    def test_guide_keeps_movable_joints_in_the_export(self):
        self.assertTrue((ROOT / 'art/blender/TrailGuide.blend').is_file())
        model = glb('TrailGuide')
        names = {node.get('name') for node in model['nodes']}
        for joint in ['Head', 'LeftArm', 'RightArm', 'LeftLeg', 'RightLeg']:
            self.assertIn(joint, names)
        triangles = sum(model['accessors'][part['indices']]['count'] // 3
                        for mesh in model['meshes'] for part in mesh['primitives'])
        self.assertGreater(triangles, 1000)
        self.assertLess(triangles, 20000)


if __name__ == '__main__':
    unittest.main()
