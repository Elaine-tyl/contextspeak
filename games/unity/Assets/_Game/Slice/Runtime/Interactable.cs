using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Slice
{
    /// Something the player can use with E (coffee machine, menu board, bakery display…).
    public sealed class Interactable : MonoBehaviour
    {
        public static Interactable Nearest(Interactable[] items, Vector3 position, string preferred = "")
        {
            var nearby = items.Where(item => item != null && Vector3.Distance(item.transform.position, position) < item.Range);
            return nearby.FirstOrDefault(item => preferred != "" && item.Id == preferred)
                ?? nearby.OrderBy(item => Vector3.Distance(item.transform.position, position)).FirstOrDefault();
        }
        public string Id;
        public string Prompt = "Use";
        public float Range = 2.2f;
        public Transform Arrival; // Optional safe map-travel point for a destination such as the lodge.
    }
}
