using System;
using System.Diagnostics;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace ContextSpeak.Life
{
    /// Live tuning multipliers (swimming debug panel, F5). All of them change actual gameplay.
    [Serializable]
    public sealed class SwimTuning
    {
        public float SpeedScale = 1, BuoyancyScale = 1, DampingScale = 1, WaterlineOffset, BobScale = 1, AnimationBlendScale = 1, WakeScale = 1, SplashScale = 1;
    }

    /// Scripted input for automated Play-mode tests (replaces keyboard/gamepad while set).
    public sealed class SwimScriptedInput
    {
        public Vector2 Move;
        public bool Up, Down, Fast, BackFloatPressed, InteractPressed;
        /// Underwater: −1…1 vertical aim (the camera pitch with real input).
        public float Aim;
    }

    /// Third-person swimming for the existing CafePlayer + CharacterController. There is one movement
    /// owner at any time: CafePlayer walks (and wades); when the water gets deep enough this component
    /// becomes CafePlayer.LocomotionOverride and moves the same CharacterController with a
    /// buoyancy-inspired vertical velocity (never by setting a fixed height or teleporting), camera-
    /// relative strokes with water resistance, currents and wave surge; then hands back to CafePlayer
    /// with its momentum when the body stands in shallow water or has climbed out.
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CafePlayer))]
    public sealed class SwimmingController : MonoBehaviour, CafePlayer.ILocomotionOverride
    {
        [Tooltip("Forces this profile everywhere (testing). Normally each WaterBody supplies its own.")]
        public SwimmingProfile ProfileOverride;
        public SwimTuning Tuning = new SwimTuning();

        [Header("Input (Unity Input System — rebind here)")]
        public InputAction UpAction = Make("Swim up", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        public InputAction DownAction = Make("Dive", "<Keyboard>/c", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
        public InputAction FastAction = Make("Swim fast", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
        public InputAction BackFloatAction = Make("Back float", "<Keyboard>/f", "<Gamepad>/dpad/down");
        public InputAction InteractAction = Make("Climb out", "<Keyboard>/e", "<Gamepad>/buttonWest");

        [NonSerialized] public SwimScriptedInput ScriptedInput;

        public readonly SwimmingStateMachine Machine = new SwimmingStateMachine();
        public readonly CharacterBuoyancyController Buoyancy = new CharacterBuoyancyController();
        public readonly WaterSurfaceFollower Surface = new WaterSurfaceFollower();
        public CharacterWaterDetector Detector { get; private set; }
        public SwimmingProfile Profile { get; private set; }
        public WaterBody Body => Detector?.Body;
        public SwimState State => Machine.State;
        public bool IsSwimming => Machine.IsSwimming;
        public bool OwnsMovement => Machine.OwnsMovement || exit != ExitMode.None;
        /// Head under water (brief face dips during a surface stroke do not count).
        public bool HeadUnder => Machine.IsSwimming && Detector != null && Detector.HeadDepth > (Machine.AtSurface ? 0.15f : 0.02f);
        public float Breath { get; private set; } = 1;
        public float VerticalVelocity => vy;
        public Vector3 FlatVelocity => flat;
        public float HorizontalSpeed => flat.magnitude;
        public Vector3 Heading { get; private set; } = Vector3.forward;
        /// Signed turn rate (°/s) for banking into turns.
        public float TurnRate { get; private set; }
        public float FloatLine => (Profile != null ? Profile.FloatLine : 1.38f) - Tuning.WaterlineOffset;
        public float Height { get; private set; } = 1.7f;
        public float CpuMilliseconds { get; private set; }
        public bool CameraUnderwater => UnderwaterInland.Active || UnderwaterView.Active;
        public string ExitPhase => exit == ExitMode.None ? "" : exit + " · " + phase;
        public ExitMode Exit => exit;
        public int ExitPhaseIndex => phase;
        public bool HasExitCandidate => candidate.Valid;
        /// The animation controller reports when the body is upright and back on its feet visually.
        [NonSerialized] public bool VisualSettled = true;

        public enum ExitMode { None, Edge, Ladder, Dock, LadderDown }

        public bool Active => OwnsMovement;
        public PlayerAnimState AnimState =>
            Machine.State == SwimState.ExitingWater ? (flat.magnitude > 0.15f ? PlayerAnimState.Walk : PlayerAnimState.Idle) : PlayerAnimState.Idle;

        private CafePlayer player;
        private CharacterController controller;
        private Vector3 flat;
        private float vy, cpuAverage, hintCooldown, probeTimer, wallTimer, outOfBreathToast;
        private bool plunging, forceSurface, introShown, upHeld, downHeld, fastHeld, togglePressed, interactPressed;
        private Vector2 move;
        private float aim;
        private ExitMode exit = ExitMode.None;
        private int phase;
        private float phaseTime;
        private (bool Valid, Vector3 Grab, Vector3 Top, Vector3 Face, WaterExitPoint Point) candidate;
        private readonly Stopwatch watch = new Stopwatch();

        private static InputAction Make(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings) action.AddBinding(binding);
            return action;
        }

        // ------------------------------------------------------------------ setup

        public static bool Handles(CafePlayer player) =>
            player != null && player.TryGetComponent<SwimmingController>(out var swimmer) && swimmer.isActiveAndEnabled;

        private static bool ensuring;

        /// Every playable scene with water gets swimming automatically (pond, pool, river, lake, ocean),
        /// also scenes that only have the older PondSwimming / beach Swimming components.
        public static void EnsureForScene()
        {
            if (!Application.isPlaying || ensuring) return;
            ensuring = true;
            try
            {
                foreach (var pond in FindObjectsByType<PondSwimming>(FindObjectsSortMode.None))
                {
                    var covered = false;
                    foreach (var body in WaterBody.All) if (body != null && body.Volume.Intersects(pond.Water)) { covered = true; break; }
                    if (covered) continue;
                    var added = pond.gameObject.AddComponent<WaterBody>();
                    added.Kind = WaterKind.Pond;
                    added.Volume = pond.Water;
                }
                var ocean = FindFirstObjectByType<OceanWaves>();
                if (ocean != null && ocean.GetComponent<WaterBody>() == null && !WaterBody.All.Exists(b => b != null && b.Kind == WaterKind.Ocean))
                {
                    var sea = ocean.gameObject.AddComponent<WaterBody>();
                    sea.Kind = WaterKind.Ocean;
                    sea.Volume = new Bounds(new Vector3(ocean.IslandCentre.x, OceanWaves.SeaLevel - 15, ocean.IslandCentre.z), new Vector3(4000, 34, 4000));
                }
                if (WaterBody.All.Count == 0) return;
                foreach (var player in FindObjectsByType<CafePlayer>(FindObjectsSortMode.None))
                {
                    if (player.GetComponent<CharacterWaterInteraction>() == null) player.gameObject.AddComponent<CharacterWaterInteraction>();
                    if (player.GetComponent<SwimmingController>() == null) player.gameObject.AddComponent<SwimmingController>();
                }
            }
            finally { ensuring = false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene();

        private void Awake()
        {
            player = GetComponent<CafePlayer>();
            controller = GetComponent<CharacterController>();
            Height = controller != null ? Mathf.Max(1.2f, controller.height) : 1.7f;
            Detector = new CharacterWaterDetector(transform, Height);
            if (GetComponent<SwimmingAnimationController>() == null) gameObject.AddComponent<SwimmingAnimationController>();
            if (GetComponent<SwimmingCameraController>() == null) gameObject.AddComponent<SwimmingCameraController>();
            if (GetComponent<SwimmingWaterInteraction>() == null) gameObject.AddComponent<SwimmingWaterInteraction>();
            if (FindFirstObjectByType<SwimmingDebugPanel>() == null) new GameObject("Swimming debug panel").AddComponent<SwimmingDebugPanel>();
            Machine.Changed += OnStateChanged;
        }

        private void OnEnable()
        {
            if (player != null) player.LocomotionOverride = this;
            UpAction.Enable(); DownAction.Enable(); FastAction.Enable(); BackFloatAction.Enable(); InteractAction.Enable();
        }

        private void OnDisable()
        {
            if (player != null && ReferenceEquals(player.LocomotionOverride, this))
            {
                if (OwnsMovement) player.SetVelocity(flat, vy);
                player.LocomotionOverride = null;
            }
            UpAction.Disable(); DownAction.Disable(); FastAction.Disable(); BackFloatAction.Disable(); InteractAction.Disable();
        }

        private void OnDestroy()
        {
            Machine.Changed -= OnStateChanged;
            UpAction.Dispose(); DownAction.Dispose(); FastAction.Dispose(); BackFloatAction.Dispose(); InteractAction.Dispose();
        }

        // ------------------------------------------------------------------ sensing + state (before CafePlayer)

        private void Update()
        {
            if (controller == null || !controller.enabled) return;
            watch.Restart();
            var dt = Time.deltaTime;
            if (player.LocomotionOverride == null) player.LocomotionOverride = this;
            var animation = GetComponent<SwimmingAnimationController>();
            Detector.HeadBone = animation != null ? animation.HeadBone : null;
            Detector.Sample();
            var profile = ProfileOverride != null ? ProfileOverride : Body != null ? Body.ActiveSwimming : Profile;
            if (profile == null) profile = Profile = SwimmingProfile.Create(WaterKind.Pond);
            if (profile != Profile) { Profile = profile; Machine.Configure(profile); }
            ReadInput();

            var blocked = player.Blocked || (player.Controls != null && player.Controls.Suspended && ScriptedInput == null);
            var sense = new SwimSense
            {
                InWater = Detector.InWater,
                Immersion = Detector.Immersion,
                Depth = Detector.Depth,
                HeadDepth = Detector.HeadDepth,
                FeetAboveBed = Body != null ? transform.position.y - Detector.Bed : 99,
                Grounded = OwnsMovement ? (controller.collisionFlags & CollisionFlags.Below) != 0 : controller.isGrounded,
                VerticalSpeed = OwnsMovement ? vy : player.Velocity.y,
                Move = blocked ? Vector2.zero : move,
                Up = !blocked && upHeld,
                Down = !blocked && downHeld,
                ToggleBackFloat = !blocked && togglePressed && !NpcNearby(),
                ForceSurface = forceSurface,
            };
            if (exit == ExitMode.None) Machine.Update(sense, dt);
            UpdateBreath(dt);
            if (Machine.AtSurface && exit == ExitMode.None) FindExit(dt, blocked);
            else if (!OwnsMovement && exit == ExitMode.None && !blocked && controller.isGrounded && player.Activity == PlayerAnimState.Idle) FindLadderDown();
            watch.Stop();
            cpuAverage = Mathf.Lerp(cpuAverage, (float)watch.Elapsed.TotalMilliseconds, 0.05f);
        }

        private void ReadInput()
        {
            if (ScriptedInput != null)
            {
                move = ScriptedInput.Move; upHeld = ScriptedInput.Up; downHeld = ScriptedInput.Down; fastHeld = ScriptedInput.Fast;
                togglePressed = ScriptedInput.BackFloatPressed; interactPressed = ScriptedInput.InteractPressed;
                ScriptedInput.BackFloatPressed = false; ScriptedInput.InteractPressed = false;
                aim = ScriptedInput.Aim;
                return;
            }
            move = player.Controls != null ? player.Controls.Movement : Vector2.zero;
            upHeld = UpAction.IsPressed(); downHeld = DownAction.IsPressed(); fastHeld = FastAction.IsPressed();
            togglePressed = BackFloatAction.WasPressedThisFrame(); interactPressed = InteractAction.WasPressedThisFrame();
            var view = player.View != null ? player.View.transform : Camera.main != null ? Camera.main.transform : null;
            aim = view != null ? Mathf.Clamp(view.forward.y * 1.6f, -1, 1) : 0;
        }

        private float npcCheck;
        private bool npcNear;

        /// F also talks to NPCs: near someone, F is left to the conversation (talking while treading water).
        private bool NpcNearby()
        {
            if (Time.time < npcCheck) return npcNear;
            npcCheck = Time.time + 0.5f;
            npcNear = false;
            foreach (var npc in FindObjectsByType<NPCController>(FindObjectsSortMode.None))
                if ((npc.transform.position - transform.position).sqrMagnitude < 3.5f * 3.5f) { npcNear = true; break; }
            return npcNear;
        }

        private void OnStateChanged(SwimState from, SwimState to)
        {
            if (to == SwimState.EnteringWater && (from == SwimState.Grounded || from == SwimState.Wading))
            {
                var v = player.Velocity;
                flat = new Vector3(v.x, 0, v.z);
                vy = v.y;
                plunging = vy < -2.5f;
                Surface.Reset();
                if (flat.sqrMagnitude > 0.01f) Heading = flat.normalized;
                else { var f = player.Model != null ? player.Model.forward : transform.forward; f.y = 0; if (f.sqrMagnitude > 0.01f) Heading = f.normalized; }
                if (!introShown)
                {
                    introShown = true;
                    LifeUI.Toast("Swimming — WASD swim · Shift faster · Space up · C/Ctrl dive · F float on your back · E climb out at ladders and edges");
                }
                ActionHud.Instance?.Activity("SWIMMING" + (Body != null ? " · " + Body.Kind.ToString().ToUpperInvariant() : ""), 2.5f);
            }
            if (to == SwimState.Diving) { vy = Mathf.Min(vy, -0.9f); plunging = false; }
            if (to == SwimState.ExitingWater) plunging = false;
        }

        // ------------------------------------------------------------------ movement (called by CafePlayer.Update)

        public void Tick(float dt)
        {
            watch.Restart();
            if (dt <= 0) return;
            if (exit != ExitMode.None) TickExit(dt);
            else if (Machine.State == SwimState.ExitingWater) TickWadeOut(dt);
            else TickSwim(dt);
            watch.Stop();
            CpuMilliseconds = Mathf.Lerp(CpuMilliseconds, cpuAverage + (float)watch.Elapsed.TotalMilliseconds, 0.05f);
        }

        private Vector3 Wish()
        {
            var input = Vector2.ClampMagnitude(player.Blocked ? Vector2.zero : move, 1);
            return Quaternion.Euler(0, player.Yaw, 0) * new Vector3(input.x, 0, input.y);
        }

        private void TickSwim(float dt)
        {
            var p = Profile;
            var state = Machine.State;
            var wish = Wish();
            var speed = state switch
            {
                SwimState.SurfaceSwimming => fastHeld ? p.FastSpeed : p.SurfaceSpeed,
                SwimState.TreadingWater => p.TreadSpeed,
                SwimState.BackFloating => p.BackFloatSpeed,
                SwimState.Diving or SwimState.UnderwaterSwimming => p.UnderwaterSpeed * (fastHeld ? 1.35f : 1),
                SwimState.Surfacing => p.SurfaceSpeed * 0.6f,
                _ => p.SurfaceSpeed * 0.5f,
            } * Tuning.SpeedScale;
            var stroking = wish.sqrMagnitude > 0.0025f;
            flat = Vector3.MoveTowards(flat, wish * speed, (stroking ? p.Acceleration : p.Deceleration) * Tuning.SpeedScale * dt);

            // Vertical: buoyancy around the (smoothed, wave-following) waterline + player thrust.
            Surface.Smoothing = p.SurfaceSmoothing;
            Surface.WaveFollow = p.WaveFollow;
            Surface.BobAmplitude = p.BobAmplitude * Tuning.BobScale;
            Surface.BobFrequency = p.BobFrequency;
            var calm = Body != null && Body.Kind == WaterKind.Ocean ? OceanWaves.SeaLevel : Detector.RawSurface;
            var waterline = Detector.Body != null ? Surface.Update(Detector.RawSurface, calm, dt) : Surface.Height;
            var thrust = 0f;
            var netScale = 1f;
            var maxSink = p.MaxSinkSpeed;
            var maxRise = p.MaxRiseSpeed;
            switch (state)
            {
                case SwimState.Diving:
                    thrust = -p.VerticalThrust * 1.2f; netScale = p.UnderwaterBuoyancy; maxSink = p.DescentSpeed * 1.4f;
                    break;
                case SwimState.UnderwaterSwimming:
                {
                    var vertical = (upHeld ? 1 : 0) - (downHeld ? 1 : 0) + (stroking ? aim * Vector2.ClampMagnitude(move, 1).y : 0);
                    vertical = Mathf.Clamp(vertical, -1, 1);
                    var target = vertical > 0 ? vertical * p.AscentSpeed : vertical * p.DescentSpeed;
                    var active = stroking || upHeld || downHeld;
                    netScale = active ? p.UnderwaterBuoyancy : 1;          // idle: float up naturally
                    thrust = active ? (target - vy) * 3f : 0;
                    maxSink = p.DescentSpeed; maxRise = Mathf.Max(p.MaxRiseSpeed, p.AscentSpeed);
                    break;
                }
                case SwimState.Surfacing:
                    thrust = upHeld ? p.VerticalThrust * 0.4f : 0;
                    break;
                default:
                    // At the surface Space kicks the head a little higher (a short burst, not flying).
                    if (upHeld && Detector.WaterLevel > FloatLine - 0.25f) thrust = p.VerticalThrust * 0.5f;
                    break;
            }
            if (forceSurface) { thrust = p.VerticalThrust; netScale = 1; }
            var damping = p.BuoyancyDamping * Tuning.DampingScale * (plunging ? 0.35f : 1);
            vy = Buoyancy.Step(vy, transform.position.y, waterline, Height, FloatLine, p.BuoyancyStrength * Tuning.BuoyancyScale,
                damping, maxRise, maxSink, thrust, netScale, dt);
            if (plunging && vy > -0.3f) plunging = false;

            // Water movement: currents and (ocean) surge.
            var current = WaterSurfaceSampler.GetWaterVelocity(Body, transform.position, p.WaveSurge) * p.CurrentInfluence;
            if (state == SwimState.BackFloating) current *= 1.15f;          // a floating body drifts with the water
            var flags = controller.Move((flat + current) * dt + Vector3.up * (vy * dt));
            if ((flags & CollisionFlags.Below) != 0 && vy < 0) vy = 0;      // resting on the pool floor / bed
            if ((flags & CollisionFlags.Above) != 0 && vy > 0) vy = 0;
            wallTimer = (flags & CollisionFlags.Sides) != 0 && stroking ? wallTimer + dt : 0;
            if ((flags & CollisionFlags.Sides) != 0) flat *= 0.85f;          // no sliding at full speed along walls

            // Facing: toward the stroke direction (turn-rate limited); NPC partner when treading and talking.
            var desired = Heading;
            if (flat.sqrMagnitude > 0.02f && state != SwimState.BackFloating) desired = flat.normalized;
            else if (state == SwimState.BackFloating && flat.sqrMagnitude > 0.01f) desired = flat.normalized;
            if (player.ConversationPartner != null && (state == SwimState.TreadingWater || state == SwimState.BackFloating))
            {
                var toward = player.ConversationPartner.position - transform.position;
                toward.y = 0;
                if (toward.sqrMagnitude > 0.01f) desired = toward.normalized;
            }
            var before = Heading;
            Heading = Vector3.RotateTowards(Heading, desired, p.TurnRate * Mathf.Deg2Rad * dt, 0);
            TurnRate = Mathf.Lerp(TurnRate, Vector3.SignedAngle(before, Heading, Vector3.up) / dt, 1 - Mathf.Exp(-6 * dt));
        }

        /// Standing up in shallow water: gravity + wading, then CafePlayer takes over once the body is upright.
        private void TickWadeOut(float dt)
        {
            var p = Profile;
            var wish = Wish();
            flat = Vector3.MoveTowards(flat, wish * p.WadeSpeed * 0.7f, 3f * dt);
            vy = Mathf.Max(vy - 9.81f * dt, -6);
            var flags = controller.Move(flat * dt + Vector3.up * (vy * dt));
            var grounded = (flags & CollisionFlags.Below) != 0;
            if (grounded) vy = -1;
            if (flat.sqrMagnitude > 0.02f) Heading = Vector3.RotateTowards(Heading, flat.normalized, p.TurnRate * Mathf.Deg2Rad * dt, 0);
            if ((grounded && VisualSettled && Machine.TimeInState > 0.35f) || Machine.TimeInState > 2.5f) Release();
        }

        private void Release()
        {
            exit = ExitMode.None;
            player.SetVelocity(flat, -2);
            Machine.Release(Detector.InWater);
            forceSurface = false;
        }

        // ------------------------------------------------------------------ climbing out (ladder, dock, edge)

        private void FindExit(float dt, bool blocked)
        {
            hintCooldown -= dt;
            if ((probeTimer -= dt) <= 0)
            {
                probeTimer = 0.15f;
                candidate = default;
                var point = WaterExitPoint.Nearest(transform.position);
                if (point != null)
                {
                    var face = point.TopPoint - point.BottomPoint; face.y = 0;
                    candidate = (true, point.BottomPoint, point.TopPoint, face.sqrMagnitude > 0.001f ? face.normalized : Heading, point);
                }
                else if (Machine.State != SwimState.BackFloating) ProbeEdge();
            }
            if (candidate.Valid && !blocked)
                ActionHud.Instance?.Prompt("swim-exit", "E", candidate.Point != null && candidate.Point.Kind == WaterExitPoint.ExitKind.Ladder ? "Climb the ladder" : candidate.Point != null && candidate.Point.Kind == WaterExitPoint.ExitKind.Dock ? "Climb onto the dock" : "Climb out");
            if (!blocked && Machine.State == SwimState.TreadingWater)
            {
                ActionHud.Instance?.Prompt("swim-dive", "C", "Dive");
                if (!NpcNearby()) ActionHud.Instance?.Prompt("swim-float", "F", "Float on your back");
            }
            else if (!blocked && Machine.State == SwimState.BackFloating) ActionHud.Instance?.Prompt("swim-float", "F", "Tread water");
            if (!blocked && interactPressed && candidate.Valid) BeginExit();
        }

        private readonly RaycastHit[] probeHits = new RaycastHit[12];

        private bool Ignore(Collider c) => c is CharacterController || c.GetComponentInParent<CharacterController>() != null || c.isTrigger;

        /// A pool edge / bank / dock in front: a wall at chest height whose flat top is a little above the water.
        private void ProbeEdge()
        {
            var waterline = Detector.RawSurface;
            var origin = new Vector3(transform.position.x, waterline - 0.1f, transform.position.z);
            var count = Physics.SphereCastNonAlloc(origin, 0.15f, Heading, probeHits, 0.85f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            RaycastHit wall = default;
            for (var i = 0; i < count; i++)
                if (!Ignore(probeHits[i].collider) && probeHits[i].distance > 0 && probeHits[i].distance < best && probeHits[i].normal.y < 0.4f) { best = probeHits[i].distance; wall = probeHits[i]; }
            if (best == float.MaxValue) return;
            var normal = new Vector3(wall.normal.x, 0, wall.normal.z).normalized;
            if (normal.sqrMagnitude < 0.5f) return;
            var above = wall.point - normal * 0.45f + Vector3.up * 1.4f;
            if (!Physics.Raycast(above, Vector3.down, out var top, 2.2f, ~0, QueryTriggerInteraction.Ignore) || Ignore(top.collider)) return;
            if (top.normal.y < 0.8f || top.point.y < waterline - 0.05f || top.point.y > waterline + 0.8f) return;
            var standCentre = top.point + Vector3.up * (Height * 0.5f + 0.05f);
            if (Physics.CheckCapsule(standCentre - Vector3.up * (Height * 0.5f - 0.32f), standCentre + Vector3.up * (Height * 0.5f - 0.32f), 0.28f, ~0, QueryTriggerInteraction.Ignore)
                && OverlapsSolid(standCentre)) return;                     // no room to stand
            var grab = wall.point + normal * 0.34f;
            grab.y = waterline;
            candidate = (true, grab, top.point, -normal, null);
        }

        private readonly Collider[] overlaps = new Collider[8];

        private bool OverlapsSolid(Vector3 centre)
        {
            var n = Physics.OverlapCapsuleNonAlloc(centre - Vector3.up * (Height * 0.5f - 0.32f), centre + Vector3.up * (Height * 0.5f - 0.32f), 0.28f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++) if (!Ignore(overlaps[i])) return true;
            return false;
        }

        private Vector3 exitGrab, exitTop, exitFace;

        public void BeginExit()
        {
            if (!candidate.Valid) return;
            exit = candidate.Point == null ? ExitMode.Edge : candidate.Point.Kind == WaterExitPoint.ExitKind.Ladder ? ExitMode.Ladder : candidate.Point.Kind == WaterExitPoint.ExitKind.Dock ? ExitMode.Dock : ExitMode.Edge;
            exitGrab = candidate.Grab; exitTop = candidate.Top; exitFace = candidate.Face;
            phase = 0; phaseTime = 0;
            Machine.Force(SwimState.ExitingWater);
        }

        /// On the deck at the top of a pool ladder: E climbs down into the water.
        private void FindLadderDown()
        {
            WaterExitPoint best = null;
            foreach (var point in WaterExitPoint.All)
            {
                if (point == null || !point.isActiveAndEnabled || point.Kind != WaterExitPoint.ExitKind.Ladder) continue;
                var d = point.TopPoint - transform.position;
                if (Mathf.Abs(d.y) < 0.6f && new Vector2(d.x, d.z).magnitude < 1.0f) { best = point; break; }
            }
            if (best == null) return;
            var driver = GetComponent<VehicleDriver>();
            if (driver != null && driver.Driving) return;
            ActionHud.Instance?.Prompt("swim-ladder-down", "E", "Climb down the ladder");
            if (!interactPressed) return;
            var face = best.TopPoint - best.BottomPoint; face.y = 0;
            exitGrab = best.BottomPoint; exitTop = best.TopPoint; exitFace = face.sqrMagnitude > 0.001f ? face.normalized : Heading;
            exit = ExitMode.LadderDown;
            phase = 0; phaseTime = 0;
            flat = Vector3.zero; vy = 0;
            Machine.Force(SwimState.ExitingWater);
        }

        /// Ladder down: 0 step to the top of the ladder · 1 swing over onto the rungs · 2 climb down into the water.
        private void TickLadderDown(float dt)
        {
            phaseTime += dt;
            var position = transform.position;
            Heading = Vector3.RotateTowards(Heading, exitFace, 6 * dt, 0);
            switch (phase)
            {
                case 0:
                {
                    var to = exitTop - position; to.y = 0;
                    controller.Move(Vector3.ClampMagnitude(to, 1.2f * dt));
                    if (to.magnitude < 0.1f || phaseTime > 1.5f) Next();
                    break;
                }
                case 1:
                {
                    var to = exitGrab - position; to.y = 0;
                    controller.Move(Vector3.ClampMagnitude(to, 0.8f * dt));
                    if (to.magnitude < 0.08f || phaseTime > 1.8f) Next();
                    break;
                }
                default:
                {
                    var anim = GetComponent<SwimmingAnimationController>();
                    var speed = anim != null ? anim.LadderClimbSpeed : 0.6f;
                    var flags = controller.Move(Vector3.down * speed * dt);
                    Detector.Sample();
                    var waterline = Detector.Body != null ? Detector.RawSurface : exitGrab.y;
                    if (Detector.Body != null && transform.position.y <= waterline - FloatLine + 0.05f)
                    {
                        exit = ExitMode.None;
                        vy = 0; Surface.Reset();
                        Heading = -exitFace;
                        Machine.Force(SwimState.TreadingWater);
                        GetComponent<CharacterWaterInteraction>()?.OnDiveEntry();
                    }
                    else if ((flags & CollisionFlags.Below) != 0 || phaseTime > 6) Release();      // shallow end: stand
                    break;
                }
            }
            flat = Vector3.zero;
        }

        /// 0 swim to the hold · 1 take hold (EdgeReach) · 2 pull/climb up · 3 step over onto the top · 4 stand.
        private void TickExit(float dt)
        {
            if (exit == ExitMode.LadderDown) { TickLadderDown(dt); return; }
            phaseTime += dt;
            var position = transform.position;
            Heading = Vector3.RotateTowards(Heading, exitFace, 6 * dt, 0);
            var waterline = Detector.Body != null ? Detector.RawSurface : exitGrab.y;
            switch (phase)
            {
                case 0:
                {
                    var to = exitGrab - position; to.y = 0;
                    var feetTarget = waterline - (FloatLine - 0.1f);
                    var step = Vector3.ClampMagnitude(to, 1.2f * dt);
                    var rise = Mathf.Clamp(feetTarget - position.y, -0.8f * dt, 0.8f * dt);
                    var flags = controller.Move(step + Vector3.up * rise);
                    if (to.magnitude < 0.08f || phaseTime > 1.2f || (flags & CollisionFlags.Sides) != 0 && phaseTime > 0.3f) Next();
                    break;
                }
                case 1:
                    controller.Move(Vector3.up * Mathf.Clamp(waterline - (FloatLine - 0.25f) - position.y, -0.5f * dt, 0.5f * dt));
                    if (phaseTime > (exit == ExitMode.Ladder ? 0.45f : 0.9f)) Next();
                    break;
                case 2:
                {
                    var anim = GetComponent<SwimmingAnimationController>();
                    var speed = exit == ExitMode.Ladder ? (anim != null ? anim.LadderClimbSpeed : 0.6f) : 1.1f;
                    var flags = controller.Move(Vector3.up * speed * dt);
                    if (transform.position.y >= exitTop.y + 0.03f) Next();
                    else if ((flags & CollisionFlags.Above) != 0 || phaseTime > 4f) Abort();
                    break;
                }
                case 3:
                {
                    var to = exitTop - transform.position; to.y = 0;
                    var flags = controller.Move(Vector3.ClampMagnitude(to, 1.1f * dt) + Vector3.down * 0.5f * dt);
                    if (to.magnitude < 0.1f || phaseTime > 1.4f) Next();
                    else if ((flags & CollisionFlags.Sides) != 0 && phaseTime > 0.8f) Next();
                    break;
                }
                default:
                    vy = Mathf.Max(vy - 9.81f * dt, -4);
                    var landed = (controller.Move(Vector3.up * vy * dt) & CollisionFlags.Below) != 0;
                    if (landed) vy = -1;
                    if ((landed && VisualSettled) || phaseTime > 1.2f)
                    {
                        flat = Vector3.zero;
                        GetComponent<CharacterWaterInteraction>()?.OnWaterExit();
                        Release();
                    }
                    break;
            }
            flat = Vector3.zero;
        }

        private void Next() { phase++; phaseTime = 0; }

        private void Abort()
        {
            exit = ExitMode.None;
            Machine.Force(SwimState.TreadingWater);
            ActionHud.Instance?.Notice("swim-notice", "!", "Not enough room to climb out here");
        }

        // ------------------------------------------------------------------ breath (simplified gameplay)

        private void UpdateBreath(float dt)
        {
            var p = Profile;
            if (p == null || !p.UseBreath || !Machine.IsSwimming) { Breath = Mathf.Min(1, Breath + dt / 2); forceSurface = false; ShowBreath(false); return; }
            var beach = Swimming.Instance;
            var tank = beach != null && beach.Equipment == Swimming.Gear.Tank;
            if (HeadUnder && !tank) Breath = Mathf.Max(0, Breath - dt / Mathf.Max(5, p.BreathSeconds));
            else Breath = Mathf.Min(1, Breath + dt / Mathf.Max(0.5f, p.RefillSeconds));
            if (Breath <= 0 && !forceSurface)
            {
                forceSurface = true;
                if (Time.time > outOfBreathToast) { outOfBreathToast = Time.time + 5; LifeUI.Toast("Out of breath — you swim up for air."); }
            }
            if (forceSurface && !HeadUnder && Breath > 0.25f) forceSurface = false;
            ShowBreath(beach == null && Breath < 0.999f);
        }

        private void ShowBreath(bool visible)
        {
            if (!visible) return;
            var anim = GetComponent<SwimmingAnimationController>();
            var head = anim != null && anim.HeadBone != null ? anim.HeadBone.position : transform.position + Vector3.up * Height;
            ActionHud.Instance?.Breath(true, Breath, head);
        }

        // ------------------------------------------------------------------ tests / tools

        /// Places the character floating in the water at p (tests, spawn points). Not used during play.
        public void PlaceInWater(Vector3 p)
        {
            exit = ExitMode.None;
            player.Place(p);
            Detector.Sample();
            flat = Vector3.zero; vy = 0; plunging = false;
            Surface.Reset();
            if (Detector.InWater) Machine.Force(SwimState.TreadingWater);
        }
    }
}
