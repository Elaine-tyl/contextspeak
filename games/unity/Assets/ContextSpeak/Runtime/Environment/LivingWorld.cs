using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Small birds circling and gliding over an area. Procedural (body + flapping wings), so they
    /// need no animation rig; they settle down at night and in heavy rain, and scatter from the
    /// player. Cheap: a handful of transforms per flock, no physics.
    public sealed class BirdFlock : MonoBehaviour
    {
        public int Count = 7;
        public float Radius = 22, Height = 14, Speed = 7;
        public Color Plumage = new Color(0.22f, 0.21f, 0.2f);
        private readonly List<(Transform body, Transform left, Transform right, float phase, float lane)> birds = new List<(Transform, Transform, Transform, float, float)>();
        private float activity = 1;

        private void Start()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", Plumage);
            material.SetFloat("_Smoothness", 0.2f);
            material.enableInstancing = true;
            for (var i = 0; i < Count; i++)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(body.GetComponent<Collider>());
                body.name = "Bird";
                body.transform.SetParent(transform, false);
                body.transform.localScale = new Vector3(0.07f, 0.11f, 0.07f);
                body.GetComponent<Renderer>().sharedMaterial = material;
                Transform Wing(float side)
                {
                    var hinge = new GameObject("Wing").transform;
                    hinge.SetParent(body.transform, false);
                    var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(wing.GetComponent<Collider>());
                    wing.transform.SetParent(hinge, false);
                    wing.transform.localPosition = new Vector3(side * 2.2f, 0, 0);
                    wing.transform.localScale = new Vector3(4f, 0.12f, 1.4f);
                    wing.GetComponent<Renderer>().sharedMaterial = material;
                    return hinge;
                }
                body.transform.localRotation = Quaternion.Euler(90, 0, 0);
                birds.Add((body.transform, Wing(-1), Wing(1), Random.value * 6.28f, Random.Range(0.75f, 1.2f)));
            }
        }

        private void Update()
        {
            // Fewer and lower birds at night and in rain.
            var target = Mathf.Clamp01(SkyState.Daylight * 1.4f) * (1 - SkyState.Rain * 0.8f);
            activity = Mathf.MoveTowards(activity, target, Time.deltaTime * 0.2f);
            var player = Camera.main != null ? Camera.main.transform.position : transform.position;
            var scatter = Mathf.Clamp01(1 - Vector3.Distance(new Vector3(player.x, 0, player.z), new Vector3(transform.position.x, 0, transform.position.z)) / 10f);
            var t = Time.time;
            for (var i = 0; i < birds.Count; i++)
            {
                var (body, left, right, phase, lane) = birds[i];
                var visible = i < Mathf.CeilToInt(birds.Count * activity);
                if (body.gameObject.activeSelf != visible) body.gameObject.SetActive(visible);
                if (!visible) continue;
                var angle = t * Speed / (Radius * lane) + phase;
                var wobble = Mathf.Sin(t * 0.7f + phase * 3) * 3;
                var local = new Vector3(Mathf.Cos(angle) * Radius * lane, Height + wobble + scatter * 10, Mathf.Sin(angle) * Radius * lane);
                var previous = body.localPosition;
                body.localPosition = local;
                var direction = (local - previous);
                if (direction.sqrMagnitude > 1e-6) body.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(90, 0, 0);
                // Flap in bursts, glide in between.
                var flapping = Mathf.Sin(t * 0.9f + phase) > -0.2f || scatter > 0.2f;
                var flap = flapping ? Mathf.Sin(t * 14 + phase * 5) * 38 : 6;
                left.localRotation = Quaternion.Euler(0, flap, 0);
                right.localRotation = Quaternion.Euler(0, -flap, 0);
            }
        }
    }

    /// Butterflies fluttering between flowers on fair days (hidden in rain, wind and at night).
    public sealed class ButterflySwarm : MonoBehaviour
    {
        public int Count = 6;
        public float Radius = 5;
        public Color[] Colours = { new Color(0.95f, 0.75f, 0.2f), new Color(0.95f, 0.95f, 0.9f), new Color(0.35f, 0.55f, 0.95f), new Color(0.9f, 0.45f, 0.2f) };
        private readonly List<(Transform body, Transform left, Transform right, Vector3 target, float phase)> flies = new List<(Transform, Transform, Transform, Vector3, float)>();

        private void Start()
        {
            for (var i = 0; i < Count; i++)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Colours[i % Colours.Length]);
                material.SetFloat("_Cull", 0);
                var body = new GameObject("Butterfly").transform;
                body.SetParent(transform, false);
                Transform Wing(float side)
                {
                    var hinge = new GameObject("Wing").transform;
                    hinge.SetParent(body, false);
                    var wing = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(wing.GetComponent<Collider>());
                    wing.transform.SetParent(hinge, false);
                    wing.transform.localPosition = new Vector3(side * 0.025f, 0, 0);
                    wing.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    wing.transform.localScale = new Vector3(0.05f, 0.04f, 1);
                    wing.GetComponent<Renderer>().sharedMaterial = material;
                    return hinge;
                }
                flies.Add((body, Wing(-1), Wing(1), Random.insideUnitSphere * Radius, Random.value * 6.28f));
                body.localPosition = new Vector3(Random.Range(-Radius, Radius), 0.6f, Random.Range(-Radius, Radius));
            }
        }

        private void Update()
        {
            var fair = SkyState.Daylight > 0.45f && SkyState.Rain < 0.05f && SkyState.WindSpeed < 9;
            var t = Time.time;
            for (var i = 0; i < flies.Count; i++)
            {
                var (body, left, right, target, phase) = flies[i];
                if (body.gameObject.activeSelf != fair) body.gameObject.SetActive(fair);
                if (!fair) continue;
                var goal = new Vector3(target.x, 0.35f + Mathf.Abs(target.y) * 0.25f, target.z);
                var position = body.localPosition;
                if ((goal - position).sqrMagnitude < 0.05f) flies[i] = (body, left, right, Random.insideUnitSphere * Radius, phase);
                var jitter = new Vector3(Mathf.Sin(t * 5.3f + phase), Mathf.Sin(t * 7.1f + phase * 2) * 1.5f, Mathf.Cos(t * 4.7f + phase)) * 0.35f;
                var velocity = (goal - position).normalized * 0.9f + jitter;
                body.localPosition = position + velocity * Time.deltaTime;
                if (velocity.sqrMagnitude > 0.01f) body.localRotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z));
                var flap = Mathf.Sin(t * 22 + phase) * 65;
                left.localRotation = Quaternion.Euler(0, 0, flap);
                right.localRotation = Quaternion.Euler(0, 0, -flap);
            }
        }
    }

    /// Midges and small insects dancing over vegetation, most visible in low evening sun.
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class InsectCloud : MonoBehaviour
    {
        private ParticleSystem system;
        private float timer;

        private void Awake() => system = GetComponent<ParticleSystem>();

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 1;
            var evening = Mathf.Clamp01(1 - Mathf.Abs(SkyState.SunElevation - 8) / 20f);
            var warm = SkyState.Rain < 0.05f && SkyState.WindSpeed < 7 ? 1 : 0;
            var emission = system.emission;
            emission.rateOverTime = (2 + evening * 14) * warm * Mathf.Clamp01(SkyState.Daylight * 3);
        }

        /// Configure the particle system (called by the environment setup tool).
        public static InsectCloud Create(Transform parent, Vector3 position, Material material)
        {
            var item = new GameObject("Insects");
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            var system = item.AddComponent<ParticleSystem>();
            var main = system.main;
            main.startLifetime = 6;
            main.startSpeed = 0.15f;
            main.startSize = 0.012f;
            main.startColor = new Color(0.12f, 0.1f, 0.08f, 0.9f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.4f;
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            return item.AddComponent<InsectCloud>();
        }
    }
}
