# Ten gradual game levels and persistent branding

User requests a visible software name on every page and easier games split into ten levels. Continue under the user's earlier instruction to proceed with design choices.

- Add a shared ContextSpeak link in the root web layout, visible on all routes including loading/error states; use a small sticky header with room for the existing fixed library sidebar.
- Keep immutable v1 challenge content/IDs, moving the old three levels to 8–10. Add original v2 content for levels 1–7: words, short sentences, simple exchanges, everyday plans, explanations and short narratives. Each skill has at least three distinct challenges per level.
- Preserve access earned by legacy completion records; keep the three-distinct-passes rule, independent skill tracks and manual level choice. Extend game recommendations to ten while retaining three-level vocabulary practice.
- Verify failing regressions first, then unit/API/browser tests, typecheck, formatting and production builds. No dependencies, schema migration or RAG.

Verification: new progression, easier-content and recommendation regressions failed before implementation. Full unit/API suite: 191 passed. Browser suite: 49 passed, including brand visibility on six routes, ten level buttons and real API game progression. Beginner short-answer rubric and repeated qualifying-history regressions passed. Production web and extension builds, typecheck, formatting and diff checks passed. Final content-quality assertions also passed in a 15-test focused rerun.
