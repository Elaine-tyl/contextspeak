"""Test/export the pinned Godot 2D board; the website owns audio and scoring."""
from pathlib import Path
import shutil
import subprocess
import sys
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "games/arcade"
ENGINE = ROOT / ".local/world-tools/Godot.app/Contents/MacOS/Godot"


def run(*arguments):
    subprocess.run([str(ENGINE), "--headless", "--path", str(PROJECT), *arguments], check=True, timeout=180)


if __name__ == "__main__":
    if not ENGINE.is_file():
        raise SystemExit("Run pnpm arcade:setup to install pinned Godot tools first.")
    version = subprocess.check_output([str(ENGINE), "--version"], text=True).strip()
    if not version.startswith("4.7.2.stable."):
        raise SystemExit("Use the pinned Godot 4.7.2 stable engine.")
    run("--editor", "--import", "--quit")
    run("--script", "tests/board_test.gd")
    if "--test" not in sys.argv:
        output = ROOT / "apps/web/public/arcade-engine"
        # Preserve an older Unity preview instead of mixing two engines' generated files.
        if output.exists() and (output / "Build").exists():
            backup = ROOT / ".local/migration-backups" / ("unity-arcade-" + uuid4().hex)
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(output), backup)
        output.mkdir(parents=True, exist_ok=True)
        run("--editor", "--export-release", "Web", str(output / "index.html"))
        index = output / "index.html"
        html = index.read_text().replace("<head>", '<head><script src="bridge.js"></script>', 1)
        html = html.replace("</body>", '<output id="arcade-status" hidden></output></body>')
        index.write_text(html)
        shutil.copyfile(PROJECT / "web/bridge.js", output / "bridge.js")
        print("Godot 2D export ready: " + str(output))
