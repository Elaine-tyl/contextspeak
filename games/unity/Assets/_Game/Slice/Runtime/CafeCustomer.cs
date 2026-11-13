using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Slice
{
    /// Ambient café customers: enter, queue, browse, order, wait, sit, eat and drink, chat, pay and leave.
    public sealed class CafeCustomer : SliceWalker
    {
        public enum Phase { Arriving, Queueing, Ordering, Waiting, Seating, Eating, Chatting, Leaving, Away }
        public Phase Current = Phase.Away;
        public Transform Door, Counter;
        public Seat[] Seats = new Seat[0];
        public GameObject Cup;
        public CafeCustomer Friend;
        public float Patience;
        private static readonly List<CafeCustomer> Queue = new List<CafeCustomer>();
        private Seat seat;
        private float timer;
        private System.Random random;

        protected override void Awake()
        {
            base.Awake();
            random = new System.Random(GetInstanceID());
            timer = (float)random.NextDouble() * 20;
        }
        private void OnDisable() { Queue.Remove(this); if (seat != null && seat.Occupant == this) seat.Occupant = null; }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (Cup != null) Cup.SetActive(Current == Phase.Eating || Current == Phase.Chatting);
            switch (Current)
            {
                case Phase.Away:
                    if (body != transform) body.gameObject.SetActive(false);
                    if (timer <= 0 && Door != null)
                    {
                        body.gameObject.SetActive(true);
                        if (agent != null && agent.isOnNavMesh) agent.Warp(Door.position); else transform.position = Door.position;
                        Current = Phase.Arriving;
                    }
                    break;
                case Phase.Arriving:
                    if (!Queue.Contains(this)) Queue.Add(this);
                    Current = Phase.Queueing;
                    break;
                case Phase.Queueing:
                    var place = Queue.IndexOf(this);
                    var spot = Counter.position + Counter.forward * (2.4f + place * 0.9f);
                    if (MoveTo(spot))
                    {
                        Face(-Counter.forward);
                        if (place == 0) { Current = Phase.Ordering; timer = 4 + (float)random.NextDouble() * 3; }
                    }
                    break;
                case Phase.Ordering:
                    Bob(0, 0, 1); // browsing the menu and talking to the barista
                    if (timer <= 0) { Queue.Remove(this); Current = Phase.Waiting; timer = 5 + (float)random.NextDouble() * 4; }
                    break;
                case Phase.Waiting:
                    if (MoveTo(Counter.position + Counter.right * 2.6f + Counter.forward * 2.6f)) Bob(0);
                    if (timer <= 0)
                    {
                        seat = Seats.Where(candidate => candidate.Free).OrderBy(_ => random.Next()).FirstOrDefault();
                        if (seat == null) { Current = Phase.Leaving; break; }
                        seat.Occupant = this;
                        Current = Phase.Seating;
                    }
                    break;
                case Phase.Seating:
                    if (MoveTo(seat.SitPoint)) { Current = Phase.Eating; timer = 25 + (float)random.NextDouble() * 30; if (agent != null) agent.enabled = false; }
                    break;
                case Phase.Eating:
                    Face(seat.Facing);
                    Bob(0, 0.42f, Friend != null && Friend.Current == Phase.Eating ? 1 : 0.3f);
                    if (timer <= 0) { Current = Phase.Chatting; timer = 6; }
                    break;
                case Phase.Chatting:
                    Bob(0, 0.42f, 1);
                    if (timer <= 0)
                    {
                        seat.Occupant = null;
                        seat = null;
                        if (agent != null) { agent.enabled = true; agent.Warp(transform.position); }
                        Current = Phase.Leaving;
                    }
                    break;
                case Phase.Leaving:
                    if (Door == null || MoveTo(Door.position)) { Current = Phase.Away; timer = 15 + (float)random.NextDouble() * 25; }
                    break;
            }
        }
    }
}
