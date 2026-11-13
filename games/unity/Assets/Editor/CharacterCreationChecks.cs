using System;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class CharacterCreationChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Character creation: " + message);
        }

        public static void VerifyNavigationQueue()
        {
            var host = new GameObject("Isolated navigation gate");
            try
            {
                var loader = host.AddComponent<WorldScenes>();
                var method = typeof(WorldScenes).GetMethod("LoadWhenReady", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(method != null, "Navigation requested during scene activation must wait instead of disappearing.");
                var loading = typeof(WorldScenes).GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic);
                loading.SetValue(loader, true);
                var request = (IEnumerator)method.Invoke(loader, new object[] { "ForestSlice", null });
                Check(request.MoveNext() && request.Current == null, "A new destination waits while the previous scene unloads.");
                loading.SetValue(loader, false);
                Check(request.MoveNext() && request.Current is IEnumerator && (bool)loading.GetValue(loader),
                    "After waiting, exactly one load owns the navigation gate before starting Addressables.");
                (request as IDisposable)?.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        public static void VerifyScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Generated/CharacterCreation.unity", OpenSceneMode.Additive);
            try
            {
                var creator = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CharacterCreationController>()).Single();
                Check(creator.Preview != null && creator.Preview.PreviewOnly && creator.PreviewCamera != null,
                    "Creation has a separate preview and camera, with no gameplay controller.");
                var projected = creator.PreviewCamera.WorldToViewportPoint(creator.Preview.transform.position + Vector3.up);
                Check(projected.x > 0.6f && projected.x < 0.95f, "The real character preview must be on the right, clear of the left form.");
                foreach (var character in new[] { creator.Preview.Girl, creator.Preview.Boy })
                {
                    Check(character != null && character.GetComponentInChildren<SkinnedMeshRenderer>(true) != null,
                        "Both supplied protagonists must have real skinned previews.");
                    Check(character.GetComponentsInChildren<Collider>(true).Length == 0, "Preview rotation cannot create physical blockers.");
                }
                creator.Preview.ApplyProfile(new CharacterProfile { Look = PlayerLook.Boy, Height = 1.05f });
                Check(creator.Preview.Boy.activeSelf && !creator.Preview.Girl.activeSelf && creator.Preview.Boy.transform.localScale.y == 1.05f,
                    "Changing the draft updates the real preview without saving it.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        public static void VerifyWorld()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var interactions = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            foreach (var id in new[] { "wardrobe", "fitting-room", "timetable" })
            {
                var item = interactions.FirstOrDefault(candidate => candidate.Id == id && candidate.Arrival != null);
                Check(item != null, "A reachable real interaction is needed: " + id);
                var route = new NavMeshPath();
                Check(NavMesh.CalculatePath(new Vector3(0, 0.1f, 10.5f), item.Arrival.position, NavMesh.AllAreas, route)
                    && route.status == NavMeshPathStatus.PathComplete, "The interaction must be reachable on foot: " + id);
            }
            var teacher = UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None).FirstOrDefault(npc => npc.SceneId == "school");
            Check(teacher != null, "The school needs a teacher connected to the shared speech tasks.");
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(new Vector3(0, 0.1f, 10.5f), teacher.Arrival, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete, "The classroom is reachable on foot from the café.");
        }

        public static void Verify()
        {
            var hadProfile = PlayerPrefs.HasKey(CharacterProfiles.Key);
            var savedProfile = PlayerPrefs.GetString(CharacterProfiles.Key);
            var hadLook = PlayerPrefs.HasKey(PlayerAppearance.Preference);
            var savedLook = PlayerPrefs.GetInt(PlayerAppearance.Preference);
            try
            {
                PlayerPrefs.DeleteKey(CharacterProfiles.Key);
                PlayerPrefs.DeleteKey(PlayerAppearance.Preference);
                Check(!CharacterProfiles.Exists, "An existing gender setting is not a completed character profile.");
                var profile = CharacterProfiles.Draft();
                Check(profile.Look == PlayerLook.Girl && profile.Name == "Emma" && profile.Band == 5, "Defaults match the supplied prototype.");
                PlayerPrefs.SetInt(PlayerAppearance.Preference, 1);
                Check(CharacterProfiles.Draft().Look == PlayerLook.Boy && CharacterProfiles.Draft().Name == "Lucas", "Earlier protagonist choice migrates into a draft.");
                profile = new CharacterProfile { Name = "  \n ", Look = (PlayerLook)99, Band = 12, Height = float.NaN, Build = 3 };
                profile.Normalize();
                Check(profile.Name == "Emma" && profile.Look == PlayerLook.Girl && profile.Band == 9 && profile.Height == 1 && profile.Build == 1.05f,
                    "Invalid fields are bounded without breaking skeleton proportions.");
                profile.Name = new string('a', 40);
                profile.Normalize();
                Check(profile.Name.Length == 24, "Names stay within their display budget.");
                profile = new CharacterProfile { Name = " Elaine ", Look = PlayerLook.Boy, Band = 7, Height = 1.03f, Build = 0.98f };
                Check(CharacterProfiles.TrySave(profile, out var error), "The profile saves: " + error);
                Check(CharacterProfiles.TryLoad(out var restored) && restored.Created && restored.Name == "Elaine" && restored.Look == PlayerLook.Boy
                    && restored.Band == 7 && Mathf.Approximately(restored.Height, 1.03f) && Mathf.Approximately(restored.Build, 0.98f), "Actual JSON storage round-trips all supported options.");
                Check(!CharacterProfiles.TrySave(new CharacterProfile { Version = 2 }, out _), "Unsupported input versions cannot be written.");
                var draft = CharacterProfiles.Draft();
                draft.Name = "Unsaved edit";
                Check(CharacterProfiles.Draft().Name == "Elaine", "Editing a draft cannot mutate the saved profile.");
                foreach (var broken in new[] { "", "null", "{}", "{bad", "{\"Version\":2,\"Created\":true}", new string('x', 5000) })
                    Check(!CharacterProfiles.Decode(broken, out _), "Invalid or unsupported storage must be rejected.");
                const string future = "{\"Version\":2,\"Created\":true,\"Name\":\"Future\"}";
                PlayerPrefs.SetString(CharacterProfiles.Key, future);
                Check(!CharacterProfiles.TrySave(profile, out error) && PlayerPrefs.GetString(CharacterProfiles.Key) == future && error.Contains("newer"), "Older clients must not overwrite newer profiles.");
            }
            finally
            {
                if (hadProfile) PlayerPrefs.SetString(CharacterProfiles.Key, savedProfile); else PlayerPrefs.DeleteKey(CharacterProfiles.Key);
                if (hadLook) PlayerPrefs.SetInt(PlayerAppearance.Preference, savedLook); else PlayerPrefs.DeleteKey(PlayerAppearance.Preference);
                PlayerPrefs.Save();
            }
            Debug.Log("Character profile regression checks passed");
        }
    }
}
