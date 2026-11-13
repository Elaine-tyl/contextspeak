# Audio settings and friend rooms

The user requests mute/settings and online play, selecting friend rooms for shared exploration and NPC practice. Existing routine implementation decisions are delegated.

## Product behavior

- Settings provides master mute and volume, remembered across reloads, restarts and district changes. Mute silences all Unity sources (NPCs, footsteps, ambience), not microphone capture. Keep subtitles and speech correction available.
- Private guest rooms hold up to four players in ForestSlice. Create/join with a random code, see named remote avatars move smoothly and see who is practising with an NPC. Leave/disconnect removes the avatar. A session timeout removes abandoned players. Personal quests, wallet, assessment and saves remain local.
- No implicit microphone sharing, PvP, leaderboard or cloud account migration. This is not shared physics or a trusted score service.

## Approach

Reuse Unity C#, UI Toolkit and the standalone TypeScript companion. A separate dependency-free HTTP room service stores short-lived presence in memory; Unity polls at 5 Hz and interpolates visuals. Alternatives: Unity Relay/Netcode introduces account/service setup; WebSockets add transport/platform dependencies. Low-rate HTTP fits four-player cozy exploration and the existing UnityWebRequest bridge.

Room requests use per-player random bearer tokens. Room codes invite joining, not impersonation. Validate all payloads, bound room count/players/message sizes, expire idle members, enforce CORS on browser requests. Room metadata never contains recordings, AI credentials, grades or private quest data. Keep the local AI companion loopback-only. Room service binds loopback by default; LAN/public hosting is an explicit operator configuration. Remote clients require an accessible endpoint; public browser microphone practice needs HTTPS or its existing localhost companion.

The web companion supplies a configured room origin and CSP allowance. Native client uses the same configured URL through settings, defaulting to local room service. No automatic public deployment or firewall change.

## Acceptance

Native audio checks prove actual listener volume/persistence/defaults. Room tests cover create/join, isolation, auth, capacity, expiry and leave. Two packaged browser clients must observe each other's movement/activity and leave cleanly. Existing recording/correction/save regressions stay green. Build Web and macOS; document how another computer connects and what still requires hosting.
