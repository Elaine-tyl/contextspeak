using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Swimming camera mode on top of the existing third-person camera (CafePlayer → Cinemachine):
    /// while swimming the camera follows a stable point at the smoothed waterline (not the bobbing
    /// body or the stroke), sits a little closer, and stays just above the water unless the swimmer
    /// dives — then it follows the body down smoothly. Blends in and out (no snapping); wall and
    /// terrain clipping is still handled by CafePlayer's sphere-cast. Underwater rendering is decided
    /// from the camera's own sampled position (UnderwaterInland / UnderwaterView), not the player's.
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(SwimmingController))]
    public sealed class SwimmingCameraController : MonoBehaviour
    {
        [Tooltip("Height of the look-at point above the waterline while at the surface (m).")] public float SurfaceLookHeight = 0.45f;
        [Tooltip("Camera distance multiplier while swimming.")] public float DistanceScale = 0.88f;
        [Tooltip("Keep the lens this far above the water at the surface (m).")] public float Clearance = 0.3f;
        public float BlendSeconds = 0.6f, FollowSmoothing = 0.12f;

        public float Blend { get; private set; }
        private SwimmingController swim;
        private CafePlayer player;
        private Vector3 target, velocity;
        private float floor = float.NegativeInfinity;

        private void Awake()
        {
            swim = GetComponent<SwimmingController>();
            player = GetComponent<CafePlayer>();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var on = swim.OwnsMovement && swim.Exit == SwimmingController.ExitMode.None && swim.State != SwimState.ExitingWater;
            Blend = Mathf.MoveTowards(Blend, on ? 1 : 0, dt / Mathf.Max(0.05f, BlendSeconds));
            var normal = transform.position + Vector3.up * 1.35f;
            if (Blend <= 0)
            {
                player.OverrideCameraTarget = false;
                player.CameraDistanceScale = 1;
                player.CameraFloor = float.NegativeInfinity;
                target = normal; velocity = Vector3.zero; floor = float.NegativeInfinity;
                return;
            }
            var waterline = swim.Surface.Height;                         // smoothed, without the bob
            var under = swim.Machine.Underwater || swim.HeadUnder;
            var swimTarget = under
                ? transform.position + Vector3.up * (swim.FloatLine - 0.15f)
                : new Vector3(transform.position.x, waterline + SurfaceLookHeight, transform.position.z);
            var eased = Mathf.SmoothStep(0, 1, Blend);
            var wanted = Vector3.Lerp(normal, swimTarget, eased);
            if (!player.OverrideCameraTarget) { target = player.CameraAim != null ? player.CameraAim.position : normal; velocity = Vector3.zero; }
            target = Vector3.SmoothDamp(target, wanted, ref velocity, FollowSmoothing, Mathf.Infinity, Mathf.Max(dt, 1e-5f));
            player.OverrideCameraTarget = true;
            player.CameraTarget = target;
            player.CameraDistanceScale = Mathf.Lerp(1, DistanceScale, eased);
            // Lens floor: above the water at the surface; released gradually when diving (no jump).
            var wantedFloor = under ? waterline - 30 : waterline + Clearance;
            if (float.IsNegativeInfinity(floor)) floor = Mathf.Min(wantedFloor, player.View != null ? player.View.transform.position.y : wantedFloor);
            floor = Mathf.MoveTowards(floor, wantedFloor, (under ? 3f : 1.5f) * dt);
            player.CameraFloor = eased >= 0.98f ? floor : float.NegativeInfinity;
        }
    }
}
