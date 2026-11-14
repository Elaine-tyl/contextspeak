using System;
using ContextSpeak.Slice;
class Program
{
    static int count;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); count++; }
    static void Main()
    {
        Check(SliceUiPolicy.Parent("speaking") == "map", "Map tab backs into the Map app.");
        Check(SliceUiPolicy.Parent("places") == "map", "Life scenes backs into Map.");
        Check(SliceUiPolicy.Parent("map") == "phone", "Map backs to phone home.");
        Check(SliceUiPolicy.Parent("pause") == "phone", "Settings backs to phone home.");
        Check(SliceUiPolicy.Parent("inventory") == "phone", "Bag backs to phone home.");
        Check(SliceUiPolicy.Parent("phone") == "", "Home backs to the world.");
        Check(SliceUiPolicy.Parent("scenery") == "places", "Scenery picker preserves its parent.");
        var needs = new ContextNeeds();
        Check(!needs.Visible(.9f, .1f, .1f, 0), "Healthy unchanged needs are hidden.");
        Check(needs.Visible(.65f, .1f, .1f, 1), "A meaningful change reveals needs.");
        Check(!needs.Visible(.65f, .1f, .1f, 10), "Stable healthy needs fade away.");
        Check(needs.Visible(.15f, .1f, .1f, 11), "Low energy stays visible.");
        Check(needs.Visible(.15f, .1f, .1f, 100), "Critical needs never time out.");
        Check(needs.Visible(.8f, .9f, .1f, 110), "High hunger is contextual.");
        var labels = new MapLabelLayout(150, 150);
        var first = labels.Place(50, 50, 45, 16);
        var second = labels.Place(50, 50, 45, 16);
        Check(!first.Overlaps(second), "Nearby map labels do not overlap.");
        var edge = labels.Place(149, 149, 45, 16);
        Check(edge.X >= 0 && edge.Y >= 0 && edge.X + edge.Width <= 150 && edge.Y + edge.Height <= 150, "Map labels stay on the map.");
        Console.WriteLine("Immersive UI policy checks passed: " + count);
    }
}
