using UnityEngine;

namespace ContextSpeak.City
{
    /// Gestures and a proper jump pose for the protagonist, layered on top of the walk/run clips.
    /// Works on any humanoid rig: it aims limbs by direction (in the character's own frame) rather
    /// than by bone-specific angles. 1 wave · 2 cheer · 3 dance · 4 bow · 5 clap.
    public sealed class CityEmotes : MonoBehaviour
    {
        public enum Gesture { None, Wave, Cheer, Dance, Bow, Clap }
        public Gesture Current { get; private set; }
        public bool Airborne;
        private float started, airBlend;
        private Transform rig;
        private Transform hips, spine, rightArm, rightFore, leftArm, leftFore, rightThigh, rightShin, leftThigh, leftShin;

        public void Play(Gesture gesture)
        {
            Current = Current == gesture ? Gesture.None : gesture;
            started = Time.time;
        }

        public void Stop() { Current = Gesture.None; }

        private void Bind()
        {
            // The visible protagonist (girl or boy) is whichever child is active.
            Transform active = null;
            foreach (Transform child in transform)
                if (child.gameObject.activeInHierarchy) { active = child; break; }
            if (active == rig && hips != null) return;
            rig = active;
            if (rig == null) return;
            hips = Find("Hips"); spine = Find("Spine1") ?? Find("Spine");
            rightArm = Find("RightArm"); rightFore = Find("RightForeArm");
            leftArm = Find("LeftArm"); leftFore = Find("LeftForeArm");
            rightThigh = Find("RightUpLeg"); rightShin = Find("RightLeg");
            leftThigh = Find("LeftUpLeg"); leftShin = Find("LeftLeg");
        }

        private Transform Find(string suffix)
        {
            foreach (var bone in rig.GetComponentsInChildren<Transform>())
                if (bone.name.EndsWith(suffix) && !(suffix == "Spine" && bone.name.EndsWith("Spine1"))) return bone;
            return null;
        }

        /// Turns a bone so that the segment to its child points along `direction` (character space).
        private void Aim(Transform bone, Transform child, Vector3 direction, float weight)
        {
            if (bone == null || child == null || weight <= 0) return;
            var current = child.position - bone.position;
            var wanted = transform.TransformDirection(direction.normalized) * current.magnitude;
            var turn = Quaternion.FromToRotation(current, wanted);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
        }

        private static Transform Child(Transform bone) => bone != null && bone.childCount > 0 ? bone.GetChild(0) : null;

        private void LateUpdate()
        {
            Bind();
            if (rig == null) return;
            var t = Time.time - started;
            var w = Mathf.Clamp01(t * 4f);   // ease into the gesture
            switch (Current)
            {
                case Gesture.Wave:
                    Aim(rightArm, rightFore, new Vector3(0.55f, 0.75f, 0.15f), w);
                    Aim(rightFore, Child(rightFore), new Vector3(0.25f + Mathf.Sin(t * 9f) * 0.45f, 1f, 0.05f), w);
                    if (t > 3.5f) Current = Gesture.None;
                    break;
                case Gesture.Cheer:
                    Aim(rightArm, rightFore, new Vector3(0.35f, 1f, 0.1f), w);
                    Aim(leftArm, leftFore, new Vector3(-0.35f, 1f, 0.1f), w);
                    Aim(rightFore, Child(rightFore), new Vector3(0.1f, 1f, 0.05f), w);
                    Aim(leftFore, Child(leftFore), new Vector3(-0.1f, 1f, 0.05f), w);
                    if (hips != null) hips.position += Vector3.up * Mathf.Abs(Mathf.Sin(t * 6f)) * 0.08f * w;
                    if (t > 3f) Current = Gesture.None;
                    break;
                case Gesture.Dance:
                    var beat = t * 7f;
                    if (hips != null) hips.rotation = Quaternion.AngleAxis(Mathf.Sin(beat * 0.5f) * 18f * w, transform.up) * hips.rotation;
                    if (hips != null) hips.position += transform.right * Mathf.Sin(beat * 0.5f) * 0.06f * w + Vector3.up * Mathf.Abs(Mathf.Sin(beat)) * 0.05f * w;
                    Aim(rightArm, rightFore, new Vector3(0.7f, Mathf.Sin(beat) * 0.8f, 0.3f), w);
                    Aim(leftArm, leftFore, new Vector3(-0.7f, -Mathf.Sin(beat) * 0.8f, 0.3f), w);
                    break;
                case Gesture.Bow:
                    var bow = Mathf.Sin(Mathf.Clamp01(t / 2.2f) * Mathf.PI);
                    if (spine != null) spine.rotation = Quaternion.AngleAxis(42f * bow, transform.right) * spine.rotation;
                    if (t > 2.2f) Current = Gesture.None;
                    break;
                case Gesture.Clap:
                    var clap = Mathf.Abs(Mathf.Sin(t * 8f));
                    Aim(rightArm, rightFore, new Vector3(0.35f, -0.2f, 0.9f), w);
                    Aim(leftArm, leftFore, new Vector3(-0.35f, -0.2f, 0.9f), w);
                    Aim(rightFore, Child(rightFore), new Vector3(-0.2f - clap * 0.5f, 0.5f, 0.8f), w);
                    Aim(leftFore, Child(leftFore), new Vector3(0.2f + clap * 0.5f, 0.5f, 0.8f), w);
                    if (t > 3f) Current = Gesture.None;
                    break;
            }
            // Jump: tuck the knees and lift the arms while in the air.
            airBlend = Mathf.MoveTowards(airBlend, Airborne ? 1 : 0, Time.deltaTime * 8f);
            if (airBlend > 0.01f)
            {
                Aim(rightThigh, rightShin, new Vector3(0.1f, -0.45f, 0.9f), airBlend);
                Aim(leftThigh, leftShin, new Vector3(-0.1f, -0.75f, 0.6f), airBlend);
                Aim(rightShin, Child(rightShin), new Vector3(0.05f, -1f, -0.6f), airBlend);
                Aim(leftShin, Child(leftShin), new Vector3(-0.05f, -1f, -0.4f), airBlend);
                if (Current == Gesture.None)
                {
                    Aim(rightArm, rightFore, new Vector3(0.6f, 0.5f, 0.2f), airBlend * 0.8f);
                    Aim(leftArm, leftFore, new Vector3(-0.6f, 0.5f, 0.2f), airBlend * 0.8f);
                }
            }
        }
    }
}
