# Café playtest and immersion improvements

User request: test recording, NPC follow-ups and completion as a user, fix issues,
then improve the café scene and characters. Continue in the existing isolated
worktree; preserve the website redesign and keep this work local.

Chosen implementation: retain Godot and the bounded local AI companion. Improve
this one café with selected Kenney CC0 models, warm lighting and a closer
conversation camera. A full free-camera simulation or a different engine would
expand scope without improving this learning loop. These are implementation
choices under the user's request to improve the existing prototype.

Required behavior:
- Record → stop → local recognition → NPC follow-up without another submit click.
- Silent input, questions and ambiguous confirmation cannot complete the mission.
- Changing a completed order's options before confirmation must work naturally.
- Errors retain mission state and offer recording retry or typed fallback.
- Show the current order, price, budget and current communication goal clearly.
- Clear microphone activity/timer and completion feedback; task score stays
  separate from pronunciation accuracy.
- Use real CC0 low-poly characters/props with provenance; warm café materials,
  furniture, coffee equipment, subtle character animation and a smooth closer
  conversation view. Preserve movement, keyboard controls and local progress.
- Honor reduced motion where available; do not add music that masks speech.

Testing: fresh screenshots before/after, real exported WebGL with synthetic
microphone input through local Whisper (not a mocked transcript), deterministic
mission/audio regressions, error/retry and task/save/restore checks. A synthetic
voice does not verify human accent accuracy or the physical microphone.
