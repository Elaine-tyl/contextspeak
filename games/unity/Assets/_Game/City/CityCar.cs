using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.City
{
    /// A car that drives a closed loop, stops at red lights and keeps a gap to whatever is in front
    /// (other cars, pedestrians and the player).
    public sealed class CityCar : MonoBehaviour
    {
        public Vector3[] Path = new Vector3[0];
        public Vector3[] Junctions = new Vector3[0];
        public float Cruise = 7f;
        public float YawOffset;          // corrects models whose nose is not +Z
        private int target;
        private float speed;

        private void Start()
        {
            if (Path.Length < 2) { enabled = false; return; }
            // Start heading for the nearest corner after our position.
            var best = 0;
            var distance = float.MaxValue;
            for (var index = 0; index < Path.Length; index++)
            {
                var candidate = (Path[index] - transform.position).sqrMagnitude;
                if (candidate < distance) { distance = candidate; best = index; }
            }
            target = (best + 1) % Path.Length;
        }

        private void Update()
        {
            var goal = Path[target];
            var delta = goal - transform.position;
            delta.y = 0;
            if (delta.magnitude < 0.6f) { target = (target + 1) % Path.Length; return; }
            var heading = delta.normalized;
            var wanted = Cruise;

            // Red or amber light ahead: stop before the junction box.
            var alongX = Mathf.Abs(heading.x) > Mathf.Abs(heading.z);
            if (CitySignals.State(alongX) != 2)
                foreach (var junction in Junctions)
                {
                    var toJunction = junction - transform.position;
                    toJunction.y = 0;
                    var ahead = Vector3.Dot(toJunction, heading);
                    var side = Vector3.Cross(heading, toJunction).magnitude;
                    if (side < 7f && ahead > 9f && ahead < 15f) { wanted = Mathf.Min(wanted, Mathf.Max(0, (ahead - 10.5f) * 1.6f)); }
                }

            // Something in the way: brake.
            var origin = transform.position + Vector3.up * 0.7f + heading * 2.6f;
            if (Physics.SphereCast(origin, 0.9f, heading, out var hit, 7f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != transform.root)
                wanted = Mathf.Min(wanted, Mathf.Max(0, (hit.distance - 1.5f) * 1.5f));

            speed = Mathf.MoveTowards(speed, wanted, (wanted < speed ? 9f : 3f) * Time.deltaTime);
            transform.position += heading * speed * Time.deltaTime;
            var look = Quaternion.LookRotation(heading) * Quaternion.Euler(0, YawOffset, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
        }
    }
}
