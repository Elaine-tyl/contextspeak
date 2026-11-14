using System;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// Upgrades the existing Forest Café scene in place (additive and repeatable): a stream with
    /// moss rocks and stepping stones, a waterfall with mist, ferns, a timber-and-glass
    /// conservatory roof over the terrace, extra forest visitors, a train stop back to Metro City,
    /// and the bridge that turns served orders into inventory items and life XP.
    /// The café's own menu, customisation, waiter, payment and seating systems are kept as they are.
    public static class ForestCafeUpgrade
    {
        public const string ScenePath = "Assets/Generated/ForestSlice.unity";
        private const string RootName = "Forest Café life upgrade";

        public static bool Apply()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Missing.Add("ForestSlice scene (" + ScenePath + ") — build the Forest Café first; upgrade skipped.");
                return false;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var old = GameObject.Find(RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            Begin(RootName);
            var moss = Hex("4F6B3A"); var rock = Hex("7D7A72"); var water = new Color(0.32f, 0.55f, 0.6f, 0.85f); var timber = Hex("8A5E3B");

            // Stream: winds down the east meadow from the waterfall to the trees.
            var course = new[] { new Vector3(24, 0, -17), new Vector3(23, 0, -11), new Vector3(25, 0, -5), new Vector3(23.5f, 0, 1), new Vector3(25.5f, 0, 7), new Vector3(24, 0, 13) };
            var random = new System.Random(20261113);
            for (var index = 0; index + 1 < course.Length; index++)
            {
                var from = course[index]; var to = course[index + 1];
                var middle = (from + to) / 2;
                var yaw = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
                var length = Vector3.Distance(from, to) + 0.8f;
                Box("Stream bed", middle + Vector3.up * 0.005f, new Vector3(2.4f, 0.02f, length), Hex("5E5244"), false, null, yaw);
                Box("Stream water", middle + Vector3.up * 0.03f, new Vector3(2.0f, 0.02f, length), water, false, null, yaw);
                for (var rockIndex = 0; rockIndex < 5; rockIndex++)
                {
                    var t = (float)random.NextDouble();
                    var side = random.Next(2) == 0 ? -1 : 1;
                    var across = Quaternion.Euler(0, yaw, 0) * Vector3.right * side * (1.3f + (float)random.NextDouble() * 0.5f);
                    var size = 0.35f + (float)random.NextDouble() * 0.6f;
                    var stone = Shape("Moss rock", PrimitiveType.Sphere, Vector3.Lerp(from, to, t) + across + Vector3.up * size * 0.25f,
                        new Vector3(size * 1.3f, size * 0.6f, size), random.Next(3) == 0 ? rock : moss, true);
                    stone.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                }
            }
            foreach (var z in new[] { -2.2f, -1.2f, -0.2f })
                Shape("Stepping stone", PrimitiveType.Cylinder, new Vector3(24.2f + z * 0.3f, 0.06f, z), new Vector3(0.55f, 0.06f, 0.5f), rock, true);

            // Waterfall: rock cliff, falling water sheets, pool and mist.
            Box("Waterfall cliff", new Vector3(24, 2.2f, -19.6f), new Vector3(7, 4.4f, 2.4f), rock, true);
            Shape("Cliff moss", PrimitiveType.Sphere, new Vector3(22.2f, 4.3f, -19.2f), new Vector3(2.6f, 0.8f, 2.2f), moss, false);
            Shape("Cliff moss", PrimitiveType.Sphere, new Vector3(26.1f, 4.0f, -19.0f), new Vector3(2.2f, 0.7f, 2.0f), moss, false);
            Box("Falling water", new Vector3(24, 2.1f, -18.35f), new Vector3(1.6f, 4.2f, 0.06f), new Color(0.82f, 0.93f, 0.97f, 0.7f), false);
            Box("Falling water spray", new Vector3(24, 1.6f, -18.25f), new Vector3(1.9f, 3.2f, 0.04f), new Color(0.95f, 0.98f, 1f, 0.35f), false);
            Shape("Plunge pool", PrimitiveType.Cylinder, new Vector3(24, 0.03f, -17.2f), new Vector3(3.6f, 0.02f, 2.4f), water, false);
            Mist(new Vector3(24, 0.3f, -17.6f));
            Board("Mossy Falls", new Vector3(20.2f, 1.3f, -15.8f), 0, new Vector2(1.6f, 0.4f), timber, Hex("F3E5CE"));

            // Ferns along the banks (downloaded meadow plants), small fern sprigs by the café.
            EnvironmentArt.Begin();
            var ferns = EnvironmentArt.Root;
            ferns.name = "Stream ferns (downloaded plants)";
            ferns.SetParent(GameObject.Find(RootName).transform, false);
            for (var index = 0; index < course.Length; index++)
            {
                try { EnvironmentArt.GrassPatch(course[index] + new Vector3(2.4f, 0, 0.5f), index * 47); EnvironmentArt.GrassPatch(course[index] + new Vector3(-2.3f, 0, -0.4f), index * 71); }
                catch (Exception error) { Missing.Add("Downloaded meadow plants for ferns (" + error.Message + ")"); break; }
            }
            for (var index = 0; index < 10; index++)
                Fern(new Vector3(-8.6f + index * 1.9f, 0, 12.6f), index * 33);

            // Timber-and-glass conservatory roof over the terrace (posts outside the seating).
            foreach (var x in new[] { -9.2f, 9.2f })
                foreach (var z in new[] { 7.1f, 12.1f })
                    Box("Conservatory post", new Vector3(x, 1.5f, z), new Vector3(0.14f, 3.0f, 0.14f), timber, true);
            for (var x = -9.2f; x <= 9.21f; x += 2.3f) Box("Roof rafter", new Vector3(x, 3.05f, 9.6f), new Vector3(0.1f, 0.12f, 5.2f), timber, false);
            Box("Glass roof", new Vector3(0, 3.1f, 9.6f), new Vector3(18.6f, 0.04f, 5.2f), new Color(0.85f, 0.94f, 0.92f, 0.35f), false);
            for (var x = -8f; x <= 8f; x += 4) Shape("Hanging fern", PrimitiveType.Sphere, new Vector3(x, 2.6f, 9.6f), new Vector3(0.5f, 0.35f, 0.5f), Hex("5D8A4A"), false);

            // Forest train stop back to Metro City.
            var stop = Group("Forest train stop", new Vector3(-12.5f, 0, 10.5f));
            Box("Platform", new Vector3(-12.5f, 0.1f, 10.5f), new Vector3(3, 0.2f, 1.6f), Hex("B9B2A3"), true);
            Box("Stop pole", new Vector3(-13.6f, 1.2f, 10.5f), new Vector3(0.1f, 2.4f, 0.1f), Hex("2E4057"), false);
            Board("TRAIN → METRO CITY\nPress B", new Vector3(-13.6f, 2.3f, 10.5f), 90, new Vector2(1.6f, 0.6f), Hex("2E4057"), Color.white);

            // Forest visitors (the café's waiter, chef and customers stay as they were).
            Visitor("QuietMorning", new Vector3(22, 0, -9), 80);
            Visitor("Contemplation", new Vector3(26.5f, 0, 3), 250);
            Visitor("OpenArms", new Vector3(-16.5f, 0, 18), 140);

            var bridge = new GameObject("Forest Café life bridge").AddComponent<ForestCafeLife>();
            bridge.transform.SetParent(GameObject.Find(RootName).transform, false);
            bridge.TrainStop = stop;
            RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.008f);
            SkyInstaller.Install();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static void Fern(Vector3 position, float yaw)
        {
            var fern = Group("Fern", position, yaw);
            for (var leaf = 0; leaf < 6; leaf++)
            {
                var frond = Shape("Frond", PrimitiveType.Cube, Vector3.zero, new Vector3(0.12f, 0.02f, 0.7f), Hex("4E7F3E"), false, fern, leaf * 60);
                frond.transform.localPosition = Quaternion.Euler(0, leaf * 60, 0) * new Vector3(0, 0.22f, 0.28f);
                frond.transform.localRotation = Quaternion.Euler(-28, leaf * 60, 0);
            }
        }

        private static void Mist(Vector3 position)
        {
            var mist = new GameObject("Waterfall mist").AddComponent<ParticleSystem>();
            mist.transform.SetParent(GameObject.Find(RootName).transform, false);
            mist.transform.position = position;
            var main = mist.main;
            main.startLifetime = 2.5f;
            main.startSpeed = 0.6f;
            main.startSize = 1.2f;
            main.startColor = new Color(1, 1, 1, 0.18f);
            main.maxParticles = 120;
            var emission = mist.emission;
            emission.rateOverTime = 30;
            var shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.4f, 0.2f, 1);
            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            var path = Data + "/Materials/WaterfallMist.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetFloat("_Surface", 1);
                material.SetColor("_BaseColor", new Color(1, 1, 1, 0.25f));
                System.IO.Directory.CreateDirectory(Data + "/Materials");
                AssetDatabase.CreateAsset(material, path);
            }
            renderer.sharedMaterial = material;
        }

        private static void Visitor(string model, Vector3 position, float yaw)
        {
            try
            {
                var body = MeshyCharacters.Create(model, position);
                body.name = "Forest visitor · " + model;
                body.transform.SetParent(GameObject.Find(RootName).transform, true);
                body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
            catch (Exception error) { Missing.Add("Visitor model " + model + " (" + error.Message + ")"); }
        }
    }
}
