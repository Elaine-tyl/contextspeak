# Unity 3D and Godot 2D clients

## Intent and approach

The user explicitly chose Unity for immersive 3D worlds and Godot for the eight
classified 2D learning games. Keep the existing website, extension, accounts,
question catalog, authoritative scoring, speech cache and learning history.
The intended exports remain Web and desktop. Migrate in verifiable stages;
retain the working clients as comparison/fallback until replacements pass.

A complete simultaneous rewrite would duplicate authentication and scoring.
Recommended builder call: first ship a Godot Web game renderer against the
existing account API, then compile/test the Unity cafe and extend the remaining
world scenes. Standalone desktop account pairing is a separate follow-on gate;
do not imply a Web build is already a native account-connected application.
Unity Hub/Editor were not found. The user is installing/activating Unity6 LTS
with Web support; credential/license steps remain theirs. No uncompiled Unity
placeholder is presented as a working replacement. Once ready, use C#, URP and
the existing verified free assets/local guest companion for the cafe vertical
slice before replacing other missions. The current Godot3D prototype stays usable.

## Godot Web renderer deliverable

Add a real Godot4.7.2 GDScript/Control2D project in games/arcade. Render/play all
eight current game types, ten levels, typed answers, six word blanks, four native
vector choice pictures, spoken responses, result score/corrections and PK target.
Use a restrained cream/sage/blue visual theme, distinct mode illustrations and
small feedback animation. Godot owns input and visible game-board controls.
Keep the website's mode/level/source navigation, sign-in and accessibility fallback.

Expose /games/arcade as a preview using the same GamesPage controller as /games.
Extract the existing round logic into useGameRound without changing its tested
behaviour. Both renderers reuse completed AI plays, browser MediaRecorder,
automatic recording submission, exact-event retries, server receipts and URL
recovery. API keys stay server-side. Browser recordings remain in memory and are
never posted to Godot; the host submits to the existing bounded assessment API.

The host sends a bounded plain-data snapshot to its same-origin iframe, never
CSRF/auth tokens or raw audio. The frame posts allowlisted actions. Validate
message origin, exact iframe window, message type and payload bounds. Frame input
cannot set user identity, event IDs, completed play counts, marks, round IDs or
API paths. Submit takes an explicit answer override so React state timing cannot
lose the typed answer. Invalid cross-kind payloads fail before networking.
The Godot bridge caches latest host state until its callback is ready; no eval
of message content. Keep inputs/focus on same-round status updates rather than
rebuilding the board on every state tick. Render model output as plain labels.

Navigation/unmount/account expiry must cancel audio, requests and microphone
tracks. Parent polling/expiry and current-token checks preserve account isolation.
The Godot renderer cannot bypass playback gating. Pending network retries keep
the exact original event/payload. Accessible HTML detail feedback/IPA and source
credits remain available beside the canvas, plus the current web renderer link.
Do not downgrade auth or add a public answer/grading endpoint.

## Build and fallback

Use the existing pinned engine/templates under .local/world-tools. A dedicated
arcade build script exports to ignored apps/web/public/arcade-engine and injects
a small bridge.js before engine startup. No extra runtime npm dependency, paid
art or downloaded pack. Source and build commands are committed; exports/caches
are ignored. Missing export gives a clear setup message and working web fallback.
Browser build is the first verified deliverable, not a native desktop claim.

## Verification

Watch failing tests for bounded action validation, wrong-frame/origin rejection,
answer override/cross-kind safety and stale ownership; run existing Games browser
regressions after shared-hook extraction. Headless Godot tests cover modes,
question/result transitions, six inputs, long words and small viewports. Export
and run actual WebGL browser journeys with simulated auth/audio/microphone,
server-owned results, Retry/Next/reload and cleanup. Test real public-text local
AI playback without adding a fake score to the user's account. Run full source
suite, typecheck/format/build, scoped final review. Do not auto-commit/push this
new migration unless requested. Unity compilation remains blocked by installation.
