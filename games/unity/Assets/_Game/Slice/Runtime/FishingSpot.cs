using System;
using UnityEngine;

namespace ContextSpeak.Slice
{
    /// Rod, line and float visuals for the pier. Game rules live in FishingSession.
    public sealed class FishingSpot : MonoBehaviour
    {
        public Transform Stand;         // where the player stands
        public Transform WaterTarget;   // where the float lands
        public Transform RodTip;        // attached to the player while fishing
        public GameObject Rod;
        public Material LineMaterial;
        public float Range = 2f;
        private LineRenderer line;
        private Transform bobber;
        private Vector3 restingFloat;

        private void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 12;
            line.widthMultiplier = 0.012f;
            line.material = new Material(LineMaterial); // a scene reference survives player shader stripping
            line.material.color = new Color(0.95f, 0.95f, 0.9f, 0.9f);
            line.enabled = false;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "Fishing float";
            ball.transform.localScale = Vector3.one * 0.12f;
            ball.GetComponent<Renderer>().material = new Material(LineMaterial) { color = new Color(0.9f, 0.25f, 0.18f) };
            bobber = ball.transform;
            bobber.gameObject.SetActive(false);
            if (Rod != null) Rod.SetActive(false);
        }

        /// Draw the current state. t = seconds since the cast started (for the arc animation).
        public void Show(FishingSession session, Transform hand, float t)
        {
            var active = session.State != FishingState.Idle;
            if (Rod != null)
            {
                Rod.SetActive(active);
                if (active && hand != null) { Rod.transform.position = hand.position; Rod.transform.rotation = Quaternion.LookRotation(WaterTarget.position - hand.position) * Quaternion.Euler(-35 - session.Tension * 25, 0, 0); }
            }
            line.enabled = active && session.State != FishingState.Caught && session.State != FishingState.Escaped;
            bobber.gameObject.SetActive(line.enabled);
            if (!line.enabled) return;
            var tip = RodTip != null ? RodTip.position : hand.position + Vector3.up * 1.6f;
            restingFloat = WaterTarget.position;
            Vector3 target;
            switch (session.State)
            {
                case FishingState.Casting:
                    var progress = Mathf.Clamp01(1 - session.Timer / 0.9f);
                    target = Vector3.Lerp(tip, restingFloat, progress) + Vector3.up * Mathf.Sin(progress * Mathf.PI) * 1.5f;
                    break;
                case FishingState.Nibble:
                    target = restingFloat + Vector3.down * Mathf.Abs(Mathf.Sin(t * 18)) * 0.04f;
                    break;
                case FishingState.Bite:
                    target = restingFloat + Vector3.down * 0.16f;
                    break;
                case FishingState.Reeling:
                    target = Vector3.Lerp(restingFloat, Stand.position + (restingFloat - Stand.position) * 0.15f, session.Progress) + new Vector3(Mathf.Sin(t * 7), 0, Mathf.Cos(t * 5)) * 0.25f * (1 - session.Progress);
                    target.y = restingFloat.y - 0.08f;
                    break;
                default:
                    target = restingFloat + Vector3.up * Mathf.Sin(t * 2) * 0.015f;
                    break;
            }
            bobber.position = target;
            var sag = session.State == FishingState.Reeling ? Mathf.Lerp(0.6f, 0.02f, session.Tension) : 0.5f;
            for (var i = 0; i < line.positionCount; i++)
            {
                var k = i / (float)(line.positionCount - 1);
                line.SetPosition(i, Vector3.Lerp(tip, target, k) + Vector3.down * Mathf.Sin(k * Mathf.PI) * sag);
            }
            line.material.color = session.Tension > 0.8f ? new Color(0.95f, 0.3f, 0.2f) : new Color(0.95f, 0.95f, 0.9f);
        }

        public void Hide()
        {
            if (line != null) line.enabled = false;
            if (bobber != null) bobber.gameObject.SetActive(false);
            if (Rod != null) Rod.SetActive(false);
        }
        private void OnDestroy()
        {
            if (line != null) Destroy(line.sharedMaterial);
            if (bobber != null)
            {
                Destroy(bobber.GetComponent<Renderer>().sharedMaterial);
                Destroy(bobber.gameObject);
            }
        }
    }
}
