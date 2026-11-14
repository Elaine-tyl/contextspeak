using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Lets the player get into any DrivableVehicle and drive it. Same single-owner rule as swimming:
    /// while driving this is CafePlayer.LocomotionOverride (the walking controller is paused, the
    /// character sits in the driver's seat and moves with the car), and on getting out the previous
    /// owner (e.g. swimming) is restored and the player stands at the driver's door.
    ///
    /// Controls: E get in / out (out only when nearly stopped) · W/S accelerate, brake, reverse ·
    /// A/D steer · Space handbrake · H horn · L headlights · mouse looks around (the camera swings
    /// back behind the car on its own).
    [DefaultExecutionOrder(-45)]
    [RequireComponent(typeof(CafePlayer))]
    public sealed class VehicleDriver : MonoBehaviour, CafePlayer.ILocomotionOverride
    {
        public InputAction EnterAction = Make("Enter / exit vehicle", "<Keyboard>/e", "<Gamepad>/buttonNorth");
        public InputAction HandbrakeAction = Make("Handbrake", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        public InputAction HornAction = Make("Horn", "<Keyboard>/h", "<Gamepad>/leftStickPress");
        public InputAction LightsAction = Make("Headlights", "<Keyboard>/l", "<Gamepad>/dpad/up");
        public InputAction ThrottleAction = MakeAxis("Throttle (gamepad)", "<Gamepad>/rightTrigger", "<Gamepad>/leftTrigger");
        [Tooltip("How close (m) to a vehicle's body you must be to get in.")] public float Reach = 1.6f;
        [Tooltip("Chase camera distance multiplier.")] public float CameraDistance = 1.7f;

        public DrivableVehicle Vehicle { get; private set; }
        public DrivableVehicle Nearby { get; private set; }
        public bool Driving => Vehicle != null;
        public bool Active => Driving;
        public PlayerAnimState AnimState => PlayerAnimState.Sit;

        private CafePlayer player;
        private CharacterController body;
        private CafePlayer.ILocomotionOverride previous;
        private float scan, lookIdle, cameraYawVelocity, hintShown;
        private Vector3 cameraTarget, cameraVelocity;
        private SeatedPose pose;

        private static InputAction Make(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            return action;
        }

        private static InputAction MakeAxis(string name, string positive, string negative)
        {
            var action = new InputAction(name, InputActionType.Value);
            action.AddCompositeBinding("1DAxis").With("Positive", positive).With("Negative", negative);
            return action;
        }

        private void Awake()
        {
            player = GetComponent<CafePlayer>();
            body = GetComponent<CharacterController>();
            pose = new SeatedPose();
        }

        private void OnEnable() { EnterAction.Enable(); HandbrakeAction.Enable(); HornAction.Enable(); LightsAction.Enable(); ThrottleAction.Enable(); }
        private void OnDisable()
        {
            if (Driving) GetOut(true);
            EnterAction.Disable(); HandbrakeAction.Disable(); HornAction.Disable(); LightsAction.Disable(); ThrottleAction.Disable();
        }
        private void OnDestroy() { EnterAction.Dispose(); HandbrakeAction.Dispose(); HornAction.Dispose(); LightsAction.Dispose(); ThrottleAction.Dispose(); }

        private bool Busy()
        {
            if (player.Blocked || (player.Controls != null && player.Controls.Suspended)) return true;
            if (player.Activity != PlayerAnimState.Idle) return true;
            var swim = GetComponent<SwimmingController>();
            return swim != null && swim.OwnsMovement;
        }

        private void Update()
        {
            if (Driving)
            {
                if (EnterAction.WasPressedThisFrame())
                {
                    if (Mathf.Abs(Vehicle.Speed) < 2.5f) GetOut(false);
                    else Hint("Slow down to get out");
                }
                if (HornAction.WasPressedThisFrame()) Vehicle.Horn();
                if (LightsAction.WasPressedThisFrame()) Vehicle.ToggleHeadlights();
                return;
            }
            if ((scan -= Time.deltaTime) <= 0)
            {
                scan = 0.2f;
                Nearby = null;
                var best = Reach;
                foreach (var vehicle in DrivableVehicle.All)
                {
                    if (vehicle == null || !vehicle.isActiveAndEnabled || vehicle.Occupied || vehicle.IdleSpeed > 0.6f) continue;
                    var d = vehicle.DistanceTo(transform.position + Vector3.up * 0.9f);
                    if (d < best) { best = d; Nearby = vehicle; }
                }
            }
            var busy = Busy();
            ShowHint(Nearby != null && !busy ? "Drive the " + Nearby.Label : null);
            if (Nearby != null && !busy && EnterAction.WasPressedThisFrame()) GetIn(Nearby);
        }

        // ------------------------------------------------------------------ in / out

        public void GetIn(DrivableVehicle vehicle)
        {
            if (vehicle == null || Driving) return;
            vehicle.Measure();
            if (vehicle.IdleSpeed > 0.6f) return;
            Vehicle = vehicle;
            previous = ReferenceEquals(player.LocomotionOverride, this) ? null : player.LocomotionOverride;
            player.LocomotionOverride = this;
            player.SetVelocity(Vector3.zero, 0);
            player.Route.Clear();
            if (body != null) body.enabled = false;                                // the car's collider is the body now
            vehicle.Enter();
            lookIdle = 0;
            cameraTarget = vehicle.transform.position + Vector3.up * (vehicle.Height + 0.4f);
            cameraVelocity = Vector3.zero;
            Seat();
            player.Controls?.Clear();
            if (Time.time > hintShown)
            {
                hintShown = Time.time + 60;
                LifeUI.Toast("Driving the " + vehicle.Label + " — W/S accelerate · brake · reverse, A/D steer, Space handbrake, H horn, L lights");
                ActionHud.Instance?.Activity("DRIVING · " + vehicle.Label.ToUpperInvariant(), 3);
            }
        }

        public void GetOut(bool force)
        {
            var vehicle = Vehicle;
            if (vehicle == null) return;
            var spot = FreeSpot(vehicle);
            if (spot == null && !force) { Hint("No room to open the door here"); return; }
            Vehicle = null;
            vehicle.Exit();
            pose.Release();
            var model = player.Model;
            if (model != null) { model.localPosition = Vector3.zero; model.rotation = Quaternion.LookRotation(vehicle.Right * (vehicle.RightHandDrive ? 1 : -1)); }
            if (body != null) body.enabled = true;
            player.Place(spot ?? vehicle.ExitPosition + Vector3.up * 0.1f);
            player.Face(vehicle.Right * (vehicle.RightHandDrive ? 1 : -1));
            player.OverrideCameraTarget = false;
            player.CameraDistanceScale = 1;
            player.CameraFloor = float.NegativeInfinity;
            if (ReferenceEquals(player.LocomotionOverride, this)) player.LocomotionOverride = previous;
            previous = null;
            player.Controls?.Clear();
        }

        /// Door side first, then the passenger side: a spot where the standing capsule fits.
        private Vector3? FreeSpot(DrivableVehicle vehicle)
        {
            foreach (var candidate in new[] { vehicle.ExitPosition, vehicle.OtherExitPosition })
            {
                var height = body != null ? body.height : 1.7f;
                var radius = body != null ? body.radius : 0.3f;
                var feet = candidate;
                if (Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, out var ground, 3.5f, ~0, QueryTriggerInteraction.Ignore) && !ground.collider.transform.IsChildOf(vehicle.transform))
                    feet = ground.point + Vector3.up * 0.05f;
                var bottom = feet + Vector3.up * (radius + 0.02f);
                var top = feet + Vector3.up * (height - radius);
                var blocked = false;
                foreach (var c in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(vehicle.transform) && c != body) { blocked = true; break; }
                if (!blocked) return feet;
            }
            return null;
        }

        // ------------------------------------------------------------------ driving (CafePlayer.Update calls Tick)

        public void Tick(float dt)
        {
            var vehicle = Vehicle;
            if (vehicle == null) return;
            var move = player.Controls != null ? player.Controls.Movement : Vector2.zero;
            var pad = ThrottleAction.ReadValue<float>();
            var throttle = Mathf.Clamp(move.y + pad, -1, 1);
            vehicle.Drive(throttle, Mathf.Clamp(move.x, -1, 1), HandbrakeAction.IsPressed(), dt);
            var look = player.Controls != null ? player.Controls.Look : Vector2.zero;
            lookIdle = look.sqrMagnitude > 0.5f ? 0 : lookIdle + dt;
        }

        private void LateUpdate()
        {
            if (!Driving) return;
            Seat();
            var vehicle = Vehicle;
            // Chase camera: above and behind the car, swinging back behind it when the mouse is idle.
            var wanted = vehicle.transform.position + Vector3.up * (vehicle.Height + 0.45f);
            cameraTarget = Vector3.SmoothDamp(cameraTarget, wanted, ref cameraVelocity, 0.08f);
            player.OverrideCameraTarget = true;
            player.CameraTarget = cameraTarget;
            player.CameraDistanceScale = CameraDistance * Mathf.Clamp(vehicle.Length / 4.5f, 0.9f, 1.8f);
            player.CameraFloor = vehicle.transform.position.y + 0.6f;
            if (lookIdle > 1.2f)
            {
                var forward = Vector3.ProjectOnPlane(vehicle.Forward, Vector3.up);
                if (vehicle.Speed < -0.5f) forward = -forward;                             // look back when reversing
                var current = Quaternion.Euler(0, player.Yaw, 0) * Vector3.forward;
                var angle = Mathf.SmoothDampAngle(0, Vector3.SignedAngle(current, forward, Vector3.up), ref cameraYawVelocity, 0.45f);
                var model = player.Model;
                var keep = model != null ? model.rotation : Quaternion.identity;
                player.Face(Quaternion.Euler(0, angle, 0) * current);
                if (model != null) model.rotation = keep;
            }
            var hud = ActionHud.Instance;
            if (hud != null)
            {
                var gearText = vehicle.Speed < -0.3f ? "R" : Mathf.Abs(vehicle.Speed) < 0.3f && HandbrakeAction.IsPressed() ? "P" : vehicle.Speed < 0.3f && (player.Controls != null && player.Controls.Movement.y < -0.1f) ? "R" : "D";
                hud.Speed(true, vehicle.Speed, vehicle.MaxSpeed, gearText, vehicle.HeadlightsOn);
                if (Mathf.Abs(vehicle.Speed) < 2.5f) hud.Prompt("vehicle", "E", "Get out");
            }
        }

        /// Character in the driver's seat (CharacterController off, so this is not a teleport of a
        /// physics body — the car carries the driver) with a seated pose.
        private void Seat()
        {
            var vehicle = Vehicle;
            var model = player.Model;
            var hips = vehicle.SeatPosition;
            transform.position = hips - Vector3.up * 0.5f;
            if (model == null) return;
            var forward = vehicle.Forward;
            model.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, vehicle.transform.up), vehicle.transform.up);
            pose.Apply(model, hips, forward, vehicle.transform.up, vehicle.SteerAngle);
        }

        // ------------------------------------------------------------------ UI (shared action HUD)

        private void Hint(string text) => ActionHud.Instance?.Notice("vehicle-notice", "!", text);

        private void ShowHint(string text)
        {
            if (text != null) ActionHud.Instance?.Prompt("vehicle", "E", text);
        }

        // ------------------------------------------------------------------ setup

        public static void Ensure()
        {
            if (!Application.isPlaying || DrivableVehicle.All.Count == 0) return;
            foreach (var p in FindObjectsByType<CafePlayer>(FindObjectsSortMode.None))
                if (p.GetComponent<VehicleDriver>() == null) p.gameObject.AddComponent<VehicleDriver>();
        }
    }

    /// TEMPORARY procedural seated pose (no authored sitting/driving clip on the player rigs yet):
    /// holds the idle pose, puts the hips on the seat, thighs forward, shins down, hands on the wheel.
    public sealed class SeatedPose
    {
        private AuthoredMotion motion;
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private Transform boundModel;

        private static readonly string[] Names = { "Hips", "Spine", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };

        private void Bind(Transform model)
        {
            AuthoredMotion active = null;
            foreach (var m in model.GetComponentsInChildren<AuthoredMotion>(false)) { active = m; break; }
            if (active == motion && boundModel == model && bones.Count > 0) return;
            Release();
            motion = active;
            boundModel = model;
            bones.Clear();
            foreach (var t in (motion != null ? motion.transform : model).GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                var colon = n.LastIndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                foreach (var wanted in Names) if (n == wanted && !bones.ContainsKey(wanted)) bones[wanted] = t;
            }
        }

        public void Release()
        {
            if (motion != null) { motion.Override = null; motion.OverrideTime = -1; }
        }

        public void Apply(Transform model, Vector3 seat, Vector3 forward, Vector3 up, float steer)
        {
            Bind(model);
            if (motion != null && motion.Idle != null) { motion.Override = motion.Idle; motion.OverrideTime = 0; }
            if (!bones.TryGetValue("Hips", out var hips)) return;
            // Hips onto the seat.
            model.position += seat - hips.position;
            forward = Vector3.ProjectOnPlane(forward, up).normalized;
            var right = Vector3.Cross(up, forward);
            var wheel = Quaternion.AngleAxis(steer * 1.6f, -forward);
            Aim("LeftUpLeg", "LeftLeg", forward * 0.95f - right * 0.08f + up * 0.05f);
            Aim("RightUpLeg", "RightLeg", forward * 0.95f + right * 0.08f + up * 0.05f);
            Aim("LeftLeg", "LeftFoot", -up * 0.85f + forward * 0.45f);
            Aim("RightLeg", "RightFoot", -up * 0.85f + forward * 0.45f);
            Aim("LeftArm", "LeftForeArm", forward * 0.7f - up * 0.45f - right * 0.15f);
            Aim("RightArm", "RightForeArm", forward * 0.7f - up * 0.45f + right * 0.15f);
            Aim("LeftForeArm", "LeftHand", wheel * (forward * 0.8f + up * 0.25f + right * 0.12f));
            Aim("RightForeArm", "RightHand", wheel * (forward * 0.8f + up * 0.25f - right * 0.12f));
        }

        private void Aim(string bone, string child, Vector3 direction)
        {
            if (!bones.TryGetValue(bone, out var b) || !bones.TryGetValue(child, out var c)) return;
            var current = c.position - b.position;
            if (current.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.FromToRotation(current.normalized, direction.normalized) * b.rotation;
        }
    }
}
