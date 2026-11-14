using System;
using System.Collections.Generic;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    public readonly struct ToolkitOption
    {
        internal readonly int Kind;
        internal readonly float Value;
        internal ToolkitOption(int kind, float value) { Kind = kind; Value = value; }
    }

    public static class ToolkitLayout
    {
        internal sealed class Slot
        {
            internal VisualElement Element;
            internal string Identity = "";
            internal bool Clicked, Changed, Submit;
            internal object Value;
        }

        /// Pools children by parent and ordinal. Hidden branches lose pending commands so an old
        /// click cannot buy, pay, or advance a different panel after navigation.
        public sealed class Canvas
        {
            internal readonly VisualElement Root;
            internal readonly Dictionary<VisualElement, List<Slot>> pools = new Dictionary<VisualElement, List<Slot>>();
            internal readonly Dictionary<VisualElement, int> used = new Dictionary<VisualElement, int>();
            private readonly List<VisualElement> retired = new List<VisualElement>();
            private readonly Stack<VisualElement> stack = new Stack<VisualElement>();
            internal VisualElement Parent => stack.Count == 0 ? Root : stack.Peek();
            internal float Width => Root.resolvedStyle.width > 0 ? Root.resolvedStyle.width : 1920;
            internal float Height => Root.resolvedStyle.height > 0 ? Root.resolvedStyle.height : 1080;
            public Canvas(VisualElement root) { Root = root; }
            public void Suspend()
            {
                foreach (var list in pools.Values)
                    foreach (var slot in list) slot.Clicked = slot.Changed = slot.Submit = false;
            }
            public void Render(Action draw)
            {
                var previous = current;
                current = this;
                used.Clear(); stack.Clear();
                ToolkitGui.enabled = true; ToolkitGui.color = Color.white;
                try { draw(); }
                finally
                {
                    foreach (var pair in pools)
                    {
                        used.TryGetValue(pair.Key, out var count);
                        for (var i = count; i < pair.Value.Count; i++)
                        {
                            var slot = pair.Value[i];
                            slot.Element.style.display = DisplayStyle.None;
                            slot.Clicked = slot.Changed = slot.Submit = false;
                        }
                    }
                    current = previous;
                }
            }
            internal Slot Take<T>(Func<T> create, string identity = "") where T : VisualElement
            {
                var parent = Parent;
                if (!pools.TryGetValue(parent, out var list)) pools[parent] = list = new List<Slot>();
                used.TryGetValue(parent, out var index); used[parent] = index + 1;
                Slot slot;
                if (index == list.Count) { slot = new Slot { Element = create() }; list.Add(slot); parent.Add(slot.Element); }
                else
                {
                    slot = list[index];
                    if (slot.Element.GetType() != typeof(T))
                    {
                        retired.Clear();
                        foreach (var key in pools.Keys)
                            if (key == slot.Element || slot.Element.Contains(key)) retired.Add(key);
                        foreach (var key in retired) { pools.Remove(key); used.Remove(key); }
                        slot.Element.RemoveFromHierarchy();
                        slot = new Slot { Element = create() }; list[index] = slot;
                        parent.Insert(Mathf.Min(index, parent.childCount), slot.Element);
                    }
                }
                if (slot.Identity != identity) { slot.Clicked = slot.Changed = slot.Submit = false; slot.Value = null; slot.Identity = identity; }
                slot.Element.style.display = DisplayStyle.Flex;
                slot.Element.SetEnabled(ToolkitGui.enabled);
                return slot;
            }
            internal void Push(VisualElement element) { stack.Push(element); }
            internal void Pop() { if (stack.Count > 0) stack.Pop(); }
        }

        private static Canvas current;
        internal static float CanvasWidth => current?.Width ?? 1920;
        internal static float CanvasHeight => current?.Height ?? 1080;
        public static ToolkitOption Width(float value) => new ToolkitOption(0, value);
        public static ToolkitOption Height(float value) => new ToolkitOption(1, value);
        public static ToolkitOption ExpandWidth(bool value) => new ToolkitOption(2, value ? 1 : 0);

        private static void Options(VisualElement element, ToolkitOption[] options)
        {
            element.style.width = StyleKeyword.Auto;
            element.style.height = StyleKeyword.Auto;
            element.style.flexGrow = 0;
            element.style.flexShrink = 1;
            foreach (var option in options)
            {
                if (option.Kind == 0) element.style.width = option.Value;
                if (option.Kind == 1) element.style.height = option.Value;
                if (option.Kind == 2) element.style.flexGrow = option.Value;
            }
        }
        internal static void Appearance(VisualElement element, GUIStyle appearance, bool panel = false)
        {
            element.EnableInClassList("legacy-paper", panel && appearance != null);
            if (appearance == null) return;
            element.style.fontSize = Mathf.Clamp(appearance.fontSize, 13, 32);
            element.style.unityFontStyleAndWeight = appearance.fontStyle;
            element.style.whiteSpace = WhiteSpace.Normal;
            if (element is TextElement text) text.enableRichText = true;
        }
        private static VisualElement Group(bool horizontal, GUIStyle appearance, ToolkitOption[] options)
        {
            var element = current.Take(() => new VisualElement { pickingMode = PickingMode.Ignore }).Element;
            element.style.position = UnityEngine.UIElements.Position.Relative;
            element.style.flexDirection = horizontal ? FlexDirection.Row : FlexDirection.Column;
            element.style.flexWrap = horizontal ? Wrap.Wrap : Wrap.NoWrap;
            Options(element, options); Appearance(element, appearance, appearance != null);
            current.Push(element);
            return element;
        }
        public static void BeginHorizontal(params ToolkitOption[] options) => Group(true, null, options);
        public static void BeginHorizontal(GUIStyle style, params ToolkitOption[] options) => Group(true, style, options);
        public static void EndHorizontal() => current.Pop();
        public static void BeginVertical(params ToolkitOption[] options) => Group(false, null, options);
        public static void BeginVertical(GUIStyle style, params ToolkitOption[] options) => Group(false, style, options);
        public static void EndVertical() => current.Pop();
        public static void BeginArea(Rect area, GUIStyle style = null)
        {
            // An area is scrollable at larger accessibility scales; its parent never blocks the world.
            var slot = current.Take(() => new ScrollView(ScrollViewMode.Vertical));
            var scroll = (ScrollView)slot.Element;
            scroll.AddToClassList("legacy-area");
            scroll.RemoveFromClassList("legacy-subtitle");
            Position(scroll, area); Appearance(scroll, style, style != null);
            current.Push(scroll.contentContainer);
        }
        public static void BeginSubtitleArea(Rect area)
        {
            BeginArea(area);
            current.Parent.GetFirstAncestorOfType<ScrollView>()?.AddToClassList("legacy-subtitle");
        }
        public static void EndArea() => current.Pop();
        public static Vector2 BeginScrollView(Vector2 position, params ToolkitOption[] options)
        {
            var scroll = (ScrollView)current.Take(() => new ScrollView(ScrollViewMode.Vertical)).Element;
            scroll.style.position = UnityEngine.UIElements.Position.Relative;
            Options(scroll, options); scroll.style.flexGrow = 1; scroll.style.minHeight = 80;
            current.Push(scroll.contentContainer);
            return scroll.scrollOffset;
        }
        public static void EndScrollView() => current.Pop();
        public static void Subtitle(string value)
        {
            Label(value);
            var list = current.pools[current.Parent];
            var slot = list[current.used[current.Parent] - 1];
            slot.Element.style.fontSize = UiPreferences.Current.SubtitleSize;
            slot.Element.AddToClassList("legacy-spoken-line");
        }
        public static void Label(string value, params ToolkitOption[] options) => Label(value, null, options);
        public static void Label(string value, GUIStyle style, params ToolkitOption[] options)
        {
            var label = (Label)current.Take(() => new Label()).Element;
            label.style.position = UnityEngine.UIElements.Position.Relative;
            label.text = value; label.pickingMode = PickingMode.Ignore;
            Options(label, options); Appearance(label, style);
        }
        public static bool Button(string value, params ToolkitOption[] options) => Button(value, null, options);
        public static bool Button(string value, GUIStyle style, params ToolkitOption[] options)
        {
            Slot slot = null;
            slot = current.Take(() =>
            {
                var button = new Button();
                // Capture the retained slot once; subsequent renders preserve this callback.
                button.clicked += () => { if (slot != null && button.enabledInHierarchy) slot.Clicked = true; };
                return button;
            }, value);
            var control = (Button)slot.Element;
            control.text = value; Options(control, options);
            var clicked = slot.Clicked && ToolkitGui.enabled && control.enabledInHierarchy; slot.Clicked = false;
            return clicked;
        }
        public static bool Toggle(bool value, string text, GUIStyle style = null, params ToolkitOption[] options)
        {
            Slot slot = null;
            slot = current.Take(() =>
            {
                var toggle = new Toggle();
                toggle.RegisterValueChangedCallback(e => { if (slot != null) { slot.Changed = true; slot.Value = e.newValue; } });
                return toggle;
            }, text);
            if (slot.Changed) { value = (bool)slot.Value; slot.Changed = false; }
            var control = (Toggle)slot.Element;
            control.label = text; control.SetValueWithoutNotify(value); Options(control, options);
            return value;
        }
        public static string TextField(string value, int maxLength, params ToolkitOption[] options)
        {
            Slot slot = null;
            slot = current.Take(() =>
            {
                var field = new TextField();
                field.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                    if (slot != null) slot.Submit = true;
                    e.StopPropagation();
                });
                field.RegisterValueChangedCallback(e => { if (slot != null) { slot.Changed = true; slot.Value = e.newValue; } });
                return field;
            });
            if (slot.Changed) { value = (string)slot.Value; slot.Changed = false; }
            var control = (TextField)slot.Element;
            control.maxLength = maxLength; control.SetValueWithoutNotify(value); Options(control, options); lastField = slot;
            return value;
        }
        public static bool ConsumeTextSubmit()
        {
            if (lastField == null) return false;
            var submit = lastField.Submit; lastField.Submit = false; return submit;
        }
        private static Slot lastField;
        private sealed class Embedded : VisualElement { }
        public static void Embed(string name, Action<VisualElement> build)
        {
            var slot = current.Take(() => new Embedded { name = name }, name);
            if (slot.Element.childCount == 0) build(slot.Element);
        }
        public static float HorizontalSlider(float value, float min, float max, params ToolkitOption[] options)
        {
            Slot slot = null;
            slot = current.Take(() =>
            {
                var slider = new Slider();
                slider.RegisterValueChangedCallback(e => { if (slot != null) { slot.Changed = true; slot.Value = e.newValue; } });
                return slider;
            });
            if (slot.Changed) { value = (float)slot.Value; slot.Changed = false; }
            var control = (Slider)slot.Element;
            control.lowValue = min; control.highValue = max; control.SetValueWithoutNotify(value); Options(control, options); control.style.flexGrow = 1;
            return value;
        }
        public static int Toolbar(int selected, string[] choices, GUIStyle style = null) => SelectionGrid(selected, choices, choices.Length, style);
        public static int SelectionGrid(int selected, string[] choices, int columns, params ToolkitOption[] options) => SelectionGrid(selected, choices, columns, null, options);
        public static int SelectionGrid(int selected, string[] choices, int columns, GUIStyle style, params ToolkitOption[] options)
        {
            BeginVertical(options);
            for (var i = 0; i < choices.Length; i++)
            {
                if (i % Mathf.Max(1, columns) == 0) BeginHorizontal();
                if (Button((selected == i ? "✓ " : "") + choices[i], style)) selected = i;
                if (i % Mathf.Max(1, columns) == columns - 1 || i == choices.Length - 1) EndHorizontal();
            }
            EndVertical(); return selected;
        }
        public static void Space(float size)
        {
            var element = current.Take(() => new VisualElement { pickingMode = PickingMode.Ignore }).Element;
            element.style.minHeight = size; element.style.minWidth = size;
        }
        public static void FlexibleSpace()
        {
            var element = current.Take(() => new VisualElement { pickingMode = PickingMode.Ignore }).Element;
            element.style.flexGrow = 1;
        }
        internal static void Position(VisualElement element, Rect rect)
        {
            element.style.position = UnityEngine.UIElements.Position.Absolute;
            element.style.left = rect.x; element.style.top = rect.y;
            element.style.width = Mathf.Max(0, rect.width); element.style.height = Mathf.Max(0, rect.height);
        }
        internal static void Fixed(Rect rect, string value, GUIStyle style, bool panel)
        {
            var label = (Label)current.Take(() => new Label { pickingMode = PickingMode.Ignore }).Element;
            label.text = value; Position(label, rect); Appearance(label, style, panel);
        }
        public static Rect Reserve(float width, float height, params ToolkitOption[] options)
        {
            var element = current.Take(() => new VisualElement { pickingMode = PickingMode.Ignore }).Element;
            Options(element, options);
            element.style.minWidth = width; element.style.minHeight = height;
            var rect = element.layout;
            return new Rect(float.IsNaN(rect.x) ? 0 : rect.x, float.IsNaN(rect.y) ? 0 : rect.y, width, height);
        }
        internal static void Texture(Rect rect, Texture texture)
        {
            var element = current.Take(() => new VisualElement { pickingMode = PickingMode.Ignore }).Element;
            Position(element, rect); element.style.backgroundColor = ToolkitGui.color;
            // Existing one-pixel textures are solid material swatches, never gameplay images.
        }
    }

    public static class ToolkitGui
    {
        public sealed class Styles
        {
            public readonly GUIStyle box = new GUIStyle(), label = new GUIStyle(), button = new GUIStyle();
        }
        public static readonly Styles skin = new Styles();
        public static Color color = Color.white;
        public static bool enabled = true;
        public static void Box(Rect rect, GUIContent value, GUIStyle style = null) => ToolkitLayout.Fixed(rect, value?.text ?? "", style, true);
        public static void Box(Rect rect, string value, GUIStyle style = null) => ToolkitLayout.Fixed(rect, value, style, true);
        public static void Label(Rect rect, string value, GUIStyle style = null) => ToolkitLayout.Fixed(rect, value, style, false);
        public static void DrawTexture(Rect rect, Texture texture) => ToolkitLayout.Texture(rect, texture);
    }
    public static class ToolkitLayoutUtility
    {
        public static Rect GetRect(float width, float height, params ToolkitOption[] options) => ToolkitLayout.Reserve(width, height, options);
    }
    public static class ToolkitScreen
    {
        public static float width => ToolkitLayout.CanvasWidth;
        public static float height => ToolkitLayout.CanvasHeight;
    }
}
