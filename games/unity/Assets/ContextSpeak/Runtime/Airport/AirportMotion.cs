using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    [Serializable]
    public struct PathPoint
    {
        public Vector3 Position;
        public float Speed;      // m/s on the way to this point
        public float Wait;       // seconds to stay after arriving
        public bool Jump;        // appear here instantly (e.g. reset an aircraft for the next landing)
        public PathPoint(Vector3 position, float speed, float wait = 0, bool jump = false) { Position = position; Speed = speed; Wait = wait; Jump = jump; }
    }

    /// Simple path-following for cars, buses, ground vehicles and aircraft (taxi, take-off, landing):
    /// eases between per-point speeds and pitches with the climb or descent.
    public sealed class PathMover : MonoBehaviour
    {
        public List<PathPoint> Points = new List<PathPoint>();
        public float YawOffset;
        public bool BankAndPitch;
        private int target = 1;
        private float speed, wait;

        private void Start()
        {
            if (Points.Count == 0) return;
            transform.position = Points[0].Position;
            speed = Points[Mathf.Min(1, Points.Count - 1)].Speed;
        }

        private void Update()
        {
            if (Points.Count < 2) return;
            if (wait > 0) { wait -= Time.deltaTime; return; }
            var point = Points[target];
            if (point.Jump)
            {
                transform.position = point.Position;
                Arrive(point);
                return;
            }
            speed = Mathf.MoveTowards(speed, point.Speed, Mathf.Max(2f, point.Speed * 0.25f) * Time.deltaTime);
            var delta = point.Position - transform.position;
            var step = Mathf.Max(0.5f, speed) * Time.deltaTime;
            if (delta.magnitude <= step) { transform.position = point.Position; Arrive(point); return; }
            transform.position += delta.normalized * step;
            var direction = BankAndPitch ? delta : new Vector3(delta.x, 0, delta.z);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction) * Quaternion.Euler(0, YawOffset, 0), Time.deltaTime * 2.5f);
        }

        private void Arrive(PathPoint point)
        {
            wait = point.Wait;
            target = (target + 1) % Points.Count;
        }
    }

    /// A conveyor: items ride from Start to End (check-in belt, X-ray, baggage carousel loop).
    public sealed class Conveyor : MonoBehaviour
    {
        public Transform[] Route = new Transform[0];
        public float Speed = 0.6f;
        public bool Loop;
        public Renderer Belt;
        private readonly List<(Transform item, int leg, float t)> riders = new List<(Transform, int, float)>();
        private MaterialPropertyBlock block;
        private float scroll;

        public void Add(Transform item)
        {
            if (Route.Length < 2 || item == null) return;
            item.position = Route[0].position;
            riders.Add((item, 0, 0));
        }

        public int Count => riders.Count;

        private void Update()
        {
            scroll += Time.deltaTime * Speed;
            if (Belt != null)
            {
                block ??= new MaterialPropertyBlock();
                Belt.GetPropertyBlock(block);
                block.SetVector("_BaseMap_ST", new Vector4(1, 4, 0, -scroll));
                Belt.SetPropertyBlock(block);
            }
            for (var i = riders.Count - 1; i >= 0; i--)
            {
                var (item, leg, t) = riders[i];
                if (item == null) { riders.RemoveAt(i); continue; }
                var from = Route[leg].position;
                var to = Route[(leg + 1) % Route.Length].position;
                var length = Mathf.Max(0.01f, Vector3.Distance(from, to));
                t += Speed * Time.deltaTime / length;
                if (t >= 1)
                {
                    t = 0;
                    leg++;
                    if (leg >= Route.Length - 1 && !Loop) { Destroy(item.gameObject); riders.RemoveAt(i); continue; }
                    leg %= Route.Length;
                    from = Route[leg].position; to = Route[(leg + 1) % Route.Length].position;
                }
                item.position = Vector3.Lerp(from, to, t);
                riders[i] = (item, leg, t);
            }
        }
    }

    /// Passengers in a queue shuffle forward: every few seconds the front person leaves for the
    /// exit and rejoins at the back, everyone else steps up one place.
    public sealed class QueueLine : MonoBehaviour
    {
        public Transform[] Slots = new Transform[0];
        public NPCController[] People = new NPCController[0];
        public Transform Exit;
        public float Interval = 9f;
        private float timer;
        private int offset;

        private void Start() { timer = Interval * (0.5f + UnityEngine.Random.value); }

        private void Update()
        {
            if (People.Length == 0 || Slots.Length == 0) return;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = Interval;
            offset = (offset + 1) % People.Length;
            for (var i = 0; i < People.Length; i++)
            {
                var person = People[i];
                if (person == null || person.Talking) continue;
                var place = (i - offset + People.Length) % People.Length;
                if (place == People.Length - 1 && Exit != null)
                {
                    var back = Slots[Mathf.Min(place, Slots.Length - 1)].position;
                    person.GoTo(Exit.position, () => person.GoTo(back));
                }
                else person.GoTo(Slots[Mathf.Min(place, Slots.Length - 1)].position);
            }
        }
    }
}
