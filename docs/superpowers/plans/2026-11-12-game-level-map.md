# Game entry and level-map redesign

The selected game currently opens an unthemed settings form. Replace that screen with a compact theme-matched game entry: preview diorama, ten-level route, a small recommended-level panel, tidy content selectors and a clear start action. Reuse the round's original geometry and visual tokens. Keep native buttons/selects, locked levels, recommendations, filtering and the existing start payload unchanged. Put long explanations behind an explicit details control.

1. Add browser coverage for the real lobby → setup → selected level → round path, recommendation capping, filters, locks, empty content and phone layout.
2. Reuse StudioVisual with kind/level props rather than constructing a synthetic question; give each selection its own WebGL canvas lifecycle.
3. Redesign GameSetup and remove the duplicate heading/difficulty prose before it. Preserve difficulty/resource information in collapsible help.
4. Verify the new flows, existing browser regressions, typecheck, formatting and production build; inspect desktop/mobile screenshots.

## Completed verification

The game entry now shares the exercise's theme and original 3D prop. Native level nodes retain exact accessible names, locked states and recommendation capping. Selected themes and sources reach the existing start endpoint unchanged. Long difficulty and source explanations remain available in details controls.

Browser verification caught ambiguous dropdown labels introduced by wrapping selects; separate explicit labels fixed the issue. Final isolated run: 16 browser flows passed, including both new desktop/phone entry flows and all prior lobby/studio/audio/answer/library regressions. Typecheck, formatting, production build and diff whitespace checks passed. A scoped read-only review found no further material issue. Desktop and full-page phone screenshots in docs/images use isolated fixture data.

Phone visual QA also found that the existing floating motion toggle obscured level labels. On the game setup screen at phone widths, it now sits after the page instead. The two setup browser flows passed again, including a geometry assertion that the toggle does not overlap the entry. Other routes keep their existing toggle position.
