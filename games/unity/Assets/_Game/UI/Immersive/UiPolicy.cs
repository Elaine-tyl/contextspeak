using System;
using System.Collections.Generic;

namespace ContextSpeak.Cafe.UI
{
    // Portable layout/accessibility rules, shared by runtime and source-only checks.
    public static class UiPolicy
    {
        public static float Scale(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0.8f, Math.Min(1.4f, value));
        public static int SubtitleSize(int value) => Math.Max(18, Math.Min(32, value));
        public static float ClampPosition(float position, float size, float viewport, float margin)
        {
            var maximum = viewport - size - margin;
            return maximum < margin ? 0f : Math.Max(margin, Math.Min(maximum, position));
        }
    }

    public sealed class TutorialHints
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        public bool BecameAvailable(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            counts.TryGetValue(action, out var count);
            if (count >= 3) return false;
            counts[action] = count + 1;
            return true;
        }
    }

    public struct ToastMessage
    {
        public string Text;
        public int Kind;
        public float Seconds;
    }

    public sealed class ToastBuffer
    {
        private readonly ToastMessage[] entries = new ToastMessage[32];
        private int head;
        public int Count { get; private set; }
        public void Enqueue(string text, int kind, float seconds)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Count == entries.Length) { head = (head + 1) % entries.Length; Count--; }
            entries[(head + Count) % entries.Length] = new ToastMessage { Text = text, Kind = kind, Seconds = Math.Max(1f, seconds) };
            Count++;
        }
        public ToastMessage Dequeue()
        {
            if (Count == 0) return default;
            var result = entries[head];
            entries[head] = default;
            head = (head + 1) % entries.Length;
            Count--;
            return result;
        }
    }
}
