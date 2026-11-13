using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Slice
{
    /// A chair the player or an NPC can sit on. The table id links deliveries to the right table.
    public sealed class Seat : MonoBehaviour
    {
        public string Table = "T1";
        public bool Outdoor;
        public Transform TableTop;
        [NonSerialized] public MonoBehaviour Occupant;
        public bool Free => Occupant == null;
        public Vector3 SitPoint => transform.position + Vector3.up * 0.05f;
        public Vector3 Facing => TableTop != null ? (TableTop.position - transform.position) : transform.forward;
    }
}
