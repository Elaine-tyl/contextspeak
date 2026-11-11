using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Arcade
{
    // Original lightweight vector illustrations; no external textures or answer text.
    public sealed class ArcadePicture : VisualElement
    {
        private readonly string kind;
        public ArcadePicture(string picture)
        {
            kind = picture;
            style.height = 100;
            style.minWidth = 180;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var center = contentRect.width / 2;
            painter.lineWidth = 5;
            painter.strokeColor = new Color32(32, 91, 76, 255);
            painter.fillColor = new Color32(226, 144, 54, 255);
            if (kind == "umbrella")
            {
                Polygon(painter, new[] { new Vector2(center - 55, 48), new Vector2(center - 35, 15), new Vector2(center, 2), new Vector2(center + 35, 15), new Vector2(center + 55, 48) });
                Line(painter, new Vector2(center, 45), new Vector2(center, 88));
                Line(painter, new Vector2(center, 88), new Vector2(center - 15, 88));
            }
            else if (kind == "book")
            {
                Polygon(painter, new[] { new Vector2(center - 55, 10), new Vector2(center, 20), new Vector2(center + 55, 10), new Vector2(center + 55, 78), new Vector2(center, 88), new Vector2(center - 55, 78) });
                Line(painter, new Vector2(center, 20), new Vector2(center, 88));
            }
            else if (kind == "bridge")
            {
                Line(painter, new Vector2(center - 70, 68), new Vector2(center + 70, 68));
                Line(painter, new Vector2(center - 70, 25), new Vector2(center + 70, 25));
                foreach (var x in new[] { -60, -30, 0, 30, 60 }) Line(painter, new Vector2(center + x, 25), new Vector2(center + x, 78));
                painter.strokeColor = new Color32(49, 86, 206, 255);
                Line(painter, new Vector2(center - 75, 90), new Vector2(center + 75, 90));
            }
            else
            {
                Circle(painter, new Vector2(center - 44, 65), 26);
                Circle(painter, new Vector2(center + 44, 65), 26);
                Line(painter, new Vector2(center - 44, 65), new Vector2(center - 10, 25));
                Line(painter, new Vector2(center - 10, 25), new Vector2(center + 10, 65));
                Line(painter, new Vector2(center + 10, 65), new Vector2(center - 44, 65));
                Line(painter, new Vector2(center - 10, 25), new Vector2(center + 28, 25));
                Line(painter, new Vector2(center + 28, 25), new Vector2(center + 44, 65));
                Line(painter, new Vector2(center + 28, 25), new Vector2(center + 25, 10));
                Line(painter, new Vector2(center + 25, 10), new Vector2(center + 40, 10));
            }
        }
        private static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }
        private static void Polygon(Painter2D painter, Vector2[] points)
        {
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (var index = 1; index < points.Length; index++) painter.LineTo(points[index]);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }
        private static void Circle(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.MoveTo(center + Vector2.right * radius);
            for (var index = 1; index <= 24; index++)
            {
                var angle = index * Mathf.PI * 2 / 24;
                painter.LineTo(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            painter.Stroke();
        }
    }
}
