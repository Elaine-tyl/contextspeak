using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Makes water react to a character: ripples at each foot as it meets the surface (stronger and
    /// wider when running, none while standing still), bow waves and wakes once the water is at the
    /// knees, drag that slows walking as the water gets deeper, an impact splash + rings + bubbles
    /// sized by the actual entry speed when jumping in, stroke splashes and wakes while swimming,
    /// bubbles under water, wetness/drips/footprints after leaving, and matching sounds.
    /// Works for any rig (male, female, Meshy, Mixamo): uses foot bones when found, otherwise the
    /// footstep events of CafePlayer with a velocity-aware cooldown. Animation events can call
    /// OnLeftFootWater / OnRightFootWater / OnSwimStroke / OnDiveEntry / OnWaterExit directly.
    [RequireComponent(typeof(CafePlayer))]
    public sealed class CharacterWaterInteraction : MonoBehaviour
    {
        public float Height = 1.7f, BodyRadius = 0.3f;
        [Tooltip("Multiplies ripple radii (debugger).")] public float RippleSize = 1;
        public float MaxDrag = 0.62f;
        [Tooltip("Set by SwimmingWaterInteraction while swimming: strokes come from the animated limbs instead of a timer.")]
        [System.NonSerialized] public bool StrokesExternal;
        public AudioClip[] SloshClips = new AudioClip[0], SplashClips = new AudioClip[0];
        public readonly ImmersionStateMachine Immersion = new ImmersionStateMachine();
        public WaterContactDetector Detector { get; private set; }
        public WaterContact State => Immersion.State;
        public float EntrySpeed { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public WaterBody Body => Detector?.Body;

        private CafePlayer player;
        private WaterWetnessController wetness;
        private readonly WaterWakeController wake = new WaterWakeController();
        private readonly FootContactTracker left = new FootContactTracker(), right = new FootContactTracker();
        private AudioSource source;
        private float lastY, verticalSpeed, previousDepth = -1, soundCooldown, bubbleTimer, rebindTimer;
        private Vector3 lastPosition;
        private Transform model;
        private PondSwimming[] pools = new PondSwimming[0];
        private SwimmingController swimmer;

        private void Awake()
        {
            player = GetComponent<CafePlayer>();
            var controller = GetComponent<CharacterController>();
            if (controller != null) { Height = Mathf.Max(1.2f, controller.height); BodyRadius = controller.radius; }
            Immersion.Height = Height;
            wetness = GetComponent<WaterWetnessController>();
            if (wetness == null) wetness = gameObject.AddComponent<WaterWetnessController>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0.6f; source.rolloffMode = AudioRolloffMode.Linear; source.maxDistance = 30;
            lastY = transform.position.y;
            lastPosition = transform.position;
        }

        private void OnEnable() { if (player != null) player.Footstep += OnFootstep; }
        private void OnDisable() { if (player != null) { player.Footstep -= OnFootstep; player.SpeedScale = 1; } }

        private void Start()
        {
            Rebind();
            pools = FindObjectsByType<PondSwimming>(FindObjectsSortMode.None);
            if (WaterRippleSimulation.Instance == null) new GameObject("Water ripple simulation").AddComponent<WaterRippleSimulation>().Follow = transform;
            if (Camera.main != null && Camera.main.GetComponent<UnderwaterInland>() == null) Camera.main.gameObject.AddComponent<UnderwaterInland>();
        }

        /// The playable character can be swapped (male/female): find the rig again when the model changes.
        private void Rebind()
        {
            model = player.Model != null ? player.Model : transform;
            Detector = new WaterContactDetector(transform, Height);
            if (model != transform) { var probe = new WaterContactDetector(model, Height); Detector.LeftFoot = probe.LeftFoot; Detector.RightFoot = probe.RightFoot; Detector.Head = probe.Head; }
            wetness.Bind(model, Height);
        }

        private bool Swimming
        {
            get
            {
                if (swimmer == null) swimmer = GetComponent<SwimmingController>();
                if (swimmer != null && swimmer.isActiveAndEnabled) return swimmer.IsSwimming;
                foreach (var pool in pools) if (pool != null && pool.InWater) return true;
                return ContextSpeak.Life.Swimming.Instance != null && ContextSpeak.Life.Swimming.Instance.InWater;
            }
        }

        private void Update()
        {
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if ((rebindTimer -= dt) <= 0) { rebindTimer = 2; if ((player.Model != null ? player.Model : transform) != model) Rebind(); }
            var position = transform.position;
            verticalSpeed = Mathf.Lerp(verticalSpeed, (position.y - lastY) / dt, 0.6f);
            var flatVelocity = new Vector3(position.x - lastPosition.x, 0, position.z - lastPosition.z) / dt;
            HorizontalSpeed = Mathf.Lerp(HorizontalSpeed, flatVelocity.magnitude, 0.5f);
            lastY = position.y; lastPosition = position;

            Detector.Sample();
            var body = Detector.Body;
            var profile = body != null ? body.ActiveProfile : null;
            var swimming = Swimming;
            var before = State;
            Immersion.Update(Detector.Depth, Detector.HeadDepth, swimming, dt);
            soundCooldown -= dt;

            if (body == null)
            {
                player.SpeedScale = Mathf.MoveTowards(player.SpeedScale, 1, dt * 2);
                if (before != WaterContact.Dry && before != WaterContact.ExitingWater && State == WaterContact.ExitingWater) OnWaterExit();
                previousDepth = -1;
                return;
            }
            var surface = Detector.Surface;
            var depth = Detector.Depth;
            var effects = WaterSplashController.Instance;

            // Jump / fall into the water: splash sized by the measured entry speed.
            if (previousDepth <= 0.02f && depth > 0.02f && verticalSpeed < -1.8f)
            {
                EntrySpeed = -verticalSpeed;
                var area = Mathf.Clamp01(depth / Height + 0.4f);
                var impact = WaterInteractionMath.Impact(EntrySpeed, HorizontalSpeed, area);
                var radius = WaterInteractionMath.JumpRadius(profile, impact) * RippleSize;
                var p = new Vector3(position.x, surface, position.z);
                WaterRippleSimulation.Disturb(p, radius, -Mathf.Lerp(0.05f, 0.16f, impact) * profile.RippleStrength);
                WaterRippleSimulation.Disturb(p, radius * 0.45f, -Mathf.Lerp(0.03f, 0.09f, impact) * profile.RippleStrength);
                effects?.Splash(p, surface, flatVelocity, WaterInteractionMath.JumpSplash(profile, impact), profile);
                effects?.Bubbles(new Vector3(position.x, surface - Mathf.Min(depth, 1.2f) * 0.6f, position.z), surface, Mathf.RoundToInt(Mathf.Lerp(8, 40, impact)), profile);
                Play(impact > 0.45f ? WaterAudio.Plunge : WaterAudio.Splash, Mathf.Lerp(0.5f, 1f, impact), true);
            }
            previousDepth = depth;

            // Feet: ripples and splashes as each foot meets the water.
            var level = ImmersionStateMachine.Level(State);
            if (level >= 1 && level <= 3)
            {
                var run = WaterInteractionMath.RunAmount(HorizontalSpeed, player.WalkSpeed, player.RunSpeed);
                var cooldown = WaterInteractionMath.Cooldown(profile, run);
                if (Detector.HasFeet)
                {
                    var l = left.Update(Detector.LeftFoot.position, surface, dt, cooldown);
                    if (l > 0) FootRipple(Detector.LeftFoot.position, surface, l, profile, depth);
                    var r = right.Update(Detector.RightFoot.position, surface, dt, cooldown);
                    if (r > 0) FootRipple(Detector.RightFoot.position, surface, r, profile, depth);
                }
            }

            // Drag: walking gets slower as the water gets deeper (more in surf and currents).
            var depthFactor = Mathf.InverseLerp(Immersion.FeetLevel, Immersion.ChestLevel, depth);
            var drag = Mathf.Clamp01(depthFactor * MaxDrag * profile.Drag);
            player.SpeedScale = Mathf.MoveTowards(player.SpeedScale, swimming ? 1 : 1 - drag, dt * 2);

            // Wakes from knee depth on, strokes while swimming, bubbles under water.
            if (level >= 2)
            {
                var stroke = wake.Tick(position, flatVelocity, surface, Mathf.InverseLerp(Immersion.KneeLevel, Immersion.ChestLevel, depth), level >= 4 && !StrokesExternal, level == 5, dt, profile, effects, BodyRadius);
                if (stroke) Play(WaterAudio.Stroke, Mathf.Clamp01(HorizontalSpeed / 2) * 0.6f + 0.2f, false);
            }
            if (level == 5 && (bubbleTimer -= dt) <= 0)
            {
                bubbleTimer = HorizontalSpeed > 0.3f ? 0.25f : Random.Range(1.5f, 3f);
                effects?.Bubbles(Detector.HeadPoint - Vector3.up * 0.1f, surface, HorizontalSpeed > 0.3f ? 4 : 6, profile);
            }
            if (before < WaterContact.Underwater && State == WaterContact.Underwater) OnDiveEntry();
            if (before != WaterContact.ExitingWater && State == WaterContact.ExitingWater) OnWaterExit();

            // Wet to the height of the water (whole-body materials: the level sets how wet).
            var soak = level switch { 1 => 0.18f, 2 => 0.38f, 3 => 0.62f, 4 => 0.9f, 5 => 1f, _ => 0f };
            if (soak > 0) wetness.Soak(soak);
        }

        private void FootRipple(Vector3 foot, float surface, float strength, WaterInteractionProfile profile, float depth)
        {
            var radius = WaterInteractionMath.StepRadius(profile, HorizontalSpeed, player.WalkSpeed, player.RunSpeed) * RippleSize;
            var splash = WaterInteractionMath.StepSplash(profile, HorizontalSpeed, player.WalkSpeed, player.RunSpeed) * strength;
            // Deeper water: more water displaced around the leg, but the leg throws less spray.
            var deep = Mathf.InverseLerp(0, Immersion.KneeLevel, depth);
            var p = new Vector3(foot.x, surface, foot.z);
            WaterRippleSimulation.Disturb(p, radius * Mathf.Lerp(1, 1.5f, deep), -Mathf.Lerp(0.008f, 0.022f, splash) * Mathf.Lerp(1, 1.6f, deep) * profile.RippleStrength);
            var direction = transform.forward * HorizontalSpeed;
            if (splash > 0.05f) WaterSplashController.Instance?.Splash(p, surface, direction, splash * Mathf.Lerp(1, 0.55f, deep), profile);
            if (soundCooldown <= 0) Play(splash > 0.45f ? WaterAudio.Splash : WaterAudio.Slosh, Mathf.Lerp(0.25f, 0.8f, splash), false);
        }

        /// Fallback for rigs without usable foot bones: the controller's own stride events.
        private void OnFootstep(string surface, float intensity)
        {
            if (Detector == null || Detector.HasFeet || Detector.Body == null) return;
            var level = ImmersionStateMachine.Level(State);
            if (level < 1 || level > 3 || HorizontalSpeed < 0.2f) return;
            var side = Random.value < 0.5f ? -0.12f : 0.12f;
            var foot = transform.position + transform.right * side + transform.forward * 0.15f;
            FootRipple(foot, Detector.Surface, Mathf.Lerp(0.4f, 1f, intensity), Detector.Body.ActiveProfile, Detector.Depth);
        }

        private void Play(AudioClip fallback, float volume, bool force)
        {
            if (!force && soundCooldown > 0) return;
            soundCooldown = 0.12f;
            var clips = fallback == WaterAudio.Slosh ? SloshClips : fallback == WaterAudio.Splash || fallback == WaterAudio.Plunge ? SplashClips : null;
            var clip = clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : fallback;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ---------------------------------------------------------------- animation events
        public void OnLeftFootWater() { if (Detector?.LeftFoot != null && Detector.Body != null) FootRipple(Detector.LeftFoot.position, Detector.Surface, 0.8f, Detector.Body.ActiveProfile, Detector.Depth); }
        public void OnRightFootWater() { if (Detector?.RightFoot != null && Detector.Body != null) FootRipple(Detector.RightFoot.position, Detector.Surface, 0.8f, Detector.Body.ActiveProfile, Detector.Depth); }
        public void OnSwimStroke() { if (Detector?.Body != null) { WaterRippleSimulation.Disturb(transform.position + transform.forward * 0.5f, 0.22f, -0.016f); Play(WaterAudio.Stroke, 0.5f, false); } }

        public void OnDiveEntry()
        {
            if (Detector?.Body == null) return;
            var p = new Vector3(transform.position.x, Detector.Surface, transform.position.z);
            WaterRippleSimulation.Disturb(p, 0.5f, -0.04f);
            WaterSplashController.Instance?.Bubbles(Detector.HeadPoint, Detector.Surface, 18, Detector.Body.ActiveProfile);
            Play(WaterAudio.Splash, 0.5f, true);
        }

        public void OnWaterExit()
        {
            Play(WaterAudio.Exit, 0.6f, true);
            wetness.Wetness = Mathf.Max(wetness.Wetness, 0.3f);
        }

        private void LateUpdate()
        {
            // Wet footprints on dry ground after leaving the water.
            if (State != WaterContact.Dry && State != WaterContact.ExitingWater) return;
            if (wetness.Wetness < 0.22f || HorizontalSpeed < 0.3f) return;
            stepTimer -= Time.deltaTime * HorizontalSpeed;
            if (stepTimer > 0) return;
            stepTimer = 0.75f;
            footSide = -footSide;
            WaterSplashController.Instance?.Footprint(transform.position + transform.right * 0.11f * footSide, transform.forward, wetness.Wetness);
        }
        private float stepTimer;
        private float footSide = 1;
    }

    /// Underwater look for pools, ponds, rivers and lakes (the ocean has its own UnderwaterView):
    /// when the camera goes below the sampled surface the scene tints and absorbs light like water,
    /// sounds are muffled and a low underwater bed fades in; it fades back at the surface.
    public sealed class UnderwaterInland : MonoBehaviour
    {
        public static bool Active { get; private set; }
        private float blend;
        private Color skyFog;
        private float skyDensity;
        private bool skyFogOn;
        private AudioSource bed;
        private AudioLowPassFilter lowPass;
        private UnityEngine.UIElements.VisualElement tint;

        private void LateUpdate()
        {
            var p = transform.position;
            var body = WaterBody.Find(p, 0.5f);
            var oceanActive = UnderwaterView.Active;
            var under = body != null && body.Kind != WaterKind.Ocean && p.y < body.SurfaceHeight(p) - 0.03f;
            if (!under && blend <= 0)
            {
                Active = false;
                if (!oceanActive) { skyFog = RenderSettings.fogColor; skyDensity = RenderSettings.fogDensity; skyFogOn = RenderSettings.fog; }
                SetAudio(oceanActive ? 1 : 0);
                return;
            }
            blend = Mathf.MoveTowards(blend, under ? 1 : 0, Time.deltaTime * 3);
            Active = blend > 0.5f;
            SkySystem.FogLocked = blend > 0;
            RenderSettings.fog = blend > 0 || skyFogOn;
            var water = body != null && body.Kind == WaterKind.Pool ? new Color(0.12f, 0.45f, 0.55f) : new Color(0.1f, 0.28f, 0.22f);
            RenderSettings.fogColor = Color.Lerp(skyFog, water * Mathf.Lerp(0.3f, 1f, SkyState.Daylight), blend);
            RenderSettings.fogDensity = Mathf.Lerp(skyDensity, body != null && body.Kind == WaterKind.Pool ? 0.06f : 0.14f, blend);
            if (blend <= 0) { SkySystem.FogLocked = false; RenderSettings.fog = skyFogOn; }
            SetAudio(blend);
            if (tint == null)
            {
                tint = new UnityEngine.UIElements.VisualElement { pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
                tint.style.position = UnityEngine.UIElements.Position.Absolute;
                tint.style.left = 0; tint.style.top = 0; tint.style.right = 0; tint.style.bottom = 0;
                ContextSpeak.Cafe.UI.GameUiRoot.Attach(gameObject).Add(tint);
            }
            tint.style.backgroundColor = new Color(water.r, water.g, water.b, 0.22f * blend);
        }

        private void SetAudio(float amount)
        {
            if (bed == null && amount <= 0) return;
            if (bed == null)
            {
                bed = gameObject.AddComponent<AudioSource>();
                bed.clip = WaterAudio.Underwater; bed.loop = true; bed.spatialBlend = 0; bed.volume = 0; bed.Play();
            }
            bed.volume = Mathf.MoveTowards(bed.volume, amount * 0.5f, Time.deltaTime);
            var listener = GetComponent<AudioListener>();
            if (listener != null)
            {
                if (lowPass == null) { lowPass = gameObject.GetComponent<AudioLowPassFilter>(); if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>(); }
                lowPass.cutoffFrequency = Mathf.Lerp(22000, 750, amount);
            }
        }
    }
}
