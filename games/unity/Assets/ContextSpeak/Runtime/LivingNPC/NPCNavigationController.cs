using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Life
{
    /// NavMesh walking for a living NPC: per-person walking speed, jogging when late, smooth
    /// acceleration and turning, correct stopping distances, local avoidance of people, and road
    /// crossings: the road is not walkable except at the crossing, and the NPC waits at the kerb until
    /// the pedestrian light is green. Doors open for NPCs (DoorController). Off-NavMesh it walks in
    /// straight lines so nothing ever freezes.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NPCNavigationController : MonoBehaviour
    {
        public float WalkSpeed = 1.3f, JogSpeed = 2.7f;
        public bool Jogging;
        public Vector3 Destination { get; private set; }
        public bool HasDestination { get; private set; }
        public bool WaitingAtCrossing { get; private set; }
        public float Stuck { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public float Speed => Agent != null && Agent.enabled ? Agent.velocity.magnitude : manualSpeed;

        private float manualSpeed, stopDistance = 0.2f, stalled;
        private Vector3 lastPosition;
        private LivingCrossing crossing;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Agent.radius = 0.28f;
            Agent.height = 1.7f;
            Agent.acceleration = 5.5f;
            Agent.angularSpeed = 320;
            Agent.autoBraking = true;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            Agent.avoidancePriority = Random.Range(35, 65);
            Agent.autoRepath = true;
        }

        public bool OnMesh => Agent != null && Agent.enabled && Agent.isOnNavMesh;

        public void Enable(bool on)
        {
            if (Agent == null) return;
            if (on && !Agent.enabled)
            {
                Agent.enabled = true;
                if (!Agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) Agent.Warp(hit.position);
            }
            else if (!on && Agent.enabled) Agent.enabled = false;
        }

        public void Go(Vector3 destination, float stopAt = 0.2f)
        {
            Destination = destination;
            HasDestination = true;
            stopDistance = stopAt;
            Stuck = 0;
            stalled = 0;
            if (OnMesh)
            {
                Agent.updateRotation = true;
                Agent.isStopped = false;
                Agent.stoppingDistance = stopAt;
                if (NavMesh.SamplePosition(destination, out var hit, 1.5f, NavMesh.AllAreas)) Agent.SetDestination(hit.position);
                else Agent.SetDestination(destination);
            }
        }

        public void Stop()
        {
            HasDestination = false;
            WaitingAtCrossing = false;
            if (OnMesh) { Agent.isStopped = true; Agent.ResetPath(); }
            manualSpeed = 0;
        }

        public bool Arrived
        {
            get
            {
                if (!HasDestination) return true;
                if (OnMesh)
                {
                    if (Agent.pathPending) return false;
                    if (Agent.pathStatus == NavMeshPathStatus.PathInvalid || stalled > 4) return true;   // unreachable: as close as it gets
                    return Agent.remainingDistance <= stopDistance + 0.08f;
                }
                var d = Destination - transform.position; d.y = 0;
                return d.magnitude <= stopDistance + 0.05f;
            }
        }

        /// Called by LivingNPC every frame (full tier) or at a lower rate (background tier).
        public void Tick(float dt, Vector3 personalSpeedFactor)
        {
            var speed = (Jogging ? JogSpeed : WalkSpeed) * personalSpeedFactor.x;
            if (OnMesh)
            {
                Agent.speed = speed;
                // Pedestrian lights: hold at the kerb while the next stretch of path crosses the road on red.
                crossing = HasDestination ? LivingCrossing.Ahead(Agent, transform.position) : null;
                var hold = crossing != null && !crossing.WalkAllowed && !crossing.OnCrossing(transform.position);
                if (hold != WaitingAtCrossing)
                {
                    WaitingAtCrossing = hold;
                    Agent.isStopped = hold || !HasDestination;
                }
                if (HasDestination && !hold && Agent.velocity.magnitude < 0.05f && !Agent.pathPending && Agent.remainingDistance > stopDistance + 0.3f)
                {
                    Stuck += dt;
                    if (Stuck > 3) { Go(Destination, stopDistance); Stuck = 0.01f; }   // repath around whatever blocks it
                }
                else Stuck = 0;
                stalled = HasDestination && !hold && !Agent.pathPending && Agent.velocity.magnitude < 0.05f && Agent.remainingDistance > stopDistance + 0.08f ? stalled + dt : 0;
            }
            else if (HasDestination)
            {
                var d = Destination - transform.position; d.y = 0;
                manualSpeed = Mathf.MoveTowards(manualSpeed, d.magnitude > stopDistance ? speed : 0, 4 * dt);
                if (d.magnitude > stopDistance)
                {
                    transform.position += d.normalized * Mathf.Min(d.magnitude, manualSpeed * dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-6 * dt));
                }
            }
            lastPosition = transform.position;
        }

        public void Face(Vector3 direction, float dt)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (OnMesh) Agent.updateRotation = !(Arrived || !HasDestination);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240 * dt);
        }

        public Vector3[] Corners() => OnMesh && Agent.hasPath ? Agent.path.corners : new Vector3[0];
    }
}
