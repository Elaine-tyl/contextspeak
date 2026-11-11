"""Verify vendored free art offline against pinned creator or Git blob hashes."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "games/cafe/assets"


def verify():
    entries = json.loads((ROOT / "sources.json").read_text())
    for entry in entries:
        data = (ROOT / entry["path"]).read_bytes()
        if "sha256" in entry:
            digest = hashlib.sha256(data).hexdigest()
            expected = entry["sha256"]
        else:
            digest = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
            expected = entry["gitSha"]
        if digest != expected:
            raise SystemExit(f"Asset changed: {entry['path']}. Review its provenance before building.")
    print(f"Verified {len(entries)} pinned CC0 asset/license files")


if __name__ == "__main__":
    verify()
