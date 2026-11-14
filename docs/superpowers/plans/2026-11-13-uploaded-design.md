# Uploaded Design Integration Plan

> Execute inline with superpowers:executing-plans. No automatic commit or push.

**Goal:** Use the uploaded visual baseline while retaining working gameplay and later fixes.
**Architecture:** Idempotent archive reconciliation plus a narrow, serialized BuildingSkin visibility mode. Reuse the supplied greenhouse builder and environment checks.
**Tech Stack:** Unity 6.3/C#, URP, existing GLB and PBR assets.
**Spec:** ../specs/2026-11-13-uploaded-design-design.md

## Review Focus
- An imported LOD shell must stay inactive after the greenhouse replaces it.
- Normal room inside/outside switching must remain unchanged.
- Archive integration must preserve all supplied animation takes, facing and story build scenes.
- Scene upgrades must retain tasks, NPC interaction objects, saves and authoring setup.
- Visual inspection must distinguish existing images from freshly generated output.

## Tasks
- [x] Compare every archive member to the workspace and record compatible retained changes in .local/design-update/archive-audit.json.
- [x] Confirm all nine existing scenes contain one instance of each environment controller.
- [x] Add and run failing native regressions for shell visibility and physical occupancy; implement the serialized mode and pass all nine checks, including JSON reload.
- [x] Apply the uploaded greenhouse upgrade, render 24 fresh café shots, inspect daylight/night/rain, and pass environment and isolated story-play checks.
- [x] Record integration status below; address the focused review's occupancy, serialization and architecture-test findings.

## Verified result — 2026-11-13

- Both ZIPs were reconciled by SHA-256. All 81 art files were already present and imported; no duplicate assets or GUID replacement was needed.
- Retained the later scene-build, full-animation-take and character-facing fixes instead of overwriting them with the three older incoming versions.
- Saved the upgraded `ForestSlice` scene with the authored greenhouse visible inside and outside. The old Meshy shell remains in the hierarchy but inactive, so LOD updates cannot cover the greenhouse again.
- Kept physical room occupancy independent of the visual mode. Existing normal-room cutaways remain unchanged.
- Corrected an environment-check false positive: the four supplied 418–444-triangle terracotta props intentionally use one culled LOD. Denser vegetation still requires multiple LODs.
- Unity 6000.3.28f1: nine design-visibility checks passed; full environment checks passed; isolated native story flow passed with synthetic reviewer inputs. The test restored `ForestSlice` and the pre-test save/profile.
- Core checks: 60 living-world regressions and 335 café-slice regressions passed.
- Fresh renders: `games/unity/EnvironmentShots/ForestSlice/` (24 images, including day, sunset, night and rain). The greenhouse no longer has the imported shell covering its interior.
- This task updates the Unity project and saved authoring scene. Web/Mac player exports were not rebuilt; existing exported players still contain their earlier build. No fresh human microphone assessment, performance benchmark, commit or push is claimed.
