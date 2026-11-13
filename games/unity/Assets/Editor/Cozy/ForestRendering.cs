using System;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// <summary>Persisted, forward-rendered forest settings for the Mac and WebGL scenes.</summary>
    public static class ForestRendering
    {
        private const string Generated = "Assets/Generated/";

        public static void Configure(Camera camera, Light sun)
        {
            if (camera == null || sun == null)
                throw new ArgumentException("Forest rendering requires the scene camera and directional sun.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "CafePipeline.asset");
            var defaultRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "Renderer.asset");
            if (pipeline == null || defaultRenderer == null)
                throw new InvalidOperationException("Call CafeBuild.ConfigurePipeline before ForestRendering.Configure.");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "ForestRenderer.asset");
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Generated + "ForestRenderer.asset");
            }
            // Migrate this feature out of older generated defaults; 2D cameras keep a pass-free renderer.
            foreach (var legacy in defaultRenderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>()
                .Where(feature => feature.name == "Forest contact occlusion").ToArray())
            {
                defaultRenderer.rendererFeatures.Remove(legacy);
                UnityEngine.Object.DestroyImmediate(legacy, true);
            }
            SyncFeatureMap(defaultRenderer);

            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false; // Opt in on the forest camera only.
            pipeline.shadowDistance = 48f;
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowCascadeCount = 4;
            pipeline.cascade4Split = new Vector3(0.12f, 0.30f, 0.60f);
            pipeline.cascadeBorder = 0.15f;
            pipeline.shadowDepthBias = 0.7f;
            pipeline.shadowNormalBias = 0.5f;
            // These URP17 properties have internal setters; use their serialized Inspector fields.
            var pipelineSettings = new SerializedObject(pipeline);
            Required(pipelineSettings, "m_MainLightShadowsSupported").boolValue = true;
            Required(pipelineSettings, "m_AnyShadowsSupported").boolValue = true;
            Required(pipelineSettings, "m_SoftShadowsSupported").boolValue = true;
            SetEnum(Required(pipelineSettings, "m_SoftShadowQuality"), "Medium");
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();

            // Forward avoids the deferred path's MSAA restrictions. Copy depth before transparent water.
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.copyDepthMode = CopyDepthMode.AfterOpaques;
            ConfigureOcclusion(renderer);

            camera.allowHDR = true;
            camera.allowMSAA = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            SelectForestRenderer(pipeline, renderer, cameraData);
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            var browserQuality = camera.gameObject.AddComponent<ForestBrowserQuality>();
            browserQuality.Pipeline = pipeline;
            browserQuality.ContactOcclusion = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            browserQuality.Camera = cameraData;
            sun.shadows = LightShadows.Soft;
            var lightData = sun.GetUniversalAdditionalLightData();
            lightData.usePipelineSettings = true;
            lightData.softShadowQuality = SoftShadowQuality.UsePipelineSettings;
            RenderSettings.sun = sun;
            ConfigureSky(camera);

            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(defaultRenderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssetIfDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(defaultRenderer);
            AssetDatabase.SaveAssetIfDirty(pipeline);
        }

        private static void ConfigureOcclusion(UniversalRendererData renderer)
        {
            // Reuse the referenced feature, or recover an existing subasset before creating one.
            var feature = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault()
                ?? AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(renderer))
                    .OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "Forest contact occlusion";
                AssetDatabase.AddObjectToAsset(feature, renderer);
            }
            if (!renderer.rendererFeatures.Contains(feature)) renderer.rendererFeatures.Add(feature);
            feature.SetActive(true);

            var serialized = new SerializedObject(feature);
            var settings = Required(serialized, "m_Settings");
            SetEnum(Member(settings, "AOMethod"), "InterleavedGradient");
            Member(settings, "Downsample").boolValue = true;
            // A late, restrained composite also reaches the existing custom wood shader.
            // It darkens direct light too; this is an inexpensive contact cue, not baked GI.
            Member(settings, "AfterOpaque").boolValue = true;
            SetEnum(Member(settings, "Source"), "Depth");
            SetEnum(Member(settings, "NormalSamples"), "Medium");
            SetEnum(Member(settings, "Samples"), "Low");
            SetEnum(Member(settings, "BlurQuality"), "Medium");
            Member(settings, "Intensity").floatValue = 0.55f;
            Member(settings, "DirectLightingStrength").floatValue = 0.1f;
            Member(settings, "Radius").floatValue = 0.22f;
            Member(settings, "Falloff").floatValue = 32f;
            Member(settings, "SampleCount").intValue = -1; // Disable legacy setting migration.
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SyncFeatureMap(renderer);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(feature);
        }

        private static void SelectForestRenderer(UniversalRenderPipelineAsset pipeline, UniversalRendererData renderer, UniversalAdditionalCameraData camera)
        {
            var settings = new SerializedObject(pipeline);
            var renderers = Required(settings, "m_RendererDataList");
            var index = 0;
            while (index < renderers.arraySize && renderers.GetArrayElementAtIndex(index).objectReferenceValue != renderer) index++;
            if (index == renderers.arraySize) renderers.arraySize++;
            renderers.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            settings.ApplyModifiedPropertiesWithoutUndo();
            camera.SetRenderer(index);
        }

        private static void SyncFeatureMap(UniversalRendererData renderer)
        {
            // URP's Inspector keeps a local file-ID map beside feature references for reload recovery.
            var rendererSettings = new SerializedObject(renderer);
            var map = Required(rendererSettings, "m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (var i = 0; i < map.arraySize; i++)
            {
                long localId = 0;
                if (renderer.rendererFeatures[i] != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSky(Camera camera)
        {
            const string skyPath = Generated + "ForestSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null) throw new InvalidOperationException("The built-in procedural sky shader is unavailable.");
                sky = new Material(shader) { name = "Forest daylight sky" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunDisk", 2f);
            sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            sky.DisableKeyword("_SUNDISK_NONE");
            sky.DisableKeyword("_SUNDISK_SIMPLE");
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetFloat("_Exposure", 1f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.59f, 0.59f));
            sky.SetColor("_GroundColor", new Color(0.36f, 0.39f, 0.30f));
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;

            // A tiny authored sky/ground approximation supplies real reflection data without a capture.
            // It contains no forest silhouettes and is deliberately independent of runtime GI updates.
            const string reflectionPath = Generated + "ForestSkyReflection.asset";
            var reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            if (reflection == null)
            {
                reflection = new Cubemap(32, TextureFormat.RGBAHalf, true) { name = "Forest sky approximation" };
                AssetDatabase.CreateAsset(reflection, reflectionPath);
            }
            reflection.wrapMode = TextureWrapMode.Clamp;
            reflection.filterMode = FilterMode.Trilinear;
            var skyColour = new Color(0.38f, 0.56f, 0.66f).linear;
            var horizonColour = new Color(0.76f, 0.81f, 0.73f).linear;
            var groundColour = new Color(0.31f, 0.37f, 0.25f).linear;
            for (var face = 0; face < 6; face++)
            {
                var pixels = new Color[reflection.width * reflection.width];
                for (var y = 0; y < reflection.width; y++)
                for (var x = 0; x < reflection.width; x++)
                {
                    var u = 2f * (x + 0.5f) / reflection.width - 1f;
                    var v = 2f * (y + 0.5f) / reflection.width - 1f;
                    var vertical = face == (int)CubemapFace.PositiveY ? 1f : face == (int)CubemapFace.NegativeY ? -1f : -v;
                    var height = vertical / Mathf.Sqrt(1f + u * u + v * v);
                    pixels[y * reflection.width + x] = Color.Lerp(horizonColour, height >= 0 ? skyColour : groundColour,
                        Mathf.Pow(Mathf.Abs(height), 0.55f));
                }
                reflection.SetPixels(pixels, (CubemapFace)face);
            }
            reflection.Apply(true, false);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = 0.8f;
            EditorUtility.SetDirty(sky);
            EditorUtility.SetDirty(reflection);
            AssetDatabase.SaveAssetIfDirty(sky);
            AssetDatabase.SaveAssetIfDirty(reflection);
        }

        private static SerializedProperty Required(SerializedObject target, string name) => target.FindProperty(name)
            ?? throw new InvalidOperationException("URP17 serialized field is missing: " + name);

        private static SerializedProperty Member(SerializedProperty settings, string name) => settings.FindPropertyRelative(name)
            ?? throw new InvalidOperationException("URP17 SSAO setting is missing: " + name);

        private static void SetEnum(SerializedProperty property, string name)
        {
            var index = Array.IndexOf(property.enumNames, name);
            if (index < 0) throw new InvalidOperationException("URP17 option is missing: " + name);
            property.enumValueIndex = index;
        }
    }
}
