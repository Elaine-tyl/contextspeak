"""Source migration checks; native control behaviour is checked by LegacyToolkitChecks."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'games/unity/Assets'
RUNTIME = ASSETS / 'ContextSpeak/Runtime'

class LegacyToolkitMigration(unittest.TestCase):
    def test_no_player_surface_relies_on_imgui_events(self):
        remaining = [str(p.relative_to(ROOT)) for p in ASSETS.rglob('*.cs') if 'Editor' not in p.relative_to(ASSETS).parts and re.search(r'void\s+OnGUI\s*\(', p.read_text())]
        self.assertEqual([], remaining, 'Player UI must be rendered by UI Toolkit')

    def test_compatibility_controls_are_native_toolkit_controls(self):
        adapter = RUNTIME / 'UI/ToolkitLayout.cs'
        self.assertTrue(adapter.exists(), 'Toolkit control adapter has not been implemented')
        source = adapter.read_text()
        for control in ('new Button(', 'new TextField(', 'new Toggle(', 'new Slider(', 'new ScrollView('):
            self.assertIn(control, source)
        self.assertNotIn('IMGUIContainer', source)
        self.assertNotIn('Event.current', source)

    def test_nested_panels_have_owned_modal_and_shared_back_dispatch(self):
        source = (RUNTIME / 'UI/LifeUI.cs').read_text()
        self.assertIn('LegacyModalStack', source)
        self.assertNotIn('interaction.Modal = Talking', source)
        for name in ('Commerce/MenuController.cs', 'Commerce/PaymentController.cs', 'Airport/AirportFlowBase.cs'):
            view = (RUNTIME / name).read_text()
            self.assertIn('SetPanelOpen(this,', view, name)
            self.assertIn('LifeUI.RequestBack()', view, name)

    def test_typed_rehearsal_keeps_existing_speech_entry_point(self):
        source = (RUNTIME / 'UI/LifeUI.cs').read_text()
        self.assertIn('SubmitTyped(typed)', source)
        self.assertNotIn('Machine.Submit', source)

if __name__ == '__main__':
    unittest.main()
