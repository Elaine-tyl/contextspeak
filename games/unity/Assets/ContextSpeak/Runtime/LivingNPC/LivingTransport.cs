using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Life
{
    /// A signalled pedestrian crossing: cars get green while pedestrians wait, then the reverse.
    /// The zebra is the only walkable strip across the road, so NPC paths use it; NPCs hold at the kerb
    /// on red (NPCNavigationController) and cars stop at the line on red or when someone is still crossing.
    public sealed class LivingCrossing : MonoBehaviour
    {
        public static readonly List<LivingCrossing> All = new List<LivingCrossing>();
        [Tooltip("Local box of the zebra (walkable strip across the road).")] public Bounds Zebra = new Bounds(Vector3.zero, new Vector3(3, 2, 8));
        public float CarGreen = 14, WalkGreen = 9, Clearance = 3;
        public Renderer[] WalkLights = new Renderer[0], StopLights = new Renderer[0];
        public bool WalkAllowed { get; private set; }
        public float PhaseTime { get; private set; }
        public int Violations { get; private set; }
        private Material on, off, red;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Start()
        {
            on = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.3f, 1f, 0.5f) };
            on.EnableKeyword("_EMISSION"); on.SetColor("_EmissionColor", new Color(0.2f, 1.6f, 0.5f));
            red = new Material(on) { color = new Color(1f, 0.25f, 0.2f) };
            red.SetColor("_EmissionColor", new Color(1.8f, 0.2f, 0.15f));
            off = new Material(on) { color = new Color(0.12f, 0.12f, 0.12f) };
            off.SetColor("_EmissionColor", Color.black);
            Apply();
        }

        private void Update()
        {
            PhaseTime += Time.deltaTime;
            var length = WalkAllowed ? WalkGreen : CarGreen;
            if (PhaseTime >= length) { WalkAllowed = !WalkAllowed; PhaseTime = 0; Apply(); }
        }

        private void Apply()
        {
            foreach (var r in WalkLights) if (r != null) r.sharedMaterial = WalkAllowed ? on : off;
            foreach (var r in StopLights) if (r != null) r.sharedMaterial = WalkAllowed ? off : red;
        }

        /// Cars must stop: pedestrians have green, or the amber/clearance period, or someone is still on the zebra.
        public bool CarsMustStop => WalkAllowed || PhaseTime < Clearance || Occupied();

        public bool OnCrossing(Vector3 p) => Zebra.Contains(transform.InverseTransformPoint(p));

        private bool Occupied()
        {
            foreach (var npc in LivingNPC.All) if (npc != null && OnCrossing(npc.transform.position)) return true;
            var player = Object.FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            return player != null && OnCrossing(player.transform.position);
        }

        /// Counts NPCs that are on the zebra while cars have green (used by the tests).
        public void Audit()
        {
            if (WalkAllowed || PhaseTime < Clearance + 1.5f) return;
            foreach (var npc in LivingNPC.All)
                if (npc != null && npc.Navigation != null && OnCrossing(npc.transform.position) && !npc.Navigation.WaitingAtCrossing && npc.EnteredCrossingOnRed(this)) Violations++;
        }

        /// The crossing the agent's path is about to use (within the next few metres), if any.
        public static LivingCrossing Ahead(NavMeshAgent agent, Vector3 position)
        {
            if (All.Count == 0 || !agent.hasPath) return null;
            var corners = agent.path.corners;
            var travelled = 0f;
            var previous = position;
            for (var i = 0; i < corners.Length && travelled < 2.5f; i++)
            {
                var segment = corners[i] - previous;
                var steps = Mathf.CeilToInt(segment.magnitude / 0.5f);
                for (var s = 1; s <= steps && travelled < 2.5f; s++)
                {
                    var p = previous + segment * (s / (float)steps);
                    travelled += segment.magnitude / steps;
                    foreach (var crossing in All) if (crossing != null && crossing.OnCrossing(p)) return crossing;
                }
                previous = corners[i];
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = WalkAllowed ? Color.green : Color.red;
            Gizmos.DrawWireCube(Zebra.center, Zebra.size);
        }
    }

    /// A car on a loop of road that stops at a red line (or for people on the zebra) and keeps its
    /// distance to the car in front. NPCs never walk through it: it carries a NavMeshObstacle.
    public sealed class LivingCar : MonoBehaviour
    {
        public Vector3[] Path = new Vector3[0];
        [Tooltip("Path indices that are stop lines, and the crossing each one belongs to.")] public int[] StopPoints = new int[0];
        public LivingCrossing[] StopCrossings = new LivingCrossing[0];
        public float Cruise = 7;
        public float Speed => speed;
        private int target = 1;
        private float speed;

        private void Start()
        {
            if (Path.Length < 2) { enabled = false; return; }
            var best = 0; var d = float.MaxValue;
            for (var i = 0; i < Path.Length; i++) { var dd = (Path[i] - transform.position).sqrMagnitude; if (dd < d) { d = dd; best = i; } }
            target = (best + 1) % Path.Length;
        }

        private void Update()
        {
            var goal = Path[target];
            var delta = goal - transform.position; delta.y = 0;
            if (delta.magnitude < 0.5f) { target = (target + 1) % Path.Length; return; }
            var heading = delta.normalized;
            var wanted = Cruise;
            for (var i = 0; i < StopPoints.Length && i < StopCrossings.Length; i++)
                if (StopPoints[i] == target && StopCrossings[i] != null && StopCrossings[i].CarsMustStop)
                    wanted = Mathf.Min(wanted, Mathf.Max(0, (delta.magnitude - 0.6f) * 1.4f));
            if (Physics.SphereCast(transform.position + Vector3.up * 0.8f + heading * 2.4f, 0.9f, heading, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != transform.root && hit.normal.y < 0.5f)
                wanted = Mathf.Min(wanted, Mathf.Max(0, (hit.distance - 1.2f) * 1.6f));
            speed = Mathf.MoveTowards(speed, wanted, (wanted < speed ? 8f : 2.5f) * Time.deltaTime);
            transform.position += heading * speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * 4);
        }
    }

    /// A tram shuttling between stops: it dwells with its doors open, NPCs (and the player) board
    /// through the door anchors into reserved seats, ride parented to the car (stable while it moves),
    /// and leave at their stop through the door to the platform. Occupancy is limited by the seats.
    public sealed class LivingTram : MonoBehaviour
    {
        public static readonly List<LivingTram> All = new List<LivingTram>();
        public Transform[] Stops = new Transform[0];       // where the tram's origin halts
        public string[] StopPlaceIds = new string[0];       // LivingPlace ids of the tram stops (same order)
        public Transform[] Doors = new Transform[0];        // local door anchors (inside the doorway)
        public float Cruise = 7, Dwell = 14;
        public int CurrentStop { get; private set; }
        public bool Dwelling => dwell > 0;
        public int Boarded { get; private set; }
        public int Alighted { get; private set; }
        private int direction = 1, next = 1;
        private float dwell = 6, speed;
        private readonly List<ActivitySpot> seats = new List<ActivitySpot>();

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Awake()
        {
            seats.AddRange(GetComponentsInChildren<ActivitySpot>(true));
            if (Stops.Length > 0) transform.position = Stops[0].position;
        }

        public string StopId => CurrentStop >= 0 && CurrentStop < StopPlaceIds.Length ? StopPlaceIds[CurrentStop] : "";

        public ActivitySpot ReserveSeat(LivingNPC who)
        {
            foreach (var s in seats) if (s.Reserve(who)) return s;
            return null;
        }

        public Vector3 NearestDoor(Vector3 p)
        {
            var best = transform.position; var d = float.MaxValue;
            foreach (var door in Doors) { var dd = (door.position - p).sqrMagnitude; if (dd < d) { d = dd; best = door.position; } }
            return best;
        }

        public void CountBoarding(bool boarding) { if (boarding) Boarded++; else Alighted++; }

        private void Update()
        {
            if (Stops.Length < 2) return;
            if (dwell > 0)
            {
                dwell -= Time.deltaTime;
                if (dwell <= 0)
                {
                    if (next >= Stops.Length || next < 0) { direction = -direction; next = CurrentStop + direction; }
                }
                return;
            }
            var goal = Stops[next].position;
            var delta = goal - transform.position;
            var distance = delta.magnitude;
            var wanted = Mathf.Min(Cruise, Mathf.Max(0.6f, distance * 0.5f));
            speed = Mathf.MoveTowards(speed, wanted, 1.6f * Time.deltaTime);
            if (distance < 0.05f)
            {
                transform.position = goal;
                speed = 0;
                CurrentStop = next;
                next = CurrentStop + direction;
                if (next >= Stops.Length || next < 0) { direction = -direction; next = CurrentStop + direction; }
                dwell = Dwell;
                return;
            }
            transform.position += delta / distance * Mathf.Min(distance, speed * Time.deltaTime);
        }
    }
}
