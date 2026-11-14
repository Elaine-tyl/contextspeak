using ContextSpeak.Cafe;
using UnityEngine;
namespace ContextSpeak.Proactive
{
    [RequireComponent(typeof(CafePlayer))]
    public sealed class NPCPlayerCollisionInteraction : MonoBehaviour
    {
        private CafePlayer player;
        private void Awake() { player = GetComponent<CafePlayer>(); }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > .5f || ProactiveLifeDirector.Instance == null) return;
            var npc = hit.collider.GetComponentInParent<ProactiveNPCController>();
            if (npc == null) return;
            var relative = player.Velocity - npc.Velocity;
            relative.y = 0;
            var normal = new Vector3(-hit.normal.x, 0, -hit.normal.z).normalized;
            var contact = new ContactEvidence {
                ActualContact = true, ClosingSpeed = Vector3.Dot(relative, normal),
                ApproachDot = relative.sqrMagnitude < .001f ? 0 : Vector3.Dot(relative.normalized, normal),
                ObstacleClear = npc.Detector.SafeRetreat(normal), NpcAvailable = npc.Available,
            };
            if (CollisionPolicy.Candidate(contact)) ProactiveLifeDirector.Instance.Contact(npc, contact);
        }
    }
}
