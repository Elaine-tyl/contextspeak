using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Slice
{
    /// Rowan the waiter: waits at the counter, collects ready orders from the pass and carries them to the table.
    public sealed class WaiterNpc : SliceWalker
    {
        public Transform Counter, Pass;
        public GameObject Tray;
        public enum Job { AtCounter, ToPass, Carrying, Returning, Talking }
        public Job Current = Job.AtCounter;
        private Vector3 table;
        private Action delivered;
        public bool Busy => Current == Job.ToPass || Current == Job.Carrying;

        public void Deliver(Vector3 tablePosition, Action onDelivered)
        {
            table = tablePosition;
            delivered = onDelivered;
            Current = Job.ToPass;
        }
        public void Talk(bool talking)
        {
            if (talking && Current == Job.AtCounter) Current = Job.Talking;
            else if (!talking && Current == Job.Talking) Current = Job.AtCounter;
        }
        public void CancelDelivery()
        {
            delivered = null;
            Current = Job.Returning;
            if (Tray != null) Tray.SetActive(false);
        }
        private void Update()
        {
            if (Tray != null) Tray.SetActive(Current == Job.Carrying);
            switch (Current)
            {
                case Job.AtCounter:
                    if (Counter != null && MoveTo(Counter.position)) Face(Counter.forward);
                    break;
                case Job.Talking:
                    Bob(0, 0, 1);
                    break;
                case Job.ToPass:
                    if (Pass == null || MoveTo(Pass.position)) Current = Job.Carrying;
                    break;
                case Job.Carrying:
                    if (MoveTo(table))
                    {
                        delivered?.Invoke();
                        delivered = null;
                        Current = Job.Returning;
                    }
                    break;
                case Job.Returning:
                    if (Counter == null || MoveTo(Counter.position)) Current = Job.AtCounter;
                    break;
            }
        }
    }
}
