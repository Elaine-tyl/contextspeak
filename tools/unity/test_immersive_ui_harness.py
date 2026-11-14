"""Source contract checks; native layout/render proof lives in ImmersiveUiChecks.

These cannot establish that Unity compiles or that screenshots look correct.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'games/unity/Assets'

class ImmersiveCaptureContracts(unittest.TestCase):
    def test_capture_is_explicit_play_mode_and_never_saves_scenes(self):
        path = ASSETS / 'Editor/ImmersiveUiCapture.cs'
        self.assertTrue(path.exists(), 'Play-mode UI screenshot harness is missing')
        source = path.read_text()
        self.assertIn('ContextSpeak/UI/Capture UI Screenshots', source)
        self.assertIn('EditorApplication.isPlaying = true', source)
        self.assertIn('scene.isDirty', source)
        self.assertIn('CaptureScreenshotAsTexture', source)
        self.assertNotIn('SaveScene(', source)
        self.assertNotIn('NewScene(', source)
        self.assertNotIn('OpenScene(', source)

    def test_fixture_and_live_evidence_are_distinct(self):
        path = ASSETS / 'Editor/ImmersiveUiCapture.cs'
        self.assertTrue(path.exists(), 'Play-mode UI screenshot harness is missing')
        source = path.read_text()
        self.assertIn('LIVE HUD', source)
        self.assertIn('DETERMINISTIC UI FIXTURE', source)
        for screen in ('map', 'journal', 'inventory', 'pause', 'friends', 'dialogue', 'correction', 'menu'):
            self.assertIn('"' + screen + '"', source)
        self.assertIn('1920, 1080', source)
        self.assertIn('1280, 720', source)
        self.assertIn('1920, 1200', source)
        self.assertIn('2520, 1080', source)

    def test_auxiliary_ui_has_contextual_actions_and_practice_disclaimer(self):
        for relative in ('_Game/UI/LifeInterface.cs', '_Game/UI/CafeInterface.cs'):
            source = (ASSETS / relative).read_text()
            self.assertIn('Foldout', source, relative + ' needs collapsed secondary actions')
            self.assertIn('practice-note', source, relative + ' needs visible practice disclaimer')
            self.assertNotIn('controls.AddToClassList("controls")', source, relative + ' still has permanent controls bar')

if __name__ == '__main__':
    unittest.main()
