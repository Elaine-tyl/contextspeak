using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    public enum ToastKind { Info, Success, Error }

    // The two elements live for the presenter's entire lifetime; excess notices wait in FIFO order.
    public sealed class ToastPresenter : IDisposable
    {
        private readonly VisualElement container;
        private readonly Label[] labels = new Label[2];
        private readonly float[] until = new float[2];
        private readonly ToastBuffer queue = new ToastBuffer();
        private readonly IVisualElementScheduledItem timer;
        public ToastPresenter(VisualElement root)
        {
            container = new VisualElement { name = "toast-stack", pickingMode = PickingMode.Ignore };
            container.AddToClassList("toast-stack");
            root.Add(container);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i] = new Label { pickingMode = PickingMode.Ignore, enableRichText = false };
                labels[i].AddToClassList("toast");
                labels[i].style.display = DisplayStyle.None;
                container.Add(labels[i]);
            }
            timer = container.schedule.Execute(Tick).Every(50);
        }
        public void Enqueue(string message, ToastKind kind = ToastKind.Info, float seconds = 3.5f)
        {
            queue.Enqueue(message, (int)kind, seconds);
            Tick();
        }
        private void Tick()
        {
            var now = Time.unscaledTime;
            for (var i = 0; i < labels.Length; i++)
            {
                if (until[i] > now) continue;
                if (queue.Count == 0)
                {
                    if (labels[i].style.display != DisplayStyle.None) labels[i].style.display = DisplayStyle.None;
                    continue;
                }
                var item = queue.Dequeue();
                var label = labels[i];
                label.text = item.Kind == (int)ToastKind.Success ? "✓  " + item.Text : item.Kind == (int)ToastKind.Error ? "!  " + item.Text : item.Text;
                label.EnableInClassList("toast-success", item.Kind == (int)ToastKind.Success);
                label.EnableInClassList("toast-error", item.Kind == (int)ToastKind.Error);
                label.style.display = DisplayStyle.Flex;
                until[i] = now + item.Seconds;
            }
        }
        public void Dispose()
        {
            timer.Pause();
            container.RemoveFromHierarchy();
        }
    }
}
