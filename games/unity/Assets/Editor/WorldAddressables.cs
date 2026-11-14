using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Editor
{
    public static class WorldAddressables
    {
        public const string Bootstrap = "Assets/Generated/Bootstrap.unity";
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AssetDatabase.Refresh();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create("Assets/Generated/Addressables", "Settings", true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
            }
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            var group = settings.FindGroup("Life districts") ?? settings.CreateGroup("Life districts", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            // A browser must not download every world just to enter the cafe.
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            // The old Forest Café and Life town were retired (duplicates of the Forest Café world and the villa home).
            foreach (var retired in new[] { "ForestCafe", "LifeTown" })
            {
                var guid = AssetDatabase.AssetPathToGUID("Assets/Generated/" + retired + ".unity");
                if (!string.IsNullOrEmpty(guid) && settings.FindAssetEntry(guid) != null) settings.RemoveAssetEntry(guid);
            }
            foreach (var scene in new[] { "ForestSlice", "CharacterCreation" })
            {
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID("Assets/Generated/" + scene + ".unity"), group);
                entry.address = scene;
            }
            WorldAssetBuilder.Addressables();
            EditorUtility.SetDirty(settings);
            var bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("ContextSpeak bootstrap").AddComponent<ContextSpeak.Cafe.WorldBootstrap>();
            EditorSceneManager.SaveScene(bootstrap, Bootstrap);
            AssetDatabase.SaveAssets();
        }
        public static void Build()
        {
            Prepare();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) throw new Exception("World district build failed: " + result.Error);
        }
    }
}
