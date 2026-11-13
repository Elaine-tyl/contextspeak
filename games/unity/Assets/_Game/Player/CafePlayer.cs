using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Animation states the controller reports. Activities (Sit … UseObject) are set by gameplay systems.
    public enum PlayerAnimState { Idle, Walk, Run, Sprint, Jump, Fall, Land, Sit, Talk, Fish, Drink, Eat, Shop, UseObject, Crouch }
    public enum Gait { Walk, Run }

    /// Third-person controller shared by every district. All tuning values are editable in the Inspector.
    [RequireComponent(typeof(CharacterController), typeof(WorldInput))]
    public sealed class CafePlayer : MonoBehaviour
    {
        public Camera View;
        public Transform Model;
        public Transform ConversationPartner;
        public bool Blocked;

        [Header("Speeds (metres per second)")]
        [Tooltip("Ctrl held, Caps Lock toggled, or when DefaultGait is Walk.")]
        public float WalkSpeed = 4.5f;
        [Tooltip("Default jog speed.")]
        public float RunSpeed = 7f;
        [Tooltip("While holding Shift.")]
        public float SprintSpeed = 9f;
        public float CrouchSpeed = 2.2f;
        public Gait DefaultGait = Gait.Walk;
        [Tooltip("How quickly the avatar reaches its target speed (m/s²).")]
        public float Acceleration = 22f;
        [Tooltip("How quickly the avatar stops (m/s²).")]
        public float Deceleration = 30f;
        [Range(0, 1)] public float AirControl = 0.45f;

        [Header("Jumping and gravity")]
        [Tooltip("Jump height in metres (Space).")]
        public float JumpHeight = 1.1f;
        public float Gravity = -22f;
        [Tooltip("A jump still counts this long after walking off a ledge.")]
        public float CoyoteTime = 0.15f;
        [Tooltip("A jump pressed this long before landing still happens.")]
        public float JumpBuffer = 0.12f;
        [Tooltip("Steeper ground than this makes the avatar slide.")]
        public float SlopeLimit = 46f;
        public float StepOffset = 0.35f;

        [Header("Crouch")]
        public float StandingHeight = 1.7f;
        public float CrouchHeight = 1.1f;

        [Header("Camera")]
        [Range(0.1f, 3f)] public float MouseSensitivity = 1f;
        public bool InvertY;
        public float MinDistance = 2.2f, MaxDistance = 8f;
        public float MinPitch = -10f, MaxPitch = 65f;
        [Tooltip("Sphere radius used to keep the camera out of walls.")]
        public float CameraCollisionRadius = 0.28f;
        public LayerMask CameraCollisionMask = ~0;

        public readonly Queue<Vector3> Route = new Queue<Vector3>();
        public Transform CameraPosition, CameraAim;
        public WorldInput Controls { get; private set; }
        public bool Grounded => controller != null && controller.isGrounded;
        public bool Crouching { get; private set; }
        public float Speed => horizontal.magnitude;
        public PlayerAnimState State { get; private set; }
        /// Gameplay activity override (sitting, fishing, talking…). Idle means "none".
        public PlayerAnimState Activity = PlayerAnimState.Idle;
        /// Fired on each footstep with the surface name ("wood", "grass", "stone", "water", "gravel").
        public event Action<string, float> Footstep;

        private CharacterController controller;
        private Animator animator;
        private float yaw = 180, pitch = 24, distance = 5.5f, currentDistance = 5.5f;
        private float stride, vertical, sinceGrounded = 1, sinceJumpPressed = 1, squash, landTimer;
        private Vector3 horizontal, groundNormal = Vector3.up;
        private Vector3 entryPosition;
        private readonly RaycastHit[] cameraHits = new RaycastHit[32];
        private bool wasGrounded = true;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            entryPosition = transform.position;
            Controls = GetComponent<WorldInput>();
            controller.slopeLimit = SlopeLimit;
            controller.stepOffset = Mathf.Max(controller.stepOffset, StepOffset);
            StandingHeight = controller.height > 0 ? controller.height : StandingHeight;
            if (Model != null) animator = Model.GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            // Recover from walking beyond the terrain or reopening a save made during a fall.
            if (transform.position.y < -20)
            {
                Place(entryPosition);
                return;
            }
            var dt = Time.deltaTime;
            var direction = Vector3.zero;
            var targetSpeed = 0f;
            var followingRoute = false;
            if (!Blocked && Activity == PlayerAnimState.Idle)
            {
                if (Controls.Crouch) ToggleCrouch();
                var movement = Controls.Movement;
                var input = new Vector3(movement.x, 0, movement.y);
                if (input.sqrMagnitude > 0) Route.Clear();
                if (Route.Count > 0)
                {
                    var delta = Route.Peek() - transform.position;
                    delta.y = 0;
                    followingRoute = true;
                    if (delta.magnitude < 0.2f) { Route.Dequeue(); horizontal = Vector3.zero; }
                    else
                    {
                        direction = delta.normalized;
                        targetSpeed = Mathf.Min(RunSpeed, delta.magnitude / Mathf.Max(dt, 0.001f));
                    }
                }
                else if (input.sqrMagnitude > 0.0001f)
                {
                    direction = Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(input, 1);
                    targetSpeed = InputSpeed(Crouching, Controls.Sprint, Controls.Walk);
                    targetSpeed *= Mathf.Clamp01(input.magnitude); // analogue sticks walk slowly at small tilt
                }
            }

            // Smooth acceleration and deceleration, with reduced control in the air.
            var target = direction * targetSpeed;
            var rate = (target.sqrMagnitude > horizontal.sqrMagnitude ? Acceleration : Deceleration) * (controller.isGrounded ? 1 : AirControl);
            // Guided routes follow each safe segment exactly. Smoothing lateral velocity across a
            // sharp corner cuts inside the NavMesh path and can strand the player against furniture.
            horizontal = followingRoute
                ? direction * Mathf.MoveTowards(horizontal.magnitude, targetSpeed, rate * dt)
                : Vector3.MoveTowards(horizontal, target, rate * dt);

            // Gravity, coyote time and jump buffering.
            if (controller.isGrounded) sinceGrounded = 0; else sinceGrounded += dt;
            sinceJumpPressed = !Blocked && Activity == PlayerAnimState.Idle && Controls.Jump ? 0 : sinceJumpPressed + dt;
            if (controller.isGrounded && vertical < 0) vertical = -2f; // keeps contact when walking down slopes and stairs
            if (sinceJumpPressed < JumpBuffer && sinceGrounded < CoyoteTime && !Crouching)
            {
                vertical = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                sinceGrounded = CoyoteTime;
                sinceJumpPressed = JumpBuffer;
                squash = 1;
            }
            vertical += Gravity * dt;

            // Slide down slopes that are too steep to stand on.
            var slide = Vector3.zero;
            var angle = Vector3.Angle(groundNormal, Vector3.up);
            if (controller.isGrounded && angle > SlopeLimit)
                slide = new Vector3(groundNormal.x, 0, groundNormal.z).normalized * Mathf.Lerp(2, 7, (angle - SlopeLimit) / 30f);

            controller.Move((horizontal + slide + Vector3.up * vertical) * dt);
            groundNormal = controller.isGrounded ? groundNormal : Vector3.up;

            if (controller.isGrounded && !wasGrounded)
            {
                // Landing: a small squash so the jump has weight, and a louder footstep.
                if (vertical < -6f) { squash = 0.6f; landTimer = 0.18f; }
                Footstep?.Invoke(Surface(), 1f);
                vertical = -2f;
            }
            wasGrounded = controller.isGrounded;
            landTimer -= dt;

            Animate(dt, direction);
            if (!Blocked)
            {
                var sensitivity = MouseSensitivity;
                yaw += Controls.Look.x * 0.12f * sensitivity;
                pitch = Mathf.Clamp(pitch - Controls.Look.y * 0.1f * sensitivity * (InvertY ? -1 : 1), MinPitch, MaxPitch);
                distance = Mathf.Clamp(distance - Controls.Zoom * 0.35f, MinDistance, MaxDistance);
            }
        }

        private void Animate(float dt, Vector3 direction)
        {
            var speed = horizontal.magnitude;
            State = Activity != PlayerAnimState.Idle ? Activity
                : Blocked ? PlayerAnimState.Talk
                : !controller.isGrounded ? (vertical > 0 ? PlayerAnimState.Jump : PlayerAnimState.Fall)
                : landTimer > 0 ? PlayerAnimState.Land
                : Crouching ? PlayerAnimState.Crouch
                : speed < 0.15f ? PlayerAnimState.Idle
                : speed <= WalkSpeed + 0.2f ? PlayerAnimState.Walk
                : speed <= RunSpeed + 0.2f ? PlayerAnimState.Run : PlayerAnimState.Sprint;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger("State", (int)State);
                animator.SetFloat("Speed", speed);
            }
            if (Model == null) return;
            if (direction.sqrMagnitude > 0.01f)
                Model.rotation = Quaternion.Slerp(Model.rotation, Quaternion.LookRotation(direction), dt * 14);
            if (controller.isGrounded && speed > 0.3f)
            {
                var before = Mathf.Floor(stride / Mathf.PI);
                stride += dt * Mathf.Lerp(8, 17, speed / SprintSpeed);
                if (Mathf.Floor(stride / Mathf.PI) != before) Footstep?.Invoke(Surface(), Mathf.Clamp01(speed / SprintSpeed));
            }
            // Procedural fallback motion for models without an Animator: bob, squash, crouch and sit offsets.
            var bob = controller.isGrounded && speed > 0.3f ? Mathf.Abs(Mathf.Sin(stride)) * Mathf.Lerp(0.03f, 0.075f, speed / SprintSpeed) : 0;
            var lower = State == PlayerAnimState.Sit ? -0.42f : Crouching ? -0.25f : 0;
            Model.localPosition = Vector3.Lerp(Model.localPosition, new Vector3(0, bob + lower, 0), dt * 12);
            squash = Mathf.MoveTowards(squash, 0, dt * 4f);
            var stretch = 1 + Mathf.Sin(squash * Mathf.PI) * 0.08f;
            var crouchScale = Crouching ? 0.78f : 1;
            Model.localScale = new Vector3(1 / Mathf.Sqrt(stretch), stretch * crouchScale, 1 / Mathf.Sqrt(stretch));
        }

        public float InputSpeed(bool crouching, bool sprinting, bool walking)
        {
            if (crouching) return CrouchSpeed;
            if (walking) return WalkSpeed;
            if (sprinting) return SprintSpeed;
            return DefaultGait == Gait.Walk ? WalkSpeed : RunSpeed;
        }

        private void ToggleCrouch()
        {
            if (Crouching)
            {
                // Only stand up when there is head room.
                var top = transform.position + Vector3.up * (CrouchHeight - controller.radius);
                if (Physics.SphereCast(top, controller.radius * 0.9f, Vector3.up, out _, StandingHeight - CrouchHeight + 0.05f, ~0, QueryTriggerInteraction.Ignore)) return;
                Crouching = false;
                controller.height = StandingHeight;
            }
            else
            {
                Crouching = true;
                controller.height = CrouchHeight;
            }
            controller.center = new Vector3(0, controller.height / 2f, 0);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.1f) groundNormal = hit.normal;
        }

        /// Surface under the feet, from collider/material/object names. Used for footstep sounds.
        public string Surface()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return "stone";
            var name = (hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.name + " " : "") + hit.collider.gameObject.name;
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null) name += " " + renderer.sharedMaterial.name;
            name = name.ToLowerInvariant();
            foreach (var surface in new[] { "water", "wood", "grass", "gravel", "stone" })
                if (name.Contains(surface)) return surface;
            if (name.Contains("floor") || name.Contains("pier") || name.Contains("deck") || name.Contains("bridge") || name.Contains("oak")) return "wood";
            if (name.Contains("lawn") || name.Contains("meadow") || name.Contains("ground")) return "grass";
            if (name.Contains("path") || name.Contains("trail")) return "gravel";
            return "stone";
        }

        private void LateUpdate()
        {
            if (View == null) return;
            var target = transform.position + Vector3.up * (Crouching ? 0.95f : 1.35f);
            var wanted = distance;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            if (ConversationPartner != null)
            {
                // Over-the-shoulder framing of the NPC during conversations.
                target = ConversationPartner.position + Vector3.up * 1.5f;
                var away = transform.position - ConversationPartner.position;
                away.y = 0;
                away.Normalize();
                var shoulder = Vector3.Cross(Vector3.up, away);
                var framed = target + away * 3.8f + shoulder * 1.8f + Vector3.up;
                rotation = Quaternion.LookRotation(target - framed);
                wanted = Vector3.Distance(framed, target);
            }
            // Keep the camera out of walls: pull it in to the first obstacle, then ease back out.
            var back = rotation * Vector3.back;
            var hits = Physics.SphereCastNonAlloc(target, CameraCollisionRadius, back, cameraHits, wanted, CameraCollisionMask, QueryTriggerInteraction.Ignore);
            for (var index = 0; index < hits; index++)
            {
                var hit = cameraHits[index];
                // Characters may pass in front of the lens without collapsing the camera distance.
                if (hit.collider.GetComponentInParent<CharacterController>() != null
                    || hit.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null
                    || hit.collider.GetComponentInParent<ContextSpeak.Slice.LearningNpc>() != null) continue;
                wanted = Mathf.Min(wanted, Mathf.Max(0.6f, hit.distance - 0.05f));
            }
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, Time.deltaTime * 6f);
            var desired = target + back * currentDistance;
            // Cinemachine is the only writer to the real camera transform.
            if (CameraPosition != null) CameraPosition.position = desired;
            if (CameraAim != null) CameraAim.position = target;
        }

        public void Place(Vector3 position)
        {
            Route.Clear();
            vertical = 0;
            horizontal = Vector3.zero;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        /// Face a direction instantly (e.g. when sitting down or casting toward the pond).
        public void Face(Vector3 forward)
        {
            forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) return;
            if (Model != null) Model.rotation = Quaternion.LookRotation(forward);
            yaw = Quaternion.LookRotation(forward).eulerAngles.y;
        }
    }
}
