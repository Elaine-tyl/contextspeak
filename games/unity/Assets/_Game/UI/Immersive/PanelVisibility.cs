using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    // One reusable timer per surface: no new elements or timers during snapshot updates.
    public static class PanelVisibility
    {
        private sealed class Fade
        {
            private readonly VisualElement element;
            private readonly IVisualElementScheduledItem timer;
            private bool visible, initialized;

            public Fade(VisualElement element)
            {
                this.element = element;
                element.AddToClassList("fade-surface");
                timer = element.schedule.Execute(Complete);
                timer.Pause();
            }
            public void Set(bool next)
            {
                if (initialized && visible == next) return;
                var first = !initialized;
                initialized = true;
                visible = next;
                timer.Pause();
                if (UiPreferences.Current.ReduceMotion || first && !next)
                {
                    element.style.opacity = next ? 1 : 0;
                    element.style.display = next ? DisplayStyle.Flex : DisplayStyle.None;
                    return;
                }
                if (next)
                {
                    element.style.display = DisplayStyle.Flex;
                    element.style.opacity = 0;
                    timer.ExecuteLater(20);
                }
                else
                {
                    element.style.opacity = 0;
                    timer.ExecuteLater(180);
                }
            }
            private void Complete()
            {
                if (visible) element.style.opacity = 1;
                else element.style.display = DisplayStyle.None;
            }
        }
        private static readonly ConditionalWeakTable<VisualElement, Fade> surfaces = new ConditionalWeakTable<VisualElement, Fade>();
        public static void Set(VisualElement element, bool visible)
        {
            if (!surfaces.TryGetValue(element, out var fade))
            {
                fade = new Fade(element);
                surfaces.Add(element, fade);
            }
            fade.Set(visible);
        }
    }
}
