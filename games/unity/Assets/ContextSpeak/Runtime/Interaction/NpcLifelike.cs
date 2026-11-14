using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Small signs of life for every NPC, on top of whatever animation plays: the head and eyes follow
    /// the player when they come close (and now and then glance around), the chest breathes and the
    /// weight shifts from foot to foot. Added automatically by NPCController.
    public sealed class NpcLifelike : MonoBehaviour
    {
        private Transform head, neck, spine, hips, player;
        private float yaw, pitch, glance, glanceTimer, seed;

        private void Start()
        {
            seed = Random.value * 10;
            foreach (var bone in GetComponentsInChildren<Transform>())
            {
                var name = bone.name.ToLowerInvariant();
                if (head == null && name.EndsWith("head")) head = bone;
                else if (neck == null && name.EndsWith("neck")) neck = bone;
                else if (spine == null && (name.EndsWith("spine1") || name.EndsWith("spine01") || name.EndsWith("chest"))) spine = bone;
                else if (hips == null && (name.EndsWith("hips") || name.EndsWith("pelvis"))) hips = bone;
            }
            var found = FindFirstObjectByType<CafePlayer>();
            if (found != null) player = found.transform;
            if (head == null) enabled = false;
        }

        private void LateUpdate()
        {
            var t = Time.time + seed;
            float wantYaw = 0, wantPitch = 0;
            if (player != null)
            {
                var to = player.position + Vector3.up * 1.5f - head.position;
                if (to.magnitude < 6)
                {
                    var local = transform.InverseTransformDirection(to.normalized);
                    var angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(angle) < 85) { wantYaw = Mathf.Clamp(angle, -60, 60); wantPitch = Mathf.Clamp(-Mathf.Asin(local.y) * Mathf.Rad2Deg, -20, 20); }
                }
            }
            if (wantYaw == 0)
            {
                if ((glanceTimer -= Time.deltaTime) <= 0) { glanceTimer = 3 + Random.value * 5; glance = Random.value < 0.4f ? 0 : Random.Range(-35f, 35f); }
                wantYaw = glance;
            }
            yaw = Mathf.Lerp(yaw, wantYaw, Time.deltaTime * 3);
            pitch = Mathf.Lerp(pitch, wantPitch, Time.deltaTime * 3);
            var up = transform.up; var right = transform.right;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(yaw * 0.35f, up) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(yaw * 0.65f, up) * Quaternion.AngleAxis(pitch, right) * head.rotation;
            if (spine != null) spine.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 1.3f) * 0.8f, right) * spine.rotation;          // breathing
            if (hips != null) hips.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 0.35f) * 1.5f, transform.forward) * hips.rotation;   // weight shift
        }
    }
}
