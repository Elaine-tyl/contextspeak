using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Light, kiosk-style panels for the airport (IMGUI, 1080-pixel-high canvas from LifeUI.Begin):
    /// FlightBookingUI, FlightInformationBoard, AirportMapUI, CheckInUI, AirlineSeatSelectionUI, BaggageCheckUI,
    /// SecurityCheckpointUI, PassportControlUI, BoardingPassUI, GateInformationUI, BaggageClaimUI, TaxiBookingUI.
    /// Shops and restaurant menus reuse MenuController (AirportShopUI / RestaurantMenuUI).
    public static class AirportUI
    {
        public static GUIStyle Card, Header, HeaderText, Ink, InkBig, InkHuge, Muted, Accent, Primary, Secondary, Mono, Good, Bad, Board, BoardText, Amber;
        private static Texture2D white, blue, navy, line;
        public static readonly Color Brand = new Color(0.06f, 0.24f, 0.55f), Teal = new Color(0f, 0.62f, 0.6f);

        private static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, colour); texture.Apply(); return texture;
        }

        private static GUIStyle Text(int size, Color colour, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var result = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, richText = true, fontStyle = style, alignment = anchor };
            result.normal.textColor = colour;
            return result;
        }

        /// Change the header/button colour (blue for the airport, sea-teal for the beach).
        public static void Theme(Color brand)
        {
            if (Card == null) return;
            blue = Solid(brand);
            Header.normal.background = blue;
            Primary.normal.background = blue;
        }

        public static void Begin(out float width, out float height)
        {
            LifeUI.Begin(out width, out height);
            if (Card != null && white != null) return;
            white = Solid(new Color(0.98f, 0.98f, 0.99f)); blue = Solid(Brand); navy = Solid(new Color(0.04f, 0.07f, 0.12f)); line = Solid(new Color(0.85f, 0.87f, 0.9f));
            Card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(26, 26, 18, 18) }; Card.normal.background = white;
            Header = new GUIStyle(GUI.skin.box); Header.normal.background = blue;
            HeaderText = Text(26, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            Ink = Text(19, new Color(0.1f, 0.12f, 0.16f)); InkBig = Text(24, new Color(0.08f, 0.1f, 0.14f), FontStyle.Bold);
            InkHuge = Text(46, Brand, FontStyle.Bold); Muted = Text(15, new Color(0.42f, 0.45f, 0.5f));
            Mono = Text(18, new Color(0.1f, 0.12f, 0.16f)); Mono.font = Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Consolas", "Courier New" }, 18);
            Good = Text(18, new Color(0.1f, 0.55f, 0.3f), FontStyle.Bold); Bad = Text(18, new Color(0.8f, 0.2f, 0.15f), FontStyle.Bold);
            Accent = Text(18, Teal, FontStyle.Bold);
            Primary = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 46, fontStyle = FontStyle.Bold };
            Primary.normal.background = blue; Primary.hover.background = Solid(new Color(0.1f, 0.33f, 0.7f)); Primary.active.background = navy;
            Primary.normal.textColor = Primary.hover.textColor = Primary.active.textColor = Color.white;
            Secondary = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 38 };
            Board = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 14, 14) }; Board.normal.background = navy;
            BoardText = Text(19, new Color(0.95f, 0.95f, 0.9f)); BoardText.font = Mono.font;
            Amber = Text(19, new Color(1f, 0.78f, 0.25f), FontStyle.Bold); Amber.font = Mono.font;
        }

        /// A white card with a blue title bar; returns the content rect.
        public static Rect Window(string title, float width, float height, float screenWidth, float screenHeight)
        {
            var box = new Rect(screenWidth / 2 - width / 2, screenHeight / 2 - height / 2, width, height);
            GUI.Box(box, GUIContent.none, Card);
            GUI.Box(new Rect(box.x, box.y, box.width, 58), GUIContent.none, Header);
            GUI.Label(new Rect(box.x + 24, box.y + 4, box.width - 48, 50), "✈  " + title, HeaderText);
            return new Rect(box.x + 26, box.y + 74, box.width - 52, box.height - 92);
        }

        public static void Row(string label, string value, GUIStyle valueStyle = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Muted, GUILayout.Width(200));
            GUILayout.Label(value, valueStyle ?? Ink);
            GUILayout.EndHorizontal();
        }

        public static void Divider()
        {
            var rect = GUILayoutUtility.GetRect(10, 2, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, line);
            GUILayout.Space(6);
        }

        /// A realistic two-part boarding pass with a (decorative) barcode.
        public static void BoardingPass(Rect area, TravelBooking booking, string name, bool scanned = false)
        {
            GUI.Box(area, GUIContent.none, Card);
            GUI.Box(new Rect(area.x, area.y, area.width, 52), GUIContent.none, Header);
            GUI.Label(new Rect(area.x + 20, area.y + 4, area.width * 0.6f, 44), "SKYLARK AIR  ·  BOARDING PASS", HeaderText);
            GUI.Label(new Rect(area.x + area.width - 220, area.y + 4, 200, 44), booking.seatClass.ToUpperInvariant(), new GUIStyle(HeaderText) { alignment = TextAnchor.MiddleRight, fontSize = 18 });
            var where = booking.Where;
            float x = area.x + 24, y = area.y + 66, stub = area.x + area.width * 0.72f;
            GUI.Label(new Rect(x, y, 420, 24), "PASSENGER", Muted);
            GUI.Label(new Rect(x, y + 20, 420, 32), name.ToUpperInvariant(), InkBig);
            GUI.Label(new Rect(x, y + 64, 140, 60), "METRO", InkHuge);
            GUI.Label(new Rect(x + 160, y + 78, 60, 40), "→", InkBig);
            GUI.Label(new Rect(x + 210, y + 64, 200, 60), where.Code, InkHuge);
            GUI.Label(new Rect(x, y + 120, 420, 24), "Metro City  →  " + where.City, Ink);
            string[] labels = { "FLIGHT", "GATE", "BOARDING", "DEPARTS", "SEAT" };
            string[] values = { booking.flight, booking.gate, FlightSchedule.BoardingTime(booking), booking.departure, booking.seat == "" ? "—" : booking.seat };
            for (var i = 0; i < labels.Length; i++)
            {
                GUI.Label(new Rect(x + i * 108, y + 160, 108, 22), labels[i], Muted);
                GUI.Label(new Rect(x + i * 108, y + 180, 108, 34), values[i], InkBig);
            }
            GUI.Label(new Rect(x, y + 222, 520, 22), "Booking ref " + booking.bookingRef + "   ·   Gate closes 15 min before departure", Muted);
            // Perforation and stub.
            for (var dot = area.y + 60; dot < area.yMax - 10; dot += 14) GUI.DrawTexture(new Rect(stub - 14, dot, 2, 7), line);
            GUI.Label(new Rect(stub, y, area.xMax - stub - 16, 24), "SEAT", Muted);
            GUI.Label(new Rect(stub, y + 18, area.xMax - stub - 16, 60), booking.seat == "" ? "—" : booking.seat, InkHuge);
            GUI.Label(new Rect(stub, y + 84, area.xMax - stub - 16, 22), booking.flight + "  ·  " + booking.gate, Ink);
            var random = new System.Random(booking.bookingRef.GetHashCode());
            var bar = new Rect(stub, y + 120, area.xMax - stub - 24, 70);
            var previous = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.12f);
            for (var bx = bar.x; bx < bar.xMax; bx += 3 + random.Next(3)) GUI.DrawTexture(new Rect(bx, bar.y, 1 + random.Next(3), bar.height), Texture2D.whiteTexture);
            GUI.color = previous;
            if (scanned) GUI.Label(new Rect(stub, y + 196, 200, 26), "✔ SCANNED", Good);
        }

        /// Departures board rows (the player's flight first among fictional others).
        public static List<string[]> Departures(TravelBooking booking, string status)
        {
            var rows = new List<string[]>();
            var others = new[] { ("SL 118", "Singapore", "G1"), ("SL 405", "Bangkok", "G2"), ("NW 772", "Dubai", "G3"), ("SL 061", "Seoul", "G1"), ("AQ 330", "Auckland", "G2") };
            var hour = SkyState.Hour;
            for (var i = 0; i < others.Length; i++)
                rows.Add(new[] { FlightSchedule.Time(hour + 0.6f + i * 0.45f), others[i].Item1, others[i].Item2, others[i].Item3, i == 0 ? "BOARDING" : i == 1 ? "DELAYED" : "ON TIME" });
            if (booking.booked) rows.Insert(2, new[] { booking.departure, booking.flight, booking.Where.City, booking.gate, status });
            return rows;
        }
    }
}
