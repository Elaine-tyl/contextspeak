using System;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// Retained Toolkit controls for older location presenters. Gameplay still owns every value
    /// and command; all controls and input are provided by UI Toolkit.
    public sealed class ToolkitView : MonoBehaviour
    {
        private MonoBehaviour owner;
        private Action draw;
        private float nextRefresh;
        private VisualElement root;
        private ToolkitLayout.Canvas canvas;

        public static ToolkitView Attach(MonoBehaviour owner, Action draw)
        {
            foreach (var existing in owner.gameObject.GetComponents<ToolkitView>())
                if (existing.owner == owner) { existing.draw = draw; return existing; }
            var view = owner.gameObject.AddComponent<ToolkitView>();
            view.owner = owner;
            view.draw = draw;
            var documentRoot = GameUiRoot.Attach(owner.gameObject);
            view.root = new VisualElement { name = owner.GetType().Name + "-toolkit", pickingMode = PickingMode.Ignore };
            view.root.style.position = UnityEngine.UIElements.Position.Absolute;
            view.root.StretchToParentSize();
            documentRoot.Add(view.root);
            var sheet = Resources.Load<StyleSheet>("UI/Legacy");
            if (sheet != null) view.root.styleSheets.Add(sheet);
            view.canvas = new ToolkitLayout.Canvas(view.root);
            return view;
        }

        public bool TextInputFocused
        {
            get
            {
                var focused = root?.panel?.focusController?.focusedElement as VisualElement;
                return focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
            }
        }

        public void BringToFront() { root?.BringToFront(); }

        private void LateUpdate()
        {
            if (owner == null) { Destroy(this); return; }
            var canUse = LifeUI.CanUsePanel(owner);
            root.SetEnabled(canUse);
            if (!canUse) canvas.Suspend();
            root.style.display = owner.isActiveAndEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            if (!owner.isActiveAndEnabled) { canvas.Suspend(); return; }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            canvas.Render(draw);
        }

        private void OnDestroy() { root?.RemoveFromHierarchy(); }
    }

}
