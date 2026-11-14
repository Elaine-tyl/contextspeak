using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Life
{
    /// Automated Play-mode verification in the Living Neighbourhood (started from
    /// ContextSpeak > Checks > Living NPC Play Mode Tests). Runs the real simulation at 3× speed and
    /// records measured results; nothing is reported as passed unless it was observed.
    /// Report: Logs/LivingNPCPlayModeReport.md (+ console lines starting with [NPCTest]).
    public sealed class LivingNPCPlayTest : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static string Summary { get; private set; } = "";
        private readonly List<(string name, string result, string detail)> results = new List<(string, string, string)>();
        private int wallHits, groundSamples, groundBad, seatedSeen, indoorSeen, crossedSeen;
        private readonly List<float> frameMs = new List<float>(), simMs = new List<float>();
        private readonly HashSet<string> activities = new HashSet<string>();
        private CafePlayer player;
        private readonly Collider[] overlap = new Collider[16];

        private void Pass(string n, string d) { results.Add((n, "PASS", d)); Debug.Log("[NPCTest] PASS " + n + " — " + d); }
        private void Fail(string n, string d) { results.Add((n, "FAIL", d)); Debug.LogWarning("[NPCTest] FAIL " + n + " — " + d); }
        private void Check(string n, bool ok, string d) { if (ok) Pass(n, d); else Fail(n, d); }

        private void Start() { Finished = false; StartCoroutine(Run()); }

        private void Update()
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000);
            if (NPCLifeSimulationManager.Instance != null) simMs.Add(NPCLifeSimulationManager.Instance.MeasuredMilliseconds);
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null) continue;
                activities.Add(npc.Activity);
                if (npc.Place != null && npc.Place.Indoor) indoorSeen++;
                if (LivingCrossing.All.Any(c => c != null && c.OnCrossing(npc.transform.position))) crossedSeen++;
                var action = npc.Animation.Action;
                if (action == NPCAction.Sit || action == NPCAction.Drink || action == NPCAction.Type || action == NPCAction.Eat) seatedSeen++;
                if (npc.Tier != 1 || npc.Riding || npc.Hidden || action == NPCAction.Lie) continue;
                // Walls: the body must not be inside a wall / counter / shelf.
                var p = npc.transform.position;
                var n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.5f, p + Vector3.up * 1.4f, 0.12f, overlap, ~0, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < n; i++)
                {
                    var c = overlap[i];
                    if (c.transform.IsChildOf(npc.transform) || c is CharacterController || c.GetComponentInParent<LivingNPC>() != null || c.GetComponentInParent<CafePlayer>() != null) continue;
                    if (c.bounds.max.y < p.y + 0.6f) continue;                 // floors, kerbs
                    if (c.GetComponentInParent<LivingCar>() != null || c.GetComponentInParent<LivingTram>() != null) continue;
                    wallHits++;
                    break;
                }
                // Ground contact.
                if (npc.Navigation.OnMesh && Time.frameCount % 10 == 0)
                {
                    groundSamples++;
                    if (NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) > 0.15f) groundBad++;
                }
            }
        }

        private LivingNPC Npc(string id) => LivingNPC.All.FirstOrDefault(n => n != null && n.Id == id);

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(2);
            player = FindFirstObjectByType<CafePlayer>();
            var manager = NPCLifeSimulationManager.Instance;
            var director = FindFirstObjectByType<NPCEventDirector>();
            if (manager == null || player == null || LivingNPC.All.Count == 0) { Fail("Setup", "Open the Living Neighbourhood scene (no manager / player / NPCs)."); Finish(); yield break; }
            Time.timeScale = 3;
            var tram = LivingTram.All.FirstOrDefault();
            var cafe = LivingPlace.ById("forest_cafe");

            // Schedule changes with world time.
            var emma = Npc("emma");
            LivingClock.Set(8.5f);
            yield return new WaitForSeconds(3);
            var before = emma != null ? emma.Activity : "";
            LivingClock.Set(9.2f);
            yield return new WaitForSeconds(4);
            Check("NPC schedule changes correctly", emma != null && before == "Commute" && emma.Activity == "ServeCustomers", "Emma at 08:30 → " + before + ", at 09:12 → " + (emma != null ? emma.Activity : "missing"));
            LivingClock.Set(8.1f);

            // Let the morning run.
            player.Place(new Vector3(8, 0.05f, 12));
            var t = 0f; var served = cafe != null && cafe.Service != null ? cafe.Service.Completed : 0;
            while (t < 150) { t += Time.deltaTime; yield return null; }
            Check("Barista performs a complete work sequence", cafe != null && cafe.Service.Completed > served, (cafe != null ? cafe.Service.Completed - served : 0) + " orders taken → made → handed over → collected");
            Check("NPCs use doors and seats", indoorSeen > 0 && seatedSeen > 0, "frames with NPCs inside buildings: " + indoorSeen + ", seated frames: " + seatedSeen);
            Check("Transport boarding works", tram != null && tram.Boarded > 0, tram != null ? tram.Boarded + " boarded, " + tram.Alighted + " got off" : "no tram");
            var violations = LivingCrossing.All.Sum(c => c != null ? c.Violations : 0);
            Check("Pedestrians cross roads safely", violations == 0 && crossedSeen > 0, crossedSeen + " frames of NPCs on zebras, " + violations + " entered on red");
            Check("NPCs interact with other NPCs", manager.Greetings > 0 || LivingNPC.All.Any(n => n != null && n.Step.StartsWith("walking to school with")), manager.Greetings + " greetings/chats" + (LivingNPC.All.Any(n => n != null && n.Step.StartsWith("walking to school with")) ? ", students walking together" : ""));
            Check("NPCs navigate without walking through walls", wallHits <= 3, wallHits + " frames with a body inside a wall/counter (tolerance 3)");
            Check("Characters keep ground contact", groundSamples > 0 && groundBad <= groundSamples * 0.03f, groundBad + "/" + groundSamples + " samples more than 15 cm from the NavMesh");

            // Player proximity.
            var glances = manager.PlayerGlances;
            var near = LivingNPC.All.Where(n => n != null && n.Tier == 1 && !n.Hidden && !n.Riding).OrderBy(n => (n.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (near != null) player.Place(near.transform.position + near.transform.forward * 2f);
            yield return new WaitForSeconds(4);
            Check("NPCs react to player proximity", manager.PlayerGlances > glances, (manager.PlayerGlances - glances) + " glances (short, with cooldowns)");

            // Bump events must not repeat.
            var bumpTarget = LivingNPC.All.FirstOrDefault(n => n != null && n.Tier == 1 && n.Interruptible && n.Profile != null && !n.Profile.Occupation.ToLowerInvariant().Contains("barista"));
            if (director != null && bumpTarget != null)
            {
                var first = director.TryBump(bumpTarget);
                var second = director.TryBump(bumpTarget);
                Check("Accidental collision events do not repeat excessively", !second, "first attempt " + (first ? "started" : "not started") + ", immediate second attempt " + (second ? "STARTED (bad)" : "refused by cooldown"));
            }
            else Fail("Accidental collision events do not repeat excessively", "no director or walking NPC");

            // Dialogue (IELTS guided pipeline) — started through the flow with a real NPC.
            var flow = LocationFlow.Current as NeighborhoodFlow;
            var barista = LivingNPC.All.FirstOrDefault(n => n != null && n.Profile != null && n.Profile.Occupation.ToLowerInvariant().Contains("barista") && n.Place != null && n.Place.Service != null);
            if (flow != null && barista != null && ScenarioManager.Instance != null)
            {
                player.Place(barista.transform.position + barista.transform.forward * 1.6f);
                yield return null;
                flow.Talk(barista.Legacy);
                yield return new WaitForSeconds(1);
                var active = ScenarioManager.Instance.Active && ScenarioManager.Instance.Machine.Scenario != null && ScenarioManager.Instance.Machine.Scenario.Title == "Neighbourhood Café Order";
                var held = barista.InConversation;
                ScenarioManager.Instance.End(false);
                Check("AI English quest starts with the right NPC", active && held, "café order scenario active: " + active + ", barista stays in the conversation: " + held);
            }
            else Fail("AI English quest starts with the right NPC", "no flow / barista on shift / ScenarioManager");
            var machine = new DialogueStateMachine();
            machine.Start(NeighborhoodScenarios.Create("Neighbourhood Café Order"), ContextSpeak.Slice.IeltsBand.Band6, true);
            for (var i = 0; i < 3; i++) machine.Answer("hello there", true);
            var gated = machine.Phase == DialoguePhase.AwaitingRepeat && !machine.Skip();
            var advanced = machine.Repeat(machine.Target) && machine.Index == 1;
            Check("IELTS learning mode enforces correction and repetition", gated && advanced, "repeat required: " + gated + ", advances only after repeating: " + advanced);

            // Rain.
            if (SkySystem.Active != null)
            {
                SkySystem.Active.SetWeather(SkyWeather.Rain, 1);
                yield return new WaitForSeconds(8);
                var sheltering = LivingNPC.All.Count(n => n != null && n.Activity == "Shelter");
                SkySystem.Active.SetWeather(SkyWeather.Clear, 1);
                Check("Rain changes behaviour", sheltering > 0, sheltering + " NPCs went for shelter");
            }

            // Crowd LOD.
            var full = manager.TierCounts[1];
            player.Place(new Vector3(-85, 0.05f, -6));
            yield return new WaitForSeconds(2);
            var farFull = manager.TierCounts[1]; var farAbstract = manager.TierCounts[3];
            player.Place(new Vector3(8, 0.05f, -6));
            yield return new WaitForSeconds(2);
            Check("Crowd LOD transitions", farFull < full && farAbstract > 0 && manager.TierCounts[1] > farFull, "near: " + full + " full; far away: " + farFull + " full / " + farAbstract + " abstract; back: " + manager.TierCounts[1] + " full");

            // Save / load.
            manager.SaveAll();
            NPCRelationshipMemory.Load();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var stored = LivingNPC.All.Count(n => n != null && NPCRelationshipMemory.State(n.Id, scene) != null);
            var sample = LivingNPC.All.FirstOrDefault(n => n != null);
            var state = sample != null ? NPCRelationshipMemory.State(sample.Id, scene) : null;
            Check("NPC state persists through saving and loading", stored == LivingNPC.All.Count && state != null && Vector3.Distance(new Vector3(state.X, state.Y, state.Z), sample.transform.position) < 0.5f, stored + "/" + LivingNPC.All.Count + " NPC states in " + NPCRelationshipMemory.FilePath);

            // Performance (Stopwatch around the whole simulation; use the Unity Profiler for a full breakdown).
            Time.timeScale = 1;
            Check("Performance measured", simMs.Count > 0, "simulation " + (simMs.Count > 0 ? simMs.Average() : 0).ToString("0.00") + " ms/frame avg, frame " + (frameMs.Count > 0 ? frameMs.Average() : 0).ToString("0.0") + " ms avg (editor, " + LivingNPC.All.Count + " NPCs). Visual quality is not validated by this test.");
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1;
            var passed = results.Count(r => r.result == "PASS"); var failed = results.Count(r => r.result == "FAIL");
            Summary = passed + " passed, " + failed + " failed";
            var md = new StringBuilder();
            md.AppendLine("# Living NPC Play-mode test report").AppendLine();
            md.AppendLine("Run: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · Unity " + Application.unityVersion + " · " + Summary).AppendLine();
            md.AppendLine("| Test | Result | Measured |").AppendLine("|---|---|---|");
            foreach (var (n, r, d) in results) md.AppendLine("| " + n + " | " + r + " | " + d.Replace("|", "/") + " |");
            md.AppendLine().AppendLine("Activities observed: " + string.Join(", ", activities.Where(a => !string.IsNullOrEmpty(a))));
            try
            {
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "LivingNPCPlayModeReport.md"), md.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[NPCTest] report not written: " + e.Message); }
            Debug.Log("[NPCTest] DONE " + Summary + "\n" + md);
            Finished = true;
        }
    }
}
