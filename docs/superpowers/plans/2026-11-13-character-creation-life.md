# Character creation and first life loop implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, with test-driven-development and verification-before-completion. User explicitly requests immediate implementation and has delegated routine choices.

**Goal:** A usable first-run creator with the supplied protagonists, saved appearance, wardrobe editing and a real classroom conversation.
**Architecture:** Separate profile/persistence, appearance application, creation UI/preview, scene builder and existing world integration. Existing actor roots and game saves remain intact.
**Spec:** ../specs/2026-11-13-character-creation-life-design.md
**Tech:** Pinned Unity 6000.3.28f1, C#, UI Toolkit, Input System, Addressables, existing browser bridge.

## Global constraints
No new providers or credentials. Keep code readable. No placeholder avatar when the supplied protagonists exist. Original FBXs untouched. No fake pronunciation scoring, facial animation, unimplemented controls or claims that all 15 districts are finished.

## Review focus
- Existing quest saves and difficulty survive profile editing.
- Invalid/corrupt/unsupported profile cannot crash startup or silently overwrite newer data.
- Closing creation without confirming restores the original profile and appearance.
- Preview rotation cannot move the player or lock the mouse.
- Every new world interaction is reachable and actually changes the intended UI/task.

## Tasks
- [x] Profile: add RED tests for defaults, bounded dimensions/Band/name, JSON round trip, legacy preference migration and corrupt data. Implement CharacterProfile/CharacterProfiles and connect PlayerAppearance without losing existing selection.
- [x] Creator: add RED first-run/scene checks. Build an Addressable creation scene with real male/female previews, native and accessible browser controls, rotate/reset, save-and-enter, cancel editing and storage-error feedback. Bootstrap gates first run; start difficulty only seeds new quest saves.
- [x] World loop: add RED wardrobe/fitting/classroom checks. Add physical wardrobe/fitting entrances, persist before editing and return to the same world position; add a reachable teacher, timetable and Band 3–9 original classroom dialogue through the existing speech/retry system.
- [x] Verification: update old browser tests to create a profile as a real first-time user, add dedicated creator/cancel/reload/classroom cases, run project/native/browser regressions, build Web and Mac, inspect both. Document implemented versus pending features; no automatic commit requested.

## Verification ledger
- RED observed: named profile absent; browser creation form absent; wardrobe interaction absent.
- GREEN: profile normalization/migration/round-trip/corrupt and future-version guards; both actual Meshy preview skins and bounded dimensions; native creation form and existing UI checks; all new walking routes and teacher actor verified.
- Existing 609 project tests passed after browser bridge integration. Shared speech/correction code is reused; no new pronunciation score is invented.
- Asset constraint: both supplied protagonists are merged meshes with authored textures, so unsupported facial/hair/clothing controls are deliberately absent. Feature matrix records these and all later world systems as pending.
- Review caught a scene-lifecycle request-loss race. RED wait/gate check reproduced the missing behavior; navigation now waits for unloading to finish and claims the loading gate before starting its nested operation.
- Target selection now preserves the learner's correction mode instead of silently switching Guided to optional Practice. Native runtime regression observed RED and now passes. New task saves retain Guided independently of the selected Band.
- Actual desktop screenshot caught the preview behind the form (camera +Z reverses horizontal screen projection). Viewport-position regression observed RED; builder and responsive runtime now place it at negative X on the visible right side. Mobile keeps the model centered above the form.
- Actual browser computed-style regression observed both gender buttons using identical green backgrounds; inactive buttons are now white with green text and the active choice is filled green.
- Independent follow-up review of all fixes found no important remaining issues. Full native suite and 609 project tests passed. Final Web/Mac builds, 26 browser flows and four packaged creator smoke tests passed.
- Full browser run exposed a startup-handshake cancellation race and a late old-session error invalidating a new connection. Deterministic held-request RED tests reproduced both; separate session cancellation/generation and token invalidation guards now pass.
- Additive wardrobe scene overlap could hide the browser creator with a late outgoing snapshot. A deterministic stale-snapshot RED test reproduced it; the browser bridge now retains the creator until Unity confirms save/cancel navigation. The real wardrobe path also passes.
- Final browser suite: 26/26 passed, including fake-device microphone capture, three friend follow-ups, classroom correction gating, profile persistence, cancel/save, fitting-room and mobile layout. Final typecheck passed.
- Actual Mac creator was visually confirmed after the user brought the app forward. Both protagonist controls initially inherited identical green native button backgrounds; native computed-color RED test reproduced it and now passes with separate selected styles. Final packaging includes this last style fix.

- Final delivery: Web and macOS builds completed successfully with the final native selection styles; packaged creator smoke 4/4 passed. Desktop first-run creator was confirmed on the actual Mac window, and browser screenshots were saved under docs/images. The larger requested life-world backlog remains explicitly pending in docs/CHARACTER_CREATION.md. No commit/push was requested for this update.
