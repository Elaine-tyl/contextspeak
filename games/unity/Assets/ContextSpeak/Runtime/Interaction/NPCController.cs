using System.Collections.Generic;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Life
{
    /// Work position, a simple daily schedule (walk between points and linger), and turning to face
    /// the player while talking. Walks on the NavMesh when baked, otherwise in straight lines.
    public sealed class NPCController : MonoBehaviour
    {
        public static readonly List<NPCController> All = new List<NPCController>();
        public string Key = "npc";
        public string DisplayName = "NPC", Role = "";
        public Vector3[] Schedule = new Vector3[0];   // points visited in order; empty = stays at post
        public float Linger = 8f, Speed = 1.3f;
        public bool Talking;
        public Vector3 Home;
        public Quaternion HomeFacing;
        private NavMeshAgent agent;
        private Transform body, player;
        private int next;
        private float wait;
        private Vector3? errand;
        private System.Action arrived;
        private Animator animator;
        private bool hasSpeed;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Start()
        {
            Home = transform.position;
            HomeFacing = transform.rotation;
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) { agent.enabled = true; agent.speed = Speed; agent.angularSpeed = 540; agent.stoppingDistance = 0.2f; }
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
            var found = FindFirstObjectByType<CafePlayer>();
            if (found != null) player = found.transform;
            animator = GetComponentInChildren<Animator>();
            if (animator != null) foreach (var parameter in animator.parameters) if (parameter.name == "Speed") hasSpeed = true;
            wait = Random.Range(1f, Linger);
            if (GetComponent<NpcLifelike>() == null) gameObject.AddComponent<NpcLifelike>();
        }

        /// Send the NPC somewhere now (e.g. waiter to a table); `done` runs on arrival.
        public void GoTo(Vector3 point, System.Action done = null)
        {
            errand = point;
            arrived = done;
        }

        public void ReturnHome(System.Action done = null) => GoTo(Home, () => { transform.rotation = HomeFacing; done?.Invoke(); });

        private void Update()
        {
            if (Talking && player != null)
            {
                Stop();
                Face(player.position - transform.position);
                return;
            }
            if (errand.HasValue)
            {
                if (Move(errand.Value))
                {
                    errand = null;
                    var done = arrived;
                    arrived = null;
                    done?.Invoke();
                }
                return;
            }
            if (Schedule.Length == 0)
            {
                Stop();
                if (player != null && (player.position - transform.position).sqrMagnitude < 9) Face(player.position - transform.position);
                else transform.rotation = Quaternion.Slerp(transform.rotation, HomeFacing, Time.deltaTime * 2);
                return;
            }
            // At night or in the rain people with walking routines stay at their post.
            if (SkyState.Sheltering && Application.isPlaying)
            {
                if ((transform.position - Home).sqrMagnitude > 0.25f) { if (Move(Home)) transform.rotation = HomeFacing; }
                else Stop();
                return;
            }
            if (wait > 0) { wait -= Time.deltaTime; Stop(); return; }
            if (Move(Schedule[next])) { next = (next + 1) % Schedule.Length; wait = Linger; }
        }

        private bool Move(Vector3 target)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if ((agent.destination - target).sqrMagnitude > 0.04f) agent.SetDestination(target);
                var done = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f;
                Animate(done ? 0 : agent.velocity.magnitude);
                return done;
            }
            var delta = target - transform.position;
            delta.y = 0;
            if (delta.magnitude < 0.15f) { Animate(0); return true; }
            transform.position += delta.normalized * Mathf.Min(delta.magnitude, Speed * Time.deltaTime);
            Face(delta);
            Animate(Speed);
            return false;
        }

        private void Stop()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            Animate(0);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6);
        }

        private float bob;
        private void Animate(float speed)
        {
            if (hasSpeed) animator.SetFloat("Speed", speed);
            if (body == null || body == transform) return;
            bob += Time.deltaTime * (speed > 0.1f ? 9 : 1.6f);
            var lift = speed > 0.1f ? Mathf.Abs(Mathf.Sin(bob)) * 0.04f : Mathf.Sin(bob) * 0.005f;   // breathing idle
            body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0, lift, 0), Time.deltaTime * 10);
            var actor = GetComponent<NpcActor>();
            if (actor != null && !Talking) actor.SetState(NpcState.Idle);
        }
    }
}
