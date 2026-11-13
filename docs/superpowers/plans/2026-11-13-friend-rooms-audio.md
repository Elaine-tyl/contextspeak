# Friend rooms and audio implementation plan

> Use superpowers:executing-plans inline; test-driven-development; one final fresh review.

**Goal:** Persistent mute/volume and working four-player private room presence in the Unity Forest Café.
**Architecture:** Global Unity audio preferences; separate HTTP room authority; visual-only smoothed peer avatars; existing UI and standalone loopback AI unchanged.
**Tech stack:** Unity 6000.3.28f1 / C#, Node HTTP, TypeScript, Zod, Vitest, Playwright.
**Spec:** ../specs/2026-11-13-friend-rooms-audio-design.md

## Constraints / review focus
No API keys or recordings in room packets. Never let remote clients change personal progress. No new dependency or public listener by default. Handle pending join cancellation, expired sessions, late polls, scene unload and malformed inputs. Do not interrupt ongoing voice/correction requests. Keep UI fields stable while presence updates.

- [x] Audio: native RED check for listener volume, mute/unmute/persist/default/invalid values; implement Core/GameAudio.cs, add Settings controls and browser state; verify native and browser persistence/recording.
- [x] Rooms authority: RED tests; implement packages/world/rooms.ts and room-server.ts, scripts/rooms.ts + package command; auth, isolation, capacity, idle expiry, bounded HTTP validation and CORS. Add configured room origin to existing web companion without widening AI access.
- [x] Unity rooms: RoomClient + RemotePlayer visuals; create/join/leave/status controls, names/activity, smooth pose updates, generation guards; no local PlayerController on peers. Test two client movement, activity, room isolation and cancellation.
- [x] Verify integration, existing regressions, final review; build Web/macOS and document local versus remote hosting. No commit/push requested.

## Evidence and rulings

- Native audio RED → GREEN: absent master audio class; now actual listener volume, persistence, clamping and restoration pass.
- Room API RED → GREEN: missing transport; now 6 real HTTP tests cover auth, isolation, four-player cap, expiry, leave and bounded requests. Profile-change test also reproduced rejection before adding safe name/look updates.
- Companion config RED → GREEN: missing `/rooms-config` endpoint; configured origin is now advertised/allowed in CSP without opening local AI routes to that origin. Combined service suites: 18 passed.
- Native geometry/navigation/UI/counter/audio/remote-visual checks passed. Project tests: 616 passed; typecheck helper issue fixed, final typecheck passes.
- Final review found no important blockers. Minor deferred: cloning a currently moving live protagonist can slightly offset the peer's initial procedural bind pose; canonical visual cloning is a later animation refinement. Pixel-level peer verification remains part of browser acceptance.
- Web build passed. Packaged browser integration and macOS build in progress.

- Web/macOS builds succeeded. All 26 existing browser cases passed. New room/audio run: initial 2/3 passed; reload status parsing happened before Unity produced state. Added explicit scene-ready wait, then all 3/3 passed on fresh targeted run. Actual peer models/names and Settings/Friends screenshots inspected and saved in docs/images.
- Different-computer/Internet hosting not provisioned or tested; configuration and boundaries documented in docs/FRIEND_ROOMS.md. Local companion and room service remain running for use. No commit/push requested.
