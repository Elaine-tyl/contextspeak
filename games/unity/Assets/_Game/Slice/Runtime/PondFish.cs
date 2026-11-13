using System;
using UnityEngine;

namespace ContextSpeak.Slice
{
    /// Fish swimming lazy loops under the pond surface.
    public sealed class PondFish : MonoBehaviour
    {
        public Vector3 Centre;
        public Vector2 Radius = new Vector2(6, 4);
        public float Speed = 0.2f, Phase;
        private void Update()
        {
            var t = Time.time * Speed + Phase;
            var position = Centre + new Vector3(Mathf.Cos(t) * Radius.x * (0.6f + 0.4f * Mathf.Sin(t * 0.37f + Phase)), Mathf.Sin(t * 2.3f) * 0.08f, Mathf.Sin(t * 1.3f) * Radius.y);
            var direction = position - transform.position;
            transform.position = position;
            if (direction.sqrMagnitude > 0.00001f) transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);
        }
    }
}
