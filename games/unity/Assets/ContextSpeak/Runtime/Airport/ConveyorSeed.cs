using UnityEngine;

namespace ContextSpeak.Life
{
    /// Puts this object onto the nearest conveyor after a delay (other passengers' bags on a carousel).
    public sealed class ConveyorSeed : MonoBehaviour
    {
        public float Delay;
        private float timer;
        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < Delay) return;
            var conveyor = FindFirstObjectByType<Conveyor>();
            if (conveyor != null) conveyor.Add(transform);
            Destroy(this);
        }
    }
}
