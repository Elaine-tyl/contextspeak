using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A body of water the character can enter: a volume (surface = top of Volume) or the ocean
    /// (surface = OceanWaves' Gerstner height, the same maths the ocean shader uses).
    /// Created by ContextSpeak > Water > Setup Character Water Interaction from the scene's water.
    public sealed class WaterBody : MonoBehaviour
    {
        public static readonly List<WaterBody> All = new List<WaterBody>();
        public WaterKind Kind = WaterKind.Pond;
        public WaterInteractionProfile Profile;
        [Tooltip("World-space volume: top = water surface, bottom = deepest bed.")] public Bounds Volume = new Bounds(Vector3.zero, new Vector3(10, 1.5f, 10));
        [Tooltip("Current in m/s, world XZ (rivers, streams).")] public Vector2 Flow;
        public Renderer Surface;
        [Tooltip("Swimming tuning for this water (defaults per kind when empty).")] public SwimmingProfile Swimming;
        private WaterInteractionProfile runtimeDefault;
        private SwimmingProfile swimmingDefault;

        public SwimmingProfile ActiveSwimming
        {
            get
            {
                if (Swimming != null) return Swimming;
                if (swimmingDefault == null) swimmingDefault = SwimmingProfile.Create(Kind);
                return swimmingDefault;
            }
        }

        public WaterInteractionProfile ActiveProfile
        {
            get
            {
                if (Profile != null) return Profile;
                if (runtimeDefault == null) runtimeDefault = WaterInteractionProfile.Create(Kind);
                return runtimeDefault;
            }
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); SwimmingController.EnsureForScene(); }
        private void OnDisable() { All.Remove(this); }

        public bool InsideXZ(Vector3 p, float margin = 0) =>
            p.x > Volume.min.x + margin && p.x < Volume.max.x - margin && p.z > Volume.min.z + margin && p.z < Volume.max.z - margin;

        public float SurfaceHeight(Vector3 p)
        {
            if (Kind == WaterKind.Ocean && OceanWaves.Active != null) return OceanWaves.Active.Height(p);
            return Volume.max.y;
        }

        /// Height of whatever is under the water at p (pool floor, pond bed, sea bed).
        public float BedHeight(Vector3 p)
        {
            var surface = SurfaceHeight(p);
            var terrain = Terrain.activeTerrain;
            return Floor(new Vector3(p.x, surface + 0.05f, p.z), 60, terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : Volume.min.y);
        }

        private static readonly RaycastHit[] hits = new RaycastHit[16];

        /// First solid surface below `from`, ignoring characters (their own capsules) and triggers.
        public static float Floor(Vector3 from, float distance, float fallback)
        {
            var count = Physics.RaycastNonAlloc(from, Vector3.down, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            var y = fallback;
            for (var i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider is CharacterController || collider.GetComponentInParent<CharacterController>() != null || collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                if (hits[i].distance < best) { best = hits[i].distance; y = hits[i].point.y; }
            }
            return y;
        }

        /// The water body at p: a pool/pond/lake/river volume takes precedence over the ocean (whose
        /// volume is huge and also spans the land); among several, the one with the highest surface.
        /// The ocean only counts where its bed is actually below the water (not on the beach or inland).
        public static WaterBody Find(Vector3 p, float above = 2)
        {
            WaterBody best = null;
            var bestSurface = float.MinValue;
            foreach (var body in All)
            {
                if (body == null || !body.isActiveAndEnabled || !body.InsideXZ(p)) continue;
                var surface = body.SurfaceHeight(p);
                if (p.y > surface + above || p.y < body.Volume.min.y - 3) continue;
                var ocean = body.Kind == WaterKind.Ocean;
                if (best != null && best.Kind != WaterKind.Ocean && ocean) continue;
                if (best != null && !(best.Kind == WaterKind.Ocean && !ocean) && surface <= bestSurface) continue;
                if (ocean && body.BedHeight(p) > surface - 0.03f) continue;          // dry land under the ocean volume
                best = body;
                bestSurface = surface;
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.55f, 1f, 0.5f);
            Gizmos.DrawWireCube(Volume.center, Volume.size);
        }
    }
}
