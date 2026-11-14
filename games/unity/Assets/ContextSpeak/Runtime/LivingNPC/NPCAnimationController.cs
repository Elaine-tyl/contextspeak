using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Extra clips for one NPC model, retargeted by the setup tool from whichever supplied FBX has them.
    [CreateAssetMenu(menuName = "ContextSpeak/NPC/Animation Set", fileName = "NPC Animations")]
    public sealed class NPCAnimationSet : ScriptableObject
    {
        public string ModelName;
        [Tooltip("Seated pose (first frame of Sit_to_Stand).")] public AnimationClip Sit;
        public AnimationClip Wave, Talk, LookAround, Dance;
        [Tooltip("Ground speed (m/s) at which the walk clip's feet do not slide.")] public float WalkClipSpeed = 1.35f;
        public float RunClipSpeed = 3.6f;
        [TextArea] public string Notes;
    }

    public enum NPCAction
    {
        None, Sit, Lie, Wave, Talk, Listen, Nod, ShakeHead, Shrug, Point, LookAround, Stretch, Yawn,
        Phone, Drink, Eat, Read, Type, Cook, Wipe, OperateMachine, Browse, Pay, Map, Photo, CarryBag, PickUp, Board,
    }

    /// Full-body animation for living NPCs on the supplied Meshy/Mixamo rigs (legacy clips through
    /// AuthoredMotion, the same path the player uses):
    ///  • locomotion clip speed matched to the real ground speed (no foot sliding);
    ///  • authored actions where a clip exists (sit, wave, talk gesture, look around) — retargeted;
    ///  • everyday actions as procedural limb poses with props (cup, phone, book, laptop, bag, map,
    ///    umbrella) — reported as TEMPORARY until authored clips are added;
    ///  • gaze: head turns with natural limits, short glances and saccades, never a constant stare;
    ///  • breathing and small posture shifts while idle.
    /// Facial blinking / visemes need facial blendshapes, which these models do not have (reported).
    public sealed class NPCAnimationController : MonoBehaviour
    {
        public NPCAction Action { get; private set; }
        public string Current { get; private set; } = "Idle";
        public bool Temporary { get; private set; }
        public AuthoredMotion Motion => motion;
        public NPCAnimationSet Set => set;
        public Transform Bone(string name) => bones.TryGetValue(name, out var b) ? b : null;

        private AuthoredMotion motion;
        private NPCAnimationSet set;
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private readonly Dictionary<string, GameObject> props = new Dictionary<string, GameObject>();
        private Transform model;
        private float actionTime, clock, shift, shiftTarget, shiftTimer, seatHeight;
        private Vector3? lookTarget;
        private float lookWeight, saccadeTimer;
        private Vector3 saccade;
        private Quaternion headRest;
        private static NPCAnimationSet[] sets;

        private static readonly string[] Names =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
        };

        public void Bind()
        {
            motion = GetComponentInChildren<AuthoredMotion>(true);
            model = motion != null ? motion.transform : transform.childCount > 0 ? transform.GetChild(0) : transform;
            sets ??= Resources.LoadAll<NPCAnimationSet>("LivingNPC");
            set = null;
            if (motion != null) foreach (var s in sets) if (s != null && s.ModelName == motion.ModelName) { set = s; break; }
            bones.Clear();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name; var c = n.LastIndexOf(':'); if (c >= 0) n = n.Substring(c + 1);
                foreach (var w in Names) if (n == w && !bones.ContainsKey(w)) bones[w] = t;
            }
            if (motion != null && motion.Player != null) motion.Player.cullingType = AnimationCullingType.BasedOnRenderers;
            clock = Random.value * 10;
        }

        private void Awake() { if (motion == null) Bind(); }

        public void Play(NPCAction action, float seatY = 0)
        {
            if (action == Action && Mathf.Approximately(seatY, seatHeight)) return;
            Action = action;
            actionTime = 0;
            seatHeight = seatY;
            UpdateProps();
        }

        /// Look at a world point for a while (perception decides what is interesting).
        public void LookAt(Vector3? point) { lookTarget = point; }

        /// Called every frame for full-tier NPCs, less often for background ones.
        public void Tick(float dt, float groundSpeed, bool full)
        {
            if (motion == null) return;
            clock += dt; actionTime += dt;
            // Arm-only actions keep the legs walking (phone while walking, carrying, reading a map on the move).
            var onTheMove = Speed > 0.15f && (Action == NPCAction.Phone || Action == NPCAction.Map || Action == NPCAction.CarryBag || Action == NPCAction.Talk);
            var authored = onTheMove ? null : Authored(Action);
            if (authored != null)
            {
                motion.Override = authored.Value.clip;
                motion.OverrideTime = authored.Value.hold ? authored.Value.time : -1;
                motion.OverrideFade = 0.3f;
                motion.OverrideSpeed = 1;
            }
            else if (Procedural(Action) && !onTheMove)
            {
                motion.Override = motion.Idle;
                motion.OverrideTime = 0;
            }
            else
            {
                motion.Override = null;
                motion.OverrideTime = -1;
                // Foot sliding: play the walk/run clip at the rate the body actually moves.
                var player = motion.Player;
                if (player != null)
                {
                    var walk = motion.Walk; var run = motion.Run;
                    var walkSpeed = set != null ? set.WalkClipSpeed : 1.35f; var runSpeed = set != null ? set.RunClipSpeed : 3.6f;
                    if (walk != null && player[walk.name] != null) player[walk.name].speed = Mathf.Clamp(groundSpeed / walkSpeed, 0.5f, 1.6f);
                    if (run != null && player[run.name] != null) player[run.name].speed = Mathf.Clamp(groundSpeed / runSpeed, 0.6f, 1.5f);
                }
            }
            Current = Label();
        }

        /// After the animation has been evaluated: seat height, procedural poses, gaze, breathing.
        public void Late(float dt, bool full)
        {
            if (motion == null) return;
            var sitting = Action == NPCAction.Sit || Action == NPCAction.Lie || Seated;
            var offset = Vector3.zero;
            if (Action == NPCAction.Lie)
            {
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(-88, 0, 0), 1 - Mathf.Exp(-5 * dt));
                offset = Vector3.up * (seatHeight + 0.12f);
                var hips = Bone("Hips");
                if (hips != null) offset += model.parent.InverseTransformVector(Vector3.ProjectOnPlane(model.parent.position - hips.position, Vector3.up));
            }
            else
            {
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, 1 - Mathf.Exp(-6 * dt));
                if (sitting) offset = SeatOffset();
            }
            model.localPosition = Vector3.Lerp(model.localPosition, offset, 1 - Mathf.Exp(-8 * dt));
            if (Procedural(Action)) Pose(Action);
            if (full) { Micro(dt); Gaze(dt); }
            AttachProps();
        }

        private bool Seated => Action == NPCAction.Drink && seatHeight > 0 || Action == NPCAction.Eat && seatHeight > 0 || Action == NPCAction.Type || Action == NPCAction.Read && seatHeight > 0 || Action == NPCAction.Phone && seatHeight > 0;

        private Vector3 SeatOffset()
        {
            // The sit pose's hips height above the feet, lowered so the hips rest on the seat surface.
            var hips = Bone("Hips");
            if (hips == null || seatHeight <= 0) return new Vector3(0, -0.42f, 0);
            var rootY = model.parent.position.y;
            var hipsAboveRoot = hips.position.y - (model.position.y);
            return Vector3.up * (seatHeight + 0.08f - hipsAboveRoot) + Vector3.zero * rootY;
        }

        private (AnimationClip clip, bool hold, float time)? Authored(NPCAction action)
        {
            switch (action)
            {
                case NPCAction.Sit:
                case NPCAction.Drink when seatHeight > 0:
                case NPCAction.Eat when seatHeight > 0:
                case NPCAction.Type:
                case NPCAction.Read when seatHeight > 0:
                case NPCAction.Phone when seatHeight > 0:
                    if (set != null && set.Sit != null) return (set.Sit, true, 0);
                    return null;
                case NPCAction.Wave: if (set != null && set.Wave != null) return (set.Wave, false, 0); return null;
                case NPCAction.Talk: if (set != null && set.Talk != null) return (set.Talk, false, 0); return null;
                case NPCAction.LookAround: if (set != null && set.LookAround != null) return (set.LookAround, false, 0); return null;
            }
            return null;
        }

        private bool Procedural(NPCAction action) => action != NPCAction.None && Authored(action) == null || action == NPCAction.Drink || action == NPCAction.Eat || action == NPCAction.Type || action == NPCAction.Read || action == NPCAction.Phone;

        private string Label()
        {
            if (Action == NPCAction.None) return motion.CurrentClip.Replace(motion.ModelName + "-", "");
            var authored = Authored(Action) != null;
            var extra = Action == NPCAction.Drink || Action == NPCAction.Eat || Action == NPCAction.Type || Action == NPCAction.Read || Action == NPCAction.Phone;
            Temporary = !authored || extra;
            return Action + (authored && !extra ? " (authored)" : authored ? " (authored sit + TEMPORARY arms)" : " (TEMPORARY procedural)");
        }

        // ------------------------------------------------------------------ procedural poses

        private void Aim(string bone, string child, Vector3 direction, float weight = 1)
        {
            var b = Bone(bone); var c = Bone(child);
            if (b == null || c == null || direction.sqrMagnitude < 1e-6f) return;
            var current = c.position - b.position;
            if (current.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(current.normalized, direction.normalized), weight) * b.rotation;
        }

        private void Pose(NPCAction action)
        {
            var f = transform.forward; var r = transform.right; var u = Vector3.up;
            var t = actionTime; var w = Mathf.Clamp01(actionTime * 3);
            switch (action)
            {
                case NPCAction.Phone:
                    Aim("RightArm", "RightForeArm", -u * 0.9f + r * 0.15f + f * 0.2f, w);
                    Aim("RightForeArm", "RightHand", u * 0.75f + f * 0.6f - r * 0.25f, w);
                    HeadDown(18 * w);
                    break;
                case NPCAction.Photo:
                    Aim("RightArm", "RightForeArm", f * 0.8f - u * 0.1f + r * 0.2f, w);
                    Aim("RightForeArm", "RightHand", f + u * 0.25f - r * 0.2f, w);
                    Aim("LeftArm", "LeftForeArm", f * 0.8f - u * 0.1f - r * 0.2f, w);
                    Aim("LeftForeArm", "LeftHand", f + u * 0.25f + r * 0.2f, w);
                    break;
                case NPCAction.Drink:
                {
                    var sip = Mathf.Repeat(t, 9f) > 7.5f ? Mathf.Sin((Mathf.Repeat(t, 9f) - 7.5f) / 1.5f * Mathf.PI) : 0;
                    Aim("RightArm", "RightForeArm", -u * 0.9f + f * 0.25f + r * 0.1f, w);
                    Aim("RightForeArm", "RightHand", Vector3.Lerp(f * 0.8f + u * 0.3f, u + f * 0.25f - r * 0.3f, sip), w);
                    HeadDown(-8 * sip);
                    break;
                }
                case NPCAction.Eat:
                {
                    var bite = Mathf.Max(0, Mathf.Sin(t * 1.6f));
                    Aim("RightArm", "RightForeArm", -u * 0.85f + f * 0.35f + r * 0.1f, w);
                    Aim("RightForeArm", "RightHand", Vector3.Lerp(f + u * 0.05f, u * 0.9f + f * 0.4f - r * 0.2f, bite * 0.8f), w);
                    Aim("LeftArm", "LeftForeArm", -u * 0.85f + f * 0.35f - r * 0.1f, w);
                    Aim("LeftForeArm", "LeftHand", f + u * 0.05f, w);
                    HeadDown(14);
                    break;
                }
                case NPCAction.Read:
                case NPCAction.Map:
                    Aim("RightArm", "RightForeArm", -u * 0.85f + f * 0.3f + r * 0.1f, w);
                    Aim("RightForeArm", "RightHand", f + u * 0.45f - r * 0.15f, w);
                    Aim("LeftArm", "LeftForeArm", -u * 0.85f + f * 0.3f - r * 0.1f, w);
                    Aim("LeftForeArm", "LeftHand", f + u * 0.45f + r * 0.15f, w);
                    HeadDown(22);
                    break;
                case NPCAction.Type:
                {
                    var tap = Mathf.Sin(t * 13) * 0.06f;
                    Aim("RightArm", "RightForeArm", -u * 0.8f + f * 0.45f + r * 0.08f, w);
                    Aim("RightForeArm", "RightHand", f - u * (0.12f + tap) - r * 0.15f, w);
                    Aim("LeftArm", "LeftForeArm", -u * 0.8f + f * 0.45f - r * 0.08f, w);
                    Aim("LeftForeArm", "LeftHand", f - u * (0.12f - tap) + r * 0.15f, w);
                    HeadDown(16);
                    break;
                }
                case NPCAction.Cook:
                case NPCAction.OperateMachine:
                {
                    var stir = new Vector3(Mathf.Cos(t * 3), 0, Mathf.Sin(t * 3)) * 0.18f;
                    Aim("RightArm", "RightForeArm", -u * 0.7f + f * 0.55f + r * 0.1f, w);
                    Aim("RightForeArm", "RightHand", f - u * 0.35f + stir.x * r + stir.z * f, w);
                    Aim("LeftArm", "LeftForeArm", -u * 0.7f + f * 0.5f - r * 0.12f, w);
                    Aim("LeftForeArm", "LeftHand", f - u * 0.3f + r * 0.2f, w);
                    HeadDown(20);
                    break;
                }
                case NPCAction.Wipe:
                {
                    var circle = new Vector3(Mathf.Cos(t * 4), 0, Mathf.Sin(t * 4)) * 0.25f;
                    Aim("RightArm", "RightForeArm", -u * 0.6f + f * 0.7f + r * (0.15f + circle.x), w);
                    Aim("RightForeArm", "RightHand", f * (0.8f + circle.z) - u * 0.6f, w);
                    HeadDown(24);
                    break;
                }
                case NPCAction.Browse:
                case NPCAction.PickUp:
                {
                    var reach = action == NPCAction.PickUp ? 1 : Mathf.Max(0, Mathf.Sin(t * 0.9f));
                    Aim("RightArm", "RightForeArm", Vector3.Lerp(-u * 0.95f + r * 0.1f, f * 0.8f + u * (action == NPCAction.PickUp ? -0.6f : 0.1f) + r * 0.2f, reach), w);
                    Aim("RightForeArm", "RightHand", Vector3.Lerp(-u + f * 0.2f, f + (action == NPCAction.PickUp ? -u * 0.4f : Vector3.zero), reach), w);
                    if (action == NPCAction.PickUp) Bend(35 * w); else HeadDown(10);
                    break;
                }
                case NPCAction.Pay:
                    Aim("RightArm", "RightForeArm", -u * 0.6f + f * 0.7f + r * 0.1f, w);
                    Aim("RightForeArm", "RightHand", f + u * 0.1f, w);
                    break;
                case NPCAction.Point:
                    Aim("RightArm", "RightForeArm", f + u * 0.15f + r * 0.2f, w);
                    Aim("RightForeArm", "RightHand", f + u * 0.15f + r * 0.1f, w);
                    break;
                case NPCAction.Wave:
                {
                    var wave = Mathf.Sin(t * 9) * 0.35f;
                    Aim("RightArm", "RightForeArm", u * 0.7f + r * 0.6f + f * 0.1f, w);
                    Aim("RightForeArm", "RightHand", u + r * wave, w);
                    break;
                }
                case NPCAction.Talk:
                case NPCAction.Listen:
                {
                    var g = action == NPCAction.Talk ? 1f : 0.3f;
                    var a = Mathf.Sin(t * 2.1f) * 0.25f * g; var b = Mathf.Sin(t * 1.7f + 1) * 0.25f * g;
                    Aim("RightArm", "RightForeArm", -u * 0.9f + f * (0.2f + a) + r * 0.15f, w);
                    Aim("RightForeArm", "RightHand", f * 0.8f + u * (0.25f + a) - r * 0.1f, w * g);
                    Aim("LeftArm", "LeftForeArm", -u * 0.9f + f * (0.15f + b) - r * 0.15f, w);
                    Aim("LeftForeArm", "LeftHand", f * 0.8f + u * (0.2f + b) + r * 0.1f, w * g);
                    break;
                }
                case NPCAction.Nod: HeadDown(Mathf.Sin(t * 7) * 10 * Mathf.Clamp01(2 - t)); break;
                case NPCAction.ShakeHead: HeadTurn(Mathf.Sin(t * 8) * 16 * Mathf.Clamp01(2 - t)); break;
                case NPCAction.Shrug:
                {
                    var s = Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI);
                    Aim("RightArm", "RightForeArm", -u * 0.85f + r * 0.35f, w);
                    Aim("RightForeArm", "RightHand", f * 0.6f + r * 0.6f + u * 0.3f * s, w);
                    Aim("LeftArm", "LeftForeArm", -u * 0.85f - r * 0.35f, w);
                    Aim("LeftForeArm", "LeftHand", f * 0.6f - r * 0.6f + u * 0.3f * s, w);
                    break;
                }
                case NPCAction.LookAround: HeadTurn(Mathf.Sin(t * 0.9f) * 50); break;
                case NPCAction.Stretch:
                {
                    var s = Mathf.Sin(Mathf.Clamp01(t / 3) * Mathf.PI);
                    Aim("RightArm", "RightForeArm", Vector3.Lerp(-u, u + r * 0.2f, s), w);
                    Aim("LeftArm", "LeftForeArm", Vector3.Lerp(-u, u - r * 0.2f, s), w);
                    HeadDown(-12 * s);
                    break;
                }
                case NPCAction.Yawn:
                {
                    var s = Mathf.Sin(Mathf.Clamp01(t / 2.5f) * Mathf.PI);
                    Aim("RightArm", "RightForeArm", -u * 0.6f + f * 0.6f, s);
                    Aim("RightForeArm", "RightHand", u + f * 0.3f - r * 0.5f, s);
                    HeadDown(-18 * s);
                    break;
                }
                case NPCAction.CarryBag:
                    Aim("RightArm", "RightForeArm", -u + r * 0.12f, w);
                    break;
                case NPCAction.Board:
                    break;
            }
        }

        private void HeadDown(float degrees)
        {
            var head = Bone("Head") ?? Bone("Neck");
            if (head != null) head.rotation = Quaternion.AngleAxis(degrees, transform.right) * head.rotation;
        }

        private void HeadTurn(float degrees)
        {
            var head = Bone("Head") ?? Bone("Neck");
            if (head != null) head.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * head.rotation;
        }

        private void Bend(float degrees)
        {
            var spine = Bone("Spine");
            if (spine != null) spine.rotation = Quaternion.AngleAxis(degrees, transform.right) * spine.rotation;
        }

        // ------------------------------------------------------------------ life: breathing, weight shift, gaze

        private void Micro(float dt)
        {
            var spine = Bone("Spine1") ?? Bone("Spine");
            if (spine != null) spine.rotation = Quaternion.AngleAxis(Mathf.Sin(clock * 1.3f) * 1.2f, transform.right) * spine.rotation;   // breathing
            if (Action != NPCAction.None || Speed > 0.1f) return;
            if ((shiftTimer -= dt) <= 0) { shiftTimer = Random.Range(6f, 14f); shiftTarget = Random.Range(-1f, 1f); }
            shift = Mathf.MoveTowards(shift, shiftTarget, dt * 0.5f);
            var hips = Bone("Hips");
            if (hips != null) hips.rotation = Quaternion.AngleAxis(shift * 3f, transform.forward) * hips.rotation;   // weight on one leg
        }

        public float Speed { get; set; }

        private void Gaze(float dt)
        {
            var head = Bone("Head");
            if (head == null) return;
            lookWeight = Mathf.MoveTowards(lookWeight, lookTarget.HasValue ? 1 : 0, dt * 2.5f);
            if (lookWeight <= 0.001f) return;
            if ((saccadeTimer -= dt) <= 0) { saccadeTimer = Random.Range(0.2f, 0.7f); saccade = Random.insideUnitSphere * 0.08f; }
            var target = (lookTarget ?? head.position + transform.forward) + saccade;
            var to = target - head.position;
            var flat = Vector3.ProjectOnPlane(to, Vector3.up);
            var yaw = Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), flat, Vector3.up), -70, 70);
            var pitch = Mathf.Clamp(-Mathf.Atan2(to.y, Mathf.Max(0.1f, flat.magnitude)) * Mathf.Rad2Deg, -30, 35);
            var neck = Bone("Neck");
            var turn = Quaternion.AngleAxis(yaw * lookWeight, Vector3.up) * Quaternion.AngleAxis(pitch * lookWeight, transform.right);
            if (neck != null)
            {
                neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.4f) * neck.rotation;
                head.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.6f) * head.rotation;
            }
            else head.rotation = turn * head.rotation;
        }

        // ------------------------------------------------------------------ props

        private void UpdateProps()
        {
            Prop("cup", Action == NPCAction.Drink || Action == NPCAction.OperateMachine);
            Prop("phone", Action == NPCAction.Phone || Action == NPCAction.Photo);
            Prop("book", Action == NPCAction.Read);
            Prop("map", Action == NPCAction.Map);
            Prop("laptop", Action == NPCAction.Type);
        }

        /// Things carried between actions (shopping bag, umbrella in the rain).
        public void Carry(string prop, bool on) => Prop(prop, on);

        public bool Carrying(string prop) => props.TryGetValue(prop, out var p) && p != null && p.activeSelf;

        private void Prop(string name, bool on)
        {
            if (!props.TryGetValue(name, out var prop) || prop == null)
            {
                if (!on) return;
                props[name] = prop = Props.Make(name);
            }
            prop.SetActive(on);
        }

        private void AttachProps()
        {
            foreach (var pair in props)
            {
                var prop = pair.Value;
                if (prop == null || !prop.activeSelf) continue;
                var right = Bone("RightHand"); var left = Bone("LeftHand");
                switch (pair.Key)
                {
                    case "laptop":
                        prop.transform.SetPositionAndRotation(transform.position + transform.forward * 0.42f + Vector3.up * (seatHeight > 0 ? seatHeight + 0.3f : 0.75f), transform.rotation);
                        break;
                    case "bag":
                        if (right != null) prop.transform.SetPositionAndRotation(right.position - Vector3.up * 0.2f, transform.rotation);
                        break;
                    case "umbrella":
                        if (left != null) prop.transform.SetPositionAndRotation(left.position, Quaternion.LookRotation(transform.forward, Vector3.up));
                        break;
                    case "map":
                    case "book":
                        if (right != null && left != null) prop.transform.SetPositionAndRotation((right.position + left.position) / 2, Quaternion.LookRotation(Vector3.up * 0.7f - transform.forward, transform.forward));
                        break;
                    default:
                        if (right != null) prop.transform.SetPositionAndRotation(right.position + right.TransformDirection(Vector3.up) * 0.04f, transform.rotation);
                        break;
                }
            }
        }

        private void OnDestroy() { foreach (var p in props.Values) if (p != null) Destroy(p); }
    }

    /// Simple generated props (replace with art later): cup, phone, book, map, laptop, shopping bag, umbrella.
    public static class Props
    {
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        private static Material Mat(Color c)
        {
            var key = ColorUtility.ToHtmlStringRGB(c);
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
            m.SetFloat("_Smoothness", 0.35f);
            materials[key] = m;
            return m;
        }

        private static GameObject Part(PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Color colour)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(colour);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static GameObject Make(string name)
        {
            var root = new GameObject("Prop · " + name);
            switch (name)
            {
                case "cup":
                    Part(PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(0.075f, 0.05f, 0.075f), new Color(0.95f, 0.93f, 0.88f));
                    Part(PrimitiveType.Cylinder, root.transform, Vector3.up * 0.055f, new Vector3(0.078f, 0.006f, 0.078f), new Color(0.35f, 0.24f, 0.16f));
                    break;
                case "phone": Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.07f, 0.145f, 0.008f), new Color(0.08f, 0.08f, 0.1f)); break;
                case "book": Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.22f, 0.02f, 0.16f), new Color(0.55f, 0.18f, 0.15f)); break;
                case "map": Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.42f, 0.003f, 0.3f), new Color(0.93f, 0.9f, 0.78f)); break;
                case "laptop":
                    Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.33f, 0.015f, 0.23f), new Color(0.7f, 0.72f, 0.75f));
                    var screen = Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0.11f, 0.11f), new Vector3(0.33f, 0.22f, 0.012f), new Color(0.15f, 0.2f, 0.3f));
                    screen.transform.localRotation = Quaternion.Euler(-12, 0, 0);
                    break;
                case "bag":
                    Part(PrimitiveType.Cube, root.transform, Vector3.down * 0.15f, new Vector3(0.3f, 0.32f, 0.14f), new Color(0.86f, 0.78f, 0.6f));
                    Part(PrimitiveType.Cube, root.transform, Vector3.up * 0.04f, new Vector3(0.02f, 0.08f, 0.02f), new Color(0.5f, 0.4f, 0.3f));
                    break;
                case "umbrella":
                    Part(PrimitiveType.Cylinder, root.transform, Vector3.up * 0.4f, new Vector3(0.02f, 0.45f, 0.02f), new Color(0.2f, 0.2f, 0.2f));
                    Part(PrimitiveType.Sphere, root.transform, Vector3.up * 0.88f, new Vector3(1.0f, 0.22f, 1.0f), new Color(0.2f, 0.35f, 0.55f));
                    break;
            }
            return root;
        }
    }
}
