using UnityEngine;

namespace ContextSpeak.Cafe
{
    [CreateAssetMenu(menuName = "ContextSpeak/Life quest")]
    public sealed class LifeQuestDefinition : ScriptableObject
    {
        public TextAsset Catalog;
        public LifeCatalog Read() => JsonUtility.FromJson<LifeCatalog>(Catalog.text);
    }
}
