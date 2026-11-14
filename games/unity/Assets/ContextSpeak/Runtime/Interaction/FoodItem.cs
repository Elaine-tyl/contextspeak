using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// A served dish or drink. E takes a bite or a sip; it shrinks and is finished after a few uses.
    public sealed class FoodItem : InteractableObject
    {
        public string DisplayName = "Dish";
        public bool Drink;
        public int Bites = 3;
        public Action<FoodItem> Finished;
        private int taken;
        private Vector3 size;

        private void Start() { size = transform.localScale; Range = 2.6f; }
        public bool Done => taken >= Bites;
        public override bool CanInteract => base.CanInteract && !Done;
        public override string Label => (Drink ? "Drink the " : "Eat the ") + DisplayName;

        public override void Interact(InteractionManager manager)
        {
            taken++;
            transform.localScale = Vector3.Scale(size, new Vector3(1, Mathf.Max(0.15f, 1f - (float)taken / Bites), 1));
            LifeUI.Toast(Drink ? "You take a sip of the " + DisplayName + "." : "You eat some " + DisplayName + ". Delicious!");
            if (Done) Finished?.Invoke(this);
        }
    }
}
