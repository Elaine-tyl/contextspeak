using UnityEngine;

namespace ContextSpeak.Life
{
    /// Pooled water effects for the whole scene: fine spray droplets (falling under gravity), mist,
    /// surface foam, underwater bubbles, drips from a wet character and fading wet footprints.
    /// Each effect is one ParticleSystem that reuses its particles (Emit), so nothing is instantiated
    /// per splash. Sizes and counts scale with the impact and the graphics preset.
    public sealed class WaterSplashController : MonoBehaviour
    {
        private static WaterSplashController instance;
        public static WaterSplashController Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<WaterSplashController>();
                if (instance == null && Application.isPlaying) instance = new GameObject("Water effects").AddComponent<WaterSplashController>();
                return instance;
            }
        }

        public float ParticleScale = 1;
        public int ParticlesEmitted { get; private set; }
        private ParticleSystem droplets, mist, foam, bubbles, drips;
        private static Texture2D dot;
        private Material dotMaterial, footMaterial;
        private readonly Transform[] prints = new Transform[24];
        private readonly float[] printAge = new float[24];
        private int nextPrint;
        private MaterialPropertyBlock block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            instance = this;
            var level = QualitySettings.names[QualitySettings.GetQualityLevel()].ToLowerInvariant();
            ParticleScale = level.Contains("ultra") ? 1.4f : level.Contains("medium") || level.Contains("performance") ? 0.55f : 1;
            dotMaterial = Material("Water droplet", Dot(), 0);
            droplets = System("Spray droplets", 1400, 1f, ParticleSystemRenderMode.Stretch);
            mist = System("Splash mist", 300, -0.02f, ParticleSystemRenderMode.Billboard);
            foam = System("Surface foam", 400, 0, ParticleSystemRenderMode.HorizontalBillboard);
            bubbles = System("Bubbles", 500, 0, ParticleSystemRenderMode.Billboard);
            drips = System("Drips", 300, 1f, ParticleSystemRenderMode.Stretch);
            Fade(mist, 0.35f); Fade(foam, 0.8f, true); Fade(bubbles, 0.6f);
            var foamRenderer = foam.GetComponent<ParticleSystemRenderer>();
            foamRenderer.sortingFudge = -10;
        }

        private static Texture2D Dot()
        {
            if (dot != null) return dot;
            dot = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Water dot" };
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                    var a = Mathf.Clamp01(1 - d);
                    dot.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
                }
            dot.Apply();
            return dot;
        }

        private static Material Material(string name, Texture texture, float darken)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3100;
            material.SetColor("_BaseColor", new Color(1 - darken, 1 - darken, 1 - darken, 1));
            return material;
        }

        private ParticleSystem System(string name, int max, float gravity, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = Mathf.RoundToInt(max * Mathf.Max(0.5f, ParticleScale));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startLifetime = 1;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dotMaterial;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Stretch) { renderer.velocityScale = 0.045f; renderer.lengthScale = 1.1f; }
            system.Play();
            return system;
        }

        private static void Fade(ParticleSystem system, float start, bool grow = false)
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(start, 0), new GradientAlphaKey(start, 0.4f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            if (!grow) return;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 1.4f));
        }

        private int Count(float value) => Mathf.Max(0, Mathf.RoundToInt(value * ParticleScale));

        /// Spray + mist + foam at the waterline. `direction` = movement (adds forward throw), intensity 0…1.
        public void Splash(Vector3 at, float surface, Vector3 direction, float intensity, WaterInteractionProfile profile)
        {
            if (intensity <= 0.01f) return;
            intensity = Mathf.Clamp01(intensity);
            var scale = profile != null ? profile.ParticleScale : 1;
            var tint = profile != null ? profile.DropletTint : new Color(0.86f, 0.93f, 0.96f, 0.85f);
            var light = Mathf.Lerp(0.35f, 1f, SkyState.Daylight);
            var colour = new Color(tint.r * light, tint.g * light, tint.b * light, tint.a);
            var flat = new Vector3(direction.x, 0, direction.z);
            var spread = Mathf.Lerp(0.08f, 0.6f, intensity);
            var count = Count(Mathf.Lerp(6, 130, intensity * intensity) * scale);
            var p = new ParticleSystem.EmitParams();
            for (var i = 0; i < count; i++)
            {
                var disc = Random.insideUnitCircle * spread;
                var up = Mathf.Lerp(1.0f, 5.8f, intensity) * Random.Range(0.45f, 1.1f);
                var out_ = new Vector3(disc.x, 0, disc.y).normalized * Random.Range(0.3f, 1f) * Mathf.Lerp(0.6f, 2.4f, intensity);
                p.position = new Vector3(at.x + disc.x, surface + 0.02f, at.z + disc.y);
                p.velocity = Vector3.up * up + out_ + flat * Random.Range(0.2f, 0.7f);
                p.startLifetime = 2 * up / 9.81f + 0.05f;
                p.startSize = Random.Range(0.012f, 0.035f) * Mathf.Lerp(0.8f, 1.6f, intensity);
                p.startColor = colour;
                droplets.Emit(p, 1);
            }
            var misty = Count(Mathf.Lerp(0, 24, intensity * intensity) * scale);
            for (var i = 0; i < misty; i++)
            {
                var disc = Random.insideUnitCircle * spread * 1.4f;
                p.position = new Vector3(at.x + disc.x, surface + Random.Range(0.05f, 0.6f) * intensity, at.z + disc.y);
                p.velocity = Vector3.up * Random.Range(0.2f, 1.2f) * intensity + new Vector3(disc.x, 0, disc.y);
                p.startLifetime = Random.Range(0.7f, 1.6f);
                p.startSize = Random.Range(0.2f, 0.65f) * Mathf.Lerp(0.6f, 1.4f, intensity);
                p.startColor = new Color(colour.r, colour.g, colour.b, 0.35f);
                mist.Emit(p, 1);
            }
            Foam(at, surface, Mathf.Lerp(0.2f, 1.4f, intensity), Mathf.Lerp(2, 16, intensity) * (profile != null ? profile.FoamAmount : 1), profile);
            ParticlesEmitted += count + misty;
        }

        public void Foam(Vector3 at, float surface, float radius, float amount, WaterInteractionProfile profile)
        {
            var tint = profile != null ? profile.FoamTint : new Color(0.95f, 0.97f, 0.97f, 0.8f);
            var light = Mathf.Lerp(0.35f, 1f, SkyState.Daylight);
            var p = new ParticleSystem.EmitParams();
            var count = Count(amount);
            for (var i = 0; i < count; i++)
            {
                var disc = Random.insideUnitCircle * radius;
                p.position = new Vector3(at.x + disc.x, surface + 0.012f, at.z + disc.y);
                p.velocity = new Vector3(disc.x, 0, disc.y) * 0.35f;
                p.startLifetime = Random.Range(1.4f, 3.6f);
                p.startSize = Random.Range(0.18f, 0.6f) * Mathf.Lerp(0.6f, 1.3f, radius);
                p.rotation = Random.Range(0, 360f);
                p.startColor = new Color(tint.r * light, tint.g * light, tint.b * light, tint.a);
                foam.Emit(p, 1);
            }
        }

        public void Bubbles(Vector3 at, float surface, int amount, WaterInteractionProfile profile)
        {
            var life = profile != null ? profile.BubbleLifetime : new Vector2(0.4f, 2f);
            var p = new ParticleSystem.EmitParams();
            var count = Count(amount * (profile != null ? profile.BubbleIntensity : 1));
            for (var i = 0; i < count; i++)
            {
                var offset = Random.insideUnitSphere * 0.25f;
                var y = Mathf.Min(at.y + offset.y, surface - 0.03f);
                var rise = Random.Range(0.35f, 0.9f);
                p.position = new Vector3(at.x + offset.x, y, at.z + offset.z);
                p.velocity = Vector3.up * rise + Random.insideUnitSphere * 0.08f;
                p.startLifetime = Mathf.Max(0.05f, Mathf.Min(Random.Range(life.x, life.y), (surface - y) / rise));
                p.startSize = Random.Range(0.012f, 0.05f);
                p.startColor = new Color(0.9f, 0.97f, 1f, 0.7f);
                bubbles.Emit(p, 1);
            }
        }

        public void Drip(Vector3 at)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = at, velocity = Vector3.down * 0.2f, startLifetime = 0.9f, startSize = Random.Range(0.01f, 0.018f),
                startColor = new Color(0.85f, 0.92f, 0.96f, 0.8f),
            };
            drips.Emit(p, 1);
        }

        /// A wet footprint on dry ground (fades over ~20 s). Pool of 24 quads.
        public void Footprint(Vector3 at, Vector3 forward, float wetness)
        {
            if (!Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (WaterBody.Find(hit.point, 0.05f) != null) return;
            if (footMaterial == null)
            {
                footMaterial = Material("Wet footprint", Dot(), 0);
                footMaterial.SetColor("_BaseColor", new Color(0, 0, 0, 0.32f));
            }
            var print = prints[nextPrint];
            if (print == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Wet footprint";
                quad.transform.SetParent(transform, false);
                quad.GetComponent<Renderer>().sharedMaterial = footMaterial;
                quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                print = prints[nextPrint] = quad.transform;
            }
            forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            print.gameObject.SetActive(true);
            print.position = hit.point + hit.normal * 0.006f;
            print.rotation = Quaternion.LookRotation(-hit.normal, forward);
            print.localScale = new Vector3(0.11f, 0.27f, 1) * Mathf.Lerp(0.8f, 1.05f, wetness);
            printAge[nextPrint] = 0.0001f + (1 - wetness) * 8;
            nextPrint = (nextPrint + 1) % prints.Length;
        }

        private void Update()
        {
            for (var i = 0; i < prints.Length; i++)
            {
                if (prints[i] == null || printAge[i] <= 0) continue;
                printAge[i] += Time.deltaTime;
                if (printAge[i] > 20) { printAge[i] = 0; prints[i].gameObject.SetActive(false); continue; }
                block ??= new MaterialPropertyBlock();
                block.SetColor(BaseColor, new Color(0, 0, 0, 0.32f * (1 - printAge[i] / 20f)));
                prints[i].GetComponent<Renderer>().SetPropertyBlock(block);
            }
        }
    }
}
