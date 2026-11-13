using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Presentation only: this never moves colliders, NPCs, quest state or answer controls.
    public sealed class CozySceneryMotion : MonoBehaviour
    {
        public Vector3 Axis = Vector3.forward;
        public float DegreesPerSecond = 4;
        public bool KeepChildrenUpright;

        private void Update()
        {
            transform.Rotate(Axis, DegreesPerSecond * Time.deltaTime, Space.Self);
            if (!KeepChildrenUpright) return;
            foreach (Transform child in transform)
                if (child.name.Contains("gondola")) child.rotation = Quaternion.identity;
        }
    }
}
