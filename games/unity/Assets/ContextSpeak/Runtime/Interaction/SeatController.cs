using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A chair, bench or sofa seat. Facing direction is the seat's forward.
    public sealed class SeatController : InteractableObject
    {
        public string Group = "";             // e.g. table id, "waiting"
        public bool Occupied;
        public bool Reserved;                 // only usable when the location allows it
        public float SeatHeight = 0.45f;
        public Action Seated, Stood;
        public Func<bool> Allowed;
        public bool CanStand = true;
        public Vector3 SitPoint => transform.position + Vector3.up * 0.05f;
        public Vector3 StandPoint => transform.position + transform.forward * 0.75f + Vector3.up * 0.1f;
        public override bool CanInteract => base.CanInteract && !Occupied && (Allowed == null || Allowed());
        public override string Label => "Sit down";
        public override void Interact(InteractionManager manager) => manager.Sit(this);
    }
}
