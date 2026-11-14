using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Life;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ContextSpeak.Editor
{
    /// Apply downloaded skins to existing city interactions without rebuilding the artist's scene.
    public static class DownloadedCastUpgrade
    {
        private static readonly string[] Cars = { "Worldec2199483e", "Worldf412ab1c19", "Worldce470abc08", "World0a9f786f02" };

        [UnityEditor.MenuItem("ContextSpeak/Meshy/Render applied vehicles and NPCs")]
        public static void Render()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(CityScene.ScenePath);
            try
            {
                void Mark(string name, Vector3 position, Vector3 target)
                {
                    var mark = new GameObject("Shot · " + name).transform;
                    mark.position = position;
                    mark.LookAt(target);
                }
                Mark("Downloaded cars", new Vector3(114, 12, -83.5f), new Vector3(99, .5f, -83.5f));
                foreach (var id in new[] { "cafe:Maya", "cafe:Alex", "mall:Ellie" })
                {
                    var npc = DownloadedCastChecks.All<LearningNpc>(scene).Single(actor => actor.SceneId == id);
                    Mark(id.Replace(':', '-'), npc.transform.position + npc.transform.forward * 2.7f + Vector3.up * 1.4f,
                        npc.transform.position + Vector3.up * .95f);
                }
                EnvironmentShots.Render("MetroCity");
            }
            finally
            {
                // Rendering previews must not persist temporary cameras, weather or authoring changes.
                EditorSceneManager.OpenScene(CityScene.ScenePath);
                var car = DownloadedCastChecks.All<CityDrivable>(SceneManager.GetActiveScene())
                    .First(driver => driver.name.Contains(Cars[0]));
                Selection.activeGameObject = car.gameObject;
                SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(99, 1, -83.5f), new Vector3(9, 4, 28)), false);
            }
        }

        [UnityEditor.MenuItem("ContextSpeak/Meshy/Apply downloaded vehicles and NPCs")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var backup = Path.GetFullPath("../../.local/vehicle-npc-update");
            Directory.CreateDirectory(backup);
            var original = Path.Combine(backup, "MetroCity.before.unity");
            if (!File.Exists(original)) File.Copy(CityScene.ScenePath, original);
            var scene = EditorSceneManager.OpenScene(CityScene.ScenePath);
            DownloadedCastChecks.VerifyUpgrade(scene);
            DownloadedCastChecks.Verify();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Downloaded vehicles and NPCs applied to MetroCity; existing tasks, drivers and lighting retained.");
        }

        internal static void Upgrade(Scene scene)
        {
            if (scene.path != CityScene.ScenePath) throw new InvalidOperationException("Select the saved MetroCity scene.");
            var cars = DownloadedCastChecks.All<CityDrivable>(scene)
                .Where(car => car.transform.position.x > 90 && car.transform.position.z < 0)
                .OrderBy(car => car.transform.position.z).ToArray();
            var npcs = DownloadedCastChecks.All<LearningNpc>(scene);
            if (cars.Length != 4) throw new InvalidOperationException("The four city parking spaces were changed; review their placement before applying skins.");
            foreach (var id in Cars)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + id + ".prefab") == null)
                    throw new FileNotFoundException("Missing prepared car: " + id);
            var assignments = new[] { (id: "cafe:Maya", model: "CafeCharm"), (id: "mall:Ellie", model: "BraidedSchoolgirl") };
            // Validate all targets before changing the saved scene.
            foreach (var assignment in assignments)
            {
                var actor = npcs.Single(npc => npc.SceneId == assignment.id);
                if (actor.GetComponent<NpcActor>()?.Animator == null ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + assignment.model + ".prefab") == null)
                    throw new InvalidOperationException("Missing conversation pivot or prepared NPC: " + assignment.id);
            }
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                for (var index = 0; index < cars.Length; index++)
                {
                    ReplaceCar(cars[index], Cars[index]);
                    EnsureHeadlights(cars[index]);
                }
                // The former bay crossed the east–west road. Keep all four spaces on the mall's east side.
                var transforms = DownloadedCastChecks.All<Transform>(scene);
                var floor = transforms.Single(item => item.name == "Car park");
                floor.position = new Vector3(99, .03f, -83.5f);
                floor.localScale = new Vector3(8, .04f, 28);
                var lines = transforms.Where(item => item.name == "Parking line").OrderBy(item => item.position.z).ToArray();
                for (var index = 0; index < cars.Length; index++)
                {
                    cars[index].transform.position = new Vector3(99, .06f, -94 + index * 7);
                    lines[index].position = new Vector3(99, .055f, -90.5f + index * 7);
                }
                foreach (var assignment in assignments)
                    ReplaceNpc(npcs.Single(npc => npc.SceneId == assignment.id), assignment.model);
            }
            finally { if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }

        private static void ReplaceCar(CityDrivable driver, string id)
        {
            if (DownloadedCastChecks.MeshPaths(driver.gameObject).Any(path => path.Contains(id))) return;
            var prepared = CityScene.Car(driver.transform.parent, id, Vector3.zero, 0);
            var visual = prepared.transform.GetChild(0);
            var box = driver.GetComponent<BoxCollider>();
            var shape = prepared.GetComponent<BoxCollider>();
            // Keep the driving root, physics settings and parking position; only replace its skin.
            foreach (var child in driver.transform.Cast<Transform>().Where(child => DownloadedCastChecks.MeshPaths(child.gameObject)
                .Any(path => path.StartsWith(WorldAssetBuilder.Source, StringComparison.Ordinal))).ToArray())
                Object.DestroyImmediate(child.gameObject);
            visual.SetParent(driver.transform, false);
            box.center = shape.center;
            box.size = shape.size;
            driver.name = "Car · " + id;
            Object.DestroyImmediate(prepared);
        }

        private static void EnsureHeadlights(CityDrivable driver)
        {
            var switcher = driver.GetComponent<SkyLightSwitch>() ?? driver.gameObject.AddComponent<SkyLightSwitch>();
            switcher.Mode = SkyLightSwitch.Kind.Headlight;
            switcher.Glow = driver.GetComponentsInChildren<Renderer>().Where(renderer => renderer.sharedMaterials
                .Any(material => material != null && material.name.StartsWith("Headlight"))).ToArray();
            if (switcher.Lights.Any(light => light != null)) return;
            // Repair an earlier visual swap using the same spotlight settings as SkyInstaller.
            var beam = new GameObject("Sky headlight").AddComponent<Light>();
            beam.transform.SetParent(driver.transform, false);
            var box = driver.GetComponent<BoxCollider>();
            beam.transform.localPosition = new Vector3(0, .7f, box.size.z / 2 + .3f);
            beam.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -.12f, 1));
            beam.type = LightType.Spot;
            beam.spotAngle = 70;
            beam.innerSpotAngle = 40;
            beam.range = 20;
            beam.intensity = 3.5f;
            beam.color = new Color(1, .96f, .88f);
            beam.shadows = LightShadows.None;
            switcher.Lights = new[] { beam };
        }

        private static void ReplaceNpc(LearningNpc actor, string model)
        {
            if (DownloadedCastChecks.MeshPaths(actor.gameObject).Any(path => path.Contains("/" + model + ".glb") || path.Contains("/" + model + "-"))) return;
            var pivot = actor.GetComponent<NpcActor>().Animator.transform;
            var old = pivot.Cast<Transform>().Where(child => child.GetComponentInChildren<Renderer>(true) != null).ToArray();
            if (old.Length != 1) throw new InvalidOperationException("Expected one visual body under the NPC conversation pivot: " + actor.SceneId);
            var body = MeshyCharacters.Create(model, Vector3.zero);
            body.transform.SetParent(pivot, false);
            body.transform.localPosition = old[0].localPosition;
            body.transform.localRotation = old[0].localRotation;
            Object.DestroyImmediate(old[0].gameObject);
        }
    }
}
