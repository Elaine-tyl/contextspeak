"""Install the verified Godot engine/templates for 2D games on macOS."""
import hashlib
from pathlib import Path
import subprocess
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / ".local/world-tools"
VERSION = "4.7.2"
PACKAGES = {
    "engine": (f"Godot_v{VERSION}-stable_macos.universal.zip", "c58a24e31d720be9d62f60cb5627c4e695fb72f21b0cfe1bc9ccaa9a3b3ba63e"),
    "templates": (f"Godot_v{VERSION}-stable_export_templates.tpz", "f298490b8d44d934be425a5a65a51bf15f422428b229a06a6e11d9ffea248011"),
}


def install(kind, name, expected):
    archive = TOOLS / name
    if not archive.exists():
        temporary = archive.with_suffix(".download")
        url = f"https://github.com/godotengine/godot/releases/download/{VERSION}-stable/{name}"
        subprocess.run(["curl", "--fail", "--location", "--retry", "2", "--output", str(temporary), url], check=True)
        temporary.replace(archive)
    with archive.open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest() != expected:
            raise SystemExit("Godot archive checksum mismatch: " + str(archive))
    with ZipFile(archive) as package:
        if any(Path(item).is_absolute() or ".." in Path(item).parts for item in package.namelist()):
            raise SystemExit("Unsafe Godot archive path.")
        if kind == "engine":
            subprocess.run(["ditto", "-xk", str(archive), str(TOOLS)], check=True)
        else:
            destination = TOOLS / "templates"
            destination.mkdir(exist_ok=True)
            for item in package.infolist():
                filename = Path(item.filename).name
                if filename.startswith("web_") or filename == "version.txt":
                    (destination / filename).write_bytes(package.read(item))


if __name__ == "__main__":
    if sys.platform != "darwin":
        raise SystemExit("Install Godot 4.7.2 stable and its Web templates manually on this platform.")
    TOOLS.mkdir(parents=True, exist_ok=True)
    for kind, (name, expected) in PACKAGES.items():
        install(kind, name, expected)
    print("Verified Godot 4.7.2 engine and Web templates installed.")
