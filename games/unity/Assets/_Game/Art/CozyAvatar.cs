using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Small joint motions use supplied or generated skeletons without a large animation dependency.
    public sealed class CozyAvatar : MonoBehaviour
    {
        public Transform Head, LeftArm, RightArm, LeftLeg, RightLeg;
        private Vector3 previous;
        private float phase;
        private bool hasBindPose;
        private Quaternion headBind, leftArmBind, rightArmBind, leftLegBind, rightLegBind;

        private void OnEnable() { previous = transform.position; }

        private void LateUpdate()
        {
            // Imported skeletons need their authored local axes; zeroing them breaks the pose.
            if (!hasBindPose)
            {
                headBind = Head != null ? Head.localRotation : Quaternion.identity;
                leftArmBind = LeftArm != null ? LeftArm.localRotation : Quaternion.identity;
                rightArmBind = RightArm != null ? RightArm.localRotation : Quaternion.identity;
                leftLegBind = LeftLeg != null ? LeftLeg.localRotation : Quaternion.identity;
                rightLegBind = RightLeg != null ? RightLeg.localRotation : Quaternion.identity;
                hasBindPose = true;
            }
            var speed = Vector3.Distance(previous, transform.position) / Mathf.Max(Time.deltaTime, 0.001f);
            previous = transform.position;
            phase += Time.deltaTime * (speed > 0.2f ? 8 : 1.8f);
            var stride = speed > 0.2f ? Mathf.Sin(phase) * Mathf.Min(25, speed * 7) : 0;
            if (LeftLeg != null) LeftLeg.localRotation = leftLegBind * Quaternion.Euler(stride, 0, 0);
            if (RightLeg != null) RightLeg.localRotation = rightLegBind * Quaternion.Euler(-stride, 0, 0);
            if (LeftArm != null) LeftArm.localRotation = leftArmBind * Quaternion.Euler(-stride * 0.6f, 0, 4 + Mathf.Sin(phase) * 1.5f);
            if (RightArm != null) RightArm.localRotation = rightArmBind * Quaternion.Euler(stride * 0.6f, 0, -4 - Mathf.Sin(phase) * 1.5f);
            if (Head != null) Head.localRotation = headBind * Quaternion.Euler(Mathf.Sin(phase * 0.6f) * 1.4f, Mathf.Sin(phase * 0.4f) * 3, 0);
        }
    }
}
