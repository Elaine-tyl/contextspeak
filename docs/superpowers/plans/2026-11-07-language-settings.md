# Language settings implementation plan

**Goal:** Implement the approved reference-inspired extension popup.
**Architecture:** Keep existing messaging and persisted preferences. Add a focused React searchable language picker and popup stylesheet.
**Tech stack:** React, TypeScript, WXT, CSS, Radix icons.
**Spec:** docs/superpowers/specs/2026-11-07-language-settings-design.md

- [x] Add a failing browser regression for search, empty results, keyboard selection, Escape, and saving/reopening preferences.
- [x] Implement accessible source/target pickers, charcoal surfaces, teal toggles, and consistent surrounding settings.
- [x] Adapt existing language-selector assertions to the new accessible controls; preserve backend behavior.
- [x] Run browser tests, type checking, formatting and the real translation flow. Inspect the rendered popup and save visual QA.

Constraints: English and Chinese only; source supports automatic detection; explicit Save remains; no changes to website or selection panel. Keyboard focus, disabled loading state, and account switches must remain safe. User approved inline implementation; no additional planning approval required.
