using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Slice
{
    /// Helpers shared by NPC controllers: NavMesh movement with a straight-line fallback when no NavMesh is baked.
    public abstract class SliceWalker : MonoBehaviour
    {
        public float Speed = 1.6f;
        protected NavMeshAgent agent;
        protected Transform body;
        protected float bob;
        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (agent != null) { agent.speed = Speed; agent.angularSpeed = 540; agent.acceleration = 10; agent.stoppingDistance = 0.15f; }
        }
        protected virtual void Start()
        {
            if (agent != null) agent.enabled = true;
        }
        protected bool MoveTo(Vector3 target)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if ((agent.destination - target).sqrMagnitude > 0.04f) agent.SetDestination(target);
                var arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f;
                Bob(arrived ? 0 : agent.velocity.magnitude);
                return arrived;
            }
            var delta = target - transform.position;
            delta.y = 0;
            if (delta.magnitude < 0.1f) { Bob(0); return true; }
            var step = Mathf.Min(delta.magnitude, Speed * Time.deltaTime);
            transform.position += delta.normalized * step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 8);
            Bob(Speed);
            return false;
        }
        protected void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6);
        }
        /// Procedural walk bob, sit and gesture so NPCs are never motionless even without animation clips.
        protected void Bob(float speed, float sit = 0, float gesture = 0)
        {
            if (body == null || body == transform) return;
            bob += Time.deltaTime * (speed > 0.1f ? 9 : 2);
            var walk = speed > 0.1f ? Mathf.Abs(Mathf.Sin(bob)) * 0.05f : Mathf.Sin(bob) * 0.006f;
            body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0, walk - sit, 0), Time.deltaTime * 10);
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(gesture * Mathf.Sin(bob * 1.7f) * 4, 0, 0), Time.deltaTime * 6);
        }
    }
}
