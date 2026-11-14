using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Play mode can only open a non-Addressables world (airport, hospital, zoo, villa, …) if its scene is in
    /// File > Build Settings. Keep every saved world from WorldSceneCatalog there, so F10 travel works in the Editor
    /// exactly like in the builds. Existing entries are kept; nothing is removed.
    [InitializeOnLoad]
    public static class WorldTravelSync
    {
        static WorldTravelSync() => EditorApplication.delayCall += () => Sync(false);

        [MenuItem("ContextSpeak/World/Add All Worlds To Build Settings")]
        public static void SyncMenu() => Sync(true);

        public static void Sync(bool log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scenes = EditorBuildSettings.scenes.ToList();
            var added = 0;
            foreach (var entry in WorldSceneCatalog.All.Where(scene => !scene.Streamed))
            {
                if (!File.Exists(entry.Path)) continue;
                var existing = scenes.FindIndex(scene => scene.path == entry.Path);
                if (existing >= 0)
                {
                    if (scenes[existing].enabled) continue;
                    scenes[existing] = new EditorBuildSettingsScene(entry.Path, true);
                }
                else scenes.Add(new EditorBuildSettingsScene(entry.Path, true));
                added++;
            }
            if (added > 0) EditorBuildSettings.scenes = scenes.ToArray();
            if (log || added > 0) Debug.Log("World travel: " + added + " world scene(s) added/enabled in Build Settings (" + scenes.Count(scene => scene.enabled) + " enabled).");
        }
    }
}
