using UnityEngine;
namespace ContextSpeak.Proactive
{
    [CreateAssetMenu(menuName = "ContextSpeak/NPC/Proactive life event")]
    public sealed class ProactiveEventAsset : ScriptableObject
    {
        public LifeEventDefinition Definition = new LifeEventDefinition();
    }
}
