using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    // A pooled UI Toolkit label projected next to the real world anchor, never the screen centre.
    public sealed class WorldPrompt : MonoBehaviour
    {
        private static readonly TutorialHints Hints = new TutorialHints();
        private VisualElement root;
        private Label label;
        private Transform anchor;
        private Camera view;
        private string actionId, description, key;
        private bool tutorial;
        public static WorldPrompt Attach(GameObject owner, VisualElement root)
        {
            var prompt = owner.GetComponent<WorldPrompt>();
            if (prompt == null) prompt = owner.AddComponent<WorldPrompt>();
            prompt.root = root;
            if (prompt.label == null)
            {
                prompt.label = new Label { name = "world-prompt", pickingMode = PickingMode.Ignore, enableRichText = false };
                prompt.label.AddToClassList("world-prompt");
                prompt.label.style.display = DisplayStyle.None;
                root.Add(prompt.label);
            }
            return prompt;
        }
        public void Show(Transform target, string action, string text, string inputKey = "E")
        {
            if (target == null || string.IsNullOrWhiteSpace(text)) { Hide(); return; }
            var changed = anchor != target || actionId != action;
            if (changed) tutorial = Hints.BecameAvailable(action);
            if (changed || description != text || key != inputKey)
            {
                description = text; key = inputKey;
                label.text = tutorial && !string.IsNullOrEmpty(inputKey) ? inputKey + " · " + text : text;
            }
            anchor = target; actionId = action;
            LateUpdate();
        }
        public void Hide()
        {
            anchor = null; actionId = null;
            if (label != null) label.style.display = DisplayStyle.None;
        }
        private void OnDisable() => Hide();
        private void OnDestroy() => label?.RemoveFromHierarchy();
        private void LateUpdate()
        {
            if (anchor == null || root?.panel == null) return;
            if (float.IsNaN(root.layout.width) || float.IsNaN(root.layout.height) || root.layout.width <= 0 || root.layout.height <= 0) return;
            if (view == null) view = Camera.main;
            if (view == null) return;
            var point = anchor.position + Vector3.up * 1.35f;
            if (view.WorldToViewportPoint(point).z <= 0) { label.style.display = DisplayStyle.None; return; }
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, point, view);
            var local = root.WorldToLocal(panelPoint);
            var size = label.layout.size;
            var w = float.IsNaN(size.x) || size.x < 1 ? 260 : size.x;
            var h = float.IsNaN(size.y) || size.y < 1 ? 42 : size.y;
            label.style.left = UiPolicy.ClampPosition(local.x - w * 0.5f, w, root.layout.width, 16);
            label.style.top = UiPolicy.ClampPosition(local.y + 12, h, root.layout.height, 16);
            label.style.display = DisplayStyle.Flex;
        }
    }
}
