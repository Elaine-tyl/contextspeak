using UnityEngine;

namespace ContextSpeak.Life
{
    /// Anything the player can use with E. Scenes configure these instead of duplicating systems.
    public abstract class InteractableObject : MonoBehaviour
    {
        public string Prompt = "Use";
        public float Range = 2.2f;
        public virtual bool CanInteract => isActiveAndEnabled;
        public virtual string Label => Prompt;
        public abstract void Interact(InteractionManager manager);
        public virtual Vector3 Point => transform.position;
    }
}
