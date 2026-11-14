using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Swimming in a pond or pool (a box of water): once the water is chest deep you float, WASD swims
    /// (Shift faster), hold C to dive and Space to come up; swim into the bank to climb out, or into
    /// shallow water to stand up again. Ripples/splashes come from CharacterWaterInteraction and the
    /// ripple simulation (the simple ring lines below are only a fallback when those are missing).
    /// Reusable: set Water to the volume (top = surface, bottom = bed).
    public sealed class PondSwimming : MonoBehaviour
    {
        public Bounds Water = new Bounds(Vector3.zero, new Vector3(10, 1.5f, 10));
        public float FloatDepth = 1.3f, SwimSpeed = 1.4f, FastSpeed = 2.3f, DiveSpeed = 1.0f;
        [Tooltip("Water must be at least this deep (m) to swim; shallower water is waded.")] public float MinSwimDepth = 1.15f;
        public bool InWater { get; private set; }
        private CafePlayer player;
        private readonly LineRenderer[] rings = new LineRenderer[10];
        private readonly float[] age = new float[10];
        private int nextRing;
        private float rippleTimer;
        private Material ringMaterial;
        private const float RingLife = 1.6f;

        private bool Inside(Vector3 p, float margin) =>
            p.x > Water.min.x + margin && p.x < Water.max.x - margin && p.z > Water.min.z + margin && p.z < Water.max.z - margin;

        private float BedAt(Vector3 p)
        {
            return WaterBody.Floor(new Vector3(p.x, Water.max.y + 0.05f, p.z), Water.size.y + 3, Water.min.y);
        }

        private void Ripple(Vector3 at, float strength)
        {
            if (WaterRippleSimulation.Instance != null) return;
            if (ringMaterial == null) ringMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            var ring = rings[nextRing];
            if (ring == null)
            {
                ring = rings[nextRing] = new GameObject("Water ripple").AddComponent<LineRenderer>();
                ring.transform.SetParent(transform, false);
                ring.loop = true; ring.useWorldSpace = false; ring.positionCount = 40; ring.sharedMaterial = ringMaterial;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (var i = 0; i < 40; i++) { var a = i / 40f * Mathf.PI * 2; ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); }
            }
            ring.transform.position = new Vector3(at.x, Water.max.y + 0.015f, at.z);
            ring.transform.localScale = Vector3.one * 0.25f * strength;
            ring.gameObject.SetActive(true);
            age[nextRing] = 0.0001f;
            nextRing = (nextRing + 1) % rings.Length;
        }

        private void Update()
        {
            for (var i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null || age[i] <= 0) continue;
                age[i] += Time.deltaTime;
                var t = age[i] / RingLife;
                if (t >= 1) { age[i] = 0; rings[i].gameObject.SetActive(false); continue; }
                rings[i].transform.localScale += Vector3.one * Time.deltaTime * 1.1f;
                var colour = new Color(1, 1, 1, 0.55f * (1 - t) * (1 - t));
                rings[i].startColor = rings[i].endColor = colour;
                rings[i].widthMultiplier = Mathf.Lerp(0.05f, 0.015f, t);
            }
        }

        private void LateUpdate()
        {
            if (player == null) { player = FindFirstObjectByType<CafePlayer>(); if (player == null) return; }
            if (SwimmingController.Handles(player)) { InWater = false; return; }
            var position = player.transform.position;
            var surface = Water.max.y;
            if (!InWater)
            {
                if (!Inside(position, 0) || position.y > surface - 0.45f || player.Blocked) return;
                if (surface - BedAt(position) < MinSwimDepth) return;                     // wading depth: keep walking
                InWater = true;
                for (var i = 0; i < 3; i++) Ripple(position, 1 + i * 0.8f);
                LifeUI.Toast("You're swimming — WASD to swim, Shift faster, hold C to dive, Space to come up. Swim to the bank or the shallow end to get out.");
            }
            var keyboard = Keyboard.current;
            var move = player.Controls != null ? player.Controls.Movement : Vector2.zero;
            var view = Camera.main != null ? Camera.main.transform : player.transform;
            var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var velocity = (forward * move.y + right * move.x) * (keyboard != null && keyboard.leftShiftKey.isPressed ? FastSpeed : SwimSpeed);
            var next = position + velocity * Time.deltaTime;
            var bed = BedAt(next);
            if (surface - bed < MinSwimDepth - 0.15f && Inside(next, 0.35f))
            {
                // Swum into shallow water: stand up and wade (the controller's gravity puts the feet on the floor).
                InWater = false;
                player.Place(new Vector3(next.x, Mathf.Max(bed, position.y) + 0.02f, next.z));
                return;
            }
            var targetY = position.y;
            if (keyboard != null && keyboard.cKey.isPressed) targetY -= DiveSpeed * Time.deltaTime;              // dive
            else if (keyboard != null && keyboard.spaceKey.isPressed) targetY += DiveSpeed * 1.3f * Time.deltaTime; // swim up
            else targetY = Mathf.MoveTowards(position.y, surface - FloatDepth, Time.deltaTime * 1.5f);            // buoyancy
            next.y = Mathf.Clamp(targetY, bed + 0.05f, surface - FloatDepth) + Mathf.Sin(Time.time * 1.7f) * 0.01f;
            if (!Inside(next, 0.35f) && velocity.sqrMagnitude > 0.05f)
            {
                // At the bank and still swimming towards it: climb out onto the ground beyond.
                var outward = velocity.normalized;
                var top = new Vector3(Mathf.Clamp(next.x, Water.min.x, Water.max.x), surface + 4, Mathf.Clamp(next.z, Water.min.z, Water.max.z)) + outward * 1.1f;
                if (Physics.Raycast(top, Vector3.down, out var hit, 8, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > surface - 0.1f && hit.point.y < surface + 1.6f && !Inside(hit.point, 0))
                {
                    player.Place(hit.point + Vector3.up * 0.05f);
                    InWater = false;
                    Ripple(position, 1.5f);
                    return;
                }
                next.x = Mathf.Clamp(next.x, Water.min.x + 0.35f, Water.max.x - 0.35f);
                next.z = Mathf.Clamp(next.z, Water.min.z + 0.35f, Water.max.z - 0.35f);
            }
            if (velocity.sqrMagnitude > 0.01f) player.Face(velocity);
            player.Place(next);
            if ((rippleTimer -= Time.deltaTime) <= 0) { rippleTimer = velocity.sqrMagnitude > 0.05f ? 0.45f : 1.6f; Ripple(next, velocity.sqrMagnitude > 0.05f ? 1 : 0.7f); }
        }

        private void OnDrawGizmosSelected() { Gizmos.color = new Color(0.2f, 0.6f, 1, 0.4f); Gizmos.DrawWireCube(Water.center, Water.size); }
    }
}
