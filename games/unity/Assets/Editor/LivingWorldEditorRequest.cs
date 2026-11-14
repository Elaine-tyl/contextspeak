using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    [InitializeOnLoad]
    public static class LivingWorldEditorRequest
    {
        private const string Folder = "../../.local/living-world/";
        static LivingWorldEditorRequest() => EditorApplication.update += Tick;
        private static void Tick()
        {
            if (!File.Exists(Folder + "prepare.request") || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "prepare.request");
            Prepare();
        }
        [MenuItem("ContextSpeak/Living World/Prepare stories and activity district")]
        public static void Prepare()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                LivingWorldBuilder.Prepare();
                File.WriteAllText(Folder + "native-prepare.json", "{\"passed\":true}");
            }
            catch (Exception error)
            {
                File.WriteAllText(Folder + "native-prepare.json", JsonUtility.ToJson(new Error { error = error.ToString() }));
                Debug.LogException(error);
            }
        }
        [Serializable] private sealed class Error { public bool passed = false; public string error; }
    }
}
