using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Life
{
    /// Walk-in scene changes between Metro City and the large locations. The current position is
    /// saved before leaving so the player returns to the same door; all progress lives in SaveManager.
    public static class SceneTransitionManager
    {
        public static string LastError { get; private set; } = "";

        public static bool Exists(string scene) => Application.CanStreamedLevelBeLoaded(scene);

        public static bool Enter(string scene, string entrance, Transform player)
        {
            if (!Exists(scene))
            {
                LastError = scene + " is not built yet. Run ContextSpeak > Build Life Simulation Locations.";
                Debug.LogWarning(LastError);
                return false;
            }
            var data = SaveManager.Data;
            data.returnScene = SceneManager.GetActiveScene().name;
            data.returnEntrance = entrance;
            if (player != null)
            {
                data.returnX = player.position.x;
                data.returnY = player.position.y;
                data.returnZ = player.position.z;
                data.returnYaw = player.eulerAngles.y;
            }
            SaveManager.Save();
            SceneManager.LoadScene(scene);
            return true;
        }

        /// Back to where the player came from (Metro City by default).
        public static bool Leave(string fallback = "MetroCity")
        {
            var target = string.IsNullOrEmpty(SaveManager.Data.returnScene) ? fallback : SaveManager.Data.returnScene;
            if (!Exists(target))
            {
                LastError = target + " is not in the build. Add it with its builder menu.";
                Debug.LogWarning(LastError);
                return false;
            }
            SceneManager.LoadScene(target);
            return true;
        }

        /// Called by the city when it loads: true (with the saved pose) if we are coming back out of a location.
        public static bool TakeReturnPoint(string scene, out Vector3 position, out float yaw)
        {
            var data = SaveManager.Data;
            position = new Vector3(data.returnX, data.returnY, data.returnZ);
            yaw = data.returnYaw;
            if (data.returnScene != scene || string.IsNullOrEmpty(data.returnEntrance)) return false;
            data.returnEntrance = "";
            SaveManager.Save();
            return true;
        }
    }
}
