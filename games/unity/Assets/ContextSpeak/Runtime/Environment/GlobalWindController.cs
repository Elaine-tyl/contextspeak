using UnityEngine;

namespace ContextSpeak.Life
{
    /// One wind for the whole scene, driven by the sky's weather (SkyState.WindSpeed / direction).
    /// Feeds the vegetation and water shaders: steady strength, a slow gust envelope, a travelling
    /// localized gust front and a shared wind clock. Vegetation types react differently through
    /// their own material response (_WindResponse, _Flex, _GrassWave) — see VegetationController.
    [DisallowMultipleComponent]
    public sealed class GlobalWindController : MonoBehaviour
    {
        public static GlobalWindController Active { get; private set; }

        [Tooltip("Multiplies the sky's wind for this scene (sheltered valleys < 1, exposed coasts > 1).")]
        [Range(0, 3)] public float SceneExposure = 1;
        [Tooltip("Seconds between localized gusts.")] public Vector2 GustInterval = new Vector2(6, 16);
        [Tooltip("Radius of a gust front in metres.")] public float GustRadius = 28;
        [Tooltip("Extra m/s inside a gust.")] public float GustBoost = 5;

        public float Strength { get; private set; }
        public Vector2 Direction { get; private set; } = new Vector2(0.8f, 0.6f);
        public Vector2 GustCentre { get; private set; }
        public float GustEnvelope { get; private set; }

        private float clock, nextGust, gustLife, gustAge;
        private Vector2 gustStart;
        private float smoothedStrength;

        private static readonly int StrengthId = Shader.PropertyToID("_WindStrength"), GustId = Shader.PropertyToID("_WindGust"),
            DirectionId = Shader.PropertyToID("_WindDirection"), GustCentreId = Shader.PropertyToID("_WindGustCentre"), TimeId = Shader.PropertyToID("_WindTime");

        private void OnEnable() { Active = this; nextGust = Random.Range(GustInterval.x, GustInterval.y); Push(0); }
        private void OnDisable() { if (Active == this) Active = null; }

        private void Update() => Push(Application.isPlaying ? Time.deltaTime : 0.02f);

        /// Evaluate the wind now (also used by editor test renders).
        public void Push(float dt)
        {
            var target = Mathf.Clamp(SkyState.WindSpeed, 0.3f, 22f) * SceneExposure * (1 + SkyState.Rain * 0.35f);
            smoothedStrength = dt <= 0 ? target : Mathf.MoveTowards(smoothedStrength, target, dt * 1.5f);
            Strength = smoothedStrength;
            Direction = SkyState.WindDirection.sqrMagnitude > 0.01f ? SkyState.WindDirection.normalized : Direction;
            // Calmer wind is steadier; stronger wind is gustier.
            clock += dt * (0.8f + Strength * 0.05f);
            GustEnvelope = Mathf.Clamp01(Mathf.PerlinNoise(clock * 0.08f, 0.37f) * 1.4f - 0.2f);

            // A gust front sweeps across the area round the camera, downwind.
            nextGust -= dt;
            if (nextGust <= 0 && dt > 0)
            {
                nextGust = Random.Range(GustInterval.x, GustInterval.y) / Mathf.Max(0.5f, Strength / 6);
                var focus = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                gustStart = new Vector2(focus.x, focus.z) - Direction * (GustRadius * 2.5f) + Random.insideUnitCircle * GustRadius;
                gustLife = GustRadius * 5 / Mathf.Max(Strength, 2);
                gustAge = 0;
            }
            gustAge += dt;
            var alive = gustLife > 0 && gustAge < gustLife;
            var travel = gustAge * Mathf.Max(Strength, 2);
            GustCentre = gustStart + Direction * travel;
            var gustPower = alive ? Mathf.Sin(Mathf.Clamp01(gustAge / gustLife) * Mathf.PI) * GustBoost * Mathf.Clamp01(Strength / 4) : 0;

            Shader.SetGlobalFloat(StrengthId, Strength);
            Shader.SetGlobalFloat(GustId, GustEnvelope);
            Shader.SetGlobalVector(DirectionId, new Vector4(Direction.x, Direction.y, 0, 0));
            Shader.SetGlobalVector(GustCentreId, new Vector4(GustCentre.x, GustCentre.y, GustRadius, gustPower));
            Shader.SetGlobalFloat(TimeId, Application.isPlaying ? Time.time : 12.3f);
        }
    }
}
