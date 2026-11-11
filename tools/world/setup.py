"""Install pinned Godot tools locally; never change the system installation."""

import argparse
import hashlib
from pathlib import Path
import subprocess
import sys
import tarfile
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / ".local" / "world-tools"
VERSION = "4.7.2"
DOWNLOADS = {
    "engine": (
        f"Godot_v{VERSION}-stable_macos.universal.zip",
        "c58a24e31d720be9d62f60cb5627c4e695fb72f21b0cfe1bc9ccaa9a3b3ba63e",
    ),
    "templates": (
        f"Godot_v{VERSION}-stable_export_templates.tpz",
        "f298490b8d44d934be425a5a65a51bf15f422428b229a06a6e11d9ffea248011",
    ),
}

def download(url, path, expected):
    if not path.exists():
        temporary = path.with_suffix(".download")
        subprocess.run(["curl", "--fail", "--location", "--retry", "2", "--silent", "--show-error", "--output", str(temporary), url], check=True)
        temporary.replace(path)
    with path.open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest() != expected:
            raise RuntimeError(f"Checksum mismatch: remove {path} and retry")


def install_voice():
    directory = TOOLS / "whisper"
    directory.mkdir(exist_ok=True)
    archive = directory / "source.tar.gz"
    download("https://github.com/ggml-org/whisper.cpp/archive/refs/tags/v1.9.5.tar.gz", archive, "ff1a9053feb509ff9d7729703355541ae9690073a6b1c40eb692c962e0dc1720")
    download("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin", directory / "ggml-base.en.bin", "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002")
    source = directory / "whisper.cpp-1.9.5"
    if not source.exists():
        with tarfile.open(archive) as package:
            package.extractall(directory, filter="data")
    python = TOOLS / "python" / "bin" / "python"
    cmake = python.parent / "cmake"
    if not cmake.exists():
        subprocess.run([sys.executable, "-m", "venv", str(TOOLS / "python")], check=True)
        subprocess.run([str(python), "-m", "pip", "install", "cmake==4.1.2"], check=True)
    build = directory / "build"
    subprocess.run([str(cmake), "-S", str(source), "-B", str(build), "-DCMAKE_BUILD_TYPE=Release", "-DGGML_METAL=OFF", "-DBUILD_SHARED_LIBS=OFF", "-DWHISPER_BUILD_TESTS=OFF"], check=True)
    subprocess.run([str(cmake), "--build", str(build), "--target", "whisper-cli", "-j", "2"], check=True)
    print("Verified local English speech recognition ready", flush=True)


def install(kind):
    name, expected = DOWNLOADS[kind]
    archive = TOOLS / name
    if not archive.exists():
        url = f"https://github.com/godotengine/godot/releases/download/{VERSION}-stable/{name}"
        print(f"Downloading official {name}", flush=True)
        temporary = archive.with_suffix(".download")
        # System curl uses the macOS trust store; never disable TLS validation.
        subprocess.run(["curl", "--fail", "--location", "--retry", "2", "--output", str(temporary), url], check=True)
        temporary.replace(archive)
    with archive.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    if digest != expected:
        raise RuntimeError(f"Checksum mismatch: remove {archive} and retry")
    with ZipFile(archive) as package:
        if any(Path(name).is_absolute() or ".." in Path(name).parts for name in package.namelist()):
            raise RuntimeError("Unsafe archive paths")
        if kind == "engine":
            subprocess.run(["ditto", "-xk", str(archive), str(TOOLS)], check=True)
        else:
            destination = TOOLS / "templates"
            destination.mkdir(exist_ok=True)
            for entry in package.infolist():
                filename = Path(entry.filename).name
                if filename.startswith("web_") or filename in {"macos.zip", "version.txt"}:
                    (destination / filename).write_bytes(package.read(entry))
            print("Installed templates:", ", ".join(p.name for p in destination.iterdir()))
    print(f"Verified and installed {kind}", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("parts", nargs="*", choices=[*DOWNLOADS, "voice"], default=["engine", "templates", "voice"])
    args = parser.parse_args()
    if sys.platform != "darwin":
        raise SystemExit("This bootstrap targets macOS; see games/cafe/README.md for other platforms.")
    TOOLS.mkdir(parents=True, exist_ok=True)
    for part in args.parts:
        if part == "voice":
            install_voice()
        else:
            install(part)
