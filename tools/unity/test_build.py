"""Build-tool regressions require no installed Editor or third-party Python package."""
import importlib.util
from pathlib import Path
import plistlib
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("unity_build", Path(__file__).with_name("build.py"))
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)


class EditorVersionTest(unittest.TestCase):
    def test_wrong_editor_is_rejected_before_launching_or_modifying_project(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            contents = root / "Unity.app/Contents"
            editor = contents / "MacOS/Unity"
            editor.parent.mkdir(parents=True)
            editor.write_text("#!/bin/sh\ntouch '" + str(root / "launched") + "'\n")
            editor.chmod(0o700)
            with (contents / "Info.plist").open("wb") as file:
                plistlib.dump({"CFBundleVersion": "6000.6.8f1"}, file)
            with patch.dict("os.environ", {"CONTEXTSPEAK_UNITY_EDITOR": str(editor)}), patch.object(build, "ROOT", root):
                with self.assertRaisesRegex(SystemExit, "6000.3.28f1"):
                    build.run("test")
            self.assertFalse((root / "launched").exists())


if __name__ == "__main__":
    unittest.main()
