# Proactive NPC V7.1

The user supplied an implementation specification and selected high frequency + Guided mode. Execute inline, preserving authoring scenes and current V7 services/data.

## Design
Use pure C# contact, cooldown, contextual selection and event-chain policies; Unity adapters attach to existing actors at scene load and an Undo-enabled editor command can persist that setup. The existing proximity-generated bump director yields to the new director. Ambient lines are nonmodal; invitations expire/decline without penalties. Accepting starts voice-first practice through the existing V7 assessment/repetition/recall policy. Only verified, assessed spoken success advances deterministic event steps. No AI world commands, inventory grants or currency.

Proactive memory/settings live in an additive atomic sidecar. Stable NPC identities, relationship summaries, chain steps, pending correction and cooldown dates survive reload. Scene aliases cover the requested existing locations; missing actors/dependencies are reported rather than fabricated. NPC motion ownership is acquired/released with previous state restored, including on cancellation and scene changes. Safe small props use colliders and low-mass limited physics; no character impulses.

High frequency means a 25-second global unsolicited-offer cooldown, not constant interruptions. Per-NPC (90 s), per-event (180 s), offer timeout, one active event, scene density and availability remain enforced. Off/low/normal/high are player settings. Guided is the default for this new interaction system, independently of previous saved V7 mode.

## Tasks
- [x] RED/GREEN behavioral checks: contacts vs proximity, speed/direction, cooldowns/reload, busy/exam/swim/vehicles/transactions, decline/ignore, semantic follow-ups, gated repetition/recall, event-chain idempotency, scene transfer, bounded memory.
- [x] Core Proactive records, EventCooldownManager, ContextualEventSelector, NPCMemoryService, NPCRelationshipManager, EventQuestAdapter; original authored event content.
- [x] Runtime ProactiveNPCController, NPCInteractionDetector, NPCPlayerCollisionInteraction, NPCReactionAnimator, NPCConversationInitiator, ProactiveInteractionUI and persistent director; reuse speech + learning.
- [x] Undo-enabled Setup Proactive Interaction System; ScriptableObject events/profiles; test scene and native integration checks; animation/dependency report.
- [x] Verify compilation, behavior, old suites, browser export/UI where resources permit. Document actual tested/mocked/incomplete results.

## Review focus
No proximity-only collision; no credit from unassessed/text replies; wrong repeats cannot advance; one accepted event at a time; prompt UI never locks movement before acceptance; restore navigation/props/voice on timeout/scene unload; do not collide with old directors; cooldown bypass on reload; do not claim acoustic assessment or unexecuted native recording tests.

## Execution ledger
- Built 50 behavioral checks with RED/GREEN cycles for contact/evidence gates, resume cooldown, named/unknown venue mapping and scene density. Test logs under `.local/v71/`.
- Read-only review identified and prompted fixes for cursor ownership, FreeLife event-goal gating, named city IDs, legacy navigation ownership and gesture cleanup order. A final review caught unknown IDs defaulting to café; pinned by a regression.
- First export compiled/native checks passed but failed while writing a generated cache on full disk. Removed retired generated browser versions, retained current/recent working exports and source. Shared identical snapshot cache bytes with independent APFS clones, not hardlinks.
- Subsequent player build succeeded but isolated publication lacked companion JS inputs. Those were supplied for the final rebuild; no incomplete export was published.
- Ruling: reuse V7 policies and local speech with an additive event sidecar instead of changing legacy quest/save schemas. Only event evidence is merged into the existing dashboard. No new acoustic score or physical currency/inventory reward.
- Final release `20261114.114441` published after successful native checks and Web build; all 14 saved authoring-scene hashes unchanged. Disposable build copy removed; source, receipt and logs retained.
- Fixed browser-discovered overlay input/stacking with sorting order 100, empty-root picking Ignore and N shortcut. Native regression passes. Final browser checks confirm High / GuidedLearning settings and working Close in City; full human recording/encounter/navigation playthrough remains unverified.
- Final web suite: 621 tests / 85 files passed; proactive 50, V7 36, Slice 335, Living World 60, recording bridge 8; TypeScript passed. Existing unrelated generated ProjectSettings whitespace and VillaHome migration check are not treated as new feature failures. No commit/push requested.
