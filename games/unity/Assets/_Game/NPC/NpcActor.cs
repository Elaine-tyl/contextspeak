using UnityEngine;

namespace ContextSpeak.Cafe
{
    public enum NpcState { Idle, Talking, Listening, Thinking }
    public sealed class NpcActor : MonoBehaviour
    {
        public NpcDefinition Definition;
        public Animator Animator;
        public NpcState State { get; private set; }
        public void SetState(NpcState state)
        {
            if (State == state) return;
            State = state;
            if (Animator != null) Animator.SetInteger("Conversation", (int)state);
        }
    }
}
