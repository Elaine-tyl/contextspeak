using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Automated Play-mode test of the swimming system in SwimmingTestScene. Started by
    /// ContextSpeak > Checks > Swimming Play Mode Tests; drives the real player (CafePlayer routes on
    /// land, SwimmingController.ScriptedInput in the water) through every scenario and records
    /// measured results. Nothing is marked passed unless it was executed and measured.
    /// Report: Logs/SwimmingPlayModeReport.md (+ console lines starting with [SwimTest]).
    public sealed class SwimmingPlayModeTest : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static string Summary { get; private set; } = "";

        private readonly List<(string name, string result, string detail)> results = new List<(string, string, string)>();
        private readonly HashSet<string> animations = new HashSet<string>();
        private CafePlayer player;
        private SwimmingController swim;
        private SwimmingAnimationController anim;
        private SwimmingWaterInteraction fx;
        private readonly SwimScriptedInput input = new SwimScriptedInput();
        private float maxCameraStep, settleUntil;
        private Vector3 lastCamera;
        private bool nan;
        private readonly List<float> frameMs = new List<float>();
        private readonly List<float> swimCpu = new List<float>();

        private void Start()
        {
            Finished = false;
            StartCoroutine(Run());
        }

        private void Pass(string name, string detail) { results.Add((name, "PASS", detail)); Debug.Log("[SwimTest] PASS " + name + " — " + detail); }
        private void Fail(string name, string detail) { results.Add((name, "FAIL", detail)); Debug.LogWarning("[SwimTest] FAIL " + name + " — " + detail); }
        private void Skip(string name, string detail) { results.Add((name, "NOT RUN", detail)); Debug.LogWarning("[SwimTest] NOT RUN " + name + " — " + detail); }
        private void Check(string name, bool ok, string detail) { if (ok) Pass(name, detail); else Fail(name, detail); }

        private void Update()
        {
            if (swim == null) return;
            frameMs.Add(Time.unscaledDeltaTime * 1000);
            swimCpu.Add(swim.CpuMilliseconds);
            if (anim != null && anim.CurrentAnimation != "—") animations.Add(anim.CurrentAnimation.Split(' ')[0]);
            var p = player.transform.position;
            if (float.IsNaN(p.x + p.y + p.z) || float.IsNaN(swim.VerticalVelocity)) nan = true;
            var cam = player.CameraPosition != null ? player.CameraPosition.position : Camera.main != null ? Camera.main.transform.position : p;
            if (Time.time > settleUntil) maxCameraStep = Mathf.Max(maxCameraStep, (cam - lastCamera).magnitude);
            lastCamera = cam;
        }

        private static WaterBody Water(string name) => WaterBody.All.FirstOrDefault(b => b != null && b.name.StartsWith(name));

        private void Teleport(Vector3 feet, Vector3 face)
        {
            settleUntil = Time.time + 0.6f;
            swim.ScriptedInput = input;
            input.Move = Vector2.zero; input.Up = input.Down = input.Fast = false;
            player.Face(face);
        }

        private IEnumerator PlaceSwimming(WaterBody body, Vector3 xz, Vector3 face)
        {
            Teleport(xz, face);
            var surface = body.SurfaceHeight(xz);
            swim.PlaceInWater(new Vector3(xz.x, surface - swim.FloatLine, xz.z));
            player.Face(face);
            yield return new WaitForSeconds(1.5f);
        }

        private IEnumerator PlaceOnLand(Vector3 feet, Vector3 face)
        {
            Teleport(feet, face);
            player.Place(feet);
            yield return new WaitForSeconds(0.6f);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.5f);
            player = FindFirstObjectByType<CafePlayer>();
            swim = player != null ? player.GetComponent<SwimmingController>() : null;
            if (swim == null) { Fail("Setup", "No CafePlayer with SwimmingController in the scene."); Finish(); yield break; }
            anim = swim.GetComponent<SwimmingAnimationController>();
            fx = swim.GetComponent<SwimmingWaterInteraction>();
            var pool = Water("Pool water"); var shallow = Water("Shallow water"); var lake = Water("Lake water"); var ocean = WaterBody.All.FirstOrDefault(b => b != null && b.Kind == WaterKind.Ocean);
            if (pool == null || shallow == null || lake == null) { Fail("Setup", "SwimmingTestScene water bodies not found (rebuild the scene)."); Finish(); yield break; }
            swim.Tuning = new SwimTuning();

            // 1. Shallow water: wade, never swim.
            {
                var c = shallow.Volume.center;
                yield return PlaceOnLand(new Vector3(c.x - 6, 0.05f, c.z), Vector3.right);
                swim.ScriptedInput = null;
                player.Route.Enqueue(new Vector3(c.x, c.y, c.z));
                var waded = false; var swam = false; var maxSpeed = 0f;
                for (var t = 0f; t < 7; t += Time.deltaTime)
                {
                    if (swim.State == SwimState.Wading) waded = true;
                    if (swim.IsSwimming) swam = true;
                    if (swim.State == SwimState.Wading) maxSpeed = Mathf.Max(maxSpeed, player.Speed);
                    yield return null;
                }
                player.Route.Clear();
                Check("1 Walk through shallow water", waded && !swam, "wading reached: " + waded + ", swimming triggered: " + swam + ", depth " + swim.Detector.Depth.ToString("0.00") + " m");
            }

            // 2. Walk from the deck into the deep end → swimming.
            {
                var c = pool.Volume.center;
                yield return PlaceOnLand(new Vector3(c.x + 3.4f, 0.05f, pool.Volume.min.z - 1.2f), Vector3.forward);
                swim.ScriptedInput = null;
                player.Route.Enqueue(new Vector3(c.x + 3.4f, 0, c.z));
                var t = 0f;
                while (!swim.IsSwimming && t < 8) { t += Time.deltaTime; yield return null; }
                player.Route.Clear();
                swim.ScriptedInput = input;
                Check("2 Transition to swimming in deep water", swim.IsSwimming, "state " + swim.State + " after " + t.ToString("0.0") + " s, depth " + swim.Detector.Depth.ToString("0.00") + " m");
                yield return new WaitForSeconds(3f);
            }

            // 3–4. Float naturally, never sink while idle, never hover.
            yield return PlaceSwimming(pool, pool.Volume.center + new Vector3(2.8f, 0, 0), Vector3.left);
            yield return Float("3 Floats near the waterline / 4 does not sink / 5 does not hover", pool, 8);

            // 6. Swim forward.
            {
                var start = player.transform.position; var strokes = fx != null ? fx.Strokes : 0; var dist = fx != null ? fx.Disturbances : 0;
                input.Move = new Vector2(0, 1);
                var states = new HashSet<SwimState>();
                for (var t = 0f; t < 3f; t += Time.deltaTime) { states.Add(swim.State); yield return null; }
                input.Move = Vector2.zero;
                var moved = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
                Check("6 Swim forward", moved > 2.5f && states.Contains(SwimState.SurfaceSwimming), "moved " + moved.ToString("0.00") + " m in 3 s, states: " + string.Join(",", states));
                var newStrokes = fx != null ? fx.Strokes - strokes : 0; var newDisturb = fx != null ? fx.Disturbances - dist : 0;
                Check("14 Water responds to swimming strokes", newStrokes > 0 && newDisturb > 5 && WaterRippleSimulation.Instance != null,
                    newStrokes + " limb-synchronised strokes, " + newDisturb + " height-field disturbances, ripple sim " + (WaterRippleSimulation.Instance != null ? "running" : "missing"));
            }

            // 7. Turn.
            {
                yield return PlaceSwimming(pool, pool.Volume.center + new Vector3(-1, 0, 0), Vector3.right);
                var before = swim.Heading;
                input.Move = new Vector2(-1, 0);
                yield return new WaitForSeconds(1.6f);
                input.Move = Vector2.zero;
                var turned = Vector3.Angle(before, swim.Heading);
                Check("7 Turn", turned > 45, "heading changed " + turned.ToString("0") + "°");
            }

            // 8. Tread water when input stops.
            {
                var t = 0f;
                while (swim.State != SwimState.TreadingWater && t < 2.5f) { t += Time.deltaTime; yield return null; }
                yield return new WaitForSeconds(2);
                Check("8 Tread water", swim.State == SwimState.TreadingWater && swim.Detector.HeadDepth < 0, "state " + swim.State + ", head " + (-swim.Detector.HeadDepth).ToString("0.00") + " m above the water");
            }

            // 9. Back float.
            {
                input.BackFloatPressed = true;
                yield return new WaitForSeconds(4);
                var hips = anim != null ? anim.Bone("Hips") : null;
                var head = anim != null ? anim.HeadBone : null;
                var surface = pool.SurfaceHeight(player.transform.position);
                var ok = swim.State == SwimState.BackFloating && (head == null || head.position.y > surface - 0.05f) && (hips == null || hips.position.y < surface + 0.05f);
                Check("9 Float on back", ok, "state " + swim.State + (head != null ? ", face " + (head.position.y - surface).ToString("0.00") + " m vs water" : "") + (hips != null ? ", hips " + (hips.position.y - surface).ToString("0.00") + " m vs water" : ""));
                input.BackFloatPressed = true;
                yield return new WaitForSeconds(1.5f);
            }

            // 10. Dive.
            {
                input.Down = true;
                var t = 0f; var deepest = 0f;
                while (t < 2.2f) { t += Time.deltaTime; deepest = Mathf.Max(deepest, swim.Detector.HeadDepth); yield return null; }
                input.Down = false;
                var under = swim.Machine.Underwater;
                yield return new WaitForSeconds(0.6f);
                Check("10 Dive", under && deepest > 0.3f, "underwater: " + under + ", head reached " + deepest.ToString("0.00") + " m below the surface, camera underwater: " + swim.CameraUnderwater);
            }

            // 11. Surface (released: rises naturally).
            {
                var t = 0f;
                while ((swim.Detector.HeadDepth > 0 || !swim.Machine.AtSurface) && t < 10) { t += Time.deltaTime; yield return null; }
                Check("11 Surface", swim.Machine.AtSurface && swim.Detector.HeadDepth < 0, "back at the surface after " + t.ToString("0.0") + " s (state " + swim.State + ")");
            }

            // 12. Exit through the lake shore.
            {
                var c = lake.Volume.center;
                yield return PlaceSwimming(lake, new Vector3(c.x, 0, c.z + 1), Vector3.forward);
                input.Move = new Vector2(0, 1);
                var t = 0f;
                while (swim.OwnsMovement && t < 25) { t += Time.deltaTime; yield return null; }
                input.Move = Vector2.zero;
                var released = !swim.OwnsMovement;
                swim.ScriptedInput = null;
                player.Route.Enqueue(new Vector3(c.x, 0, lake.Volume.max.z + 3));
                var u = 0f;
                while (u < 8 && Vector3.Distance(player.transform.position, new Vector3(c.x, player.transform.position.y, lake.Volume.max.z + 3)) > 0.5f) { u += Time.deltaTime; yield return null; }
                player.Route.Clear();
                swim.ScriptedInput = input;
                Check("12 Exit water through a shore", released && player.transform.position.y > -0.1f && !swim.Detector.InWater, "released to walking: " + released + " after " + t.ToString("0.0") + " s; on land at y " + player.transform.position.y.ToString("0.00"));
            }

            // 13. Pool ladder.
            {
                var ladder = WaterExitPoint.All.FirstOrDefault(p => p != null && p.Kind == WaterExitPoint.ExitKind.Ladder);
                if (ladder == null) Skip("13 Climb pool ladder", "no ladder in the scene");
                else
                {
                    var face = ladder.TopPoint - ladder.BottomPoint; face.y = 0;
                    yield return PlaceSwimming(pool, ladder.BottomPoint - face.normalized * 0.5f, face);
                    yield return new WaitForSeconds(0.4f);
                    input.InteractPressed = true;
                    var t = 0f;
                    yield return null;
                    var started = swim.Exit == SwimmingController.ExitMode.Ladder;
                    while (swim.OwnsMovement && t < 10) { t += Time.deltaTime; yield return null; }
                    var top = player.transform.position.y;
                    Check("13 Climb pool ladder", started && !swim.OwnsMovement && top > pool.SurfaceHeight(player.transform.position) + 0.05f, "ladder exit started: " + started + ", finished in " + t.ToString("0.0") + " s, standing at y " + top.ToString("0.00"));
                }
            }

            // Pool edge without a ladder (extra).
            {
                var c = pool.Volume.center;
                yield return PlaceSwimming(pool, new Vector3(c.x + 1.5f, 0, pool.Volume.max.z - 0.75f), Vector3.forward);
                input.Move = new Vector2(0, 1);
                yield return new WaitForSeconds(1.0f);
                input.Move = Vector2.zero;
                input.InteractPressed = true;
                yield return null;
                var started = swim.Exit == SwimmingController.ExitMode.Edge;
                var t = 0f;
                while (swim.OwnsMovement && t < 8) { t += Time.deltaTime; yield return null; }
                Check("13b Climb out at a pool edge", started && !swim.OwnsMovement && player.transform.position.y > pool.SurfaceHeight(player.transform.position), "edge found: " + started + ", out after " + t.ToString("0.0") + " s");
            }

            // 15. Waves move the floating body.
            if (ocean == null) Skip("15 Dynamic wave height affects buoyancy", "no ocean in the scene");
            else
            {
                var xz = new Vector3(0, 0, -112);
                yield return PlaceSwimming(ocean, xz, Vector3.forward);
                yield return new WaitForSeconds(2);
                var ys = new List<float>(); var ss = new List<float>();
                for (var t = 0f; t < 10; t += Time.deltaTime) { ys.Add(player.transform.position.y); ss.Add(ocean.SurfaceHeight(player.transform.position)); yield return null; }
                float Std(List<float> v) { var m = v.Average(); return Mathf.Sqrt(v.Sum(x => (x - m) * (x - m)) / v.Count); }
                var sy = Std(ys); var sw = Std(ss);
                var my = ys.Average(); var ms = ss.Average();
                var cov = ys.Zip(ss, (a, b) => (a - my) * (b - ms)).Sum() / ys.Count;
                var corr = sy > 1e-4f && sw > 1e-4f ? cov / (sy * sw) : 0;
                if (sw < 0.02f) Skip("15 Dynamic wave height affects buoyancy", "waves too small here to measure (surface std " + sw.ToString("0.000") + " m)");
                else Check("15 Dynamic wave height affects buoyancy", corr > 0.4f && sy > 0.3f * sw, "surface std " + sw.ToString("0.00") + " m, body std " + sy.ToString("0.00") + " m, correlation " + corr.ToString("0.00"));
            }

            // 17. Male and female.
            {
                var appearance = player.GetComponentInChildren<PlayerAppearance>();
                if (appearance == null) Skip("17 Male and female characters", "no PlayerAppearance on the player");
                else
                {
                    var report = new StringBuilder();
                    var ok = true;
                    foreach (var look in new[] { PlayerLook.Boy, PlayerLook.Girl })
                    {
                        var profile = CharacterProfiles.Draft();
                        profile.Look = look;
                        appearance.ApplyProfile(profile);
                        yield return PlaceSwimming(pool, pool.Volume.center + new Vector3(2.5f, 0, 0), Vector3.left);
                        yield return new WaitForSeconds(2);
                        var surface = pool.SurfaceHeight(player.transform.position);
                        var y = player.transform.position.y;
                        var floating = Mathf.Abs(y - (surface - swim.FloatLine)) < 0.25f;
                        input.Move = new Vector2(0, 1);
                        var start = player.transform.position;
                        yield return new WaitForSeconds(1.5f);
                        input.Move = Vector2.zero;
                        var moved = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
                        var authored = anim != null && anim.ActiveSet != null && anim.ActiveSet.Swim != null;
                        ok &= floating && moved > 1 && anim != null && anim.Motion != null;
                        report.Append(look + ": rig " + (anim != null && anim.Motion != null ? anim.Motion.ModelName : "none") + ", floats " + floating + ", swam " + moved.ToString("0.0") + " m, authored swim clip " + authored + ". ");
                    }
                    appearance.Restore();
                    Check("17 Male and female characters", ok, report.ToString());
                }
            }

            // 16. Animation transitions seen.
            {
                var wanted = new[] { "SwimForward", "TreadWater", "BackFloat", "Dive", "UnderwaterSwim", "Surface", "ExitWater", "ClimbPoolLadder" };
                var missing = wanted.Where(w => !animations.Contains(w)).ToArray();
                Check("16 Swimming animations transition", missing.Length == 0, "seen: " + string.Join(", ", animations) + (missing.Length > 0 ? " · missing: " + string.Join(", ", missing) : ""));
            }
            // 18. Stability.
            Check("18 Character and camera stable", !nan && maxCameraStep < 0.6f, "NaN: " + nan + ", largest camera move in one frame (outside placements) " + maxCameraStep.ToString("0.00") + " m");
            // 19. Performance.
            {
                var cpu = swimCpu.Count > 0 ? swimCpu.Average() : 0;
                var frame = frameMs.Count > 0 ? frameMs.Average() : 0;
                var sim = WaterRippleSimulation.Instance != null ? WaterRippleSimulation.Instance.LastStepMilliseconds : 0;
                Check("19 Performance budget (swimming CPU < 0.5 ms/frame)", cpu < 0.5f, "swimming scripts " + cpu.ToString("0.000") + " ms, ripple sim " + sim.ToString("0.00") + " ms, average frame " + frame.ToString("0.0") + " ms (editor)");
            }
            Finish();
        }

        private IEnumerator Float(string name, WaterBody body, float seconds)
        {
            var ys = new List<float>(); var hipsAbove = float.MinValue; var headAbove = float.MaxValue;
            var hips = anim != null ? anim.Bone("Hips") : null;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var surface = body.SurfaceHeight(player.transform.position);
                ys.Add(player.transform.position.y - (surface - swim.FloatLine));
                if (hips != null) hipsAbove = Mathf.Max(hipsAbove, hips.position.y - surface);
                headAbove = Mathf.Min(headAbove, -swim.Detector.HeadDepth);
                yield return null;
            }
            var last = ys.Skip(ys.Count / 2).ToList();
            var mean = last.Average();
            var sinkTrend = last.Last() - last.First();
            var ok = Mathf.Abs(mean) < 0.12f && ys.Min() > -0.35f && Mathf.Abs(sinkTrend) < 0.1f && (hips == null || hipsAbove < 0.05f) && headAbove > -0.02f && swim.IsSwimming;
            Check(name, ok, "mean offset from float line " + mean.ToString("0.000") + " m, lowest " + ys.Min().ToString("0.00") + " m, drift over last half " + sinkTrend.ToString("0.000") +
                            " m, highest hips vs water " + (hips != null ? hipsAbove.ToString("0.00") : "n/a") + " m, lowest head clearance " + headAbove.ToString("0.00") + " m");
        }

        private void Finish()
        {
            if (swim != null) swim.ScriptedInput = null;
            var passed = results.Count(r => r.result == "PASS");
            var failed = results.Count(r => r.result == "FAIL");
            var notRun = results.Count(r => r.result == "NOT RUN");
            Summary = passed + " passed, " + failed + " failed, " + notRun + " not run";
            var md = new StringBuilder();
            md.AppendLine("# Swimming Play-mode test report");
            md.AppendLine();
            md.AppendLine("Run: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · Unity " + Application.unityVersion + " · " + Summary);
            md.AppendLine();
            md.AppendLine("| Test | Result | Measured |");
            md.AppendLine("|---|---|---|");
            foreach (var (n, r, d) in results) md.AppendLine("| " + n + " | " + r + " | " + d.Replace("|", "/") + " |");
            md.AppendLine();
            md.AppendLine("Animations used: " + string.Join(", ", animations));
            if (anim != null && anim.ActiveSet != null && !string.IsNullOrEmpty(anim.ActiveSet.Notes)) md.AppendLine().AppendLine(anim.ActiveSet.Notes);
            try
            {
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "SwimmingPlayModeReport.md"), md.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[SwimTest] could not write report: " + e.Message); }
            Debug.Log("[SwimTest] DONE " + Summary + "\n" + md);
            Finished = true;
        }
    }
}
