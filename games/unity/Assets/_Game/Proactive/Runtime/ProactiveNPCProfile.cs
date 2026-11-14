using UnityEngine;
namespace ContextSpeak.Proactive
{
    [CreateAssetMenu(menuName = "ContextSpeak/NPC/Proactive social profile")]
    public sealed class ProactiveNPCProfile : ScriptableObject
    {
        public NPCSocialProfile Definition = new NPCSocialProfile();
        [TextArea] public string ScheduleNotes = "Keep the existing NPC daily schedule and service post.";
    }
}
