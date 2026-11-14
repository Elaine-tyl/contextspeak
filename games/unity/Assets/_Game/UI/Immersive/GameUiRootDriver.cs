using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    [DisallowMultipleComponent]
    public sealed class GameUiRootDriver : MonoBehaviour
    {
        public VisualElement SafeArea { get; private set; }
        private VisualElement root;
        private Rect lastSafe;
        private int width, height, subtitleSize;
        private float lastScale;
        private RenderTexture lastTarget;
        public void Initialize(VisualElement documentRoot, VisualElement safe)
        {
            root = documentRoot;
            SafeArea = safe;
            GameUiRoot.Owners++;
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            ApplyPreferences();
            ApplySafeArea();
        }
        private void OnEnable() => UiPreferences.Changed += ApplyPreferences;
        private void OnDisable() => UiPreferences.Changed -= ApplyPreferences;
        private void OnDestroy()
        {
            if (root == null) return;
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            GameUiRoot.Release();
        }
        private void OnGeometry(GeometryChangedEvent evt) => ApplySafeArea();
        private void Update()
        {
            if (root == null) return;
            var target = GameUiRoot.Panel.targetTexture;
            if (lastTarget != target || width != (target != null ? target.width : Screen.width) || height != (target != null ? target.height : Screen.height) || (target == null && lastSafe != Screen.safeArea) || lastScale != UiPreferences.Current.Scale) ApplySafeArea();
        }
        private void ApplyPreferences()
        {
            if (root == null) return;
            var prefs = UiPreferences.Current;
            GameUiRoot.Panel.scale = prefs.Scale;
            root.EnableInClassList("reduce-motion", prefs.ReduceMotion);
            root.EnableInClassList("colour-blind", prefs.ColourBlind);
            root.EnableInClassList("hud-off", prefs.Hud == HudMode.Off);
            root.EnableInClassList("hud-minimal", prefs.Hud == HudMode.Minimal);
            root.EnableInClassList("hud-full", prefs.Hud == HudMode.Full);
            if (subtitleSize > 0) root.RemoveFromClassList("subtitle-size-" + subtitleSize);
            subtitleSize = prefs.SubtitleSize;
            root.AddToClassList("subtitle-size-" + subtitleSize);
            ApplySafeArea();
        }
        private void ApplySafeArea()
        {
            if (SafeArea == null || root.panel == null) return;
            lastTarget = GameUiRoot.Panel.targetTexture;
            width = lastTarget != null ? lastTarget.width : Screen.width;
            height = lastTarget != null ? lastTarget.height : Screen.height;
            if (width <= 0 || height <= 0) return;
            lastSafe = lastTarget != null ? new Rect(0, 0, width, height) : Screen.safeArea;
            lastScale = UiPreferences.Current.Scale;
            // Normalized insets remain correct when a capture routes the shared panel to a
            // RenderTexture whose resolution differs from the authoring Game view.
            var panelWidth = root.layout.width;
            var panelHeight = root.layout.height;
            if (float.IsNaN(panelWidth) || float.IsNaN(panelHeight) || panelWidth <= 0 || panelHeight <= 0) return;
            SafeArea.style.left = lastSafe.xMin / width * panelWidth;
            SafeArea.style.top = (height - lastSafe.yMax) / height * panelHeight;
            SafeArea.style.right = (width - lastSafe.xMax) / width * panelWidth;
            SafeArea.style.bottom = lastSafe.yMin / height * panelHeight;
        }
    }
}
