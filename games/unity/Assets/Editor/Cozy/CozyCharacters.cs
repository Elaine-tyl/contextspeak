using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Both protagonist visuals share the existing player controller and hand anchor.
    public static class CozyCharacters
    {
        public static void DressPlayer(Transform avatar)
        {
            var appearance = avatar.gameObject.AddComponent<PlayerAppearance>();
            appearance.Girl = MeshyCharacters.Create("Academy", avatar.position);
            appearance.Boy = MeshyCharacters.Create("Daydream", avatar.position);
            appearance.Girl.transform.SetParent(avatar, true);
            appearance.Boy.transform.SetParent(avatar, true);
            // Visuals face the Avatar's +Z; never inherit the spawn yaw (that made the player walk backwards).
            appearance.Girl.transform.localRotation = Quaternion.identity;
            appearance.Boy.transform.localRotation = Quaternion.identity;
            appearance.Boy.SetActive(false);
        }
    }
}
