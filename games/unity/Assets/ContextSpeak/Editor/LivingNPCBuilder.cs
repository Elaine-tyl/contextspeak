using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > NPC > Build Living NPC System
    ///  1–3. inspects the character prefabs (animation system, rig, which clips exist),
    ///  4.   builds per-model NPC animation sets (sit / wave / talk / look-around retargeted by bone name
    ///       from whichever supplied FBX has them; walk-clip ground speed measured so feet do not slide),
    ///  5–10. creates editable NPC profiles + schedules and builds the first complete demonstration:
    ///       the Living Neighbourhood (family house, apartments, Forest Café with staff room, supermarket,
    ///       school entrance, three signalled crossings with traffic, two tram stops and a tram, 14 NPCs),
    ///  11–13. bakes navigation, saves, and reports missing animations and models.
    /// Existing scenes are not modified. ContextSpeak > NPC > Add Living NPC Systems To Open Scene adds
    /// the managers to another scene (with Undo).
    public static class LivingNPCBuilder
    {
        public const string Scene = "Assets/ContextSpeak/Scenes/LivingNeighborhood.unity";
        public const string ProfileFolder = "Assets/ContextSpeak/Data/LivingNPC/Profiles";
        public const string SetFolder = "Assets/ContextSpeak/Resources/LivingNPC";
        public const string ClipFolder = "Assets/Generated/Motion/Living/";
        private static readonly string[] Models = { "CafeCharm", "Elliot", "OldCardigan", "TrenchElder", "OfficerTaylor", "OfficerMason", "Academy", "Daydream" };

        [MenuItem("ContextSpeak/NPC/Build Living NPC System")]
        public static void BuildMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = new List<string>();
            report.AddRange(Inspect());
            report.AddRange(AnimationSets());
            var profiles = Profiles(report);
            report.AddRange(BuildScene(profiles));
            Debug.Log("Living NPC system built:\n- " + string.Join("\n- ", report));
        }

        // ------------------------------------------------------------------ 1–3 inspection

        public static List<string> Inspect()
        {
            var report = new List<string>();
            foreach (var model in Models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotionAssets.Folder + model + ".prefab");
                if (prefab == null) { report.Add("MISSING model prefab: " + model + " (Generated/Motion) — build the characters first"); continue; }
                var motion = prefab.GetComponentInChildren<AuthoredMotion>(true);
                var animator = prefab.GetComponentInChildren<Animator>(true);
                var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var bones = skinned.SelectMany(s => s.bones).Where(b => b != null).Select(b => b.name).Distinct().ToArray();
                var mixamo = bones.Any(b => b.EndsWith("Hips")) && bones.Any(b => b.EndsWith("LeftHand"));
                var clips = motion != null ? new[] { motion.Idle, motion.Walk, motion.Run }.Concat(motion.Extras).Where(c => c != null).Select(c => c.name.Replace(model + "-", "")).ToArray() : new string[0];
                report.Add(model + ": " + (skinned.Length > 0 ? "skinned (" + bones.Length + " bones, " + (mixamo ? "Mixamo skeleton — retargetable by bone name" : "NOT a Mixamo skeleton") + ")" : "NOT RIGGED — cannot animate") +
                           ", " + (motion != null ? "legacy Animation via AuthoredMotion" : animator != null ? "Animator" : "no animation component") +
                           ", humanoid avatar: no (generic/legacy)" + ", clips: " + string.Join(" ", clips));
            }
            report.Add("Facial: the Meshy models have no facial blendshapes or eye bones — blinking, eye saccades and visemes are NOT possible on them; gaze is done with the neck/head bones.");
            report.Add("Foot IK: legacy Animation has no IK pass; ground contact relies on walk-speed matching (measured per model) and NavMesh height.");
            return report;
        }

        // ------------------------------------------------------------------ 4 animation sets

        public static List<string> AnimationSets()
        {
            var report = new List<string>();
            Directory.CreateDirectory(SetFolder);
            Directory.CreateDirectory(ClipFolder);
            AssetDatabase.Refresh();
            AnimationClip Source(params string[] names)
            {
                foreach (var n in names) { var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(MotionAssets.Folder + n + ".anim"); if (c != null) return c; }
                return null;
            }
            var sitSource = Source("CafeCharm-Sit_to_Stand_Transition_F", "Academy-Sit_to_Stand_Transition_F", "Daydream-Sit_to_Stand_Transition_F");
            var waveSource = Source("Academy-Big_Wave_Hello", "Daydream-Big_Wave_Hello", "CafeCharm-Hello_Run");
            var talkSource = Source("Elliot-Agree_Gesture", "Academy-Agree_Gesture", "Daydream-Agree_Gesture");
            var lookSource = Source("Elliot-Alert", "Daydream-Alert", "Academy-Alert_Quick_Turn_Right");
            var danceSource = Source("Academy-All_Night_Dance", "Elliot-All_Night_Dance");
            if (sitSource == null) report.Add("MISSING animation: a sitting clip (Sit_to_Stand…) — NPCs sit with a lowered standing pose (TEMPORARY)");
            if (waveSource == null) report.Add("MISSING animation: wave — procedural wave used");
            if (talkSource == null) report.Add("MISSING animation: talking gesture — procedural gestures used");
            foreach (var model in Models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotionAssets.Folder + model + ".prefab");
                if (prefab == null) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var motion = instance.GetComponentInChildren<AuthoredMotion>(true);
                    var rig = motion != null && motion.Player != null ? motion.Player.gameObject : instance;
                    var path = SetFolder + "/" + model + " NPC Animations.asset";
                    var set = AssetDatabase.LoadAssetAtPath<NPCAnimationSet>(path);
                    if (set == null) { set = ScriptableObject.CreateInstance<NPCAnimationSet>(); AssetDatabase.CreateAsset(set, path); }
                    set.ModelName = model;
                    set.Sit = sitSource != null ? Save(SittingPose(sitSource, rig, motion), model + "-Sit") : null;
                    set.Wave = waveSource != null ? Save(Retarget(waveSource, rig, WrapMode.Once), model + "-Wave") : null;
                    set.Talk = talkSource != null ? Save(Retarget(talkSource, rig, WrapMode.Loop), model + "-Talk") : null;
                    set.LookAround = lookSource != null ? Save(Retarget(lookSource, rig, WrapMode.Once), model + "-LookAround") : null;
                    set.Dance = danceSource != null ? Save(Retarget(danceSource, rig, WrapMode.Loop), model + "-Dance") : null;
                    set.WalkClipSpeed = motion != null && motion.Walk != null ? StrideSpeed(motion.Walk, rig, motion.transform, 1.35f) : 1.35f;
                    set.RunClipSpeed = motion != null && motion.Run != null ? StrideSpeed(motion.Run, rig, motion.transform, 3.6f) : 3.6f;
                    set.Notes = "Authored: sit pose, wave, talk gesture, look-around (retargeted). TEMPORARY procedural: phone, drink, eat, read, type, cook, wipe, machine, browse, pay, point, shrug, nod, stretch, yawn, map, photo, lie down.";
                    EditorUtility.SetDirty(set);
                    report.Add(model + " animations: sit " + (set.Sit != null) + ", wave " + (set.Wave != null) + ", talk " + (set.Talk != null) + ", look " + (set.LookAround != null) + " · walk clip ground speed " + set.WalkClipSpeed.ToString("0.00") + " m/s");
                }
                finally { Object.DestroyImmediate(instance); }
            }
            report.Add("MISSING authored everyday actions (procedural TEMPORARY used): drink coffee, eat, phone, read, type, cook, wipe, push cart, umbrella, take photo, carry bag, lie down — suggested Mixamo: Drinking, Eating, Texting While Standing, Reading, Typing, Cooking, Cleaning, Sitting Idle, Sleeping Idle.");
            AssetDatabase.SaveAssets();
            return report;
        }

        private static AnimationClip Save(AnimationClip clip, string name)
        {
            clip.name = name;
            var path = ClipFolder + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// Copies a legacy clip onto another rig of the same (Mixamo) skeleton, matching bones by name.
        private static AnimationClip Retarget(AnimationClip source, GameObject rig, WrapMode wrap)
        {
            var byName = new Dictionary<string, string>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t != rig.transform && !byName.ContainsKey(t.name)) byName[t.name] = AnimationUtility.CalculateTransformPath(t, rig.transform);
            var clip = new AnimationClip { legacy = true, frameRate = source.frameRate, wrapMode = wrap };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
                var target = binding;
                if (rig.transform.Find(binding.path) == null)
                {
                    var last = binding.path.Substring(binding.path.LastIndexOf('/') + 1);
                    if (!byName.TryGetValue(last, out var p)) continue;
                    target.path = p;
                }
                AnimationUtility.SetEditorCurve(clip, target, AnimationUtility.GetEditorCurve(source, binding));
            }
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        /// The seated end of a sit/stand transition as a held pose (whichever end has the lower hips).
        private static AnimationClip SittingPose(AnimationClip source, GameObject rig, AuthoredMotion motion)
        {
            var moved = Retarget(source, rig, WrapMode.ClampForever);
            var hips = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Hips"));
            var at = 0f;
            if (hips != null)
            {
                moved.SampleAnimation(rig, 0); var start = hips.position.y;
                moved.SampleAnimation(rig, moved.length); var end = hips.position.y;
                at = start <= end ? 0 : moved.length;
                if (motion != null && motion.Idle != null) motion.Idle.SampleAnimation(rig, 0);
            }
            var pose = new AnimationClip { legacy = true, frameRate = 30, wrapMode = WrapMode.Loop };
            foreach (var binding in AnimationUtility.GetCurveBindings(moved))
            {
                var value = AnimationUtility.GetEditorCurve(moved, binding).Evaluate(at);
                if (binding.path.EndsWith("Hips") && (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z")) value = AnimationUtility.GetEditorCurve(moved, binding).Evaluate(0);
                AnimationUtility.SetEditorCurve(pose, binding, AnimationCurve.Constant(0, 1, value));
            }
            Object.DestroyImmediate(moved);
            return pose;
        }

        /// Ground speed implied by the walk cycle: each foot sweeps back one step length per step, two steps per cycle.
        private static float StrideSpeed(AnimationClip clip, GameObject rig, Transform root, float fallback)
        {
            var foot = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("LeftFoot"));
            if (foot == null || clip.length <= 0.1f) return fallback;
            var min = float.MaxValue; var max = float.MinValue;
            for (var i = 0; i <= 40; i++)
            {
                clip.SampleAnimation(rig, clip.length * i / 40f);
                var z = root.InverseTransformPoint(foot.position).z * root.lossyScale.z;
                min = Mathf.Min(min, z); max = Mathf.Max(max, z);
            }
            var speed = 2 * (max - min) / clip.length;
            return speed > 0.5f && speed < 6 ? speed : fallback;
        }

        // ------------------------------------------------------------------ 5–8 profiles

        private sealed class Person
        {
            public string Id, Name, Job, Model, Home, Work, WalkWith = "";
            public AgeGroup Age = AgeGroup.Adult;
            public string[] Friends = new string[0], Family = new string[0], Coworkers = new string[0];
            public float Social = 0.5f, Shy = 0.3f, Patience = 0.6f, Busy = 0.4f, Distract = 0.4f;
            public int Band = 6; public EnglishAccent Accent = EnglishAccent.Australian;
            public string Schedule;
            public Vector3 Start;
            public string[] Greetings, Talk;
        }

        private static string J(params (float s, float e, string a, string l)[] blocks) =>
            "[\n" + string.Join(",\n", blocks.Select(b => "  { \"startHour\": " + b.s.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"endHour\": " + b.e.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"activity\": \"" + b.a + "\", \"location\": \"" + b.l + "\" }")) + "\n]";

        private static List<Person> People() => new List<Person>
        {
            // Emma's routine is exactly the designer JSON (Chinese location names resolve to places).
            new Person { Id = "emma", Name = "Emma Carter", Job = "Forest Café Barista", Model = "CafeCharm", Home = "riverside", Work = "forest_cafe", Friends = new[] { "mei" }, Coworkers = new[] { "liam" },
                Social = 0.55f, Shy = 0.55f, Patience = 0.85f, Busy = 0.65f, Distract = 0.2f, Band = 7, Accent = EnglishAccent.Australian, Start = new Vector3(-52, 0, 6),
                Greetings = new[] { "Hi there!", "Morning!", "Hey, welcome in!" }, Talk = new[] { "It's been busy this morning!", "Have you tried the new oat latte?", "I love this time of day." },
                Schedule = J((0, 7, "Sleep", "住宅"), (7, 8, "Breakfast", "住宅"), (8, 9, "Commute", "城市 / Tram"), (9, 12, "ServeCustomers", "Forest Café"), (12, 13, "Break", "员工休息区"),
                             (13, 17, "Work", "Forest Café"), (17, 18, "LeaveWork", "城市"), (18, 20, "Socialize", "商场 / 餐厅"), (20, 23, "Relax", "住宅"), (23, 24, "Sleep", "住宅")) },
            new Person { Id = "liam", Name = "Liam Nguyen", Job = "Barista", Model = "Elliot", Home = "riverside", Work = "forest_cafe", Friends = new[] { "emma", "sam" }, Coworkers = new[] { "emma" },
                Social = 0.8f, Shy = 0.1f, Busy = 0.6f, Band = 6, Accent = EnglishAccent.Australian, Start = new Vector3(10, 0, 19),
                Greetings = new[] { "G'day!", "Hey mate!", "What can I get you?" }, Talk = new[] { "Footy on the weekend?", "Big order just came in.", "Coffee's on me later!" },
                Schedule = J((0, 6, "Sleep", "home"), (6, 7, "Breakfast", "home"), (7, 12, "ServeCustomers", "forest_cafe"), (12, 12.5f, "Break", "staff"), (12.5f, 15, "Work", "forest_cafe"),
                             (15, 18, "Socialize", "café"), (18, 22, "Relax", "home"), (22, 24, "Sleep", "home")) },
            new Person { Id = "david", Name = "David Tan", Job = "Supermarket cashier", Model = "OldCardigan", Home = "house_tan", Work = "supermarket", Family = new[] { "mei", "sophie" }, Friends = new[] { "arthur" },
                Social = 0.45f, Shy = 0.35f, Busy = 0.55f, Band = 6, Accent = EnglishAccent.Malaysian, Start = new Vector3(-36, 0, 14),
                Greetings = new[] { "Morning!", "Hello!" }, Talk = new[] { "Did you see the specials today?", "The tram was packed this morning." },
                Schedule = J((0, 6.5f, "Sleep", "home"), (6.5f, 7.75f, "Breakfast", "home"), (7.75f, 8.75f, "Commute", "城市 / Tram"), (8.75f, 17, "Work", "supermarket"),
                             (17, 18, "LeaveWork", "城市"), (18, 19, "Dinner", "home"), (19, 22.5f, "Relax", "home"), (22.5f, 24, "Sleep", "home")) },
            new Person { Id = "mei", Name = "Mei Tan", Job = "Graphic designer", Model = "CafeCharm", Home = "house_tan", Work = "", Family = new[] { "david", "sophie" }, Friends = new[] { "emma" },
                Social = 0.6f, Shy = 0.3f, Busy = 0.5f, Distract = 0.6f, Band = 8, Accent = EnglishAccent.British, Start = new Vector3(-34, 0, 16),
                Greetings = new[] { "Hi!", "Good morning!" }, Talk = new[] { "I've got a deadline today.", "This café is my second office!" },
                Schedule = J((0, 7, "Sleep", "home"), (7, 8.5f, "Breakfast", "home"), (8.5f, 11, "Coffee", "Forest Café"), (11, 12, "Shop", "supermarket"), (12, 13, "Lunch", "home"),
                             (13, 17.5f, "Relax", "home"), (17.5f, 19, "Dinner", "home"), (19, 23, "Relax", "home"), (23, 24, "Sleep", "home")) },
            new Person { Id = "sophie", Name = "Sophie Tan", Job = "High-school student", Model = "Academy", Age = AgeGroup.Teen, Home = "house_tan", Work = "school", WalkWith = "jack", Family = new[] { "david", "mei" }, Friends = new[] { "jack", "ava" },
                Social = 0.7f, Shy = 0.25f, Distract = 0.7f, Band = 6, Accent = EnglishAccent.Australian, Start = new Vector3(-37, 0, 12),
                Greetings = new[] { "Hi!", "Heyyy!" }, Talk = new[] { "Did you finish the maths homework?", "We have a test on Friday!", "Want to get bubble tea later?" },
                Schedule = J((0, 7, "Sleep", "home"), (7, 8, "Breakfast", "home"), (8, 15, "School", "school"), (15, 16.5f, "Coffee", "Forest Café"), (16.5f, 22, "Relax", "home"), (22, 24, "Sleep", "home")) },
            new Person { Id = "jack", Name = "Jack Wilson", Job = "High-school student", Model = "Daydream", Age = AgeGroup.Teen, Home = "riverside", Work = "school", WalkWith = "sophie", Friends = new[] { "sophie", "ava" },
                Social = 0.65f, Shy = 0.3f, Distract = 0.6f, Band = 5, Accent = EnglishAccent.Australian, Start = new Vector3(-46, 0, 6.5f),
                Greetings = new[] { "Hey!", "Yo!" }, Talk = new[] { "I'm so tired today.", "Did you watch the game?", "Let's walk together!" },
                Schedule = J((0, 7, "Sleep", "home"), (7, 7.8f, "Breakfast", "home"), (7.8f, 15, "School", "school"), (15, 17, "Socialize", "café"), (17, 22.5f, "Relax", "home"), (22.5f, 24, "Sleep", "home")) },
            new Person { Id = "ava", Name = "Ava Patel", Job = "High-school student", Model = "Academy", Age = AgeGroup.Teen, Home = "riverside", Work = "school", Friends = new[] { "sophie", "jack" },
                Social = 0.5f, Shy = 0.5f, Distract = 0.5f, Band = 7, Accent = EnglishAccent.Indian, Start = new Vector3(-20, 0, 6),
                Greetings = new[] { "Hi!", "Morning!" }, Talk = new[] { "I'm reading a great book.", "Library after school?" },
                Schedule = J((0, 7, "Sleep", "home"), (7, 7.8f, "Breakfast", "home"), (7.8f, 15, "School", "school"), (15, 16, "Shop", "supermarket"), (16, 22, "Relax", "home"), (22, 24, "Sleep", "home")) },
            new Person { Id = "ted", Name = "Ted Morgan", Job = "Tourist", Model = "TrenchElder", Age = AgeGroup.Senior, Home = "riverside", Work = "", Friends = new string[0],
                Social = 0.7f, Shy = 0.15f, Patience = 0.7f, Distract = 0.8f, Band = 9, Accent = EnglishAccent.Scottish, Start = new Vector3(-12, 0, -6),
                Greetings = new[] { "Hello there!", "Good morning to you!" }, Talk = new[] { "What a charming street!", "Is the tram this way?" },
                Schedule = J((0, 8, "Sleep", "home"), (8, 12, "Explore", "street"), (12, 13, "Coffee", "Forest Café"), (13, 18, "Explore", "street"), (18, 20, "Socialize", "café"), (20, 24, "Sleep", "home")) },
            new Person { Id = "officer", Name = "Officer Rosa Diaz", Job = "Police officer", Model = "OfficerTaylor", Home = "riverside", Work = "street", Friends = new[] { "arthur" },
                Social = 0.5f, Shy = 0.2f, Busy = 0.5f, Band = 7, Accent = EnglishAccent.American, Start = new Vector3(18, 0, -6),
                Greetings = new[] { "Morning.", "Have a good one!" }, Talk = new[] { "All quiet today.", "Watch the crossing lights." },
                Schedule = J((0, 7, "Sleep", "home"), (7, 8, "Breakfast", "home"), (8, 12, "Wander", "street"), (12, 13, "Coffee", "Forest Café"), (13, 18, "Wander", "street"), (18, 23, "Relax", "home"), (23, 24, "Sleep", "home")) },
            new Person { Id = "arthur", Name = "Arthur Bell", Job = "Retiree", Model = "OldCardigan", Age = AgeGroup.Senior, Home = "riverside", Work = "", Friends = new[] { "officer", "david" },
                Social = 0.75f, Shy = 0.2f, Patience = 0.9f, Busy = 0.1f, Distract = 0.1f, Band = 8, Accent = EnglishAccent.British, Start = new Vector3(0, 0, 7),
                Greetings = new[] { "Morning, morning!", "Lovely day!" }, Talk = new[] { "In my day this street had a bakery.", "My knees don't like the cold." },
                Schedule = J((0, 6, "Sleep", "home"), (6, 8, "Breakfast", "home"), (8, 10, "Rest", "street"), (10, 11, "Coffee", "Forest Café"), (11, 12, "Shop", "supermarket"), (12, 16, "Wander", "street"), (16, 21, "Relax", "home"), (21, 24, "Sleep", "home")) },
            new Person { Id = "nina", Name = "Nina Kowalski", Job = "Nurse", Model = "CafeCharm", Home = "riverside", Work = "tram_east", Friends = new[] { "zoe" },
                Social = 0.4f, Shy = 0.4f, Busy = 0.8f, Distract = 0.5f, Band = 7, Accent = EnglishAccent.NewZealand, Start = new Vector3(-30, 0, -6),
                Greetings = new[] { "Hi!" }, Talk = new[] { "Long night shift…", "I need a coffee!" },
                Schedule = J((0, 7, "Sleep", "home"), (7, 8, "Breakfast", "home"), (8, 9, "Commute", "tram"), (9, 17, "Wander", "street"), (17, 18, "Coffee", "café"), (18, 23, "Relax", "home"), (23, 24, "Sleep", "home")) },
            new Person { Id = "sam", Name = "Sam O'Brien", Job = "Office worker", Model = "Elliot", Home = "riverside", Work = "", Friends = new[] { "liam", "zoe" },
                Social = 0.6f, Shy = 0.3f, Busy = 0.9f, Distract = 0.8f, Band = 6, Accent = EnglishAccent.Irish, Start = new Vector3(6, 0, 12.5f),
                Greetings = new[] { "Morning!", "Hey!" }, Talk = new[] { "Meetings all day…", "Can't function without coffee." },
                Schedule = J((0, 7, "Sleep", "home"), (7, 8, "Breakfast", "home"), (8, 9, "Coffee", "Forest Café"), (9, 17, "Wander", "street"), (17, 19, "Socialize", "café"), (19, 23, "Relax", "home"), (23, 24, "Sleep", "home")) },
            new Person { Id = "zoe", Name = "Zoe Chen", Job = "University student", Model = "Academy", Age = AgeGroup.YoungAdult, Home = "riverside", Work = "", Friends = new[] { "sam", "nina" },
                Social = 0.4f, Shy = 0.6f, Busy = 0.6f, Distract = 0.7f, Band = 8, Accent = EnglishAccent.American, Start = new Vector3(-56, 0, 7),
                Greetings = new[] { "Oh, hi!" }, Talk = new[] { "Essay due tomorrow…", "This café has the best wifi." },
                Schedule = J((0, 8, "Sleep", "home"), (8, 9, "Breakfast", "home"), (9, 14, "Coffee", "Forest Café"), (14, 15, "Shop", "supermarket"), (15, 23, "Relax", "home"), (23, 24, "Sleep", "home")) },
            new Person { Id = "ben", Name = "Ben Taylor", Job = "Personal trainer", Model = "Daydream", Age = AgeGroup.YoungAdult, Home = "riverside", Work = "street", Friends = new[] { "jack" },
                Social = 0.65f, Shy = 0.2f, Busy = 0.7f, Distract = 0.2f, Band = 6, Accent = EnglishAccent.Australian, Start = new Vector3(30, 0, 6),
                Greetings = new[] { "Hey!", "How's it going?" }, Talk = new[] { "Just finished a 10k!", "Staying hydrated?" },
                Schedule = J((0, 6, "Sleep", "home"), (6, 7, "Breakfast", "home"), (7, 11, "Wander", "street"), (11, 12, "Coffee", "café"), (12, 16, "Wander", "street"), (16, 22, "Relax", "home"), (22, 24, "Sleep", "home")) },
        };

        private static List<(Person person, NPCProfile profile)> Profiles(List<string> report)
        {
            Directory.CreateDirectory(ProfileFolder);
            var result = new List<(Person, NPCProfile)>();
            foreach (var p in People())
            {
                var path = ProfileFolder + "/" + p.Id + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<NPCProfile>(path);
                var created = profile == null;
                if (created) { profile = ScriptableObject.CreateInstance<NPCProfile>(); AssetDatabase.CreateAsset(profile, path); }
                if (created)                         // never overwrite a profile someone has edited
                {
                    profile.Id = p.Id; profile.DisplayName = p.Name; profile.Occupation = p.Job; profile.Model = p.Model; profile.Age = p.Age;
                    profile.Home = p.Home; profile.Work = p.Work; profile.WalkWith = p.WalkWith;
                    profile.Friends = p.Friends; profile.Family = p.Family; profile.Coworkers = p.Coworkers;
                    profile.Personality = new NPCPersonality { Sociability = p.Social, Shyness = p.Shy, Patience = p.Patience, Busyness = p.Busy, Distraction = p.Distract };
                    profile.ScheduleJson = p.Schedule; profile.EnglishBand = p.Band; profile.Voice = new NPCVoice { Accent = p.Accent };
                    if (p.Greetings != null) profile.Greetings = p.Greetings;
                    if (p.Talk != null) profile.SmallTalk = p.Talk;
                    profile.PreferredActivities = new[] { "Coffee", "Wander" };
                    EditorUtility.SetDirty(profile);
                }
                result.Add((p, profile));
            }
            report.Add("Profiles: " + ProfileFolder + " (" + result.Count + " NPCs; Emma uses the supplied JSON routine verbatim; existing profiles kept)");
            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------------ 9–12 demonstration scene

        private static readonly Color Asphalt = Hex("3B3F44"), Pavement = Hex("B9B4AA"), Brick = Hex("B5654A"), Plaster = Hex("EDE6D8"), Wood = Hex("8A6244"), Glass = new Color(0.75f, 0.88f, 0.95f, 0.35f), Leaf = Hex("5E8A4E");

        private static void NotWalkable(GameObject item) { var m = item.AddComponent<NavMeshModifier>(); m.overrideArea = true; m.area = 1; }
        private static void Walkable(GameObject item) { var m = item.AddComponent<NavMeshModifier>(); m.overrideArea = true; m.area = 0; }

        private static Transform Point(string name, Transform parent, Vector3 world, Vector3 forward)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.position = world;
            t.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0 ? forward : Vector3.forward);
            return t;
        }

        private static ActivitySpot Spot(Transform parent, SpotKind kind, Vector3 world, Vector3 forward, string group = "", float seat = 0.45f)
        {
            var t = Point(kind + (group != "" ? " · " + group : ""), parent, world, forward);
            var spot = t.gameObject.AddComponent<ActivitySpot>();
            spot.Kind = kind; spot.Group = group; spot.SeatHeight = seat;
            return spot;
        }

        private static LivingPlace Place(string id, PlaceKind kind, Vector3 front, Vector2 size, bool indoor, bool visible, params string[] aliases)
        {
            var go = new GameObject("Place · " + id);
            go.transform.position = front;
            var place = go.AddComponent<LivingPlace>();
            place.Id = id; place.Kind = kind; place.Indoor = indoor; place.InteriorVisible = visible; place.Aliases = aliases;
            place.Area = new Bounds(new Vector3(0, 1.5f, size.y / 2), new Vector3(size.x - 0.4f, 3.6f, size.y - 0.4f));
            return place;
        }

        /// A building on the north side: back/side walls, front wall with a door gap (glass above a low wall
        /// when the interior should be visible), floor, roof.
        private static void Building(string name, Vector3 front, Vector2 size, Color wall, bool open, float height = 3.4f)
        {
            var x0 = front.x - size.x / 2; var x1 = front.x + size.x / 2; var z0 = front.z; var z1 = front.z + size.y;
            Wall(name + " wall", new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), height, wall);
            Wall(name + " wall", new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), height, wall);
            Wall(name + " wall", new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), height, wall);
            if (open)
            {
                Wall(name + " front", new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), 0.9f, wall, new Vector2(size.x / 2, 2f));
                Box(name + " window", new Vector3(front.x - size.x / 4 - 0.5f, 1.9f, z0), new Vector3(size.x / 2 - 1.5f, 2f, 0.05f), Glass, false);
                Box(name + " window", new Vector3(front.x + size.x / 4 + 0.5f, 1.9f, z0), new Vector3(size.x / 2 - 1.5f, 2f, 0.05f), Glass, false);
                Box(name + " lintel", new Vector3(front.x, (height + 2.9f) / 2, z0), new Vector3(size.x, height - 2.9f, 0.2f), wall, true);
            }
            else Wall(name + " front", new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), height, wall, new Vector2(size.x / 2, 1.8f));
            Box(name + " floor", new Vector3(front.x, 0.01f, (z0 + z1) / 2), new Vector3(size.x, 0.02f, size.y), Hex("C9B9A0"), false);
            Box(name + " roof", new Vector3(front.x, height + 0.1f, (z0 + z1) / 2), new Vector3(size.x + 0.4f, 0.2f, size.y + 0.4f), Hex("5B514A"), true);
        }

        private static List<string> BuildScene(List<(Person person, NPCProfile profile)> profiles)
        {
            var report = new List<string>();
            Missing.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin("Living neighbourhood");
            Daylight("LivingNeighborhood", "E9ECEF");
            Box("Ground", new Vector3(0, -0.25f, 0), new Vector3(180, 0.5f, 90), Hex("7C8F5A"), true);
            Box("North pavement", new Vector3(0, 0.01f, 6), new Vector3(180, 0.02f, 4), Pavement, false);
            Box("South pavement", new Vector3(0, 0.01f, -7), new Vector3(180, 0.02f, 6), Pavement, false);
            var road = Box("Road", new Vector3(0, -0.095f, 0), new Vector3(180, 0.25f, 8), Asphalt, true);
            NotWalkable(road);
            for (var x = -86f; x < 86; x += 6) Box("Lane marking", new Vector3(x, 0.035f, 0), new Vector3(3, 0.01f, 0.15f), Color.white, false);
            var track = Box("Tram track bed", new Vector3(0, -0.07f, -12), new Vector3(180, 0.25f, 3.2f), Hex("6B635A"), true);
            NotWalkable(track);
            foreach (var z in new[] { -12.7f, -11.3f }) Box("Rail", new Vector3(0, 0.07f, z), new Vector3(180, 0.06f, 0.08f), Hex("9EA3A8"), false);
            Box("Fence", new Vector3(0, 0.6f, -13.8f), new Vector3(180, 1.2f, 0.1f), Hex("4F5B52"), true);

            // Crossings (x = −40 by the west stop, 0 by the school, 25 by the east stop) with lights.
            var crossings = new List<LivingCrossing>();
            foreach (var cx in new[] { -40f, 0f, 25f })
            {
                var zebra = Box("Zebra crossing", new Vector3(cx, -0.08f, 0), new Vector3(3, 0.25f, 8), Asphalt, true);
                Walkable(zebra);
                for (var s = -3.2f; s <= 3.3f; s += 0.8f) Box("Zebra stripe", new Vector3(cx, 0.06f, s), new Vector3(3, 0.01f, 0.45f), Color.white, false);
                var crossing = new GameObject("Crossing · x " + cx).AddComponent<LivingCrossing>();
                crossing.transform.position = new Vector3(cx, 0, 0);
                crossing.Zebra = new Bounds(new Vector3(0, 0.5f, 0), new Vector3(3.2f, 2, 8.2f));
                crossing.CarGreen = 16; crossing.WalkGreen = 9;
                var walk = new List<Renderer>(); var stop = new List<Renderer>();
                foreach (var side in new[] { -1f, 1f })
                {
                    var pole = new Vector3(cx + 1.9f * side, 0, 4.4f * side);
                    Box("Signal pole", pole + Vector3.up * 1.4f, new Vector3(0.12f, 2.8f, 0.12f), Hex("2C2F33"), true);
                    walk.Add(Box("Walk light", pole + new Vector3(0, 2.3f, -0.12f * side), new Vector3(0.22f, 0.2f, 0.06f), Color.black, false).GetComponent<Renderer>());
                    stop.Add(Box("Stop light", pole + new Vector3(0, 2.58f, -0.12f * side), new Vector3(0.22f, 0.2f, 0.06f), Color.black, false).GetComponent<Renderer>());
                }
                crossing.WalkLights = walk.ToArray(); crossing.StopLights = stop.ToArray();
                crossing.transform.SetParent(null);
                crossings.Add(crossing);
            }

            // Traffic: drive on the left (eastbound north lane z = +2, westbound south lane z = −2).
            var path = new List<Vector3>(); var stops = new List<int>(); var stopCrossings = new List<LivingCrossing>();
            path.Add(new Vector3(-84, 0, 2));
            foreach (var c in crossings) { stops.Add(path.Count); stopCrossings.Add(c); path.Add(new Vector3(c.transform.position.x - 3.4f, 0, 2)); }
            path.Add(new Vector3(84, 0, 2)); path.Add(new Vector3(87, 0, 0)); path.Add(new Vector3(84, 0, -2));
            foreach (var c in Enumerable.Reverse(crossings)) { stops.Add(path.Count); stopCrossings.Add(c); path.Add(new Vector3(c.transform.position.x + 3.4f, 0, -2)); }
            path.Add(new Vector3(-84, 0, -2)); path.Add(new Vector3(-87, 0, 0));
            var carStarts = new[] { new Vector3(-70, 0, 2), new Vector3(10, 0, 2), new Vector3(50, 0, -2), new Vector3(-20, 0, -2) };
            for (var i = 0; i < carStarts.Length; i++)
            {
                var car = Car(i % 2 == 0 ? "CityTaxi" : "FreshMartVan", carStarts[i], carStarts[i].z > 0 ? Vector3.right : Vector3.left);
                var driver = car.AddComponent<LivingCar>();
                driver.Path = path.ToArray(); driver.StopPoints = stops.ToArray(); driver.StopCrossings = stopCrossings.ToArray();
                driver.Cruise = 6.5f + i * 0.4f;
            }

            // Tram: track on the south side, stops (platform = the pavement) at x = −40 and x = 25.
            var stopWest = Place("tram_west", PlaceKind.TramStop, new Vector3(-40, 0, -10), new Vector2(8, 5), false, true, "west stop");
            var stopEast = Place("tram_east", PlaceKind.TramStop, new Vector3(25, 0, -10), new Vector2(8, 5), false, true, "east stop");
            foreach (var stop in new[] { stopWest, stopEast })
            {
                stop.Shelter = true;
                stop.Area = new Bounds(new Vector3(0, 1.5f, 2.5f), new Vector3(8, 3.6f, 5));
                var p = stop.transform.position;
                stop.Entrance = Point("Entrance", stop.transform, p + new Vector3(0, 0, 3.5f), Vector3.back);
                Box("Shelter roof", p + new Vector3(0, 2.6f, 1), new Vector3(5, 0.1f, 2), Hex("2E5E45"), false);
                Box("Shelter post", p + new Vector3(-2.4f, 1.3f, 1.8f), new Vector3(0.1f, 2.6f, 0.1f), Hex("2C2F33"), true);
                Box("Shelter post", p + new Vector3(2.4f, 1.3f, 1.8f), new Vector3(0.1f, 2.6f, 0.1f), Hex("2C2F33"), true);
                Board("TRAM · " + (stop == stopWest ? "Riverside" : "Market St"), p + new Vector3(0, 2.2f, 1.9f), 180, new Vector2(2.6f, 0.4f), Hex("2E5E45"), Color.white);
                for (var i = -1; i <= 1; i++) Spot(stop.transform, SpotKind.Wait, p + new Vector3(i * 1.4f, 0, 0.9f), Vector3.back);
                Box("Shelter bench", p + new Vector3(0, 0.23f, 1.75f), new Vector3(2.4f, 0.45f, 0.4f), Wood, false);
                Spot(stop.transform, SpotKind.Bench, p + new Vector3(-0.6f, 0, 1.4f), Vector3.back, "", 0.45f);
                Spot(stop.transform, SpotKind.Bench, p + new Vector3(0.6f, 0, 1.4f), Vector3.back, "", 0.45f);
            }
            var tramGo = Box("Tram", new Vector3(-40, 1.5f, -12), new Vector3(12, 2.9f, 2.6f), Hex("3A7D5C"), true);
            tramGo.isStatic = false;
            var tramRoot = new GameObject("Tram (living)");
            tramRoot.transform.position = new Vector3(-40, 0, -12);
            tramGo.transform.SetParent(tramRoot.transform, true);
            NotWalkable(tramGo);
            for (var i = -1; i <= 1; i += 2) Box("Tram door", new Vector3(-40 + i * 2.6f, 1.2f, -10.69f), new Vector3(1.1f, 2.1f, 0.02f), Hex("1C2B24"), false, tramRoot.transform).isStatic = false;
            for (var x = -5f; x <= 5f; x += 2.5f) Box("Tram window", new Vector3(-40 + x, 2, -10.69f), new Vector3(1.6f, 0.9f, 0.02f), Glass, false, tramRoot.transform).isStatic = false;
            var tram = tramRoot.AddComponent<LivingTram>();
            tram.Stops = new[] { Point("Tram stop · west", null, new Vector3(-40, 0, -12), Vector3.right), Point("Tram stop · east", null, new Vector3(25, 0, -12), Vector3.right) };
            tram.StopPlaceIds = new[] { "tram_west", "tram_east" };
            tram.Doors = new[] { Point("Door", tramRoot.transform, new Vector3(-42.6f, 0.15f, -10.9f), Vector3.forward), Point("Door", tramRoot.transform, new Vector3(-37.4f, 0.15f, -10.9f), Vector3.forward) };
            for (var i = 0; i < 8; i++) Spot(tramRoot.transform, SpotKind.TramSeat, new Vector3(-44.5f + (i % 4) * 2.6f + (i > 3 ? 1.3f : 0), 0.15f, i > 3 ? -12.6f : -11.6f), i > 3 ? Vector3.forward : Vector3.back, "", 0.45f);
            foreach (var r in tramRoot.GetComponentsInChildren<Transform>()) r.gameObject.isStatic = false;

            // North side buildings.
            var riverside = Place("riverside", PlaceKind.Home, new Vector3(-58, 0, 10), new Vector2(16, 12), true, false, "riverside apartments", "apartments", "hotel");
            Building("Riverside Apartments", new Vector3(-58, 0, 10), new Vector2(16, 12), Brick, false, 7);
            Board("RIVERSIDE APARTMENTS", new Vector3(-58, 4.2f, 9.85f), 0, new Vector2(6, 0.6f), Hex("2C2F33"), Color.white);
            riverside.Entrance = Point("Entrance", riverside.transform, new Vector3(-58, 0, 8.6f), Vector3.back);
            riverside.Inside = Point("Inside", riverside.transform, new Vector3(-58, 0, 11.5f), Vector3.forward);
            for (var i = 0; i < 8; i++) Spot(riverside.transform, SpotKind.Bed, new Vector3(-64 + i * 1.7f, 0, 20.5f), Vector3.back, "", 0.5f);
            for (var i = 0; i < 4; i++) Spot(riverside.transform, SpotKind.Sofa, new Vector3(-63 + i * 1.2f, 0, 15), Vector3.forward);
            for (var i = 0; i < 4; i++) Spot(riverside.transform, SpotKind.DiningSeat, new Vector3(-54 + i * 1.2f, 0, 15), Vector3.forward);
            Spot(riverside.transform, SpotKind.Stove, new Vector3(-52, 0, 20.4f), Vector3.forward);
            Spot(riverside.transform, SpotKind.Sink, new Vector3(-53.5f, 0, 20.4f), Vector3.forward);

            var house = Place("house_tan", PlaceKind.Home, new Vector3(-36, 0, 10), new Vector2(12, 10), true, true, "tan house", "family house");
            Building("Tan family house", new Vector3(-36, 0, 10), new Vector2(12, 10), Plaster, true);
            house.Entrance = Point("Entrance", house.transform, new Vector3(-36, 0, 8.6f), Vector3.back);
            house.Inside = Point("Inside", house.transform, new Vector3(-36, 0, 11.4f), Vector3.forward);
            Box("Kitchen bench", new Vector3(-40, 0.45f, 19.3f), new Vector3(3.4f, 0.9f, 0.7f), Plaster, true);
            Spot(house.transform, SpotKind.Stove, new Vector3(-40.6f, 0, 18.5f), Vector3.forward);
            Spot(house.transform, SpotKind.Sink, new Vector3(-39f, 0, 18.5f), Vector3.forward);
            Box("Dining table", new Vector3(-39.5f, 0.38f, 15), new Vector3(1.6f, 0.75f, 0.9f), Wood, false);
            Spot(house.transform, SpotKind.DiningSeat, new Vector3(-40.1f, 0, 14.05f), Vector3.forward, "dining");
            Spot(house.transform, SpotKind.DiningSeat, new Vector3(-38.9f, 0, 14.05f), Vector3.forward, "dining");
            Spot(house.transform, SpotKind.DiningSeat, new Vector3(-39.5f, 0, 15.95f), Vector3.back, "dining");
            Box("Sofa", new Vector3(-32.5f, 0.25f, 14.2f), new Vector3(2.6f, 0.5f, 0.9f), Hex("5B7B9A"), false);
            Box("TV", new Vector3(-32.5f, 1.1f, 19.6f), new Vector3(1.6f, 0.9f, 0.08f), Hex("111111"), false);
            Spot(house.transform, SpotKind.Sofa, new Vector3(-33.1f, 0, 14.2f), Vector3.forward, "sofa");
            Spot(house.transform, SpotKind.Sofa, new Vector3(-31.9f, 0, 14.2f), Vector3.forward, "sofa");
            Box("Bed", new Vector3(-31.5f, 0.25f, 18.6f), new Vector3(1.6f, 0.5f, 2.1f), Hex("DCD3C4"), false);
            Box("Bed", new Vector3(-34.3f, 0.25f, 18.6f), new Vector3(1.0f, 0.5f, 2.0f), Hex("C7B9D8"), false);
            Spot(house.transform, SpotKind.Bed, new Vector3(-31.9f, 0, 17.8f), Vector3.back, "", 0.5f);
            Spot(house.transform, SpotKind.Bed, new Vector3(-31.1f, 0, 17.8f), Vector3.back, "", 0.5f);
            Spot(house.transform, SpotKind.Bed, new Vector3(-34.3f, 0, 17.8f), Vector3.back, "", 0.5f);

            var school = Place("school", PlaceKind.School, new Vector3(-14, 0, 10), new Vector2(16, 12), true, false, "high school", "学校");
            Building("Riverside High School", new Vector3(-14, 0, 10), new Vector2(16, 12), Hex("A9B6C2"), false, 7);
            Board("RIVERSIDE HIGH SCHOOL", new Vector3(-14, 4.5f, 9.85f), 0, new Vector2(7, 0.7f), Hex("1D4F91"), Color.white);
            school.Entrance = Point("Entrance", school.transform, new Vector3(-14, 0, 8.6f), Vector3.back);
            school.Inside = Point("Inside", school.transform, new Vector3(-14, 0, 11.5f), Vector3.forward);
            for (var i = 0; i < 10; i++) Spot(school.transform, SpotKind.Desk, new Vector3(-20 + (i % 5) * 2.8f, 0, 14 + (i / 5) * 3), Vector3.forward);

            // Forest Café: counter, queue, espresso machine, pickup, tables in pairs, staff room.
            var cafe = Place("forest_cafe", PlaceKind.Cafe, new Vector3(10, 0, 10), new Vector2(16, 12), true, true, "Forest Café", "café", "cafe", "咖啡店");
            Building("Forest Café", new Vector3(10, 0, 10), new Vector2(16, 12), Hex("6E8B5E"), true);
            Board("FOREST CAFÉ", new Vector3(10, 3.1f, 9.85f), 0, new Vector2(4, 0.5f), Hex("2E5E45"), Hex("F7F3EA"));
            cafe.Entrance = Point("Entrance", cafe.transform, new Vector3(10, 0, 8.6f), Vector3.back);
            cafe.Inside = Point("Inside", cafe.transform, new Vector3(10, 0, 11.6f), Vector3.forward);
            Box("Counter", new Vector3(8, 0.52f, 18), new Vector3(8, 1.05f, 0.8f), Wood, true);
            Box("Back counter", new Vector3(9, 0.47f, 21.2f), new Vector3(8, 0.95f, 0.7f), Wood, true);
            var machineArt = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/kitchen/EspressoMachine.glb");
            if (machineArt != null) { var m = (GameObject)PrefabUtility.InstantiatePrefab(machineArt); m.transform.position = new Vector3(11, 0.95f, 21.2f); m.transform.rotation = Quaternion.Euler(0, 180, 0); }
            else Box("Espresso machine", new Vector3(11, 1.2f, 21.2f), new Vector3(0.7f, 0.5f, 0.5f), Hex("B8BCC0"), false);
            var service = new GameObject("Service · café till").AddComponent<ServicePoint>();
            service.transform.SetParent(cafe.transform, true);
            service.transform.position = new Vector3(6, 0, 18);
            service.ServerPoint = Point("Server", service.transform, new Vector3(6, 0, 18.9f), Vector3.back);
            service.PreparePoint = Point("Espresso machine", service.transform, new Vector3(11, 0, 20.3f), Vector3.forward);
            service.PickupPoint = Point("Pickup", service.transform, new Vector3(10.5f, 0, 17.5f), Vector3.back);
            service.QueuePoints = Enumerable.Range(0, 5).Select(i => Point("Queue " + (i + 1), service.transform, new Vector3(6, 0, 16.9f - i * 0.85f), Vector3.forward)).ToArray();
            cafe.Service = service;
            Spot(cafe.transform, SpotKind.Counter, new Vector3(8.5f, 0, 18.9f), Vector3.back);
            Spot(cafe.transform, SpotKind.Sink, new Vector3(6.5f, 0, 20.4f), Vector3.forward);
            var tables = new[] { new Vector3(4, 0, 13.4f), new Vector3(8, 0, 13.4f), new Vector3(14, 0, 13.4f), new Vector3(14, 0, 16.6f) };
            for (var i = 0; i < tables.Length; i++)
            {
                Box("Café table", tables[i] + Vector3.up * 0.37f, new Vector3(0.8f, 0.74f, 0.8f), Wood, false);
                Box("Chair", tables[i] + new Vector3(-0.8f, 0.23f, 0), new Vector3(0.45f, 0.45f, 0.45f), Hex("4A3C30"), false);
                Box("Chair", tables[i] + new Vector3(0.8f, 0.23f, 0), new Vector3(0.45f, 0.45f, 0.45f), Hex("4A3C30"), false);
                Spot(cafe.transform, SpotKind.Seat, tables[i] + new Vector3(-0.8f, 0, 0), Vector3.right, "table" + i);
                Spot(cafe.transform, SpotKind.Seat, tables[i] + new Vector3(0.8f, 0, 0), Vector3.left, "table" + i);
                Spot(cafe.transform, SpotKind.Table, tables[i] + new Vector3(0, 0, -0.75f), Vector3.forward);
            }
            Wall("Staff room wall", new Vector3(14, 0, 18.2f), new Vector3(17.9f, 0, 18.2f), 3.4f, Hex("6E8B5E"), new Vector2(1.2f, 1f));
            var staff = Place("staff_room", PlaceKind.BreakRoom, new Vector3(16, 0, 18.4f), new Vector2(3.8f, 3.4f), true, true, "staff room", "员工休息区", "break room");
            staff.Entrance = Point("Entrance", staff.transform, new Vector3(15.2f, 0, 17.5f), Vector3.back);
            staff.Inside = Point("Inside", staff.transform, new Vector3(15.2f, 0, 19), Vector3.forward);
            Spot(staff.transform, SpotKind.Seat, new Vector3(16.4f, 0, 20.6f), Vector3.back, "staff");
            Spot(staff.transform, SpotKind.Seat, new Vector3(17.2f, 0, 19.6f), Vector3.left, "staff");

            // Supermarket: shelves, checkout with a cashier.
            var market = Place("supermarket", PlaceKind.Supermarket, new Vector3(36, 0, 10), new Vector2(16, 12), true, true, "FreshMart", "超市", "grocery");
            Building("FreshMart", new Vector3(36, 0, 10), new Vector2(16, 12), Hex("D9D4C7"), true);
            Board("FRESHMART", new Vector3(36, 3.1f, 9.85f), 0, new Vector2(4, 0.5f), Hex("C0392B"), Color.white);
            market.Entrance = Point("Entrance", market.transform, new Vector3(36, 0, 8.6f), Vector3.back);
            market.Inside = Point("Inside", market.transform, new Vector3(36, 0, 11.6f), Vector3.forward);
            for (var row = 0; row < 3; row++)
            {
                var x = 33 + row * 4;
                Box("Shelf", new Vector3(x, 0.9f, 17.5f), new Vector3(0.8f, 1.8f, 5), Hex("E8E2D4"), true);
                for (var k = 0; k < 2; k++)
                {
                    Spot(market.transform, SpotKind.Shelf, new Vector3(x - 0.85f, 0, 16 + k * 3), Vector3.right);
                    Spot(market.transform, SpotKind.Shelf, new Vector3(x + 0.85f, 0, 16 + k * 3), Vector3.left);
                }
            }
            Box("Checkout", new Vector3(30.5f, 0.45f, 13.5f), new Vector3(0.8f, 0.9f, 2.4f), Hex("8E8E8E"), true);
            var till = new GameObject("Service · checkout").AddComponent<ServicePoint>();
            till.transform.SetParent(market.transform, true);
            till.transform.position = new Vector3(30.5f, 0, 13.5f);
            till.NeedsPreparation = false; till.ItemsCsv = "groceries";
            till.ServerPoint = Point("Cashier", till.transform, new Vector3(29.5f, 0, 13.5f), Vector3.right);
            till.QueuePoints = Enumerable.Range(0, 4).Select(i => Point("Queue " + (i + 1), till.transform, new Vector3(31.5f, 0, 13.5f + i * 0.85f), Vector3.left)).ToArray();
            market.Service = till;

            // Street: purposeful waypoints and benches.
            var street = Place("street", PlaceKind.Street, new Vector3(0, 0, -10), new Vector2(170, 20), false, true, "城市", "city", "street", "town");
            street.Area = new Bounds(new Vector3(0, 1.5f, 10), new Vector3(170, 3.6f, 18));
            var waypoints = new List<Transform>();
            for (var x = -66f; x <= 66; x += 12) { waypoints.Add(Point("Waypoint", street.transform, new Vector3(x, 0, 6.2f), Vector3.right)); waypoints.Add(Point("Waypoint", street.transform, new Vector3(x + 6, 0, -6.5f), Vector3.right)); }
            street.Waypoints = waypoints.ToArray();
            foreach (var bx in new[] { -24f, -2f, 22f, 48f })
            {
                Box("Bench", new Vector3(bx, 0.23f, 7.6f), new Vector3(1.8f, 0.45f, 0.5f), Wood, false);
                Spot(street.transform, SpotKind.Bench, new Vector3(bx - 0.45f, 0, 7.3f), Vector3.back, "bench" + bx, 0.45f);
                Spot(street.transform, SpotKind.Bench, new Vector3(bx + 0.45f, 0, 7.3f), Vector3.back, "bench" + bx, 0.45f);
                Spot(street.transform, SpotKind.Viewpoint, new Vector3(bx + 3, 0, 6), Vector3.back);
            }
            for (var x = -80f; x <= 80; x += 16) { Shape("Street tree", PrimitiveType.Sphere, new Vector3(x + 8, 3.2f, 7.4f), new Vector3(2.6f, 2.6f, 2.6f), Leaf, false); Box("Trunk", new Vector3(x + 8, 1.2f, 7.4f), new Vector3(0.25f, 2.4f, 0.25f), Hex("5A4636"), true); }
            for (var x = -72f; x <= 72; x += 24) { Lamp(new Vector3(x, 4.5f, 4.6f), 10, 1.4f); Lamp(new Vector3(x + 12, 4.5f, -4.6f), 10, 1.4f); }
            Label("Living neighbourhood · F6 NPC inspector · F7 crowd overlay", new Vector3(4, 2.2f, -9.5f), 180, 0.25f, Color.white);

            // People.
            var (player, camera) = Player(new Vector3(4, 0.05f, -6.5f), 0);
            camera.farClipPlane = 400;
            var systemsGo = new GameObject("Systems");
            var flow = systemsGo.AddComponent<NeighborhoodFlow>();
            var npcs = new List<(GameObject go, NPCProfile profile, Vector3 start)>();
            foreach (var (person, profile) in profiles)
            {
                var npc = Npc(profile.Id, profile.DisplayName, profile.Occupation, profile.Model, person.Start, 0);
                npcs.Add((npc.gameObject, profile, person.Start));
            }
            Finish(player, camera, flow, "Living neighbourhood", "LivingNeighborhood");
            foreach (var (go, profile, start) in npcs)
            {
                var legacy = go.GetComponent<NPCController>();
                legacy.enabled = false;                                   // LivingNPC drives this person; NPCController is kept for dialogue hooks
                var agent = go.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = true;
                go.AddComponent<NPCNavigationController>();
                go.AddComponent<NPCAnimationController>();
                var living = go.AddComponent<LivingNPC>();
                living.Profile = profile;
            }
            var world = new GameObject("Living world");
            world.AddComponent<NPCLifeSimulationManager>();
            world.AddComponent<NPCEventDirector>();
            world.AddComponent<NPCDebugInspector>();
            var sky = SkyInstaller.Install();
            if (sky != null) { sky.SetTime(8.1f); EditorUtility.SetDirty(sky); }
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture = true;
            LifeBuild.Save(scene, Scene);
            report.Add("Scene: " + Scene + " — family house, Riverside Apartments, school entrance, Forest Café (+ staff room), FreshMart, 3 signalled crossings with traffic, tram with 2 stops, 14 NPCs; starts at 08:06");
            if (Missing.Count > 0) report.AddRange(Missing.Select(m => "MISSING: " + m));
            return report;
        }

        private static GameObject Car(string file, Vector3 position, Vector3 forward)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/city/" + file + ".glb");
            var root = new GameObject(file);
            root.transform.position = position;
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.transform.SetParent(root.transform, false);
                var lights = model.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterials.Any(m => m != null && m.name.StartsWith("Headlight"))).ToArray();
                if (lights.Length > 0 && lights.Average(r => root.transform.InverseTransformPoint(r.bounds.center).z) < 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var b = AirportKit.Measure(model);
                model.transform.position += new Vector3(position.x - b.center.x, position.y - b.min.y, position.z - b.center.z);
                b = AirportKit.Measure(model);
                var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(b.center); box.size = b.size;
            }
            else
            {
                Missing.Add("Assets/Art/city/" + file + ".glb — box car used");
                Box("Car body", position + Vector3.up * 0.75f, new Vector3(1.8f, 1.4f, 4.3f), Hex("E8C547"), false, root.transform);
                var box = root.AddComponent<BoxCollider>(); box.center = Vector3.up * 0.75f; box.size = new Vector3(1.8f, 1.4f, 4.3f);
            }
            root.transform.rotation = Quaternion.LookRotation(forward);
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            root.AddComponent<Rigidbody>().isKinematic = true;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.carving = false;
            var collider = root.GetComponent<BoxCollider>();
            obstacle.center = collider.center; obstacle.size = collider.size;
            return root;
        }

        // ------------------------------------------------------------------ other scenes

        [MenuItem("ContextSpeak/NPC/Add Living NPC Systems To Open Scene")]
        public static void AddToOpenScene()
        {
            var existing = Object.FindFirstObjectByType<NPCLifeSimulationManager>();
            if (existing != null) { Debug.Log("Living NPC systems already present."); return; }
            var world = new GameObject("Living world");
            Undo.RegisterCreatedObjectUndo(world, "Add Living NPC systems");
            world.AddComponent<NPCLifeSimulationManager>();
            world.AddComponent<NPCEventDirector>();
            world.AddComponent<NPCDebugInspector>();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Added the Living NPC managers. Add LivingPlace / ActivitySpot markers and LivingNPC (with an NPCProfile) to people in this scene; see LIVING_NPC.md.");
        }
    }
}
