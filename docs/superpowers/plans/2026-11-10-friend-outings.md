# Friend Outings Implementation Plan

> **For agentic workers:** Apply test-driven development. Backend dialogue and scene artwork may run independently; root owns integration and verification.

**Goal:** Replace school with shopping alongside a friend and add an amusement-park outing, using natural spoken English.

**Architecture:** Extend the existing catalog and local evaluator, keep the recording/correction state machine, and render original procedural settings. The existing cafe, farm, market and restaurant remain available. New friend scenes accept reasonable choices and short replies; visual effects use the accepted answer so a red jacket does not become a blue hoodie.

**Tech Stack:** Godot 4.7.2, TypeScript/Zod, local Qwen/Whisper/Phonon, Vitest and Playwright.

## Design decisions

- Scene IDs in order: `shopping`, `farm`, `market`, `restaurant`, `amusement_park`. No school progress is reused to unlock shopping.
- Two roles per new setting: shopping `shopper`/`buddy`, park `visitor`/`planner`; each has three conversational goals and a friend NPC.
- Shopping covers choosing clothes, discussing price/color and suggesting what to do next. Park covers rides, reassurance, snacks, tickets, queues and meeting points.
- Contractions, fragments and valid alternatives are welcome. Negative preferences such as “I don't like red” are not automatically treated as failed goals. Corrections remain gated readbacks and assisted evidence.
- New actions: `outfit`, `price`, `bags`, `ride`, `tickets`, `snack`, `meet`. Remove school-only code and actions. Preserve existing action IDs used by other scenes.
- Procedural free artwork uses existing CC0 avatars and props; no new dependency or paid asset is required. Ambient park motion honors reduced motion.
- UI/code/docs remain English. No commit or push is part of this request.

## Tasks

- [x] Backend: add failing catalog/colloquial-answer tests, replace school, add park, broaden friend-scene goal guards and update tutor tone; run targeted Vitest.
- [x] Art: add failing five-scene/choice/motion tests, replace classroom with clothing store and build fairground; run full headless placement checks.
- [x] Integration: update scene/state/browser regressions; pass actual accepted friend-scene answers to effects on advance and restore; verify old school saves do not become shopping progress.
- [x] Verification: run TypeScript tests/typecheck/format, Godot tests, Web/macOS exports, browser regression, native startup and real local AI sample checks; inspect screenshots and request fresh review.

## Review focus

Valid short/negative replies must pass when they fulfill the goal; unrelated replies and prompt instructions must not. Correction readback must keep working for contractions. Choices must match props on both advance and restore. Old scene IDs cannot unlock new content. Park animation must stop under reduced motion and be cleaned up when switching scenes.

## Execution record

- School-only environment/actions removed; five settings and ten roles are available. Existing cafe and fixed-goal scenes kept.
- State/controller regression covers preserving actual accepted outfit choices across restore and rejecting old school records for shopping.
- Review fixes cover natural alternatives, sample grounding, choice order/negation, Unicode apostrophes, snack alternatives, declined fitting and ticket counts. Tiny subject-verb fallback fixes I/you/we/they + wants/needs/likes when the local model misses it.
- Full TypeScript suite: 408 tests in 65 files; typecheck/format passed. Eleven Godot scripts passed. Web/macOS exports succeeded; native startup exit0. Real local Qwen and synthetic Phonon/Whisper/Qwen checks passed. Final exported-build browser suite: five tests passed (2.9m), including all ten roles and the full casual park conversation. Live authenticated companion HTTP checks passed; service remains at http://127.0.0.1:4320.
- Final integration review fixes pinned RED→GREEN: snack alternative selection, Unicode apostrophes in negative choices, declined fitting-room option and actual ticket count. All 11 Godot scripts passed again, final exports succeeded and native startup exited0.
