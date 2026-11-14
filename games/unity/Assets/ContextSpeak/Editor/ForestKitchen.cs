using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Environment > Upgrade Forest Café Kitchen — turns the box kitchen behind the café
    /// into a bright, working café kitchen: a two-group espresso machine, grinder and barista tools on
    /// the coffee counter; a six-burner range under a stainless hood, prep tables, a double sink,
    /// a fridge, open oak shelves with plates, jars and herbs, a pan rail, a pass with heat lamps,
    /// white subway-tile splashbacks, a charcoal tile floor and neutral kitchen lighting.
    /// Models: Assets/Art/kitchen (made by tools/blender/kitchen.py). Repeatable: it replaces its own
    /// previous result and only switches the old placeholder pieces off (nothing is deleted).
    ///
    /// ContextSpeak > Environment > Snap Props To Ground — puts floating trees, plants, rocks and
    /// furniture of the open scene back on the ground (or the floor/table under them).
    public static class ForestKitchen
    {
        private const string Art = "Assets/Art/kitchen/";
        private const string Folder = EnvironmentSetup.Root + "/Kitchen";
        private const string RootName = "Café kitchen upgrade";

        [UnityEditor.MenuItem("ContextSpeak/Environment/Upgrade Forest Café Kitchen")]
        public static void Upgrade()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ForestGreenhouse.ScenePath)) { Debug.LogError("Forest café scene not built yet: " + ForestGreenhouse.ScenePath); return; }
            var scene = EditorSceneManager.OpenScene(ForestGreenhouse.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Folder);
            var missing = new[] { "EspressoMachine", "CoffeeGrinder", "BaristaTools", "Range", "ExtractorHood", "PrepTable", "SinkUnit", "Fridge", "WallShelf", "PotRail", "PassShelf", "KitchenPendant" }
                .Where(name => AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".glb") == null).ToArray();
            if (missing.Length > 0) { Debug.LogError("Kitchen models missing in " + Art + ": " + string.Join(", ", missing) + " — run tools/blender/kitchen.py in Blender."); return; }
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            var notes = new List<string>();

            notes.Add(RetireOld() + " placeholder pieces switched off");
            Surfaces(root);
            Equipment(root);
            BaristaStation(root);
            notes.Add(Lights(root) + " kitchen lights");
            var probe = new GameObject("Kitchen reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false);
            probe.transform.position = new Vector3(4.5f, 1.6f, -9.5f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.boxProjection = true;
            probe.size = new Vector3(9, 3.6f, 5);
            probe.resolution = 128;
            probe.importance = 2;
            notes.Add(SnapToGround() + " floating objects put back on the ground");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Forest café kitchen upgraded: " + string.Join(", ", notes) + ". Rebake navigation if staff paths look wrong (ContextSpeak > Environment > Setup Realistic World does it).");
        }

        // ------------------------------------------------------------------ helpers
        private static GameObject Place(string name, Transform parent, Vector3 position, float yaw, bool solid = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".glb");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            item.name = "Kitchen · " + name;
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            item.isStatic = true;
            foreach (var t in item.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            if (solid)
            {
                var renderers = item.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                var box = item.AddComponent<BoxCollider>();
                box.center = item.transform.InverseTransformPoint(bounds.center);
                var size = item.transform.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                var obstacle = item.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.center = box.center;
                obstacle.size = box.size;
            }
            return item;
        }

        private static GameObject Panel(string name, Transform parent, Vector3 centre, Vector3 size, Material material)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.position = centre;
            panel.transform.localScale = size;
            panel.GetComponent<Renderer>().sharedMaterial = material;
            panel.isStatic = true;
            return panel;
        }

        private static int RetireOld()
        {
            var count = 0;
            var kitchen = new Bounds(new Vector3(4.5f, 1.8f, -9.5f), new Vector3(8.8f, 3.4f, 4.8f));
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                var name = t.name;
                var inKitchen = kitchen.Contains(t.position);
                var retire = inKitchen && (name == "Stove" || name == "Stove top" || name == "Pot" || name == "Prep counter" || name == "Pendant shade" || name == "Warm café light");
                var prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                {
                    if (prefab.EndsWith("kitchenCoffeeMachine.glb")) retire = true;                     // the toy machines on the counter
                    if (inKitchen && prefab.EndsWith("jar_A_medium.gltf")) retire = true;
                }
                if (!retire || !t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(false);
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ tiles
        private static Texture2D SaveTexture(string name, Texture2D texture, bool normal)
        {
            var path = Folder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// Glazed tiles: `columns`×`rows` per texture, `offset` staggers alternate rows (subway bond).
        private static (Texture2D colour, Texture2D normal) Tiles(string name, int width, int height, int columns, int rows, bool offset, Color glaze, Color grout, float variation, int seed)
        {
            var random = new System.Random(seed);
            var colour = new Texture2D(width, height, TextureFormat.RGBA32, true);
            var heights = new float[width * height];
            var tones = new float[columns * 2 * rows];
            for (var i = 0; i < tones.Length; i++) tones[i] = (float)(random.NextDouble() - 0.5) * variation;
            var tileW = width / (float)columns; var tileH = height / (float)rows;
            const float groutPx = 3.5f, bevelPx = 6;
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var row = Mathf.FloorToInt(y / tileH);
                    var shift = offset && row % 2 == 1 ? tileW * 0.5f : 0;
                    var fx = (x + shift) % width;
                    var column = Mathf.FloorToInt(fx / tileW);
                    var lx = fx - column * tileW; var ly = y - row * tileH;
                    var edge = Mathf.Min(Mathf.Min(lx, tileW - lx), Mathf.Min(ly, tileH - ly));
                    var h = edge < groutPx ? 0 : Mathf.SmoothStep(0.55f, 1, Mathf.Clamp01((edge - groutPx) / bevelPx));
                    heights[y * width + x] = h;
                    var tone = tones[(row * columns * 2 + column) % tones.Length];
                    var c = edge < groutPx ? grout : new Color(glaze.r + tone, glaze.g + tone, glaze.b + tone * 0.8f, 1);
                    colour.SetPixel(x, y, c);
                }
            colour.Apply();
            var normal = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    float H(int px, int py) => heights[((py + height) % height) * width + (px + width) % width];
                    var dx = (H(x + 1, y) - H(x - 1, y)) * 2.2f;
                    var dy = (H(x, y + 1) - H(x, y - 1)) * 2.2f;
                    var n = new Vector3(-dx, -dy, 1).normalized;
                    normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1));
                }
            normal.Apply();
            return (SaveTexture(name + "_Color", colour, false), SaveTexture(name + "_Normal", normal, true));
        }

        private static Material TileMaterial(string name, (Texture2D colour, Texture2D normal) maps, Vector2 tiling, float smoothness)
        {
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", maps.colour);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", maps.normal);
            material.SetFloat("_BumpScale", 1);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureScale("_BumpMap", tiling);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Surfaces(Transform root)
        {
            // Subway tiles 15 × 7.5 cm: one texture = 0.6 m × 0.3 m (4 × 4 tiles, staggered).
            var subway = Tiles("SubwayTile", 512, 256, 4, 4, true, new Color(0.93f, 0.925f, 0.9f), new Color(0.74f, 0.73f, 0.7f), 0.025f, 4);
            // Floor: 30 cm charcoal porcelain tiles, one texture = 0.6 m × 0.6 m.
            var floor = Tiles("KitchenFloorTile", 512, 512, 2, 2, false, new Color(0.22f, 0.22f, 0.215f), new Color(0.13f, 0.13f, 0.125f), 0.035f, 9);
            const float splash = 2.3f;
            var back = TileMaterial("SplashbackBack", subway, new Vector2(8.8f / 0.6f, splash / 0.3f), 0.82f);
            var side = TileMaterial("SplashbackSide", subway, new Vector2(4.8f / 0.6f, splash / 0.3f), 0.82f);
            var front = TileMaterial("SplashbackPass", subway, new Vector2(3.9f / 0.6f, splash / 0.3f), 0.82f);
            Panel("Subway tile splashback · back", root, new Vector3(4.5f, splash / 2, -11.885f), new Vector3(8.8f, splash, 0.02f), back);
            Panel("Subway tile splashback · west", root, new Vector3(0.115f, splash / 2, -9.5f), new Vector3(0.02f, splash, 4.8f), side);
            Panel("Subway tile splashback · east", root, new Vector3(8.885f, splash / 2, -9.5f), new Vector3(0.02f, splash, 4.8f), side);
            Panel("Subway tile splashback · pass wall", root, new Vector3(1.95f, splash / 2, -7.115f), new Vector3(3.9f, splash, 0.02f), front);
            var floorMaterial = TileMaterial("KitchenFloor", floor, new Vector2(9 / 0.6f, 5 / 0.6f), 0.4f);
            var stone = GameObject.Find("Kitchen floor stone");
            if (stone != null) { stone.GetComponent<Renderer>().sharedMaterial = floorMaterial; EditorUtility.SetDirty(stone); }
            else Panel("Kitchen tile floor", root, new Vector3(4.5f, 0.055f, -9.5f), new Vector3(8.8f, 0.01f, 4.8f), floorMaterial);
            // A stainless worktop strip under the pass so the opening reads as a working pass.
        }

        // ------------------------------------------------------------------ layout
        private static void Equipment(Transform root)
        {
            const float back = -11.89f;
            // Cooking line on the back wall: range + hood, prep table + pan rail, double sink + shelf, fridge.
            Place("Range", root, new Vector3(1.75f, 0.05f, back + 0.43f), 0);
            Place("ExtractorHood", root, new Vector3(1.75f, 0, back + 0.47f), 0, false);
            Place("PrepTable", root, new Vector3(3.75f, 0.05f, back + 0.38f), 0);
            Place("PotRail", root, new Vector3(3.75f, 0, back + 0.06f), 0, false);
            Place("SinkUnit", root, new Vector3(5.85f, 0.05f, back + 0.36f), 0);
            Place("WallShelf", root, new Vector3(5.85f, 0, back + 0.15f), 0, false);
            Place("Fridge", root, new Vector3(7.75f, 0.05f, back + 0.41f), 0);
            Place("Fridge", root, new Vector3(8.5f, 0.05f, back + 0.41f), 0);
            // West wall: a second prep table (pastry / cold section) with shelving above.
            Place("PrepTable", root, new Vector3(0.12f + 0.38f, 0.05f, -9.0f), 90);
            Place("WallShelf", root, new Vector3(0.12f + 0.15f, 0, -9.0f), 90, false);
            // Pass: a stainless counter behind the pass window with heat lamps over it.
            Place("PrepTable", root, new Vector3(5.5f, 0.05f, -7.12f - 0.38f), 180);
            Place("PassShelf", root, new Vector3(5.5f, 1.1f, -7.0f), 0, false);
            // East wall: dry store shelving.
            Place("WallShelf", root, new Vector3(8.75f, -0.5f, -9.0f), -90, false);
            Place("WallShelf", root, new Vector3(8.75f, 0.35f, -9.0f), -90, false);
        }

        private static void BaristaStation(Transform root)
        {
            // On the walnut coffee counter (top at y ≈ 1.17), facing the barista behind it (-Z).
            var top = 1.17f;
            var counter = GameObject.Find("Walnut counter top");
            if (counter != null) top = counter.GetComponent<Renderer>().bounds.max.y;
            Place("EspressoMachine", root, new Vector3(4.55f, top, -4.45f), 180, false);
            Place("CoffeeGrinder", root, new Vector3(3.75f, top, -4.5f), 180, false);
            Place("CoffeeGrinder", root, new Vector3(3.45f, top, -4.5f), 180, false);
            Place("BaristaTools", root, new Vector3(3.0f, top, -4.55f), 180, false);
        }

        private static int Lights(Transform root)
        {
            var count = 0;
            foreach (var x in new[] { 1.8f, 4.5f, 7.2f })
            {
                Place("KitchenPendant", root, new Vector3(x, 3.6f - 1.1f, -9.4f), 0, false);
                var light = new GameObject("Kitchen light").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.position = new Vector3(x, 2.4f, -9.4f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.94f, 0.86f);          // ~4000 K: neutral, good for working
                light.intensity = 2.6f;
                light.range = 6.5f;
                light.shadows = x == 4.5f ? LightShadows.Soft : LightShadows.None;
                count++;
            }
            // Under-hood task light over the range.
            var task = new GameObject("Range task light").AddComponent<Light>();
            task.transform.SetParent(root, false);
            task.transform.position = new Vector3(1.75f, 1.95f, -11.2f);
            task.transform.rotation = Quaternion.Euler(90, 0, 0);
            task.type = LightType.Spot;
            task.spotAngle = 95;
            task.color = new Color(1f, 0.9f, 0.75f);
            task.intensity = 3;
            task.range = 3;
            return count + 1;
        }

        // ------------------------------------------------------------------ snap to ground
        [UnityEditor.MenuItem("ContextSpeak/Environment/Snap Props To Ground")]
        public static void SnapMenu()
        {
            var moved = SnapToGround();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Snap props to ground: " + moved + " objects moved in " + EditorSceneManager.GetActiveScene().name + " (save the scene to keep it).");
        }

        public static int SnapToGround() => GroundedProps.Repair(EditorSceneManager.GetActiveScene());
    }
}
