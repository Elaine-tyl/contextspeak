# Café friend conversation implementation plan

**Goal:** A voiced counter-to-table learning journey in the current game.
**Architecture:** Godot journey + bounded local chat endpoint + browser Web Audio.
**Spec:** ../specs/2026-11-10-cafe-friend-design.md

- [x] Add RED tests for friend stage gating, three accepted turns, rejection,
  migration and persistence; implement a small standalone friend state object.
- [x] Add RED protocol/server checks for bounded local conversation input/output;
  implement grounded Qwen acknowledgement/feedback using the existing cancellation.
- [x] Add browser voice playback lifecycle checks and implement gesture unlock,
  start/end/replay/cancel handling; preserve native playback.
- [x] Join the journey to movement/seated animation, table camera and UI. Keep
  order feedback visible and automatically start the friend's spoken questions.
- [x] Verify browser ordering→voice→seating→chat→completion/save and failure/reset
  regressions, local speech, native startup, full tests/types/format and screenshots.
- [x] Record primary-source free asset comparisons and adoption decisions.

Review focus: stale playback cannot trigger seating after reset; conversation
cannot bypass the order; invalid model replies do not advance; failed checks keep
text; old saves resume at the right phase; both characters are visible at the table.

Additional defects found and fixed during verification: false-negative weekend
plans, help requests mistaken for answers, feedback overwritten at voice end,
lost saved feedback, and imported model origins displacing the table/chairs.
Evidence and remaining limits: docs/CAFE_FRIEND_PLAYTEST.md.
