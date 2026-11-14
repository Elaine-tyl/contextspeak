using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Procedural animation for the rigged zoo animals (tools/blender/zoo_animals.py): a four-beat walk
    /// driven by the distance actually travelled (no foot sliding), head down to graze or drink, trunk
    /// curling to the mouth, ears flapping, tail swishing, looking around and breathing. Works on any
    /// model whose bones use the names Hips, Spine, Chest, Neck, Head, Trunk1-3, Ear_L/R, Tail1-3 and
    /// Leg_{FL,FR,BL,BR}_{1,2,3}; missing bones are simply skipped.
    public sealed class AnimalAnimator : MonoBehaviour
    {
        public float Stride = 1.5f, LegSwing = 22, HeadDrop = 40, NeckLift, WalkSpeed = 1, Height = 1;
        public bool RigidLegs, Browser, Active = true;
        public AnimalState State = AnimalState.Idle;
        public float Speed;                       // set by the controller (m/s)
        private readonly List<(Transform bone, Quaternion rest, string name)> bones = new List<(Transform, Quaternion, string)>();
        private readonly Dictionary<string, float> pitch = new Dictionary<string, float>(), yaw = new Dictionary<string, float>(), roll = new Dictionary<string, float>();
        private Transform hips;
        private Vector3 hipsRest;
        private float phase, head, look, lookTarget, lookTimer, seed, flick, curl;
        private static readonly string[] Names =
        {
            "Hips", "Spine", "Chest", "Neck", "Head", "Trunk1", "Trunk2", "Trunk3", "Ear_L", "Ear_R", "Tail1", "Tail2", "Tail3",
            "Leg_FL_1", "Leg_FL_2", "Leg_FL_3", "Leg_FR_1", "Leg_FR_2", "Leg_FR_3", "Leg_BL_1", "Leg_BL_2", "Leg_BL_3", "Leg_BR_1", "Leg_BR_2", "Leg_BR_3",
        };
        // Lateral-sequence walk: left hind, left fore, right hind, right fore.
        private static readonly (string leg, float offset)[] Gait = { ("BL", 0), ("FL", 0.25f), ("BR", 0.5f), ("FR", 0.75f) };

        public int BoneCount => bones.Count;

        private void Awake() { Bind(); }

        public void Bind()
        {
            bones.Clear();
            seed = (transform.position.x * 7.3f + transform.position.z * 3.1f) % 10f;
            var all = GetComponentsInChildren<Transform>(true);
            int Depth(Transform t) { var d = 0; while (t != null && t != transform) { d++; t = t.parent; } return d; }
            foreach (var bone in all.Where(t => System.Array.IndexOf(Names, t.name) >= 0).OrderBy(Depth))
                bones.Add((bone, bone.localRotation, bone.name));
            hips = bones.FirstOrDefault(b => b.name == "Hips").bone;
            if (hips != null) hipsRest = hips.localPosition;
        }

        private void Set(Dictionary<string, float> channel, string bone, float degrees) { channel[bone] = degrees; }

        private void LateUpdate()
        {
            if (!Active || bones.Count == 0) return;
            var dt = Time.deltaTime;
            var t = Time.time + seed;
            pitch.Clear(); yaw.Clear(); roll.Clear();

            // ---- gait
            phase = (phase + Speed * dt / Mathf.Max(0.2f, Stride)) % 1f;
            var effort = Mathf.Clamp01(Speed / Mathf.Max(0.1f, WalkSpeed));
            var run = Mathf.Clamp01((Speed - WalkSpeed) / Mathf.Max(0.1f, WalkSpeed));
            if (!RigidLegs)
                foreach (var (leg, offset) in Gait)
                {
                    var a = (phase + offset) * Mathf.PI * 2;
                    var swing = Mathf.Sin(a) * LegSwing * effort * (1 + run * 0.5f);
                    var lift = Mathf.Max(0, -Mathf.Cos(a)) * LegSwing * 0.8f * effort;       // fold while the leg swings forward
                    Set(pitch, "Leg_" + leg + "_1", swing);
                    Set(pitch, "Leg_" + leg + "_2", lift);
                    Set(pitch, "Leg_" + leg + "_3", -lift * 0.4f);
                }
            var bob = Mathf.Sin(phase * Mathf.PI * 4) * effort;
            Set(roll, "Hips", Mathf.Sin(phase * Mathf.PI * 2) * (RigidLegs ? 2.5f : 1.2f) * effort);
            Set(pitch, "Spine", Mathf.Sin(t * 0.9f) * 0.5f);                                // breathing

            // ---- head: graze, drink, browse, rest
            var eating = State == AnimalState.Eating;
            var wantedHead = State == AnimalState.Drinking ? HeadDrop
                : eating ? (Browser ? -NeckLift : HeadDrop)
                : State == AnimalState.Sleeping ? HeadDrop * 0.45f
                : State == AnimalState.Resting ? HeadDrop * 0.2f
                : State == AnimalState.Grooming ? HeadDrop * 0.3f : 0;
            head = Mathf.MoveTowards(head, wantedHead, Mathf.Max(18, Mathf.Abs(HeadDrop) * 0.6f) * dt);
            var chew = eating && Mathf.Abs(head - wantedHead) < 4 ? Mathf.Sin(t * 5) * 1.5f : 0;
            Set(pitch, "Chest", head * 0.08f);
            Set(pitch, "Neck", head * 0.6f + bob * 1.5f);
            Set(pitch, "Head", head * 0.32f + chew);

            // ---- looking around: turn, hold, turn back
            if (State == AnimalState.LookingAround || State == AnimalState.Idle)
            {
                if ((lookTimer -= dt) <= 0)
                {
                    lookTimer = 2.5f + Mathf.PerlinNoise(t * 0.3f, seed) * 4;
                    lookTarget = State == AnimalState.LookingAround ? (Mathf.PerlinNoise(t, seed * 2) - 0.5f) * 70 : (Mathf.PerlinNoise(t, seed * 2) - 0.5f) * 20;
                }
            }
            else lookTarget = 0;
            look = Mathf.MoveTowards(look, lookTarget, 30 * dt);
            Set(yaw, "Neck", look * 0.55f);
            Set(yaw, "Head", look * 0.45f);

            // ---- trunk: sway, and curl to the mouth when eating or drinking
            var wantedCurl = (eating || State == AnimalState.Drinking) && Mathf.Abs(head - wantedHead) < 6 ? 0.5f + 0.5f * Mathf.Sin(t * (eating ? 0.9f : 0.7f)) : 0;
            curl = Mathf.MoveTowards(curl, wantedCurl, dt * 1.2f);
            for (var i = 1; i <= 3; i++)
            {
                Set(yaw, "Trunk" + i, Mathf.Sin(t * 0.6f + i * 0.7f) * (3 + i * 2) * (1 - curl));
                Set(pitch, "Trunk" + i, curl * (6 + i * 6) + Mathf.Sin(t * 0.45f + i) * 1.5f + bob * i);
            }

            // ---- ears and tail
            var flap = 5 + 7 * Mathf.Max(0, Mathf.Sin(t * 1.4f)) * (State == AnimalState.Sleeping ? 0.2f : 1);
            Set(yaw, "Ear_L", flap);
            Set(yaw, "Ear_R", -flap);
            if (flick <= 0 && Mathf.PerlinNoise(t * 0.5f, seed + 4) > 0.72f) flick = 1;
            flick = Mathf.Max(0, flick - dt * 1.5f);
            var swish = Mathf.Sin(t * 2.2f) * (4 + 14 * flick) + Mathf.Sin(phase * Mathf.PI * 2) * 5 * effort;
            Set(roll, "Tail1", swish);
            Set(roll, "Tail2", swish * 1.3f);
            Set(roll, "Tail3", swish * 1.5f);

            // ---- apply, parents first, as rotations about the animal's own axes
            var right = transform.right; var up = transform.up; var forward = transform.forward;
            foreach (var (bone, rest, name) in bones)
            {
                bone.localRotation = rest;
                var p = pitch.TryGetValue(name, out var pv) ? pv : 0;
                var y = yaw.TryGetValue(name, out var yv) ? yv : 0;
                var r = roll.TryGetValue(name, out var rv) ? rv : 0;
                if (p == 0 && y == 0 && r == 0) continue;
                bone.rotation = Quaternion.AngleAxis(y, up) * Quaternion.AngleAxis(p, right) * Quaternion.AngleAxis(r, forward) * bone.rotation;
            }
            if (hips != null) hips.localPosition = hipsRest + hips.parent.InverseTransformVector(up * (bob * 0.012f * Height * (RigidLegs ? 2 : 1)));
        }
    }
}
