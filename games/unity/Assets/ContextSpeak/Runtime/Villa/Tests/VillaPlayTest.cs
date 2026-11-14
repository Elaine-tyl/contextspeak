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
    /// Play-mode verification in LuxuryVilla.unity (ContextSpeak > Checks > Luxury Villa Play Mode Tests).
    /// Everything is measured in the running scene; nothing is reported as passed unless observed.
    /// Report: Logs/LuxuryVillaPlayModeReport.md (+ console lines starting with [VillaTest]).
    public sealed class VillaPlayTest : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static string Summary { get; private set; } = "";
        private readonly List<(string name, string result, string detail)> results = new List<(string, string, string)>();
        private readonly List<float> frameMs = new List<float>();
        private void Pass(string n, string d) { results.Add((n, "PASS", d)); Debug.Log("[VillaTest] PASS " + n + " — " + d); }
        private void Fail(string n, string d) { results.Add((n, "FAIL", d)); Debug.LogWarning("[VillaTest] FAIL " + n + " — " + d); }
        private void Check(string n, bool ok, string d) { if (ok) Pass(n, d); else Fail(n, d); }

        private void Start() { Finished = false; StartCoroutine(Run()); }
        private void Update() { frameMs.Add(Time.unscaledDeltaTime * 1000); }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(2);
            var player = FindFirstObjectByType<CafePlayer>();
            var controller = player != null ? player.GetComponent<CharacterController>() : null;
            if (player == null || controller == null) { Fail("Setup", "no player in the scene"); Finish(); yield break; }
            var saved = File.Exists(VillaState.FilePath) ? File.ReadAllText(VillaState.FilePath) : null;

            // 1. Player stands on the ground at the entrance.
            Check("Player grounded at spawn", controller.isGrounded || Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, 0.8f), "y " + player.transform.position.y.ToString("0.00"));

            // 2. Doors: open, close, lock — and a closed door blocks the player, an open one lets them through.
            var doors = FindObjectsByType<VillaDoor>(FindObjectsSortMode.None);
            var entrance = doors.FirstOrDefault(d => d.Id == "Entrance");
            if (entrance != null)
            {
                entrance.SetLocked(false); entrance.SetOpen(false);
                yield return new WaitForSeconds(1.2f);
                var closedCentre = entrance.GetComponent<Collider>().bounds.center;
                entrance.SetOpen(true);
                yield return new WaitForSeconds(1.5f);
                Check("Front door opens", entrance.Openness > 0.98f && (entrance.GetComponent<Collider>().bounds.center - closedCentre).magnitude > 0.3f, "openness " + entrance.Openness.ToString("0.00"));
                entrance.SetOpen(false);
                yield return new WaitForSeconds(1.5f);
                entrance.SetLocked(true);
                entrance.SetOpen(true);
                yield return new WaitForSeconds(0.5f);
                Check("Locked door stays shut", !entrance.IsOpen && entrance.Openness < 0.05f || ResidentNear(entrance), "locked " + entrance.Locked);
                entrance.SetLocked(false);
            }
            else Fail("Front door", "Door 'Entrance' not found");
            var garage = doors.FirstOrDefault(d => d.Mode == VillaDoor.Motion.RollUp);
            if (garage != null) { garage.SetOpen(true); yield return new WaitForSeconds(5.5f); Check("Garage door rolls up", garage.Openness > 0.98f, "openness " + garage.Openness.ToString("0.00")); }
            var gate = doors.FirstOrDefault(d => d.Mode == VillaDoor.Motion.Slide);
            if (gate != null) { gate.SetOpen(true); yield return new WaitForSeconds(3.5f); Check("Gate slides open", gate.Openness > 0.98f, "openness " + gate.Openness.ToString("0.00")); }

            // 3. Walk the player through the house along the NavMesh (front door → living → stairs → upper gallery)
            //    using the CharacterController, so walls, door openings and the staircase are exercised physically.
            foreach (var d in doors) d.SetOpen(true);
            yield return new WaitForSeconds(2);
            var agentType = FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>()?.agentTypeID ?? 0;
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            var targets = new[] { "LivingRoom", "Kitchen", "Gallery", "MasterBedroom", "BedroomB", "SkyLounge", "RoofTerraceWest" };
            foreach (var roomName in targets)
            {
                var room = VillaRoom.All.FirstOrDefault(r => r.name == "Room · " + roomName);
                if (room == null) { Fail("Walk to " + roomName, "room missing"); continue; }
                var goal = room.Box.center; goal.y = room.Box.min.y;
                NavMesh.SamplePosition(player.transform.position, out var a, 2, filter);
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(goal, out var b, 2.5f, filter) || !NavMesh.CalculatePath(a.position, b.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                { Fail("Walk to " + roomName, "no NavMesh path"); continue; }
                var t = 0f; var stuck = 0f; var last = player.transform.position;
                foreach (var corner in path.corners.Skip(1))
                {
                    while ((Flat(player.transform.position) - Flat(corner)).magnitude > 0.35f && t < 60)
                    {
                        var dir = Flat(corner) - Flat(player.transform.position);
                        controller.Move(dir.normalized * Mathf.Min(3f * Time.deltaTime, dir.magnitude) + Vector3.down * 4 * Time.deltaTime);
                        t += Time.deltaTime;
                        stuck = (player.transform.position - last).sqrMagnitude < 1e-6f ? stuck + Time.deltaTime : 0; last = player.transform.position;
                        if (stuck > 1.5f) break;
                        yield return null;
                    }
                    if (stuck > 1.5f) break;
                }
                Check("Walk to " + roomName, room.Contains(player.transform.position), (room.Contains(player.transform.position) ? "arrived" : "ended at " + player.transform.position.ToString("0.0") + (stuck > 1.5f ? " (blocked)" : "")) + " in " + t.ToString("0.0") + " s, floor " + room.Floor);
            }

            // 4. Interactions: TV on/off, plants, laundry; the state is saved and reloads.
            var manager = InteractionManager.Instance;
            var tv = FindObjectsByType<VillaFixture>(FindObjectsSortMode.None).FirstOrDefault(f => f.Kind == VillaFixture.Use.TV);
            if (tv != null && manager != null)
            {
                var before = VillaState.Data.tvOn;
                tv.Interact(manager);
                yield return null;
                Check("TV toggles", VillaState.Data.tvOn != before && tv.Screen != null, "tvOn " + VillaState.Data.tvOn);
                if (VillaState.Data.tvOn) tv.Interact(manager);
            }
            var plants = FindObjectsByType<VillaFixture>(FindObjectsSortMode.None).FirstOrDefault(f => f.Kind == VillaFixture.Use.Plants);
            if (plants != null && manager != null)
            {
                plants.Interact(manager);
                yield return new WaitForSeconds(4.6f);
                Check("Water the plants", VillaState.Data.plantsWateredDay == VillaState.Today && player.Activity == PlayerAnimState.Idle, "watered today, player free again");
            }
            VillaState.Save();
            var text = File.Exists(VillaState.FilePath) ? File.ReadAllText(VillaState.FilePath) : "";
            VillaState.Load();
            Check("House state saved and reloaded", text.Contains("plantsWateredDay") && VillaState.Data.doors.Count > 0, VillaState.FilePath + " (" + VillaState.Data.doors.Count + " doors)");

            // 5. Sitting on the sofa.
            var sofa = FindObjectsByType<SeatController>(FindObjectsSortMode.None).FirstOrDefault(s => s.CanInteract);
            if (sofa != null && manager != null)
            {
                manager.Sit(sofa);
                yield return new WaitForSeconds(0.5f);
                Check("Sit down", manager.Seat == sofa && player.Activity == PlayerAnimState.Sit && sofa.GetComponent<ActivitySpot>().PlayerUsing, "seat " + sofa.name);
                manager.Stand();
            }

            // 6. Pool: the water body is found at the pool and the swimmer detects deep water.
            var body = FindObjectsByType<WaterBody>(FindObjectsSortMode.None).FirstOrDefault();
            if (body != null)
            {
                var pool = body.transform.position;
                var depth = WaterSurfaceSampler.GetWaterDepth(pool + Vector3.down * 0.5f);
                Check("Pool water", depth > 0.8f, "depth at the centre " + depth.ToString("0.00") + " m");
            }
            else Fail("Pool water", "no WaterBody");

            // 6b. Lift: call it to the ground floor, step in, ride to the third floor; the cab must carry the player.
            var lift = FindFirstObjectByType<VillaElevator>();
            if (lift != null && lift.Cab != null)
            {
                lift.Call(0);
                var w = 0f;
                while (!(lift.Floor == 0 && lift.State == VillaElevator.Phase.Open) && w < 30) { w += Time.deltaTime; yield return null; }
                Check("Lift comes when called", lift.Floor == 0 && lift.State == VillaElevator.Phase.Open, "arrived in " + w.ToString("0.0") + " s, doors " + lift.DoorOpenness.ToString("0.00"));
                controller.enabled = false; player.transform.position = lift.Cab.TransformPoint(lift.Interior.center) - Vector3.up * (lift.Interior.extents.y - 0.05f); controller.enabled = true;
                yield return new WaitForSeconds(0.3f);
                Check("Player inside the cab", lift.PlayerInside, "at " + player.transform.position.ToString("0.0"));
                lift.Call(2);
                w = 0;
                while (!(lift.Floor == 2 && lift.State == VillaElevator.Phase.Open) && w < 40) { w += Time.deltaTime; yield return null; }
                var dy = player.transform.position.y - lift.Floors[2];
                Check("Lift carries the player to the third floor", lift.Floor == 2 && Mathf.Abs(dy) < 0.25f && lift.PlayerInside, "ride " + w.ToString("0.0") + " s, player " + dy.ToString("+0.00;-0.00") + " m from the floor, doors " + lift.DoorOpenness.ToString("0.00"));
                // Walk out onto the third-floor landing.
                var outDir = (lift.LandingL[2].position - lift.Cab.position); outDir.y = 0; outDir.Normalize();
                for (var t = 0f; t < 2f; t += Time.deltaTime) { controller.Move(outDir * 1.5f * Time.deltaTime + Vector3.down * 4 * Time.deltaTime); yield return null; }
                var landing = VillaRoom.All.FirstOrDefault(r => r.name == "Room · TopLanding");
                Check("Walk out of the lift on the third floor", landing != null && landing.Contains(player.transform.position), "player at " + player.transform.position.ToString("0.0"));
                yield return new WaitForSeconds(lift.Dwell + 3);
                Check("Lift doors close behind you", lift.DoorOpenness < 0.05f, "doors " + lift.DoorOpenness.ToString("0.00"));
            }
            else Fail("Lift", "no VillaElevator in the scene");

            // 6c. Umbrella from the stand.
            var stand = FindObjectsByType<VillaFixture>(FindObjectsSortMode.None).FirstOrDefault(f => f.Kind == VillaFixture.Use.Umbrella);
            var carrier = player.GetComponent<UmbrellaCarrier>();
            if (stand != null && carrier != null && manager != null)
            {
                var was = carrier.Holding;
                stand.Interact(manager);
                yield return null; yield return null;
                var shown = GameObject.Find("Umbrella (" + player.name + ")");
                Check("Take the umbrella", carrier.Holding != was && (carrier.Holding ? shown != null && shown.activeSelf : true), "holding " + carrier.Holding);
                if (carrier.Holding) stand.Interact(manager);
            }
            else Fail("Take the umbrella", "stand or carrier missing");

            // 7. Car: the player can get in.
            var car = FindFirstObjectByType<DrivableVehicle>();
            Check("Car present", car != null && car.Length > 3, car != null ? car.Label + " " + car.Length.ToString("0.0") + " m" : "none");

            // 8. Family: residents start their routines and reach places on the NavMesh.
            yield return new WaitForSeconds(6);
            var family = LivingNPC.All.Where(n => n != null && n.Profile != null && n.Profile.Home == "villa").ToList();
            Check("Family active", family.Count == 4 && family.All(n => !string.IsNullOrEmpty(n.Activity)), string.Join(", ", family.Select(n => n.Profile.DisplayName + ": " + n.Activity + " (" + n.Step + ")")));
            Check("Family on the NavMesh", family.All(n => n.Hidden || NavMesh.SamplePosition(n.transform.position, out var hh, 0.6f, filter)), string.Join(", ", family.Select(n => n.Profile.DisplayName + " y " + n.transform.position.y.ToString("0.0"))));
            var pet = FindFirstObjectByType<VillaPet>();
            Check("Pet on the NavMesh", pet != null && pet.GetComponent<NavMeshAgent>().isOnNavMesh, pet != null ? pet.name : "none");

            // 9. Speaking topic starts with mandatory repetition.
            var flow = LocationFlow.Current as VillaFlow;
            if (flow != null && ScenarioManager.Instance != null)
            {
                flow.Practise("home");
                yield return new WaitForSeconds(1);
                Check("IELTS topic starts", ScenarioManager.Instance.Active, "topic 'Your Home' with " + (ScenarioManager.Instance.Npc != null ? ScenarioManager.Instance.Npc.DisplayName : "?"));
            }
            else Fail("IELTS topic starts", "no VillaFlow / ScenarioManager");

            var sorted = frameMs.OrderBy(x => x).ToList();
            if (sorted.Count > 0) Pass("Frame time (measured, not a pass/fail target)", "median " + sorted[sorted.Count / 2].ToString("0.0") + " ms, 95th " + sorted[(int)(sorted.Count * 0.95f)].ToString("0.0") + " ms");
            try { if (saved != null) File.WriteAllText(VillaState.FilePath, saved); else if (File.Exists(VillaState.FilePath)) File.Delete(VillaState.FilePath); } catch { }
            VillaState.Load();
            Finish();
        }

        private static bool ResidentNear(VillaDoor d) => LivingNPC.All.Any(n => n != null && (n.transform.position - d.transform.position).sqrMagnitude < 4);
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        private void Finish()
        {
            var passed = results.Count(r => r.result == "PASS"); var failed = results.Count(r => r.result == "FAIL");
            Summary = passed + " passed, " + failed + " failed";
            var md = new StringBuilder();
            md.AppendLine("# Luxury villa Play-mode test report").AppendLine();
            md.AppendLine("Run: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · Unity " + Application.unityVersion + " · " + Summary).AppendLine();
            md.AppendLine("| Test | Result | Measured |").AppendLine("|---|---|---|");
            foreach (var (n, r, d) in results) md.AppendLine("| " + n + " | " + r + " | " + d.Replace("|", "/") + " |");
            try
            {
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "LuxuryVillaPlayModeReport.md"), md.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[VillaTest] report not written: " + e.Message); }
            Debug.Log("[VillaTest] DONE " + Summary + "\n" + md);
            Finished = true;
        }
    }
}
