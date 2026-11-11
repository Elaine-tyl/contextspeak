"""Compatibility launcher for the unified Unity project."""
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
mode = "test" if "--test" in sys.argv else "web"
raise SystemExit(subprocess.call([sys.executable, str(ROOT / "tools/unity/build.py"), mode]))
