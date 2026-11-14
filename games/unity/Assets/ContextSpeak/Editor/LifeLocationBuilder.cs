using System.IO;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// ContextSpeak > Build Life Simulation Locations: writes the editable scenario and menu assets,
    /// builds Hospital.unity and Restaurant.unity, upgrades the Forest Café, registers the scenes in
    /// Build Settings and reports anything that was missing. Safe to run again (rebuilds in place).
    public static class LifeLocationBuilder
    {
        [UnityEditor.MenuItem("ContextSpeak/Build Life Simulation Locations")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            LifeBuild.Missing.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("ContextSpeak", "Preparing render pipeline and data…", 0.05f);
                CafeBuild.ConfigurePipeline();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(WorldAssetBuilder.Folder + "World784ed44e61.prefab") == null) WorldAssetBuilder.Build();
                foreach (var folder in new[] { LifeBuild.Data + "/Scenarios", LifeBuild.Data + "/Menus", LifeBuild.Data + "/Materials", LifeBuild.Scenes })
                    Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                WriteData();
                EditorUtility.DisplayProgressBar("ContextSpeak", "Building City Hospital…", 0.25f);
                HospitalBuilder.Build();
                EditorUtility.DisplayProgressBar("ContextSpeak", "Building Harbour Table restaurant…", 0.55f);
                RestaurantBuilder.Build();
                EditorUtility.DisplayProgressBar("ContextSpeak", "Upgrading the Forest Café…", 0.8f);
                ForestCafeUpgrade.Apply();
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            var report = "Life locations built: " + HospitalBuilder.ScenePath + ", " + RestaurantBuilder.ScenePath + ", Forest Café upgraded.\n"
                + "Rebuild Metro City (ContextSpeak > Build Metro City) to add the hospital/restaurant/train entrances, then press Play in MetroCity.";
            if (LifeBuild.Missing.Count > 0) Debug.LogWarning(report + "\nMissing assets (placeholders used):\n- " + string.Join("\n- ", LifeBuild.Missing.Distinct()));
            else Debug.Log(report);
            var errors = LifeLocationChecks.Run().Concat(SkyChecks.Run()).ToArray();
            if (errors.Length == 0) Debug.Log("ContextSpeak life-location checks passed.");
            else Debug.LogError("ContextSpeak life-location checks failed:\n- " + string.Join("\n- ", errors));
        }

        /// Scenario scripts and menus become assets you can edit in the Inspector. Existing assets
        /// are kept (your edits survive a rebuild); delete one to restore the built-in version.
        public static void WriteData()
        {
            foreach (var name in ScenarioLibrary.Names)
            {
                var path = LifeBuild.Data + "/Scenarios/" + name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path) == null) AssetDatabase.CreateAsset(ScenarioLibrary.Create(name), path);
            }
            foreach (var menu in new[] { MenuLibrary.Restaurant(), MenuLibrary.Cafe() })
            {
                var path = LifeBuild.Data + "/Menus/" + menu.name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<MenuCatalog>(path) == null) AssetDatabase.CreateAsset(menu, path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
