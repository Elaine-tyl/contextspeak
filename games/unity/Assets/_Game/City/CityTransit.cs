using UnityEngine;

namespace ContextSpeak.City
{
    /// Public transport you can ride: a bus that loops its route, or a tram that runs to the end of
    /// the line and back (trams have a cab at each end, so it never turns round). It halts at every
    /// stop with its doors open; walk up and press E to board, press E at a stop to get off.
    public sealed class CityTransit : MonoBehaviour
    {
        public string LineName = "Bus 12";
        public bool Shuttle;                  // tram: back and forth along Path
        public Vector3[] Path = new Vector3[0];
        public Vector3[] Stops = new Vector3[0];
        public string[] StopNames = new string[0];
        public Vector3[] Junctions = new Vector3[0];
        public bool ObeySignals = true;
        public float Cruise = 8f, Dwell = 9f;
        public Transform Seat;
        public Vector3 ExitOffset = new Vector3(4.5f, 0, 0);   // local: the pavement beside the doors
        public bool BothSides;                // tram: get off on the side you boarded from
        public float HalfLength = 6f;         // for "is the player beside the doors"
        public float YawOffset;

        public bool Dwelling => dwell > 0;
        public string StopName => current >= 0 && current < StopNames.Length ? StopNames[current] : "";
        public string NextStopName => next >= 0 && next < StopNames.Length ? StopNames[next] : "";
        public bool Riding { get; set; }

        /// True when a point is beside this vehicle, close enough to step on board.
        public bool Beside(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.z) < HalfLength + 0.5f && Mathf.Abs(local.x) < 7.5f;
        }

        private int target = 1, step = 1, current = -1, next;
        private float speed, dwell;
        private Collider[] solids;

        private void Start()
        {
            solids = GetComponentsInChildren<Collider>();
            if (Path.Length < 2) { enabled = false; return; }
            next = NearestStopAhead();
        }

        public void SetSolid(bool solid)
        {
            if (solids == null) solids = GetComponentsInChildren<Collider>();
            foreach (var item in solids) if (item != null) item.enabled = solid;
        }

        private void Update()
        {
            if (dwell > 0)
            {
                dwell -= Time.deltaTime;
                if (dwell <= 0) next = Advance(current);
                return;
            }
            var goal = Path[target];
            var delta = goal - transform.position;
            delta.y = 0;
            if (delta.magnitude < 0.5f)
            {
                if (Shuttle && (target == Path.Length - 1 || target == 0)) step = -step;
                target = Shuttle ? Mathf.Clamp(target + step, 0, Path.Length - 1) : (target + 1) % Path.Length;
                return;
            }
            var heading = delta.normalized;
            var wanted = Cruise;

            // Slow into the next stop and halt on it.
            if (next >= 0 && next < Stops.Length)
            {
                var toStop = Stops[next] - transform.position;
                toStop.y = 0;
                var ahead = Vector3.Dot(toStop, heading);
                var side = Vector3.Cross(heading, toStop).magnitude;
                if (side < 2.5f && ahead > -0.3f && ahead < 40f)
                {
                    if (ahead < 0.4f) { Arrive(next); return; }
                    wanted = Mathf.Min(wanted, Mathf.Max(1.2f, ahead * 0.55f));
                }
            }

            if (ObeySignals)
            {
                var alongX = Mathf.Abs(heading.x) > Mathf.Abs(heading.z);
                if (CitySignals.State(alongX) != 2)
                    foreach (var junction in Junctions)
                    {
                        var toJunction = junction - transform.position;
                        toJunction.y = 0;
                        var ahead = Vector3.Dot(toJunction, heading);
                        var side = Vector3.Cross(heading, toJunction).magnitude;
                        if (side < 7f && ahead > 11f && ahead < 18f) wanted = Mathf.Min(wanted, Mathf.Max(0, (ahead - 12.5f) * 1.4f));
                    }
            }

            // Keep a gap to anything in front (cars, people, the player) — but never brake for the rider.
            var origin = transform.position + Vector3.up * 1.0f + heading * (Shuttle ? 12.6f : 6.4f);
            if (Physics.SphereCast(origin, 1.0f, heading, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != transform.root && !(Riding && hit.collider.GetComponent<CharacterController>() != null))
                wanted = Mathf.Min(wanted, Mathf.Max(0, (hit.distance - 1.5f) * 1.3f));

            speed = Mathf.MoveTowards(speed, wanted, (wanted < speed ? 4f : 1.6f) * Time.deltaTime);
            transform.position += heading * speed * Time.deltaTime;
            if (!Shuttle)
            {
                var look = Quaternion.LookRotation(heading) * Quaternion.Euler(0, YawOffset, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 3f);
            }
        }

        private void Arrive(int stop)
        {
            current = stop;
            speed = 0;
            dwell = Dwell;
        }

        private int Advance(int stop)
        {
            if (Stops.Length == 0) return -1;
            if (!Shuttle) return (stop + 1) % Stops.Length;
            // Shuttle stops are listed from Path[0] to the last point; turn round at either end.
            var following = stop + step;
            if (following < 0 || following >= Stops.Length) following = stop - step;
            return Mathf.Clamp(following, 0, Stops.Length - 1);
        }

        private int NearestStopAhead()
        {
            var best = 0;
            var distance = float.MaxValue;
            var heading = (Path[target] - transform.position).normalized;
            for (var index = 0; index < Stops.Length; index++)
            {
                var toStop = Stops[index] - transform.position;
                var ahead = Vector3.Dot(toStop, heading);
                if (ahead > 0 && ahead < distance) { distance = ahead; best = index; }
            }
            return best;
        }
    }
}
