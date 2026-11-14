using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using ContextSpeak.LivingWorld;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Editor
{
    /// Synthetic reviewer fixtures exercise UI/input/cancellation; they are not human speech evidence.
    [InitializeOnLoad]
    public static class LivingWorldPlayChecks
    {
        private const string Folder = "../../.local/living-world/";
        private const string Phase = "ContextSpeak.LivingWorld.TestPhase";
        [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
        private static double deadline;
        static LivingWorldPlayChecks() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayChanged; }
        [UnityEditor.MenuItem("ContextSpeak/Living World/Run isolated play checks")]
        public static void Request()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "play-check.request", "run");
        }
        [UnityEditor.MenuItem("ContextSpeak/Living World/Preview with live speech")]
        public static void Preview()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "play-check.request", "preview");
        }
        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // Entering Play Mode reloads static fields; arm the watchdog again after reload.
            if (SessionState.GetInt(Phase, 0) != 0 && deadline == 0) deadline = EditorApplication.timeSinceStartup + (SessionState.GetBool(Phase + ".Preview", false) ? 600 : 120);
            if (SessionState.GetInt(Phase, 0) != 0 && EditorApplication.timeSinceStartup > deadline)
            { Done("Native play-check timed out; inspect the Editor console."); deadline = 0; return; }
            if (File.Exists(Folder + "play-check.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Phase + ".Preview", File.ReadAllText(Folder + "play-check.request").Trim() == "preview");
                File.Delete(Folder + "play-check.request");
                if (Enumerable.Range(0, SceneManager.sceneCount).Any(index => SceneManager.GetSceneAt(index).isDirty))
                {
                    File.WriteAllText(Folder + "native-play.json", "{\"passed\":false,\"reason\":\"Open scene has unsaved edits; play check deferred.\"}"); return;
                }
                SessionState.SetString(Phase + ".Scenes", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
                SessionState.SetString(Phase + ".StorySave", PlayerPrefs.GetString(DynamicLifeEventManager.SaveKey, ""));
                SessionState.SetBool(Phase + ".HadStorySave", PlayerPrefs.HasKey(DynamicLifeEventManager.SaveKey));
                SessionState.SetString(Phase + ".Profile", PlayerPrefs.GetString(CharacterProfiles.Key, ""));
                SessionState.SetBool(Phase + ".HadProfile", PlayerPrefs.HasKey(CharacterProfiles.Key));
                PlayerPrefs.SetString(CharacterProfiles.Key, JsonUtility.ToJson(new CharacterProfile { Created = true, Name = "Story test" }));
                var cafeSave = Path.Combine(Application.persistentDataPath, "forest-cafe-slice.json");
                SessionState.SetBool(Phase + ".HadCafeSave", File.Exists(cafeSave));
                if (File.Exists(cafeSave)) File.Copy(cafeSave, Folder + "cafe-save.backup", true);
                SessionState.SetInt(Phase, 1);
                deadline = EditorApplication.timeSinceStartup + (SessionState.GetBool(Phase + ".Preview", false) ? 600 : 120);
                EditorSceneManager.OpenScene(LivingWorldBuilder.District);
                EditorApplication.isPlaying = true;
            }
            if (!EditorApplication.isPlaying || SessionState.GetInt(Phase, 0) != 1) return;
            var director = DynamicLifeEventManager.Instance;
            if (director == null || !director.Ready) return;
            SessionState.SetInt(Phase, 2);
            var runner = director.gameObject.AddComponent<LivingWorldPlayRunner>();
            runner.LivePreview = SessionState.GetBool(Phase + ".Preview", false);
            runner.Finished = Done;
            runner.StartCoroutine(runner.Run());
        }
        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || SessionState.GetInt(Phase, 0) == 0) return;
            var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Phase + ".Scenes", ""));
            if (setup?.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            if (SessionState.GetBool(Phase + ".HadStorySave", false)) PlayerPrefs.SetString(DynamicLifeEventManager.SaveKey, SessionState.GetString(Phase + ".StorySave", ""));
            else PlayerPrefs.DeleteKey(DynamicLifeEventManager.SaveKey);
            if (SessionState.GetBool(Phase + ".HadProfile", false)) PlayerPrefs.SetString(CharacterProfiles.Key, SessionState.GetString(Phase + ".Profile", ""));
            else PlayerPrefs.DeleteKey(CharacterProfiles.Key);
            var cafeSave = Path.Combine(Application.persistentDataPath, "forest-cafe-slice.json");
            if (SessionState.GetBool(Phase + ".HadCafeSave", false)) File.Copy(Folder + "cafe-save.backup", cafeSave, true);
            else if (File.Exists(cafeSave)) File.Delete(cafeSave);
            PlayerPrefs.Save(); SessionState.SetInt(Phase, 0);
        }
        public static void Done(string error)
        {
            var preview = SessionState.GetBool(Phase + ".Preview", false);
            File.WriteAllText(Folder + (preview ? "live-speech.json" : "native-play.json"), error == "" ? "{\"passed\":true,\"inputs\":\"" + (preview ? "live microphone and configured local AI" : "synthetic reviewer fixtures") + "\"}"
                : JsonUtility.ToJson(new Failure { error = error }));
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class Failure { public bool passed = false; public string error; }

    }
}
