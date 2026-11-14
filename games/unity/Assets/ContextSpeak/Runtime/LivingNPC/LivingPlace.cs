using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    public enum PlaceKind { Home, Cafe, Supermarket, School, TramStop, Street, Crossing, Restaurant, BreakRoom, Mall, Park }

    /// A place people go to: home, café, supermarket, school, tram stop, park… with a door (Entrance
    /// outside, Inside just past it) and activity spots (seats, counters, machines, beds…) as children.
    /// Schedule "location" strings are matched against Id and Aliases (English or Chinese).
    public sealed class LivingPlace : MonoBehaviour
    {
        public static readonly List<LivingPlace> All = new List<LivingPlace>();
        public string Id = "place";
        public string[] Aliases = new string[0];
        public PlaceKind Kind = PlaceKind.Street;
        [Tooltip("Inside a building (counts as shelter; people inside are not drawn when the interior cannot be seen).")] public bool Indoor;
        [Tooltip("Has a roof (tram shelter, café terrace awning).")] public bool Shelter;
        [Tooltip("The interior is visible from outside (open-front café). If false, people inside are simulated cheaply.")] public bool InteriorVisible = true;
        public Transform Entrance, Inside;
        [Tooltip("Local box of the interior / area.")] public Bounds Area = new Bounds(Vector3.zero, new Vector3(10, 4, 10));
        [Tooltip("Points worth looking at / walking to inside the place (street corners, viewpoints).")] public Transform[] Waypoints = new Transform[0];
        [Tooltip("Optional: a service counter (café, checkout).")] public ServicePoint Service;

        private readonly List<ActivitySpot> spots = new List<ActivitySpot>();
        public IReadOnlyList<ActivitySpot> Spots { get { if (spots.Count == 0) Collect(); return spots; } }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public void Collect()
        {
            spots.Clear();
            spots.AddRange(GetComponentsInChildren<ActivitySpot>(true));
            if (Service == null) Service = GetComponentInChildren<ServicePoint>(true);
        }

        public bool Contains(Vector3 p) => Area.Contains(transform.InverseTransformPoint(p));
        public Vector3 Door => Entrance != null ? Entrance.position : transform.position;
        public Vector3 InsidePoint => Inside != null ? Inside.position : Door;

        public Vector3 RandomPoint(System.Random random)
        {
            if (Waypoints.Length > 0) return Waypoints[random.Next(Waypoints.Length)].position;
            var local = Area.center + new Vector3(((float)random.NextDouble() - 0.5f) * Area.size.x * 0.8f, -Area.extents.y, ((float)random.NextDouble() - 0.5f) * Area.size.z * 0.8f);
            return transform.TransformPoint(local);
        }

        /// The nearest free spot of a kind (and reserves it for `who`).
        public ActivitySpot Reserve(SpotKind kind, LivingNPC who, Vector3 near, string group = null)
        {
            ActivitySpot best = null;
            var bestDistance = float.MaxValue;
            foreach (var spot in Spots)
            {
                if (spot == null || spot.Kind != kind || !spot.Free(who)) continue;
                if (group != null && spot.Group != group) continue;
                var d = (spot.transform.position - near).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = spot; }
            }
            return best != null && best.Reserve(who) ? best : null;
        }

        public int Count(SpotKind kind) { var n = 0; foreach (var s in Spots) if (s != null && s.Kind == kind) n++; return n; }

        private static readonly (string word, PlaceKind kind)[] Words =
        {
            ("住宅", PlaceKind.Home), ("家", PlaceKind.Home), ("home", PlaceKind.Home), ("house", PlaceKind.Home), ("apartment", PlaceKind.Home),
            ("咖啡", PlaceKind.Cafe), ("café", PlaceKind.Cafe), ("cafe", PlaceKind.Cafe), ("coffee", PlaceKind.Cafe),
            ("休息", PlaceKind.BreakRoom), ("staff", PlaceKind.BreakRoom), ("break", PlaceKind.BreakRoom),
            ("超市", PlaceKind.Supermarket), ("supermarket", PlaceKind.Supermarket), ("grocer", PlaceKind.Supermarket),
            ("学校", PlaceKind.School), ("school", PlaceKind.School), ("class", PlaceKind.School),
            ("商场", PlaceKind.Mall), ("mall", PlaceKind.Mall), ("餐厅", PlaceKind.Restaurant), ("restaurant", PlaceKind.Restaurant),
            ("tram", PlaceKind.TramStop), ("电车", PlaceKind.TramStop), ("车站", PlaceKind.TramStop), ("stop", PlaceKind.TramStop),
            ("公园", PlaceKind.Park), ("park", PlaceKind.Park), ("城市", PlaceKind.Street), ("city", PlaceKind.Street), ("street", PlaceKind.Street),
        };

        /// Resolves a schedule location ("Forest Café", "商场 / 餐厅", "home") to a place; `home`/`work` are
        /// the person's own. Alternatives separated by "/" are tried in order; the first that exists wins.
        public static LivingPlace Find(string location, string home = null, string work = null, int seed = 0)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            foreach (var raw in location.Split('/'))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                var lower = part.ToLowerInvariant();
                var exact = ById(part);
                if (exact != null) return exact;
                foreach (var (word, kind) in Words)
                {
                    if (!lower.Contains(word)) continue;
                    if (kind == PlaceKind.Home && home != null) { var h = ById(home); if (h != null) return h; }
                    if (work != null) { var w = ById(work); if (w != null && w.Kind == kind) return w; }
                    var candidates = All.FindAll(p => p != null && p.isActiveAndEnabled && p.Kind == kind);
                    if (candidates.Count > 0) return candidates[Mathf.Abs(seed) % candidates.Count];
                }
            }
            return null;
        }

        public static LivingPlace ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in All)
            {
                if (p == null) continue;
                if (string.Equals(p.Id, id, System.StringComparison.OrdinalIgnoreCase)) return p;
                foreach (var a in p.Aliases) if (string.Equals(a, id, System.StringComparison.OrdinalIgnoreCase)) return p;
            }
            return null;
        }

        public static LivingPlace At(Vector3 p)
        {
            foreach (var place in All) if (place != null && place.Contains(p)) return place;
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Area.center, Area.size);
        }
    }
}
