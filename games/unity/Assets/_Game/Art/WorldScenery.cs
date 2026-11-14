using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ContextSpeak.Cafe
{
    [Serializable]
    public sealed class SceneryEntry
    {
        public string id, name, sourceHash, usage, zone, purpose, model, motion;
        public string[] motions;
        public float[] position;
        public float extent;
        public float targetHeight;
        public float realSize;          // real-world largest dimension in metres; 0 keeps the exhibit size
        public int triangleBudget;
        public float Size => realSize > 0 ? realSize : extent;
        public Vector3 Point => new Vector3(position[0], position[1], position[2]);
        public string Label => System.Text.RegularExpressions.Regex.Replace(name, @"_\d{8}.*$", "").Replace('_', ' ');
    }

    /// Completion callbacks own handle release; IsDone can precede delivery of that callback.
    public sealed class SceneryLease
    {
        public bool Delivered { get; private set; }
        private bool released;
        public void Complete() { Delivered = true; }
        public bool TryRelease()
        {
            if (!Delivered || released) return false;
            released = true;
            return true;
        }
    }

    /// Geometry is streamed separately; catalog, collision proxies and map points stay lightweight.
    public sealed class WorldScenery : MonoBehaviour
    {
        [Serializable] private sealed class Catalog { public SceneryEntry[] items; }
        private sealed class Load
        {
            public SceneryEntry Entry;
            public AsyncOperationHandle<GameObject> Handle;
            public readonly SceneryLease Lease = new SceneryLease();
        }
        public TextAsset CatalogJson;
        public SceneryEntry[] Entries => entries ?? (entries = JsonUtility.FromJson<Catalog>("{\"items\":" + CatalogJson.text + "}").items);
        private SceneryEntry[] entries;
        private readonly Dictionary<string, Load> active = new Dictionary<string, Load>();
        private readonly HashSet<string> wanted = new HashSet<string>();
        private CafePlayer player;
        private float nextUpdate;
        private bool closing;
        public string[] LoadedIds => active.Where(pair => pair.Value.Lease.Delivered && pair.Value.Handle.Status == AsyncOperationStatus.Succeeded).Select(pair => pair.Key).ToArray();
        public int LoadedCount => LoadedIds.Length;
        public int LoadingCount => active.Values.Count(load => !load.Lease.Delivered);
        public int LoadedTriangleBudget => active.Values.Sum(load => load.Entry.triangleBudget);
        public const int MaximumVisible = 20;
        public static bool CanReserve(int instances, int pending, int triangles, int next) =>
            instances < MaximumVisible && pending < 2 && triangles + next <= 400000;

        public static Vector3 SafeSpawn(Vector3 requested, Vector3 fallback)
        {
            // New scenery may occupy an old save; avoid disconnected prop-top navmesh islands too.
            if (!NavMesh.SamplePosition(requested, out var hit, 5, NavMesh.AllAreas)) return fallback;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(fallback, hit.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete ? hit.position : fallback;
        }

        private void Start() { player = FindFirstObjectByType<CafePlayer>(); }
        private void Update()
        {
            if (player == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.4f;
            wanted.Clear();
            var triangles = 0;
            var nearby = Entries.Where(entry => entry.usage == "world")
                .Where(entry => Vector3.Distance(entry.Point, player.transform.position) < 22)
                .OrderBy(entry => Vector3.SqrMagnitude(entry.Point - player.transform.position)).Take(MaximumVisible).ToArray();
            foreach (var entry in nearby)
            {
                if (triangles + entry.triangleBudget > 400000) continue;
                triangles += entry.triangleBudget;
                wanted.Add(entry.id);
            }
            foreach (var load in active.Values.Where(load => !wanted.Contains(load.Entry.id)).ToArray()) Release(load);
            foreach (var entry in nearby.Where(entry => wanted.Contains(entry.id) && !active.ContainsKey(entry.id)))
            {
                if (!CanReserve(active.Count, LoadingCount, LoadedTriangleBudget, entry.triangleBudget)) continue;
                var load = new Load { Entry = entry };
                load.Handle = Addressables.InstantiateAsync(entry.id, entry.Point, Quaternion.identity, transform);
                active.Add(entry.id, load);
                load.Handle.Completed += result =>
                {
                    load.Lease.Complete();
                    if (result.Status == AsyncOperationStatus.Succeeded && result.Result != null) Fit(result.Result, entry);
                    if (closing || !wanted.Contains(entry.id) || result.Status != AsyncOperationStatus.Succeeded) Release(load);
                };
            }
        }
        /// Real-world scale, standing on the ground: exports are normalised per category and keep the mesh's own pivot.
        public static void Fit(GameObject item, SceneryEntry entry)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds Measure()
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
            var size = Measure().size;
            var largest = Mathf.Max(size.x, size.y, size.z);
            if (entry.realSize > 0 && largest > 0.0001f) item.transform.localScale *= entry.realSize / largest;
            var fitted = Measure();
            var ground = entry.Point;
            item.transform.position += new Vector3(ground.x - fitted.center.x, ground.y - fitted.min.y, ground.z - fitted.center.z);
        }
        private void Release(Load load)
        {
            // Pending reservations remain until their callback, including during scene teardown.
            if (!load.Lease.TryRelease()) return;
            if (active.TryGetValue(load.Entry.id, out var owned) && ReferenceEquals(owned, load)) active.Remove(load.Entry.id);
            if (load.Handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(load.Handle);
            else Addressables.Release(load.Handle);
        }
        private void OnDestroy()
        {
            closing = true;
            foreach (var load in active.Values.ToArray()) Release(load);
        }
    }
}
