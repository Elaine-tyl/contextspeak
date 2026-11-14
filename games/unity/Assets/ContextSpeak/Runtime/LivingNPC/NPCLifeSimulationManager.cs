using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Life
{
    /// World time for NPC routines: the sky system's hour when present (so routines follow the visible
    /// day), otherwise its own clock. Days are counted for memories ("seen yesterday").
    public static class LivingClock
    {
        public static float Hour => SkySystem.Active != null ? SkyState.Hour : own;
        public static int Day { get; private set; }
        public static float DayLengthMinutes = 24;
        private static float own = 8, last = -1;

        public static float Tick(float dt)
        {
            var before = Hour;
            if (SkySystem.Active == null) own = Mathf.Repeat(own + dt / (DayLengthMinutes * 60f) * 24f, 24);
            var after = Hour;
            if (last >= 0 && after < before - 12) Day++;
            last = after;
            var gameHours = Mathf.Repeat(after - before, 24);
            return gameHours > 12 ? 0 : gameHours;
        }

        public static void Advance(float hours)
        {
            var target = Mathf.Repeat(Hour + hours, 24);
            if (target < Hour) Day++;
            if (SkySystem.Active != null) SkySystem.Active.SetTime(target);
            else own = target;
        }

        public static void Set(float hour)
        {
            if (SkySystem.Active != null) SkySystem.Active.SetTime(hour); else own = hour;
        }
    }

    /// Events NPCs can perceive (sound, something dropped, rain starting, a bump…).
    public static class WorldEvents
    {
        public struct Event { public string Kind; public Vector3 Position; public float Radius, Time; public LivingNPC Source; }
        public static readonly List<Event> Recent = new List<Event>();
        public static void Raise(string kind, Vector3 position, float radius, LivingNPC source = null)
        {
            Recent.Add(new Event { Kind = kind, Position = position, Radius = radius, Time = UnityEngine.Time.time, Source = source });
            if (Recent.Count > 32) Recent.RemoveAt(0);
        }
    }

    /// NPCLifeSimulationManager + NPCCrowdLODManager + perception scheduler + social director:
    ///  • Tier 1 (near, visible or interacting): every frame — animation detail, gaze, perception, social;
    ///  • Tier 2 (visible further away): every 3rd frame — navigation + activities, no gaze/micro detail;
    ///  • Tier 3 (far or inside a closed building): once a second — schedule logic only; travel is
    ///    simulated by time and the body reappears at the right place only where nobody can see the jump.
    /// Perception runs a few NPCs per frame (staggered), never physics queries for everyone every frame.
    /// Saves NPC state + memories to living_npcs.json every minute and on exit.
    public sealed class NPCLifeSimulationManager : MonoBehaviour
    {
        public static NPCLifeSimulationManager Instance { get; private set; }
        public int MaxFullTier = 14;
        public float FullDistance = 28, BackgroundDistance = 70;
        public float PerceptionRadius = 9, FieldOfView = 120;
        public float MeasuredMilliseconds { get; private set; }
        public int[] TierCounts { get; } = new int[4];
        public int Greetings { get; private set; }
        public int Bumps { get; private set; }
        public int PlayerGlances { get; private set; }

        private CafePlayer player;
        private int frame, perceptionCursor;
        private float tierTimer, saveTimer, socialTimer;
        private readonly Stopwatch watch = new Stopwatch();
        private readonly Dictionary<LivingNPC, float> background = new Dictionary<LivingNPC, float>();
        private readonly Dictionary<LivingNPC, (float until, float next)> glances = new Dictionary<LivingNPC, (float, float)>();
        private readonly Dictionary<string, float> pairCooldown = new Dictionary<string, float>();
        private bool wasRaining;

        private void Awake()
        {
            Instance = this;
            NPCRelationshipMemory.Load();
        }

        private void OnDestroy() { if (Instance == this) { SaveAll(); Instance = null; } }
        private void OnApplicationQuit() => SaveAll();

        private IEnumerator Start()
        {
            player = FindFirstObjectByType<CafePlayer>();
            yield return null;
            // Restore saved positions for this scene (NPC state persists through save/load).
            var scene = SceneManager.GetActiveScene().name;
            foreach (var npc in LivingNPC.All)
            {
                var state = NPCRelationshipMemory.State(npc.Id, scene);
                if (state != null) npc.Restore(state);
            }
        }

        public void SaveAll()
        {
            var scene = SceneManager.GetActiveScene().name;
            foreach (var npc in LivingNPC.All) if (npc != null) NPCRelationshipMemory.Store(npc.Capture(scene));
            NPCRelationshipMemory.Data.Day = LivingClock.Day;
            NPCRelationshipMemory.Data.Hour = LivingClock.Hour;
            NPCRelationshipMemory.Save();
        }

        private void Update()
        {
            watch.Restart();
            var dt = Time.deltaTime;
            var gameHours = LivingClock.Tick(dt);
            frame++;
            if (player == null) player = FindFirstObjectByType<CafePlayer>();
            if ((tierTimer -= dt) <= 0) { tierTimer = 0.5f; AssignTiers(); }

            foreach (var npc in LivingNPC.All.ToArray())
            {
                if (npc == null) continue;
                if (npc.Tier == 1) npc.Simulate(dt, gameHours);
                else
                {
                    background.TryGetValue(npc, out var acc);
                    acc += dt;
                    var interval = npc.Tier == 2 ? 3 * dt : 1f;
                    var stagger = (npc.GetInstanceID() & 3) == (frame & 3);
                    if (acc >= interval && (npc.Tier == 3 || stagger || acc > interval * 2)) { npc.Simulate(acc, gameHours * acc / Mathf.Max(dt, 1e-4f)); acc = 0; }
                    background[npc] = acc;
                }
            }
            Perceive();
            if ((socialTimer -= dt) <= 0) { socialTimer = 0.5f; Social(); }
            WeatherReaction();
            foreach (var crossing in LivingCrossing.All) if (crossing != null) crossing.Audit();
            if ((saveTimer += dt) > 60) { saveTimer = 0; SaveAll(); }
            watch.Stop();
            MeasuredMilliseconds = Mathf.Lerp(MeasuredMilliseconds, (float)watch.Elapsed.TotalMilliseconds, 0.05f);
        }

        // ------------------------------------------------------------------ LOD

        private readonly List<(LivingNPC npc, float score)> ranking = new List<(LivingNPC, float)>();

        private void AssignTiers()
        {
            for (var i = 0; i < TierCounts.Length; i++) TierCounts[i] = 0;
            var camera = Camera.main;
            var origin = player != null ? player.transform.position : camera != null ? camera.transform.position : Vector3.zero;
            ranking.Clear();
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null) continue;
                var d = Vector3.Distance(origin, npc.transform.position);
                var visible = Visible(camera, npc.transform.position);
                var closed = npc.Hidden;
                int tier;
                if (npc.InConversation && d < 40) tier = 1;
                else if (closed) tier = 3;
                else if (d < FullDistance && (visible || d < 8)) tier = 1;
                else if (d < BackgroundDistance || visible) tier = 2;
                else tier = 3;
                if (tier == 1) ranking.Add((npc, d));
                npc.Tier = tier;
            }
            // Budget: only the nearest N get full detail.
            ranking.Sort((a, b) => a.score.CompareTo(b.score));
            for (var i = MaxFullTier; i < ranking.Count; i++) ranking[i].npc.Tier = 2;
            foreach (var npc in LivingNPC.All) if (npc != null) TierCounts[npc.Tier]++;
        }

        private static bool Visible(Camera camera, Vector3 p)
        {
            if (camera == null) return false;
            var v = camera.WorldToViewportPoint(p + Vector3.up);
            return v.z > 0 && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        // ------------------------------------------------------------------ perception (staggered)

        private void Perceive()
        {
            var list = LivingNPC.All;
            if (list.Count == 0) return;
            var budget = Mathf.Min(4, list.Count);
            for (var k = 0; k < budget; k++)
            {
                perceptionCursor = (perceptionCursor + 1) % list.Count;
                var npc = list[perceptionCursor];
                if (npc == null || npc.Tier != 1) continue;
                Look(npc);
            }
        }

        private void Look(LivingNPC npc)
        {
            var now = Time.time;
            glances.TryGetValue(npc, out var g);
            if (now < g.until) return;
            var eyes = npc.transform.position + Vector3.up * 1.6f;
            var forward = npc.transform.forward;
            var shy = npc.Profile != null ? npc.Profile.Personality.Shyness : 0.3f;
            // 1. Events (a dropped bag, a noise): brief glance.
            foreach (var e in WorldEvents.Recent)
                if (now - e.Time < 1.5f && e.Source != npc && (e.Position - npc.transform.position).sqrMagnitude < e.Radius * e.Radius)
                {
                    npc.Animation.LookAt(e.Position + Vector3.up * 0.3f);
                    glances[npc] = (now + Random.Range(0.8f, 1.6f), g.next);
                    return;
                }
            // 2. The player approaching (cashier looks up, passers-by glance) — short, with a cooldown: no staring.
            if (player != null && !npc.InConversation)
            {
                var to = player.transform.position - npc.transform.position;
                var d = to.magnitude;
                var inView = Vector3.Angle(forward, to) < FieldOfView / 2;
                var service = npc.Place != null && npc.Place.Service != null && npc.Place.Service.Server == npc;
                var near = d < (service ? 4.5f : PerceptionRadius) && (inView || d < 2.2f);
                if (near && now > g.next)
                {
                    npc.Animation.LookAt(player.transform.position + Vector3.up * 1.6f);
                    var duration = service ? Random.Range(1.5f, 3f) : Random.Range(0.8f, 2.2f) * (1.2f - shy);
                    glances[npc] = (now + duration, now + duration + Random.Range(5f, 11f) * (0.7f + shy));
                    PlayerGlances++;
                    NPCRelationshipMemory.Encounter(npc.Id, LivingClock.Day);
                    return;
                }
            }
            // 3. Otherwise look where they are going / at whatever they are doing.
            npc.Animation.LookAt(null);
            glances[npc] = (now + Random.Range(0.6f, 1.5f), g.next);
        }

        // ------------------------------------------------------------------ NPC-to-NPC encounters

        private void Social()
        {
            var list = LivingNPC.All;
            for (var i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || a.Tier == 3 || a.InConversation || !a.Interruptible || a.Profile == null) continue;
                for (var j = i + 1; j < list.Count; j++)
                {
                    var b = list[j];
                    if (b == null || b.Tier == 3 || b.InConversation || !b.Interruptible || b.Profile == null) continue;
                    if (!a.Profile.IsFriend(b.Id) && !b.Profile.IsFriend(a.Id)) continue;
                    if ((a.transform.position - b.transform.position).sqrMagnitude > 5.5f * 5.5f) continue;
                    var key = string.CompareOrdinal(a.Id, b.Id) < 0 ? a.Id + "|" + b.Id : b.Id + "|" + a.Id;
                    if (pairCooldown.TryGetValue(key, out var until) && Time.time < until) continue;
                    pairCooldown[key] = Time.time + 150;
                    var chat = Random.value < (a.Profile.Personality.Sociability + b.Profile.Personality.Sociability) / 2;
                    var initiator = a.Profile.Personality.Shyness <= b.Profile.Personality.Shyness ? a : b;
                    var other = initiator == a ? b : a;
                    initiator.Interrupt(initiator.Greet(other, true, chat));
                    other.Interrupt(other.Greet(initiator, false, chat));
                    Greetings++;
                    return;
                }
            }
        }

        public void CountBump() => Bumps++;

        private void WeatherReaction()
        {
            var raining = SkyState.Raining;
            if (raining && !wasRaining)
            {
                WorldEvents.Raise("rain", player != null ? player.transform.position : Vector3.zero, 30);
                foreach (var npc in LivingNPC.All)
                    if (npc != null && npc.Tier == 1 && (npc.Place == null || !npc.Place.Indoor)) { npc.Say(Random.value < 0.5f ? "Oh no, it's starting to rain!" : "Looks like rain…"); break; }
            }
            wasRaining = raining;
        }
    }
}
