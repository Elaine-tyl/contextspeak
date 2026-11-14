using System;
using ContextSpeak.Cafe.UI;

static class Program
{
    static void Equal(object expected, object actual, string message)
    {
        if (!expected.Equals(actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    static void Main()
    {
        Equal(0.8f, UiPolicy.Scale(-1f), "UI scale must not collapse controls");
        Equal(1.4f, UiPolicy.Scale(9f), "UI scale must retain room for phone navigation");
        Equal(1f, UiPolicy.Scale(float.NaN), "Malformed saved scale must recover");
        Equal(18, UiPolicy.SubtitleSize(2), "Subtitles must remain legible");
        Equal(32, UiPolicy.SubtitleSize(99), "Oversized subtitles must retain viewport room");
        Equal(16f, UiPolicy.ClampPosition(-100f, 260f, 1280f, 16f), "Left prompt edge must remain inside safe area");
        Equal(1004f, UiPolicy.ClampPosition(1260f, 260f, 1280f, 16f), "Right prompt edge must remain inside safe area");
        Equal(0f, UiPolicy.ClampPosition(100f, 400f, 100f, 16f), "Oversized prompt must not produce a negative position");
        var hints = new TutorialHints();
        Equal(true, hints.BecameAvailable("order"), "First interaction teaches the key");
        Equal(true, hints.BecameAvailable("order"), "Second interaction teaches the key");
        Equal(true, hints.BecameAvailable("order"), "Third interaction teaches the key");
        Equal(false, hints.BecameAvailable("order"), "Fourth interaction must not repeat tutorial");
        Equal(true, hints.BecameAvailable("talk"), "Tutorial count is independent per action");
        var queue = new ToastBuffer();
        queue.Enqueue("one", 0, 2f);
        queue.Enqueue("two", 1, 3f);
        Equal("one", queue.Dequeue().Text, "Toasts must preserve order");
        Equal("two", queue.Dequeue().Text, "Later queued toasts follow earlier toasts");
        Equal(0, queue.Count, "Draining toasts releases queue entries");
        for (int i = 0; i < 40; i++) queue.Enqueue("toast " + i, 0, 3f);
        Equal(32, queue.Count, "Long sessions must not grow the toast queue without a bound");
        Equal("toast 8", queue.Dequeue().Text, "Overflow discards the oldest stale toast");
        Console.WriteLine("Immersive UI policy checks passed (18 assertions).");
    }
}
