"""Install only the reviewed models in models.json; verify every download."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import argostranslate.package as package
import requests

root = Path(__file__).resolve().parent
cache = Path(os.environ["XDG_CACHE_HOME"]) / "model-downloads"
cache.mkdir(parents=True, exist_ok=True)
installed = {(p.from_code, p.to_code, str(p.package_version)) for p in package.get_installed_packages()}

for model in json.loads((root / "models.json").read_text()):
    path = cache / model["url"].rsplit("/", 1)[-1]
    if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != model["sha256"]:
        print("Downloading", path.name, flush=True)
        partial = path.with_suffix(".part")
        with requests.get(model["url"], stream=True, timeout=(10, 180)) as response:
            response.raise_for_status()
            with partial.open("wb") as output:
                for chunk in response.iter_content(1024 * 1024):
                    output.write(chunk)
        if hashlib.sha256(partial.read_bytes()).hexdigest() != model["sha256"]:
            partial.unlink()
            raise RuntimeError("Model checksum mismatch: " + path.name)
        partial.replace(path)
    if model.get("kind") == "sentence":
        destination = Path(os.environ["XDG_DATA_HOME"]) / "argos-translate" / "minisbd" / model["filename"]
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists() or hashlib.sha256(destination.read_bytes()).hexdigest() != model["sha256"]:
            shutil.copyfile(path, destination)
    elif (model["from"], model["to"], model["version"]) not in installed:
        print("Installing", path.name, flush=True)
        package.install_from_path(path)
print("Pinned English/Chinese models ready.", flush=True)
