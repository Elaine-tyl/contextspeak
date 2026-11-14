using System;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Life;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Editor
{
    public static class DownloadedCastChecks
    {
        private static readonly string[] Cars = { "Worldec2199483e", "Worldf412ab1c19", "Worldce470abc08", "World0a9f786f02" };

        [UnityEditor.MenuItem("ContextSpeak/Checks/Downloaded vehicles and NPCs")]
        public static void Verify()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(CityScene.ScenePath);
            var opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(CityScene.ScenePath, OpenSceneMode.Additive);
            try
            {
                var cars = All<CityDrivable>(scene).Where(car => car.transform.position.x > 90 && car.transform.position.z < 0)
                    .OrderBy(car => car.transform.position.z).ToArray();
                Require(cars.Length == 4, "The four original parking spaces must remain.");
                for (var index = 0; index < Cars.Length; index++)
                {
                    var car = cars[index];
                    Require(MeshPaths(car.gameObject).Any(path => path.Contains(Cars[index])), "Unused downloaded car is missing from parking space " + (index + 1) + ": " + Cars[index]);
                    var box = car.GetComponent<BoxCollider>();
                    Require(box != null && box.size.z > 4 && box.size.z < 5.2f && box.size.x > 1 && box.size.x < 3,
                        "Downloaded car must have a plausible driving collider.");
                    var renderers = car.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    Require(bounds.min.x > 94 && bounds.max.x < 104 && bounds.min.z > -100 && bounds.max.z < -65,
                        "Parking must stay beside the mall, clear of roads and pavements: " + car.name);
                    Require(Mathf.Abs(bounds.min.y - car.transform.position.y) < .03f, "Car wheels must stand on the road.");
                    Require(car.GetComponent<Rigidbody>()?.isKinematic == true && car.MaxSpeed > 0, "Driving behavior must remain attached.");
                    var headlights = car.GetComponent<SkyLightSwitch>();
                    Require(headlights != null && headlights.Lights.Length > 0 && headlights.Lights.All(light => light != null),
                        "Replacement must retain working night headlights: " + car.name);
                    var parked = car.transform.position;
                    try
                    {
                        // Edit-mode checks do not receive Awake; initialize the real driver's collider as Play mode does.
                        typeof(CityDrivable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(car, null);
                        Physics.SyncTransforms();
                        car.Drive(1, 0, .1f);
                        Require(car.Speed > 0 && Vector3.Distance(car.transform.position, parked) > .001f, "New car must be able to pull out of its parking space.");
                        car.Stop();
                        Require(car.Speed == 0, "Car must stop after driving.");
                        var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        try
                        {
                            car.transform.position = parked;
                            barrier.transform.rotation = car.transform.rotation;
                            barrier.transform.localScale = new Vector3(6, 4, .2f);
                            barrier.transform.position = parked + car.transform.forward * (box.size.z / 2 + .25f) + Vector3.up;
                            Physics.SyncTransforms();
                            car.Drive(1, 0, .5f);
                            // Ground following may adjust Y; collision must prevent horizontal penetration.
                            Require(car.Speed == 0 && Vector3.ProjectOnPlane(car.transform.position - parked, Vector3.up).magnitude < .001f,
                                "New car must stop before a solid obstacle: " + car.name + ", speed=" + car.Speed
                                + ", offset=" + (car.transform.position - parked) + ", collider=" + box.size);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(barrier); }
                    }
                    finally { car.Stop(); car.transform.position = parked; Physics.SyncTransforms(); }
                }
                var npcs = All<LearningNpc>(scene);
                CheckNpc(npcs, "cafe:Maya", "CafeCharm");
                CheckNpc(npcs, "cafe:Alex", "Elliot");
                CheckNpc(npcs, "mall:Ellie", "BraidedSchoolgirl");
                Debug.Log("Downloaded vehicles and NPC checks passed: 4 cars and 3 conversation actors.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        internal static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        internal static void VerifyUpgrade(Scene scene)
        {
            var actors = All<LearningNpc>(scene);
            var ids = actors.Select(actor => actor.SceneId).ToArray();
            var positions = actors.Select(actor => actor.transform.position).ToArray();
            var animators = actors.Select(actor => actor.GetComponent<NpcActor>()?.Animator).ToArray();
            var drivers = All<CityDrivable>(scene);
            var parking = drivers.Select(driver => driver.transform.position).ToArray();
            var speeds = drivers.Select(driver => driver.MaxSpeed).ToArray();
            DownloadedCastUpgrade.Upgrade(scene);
            var renderers = All<Renderer>(scene).Length;
            var lights = All<Light>(scene).Length;
            DownloadedCastUpgrade.Upgrade(scene);
            Require(All<Renderer>(scene).Length == renderers && All<LearningNpc>(scene).Length == actors.Length
                && All<CityDrivable>(scene).Length == drivers.Length && All<Light>(scene).Length == lights,
                "Applying again must not duplicate visuals, lights or interactions.");
            for (var index = 0; index < actors.Length; index++)
                Require(actors[index] != null && actors[index].SceneId == ids[index] && actors[index].transform.position == positions[index]
                    && actors[index].GetComponent<NpcActor>()?.Animator == animators[index], "NPC root, mission ID and conversation animator must survive visual replacement.");
            for (var index = 0; index < drivers.Length; index++)
                Require(drivers[index] != null && drivers[index].MaxSpeed == speeds[index]
                    && (drivers[index].transform.position == parking[index] || Cars.Any(id => drivers[index].name.EndsWith(id)) && drivers[index].transform.position.x == 99),
                    "Car replacement must preserve the existing driver; only the four mall spaces may move.");
            Debug.Log("Downloaded cast preservation and repeated-apply checks passed.");
        }

        internal static string[] MeshPaths(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true)
            .Select(filter => filter.sharedMesh).Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin => skin.sharedMesh))
            .Where(mesh => mesh != null).Select(AssetDatabase.GetAssetPath).ToArray();

        private static void CheckNpc(LearningNpc[] npcs, string id, string model)
        {
            var npc = npcs.Single(actor => actor.SceneId == id);
            Require(MeshPaths(npc.gameObject).Any(path => path.Contains("/" + model + ".glb") || path.Contains("/" + model + "-")), "New NPC appearance is missing: " + id + "/" + model);
            Require(npc.GetComponent<CapsuleCollider>() != null && !string.IsNullOrEmpty(npc.DisplayName), "NPC interaction identity must remain.");
            if (model == "CafeCharm" || model == "Elliot")
            {
                var motion = npc.GetComponentInChildren<AuthoredMotion>();
                Require(motion != null && motion.ModelName == model && motion.Idle != null && motion.Player != null,
                    "Animated NPC must retain its supplied idle and clip player: " + model);
                foreach (var role in MotionAssets.Roles(model))
                    Require(motion.Find(role) != null, "Supplied animation is missing: " + model + "/" + role);
            }
            foreach (var renderer in npc.GetComponentsInChildren<Renderer>())
                Require(renderer.sharedMaterials.All(material => material != null && material.shader != null
                    && material.shader.name != "Hidden/InternalErrorShader"), "NPC materials must be valid: " + id);
        }

        private static void Require(bool ok, string message)
        {
            if (!ok) throw new Exception("Downloaded cast: " + message);
        }
    }
}
