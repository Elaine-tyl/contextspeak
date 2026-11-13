"""Prepare verified free art and compile/test the pinned Unity cafe project."""
import argparse
import os
import plistlib
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "games/unity"
DEFAULT_EDITOR = Path("/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity")
sys.path.insert(0, str(ROOT / "tools/world"))
from verify_assets import verify


def prepare():
    verify()
    prepare_microphone()
    # In a clean checkout, establish URP before the first glTF material import.
    if not (PROJECT / "Assets/Generated/CafePipeline.asset").exists():
        run("setup")
    destination = PROJECT / "Assets/Art"
    destination.mkdir(parents=True, exist_ok=True)
    for folder in ["mini-characters", "quaternius", "furniture", "food", "kaykit"]:
        shutil.copytree(ROOT / "games/cafe/assets" / folder, destination / folder, dirs_exist_ok=True, ignore=shutil.ignore_patterns("*.import", "*.uid"))


def prepare_microphone():
    source = Path(__file__).with_name("mac-microphone.mm")
    output = PROJECT / "Assets/Plugins/macOS/libContextSpeakMic.dylib"
    if output.exists() and output.stat().st_mtime >= max(source.stat().st_mtime, Path(__file__).stat().st_mtime):
        return
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["xcrun", "clang++", "-dynamiclib", "-fobjc-arc", "-arch", "arm64", "-arch", "x86_64", "-mmacosx-version-min=12.0",
                    "-framework", "AVFoundation", "-framework", "Foundation", "-install_name", "@rpath/libContextSpeakMic.dylib",
                    str(source), "-o", str(output)], check=True)


def run(mode):
    editor = Path(os.environ.get("CONTEXTSPEAK_UNITY_EDITOR", str(DEFAULT_EDITOR)))
    if not editor.is_file():
        raise SystemExit("Install/activate Unity 6000.3.28f1 with Web and macOS support, or set CONTEXTSPEAK_UNITY_EDITOR to its executable.")
    try:
        with (editor.parent.parent / "Info.plist").open("rb") as metadata:
            version = plistlib.load(metadata).get("CFBundleVersion")
    except (OSError, plistlib.InvalidFileException) as error:
        raise SystemExit("Cannot verify Unity 6000.3.28f1. Use the pinned macOS Editor executable.") from error
    if version != "6000.3.28f1":
        raise SystemExit("Use Unity 6000.3.28f1 LTS; the selected Editor is " + str(version))
    logs = ROOT / ".local/unity-logs"
    logs.mkdir(parents=True, exist_ok=True)
    log = logs / (mode + ".log")
    method = {
        "ui-test": "ContextSpeak.Editor.CafeNativeUiChecks.Begin",
        "interaction-test": "ContextSpeak.Editor.SliceInteractionChecks.Begin",
        "setup": "ContextSpeak.Editor.CafeBuild.ConfigurePipeline",
        "test": "ContextSpeak.Editor.CafeBuild.Verify",
        "web": "ContextSpeak.Editor.CafeBuild.BuildWeb",
        "arcade": "ContextSpeak.Editor.CafeBuild.BuildArcade",
        "mac": "ContextSpeak.Editor.CafeBuild.BuildMac",
    }[mode]
    arguments = [str(editor), "-batchmode", "-nographics", "-projectPath", str(PROJECT), "-executeMethod", method, "-logFile", str(log)]
    if mode in {"web", "arcade"}:
        arguments += ["-buildTarget", "WebGL"]
    elif mode == "mac":
        arguments += ["-buildTarget", "OSXUniversal"]
    play_check = mode in {"ui-test", "interaction-test"}
    if play_check:
        arguments += ["-contextspeak-ui-test" if mode == "ui-test" else "-contextspeak-interaction-test"]
    else:
        arguments += ["-quit"]
    result = subprocess.run(arguments, timeout=180 if play_check else 1200)
    if result.returncode:
        raise SystemExit("Unity failed; inspect the local log at " + str(log))
    # Unity can succeed while replacing a failed platform shader with magenta.
    if log.is_file() and "Shader error in '" in log.read_text(errors="replace"):
        raise SystemExit("Shader compilation failed; inspect the local log at " + str(log))
    print("Unity " + mode + " finished. Log: " + str(log))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["prepare", "test", "ui-test", "interaction-test", "web", "mac", "arcade"])
    args = parser.parse_args()
    prepare()
    if args.mode != "prepare":
        run(args.mode)
    if args.mode == "test":
        run("ui-test")
        run("interaction-test")
    if args.mode == "mac":
        archive = ROOT / ".local/world-builds/web/unity/ContextSpeak-Cafe-macOS.zip"
        archive.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(["ditto", "-c", "-k", "--keepParent", str(ROOT / ".local/unity-builds/ContextSpeak Cafe.app"), str(archive)], check=True)
        print("Desktop download: " + str(archive))
