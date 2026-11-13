# IELTS Life World Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task.

**Goal:** Provide IELTS-inspired practice targets from band 3 to 9 in a coherent, reference-led, editable Unity life world.

**Architecture:** Keep the existing café transaction engine and voice pipeline. Add a pure C# curriculum and three-turn scene lessons; extend the existing dialogue UI to additional NPCs. Generate original reusable character prefabs and eight walkable scene compositions in the current Unity project, retaining free licensed furniture.

**Tech stack:** Unity 6000.3.28f1, C#, URP, UI Toolkit, existing local STT/TTS/LLM companion.

**Spec:** The user's November 12 request and nine supplied concept images. The user previously delegated remaining design choices; execute inline without another design approval round.

## Constraints

- Targets 3, 4, 5, 6, 7, 8 and 9 are training settings, not certified IELTS scores. Use original content informed by the public descriptors: https://ielts.org/cdn/ielts-guides/ielts-speaking-band-descriptors.pdf.
- There is no official prescribed vocabulary list; choose useful collocations and communicative skills rather than rare-word quotas.
- Preserve numeric saved band values 5–8, receipts, orders, learning history and saves. Validate undefined enum values. New games start at 3; existing games keep their selection.
- Do not derive pronunciation or acoustic fluency from text. Natural alternative responses must pass; menu/payment replies need not become essays.
- Keep original source/art ownership and any existing CC0 notices. Do not substitute concept images for playable 3D scenes or claim exact photorealistic reconstruction.
- Back up generated scenes and close the unmodified Editor before batch builds. Do not erase prior uncommitted archive work.

## Review focus

1. Existing saved levels and purchases survive; invalid bands cannot select accidental expert dialogue.
2. A failed answer or required correction cannot advance the scene lesson; duplicate callbacks cannot advance twice.
3. A beginner receives comprehensible questions; higher targets require reasons, comparison and nuanced discussion, without mandatory obscure words.
4. Every new NPC is physically reachable; scenery must not obstruct the existing café, kitchen and pier routes.
5. Procedural art meshes/materials are persisted as editable prefab assets and render in both WebGL and desktop builds.

## Tasks

### 1. Seven-band curriculum and compatibility

- [x] Add failing Core/companion regressions for 3–9, invalid inputs, saved values and beginner dialogue.
- [x] Extend `IeltsBand`, profiles, speaking feedback, dialogue branches and companion validation.
- [x] Add `LifeCurriculum.cs`: original band-specific goals, vocabulary and scene questions for home, café, fishing, market, restaurant, Japanese restaurant, park and mall.
- [x] Verify Core and companion tests.

### 2. Playable scene lessons

- [x] Test reply relevance, accepted paraphrases, off-topic/repeat gates, completion and band-aware follow-ups.
- [x] Add a pure `SceneLesson` session and NPC component; integrate with `SliceGame`, settings, map, journal, saving and the existing recording/feedback UI.
- [x] Show the current learning goal and optional vocabulary hints without forcing a specific memorized sentence.

### 3. Reference-led 3D art

- [x] Create reusable original stylized character meshes/prefabs: player, friend, barista, waitress, fisherman and vendor, with distinct clothing/accessories and restrained animation.
- [x] Upgrade forest café and pier architecture/materials; compose home, market, restaurant, Japanese restaurant, amusement park and mall around the forest district with walkable links and map travel.
- [x] Validate geometry persistence and NPC navigation; preserve all existing transaction/voice tests.

### 4. Verify and deliver

- [x] Run source suite, Unity logic/scene/UI checks, WebGL build and desktop build.
- [x] Exercise beginner and expert scene lessons, an incorrect answer, retry, café order and reload in browser; inspect actual character and environment renders.
- [ ] Reopen the project in Unity Editor at the updated scene. Report verified behavior and remaining visual/assessment limits honestly.

Delivery note: after unlock, the actual Mac café, saved training target, map, walking and Rowan dialogue were inspected. A dialogue-header overflow was reproduced and fixed; native UI checks, 584 source tests and four browser end-to-end tests passed again. Updated WebGL/Mac builds and Mac ZIP CRC passed. The current native window is retained for the user's microphone turn; it uses the previous in-memory layout until relaunch. Human-recording feedback, inspection of the rebuilt Mac header and reopening Unity Editor remain pending.
