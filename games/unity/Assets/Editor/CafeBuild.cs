using System;
using System.IO;
using System.Reflection;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    public static class CafeBuild
    {
        private const string Generated = "Assets/Generated";
        private const string SlicePath = Generated + "/ForestSlice.unity";
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        // The old Forest Café (ForestCafe) and Life town (LifeTown) scenes were retired: the Forest Café world is
        // ForestSlice and home is the villa. This now rebuilds the Forest Café world and character studio only.
        public static void CreateScene()
        {
            ConfigurePipeline();
            MeshyCharacters.BuildAssets();
            WorldAssetBuilder.Build();
            // Imported materials depend on the pipeline selected before glTF import.
            AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            foreach (var export in Directory.GetFiles("Assets/_Game/Art/Blender", "*.glb"))
                AssetDatabase.ImportAsset(export, ImportAssetOptions.ForceUpdate);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SliceScene.Create();
            EditorSceneManager.SaveScene(scene, SlicePath);
            CharacterCreationScene.Create();
            AssetDatabase.SaveAssets();
            WorldAddressables.Prepare();
            EditorSceneManager.OpenScene(SlicePath);
        }
        public static void ConfigurePipeline()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            var microphone = AssetImporter.GetAtPath("Assets/Plugins/macOS/libContextSpeakMic.dylib") as PluginImporter;
            if (microphone != null)
            {
                microphone.SetCompatibleWithAnyPlatform(false);
                microphone.SetCompatibleWithEditor(false);
                microphone.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                microphone.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
                microphone.SaveAndReimport();
            }
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "/Renderer.asset");
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Generated + "/Renderer.asset");
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/CafePipeline.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, Generated + "/CafePipeline.asset");
            }
            pipeline.msaaSampleCount = 2;
            pipeline.shadowDistance = 28;
            pipeline.mainLightShadowmapResolution = 2048;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // After ContextSpeak > Environment > Setup Realistic World/Lighting each quality level has its own
            // Env-* URP asset (Performance/High/Ultra); don't overwrite that assignment on every rebuild.
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/Env-High.asset") == null)
                QualitySettings.renderPipeline = pipeline;
            // The pinned URP package requires its Editor-only global resources in player builds.
            var globals = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
            globals?.GetMethod("Ensure", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, new object[] { true });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "ContextSpeak";
            PlayerSettings.productName = "ContextSpeak Unity Cafe";
            PlayerSettings.runInBackground = false;
            // The game client only connects to the pinned loopback companion.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            AssetDatabase.SaveAssets();
        }
        public static void Verify()
        {
            MicrophoneChecks.Verify();
            GameAudioChecks.Verify();
            ArcadeChecks.Verify();
            SliceChecks.Verify();
            CreateScene();
            MeshyChecks.Verify();
            SuppliedMotionChecks.Verify();
            RoomVisualChecks.Verify();
            CharacterCreationChecks.Verify();
            CharacterCreationChecks.VerifyNavigationQueue();
            CharacterCreationChecks.VerifyScene();
            EditorSceneManager.OpenScene(SlicePath);
            SliceSceneChecks.VerifyScene();
            NpcCastChecks.VerifyScene();
            CharacterCreationChecks.VerifyWorld();
            WorldAssetChecks.Verify();
            SceneryReviewChecks.Verify();
            EnvironmentUpgradeChecks.Verify();
            BuildingArtChecks.Verify();
        }
        public static void BuildWeb()
        {
            Verify();
            PlayerSettings.WebGL.template = "PROJECT:ContextSpeak";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.emscriptenArgs = "-s DYNAMIC_EXECUTION=0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            var output = Path.Combine(Root, ".local/world-builds/web/unity");
            WorldAddressables.Build();
            Build(output, BuildTarget.WebGL);
            File.WriteAllText(Path.Combine(Root, ".local/world-builds/web/index.html"),
                "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>ContextSpeak Life</title><meta http-equiv=\"refresh\" content=\"0;url=/unity/?world=life\"><a href=\"/unity/?world=life\">Open ContextSpeak Life</a></html>");
            foreach (var file in new[] { "microphone.js", "capture-worklet.js" })
                File.Copy(Path.Combine(Root, "games/cafe/web", file), Path.Combine(Root, ".local/world-builds/web", file), true);
        }
        public static void BuildArcade()
        {
            ConfigurePipeline();
            ArcadeChecks.Verify();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Arcade camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.96f, 0.97f, 0.95f);
            camera.gameObject.AddComponent<AudioListener>();
            new GameObject("ArcadeRuntime").AddComponent<ContextSpeak.Arcade.ArcadeBoard>();
            var path = Generated + "/Arcade.unity";
            EditorSceneManager.SaveScene(scene, path);
            PlayerSettings.WebGL.template = "PROJECT:Arcade";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.emscriptenArgs = "-s DYNAMIC_EXECUTION=0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            var output = Path.Combine(Root, "apps/web/public/arcade-engine");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { path }, locationPathName = output, target = BuildTarget.WebGL });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Unity arcade export failed.");
            Debug.Log("ContextSpeak Unity arcade export: " + output);
        }
        public static void BuildMac()
        {
            Verify();
            // The installed arm64 Mono player avoids requiring a separate Xcode/IL2CPP toolchain.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.macOS.microphoneUsageDescription = "Record English replies for local ContextSpeak cafe conversation practice. Audio stays on your computer.";
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
            WorldAddressables.Build();
            var output = Path.Combine(Root, ".local/unity-builds/ContextSpeak Cafe.app");
            Build(output, BuildTarget.StandaloneOSX);
            if (!File.Exists(Path.Combine(output, "Contents/Plugins/libContextSpeakMic.dylib")))
                throw new BuildFailedException("Desktop export is missing its microphone permission library.");
        }
        private static void Build(string output, BuildTarget target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = LivingWorldBuilder.PlayerScenes(WorldAddressables.Bootstrap), locationPathName = output, target = target, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Unity export failed: " + report.summary.result);
            Debug.Log("ContextSpeak Unity export: " + output);
        }
    }
}
