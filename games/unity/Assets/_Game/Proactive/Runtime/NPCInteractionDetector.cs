using ContextSpeak.Cafe;
using UnityEngine;
namespace ContextSpeak.Proactive
{
    public sealed class NPCInteractionDetector : MonoBehaviour
    {
        private readonly RaycastHit[] hits = new RaycastHit[12];
        public bool ClearPath(CafePlayer player, float distance = 5)
        {
            if (player == null) return false;
            var offset = player.transform.position - transform.position;
            if (offset.sqrMagnitude > distance * distance || Mathf.Abs(offset.y) > 1.5f) return false;
            var start = transform.position + Vector3.up * 1.1f;
            var delta = player.transform.position + Vector3.up - start;
            var count = Physics.RaycastNonAlloc(start, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(transform) && !hits[i].transform.IsChildOf(player.transform)) return false;
            return true;
        }
        public bool SafeRetreat(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) return false;
            var count = Physics.SphereCastNonAlloc(transform.position + Vector3.up, .25f, direction.normalized, hits, .3f, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++) if (!hits[i].transform.IsChildOf(transform)) return false;
            return true;
        }
    }
}
