# Café free art integration

User instruction: replace the current people with free Quaternius characters and
use KayKit café/restaurant assets, preserving voiced ordering, automatic seating,
friend chat and recording. Reproduce the screenshot's repeated weekend question.

Approach: retain the current Godot world and deterministic journey. Import only
licensed free-edition bytes, with creator/source/archive hashes and offline file
verification. Prefer animated clothed Quaternius characters over unclothed base
bodies requiring a new clothing/retargeting pipeline; reuse/author simple seated
poses if a compatible baked sit clip is unavailable. Use a coherent subset of
KayKit FREE restaurant props, not paid extras. This is an art/UX integration, not
an engine migration or a new campaign. Existing Kenney files may remain for props
not covered by the free restaurant archive.

Proposed defaults chosen for implementation: warm low-poly café, visible faces,
barista/learner/friend differentiated by clothing, café equipment and drinks at
human scale, unchanged controls and save keys. Keep English product UI.

The question bug should be fixed from actual text and state evidence: a meaningful
weekend activity with imperfect grammar advances. Missing/off-topic answers keep
the task and get a repair hint; they must not sound like the game ignored the
answer. Preserve existing local models, cancellation and saved progress.

Verify licensed file integrity, imports/animations/placement, browser full journey
and failure paths, local microphone pipeline, desktop startup and rendered UI.
No paid download, account setup, production deployment or unrelated Git changes.
