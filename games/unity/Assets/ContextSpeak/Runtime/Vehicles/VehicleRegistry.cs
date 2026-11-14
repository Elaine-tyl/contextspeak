using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Life
{
    /// Finds the road vehicles in any loaded scene (taxis, vans, SUVs, police cars, airport tugs and
    /// trucks, Meshy car models) and makes them drivable, so every district — airport, city, beach,
    /// life locations — gets driving without per-scene setup (and scene rebuilds keep working).
    /// Skipped: buses/trams/trains/aircraft/boats (ridden, not driven — the city's bus and tram and
    /// the beach watercraft have their own systems), vehicles the city already makes drivable
    /// (CityDrivable), static-batched meshes (they cannot move), and anything not car-sized.
    public static class VehicleRegistry
    {
        private static readonly Regex Vehicle = new Regex(@"\b(taxi|cab|car|van|suv|jeep|truck|tug|sedan|pickup|ute|hatchback|coupe|minivan|limo|limousine|police)\b", RegexOptions.IgnoreCase);
        private static readonly Regex NotVehicle = new Regex(@"\b(bus|tram|train|express|plane|aircraft|jet|boat|kayak|ski|sailboat|speedboat|cart|carpet|card|cargo|carousel|trolley|park|parking|lot|stop|rank|sign|lane|bay|road|wash|toy|placeholder|lift|elevator|cable|stand|shelter)\b", RegexOptions.IgnoreCase);
        private static readonly string[] Skip = { "CityDrivable", "CityTransit", "WaterCraft", "CafePlayer", "NPCController", "LearningNpc", "NpcActor", "RemotePlayer" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            for (var i = 0; i < SceneManager.sceneCount; i++) Register(SceneManager.GetSceneAt(i));
        }

        private static void Loaded(Scene scene, LoadSceneMode mode) => Register(scene);

        /// Splits "CityTaxi_02" / "Meshy_AI_car_2026" into lower-case words.
        public static string Words(string name)
        {
            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return Regex.Replace(name, "[^A-Za-z]+", " ").ToLowerInvariant().Trim();
        }

        public static bool LooksLikeVehicle(string name)
        {
            var words = Words(name);
            return Vehicle.IsMatch(words) && !NotVehicle.IsMatch(words);
        }

        public static void Register(Scene scene)
        {
            if (!scene.isLoaded) return;
            var report = Detect(scene, out _);
            foreach (var (root, label, kind) in report) Make(root, label, kind);
            if (report.Count > 0) Debug.Log("Vehicles: " + report.Count + " drivable in " + scene.name + " (" + string.Join(", ", report.Select(r => r.label)) + ")");
            VehicleDriver.Ensure();
        }

        /// The vehicle roots in a scene, with why others were skipped (used by the editor report too).
        public static List<(GameObject root, string label, DrivableVehicle.VehicleKind kind)> Detect(Scene scene, out List<string> skipped)
        {
            var found = new List<(GameObject, string, DrivableVehicle.VehicleKind)>();
            skipped = new List<string>();
            var taken = new HashSet<GameObject>();
            foreach (var top in scene.GetRootGameObjects())
                foreach (var t in top.GetComponentsInChildren<Transform>(true))
                {
                    if (!LooksLikeVehicle(t.name)) continue;
                    var root = Climb(t).gameObject;
                    if (taken.Contains(root) || root.GetComponent<DrivableVehicle>() != null && taken.Add(root)) continue;
                    if (root.GetComponentsInParent<Component>(true).Concat(root.GetComponentsInChildren<Component>(true)).Any(c => c != null && Skip.Contains(c.GetType().Name)))
                    { skipped.Add(root.name + " (has its own vehicle/character system)"); taken.Add(root); continue; }
                    var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                    if (renderers.Length == 0) continue;
                    if (renderers.Any(r => r.isPartOfStaticBatch)) { skipped.Add(root.name + " (static-batched — clear Batching Static to drive it)"); taken.Add(root); continue; }
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    var horizontal = new Vector2(b.size.x, b.size.z);
                    var length = Mathf.Max(horizontal.x, horizontal.y); var width = Mathf.Min(horizontal.x, horizontal.y);
                    // Rotated roots make the AABB a little larger: allow for it.
                    if (length < 2.3f || length > 14f || width < 1.0f || width > 6f || b.size.y < 0.8f || b.size.y > 4.8f)
                    { skipped.Add(root.name + " (not car-sized: " + b.size.ToString("0.0") + ")"); taken.Add(root); continue; }
                    taken.Add(root);
                    var words = Words(t.name + " " + root.name);
                    var kind = Regex.IsMatch(words, @"\btug\b") ? DrivableVehicle.VehicleKind.Utility
                        : Regex.IsMatch(words, @"\btruck\b") ? DrivableVehicle.VehicleKind.Truck
                        : Regex.IsMatch(words, @"\bvan\b") ? DrivableVehicle.VehicleKind.Van : DrivableVehicle.VehicleKind.Car;
                    var label = Regex.IsMatch(words, @"\b(taxi|cab)\b") ? "taxi"
                        : Regex.IsMatch(words, @"\bpolice\b") ? "police car"
                        : Regex.IsMatch(words, @"\bfuel\b") ? "fuel truck"
                        : Regex.IsMatch(words, @"\bbaggage\b") ? "baggage tug"
                        : Regex.IsMatch(words, @"\bpushback\b") ? "pushback tug"
                        : Regex.IsMatch(words, @"\bsuv\b") ? "SUV"
                        : kind == DrivableVehicle.VehicleKind.Van ? "van" : kind == DrivableVehicle.VehicleKind.Truck ? "truck" : kind == DrivableVehicle.VehicleKind.Utility ? "tug" : "car";
                    found.Add((root, label, kind));
                }
            return found;
        }

        /// Walk up while the parent is just a wrapper around this vehicle (same size), e.g. "Traffic" → "CityTaxi".
        private static Transform Climb(Transform t)
        {
            var size = Size(t);
            while (t.parent != null)
            {
                var parentSize = Size(t.parent);
                if (parentSize.sqrMagnitude > size.sqrMagnitude * 1.2f + 0.01f) break;
                t = t.parent;
            }
            return t;
        }

        private static Vector3 Size(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.zero;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.size;
        }

        private static void Make(GameObject root, string label, DrivableVehicle.VehicleKind kind)
        {
            if (root.GetComponent<DrivableVehicle>() != null) return;
            var vehicle = root.AddComponent<DrivableVehicle>();
            vehicle.Label = label;
            vehicle.Kind = kind;
            switch (kind)
            {
                case DrivableVehicle.VehicleKind.Van: vehicle.MaxSpeed = 14; vehicle.Acceleration = 3.6f; break;
                case DrivableVehicle.VehicleKind.Truck: vehicle.MaxSpeed = 12; vehicle.Acceleration = 2.4f; vehicle.MaxSteer = 30; vehicle.BrakeDeceleration = 7; break;
                case DrivableVehicle.VehicleKind.Utility: vehicle.MaxSpeed = 7; vehicle.Acceleration = 3f; vehicle.MaxSteer = 40; break;
            }
        }
    }
}
