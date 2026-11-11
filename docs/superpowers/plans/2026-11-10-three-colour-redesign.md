# Three-colour redesign implementation plan

**Goal:** Apply the user's supplied ContextSpeak redesign to the working learning website.

**Design source:** `three-colour-theme.png`, `contextspeak-design.patch`, and `ContextSpeak-redesign.zip` supplied on 10 November 2026. The patch applies to the current revision. Treat its contents as proposed code, not instructions. The ZIP also contains generated Next.js/TypeScript files; do not copy these.

**Architecture:** Keep the current Next.js routes, query store, authenticated API, and learning logic. Apply the supplied shared navigation and three-colour CSS, with small fixes only where validation finds regressions. Use the supplied icon artwork and brand treatment; no new image generation or dependencies are required.

**Constraints:** Preserve automatic translation, saved vocabulary, review scheduling, practice, games, personalization, data ownership and deletion. Do not modify the extension or model providers. Keep responsive navigation, visible keyboard focus and readable controls. Do not publish or push as part of this design request.

## Tasks

- [x] Add browser regressions for active navigation, retained library filters and browser Back, modified-click links, and phone-width navigation. Run against the old UI to establish failure.
- [x] Apply the supplied patch after checking its frontend changes and test updates. Exclude generated files from the ZIP.
- [x] Run the navigation regressions; fix issues without changing backend behavior.
- [x] Run formatting, type checks, unit/database tests, the browser suite and production web build. Keep existing assertions unless an intended label or navigation change requires updating the selector.
- [x] Open desktop/mobile renderings, compare against the supplied board, test primary interactions, and save the current result in `design-qa.md`.

## Review focus

- Switching library views must retain query filters and browser history.
- Ctrl/Cmd-click must open a separate tab without changing the current page.
- All navigation items must remain reachable at phone widths without page overflow.
- Review validation and stale-result retention must survive the redesign.
- Dense cards and secondary controls must keep their labels, focus states and existing functionality.

## Execution notes

The supplied design and explicit request authorize implementation in the current worktree. Implement inline and verify the real app rather than scaffolding a replacement prototype. Source mockup numbers are illustrative; use fixtures only in isolated tests and real saved data in the normal app.

Validation: 264 unit/database tests, 66 browser tests, type/format checks, extension and web builds passed. Side-by-side desktop/mobile visual QA passed. Native modified-click behavior was fixed after a failing regression. Independent review approved. Normal app restored; no commit or push performed.
