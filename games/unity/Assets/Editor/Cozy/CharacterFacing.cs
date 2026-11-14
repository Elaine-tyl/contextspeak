using System.Linq;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Makes every character visual face the actor's +Z (Unity forward), whatever axis convention
    /// its source file used. Facing is measured from the skeleton (left → right shoulder, or hips),
    /// so the player and NPCs walk and talk face-first instead of moon-walking backwards.
    public static class CharacterFacing
    {
        public static Transform Bone(Transform root, string name) => root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == name || t.name.EndsWith(":" + name) || t.name.EndsWith("_" + name));

        /// Rotates `visual` about `root` so the character faces root.forward. Returns the applied angle (0 if none).
        public static float Align(Transform root, Transform visual)
        {
            var left = Bone(root, "LeftShoulder") ?? Bone(root, "LeftArm") ?? Bone(root, "LeftUpLeg");
            var right = Bone(root, "RightShoulder") ?? Bone(root, "RightArm") ?? Bone(root, "RightUpLeg");
            if (left == null || right == null) return 0;
            var across = right.position - left.position;
            across.y = 0;
            if (across.sqrMagnitude < 1e-6f) return 0;
            var facing = Vector3.Cross(across.normalized, Vector3.up);          // right × up = forward
            var angle = Vector3.SignedAngle(facing, root.forward, Vector3.up);
            if (Mathf.Abs(angle) < 20) return 0;                                 // already facing forward
            angle = Mathf.Round(angle / 90) * 90;                                // source files differ by whole quarter turns
            visual.RotateAround(root.position, Vector3.up, angle);
            return angle;
        }
    }
}
