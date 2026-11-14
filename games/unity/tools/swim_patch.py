#!/usr/bin/env python3
"""In-place, idempotent patches that let the V6.3 swimming system own movement without a second
player controller. Run from the Unity project root:  python3 tools/swim_patch.py
Each edit is skipped when its marker is already present, and fails loudly if an anchor moved."""
import sys

def patch(path, edits):
    try:
        text = open(path, encoding="utf-8").read()
    except FileNotFoundError:
        print(path + ": MISSING"); return
    done = 0
    for marker, anchor, replacement in edits:
        if marker in text:
            continue
        if anchor not in text:
            print(path + ": anchor not found -> " + anchor[:70]); sys.exit(1)
        text = text.replace(anchor, replacement, 1)
        done += 1
    open(path, "w", encoding="utf-8").write(text)
    print(path + ": " + str(done) + " edit(s)")

PLAYER = "Assets/_Game/Player/CafePlayer.cs"
patch(PLAYER, [
    ("interface ILocomotionOverride",
     "        public event Action<string, float> Footstep;\n",
     "        public event Action<string, float> Footstep;\n"
     "        /// A system that temporarily owns movement (swimming). While Active, CafePlayer only updates the camera look.\n"
     "        public interface ILocomotionOverride { bool Active { get; } PlayerAnimState AnimState { get; } void Tick(float dt); }\n"
     "        public ILocomotionOverride LocomotionOverride;\n"
     "        /// Camera overrides used by the swimming camera: target, distance multiplier and lowest lens height.\n"
     "        [NonSerialized] public bool OverrideCameraTarget;\n"
     "        [NonSerialized] public Vector3 CameraTarget;\n"
     "        [NonSerialized] public float CameraDistanceScale = 1;\n"
     "        [NonSerialized] public float CameraFloor = float.NegativeInfinity;\n"
     "        public float Yaw => yaw;\n"
     "        public Vector3 Velocity => horizontal + Vector3.up * vertical;\n"
     "        /// Hands momentum back to the walking controller when an override ends.\n"
     "        public void SetVelocity(Vector3 flat, float up) { horizontal = new Vector3(flat.x, 0, flat.z); vertical = up; }\n"),
    ("LocomotionOverride.Tick(dt)",
     "            var dt = Time.deltaTime;\n",
     "            var dt = Time.deltaTime;\n"
     "            if (LocomotionOverride != null && LocomotionOverride.Active)\n"
     "            {\n"
     "                LocomotionOverride.Tick(dt);\n"
     "                State = LocomotionOverride.AnimState;\n"
     "                wasGrounded = false;\n"
     "                if (!Blocked)\n"
     "                {\n"
     "                    yaw += Controls.Look.x * 0.12f * MouseSensitivity;\n"
     "                    pitch = Mathf.Clamp(pitch - Controls.Look.y * 0.1f * MouseSensitivity * (InvertY ? -1 : 1), MinPitch, MaxPitch);\n"
     "                    distance = Mathf.Clamp(distance - Controls.Zoom * 0.35f, MinDistance, MaxDistance);\n"
     "                }\n"
     "                return;\n"
     "            }\n"),
    ("OverrideCameraTarget ? CameraTarget",
     "            var target = transform.position + Vector3.up * (Crouching ? 0.95f : 1.35f);\n",
     "            var target = OverrideCameraTarget ? CameraTarget : transform.position + Vector3.up * (Crouching ? 0.95f : 1.35f);\n"),
    ("distance * CameraDistanceScale",
     "            var wanted = distance;\n",
     "            var wanted = distance * CameraDistanceScale;\n"),
    ("desired.y < CameraFloor",
     "            var desired = target + back * currentDistance;\n",
     "            var desired = target + back * currentDistance;\n"
     "            if (desired.y < CameraFloor && ConversationPartner == null) desired.y = CameraFloor;\n"),
])

MOTION = "Assets/_Game/Art/AuthoredMotion.cs"
patch(MOTION, [
    ("public AnimationClip Override;",
     "        public AnimationClip[] Extras = Array.Empty<AnimationClip>();\n",
     "        public AnimationClip[] Extras = Array.Empty<AnimationClip>();\n"
     "        /// Set by gameplay systems (swimming) to play a clip instead of locomotion; OverrideTime >= 0 holds that pose.\n"
     "        [NonSerialized] public AnimationClip Override;\n"
     "        [NonSerialized] public float OverrideSpeed = 1, OverrideFade = 0.25f, OverrideTime = -1;\n"),
    ("if (Override != null) { PlayOverride(); return; }",
     "            if (Time.time < previewUntil || Player == null) return;\n",
     "            if (Time.time < previewUntil || Player == null) return;\n"
     "            if (Override != null) { PlayOverride(); return; }\n"),
    ("private void PlayOverride()",
     "        public AnimationClip Locomotion(PlayerAnimState state, float speed = 1)\n",
     "        private void PlayOverride()\n"
     "        {\n"
     "            if (Fallback != null) Fallback.enabled = false;\n"
     "            if (OverrideTime >= 0)\n"
     "            {\n"
     "                Player.Stop();\n"
     "                Player.enabled = false;\n"
     "                Override.SampleAnimation(Player.gameObject, Mathf.Clamp(OverrideTime, 0, Override.length));\n"
     "                current = Override.name;\n"
     "                samplingJump = true;\n"
     "                return;\n"
     "            }\n"
     "            if (Player.GetClip(Override.name) == null) Player.AddClip(Override, Override.name);\n"
     "            if (samplingJump) { current = null; samplingJump = false; }\n"
     "            if (current != Override.name) { Player.enabled = true; Player.CrossFade(Override.name, OverrideFade); current = Override.name; }\n"
     "            var playing = Player[Override.name];\n"
     "            if (playing != null) playing.speed = OverrideSpeed;\n"
     "        }\n\n"
     "        public AnimationClip Locomotion(PlayerAnimState state, float speed = 1)\n"),
])

# Older swimming systems hand over to SwimmingController when it is on the player.
patch("Assets/ContextSpeak/Runtime/Environment/PondSwimming.cs", [
    ("SwimmingController.Handles(player)",
     "            var position = player.transform.position;\n            var surface = Water.max.y;\n",
     "            if (SwimmingController.Handles(player)) { InWater = false; return; }\n"
     "            var position = player.transform.position;\n            var surface = Water.max.y;\n"),
])
patch("Assets/ContextSpeak/Runtime/Beach/Swimming.cs", [
    ("SwimmingController.Handles(Player)",
     "            if (Player == null) return;\n            var position = Player.transform.position;\n",
     "            if (Player == null) return;\n"
     "            var modern = SwimmingController.Handles(Player) ? Player.GetComponent<SwimmingController>() : null;\n"
     "            if (modern != null) { InWater = modern.IsSwimming; HeadUnder = modern.HeadUnder; Breath = modern.Breath; return; }\n"
     "            var position = Player.transform.position;\n"),
])
