"""Source emissive/alpha channels must survive the actual game export."""
import json
from pathlib import Path
import struct
import unittest
from PIL import Image

ART = Path(__file__).resolve().parents[2] / 'games/unity/Assets/_Game/Art'

class WorldMaterialTests(unittest.TestCase):
    def test_authored_emission_and_cutouts_survive_export(self):
        for item in json.loads((ART / 'WorldAssets/catalog.json').read_text()):
            if item['usage'] != 'world':
                continue
            data = (ART / 'WorldAssets' / (item['id'] + '.glb')).read_bytes()
            size = struct.unpack_from('<I', data, 12)[0]
            materials = json.loads(data[20:20 + size])['materials']
            if item['textures'].get('emission'):
                with self.subTest(emission=item['id']):
                    self.assertTrue(any(mat.get('emissiveTexture') for mat in materials))
            if item['textures'].get('baseColor'):
                with Image.open(ART / 'MeshyLibrary' / item['textures']['baseColor']) as image:
                    if image.mode == 'RGBA' and image.getextrema()[3][0] < 128:
                        with self.subTest(alpha=item['id']):
                            self.assertTrue(any(mat.get('alphaMode', 'OPAQUE') != 'OPAQUE' for mat in materials))

if __name__ == '__main__':
    unittest.main()
