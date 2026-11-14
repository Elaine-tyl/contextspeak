using System;
using System.Collections.Generic;

namespace ContextSpeak.Life
{
    /// Ownership and one-press back dispatch shared by independent legacy presenters.
    /// A required form consumes Back without removing its owner or changing gameplay.
    public sealed class LegacyModalStack
    {
        private sealed class Layer
        {
            internal object Owner;
            internal Func<bool> Back;
            internal int Priority;
            internal long Order;
        }
        private readonly List<Layer> layers = new List<Layer>();
        private long order;
        private int lastBack = int.MinValue;
        public bool Open => layers.Count != 0;
        private Layer Top
        {
            get
            {
                Layer top = null;
                foreach (var layer in layers)
                    if (top == null || layer.Priority > top.Priority || layer.Priority == top.Priority && layer.Order > top.Order) top = layer;
                return top;
            }
        }
        public object TopOwner => Top?.Owner;
        public bool Set(object owner, bool open, Func<bool> back = null, int priority = 0)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            for (var i = 0; i < layers.Count; i++)
            {
                if (!ReferenceEquals(layers[i].Owner, owner)) continue;
                if (!open) layers.RemoveAt(i);
                else { layers[i].Back = back; layers[i].Priority = priority; }
                return !open;
            }
            if (!open) return false;
            layers.Add(new Layer { Owner = owner, Back = back, Priority = priority, Order = ++order });
            return true;
        }
        public bool Back(int press, Action whenEmpty = null)
        {
            if (lastBack == press) return true;
            lastBack = press;
            var top = Top;
            if (top != null) { top.Back?.Invoke(); return true; }
            if (whenEmpty == null) return false;
            whenEmpty();
            return true;
        }
    }
}
