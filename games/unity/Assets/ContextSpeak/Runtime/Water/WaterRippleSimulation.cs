using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Local interactive water around the player: a GPU damped wave equation on a square window
    /// (default 24 m) that scrolls with the player. Any script can add a disturbance with
    /// WaterRippleSimulation.Disturb(position, radius, amount); overlapping disturbances interfere
    /// naturally, waves travel at the profile's speed, reflect off pool walls and banks, drift with a
    /// river's current and fade by the profile's damping. Results are published as global shader
    /// values read by the water shaders (normals, foam, optional displacement) — the base ocean waves
    /// are untouched; ripples are layered on top.
    public sealed class WaterRippleSimulation : MonoBehaviour
    {
        public static WaterRippleSimulation Instance { get; private set; }
        public Transform Follow;
        [Tooltip("Side of the simulated square (m).")] public float Size = 24;
        [Tooltip("0 = by graphics preset (256 / 512 / 1024).")] public int Resolution;
        public float StepHz = 60;
        [Tooltip("Debugger / tuning overrides; otherwise values come from the water body's profile.")] public bool Override;
        public float WaveSpeed = 1.2f;
        [Range(0.9f, 0.9999f)] public float Damping = 0.985f;
        public float NormalStrength = 1, FoamAmount = 1, Displacement;
        public int MaxSources = 32;

        public WaterBody Body { get; private set; }
        public int ActiveSources { get; private set; }
        public float LastStepMilliseconds { get; private set; }
        public int CurrentResolution { get; private set; }
        public float RainRate = 1, FishRate = 1;

        private RenderTexture[] state = new RenderTexture[2];
        private int current;
        private Material material;
        private Vector2 origin;
        private bool hasOrigin;
        private float accumulator, fishTimer;
        private readonly List<Vector4> pending = new List<Vector4>();
        private readonly Vector4[] drops = new Vector4[64];
        private readonly Stopwatch watch = new Stopwatch();
        private static readonly int TexId = Shader.PropertyToID("_CSRippleTex"), AreaId = Shader.PropertyToID("_CSRippleArea"), ParamsId = Shader.PropertyToID("_CSRippleParams"),
            SimArea = Shader.PropertyToID("_Area"), SimShift = Shader.PropertyToID("_Shift"), SimWave = Shader.PropertyToID("_Wave"), SimRect = Shader.PropertyToID("_Rect"),
            SimDrops = Shader.PropertyToID("_Drops"), SimDropCount = Shader.PropertyToID("_DropCount");

        public static void Disturb(Vector3 position, float radius, float amount)
        {
            if (Instance == null) return;
            Instance.Add(position, radius, amount);
        }

        public void Add(Vector3 position, float radius, float amount)
        {
            if (pending.Count >= Mathf.Min(64, MaxSources * 2)) return;
            if (hasOrigin && (position.x < origin.x || position.z < origin.y || position.x > origin.x + Size || position.z > origin.y + Size)) return;
            pending.Add(new Vector4(position.x, position.z, Mathf.Max(0.02f, radius), amount));
        }

        private static int PresetResolution()
        {
            var level = QualitySettings.names[QualitySettings.GetQualityLevel()].ToLowerInvariant();
            return level.Contains("ultra") ? 1024 : level.Contains("medium") || level.Contains("low") || level.Contains("performance") ? 256 : 512;
        }

        private void OnEnable()
        {
            Instance = this;
            var shader = Shader.Find("Hidden/ContextSpeak/Water Ripple Sim");
            if (shader == null || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)) { enabled = false; UnityEngine.Debug.LogWarning("Water ripple simulation unavailable (shader or RGHalf render textures missing)."); return; }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            Allocate(Resolution > 0 ? Resolution : PresetResolution());
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            Release();
            if (material != null) Destroy(material);
        }

        private void Release()
        {
            for (var i = 0; i < 2; i++) if (state[i] != null) { state[i].Release(); Destroy(state[i]); state[i] = null; }
        }

        public void Allocate(int resolution)
        {
            Release();
            CurrentResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 64, 2048);
            for (var i = 0; i < 2; i++)
            {
                state[i] = new RenderTexture(CurrentResolution, CurrentResolution, 0, RenderTextureFormat.RGHalf) { name = "Water ripples " + i, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                state[i].Create();
                var active = RenderTexture.active;
                RenderTexture.active = state[i];
                GL.Clear(false, true, Color.clear);
                RenderTexture.active = active;
            }
            hasOrigin = false;
        }

        private void Update()
        {
            if (material == null || state[0] == null) return;
            if (Follow == null)
            {
                var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
                if (player != null) Follow = player.transform;
                else if (Camera.main != null) Follow = Camera.main.transform;
                else return;
            }
            var centre = Follow.position;
            var body = WaterBody.Find(centre, 4);
            if (body != null) Body = body;
            var profile = Body != null ? Body.ActiveProfile : null;
            if (!Override && profile != null) { WaveSpeed = profile.PropagationSpeed; Damping = profile.DampingPerStep; NormalStrength = profile.RippleStrength; FoamAmount = profile.FoamAmount; }

            var cell = Size / CurrentResolution;
            var wanted = new Vector2(Mathf.Floor((centre.x - Size / 2) / cell) * cell, Mathf.Floor((centre.z - Size / 2) / cell) * cell);
            if (!hasOrigin) { origin = wanted; hasOrigin = true; }
            var shift = (wanted - origin) / Size;
            if (Mathf.Abs(shift.x) >= 1 || Mathf.Abs(shift.y) >= 1) { Allocate(CurrentResolution); origin = wanted; hasOrigin = true; shift = Vector2.zero; }
            origin = wanted;

            var dt = Time.deltaTime;
            AmbientSources(dt, profile);
            accumulator = Mathf.Min(accumulator + dt, 4f / StepHz);
            var step = 1f / StepHz;
            var substeps = WaterInteractionMath.SubSteps(WaveSpeed, step, cell);
            var subDt = step / substeps;
            var courant = WaterInteractionMath.Courant(WaveSpeed, subDt, cell);
            var damping = Mathf.Pow(Damping, subDt * (profile != null ? profile.ReferenceStepHz : 60));
            var flow = Body != null && profile != null && profile.AdvectWithFlow ? Body.Flow * subDt / Size : Vector2.zero;
            var walls = Body != null && Body.Kind != WaterKind.Ocean && (profile == null || profile.WallReflection);
            material.SetVector(SimArea, new Vector4(origin.x, origin.y, Size, 0));
            material.SetVector(SimWave, new Vector4(courant * courant, damping, 5, walls ? 1 : 0));
            if (Body != null) material.SetVector(SimRect, new Vector4(Body.Volume.min.x, Body.Volume.min.z, Body.Volume.max.x, Body.Volume.max.z));

            watch.Restart();
            ActiveSources = pending.Count;
            var first = true;
            while (accumulator >= step)
            {
                accumulator -= step;
                if (first && pending.Count > 0)
                {
                    var count = Mathf.Min(pending.Count, drops.Length);
                    for (var i = 0; i < count; i++) drops[i] = pending[i];
                    pending.Clear();
                    material.SetVectorArray(SimDrops, drops);
                    material.SetInt(SimDropCount, count);
                    Blit(1);
                }
                for (var s = 0; s < substeps; s++)
                {
                    material.SetVector(SimShift, new Vector4(first && s == 0 ? shift.x : 0, first && s == 0 ? shift.y : 0, flow.x, flow.y));
                    Blit(0);
                }
                first = false;
            }
            if (first) pending.Clear();         // no step this frame: drop the queue rather than letting it grow
            watch.Stop();
            LastStepMilliseconds = (float)watch.Elapsed.TotalMilliseconds;

            Shader.SetGlobalTexture(TexId, state[current]);
            Shader.SetGlobalVector(AreaId, new Vector4(origin.x, origin.y, Size, 1f / CurrentResolution));
            Shader.SetGlobalVector(ParamsId, new Vector4(NormalStrength, FoamAmount, 1, Displacement));
        }

        private void Blit(int pass)
        {
            var next = 1 - current;
            Graphics.Blit(state[current], state[next], material, pass);
            current = next;
        }

        /// Rain drops on the water inside the window, and the odd rising fish in ponds and lakes.
        private void AmbientSources(float dt, WaterInteractionProfile profile)
        {
            if (Body == null || !hasOrigin) return;
            var rain = SkyState.Rain * RainRate;
            if (rain > 0.05f)
            {
                var count = Mathf.RoundToInt(rain * 40 * dt * Size * Size / 100f + Random.value);
                for (var i = 0; i < count && pending.Count < 48; i++)
                {
                    var p = new Vector3(origin.x + Random.value * Size, 0, origin.y + Random.value * Size);
                    if (!Body.InsideXZ(p)) continue;
                    pending.Add(new Vector4(p.x, p.z, Random.Range(0.03f, 0.06f), -Random.Range(0.002f, 0.006f) * rain));
                }
            }
            if (Body.Kind == WaterKind.Pond || Body.Kind == WaterKind.Lake)
            {
                fishTimer -= dt * FishRate;
                if (fishTimer <= 0)
                {
                    fishTimer = Random.Range(6f, 18f);
                    var p = new Vector3(Random.Range(Body.Volume.min.x + 1, Body.Volume.max.x - 1), 0, Random.Range(Body.Volume.min.z + 1, Body.Volume.max.z - 1));
                    Add(p, 0.09f, -0.012f);
                }
            }
        }
    }
}
