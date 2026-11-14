using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Export saved authoring scenes without regenerating or replacing their design.
    [InitializeOnLoad]
    public static class AuthoredWebBuild
    {
        private const string Pending = "ContextSpeak.AuthoredWebBuild";
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        private static string Reports => Path.Combine(Root, ".local/character-repair");

        static AuthoredWebBuild() => EditorApplication.update += Tick;

        [MenuItem("ContextSpeak/Export current design/Web browser")]
        public static void Request()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Pending, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling
                || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                {
                    SessionState.SetBool(Pending, false);
                    Debug.LogError("Could not switch the authored export to WebGL.");
                }
                return; // Continue after platform imports and domain reload finish.
            }
            SessionState.SetBool(Pending, false);
            try { Build(); }
            catch (Exception error)
            {
                Directory.CreateDirectory(Reports);
                File.WriteAllText(Path.Combine(Reports, "web-error.txt"), error.ToString());
                Debug.LogException(error);
            }
        }

        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new BuildFailedException("Select WebGL before building district bundles.");
            CharacterPoseChecks.Verify();
            UploadedDesignChecks.Verify();
            var scenes = ContextSpeak.Cafe.WorldSceneCatalog.All.Select(scene => scene.Path).OrderBy(path => path).ToArray();
            foreach (var path in scenes)
                if (!File.Exists(path)) throw new BuildFailedException("Saved scene is missing: " + path);
            var hashes = scenes.Select(Hash).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var version = DateTime.UtcNow.ToString("yyyyMMdd.HHmmss");
            var staging = Path.Combine(Root, ".local/world-builds/staging-" + version, "unity");
            try
            {
                PlayerSettings.bundleVersion = version;
                PlayerSettings.WebGL.template = "PROJECT:ContextSpeak";
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                // Imported 3D districts need room to grow beyond the old 2 GB desktop-browser cap.
                PlayerSettings.WebGL.initialMemorySize = 256;
                PlayerSettings.WebGL.maximumMemorySize = 4096;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.emscriptenArgs = "-s DYNAMIC_EXECUTION=0";
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
                WorldAddressables.Build();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = LivingWorldBuilder.PlayerScenes(WorldAddressables.Bootstrap),
                    locationPathName = staging, target = BuildTarget.WebGL, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Authored browser export failed: " + report.summary.result);
                if (!hashes.SequenceEqual(scenes.Select(Hash)))
                    throw new BuildFailedException("An authored scene changed during export; previous web build retained.");
                File.WriteAllText(Path.Combine(staging, "build-info.json"), JsonUtility.ToJson(new Receipt {
                    version = version, sceneHashes = scenes.Zip(hashes, (path, hash) => path + ":" + hash).ToArray()
                }, true));
                var served = Path.Combine(Root, ".local/world-builds/web/unity");
                var backup = Path.Combine(Root, ".local/world-builds/previous-" + version);
                Directory.CreateDirectory(Path.GetDirectoryName(served));
                var desktop = Path.Combine(served, "ContextSpeak-Cafe-macOS.zip");
                if (File.Exists(desktop)) File.Copy(desktop, Path.Combine(staging, Path.GetFileName(desktop)), true);
                // Validate companion files before touching the last working player export.
                foreach (var file in new[] { "microphone.js", "capture-worklet.js" })
                    File.Copy(Path.Combine(Root, "games/cafe/web", file), Path.Combine(Path.GetDirectoryName(served), file), true);
                if (Directory.Exists(served)) Directory.Move(served, backup);
                try { Directory.Move(staging, served); }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, served); throw; }
                Directory.CreateDirectory(Reports);
                File.WriteAllText(Path.Combine(Reports, "web-success.txt"), version);
                Debug.Log("Authored web build published: " + version + "; saved scene hashes unchanged.");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] private sealed class Receipt { public string version; public string[] sceneHashes; }
    }
}
