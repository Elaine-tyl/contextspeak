using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Refresh character prefabs without replacing or saving the artist's open scene.
    [InitializeOnLoad]
    public static class CharacterAnimationUpgrade
    {
        private const string Request = "../../.local/meshy/animated-upgrade/apply.request";
        private const string Report = "../../.local/meshy/animated-upgrade/unity-report.json";

        static CharacterAnimationUpgrade() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Apply();
        }

        [MenuItem("ContextSpeak/Meshy/Apply complete character animations")]
        public static void Apply()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            try
            {
                MotionAssets.BuildAndVerify();
                foreach (var name in MotionAssets.Names)
                    MeshyCharacters.CharacterPrefab(name, MeshyCharacters.Folder + name + ".prefab");
                MeshyChecks.Verify();
                AssetDatabase.SaveAssets();
                var count = MotionAssets.Names.Sum(name => MotionAssets.Roles(name).Length);
                foreach (var name in MotionAssets.Names)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyCharacters.Folder + name + ".prefab");
                    var motion = prefab.GetComponent<ContextSpeak.Cafe.AuthoredMotion>();
                    foreach (var role in MotionAssets.Roles(name))
                        if (motion.Find(role) == null) throw new Exception("Missing gameplay role: " + name + "/" + role);
                }
                File.WriteAllText(Report, "{\"passed\":true,\"suppliedClips\":" + count + "}");
                Debug.Log("Character animation upgrade verified: " + count + " supplied clips.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Report, JsonUtility.ToJson(new Failure { error = error.ToString() }));
                Debug.LogException(error);
                throw;
            }
        }

        [Serializable] private sealed class Failure { public bool passed = false; public string error; }
    }
}
