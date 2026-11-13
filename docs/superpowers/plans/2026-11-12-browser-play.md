# Browser Play Implementation Plan

User delegates design choices; replace the Godot web-game entry with the existing React controls. Keep Unity worlds, server grading, account ownership, retry IDs and saved levels intact. This implements the user's request for interactive browser games with sound and effects.

Approach: React/Next.js with CSS motion and synthesized Web Audio effects. No additional engine or downloaded sound assets. Preserve old Godot source as an archive, remove it from active website routes and tests. Legacy web/arcade URLs use the same controls and can still resume run IDs.

- [x] Add failing audio regressions for gesture gating, mute, unsupported audio and cleanup, and a browser check that the primary route has no engine iframe.
- [x] Make React the sole website game renderer; retain all game kinds and backend progression.
- [x] Add sound preference, selection tones, outcome chimes, score celebration and level progress. Avoid effects over microphone/lesson audio; respect reduced motion.
- [x] Refresh the games hall with skill counts, illustrated mode badges, hover/pressed states and responsive layout.
- [x] Run unit/type/format and real browser flows, inspect screenshots. No automatic commit of the existing dirty baseline.

Verification: 587 source tests, audio regression RED/GREEN (including immediate mute cancellation), TypeScript and desktop/mobile browser flow pass. Browser test uses isolated API/audio fixtures and also checks actual Web Audio oscillator creation after unmute. Native Mac world window now opens after unlock; no new human microphone check claimed. Retired generated Godot Web bundle moved out of public/ to .local/retired-godot-web-20261112; historical engine source retained.
