using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    public static class VoiceGroundingChecks
    {
        [UnityEditor.MenuItem("ContextSpeak/Checks/Voice controls and grounded props")]
        public static void Verify()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(scene => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty))
                throw new Exception("Save the current scene before running isolated checks.");
            var errors = new System.Collections.Generic.List<string>();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var floor = Cube("Ground", new Vector3(0, 1.9f, 0), new Vector3(30, .2f, 30));
                var table = new GameObject("Dining table");
                Cube("Table top", new Vector3(0, 5, 0), new Vector3(1, .2f, 1)).transform.SetParent(table.transform, true);
                Cube("Table legs", new Vector3(0, 4.2f, 0), new Vector3(.6f, 1.4f, .6f)).transform.SetParent(table.transform, true);
                var tree = new GameObject("Pine tree");
                tree.AddComponent<LODGroup>();
                var trunk = Cube("Trunk", new Vector3(4, 7, 0), new Vector3(.5f, 2, .5f));
                trunk.transform.SetParent(tree.transform, true);
                var npc = new GameObject("NPC");
                npc.AddComponent<Animator>();
                var body = Cube("Body", new Vector3(-4, 5, 0), new Vector3(.5f, 2, .5f));
                body.transform.SetParent(npc.transform, true);
                var lamp = Cube("Hanging lamp", new Vector3(0, 8, 4), Vector3.one);
                var cup = Cube("Cup", new Vector3(0, 5.6f, 0), Vector3.one * .2f);
                Cube("Table support", new Vector3(8, 3, 0), new Vector3(2, 2, 2));
                var plant = Cube("Potted plant", new Vector3(8, 6, 0), Vector3.one);
                plant.AddComponent<LODGroup>();
                var decoration = new GameObject("TerracottaJug");
                decoration.AddComponent<LODGroup>();
                var jug = Cube("Decorative mesh", new Vector3(11, 6, 0), Vector3.one);
                jug.transform.SetParent(decoration.transform, true);
                var canopy = Cube("Soft tree canopy", new Vector3(-8, 6, 0), Vector3.one);
                Cube("Terracotta planter", new Vector3(-11, 3, 0), new Vector3(2, 2, 2));
                var grass = Cube("GrassShort", new Vector3(-11, 6, 0), Vector3.one);
                grass.AddComponent<LODGroup>();
                var tabletop = Cube("Picnic table wood", new Vector3(12, 7, 0), new Vector3(1, .1f, 1));
                ForestKitchen.SnapToGround();
                Check(Mathf.Abs(CharacterPoseChecks.Measure(table).min.y - 2) < .03f, "A non-prefab furniture assembly must reach an elevated floor as a whole.");
                Check(Mathf.Abs(trunk.GetComponent<Renderer>().bounds.min.y - 2) < .05f, "Trees more than 1.2 metres above ground must be repaired.");
                Check(Mathf.Abs(body.GetComponent<Renderer>().bounds.min.y - 2) < .03f, "Animated NPC roots must be grounded as a whole.");
                Check(lamp.transform.position.y == 8, "Hanging lamps must stay attached to the ceiling.");
                Check(cup.transform.position.y == 5.6f, "Tabletop props must not be dropped to the floor.");
                Check(Mathf.Abs(plant.GetComponent<Renderer>().bounds.min.y - 4) < .03f, "A tabletop plant must use its table as support, not the ground below it.");
                Check(jug.transform.position.y == 6, "LOD metadata alone must not classify a decoration as a grounded tree.");
                Check(canopy.transform.position.y == 6, "A separately authored tree canopy must remain above its trunk.");
                Check(Mathf.Abs(grass.GetComponent<Renderer>().bounds.min.y - 4) < .03f, "Plants in a pot must keep their planter as support.");
                Check(tabletop.transform.position.y == 7, "A separate procedural tabletop must not be dropped independently of its legs.");
                Check(ForestKitchen.SnapToGround() == 0, "Repeating repair must not drift grounded objects.");
                var ui = new GameObject("Voice controls fixture").AddComponent<SliceInterface>();
                var root = ui.GetComponent<UIDocument>()?.rootVisualElement;
                if (root == null || root.Q("slice-dialogue") == null)
                {
                    typeof(SliceInterface).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
                    root = ui.GetComponent<UIDocument>().rootVisualElement;
                }
                var dialogue = root.Q("slice-dialogue");
                Check(dialogue.Query<TextField>().ToList().Count == 0, "Speaking dialogue must not expose a typed reply field.");
                var tools = dialogue.Q(className: "dialogue-header").Query<Button>().ToList();
                Check(tools.Count == 3 && tools.All(button => button.text.Length <= 3 && !string.IsNullOrEmpty(button.tooltip)), "Dialogue tools need compact labels and descriptive tooltips.");
                Check(dialogue.Query<Button>().ToList().Any(button => button.ClassListContains("mic")), "The microphone action must remain available.");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory("../../.local/grounding");
                File.WriteAllText("../../.local/grounding/checks.txt", errors.Count == 0 ? "PASS" : string.Join("\n", errors));
            }
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            Debug.Log("Voice and grounding checks passed: 14 native fixtures.");
            void Check(bool passed, string message) { if (!passed) errors.Add(message); }
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            return item;
        }
    }
}
