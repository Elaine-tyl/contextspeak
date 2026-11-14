using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// The villa's passenger lift (3 floors). A real moving cab: call it from any floor (E at the call
    /// button), step in, press 1 / 2 / 3 for the floor. Cab and landing doors slide open only when the cab
    /// is at that floor; they stay open while someone stands in the doorway. The cab carries the player
    /// (CharacterController) with it. Chime on arrival, motor hum while moving. Last floor is saved.
    [DefaultExecutionOrder(-60)]
    public sealed class VillaElevator : MonoBehaviour
    {
        public enum Phase { Idle, Opening, Open, Closing, Moving }
        public static VillaElevator Instance { get; private set; }

        public Transform Cab;
        public Transform CabDoorL, CabDoorR;
        public Transform[] LandingL = new Transform[0], LandingR = new Transform[0];
        [Tooltip("World height of each floor (cab floor top).")] public float[] Floors = { 0, 3.4f, 6.8f };
        public string[] Names = { "Ground", "First", "Second" };
        [Tooltip("World direction the left leaves slide when opening (right leaves go the other way).")] public Vector3 OpenDirection = Vector3.forward;
        public float DoorSlide = 0.5f, DoorTime = 1.1f, Dwell = 4f, MaxSpeed = 1.5f, Acceleration = 1.2f;
        [Tooltip("Cab interior, in the cab's local space (for riders and the floor buttons).")] public Bounds Interior = new Bounds(new Vector3(0, 1.2f, 0), new Vector3(1.6f, 2.4f, 1.6f));
        [Tooltip("Doorway (cab local space): someone here stops the doors from closing.")] public Bounds Doorway = new Bounds(new Vector3(0, 1, 0), new Vector3(0.6f, 2, 1.0f));
        public TextMesh[] Indicators = new TextMesh[0];

        public int Floor { get; private set; }
        public Phase State { get; private set; } = Phase.Idle;
        public float DoorOpenness => doors;
        public int Target => target;
        public bool PlayerInside => player != null && Inside(player.transform.position);

        private readonly List<int> requests = new List<int>();
        private float doors, speed, openSince;
        private int target;
        private Vector3 cabL0, cabR0;
        private Vector3[] landL0 = new Vector3[0], landR0 = new Vector3[0];
        private CafePlayer player;
        private CharacterController controller;
        private AudioSource audioSource;
        private AudioClip chime, hum;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (!VillaState.Loaded) VillaState.Load();
            player = FindFirstObjectByType<CafePlayer>();
            if (player != null) controller = player.GetComponent<CharacterController>();
            if (CabDoorL != null) cabL0 = CabDoorL.localPosition;
            if (CabDoorR != null) cabR0 = CabDoorR.localPosition;
            landL0 = new Vector3[LandingL.Length]; landR0 = new Vector3[LandingR.Length];
            for (var i = 0; i < LandingL.Length; i++) if (LandingL[i] != null) landL0[i] = LandingL[i].position;
            for (var i = 0; i < LandingR.Length; i++) if (LandingR[i] != null) landR0[i] = LandingR[i].position;
            Floor = Mathf.Clamp(VillaState.Data.elevatorFloor, 0, Floors.Length - 1);
            if (Cab != null) { var p = Cab.position; p.y = Floors[Floor]; Cab.position = p; }
            audioSource = Cab != null ? Cab.gameObject.AddComponent<AudioSource>() : null;
            if (audioSource != null) { audioSource.spatialBlend = 1; audioSource.maxDistance = 18; audioSource.rolloffMode = AudioRolloffMode.Linear; }
            chime = Tone("Lift chime", new[] { 880f, 1318.5f }, 0.9f, false);
            hum = Tone("Lift hum", new[] { 70f, 140f }, 1f, true);
            ApplyDoors();
            UpdateIndicators();
        }

        // ---------------------------------------------------------------- requests

        /// Call or send the cab to a floor (0-based).
        public void Call(int floor)
        {
            floor = Mathf.Clamp(floor, 0, Floors.Length - 1);
            if (floor == Floor && State != Phase.Moving)
            {
                if (State == Phase.Open) openSince = Time.time;              // hold the doors
                else if (State != Phase.Opening) State = Phase.Opening;
                return;
            }
            if (!requests.Contains(floor)) requests.Add(floor);
        }

        public bool Inside(Vector3 world) => Cab != null && Interior.Contains(Cab.InverseTransformPoint(world + Vector3.up * 0.3f));

        private bool DoorwayBlocked()
        {
            if (Cab == null) return false;
            if (player != null && Doorway.Contains(Cab.InverseTransformPoint(player.transform.position + Vector3.up * 0.3f))) return true;
            foreach (var npc in LivingNPC.All) if (npc != null && Doorway.Contains(Cab.InverseTransformPoint(npc.transform.position + Vector3.up * 0.3f))) return true;
            return false;
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            var dt = Time.deltaTime;
            switch (State)
            {
                case Phase.Idle:
                    if (requests.Count > 0)
                    {
                        target = requests[0];
                        if (target == Floor) { requests.RemoveAt(0); State = Phase.Opening; }
                        else State = doors > 0.001f ? Phase.Closing : Phase.Moving;
                    }
                    break;
                case Phase.Opening:
                    doors = Mathf.MoveTowards(doors, 1, dt / DoorTime);
                    if (doors >= 1) { State = Phase.Open; openSince = Time.time; }
                    break;
                case Phase.Open:
                    if (DoorwayBlocked()) openSince = Time.time;
                    if (Time.time - openSince > (requests.Count > 0 ? 1.5f : Dwell)) State = Phase.Closing;
                    break;
                case Phase.Closing:
                    if (DoorwayBlocked()) { State = Phase.Opening; break; }
                    doors = Mathf.MoveTowards(doors, 0, dt / DoorTime);
                    if (doors <= 0)
                    {
                        requests.Remove(Floor);
                        if (requests.Count > 0) { target = requests[0]; State = target == Floor ? Phase.Opening : Phase.Moving; }
                        else State = Phase.Idle;
                    }
                    break;
                case Phase.Moving: Move(dt); break;
            }
            ApplyDoors();
            UpdateIndicators();
            Controls();
        }

        private void Move(float dt)
        {
            if (Cab == null) { State = Phase.Idle; return; }
            var y = Cab.position.y; var goal = Floors[target];
            var remaining = goal - y;
            var dir = Mathf.Sign(remaining);
            // Trapezoid profile: accelerate, cruise, decelerate to land exactly on the floor.
            var brake = Mathf.Sqrt(2 * Acceleration * Mathf.Abs(remaining));
            speed = Mathf.Min(speed + Acceleration * dt, MaxSpeed, Mathf.Max(0.12f, brake));
            var step = dir * Mathf.Min(speed * dt, Mathf.Abs(remaining));
            if (audioSource != null && hum != null && !audioSource.isPlaying) { audioSource.clip = hum; audioSource.loop = true; audioSource.volume = 0.25f; audioSource.Play(); }
            var riders = PlayerInside;
            var delta = Vector3.up * step;
            if (dir > 0) { Carry(riders, delta); Cab.position += delta; }
            else { Cab.position += delta; Carry(riders, delta); }
            Physics.SyncTransforms();
            if (Mathf.Abs(goal - Cab.position.y) < 0.001f)
            {
                var p = Cab.position; p.y = goal; Cab.position = p;
                Floor = target; speed = 0;
                requests.Remove(target);
                VillaState.Data.elevatorFloor = Floor; VillaState.Touch();
                if (audioSource != null) { audioSource.Stop(); audioSource.loop = false; if (chime != null) audioSource.PlayOneShot(chime, 0.6f); }
                State = Phase.Opening;
            }
        }

        private void Carry(bool riding, Vector3 delta)
        {
            if (!riding || player == null) return;
            if (controller != null) controller.enabled = false;
            player.transform.position += delta;
            if (controller != null) controller.enabled = true;
        }

        private void ApplyDoors()
        {
            var e = Mathf.SmoothStep(0, 1, doors) * DoorSlide;
            var dir = OpenDirection.normalized;
            if (Cab != null)
            {
                var local = Cab.InverseTransformDirection(dir);
                if (CabDoorL != null) CabDoorL.localPosition = cabL0 + local * e;
                if (CabDoorR != null) CabDoorR.localPosition = cabR0 - local * e;
            }
            for (var i = 0; i < LandingL.Length; i++)
            {
                var open = i == Floor && State != Phase.Moving ? e : 0;
                if (LandingL[i] != null) LandingL[i].position = landL0[i] + dir * open;
                if (i < LandingR.Length && LandingR[i] != null) LandingR[i].position = landR0[i] - dir * open;
            }
        }

        private void UpdateIndicators()
        {
            var arrow = State == Phase.Moving ? (Floors[target] > (Cab != null ? Cab.position.y : 0) ? "▲" : "▼") : "";
            var shown = State == Phase.Moving && Cab != null ? Nearest(Cab.position.y) : Floor;
            foreach (var t in Indicators) if (t != null) t.text = arrow + (shown + 1);
        }

        private int Nearest(float y) { var best = 0; for (var i = 1; i < Floors.Length; i++) if (Mathf.Abs(Floors[i] - y) < Mathf.Abs(Floors[best] - y)) best = i; return best; }

        private void Controls()
        {
            if (!PlayerInside) return;
            var hud = ActionHud.Instance;
            for (var i = 0; i < Floors.Length; i++)
                hud?.Prompt("lift-" + i, (i + 1).ToString(), (i == Floor && State != Phase.Moving ? "● " : "") + Names[Mathf.Min(i, Names.Length - 1)] + " floor");
            var k = Keyboard.current;
            if (k == null) return;
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Call(0);
            if (Floors.Length > 1 && (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame)) Call(1);
            if (Floors.Length > 2 && (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame)) Call(2);
        }

        private static AudioClip Tone(string name, float[] freqs, float seconds, bool loop)
        {
            const int rate = 22050;
            var n = (int)(rate * seconds);
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = (float)i / rate;
                float v;
                if (loop) v = 0.35f * Mathf.Sin(2 * Mathf.PI * freqs[0] * t) + 0.15f * Mathf.Sin(2 * Mathf.PI * freqs[1] * t);
                else
                {
                    var half = seconds / freqs.Length;
                    var k = Mathf.Min(freqs.Length - 1, (int)(t / half));
                    var local = t - k * half;
                    v = 0.5f * Mathf.Sin(2 * Mathf.PI * freqs[k] * t) * Mathf.Exp(-local * 4f);
                }
                data[i] = v;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// The call button beside the lift doors on each floor.
    public sealed class VillaElevatorCall : InteractableObject
    {
        public int Floor;
        public VillaElevator Lift;
        public override bool CanInteract => base.CanInteract && Lift != null && !Lift.PlayerInside;
        public override string Label => Lift != null && Lift.Floor == Floor && Lift.State != VillaElevator.Phase.Moving ? "Open the lift" : "Call the lift";
        public override void Interact(InteractionManager manager) { if (Lift != null) Lift.Call(Floor); }
    }

    /// Carries an umbrella: the player takes one from the stand by the front door (VillaFixture Umbrella);
    /// residents open theirs automatically when it rains and they are outdoors (garden, terraces, street).
    /// Held at the right hand, canopy upright above the head (no dedicated holding pose yet).
    public sealed class UmbrellaCarrier : MonoBehaviour
    {
        public GameObject Prefab;
        [Tooltip("Open automatically outdoors in the rain (residents).")] public bool Auto;
        public bool Holding;
        private Transform hand;
        private GameObject instance;
        private LivingNPC npc;

        private void Start()
        {
            npc = GetComponent<LivingNPC>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("RightHand")) { hand = t; break; }
        }

        public bool Outdoors
        {
            get
            {
                var room = VillaRoom.At(transform.position);
                return room == null || room.Kind == "terrace";
            }
        }

        private void LateUpdate()
        {
            var want = Auto ? SkyState.Raining && Outdoors && (npc == null || !npc.Hidden) : Holding;
            if (want && instance == null && Prefab != null)
            {
                instance = Instantiate(Prefab);
                instance.name = "Umbrella (" + name + ")";
                foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            if (instance == null) return;
            if (instance.activeSelf != want) instance.SetActive(want);
            if (!want) return;
            var yaw = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            var grip = hand != null ? hand.position : transform.position + Vector3.up * 1.0f + yaw * new Vector3(0.25f, 0, 0.1f);
            grip.y = Mathf.Max(grip.y, transform.position.y + 1.15f);       // keep the canopy above the head
            // Handle in the hand, shaft leaning slightly back so the canopy is over the head.
            instance.transform.SetPositionAndRotation(grip - Vector3.up * 0.06f, yaw * Quaternion.Euler(-8, 0, 6));
        }

        private void OnDestroy() { if (instance != null) Destroy(instance); }
    }
}
