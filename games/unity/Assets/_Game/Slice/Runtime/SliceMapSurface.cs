using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Slice
{
    // The image is a real top-down camera; pins/labels are pooled and positioned over the same bounds.
    public sealed class SliceMapSurface : VisualElement
    {
        private sealed class Pin { public VisualElement Dot; public Label Name; public string Target; }
        private readonly List<Pin> pins = new List<Pin>(32);
        private readonly MapLabelLayout labels = new MapLabelLayout(400, 400);
        private readonly WorldMapCapture capture;
        private readonly Image image;
        private readonly bool expanded;
        private readonly Action<string> visit;
        private Vector2 player, target;
        private bool hasTarget;
        public SliceMapSurface(WorldMapCapture capture, bool expanded, Action<string> visit)
        {
            this.capture = capture; this.expanded = expanded; this.visit = visit;
            AddToClassList(expanded ? "phone-map-surface" : "minimap-surface");
            image = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
            image.style.position = Position.Absolute; image.style.left = image.style.right = image.style.top = image.style.bottom = 0;
            Add(image); generateVisualContent += DrawRoute;
        }
        private static Vector2 Point(float x, float z) => new Vector2(Mathf.InverseLerp(-66, 66, x), 1 - Mathf.InverseLerp(-60, 72, z));
        public void Refresh(SliceView view)
        {
            if (capture != null) image.image = capture.MapTexture;
            var markers = view.Markers ?? Array.Empty<Vector4>();
            while (pins.Count < markers.Length + 1)
            {
                var pin = new Pin { Dot = new VisualElement(), Name = new Label { enableRichText = false } };
                pin.Dot.AddToClassList("map-pin"); pin.Name.AddToClassList("map-pin-label");
                pin.Dot.RegisterCallback<ClickEvent>(_ => { if (expanded && !string.IsNullOrEmpty(pin.Target)) visit?.Invoke(pin.Target); });
                Add(pin.Dot); Add(pin.Name); pins.Add(pin);
            }
            var width = float.IsNaN(layout.width) || layout.width < 1 ? (expanded ? 450 : 156) : layout.width;
            var height = float.IsNaN(layout.height) || layout.height < 1 ? width : layout.height;
            labels.Reset(width, height); hasTarget = false;
            for (var i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                var visible = i <= markers.Length;
                pin.Dot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                pin.Name.style.display = DisplayStyle.None;
                if (!visible) continue;
                var isPlayer = i == markers.Length;
                var marker = isPlayer ? new Vector4(view.PlayerX, view.PlayerZ, -1, 0) : markers[i];
                var point = Point(marker.x, marker.y);
                pin.Dot.EnableInClassList("player", isPlayer);
                pin.Dot.EnableInClassList("target", !isPlayer && marker.w > 0);
                pin.Dot.style.left = Length.Percent(point.x * 100); pin.Dot.style.top = Length.Percent(point.y * 100);
                pin.Target = isPlayer ? "" : marker.z == 0 ? "counter" : marker.z == 1 ? "pier" : marker.z == 2 ? "pier" : "";
                if (isPlayer) player = point;
                else if (marker.w > 0) { target = point; hasTarget = true; }
                var name = isPlayer ? "You" : marker.z == 0 ? "Rowan" : marker.z == 1 ? "Hazel" : marker.z == 2 ? "Pier" : "";
                if (!expanded || name == "") continue;
                var box = labels.Place(point.x * width, point.y * height, name.Length * 9 + 12, 23);
                pin.Name.text = name; pin.Name.style.display = box.Visible ? DisplayStyle.Flex : DisplayStyle.None;
                pin.Name.style.left = box.X; pin.Name.style.top = box.Y;
            }
            MarkDirtyRepaint();
        }
        private void DrawRoute(MeshGenerationContext context)
        {
            if (!expanded || !hasTarget) return;
            var painter = context.painter2D; painter.strokeColor = new Color(.18f, .37f, .27f); painter.lineWidth = 3;
            painter.BeginPath(); painter.MoveTo(new Vector2(player.x * layout.width, player.y * layout.height));
            painter.LineTo(new Vector2(target.x * layout.width, target.y * layout.height)); painter.Stroke();
        }
    }
}
