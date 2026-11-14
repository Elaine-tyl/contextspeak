using UnityEngine;

namespace ContextSpeak.Life
{
    /// "Talk to …" on an NPC. The location decides which scenario runs (it depends on quest progress).
    public sealed class NPCDialogueController : InteractableObject
    {
        public NPCController Npc;
        public override bool CanInteract => base.CanInteract && LocationFlow.Current != null && !LocationFlow.Current.Busy;
        public override string Label => "Talk to " + (Npc != null ? Npc.DisplayName + (Npc.Role != "" ? " (" + Npc.Role + ")" : "") : name);
        public override Vector3 Point => transform.position + Vector3.up;
        public override void Interact(InteractionManager manager)
        {
            if (LocationFlow.Current != null) LocationFlow.Current.Talk(Npc);
        }
    }
}
