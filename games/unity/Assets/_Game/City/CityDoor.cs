using UnityEngine;

namespace ContextSpeak.City
{
    /// A hinged door that swings open when someone walks up to it and closes again behind them.
    public sealed class CityDoor : MonoBehaviour
    {
        public Transform Leaf;          // pivot at the hinge
        public float OpenAngle = 92f;
        public float Range = 3.2f;
        public float Speed = 4f;

        private Transform player;
        private float angle;

        private void Start()
        {
            var found = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            if (found != null) player = found.transform;
        }

        private void Update()
        {
            if (Leaf == null) return;
            var near = player != null && (player.position - transform.position).sqrMagnitude < Range * Range;
            if (!near)
                foreach (var walker in CityPedestrian.All)
                    if (walker != null && (walker.transform.position - transform.position).sqrMagnitude < 2.4f * 2.4f) { near = true; break; }
            angle = Mathf.MoveTowards(angle, near ? OpenAngle : 0, Speed * 60 * Time.deltaTime);
            Leaf.localRotation = Quaternion.Euler(0, angle, 0);
        }
    }
}
