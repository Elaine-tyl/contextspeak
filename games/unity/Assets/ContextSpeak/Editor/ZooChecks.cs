using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Checks > Zoo: animal needs and behaviour choice, habitat containment, the quest
    /// order (including skipped later-phase objectives), tickets, the conversations and the dialogue
    /// repeat gate (EditMode, no save touched), plus the WildlifeZoo scene wiring.
    /// Not covered here (needs a manual Play run): walking and animation in motion, ticket payment UI,
    /// café ordering, photos, NPC schedules, frame rate.
    public static class ZooChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Zoo")]
        public static void Menu()
        {
            var errors = Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak zoo checks passed.");
            else Debug.LogError("ContextSpeak zoo checks failed:\n- " + string.Join("\n- ", errors));
        }

        public static string[] Run()
        {
            var errors = new List<string>();
            void Check(bool value, string message) { if (!value) errors.Add(message); }
            void Guard(string name, Action body)
            {
                try { body(); } catch (Exception error) { errors.Add(name + " threw " + error.GetType().Name + ": " + error.Message); }
            }
            var persist = SaveManager.Persist;
            SaveManager.Persist = false;
            try
            {
                Guard("Needs", () => Needs(Check));
                Guard("Behaviour", () => Behaviour(Check));
                Guard("Habitat", () => Habitat(Check));
                Guard("Quest", () => Quest(Check));
                Guard("Dialogue", () => Dialogue(Check));
            }
            finally { SaveManager.UseInMemory(null); SaveManager.Persist = persist; }
            Guard("Zoo scene", () => Scene(Check));
            return errors.ToArray();
        }

        private static void Needs(Action<bool, string> check)
        {
            var elephant = AnimalCatalog.Find("elephant");
            var needs = new AnimalNeeds();
            needs.Tick(5, elephant, AnimalState.Walking, true, false);
            check(needs.Thirst > 0.3f && needs.Hunger > 0.3f, "Needs grow while an animal walks.");
            var thirst = needs.Thirst;
            needs.Tick(0.2f, elephant, AnimalState.Drinking, true, false);
            check(needs.Thirst < thirst, "Drinking reduces thirst.");
            needs.Tick(60, elephant, AnimalState.Walking, false, true);
            check(needs.Hunger <= 1 && needs.Thirst <= 1 && needs.Stress <= 1 && needs.Social <= 1, "Needs stay within 0–1.");
            check(AnimalCatalog.All.Select(species => species.Id).Distinct().Count() == AnimalCatalog.All.Length, "Species ids are unique.");
            foreach (var species in AnimalCatalog.All)
                check(species.Fact != "" && species.Describe != "" && species.Vocabulary.Length >= 3, species.Name + " has a fact, a description and vocabulary.");
        }

        private static void Behaviour(Action<bool, string> check)
        {
            var zebra = AnimalCatalog.Find("zebra");
            var rhino = AnimalCatalog.Find("rhino");
            (AnimalState state, float seconds) Choose(AnimalSpecies species, AnimalNeeds needs, float hour, bool inside = true, bool herdFar = false, float roll = 0.5f) =>
                AnimalBehaviorStateMachine.Choose(species, needs, hour, inside, herdFar, roll);
            check(Choose(zebra, new AnimalNeeds { Thirst = 0.9f }, 10).state == AnimalState.Drinking, "A thirsty animal goes to drink.");
            check(Choose(zebra, new AnimalNeeds { Hunger = 0.8f }, 10).state == AnimalState.Eating, "A hungry animal eats.");
            check(Choose(zebra, new AnimalNeeds(), 10, false).state == AnimalState.ReturningToHabitat, "An animal outside its habitat returns.");
            check(Choose(zebra, new AnimalNeeds(), 23).state == AnimalState.Sleeping, "Animals sleep at night.");
            check(Choose(zebra, new AnimalNeeds(), 10, true, true).state == AnimalState.Socializing, "A herd animal rejoins its herd.");
            check(Choose(rhino, new AnimalNeeds { Stress = 0.9f }, 10).state != AnimalState.Running, "Species only use their own behaviours (a rhino does not run).");
            var calm = 0;
            for (var i = 0; i < 100; i++)
            {
                var state = Choose(rhino, new AnimalNeeds(), 13, true, false, i / 100f).state;
                if (state != AnimalState.Walking && state != AnimalState.Running && state != AnimalState.Playing) calm++;
                check(rhino.Can(state), "Rhino behaviour " + state + " is in its set.");
            }
            check(calm >= 50, "Animals are mostly calm in the midday heat (" + calm + "% calm).");
        }

        private static void Habitat(Action<bool, string> check)
        {
            var item = new GameObject("Habitat check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var habitat = item.AddComponent<AnimalHabitat>();
                habitat.Radii = new Vector2(40, 25);
                habitat.Margin = 4;
                habitat.Avoid = new[] { new Vector4(5, 5, 6, 0) };
                var random = new System.Random(3);
                var worst = 0f;
                var inPond = 0;
                for (var i = 0; i < 400; i++)
                {
                    var point = habitat.Clamp(new Vector3((float)random.NextDouble() * 200 - 100, 0, (float)random.NextDouble() * 200 - 100));
                    worst = Mathf.Max(worst, AnimalHabitat.Ellipse(Vector2.zero, new Vector2(36, 21), new Vector2(point.x, point.z)));
                    if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(5, 5)) < 5.9f) inPond++;
                }
                check(worst <= 1.2f, "Clamped points stay inside the fence (worst " + worst.ToString("0.00") + ").");
                check(inPond == 0, "Clamped points stay out of the pond.");
                check(habitat.Contains(Vector3.zero, 4) && !habitat.Contains(new Vector3(39, 0, 0), 4), "Contains respects the margin.");
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }

        private static void Quest(Action<bool, string> check)
        {
            SaveManager.UseInMemory(new LifeProgress());
            var progress = ZooQuest.Progress;
            check(!ZooQuest.Advance(ZooStage.MetGuide) && progress.stage == 0, "Objectives cannot be skipped by the player.");
            check(ZooQuest.Advance(ZooStage.AtZoo) && ZooQuest.Advance(ZooStage.BoughtTicket) && ZooQuest.Advance(ZooStage.OpenedMap) && ZooQuest.Advance(ZooStage.MetGuide), "Objectives 1–4 advance in order.");
            check(ZooQuest.Advance(ZooStage.AtElephants) && ZooQuest.Advance(ZooStage.DescribedElephant) && ZooQuest.Advance(ZooStage.AtGiraffes) && ZooQuest.Advance(ZooStage.ComparedAnimals), "Objectives 5–8 advance in order.");
            check(progress.stage == (int)ZooStage.VisitedAviary && progress.skipped.Count == 2, "Penguins and aviary (not built yet) are skipped and recorded.");
            check(ZooQuest.Advance(ZooStage.OrderedDrink) && progress.stage == (int)ZooStage.RodeTram, "After the café the tram tour (not built yet) is skipped.");
            var xp = SaveManager.Data.xp;
            check(ZooQuest.Advance(ZooStage.ConservationTalk) && progress.Stage == ZooStage.Rewarded, "The conservation task completes the quest.");
            check(SaveManager.Data.xp == xp + 250 && InventoryManager.Count("zoo-badge") == 1, "Completion gives 250 XP and the badge.");
            check(SaveManager.Data.quests.Any(quest => quest.id == ZooQuest.Id && quest.done), "ZOO_VISIT_001 is marked done in the save.");
            var json = JsonUtility.ToJson(SaveManager.Data);
            var loaded = JsonUtility.FromJson<LifeProgress>(json);
            check(loaded.zoo != null && loaded.zoo.stage == progress.stage && loaded.zoo.skipped.Count == 3, "Zoo progress survives save and load.");
            ZooQuest.CompleteOptional("ZOO_QUIZ", 30);
            ZooQuest.CompleteOptional("ZOO_QUIZ", 30);
            check(SaveManager.Data.xp == xp + 280, "Optional quests pay out once.");
            check(ZooTickets.All.Length == 3 && ZooTickets.Find("family").Cents > ZooTickets.Find("standard").Cents, "Three ticket types with sensible prices.");
            check(ZooQuiz.Questions.All(question => question.Correct >= 0 && question.Correct < question.Answers.Length), "Quiz answers are valid.");
        }

        private static void Dialogue(Action<bool, string> check)
        {
            var tokens = new Dictionary<string, string> { { "ticket", "Standard ticket" }, { "price", "$24.00" }, { "animal", "elephant" } };
            foreach (var name in ZooScenarios.Names)
            {
                var scenario = ZooScenarios.Create(name, tokens);
                check(scenario != null && scenario.Steps != null && scenario.Steps.Count > 0, "Scenario '" + name + "' builds.");
                if (scenario == null) continue;
                foreach (var step in scenario.Steps)
                {
                    check(step.simple != "" && step.standard != "" && step.advanced != "" && step.expert != "", name + "/" + step.id + " has lines for every band group.");
                    check(DialogueStateMachine.Conveys(step, step.model), name + "/" + step.id + ": the model answer is accepted.");
                    check(!DialogueStateMachine.Conveys(step, "banana purple"), name + "/" + step.id + ": an unrelated answer is rejected.");
                }
                UnityEngine.Object.DestroyImmediate(scenario);
            }
            // Equivalent wording is accepted, and the repeat gate holds in learning mode.
            var keeper = ZooScenarios.Create("Giraffe Keeper", tokens);
            check(DialogueStateMachine.Conveys(keeper.Steps[0], "they mostly feed on the green bits of acacia trees"), "A differently worded correct answer is accepted.");
            var machine = new DialogueStateMachine();
            machine.Start(keeper, IeltsBand.Band5, true);
            machine.Answer("giraffe eat leaf from tree", false);
            if (machine.Phase == DialoguePhase.AwaitingRepeat)
            {
                check(!machine.Repeat("I like pizza") && machine.Index == 0, "A wrong repeat does not advance the dialogue.");
                check(!machine.Skip(), "The repeat cannot be skipped in learning mode.");
                check(machine.Repeat(machine.Target) && machine.Index == 1, "Repeating the corrected sentence unlocks the next line.");
            }
            else check(machine.Index == 1, "An answer needing no correction moves straight on.");
            UnityEngine.Object.DestroyImmediate(keeper);
        }

        private static void Scene(Action<bool, string> check)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ZooBuilder.ScenePath) == null) { check(false, "WildlifeZoo.unity not built yet (ContextSpeak > Build Wildlife Zoo)."); return; }
            var active = EditorSceneManager.GetActiveScene();
            var opened = active.path == ZooBuilder.ScenePath;
            var scene = opened ? active : EditorSceneManager.OpenScene(ZooBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
                var flow = All<ZooFlow>().FirstOrDefault();
                check(flow != null, "Zoo systems object has a ZooFlow.");
                if (flow != null) check(flow.Spawn != null && flow.Barrier != null && flow.Savannah != null && flow.Farm != null, "ZooFlow is wired (spawn, gate barrier, habitats).");
                var animals = All<AnimalController>();
                check(animals.Length >= 15, "At least 15 animals placed (" + animals.Length + ").");
                foreach (var animal in animals)
                {
                    check(animal.Habitat != null && AnimalCatalog.Find(animal.SpeciesId) != null, animal.name + " has a habitat and a known species.");
                    if (animal.Habitat != null) check(animal.Habitat.Contains(animal.transform.position, 0), animal.name + " starts inside its habitat.");
                    check(animal.GetComponent<AnimalAnimator>() != null && animal.GetComponent<AnimalMovement>() != null, animal.name + " has movement and animation.");
                    var rigged = animal.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Leg_") || t.name == "Neck" || t.name == "Head");
                    var species = AnimalCatalog.Find(animal.SpeciesId);
                    if (species != null && species.Id != "chicken") check(rigged >= 6, animal.name + " has animation bones (" + rigged + ").");
                }
                foreach (var id in new[] { "elephant", "giraffe", "zebra", "antelope" })
                    check(animals.Any(animal => animal.SpeciesId == id), "Savannah has " + id + "s.");
                var keys = All<NPCController>().Select(npc => npc.Key).ToArray();
                foreach (var key in new[] { "ticket", "guide", "keeper-elephant", "keeper-giraffe", "barista" })
                    check(keys.Contains(key), "NPC '" + key + "' is in the scene.");
                check(All<AnimalInteraction>().Length >= 5, "Animal information boards placed.");
                check(All<AirportSpot>().Any(spot => spot.Action == "gate"), "Ticket gate spot placed.");
                check(All<Terrain>().Length == 1, "One terrain.");
                check(All<PortalDoor>().Any(portal => portal.TargetScene == "MetroCity"), "Portal back to Metro City.");
            }
            finally { if (!opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
