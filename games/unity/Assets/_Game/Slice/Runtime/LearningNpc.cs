using UnityEngine;

namespace ContextSpeak.Slice
{
    /// An authored scene actor. Dialogue state lives in the save, not on the visual prefab.
    public sealed class LearningNpc : MonoBehaviour
    {
        public string SceneId;
        public string NameOverride, RoleOverride;
        public Vector3 Arrival;
        public LessonPlace Place => LifeCurriculum.Find(SceneId);
        public string DisplayName => string.IsNullOrEmpty(NameOverride) ? Place.Npc : NameOverride;
        public string DisplayRole => string.IsNullOrEmpty(RoleOverride) ? Place.Role : RoleOverride;
    }
}
