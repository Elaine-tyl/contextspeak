using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Swimming — Edit-mode checks of the pure parts (state machine,
    /// buoyancy, surface follower, profiles, clips, controller hooks).
    /// ContextSpeak > Checks > Swimming Play Mode Tests — opens SwimmingTestScene, enters Play mode and
    /// runs SwimmingPlayModeTest (real player, real physics), then leaves Play mode and logs the report.
    [InitializeOnLoad]
    public static class SwimmingChecks
    {
        private const string Flag = "ContextSpeak.SwimmingPlayTest";

        static SwimmingChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("ContextSpeak/Checks/Swimming")]
        public static void RunMenu()
        {
            var lines = Run();
            var failed = lines.Count(l => l.StartsWith("FAIL"));
            var text = "Swimming checks: " + (lines.Count - failed) + "/" + lines.Count + " passed\n" + string.Join("\n", lines);
            if (failed > 0) Debug.LogError(text); else Debug.Log(text);
        }

        public static List<string> Run()
        {
            var lines = new List<string>();
            void Check(string name, bool ok, string detail) => lines.Add((ok ? "PASS " : "FAIL ") + name + " — " + detail);
            const float dt = 1 / 60f;

            // Buoyancy: settles at the float line from above and below, never sinks, damped.
            var b = new CharacterBuoyancyController();
            float Simulate(float feet, float v, float seconds, float surface, out float min, out float max, float thrust = 0, float net = 1, Func<float, float> wave = null)
            {
                min = float.MaxValue; max = float.MinValue;
                for (var t = 0f; t < seconds; t += dt)
                {
                    var s = wave != null ? wave(t) : surface;
                    v = b.Step(v, feet, s, 1.7f, 1.38f, 1, 2.8f, 1.5f, 1.6f, thrust, net, dt);
                    feet += v * dt;
                    min = Mathf.Min(min, feet); max = Mathf.Max(max, feet);
                }
                return feet;
            }
            var rest = Simulate(-1.38f, 0, 20, 0, out var lo, out var hi);
            Check("Buoyancy equilibrium", Mathf.Abs(rest + 1.38f) < 0.01f && hi - lo < 0.02f, "idle body stays at " + rest.ToString("0.000") + " (float line −1.380), range " + (hi - lo).ToString("0.000") + " m");
            var fromDeep = Simulate(-3f, 0, 20, 0, out _, out var peak);
            Check("Rises from depth without overshooting out of the water", Mathf.Abs(fromDeep + 1.38f) < 0.02f && peak < -1.0f, "settles at " + fromDeep.ToString("0.00") + ", highest feet " + peak.ToString("0.00") + " m");
            var plunge = Simulate(0f, -5.5f, 15, 0, out var deepest, out _);
            Check("Jump in: plunges then resurfaces", deepest < -1.6f && Mathf.Abs(plunge + 1.38f) < 0.03f, "deepest feet " + deepest.ToString("0.00") + " m, settles " + plunge.ToString("0.00"));
            Simulate(-1.38f, 0, 6, 0, out var diveMin, out _, -5.5f * 1.2f, 0.3f);
            Check("Dive thrust goes down, capped", diveMin < -3f, "feet reach " + diveMin.ToString("0.00") + " m in 6 s");
            var waveFeet = new List<float>(); var waveSurface = new List<float>();
            var feetW = -1.38f; var vW = 0f;
            for (var t = 0f; t < 20; t += dt)
            {
                var s = 0.4f * Mathf.Sin(t * 2 * Mathf.PI / 6f);
                vW = b.Step(vW, feetW, s, 1.7f, 1.38f, 1, 2.4f, 1.5f, 1.6f, 0, 1, dt);
                feetW += vW * dt;
                if (t > 8) { waveFeet.Add(feetW); waveSurface.Add(s); }
            }
            var gain = (waveFeet.Max() - waveFeet.Min()) / (waveSurface.Max() - waveSurface.Min());
            Check("Rides a 0.8 m, 6 s swell", gain > 0.7f && gain < 1.5f, "body/wave height ratio " + gain.ToString("0.00"));

            // Surface follower: smooth, follows, bobs.
            var f = new WaterSurfaceFollower { Smoothing = 0.25f, WaveFollow = 1, BobAmplitude = 0 };
            var h = 0f; for (var i = 0; i < 120; i++) h = f.Update(1, 0, dt);
            Check("Surface follower converges", Mathf.Abs(h - 1) < 0.02f, "after 2 s: " + h.ToString("0.000"));
            f = new WaterSurfaceFollower { Smoothing = 0.25f, WaveFollow = 0, BobAmplitude = 0.04f };
            var bobs = new List<float>(); for (var i = 0; i < 600; i++) bobs.Add(f.Update(0.5f, 0, dt));
            Check("Pool (WaveFollow 0) ignores waves, keeps a small bob", Mathf.Abs(bobs.Average()) < 0.02f && bobs.Max() - bobs.Min() > 0.03f && bobs.Max() - bobs.Min() < 0.1f, "range " + (bobs.Max() - bobs.Min()).ToString("0.000") + " m");

            // State machine.
            var m = new SwimmingStateMachine();
            SwimSense S(float immersion, float depth, float head = -0.5f, float aboveBed = 0, bool grounded = true, Vector2 move = default, bool down = false, bool toggle = false, float vs = 0)
                => new SwimSense { InWater = immersion > 0.005f, Immersion = immersion, Depth = depth, HeadDepth = head, FeetAboveBed = aboveBed, Grounded = grounded, Move = move, Down = down, ToggleBackFloat = toggle, VerticalSpeed = vs };
            void Hold(SwimSense s, float seconds) { for (var t = 0f; t < seconds; t += dt) m.Update(s, dt); }
            Hold(S(0.05f, 0.1f), 2);
            Check("Puddle never swims", m.State == SwimState.Wading, "state " + m.State);
            Hold(S(0.5f, 0.85f), 2);
            Check("Waist deep wades", m.State == SwimState.Wading, "state " + m.State);
            Hold(S(0.8f, 1.0f), 2);
            Check("Chest deep but shallow pool does not swim", m.State == SwimState.Wading, "state " + m.State);
            var changes = 0; m.Changed += (a, c) => changes++;
            for (var t = 0f; t < 3; t += dt) m.Update(S(0.72f + 0.03f * Mathf.Sin(t * 40), 1.6f), dt);
            Check("No flicker at the threshold", changes <= 2, changes + " transitions in 3 s of ±3 cm jitter (state " + m.State + ")");
            Hold(S(0.82f, 1.8f, -0.3f, 0.4f, false), 1);
            Check("Deep water: enters then treads", m.State == SwimState.TreadingWater, "state " + m.State);
            Hold(S(0.82f, 1.8f, -0.3f, 0.4f, false, new Vector2(0, 1)), 0.6f);
            Check("Input → surface swimming", m.State == SwimState.SurfaceSwimming, "state " + m.State);
            Hold(S(0.82f, 1.8f, -0.3f, 0.4f, false), 0.6f);
            Check("Release → treading", m.State == SwimState.TreadingWater, "state " + m.State);
            m.Update(S(0.82f, 1.8f, -0.3f, 0.4f, false, toggle: true), dt); Hold(S(0.82f, 1.8f, -0.3f, 0.4f, false), 0.5f);
            Check("F → back float", m.State == SwimState.BackFloating, "state " + m.State);
            m.Update(S(0.82f, 1.8f, -0.3f, 0.4f, false, toggle: true), dt);
            Hold(S(0.9f, 2.5f, 0.1f, 0.4f, false, down: true), 0.3f);
            Check("C → dive", m.State == SwimState.Diving, "state " + m.State);
            Hold(S(1f, 2.5f, 0.6f, 0.5f, false, down: true), 1);
            Check("Dive → underwater", m.State == SwimState.UnderwaterSwimming, "state " + m.State);
            Hold(S(0.85f, 2.5f, 0.05f, 0.5f, false, vs: 0.5f), 0.4f);
            Check("Rising → surfacing", m.State == SwimState.Surfacing, "state " + m.State);
            Hold(S(0.82f, 2.5f, -0.3f, 0.5f, false), 1);
            Check("Surfacing → treading", m.State == SwimState.TreadingWater, "state " + m.State);
            Hold(S(0.62f, 1.0f, -0.6f, 0.05f, true, new Vector2(0, 1)), 1);
            Check("Shallow + feet on the bed → exiting", m.State == SwimState.ExitingWater, "state " + m.State);
            m.Release(true);
            Check("Release hands back to wading", m.State == SwimState.Wading && !m.OwnsMovement, "state " + m.State);

            // Profiles differ by water.
            var pool = SwimmingProfile.Create(WaterKind.Pool); var ocean = SwimmingProfile.Create(WaterKind.Ocean);
            Check("Profiles differ (pool vs ocean)", pool.WaveFollow < ocean.WaveFollow && pool.CurrentInfluence < ocean.CurrentInfluence && ocean.WaveSurge > 0, "wave follow " + pool.WaveFollow + "/" + ocean.WaveFollow + ", current " + pool.CurrentInfluence + "/" + ocean.CurrentInfluence);
            UnityEngine.Object.DestroyImmediate(pool); UnityEngine.Object.DestroyImmediate(ocean);

            // Hooks in the shared controller files.
            Check("CafePlayer locomotion override hook", typeof(CafePlayer).GetField("LocomotionOverride") != null && typeof(CafePlayer).GetMethod("SetVelocity") != null, "tools/swim_patch.py applied");
            Check("AuthoredMotion override hook", typeof(AuthoredMotion).GetField("Override") != null, "tools/swim_patch.py applied");

            // Clips.
            foreach (var model in new[] { "Academy", "Daydream" })
            {
                var set = AssetDatabase.LoadAssetAtPath<SwimClipSet>(SwimmingSetup.ClipSetFolder + model + " Swim Clips.asset");
                if (set == null) { Check(model + " swim clips", false, "not imported — run ContextSpeak > Water > Setup Swimming System"); continue; }
                var loopOk = set.Swim != null && set.Swim.wrapMode == WrapMode.Loop;
                var seam = 0f;
                if (set.Swim != null)
                    foreach (var group in AnimationUtility.GetCurveBindings(set.Swim).Where(x => x.propertyName.StartsWith("m_LocalRotation")).GroupBy(x => x.path))
                    {
                        var c = group.ToDictionary(x => x.propertyName.Substring(x.propertyName.Length - 1), x => AnimationUtility.GetEditorCurve(set.Swim, x));
                        if (c.Count < 4) continue;
                        Quaternion At(float t) => new Quaternion(c["x"].Evaluate(t), c["y"].Evaluate(t), c["z"].Evaluate(t), c["w"].Evaluate(t)).normalized;
                        seam = Mathf.Max(seam, Quaternion.Angle(At(0), At(set.Swim.length)));
                    }
                Check(model + " swim clips", loopOk && set.EdgeReach != null && seam < 2f && Mathf.Abs(set.SwimBodyPitch) < 35,
                    "swim loop " + (set.Swim != null ? set.Swim.length.ToString("0.00") + " s, seam " + seam.ToString("0.000") : "missing") + ", edge reach " + (set.EdgeReach != null ? "ok" : "missing") + ", body pitch " + set.SwimBodyPitch.ToString("0") + "°");
            }
            return lines;
        }

        // ------------------------------------------------------------------ Play mode

        [MenuItem("ContextSpeak/Checks/Swimming Play Mode Tests")]
        public static void PlayTests()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(SwimmingSetup.TestScene)) SwimmingSetup.BuildTestScene();
            EditorSceneManager.OpenScene(SwimmingSetup.TestScene);
            EditorPrefs.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(Flag, false)) return;
            EditorPrefs.SetBool(Flag, false);
            new GameObject("Swimming Play-mode test").AddComponent<SwimmingPlayModeTest>();
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        private static void Watch()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Watch; return; }
            if (!SwimmingPlayModeTest.Finished) return;
            EditorApplication.update -= Watch;
            Debug.Log("Swimming Play-mode tests finished: " + SwimmingPlayModeTest.Summary + " — report: Logs/SwimmingPlayModeReport.md");
            EditorApplication.ExitPlaymode();
        }
    }
}
