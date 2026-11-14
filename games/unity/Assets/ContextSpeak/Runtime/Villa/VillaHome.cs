using System.Collections;
using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// One room of the villa (an axis-aligned box in world space). Used for lights, for choosing the
    /// speaking topic that fits where the player is, and by the validation tool.
    public sealed class VillaRoom : MonoBehaviour
    {
        public static readonly List<VillaRoom> All = new List<VillaRoom>();
        public string Kind = "living";
        public int Floor;
        public Bounds Box = new Bounds(Vector3.zero, Vector3.one);
        public Light[] Lights = new Light[0];
        public float[] BaseIntensity = new float[0];
        public Renderer[] Panels = new Renderer[0];
        public enum LightMode { Auto, On, Off }
        public LightMode Mode
        {
            get
            {
                var key = name.Replace("Room · ", "") + ":";
                foreach (var e in VillaState.Data.roomLights) if (e.StartsWith(key)) return e.EndsWith("on") ? LightMode.On : LightMode.Off;
                return LightMode.Auto;
            }
            set
            {
                var key = name.Replace("Room · ", "") + ":";
                VillaState.Data.roomLights.RemoveAll(e => e.StartsWith(key));
                if (value != LightMode.Auto) VillaState.Data.roomLights.Add(key + (value == LightMode.On ? "on" : "off"));
                VillaState.Touch();
            }
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Contains(Vector3 p) => Box.Contains(p + Vector3.up * 0.5f);
        public static VillaRoom At(Vector3 p) { foreach (var r in All) if (r != null && r.Contains(p)) return r; return null; }

        private void OnDrawGizmosSelected() { Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f); Gizmos.DrawWireCube(Box.center, Box.size); }
    }

    /// Things to do at home with E. Each one moves the player to its spot, plays a matching state for a
    /// few seconds, changes the house (TV on, plants watered, laundry done…) and then offers the related
    /// IELTS speaking topic (T) through VillaFlow — guided mode with mandatory repetition.
    public sealed class VillaFixture : InteractableObject
    {
        public enum Use { Sleep, Computer, Wardrobe, Fridge, Cook, Sink, Washbasin, TV, Laundry, Plants, Pet, Snack, Garage, Read, Exercise, Umbrella }
        public Use Kind = Use.Read;
        public string Topic = "";
        [Tooltip("TV only: the screen renderer that lights up.")] public Renderer Screen;
        [Tooltip("TV only: optional light in front of the screen.")] public Light ScreenLight;
        [Tooltip("Umbrella only: the closed umbrella shown in the stand.")] public Renderer StandUmbrella;
        public Transform Stand;                  // where the player stands / sits (defaults to this transform)
        private bool busy;
        private static Coroutine running;
        private Material screenMaterial;
        private float offerUntil;
        private static VillaFixture offering;

        public override bool CanInteract => base.CanInteract && !busy && (InteractionManager.Instance == null || InteractionManager.Instance.Seat == null);

        public override string Label
        {
            get
            {
                switch (Kind)
                {
                    case Use.Sleep: return SkyState.Hour >= 20 || SkyState.Hour < 6 ? "Go to sleep" : "Take a nap";
                    case Use.Computer: return "Use the computer";
                    case Use.Wardrobe: return "Change clothes";
                    case Use.Fridge: return "Get a drink from the fridge";
                    case Use.Cook: return "Cook a meal";
                    case Use.Sink: return "Wash the dishes";
                    case Use.Washbasin: return "Wash your hands";
                    case Use.TV: return VillaState.Data.tvOn ? "Turn the TV off" : "Turn the TV on";
                    case Use.Laundry: return "Do the laundry";
                    case Use.Plants: return "Water the plants";
                    case Use.Pet: return "Pet " + (string.IsNullOrEmpty(Prompt) ? "the pet" : Prompt);
                    case Use.Snack: return "Make a snack";
                    case Use.Exercise: return "Work out";
                    case Use.Umbrella: return Carrier() != null && Carrier().Holding ? "Put the umbrella back" : "Take an umbrella";
                    default: return Prompt;
                }
            }
        }

        private void Start()
        {
            if (!VillaState.Loaded) VillaState.Load();
            if (Kind == Use.TV && Screen != null) { screenMaterial = Screen.material; ShowScreen(VillaState.Data.tvOn); }
            if (Kind == Use.Umbrella) { var c = Carrier(); if (c != null) c.Holding = VillaState.Data.holdingUmbrella; if (StandUmbrella != null) StandUmbrella.enabled = !VillaState.Data.holdingUmbrella; }
        }

        public override void Interact(InteractionManager manager)
        {
            if (busy || manager == null || manager.Player == null) return;
            if (Kind == Use.Umbrella)
            {
                var c = Carrier(); if (c == null) return;
                c.Holding = !c.Holding; VillaState.Data.holdingUmbrella = c.Holding; VillaState.Touch();
                if (StandUmbrella != null) StandUmbrella.enabled = !c.Holding;
                ActionHud.Instance?.Activity(c.Holding ? (SkyState.Raining ? "Umbrella up — stay dry!" : "You take an umbrella, just in case") : "Umbrella back in the stand", 2.5f);
                return;
            }
            if (Kind == Use.TV) { VillaState.Data.tvOn = !VillaState.Data.tvOn; VillaState.Touch(); ShowScreen(VillaState.Data.tvOn); Offer(); return; }
            running = manager.StartCoroutine(Do(manager.Player));
        }

        private IEnumerator Do(CafePlayer player)
        {
            busy = true;
            var stand = Stand != null ? Stand : transform;
            player.Route.Clear();
            player.Place(stand.position + Vector3.up * 0.05f);
            player.Face(stand.forward);
            var (state, seconds, text) = Action();
            player.Activity = state;
            ActionHud.Instance?.Activity(text, seconds);
            if (Kind == Use.Sleep) { yield return Sleep(); }
            else yield return new WaitForSeconds(seconds);
            Effect(player);
            player.Activity = PlayerAnimState.Idle;
            busy = false;
            Offer();
        }

        private (PlayerAnimState, float, string) Action()
        {
            switch (Kind)
            {
                case Use.Sleep: return (PlayerAnimState.Sit, 2, "Getting into bed…");            // TEMPORARY: no lying-down clip yet (sits on the bed edge)
                case Use.Computer: return (PlayerAnimState.UseObject, 6, "Checking emails and studying online");
                case Use.Wardrobe: return (PlayerAnimState.UseObject, 3, "Choosing an outfit");
                case Use.Fridge: return (PlayerAnimState.Drink, 4, "A cold drink from the fridge");
                case Use.Cook: return (PlayerAnimState.UseObject, 8, "Cooking dinner");
                case Use.Sink: return (PlayerAnimState.UseObject, 5, "Washing the dishes");
                case Use.Washbasin: return (PlayerAnimState.UseObject, 3, "Washing your hands");
                case Use.Laundry: return (PlayerAnimState.UseObject, 5, "Loading the washing machine");
                case Use.Plants: return (PlayerAnimState.UseObject, 4, "Watering the plants");
                case Use.Pet: return (PlayerAnimState.Crouch, 3, "Petting " + Prompt);
                case Use.Snack: return (PlayerAnimState.Eat, 5, "Making and eating a snack");
                case Use.Exercise: return (PlayerAnimState.UseObject, 8, "Working out");
                default: return (PlayerAnimState.UseObject, 3, Prompt);
            }
        }

        private void Effect(CafePlayer player)
        {
            var d = VillaState.Data;
            switch (Kind)
            {
                case Use.Plants: d.plantsWateredDay = VillaState.Today; break;
                case Use.Pet: d.petFedDay = VillaState.Today; var pet = GetComponentInParent<VillaPet>(); if (pet != null) pet.Happy(); break;
                case Use.Laundry: d.laundryDay = VillaState.Today; break;
                case Use.Cook: d.cookedDay = VillaState.Today; break;
                case Use.Wardrobe: d.outfit = (d.outfit + 1) % VillaOutfit.Palettes.Length; VillaOutfit.Apply(player, d.outfit); break;
            }
            VillaState.Touch();
        }

        // Sleeping: fade to black, skip to the morning (or one hour for a nap), fade back.
        private float fade;
        private IEnumerator Sleep()
        {
            var night = SkyState.Hour >= 20 || SkyState.Hour < 6;
            for (fade = 0; fade < 1; fade += Time.unscaledDeltaTime / 1.2f) yield return null;
            fade = 1;
            var target = night ? 7f : (SkyState.Hour + 1) % 24;
            if (SkySystem.Active != null) SkySystem.Active.SetTime(target);
            yield return new WaitForSecondsRealtime(0.8f);
            for (; fade > 0; fade -= Time.unscaledDeltaTime / 1.2f) yield return null;
            fade = 0;
            ActionHud.Instance?.Activity(night ? "Good morning! It's " + SkyState.Clock : "You feel rested", 3);
        }

        private void OnGUI()
        {
            if (fade <= 0) return;
            var c = GUI.color; GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }

        private void ShowScreen(bool on)
        {
            if (ScreenLight != null) ScreenLight.enabled = on;
            if (screenMaterial == null) return;
            screenMaterial.EnableKeyword("_EMISSION");
            screenMaterial.SetColor("_EmissionColor", on ? new Color(0.5f, 0.6f, 0.75f) * 1.6f : Color.black);
        }

        private void Update()
        {
            // TV picture: slow colour changes (a programme, not a static glow).
            if (Kind == Use.TV && VillaState.Data.tvOn && screenMaterial != null)
            {
                var k = Time.time * 0.35f;
                var col = new Color(0.45f + 0.25f * Mathf.Sin(k), 0.5f + 0.2f * Mathf.Sin(k * 1.7f + 1), 0.6f + 0.25f * Mathf.Sin(k * 0.9f + 2)) * 1.5f;
                screenMaterial.SetColor("_EmissionColor", col);
                if (ScreenLight != null) ScreenLight.color = col.linear.maxColorComponent > 0 ? col / col.maxColorComponent : Color.white;
            }
            if (offering == this && Time.time < offerUntil)
            {
                ActionHud.Instance?.Prompt("villa-topic", "T", "Talk about it (IELTS: " + VillaFlow.TopicTitle(Topic) + ")");
                if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                {
                    offering = null;
                    var flow = LocationFlow.Current as VillaFlow;
                    if (flow != null) flow.Practise(Topic);
                }
            }
        }

        private static UmbrellaCarrier carrier;
        private static UmbrellaCarrier Carrier()
        {
            if (carrier == null) { var p = FindFirstObjectByType<CafePlayer>(); if (p != null) carrier = p.GetComponent<UmbrellaCarrier>(); }
            return carrier;
        }

        private void Offer()
        {
            if (string.IsNullOrEmpty(Topic) || !(LocationFlow.Current is VillaFlow)) return;
            offering = this; offerUntil = Time.time + 8;
        }
    }

    /// Keeps a player seat and the residents' spot at the same place consistent: the player can't sit
    /// where a resident sits, and residents don't pick a seat the player is using.
    [RequireComponent(typeof(SeatController), typeof(ActivitySpot))]
    public sealed class VillaSeatLink : MonoBehaviour
    {
        private void Awake()
        {
            var seat = GetComponent<SeatController>(); var spot = GetComponent<ActivitySpot>();
            seat.Allowed = () => spot.Occupant == null || !spot.Occupant.isActiveAndEnabled;
            seat.Seated += () => spot.PlayerUsing = true;
            seat.Stood += () => spot.PlayerUsing = false;
        }
    }

    /// "Change clothes": recolours the clothing materials of the player's avatar (materials whose name
    /// suggests clothing). If the avatar has no separate clothing materials nothing is faked — the
    /// player is told. A real outfit-swap needs per-outfit meshes (see VILLA.md).
    public static class VillaOutfit
    {
        public static readonly Color[] Palettes = { Color.white, new Color(0.55f, 0.66f, 0.85f), new Color(0.9f, 0.72f, 0.62f), new Color(0.55f, 0.7f, 0.55f), new Color(0.35f, 0.35f, 0.4f) };
        private static readonly string[] Words = { "cloth", "shirt", "top", "pant", "trouser", "dress", "jacket", "skirt", "coat", "hoodie", "outfit", "sweater", "jean" };

        public static void Apply(CafePlayer player, int index)
        {
            if (player == null || player.Model == null) return;
            var changed = 0;
            foreach (var r in player.Model.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials)
                {
                    var n = m.name.ToLowerInvariant();
                    var clothing = false;
                    foreach (var w in Words) if (n.Contains(w)) { clothing = true; break; }
                    if (!clothing) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Palettes[index]);
                    changed++;
                }
            ActionHud.Instance?.Activity(changed > 0 ? "New outfit" : "This avatar has no separate clothing materials to change", 3);
        }
    }

    /// The family pet: wanders between favourite spots (garden, sofa, kitchen), follows the player for a
    /// while after being petted, sits and looks around. Uses the NavMesh like the residents.
    public sealed class VillaPet : MonoBehaviour
    {
        public static readonly List<VillaPet> All = new List<VillaPet>();
        public Transform[] Favourites = new Transform[0];
        public float WalkSpeed = 1.1f;
        private NavMeshAgent agent;
        private float waitUntil, followUntil;
        private Transform player;
        private Vector3 lastPos;
        private Transform body;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) { agent.speed = WalkSpeed; agent.angularSpeed = 240; agent.stoppingDistance = 0.3f; agent.enabled = true; }
            var p = FindFirstObjectByType<CafePlayer>(); if (p != null) player = p.transform;
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
            lastPos = transform.position;
        }

        public void Happy() { followUntil = Time.time + 40; }

        private void Update()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            // A small trot bob while moving; still while sitting.
            var speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
            lastPos = transform.position;
            if (body != transform) body.localPosition = new Vector3(0, speed > 0.2f ? Mathf.Abs(Mathf.Sin(Time.time * 10)) * 0.03f : 0, 0);
            if (Time.time < followUntil && player != null)
            {
                if ((player.position - transform.position).sqrMagnitude > 2.2f * 2.2f) agent.SetDestination(player.position);
                else agent.ResetPath();
                return;
            }
            if (Time.time < waitUntil || agent.pathPending || agent.hasPath && agent.remainingDistance > agent.stoppingDistance) return;
            if (Random.value < 0.5f || Favourites.Length == 0)
            {
                waitUntil = Time.time + Random.Range(6f, 18f);       // sit and watch
                return;
            }
            var target = Favourites[Random.Range(0, Favourites.Length)];
            if (target != null && NavMesh.SamplePosition(target.position, out var hit, 1.5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            waitUntil = Time.time + 2;
        }
    }

    /// Lights tied to the day/night cycle: interior lights turn on as daylight fades (rooms with someone
    /// in them first, at full brightness; empty rooms dimmed), garden and pool lights at night. The
    /// emissive light panels follow their lights. "lightsMode" in VillaState can force on/off.
    public sealed class VillaLighting : MonoBehaviour
    {
        public Light[] Garden = new Light[0];
        public Light[] Pool = new Light[0];
        public Light[] Lamps = new Light[0];
        private float[] gardenBase, poolBase, lampBase;
        private float probeHour = -1;
        private Transform player;
        private readonly Dictionary<Material, Color> panelBase = new Dictionary<Material, Color>();

        private void Start()
        {
            gardenBase = Bases(Garden); poolBase = Bases(Pool); lampBase = Bases(Lamps);
            var p = FindFirstObjectByType<CafePlayer>(); if (p != null) player = p.transform;
        }

        private static float[] Bases(Light[] lights) { var b = new float[lights.Length]; for (var i = 0; i < lights.Length; i++) b[i] = lights[i] != null ? lights[i].intensity : 0; return b; }

        private bool Occupied(VillaRoom room)
        {
            if (player != null && room.Contains(player.position)) return true;
            foreach (var npc in LivingNPC.All) if (npc != null && !npc.Hidden && room.Contains(npc.transform.position)) return true;
            return false;
        }

        private void Update()
        {
            var mode = VillaState.Data.lightsMode;
            var need = mode == "on" ? 1 : mode == "off" ? 0 : Mathf.Clamp01(SkyState.LightsNeeded * 1.3f);
            var night = mode == "on" ? 1 : mode == "off" ? 0 : Mathf.Clamp01((SkyState.LightsNeeded - 0.4f) * 2.5f);
            foreach (var room in VillaRoom.All)
            {
                if (room == null) continue;
                var level = room.Mode == VillaRoom.LightMode.On ? 1f : room.Mode == VillaRoom.LightMode.Off ? 0f : need * (Occupied(room) ? 1f : 0.35f);
                level *= EnvironmentTuning.IndoorLights;
                for (var i = 0; i < room.Lights.Length; i++)
                {
                    var l = room.Lights[i]; if (l == null) continue;
                    var target = room.BaseIntensity.Length > i ? room.BaseIntensity[i] * level : level;
                    l.intensity = Mathf.MoveTowards(l.intensity, target, Time.deltaTime * 4);
                    l.enabled = l.intensity > 0.01f;
                }
                foreach (var panel in room.Panels) Glow(panel, level);
            }
            Set(Garden, gardenBase, night); Set(Pool, poolBase, night); Set(Lamps, lampBase, need * 0.8f);
            // Realtime reflection probes follow the time of day (re-rendered every half hour of game time).
            if (Mathf.Abs(SkyState.Hour - probeHour) > 0.5f || probeHour < 0)
            {
                probeHour = SkyState.Hour;
                foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                    if (probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime && probe.refreshMode == UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting) probe.RenderProbe();
            }
        }

        private static void Set(Light[] lights, float[] bases, float level)
        {
            for (var i = 0; i < lights.Length; i++) { var l = lights[i]; if (l == null) continue; l.intensity = bases[i] * level; l.enabled = level > 0.01f; }
        }

        private void Glow(Renderer r, float level)
        {
            if (r == null) return;
            var m = r.material;
            if (!panelBase.TryGetValue(m, out var c)) { c = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.white; if (c.maxColorComponent < 0.01f) c = new Color(1, 0.86f, 0.66f) * 3; panelBase[m] = c; m.EnableKeyword("_EMISSION"); }
            m.SetColor("_EmissionColor", c * level);
        }
    }

    /// Light switch by a room's door: E cycles automatic → always on → off. Saved with the house.
    public sealed class VillaLightSwitch : InteractableObject
    {
        public VillaRoom Room;
        public override string Label => Room == null ? "Lights" : "Lights: " + (Room.Mode == VillaRoom.LightMode.Auto ? "auto → on" : Room.Mode == VillaRoom.LightMode.On ? "on → off" : "off → auto");
        public override void Interact(InteractionManager manager)
        {
            if (Room == null) return;
            Room.Mode = Room.Mode == VillaRoom.LightMode.Auto ? VillaRoom.LightMode.On : Room.Mode == VillaRoom.LightMode.On ? VillaRoom.LightMode.Off : VillaRoom.LightMode.Auto;
            ActionHud.Instance?.Activity(Room.name.Replace("Room · ", "") + " lights: " + Room.Mode.ToString().ToLowerInvariant(), 2);
        }
    }

    /// Gentle wind sway for the Blender-made plants (tropical leaves, shrubs). Each plant pivots at its base
    /// and leans with a slow gust plus a quick flutter, scaled by the shared wind settings.
    public sealed class VillaWind : MonoBehaviour
    {
        public Transform[] Plants = new Transform[0];
        public float Strength = 2.2f;
        private Quaternion[] rest;
        private float[] phase;

        private void Start()
        {
            rest = new Quaternion[Plants.Length]; phase = new float[Plants.Length];
            for (var i = 0; i < Plants.Length; i++) if (Plants[i] != null) { rest[i] = Plants[i].localRotation; phase[i] = Plants[i].position.x * 0.37f + Plants[i].position.z * 0.21f; }
        }

        private void Update()
        {
            var wind = SkyState.WindSpeed * EnvironmentTuning.WindScale;
            var dir = SkyState.WindDirection.normalized;
            var t = Time.time;
            for (var i = 0; i < Plants.Length; i++)
            {
                var p = Plants[i]; if (p == null) continue;
                var gust = Mathf.PerlinNoise(t * 0.25f + phase[i], phase[i]) * 0.8f + 0.2f;
                var flutter = Mathf.Sin(t * 3.1f + phase[i] * 7) * 0.25f;
                var angle = Mathf.Min(8f, Strength * wind * 0.25f) * (gust + flutter);
                p.localRotation = rest[i] * Quaternion.AngleAxis(angle, new Vector3(dir.y, 0, -dir.x));
            }
        }
    }

    /// Ties the villa together at runtime: loads/saves VillaState, restores every car where it was parked,
    /// and saves on quit.
    public sealed class VillaDirector : MonoBehaviour
    {
        public DrivableVehicle[] Cars = new DrivableVehicle[0];

        private void Awake() { if (!VillaState.Loaded) VillaState.Load(); }

        private void Start()
        {
            foreach (var car in Cars)
            {
                if (car == null) continue;
                var key = car.name + ":";
                var entry = VillaState.Data.cars.Find(e => e.StartsWith(key));
                if (entry == null) continue;
                var v = entry.Substring(key.Length).Split(',');
                if (v.Length < 4) continue;
                float F(int i) => float.Parse(v[i], System.Globalization.CultureInfo.InvariantCulture);
                car.transform.SetPositionAndRotation(new Vector3(F(0), F(1), F(2)), Quaternion.Euler(0, F(3), 0));
            }
            var player = FindFirstObjectByType<CafePlayer>();
            if (player != null && VillaState.Data.outfit != 0) VillaOutfit.Apply(player, VillaState.Data.outfit);
        }

        private float carCheck;
        private void Update()
        {
            if (Time.time > carCheck)
            {
                carCheck = Time.time + 2;
                foreach (var car in Cars)
                {
                    if (car == null || car.Occupied) continue;
                    var p = car.transform.position; var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var value = car.name + ":" + p.x.ToString("0.00", inv) + "," + p.y.ToString("0.00", inv) + "," + p.z.ToString("0.00", inv) + "," + car.transform.eulerAngles.y.ToString("0.0", inv);
                    var i = VillaState.Data.cars.FindIndex(e => e.StartsWith(car.name + ":"));
                    if (i >= 0 && VillaState.Data.cars[i] == value) continue;
                    if (i >= 0) VillaState.Data.cars[i] = value; else VillaState.Data.cars.Add(value);
                    VillaState.Touch();
                }
            }
            VillaState.Tick();
        }

        private void OnApplicationQuit() => VillaState.Save();
        private void OnDestroy() => VillaState.Save();
    }
}
