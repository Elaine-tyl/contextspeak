using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A counter where someone serves a queue (café till, supermarket checkout): customers queue on
    /// the queue points in order, the server takes the front order, prepares it, hands it over at the
    /// pickup point and takes payment. Works for NPC customers and the player.
    public sealed class ServicePoint : MonoBehaviour
    {
        public sealed class Order
        {
            public LivingNPC Customer;
            public bool ForPlayer;
            public string Item = "flat white";
            public bool Taken, Ready, Collected, Paid;
            public GameObject Product;
        }

        public string ItemsCsv = "flat white,latte,cappuccino,long black,iced latte,chai latte";
        public Transform[] QueuePoints = new Transform[0];
        public Transform ServerPoint, PickupPoint, PreparePoint;
        public bool NeedsPreparation = true;
        public LivingNPC Server { get; set; }
        public readonly List<Order> Queue = new List<Order>();
        public readonly List<Order> Waiting = new List<Order>();
        public int Completed { get; private set; }

        public string[] Items => ItemsCsv.Split(',');

        public Order Join(LivingNPC customer)
        {
            foreach (var o in Queue) if (o.Customer == customer) return o;
            var order = new Order { Customer = customer };
            Queue.Add(order);
            return order;
        }

        public Order JoinPlayer(string item)
        {
            var order = new Order { ForPlayer = true, Item = item };
            Queue.Insert(0, order);                      // the player has just ordered at the counter
            return order;
        }

        public void Leave(LivingNPC customer)
        {
            Queue.RemoveAll(o => o.Customer == customer);
            Waiting.RemoveAll(o => o.Customer == customer);
        }

        public int Position(LivingNPC customer) => Queue.FindIndex(o => o.Customer == customer);

        public Vector3 QueuePosition(int index)
        {
            if (QueuePoints.Length == 0) return transform.position;
            if (index < QueuePoints.Length) return QueuePoints[index].position;
            var last = QueuePoints[QueuePoints.Length - 1].position;
            var step = QueuePoints.Length > 1 ? last - QueuePoints[QueuePoints.Length - 2].position : -transform.forward * 0.8f;
            return last + step * (index - QueuePoints.Length + 1);
        }

        /// Front customer standing at the counter and not yet served.
        public Order Front(System.Func<Order, bool> atCounter)
        {
            foreach (var o in Queue) if (!o.Taken && (o.ForPlayer || o.Customer != null && atCounter(o))) return o;
            return null;
        }

        public void Take(Order order)
        {
            order.Taken = true;
            Queue.Remove(order);
            Waiting.Add(order);
        }

        public void Finish(Order order)
        {
            order.Collected = true;
            Waiting.Remove(order);
            Completed++;
        }
    }
}
