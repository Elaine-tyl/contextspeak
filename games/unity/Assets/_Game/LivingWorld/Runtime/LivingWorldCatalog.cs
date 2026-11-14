using System;
using UnityEngine;

namespace ContextSpeak.LivingWorld
{
    [Serializable]
    public sealed class StoryVenue
    {
        public string Id, Title, Scene, Model;
        public Vector3 Position;
    }

    [CreateAssetMenu(menuName = "ContextSpeak/Living world stories", fileName = "LivingWorldCatalog")]
    public sealed class LivingWorldCatalog : ScriptableObject
    {
        public StoryTemplate[] Events;
        public StoryVenue[] Venues;
    }

}
