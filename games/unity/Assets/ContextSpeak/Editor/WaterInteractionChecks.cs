using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Water Interaction — automated EditMode checks of everything that can be
    /// verified without rendering: immersion states and hysteresis, footstep events (none while still,
    /// larger when running), jump impact scaling, profile differences, damping conversion, simulation
    /// stability, and a CPU reference run of the same wave equation the GPU shader uses (a ripple
    /// expands and dissipates; two sources superpose). Visual checks (reflections, shoreline, wetness
    /// look, frame time) need the Play-mode run in WaterInteractionTestScene — see the README.
    public static class WaterInteractionChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Water Interaction")]
        public static void Menu()
        {
            var results = Run();
            var failed = results.Where(r => !r.ok).ToArray();
            var text = string.Join("\n", results.Select(r => (r.ok ? "PASS  " : "FAIL  ") + r.name));
            if (failed.Length == 0) Debug.Log("Water interaction checks: all " + results.Count + " passed.\n" + text);
            else Debug.LogError("Water interaction checks: " + failed.Length + " of " + results.Count + " failed.\n" + text);
        }

        public static List<(string name, bool ok)> Run()
        {
            var results = new List<(string, bool)>();
            void Check(string name, Func<bool> test)
            {
                try { results.Add((name, test())); }
                catch (Exception error) { results.Add((name + " — threw " + error.Message, false)); }
            }

            // ---- immersion states
            Check("Shallow water gives FeetSubmerged, knee depth KneeSubmerged, waist WaistSubmerged", () =>
            {
                var m = new ImmersionStateMachine { MinDwell = 0 };
                var a = m.Update(0.1f, -1.4f, false, 0.1f);
                var b = m.Update(0.6f, -0.9f, false, 0.1f);
                var c = m.Update(1.0f, -0.5f, false, 0.1f);
                return a == WaterContact.FeetSubmerged && b == WaterContact.KneeSubmerged && c == WaterContact.WaistSubmerged;
            });
            Check("Chest depth or the swimming controller gives Swimming; head under gives Underwater", () =>
            {
                var m = new ImmersionStateMachine { MinDwell = 0 };
                var swim = m.Update(1.35f, -0.2f, false, 0.1f);
                var under = m.Update(1.9f, 0.3f, true, 0.1f);
                return swim == WaterContact.Swimming && under == WaterContact.Underwater;
            });
            Check("No flicker at the waterline (hysteresis + dwell)", () =>
            {
                var m = new ImmersionStateMachine();
                var changes = 0; var last = m.State;
                for (var i = 0; i < 200; i++)
                {
                    var depth = m.KneeLevel + Mathf.Sin(i * 0.9f) * 0.05f;       // bobbing ±5 cm around the knee line
                    var s = m.Update(depth, -1, false, 1 / 60f);
                    if (s != last) { changes++; last = s; }
                }
                return changes <= 2;
            });
            Check("Leaving the water passes through ExitingWater, then Dry", () =>
            {
                var m = new ImmersionStateMachine { MinDwell = 0, ExitSeconds = 0.5f };
                m.Update(0.5f, -1, false, 0.1f);
                var exiting = m.Update(-0.3f, -2, false, 0.1f);
                for (var i = 0; i < 10; i++) m.Update(-0.3f, -2, false, 0.1f);
                return exiting == WaterContact.ExitingWater && m.State == WaterContact.Dry;
            });

            // ---- footsteps
            Check("A foot standing still in the water creates no repeated ripples", () =>
            {
                var foot = new FootContactTracker();
                var events = 0;
                for (var i = 0; i < 600; i++) if (foot.Update(new Vector3(0, -0.2f, 0), 0, 1 / 60f, 0.25f) > 0) events++;
                return events == 0;
            });
            Check("Walking feet trigger on contact, with the cooldown respected", () =>
            {
                var foot = new FootContactTracker();
                var events = 0; var lastEvent = -1f;
                var ok = true;
                for (var i = 0; i < 240; i++)
                {
                    var t = i / 60f;
                    var y = Mathf.Sin(t * Mathf.PI * 2 * 1.8f) * 0.12f + 0.02f;   // foot goes in and out ~1.8 times a second
                    var p = new Vector3(t * 1.4f, y, 0);
                    if (foot.Update(p, 0, 1 / 60f, 0.25f) > 0) { if (lastEvent >= 0 && t - lastEvent < 0.25f) ok = false; lastEvent = t; events++; }
                }
                return ok && events >= 5;
            });
            var pool = WaterInteractionProfile.Create(WaterKind.Pool);
            var pond = WaterInteractionProfile.Create(WaterKind.Pond);
            var river = WaterInteractionProfile.Create(WaterKind.River);
            var ocean = WaterInteractionProfile.Create(WaterKind.Ocean);
            Check("Running makes bigger splashes and ripples than walking", () =>
                WaterInteractionMath.StepSplash(pond, 7, 4.5f, 7) > WaterInteractionMath.StepSplash(pond, 2, 4.5f, 7) * 1.5f &&
                WaterInteractionMath.StepRadius(pond, 7, 4.5f, 7) > WaterInteractionMath.StepRadius(pond, 2, 4.5f, 7));
            Check("Walking splash stays in the profile's walking range (0.15–0.35)", () =>
            {
                var s = WaterInteractionMath.StepSplash(pond, 3, 4.5f, 7) / pond.SplashScale;
                return s >= 0.15f && s <= 0.45f;
            });
            Check("Standing still makes no splash", () => WaterInteractionMath.StepSplash(pond, 0.05f, 4.5f, 7) == 0);
            Check("A high jump hits harder than a gentle step in", () =>
                WaterInteractionMath.Impact(9, 1) > 0.8f && WaterInteractionMath.Impact(1.5f, 1) < 0.1f &&
                WaterInteractionMath.JumpRadius(pond, WaterInteractionMath.Impact(9, 1)) > WaterInteractionMath.JumpRadius(pond, WaterInteractionMath.Impact(3, 1)));
            Check("Footstep cooldown shortens when running (0.15–0.35 s)", () =>
                Mathf.Approximately(WaterInteractionMath.Cooldown(pond, 0), 0.35f) && Mathf.Approximately(WaterInteractionMath.Cooldown(pond, 1), 0.15f));

            // ---- profiles
            Check("Pool, pond, river and ocean behave differently", () =>
                pool.WallReflection && !ocean.WallReflection && river.AdvectWithFlow && !pond.AdvectWithFlow &&
                ocean.SplashScale > pool.SplashScale && ocean.FoamAmount > pool.FoamAmount && river.Drag > pool.Drag && pond.RippleStrength < ocean.RippleStrength);
            Check("Damping is converted for the step length (not timestep-independent)", () =>
                Mathf.Abs(pond.DampingFor(1 / 60f) - pond.DampingPerStep) < 1e-5f &&
                Mathf.Abs(pond.DampingFor(1 / 30f) - pond.DampingPerStep * pond.DampingPerStep) < 1e-5f);
            Check("Sub-stepping keeps the GPU step stable at every resolution", () =>
            {
                foreach (var res in new[] { 256, 512, 1024 })
                {
                    var cell = 24f / res;
                    var steps = WaterInteractionMath.SubSteps(1.6f, 1 / 60f, cell);
                    if (WaterInteractionMath.Courant(1.6f, 1 / 60f / steps, cell) > 0.5f) return false;
                }
                return true;
            });

            // ---- CPU reference of the shader's wave equation
            Check("A ripple expands outwards and then dissipates", () =>
            {
                var sim = new ReferenceSim(96, 0.1f, 1.2f, pond.DampingPerStep);
                sim.Drop(48, 48, 3, -0.05f);
                var early = sim.RingRadius();
                sim.Step(60);
                var later = sim.RingRadius();
                var energyMid = sim.Energy();
                sim.Step(600);
                return later > early + 4 && sim.Energy() < energyMid * 0.05f;
            });
            Check("Two ripple sources overlap and interfere (superposition)", () =>
            {
                var a = new ReferenceSim(96, 0.1f, 1.2f, 0.99f); a.Drop(36, 48, 3, -0.04f); a.Step(40);
                var b = new ReferenceSim(96, 0.1f, 1.2f, 0.99f); b.Drop(60, 48, 3, -0.04f); b.Step(40);
                var both = new ReferenceSim(96, 0.1f, 1.2f, 0.99f); both.Drop(36, 48, 3, -0.04f); both.Drop(60, 48, 3, -0.04f); both.Step(40);
                var error = 0f;
                for (var i = 0; i < both.H.Length; i++) error = Mathf.Max(error, Mathf.Abs(both.H[i] - (a.H[i] + b.H[i])));
                return error < 1e-4f && Mathf.Abs(both.At(48, 48)) > 1e-5f;
            });
            Check("Walls reflect ripples (pool), open edges absorb", () =>
            {
                var walled = new ReferenceSim(64, 0.1f, 1.2f, 0.995f) { Walls = true }; walled.Drop(16, 32, 3, -0.05f); walled.Step(200);
                var open = new ReferenceSim(64, 0.1f, 1.2f, 0.995f) { Walls = false }; open.Drop(16, 32, 3, -0.05f); open.Step(200);
                return walled.Energy() > open.Energy();
            });

            UnityEngine.Object.DestroyImmediate(pool); UnityEngine.Object.DestroyImmediate(pond); UnityEngine.Object.DestroyImmediate(river); UnityEngine.Object.DestroyImmediate(ocean);
            return results;
        }

        /// The same explicit damped wave equation as WaterRippleSim.shader, on the CPU.
        private sealed class ReferenceSim
        {
            public readonly float[] H, P;
            private readonly int n;
            private readonly float c2, damping, cell;
            public bool Walls;
            public ReferenceSim(int size, float cellSize, float speed, float dampingPerStep)
            {
                n = size; cell = cellSize; H = new float[n * n]; P = new float[n * n];
                var steps = WaterInteractionMath.SubSteps(speed, 1 / 60f, cell);
                var courant = WaterInteractionMath.Courant(speed, 1 / 60f / steps, cell);
                c2 = courant * courant; damping = Mathf.Pow(dampingPerStep, 1f / steps);
            }
            public float At(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0 : H[y * n + x];
            public void Drop(int cx, int cy, float radius, float amount)
            {
                for (var y = 0; y < n; y++) for (var x = 0; x < n; x++)
                {
                    var q = ((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (radius * radius);
                    H[y * n + x] += amount * Mathf.Exp(-q) * (1 - 0.35f * q);
                }
            }
            public void Step(int count)
            {
                var next = new float[n * n];
                for (var s = 0; s < count; s++)
                {
                    for (var y = 0; y < n; y++) for (var x = 0; x < n; x++)
                    {
                        var i = y * n + x;
                        var lap = At(x + 1, y) + At(x - 1, y) + At(x, y + 1) + At(x, y - 1) - 4 * H[i];
                        var h = (2 * H[i] - P[i] + c2 * lap) * damping;
                        var edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                        if (!Walls) h *= Mathf.Lerp(0.85f, 1, Mathf.Clamp01(edge / 5f));
                        next[i] = h;
                    }
                    Array.Copy(H, P, H.Length);
                    Array.Copy(next, H, H.Length);
                }
            }
            public float Energy() { var e = 0f; foreach (var h in H) e += h * h; return e; }
            public float RingRadius()
            {
                // Radius (in cells) of the strongest disturbance along the row through the centre.
                var best = 0f; var at = 0;
                for (var x = n / 2; x < n; x++) { var v = Mathf.Abs(At(x, n / 2)); if (v > best) { best = v; at = x - n / 2; } }
                return at;
            }
        }
    }
}
