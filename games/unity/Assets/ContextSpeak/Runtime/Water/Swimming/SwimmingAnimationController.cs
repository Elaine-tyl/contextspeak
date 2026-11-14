using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Swimming animation for the supplied rigs (legacy Animation clips played through AuthoredMotion,
    /// the same path walking uses), plus posture: body pitch/roll, and the animation-specific buoyancy
    /// offset that puts the hips (or, treading, the chin) at the right height relative to the water.
    ///
    /// Animation states → sources (CurrentAnimation shows which one is live):
    ///   SwimForward / SwimFast / SwimTurnLeft / SwimTurnRight / Dive / UnderwaterSwim / Surface
    ///       → authored swim cycle from the supplied FBX ("swimming_to_edge" loop part), with speed,
    ///         banking and pitch per state;
    ///   ExitWater / ClimbPoolLadder → authored edge-reach from the same FBX, then a pull-up;
    ///   TreadWater, BackFloat, pull-up and ladder steps → TEMPORARY procedural animation (bone aiming
    ///         over the idle pose) until matching clips are added to the clip set — never presented as
    ///         authored motion.
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(SwimmingController))]
    public sealed class SwimmingAnimationController : MonoBehaviour
    {
        public SwimClipSet[] ClipSets = new SwimClipSet[0];
        [Tooltip("Body pitch while diving / surfacing (°).")] public float DivePitch = 38, SurfacePitch = 18;
        [Tooltip("Banking per °/s of turn rate.")] public float BankPerDegree = 0.1f;
        public float MaxBank = 20;

        public string CurrentAnimation { get; private set; } = "—";
        public bool UsingTemporary { get; private set; }
        public Transform HeadBone => bones.TryGetValue("Head", out var b) ? b : null;
        public Transform Bone(string name) => bones.TryGetValue(name, out var b) ? b : null;
        public SwimClipSet ActiveSet => set;
        /// Natural climbing speed (m/s) of the ladder clip, so feet and hands match the rungs.
        public float LadderClimbSpeed => set != null && set.ClimbLadder != null && set.ClimbRise > 0.1f ? Mathf.Clamp(set.ClimbRise / set.ClimbLadder.length, 0.3f, 0.9f) : 0.6f;
        public AuthoredMotion Motion => motion;

        private SwimmingController swim;
        private CafePlayer player;
        private AuthoredMotion motion;
        private SwimClipSet set;
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private Quaternion rotation;
        private Vector3 offsetTarget;
        private bool owning;
        private float clock;
        private static SwimClipSet[] loaded;

        private static readonly string[] BoneNames =
        {
            "Hips", "Spine", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
        };

        private void Awake()
        {
            swim = GetComponent<SwimmingController>();
            player = GetComponent<CafePlayer>();
        }

        private SwimClipSet[] Sets
        {
            get
            {
                if (ClipSets != null && ClipSets.Length > 0) return ClipSets;
                return loaded ??= Resources.LoadAll<SwimClipSet>("Swimming");
            }
        }

        /// The playable model can switch (female/male): find the active rig again when it changes.
        private void Refresh()
        {
            var model = player.Model;
            AuthoredMotion active = null;
            if (model != null)
                foreach (var candidate in model.GetComponentsInChildren<AuthoredMotion>(false)) { active = candidate; break; }
            if (active == motion && (motion != null || bones.Count > 0)) return;
            if (motion != null) { motion.Override = null; motion.OverrideTime = -1; }
            motion = active;
            set = null;
            if (motion != null)
                foreach (var candidate in Sets) if (candidate != null && candidate.ModelName == motion.ModelName) { set = candidate; break; }
            bones.Clear();
            var root = motion != null ? motion.transform : model != null ? model : transform;
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                var name = bone.name;
                var colon = name.LastIndexOf(':');
                if (colon >= 0) name = name.Substring(colon + 1);
                var underscore = name.LastIndexOf('_');
                foreach (var wanted in BoneNames)
                    if (!bones.ContainsKey(wanted) && (name == wanted || underscore >= 0 && name.Substring(underscore + 1) == wanted))
                        bones[wanted] = bone;
            }
        }

        private void LateUpdate()
        {
            if (player.Model == null) return;
            Refresh();
            var model = player.Model;
            var owns = swim.OwnsMovement;
            if (!owns)
            {
                if (owning)
                {
                    owning = false;
                    if (motion != null) { motion.Override = null; motion.OverrideTime = -1; }
                }
                swim.VisualSettled = true;
                CurrentAnimation = "—";
                return;
            }
            if (!owning) { owning = true; rotation = model.rotation; offsetTarget = model.localPosition; }
            var dt = Time.deltaTime;
            clock += dt;
            var p = swim.Profile;
            var blend = (p != null ? p.AnimationBlend : 0.3f) * swim.Tuning.AnimationBlendScale;
            var state = swim.State;
            var speed = swim.HorizontalSpeed;
            var exit = swim.Exit;

            // ---- choose the clip / pose
            AnimationClip clip = null;
            float clipSpeed = 1, poseTime = -1;
            string label;
            var procedural = Procedural.None;
            var swimClip = set != null ? set.Swim : null;
            var anchor = Anchor.Hips;
            var pitch = 0f;
            var supine = false;
            var lean = 0f;
            var bank = Mathf.Clamp(-swim.TurnRate * BankPerDegree, -MaxBank, MaxBank);
            if (exit == SwimmingController.ExitMode.LadderDown)
            {
                anchor = Anchor.None;
                bank = 0;
                var phase = swim.ExitPhaseIndex;
                if (phase == 0 && motion != null && motion.Walk != null) { clip = motion.Walk; clipSpeed = 0.6f; label = "ClimbPoolLadder (stepping onto the ladder)"; }
                else if (set != null && set.ClimbLadder != null)
                {
                    clip = set.ClimbLadder;
                    clipSpeed = (set.ClimbAuthoredDown ? 1 : -1) * (phase == 1 ? 0.4f : 1f);
                    label = "ClimbPoolLadder (down, authored)";
                }
                else { clip = motion != null ? motion.Walk : null; clipSpeed = 0.5f; label = "ClimbPoolLadder (TEMPORARY: slowed walk steps)"; }
            }
            else if (exit != SwimmingController.ExitMode.None)
            {
                anchor = Anchor.None;
                bank = 0;
                var phase = swim.ExitPhaseIndex;
                var ladder = exit == SwimmingController.ExitMode.Ladder;
                if (phase <= 1 && set != null && set.EdgeReach != null) { clip = set.EdgeReach; label = ladder ? "ClimbPoolLadder (edge reach, authored)" : "ExitWater (edge reach, authored)"; }
                else if (ladder && phase == 2 && set != null && set.ClimbLadder != null)
                {
                    clip = set.ClimbLadder;
                    clipSpeed = set.ClimbAuthoredDown ? -1 : 1;                    // a climbing-down clip played backwards climbs up
                    label = "ClimbPoolLadder (authored)";
                }
                else if (!ladder && phase == 2 && set != null && set.PullOut != null)
                {
                    clip = set.PullOut;
                    // Spread the push-up over the time the body takes to rise out of the water.
                    clipSpeed = Mathf.Clamp(set.PullOut.length / 1.1f, 0.4f, 2f);
                    label = "ExitWater (pull-out, authored)";
                }
                else if (ladder && phase == 2 && motion != null && motion.Walk != null) { clip = motion.Walk; clipSpeed = 0.55f; label = "ClimbPoolLadder (TEMPORARY: slowed walk steps)"; UsingTemporary = true; }
                else if (phase <= 3 && motion != null && motion.Crouch != null) { clip = motion.Crouch; poseTime = 0; label = "ExitWater (crouch, standing up)"; }
                else { clip = motion != null ? motion.Idle : null; poseTime = 0; label = "ExitWater (standing)"; }
            }
            else if (state == SwimState.ExitingWater)
            {
                anchor = Anchor.None;
                bank = 0;
                clip = null;                                           // AuthoredMotion walk/idle while wading out
                label = "ExitWater (wading out, authored walk)";
            }
            else if (state == SwimState.TreadingWater || (state == SwimState.EnteringWater && speed < 0.3f && swim.VerticalVelocity > -1.5f))
            {
                anchor = Anchor.Chin;
                bank = 0;
                if (set != null && set.TreadWater != null) { clip = set.TreadWater; label = "TreadWater (authored)"; }
                else { clip = motion != null ? motion.Idle : null; poseTime = 0; procedural = Procedural.Tread; label = "TreadWater (TEMPORARY procedural)"; }
                lean = 6;
            }
            else if (state == SwimState.BackFloating)
            {
                anchor = Anchor.Back;
                bank = 0;
                if (set != null && set.BackFloat != null) { clip = set.BackFloat; label = "BackFloat (authored)"; }
                else { clip = motion != null ? motion.Idle : null; poseTime = 0; procedural = Procedural.BackFloat; supine = true; label = "BackFloat (TEMPORARY procedural)"; }
            }
            else
            {
                clip = state == SwimState.UnderwaterSwimming && set != null && set.UnderwaterSwim != null ? set.UnderwaterSwim : swimClip;
                var reference = p != null ? p.SurfaceSpeed : 1.9f;
                switch (state)
                {
                    case SwimState.Diving:
                        pitch = -DivePitch; label = "Dive"; clipSpeed = 1.1f; anchor = Anchor.Centre; break;
                    case SwimState.UnderwaterSwimming:
                        pitch = Mathf.Clamp(Mathf.Atan2(swim.VerticalVelocity, Mathf.Max(0.35f, speed)) * Mathf.Rad2Deg, -55, 55);
                        clipSpeed = Mathf.Clamp(Mathf.Max(speed, Mathf.Abs(swim.VerticalVelocity)) / Mathf.Max(0.5f, p != null ? p.UnderwaterSpeed : 1.5f), 0.45f, 1.4f);
                        label = "UnderwaterSwim"; anchor = Anchor.Centre; break;
                    case SwimState.Surfacing:
                        pitch = SurfacePitch; label = "Surface"; clipSpeed = 0.9f; anchor = Anchor.Hips; break;
                    case SwimState.EnteringWater:
                        label = "SwimForward (entering)"; clipSpeed = 0.8f; break;
                    default:
                        clipSpeed = Mathf.Clamp(speed / reference, 0.55f, 1.6f);
                        label = Mathf.Abs(bank) > 8 ? (bank > 0 ? "SwimTurnLeft" : "SwimTurnRight") : speed > reference * 1.15f ? "SwimFast" : "SwimForward";
                        pitch = Mathf.Clamp(swim.VerticalVelocity * 8, -10, 10);
                        break;
                }
                if (clip == null)
                {
                    clip = motion != null ? motion.Idle : null; poseTime = 0; procedural = Procedural.Crawl;
                    label += " (TEMPORARY procedural — no swim clip for " + (motion != null ? motion.ModelName : "this model") + ")";
                }
                else label += " (authored)";
            }
            UsingTemporary = label.Contains("TEMPORARY");
            CurrentAnimation = label;

            if (motion != null)
            {
                motion.Override = clip;
                motion.OverrideSpeed = clipSpeed;
                motion.OverrideFade = Mathf.Max(0.05f, blend);
                motion.OverrideTime = poseTime;
            }

            // ---- posture: heading, pitch, bank (smoothed), with the clip's own facing correction
            var heading = swim.Heading.sqrMagnitude > 0.01f ? swim.Heading : model.forward;
            var yawFix = set != null && clip == swimClip ? set.SwimYaw : 0;
            var posture = Quaternion.LookRotation(heading) * Quaternion.Euler(0, yawFix, 0) * Quaternion.Euler(-pitch + lean, 0, bank);
            if (supine) posture = Quaternion.LookRotation(heading) * Quaternion.Euler(-82, 0, 0);
            if (procedural == Procedural.Crawl) posture = Quaternion.LookRotation(heading) * Quaternion.Euler(80 - pitch, 0, bank);
            rotation = Quaternion.Slerp(rotation, posture, 1 - Mathf.Exp(-7 * dt));
            model.rotation = rotation;

            // ---- animation-specific buoyancy offset: put the anchor bone at its height relative to the waterline
            var floatTop = transform.position.y + swim.FloatLine;            // the waterline as the physics body sees it
            Vector3 wanted = Vector3.zero;
            var hips = Bone("Hips");
            var head = HeadBone;
            var parent = model.parent != null ? model.parent : transform;
            switch (anchor)
            {
                case Anchor.Hips when hips != null:
                    wanted = Recentre(hips, floatTop - (p != null ? p.SwimHipsDepth : 0.08f), parent, model); break;
                case Anchor.Back when head != null:
                    wanted = Recentre(head, floatTop + (p != null ? p.BackFloatFaceAbove : 0.03f), parent, model, hips); break;
                case Anchor.Chin when head != null:
                    wanted = Recentre(head, floatTop + (p != null ? p.TreadChinAbove : 0.06f) + 0.12f, parent, model, hips); break;
                case Anchor.Centre when hips != null:
                    wanted = Recentre(hips, transform.position.y + swim.Height * 0.55f, parent, model); break;
                default:
                    wanted = Vector3.zero; break;
            }
            offsetTarget = Vector3.Lerp(offsetTarget, wanted, 1 - Mathf.Exp(-dt / 0.3f));
            model.localPosition = Vector3.Lerp(model.localPosition, offsetTarget, 1 - Mathf.Exp(-10 * dt));

            // ---- temporary procedural limb motion over the held idle pose
            if (procedural != Procedural.None) Overlay(procedural, model);

            var upright = Quaternion.LookRotation(heading);
            swim.VisualSettled = model.localPosition.magnitude < 0.06f && Quaternion.Angle(model.rotation, upright) < 10;
        }

        /// Model offset (parent space) that moves `bone` to height y and horizontally over the capsule.
        private Vector3 Recentre(Transform bone, float y, Transform parent, Transform model, Transform horizontalReference = null)
        {
            var reference = horizontalReference != null ? horizontalReference : bone;
            var delta = new Vector3(transform.position.x - reference.position.x, y - bone.position.y, transform.position.z - reference.position.z);
            return model.localPosition + parent.InverseTransformVector(delta);
        }

        private enum Anchor { None, Hips, Back, Chin, Centre }
        private enum Procedural { None, Tread, BackFloat, Crawl }

        // ------------------------------------------------------------------ temporary procedural motion

        private void Overlay(Procedural kind, Transform model)
        {
            var f = model.forward; var r = model.right; var u = model.up;
            var w = clock * Mathf.PI * 2;
            switch (kind)
            {
                case Procedural.Tread:
                {
                    var t = w * 0.85f;
                    // Sculling arms in front at shoulder depth, eggbeater-style alternating leg kicks.
                    Limb("LeftArm", "LeftForeArm", -r * 0.8f + f * (0.35f + 0.2f * Mathf.Sin(t)) - u * 0.35f);
                    Limb("LeftForeArm", "LeftHand", -r * (0.35f + 0.25f * Mathf.Sin(t)) + f * 0.8f - u * 0.1f);
                    Limb("RightArm", "RightForeArm", r * 0.8f + f * (0.35f + 0.2f * Mathf.Sin(t + Mathf.PI)) - u * 0.35f);
                    Limb("RightForeArm", "RightHand", r * (0.35f + 0.25f * Mathf.Sin(t + Mathf.PI)) + f * 0.8f - u * 0.1f);
                    var k = w * 0.95f;
                    Limb("LeftUpLeg", "LeftLeg", -u * 0.9f + f * (0.3f + 0.22f * Mathf.Sin(k)) - r * 0.14f);
                    Limb("LeftLeg", "LeftFoot", -u * 0.85f - f * (0.25f + 0.3f * Mathf.Sin(k + 1.2f)) - r * 0.08f);
                    Limb("RightUpLeg", "RightLeg", -u * 0.9f + f * (0.3f + 0.22f * Mathf.Sin(k + Mathf.PI)) + r * 0.14f);
                    Limb("RightLeg", "RightFoot", -u * 0.85f - f * (0.25f + 0.3f * Mathf.Sin(k + Mathf.PI + 1.2f)) + r * 0.08f);
                    break;
                }
                case Procedural.BackFloat:
                {
                    // Model is supine: model.up points toward the head, model.forward points up out of the water.
                    var t = w * 0.35f;
                    Limb("LeftArm", "LeftForeArm", -r * 0.85f + u * 0.3f - f * 0.08f);
                    Limb("LeftForeArm", "LeftHand", -r * 0.8f + u * (0.3f + 0.15f * Mathf.Sin(t)) - f * 0.1f);
                    Limb("RightArm", "RightForeArm", r * 0.85f + u * 0.3f - f * 0.08f);
                    Limb("RightForeArm", "RightHand", r * 0.8f + u * (0.3f + 0.15f * Mathf.Sin(t + 0.8f)) - f * 0.1f);
                    Limb("LeftUpLeg", "LeftLeg", -u * 0.95f - r * 0.16f - f * 0.15f);
                    Limb("LeftLeg", "LeftFoot", -u * 0.95f - r * 0.1f - f * (0.08f + 0.04f * Mathf.Sin(t * 1.3f)));
                    Limb("RightUpLeg", "RightLeg", -u * 0.95f + r * 0.16f - f * 0.15f);
                    Limb("RightLeg", "RightFoot", -u * 0.95f + r * 0.1f - f * (0.08f + 0.04f * Mathf.Sin(t * 1.3f + 1)));
                    break;
                }
                case Procedural.Crawl:
                {
                    // Only when no swim clip exists at all: model pitched face-down; alternate arm reaches + flutter kick.
                    var t = w * 0.8f;
                    var left = Mathf.Sin(t); var right = Mathf.Sin(t + Mathf.PI);
                    Limb("LeftArm", "LeftForeArm", u * (0.6f + 0.4f * left) - f * (0.6f - 0.6f * left) - r * 0.3f);
                    Limb("RightArm", "RightForeArm", u * (0.6f + 0.4f * right) - f * (0.6f - 0.6f * right) + r * 0.3f);
                    Limb("LeftUpLeg", "LeftLeg", -u + f * 0.15f * Mathf.Sin(t * 2));
                    Limb("RightUpLeg", "RightLeg", -u - f * 0.15f * Mathf.Sin(t * 2));
                    break;
                }
            }
        }

        /// Turns `bone` so it points along `direction` (world), keeping the child chain attached.
        private void Limb(string boneName, string childName, Vector3 direction)
        {
            var bone = Bone(boneName); var child = Bone(childName);
            if (bone == null || child == null || direction.sqrMagnitude < 1e-6f) return;
            var current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current.normalized, direction.normalized) * bone.rotation;
        }
    }
}
