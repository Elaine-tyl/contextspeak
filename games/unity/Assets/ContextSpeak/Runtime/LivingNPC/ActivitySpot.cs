using UnityEngine;

namespace ContextSpeak.Life
{
    public enum SpotKind
    {
        Seat, Counter, CoffeeMachine, Register, Pickup, QueuePoint, Shelf, Checkout, Bed, Sofa, Stove, DiningSeat,
        Desk, Board, Bench, Wait, Sink, TV, Table, Viewpoint, Door, TramSeat,
    }

    /// One reservable place to do something: a seat, the coffee machine, a shelf, a bed. Only one person
    /// can hold it (no two NPCs on one chair); its transform is where to stand/sit and which way to face.
    public sealed class ActivitySpot : MonoBehaviour
    {
        public SpotKind Kind = SpotKind.Seat;
        [Tooltip("Spots with the same group belong together (seats at one table).")] public string Group = "";
        [Tooltip("Seat height above the floor (m) for sitting spots.")] public float SeatHeight = 0.45f;
        [Tooltip("Optional object the action is about (the machine, the shelf).")] public Transform Target;
        public LivingNPC Occupant { get; private set; }
        public bool PlayerUsing;

        public bool Free(LivingNPC who) => !PlayerUsing && (Occupant == null || Occupant == who || !Occupant.isActiveAndEnabled);

        public bool Reserve(LivingNPC who)
        {
            if (!Free(who)) return false;
            Occupant = who;
            return true;
        }

        public void Release(LivingNPC who) { if (Occupant == who) Occupant = null; }

        public bool Sitting => Kind == SpotKind.Seat || Kind == SpotKind.Sofa || Kind == SpotKind.DiningSeat || Kind == SpotKind.Bench || Kind == SpotKind.Desk || Kind == SpotKind.TramSeat;

        private void OnDrawGizmos()
        {
            Gizmos.color = Occupant != null ? new Color(1, 0.4f, 0.3f) : new Color(0.3f, 1, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.18f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.05f, transform.forward * 0.4f);
        }
    }
}
