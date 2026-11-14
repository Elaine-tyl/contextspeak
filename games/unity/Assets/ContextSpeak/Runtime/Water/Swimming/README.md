# ContextSpeak Life V6.3 — Swimming, Floating and Buoyancy

Third-person swimming for the existing player (`CafePlayer` + `CharacterController`). There is **one movement owner at a time**: `CafePlayer` walks and wades; when the water is deep enough `SwimmingController` becomes `CafePlayer.LocomotionOverride` and moves the *same* CharacterController (no Rigidbody, no second controller, no teleporting, no fixed Y), then hands its momentum back.

## Controls (Input System actions on `SwimmingController`, rebindable)
WASD swim (camera-relative) · Mouse camera · Shift fast · Space up / surface · C or Ctrl dive · F back float (left to NPC talk when someone is within 3.5 m) · E ladder / dock / pool edge · F5 debug panel.

## States
`GROUNDED → WADING → ENTERING_WATER → SURFACE_SWIMMING ⇄ TREADING_WATER ⇄ BACK_FLOATING`, `→ DIVING → UNDERWATER_SWIMMING → SURFACING`, `→ EXITING_WATER → WADING/GROUNDED`.
Rules use capsule sample points (feet/knees/hips/chest/head), the water depth under the body, the head bone and input. Enter at 72 % immersion *and* ≥ 1.25 m depth; leave below 60 % or with the feet on a bed shallower than 1.15 m; every rule holds 0.2 s → no flicker, puddles never swim.

## Buoyancy (`CharacterBuoyancyController`)
`a = g·(submerged/equilibrium)·strength − g − damping·v − k·v|v| + thrust`, `submerged` = displaced fraction of the body against a smoothed, wave-following waterline (`WaterSurfaceFollower`). Balanced when the feet are `FloatLine` (1.38 m) below the surface → chin just above water; damped spring (≈2.4 s) gives natural bobbing; rise/sink speeds capped; a jump plunges and resurfaces. Under water while stroking the net buoyancy is reduced (near neutral); idle, the body floats up.

## Per-water profiles (`SwimmingProfile`, `Assets/ContextSpeak/Data/Water/Swimming`)
Pool (no waves, tight damping), Pond (gentle), Lake (moderate waves, light current), River/Stream (current), Ocean (rides the Gerstner swell, surge along wave slopes, stronger drag). Each `WaterBody` has its own `Swimming` profile.

## Animation
The rigs use legacy `Animation` clips via `AuthoredMotion` (no Animator Controller). From the supplied `swimming_to_edge` FBX (female *Academy*, male *Daydream*): a seamless **swim loop** (frames 9–43) and **edge reach** (frames 41–100). Used for SwimForward / SwimFast / SwimTurnLeft / SwimTurnRight (banking) / Dive / UnderwaterSwim / Surface (pitch) and the start of ExitWater / ClimbPoolLadder.
**Temporary (clearly labelled in the debug panel and the test report):** TreadWater and BackFloat are procedural limb motion over the idle pose; the pull-up uses the crouch pose; ladder steps use a slowed walk. Drop `Academy-TreadWater.fbx`, `-BackFloat.fbx`, `-UnderwaterSwim.fbx`, `-ClimbLadder.fbx`, `-PullOut.fbx` (and the `Daydream-…` versions) into `Assets/_Game/Art/Motion/Swim/` and run *ContextSpeak › Water › Import Swimming Animations* — they replace the temporary motion automatically.

## Water response
Height-field ripple simulation only (no ring sprites): limb-synchronised stroke disturbances (hands sweeping under / feet breaking the surface, splash only when fast), V-wake whose length grows with speed, small shoulder disturbances when treading, tiny ones when floating, dive splash + bubbles, trailing bubbles, break-through splash, drips. Wetness / drips / wet footprints after leaving come from `CharacterWaterInteraction` + `WaterWetnessController` (MaterialPropertyBlocks — no material instances, base materials untouched).

## Camera
`SwimmingCameraController` drives the existing camera: look-at point on the smoothed waterline (no bob/stroke shake), slightly closer, lens kept above the water until you dive, smooth blend in/out; underwater rendering is decided by the camera's own sampled position (`UnderwaterInland`, `UnderwaterView`).

## Menus
- **ContextSpeak › Water › Setup Swimming System** — inspect player (incompatible Rigidbody warning), import clips, profiles, water bodies, player components, ladder exit points, sample prefabs, report missing animations/shaders.
- **ContextSpeak › Water › Build Swimming Test Scene** — shallow 0.45 m, pool 1–2 m + ladder, forest pond, lake with sloped shore + dock, ocean strip.
- **ContextSpeak › Checks › Swimming** — Edit-mode checks (buoyancy, state machine, follower, profiles, clips, hooks).
- **ContextSpeak › Checks › Swimming Play Mode Tests** — runs the real player through 19 scenarios, writes `Logs/SwimmingPlayModeReport.md`.

## Shared files touched (in place, `tools/swim_patch.py`, idempotent)
`CafePlayer.cs` (locomotion override hook, camera target/distance/floor, `SetVelocity`), `AuthoredMotion.cs` (`Override` clip), `PondSwimming.cs` and beach `Swimming.cs` (hand over to `SwimmingController`; beach breath/gear UI kept).
