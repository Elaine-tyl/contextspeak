using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// A school of reef fish circling a coral head; snorkellers observe them up close and can
    /// photograph them (P saves a real screenshot to the persistent data folder).
    public sealed class FishSchool : MonoBehaviour
    {
        public string Species = "Blue tang";
        public string Fact = "";
        public Transform[] Fish = new Transform[0];
        public float Radius = 3, Speed = 0.6f, Depth = -3;
        private float[] phase = new float[0];

        private void Start()
        {
            phase = new float[Fish.Length];
            for (var i = 0; i < Fish.Length; i++) phase[i] = i * 2 * Mathf.PI / Mathf.Max(1, Fish.Length) + UnityEngine.Random.value * 0.6f;
        }

        private void Update()
        {
            var centre = transform.position;
            for (var i = 0; i < Fish.Length; i++)
            {
                if (Fish[i] == null) continue;
                phase[i] += Time.deltaTime * Speed * (0.8f + 0.4f * Mathf.Sin(i * 1.7f)) / Radius;
                var r = Radius * (0.7f + 0.3f * Mathf.Sin(phase[i] * 0.7f + i));
                var position = centre + new Vector3(Mathf.Cos(phase[i]) * r, Mathf.Sin(phase[i] * 1.3f + i) * 0.4f, Mathf.Sin(phase[i]) * r);
                var direction = position - Fish[i].position;
                Fish[i].position = position;
                if (direction.sqrMagnitude > 0.0001f) Fish[i].rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180, 0);
            }
        }
    }

    /// Observation and photo tasks while swimming near the reef.
    public sealed class MarineObserver : MonoBehaviour
    {
        public float Range = 5f;
        public event Action<string> Observed, Photographed;
        private FishSchool[] schools = new FishSchool[0];
        private FishSchool watching;
        private float watchTime;
        private string toast = "";
        private float toastUntil;

        private void Start() { schools = FindObjectsByType<FishSchool>(FindObjectsSortMode.None); }

        private void Update()
        {
            var swim = Swimming.Instance;
            var camera = Camera.main;
            if (swim == null || !swim.InWater || camera == null) { watching = null; return; }
            FishSchool best = null;
            var bestScore = 0f;
            foreach (var school in schools)
            {
                if (school == null) continue;
                var offset = school.transform.position - camera.transform.position;
                var distance = offset.magnitude;
                if (distance > Range + school.Radius) continue;
                var facing = Vector3.Dot(camera.transform.forward, offset.normalized);
                if (facing > 0.6f && facing > bestScore) { best = school; bestScore = facing; }
            }
            if (best != watching) { watching = best; watchTime = 0; }
            if (watching == null) return;
            watchTime += Time.deltaTime;
            var progress = SaveManager.Data.beach;
            if (watchTime > 1.5f && !progress.observed.Contains(watching.Species))
            {
                progress.observed.Add(watching.Species);
                SaveManager.Save();
                Show("Observed: " + watching.Species + (watching.Fact != "" ? " — " + watching.Fact : ""));
                Observed?.Invoke(watching.Species);
            }
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                var folder = Path.Combine(Application.persistentDataPath, "Photos");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "PalmBay-" + watching.Species.Replace(" ", "") + "-" + DateTime.Now.ToString("HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(file);
                progress.photos++;
                SaveManager.Save();
                Show("📷 Photo of a " + watching.Species + " saved (" + progress.photos + ")");
                Photographed?.Invoke(watching.Species);
            }
        }

        private void Show(string text) { toast = text; toastUntil = Time.time + 4; LifeUI.Toast(text); }

        private GUIStyle style;
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); }

        private void DrawToolkit()
        {
            if (watching == null) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 17 };
            GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 120, 360, 30), watching.Species + "  ·  P to photograph", style);
        }
    }

    /// Footprints in the sand behind the player (pooled decals that fade).
    public sealed class Footprints : MonoBehaviour
    {
        public Transform Player;
        public Material Print;
        public float Spacing = 0.65f, Life = 25;
        private readonly Queue<(Transform mark, float born)> pool = new Queue<(Transform, float)>();
        private Vector3 last;
        private bool left;

        private void Update()
        {
            if (Player == null || Print == null) return;
            var position = Player.position;
            var terrain = Terrain.activeTerrain;
            var ground = terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : position.y;
            var onSand = ground > -0.4f && ground < 4f && Mathf.Abs(position.y - ground) < 0.25f && (Swimming.Instance == null || !Swimming.Instance.InWater);
            if (onSand && Vector3.Distance(position, last) > Spacing)
            {
                var forward = (position - last); forward.y = 0;
                last = position;
                if (forward.sqrMagnitude < 0.01f) return;
                left = !left;
                var side = Vector3.Cross(Vector3.up, forward.normalized) * (left ? -0.12f : 0.12f);
                Transform mark;
                if (pool.Count >= 60) mark = pool.Dequeue().mark;
                else
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(quad.GetComponent<Collider>());
                    quad.name = "Footprint";
                    quad.GetComponent<Renderer>().sharedMaterial = Print;
                    quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mark = quad.transform;
                    mark.localScale = new Vector3(0.11f, 0.26f, 1);
                }
                mark.position = new Vector3(position.x, ground + 0.015f, position.z) + side;
                mark.rotation = Quaternion.LookRotation(Vector3.down, forward.normalized);
                mark.gameObject.SetActive(true);
                pool.Enqueue((mark, Time.time));
            }
            foreach (var (mark, born) in pool)
                if (mark.gameObject.activeSelf && Time.time - born > Life) mark.gameObject.SetActive(false);
        }
    }

    /// Litter to collect for the beach cleanup.
    public sealed class LitterPiece : InteractableObject
    {
        public override string Label => "Pick up the litter";
        public override bool CanInteract => base.CanInteract && SaveManager.Data.beach.Done("Beach Cleanup Volunteer");
        public override void Interact(InteractionManager manager)
        {
            var progress = SaveManager.Data.beach;
            progress.litter++;
            SaveManager.Save();
            LifeUI.Toast("Litter collected " + progress.litter + "/10");
            if (progress.litter >= 10 && !progress.Done("cleanup-quest")) { progress.Mark("cleanup-quest"); BeachQuest.CompleteOptional("BEACH_CLEANUP", 40); LifeUI.Toast("Beach cleanup complete! +40 XP"); }
            Destroy(gameObject);
        }
    }
}
