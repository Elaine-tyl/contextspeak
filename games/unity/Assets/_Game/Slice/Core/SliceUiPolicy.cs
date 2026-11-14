using System;
using System.Collections.Generic;

namespace ContextSpeak.Slice
{
    // Navigation is independent of gameplay: closing an app never discards a conversation or save.
    public static class SliceUiPolicy
    {
        public static string Parent(string page)
        {
            switch (page)
            {
                case "phone": case "": return "";
                case "speaking": case "places": return "map";
                case "scenery": case "school": return "places";
                default: return "phone";
            }
        }
    }

    public sealed class ContextNeeds
    {
        private bool initialized;
        private float energy, hunger, thirst;
        private double until;
        public bool Visible(float nextEnergy, float nextHunger, float nextThirst, double now)
        {
            var changed = initialized && (Math.Abs(nextEnergy - energy) > .04f || Math.Abs(nextHunger - hunger) > .04f || Math.Abs(nextThirst - thirst) > .04f);
            if (changed) until = now + 5;
            if (!initialized || changed)
            {
                energy = nextEnergy; hunger = nextHunger; thirst = nextThirst;
            }
            initialized = true;
            return nextEnergy < .25f || nextHunger > .75f || nextThirst > .75f || now < until;
        }
    }

    public struct MapLabelBox
    {
        public float X, Y, Width, Height;
        public bool Visible;
        public bool Overlaps(MapLabelBox other) => Visible && other.Visible && X < other.X + other.Width + 3
            && X + Width + 3 > other.X && Y < other.Y + other.Height + 3 && Y + Height + 3 > other.Y;
    }

    public sealed class MapLabelLayout
    {
        private readonly List<MapLabelBox> placed = new List<MapLabelBox>(24);
        private float width, height;
        public MapLabelLayout(float width, float height) { Reset(width, height); }
        public void Reset(float width, float height) { this.width = width; this.height = height; placed.Clear(); }
        public MapLabelBox Place(float x, float y, float labelWidth, float labelHeight)
        {
            var box = new MapLabelBox { Width = Math.Min(width, labelWidth), Height = Math.Min(height, labelHeight), Visible = true };
            for (var step = 0; step < 12; step++)
            {
                box.X = Math.Max(0, Math.Min(width - box.Width, x + (step % 2 == 0 ? 8 : -box.Width - 8)));
                box.Y = Math.Max(0, Math.Min(height - box.Height, y + (step / 2) * (box.Height + 4)));
                var overlaps = false;
                for (var i = 0; i < placed.Count; i++) if (box.Overlaps(placed[i])) { overlaps = true; break; }
                if (overlaps) continue;
                placed.Add(box); return box;
            }
            box.Visible = false; // An omitted label is preferable to unreadable overlapping text.
            return box;
        }
    }
}
