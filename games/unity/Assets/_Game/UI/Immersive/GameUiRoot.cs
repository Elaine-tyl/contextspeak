using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    public static class GameUiRoot
    {
        private static PanelSettings sharedPanel;
        internal static int Owners;
        internal static PanelSettings Panel
        {
            get
            {
                if (sharedPanel != null) return sharedPanel;
                sharedPanel = ScriptableObject.CreateInstance<PanelSettings>();
                sharedPanel.name = "ContextSpeak shared UI · 1920×1080";
                sharedPanel.hideFlags = HideFlags.DontSave;
                sharedPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                sharedPanel.referenceResolution = new Vector2Int(1920, 1080);
                sharedPanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                sharedPanel.match = 0.5f;
                sharedPanel.scale = UiPreferences.Current.Scale;
                sharedPanel.textSettings = UiFonts.Settings;
                sharedPanel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/SharedTheme");
                return sharedPanel;
            }
        }
        public static VisualElement Attach(GameObject owner)
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("ContextSpeak UI input");
                events.AddComponent<EventSystem>();
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
                if (Application.isPlaying) Object.DontDestroyOnLoad(events);
            }
            var driver = owner.GetComponent<GameUiRootDriver>();
            if (driver != null && driver.SafeArea != null) return driver.SafeArea;
            var document = owner.GetComponent<UIDocument>();
            if (document == null) document = owner.AddComponent<UIDocument>();
            document.panelSettings = Panel;
            var root = document.rootVisualElement;
            root.name = "contextspeak-root";
            root.AddToClassList("game-ui");
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            var theme = Resources.Load<StyleSheet>("UI/Theme");
            if (theme != null && !root.styleSheets.Contains(theme)) root.styleSheets.Add(theme);
            var sans = Resources.Load<Font>("UI/SourceSans3");
            root.style.unityFont = sans != null ? sans : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var safe = new VisualElement { name = "safe-area", pickingMode = PickingMode.Ignore };
            safe.style.position = Position.Absolute;
            root.Add(safe);
            if (driver == null) driver = owner.AddComponent<GameUiRootDriver>();
            driver.Initialize(root, safe);
            return safe;
        }
        internal static void Release()
        {
            Owners--;
            if (Owners > 0 || sharedPanel == null) return;
            if (Application.isPlaying) Object.Destroy(sharedPanel);
            else Object.DestroyImmediate(sharedPanel);
            sharedPanel = null;
        }
    }

}
