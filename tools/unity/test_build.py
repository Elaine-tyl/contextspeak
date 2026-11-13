"""Build-tool regressions require no installed Editor or third-party Python package."""
import importlib.util
from pathlib import Path
import plistlib
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("unity_build", Path(__file__).with_name("build.py"))
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)


class EditorVersionTest(unittest.TestCase):
    def test_shader_error_fails_even_when_unity_reports_success(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            contents = root / "Unity.app/Contents"
            editor = contents / "MacOS/Unity"
            editor.parent.mkdir(parents=True)
            editor.touch()
            with (contents / "Info.plist").open("wb") as file:
                plistlib.dump({"CFBundleVersion": "6000.3.28f1"}, file)

            def launch(arguments, **_):
                Path(arguments[arguments.index("-logFile") + 1]).write_text(
                    "Shader error in 'ContextSpeak/Forest Water': undeclared identifier 'Pow5' (on gles3)\n")
                return SimpleNamespace(returncode=0)

            with patch.dict("os.environ", {"CONTEXTSPEAK_UNITY_EDITOR": str(editor)}), patch.object(build, "ROOT", root), patch.object(build.subprocess, "run", side_effect=launch):
                with self.assertRaisesRegex(SystemExit, "Shader compilation failed"):
                    build.run("web")

    def test_runtime_interaction_check_waits_for_play_mode_without_quit(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            contents = root / "Unity.app/Contents"
            editor = contents / "MacOS/Unity"
            editor.parent.mkdir(parents=True)
            editor.touch()
            with (contents / "Info.plist").open("wb") as file:
                plistlib.dump({"CFBundleVersion": "6000.3.28f1"}, file)
            with patch.dict("os.environ", {"CONTEXTSPEAK_UNITY_EDITOR": str(editor)}), patch.object(build, "ROOT", root), patch.object(build.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as launch:
                build.run("interaction-test")
            arguments = launch.call_args.args[0]
            self.assertIn("ContextSpeak.Editor.SliceInteractionChecks.Begin", arguments)
            self.assertIn("-contextspeak-interaction-test", arguments)
            self.assertNotIn("-quit", arguments)

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
