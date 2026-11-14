using UnityEngine;

namespace ContextSpeak.LivingWorld
{
    /// Separate anchors keep story invitations from replacing ordinary NPC tasks.
    public sealed class StoryLocationAnchor : MonoBehaviour
    {
        public string Id;
        public Transform Npc;
    }
}
