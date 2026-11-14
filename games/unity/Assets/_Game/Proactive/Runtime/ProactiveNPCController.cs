using System.Collections;
using ContextSpeak.Cafe;
using ContextSpeak.City;
using ContextSpeak.Life;
using UnityEngine;
using UnityEngine.AI;
namespace ContextSpeak.Proactive
{
    [DisallowMultipleComponent]
    public sealed class ProactiveNPCController : MonoBehaviour
    {
        public ProactiveNPCProfile Profile;
        public NPCSocialProfile Social = new NPCSocialProfile();
        public NPCInteractionDetector Detector { get; private set; }
        public NPCReactionAnimator Reaction { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Held { get; private set; }
        public bool MeetingArrived { get; private set; }
        public string MeetingLocation { get; private set; } = "";
        public bool Available => isActiveAndEnabled && (!Held || MeetingArrived)
            && (living == null || MeetingArrived || !living.InConversation && !living.Riding && !living.Hidden && living.Interruptible);
        private LivingNPC living;
        private NPCController legacy;
        private CityPedestrian pedestrian;
        private NavMeshAgent agent;
        private bool oldTalking, oldLegacyEnabled, oldPedestrian, wasStopped;
        private Vector3 previous;
        private float avoidUntil;
        private Vector3? meetingTarget;
        private Transform approaching;
        private void Awake()
        {
            living = GetComponent<LivingNPC>(); legacy = GetComponent<NPCController>();
            pedestrian = GetComponent<CityPedestrian>(); agent = GetComponent<NavMeshAgent>();
            Detector = GetComponent<NPCInteractionDetector>() ?? gameObject.AddComponent<NPCInteractionDetector>();
            Reaction = GetComponent<NPCReactionAnimator>() ?? gameObject.AddComponent<NPCReactionAnimator>();
            previous = transform.position;
        }
        public string Id => Profile != null ? Profile.Definition.Id : Social.Id;
        public string DisplayName => Profile != null ? Profile.Definition.Name : Social.Name;
        public NPCSocialProfile Definition => Profile != null ? Profile.Definition : Social;
        public void Hold(Transform player, string kind)
        {
            if (Held) { meetingTarget = null; Reaction.React(kind, player); return; }
            Held = true;
            if (living != null) living.Interrupt(WaitForRelease());
            if (legacy != null)
            { oldTalking = legacy.Talking; oldLegacyEnabled = legacy.enabled; legacy.Talking = true; legacy.enabled = false; }
            if (pedestrian != null) { oldPedestrian = pedestrian.enabled; pedestrian.enabled = false; }
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) { wasStopped = agent.isStopped; agent.isStopped = true; }
            // LivingNPC.Interrupt cleans up its previous gesture; apply the reaction afterwards.
            Reaction.React(kind, player);
        }
        public void MeetAt(Vector3 target, Transform player, string location)
        {
            Hold(player, "greeting");
            meetingTarget = target; MeetingLocation = location; MeetingArrived = false;
            if (living != null) living.Navigation.Go(target, .8f);
            else if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            { agent.isStopped = false; agent.SetDestination(target); }
        }
        public void Approach(Transform player)
        {
            approaching = player;
            meetingTarget = player.position;
        }
        private IEnumerator WaitForRelease()
        {
            living.InConversation = true;
            living.Navigation.Stop();
            while (Held) yield return null;
            living.InConversation = false;
        }
        public void Release()
        {
            if (!Held) return;
            Held = false;
            meetingTarget = null; approaching = null; MeetingArrived = false; MeetingLocation = "";
            if (legacy != null) { legacy.Talking = oldTalking; legacy.enabled = oldLegacyEnabled; }
            if (pedestrian != null) pedestrian.enabled = oldPedestrian;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = wasStopped;
            Reaction.Release();
        }
        private void Update()
        {
            Velocity = (transform.position - previous) / Mathf.Max(Time.deltaTime, .001f);
            previous = transform.position;
            var director = ProactiveLifeDirector.Instance;
            var player = director != null ? director.Player : null;
            if (meetingTarget.HasValue)
            {
                if (approaching != null)
                {
                    var toward = approaching.position - transform.position; toward.y = 0;
                    meetingTarget = approaching.position - toward.normalized * 1.7f;
                    if (living != null) living.Navigation.Go(meetingTarget.Value, .2f);
                    else if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    { agent.isStopped = false; agent.SetDestination(meetingTarget.Value); }
                }
                var delta = meetingTarget.Value - transform.position; delta.y = 0;
                if (delta.magnitude < 1)
                {
                    MeetingArrived = MeetingLocation != ""; meetingTarget = null; approaching = null;
                    if (living != null) living.Navigation.Stop();
                    else if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
                }
                // With no baked navigation, use a short obstacle-checked step; never teleport through walls.
                else if (living == null && (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh))
                {
                    var length = Mathf.Min(1.25f * Time.deltaTime, delta.magnitude);
                    if (!Physics.SphereCast(transform.position + Vector3.up, .28f, delta.normalized, out _, length + .1f, ~0, QueryTriggerInteraction.Ignore))
                    { transform.position += delta.normalized * length; transform.rotation = Quaternion.LookRotation(delta); }
                }
                return;
            }
            if (Held && player != null)
            {
                var delta = player.transform.position - transform.position; delta.y = 0;
                if (delta.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 5);
                return;
            }
            // Yield before physical contact. Existing NavMesh avoidance still handles NPC/NPC flow.
            if (player == null || living != null) return;
            var offset = player.transform.position - transform.position; offset.y = 0;
            var shouldYield = offset.sqrMagnitude < 1.25f && Vector3.Dot(transform.forward, offset.normalized) > .25f;
            if (shouldYield) avoidUntil = Time.time + .3f;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = Time.time < avoidUntil;
        }
        private void OnDisable() { Release(); }
    }
}
