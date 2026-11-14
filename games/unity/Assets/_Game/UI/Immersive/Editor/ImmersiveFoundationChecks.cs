using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;

namespace ContextSpeak.Editor
{
    public static class ImmersiveFoundationChecks
    {
        [MenuItem("ContextSpeak/UI/Check Shared Foundation")]
        public static void Run()
        {
            GameObject first = null, second = null;
            var originalEvents = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            var prefs = Cafe.UI.UiPreferences.Current;
            var previousScale = prefs.Scale;
            const string key = "ContextSpeak.UI.v1.scale";
            var existed = PlayerPrefs.HasKey(key);
            var savedScale = PlayerPrefs.GetFloat(key);
            var changed = 0;
            Action onChanged = () => changed++;
            Cafe.UI.UiPreferences.Changed += onChanged;
            try
            {
                Require(Resources.Load<Texture2D>("UI/Paper") != null, "Paper must import as a 2D UI texture, not a cubemap.");
                var fonts = Cafe.UI.UiFonts.Settings.fallbackFontAssets;
                Require(fonts.Count > 0 && fonts[0].HasCharacter('中', false, true)
                    && fonts[0].HasCharacter('咖', false, true), "Bundled Chinese glyphs must be available without OS fonts.");
                first = new GameObject("Temporary shared UI check A");
                second = new GameObject("Temporary shared UI check B");
                var a = Cafe.UI.GameUiRoot.Attach(first);
                var b = Cafe.UI.GameUiRoot.Attach(second);
                Require(first.GetComponent<UIDocument>().panelSettings == second.GetComponent<UIDocument>().panelSettings, "Documents must share a single PanelSettings");
                Require(ReferenceEquals(a, Cafe.UI.GameUiRoot.Attach(first)), "Repeated Attach must not duplicate a document or safe area");
                Require(a.pickingMode == PickingMode.Ignore && b.pickingMode == PickingMode.Ignore, "Empty safe area must not intercept world input");
                var panel = first.GetComponent<UIDocument>().panelSettings;
                Require(panel.referenceResolution == new Vector2Int(1920, 1080), "Panel must use the design reference resolution");
                prefs.Scale = 1.1f;
                changed = 0;
                prefs.Scale = float.NaN;
                Require(prefs.Scale == 1f && PlayerPrefs.GetFloat(key) == 1f && changed == 1, "Malformed UI scale must recover, persist and notify once");
                prefs.Scale = 1f;
                Require(changed == 1, "Unchanged settings must not dispatch redundant rebuilds");
                Cafe.UI.UiPreferences.AddControls(a);
                Require(a.Q<Slider>() != null && a.Q<SliderInt>() != null && a.Q<DropdownField>() != null && a.Query<Toggle>().ToList().Count == 2, "Accessibility controls must expose every setting");
                Debug.Log("Immersive shared foundation checks passed.");
            }
            finally
            {
                Cafe.UI.UiPreferences.Changed -= onChanged;
                prefs.Scale = previousScale;
                if (existed) PlayerPrefs.SetFloat(key, savedScale); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                if (first != null) UnityEngine.Object.DestroyImmediate(first);
                if (second != null) UnityEngine.Object.DestroyImmediate(second);
                if (originalEvents == null)
                {
                    var temporaryEvents = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
                    if (temporaryEvents != null) UnityEngine.Object.DestroyImmediate(temporaryEvents.gameObject);
                }
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
