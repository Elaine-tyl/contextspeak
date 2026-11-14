using UnityEngine;

namespace ContextSpeak.Life
{
    /// One place to ask "where is the water?" for any point: pools/ponds/lakes/rivers (WaterBody volumes)
    /// and the ocean (the same Gerstner maths as the ocean shader, so buoyancy follows the visible waves).
    public static class WaterSurfaceSampler
    {
        private const float NoWater = float.NegativeInfinity;

        /// The water body containing p horizontally, whose surface is at most `above` metres below p.
        public static WaterBody BodyAt(Vector3 p, float above = 3) => WaterBody.Find(p, above);

        public static float GetWaterHeight(Vector3 p)
        {
            var body = BodyAt(p, 1e4f);
            return body != null ? body.SurfaceHeight(p) : NoWater;
        }

        public static float GetWaterHeight(WaterBody body, Vector3 p) => body != null ? body.SurfaceHeight(p) : NoWater;

        /// Surface normal from the height field (finite differences over 25 cm — wave scale, not ripples).
        public static Vector3 GetWaterNormal(Vector3 p) => GetWaterNormal(BodyAt(p, 1e4f), p);

        public static Vector3 GetWaterNormal(WaterBody body, Vector3 p)
        {
            if (body == null || body.Kind != WaterKind.Ocean) return Vector3.up;
            const float e = 0.25f;
            var hx = body.SurfaceHeight(p + Vector3.right * e) - body.SurfaceHeight(p - Vector3.right * e);
            var hz = body.SurfaceHeight(p + Vector3.forward * e) - body.SurfaceHeight(p - Vector3.forward * e);
            return new Vector3(-hx, 2 * e, -hz).normalized;
        }

        /// Water velocity at p: the body's current, plus (ocean) a surge down the wave slopes.
        public static Vector3 GetWaterVelocity(Vector3 p, float surge = 0) => GetWaterVelocity(BodyAt(p, 1e4f), p, surge);

        public static Vector3 GetWaterVelocity(WaterBody body, Vector3 p, float surge = 0)
        {
            if (body == null) return Vector3.zero;
            var v = new Vector3(body.Flow.x, 0, body.Flow.y);
            if (surge > 0 && body.Kind == WaterKind.Ocean)
            {
                var n = GetWaterNormal(body, p);
                v += new Vector3(n.x, 0, n.z) * surge;
            }
            return v;
        }

        public static bool IsPointUnderwater(Vector3 p)
        {
            var body = BodyAt(p, 0);
            return body != null && p.y < body.SurfaceHeight(p);
        }

        /// Surface minus bed at p (0 when there is no water).
        public static float GetWaterDepth(Vector3 p) => GetWaterDepth(BodyAt(p, 1e4f), p);

        public static float GetWaterDepth(WaterBody body, Vector3 p)
        {
            if (body == null) return 0;
            return Mathf.Max(0, body.SurfaceHeight(p) - body.BedHeight(p));
        }
    }
}
