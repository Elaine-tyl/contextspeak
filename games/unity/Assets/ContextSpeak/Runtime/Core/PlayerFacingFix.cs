using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Life
{
    /// Fixes the "player walks backwards" bug in every scene, without rebuilding scenes.
    ///
    /// Cause: the editor's CozyCharacters.DressPlayer parented the Girl/Boy visuals to the Avatar with
    /// worldPositionStays = true. When a scene spawned the player with a body yaw other than 0 (e.g. 180°
    /// facing a building), the visual kept a compensating local rotation of −yaw. CafePlayer then turns
    /// the Avatar towards the movement direction, but the visual stays turned away from it.
    ///
    /// The visuals are authored to face the Avatar's +Z (MotionAssets / CharacterFacing.Align), so their
    /// local rotation must be identity. This runs after every scene load and resets it.
    public static class PlayerFacingFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            Apply();
        }

        private static void Loaded(Scene scene, LoadSceneMode mode) => Apply();

        public static int Apply()
        {
            var fixedCount = 0;
            foreach (var appearance in Object.FindObjectsByType<ContextSpeak.Cafe.PlayerAppearance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (appearance == null || appearance.PreviewOnly) continue;
                foreach (var visual in new[] { appearance.Girl, appearance.Boy })
                {
                    if (visual == null || visual.transform.parent != appearance.transform) continue;
                    if (Quaternion.Angle(visual.transform.localRotation, Quaternion.identity) < 1f) continue;
                    var angle = visual.transform.localEulerAngles.y;
                    visual.transform.localRotation = Quaternion.identity;
                    fixedCount++;
                    Debug.Log("[PlayerFacing] " + visual.name + " visual was turned " + angle.ToString("0") + "° away from the controller's forward — reset (scene " + appearance.gameObject.scene.name + ").");
                }
            }
            return fixedCount;
        }
    }
}
