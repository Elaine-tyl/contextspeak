"""Build both targets from the same Godot project, or run headless mission checks."""

import argparse
from pathlib import Path
import subprocess
import shutil
import re
import tempfile
from contextlib import contextmanager
from verify_assets import verify

ROOT = Path(__file__).resolve().parents[2]
ENGINE = ROOT / ".local/world-tools/Godot.app/Contents/MacOS/Godot"
PROJECT = ROOT / "games/cafe"
OUTPUT = ROOT / ".local/world-builds"
TEST_SCRIPTS = [
    "storage_isolation_test.gd", "world_2d_test.gd", "mission_test.gd", "interface_test.gd",
    "web_viewport_test.gd", "cozy_world_test.gd", "forest_garden_test.gd",
    "friend_test.gd", "placement_test.gd", "avatar_test.gd", "stages_test.gd",
    "stage_runtime_test.gd", "stage_session_test.gd", "roleplay_test.gd",
    "challenge_state_test.gd", "challenge_runtime_test.gd", "cafe_roleplay_world_test.gd",
    "scene_runtime_test.gd", "npc_interaction_test.gd",
    "roleplay_placement_test.gd", "roleplay_exploration_test.gd",
]


def godot(*arguments, project=PROJECT):
    if not ENGINE.exists():
        raise SystemExit("Run pnpm world:setup first (or use the export presets in Godot 4.7.2).")
    result = subprocess.run([str(ENGINE), "--headless", "--path", str(project), *arguments], capture_output=True, text=True, timeout=180)
    print(result.stdout)
    if result.returncode or "SCRIPT ERROR" in result.stderr or "ERROR:" in result.stderr:
        raise SystemExit(result.stderr or "Godot command failed")
    return result.stdout


@contextmanager
def isolated_project():
    # Reuse imported resources, but give every test run its own native user://.
    # A finally block also removes the isolated data after a failure or timeout.
    with tempfile.TemporaryDirectory(prefix="contextspeak-world-test-") as directory:
        project = Path(directory)
        name = "ContextSpeak-Test-" + project.name.removeprefix("contextspeak-world-test-")
        for source in PROJECT.iterdir():
            if source.name != "project.godot":
                (project / source.name).symlink_to(source, target_is_directory=source.is_dir())
        settings = (PROJECT / "project.godot").read_text()
        settings, count = re.subn(r"^config/name=.*$", f'config/name="{name}"', settings, count=1, flags=re.MULTILINE)
        if count != 1:
            raise SystemExit("Could not isolate the test project name")
        (project / "project.godot").write_text(settings)
        marker = "CONTEXTSPEAK_TEST_DATA="
        (project / "test_user_data.gd").write_text('extends SceneTree\nfunc _initialize():\n\tprint("' + marker + '" + OS.get_user_data_dir())\n\tquit(0)\n')
        probe = godot("--script", "test_user_data.gd", project=project)
        location = next((line[len(marker):] for line in probe.splitlines() if line.startswith(marker)), "")
        data = Path(location).resolve()
        if not location or data.name != name:
            raise SystemExit("Refusing to run tests against shared user storage")
        try:
            yield project
        finally:
            shutil.rmtree(data, ignore_errors=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--test", action="store_true")
    parser.add_argument("--script", choices=TEST_SCRIPTS, help="Run one isolated regression script with --test")
    args = parser.parse_args()
    verify()
    if args.test:
        with isolated_project() as project:
            for script in [args.script] if args.script else TEST_SCRIPTS:
                godot("--script", "tests/" + script, project=project)
    else:
        if args.script:
            parser.error("--script requires --test")
        (OUTPUT / "web").mkdir(parents=True, exist_ok=True)
        godot("--editor", "--import", "--quit")
        godot("--export-release", "Web")
        page = OUTPUT / "web/index.html"
        for source in (PROJECT / "web").glob("*.js"):
            shutil.copyfile(source, OUTPUT / "web" / source.name)
        bridge = '''<script src="microphone.js"></script>
<script src="voice.js"></script>
<script>
window.contextSpeak2D = new URLSearchParams(location.search).get("view") === "2d";
const contextSpeakSaveSuffix = window.contextSpeak2D ? "-2d-v1" : "-v1";
if (window.contextSpeak2D) document.title = "ContextSpeak — 2D Cafe";
window.contextSpeakSaveGuest = function (value) {
  try { localStorage.setItem("contextspeak-cafe" + contextSpeakSaveSuffix, value); return true; }
  catch { return false; }
};
window.contextSpeakLoadGuest = function () {
  try { return localStorage.getItem("contextspeak-cafe" + contextSpeakSaveSuffix); }
  catch { return null; }
};
window.contextSpeakSaveScene = function (value) {
  try { localStorage.setItem("contextspeak-scenes" + contextSpeakSaveSuffix, value); return true; }
  catch { return false; }
};
window.contextSpeakLoadScene = function () {
  try { return localStorage.getItem("contextspeak-scenes" + contextSpeakSaveSuffix); }
  catch { return null; }
};
</script>
<p id="world-status" role="status" aria-live="polite" style="position:absolute;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0;pointer-events:none">Loading ContextSpeak Cafe…</p>'''
        page.write_text(page.read_text().replace("</body>", bridge + "</body>"))
        godot("--export-release", "macOS")
        print("Web and macOS builds ready in", OUTPUT)
