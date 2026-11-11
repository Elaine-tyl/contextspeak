# Playable 2D café preview

Run `pnpm world:build`, then `pnpm world:dev`. Open
`http://127.0.0.1:4320/?view=2d`. No website account is required. The existing
local Qwen, Whisper and Phonon companion is reused, with no cloud requests.

This preview supports café ordering, sitting with Alex, friend conversation,
practice-stage previews and the café's US/Canada challenge. The café is original
native vector art; no new external art assets or dependencies were added.
The shared Web package still includes the existing 3D resources.

Browser progress keys are `contextspeak-cafe-2d-v1` and
`contextspeak-scenes-2d-v1`. Native filenames use the same 2D suffix. The original
3D keys/files and account Dashboard scores remain separate. The old movement,
mission, correction and recording controllers run against a new 2D world
adapter, with invisible shared physics coordinates and no imported mesh nodes.

Review fixes have regression tests: the active NPC marker now follows the
challenge target before automatic walking begins, and the bookcase has a
matching collision footprint. Final-size ellipse geometry avoids scaling
antialiasing into blurry furniture.

Verification: all 21 isolated Godot scripts, 501 source tests, both final browser
journeys (2D and original 3D), Web/macOS exports and headless native 2D startup
passed. Desktop and 573×793 framing were visually inspected. Browser microphone coverage uses synthetic audio;
physical microphone and native GUI checks remain pilot work.

Captures: [2D room](../games/cafe/docs/playtest/2d/live-room.png),
[counter dialogue](../games/cafe/docs/playtest/2d/2d-counter.png),
[friend conversation](../games/cafe/docs/playtest/2d/2d-friend.png) and
[narrow layout](../games/cafe/docs/playtest/2d/2d-narrow.png).

```sh
pnpm world:test
pnpm test --maxWorkers=4
pnpm exec playwright test --config playwright.world.config.ts cafe-2d friend
```
