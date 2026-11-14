using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.LivingWorld;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Editor
{
    /// A new additive authoring scene; never replaces or saves the artist's open world.
    public static class LivingWorldBuilder
    {
        public const string ResourceFolder = "Assets/Resources/LivingWorld/";
        public const string District = "Assets/Generated/LivingWorldDistrict.unity";
        private static Transform district;
        private static Material wood, cream, green, amber;

        public static void Prepare()
        {
            LivingWorldChecks.Verify();
            Directory.CreateDirectory(ResourceFolder + "Actors");
            var path = ResourceFolder + "Catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<LivingWorldCatalog>(path);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<LivingWorldCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
            catalog.Events = LivingWorldTemplates.All();
            catalog.Venues = new[]
            {
                Venue("cafe", "Jordan · a visitor needs directions", "ForestSlice", "CafeCharm", new Vector3(8, .05f, 8)),
                Venue("night-market", "Night Market", "LivingWorldDistrict", "CafeCharm", new Vector3(-28, .05f, -14)),
                Venue("camping", "Camping Ground", "LivingWorldDistrict", "TrenchElder", new Vector3(0, .05f, -14)),
                Venue("detective", "Detective Office", "LivingWorldDistrict", "AnimeDetective", new Vector3(28, .05f, -14)),
                Venue("pet-centre", "Pet Adoption Centre", "LivingWorldDistrict", "BraidedSchoolgirl", new Vector3(-28, .05f, 15)),
                Venue("cooking", "Cooking Academy", "LivingWorldDistrict", "Ali", new Vector3(0, .05f, 15)),
                Venue("music-festival", "Music Festival", "LivingWorldDistrict", "Plaid", new Vector3(28, .05f, 15)),
                Venue("cruise-terminal", "Cruise Terminal", "LivingWorldDistrict", "Secretary", new Vector3(0, .05f, 42)),
                Venue("airport", "Casey · airline service desk", "Airport", "Executive", new Vector3(10, .05f, -20)),
            };
            EditorUtility.SetDirty(catalog);
            foreach (var model in catalog.Venues.Select(venue => venue.Model).Distinct())
            {
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + model + ".prefab");
                if (original == null) throw new Exception("Missing story actor: " + model);
                var copy = UnityEngine.Object.Instantiate(original);
                try { PrefabUtility.SaveAsPrefabAsset(copy, ResourceFolder + "Actors/" + model + ".prefab"); }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            var rainPath = ResourceFolder + "Rain.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(rainPath) == null)
            {
                var rain = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                rain.SetColor("_BaseColor", new Color(.65f, .78f, .83f, .5f)); AssetDatabase.CreateAsset(rain, rainPath);
            }
            BuildDistrict(catalog);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(scene => scene.path == District)) scenes.Add(new EditorBuildSettingsScene(District, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Living-world catalog and seven compact activity spaces prepared.");
        }
        public static string[] PlayerScenes(string bootstrap)
        {
            // Do not depend on the editor's enabled-scene list: an authored location may be
            // open in Unity without being registered in that older list.
            var paths = WorldSceneCatalog.PlayerPaths(bootstrap);
            foreach (var path in paths)
                if (!File.Exists(path)) throw new Exception("Saved player scene is missing: " + path);
            return paths;

        }
        private static StoryVenue Venue(string id, string title, string scene, string model, Vector3 position) =>
            new StoryVenue { Id = id, Title = title, Scene = scene, Model = model, Position = position };

        private static Material Material(string id, Color color)
        {
            var path = ResourceFolder + id + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.color = color; result.SetFloat("_Smoothness", .2f); return result;
        }
        private static void BuildDistrict(LivingWorldCatalog catalog)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                district = new GameObject("Living-world activity district").transform;
                wood = Material("Wood", new Color(.38f, .23f, .13f)); cream = Material("Cream", new Color(.89f, .84f, .69f));
                green = Material("Sage", new Color(.25f, .42f, .32f)); amber = Material("Amber", new Color(.9f, .56f, .19f));
                Box("Walkable square", new Vector3(0, -.2f, 13), new Vector3(92, .4f, 85), green);
                Box("Main promenade", new Vector3(0, .01f, 10), new Vector3(85, .02f, 6), cream);
                Box("Connecting path", new Vector3(0, .02f, 15), new Vector3(6, .02f, 70), cream);
                foreach (var venue in catalog.Venues.Where(venue => venue.Scene == "LivingWorldDistrict")) Plot(venue);
                var sun = new GameObject("Late afternoon sun").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.color = new Color(1, .86f, .64f); sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(38, -35, 0);
                RenderSettings.ambientLight = new Color(.55f, .61f, .62f);
                RenderSettings.fog = true; RenderSettings.fogColor = new Color(.61f, .72f, .68f); RenderSettings.fogDensity = .003f;
                LifeBuild.Player(new Vector3(-27, .1f, -21), 0);
                var surface = district.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                EditorSceneManager.SaveScene(scene, District);
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }
        private static void Plot(StoryVenue venue)
        {
            var centre = venue.Position + Vector3.forward * 3;
            Box(venue.Title + " deck", centre + Vector3.down * .08f, new Vector3(19, .16f, 16), wood);
            var sign = new GameObject(venue.Title).AddComponent<TextMesh>(); sign.transform.SetParent(district, false);
            sign.transform.position = centre + new Vector3(0, 3.5f, 1); sign.text = venue.Title;
            sign.anchor = TextAnchor.MiddleCenter; sign.characterSize = .12f; sign.fontSize = 40; sign.color = new Color(1, .94f, .76f);
            var pole = new Vector3(7.5f, 1.7f, 4);
            foreach (var side in new[] { -1, 1 })
            {
                var point = centre + new Vector3(pole.x * side, pole.y, pole.z);
                Box("Timber lantern post", point, new Vector3(.18f, 3.4f, .18f), wood);
                var glow = Box("Warm lantern", point + Vector3.up * 1.6f, new Vector3(.6f, .5f, .6f), amber);
                var light = glow.AddComponent<Light>(); light.color = new Color(1, .68f, .32f); light.range = 7; light.intensity = 2;
            }
            if (venue.Id == "camping")
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var tent = GameObject.CreatePrimitive(PrimitiveType.Cylinder); tent.name = "Camping shelter";
                    tent.transform.SetParent(district, false); tent.transform.position = centre + new Vector3(side * 4, 1, 3);
                    tent.transform.localScale = new Vector3(3, 1, 3); tent.GetComponent<Renderer>().sharedMaterial = green;
                }
                Box("Camp supply bench", centre + new Vector3(0, .5f, 4), new Vector3(3, 1, 1.2f), wood);
                SuppliedProp("World6cf59f3fa5", centre + new Vector3(-4, .05f, 0), 1);
            }
            else if (venue.Id == "music-festival")
            {
                Box("Low performance stage", centre + new Vector3(0, .35f, 4), new Vector3(9, .7f, 4), wood);
                foreach (var side in new[] { -1, 1 }) Box("Speaker cabinet", centre + new Vector3(side * 4, 1.2f, 4), new Vector3(1, 1.7f, .8f), green);
            }
            else if (venue.Id == "cruise-terminal")
            {
                Box("Departure desk", centre + new Vector3(0, .7f, 4), new Vector3(5, 1.4f, 1.2f), cream);
                Box("Boarding pier", centre + new Vector3(0, .03f, 8), new Vector3(4, .06f, 3), wood);
                SuppliedProp("Worldb8f4bf2c59", centre + new Vector3(5, .05f, 3), 3);
            }
            else
            {
                Box("Open-front pavilion", centre + new Vector3(0, 3.2f, 3), new Vector3(14, .25f, 8), green);
                foreach (var side in new[] { -1, 1 }) Box("Pavilion post", centre + new Vector3(side * 6.5f, 1.6f, 3), new Vector3(.3f, 3.2f, .3f), wood);
                Box("Activity counter", centre + new Vector3(0, .65f, 3.5f), new Vector3(7, 1.3f, 1.2f), cream);
                if (venue.Id == "night-market" || venue.Id == "cooking")
                    for (var i = 0; i < 4; i++) Box("Food display", centre + new Vector3(i * 1.4f - 2, 1.4f, 3.5f), new Vector3(.9f, .25f, .8f), amber);
                if (venue.Id == "pet-centre")
                {
                    SuppliedProp("World8017170c0c", centre + new Vector3(4, .1f, 0), .6f);
                    Box("Pet resting mat", centre + new Vector3(4, .04f, 0), new Vector3(2, .08f, 1.5f), amber);
                    Box("Water bowl", centre + new Vector3(5, .13f, -1), new Vector3(.5f, .26f, .5f), cream);
                }
                if (venue.Id == "night-market") SuppliedProp("World32a2d6275e", centre + new Vector3(-5, .05f, 0), 1.4f);
                if (venue.Id == "detective") Box("Evidence board", centre + new Vector3(0, 2, 6), new Vector3(5, 2.2f, .15f), amber);
            }
        }
        private static void SuppliedProp(string id, Vector3 position, float size)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + id + ".prefab");
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(district, true);
            WorldScenery.Fit(instance, new SceneryEntry { realSize = size, position = new[] { position.x, position.y, position.z } });
        }
        private static GameObject Box(string name, Vector3 point, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(district, false); box.transform.position = point; box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material; return box;
        }
    }
}
