using System;
using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// NPC replacement is complete only when every real actor uses prepared Meshy geometry.
    public static class NpcCastChecks
    {
        public static void VerifySavedScenes()
        {
            foreach (var scene in new[] { "ForestSlice" })   // the old ForestCafe and LifeTown scenes were retired
            {
                EditorSceneManager.OpenScene("Assets/Generated/" + scene + ".unity");
                VerifyScene();
            }
        }

        public static void VerifyPlayerChoice()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var player = UnityEngine.Object.FindFirstObjectByType<SliceGame>().Player;
            VerifyPlayer(player);
        }

        private static void VerifyPlayer(CafePlayer player)
        {
            var appearance = player.Model.GetComponent<PlayerAppearance>();
            if (appearance == null || appearance.Girl == null || appearance.Boy == null)
                throw new Exception("Both protagonists must be wired to the same player controller.");
            var position = player.transform.position;
            var controller = player.GetComponent<CharacterController>();
            var hadProfile = PlayerPrefs.HasKey(CharacterProfiles.Key);
            var storedProfile = PlayerPrefs.GetString(CharacterProfiles.Key);
            PlayerPrefs.DeleteKey(CharacterProfiles.Key);
            var hadPreference = PlayerPrefs.HasKey(PlayerAppearance.Preference);
            var preference = PlayerPrefs.GetInt(PlayerAppearance.Preference);
            var selected = appearance.Selected;
            try
            {
                PlayerPrefs.DeleteKey(PlayerAppearance.Preference);
                appearance.Restore();
                if (!appearance.Girl.activeSelf || appearance.Boy.activeSelf) throw new Exception("Old installs must default to the girl.");
                foreach (var look in new[] { PlayerLook.Boy, PlayerLook.Girl })
                {
                    if (!appearance.Choose(look)) throw new Exception("Valid protagonist cannot be chosen.");
                    var active = look == PlayerLook.Boy ? appearance.Boy : appearance.Girl;
                    var inactive = look == PlayerLook.Boy ? appearance.Girl : appearance.Boy;
                    if (!active.activeSelf || inactive.activeSelf) throw new Exception("Only the chosen protagonist should be visible.");
                    var mesh = active.GetComponentInChildren<SkinnedMeshRenderer>();
                    var expected = look == PlayerLook.Boy ? "Daydream" : "Academy";
                    if (mesh == null || !AssetDatabase.GetAssetPath(mesh.sharedMesh).EndsWith("/" + expected + "-All.fbx"))
                        throw new Exception("Protagonist does not match the supplied reference: " + look);
                    appearance.Restore();
                    if (appearance.Selected != look) throw new Exception("Appearance did not survive restoring preferences.");
                }
                if (appearance.Choose((PlayerLook)99)) throw new Exception("Invalid choices must be rejected.");
                PlayerPrefs.SetInt(PlayerAppearance.Preference, 99);
                appearance.Restore();
                if (appearance.Selected != PlayerLook.Girl) throw new Exception("Corrupt appearance preference needs a safe default.");
                if (player.transform.position != position || player.GetComponent<CharacterController>() != controller)
                    throw new Exception("Switching character changed the gameplay root.");
                if (appearance.GetComponentsInChildren<Collider>(true).Length > 0)
                    throw new Exception("Visual switching must not add physical blockers.");
            }
            finally
            {
                appearance.Choose(selected);
                if (hadPreference) PlayerPrefs.SetInt(PlayerAppearance.Preference, preference);
                else PlayerPrefs.DeleteKey(PlayerAppearance.Preference);
                if (hadProfile) PlayerPrefs.SetString(CharacterProfiles.Key, storedProfile);
                else PlayerPrefs.DeleteKey(CharacterProfiles.Key);
                PlayerPrefs.Save();
            }
        }

        public static void VerifyScene()
        {
            foreach (var player in UnityEngine.Object.FindObjectsByType<CafePlayer>(FindObjectsSortMode.None)) VerifyPlayer(player);
            var actors = new HashSet<GameObject>();
            foreach (var actor in UnityEngine.Object.FindObjectsByType<NpcActor>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            foreach (var actor in UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CafeCustomer>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            foreach (var actor in UnityEngine.Object.FindObjectsByType<WaiterNpc>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            var game = UnityEngine.Object.FindFirstObjectByType<SliceGame>();
            if (game != null)
            {
                actors.Add(game.Hazel.gameObject);
                var chef = GameObject.Find("Chef Theo");
                if (chef == null) throw new Exception("NPC cast: kitchen chef was lost.");
                actors.Add(chef);
            }
            if (actors.Count < 2) throw new Exception("NPC cast: scene actors were lost.");
            var assignedBodies = new HashSet<string>();
            foreach (var actor in actors)
            {
                var meshes = actor.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
                    .Concat(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin => skin.sharedMesh))
                    .Where(mesh => mesh != null).ToArray();
                var humans = meshes.Where(mesh => (AssetDatabase.GetAssetPath(mesh).Contains("/Meshy/") || AssetDatabase.GetAssetPath(mesh).Contains("/Motion/"))).ToArray();
                if (humans.Length == 0) throw new Exception("NPC cast still uses an old character: " + actor.name);
                var source = AssetDatabase.GetAssetPath(humans[0]);
                if (!assignedBodies.Add(source)) throw new Exception("Repeated NPC body in the same scene: " + actor.name);
                if (humans.Any(mesh => {
                    var path = AssetDatabase.GetAssetPath(mesh);
                    return path.EndsWith("/Academy.glb") || path.EndsWith("/Daydream.glb") || path.EndsWith("/Academy-All.fbx") || path.EndsWith("/Daydream-All.fbx");
                })) throw new Exception("Protagonist reused as NPC: " + actor.name);
                var old = meshes.Where(mesh => {
                    var path = AssetDatabase.GetAssetPath(mesh);
                    return path.Contains("mini-characters") || path.Contains("ReferenceBarista")
                        || path.Contains("TrailGuide") || path.Contains("/CozyCharacters/");
                }).ToArray();
                if (old.Length > 0) throw new Exception("NPC cast includes legacy body geometry: " + actor.name);
                if (humans.Sum(mesh => (long)mesh.triangles.Length / 3) > 60000)
                    throw new Exception("NPC exceeds prepared geometry budget: " + actor.name);
            }
            foreach (var mobile in UnityEngine.Object.FindObjectsByType<CafeCustomer>(FindObjectsSortMode.None)
                .Select(actor => actor.gameObject).Concat(UnityEngine.Object.FindObjectsByType<WaiterNpc>(FindObjectsSortMode.None).Select(actor => actor.gameObject)))
            {
                var skin = mobile.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skin == null || skin.bones.Length < 7 || mobile.GetComponentInChildren<CozyAvatar>(true) == null)
                    throw new Exception("Mobile NPC requires its own weighted locomotion skeleton: " + mobile.name);
            }
            if (game != null)
            {
                var assigned = assignedBodies.Select(path => System.IO.Path.GetFileNameWithoutExtension(path).Replace("-Walk", "")).ToHashSet();
                var expected = MeshyCharacters.Names.Where(name => name != "Academy" && name != "Daydream").ToHashSet();
                if (!assigned.SetEquals(expected))
                    throw new Exception("Some supplied human identities are not in the live town: " + string.Join(", ", expected.Except(assigned)));
                foreach (var resident in UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None))
                    if (string.IsNullOrEmpty(resident.DisplayName) || string.IsNullOrEmpty(resident.DisplayRole))
                        throw new Exception("Town resident needs an identifiable name and contextual role.");
            }
            Debug.Log("Meshy NPC cast checks passed: " + actors.Count + " actors.");
        }
    }
}
