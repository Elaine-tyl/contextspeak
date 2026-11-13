using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class SceneryReviewChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Verify()
        {
            var errors = new List<string>();
            foreach (var check in new Action[] { Reservation, DeferredRelease, SelectedInspection, PreviewIsolation, RestoredSpawn })
                try { check(); Debug.Log("Review regression passed: " + check.Method.Name); }
                catch (Exception error) { errors.Add(check.Method.Name + ": " + error.Message); }
            if (errors.Count != 0) throw new Exception(string.Join("\n", errors));
        }
        private static void Reservation()
        {
            var method = typeof(WorldScenery).GetMethod("CanReserve", BindingFlags.Public | BindingFlags.Static);
            Check(method != null, "Resource reservations must include pending stale loads.");
            bool Can(int count, int pending, int used, int next) => (bool)method.Invoke(null, new object[] { count, pending, used, next });
            Check(!Can(15, 1, 387936, 172787), "A slow old load cannot exceed 400k during travel.");
            Check(Can(1, 1, 172787, 16000), "A bounded second request can proceed.");
            Check(!Can(20, 0, 320000, 16000) && !Can(1, 2, 16000, 16000), "Instance and request limits remain enforced.");
        }
        private static void DeferredRelease()
        {
            var type = typeof(WorldScenery).Assembly.GetType("ContextSpeak.Cafe.SceneryLease");
            Check(type != null, "Addressable ownership needs a completion-delivered guard.");
            var lease = Activator.CreateInstance(type);
            bool Release() => (bool)type.GetMethod("TryRelease").Invoke(lease, null);
            Check(!Release(), "IsDone before deferred callback does not transfer release ownership.");
            type.GetMethod("Complete").Invoke(lease, null);
            Check(Release() && !Release(), "Update, scene exit and completion cannot release one handle twice.");
        }
        private static void SelectedInspection()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var method = typeof(Interactable).GetMethod("Nearest", BindingFlags.Static | BindingFlags.Public);
            Check(method != null, "Map-selected scenery must win over adjacent inspection anchors.");
            var anchors = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            foreach (var anchor in anchors.Where(item => item.Id.StartsWith("World")))
                Check((Interactable)method.Invoke(null, new object[] { anchors, anchor.Arrival.position, anchor.Id }) == anchor,
                    "Wrong inspection target at " + anchor.Id);
        }
        private static void RestoredSpawn()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var method = typeof(WorldScenery).GetMethod("SafeSpawn", BindingFlags.Static | BindingFlags.Public);
            Check(method != null, "Saved positions must recover safely when new scenery occupies the old location.");
            var obstacle = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None)
                .First(item => item.Id.StartsWith("World") && item.GetComponent<BoxCollider>() != null);
            var fallback = new Vector3(7.5f, 0.1f, -0.5f);
            var safe = (Vector3)method.Invoke(null, new object[] { obstacle.transform.position, fallback });
            var box = obstacle.GetComponent<BoxCollider>().bounds;
            Check(safe.x < box.min.x || safe.x > box.max.x || safe.z < box.min.z || safe.z > box.max.z,
                "Player restored inside a newly added prop.");
            var unchanged = (Vector3)method.Invoke(null, new object[] { fallback, fallback });
            Check(Vector2.Distance(new Vector2(unchanged.x, unchanged.z), new Vector2(fallback.x, fallback.z)) < 0.1f,
                "An unobstructed saved position should stay in place.");
        }

        private static void PreviewIsolation()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<SliceGame>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(SliceGame).GetField("recorder", flags).SetValue(game, game.gameObject.AddComponent<CafeRecorder>());
            var appearance = game.Player.GetComponentInChildren<PlayerAppearance>();
            var original = PlayerPrefs.GetString(CharacterProfiles.Key, "");
            var oldLook = PlayerPrefs.GetInt(PlayerAppearance.Preference, 0);
            var remote = new GameObject("Preview must not animate this friend");
            try
            {
                appearance.Choose(PlayerLook.Girl);
                var saved = PlayerPrefs.GetString(CharacterProfiles.Key);
                var peer = remote.AddComponent<RemotePlayer>();
                peer.SetVisual(appearance.Boy, "Friend");
                var remoteMotion = remote.GetComponentInChildren<AuthoredMotion>(true);
                typeof(SliceGame).GetMethod("PreviewMotion", flags).Invoke(game, new object[] { "Daydream|Run" });
                Check(PlayerPrefs.GetString(CharacterProfiles.Key) == saved && appearance.Selected == PlayerLook.Girl,
                    "Preview changed saved protagonist selection.");
                Check(appearance.Boy.GetComponentInChildren<AuthoredMotion>(true).CurrentClip == "Daydream-Run",
                    "The requested local visual did not receive the preview.");
                Check(remoteMotion.CurrentClip == "", "Preview controlled a friend's avatar.");
            }
            finally
            {
                if (original == "") PlayerPrefs.DeleteKey(CharacterProfiles.Key); else PlayerPrefs.SetString(CharacterProfiles.Key, original);
                PlayerPrefs.SetInt(PlayerAppearance.Preference, oldLook);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(remote);
            }
        }
    }
}
