using System.Collections.Generic;
using ContextSpeak.Slice;
using UnityEngine;

namespace ContextSpeak.City
{
    /// A passer-by who walks a loop of pavement corners, waits at crossings on red,
    /// and pauses now and then to look around.
    public sealed class CityPedestrian : SliceWalker
    {
        public static readonly List<CityPedestrian> All = new List<CityPedestrian>();
        public Vector3[] Path = new Vector3[0];
        private int target;
        private float pause;
        private Animator animator;
        private bool hasSpeed;

        protected override void Awake()
        {
            base.Awake();
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.name == "Speed") hasSpeed = true;
        }

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Update()
        {
            if (Path.Length < 2) return;
            if (pause > 0)
            {
                pause -= Time.deltaTime;
                Bob(0);
                SetSpeed(0);
                return;
            }
            if (MoveTo(Path[target]))
            {
                target = (target + 1) % Path.Length;
                if (Random.value < 0.25f) pause = Random.Range(1.5f, 4f);
            }
            SetSpeed(agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : Speed);
        }

        private void SetSpeed(float value)
        {
            if (hasSpeed && animator != null) animator.SetFloat("Speed", value);
        }
    }
}
