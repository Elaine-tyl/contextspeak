using System;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    public enum NpcCapability { FamilyErrand, BuyGroceries, OrderDrink, PayBill, SocialChat }
    [CreateAssetMenu(menuName = "ContextSpeak/NPC")]
    public sealed class NpcDefinition : ScriptableObject
    {
        public string Id, DisplayName, Role;
        public NpcCapability[] Capabilities = Array.Empty<NpcCapability>();
        public bool Supports(NpcCapability capability) => Array.IndexOf(Capabilities, capability) >= 0;
    }
}
