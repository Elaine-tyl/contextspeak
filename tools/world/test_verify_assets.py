"""Offline checks for pinned creator archives and legacy Git-mirrored assets."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import verify_assets


class AssetIntegrityTests(unittest.TestCase):
    def test_creator_and_git_hashes_and_tamper_rejection(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            data = b"original free art"
            (root / "model.bin").write_bytes(data)
            entries = [
                {"path": "model.bin", "sha256": hashlib.sha256(data).hexdigest()},
                {"path": "model.bin", "gitSha": hashlib.sha1(b"blob 17\0" + data).hexdigest()},
            ]
            (root / "sources.json").write_text(json.dumps(entries))
            with patch.object(verify_assets, "ROOT", root):
                verify_assets.verify()
                (root / "model.bin").write_bytes(b"unreviewed replacement")
                with self.assertRaisesRegex(SystemExit, "Asset changed"):
                    verify_assets.verify()


if __name__ == "__main__":
    unittest.main()
