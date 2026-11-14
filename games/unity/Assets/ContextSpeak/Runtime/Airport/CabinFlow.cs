using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// The aircraft: find your seat → stow your carry-on → sit → fasten seatbelt (B) → safety
    /// announcement → take-off (engines, ground falls away) → cabin service with the flight attendant
    /// → flight transition (map, time skip) → landing → leave the aircraft for the arrival airport.
    public sealed class CabinFlow : AirportFlowBase
    {
        public enum Phase { Boarding, Seated, Taxi, Takeoff, Cruise, Service, Transition, Descent, Landed }
        public Phase State { get; private set; } = Phase.Boarding;
        [Header("Scene wiring")]
        public Transform Spawn;
        public Transform Outside;                 // ground/cloud deck below the windows; moved down during climb
        public Renderer[] SeatbeltSigns = new Renderer[0];
        public Light[] CabinLights = new Light[0];
        public Transform Galley;
        public float ClimbSeconds = 25, CruiseHeight = -1800, TransitionSeconds = 8;
        private float timer, outsideStart;
        private bool demo;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        protected override void Start()
        {
            base.Start();
            MapBounds = new Rect(-6, -22, 12, 44);
            if (!Booking.booked || Booking.stage < (int)TravelStage.Boarded)
            {
                // Opened directly in the Editor: a labelled demo booking so the cabin can be tested on its own.
                demo = true;
                FlightSchedule.Book(Booking, Destinations.Find("mel"), SkyState.Hour, 7);
                Booking.seat = "14A";
                Booking.boardingPass = true;
                Booking.stage = (int)TravelStage.Boarded;
                LifeUI.Toast("DEMO BOOKING — the cabin was opened without boarding (SL to Melbourne, seat 14A).");
            }
            if (Spawn != null) Player.Place(Spawn.position, Spawn.forward);
            if (Outside != null) outsideStart = Outside.position.y;
            foreach (var seat in FindObjectsByType<SeatController>(FindObjectsSortMode.None).Where(seat => seat.Group.StartsWith("seat-")))
            {
                var id = seat.Group.Substring(5);
                seat.Prompt = "Sit in " + id;
                seat.Allowed = () => id == Booking.seat && Booking.carryOnStowed;
                seat.CanStand = false;
                seat.Seated += () => { if (Booking.stage == (int)TravelStage.Boarded) { Advance(TravelStage.Seated); State = Phase.Seated; LifeUI.Toast("Press B to fasten your seatbelt."); } };
            }
            Objective = "Find seat " + Booking.seat + " — stow your bag in the overhead locker first.";
            Seatbelt(false);
            AirportAudio.Instance?.Engines(0.1f);
        }

        protected override void Update()
        {
            base.Update();
            if (State == Phase.Seated && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame && !Booking.seatbelt)
            {
                Booking.seatbelt = true;
                LifeUI.Toast("Seatbelt fastened ✔");
                State = Phase.Taxi;
                timer = 10;
                Seatbelt(true);
                Announce("Good afternoon, this is your captain. Cabin crew, please prepare for departure. Please make sure your seatbelt is fastened, your seat is upright and your bag is stowed.");
                AirportAudio.Instance?.Engines(0.3f);
            }
            timer -= Time.deltaTime;
            switch (State)
            {
                case Phase.Taxi when timer <= 0:
                    State = Phase.Takeoff;
                    timer = ClimbSeconds;
                    Announce("Cabin crew, seats for take-off.");
                    AirportAudio.Instance?.Engines(1f);
                    Dim(0.35f);
                    break;
                case Phase.Takeoff:
                    var k = 1 - Mathf.Clamp01(timer / ClimbSeconds);
                    if (Outside != null) Outside.position = new Vector3(Outside.position.x, Mathf.Lerp(outsideStart, outsideStart + CruiseHeight, k * k), Outside.position.z - Time.deltaTime * Mathf.Lerp(5, 80, k));
                    if (timer <= 0)
                    {
                        State = Phase.Cruise;
                        timer = 6;
                        Seatbelt(false);
                        Dim(1);
                        AirportAudio.Instance?.Engines(0.5f);
                        Announce("We've reached our cruising altitude. The seatbelt sign is now off. Our cabin crew will be coming through the cabin with drinks and a light meal.");
                    }
                    break;
                case Phase.Cruise when timer <= 0:
                    State = Phase.Service;
                    var crew = Npc("crew");
                    var seat = InteractionManager.Instance != null ? InteractionManager.Instance.Seat : null;
                    if (crew != null && seat != null) crew.GoTo(seat.transform.position - seat.transform.right * 1.1f, () => Service(crew));
                    else Service(crew);
                    break;
                case Phase.Transition:
                    if (Outside != null) Outside.position += Vector3.back * Time.deltaTime * 120;
                    if (timer <= 0)
                    {
                        State = Phase.Descent;
                        timer = ClimbSeconds;
                        Hide();
                        Seatbelt(true);
                        SkySystem.Active?.SetTime(SkyState.Hour + Booking.Where.FlightHours, true);
                        Announce("Ladies and gentlemen, we've started our descent into " + Booking.Where.City + ". Please return to your seat and fasten your seatbelt.");
                        AirportAudio.Instance?.Engines(0.35f);
                    }
                    break;
                case Phase.Descent:
                    var d = Mathf.Clamp01(timer / ClimbSeconds);
                    if (Outside != null) Outside.position = new Vector3(Outside.position.x, Mathf.Lerp(outsideStart, outsideStart + CruiseHeight, d * d), Outside.position.z - Time.deltaTime * Mathf.Lerp(5, 70, d));
                    if (timer <= 0)
                    {
                        State = Phase.Landed;
                        Seatbelt(false);
                        AirportAudio.Instance?.Engines(0.1f);
                        Advance(TravelStage.Flown);
                        Announce(Booking.Where.Greeting + "! Welcome to " + Booking.Where.Airport + ". The local time is " + SkyState.Clock + ". Please take all your belongings with you.");
                        var seatNow = InteractionManager.Instance != null ? InteractionManager.Instance.Seat : null;
                        if (seatNow != null) seatNow.CanStand = true;
                        Objective = "Stand up, take your bag and leave by the front door.";
                    }
                    break;
            }
        }

        private void Service(NPCController crew)
        {
            Speak("Flight Attendant Service", crew, () =>
            {
                Advance(TravelStage.SpokeToCrew);
                InventoryManager.Add("blanket", "Airline blanket (to return)");
                crew?.ReturnHome();
                State = Phase.Transition;
                timer = TransitionSeconds;
                Show("flightmap");
            });
        }

        private void Seatbelt(bool on)
        {
            foreach (var sign in SeatbeltSigns)
            {
                if (sign == null) continue;
                var block = new MaterialPropertyBlock();
                sign.GetPropertyBlock(block);
                block.SetColor(Emission, on ? new Color(1f, 0.75f, 0.2f) * 3 : Color.black);
                sign.SetPropertyBlock(block);
            }
        }

        private void Dim(float level) { foreach (var light in CabinLights) if (light != null) light.intensity = 1.2f * level; }

        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            switch (npc.Key)
            {
                case "crew" when State == Phase.Boarding: Speak("Flight Attendant Boarding", npc, null); return;
                case "crew" when State == Phase.Service: return;
                case "crew": LifeUI.Toast(npc.DisplayName + ": " + (State == Phase.Landed ? "Thank you for flying SkyLark Air!" : "Let me know if you need anything.")); return;
                case "pilot": Speak("Pilot Greeting", npc, null); return;
                default: LifeUI.Toast(npc.DisplayName + ": Hello!"); return;
            }
        }

        public override void Use(string action)
        {
            switch (action)
            {
                case "locker":
                    if (Booking.carryOnStowed) { LifeUI.Toast("Your bag is already in the locker."); return; }
                    var row = Booking.seat.TrimEnd('A', 'B', 'C', 'D', 'E', 'F');
                    Booking.carryOnStowed = true;
                    LifeUI.Toast("Carry-on stowed in the locker above row " + row + ". Now sit in " + Booking.seat + ".");
                    Objective = "Sit in seat " + Booking.seat + ".";
                    return;
                case "lavatory": LifeUI.Toast(State == Phase.Takeoff || State == Phase.Descent ? "Please stay seated while the seatbelt sign is on." : "Lavatory: OCCUPIED"); return;
                case "call": if (State == Phase.Cruise || State == Phase.Service) LifeUI.Toast("Call button pressed — the crew will be with you shortly."); return;
            }
        }

        protected override bool CanClose(string panel) => panel == "map";

        protected override void DrawPanel(string panel, float width, float height)
        {
            if (panel != "flightmap") return;
            var area = AirportUI.Window("In-flight map  ·  " + Booking.flight, 960, 560, width, height);
            GUILayout.BeginArea(area);
            var progress = State == Phase.Transition ? 1 - Mathf.Clamp01(timer / TransitionSeconds) : 1;
            GUILayout.Label("Metro City  →  " + Booking.Where.City + "   ·   " + Booking.Where.FlightHours + " h flight", AirportUI.InkBig);
            var bar = GUILayoutUtility.GetRect(100, 80, GUILayout.ExpandWidth(true));
            GUI.Box(bar, GUIContent.none, AirportUI.Board);
            var plane = new Rect(bar.x + 20 + (bar.width - 80) * progress, bar.y + 22, 60, 36);
            GUI.Label(plane, "✈", new GUIStyle(AirportUI.Amber) { fontSize = 30 });
            GUILayout.Label("Time remaining: " + (Booking.Where.FlightHours * (1 - progress)).ToString("0.0") + " h   ·   altitude 11,000 m   ·   ground speed 870 km/h", AirportUI.Ink);
            GUILayout.Label("You doze off under your blanket… (flight time is skipped)", AirportUI.Muted);
            if (demo) GUILayout.Label("Demo booking in use.", AirportUI.Bad);
            GUILayout.FlexibleSpace();
            GUI.enabled = State != Phase.Transition;
            if (GUILayout.Button(State == Phase.Transition ? "Flying…" : "Close", AirportUI.Primary)) Hide();
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
