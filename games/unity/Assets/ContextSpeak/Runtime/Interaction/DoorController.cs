using UnityEngine;

namespace ContextSpeak.Life
{
    /// Automatic door: sliding glass leaves part (hospital, restaurant) or a hinged leaf swings,
    /// whenever the player or an NPC is close. Optionally locked until a quest step unlocks it.
    public sealed class DoorController : MonoBehaviour
    {
        public Transform LeftLeaf, RightLeaf;   // sliding: leaves move along local X
        public Transform Hinge;                 // hinged: rotates around local Y
        public float SlideDistance = 1.1f, SwingAngle = 92f, Range = 3f, Speed = 2.5f;
        public bool Locked;
        public string LockedMessage = "";
        private Vector3 leftClosed, rightClosed;
        private float open;
        private Transform player;

        private void Start()
        {
            if (LeftLeaf != null) leftClosed = LeftLeaf.localPosition;
            if (RightLeaf != null) rightClosed = RightLeaf.localPosition;
            var found = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            if (found != null) player = found.transform;
        }

        private bool SomeoneNear()
        {
            if (player != null && (player.position - transform.position).sqrMagnitude < Range * Range) return true;
            foreach (var living in LivingNPC.All)
                if (living != null && (living.transform.position - transform.position).sqrMagnitude < 2.5f * 2.5f) return true;
            foreach (var npc in NPCController.All)
                if (npc != null && (npc.transform.position - transform.position).sqrMagnitude < 2.5f * 2.5f) return true;
            return false;
        }

        private void Update()
        {
            var wanted = !Locked && SomeoneNear() ? 1f : 0f;
            open = Mathf.MoveTowards(open, wanted, Speed * Time.deltaTime);
            var eased = Mathf.SmoothStep(0, 1, open);
            if (LeftLeaf != null) LeftLeaf.localPosition = leftClosed + Vector3.left * SlideDistance * eased;
            if (RightLeaf != null) RightLeaf.localPosition = rightClosed + Vector3.right * SlideDistance * eased;
            if (Hinge != null) Hinge.localRotation = Quaternion.Euler(0, SwingAngle * eased, 0);
        }
    }
}
