using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    [RequireComponent(typeof(CharacterController), typeof(WorldInput))]
    public sealed class CafePlayer : MonoBehaviour
    {
        public Camera View;
        public Transform Model;
        public Transform ConversationPartner;
        public bool Blocked;
        public readonly Queue<Vector3> Route = new Queue<Vector3>();
        private CharacterController controller;
        public Transform CameraPosition, CameraAim;
        public WorldInput Controls { get; private set; }
        private float yaw = 180;
        private float pitch = 34;
        private float distance = 6;
        private float stride;
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            Controls = GetComponent<WorldInput>();
        }
        private void Update()
        {
            var direction = Vector3.zero;
            if (!Blocked)
            {
                var movement = Controls.Movement;
                var input = new Vector3(movement.x, 0, movement.y);
                if (input.sqrMagnitude > 0) Route.Clear();
                if (Route.Count > 0)
                {
                    var delta = Route.Peek() - transform.position;
                    delta.y = 0;
                    if (delta.magnitude < 0.15f) Route.Dequeue();
                    else direction = delta.normalized;
                }
                else
                {
                    direction = Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(input, 1);
                }
                controller.SimpleMove(direction * 2.7f);
            }
            if (Model != null && direction.sqrMagnitude > 0.01f)
            {
                Model.rotation = Quaternion.Slerp(Model.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12);
                stride += Time.deltaTime * 10;
            }
            if (Model != null) Model.localPosition = new Vector3(0, direction.sqrMagnitude > 0 ? Mathf.Abs(Mathf.Sin(stride)) * 0.035f : 0, 0);
            if (!Blocked)
            {
                yaw += Controls.Look.x * 0.12f;
                pitch = Mathf.Clamp(pitch - Controls.Look.y * 0.1f, 18, 60);
            }
            if (!Blocked) distance = Mathf.Clamp(distance - Controls.Zoom * 0.3f, 3.5f, 8);
        }
        private void LateUpdate()
        {
            if (View == null) return;
            var target = transform.position + Vector3.up * 1.25f;
            var desired = target + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance);
            if (ConversationPartner != null)
            {
                target = ConversationPartner.position + Vector3.up * 0.65f;
                var away = transform.position - ConversationPartner.position;
                away.y = 0;
                away.Normalize();
                var shoulder = Vector3.Cross(Vector3.up, away);
                desired = target + away * 3.8f + shoulder * 1.8f + Vector3.up;
            }
            // Cinemachine is the only writer to the real camera transform.
            if (CameraPosition != null) CameraPosition.position = desired;
            if (CameraAim != null) CameraAim.position = target;
        }
        public void Place(Vector3 position)
        {
            Route.Clear();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }
    }
}
