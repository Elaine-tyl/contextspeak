# Website 3D Motion Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan inline.

**Goal:** Add a coherent, lightweight 3D motion layer to the existing learning website.

**Architecture:** Keep React, native controls and the green/blue/orange palette. A shared client shell owns pointer-driven CSS transforms and a persistent effects toggle. An original CSS 3D learning sculpture decorates primary page headers. No new rendering library or external assets.

**Scope:** Games, vocabulary cards, practice selection and progress cards gain depth; answering, recording and review controls remain steady. Existing scores, audio, routing and saved runs stay intact. Design choices are delegated in the ongoing project; this is the implementation direction chosen for this increment.

**Sources:** [MDN 3D transforms](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/transform-style), [Chrome motion guidance](https://developer.chrome.com/docs/modern-web-guidance/guides/ui-behaviors/motion), and [Codrops grid inspiration](https://tympanus.net/codrops/2014/03/27/3d-grid-effect/). Implement original code rather than copying a demo.

## Constraints and review focus

- Respect live OS reduced-motion changes, coarse pointers and keyboard navigation.
- No perpetual animation without a visible stop control; no effects over typing or recording.
- Clean up animation frames, active transforms and event listeners on navigation/unmount.
- Preserve existing native click targets; decorative layers ignore pointer events.
- Check 390px layout, contrast, hydration and fixture-based grading regressions.

## Tasks

- [x] Add a failing browser regression for card tilt/reset, persistent toggle, reduced motion and usable navigation.
- [x] Implement `SiteMotion.tsx`, `LearningSculpture.tsx` and `motion.css`; integrate the root layout and selected surfaces.
- [x] Verify desktop/mobile visuals and learning flows, run source tests, typecheck, formatting and production build.

## Verification

589 source tests passed. Four browser fixture flows cover motion preferences and OS changes, dashboard/library navigation, letter-box answers, and audio/scoring/resume. The pointer flow also passed three consecutive runs after accounting for Playwright's scroll-to-target and CSS transition frames. Mobile 390px checks pass; the decorative scene was centered and scaled to remove a measured 9px overflow. Typecheck, formatting and production build pass. Screenshots use fixture data and do not overwrite real learner records. The computer-use browser connection was unavailable, so rendered browser verification used Playwright.

Read-only code review found and verified a fix for speaker buttons inside vocabulary cards: speaker targets, focused speakers and expanded details now stop the containing card's tilt. This was reproduced with a failing browser test before the fix, and the final suite passes.
