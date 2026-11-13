"""Inspect the actual Unity exports, including topology budgets and rig honesty."""
import json
import io
from pathlib import Path
import struct
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'games/unity/Assets/_Game/Art/Meshy'
MODELS = json.loads(Path(__file__).with_name('models.json').read_text())


def glb(path):
    content = path.read_bytes()
    magic, version, length = struct.unpack_from('<4sII', content)
    if magic != b'glTF' or version != 2 or length != len(content):
        raise ValueError('Invalid GLB header')
    size, kind = struct.unpack_from('<II', content, 12)
    if kind != 0x4E4F534A:
        raise ValueError('Missing GLB JSON')
    return json.loads(content[20:20 + size]), content


class CharacterAssets(unittest.TestCase):
    def test_inspected_distinct_humans_are_all_registered(self):
        required = {'TrenchElder', 'OldCardigan', 'Adrian', 'Ali', 'Amael', 'AnimeDetective',
                    'BlueLace', 'ConfidentBoy', 'Contemplation', 'Dawn', 'Executive', 'Hooded',
                    'MaidStone', 'MaidHall', 'MidnightLace', 'OpenArms', 'PeachGala', 'Plaid',
                    'QuietMorning', 'SchoolgirlCasual', 'SkateboardBoy', 'SkaterBoy', 'SmilingBoy',
                    'SoccerCaptain', 'UrbanTrench', 'WhispersPiano'}
        self.assertTrue(required.issubset({m['id'] for m in MODELS}))

    def test_all_characters_have_bounded_real_geometry_and_embedded_materials(self):
        sources = {r['id']: r for r in json.loads((ASSETS / 'manifest.json').read_text())}
        for model in MODELS:
            with self.subTest(model=model['id']):
                path = ASSETS / (model['id'] + '.glb')
                self.assertTrue(path.exists(), 'Missing converted character: ' + model['id'])
                doc, data = glb(path)
                self.assertLess(len(data), 20_000_000)
                primitives = [p for m in doc['meshes'] for p in m['primitives']]
                triangles = sum(doc['accessors'][p['indices']]['count'] // 3 for p in primitives)
                self.assertGreater(triangles, 1000)
                self.assertLessEqual(triangles, 60000)
                self.assertTrue(all('NORMAL' in p['attributes'] and 'TEXCOORD_0' in p['attributes'] for p in primitives))
                self.assertTrue(doc.get('images'))
                self.assertTrue(all('bufferView' in image for image in doc['images']))
                binary_start = 28 + struct.unpack_from('<I', data, 12)[0]
                for image in doc['images']:
                    view = doc['bufferViews'][image['bufferView']]
                    start = binary_start + view.get('byteOffset', 0)
                    with Image.open(io.BytesIO(data[start:start + view['byteLength']])) as texture:
                        self.assertLessEqual(max(texture.size), 2048)
                    self.assertNotIn('uri', image)  # No absolute user paths or missing external textures.
                material = doc['materials'][0]
                self.assertIn('baseColorTexture', material['pbrMetallicRoughness'])
                channels = sources[model['id']].get('sourceChannels', ['Normal', 'Roughness', 'Metallic'])
                if 'Roughness' in channels or 'Metallic' in channels:
                    self.assertIn('metallicRoughnessTexture', material['pbrMetallicRoughness'])
                if 'Normal' in channels:
                    self.assertIn('normalTexture', material)

    def test_rigs_distinguish_supplied_animations_from_generated_locomotion(self):
        for model in MODELS:
            path = ASSETS / (model['id'] + '.glb')
            self.assertTrue(path.exists(), 'Missing converted character: ' + model['id'])
            doc, _ = glb(path)
            if model.get('animation'):
                self.assertGreaterEqual(len(doc['skins'][0]['joints']), 50)
                self.assertTrue(any(model['animation'] in animation['name'] for animation in doc.get('animations', [])))
            elif model.get('locomotionRig'):
                self.assertTrue(doc.get('skins'), 'Mobile NPCs need their own deforming skeleton: ' + model['id'])
                joints = [doc['nodes'][i]['name'] for i in doc['skins'][0]['joints']]
                self.assertTrue({'Hips', 'Head', 'LeftUpLeg', 'RightUpLeg'}.issubset(joints))
                self.assertTrue(all('JOINTS_0' in p['attributes'] and 'WEIGHTS_0' in p['attributes']
                                    for m in doc['meshes'] for p in m['primitives']))
                self.assertFalse(doc.get('animations'))  # Locomotion is runtime, never invented source clips.
                if model.get('bakeSourcePose'):
                    records = json.loads((ASSETS / 'manifest.json').read_text())
                    source = next(r for r in records if r['id'] == model['id'])
                    self.assertTrue(source.get('sourceAnimations'))
                    self.assertEqual(source['rigSource'], 'generated-basic')
            else:
                self.assertFalse(doc.get('skins'))
                self.assertFalse(doc.get('animations'))

    def test_source_record_has_hashes_and_natural_character_scale(self):
        path = ASSETS / 'manifest.json'
        self.assertTrue(path.exists(), 'Missing provenance record')
        records = json.loads(path.read_text())
        self.assertEqual({m['id'] for m in records}, {m['id'] for m in MODELS})
        for record in records:
            self.assertEqual(len(record['sha256']), 64)
            self.assertEqual(record['provenance'], 'User-provided Meshy archive; redistribution license not verified')
            self.assertAlmostEqual(record['exportHeight'], record['height'], places=2)
            self.assertLess(abs(record['floor']), 0.005)
            self.assertLess(record['texturesMaxSize'], 2049)


if __name__ == '__main__':
    unittest.main()
