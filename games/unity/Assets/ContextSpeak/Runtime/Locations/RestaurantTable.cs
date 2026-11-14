using UnityEngine;

namespace ContextSpeak.Life
{
    /// A dining table: capacity, area (Window / Inside / Terrace), where plates go, reserved flag.
    public sealed class RestaurantTable : MonoBehaviour
    {
        public string Id = "T1";
        public int Capacity = 2;
        public string Area = "Inside";
        public bool Reserved;
        public Transform PlateAnchor;
        public Vector3 ServicePoint => transform.position + transform.right * 1.3f;
    }
}
