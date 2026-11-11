# Campus café implementation plan

**Goal:** Cute teen campus avatars, an intentionally designed cream/wood café, and
safe manual practice-stage selection for browser/desktop testing.
**Architecture:** Preserve Godot world + local companion. Separate preview-stage
state from normal guest saves; keep art/animation adapters outside mission logic.
**Spec:** ../specs/2026-11-10-campus-cafe-design.md

- [x] Test and implement stage fixtures, explicit Select UI, isolated no-write previews,
  restart/resume, cancellation and relevant browser regressions.
- [x] Inspect exact free Teen/animation contents, retain licenses and hashes; validate
  standing/walk/seated motion and campus clothing before replacing working avatars.
- [x] Restyle the café using cream/wood/sage, composed lighting/decor and clear walkways;
  inspect actual rendered images instead of relying on geometry-only checks.
- [x] Verify preview isolation + original full journey, recording/voice, imports and
  animations, browser/macOS exports and relevant code checks; save screenshots.

Review focus: fixtures mistaken for earned progress; stale callbacks after stage
switch; stage selection keyboard access; missing seated motion/rig transforms;
new furniture blocking routes; readable characters above the reply panel.

Ruling: The user selected the proposed campus/cream-wood visual direction. Routine
asset/clothing/composition choices are implementation decisions in that direction.
Use the existing isolated worktree and preserve all earlier uncommitted changes.

Verification: eight Godot checks, six browser gameplay/preview cases, five synthetic
microphone replies through actual local models, 294 Vitest tests, types and format.
Both exports built; native headless startup checked. See docs/CAMPUS_CAFE_PLAYTEST.md.

Ruling: The free Standard base archive has no Teen models; no paid files were used.
Original campus geometry follows the existing licensed animated rig instead.
Cosmetic palette shading is checked in WebGL, not the dummy headless renderer.
Screen-reader announcements remain exposed but no longer duplicate visible dialogue.
