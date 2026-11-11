"""Install pinned local speech recognition for Unity; no Godot runtime is needed."""
import hashlib
from pathlib import Path
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / ".local/world-tools"

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


if __name__ == "__main__":
    TOOLS.mkdir(parents=True, exist_ok=True)
    install_voice()
