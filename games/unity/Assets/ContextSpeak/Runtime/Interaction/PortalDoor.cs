using UnityEngine;

namespace ContextSpeak.Life
{
    /// Press E at an entrance to load another scene; the city remembers which door you used.
    public sealed class PortalDoor : InteractableObject
    {
        public string TargetScene = "MetroCity";
        public string Entrance = "";
        public bool Exit;                     // true: back to where you came from
        public override string Label => Prompt;
        public override void Interact(InteractionManager manager)
        {
            var ok = Exit ? SceneTransitionManager.Leave(TargetScene)
                : SceneTransitionManager.Enter(TargetScene, Entrance, manager.Player != null ? manager.Player.transform : null);
            if (!ok) LifeUI.Toast(SceneTransitionManager.LastError);
        }
    }
}
