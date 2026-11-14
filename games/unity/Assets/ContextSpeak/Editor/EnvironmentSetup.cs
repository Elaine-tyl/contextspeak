using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Environment > Setup Realistic World / Setup Realistic Lighting.
    ///
    /// Lighting (both menus): inspects the pipeline (URP only — never installs HDRP features), backs
    /// up the URP assets and quality settings, creates Performance / High / Ultra URP assets with a
    /// separate SSAO renderer, recalibrates exposure and the sky's ambient, configures sun shadows,
    /// a shared post-processing volume (ACES, subtle bloom, white balance, light vignette), indoor
    /// lighting zones + local reflection probes, reusable indoor light prefabs, and adds the
    /// environment controllers (DayNightCycle, WeatherManager, GlobalWindController, quality, F3 panel)
    /// to every life scene.
    /// World (Setup Realistic World only): PBR ground materials (world-projected, macro variation,
    /// rain puddles), terrain layers with normal + mask maps and height blending, the inland water
    /// shader with per-type WaterSettings, wind-animated LOD vegetation prefabs made from
    /// Assets/Art/nature (optimised Meshy models + generated grass tufts), a scatter of grass,
    /// wildflowers, rocks and park trees on green areas, and birds, butterflies and insects.
    /// Nothing imported is overwritten: new materials/prefabs live in Assets/ContextSpeak/Data/Environment.
    public static class EnvironmentSetup
    {
        public const string Root = "Assets/ContextSpeak/Data/Environment";
        public const string NatureArt = "Assets/Art/nature/";
        public const string GroundArt = "Assets/Art/ground/";
        public const string RealTextures = "Assets/Art/Textures/";
        public const string Hdri = "Assets/Art/HDRI/";
        private const string Generated = "Assets/Generated/";
        public const string EnvRendererPath = Generated + "EnvRenderer.asset";
        public static readonly string[] PresetNames = { "Performance", "High", "Ultra" };

        private static readonly List<string> report = new List<string>();
        private static readonly List<string> missing = new List<string>();

        [UnityEditor.MenuItem("ContextSpeak/Environment/Setup Realistic World")]
        public static void SetupWorld() => Run(true);

        [UnityEditor.MenuItem("ContextSpeak/Environment/Setup Realistic Lighting")]
        public static void SetupLighting() => Run(false);

        public static void Run(bool world)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            report.Clear();
            missing.Clear();
            var previous = EditorSceneManager.GetActiveScene().path;
            try
            {
                EditorUtility.DisplayProgressBar("ContextSpeak environment", "Inspecting the render pipeline…", 0.02f);
                if (!Inspect()) return;
                Directory.CreateDirectory(Root);
                Backup();
                EditorUtility.DisplayProgressBar("ContextSpeak environment", "Quality presets…", 0.08f);
                QualityPresets();
                CalibrateMaterials();
                LightPrefabs();
                Dictionary<string, Material> ground = null;
                Dictionary<string, GameObject> plants = null;
                if (world)
                {
                    EditorUtility.DisplayProgressBar("ContextSpeak environment", "Ground materials…", 0.15f);
                    ground = GroundMaterials();
                    EditorUtility.DisplayProgressBar("ContextSpeak environment", "Vegetation prefabs…", 0.25f);
                    plants = VegetationPrefabs();
                }
                var scenes = SkyInstaller.LifeScenes.Where(path => File.Exists(path)).ToArray();
                for (var i = 0; i < scenes.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("ContextSpeak environment", "Configuring " + Path.GetFileNameWithoutExtension(scenes[i]) + "…", 0.35f + 0.6f * i / scenes.Length);
                    var scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                    ConfigureScene(world, ground, plants);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (previous != "" && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
            var file = Root + "/SetupReport.md";
            File.WriteAllText(file, "# Environment setup report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n## Changes\n- " + string.Join("\n- ", report)
                + "\n\n## Missing / placeholders\n- " + (missing.Count == 0 ? "none" : string.Join("\n- ", missing.Distinct())) + "\n");
            AssetDatabase.ImportAsset(file);
            Debug.Log("ContextSpeak " + (world ? "realistic world" : "realistic lighting") + " setup done (" + report.Count + " changes). Report: " + file
                      + (missing.Count > 0 ? "\nMissing / placeholders:\n- " + string.Join("\n- ", missing.Distinct()) : ""));
            var errors = EnvironmentChecks.Run();
            if (errors.Length == 0) Debug.Log("ContextSpeak environment checks passed.");
            else Debug.LogError("ContextSpeak environment checks failed:\n- " + string.Join("\n- ", errors));
        }

        /// Set up only the open scene (used by the benchmark builder).
        public static void ConfigureCurrentScene(bool world)
        {
            report.Clear();
            missing.Clear();
            if (!Inspect()) return;
            Directory.CreateDirectory(Root);
            QualityPresets();
            LightPrefabs();
            ConfigureScene(world, world ? GroundMaterials() : null, world ? VegetationPrefabs() : null);
            if (missing.Count > 0) Debug.LogWarning("Environment setup (open scene) — missing / placeholders:\n- " + string.Join("\n- ", missing.Distinct()));
        }

        // ------------------------------------------------------------------ inspection and backup
        private static bool Inspect()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (!(pipeline is UniversalRenderPipelineAsset urp))
            {
                Debug.LogError("ContextSpeak environment setup: the project does not use URP (" + (pipeline != null ? pipeline.GetType().Name : "Built-in") + "). Nothing changed.");
                return false;
            }
            report.Add("Inspected: Unity " + Application.unityVersion + ", URP asset " + AssetDatabase.GetAssetPath(urp) + " (HDR " + urp.supportsHDR + ", MSAA " + urp.msaaSampleCount
                       + "×, shadows " + urp.mainLightShadowmapResolution + " px / " + urp.shadowDistance + " m / " + urp.shadowCascadeCount + " cascades), quality level '"
                       + QualitySettings.names[QualitySettings.GetQualityLevel()] + "'. Pipeline unchanged (URP).");
            return true;
        }

        private static void Backup()
        {
            var folder = Root + "/Backup";
            if (Directory.Exists(folder)) return;   // keep the very first backup (the pre-upgrade state)
            Directory.CreateDirectory(folder);
            foreach (var path in new[] { Generated + "CafePipeline.asset", Generated + "Renderer.asset", Generated + "ForestRenderer.asset" })
                if (File.Exists(path)) AssetDatabase.CopyAsset(path, folder + "/" + Path.GetFileName(path).Replace(".asset", ".backup.asset"));
            File.Copy("ProjectSettings/QualitySettings.asset", folder + "/QualitySettings.asset.txt", true);
            File.Copy("ProjectSettings/GraphicsSettings.asset", folder + "/GraphicsSettings.asset.txt", true);
            AssetDatabase.Refresh();
            report.Add("Backed up the URP assets and Quality/Graphics settings to " + folder + " (copy back to restore).");
        }

        // ------------------------------------------------------------------ quality presets
        private static void QualityPresets()
        {
            var cafe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "CafePipeline.asset");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "Renderer.asset");
            if (cafe == null || renderer == null) { CafeBuild.ConfigurePipeline(); cafe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "CafePipeline.asset"); renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "Renderer.asset"); }
            var env = EnvRenderer();
            var cafeRenderers = Renderers(cafe);
            UniversalRenderPipelineAsset Make(string name, float scale, int shadowRes, float distance, int cascades, string soft, int msaa, bool ssao, int lightsPerObject)
            {
                var path = Generated + "Env-" + name + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) { asset = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(asset, path); }
                asset.renderScale = scale;
                asset.supportsHDR = true;
                asset.msaaSampleCount = msaa;
                asset.supportsCameraDepthTexture = true;     // water depth colour, shore foam, SSAO
                asset.supportsCameraOpaqueTexture = true;    // water refraction
                asset.mainLightShadowmapResolution = shadowRes;
                asset.shadowDistance = distance;
                asset.shadowCascadeCount = cascades;
                if (cascades == 4) asset.cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
                if (cascades == 2) asset.cascade2Split = 0.25f;
                asset.cascadeBorder = 0.1f;
                asset.shadowDepthBias = 0.7f;
                asset.shadowNormalBias = 0.6f;
                var so = new SerializedObject(asset);
                Set(so, "m_MainLightShadowsSupported", true);
                Set(so, "m_AnyShadowsSupported", true);
                Set(so, "m_SoftShadowsSupported", true);
                SetEnum(so.FindProperty("m_SoftShadowQuality"), soft);
                var perObject = so.FindProperty("m_AdditionalLightsPerObjectLimit");
                if (perObject != null) perObject.intValue = lightsPerObject;
                Set(so, "m_AdditionalLightShadowsSupported", name != "Performance");
                var list = so.FindProperty("m_RendererDataList");
                list.arraySize = cafeRenderers.Count + 1;
                for (var i = 0; i < cafeRenderers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = cafeRenderers[i];
                list.GetArrayElementAtIndex(cafeRenderers.Count).objectReferenceValue = ssao ? (ScriptableRendererData)env : renderer;
                var defaultIndex = so.FindProperty("m_DefaultRendererIndex");
                if (defaultIndex != null) defaultIndex.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                return asset;
            }
            var performance = Make("Performance", 0.9f, 2048, 60, 2, "Low", 1, false, 2);
            var high = Make("High", 1f, 2048, 100, 4, "Medium", 2, true, 4);
            var ultra = Make("Ultra", 1f, 4096, 140, 4, "High", 4, true, 8);

            // Map Unity quality levels to the presets (Very Low…Medium → Performance, High/Very High → High, Ultra → Ultra).
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (settings == null) { missing.Add("ProjectSettings/QualitySettings.asset could not be loaded — presets created but not assigned"); return; }
            var quality = new SerializedObject(settings);
            var levels = quality.FindProperty("m_QualitySettings");
            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var name = level.FindPropertyRelative("name").stringValue;
                var asset = name == "Ultra" ? ultra : name == "High" || name == "Very High" ? high : performance;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;
                var lod = level.FindPropertyRelative("lodBias");
                if (lod != null) lod.floatValue = asset == ultra ? 2f : asset == high ? 1.4f : 0.8f;
                var anisotropic = level.FindPropertyRelative("anisotropicTextures");
                if (anisotropic != null) anisotropic.intValue = asset == performance ? 1 : 2;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            report.Add("Quality presets: Performance (0.9× scale, 2048 px shadows, 60 m, 2 cascades, soft low, SMAA, no SSAO), High (1.0×, 2048 px, 100 m, 4 cascades, soft medium, MSAA 2×, SSAO), "
                       + "Ultra (1.0×, 4096 px, 140 m, 4 cascades, soft high, MSAA 4×, SSAO). Assigned to Unity quality levels; HDR on; depth + opaque textures on (water).");
        }

        private static List<ScriptableRendererData> Renderers(UniversalRenderPipelineAsset asset)
        {
            var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
            var result = new List<ScriptableRendererData>();
            for (var i = 0; i < list.arraySize; i++) result.Add(list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData);
            return result;
        }

        /// Index of the environment (SSAO) renderer in every Env-* asset: after the CafePipeline renderers.
        public static int EnvRendererIndex()
        {
            var cafe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "CafePipeline.asset");
            return cafe != null ? Renderers(cafe).Count : 1;
        }

        private static UniversalRendererData EnvRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(EnvRendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, EnvRendererPath);
            }
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.copyDepthMode = CopyDepthMode.AfterOpaques;
            var feature = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault()
                          ?? AssetDatabase.LoadAllAssetsAtPath(EnvRendererPath).OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "Environment contact occlusion";
                AssetDatabase.AddObjectToAsset(feature, renderer);
            }
            if (!renderer.rendererFeatures.Contains(feature)) renderer.rendererFeatures.Add(feature);
            feature.SetActive(true);
            // SSAO is a Renderer Feature in URP (not a Volume override).
            var serialized = new SerializedObject(feature);
            var settings = serialized.FindProperty("m_Settings");
            SetEnum(settings.FindPropertyRelative("AOMethod"), "InterleavedGradient");
            settings.FindPropertyRelative("Downsample").boolValue = false;
            settings.FindPropertyRelative("AfterOpaque").boolValue = false;
            SetEnum(settings.FindPropertyRelative("Source"), "DepthNormals");
            SetEnum(settings.FindPropertyRelative("NormalSamples"), "Medium");
            SetEnum(settings.FindPropertyRelative("Samples"), "Medium");
            SetEnum(settings.FindPropertyRelative("BlurQuality"), "High");
            settings.FindPropertyRelative("Intensity").floatValue = 0.6f;
            settings.FindPropertyRelative("DirectLightingStrength").floatValue = 0.2f;
            settings.FindPropertyRelative("Radius").floatValue = 0.3f;
            settings.FindPropertyRelative("Falloff").floatValue = 40f;
            var samples = settings.FindPropertyRelative("SampleCount");
            if (samples != null) samples.intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var rendererSettings = new SerializedObject(renderer);
            var map = rendererSettings.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (var i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
            return renderer;
        }

        private static void Set(SerializedObject so, string name, bool value) { var p = so.FindProperty(name); if (p != null) p.boolValue = value; }

        private static void SetEnum(SerializedProperty property, string name)
        {
            if (property == null) return;
            var index = Array.IndexOf(property.enumNames, name);
            if (index >= 0) property.enumValueIndex = index;
        }

        // ------------------------------------------------------------------ materials
        /// Albedo calibration: real-world surfaces rarely exceed ~85 % reflectance; the generated flat
        /// colours (white plaster, pale paving) blew out at noon. Glass: clean, slightly tinted, not mirrors.
        private static void CalibrateMaterials()
        {
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material Life-", new[] { Data }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_BaseColor")) continue;
                var colour = material.GetColor("_BaseColor");
                if (colour.a < 0.99f)
                {
                    material.SetFloat("_Smoothness", 0.92f);
                    material.SetFloat("_Metallic", 0);
                    changed++;
                    continue;
                }
                var peak = Mathf.Max(colour.r, colour.g, colour.b);
                if (peak <= LifeBuild.MaxAlbedo) continue;
                var scale = LifeBuild.MaxAlbedo / peak;
                material.SetColor("_BaseColor", new Color(colour.r * scale, colour.g * scale, colour.b * scale, colour.a));
                EditorUtility.SetDirty(material);
                changed++;
            }
            report.Add("Calibrated " + changed + " generated flat-colour materials (albedo ≤ " + LifeBuild.MaxAlbedo + " sRGB; glass smoothness 0.92, non-metal).");
        }

        private const string Data = "Assets/ContextSpeak/Data";

        private static readonly (string name, string[] words, float tile)[] GroundTypes =
        {
            ("Grass", new[] { "lawn", "grass", "park", "meadow", "field" }, 2.5f),
            ("DrySoil", new[] { "dirt", "soil", "earth" }, 2.5f),
            ("WetSoil", new[] { "flowerbed", "flower bed", "planter soil" }, 2f),
            ("Mud", new[] { "mud" }, 3f),
            ("Gravel", new[] { "gravel", "path", "trail" }, 1.6f),
            ("Stone", new[] { "stone floor", "cobble", "rock floor" }, 3f),
            ("Moss", new[] { "moss" }, 2f),
            ("BeachSand", new[] { "sand", "beach", "dune" }, 3f),
            ("WetBeachSand", new[] { "wet sand" }, 3f),
            ("Asphalt", new[] { "road", "asphalt", "street surface", "car park", "parking", "runway", "apron", "taxiway" }, 3.5f),
            ("Concrete", new[] { "pavement", "sidewalk", "plaza", "forecourt", "concrete", "footpath" }, 3f),
            ("ForestDebris", new[] { "forest floor", "leaf litter" }, 2f),
            ("BrickPavers", new[] { "brick paving", "pavers" }, 0.8f),
        };

        /// Ground materials: real CC0 texture sets in Assets/Art/Textures/<Name>/ are used when present
        /// (Color/Albedo/Diffuse, Normal(GL), Roughness, AO, Height/Displacement); otherwise the generated
        /// procedural sets in Assets/Art/ground/.
        private static Dictionary<string, Material> GroundMaterials()
        {
            var result = new Dictionary<string, Material>();
            var shader = Shader.Find("ContextSpeak/Ground Lit");
            if (shader == null) { missing.Add("Shader ContextSpeak/Ground Lit did not compile — ground left unchanged"); return result; }
            Directory.CreateDirectory(Root + "/Ground");
            foreach (var (name, _, tile) in GroundTypes)
            {
                var (colour, normal, mask, source) = GroundTextures(name);
                if (colour == null) { missing.Add("Ground textures for " + name + " (neither " + RealTextures + name + "/ nor " + GroundArt + name + "_Color.png)"); continue; }
                var path = Root + "/Ground/Ground-" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.SetTexture("_BaseMap", colour);
                if (normal != null) material.SetTexture("_BumpMap", normal);
                if (mask != null) material.SetTexture("_MaskMap", mask);
                material.SetFloat("_Tile", tile);
                material.SetFloat("_Puddles", name == "Asphalt" || name == "Concrete" || name == "DrySoil" || name == "Gravel" ? 0.6f : name == "Mud" ? 1 : 0.15f);
                material.SetFloat("_Porosity", name == "Asphalt" ? 0.5f : name == "Concrete" ? 0.55f : 0.7f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                result[name] = material;
                if (source == "procedural") missing.Add("Ground " + name + ": using generated procedural textures — drop a CC0 photo-scanned set into " + RealTextures + name + "/ for higher realism");
            }
            report.Add("Ground materials (world-projected PBR, macro variation, puddles): " + string.Join(", ", result.Keys));
            return result;
        }

        private static (Texture2D colour, Texture2D normal, Texture2D mask, string source) GroundTextures(string name)
        {
            var folder = RealTextures + name;
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder).Where(f => !f.EndsWith(".meta")).ToArray();
                string Find(params string[] keys) => files.FirstOrDefault(f => keys.Any(k => Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(k)));
                var colourFile = Find("color", "albedo", "diff", "basecolor");
                if (colourFile != null)
                {
                    var normalFile = Find("normalgl", "nor_gl", "normal_gl", "normal", "nrm");
                    var mask = BuildMask(folder, name, Find("rough"), Find("ao", "ambientocclusion", "occlusion"), Find("height", "disp", "displacement"));
                    return (Import(colourFile, true, false), normalFile != null ? Import(normalFile, false, true) : null, mask, "cc0");
                }
            }
            var procedural = GroundArt + name;
            if (!File.Exists(procedural + "_Color.png")) return (null, null, null, "");
            return (Import(procedural + "_Color.png", true, false), Import(procedural + "_Normal.png", false, true), Import(procedural + "_Mask.png", false, false), "procedural");
        }

        private static Texture2D Import(string path, bool srgb, bool normal)
        {
            path = path.Replace('\\', '/');
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var dirty = false;
                if (normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (!normal && importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; dirty = true; }
                if (importer.anisoLevel < 4) { importer.anisoLevel = 4; dirty = true; }
                if (importer.maxTextureSize > 2048) { importer.maxTextureSize = 2048; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// Pack roughness / AO / height into the URP mask layout (R metallic 0, G AO, B height, A smoothness).
        private static Texture2D BuildMask(string folder, string name, string rough, string ao, string height)
        {
            var output = folder + "/" + name + "_Mask.generated.png";
            if (File.Exists(output)) return Import(output, false, false);
            Texture2D Read(string file)
            {
                if (file == null) return null;
                var texture = new Texture2D(2, 2);
                return texture.LoadImage(File.ReadAllBytes(file)) ? texture : null;
            }
            var r = Read(rough); var a = Read(ao); var h = Read(height);
            var size = r != null ? r.width : a != null ? a.width : h != null ? h.width : 1024;
            size = Mathf.Min(size, 2048);
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            float Sample(Texture2D t, int x, int y, float fallback) => t == null ? fallback : t.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size).r;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(0, Sample(a, x, y, 1), Sample(h, x, y, 0.5f), 1 - Sample(r, x, y, 0.8f));
            mask.SetPixels(pixels);
            File.WriteAllBytes(output, mask.EncodeToPNG());
            return Import(output, false, false);
        }

        // ------------------------------------------------------------------ vegetation prefabs
        public enum PlantKind { Tree, Bush, Flower, Grass, Rock, Prop }

        public static readonly Dictionary<string, (PlantKind kind, float response, float flex, float leafMask, float grassWave, float translucency)> Species =
            new Dictionary<string, (PlantKind, float, float, float, float, float)>
            {
                { "OakTree", (PlantKind.Tree, 0.8f, 0.8f, 1, 0, 0.45f) }, { "BroadleafTree", (PlantKind.Tree, 0.8f, 0.8f, 1, 0, 0.45f) },
                { "PineTree", (PlantKind.Tree, 0.55f, 0.35f, 1, 0, 0.2f) }, { "SpruceTree", (PlantKind.Tree, 0.55f, 0.35f, 1, 0, 0.2f) },
                { "TomatoPlant", (PlantKind.Bush, 1.0f, 0.8f, 1, 0, 0.5f) }, { "Chrysanthemum", (PlantKind.Bush, 1.1f, 0.9f, 0.4f, 0, 0.5f) },
                { "Wheat", (PlantKind.Grass, 1.5f, 0.7f, 0, 1, 0.5f) }, { "MeadowGrass", (PlantKind.Grass, 1.6f, 0.8f, 0, 1, 0.6f) }, { "Vallisneria", (PlantKind.Grass, 1.3f, 0.8f, 0, 1, 0.6f) },
                { "GrassFresh", (PlantKind.Grass, 1.6f, 0.8f, 0, 1, 0.6f) }, { "GrassMeadow", (PlantKind.Grass, 1.7f, 0.9f, 0, 1, 0.6f) }, { "GrassDry", (PlantKind.Grass, 1.6f, 0.8f, 0, 1, 0.5f) },
                { "GrassShort", (PlantKind.Grass, 1.2f, 0.6f, 0, 0.6f, 0.5f) }, { "Reeds", (PlantKind.Grass, 1.3f, 0.7f, 0, 1, 0.5f) },
                { "WildDaisies", (PlantKind.Flower, 1.5f, 0.9f, 0, 0.8f, 0.5f) }, { "WildButtercups", (PlantKind.Flower, 1.5f, 0.9f, 0, 0.8f, 0.5f) }, { "WildViolets", (PlantKind.Flower, 1.4f, 0.9f, 0, 0.6f, 0.5f) },
                { "BlossomA", (PlantKind.Flower, 1.3f, 1, 0, 0, 0.45f) }, { "BlossomB", (PlantKind.Flower, 1.3f, 1, 0, 0, 0.45f) }, { "BlossomC", (PlantKind.Flower, 1.3f, 1, 0, 0, 0.45f) },
                { "BlossomD", (PlantKind.Flower, 1.3f, 1, 0, 0, 0.45f) }, { "BlossomE", (PlantKind.Flower, 1.3f, 1, 0, 0, 0.45f) },
                { "RockFlowerA", (PlantKind.Flower, 1.4f, 1, 0, 0, 0.45f) }, { "RockFlowerB", (PlantKind.Flower, 1.4f, 1, 0, 0, 0.45f) },
                { "RockFlowerC", (PlantKind.Flower, 1.4f, 1, 0, 0, 0.45f) }, { "RockFlowerD", (PlantKind.Flower, 1.4f, 1, 0, 0, 0.45f) },
                { "Jasmine", (PlantKind.Flower, 1.3f, 1, 0, 0, 0.5f) }, { "Irises", (PlantKind.Flower, 1.2f, 0.9f, 0, 0, 0.5f) }, { "CrimsonBlossom", (PlantKind.Flower, 1.0f, 0.8f, 0, 0, 0.4f) },
                { "PeachRose", (PlantKind.Flower, 1.2f, 0.9f, 0, 0, 0.45f) }, { "Allium", (PlantKind.Flower, 1.3f, 0.9f, 0, 0, 0.45f) }, { "TropicalFlowers", (PlantKind.Flower, 1.1f, 0.9f, 0, 0, 0.45f) },
                { "TundraRock", (PlantKind.Rock, 0, 0, 0, 0, 0) }, { "MountainRocks", (PlantKind.Rock, 0, 0, 0, 0, 0) }, { "WideCliff", (PlantKind.Rock, 0, 0, 0, 0, 0) },
                { "Monolith", (PlantKind.Rock, 0, 0, 0, 0, 0) }, { "Mushroom", (PlantKind.Prop, 0, 0, 0, 0, 0) }, { "Bluebird", (PlantKind.Prop, 0, 0, 0, 0, 0) },
                { "HangingIvy1", (PlantKind.Flower, 1.0f, 1, 0, 0, 0.6f) }, { "HangingIvy2", (PlantKind.Flower, 1.0f, 1, 0, 0, 0.6f) }, { "HangingIvy3", (PlantKind.Flower, 1.0f, 1, 0, 0, 0.6f) },
                { "TerracottaPotSmall", (PlantKind.Prop, 0, 0, 0, 0, 0) }, { "TerracottaPotTall", (PlantKind.Prop, 0, 0, 0, 0, 0) },
                { "TerracottaPotWide", (PlantKind.Prop, 0, 0, 0, 0, 0) }, { "TerracottaJug", (PlantKind.Prop, 0, 0, 0, 0, 0) },
            };

        /// Real-world heights (m) for each plant/prop prefab: a zinnia stem is knee-high, not person-high.
        public static readonly Dictionary<string, float> RealHeight = new Dictionary<string, float>
        {
            { "OakTree", 13 }, { "BroadleafTree", 12 }, { "PineTree", 18 }, { "SpruceTree", 11 },
            { "BlossomA", 0.5f }, { "BlossomB", 0.5f }, { "BlossomC", 0.5f }, { "BlossomD", 0.5f }, { "BlossomE", 0.5f },
            { "RockFlowerA", 0.35f }, { "RockFlowerB", 0.35f }, { "RockFlowerC", 0.35f }, { "RockFlowerD", 0.35f },
            { "Chrysanthemum", 0.4f }, { "Jasmine", 0.5f }, { "Irises", 0.55f }, { "CrimsonBlossom", 0.35f }, { "PeachRose", 0.45f },
            { "Allium", 0.6f }, { "TropicalFlowers", 0.45f }, { "TomatoPlant", 0.9f }, { "Mushroom", 0.1f }, { "Bluebird", 0.15f },
            { "MeadowGrass", 0.55f }, { "Vallisneria", 0.7f }, { "Wheat", 1.0f },
        };

        public static string VegetationFolder => Root + "/Vegetation";

        public static Dictionary<string, GameObject> EnsureVegetationPrefabs() { missing.Clear(); return VegetationPrefabs(); }
        public static Dictionary<string, Material> EnsureGroundMaterials() { missing.Clear(); return GroundMaterials(); }

        private static Dictionary<string, GameObject> VegetationPrefabs()
        {
            var result = new Dictionary<string, GameObject>();
            var shader = Shader.Find("ContextSpeak/Vegetation Lit");
            if (shader == null) { missing.Add("Shader ContextSpeak/Vegetation Lit did not compile — no vegetation prefabs"); return result; }
            if (!Directory.Exists(NatureArt)) { missing.Add(NatureArt + " (optimised nature models) not found"); return result; }
            Directory.CreateDirectory(VegetationFolder);
            foreach (var file in Directory.GetFiles(NatureArt, "*.glb"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!Species.TryGetValue(name, out var spec)) continue;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                if (source == null) { missing.Add(file + " (not imported yet — reopen Unity or reimport)"); continue; }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = name;
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                var lods = new[] { 0, 1, 2 }.Select(level => renderers.Where(r => r.name.EndsWith("_LOD" + level)).ToArray()).ToArray();
                if (lods[0].Length == 0) lods[0] = renderers;
                var lod0 = lods[0][0].GetComponent<MeshFilter>().sharedMesh;
                var height = lod0 != null ? lod0.bounds.size.y : 1;
                // Scale the whole prefab to its real-world height (the mesh keeps its own units for the wind shader).
                var measured = height * lods[0][0].transform.lossyScale.y;
                if (RealHeight.TryGetValue(name, out var real) && measured > 0.001f) instance.transform.localScale = Vector3.one * (real / measured);
                // One wind material per source material (LODs that were re-baked have their own textures/UVs).
                var converted = new Dictionary<Material, Material>();
                Material Convert(Material original)
                {
                    if (original != null && converted.TryGetValue(original, out var done)) return done;
                    var suffix = original == null ? "" : "-" + string.Concat(original.name.Where(char.IsLetterOrDigit));
                    var materialPath = VegetationFolder + "/" + name + suffix + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                    Texture Tex(params string[] names)
                    {
                        if (original == null) return null;
                        foreach (var property in names) if (original.HasProperty(property) && original.GetTexture(property) != null) return original.GetTexture(property);
                        return null;
                    }
                    var baseMap = Tex("baseColorTexture", "_BaseMap", "_MainTex");
                    material.SetTexture("_BaseMap", baseMap);
                    var tint = original != null && original.HasProperty("baseColorFactor") ? original.GetColor("baseColorFactor") : Color.white;
                    material.SetColor("_BaseColor", baseMap != null ? Color.white : tint);
                    var normal = Tex("normalTexture", "_BumpMap");
                    material.SetTexture("_BumpMap", normal);
                    var rough = Tex("metallicRoughnessTexture", "_MetallicGlossMap");
                    material.SetTexture("_RoughnessMap", rough);
                    material.SetFloat("_UseRoughnessMap", rough != null ? 1 : 0);
                    material.SetFloat("_ObjectHeight", name.StartsWith("HangingIvy") ? -height : height);   // ivy hangs below its pivot
                    material.SetFloat("_WindResponse", spec.response);
                    material.SetFloat("_Flex", spec.flex);
                    material.SetFloat("_LeafMask", spec.leafMask);
                    material.SetFloat("_GrassWave", spec.grassWave);
                    material.SetFloat("_Translucency", spec.translucency);
                    material.SetFloat("_HueVariation", spec.kind == PlantKind.Rock || spec.kind == PlantKind.Prop ? 0.03f : 0.1f);
                    material.SetFloat("_Porosity", spec.kind == PlantKind.Rock ? 0.6f : 0.35f);
                    material.SetFloat("_Cull", spec.kind == PlantKind.Rock || spec.kind == PlantKind.Prop ? 2 : 0);
                    material.enableInstancing = true;
                    EditorUtility.SetDirty(material);
                    if (original != null) converted[original] = material;
                    return material;
                }
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(Convert).ToArray();
                    var small = spec.kind == PlantKind.Grass || spec.kind == PlantKind.Flower;
                    // Small plants don't cast shadows (cost, flicker); trees, bushes and rocks do.
                    renderer.shadowCastingMode = small ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                var group = instance.AddComponent<LODGroup>();
                var screen = spec.kind == PlantKind.Tree ? new[] { 0.3f, 0.09f, 0.008f } : spec.kind == PlantKind.Rock ? new[] { 0.2f, 0.06f, 0.006f } : new[] { 0.09f, 0.035f, 0.012f };
                group.SetLODs(new[] { 0, 1, 2 }.Where(level => lods[level].Length > 0).Select(level => new LOD(screen[level], lods[level])).ToArray());
                group.fadeMode = LODFadeMode.None;
                if (spec.kind == PlantKind.Tree)
                {
                    var trunk = instance.AddComponent<CapsuleCollider>();
                    trunk.radius = Mathf.Clamp(height * 0.025f, 0.2f, 0.45f);
                    trunk.height = Mathf.Min(height, 6);
                    trunk.center = new Vector3(0, trunk.height / 2, 0);
                }
                if (spec.kind == PlantKind.Rock && lods[lods[2].Length > 0 ? 2 : 0].Length > 0)
                {
                    var collider = instance.AddComponent<MeshCollider>();
                    collider.sharedMesh = lods[lods[2].Length > 0 ? 2 : 0][0].GetComponent<MeshFilter>().sharedMesh;
                    collider.convex = true;
                }
                foreach (var transform in instance.GetComponentsInChildren<Transform>()) transform.gameObject.isStatic = false;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, VegetationFolder + "/" + name + ".prefab");
                UnityEngine.Object.DestroyImmediate(instance);
                result[name] = prefab;
            }
            report.Add("Vegetation prefabs (textured PBR, wind, translucency, 3 LODs): " + string.Join(", ", result.Keys.OrderBy(n => n)));
            foreach (var expected in new[] { "OakTree", "PineTree", "SpruceTree", "GrassFresh", "WildDaisies" })
                if (!result.ContainsKey(expected)) missing.Add("Nature model " + expected + ".glb missing from " + NatureArt);
            return result;
        }

        // ------------------------------------------------------------------ light prefabs
        private static void LightPrefabs()
        {
            var folder = Root + "/Lights";
            Directory.CreateDirectory(folder);
            var glowShader = Shader.Find("Universal Render Pipeline/Lit");
            void Make(string name, float kelvin, float intensity, float range, SkyLightSwitch.Kind kind, Vector3 bulb, float shade)
            {
                var root = new GameObject(name);
                var lamp = new GameObject("Light").AddComponent<Light>();
                lamp.transform.SetParent(root.transform, false);
                lamp.type = LightType.Point;
                lamp.useColorTemperature = true;
                lamp.colorTemperature = kelvin;
                lamp.color = Color.white;
                lamp.intensity = intensity;
                lamp.range = range;
                lamp.shadows = LightShadows.None;   // keep local lights cheap; enable selectively after profiling
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.name = "Bulb";
                glow.transform.SetParent(root.transform, false);
                glow.transform.localScale = bulb;
                var materialPath = folder + "/" + name + " bulb.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(glowShader); AssetDatabase.CreateAsset(material, materialPath); }
                var tint = Mathf.CorrelatedColorTemperatureToRGB(kelvin);
                material.SetColor("_BaseColor", tint);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", tint * 2.5f);
                glow.GetComponent<Renderer>().sharedMaterial = material;
                if (shade > 0)
                {
                    var hood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    UnityEngine.Object.DestroyImmediate(hood.GetComponent<Collider>());
                    hood.name = "Shade";
                    hood.transform.SetParent(root.transform, false);
                    hood.transform.localPosition = new Vector3(0, bulb.y * 0.6f, 0);
                    hood.transform.localScale = new Vector3(shade, 0.12f, shade);
                    hood.GetComponent<Renderer>().sharedMaterial = LifeBuild.Surface(LifeBuild.Hex("2B2A28"), 0.4f);
                }
                var light = root.AddComponent<SkyLightSwitch>();
                light.Mode = kind;
                light.Lights = new[] { lamp };
                light.Glow = new[] { glow.GetComponent<Renderer>() };
                light.GlowColour = tint;
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
            Make("Warm Pendant", 2700, 1.4f, 4.5f, SkyLightSwitch.Kind.Interior, new Vector3(0.14f, 0.18f, 0.14f), 0.4f);
            Make("Neutral Ceiling Panel", 4000, 1.6f, 6f, SkyLightSwitch.Kind.Interior, new Vector3(0.6f, 0.04f, 0.6f), 0);
            Make("Table Lamp", 2700, 0.8f, 2.5f, SkyLightSwitch.Kind.Interior, new Vector3(0.1f, 0.12f, 0.1f), 0.3f);
            Make("Counter Spot", 3000, 1.2f, 3f, SkyLightSwitch.Kind.Interior, new Vector3(0.08f, 0.08f, 0.08f), 0.18f);
            Make("Garden Light", 3000, 0.9f, 5f, SkyLightSwitch.Kind.Street, new Vector3(0.12f, 0.12f, 0.12f), 0);
            Make("Shop Window Light", 3500, 1.2f, 5f, SkyLightSwitch.Kind.Interior, new Vector3(1.2f, 0.04f, 0.1f), 0);
            report.Add("Reusable light prefabs (colour temperature, auto day/night via SkyLightSwitch, no shadows by default): " + folder);
        }

        // ------------------------------------------------------------------ per scene
        private static void ConfigureScene(bool world, Dictionary<string, Material> ground, Dictionary<string, GameObject> plants)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var sky = UnityEngine.Object.FindFirstObjectByType<SkySystem>() ?? SkyInstaller.Install();
            sky.BaseExposure = 0;
            if (sky.SkyProbe != null) sky.SkyProbe.resolution = 128;
            HdriSkies(sky);
            sky.ResetLook();
            EditorUtility.SetDirty(sky);
            if (sky.Sun != null)
            {
                sky.Sun.shadows = LightShadows.Soft;
                var data = sky.Sun.GetComponent<UniversalAdditionalLightData>() ?? sky.Sun.gameObject.AddComponent<UniversalAdditionalLightData>();
                data.usePipelineSettings = true;
                data.softShadowQuality = SoftShadowQuality.UsePipelineSettings;
            }

            // A lighting-only run keeps the vegetation and wildlife that Setup Realistic World scattered.
            Transform keptNature = null;
            foreach (var old in UnityEngine.Object.FindObjectsByType<EnvironmentManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var nature = old.transform.Find("Nature (generated)");
                if (!world && nature != null) { nature.SetParent(null, true); keptNature = nature; }
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var root = new GameObject("Environment (realistic)");
            if (keptNature != null) keptNature.SetParent(root.transform, true);
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 40;
            var profilePath = Root + "/Post-" + scene.name + ".asset";
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.12f);
            bloom.scatter.Override(0.65f);
            profile.Add<WhiteBalance>().temperature.Override(0);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.45f);
            var adjust = profile.Add<ColorAdjustments>();
            adjust.saturation.Override(-4);       // the flat colours read slightly oversaturated under ACES
            adjust.contrast.Override(4);
            LifeBuild.Replace(profile, profilePath);
            volume.sharedProfile = profile;
            var manager = root.AddComponent<EnvironmentManager>();
            manager.PostVolume = volume;
            root.AddComponent<DayNightCycle>();
            var weather = root.AddComponent<WeatherManager>();
            weather.Current = sky.Weather == SkyWeather.Rain ? WeatherManager.State.LightRain : sky.Weather == SkyWeather.Overcast ? WeatherManager.State.Overcast
                : sky.Weather == SkyWeather.Cloudy ? WeatherManager.State.PartlyCloudy : WeatherManager.State.Clear;
            root.AddComponent<GlobalWindController>().SceneExposure = scene.name == "TropicalBeach" ? 1.3f : scene.name == "ForestSlice" ? 0.7f : 1;
            root.AddComponent<EnvironmentQualityManager>();
            root.AddComponent<EnvironmentDebugPanel>();

            // Cameras: depth + opaque textures for water; the SSAO renderer unless the scene has its own.
            var envIndex = EnvRendererIndex();
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data == null || data.renderType == CameraRenderType.Overlay) continue;
                data.requiresDepthTexture = true;
                data.requiresColorTexture = true;
                data.renderPostProcessing = true;
                camera.allowHDR = true;
                var so = new SerializedObject(data);
                var index = so.FindProperty("m_RendererIndex");
                if (index != null && index.intValue <= 0) { index.intValue = envIndex; so.ApplyModifiedPropertiesWithoutUndo(); }
                EditorUtility.SetDirty(data);
            }

            // Interior lighting zones with local reflection probes.
            var interior = scene.name == "Hospital" || scene.name == "Restaurant" || scene.name == "AircraftCabin";
            var zones = 0;
            if (interior)
            {
                var floors = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name.ToLowerInvariant().Contains("floor") && r.bounds.size.x * r.bounds.size.z > 20).ToArray();
                foreach (var floor in floors.Take(6))
                {
                    var bounds = floor.bounds;
                    var zone = new GameObject("Lighting zone · " + floor.name).AddComponent<LightingZone>();
                    zone.transform.SetParent(root.transform, false);
                    zone.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.05f, bounds.center.z);
                    zone.Size = new Vector3(bounds.size.x, 4.5f, bounds.size.z);
                    var probe = new GameObject("Room reflections").AddComponent<ReflectionProbe>();
                    probe.transform.SetParent(zone.transform, false);
                    probe.transform.localPosition = new Vector3(0, 1.6f, 0);
                    probe.mode = ReflectionProbeMode.Realtime;
                    probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                    probe.boxProjection = true;
                    probe.size = new Vector3(bounds.size.x, 4.5f, bounds.size.z);
                    probe.center = new Vector3(0, 0.6f, 0);
                    probe.resolution = 128;
                    probe.importance = 2;
                    zone.Probe = probe;
                    zones++;
                }
            }

            var groundCount = 0; var waterCount = 0; var planted = 0;
            if (world)
            {
                groundCount = ApplyGround(ground);
                TerrainLayers(ground);
                waterCount = ApplyWater();
                if (!interior && scene.name != "AircraftCabin") planted = NatureScatter.Scatter(root.transform, plants, ground, scene.name);
            }
            SkySurfacesWithoutUpgraded();
            sky.ApplyNow();
            report.Add(scene.name + ": exposure recalibrated, environment controllers + post volume" + (zones > 0 ? ", " + zones + " lighting zones with local reflection probes" : "")
                       + (world ? ", " + groundCount + " ground surfaces, " + waterCount + " water bodies, " + planted + " plants/props" : ""));
        }

        private static void HdriSkies(SkySystem sky)
        {
            if (!Directory.Exists(Hdri)) { missing.Add("HDRI skies (optional): put day/sunset/night .hdr or .exr files in " + Hdri + " (e.g. Poly Haven, CC0) — the procedural sky is used meanwhile"); return; }
            Cubemap Load(string word)
            {
                var file = Directory.GetFiles(Hdri).FirstOrDefault(f => (f.EndsWith(".hdr") || f.EndsWith(".exr")) && Path.GetFileName(f).ToLowerInvariant().Contains(word));
                if (file == null) return null;
                file = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(file) is TextureImporter importer && importer.textureShape != TextureImporterShape.TextureCube)
                {
                    importer.textureShape = TextureImporterShape.TextureCube;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Cubemap>(file);
            }
            sky.HdriDay = Load("day") ?? sky.HdriDay;
            sky.HdriSunset = Load("sunset") ?? sky.HdriSunset;
            sky.HdriNight = Load("night") ?? sky.HdriNight;
        }

        private static string GroundType(Renderer renderer)
        {
            var name = renderer.name.ToLowerInvariant();
            if (name.Contains("line") || name.Contains("marking") || name.Contains("stripe") || name.Contains("crossing") || name.Contains("kerb") || name.Contains("curb") || name.Contains("sign")) return null;
            foreach (var (type, words, _) in GroundTypes.Reverse())
                if (words.Any(word => name.Contains(word))) return type;
            return null;
        }

        private static int ApplyGround(Dictionary<string, Material> ground)
        {
            if (ground == null || ground.Count == 0) return 0;
            var count = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var bounds = renderer.bounds;
                if (bounds.size.y > 1.2f || bounds.size.x * bounds.size.z < 3) continue;      // flat, walkable surfaces only
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name.StartsWith("ContextSpeak/")) continue;
                if (renderer.GetComponentInParent<LODGroup>() != null) continue;
                var type = GroundType(renderer);
                if (type == null || !ground.TryGetValue(type, out var material)) continue;
                // Keep the authored colour as a gentle tint so districts stay distinguishable.
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                count++;
            }
            return count;
        }

        private static void TerrainLayers(Dictionary<string, Material> ground)
        {
            if (ground == null) return;
            foreach (var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                var data = terrain.terrainData;
                var layers = data.terrainLayers;
                var changed = false;
                for (var i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    if (layer == null) continue;
                    var name = layer.name.ToLowerInvariant();
                    var type = name.Contains("wetsand") ? "WetBeachSand" : name.Contains("sand") ? "BeachSand" : name.Contains("grass") ? "Grass" : name.Contains("rock") || name.Contains("stone") ? "Stone"
                        : name.Contains("path") ? "Gravel" : name.Contains("soil") || name.Contains("dirt") ? "DrySoil" : name.Contains("moss") ? "Moss" : null;
                    if (type == null || !ground.TryGetValue(type, out var material)) continue;
                    var path = Root + "/Ground/Terrain-" + type + ".terrainlayer";
                    var upgraded = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                    if (upgraded == null) { upgraded = new TerrainLayer(); AssetDatabase.CreateAsset(upgraded, path); }
                    upgraded.diffuseTexture = material.GetTexture("_BaseMap") as Texture2D;
                    upgraded.normalMapTexture = material.GetTexture("_BumpMap") as Texture2D;
                    upgraded.maskMapTexture = material.GetTexture("_MaskMap") as Texture2D;
                    upgraded.tileSize = Vector2.one * material.GetFloat("_Tile");
                    upgraded.normalScale = 1;
                    upgraded.maskMapRemapMin = Vector4.zero;
                    upgraded.maskMapRemapMax = Vector4.one;
                    upgraded.smoothness = 1;
                    upgraded.metallic = 0;
                    EditorUtility.SetDirty(upgraded);
                    layers[i] = upgraded;
                    changed = true;
                }
                if (!changed) continue;
                data.terrainLayers = layers;
                var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (shader != null)
                {
                    var path = Root + "/Ground/TerrainLit-" + terrain.name.Replace(" ", "") + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                    material.SetFloat("_EnableHeightBlend", 1);
                    material.SetFloat("_HeightTransition", 0.25f);
                    material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                    material.EnableKeyword("_MASKMAP");
                    material.EnableKeyword("_NORMALMAP");
                    EditorUtility.SetDirty(material);
                    terrain.materialTemplate = material;
                }
                EditorUtility.SetDirty(data);
                report.Add("Terrain " + terrain.name + ": layers upgraded to PBR (normal + mask maps, height-based blending).");
            }
        }

        private static int ApplyWater()
        {
            var shader = Shader.Find("ContextSpeak/Water Surface");
            if (shader == null) { missing.Add("Shader ContextSpeak/Water Surface did not compile — inland water unchanged"); return 0; }
            var path = Root + "/Water.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.enableInstancing = true;
            var count = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var name = renderer.name.ToLowerInvariant();
                if (renderer.GetComponent<OceanWaves>() != null || renderer.sharedMaterial == null) continue;
                var shaderName = renderer.sharedMaterial.shader.name;
                if (shaderName.StartsWith("ContextSpeak/") && shaderName != "ContextSpeak/Water Surface") continue;     // forest water, ocean keep their own
                if (name.Contains("dispenser") || name.Contains("falling") || name.Contains("bottle") || name.Contains("glass") || name.Contains("cup") || name.Contains("fountain jet")) continue;
                WaterSettings.WaterType? type = name.Contains("pool") ? WaterSettings.WaterType.Pool : name.Contains("river") || name.Contains("canal") ? WaterSettings.WaterType.River
                    : name.Contains("stream") || name.Contains("brook") ? WaterSettings.WaterType.Stream : name.Contains("lake") ? WaterSettings.WaterType.Lake
                    : name.Contains("waterfall") ? WaterSettings.WaterType.Waterfall : name.Contains("pond") || name.Contains("water") ? WaterSettings.WaterType.Pond : (WaterSettings.WaterType?)null;
                if (type == null) continue;
                if (type != WaterSettings.WaterType.Waterfall && renderer.bounds.size.y > 1.5f && renderer.bounds.size.x * renderer.bounds.size.z < 2) continue;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                // A water box: drop its collider-blocking top slightly below eye-catching edges is the builder's job; just set up the look.
                var settings = renderer.GetComponent<WaterSettings>() ?? renderer.gameObject.AddComponent<WaterSettings>();
                settings.Type = type.Value;
                settings.Surfaces = new Renderer[] { renderer };
                settings.Flow = type == WaterSettings.WaterType.River ? new Vector2(0.6f, 0.2f) : type == WaterSettings.WaterType.Stream ? new Vector2(0.9f, 0.3f) : Vector2.zero;
                settings.Apply();
                count++;
            }
            return count;
        }

        private static void SkySurfacesWithoutUpgraded()
        {
            // SkySurfaces darkens ground/water by tinting _BaseColor; the new shaders handle rain themselves.
            foreach (var surfaces in UnityEngine.Object.FindObjectsByType<SkySurfaces>(FindObjectsSortMode.None))
            {
                bool Upgraded(Renderer r) => r != null && r.sharedMaterial != null && r.sharedMaterial.shader.name.StartsWith("ContextSpeak/") && r.sharedMaterial.shader.name != "ContextSpeak/Forest Water";
                surfaces.Ground = surfaces.Ground.Where(r => !Upgraded(r)).ToArray();
                surfaces.Water = surfaces.Water.Where(r => !Upgraded(r)).ToArray();
                EditorUtility.SetDirty(surfaces);
            }
        }
    }
}
