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
    /// ContextSpeak > Sky & Weather: installs the URP dynamic sky into a scene and wires the
    /// gameplay side (street lamps, headlights, interior lights, windows, water, wet roads,
    /// pedestrians, weather sound, HUD). Re-running replaces the previous installation.
    public static class SkyInstaller
    {
        public const string Folder = "Assets/ContextSpeak/Sky";
        public const string RootName = "Sky & Weather";
        public static readonly string[] LifeScenes =
        {
            "Assets/Generated/MetroCity.unity", "Assets/Generated/ForestSlice.unity",
            "Assets/ContextSpeak/Scenes/Hospital.unity", "Assets/ContextSpeak/Scenes/Restaurant.unity",
            "Assets/ContextSpeak/Scenes/Airport.unity", "Assets/ContextSpeak/Scenes/AircraftCabin.unity", "Assets/ContextSpeak/Scenes/ArrivalAirport.unity",
            "Assets/ContextSpeak/Scenes/TropicalBeach.unity", "Assets/ContextSpeak/Scenes/WildlifeZoo.unity",
        };

        [UnityEditor.MenuItem("ContextSpeak/Sky & Weather/Install in Open Scene")]
        public static void InstallOpen()
        {
            var sky = Install();
            EditorSceneManager.MarkSceneDirty(sky.gameObject.scene);
            Selection.activeObject = sky;
            Debug.Log("Sky & Weather installed in " + sky.gameObject.scene.name + ". Save the scene; press F2 in Play mode for the sky panel.");
        }

        [UnityEditor.MenuItem("ContextSpeak/Sky & Weather/Install in All Life Scenes")]
        public static void InstallAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var done = new List<string>();
            foreach (var path in LifeScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.LogWarning("Sky: scene not built yet, skipped: " + path); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Install();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done.Add(scene.name);
            }
            Debug.Log("Sky & Weather installed in: " + string.Join(", ", done));
        }

        public static SkySystem Install()
        {
            Directory.CreateDirectory(Folder);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var interior = scene.name == "Hospital" || scene.name == "Restaurant" || scene.name == "AircraftCabin";
            var forest = Object.FindFirstObjectByType<ContextSpeak.Slice.SliceGame>() != null;

            var old = Object.FindFirstObjectByType<SkySystem>();
            var baseFog = old != null ? old.BaseFogDensity : RenderSettings.fog && RenderSettings.fogDensity > 0 ? RenderSettings.fogDensity : interior ? 0.0015f : 0.004f;
            var baseExposure = old != null ? old.BaseExposure : ExistingExposure();
            var north = old != null ? old.NorthOffset : RenderSettings.sun != null ? RenderSettings.sun.transform.eulerAngles.y : -38;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (var leftover in Object.FindObjectsByType<SkyLightSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(leftover);
            foreach (var added in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(light => light.name == "Sky lamp light" || light.name == "Sky headlight").ToArray())
                Object.DestroyImmediate(added.gameObject);

            var root = new GameObject(RootName);
            var sky = root.AddComponent<SkySystem>();
            sky.enabled = false;   // wire everything before the first Apply
            sky.SkyMaterial = SkyMaterial();
            sky.Profiles = Profiles();
            sky.CloudCookies = Cookies();
            sky.BaseFogDensity = baseFog;
            sky.BaseExposure = baseExposure;
            sky.NorthOffset = north;
            sky.Interior = interior;
            sky.SyncWithForestClock = forest;
            sky.TimeOfDay = scene.name == "Restaurant" ? 17.9f : scene.name == "Hospital" ? 10 : 8.5f;

            // Sun: the scene's main directional light (or a new one); moon: a second, dim one.
            var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(light => light.type == LightType.Directional).OrderByDescending(light => light.intensity).FirstOrDefault();
            if (sun == null) { sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; }
            sun.shadows = LightShadows.Soft;
            if (sun.GetComponent<UniversalAdditionalLightData>() == null) sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            sky.Sun = sun;
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.transform.SetParent(root.transform, false);
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.55f;
            moon.gameObject.AddComponent<UniversalAdditionalLightData>();
            sky.Moon = moon;

            // Sky-only reflection probe: cheap, keeps glass, water and wet roads reflecting the real sky.
            var probe = new GameObject("Sky reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root.transform, false);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = Vector3.one * 2000;
            probe.resolution = 128;
            probe.cullingMask = 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.importance = 0;
            sky.SkyProbe = probe;

            // Exposure: night and storms open up a little so they read as dark, not black.
            var volume = new GameObject("Sky exposure").AddComponent<Volume>();
            volume.transform.SetParent(root.transform, false);
            volume.isGlobal = true;
            volume.priority = 50;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var adjust = profile.Add<ColorAdjustments>();
            adjust.postExposure.Override(baseExposure);
            LifeBuild.Replace(profile, Folder + "/SkyExposure-" + scene.name + ".asset");
            volume.sharedProfile = profile;
            sky.ExposureVolume = volume;

            // Rain (the Forest Café keeps its own rain, which now follows the sky).
            var forestRain = forest && Object.FindFirstObjectByType<ContextSpeak.Slice.Atmosphere>() is var atmosphere && atmosphere != null && atmosphere.Rain != null;
            if (!forestRain) sky.Rain = RainSystem(root.transform);

            WireLights(root.transform, interior);
            var surfaces = root.AddComponent<SkySurfaces>();
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            surfaces.Water = renderers.Where(renderer => renderer.name.ToLowerInvariant().Contains("water") && renderer.sharedMaterial != null
                && renderer.sharedMaterial.shader.name != "ContextSpeak/Forest Water" && !renderer.name.Contains("Falling") && !renderer.name.Contains("dispenser")).ToArray();
            var groundWords = new[] { "road", "pavement", "sidewalk", "plaza", "ground", "deck", "promenade", "crossing", "kerb", "path", "trail", "pier", "platform" };
            surfaces.Ground = interior ? renderers.Where(renderer => renderer.name.Contains("plaza") || renderer.name.Contains("promenade") || renderer.name.Contains("Terrace deck")).ToArray()
                : renderers.Where(renderer => groundWords.Any(word => renderer.name.ToLowerInvariant().Contains(word))).Take(2500).ToArray();
            root.AddComponent<SkyAudio>().Interior = interior;
            root.AddComponent<SkyHUD>();
            if (Object.FindFirstObjectByType<ContextSpeak.City.CityPedestrian>() != null) root.AddComponent<SkyCrowd>();

            RenderSettings.skybox = sky.SkyMaterial;
            sky.enabled = true;     // OnEnable → ApplyNow
            sky.ApplyNow();
            foreach (var item in Object.FindObjectsByType<SkyLightSwitch>(FindObjectsSortMode.None)) item.Refresh();
            surfaces.Refresh();
            return sky;
        }

        private static float ExistingExposure()
        {
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).OrderByDescending(item => item.priority))
                if (volume.sharedProfile != null && volume.sharedProfile.TryGet<ColorAdjustments>(out var adjust) && adjust.postExposure.overrideState)
                    return adjust.postExposure.value;
            return 0;
        }

        private static void WireLights(Transform root, bool interior)
        {
            var warm = new Color(1f, 0.8f, 0.55f);
            // Street lamps: every lamp head glows; every second one also casts real light (laptop budget).
            var lamps = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(item => item.name == "Street lamp")
                .OrderBy(item => item.position.x).ThenBy(item => item.position.z).ToArray();
            var lit = 0;
            for (var index = 0; index < lamps.Length; index++)
            {
                var lamp = lamps[index];
                var head = lamp.GetComponentsInChildren<Renderer>().FirstOrDefault(item => item.name == "Lamp head");
                var switcher = lamp.gameObject.AddComponent<SkyLightSwitch>();
                switcher.Mode = SkyLightSwitch.Kind.Street;
                switcher.GlowColour = warm;
                switcher.Glow = head != null ? new[] { head } : new Renderer[0];
                if (index % 2 == 0 && lit < 70 && head != null)
                {
                    var light = new GameObject("Sky lamp light").AddComponent<Light>();
                    light.transform.SetParent(lamp, false);
                    light.transform.position = head.bounds.center - Vector3.up * 0.25f;
                    light.type = LightType.Point;
                    light.range = 14;
                    light.intensity = 2.6f;
                    light.color = warm;
                    light.shadows = LightShadows.None;
                    switcher.Lights = new[] { light };
                    lit++;
                }
            }
            // Vehicle headlights: glow on the model's headlight material plus one spot light.
            var vehicles = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(item => item is ContextSpeak.City.CityCar || item is ContextSpeak.City.CityTransit || item is ContextSpeak.City.CityDrivable)
                .Select(item => item.transform).Distinct().ToArray();
            var beams = 0;
            foreach (var vehicle in vehicles)
            {
                var heads = vehicle.GetComponentsInChildren<Renderer>().Where(item => item.sharedMaterials.Any(material => material != null && material.name.StartsWith("Headlight"))).ToArray();
                var switcher = vehicle.gameObject.AddComponent<SkyLightSwitch>();
                switcher.Mode = SkyLightSwitch.Kind.Headlight;
                switcher.GlowColour = new Color(1f, 0.95f, 0.85f);
                switcher.Glow = heads;
                var renderers = vehicle.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0 || beams >= 30) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var front = heads.Length > 0 ? heads.Select(item => item.bounds.center).Aggregate(Vector3.zero, (sum, point) => sum + point) / heads.Length
                    : bounds.center + vehicle.forward * bounds.extents.magnitude * 0.5f;
                var beam = new GameObject("Sky headlight").AddComponent<Light>();
                beam.transform.SetParent(vehicle, false);
                beam.transform.position = new Vector3(front.x, bounds.min.y + 0.7f, front.z) + vehicle.forward * 0.3f;
                beam.transform.rotation = Quaternion.LookRotation(vehicle.forward + Vector3.down * 0.12f);
                beam.type = LightType.Spot;
                beam.spotAngle = 70;
                beam.innerSpotAngle = 40;
                beam.range = 20;
                beam.intensity = 3.5f;
                beam.color = new Color(1f, 0.96f, 0.88f);
                beam.shadows = LightShadows.None;
                switcher.Lights = new[] { beam };
                beams++;
            }
            // Interior lights: relatively brighter after dark (rooms read as warm and lit from outside).
            var buildings = Object.FindObjectsByType<ContextSpeak.City.CityBuilding>(FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                var switcher = building.gameObject.AddComponent<SkyLightSwitch>();
                switcher.Mode = SkyLightSwitch.Kind.Interior;
                switcher.Lights = building.Lights;
            }
            if (buildings.Length == 0)
            {
                var ceiling = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(light => light.type == LightType.Point && light.name == "Ceiling light").ToArray();
                if (ceiling.Length > 0)
                {
                    var switcher = new GameObject("Interior lights (sky)").AddComponent<SkyLightSwitch>();
                    switcher.transform.SetParent(root, false);
                    switcher.Mode = SkyLightSwitch.Kind.Interior;
                    switcher.Lights = ceiling;
                }
            }
            // Windows glow warm at night when seen from the street.
            if (!interior)
            {
                var glass = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(renderer => (renderer.name == "Glass" || renderer.name.Contains("Window glass")) && renderer.sharedMaterial != null).ToArray();
                if (glass.Length > 0)
                {
                    var windows = new GameObject("Window glow (sky)").AddComponent<SkyLightSwitch>();
                    windows.transform.SetParent(root, false);
                    windows.Mode = SkyLightSwitch.Kind.Window;
                    windows.Glow = glass;
                }
            }
        }

        private static ParticleSystem RainSystem(Transform parent)
        {
            var rain = new GameObject("Rain").AddComponent<ParticleSystem>();
            rain.transform.SetParent(parent, false);
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.4f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startColor = new Color(0.78f, 0.84f, 0.92f, 0.38f);
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = rain.emission;
            emission.rateOverTime = 0;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44, 0.5f, 44);
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0);
            velocity.y = new ParticleSystem.MinMaxCurve(-13);
            velocity.z = new ParticleSystem.MinMaxCurve(0);
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.055f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var path = Folder + "/Rain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetFloat("_Surface", 1);
                material.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f, 0.45f));
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material, path);
            }
            renderer.sharedMaterial = material;
            return rain;
        }

        private static Material SkyMaterial()
        {
            var path = Folder + "/DynamicSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("ContextSpeak/Dynamic Sky");
            if (shader == null) throw new System.InvalidOperationException("ContextSpeak/Dynamic Sky shader did not compile; check the Console.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static WeatherProfile[] Profiles()
        {
            var result = new List<WeatherProfile>();
            foreach (SkyWeather weather in System.Enum.GetValues(typeof(SkyWeather)))
            {
                var path = Folder + "/Weather-" + weather + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<WeatherProfile>();
                    var values = WeatherProfile.Default(weather);
                    profile.Weather = weather;
                    profile.CloudCover = values.Cover; profile.CloudDensity = values.Density; profile.CloudDarkness = values.Darkness;
                    profile.SunIntensity = values.Sun; profile.SkyDesaturation = values.Desaturate; profile.FogMultiplier = values.Fog;
                    profile.Rain = values.Rain; profile.WindMultiplier = values.Wind; profile.StarVisibility = values.Stars; profile.ExposureOffset = values.Exposure;
                    AssetDatabase.CreateAsset(profile, path);
                }
                result.Add(profile);
            }
            return result.ToArray();
        }

        /// Tileable cloud-shadow textures for the sun's light cookie (scrolled by the wind).
        private static Texture[] Cookies()
        {
            var result = new List<Texture>();
            for (var level = 1; level <= 3; level++)
            {
                var path = Folder + "/CloudShadows-" + level + ".png";
                if (!File.Exists(path))
                {
                    const int size = 256;
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    var threshold = 0.62f - level * 0.1f;
                    for (var y = 0; y < size; y++)
                        for (var x = 0; x < size; x++)
                        {
                            float value = 0, amplitude = 0.5f, frequency = 4;
                            for (var octave = 0; octave < 5; octave++)
                            {
                                value += amplitude * TileNoise(x / (float)size, y / (float)size, (int)frequency, octave * 17 + level);
                                frequency *= 2; amplitude *= 0.5f;
                            }
                            var shadow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(threshold, threshold + 0.18f, value));
                            var light = 1 - shadow * (0.45f + level * 0.1f);
                            texture.SetPixel(x, y, new Color(light, light, light, 1));
                        }
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Cookie;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.mipmapEnabled = true;
                    importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                }
                result.Add(AssetDatabase.LoadAssetAtPath<Texture>(path));
            }
            return result.ToArray();
        }

        private static float TileNoise(float u, float v, int period, int seed)
        {
            var x = u * period; var y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float Hash(int i, int j)
            {
                i = ((i % period) + period) % period; j = ((j % period) + period) % period;
                var h = (uint)(i * 374761393 + j * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
            var sx = fx * fx * (3 - 2 * fx); var sy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), sx), Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), sx), sy);
        }
    }
}
