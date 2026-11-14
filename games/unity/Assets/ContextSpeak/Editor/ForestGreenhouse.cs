using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Environment > Upgrade Forest Café Greenhouse — the visual benchmark for the whole game
    /// (reference: glass-house cafés with sun through the roof, plants, terracotta, dark frames, dappled shade).
    ///  • Clear, thin glazing that lets sunlight and tree shadows through (glass casts no shadow; reflections kept).
    ///  • Slender dark-steel frames: thinner roof beams, glazing bars, purlins and a ridge that cast fine shadow lines.
    ///  • Weathered brick paver floor (world-projected PBR) instead of flat wood.
    ///  • Oaks beside the glass house whose wind-animated crowns throw moving dappled shade through the roof.
    ///  • Terracotta shelving with potted plants, hanging ivy from the beams, floor planters, warm pendant lights.
    ///  • A covered lighting zone + local reflection probe, and two camera marks for the lighting test shots.
    public static class ForestGreenhouse
    {
        public const string ScenePath = "Assets/Generated/ForestSlice.unity";
        private const string Folder = EnvironmentSetup.Root + "/Greenhouse";
        private const string RootName = "Greenhouse upgrade";

        [UnityEditor.MenuItem("ContextSpeak/Environment/Upgrade Forest Café Greenhouse")]
        public static void Upgrade()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) { Debug.LogError("Forest café scene not built yet: " + ScenePath); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Folder);
            var plants = EnvironmentSetup.EnsureVegetationPrefabs();
            var ground = EnvironmentSetup.EnsureGroundMaterials();
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            var notes = new List<string>();

            var floor = GameObject.Find("Oak floor wood");
            if (floor == null) { Debug.LogError("Forest café floor ('Oak floor wood') not found — rebuild the forest scene first."); return; }
            var bounds = floor.GetComponent<Renderer>().bounds;
            var y0 = bounds.max.y;

            notes.Add(Glass() + " glass panes now clear and shadow-free");
            notes.Add(Frames() + " frame members in slender dark steel");
            notes.Add(GlazingBars(root) + " glazing bars / purlins / ridge");
            if (ground.TryGetValue("BrickPavers", out var bricks))
            {
                floor.GetComponent<Renderer>().sharedMaterial = bricks;
                notes.Add("brick paver floor");
            }
            notes.Add(Trees(root, plants, bounds) + " oaks shading the roof");
            notes.Add(Shelf(root, plants, bounds, y0) + " pots on the terracotta shelf");
            notes.Add(Ivy(root, plants, bounds) + " hanging ivy strands");
            notes.Add(Pendants(root, bounds) + " warm pendants");
            Zone(root, bounds);
            notes.Add(Skin() ? "outside and inside now show the same glass café (imported shell removed)" : "no imported shell found");
            notes.Add(GroundFurniture() + " tables/chairs/seats set on the floor");
            notes.Add(Water(root) + " water surfaces (pond, stream, waterfall) with the dynamic water shader; the pond is swimmable");
            Shot(root, "Shot · Greenhouse table", new Vector3(bounds.min.x + 2.4f, y0 + 1.55f, bounds.max.z - 1.6f), new Vector3(bounds.center.x + 3, y0 + 1.1f, bounds.min.z + 2));
            Shot(root, "Shot · Greenhouse roof", new Vector3(bounds.max.x - 2.2f, y0 + 1.4f, bounds.max.z - 1.8f), new Vector3(bounds.center.x - 3, y0 + 4.6f, bounds.min.z + 3));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Forest café greenhouse upgraded: " + string.Join(", ", notes) + ". Render it with ContextSpeak > Environment > Render Forest Café Shots.");
        }

        private static Material GlassMaterial()
        {
            var path = Folder + "/GreenhouseGlass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            // Clean float glass: almost no tint, very smooth; reflections are kept at full strength
            // (preserve specular / premultiplied) so it reads as glass, not as a grey sheet.
            material.SetColor("_BaseColor", new Color(0.86f, 0.92f, 0.9f, 0.08f));
            material.SetFloat("_Smoothness", 0.96f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_BlendModePreserveSpecular", 1);
            material.SetFloat("_Cull", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static int Glass()
        {
            var glass = GlassMaterial();
            var count = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var name = renderer.name.ToLowerInvariant();
                if (!(name.Contains("glass") || name.Contains("glazing")) || name.Contains("display")) continue;
                renderer.sharedMaterial = glass;
                renderer.shadowCastingMode = ShadowCastingMode.Off;      // sunlight and leaf shadows pass through
                renderer.receiveShadows = false;
                count++;
            }
            return count;
        }

        private static Material Steel() => LifeBuild.Surface(LifeBuild.Hex("1E2123"), 0.45f, 0.65f);

        private static int Frames()
        {
            var steel = Steel();
            var count = 0;
            var names = new[] { "Window mullion", "Window header", "Roof beam", "Door frame left", "Door frame right", "Door lintel", "Roof rafter", "Conservatory post", "Conservatory beam" };
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!names.Contains(renderer.name)) continue;
                renderer.sharedMaterial = steel;
                var scale = renderer.transform.localScale;
                if (renderer.name == "Roof beam") renderer.transform.localScale = new Vector3(scale.x, 0.11f, 0.08f);   // slender tie beams
                if (renderer.name == "Window mullion") renderer.transform.localScale = new Vector3(0.08f, scale.y, 0.08f);
                count++;
            }
            return count;
        }

        private static int GlazingBars(Transform root)
        {
            var steel = Steel();
            var group = new GameObject("Glazing bars").transform;
            group.SetParent(root, false);
            var count = 0;
            float ridgeY = float.MinValue, ridgeX = 0, ridgeZ = 0, ridgeLength = 0;
            foreach (var roof in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Greenhouse roof glass"))
            {
                var t = roof.transform;
                var size = t.lossyScale;
                var plane = new GameObject("Roof plane frame").transform;
                plane.SetParent(group, false);
                plane.SetPositionAndRotation(t.position, t.rotation);
                void Bar(Vector3 local, Vector3 dims)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(bar.GetComponent<Collider>());
                    bar.name = "Glazing bar";
                    bar.transform.SetParent(plane, false);
                    bar.transform.localPosition = local;
                    bar.transform.localScale = dims;
                    bar.GetComponent<Renderer>().sharedMaterial = steel;
                    count++;
                }
                for (var z = -size.z / 2; z <= size.z / 2 + 0.01f; z += 1.2f) Bar(new Vector3(0, -0.04f, z), new Vector3(size.x, 0.05f, 0.04f));
                foreach (var x in new[] { -size.x * 0.3f, size.x * 0.05f, size.x * 0.4f }) Bar(new Vector3(x, -0.06f, 0), new Vector3(0.05f, 0.07f, size.z));
                var top = t.TransformPoint(new Vector3(Mathf.Sign(-t.position.x + 1e-3f) * 0.5f, 0, 0));
                if (top.y > ridgeY) { ridgeY = top.y; ridgeX = 0; ridgeZ = t.position.z; ridgeLength = size.z; }
            }
            if (ridgeY > float.MinValue)
            {
                var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(ridge.GetComponent<Collider>());
                ridge.name = "Roof ridge";
                ridge.transform.SetParent(group, false);
                ridge.transform.position = new Vector3(ridgeX, ridgeY - 0.02f, ridgeZ);
                ridge.transform.localScale = new Vector3(0.12f, 0.12f, ridgeLength + 0.2f);
                ridge.GetComponent<Renderer>().sharedMaterial = steel;
                count++;
            }
            return count;
        }

        private static bool Clear(Vector3 point, float radius, params Collider[] ignore)
        {
            foreach (var hit in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!ignore.Contains(hit) && !(hit is TerrainCollider) && hit.bounds.size.y > 0.3f) return false;
            return true;
        }

        private static int Trees(Transform root, Dictionary<string, GameObject> plants, Bounds cafe)
        {
            if (!plants.TryGetValue("OakTree", out var oak)) return 0;
            Physics.SyncTransforms();
            var count = 0;
            var spots = new[]
            {
                new Vector3(cafe.min.x - 4.5f, 0, cafe.center.z - 1.5f), new Vector3(cafe.min.x - 4f, 0, cafe.center.z + 4.5f),
                new Vector3(cafe.max.x + 4.8f, 0, cafe.center.z - 2f), new Vector3(cafe.center.x - 5f, 0, cafe.min.z - 6.5f),
                new Vector3(cafe.max.x + 4.2f, 0, cafe.center.z + 4f),
            };
            foreach (var spot in spots)
            {
                if (!Physics.Raycast(spot + Vector3.up * 40, Vector3.down, out var hit, 80, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Clear(hit.point + Vector3.up * 1.2f, 0.9f, hit.collider)) continue;
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(oak, root);
                tree.transform.position = hit.point - Vector3.up * 0.05f;
                tree.transform.rotation = Quaternion.Euler(0, (spot.x * 37 + spot.z * 11) % 360, 0);
                tree.transform.localScale = oak.transform.localScale * 1.1f;
                count++;
            }
            return count;
        }

        private static GameObject Place(Dictionary<string, GameObject> plants, string name, Transform parent, Vector3 position, float scale = 1, float yaw = 0)
        {
            if (!plants.TryGetValue(name, out var prefab)) return null;
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            item.transform.localScale = prefab.transform.localScale * scale;
            return item;
        }

        private static int Shelf(Transform root, Dictionary<string, GameObject> plants, Bounds cafe, float y0)
        {
            var shelf = new GameObject("Terracotta shelf").transform;
            shelf.SetParent(root, false);
            var wood = LifeBuild.Surface(LifeBuild.Hex("8A5A36"), 0.35f);
            var backZ = cafe.min.z + 0.3f;
            var from = cafe.min.x + 1.2f;
            var width = 4.4f;
            if (!Clear(new Vector3(from + width / 2, y0 + 1.2f, backZ + 0.2f), 0.35f, Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.name.Contains("wall") || c.name.Contains("Wall") || c.name.Contains("floor")).ToArray()))
                from = cafe.min.x + 0.6f;
            void Board(Vector3 centre, Vector3 size)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Shelf board";
                b.transform.SetParent(shelf, false);
                b.transform.position = centre;
                b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = wood;
            }
            foreach (var level in new[] { 0.06f, 0.75f, 1.4f, 2.05f }) Board(new Vector3(from + width / 2, y0 + level, backZ + 0.2f), new Vector3(width, 0.04f, 0.38f));
            for (var x = 0f; x <= width + 0.01f; x += width / 3) Board(new Vector3(from + x, y0 + 1.06f, backZ + 0.2f), new Vector3(0.05f, 2.12f, 0.38f));
            var pots = new[] { "TerracottaPotSmall", "TerracottaPotTall", "TerracottaPotWide", "TerracottaJug" };
            var greens = new[] { "GrassShort", "Chrysanthemum", "Vallisneria", "WildViolets", "BlossomC" };
            var random = new System.Random(5);
            var count = 0;
            foreach (var level in new[] { 0.08f, 0.77f, 1.42f, 2.07f })
                for (var x = 0.25f; x < width - 0.15f; x += 0.32f + (float)random.NextDouble() * 0.2f)
                {
                    var name = pots[random.Next(pots.Length)];
                    var spot = new Vector3(from + x, y0 + level, backZ + 0.2f + ((float)random.NextDouble() - 0.5f) * 0.1f);
                    if (Place(plants, name, shelf, spot, 1, random.Next(360)) == null) continue;
                    count++;
                    if (name != "TerracottaJug" && random.NextDouble() < 0.6)
                        Place(plants, greens[random.Next(greens.Length)], shelf, spot + Vector3.up * (name == "TerracottaPotTall" ? 0.27f : name == "TerracottaPotWide" ? 0.13f : 0.11f), name == "TerracottaPotWide" ? 0.9f : 0.55f, random.Next(360));
                }
            // Floor planters in the corners by the glass.
            foreach (var corner in new[] { new Vector3(cafe.min.x + 0.6f, y0, cafe.max.z - 0.7f), new Vector3(cafe.max.x - 0.6f, y0, cafe.max.z - 0.7f), new Vector3(cafe.max.x - 0.6f, y0, cafe.min.z + 2.2f) })
            {
                if (!Clear(corner + Vector3.up * 0.4f, 0.3f, floorColliders())) continue;
                var pot = Place(plants, "TerracottaPotTall", shelf, corner, 2.2f);
                if (pot == null) continue;
                Place(plants, random.NextDouble() < 0.5 ? "Chrysanthemum" : "TomatoPlant", shelf, corner + Vector3.up * 0.6f, 1.3f, random.Next(360));
                count++;
            }
            return count;
        }

        private static Collider[] floorColliders() => Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.name.ToLowerInvariant().Contains("floor")).ToArray();

        private static int Ivy(Transform root, Dictionary<string, GameObject> plants, Bounds cafe)
        {
            var group = new GameObject("Hanging ivy").transform;
            group.SetParent(root, false);
            var beams = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Roof beam").Select(r => r.bounds).ToArray();
            var random = new System.Random(9);
            var count = 0;
            foreach (var beam in beams)
                foreach (var side in new[] { -1, 1 })
                {
                    if (random.NextDouble() < 0.35) continue;
                    var x = side > 0 ? beam.max.x - 0.8f - (float)random.NextDouble() * 2.5f : beam.min.x + 0.8f + (float)random.NextDouble() * 2.5f;
                    var item = Place(plants, "HangingIvy" + (1 + random.Next(3)), group, new Vector3(x, beam.min.y, beam.center.z), (float)(0.8 + random.NextDouble() * 0.4), random.Next(360));
                    if (item != null) count++;
                }
            return count;
        }

        private static int Pendants(Transform root, Bounds cafe)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentSetup.Root + "/Lights/Warm Pendant.prefab");
            if (prefab == null) return 0;
            var group = new GameObject("Pendants").transform;
            group.SetParent(root, false);
            var cable = LifeBuild.Surface(LifeBuild.Hex("161616"), 0.3f);
            var count = 0;
            foreach (var x in new[] { -5f, -1.5f, 2f })
            {
                var top = new Vector3(cafe.center.x + x, cafe.max.y + 3.65f, cafe.center.z + 2.5f);
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                lamp.transform.position = top - Vector3.up * 0.75f;
                var wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(wire.GetComponent<Collider>());
                wire.name = "Pendant cable";
                wire.transform.SetParent(group, false);
                wire.transform.position = top - Vector3.up * 0.37f;
                wire.transform.localScale = new Vector3(0.01f, 0.37f, 0.01f);
                wire.GetComponent<Renderer>().sharedMaterial = cable;
                count++;
            }
            return count;
        }

        /// The café used an imported shell outdoors and the real room indoors, so it changed when you walked in.
        /// The real glass house is now shown from both sides and the shell is switched off for good.
        private static bool Skin()
        {
            var done = false;
            foreach (var skin in Object.FindObjectsByType<BuildingSkin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (skin.Scene != "counter" || skin.Kind != "room") continue;
                foreach (var renderer in skin.Interior) if (renderer != null) { renderer.enabled = true; EditorUtility.SetDirty(renderer); }
                skin.UseAuthoredInterior();
                skin.Interior = new Renderer[0];
                if (skin.Exterior != null)
                    foreach (var collider in skin.Exterior.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                EditorUtility.SetDirty(skin);
                done = true;
            }
            return done;
        }

        private static float FloorUnder(Vector3 point, float around, MeshRenderer[] floors)
        {
            var best = float.MinValue;
            foreach (var floor in floors)
            {
                var b = floor.bounds;
                if (point.x < b.min.x || point.x > b.max.x || point.z < b.min.z || point.z > b.max.z) continue;
                if (b.max.y > around + 1.2f || b.max.y < around - 1.2f) continue;
                best = Mathf.Max(best, b.max.y);
            }
            return best;
        }

        /// Stands every table and chair on the floor that is actually under it, and brings over-wide tables to a real size.
        private static int GroundFurniture()
        {
            var floors = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r =>
            {
                var name = r.name.ToLowerInvariant();
                var b = r.bounds;
                return r.enabled && b.size.y < 0.45f && b.size.x * b.size.z > 6 && !name.Contains("glass") && !name.Contains("water") && !name.Contains("roof") && !name.Contains("ceiling") && !name.Contains("lily");
            }).ToArray();
            var count = 0;
            foreach (var item in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Downloaded café furniture"))
            {
                var renderers = item.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds Measure() { var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b; }
                var bounds = Measure();
                if (bounds.size.y < 0.95f && bounds.size.x > 1.35f)
                {
                    // A café table is about 1.1 m across; the download was 1.8 m and swallowed its chairs.
                    var k = 1.1f / bounds.size.x;
                    var centre = bounds.center;
                    item.localScale = Vector3.Scale(item.localScale, new Vector3(k, 1, k));
                    bounds = Measure();
                    item.position += new Vector3(centre.x - bounds.center.x, 0, centre.z - bounds.center.z);
                    foreach (var box in item.GetComponents<BoxCollider>()) box.size = new Vector3(box.size.x, box.size.y, box.size.z);
                    bounds = Measure();
                    count++;
                }
                var floor = FloorUnder(bounds.center, bounds.min.y, floors);
                if (floor > float.MinValue && Mathf.Abs(floor - bounds.min.y) > 0.005f) { item.position += Vector3.up * (floor - bounds.min.y); count++; }
            }
            foreach (var mark in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Seat ") || t.name.StartsWith("Table top ")))
            {
                var lift = mark.name.StartsWith("Seat ") ? 0.05f : 0.8f;
                var floor = FloorUnder(mark.position, mark.position.y - lift, floors);
                if (floor > float.MinValue && Mathf.Abs(floor + lift - mark.position.y) > 0.005f) { mark.position = new Vector3(mark.position.x, floor + lift, mark.position.z); count++; }
            }
            return count;
        }

        /// Pond, stream, plunge pool and waterfall get the shared dynamic water shader (sky reflection, moving
        /// ripples in several directions, depth colour, shore foam); the pond becomes a place to swim.
        private static int Water(Transform root)
        {
            var shader = Shader.Find("ContextSpeak/Water Surface");
            if (shader == null) { Debug.LogWarning("Shader ContextSpeak/Water Surface missing — water left unchanged."); return 0; }
            var path = EnvironmentSetup.Root + "/Water.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            var count = 0;
            Renderer pond = null;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                WaterSettings.WaterType type;
                if (renderer.name == "Pond water") { type = WaterSettings.WaterType.Pond; pond = renderer; }
                else if (renderer.name == "Stream water") type = WaterSettings.WaterType.Stream;
                else if (renderer.name == "Plunge pool") type = WaterSettings.WaterType.Pond;
                else if (renderer.name == "Falling water") type = WaterSettings.WaterType.Waterfall;
                else continue;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var settings = renderer.GetComponent<WaterSettings>();
                if (settings == null) settings = renderer.gameObject.AddComponent<WaterSettings>();
                settings.Type = type;
                settings.Surfaces = new Renderer[] { renderer };
                settings.Flow = type == WaterSettings.WaterType.Stream ? (Vector2)(new Vector2(renderer.transform.forward.x, renderer.transform.forward.z) * 0.9f) : Vector2.zero;
                settings.Apply();
                count++;
            }
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.requiresDepthTexture = true;     // depth colour and shore foam
                data.requiresColorTexture = true;     // refraction
            }
            if (pond != null)
            {
                foreach (var edge in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("Pond edge"))) edge.enabled = false;
                var bed = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Pond bed mud");
                var b = pond.bounds;
                var bottom = bed != null ? bed.bounds.max.y : b.max.y - 1.3f;
                var swim = new GameObject("Pond swimming").AddComponent<PondSwimming>();
                swim.transform.SetParent(root, false);
                swim.Water = new Bounds(new Vector3(b.center.x, (b.max.y + bottom) / 2, b.center.z), new Vector3(b.size.x, b.max.y - bottom, b.size.z));
            }
            return count;
        }

        private static void Zone(Transform root, Bounds cafe)
        {
            var zone = new GameObject("Lighting zone · greenhouse").AddComponent<LightingZone>();
            zone.transform.SetParent(root, false);
            zone.transform.position = new Vector3(cafe.center.x, cafe.max.y, cafe.center.z);
            zone.Size = new Vector3(cafe.size.x, 5.5f, cafe.size.z);
            zone.Type = LightingZone.Kind.Covered;           // glass roof: nearly outdoor light
            zone.DaylightExposure = 0.25f;
            zone.AmbientScale = 0.85f;
            var probe = new GameObject("Greenhouse reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(zone.transform, false);
            probe.transform.localPosition = new Vector3(0, 1.6f, 0);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.boxProjection = true;
            probe.size = new Vector3(cafe.size.x, 5.5f, cafe.size.z);
            probe.center = new Vector3(0, 1.1f, 0);
            probe.resolution = 128;
            probe.importance = 2;
            zone.Probe = probe;
        }

        private static void Shot(Transform root, string name, Vector3 position, Vector3 lookAt)
        {
            var mark = new GameObject(name).transform;
            mark.SetParent(root, false);
            mark.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }
    }
}
